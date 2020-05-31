namespace TasmoCC.Tasmota.Models
{
    public class TasmotaStatus
    {
        public DeviceStatus Status { get; set; } = default!;
        public ParametersStatus StatusPrm { get; set; } = default!;
        public FirmwareStatus StatusFwr { get; set; } = default!;
        public LogStatus StatusLog { get; set; } = default!;
        public MemoryStatus StatusMem { get; set; } = default!;
        public NetworkStatus StatusNet { get; set; } = default!;
        public TelemetryStatus StatusSts { get; set; } = default!;

        public string Template { get; set; } = default!;
    }
}
