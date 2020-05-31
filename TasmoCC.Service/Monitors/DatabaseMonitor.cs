using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.MongoDb.Models;
using TasmoCC.MongoDb.Repositories;
using TasmoCC.Service.Configuration;
using TasmoCC.Service.Hubs;
using TasmoCC.Service.Services;
using TasmoCC.Tasmota.Configuration;

namespace TasmoCC.Service.Monitors
{
    public sealed class DatabaseMonitor : IHostedService
    {
        private readonly IOptions<TasmotaConfiguration> _tasmotaOptions;
        private readonly ILogger<DatabaseMonitor> _logger;
        private readonly DeviceRepository _deviceRepository;
        private readonly DeviceConfigurationRepository _deviceConfigurationRepository;
        private readonly TemplateRepository _templateRepository;
        private IHubContext<DevicesHub, IDevicesHubClient> _hubContext;
        private MasterService _masterService;

        public DatabaseMonitor(IOptions<TasmotaConfiguration> tasmotaOptions, ILogger<DatabaseMonitor> logger, DeviceRepository deviceRepository, DeviceConfigurationRepository deviceConfigurationRepository, TemplateRepository templateRepository, IHubContext<DevicesHub, IDevicesHubClient> hubContext, MasterService masterService)
        {
            _tasmotaOptions = tasmotaOptions;
            _logger = logger;
            _deviceRepository = deviceRepository;
            _deviceConfigurationRepository = deviceConfigurationRepository;
            _templateRepository = templateRepository;
            _hubContext = hubContext;
            _masterService = masterService;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Initializing database...");
            await InitializeDatabaseAsync();

            _logger.LogInformation("Starting MongoDb monitor...");
            _deviceRepository.WhenDeviceChanges(cancellationToken)
                .Subscribe(async c => await DeviceChangedAsync(c), cancellationToken);

            _deviceConfigurationRepository.WhenDeviceConfigurationChanges(cancellationToken)
                .Subscribe(async c => await DeviceConfigurationChangedAsync(c), cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Monitors use CancellationTokens to stop. So nothing to do here.
            return Task.CompletedTask;
        }

        private async Task InitializeDatabaseAsync()
        {
            var configurationFilePath = _tasmotaOptions.Value.ConfigurationFile ?? ".\tasmocc.yaml";

            var configuration = new YamlConfiguration();
            if (File.Exists(configurationFilePath))
            {
                var parser = new YamlConfigurationParser();
                configuration = parser.ParseConfiguration(configurationFilePath);
            }

            await _templateRepository.InsertInitialTemplatesAsync(configuration.Templates?.Values);
            await _deviceConfigurationRepository.InsertInitialDeviceConfigurationsAsync(configuration.Devices?.Values);
        }

        private async Task DeviceChangedAsync(DocumentChange<Device> change)
        {
            var device = change.Document;
            var changeKind = change.ChangeKind;

            if (changeKind == DocumentChangeKind.Delete)
            {
                _logger.LogDebug("Device '{oldId}' deleted from db.", device._id);
                await _hubContext.Clients.All.DeviceChanged(device, changeKind);
                return;
            }

            var verb = GetChangeKindVerb(changeKind);
            _logger.LogDebug("Device '{hostname}' {verb} in db.", device.HostName, verb);
            var deviceAggregate = _deviceRepository.GetDeviceAggregate(device._id);
            await _hubContext.Clients.All.DeviceChanged(deviceAggregate, changeKind);

            if (device.State == DeviceState.ProvisionPending)
            {
                await _masterService.ProvisionAsync(device._id);
            }
        }

        private async Task DeviceConfigurationChangedAsync(DocumentChange<DeviceConfiguration> change)
        {
            var deviceConfiguration = change.Document;
            var changeKind = change.ChangeKind;

            var verb = GetChangeKindVerb(changeKind);
            var device = await _deviceRepository.GetDeviceAsync(deviceConfiguration._id);
            if (device == null)
            {
                _logger.LogDebug("Device configuration '{oldId}' {verb} in db.", deviceConfiguration._id, verb);
                return;
            }

            _logger.LogDebug("Device configuration '{id}' (for '{hostname}') changed in db.", deviceConfiguration._id, device.HostName);
            if (device.AdoptedAt.HasValue)
            {
                // Device is adopted: Provision with new configuration
                await _masterService.ProvisionAsync(device._id);
            }
            else
            {
                // Device is not adopted: Just notify UI
                var uiChange = new DocumentChange<Device>(device, DocumentChangeKind.Update);
                await DeviceChangedAsync(uiChange);
            }
        }

        private static string GetChangeKindVerb(DocumentChangeKind changeKind) =>
            changeKind switch
            {
                DocumentChangeKind.Insert => "inserted",
                DocumentChangeKind.Update => "updated",
                DocumentChangeKind.Delete => "deleted",
                DocumentChangeKind.Replace => "replaced",
                _ => throw new InvalidEnumArgumentException($"[BUGCHECK] Unexpected DocumentChangeKind ({changeKind})."),
            };
    }
}
