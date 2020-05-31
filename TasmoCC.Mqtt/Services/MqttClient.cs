using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using System;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Mqtt.Models;

namespace TasmoCC.Mqtt.Services
{
    public class MqttClient : IMqttClient
    {
        public event EventHandler<MessageReceivedEventArgs> MessageReceived = default!;
        public ManualResetEventSlim ConnectedEvent { get; private set; }

        private readonly ILogger<MqttClient> _logger;

        private IManagedMqttClient? _currentClient = null;
        private CancellationTokenSource? _currentCts;

        public MqttClient(ILogger<MqttClient> logger)
        {
            ConnectedEvent = new ManualResetEventSlim(false);
            _logger = logger;
        }

        public void Start(MqttConfiguration configuration, CancellationToken cancellationToken)
        {
            if (_currentCts != null)
            {
                throw new InvalidOperationException("MqttClient is already started.");
            }

            _currentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task.Run(async () =>
            {
                using var client = new MqttFactory()
                    .CreateManagedMqttClient();

                _currentClient = client;

                client.UseConnectedHandler(e =>
                {
                    ConnectedEvent.Set();
                    _logger.LogInformation("Connected to Mqtt server.");
                });

                client.UseDisconnectedHandler(e =>
                {
                    ConnectedEvent.Reset();
                    const string message = "Disconnected from Mqtt server. Reconnecting in 5 seconds...";
                    if (e.Exception != null)
                    {
                        _logger.LogError(e.Exception, message);
                    }
                    else
                    {
                        _logger.LogWarning(message);
                    }
                });

                await client.SubscribeAsync(new TopicFilterBuilder()
                    .WithTopic("tele/+/STATE")
                    .Build());

                await client.SubscribeAsync(new TopicFilterBuilder()
                    .WithTopic("tasmocc/#")
                    .Build());

                var clientOptions = new MqttClientOptionsBuilder()
                    .WithClientId("TasmoCC")
                    .WithTcpServer(configuration.Host, configuration.Port)
                    .WithCredentials(configuration.Username, configuration.Password)
                    .Build();

                var managedOptions = new ManagedMqttClientOptionsBuilder()
                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                    .WithClientOptions(clientOptions)
                    .Build();

                client.UseApplicationMessageReceivedHandler(e =>
                    OnMessageReceived(new MessageReceivedEventArgs()
                    {
                        Message = new MqttMessage()
                        {
                            ClientId = e.ClientId,
                            Topic = e.ApplicationMessage.Topic,
                            Payload = e.ApplicationMessage.ConvertPayloadToString()
                        }
                    }));

                await client.StartAsync(managedOptions);
                _currentCts.Token.WaitHandle.WaitOne();
                _currentClient = null;
                await client.StopAsync();
            }, _currentCts.Token);
        }

        public void Stop()
        {
            if (_currentCts != null)
            {
                _currentCts.Cancel();
                _currentCts = null;
            }
        }

        public async Task PublishMessageAsync(MqttMessage message)
        {
            if (_currentClient == null)
            {
                throw new InvalidOperationException("MqttClient is not connected. Cannot publish message.");
            }

            var m = new MqttApplicationMessageBuilder()
                .WithTopic(message.Topic)
                .WithPayload(message.Payload)
                .Build();

            await _currentClient.PublishAsync(m, CancellationToken.None);
        }

        protected virtual void OnMessageReceived(MessageReceivedEventArgs e) => MessageReceived?.Invoke(this, e);
    }
}
