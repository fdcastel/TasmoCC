using System.Net;

namespace TasmoCC.Tasmota.Configuration
{
    public class TasmotaConfiguration
    {
        public IPAddress? Subnet { get; set; }
        public string? ConfigurationFile { get; set; }
    }
}
