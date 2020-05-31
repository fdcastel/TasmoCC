using System.Collections.Generic;
using TasmoCC.MongoDb.Models;

namespace TasmoCC.Service.Configuration
{
    public class YamlConfiguration
    {
        public string? Version { get; set; }
        public IDictionary<string, Template>? Templates { get; set; }
        public IDictionary<string, DeviceConfiguration>? Devices { get; set; }
    }
}
