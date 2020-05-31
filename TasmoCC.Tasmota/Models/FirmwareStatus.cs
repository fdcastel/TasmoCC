namespace TasmoCC.Tasmota.Models
{
    public class FirmwareStatus
    {
        public string Version { get; set; } = default!;
        //public DateTime BuildDateTime { get; set; }
        //public int Boot { get; set; }
        //public string Core { get; set; }
        //public string Sdk { get; set; }
        public string Hardware { get; set; } = default!;
        //public string Cr { get; set; }
    }
}
