namespace TasmoCC.MongoDb.Models
{
    public class DeviceStatus
    {
        public int UptimeSeconds { get; set; }
        public int HeapKb { get; set; }
        public int CpuLoad { get; set; }

        public int MqttRetries { get; set; }

        public int WiFiRssi { get; set; }
        public int WiFiDbm { get; set; }
        public int WiFiRetries { get; set; }
        public int WiFiDowntimeSeconds { get; set; }

        public string[] PowerStates { get; set; } = default!;
    }
}
