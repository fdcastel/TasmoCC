using Microsoft.AspNetCore.Mvc;
using TasmoCC.Emulator.Devices;

namespace TasmoCC.Emulator.Controllers
{
    [Route("/")]
    [ApiController]
    public class CommandController : ControllerBase
    {
        private readonly DeviceEmulator _device;

        public CommandController(DeviceEmulator device)
        {
            _device = device;
        }

        [HttpGet]
        [Route("cm")]
        public dynamic? ExecuteCommand(string? cmnd)
        {
            return _device.ExecuteCommand(cmnd);
        }
    }
}
