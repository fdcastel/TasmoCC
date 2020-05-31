using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TasmoCC.MongoDb.Models;
using TasmoCC.MongoDb.Repositories;
using TasmoCC.Service.Services;
using TasmoCC.Tasmota.Services;

namespace TasmoCC.Service.Hubs
{
    public class DevicesHub : Hub<IDevicesHubClient>
    {
        private readonly ILogger<DevicesHub> _logger;

        private readonly DeviceRepository _deviceRepository;
        private readonly TemplateRepository _templateRepository;

        private readonly MasterService _masterService;

        public DevicesHub(ILogger<DevicesHub> logger, DeviceRepository deviceRepository, TemplateRepository templateRepository, MasterService masterService)
        {
            _logger = logger;
            _deviceRepository = deviceRepository;
            _templateRepository = templateRepository;
            _masterService = masterService;
        }

        public override async Task OnConnectedAsync()
        {
            var devices = _deviceRepository.GetDevicesAggregate();
            var templates = _templateRepository.GetTemplatesAggregate();

            var thisClient = Clients.Clients(Context.ConnectionId);
            thisClient.InitialPayloadReceived(devices, templates);

            await base.OnConnectedAsync();
        }

        public async Task ScanNetwork()
        {
            await _masterService.ScanNetwork();
            _ = Clients.All.NetworkScanFinished();
        }

        public async Task Adopt(string id)
        {
            await HandleUnresponsiveDevice(async () =>
                await _masterService.AdoptAsync(id)
            );
        }

        public async Task Forget(string id)
        {
            await _masterService.ForgetAsync(id);
        }

        public async Task Provision(string id)
        {
            await HandleUnresponsiveDevice(async () =>
                await _masterService.ProvisionAsync(id)
            );
        }

        public async Task ResetConfiguration(string id, bool keepWiFi)
        {
            await HandleUnresponsiveDevice(async () =>
                await _masterService.ResetConfigurationAsync(id, keepWiFi)
            );
        }

        public async Task Restart(string id)
        {
            await HandleUnresponsiveDevice(async () =>
                await _masterService.RestartAsync(id)
            );
        }

        public async Task Upgrade(string id)
        {
            await HandleUnresponsiveDevice(async () =>
                await _masterService.UpgradeAsync(id)
            );
        }

        public async Task SetConfiguration(string id, DeviceConfiguration newConfiguration)
        {
            await HandleUnresponsiveDevice(async () =>
                await _masterService.SetConfigurationAsync(id, newConfiguration)
            );
        }

        public async Task SetPower(string id, int index, string state)
        {
            await HandleUnresponsiveDevice(async () =>
                await _masterService.SetPowerAsync(id, index, state)
            );
        }

        private async Task HandleUnresponsiveDevice(Func<Task> body)
        {
            try
            {
                await body();
            }
            catch (DeviceUnresponsiveException e)
            {
                _logger.LogWarning("Device at '{ipAddress}' is unresponsive. Marking as offline.", e.IPAddress);
                await _masterService.SetDeviceOfflineAsync(e.IPAddress);
            }
        }
    }
}
