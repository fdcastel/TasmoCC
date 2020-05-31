using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TasmoCC.Tasmota.Configuration;
using TasmoCC.Tasmota.Services;

namespace TasmoCC.Tasmota
{
    public static class IServiceCollectionExtensions
    {
        public static void AddTasmotaConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            var logger = services.BuildServiceProvider().GetService<ILogger<TasmotaConfiguration>>();
            services.AddOptions<TasmotaConfiguration>()
                .Bind(configuration.GetSection("Tasmota"))
                .Configure(c => c.Subnet = IPAddress.TryParse(configuration["Tasmota:Subnet"], out var result) ? result : DetectCurrentSubnet(logger)!)
                .Validate(c => c.Subnet != null, "You must inform the IP Subnet of your devices in 'Tasmota:Subnet' configuration.")
                .PostConfigure(c => logger.LogInformation("Using subnet {subnet}.", c.Subnet));
        }

        private static IPAddress? DetectCurrentSubnet(ILogger<TasmotaConfiguration> logger)
        {
            logger.LogWarning("Subnet not present in configuration. Trying to detect...");

            // https://stackoverflow.com/questions/850650/reliable-method-to-get-machines-mac-address-in-c-sharp#comment107946437_7661829
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            var firstAddress = nic.GetIPProperties()
                .UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

#pragma warning disable CS0618 // 'Address' is obsolete
            return firstAddress != null
                ? new IPAddress(firstAddress.Address.Address & IPAddress.Parse("255.255.255.0").Address)
                : null;
#pragma warning restore CS0618
        }

        public static void AddTasmotaServices(this IServiceCollection services)
        {
            // This also register ITasmotaClient as a transient service -- https://github.com/dotnet/docs/issues/17667
            services.AddHttpClient<ITasmotaClient, TasmotaClient>(c => c.Timeout = TimeSpan.FromSeconds(5));

            services.AddTransient<TasmotaService>();
        }
    }
}
