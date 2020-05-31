using Newtonsoft.Json;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.Tasmota.Services;

namespace TasmoCC.Tests.Mocks
{
    public class MockTasmotaClient : ITasmotaClient
    {
        private readonly MockNetwork _network;

        public MockTasmotaClient(MockNetwork network)
        {
            _network = network;
        }

        public Task<string?> InvokeCommandAsync(IPAddress ipAddress, string command, string? parameters = null, CancellationToken cancellationToken = default, bool throwUnresponsiveException = false)
        {
            string? result = null;
            if (_network.DevicesByIp.ContainsKey(ipAddress))
            {
                var device = _network.DevicesByIp[ipAddress];
                var response = device.ExecuteCommand(command, parameters);
                if (response != null)
                {
                    result = JsonConvert.SerializeObject(response);
                }
                else if (throwUnresponsiveException)
                {
                    throw new DeviceUnresponsiveException(ipAddress);
                }
            }

            return Task.FromResult(result);
        }
    }
}
