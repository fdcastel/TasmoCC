using System;
using System.Linq;
using System.Text.RegularExpressions;
using TasmoCC.Tasmota.Models;

namespace TasmoCC.Service
{
    public static class TelemetryStatusExtensions
    {
        public static MongoDb.Models.DeviceStatus ToDeviceStatus(this TelemetryStatus status)
        {
            return new MongoDb.Models.DeviceStatus()
            {
                UptimeSeconds = status.UptimeSec,
                HeapKb = status.Heap,
                CpuLoad = status.LoadAvg,

                MqttRetries = status.MqttCount,

                WiFiRssi = status.WiFi.Rssi,
                WiFiDbm = status.WiFi.Signal,
                WiFiRetries = status.WiFi.LinkCount,
                WiFiDowntimeSeconds = ConvertToSeconds(status.WiFi.Downtime),

                PowerStates = new[] { status.Power, status.Power1, status.Power2, status.Power3, status.Power4 }
                    .Where(p => p != null)
                    .Select(p => p!)
                    .ToArray()
            };
        }

        private static int ConvertToSeconds(string duration)
        {
            // 0T00:00:06
            var match = Regex.Match(duration, @"^(?<days>\d+)T(?<hours>\d+):(?<minutes>\d+):(?<seconds>\d*)");
            if (match.Success)
            {
                var days = int.Parse(match.Groups["days"].Value);
                var hours = int.Parse(match.Groups["hours"].Value);
                var minutes = int.Parse(match.Groups["minutes"].Value);
                var seconds = int.Parse(match.Groups["seconds"].Value);
                var result = new TimeSpan(days, hours, minutes, seconds);
                return (int)result.TotalSeconds;
            }
            return 0;
        }
    }
}
