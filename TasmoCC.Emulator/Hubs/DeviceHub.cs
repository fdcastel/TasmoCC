using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using TasmoCC.Emulator.Devices;

namespace TasmoCC.Emulator.Hubs
{
    public class DeviceHub : Hub<IDeviceHubClient>
    {
        private readonly ILogger<DeviceHub> _logger;
        private readonly DeviceEmulator _device;

        public DeviceHub(ILogger<DeviceHub> logger, DeviceEmulator device)
        {
            _logger = logger;
            _device = device;
        }

        public DeviceEmulator GetDevice()
        {
            return _device;
        }

        public Task Restart()
        {
            _logger.LogInformation("Received request to restart device.");
            _device.ExecuteCommand("Restart", "1");
            return Task.CompletedTask;
        }

        public Task TogglePower(int index)
        {
            _logger.LogInformation($"Received request to toggle power {index}.");
            _device.ExecuteCommand($"Power{index}", "2");
            return Task.CompletedTask;
        }

        public Task DeviceChanged()
        {
            Clients.All.DeviceChanged(_device);
            return Task.CompletedTask;
        }
    }
}
