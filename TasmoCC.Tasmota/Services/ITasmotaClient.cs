using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace TasmoCC.Tasmota.Services
{
    public interface ITasmotaClient
    {
        public Task<string?> InvokeCommandAsync(IPAddress ipAddress, string command, string? parameters = null, CancellationToken cancellationToken = default, bool throwUnresponsiveException = false);
    }
}
