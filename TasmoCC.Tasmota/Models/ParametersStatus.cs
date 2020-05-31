namespace TasmoCC.Tasmota.Models
{
    public class ParametersStatus
    {
        //public int BaudRate { get; set; }
        //public string SerialConfig { get; set; }
        //public string GroupTopic { get; set; }
        //public string OtaUrl { get; set; }
        public string RestartReason { get; set; } = default!;
        //public string Uptime { get; set; }

        // Keep this nullable if enabled because sometimes Tasmota may return "" (!?)
        //public DateTime? StartupUtc { get; set; }

        //public int Sleep { get; set; }
        //public int CfgHolder { get; set; }
        //public int BootCount { get; set; }
        //public DateTime BcResetTime { get; set; }
        //public int SaveCount { get; set; }
        //public string SaveAddress { get; set; }
    }
}
