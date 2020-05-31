namespace TasmoCC.Tasmota.Models
{
    public class TelemetryStatus
    {
        //public DateTime Time { get; set; }
        public int UptimeSec { get; set; }
        public int Heap { get; set; }
        //public string SleepMode { get; set; }
        //public int Sleep { get; set; }
        public int LoadAvg { get; set; }
        public int MqttCount { get; set; }

        public string? Power { get; set; }
        public string? Power1 { get; set; }
        public string? Power2 { get; set; }
        public string? Power3 { get; set; }
        public string? Power4 { get; set; }

        public WifiStatus WiFi { get; set; } = default!;

        public class WifiStatus
        {
            //public int Ap { get; set; }
            //public string Ssid { get; set; }
            //public string Bssid { get; set; }
            //public int Channel { get; set; }
            public int Rssi { get; set; }
            public int Signal { get; set; }
            public int LinkCount { get; set; }
            public string Downtime { get; set; } = default!;
        }
    }
}