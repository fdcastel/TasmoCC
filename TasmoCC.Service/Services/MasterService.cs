using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.MongoDb.Models;
using TasmoCC.MongoDb.Repositories;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Tasmota.Models;
using TasmoCC.Tasmota.Services;

namespace TasmoCC.Service.Services
{
    public sealed class MasterService
    {
        private readonly MqttConfiguration _mqttOptions;
        private readonly ILogger<MasterService> _logger;
        private readonly TasmotaService _tasmotaService;
        private readonly DeviceRepository _deviceRepository;
        private readonly DeviceConfigurationRepository _deviceConfigurationRepository;

        public bool ScanNetworkOnStart { get; set; } = true;         // Set to false on tests

        public MasterService(IOptions<MqttConfiguration> mqttOptions, ILogger<MasterService> logger, TasmotaService tasmotaService, DeviceRepository deviceRepository, DeviceConfigurationRepository deviceConfigurationRepository)
        {
            _mqttOptions = mqttOptions.Value;
            _logger = logger;
            _tasmotaService = tasmotaService;
            _deviceRepository = deviceRepository;
            _deviceConfigurationRepository = deviceConfigurationRepository;
        }

        //
        // Commands
        //

        public Task ScanNetwork(CancellationToken cancellationToken = default) =>
            Task.Run(async () =>
            {
                var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                var startedAt = DateTime.Now;
                await _tasmotaService.ScanNetworkAsync(async (s) => await DeviceDiscoveredAsync(s), 1, 254, cts.Token);

                // Set to offline all devices not updated since the beginning of network scan
                var unresponsiveDevices = await _deviceRepository.SetDevicesOfflineAsync(startedAt);
                if (unresponsiveDevices > 0)
                {
                    _logger.LogWarning("{unresponsiveDevices} known devices did not answered the scan. Marking as offline.", unresponsiveDevices);
                }

            }, cancellationToken);

        public Task SetDeviceOfflineAsync(IPAddress ipAddress) =>
            _deviceRepository.SetDeviceOfflineAsync(ipAddress);

        public async Task ScanDeviceAsync(IPAddress ipAddress)
        {
            var newStatus = await _tasmotaService.GetStatusAsync(ipAddress);
            if (newStatus == null)
            {
                _logger.LogWarning("Failed to fetch updated status for device at '{ipAddress}'", ipAddress);
                return;
            }

            await DeviceDiscoveredAsync(newStatus);
        }

        public async Task AdoptAsync(string id)
        {
            _logger.LogInformation("Adopting device '{id}'", id);
            var device = await FindDeviceAsync(id);

            // state = 'Adopting'; adoptedAt = null; configureMqtt()
            await _deviceRepository.UpdateDeviceStateAsync(device._id, DeviceState.Adopting, "adoptedAt");
            await _tasmotaService.ConfigureMqttAsync(device.Ipv4Address, device.TopicName, _mqttOptions.Host, _mqttOptions.Port, _mqttOptions.Username, _mqttOptions.Password);

            _tasmotaService.WaitForDeviceResponding(device.Ipv4Address, TimeSpan.FromSeconds(5), async (isOnline, attempts) =>
            {
                if (isOnline)
                {
                    _logger.LogInformation("Device '{ipAddress}' is online after {attempts} attempts. Testing its Mqtt configuration...", device.Ipv4Address, attempts);

                    _deviceRepository.WaitForDeviceAdopted(device._id, async () =>
                    {
                        await _tasmotaService.TestMqttConfigurationAsync(device.Ipv4Address, device.TopicName, device._id);
                    },
                    async (isAdopted, attempts) =>
                    {
                        if (isAdopted)
                        {
                            _logger.LogInformation("Device '{ipAddress}' is adopted.", device.Ipv4Address, attempts);
                        }
                        else
                        {
                            _logger.LogWarning("Device '{ipAddress}' is not adopted after {attempts} attempts. Adoption aborted.", device.Ipv4Address, attempts);
                            await _deviceRepository.UpdateDeviceStateAsync(id, DeviceState.AdoptionPending);
                        }
                    });
                }
                else
                {
                    _logger.LogWarning("Device '{ipAddress}' is not responding after {attempts} attempts. Adoption aborted.", device.Ipv4Address, attempts);
                    await _deviceRepository.UpdateDeviceStateAsync(id, DeviceState.AdoptionPending);
                }
            });
        }

        public async Task ForgetAsync(string id)
        {
            _logger.LogInformation("Forgetting device '{id}'", id);
            var device = await FindDeviceAsync(id);

            await _deviceRepository.DeleteDeviceAsync(device._id);
            await _deviceConfigurationRepository.DeleteDeviceConfigurationAsync(device._id);
            await _tasmotaService.ClearMqttAsync(device.Ipv4Address);
        }

        public async Task ProvisionAsync(string id)
        {
            var device = _deviceRepository.GetDeviceAggregate(id, true);
            if (device == null)
            {
                _logger.LogWarning("Discarding provision request for unknown device '{id}'", id);
                return;
            }

            _logger.LogInformation("Provisioning device '{id}'", id);

            // state = 'Provisioning'; provisionedAt = null; configureDevice()
            var fieldsToUpdate = new Device()
            {
                _id = id,
                State = DeviceState.Provisioning,
            };
            if (device.Configuration != null && device.Configuration.TopicName != device.TopicName)
            {
                // Changing topic name: Must update the device in db BEFORE changing the device. 
                //   Otherwise monitor will discard telemetry message due unknown topic name.
                fieldsToUpdate.TopicName = device.Configuration.TopicName;
            }
            await _deviceRepository.UpdateDeviceAsync(fieldsToUpdate, fieldsToUnset: "provisionedAt");

            string allCommands = await GetProvisionCommandsAsync(device);
            _logger.LogInformation("Sending commands '{allCommands}'", allCommands);
            await _tasmotaService.ConfigureDeviceAsync(device.Ipv4Address, allCommands);
        }

        public async Task ResetConfigurationAsync(string id, bool keepWiFi)
        {
            _logger.LogInformation("Restoring firmware defaults for device '{id}' (keepWiFi={keepWiFi})", id, keepWiFi);
            var device = await FindDeviceAsync(id);

            await _deviceRepository.UpdateDeviceStateAsync(id, DeviceState.Restarting);
            await _tasmotaService.ResetConfigurationAsync(device.Ipv4Address, keepWiFi);

            _tasmotaService.WaitForDeviceResponding(device.Ipv4Address, TimeSpan.FromSeconds(5), async (isOnline, attempts) =>
            {
                await _deviceRepository.UpdateDeviceStateAsync(id, null, "adoptedAt", "provisionedAt");
                if (isOnline)
                {
                    _logger.LogInformation("Device '{ipAddress}' is online after {attempts} attempts. Fetching updated device information...", device.Ipv4Address, attempts);
                    await ScanDeviceAsync(device.Ipv4Address);
                }
                else
                {
                    _logger.LogWarning("Device '{ipAddress}' is not responding after {attempts} attempts.", device.Ipv4Address, attempts);
                }
            });
        }

        public async Task RestartAsync(string id)
        {
            _logger.LogInformation("Restarting device '{id}'", id);
            var device = await FindDeviceAsync(id);

            // state = 'Restarting'; RestartDevice()
            await _deviceRepository.UpdateDeviceStateAsync(id, DeviceState.Restarting);
            await _tasmotaService.RestartDeviceAsync(device.Ipv4Address);

            _tasmotaService.WaitForDeviceResponding(device.Ipv4Address, TimeSpan.FromSeconds(5), async (isOnline, attempts) =>
            {
                await _deviceRepository.UpdateDeviceStateAsync(id, null);
                if (isOnline)
                {
                    _logger.LogInformation("Device '{ipAddress}' is online after {attempts} attempts.", device.Ipv4Address, attempts);
                }
                else
                {
                    _logger.LogWarning("Device '{ipAddress}' is not responding after {attempts} attempts.", device.Ipv4Address, attempts);
                }
            });
        }

        public async Task UpgradeAsync(string id)
        {
            _logger.LogInformation("Upgrading device '{id}'", id);
            var device = await FindDeviceAsync(id);

            // state = 'Upgrading'; UpgradeFirmware()
            await _deviceRepository.UpdateDeviceStateAsync(id, DeviceState.Upgrading);
            await _tasmotaService.UpgradeFirmwareAsync(device.Ipv4Address);

            // Benchmark: A Sonoff Mini took around 2 minutes to upgrade from v8.1.0 to v8.3.1.
            //   ToDo: Improve this?
            _tasmotaService.WaitForDeviceResponding(device.Ipv4Address, TimeSpan.FromMinutes(2), async (isOnline, attempts) =>
            {
                await _deviceRepository.UpdateDeviceStateAsync(id, null);
                if (isOnline)
                {
                    _logger.LogInformation("Device '{ipAddress}' is online after {attempts} attempts.", device.Ipv4Address, attempts);
                    await ScanDeviceAsync(device.Ipv4Address);
                }
                else
                {
                    _logger.LogWarning("Device '{ipAddress}' is not responding after {attempts} attempts.", device.Ipv4Address, attempts);
                }
            });
        }

        public async Task SetConfigurationAsync(string id, DeviceConfiguration newConfiguration)
        {
            _logger.LogInformation("Setting new configuration on device '{id}'", id);

            newConfiguration._id = id;
            await _deviceConfigurationRepository.ReplaceDeviceConfigurationAsync(newConfiguration);
        }

        public async Task SetPowerAsync(string id, int index, string state)
        {
            _logger.LogInformation("Setting power{index}={state} on device '{id}'", index, state, id);
            var device = await FindDeviceAsync(id);

            await _tasmotaService.SetPowerAsync(device.Ipv4Address, index, state);
        }

        private async Task<string> GetProvisionCommandsAsync(DeviceAggregate device)
        {
            var commands = new List<string>
            {
                // Reset MQTT prefixes
                "Prefix1 1",
                "Prefix2 1",
                "Prefix3 1",

                // Reset state texts
                "StateText1 OFF",
                "StateText2 ON",
                "StateText3 TOGGLE",
                "StateText4 HOLD"
            };

            if (device.Configuration != null)
            {
                if (!string.IsNullOrWhiteSpace(device.Configuration.TopicName))
                {
                    commands.Add($"Topic {device.Configuration.TopicName}");
                    // Adds 2 seconds between Topic and remaining commands. Without this the Topic won't change! (Tasmota 8.2.0)
                    commands.Add("Delay 20");
                }

                if (device.Configuration.FriendlyNames != null)
                {
                    var i = 1;
                    foreach (var n in device.Configuration.FriendlyNames)
                    {
                        if (!String.IsNullOrWhiteSpace(n))
                        {
                            commands.Add($"FriendlyName{i} {n}");
                            i++;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(device.Configuration.SetupCommands))
                {
                    commands.AddRange(device.Configuration.SetupCommands.Split(';'));
                }
            }

            var globalConfiguration = await _deviceConfigurationRepository.GetDeviceConfigurationAsync("common");
            if (globalConfiguration != null && !string.IsNullOrWhiteSpace(globalConfiguration.SetupCommands))
            {
                commands.AddRange(globalConfiguration.SetupCommands.Split(';'));
            }

            if (device.Template != null)
            {
                commands.Add($"Template {device.Template.Definition}");
                commands.Add("Module 0");
            }

            var allCommands = string.Join(';', commands);
            return allCommands;
        }

        //
        // Database events
        //

        private async Task DeviceDiscoveredAsync(TasmotaStatus tasmotaDevice)
        {
            _logger.LogInformation("Received status for '{hostname}' ({ipAddress}). Updating database...", tasmotaDevice.StatusNet.HostName, tasmotaDevice.StatusNet.IpAddress);
            var device = new Device()
            {
                _id = tasmotaDevice.StatusNet.Mac,

                HostName = tasmotaDevice.StatusNet.HostName,
                Ipv4Address = IPAddress.Parse(tasmotaDevice.StatusNet.IpAddress),
                Ipv4SubnetPrefix = ConvertToPrefixLength(IPAddress.Parse(tasmotaDevice.StatusNet.SubnetMask)),
                Ipv4Gateway = IPAddress.Parse(tasmotaDevice.StatusNet.Gateway),
                Ipv4NameServer = IPAddress.Parse(tasmotaDevice.StatusNet.DnsServer),

                TopicName = tasmotaDevice.Status.Topic,
                FriendlyNames = tasmotaDevice.Status.FriendlyName,

                FirmwareVersion = tasmotaDevice.StatusFwr.Version.Replace("(tasmota)", ""),
                FirmwareSizeKb = tasmotaDevice.StatusMem.ProgramSize,
                FlashSizeKb = tasmotaDevice.StatusMem.FlashSize,
                Hardware = tasmotaDevice.StatusFwr.Hardware,

                RestartReason = tasmotaDevice.StatusPrm.RestartReason,
                TelemetrySeconds = tasmotaDevice.StatusLog.TelePeriod,

                TemplateDefinition = tasmotaDevice.Template,
                TemplateName = ExtractTemplateName(tasmotaDevice.Template),

                Status = tasmotaDevice.StatusSts.ToDeviceStatus(),
                Offline = false
            };
            var updatedDevice = await _deviceRepository.UpdateDeviceAsync(device, true);

            if (!updatedDevice.AdoptedAt.HasValue)
            {
                _logger.LogInformation("Device '{hostname}' ({ipAddress}) is not adopted. Testing its Mqtt configuration...", device.HostName, device.Ipv4Address);
                await _tasmotaService.TestMqttConfigurationAsync(device.Ipv4Address, device.TopicName, device._id);
            }
        }

        private static int ConvertToPrefixLength(IPAddress subnetMask)
        {
            var result = 0;

#pragma warning disable CS0618 // Type or member is obsolete
            var mask = subnetMask.Address;
#pragma warning restore CS0618 // Type or member is obsolete

            while (mask != 0)
            {
                result += (int)(mask & 1);
                mask >>= 1;
            }

            return result;
        }

        private static string ExtractTemplateName(string templateDefinition)
        {
            return JsonConvert.DeserializeObject<dynamic>(templateDefinition)
                ?.NAME ?? throw new ArgumentException("Template does not have a NAME element.");
        }

        private async Task<Device> FindDeviceAsync(string id) =>
            await _deviceRepository.GetDeviceAsync(id) ?? throw new Exception($"Unknown device '{id}'.");
    }
}
