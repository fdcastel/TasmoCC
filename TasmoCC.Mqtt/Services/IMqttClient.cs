using System;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Mqtt.Models;

namespace TasmoCC.Mqtt.Services
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public MqttMessage Message { get; set; } = default!;
    }

    public interface IMqttClient
    {
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public ManualResetEventSlim ConnectedEvent { get; }

        public void Start(MqttConfiguration configuration, CancellationToken cancellationToken = default);
        public void Stop();

        public Task PublishMessageAsync(MqttMessage message);
    }
}
