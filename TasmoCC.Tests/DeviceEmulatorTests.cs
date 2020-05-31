using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Linq;
using System.Net;
using System.Threading;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Tests.Extensions;
using TasmoCC.Tests.Mocks;

namespace TasmoCC.Tests
{
    [TestClass]
    public class DeviceEmulatorTests
    {
        public MqttConfiguration MqttConfiguration { get; }

        private readonly MockNetwork Network;
        private string? _receivedTopic;
        private string? _receivedPayload;

        public DeviceEmulatorTests()
        {
            MqttConfiguration = new MqttConfiguration()
            {
                Host = "192.168.66.200",
                Port = 64220,
                Username = "vader",
                Password = "jedi"
            };

            Network = new MockNetwork(IPAddress.Parse("192.168.66.0"), 10, MqttConfiguration);

            Network.MqttServer.MessageReceived += (s, e) =>
            {
                _receivedTopic = e.Message.Topic;
                _receivedPayload = e.Message.Payload;
            };
        }

        [TestMethod]
        public void ExecuteCommand_WhenDeviceOffline_ReturnsNull()
        {
            var device = Network.Devices[0];

            device.IsOffline = false;
            Assert.IsNotNull(device.ExecuteCommand("power", ""));

            device.IsOffline = true;
            Assert.IsNull(device.ExecuteCommand("power", ""));
        }

        [TestMethod]
        public void Power_WithSingleChannel_ChangesPowerState()
        {
            var device = Network.Devices.Where(d => d.Status.StatusSts.Power != null).Last();
            device.AssertPowerWorks(null);
        }

        [TestMethod]
        public void Power_WithDualChannel_ChangesPowerState()
        {
            var device = Network.Devices.Where(d => d.Status.StatusSts.Power2 != null && d.Status.StatusSts.Power3 == null).Last();
            device.AssertPowerWorks(1);
            device.AssertPowerWorks(2);
        }

        [TestMethod]
        public void Power_WithQuadChannel_ChangesPowerState()
        {
            var device = Network.Devices.Where(d => d.Status.StatusSts.Power4 != null).Last();
            device.AssertPowerWorks(1);
            device.AssertPowerWorks(2);
            device.AssertPowerWorks(3);
            device.AssertPowerWorks(4);
        }

        [TestMethod]
        public void Publish_SendsMqttMessage()
        {
            var device = Network.Devices[1];

            // Should work when correctly configured...
            device.ConfigureMqtt(MqttConfiguration);

            _receivedTopic = null;
            _receivedPayload = null;

            const string sentTopic = "test/topic/publish";
            const string sentPayload = "payload";
            device.ExecuteCommand("Publish", $"{sentTopic} {sentPayload}");

            Assert.AreEqual(sentTopic, _receivedTopic);
            Assert.AreEqual(sentPayload, _receivedPayload);

            // ...and should fail when not.
            device.MqttConfiguration.Host = string.Empty;
            device.Startup();          // Updates MqttClient

            _receivedTopic = null;
            _receivedPayload = null;

            device.ExecuteCommand("Publish", $"{sentTopic} {sentPayload}");

            Assert.IsNull(_receivedTopic);
            Assert.IsNull(_receivedPayload);
        }

        [TestMethod]
        public void Restart_RestartsDevice()
        {
            var device = Network.Devices[2];
            Assert.IsFalse(device.IsOffline);
            device.ExecuteCommand("Restart 1");
            device.AssertDeviceRestart();
        }

        [TestMethod]
        public void State_SendsMqttMessageAndReturnsState()
        {
            var device = Network.Devices[3];

            // Should work when correctly configured...
            device.ConfigureMqtt(MqttConfiguration);
            device.SetOption59 = 1;

            _receivedTopic = null;
            _receivedPayload = null;

            var expectedTopic = $"tele/{device.Topic}/STATE";
            var returnedPayload = device.ExecuteCommand("State");

            var jsonStatus = JsonConvert.SerializeObject(device.Status.StatusSts);
            var jsonReturnedPayload = JsonConvert.SerializeObject(returnedPayload);

            Assert.AreEqual(expectedTopic, _receivedTopic);
            Assert.AreEqual(jsonStatus, _receivedPayload);
            Assert.AreEqual(jsonStatus, jsonReturnedPayload);

            // ...and should fail when not.
            device.SetOption59 = 0;

            _receivedTopic = null;
            _receivedPayload = null;

            device.ExecuteCommand("State");

            Assert.IsNull(_receivedTopic);
            Assert.IsNull(_receivedPayload);
        }

        [TestMethod]
        public void TelePeriod_ChangesTelemetryTimer()
        {
            var device = Network.Devices[4];

            // Should work when correctly configured...
            device.ConfigureMqtt(MqttConfiguration);
            device.SetOption59 = 1;

            Assert.AreEqual(300, device.TelePeriod);

            _receivedTopic = null;
            _receivedPayload = null;

            device.ExecuteCommand("TelePeriod 10");

            Thread.Sleep(6000);

            Assert.IsNull(_receivedTopic);
            Assert.IsNull(_receivedPayload);

            Thread.Sleep(5000);

            var expectedTopic = $"tele/{device.Topic}/STATE";
            var jsonStatus = JsonConvert.SerializeObject(device.Status.StatusSts);
            Assert.AreEqual(expectedTopic, _receivedTopic);
            Assert.AreEqual(jsonStatus, _receivedPayload);

            // ...and should fail when not.
            device.SetOption59 = 0;

            _receivedTopic = null;
            _receivedPayload = null;

            Thread.Sleep(12000);

            Assert.IsNull(_receivedTopic);
            Assert.IsNull(_receivedPayload);
        }
    }
}
