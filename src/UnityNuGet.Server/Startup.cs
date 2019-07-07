using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UnityNuGet.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostingEnvironment hostingEnvironment, ILoggerFactory loggerFactory)
        {
            Configuration = configuration;
            HostingEnvironment = hostingEnvironment;
            LoggerFactory = loggerFactory;
        }

        public IConfiguration Configuration { get; }
        
        public IHostingEnvironment HostingEnvironment { get; }
        
        public ILoggerFactory LoggerFactory { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var loggerRedirect = new NuGetRedirectLogger(LoggerFactory.CreateLogger("NuGet"));

            string url = "https://unitynuget-registry.azurewebsites.net/";

            bool isDevelopment = HostingEnvironment.IsDevelopment();
            if (isDevelopment)
            {
                var urls = Configuration[WebHostDefaults.ServerUrlsKey];

                // Select HTTPS in production, HTTP in development
                url = urls.Split(';').FirstOrDefault(x => !x.StartsWith("https"));
                if (url == null)
                {
                    throw new InvalidOperationException($"Unable to find a proper server URL from `{urls}`. Expecting a `http://...` URL in development");
                }
            }

            // Get the current directory /home/site/unity_packages or binary folder in dev
            var currentDirectory = isDevelopment ? Path.GetDirectoryName(typeof(Startup).Assembly.Location) : Directory.GetCurrentDirectory();
            var unityPackageFolder = Path.Combine(currentDirectory, "unity_packages");
            loggerRedirect.LogInformation($"Using Unity Package folder `{unityPackageFolder}`");
            
            // Build the cache synchronously because ConfigureServices doesn't support async Task
            var initialCache = new RegistryCache(unityPackageFolder, url, loggerRedirect);
            initialCache.Build().GetAwaiter().GetResult();

            // Add the cache accessible from the services
            var singletonCache = new RegistryCacheSingleton(initialCache);
            services.AddSingleton(singletonCache);

            // Add the registry cache updater
            services.AddHostedService<RegistryCacheUpdater>();

            // what is that?
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.LogRequestHeaders(LoggerFactory);
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
                app.UseHttpsRedirection();
            }
            app.UseMvc();
        }
    }
}