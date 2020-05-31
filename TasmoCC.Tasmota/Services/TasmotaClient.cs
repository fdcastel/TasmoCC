using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TasmoCC.Tasmota.Services
{
    public class TasmotaClient : ITasmotaClient
    {
        private readonly HttpClient _httpClient;

        public TasmotaClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string?> InvokeCommandAsync(IPAddress ipAddress, string command, string? parameters = null, CancellationToken cancellationToken = default, bool throwUnresponsiveException = false)
        {
            var fullCommand = $"{command} {parameters}".Trim();
            var escapedCommand = Uri.EscapeDataString(fullCommand);        // https://stackoverflow.com/a/34189188/33244  
            try
            {
                using var response = await _httpClient.GetAsync($"http://{ipAddress}/cm?cmnd={escapedCommand}", cancellationToken);
                return response.IsSuccessStatusCode
                    ? await response.Content.ReadAsStringAsync()
                    : null;
            }
            catch (Exception e)
            {
                // Device unresponsive
                if (throwUnresponsiveException)
                {
                    throw new DeviceUnresponsiveException(ipAddress, e);
                }

                return null;
            }
        }
    }
}
