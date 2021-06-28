using Consul;
using ConsulProject.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Linq;

namespace ConsulProject
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddSingleton<IHostedService, ConsulHostedService>();
            services.Configure<ConsulConfig>(Configuration.GetSection("consulConfig"));
            services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(consulConfig =>
            {
                var address = Configuration["consulConfig:address"];
                consulConfig.Address = new Uri(address);
            }));
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "ConsulProject", Version = "v1" });
            });
        }
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime,
              IConsulClient consulClient, IServer server)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConsulProject v1"));
            }
            string _registrationID = "";
            lifetime.ApplicationStarted.Register(() =>
            {
                var features = server.Features;
                var addresses = features.Get<IServerAddressesFeature>();
                if (addresses.Addresses.Count == 0)
                {
                    addresses.Addresses.Add("http://localhost:5000");
                }
                var address = addresses.Addresses.First();

                var uri = new Uri(address);
                _registrationID = $"school-api-v1-final-01-{uri.Port}";

                var tcpCheck = new AgentServiceCheck()
                {
                    DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(1),
                    Interval = TimeSpan.FromSeconds(30),
                    TCP = $"http://localhost:{uri.Port}"
                };
                var httpCheck = new AgentServiceCheck()
                {
                    DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(1),
                    Interval = TimeSpan.FromSeconds(30),
                    HTTP = $"http://localhost:5000/HealthCheck"
                };

                var registration = new AgentServiceRegistration()
                {
                    ID = _registrationID,
                    Name = "bar1-api",
                    Address = $"{uri.Scheme}://{uri.Host}",
                    Port = uri.Port,
                    Tags = new[] { "logestic" },
                    Check = new AgentServiceCheck()
                    {
                        //HTTP = $"{uri.Scheme}://{uri.Host}:{uri.Port}/health",
                        Timeout = TimeSpan.FromSeconds(3),
                        Interval = TimeSpan.FromSeconds(10),
                        TCP = $"{uri.Host}:{uri.Port}",
                        DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(1),
                    }
                };
                consulClient.Agent.ServiceDeregister(registration.ID);
                consulClient.Agent.ServiceRegister(registration);
            });
            lifetime.ApplicationStopping.Register(() =>
            {
                consulClient.Agent.ServiceDeregister(_registrationID).Wait();
            });
            app.UseHttpsRedirection();
            app.UseHealth();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

    }

}
