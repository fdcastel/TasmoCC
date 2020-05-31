namespace TasmoCC.Mqtt.Models
{
    public class MqttMessage
    {
        public string ClientId { get; set; } = default!;
        public string Topic { get; set; } = default!;
        public string? Payload { get; set; } = default!;
    }
}
