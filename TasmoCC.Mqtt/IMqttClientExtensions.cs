using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Mqtt.Models;
using TasmoCC.Mqtt.Services;

namespace TasmoCC.Mqtt
{
    public static class IMqttClientExtensions
    {
        public static IObservable<MqttMessage> WhenMessageReceived(this IMqttClient client, MqttConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Observable.Create<MqttMessage>(observer =>
            {
                void handler(object s, MessageReceivedEventArgs e) => observer.OnNext(e.Message);

                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                client.MessageReceived += handler;
                client.Start(configuration, cts.Token);

                return Disposable.Create(() =>
                {
                    client.MessageReceived -= handler;
                    cts.Cancel();
                });
            });
        }
    }
}
