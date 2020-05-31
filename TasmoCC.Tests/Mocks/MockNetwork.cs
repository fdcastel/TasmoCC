using Bogus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using TasmoCC.Emulator.Devices;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Tasmota.Models;

namespace TasmoCC.Tests.Mocks
{
    public class MockNetwork
    {
        public static readonly byte FirstIpSegment = 30;

        public IList<DeviceEmulator> Devices { get; private set; }

        public IDictionary<IPAddress, DeviceEmulator> DevicesByIp { get; private set; }
        public IDictionary<string, DeviceEmulator> DevicesByMac { get; private set; }

        public MockMqttServer MqttServer { get; private set; }

        private readonly IPAddress _subnet;
        private byte _nextIpSegment = FirstIpSegment;

        public MockNetwork(IPAddress subnet, int deviceCount, MqttConfiguration configuration)
        {
            _subnet = subnet;

            MqttServer = new MockMqttServer(configuration);

            Devices = CreateDevices(deviceCount);
            DevicesByIp = Devices.ToDictionary(x => x.IpAddress);
            DevicesByMac = Devices.ToDictionary(x => x.MacAddress);
        }

        private IList<DeviceEmulator> CreateDevices(int deviceCount)
        {
            Randomizer.Seed = new Random(1138);

            var templates = new List<string>()
            {
                "{\"NAME\":\"Sonoff Mini\",\"GPIO\":[17,0,0,0,9,0,0,0,21,56,0,0,255],\"FLAG\":0,\"BASE\":1}",
                "{\"NAME\":\"Sonoff Basic\",\"GPIO\":[17,255,255,255,255,0,0,0,21,56,255,0,0],\"FLAG\":0,\"BASE\":1}",
                "{\"NAME\":\"Sonoff BasicR3\",\"GPIO\":[17,255,0,255,255,0,0,0,21,56,255,0,255],\"FLAG\":0,\"BASE\":1}",
                "{\"NAME\":\"Sonoff 4CHPro2\",\"GPIO\":[17,255,255,255,23,22,18,19,21,56,20,24,0],\"FLAG\":0,\"BASE\":23}",
                "{\"NAME\":\"Sonoff Dual R2\",\"GPIO\":[255,255,0,255,0,22,255,17,21,56,0,0,0],\"FLAG\":0,\"BASE\":39}",
            };

            var powerStates = new List<string>()
            {
                "OFF",
                "ON",
            };

            var deviceStatusGenerator = new Faker<DeviceStatus>()
                .StrictMode(true)
                .RuleFor(s => s.Topic, (f) => f.Commerce.Department(1))
                .RuleFor(s => s.FriendlyName, (_, s) => new[] { s.Topic });

            var parametersStatusGenerator = new Faker<ParametersStatus>()
                .StrictMode(true)
                .RuleFor(s => s.RestartReason, () => "Software/System restart");

            var firmwareStatusGenerator = new Faker<FirmwareStatus>()
                .StrictMode(true)
                .RuleFor(s => s.Hardware, () => "ESP8285")
                .RuleFor(s => s.Version, () => "8.2.0");

            var logStatusGenerator = new Faker<LogStatus>()
                .StrictMode(true)
                .RuleFor(s => s.TelePeriod, () => 300);

            var memoryStatusGenerator = new Faker<MemoryStatus>()
                .StrictMode(true)
                .RuleFor(s => s.ProgramSize, () => 577)
                .RuleFor(s => s.FlashSize, () => 1024);

            var networkStatusGenerator = new Faker<NetworkStatus>()
                .StrictMode(true)
                .RuleFor(s => s.HostName, (f) => f.Internet.DomainWord())
                .RuleFor(s => s.IpAddress, () => GetNextIpAddress().ToString())
                .RuleFor(s => s.Gateway, () => GetNextIpAddress(1).ToString())
                .RuleFor(s => s.SubnetMask, () => "255.255.255.0")
                .RuleFor(s => s.DnsServer, () => GetNextIpAddress(2).ToString())
                .RuleFor(s => s.Mac, (f) => f.Internet.Mac());

            var telemetryStatusGenerator = new Faker<TelemetryStatus>()
                .StrictMode(true)
                .RuleFor(s => s.UptimeSec, (f) => f.Random.Int(100, 1000))
                .RuleFor(s => s.Heap, (f) => f.Random.Int(18, 27))
                .RuleFor(s => s.LoadAvg, (f) => f.Random.Int(19, 50))
                .RuleFor(s => s.MqttCount, (f) => f.Random.Int(1, 10))
                .RuleFor(s => s.Power, (f) => f.Random.ListItem(powerStates))
                .RuleFor(s => s.Power1, () => null)
                .RuleFor(s => s.Power2, () => null)
                .RuleFor(s => s.Power3, () => null)
                .RuleFor(s => s.Power4, () => null)
                .RuleFor(s => s.WiFi, (f) => new TelemetryStatus.WifiStatus
                {
                    Rssi = 1,
                    Signal = 2,
                    LinkCount = f.Random.Int(1, 10),
                    Downtime = $"0T00:00:0{f.Random.Int(1, 9)}"
                });

            var tasmotaStatusGenerator = new Faker<TasmotaStatus>()
                .StrictMode(true)
                .RuleFor(s => s.Status, () => deviceStatusGenerator.Generate(1).First())
                .RuleFor(s => s.StatusPrm, () => parametersStatusGenerator.Generate(1).First())
                .RuleFor(s => s.StatusFwr, () => firmwareStatusGenerator.Generate(1).First())
                .RuleFor(s => s.StatusLog, () => logStatusGenerator.Generate(1).First())
                .RuleFor(s => s.StatusMem, () => memoryStatusGenerator.Generate(1).First())
                .RuleFor(s => s.StatusNet, () => networkStatusGenerator.Generate(1).First())
                .RuleFor(s => s.StatusSts, () => telemetryStatusGenerator.Generate(1).First())
                .RuleFor(s => s.Template, (f) => f.Random.ListItem(templates))
                .Generate(deviceCount);

            DeviceEmulator.UseQuickRestart = true;
            var devices = tasmotaStatusGenerator
                .Select(s => new DeviceEmulator(s, new MockMqttClient(MqttServer)))
                .ToList();

            // Update devices with 2 and 4 channels.
            var faker = new Faker();
            foreach (var d in devices)
            {
                switch (d.TemplateName)
                {
                    case "Sonoff Dual R2":
                        d.Status.Status.FriendlyName = new[] { faker.Commerce.Department(1), faker.Commerce.Department(1) };
                        d.Status.StatusSts.Power = null;
                        d.Status.StatusSts.Power1 = faker.Random.ListItem(powerStates);
                        d.Status.StatusSts.Power2 = faker.Random.ListItem(powerStates);
                        break;

                    case "Sonoff 4CHPro2":
                        d.Status.Status.FriendlyName = new[] { faker.Commerce.Department(1), faker.Commerce.Department(1), faker.Commerce.Department(1), faker.Commerce.Department(1) };
                        d.Status.StatusSts.Power = null;
                        d.Status.StatusSts.Power1 = faker.Random.ListItem(powerStates);
                        d.Status.StatusSts.Power2 = faker.Random.ListItem(powerStates);
                        d.Status.StatusSts.Power3 = faker.Random.ListItem(powerStates);
                        d.Status.StatusSts.Power4 = faker.Random.ListItem(powerStates);
                        break;
                }
            }

            return devices;
        }

        private IPAddress GetNextIpAddress(byte? segment = null)
        {
            var b = _subnet.GetAddressBytes();
            b[3] = segment ?? _nextIpSegment++;
            return new IPAddress(b);
        }
    }
}
