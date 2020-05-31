using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.MongoDb.Repositories;

namespace TasmoCC.Service.Monitors
{
    public sealed class NetworkMonitor : IHostedService
    {
        private readonly ILogger<NetworkMonitor> _logger;
        private readonly DeviceRepository _deviceRepository;

        public NetworkMonitor(ILogger<NetworkMonitor> logger, DeviceRepository deviceRepository)
        {
            _logger = logger;
            _deviceRepository = deviceRepository;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting Network monitor...");

            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    cancellationToken.ThrowIfCancellationRequested();

                    var knownDevices = _deviceRepository.GetDevices()
                        .Select(d => (d.Ipv4Address, d.Offline ?? false));

                    await PingDevicesAsync(knownDevices, cancellationToken);
                }
            }, cancellationToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Monitors use CancellationTokens to stop. So nothing to do here.
            return Task.CompletedTask;
        }

        private async Task PingDevicesAsync(IEnumerable<(IPAddress, bool)> devices, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Pinging devices...");
            var stopwach = new Stopwatch();
            stopwach.Start();

            var devicesOnline = 0;
            var devicesOffline = 0;
            var tasks = devices.Select(async device =>
            {
                var (ip, wasOffline) = device;
                var ping = new Ping();
                try
                {
                    var rep = ping.Send(ip, 5000);
                    if (rep.Status == IPStatus.Success)
                    {
                        Interlocked.Increment(ref devicesOnline);
                        if (wasOffline)
                        {
                            _logger.LogInformation("Device at '{ipAddress}' is responsive again. Marking as online.", ip);
                            await _deviceRepository.SetDeviceOfflineAsync(ip, false);
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref devicesOffline);
                        if (!wasOffline)
                        {
                            _logger.LogWarning("Device at '{ipAddress}' is unresponsive. Marking as offline.", ip);
                            await _deviceRepository.SetDeviceOfflineAsync(ip);
                        }
                    };
                }
                catch (OperationCanceledException)
                {
                    // Cancelled. Nothing to do.
                }
            });
            await Task.WhenAll(tasks);

            stopwach.Stop();
            _logger.LogDebug("Ping devices completed. Got {devicesOnline} devices online and {devicesOffline} devices offline in {seconds} seconds.", devicesOnline, devicesOffline, stopwach.Elapsed.TotalSeconds);
        }
    }
}
