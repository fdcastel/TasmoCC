using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using TasmoCC.Emulator.Hubs;
using TasmoCC.Mqtt;
using TasmoCC.Tasmota.Converters;

namespace TasmoCC.Emulator
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
                     // Preserves property member case in API responses (not in SignalR, used by web client)
                     o.SerializerSettings.ContractResolver = new DefaultContractResolver();

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

            services.AddMqttServices();

            services.AddEmulatorServices();
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

                endpoints.MapHub<DeviceHub>("/hub/device");
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
