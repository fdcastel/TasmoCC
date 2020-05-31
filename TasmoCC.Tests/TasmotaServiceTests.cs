using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.Emulator.Devices;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Tasmota.Configuration;
using TasmoCC.Tasmota.Services;
using TasmoCC.Tests.Extensions;
using TasmoCC.Tests.Mocks;

namespace TasmoCC.Tests
{
    [TestClass]
    public class TasmotaServiceTests
    {
        private static readonly string CallbackTopic = "CallbackTopic";

        public MqttConfiguration MqttConfiguration { get; }

        private readonly MockNetwork Network;
        private readonly TasmotaService Service;

        public TasmotaServiceTests()
        {
            MqttConfiguration = new MqttConfiguration()
            {
                Host = "192.168.66.200",
                Port = 64220,
                Username = "vader",
                Password = "jedi"
            };

            var configuration = new TasmotaConfiguration()
            {
                Subnet = IPAddress.Parse("192.168.66.0"),
            };

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            Network = new MockNetwork(configuration.Subnet, 30, MqttConfiguration);
            Service = new TasmotaService(Options.Create(configuration), loggerFactory.CreateLogger<TasmotaService>(), new MockTasmotaClient(Network));
        }

        [TestMethod]
        public async Task ScanNetwork_CanFindNetworkDevices()
        {
            var devicesFound = 0;

            await Service.ScanNetworkAsync((status) =>
            {
                var mac = status.StatusNet.Mac;
                var deviceByMac = Network.DevicesByMac[mac];

                var ip = IPAddress.Parse(status.StatusNet.IpAddress);
                var deviceByIp = Network.DevicesByMac[mac];
                Assert.AreSame(deviceByMac, deviceByIp, "Devices differ");

                var actual = JsonConvert.SerializeObject(status);
                var expected = JsonConvert.SerializeObject(deviceByMac.Status);
                Assert.AreEqual(expected, actual, "Status differ");

                devicesFound++;
            }, 1, 254);

            Assert.AreEqual(Network.Devices.Count, devicesFound);
        }

        [TestMethod]
        public async Task ClearMqtt_RevertsToOriginalConfigurationAndRestartsDevice()
        {
            var device = Network.Devices[0];

            device.MqttConfiguration = new MqttConfiguration
            {
                Host = DeviceEmulator.DefaultConfiguration.Host + "_",
                Port = DeviceEmulator.DefaultConfiguration.Port + 1,
                Username = DeviceEmulator.DefaultConfiguration.Username + "_",
                Password = DeviceEmulator.DefaultConfiguration.Password + "_"
            };
            device.Topic = device.DefaultTopic + "_";
            device.SetOption19 = DeviceEmulator.DefaultSetOption19 + 1;
            device.SetOption59 = DeviceEmulator.DefaultSetOption59 + 1;

            await Service.ClearMqttAsync(device.IpAddress);

            Assert.AreEqual(DeviceEmulator.DefaultConfiguration, device.MqttConfiguration);
            Assert.AreEqual(device.DefaultTopic, device.Topic);
            Assert.AreEqual(DeviceEmulator.DefaultSetOption19, device.SetOption19);
            Assert.AreEqual(DeviceEmulator.DefaultSetOption59, device.SetOption59);

            device.AssertDeviceRestart();
        }

        [TestMethod]
        public async Task ConfigureMqtt_SetsConfigurationAndRestartsDevice()
        {
            var device = Network.Devices[1];

            await Service.ConfigureMqttAsync(device.IpAddress, CallbackTopic, MqttConfiguration.Host, MqttConfiguration.Port, MqttConfiguration.Username, MqttConfiguration.Password);

            Assert.AreEqual(CallbackTopic, device.Topic);
            Assert.AreEqual(MqttConfiguration.Host, device.MqttConfiguration.Host);
            Assert.AreEqual(MqttConfiguration.Port, device.MqttConfiguration.Port);
            Assert.AreEqual(MqttConfiguration.Username, device.MqttConfiguration.Username);
            Assert.AreEqual(MqttConfiguration.Password, device.MqttConfiguration.Password);
            Assert.AreEqual(1, device.SetOption19);

            device.AssertDeviceRestart();
        }

        [TestMethod]
        public async Task TestMqttConfiguration_WhenDeviceConfigured_SendsMqttMessage()
        {
            var device = Network.Devices[2];

            // Should work when correctly configured...
            device.ConfigureMqtt(MqttConfiguration);

            string? receivedTopic = null;
            string? receivedPayload = null;

            Network.MqttServer.MessageReceived += (s, e) =>
            {
                receivedTopic = e.Message.Topic;
                receivedPayload = e.Message.Payload;
            };

            await Service.TestMqttConfigurationAsync(device.IpAddress, CallbackTopic, device.MacAddress);

            Assert.AreEqual($"tasmocc/{CallbackTopic}/mqttWorks", receivedTopic);
            Assert.AreEqual(device.MacAddress, receivedPayload);

            // ...and should fail when not.
            device.MqttConfiguration.Host = string.Empty;
            device.Startup();          // Updates MqttClient

            receivedTopic = null;
            receivedPayload = null;

            await Service.TestMqttConfigurationAsync(device.IpAddress, CallbackTopic, device.MacAddress);

            Assert.IsNull(receivedTopic);
            Assert.IsNull(receivedPayload);
        }

        [TestMethod]
        public async Task IsResponding_WhenDeviceOnline_ReturnsTrue()
        {
            var device = Network.Devices[3];

            device.IsOffline = false;
            Assert.AreEqual(true, await Service.IsRespondingAsync(device.IpAddress));

            device.IsOffline = true;
            Assert.AreEqual(false, await Service.IsRespondingAsync(device.IpAddress));

            // Non-existing IP address
            Assert.AreEqual(false, await Service.IsRespondingAsync(IPAddress.Parse("192.168.66.201")));
        }

        [TestMethod]
        public void WaitForDeviceResponding_WhenDeviceOffline_Waits()
        {
            var device = Network.Devices[4];

            var callbackCalled = new AutoResetEvent(false);
            device.IsOffline = true;
            Service.WaitForDeviceResponding(device.IpAddress, TimeSpan.Zero, (online, _) => callbackCalled.Set());
            Thread.Sleep(2000);
            Assert.AreEqual(false, callbackCalled.WaitOne(500));

            device.IsOffline = false;
            Thread.Sleep(2000);
            Assert.AreEqual(true, callbackCalled.WaitOne(500));
        }
    }
}
