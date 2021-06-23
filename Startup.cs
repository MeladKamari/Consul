using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using static ConsulProject.Startup;

namespace ConsulProject
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public class ConsulConfig
        {
            public string Address { get; set; }
            public string ServiceName { get; set; }
            public string ServiceID { get; set; }
        }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
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
              IConsulClient consulClient, IServer  server)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConsulProject v1"));
            }
            string _registrationID="";
            lifetime.ApplicationStarted.Register(() =>
            {
                var features = server.Features;
                var addresses = features.Get<IServerAddressesFeature>();
                if (addresses.Addresses.Count == 0)
                {
                    addresses.Addresses.Add("http://localhost:5000"); // Add the default address to the IServerAddressesFeature

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
                    Name = "school-api",
                    Address = $"{uri.Scheme}://{uri.Host}",
                    Port = uri.Port,
                    Tags = new[] { "Students", "Courses", "School" },
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

       


        public class HealthMiddleware
        {
            private readonly RequestDelegate _next;
            private readonly string _healthPath = "/health";

            public HealthMiddleware(RequestDelegate next, IConfiguration configuration)
            {
                this._next = next;
                var healthPath = configuration["Consul:HealthPath"];
                if (!string.IsNullOrEmpty(healthPath))
                {
                    this._healthPath = healthPath;
                }
            }

            //Monitoring inspection can return more information, such as server resource information
            public async Task Invoke(HttpContext httpContext)
            {
                if (httpContext.Request.Path == this._healthPath)
                {
                    httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
                    await httpContext.Response.WriteAsync("I'm OK!");
                }
                else
                    await this._next(httpContext);
            }
        }

    }

    public static class Hei
    {
        public static void UseHealth(this IApplicationBuilder app)
        {
            app.UseMiddleware<HealthMiddleware>();
        }
    }

}
