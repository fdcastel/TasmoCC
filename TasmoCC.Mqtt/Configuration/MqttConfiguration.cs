using System;

namespace TasmoCC.Mqtt.Configuration
{
    public class MqttConfiguration
    {
        public static readonly int DefaultPort = 1883;

        public string Host { get; set; } = default!;
        public int Port { get; set; } = DefaultPort;
        public string Username { get; set; } = default!;
        public string Password { get; set; } = default!;

        public override bool Equals(object obj)
        {
            // https://stackoverflow.com/a/2542712/33244
            if (!(obj is MqttConfiguration))
                return false;

            var comparand = (MqttConfiguration)obj;
            return Host == comparand.Host &&
                Port == comparand.Port &&
                Username == comparand.Username &&
                Password == comparand.Password;
        }

        public override int GetHashCode() => HashCode.Combine(Host, Port, Username, Password);

        public override string ToString()
        {
            return $"MqttConfiguration: Host='{Host}', Port={Port}, Username='{Username}', Password='{Password}'";
        }

        public MqttConfiguration Clone() => new MqttConfiguration()
        {
            Host = Host,
            Port = Port,
            Username = Username,
            Password = Password
        };
    }
}
