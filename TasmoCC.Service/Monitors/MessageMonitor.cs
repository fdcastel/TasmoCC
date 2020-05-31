using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.MongoDb.Models;
using TasmoCC.MongoDb.Repositories;
using TasmoCC.Mqtt;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Mqtt.Models;
using TasmoCC.Mqtt.Services;
using TasmoCC.Service.Services;
using TasmoCC.Tasmota.Models;

namespace TasmoCC.Service.Monitors
{
    public sealed class MessageMonitor : IHostedService
    {
        private readonly MqttConfiguration _mqttOptions;
        private readonly ILogger<MessageMonitor> _logger;
        private readonly DeviceRepository _deviceRepository;
        private readonly IMqttClient _messageClient;
        private readonly MasterService _masterService;

        public MessageMonitor(IOptions<MqttConfiguration> mqttOptions, ILogger<MessageMonitor> logger, DeviceRepository deviceRepository, IMqttClient messageClient, MasterService masterService)
        {
            _mqttOptions = mqttOptions.Value;
            _logger = logger;
            _deviceRepository = deviceRepository;
            _messageClient = messageClient;
            _masterService = masterService;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting Mqtt monitor...");
            _messageClient.WhenMessageReceived(_mqttOptions, cancellationToken)
                .Subscribe(async m => await MessageReceivedAsync(m), cancellationToken);

            // MqttClient may take several seconds to connect on first run.
            _logger.LogInformation("Waiting for Mqtt connection...");
            _messageClient.ConnectedEvent.Wait(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _masterService.ScanNetwork(cancellationToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Monitors use CancellationTokens to stop. So nothing to do here.
            return Task.CompletedTask;
        }

        //
        // Message events
        //

        private static string? ExtractTopicName(string fullTopic)
        {
            // "<prefix>/<topic>/*"
            var match = Regex.Match(fullTopic, "^(?<prefix>.*)/(?<topic>.*)/");
            return match.Success ? match.Groups["topic"].Value : null;
        }

        private async Task MessageReceivedAsync(MqttMessage m)
        {
            _logger.LogDebug("Received: '{topic}'.", m.Topic);

            var topicName = ExtractTopicName(m.Topic);
            if (String.IsNullOrEmpty(topicName))
            {
                _logger.LogWarning("Discarding due invalid topic '{topic}'.", m.Topic);
                return;
            }

            if (m.Topic.StartsWith("tasmocc"))
            {
                // tasmocc/{topicName}/mqttWorks

                var _id = m.Payload;
                if (_id == null)
                {
                    _logger.LogWarning("Discarding due invalid message (no payload)'.");
                    return;
                }

                var device = await _deviceRepository.GetDeviceAsync(_id);
                if (device == null)
                {
                    _logger.LogWarning("Discarding due unknown device '{_id}'.", _id);
                    return;
                }

                await MqttTestReceivedAsync(device);
            }
            else if (m.Topic.StartsWith("tele"))
            {
                // tele/{topicName}/STATE

                var status = m.Payload;
                if (status == null)
                {
                    _logger.LogWarning("Discarding due invalid message (no payload)'.");
                    return;
                }

                var device = await _deviceRepository.GetDeviceFromTopicNameAsync(topicName);
                if (device == null)
                {
                    _logger.LogWarning("Discarding due unknown device for topic name '{topicName}'.", topicName);
                    return;
                }

                var telemetryStatus = status.DeserializeIgnoringCase<TelemetryStatus>();
                await TelemetryReceivedAsync(device, telemetryStatus);
            }
        }

        private async Task MqttTestReceivedAsync(Device device)
        {
            _logger.LogInformation("Device '{hostname}' has working Mqtt configuration. Marking as adopted.", device.HostName);

            var fieldsToUpdate = new Device()
            {
                _id = device._id,
                AdoptedAt = DateTime.Now,
                State = DeviceState.ProvisionPending,
                Offline = false
            };
            await _deviceRepository.UpdateDeviceAsync(fieldsToUpdate);
        }

        private async Task TelemetryReceivedAsync(Device device, TelemetryStatus telemetryStatus)
        {
            var fieldsToUpdate = new Device()
            {
                _id = device._id,
                Status = telemetryStatus.ToDeviceStatus(),
                Offline = false
            };
            var fieldsToUnset = new string[0];
            if (device.State == DeviceState.Provisioning)
            {
                _logger.LogInformation("Device '{hostname}' has completed provisioning.", device.HostName);
                fieldsToUpdate.ProvisionedAt = DateTime.Now;
                fieldsToUnset = new[] { "state" };
            }
            await _deviceRepository.UpdateDeviceAsync(fieldsToUpdate, isUpsert: false, fieldsToUnset: fieldsToUnset);

            // After provision or restart
            if (device.State == DeviceState.Provisioning || telemetryStatus.UptimeSec < device.Status.UptimeSeconds)
            {
                // Fetch all device information again and update device in db
                await _masterService.ScanDeviceAsync(device.Ipv4Address);
            }
        }
    }
}
