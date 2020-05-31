using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using TasmoCC.MongoDb.Configuration;
using TasmoCC.MongoDb.Repositories;

namespace TasmoCC.MongoDb
{
    public static class IServiceCollectionExtensions
    {
        public static void AddMongoDbConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<MongoDbConfiguration>()
                .Bind(configuration.GetSection("MongoDb"))
                .Validate(c => c.ConnectionString != null, "You must inform your MongoDb connection string in 'MongoDb:ConnectionString' configuration.");
        }

        public static void AddMongoDbServices(this IServiceCollection services)
        {
            // Ignore null values -- https://stackoverflow.com/a/38748034
            ConventionRegistry.Register("IgnoreIfDefault", new ConventionPack { new IgnoreIfDefaultConvention(true) }, t => true);

            // Camel case element names -- https://stackoverflow.com/a/19521784
            ConventionRegistry.Register("CamelCaseElementName", new ConventionPack { new CamelCaseElementNameConvention() }, t => true);

            // Enums as strings -- https://stackoverflow.com/a/18874502
            ConventionRegistry.Register("EnumStringConvention", new ConventionPack { new EnumRepresentationConvention(BsonType.String) }, t => true);

            services.AddTransient<IMongoDatabase>(sp =>
            {
                var options = sp.GetService<IOptions<MongoDbConfiguration>>();
                return new MongoClient(options.Value.ConnectionString)
                    .GetDatabase(options.Value.Database);
            });

            services.AddTransient<DeviceRepository>();
            services.AddTransient<TemplateRepository>();
            services.AddTransient<DeviceConfigurationRepository>();
        }
    }
}
