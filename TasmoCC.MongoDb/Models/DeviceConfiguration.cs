namespace TasmoCC.MongoDb.Models
{
    public class DeviceConfiguration : IMongoDbDocument
    {
        private string __id = default!;
        public string _id { get => __id; set => __id = value.ToLowerInvariant(); }

        public bool? Disabled { get; set; }
        public string[]? FriendlyNames { get; set; }
        public string? SetupCommands { get; set; }
        public string TemplateName { get; set; } = default!;
        public string TopicName { get; set; } = default!;
    }
}
