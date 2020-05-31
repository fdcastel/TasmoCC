using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.Emulator.Devices;
using TasmoCC.Mqtt.Configuration;

namespace TasmoCC.Tests.Extensions
{
    public static class DeviceEmulatorExtensions
    {
        public static void AssertDeviceRestart(this DeviceEmulator device)
        {
            const double toleranceFactor = 1.2;

            WaitForDevice(device, true, DeviceEmulator.RestartDelayBefore * toleranceFactor);
            Assert.IsTrue(device.IsOffline);

            WaitForDevice(device, false, DeviceEmulator.RestartDelayAfter * toleranceFactor);
            Assert.IsFalse(device.IsOffline);

            Assert.IsTrue(device.Uptime < (DeviceEmulator.RestartDelayBefore + DeviceEmulator.RestartDelayAfter) * toleranceFactor);
        }

        public static void AssertPowerWorks(this DeviceEmulator device, int? powerIndex)
        {
            var i = powerIndex ?? 1;

            device.Status.StatusSts.Power("OFF", powerIndex);

            device.ExecuteCommand($"power{i} ON");
            Assert.AreEqual("ON", device.Status.StatusSts.Power(powerIndex));

            device.ExecuteCommand($"power{i} OFF");
            Assert.AreEqual("OFF", device.Status.StatusSts.Power(powerIndex));

            device.ExecuteCommand($"power{i} TOGGLE");
            Assert.AreEqual("ON", device.Status.StatusSts.Power(powerIndex));

            device.ExecuteCommand($"power{i} TOGGLE");
            Assert.AreEqual("OFF", device.Status.StatusSts.Power(powerIndex));

            device.ExecuteCommand($"power{i} 1");
            Assert.AreEqual("ON", device.Status.StatusSts.Power(powerIndex));

            device.ExecuteCommand($"power{i} 0");
            Assert.AreEqual("OFF", device.Status.StatusSts.Power(powerIndex));

            device.ExecuteCommand($"power{i} 2");
            Assert.AreEqual("ON", device.Status.StatusSts.Power(powerIndex));

            device.ExecuteCommand($"power{i} 2");
            Assert.AreEqual("OFF", device.Status.StatusSts.Power(powerIndex));
        }

        public static void ConfigureMqtt(this DeviceEmulator device, MqttConfiguration configuration)
        {
            device.MqttConfiguration = configuration.Clone();

            // Updates MqttClient
            device.Startup();
        }

        public static void WaitForDevice(this DeviceEmulator device, bool offline, TimeSpan timeout)
        {
            using var ev = new AutoResetEvent(false);
            using var cts = new CancellationTokenSource();

            Task.Run(() =>
            {
                while (device.IsOffline == offline)
                {
                    Thread.Sleep(200);
                    cts.Token.ThrowIfCancellationRequested();
                }
            });

            ev.WaitOne(timeout);
            cts.Cancel();
        }
    }
}
