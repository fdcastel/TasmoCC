using System;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Mqtt.Models;
using TasmoCC.Mqtt.Services;

namespace TasmoCC.Tests.Mocks
{
    public class MockMqttServer
    {
        public MqttConfiguration? Configuration;

        public event EventHandler<MessageReceivedEventArgs> MessageReceived = default!;

        public MockMqttServer(MqttConfiguration configuration)
        {
            Configuration = configuration;
        }

        internal void InternalReceiveMessage(MqttMessage message)
        {
            OnMessageReceived(new MessageReceivedEventArgs()
            {
                Message = message
            });
        }

        protected virtual void OnMessageReceived(MessageReceivedEventArgs e) => MessageReceived?.Invoke(this, e);
    }
}
