using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Mqtt.Services;

namespace TasmoCC.Mqtt
{
    public static class IServiceCollectionExtensions
    {
        public static void AddMqttConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<MqttConfiguration>()
                .Bind(configuration.GetSection("Mqtt"))
                .Validate(c => c.Host != null, "You must inform your MQTT server in 'Mqtt:Host' configuration.")
                .Validate(c => c.Username != null, "You must inform your MQTT username in 'Mqtt:Username' configuration.")
                .Validate(c => c.Password != null, "You must inform your MQTT password in 'Mqtt:Password' configuration.");
        }

        public static void AddMqttServices(this IServiceCollection services)
        {
            services.AddTransient<IMqttClient, MqttClient>();
        }
    }
}
