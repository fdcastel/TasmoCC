using System;
using System.Net;

namespace TasmoCC.Tasmota.Services
{
    public class DeviceUnresponsiveException : Exception
    {
        public IPAddress IPAddress { get; }

        public DeviceUnresponsiveException(string message, IPAddress ipAddress)
            : base(message)
        {
            IPAddress = ipAddress;
        }

        public DeviceUnresponsiveException(string message, Exception inner, IPAddress ipAddress)
            : base(message, inner)
        {
            IPAddress = ipAddress;
        }

        public DeviceUnresponsiveException(IPAddress ipAddress)
             : this($"Device at '{ipAddress}' is unresponsive.", ipAddress)
        {
            IPAddress = ipAddress;
        }

        public DeviceUnresponsiveException(IPAddress ipAddress, Exception inner)
             : this($"Device at '{ipAddress}' is unresponsive.", inner, ipAddress)
        {
            IPAddress = ipAddress;
        }
    }
}
