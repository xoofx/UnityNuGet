using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UnityNuGet.Server
{
    public class RegistryCacheInitializer : IHostedService
    {
        private readonly IConfiguration configuration;
        private readonly IHostEnvironment hostEnvironment;
        private readonly ILoggerFactory loggerFactory;
        private readonly RegistryOptions registryOptions;
        private readonly RegistryCacheSingleton registryCacheSingleton;

        public RegistryCacheInitializer(IConfiguration configuration, IHostEnvironment hostEnvironment, ILoggerFactory loggerFactory, IOptions<RegistryOptions> registryOptionsAccessor, RegistryCacheSingleton registryCacheSingleton)
        {
            this.configuration = configuration;
            this.hostEnvironment = hostEnvironment;
            this.loggerFactory = loggerFactory;
            registryOptions = registryOptionsAccessor.Value;
            this.registryCacheSingleton = registryCacheSingleton;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var loggerRedirect = new NuGetRedirectLogger(loggerFactory.CreateLogger("NuGet"));

            Uri uri = registryOptions.RootHttpUrl;

            bool isDevelopment = hostEnvironment.IsDevelopment();
            if (isDevelopment)
            {
                var urls = configuration[WebHostDefaults.ServerUrlsKey];

                // Select HTTPS in production, HTTP in development
                var url = urls.Split(';').FirstOrDefault(x => !x.StartsWith("https"));
                if (url == null)
                {
                    throw new InvalidOperationException($"Unable to find a proper server URL from `{urls}`. Expecting a `http://...` URL in development");
                }

                uri = new Uri(url);
            }

            // Get the current directory from registry options (prepend binary folder in dev)
            string unityPackageFolder;

            if (isDevelopment)
            {
                var currentDirectory = Path.GetDirectoryName(typeof(Startup).Assembly.Location);
                unityPackageFolder = Path.Combine(currentDirectory, new DirectoryInfo(registryOptions.RootPersistentFolder).Name);
            }
            else
            {
                if (Path.IsPathRooted(registryOptions.RootPersistentFolder))
                {
                    unityPackageFolder = registryOptions.RootPersistentFolder;
                }
                else
                {
                    unityPackageFolder = Path.Combine(Directory.GetCurrentDirectory(), registryOptions.RootPersistentFolder);
                }
            }
            loggerRedirect.LogInformation($"Using Unity Package folder `{unityPackageFolder}`");

            // Add the cache accessible from the services
            registryCacheSingleton.UnityPackageFolder = unityPackageFolder;
            registryCacheSingleton.ServerUri = uri;
            registryCacheSingleton.NuGetRedirectLogger = loggerRedirect;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
