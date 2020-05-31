using System;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Mqtt.Models;
using TasmoCC.Mqtt.Services;

namespace TasmoCC.Tests.Mocks
{
    public class MockMqttClient : IMqttClient
    {
#pragma warning disable CS0414 // The field 'MessageReceived' is assigned but its value is never used
        public event EventHandler<MessageReceivedEventArgs> MessageReceived = default!;
#pragma warning restore CS0414

        public ManualResetEventSlim ConnectedEvent { get; private set; }

        private readonly MockMqttServer _server;
        private MqttConfiguration? _configuration;

        public MockMqttClient(MockMqttServer server)
        {
            ConnectedEvent = new ManualResetEventSlim(false);
            _server = server;
        }

        public void Start(MqttConfiguration configuration, CancellationToken cancellationToken = default)
        {
            _configuration = configuration;
            ConnectedEvent.Set();
        }

        public void Stop()
        {
            _configuration = null;
            ConnectedEvent.Reset();
        }

        public Task PublishMessageAsync(MqttMessage message)
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("Client was not started. Cannot publish message.");
            }

            if (_server.Configuration == null)
            {
                throw new InvalidOperationException("MockMqttServer Configuration is not set. Cannot publish message.");
            }

            if (_configuration.Equals(_server.Configuration))
            {
                _server.InternalReceiveMessage(message);
            }

            return Task.CompletedTask;
        }
    }
}
