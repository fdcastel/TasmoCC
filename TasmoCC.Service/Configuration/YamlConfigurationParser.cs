using Newtonsoft.Json;
using System;
using System.IO;
using TasmoCC.MongoDb.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TasmoCC.Service.Configuration
{
    public class YamlConfigurationParser
    {
        public YamlConfiguration ParseConfiguration(string configurationFile)
        {
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithAttributeOverride<DeviceConfiguration>(
                    d => d._id,
                    new YamlMemberAttribute { Alias = "mac" }
                )
                .Build();

            using var reader = new StreamReader(configurationFile);

            var result = deserializer.Deserialize<YamlConfiguration>(reader);

            // Templates section
            if (result.Templates == null)
            {
                throw new Exception("Configuration file must have 'templates' section.");
            }
            foreach (var t in result.Templates)
            {
                t.Value._id = ExtractTemplateName(t.Value.Definition);
            }

            // Devices section
            if (result.Devices != null)
            {
                foreach (var d in result.Devices)
                {
                    if (d.Key == "common")
                    {
                        d.Value._id = d.Key;
                    }
                    else
                    {
                        d.Value.TopicName = d.Key;

                        var template = result.Templates[d.Value.TemplateName];
                        if (template == null)
                        {
                            throw new Exception($"Template '{d.Value.TemplateName}' for device '{d.Key}' not found in 'templates' section.");
                        }

                        d.Value.TemplateName = template._id;
                    }
                }
            }

            return result;
        }

        private static string ExtractTemplateName(string definition)
        {
            try
            {
                var def = JsonConvert.DeserializeObject<dynamic>(definition);

                var result = def.NAME;
                if (result == null)
                {
                    throw new Exception($"Template definition ({definition}) has no \"NAME\" property.");
                }

                return result;
            }
            catch (JsonException)
            {
                throw new Exception($"Invalid template definition ({definition})");
            }
        }
    }
}
