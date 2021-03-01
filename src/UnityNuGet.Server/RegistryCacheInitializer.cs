using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UnityNuGet.Server
{
    public class RegistryCacheInitializer : IHostedService
    {
        private readonly IConfiguration configuration;
        private readonly IHostEnvironment hostEnvironment;
        private readonly ILoggerFactory loggerFactory;
        private readonly RegistryCacheSingleton registryCacheSingleton;

        public RegistryCacheInitializer(IConfiguration configuration, IHostEnvironment hostEnvironment, ILoggerFactory loggerFactory, RegistryCacheSingleton registryCacheSingleton)
        {
            this.configuration = configuration;
            this.hostEnvironment = hostEnvironment;
            this.loggerFactory = loggerFactory;
            this.registryCacheSingleton = registryCacheSingleton;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var loggerRedirect = new NuGetRedirectLogger(loggerFactory.CreateLogger("NuGet"));

            string url = "https://unitynuget-registry.azurewebsites.net/";

            bool isDevelopment = hostEnvironment.IsDevelopment();
            if (isDevelopment)
            {
                var urls = configuration[WebHostDefaults.ServerUrlsKey];

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
            await initialCache.Build();

            // Add the cache accessible from the services
            registryCacheSingleton.Instance = initialCache;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
