namespace TasmoCC.Tasmota.Models
{
    public class NetworkStatus
    {
        public string HostName { get; set; } = default!;
        public string IpAddress { get; set; } = default!;
        public string Gateway { get; set; } = default!;
        public string SubnetMask { get; set; } = default!;
        public string DnsServer { get; set; } = default!;
        public string Mac { get; set; } = default!;
        //public int WebServer { get; set; }
        //public int WiFiConfig { get; set; }
        //public double WiFiPower { get; set; }
    }
}
