using System.Threading.Tasks;
using TasmoCC.Emulator.Devices;

namespace TasmoCC.Emulator.Hubs
{
    public interface IDeviceHubClient
    {
        Task Restart();

        Task TogglePower(int index);

        Task DeviceChanged(DeviceEmulator device);
    }
}
