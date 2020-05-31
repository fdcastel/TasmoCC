using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Converters;
using TasmoCC.MongoDb;
using TasmoCC.Mqtt;
using TasmoCC.Service.Hubs;
using TasmoCC.Service.Monitors;
using TasmoCC.Service.Services;
using TasmoCC.Tasmota;
using TasmoCC.Tasmota.Converters;

namespace TasmoCC.Service
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews()
                 .AddNewtonsoftJson(o =>
                 {
                     o.SerializerSettings.Converters.Add(new IPAddressConverter());
                     o.SerializerSettings.Converters.Add(new StringEnumConverter());
                 });

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/build";
            });

            services.AddSignalR()
                .AddNewtonsoftJsonProtocol(o =>
                {
                    o.PayloadSerializerSettings.Converters.Add(new IPAddressConverter());
                    o.PayloadSerializerSettings.Converters.Add(new StringEnumConverter());
                });

            services.AddMongoDbConfiguration(Configuration);
            services.AddMongoDbServices();

            services.AddMqttConfiguration(Configuration);
            services.AddMqttServices();

            services.AddTasmotaConfiguration(Configuration);
            services.AddTasmotaServices();

            services.AddTransient<MasterService>();

            services.AddHostedService<DatabaseMonitor>();
            services.AddHostedService<MessageMonitor>();
            services.AddHostedService<NetworkMonitor>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");

                endpoints.MapHub<DevicesHub>("/hub/devices");
            });

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseReactDevelopmentServer(npmScript: "start");
                }
            });
        }
    }
}
