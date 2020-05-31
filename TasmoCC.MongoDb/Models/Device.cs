using System;
using System.Net;

namespace TasmoCC.MongoDb.Models
{
    public class Device : IMongoDbDocument
    {
        private string __id = default!;
        public string _id { get => __id; set => __id = value.ToLowerInvariant(); }

        public string HostName { get; set; } = default!;

        public IPAddress Ipv4Address { get; set; } = default!;
        public int Ipv4SubnetPrefix { get; set; } = default!;
        public IPAddress Ipv4Gateway { get; set; } = default!;
        public IPAddress Ipv4NameServer { get; set; } = default!;

        public string TopicName { get; set; } = default!;
        public string[] FriendlyNames { get; set; } = default!;

        public string FirmwareVersion { get; set; } = default!;
        public int FirmwareSizeKb { get; set; } = default!;
        public int FlashSizeKb { get; set; } = default!;
        public string Hardware { get; set; } = default!;

        public string RestartReason { get; set; } = default!;
        public int? TelemetrySeconds { get; set; }

        public string TemplateDefinition { get; set; } = default!;
        public string TemplateName { get; set; } = default!;

        public DeviceStatus Status { get; set; } = default!;
        public bool? Offline { get; set; }

        public DeviceState? State { get; set; }

        public DateTime? AdoptedAt { get; set; }
        public DateTime? ProvisionedAt { get; set; }
        public DateTime UpdatedAt { get; set; } = default!;
    }
}
