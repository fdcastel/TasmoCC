using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.Tasmota.Configuration;
using TasmoCC.Tasmota.Models;

namespace TasmoCC.Tasmota.Services
{
    public class TasmotaService
    {
        private readonly ILogger<TasmotaService> _logger;
        private readonly ITasmotaClient _client;

        public TasmotaConfiguration Configuration { get; private set; }

        public TasmotaService(IOptions<TasmotaConfiguration> options, ILogger<TasmotaService> logger, ITasmotaClient client)
        {
            Configuration = options.Value;
            _client = client;
            _logger = logger;
        }

        public async Task ScanNetworkAsync(Action<TasmotaStatus> callback, int first = 1, int last = 254, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Scanning network '{subnet}/24' for devices...", Configuration.Subnet);
            var stopwach = new Stopwatch();
            stopwach.Start();

            var devicesFound = 0;
            var networkSegments = Configuration.Subnet.GetAddressBytes();
            var range = Enumerable.Range(first, last - first + 1);
            var tasks = range.Select(async i =>
            {
                networkSegments[3] = (byte)i;
                var ip = new IPAddress(networkSegments);
                try
                {
                    var result = await GetStatusAsync(ip, cancellationToken);
                    if (result != null)
                    {
                        Interlocked.Increment(ref devicesFound);
                        callback(result);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancelled. Nothing to do.
                }
            });
            await Task.WhenAll(tasks);

            stopwach.Stop();
            _logger.LogInformation("Network scan completed. Found {devicesFound} devices in {seconds} seconds.", devicesFound, stopwach.Elapsed.TotalSeconds);
        }

        public async Task<TasmotaStatus?> GetStatusAsync(IPAddress ipAddress, CancellationToken cancellationToken = default)
        {
            var content = await _client.InvokeCommandAsync(ipAddress, "status", "0", cancellationToken);
            if (content == null)
            {
                return null;
            }

            var templateContent = await _client.InvokeCommandAsync(ipAddress, "template", null, cancellationToken);
            if (templateContent == null)
            {
                return null;
            }

            try
            {
                var status = JsonConvert.DeserializeObject<TasmotaStatus>(content);
                status.Template = templateContent;
                return status;
            }
            catch (JsonException e)
            {
                // Serialization error: Broken Tasmota response may cause this (e.g. Topic = "{topic}" in v8.2.0). Log as warning.
                _logger?.LogWarning("Device '{ipAddress}' returned invalid Json ('{message}'). Content: {content}", ipAddress, e.Message, content);
                return null;
            }
        }

        public async Task ClearMqttAsync(IPAddress ipAddress) =>
            await _client.InvokeCommandAsync(ipAddress, "Backlog", "MqttHost 1; MqttPort 1; MqttUser 1; MqttPassword 1; SetOption19 0; SetOption59 0");

        public async Task ConfigureMqttAsync(IPAddress ipAddress, string topicName, string mqttHost, int mqttPort, string mqttUsername, string mqttPassword)
        {
            // Clear any pending Backlog.
            await _client.InvokeCommandAsync(ipAddress, "Backlog", throwUnresponsiveException: true);

            // Adds 2 seconds between Topic and remaining commands. Without this the Topic won't change! (Tasmota 8.2.0)
            await _client.InvokeCommandAsync(ipAddress, "Backlog", $"Topic {topicName}; Delay 20; MqttHost {mqttHost}; MqttPort {mqttPort}; MqttUser {mqttUsername}; MqttPassword {mqttPassword}; SetOption3 1; SetOption19 1", throwUnresponsiveException: true);
        }

        public async Task ConfigureDeviceAsync(IPAddress ipAddress, string backLogCommands) =>
            await _client.InvokeCommandAsync(ipAddress, "Backlog", backLogCommands, throwUnresponsiveException: true);

        public async Task ResetConfigurationAsync(IPAddress ipAddress, bool keepWiFi) =>
            await _client.InvokeCommandAsync(ipAddress, "Reset", keepWiFi ? "4" : "1", throwUnresponsiveException: true);

        public async Task RestartDeviceAsync(IPAddress ipAddress) =>
            await _client.InvokeCommandAsync(ipAddress, "Restart", "1", throwUnresponsiveException: true);

        public async Task SetPowerAsync(IPAddress ipAddress, int index, string state) =>
            await _client.InvokeCommandAsync(ipAddress, $"power{index}", state, throwUnresponsiveException: true);

        public async Task TestMqttConfigurationAsync(IPAddress ipAddress, string topic, string macAddress) =>
            await _client.InvokeCommandAsync(ipAddress, "publish", $"tasmocc/{topic}/mqttWorks {macAddress}");

        public async Task<bool> IsRespondingAsync(IPAddress ipAddress) =>
            await _client.InvokeCommandAsync(ipAddress, "power", "") != null;

        public void WaitForDeviceResponding(IPAddress ipAddress, TimeSpan initialWait, Action<bool, int> callback, int maxAttempts = 5, CancellationToken cancellationToken = default)
        {
            Task.Run(async () =>
            {
                // Wait for restart.
                Thread.Sleep(initialWait);

                var (isOnline, attempts) = await PollingAsync(() => IsRespondingAsync(ipAddress), maxAttempts, cancellationToken);

                callback(isOnline, attempts);
            }, cancellationToken);
        }

        public async Task<(bool, int)> PollingAsync(Func<Task<bool>> poller, int maxAttempts = 5, CancellationToken cancellationToken = default)
        {
            var attempts = 0;
            var result = false;
            while (!result && attempts < maxAttempts)
            {
                try
                {
                    result = await poller();
                }
                catch (Exception)
                {
                    result = false;
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
                attempts++;

                cancellationToken.ThrowIfCancellationRequested();
            }

            return (result, attempts);
        }

        public async Task UpgradeFirmwareAsync(IPAddress ipAddress)
        {
            await _client.InvokeCommandAsync(ipAddress, "Upgrade", "1", throwUnresponsiveException: true);
        }
    }
}
