using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TasmoCC.Emulator.Devices;
using TasmoCC.Emulator.Hubs;
using TasmoCC.Mqtt.Services;
using TasmoCC.Tasmota.Models;

namespace TasmoCC.Emulator
{
    public static class IServiceCollectionExtensions
    {
        public static void AddEmulatorServices(this IServiceCollection services)
        {
            services.AddSingleton<DeviceEmulator>(sp => CreateDeviceEmulator(sp));
        }

        private static DeviceEmulator CreateDeviceEmulator(IServiceProvider sp)
        {
            // https://stackoverflow.com/questions/850650/reliable-method-to-get-machines-mac-address-in-c-sharp#comment107946437_7661829
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(nic => nic.OperationalStatus == OperationalStatus.Up &&
                                       nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            var ipProperties = nic.GetIPProperties();

            var ipAddress = ipProperties
                .UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address?.ToString();

            var gateway = ipProperties
                .GatewayAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address?.ToString();

            var subnetMask = ipProperties
                .UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.IPv4Mask?.ToString();

            var dnsServer = ipProperties
                .DnsAddresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?.ToString();

            var macSegments = nic.GetPhysicalAddress()
                .GetAddressBytes();

            var macSegmentsHex = macSegments
                .Select(b => b.ToString("x2"));

            var topicSuffixHex = macSegments
                .Skip(3)
                .Select(b => b.ToString("X2"));

            var topic = "tasmota_" + string.Join("", topicSuffixHex);
            var hostnameSuffixDec = (macSegments[4] << 8 | macSegments[5]) % 10000;

            var status = new TasmotaStatus()
            {
                Status = new DeviceStatus()
                {
                    FriendlyName = new[] { "Tasmota" },
                    Topic = topic
                },
                StatusPrm = new ParametersStatus
                {
                    RestartReason = "Powered on"
                },
                StatusFwr = new FirmwareStatus
                {
                    Version = "1.0.0",
                    Hardware = "Emulator"
                },
                StatusLog = new LogStatus
                {
                    TelePeriod = 300
                },
                StatusMem = new MemoryStatus
                {
                    ProgramSize = 999,
                    FlashSize = 999
                },
                StatusNet = new NetworkStatus
                {
                    HostName = $"{topic}-{hostnameSuffixDec}",
                    IpAddress = ipAddress ?? string.Empty,
                    Gateway = gateway ?? string.Empty,
                    SubnetMask = subnetMask ?? string.Empty,
                    DnsServer = dnsServer ?? string.Empty,
                    Mac = string.Join(":", macSegmentsHex)
                },
                StatusSts = new TelemetryStatus
                {
                    UptimeSec = 0,
                    Heap = 24,
                    LoadAvg = 0,
                    MqttCount = 0,
                    Power = "OFF",
                    WiFi = new TelemetryStatus.WifiStatus
                    {
                        Rssi = 100,
                        Signal = -30,
                        LinkCount = 0,
                        Downtime = "0T00:00:00",
                    }
                },
                Template = "{\"NAME\":\"Generic\",\"GPIO\":[255,255,255,255,255,255,255,255,255,255,255,255,255],\"FLAG\":15,\"BASE\":18}",
            };

            var hubContext = sp.GetService<IHubContext<DeviceHub, IDeviceHubClient>>();

            var client = sp.GetService<IMqttClient>();

            var result = new DeviceEmulator(status, client);
            result.DeviceChanged += (s, e) => hubContext.Clients.All.DeviceChanged(result);

            var logger = sp.GetService<ILogger<DeviceEmulator>>();
            logger.LogInformation("Device emulator created with initial status = " + JsonConvert.SerializeObject(status, Formatting.Indented));

            return result;
        }
    }
}
