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
    public class RegistryCacheInitializer(IConfiguration configuration, IHostEnvironment hostEnvironment, ILoggerFactory loggerFactory, IOptions<RegistryOptions> registryOptionsAccessor, RegistryCacheSingleton registryCacheSingleton) : IHostedService
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
        private readonly ILoggerFactory _loggerFactory = loggerFactory;
        private readonly RegistryOptions _registryOptions = registryOptionsAccessor.Value;
        private readonly RegistryCacheSingleton _registryCacheSingleton = registryCacheSingleton;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ILogger logger = _loggerFactory.CreateLogger("NuGet");
            var loggerRedirect = new NuGetRedirectLogger(logger);

            Uri uri = _registryOptions.RootHttpUrl!;

            bool isDevelopment = _hostEnvironment.IsDevelopment();

            if (isDevelopment)
            {
                string? urls = _configuration[WebHostDefaults.ServerUrlsKey];

                // Select HTTPS in production, HTTP in development
                string url = (urls?.Split(';').FirstOrDefault(x => !x.StartsWith("https"))) ?? throw new InvalidOperationException($"Unable to find a proper server URL from `{urls}`. Expecting a `http://...` URL in development");

                // https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_running_in_container-and-dotnet_running_in_containers
                bool runningInContainer = _configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER");

                uri = new Uri(runningInContainer ? url.Replace("+", "localhost") : url);
            }

            // Get the current directory from registry options (prepend binary folder in dev)
            string unityPackageFolder;

            if (Path.IsPathRooted(_registryOptions.RegistryFilePath))
            {
                unityPackageFolder = _registryOptions.RootPersistentFolder!;
            }
            else
            {
                string currentDirectory;

                if (isDevelopment)
                {
                    currentDirectory = Path.GetDirectoryName(AppContext.BaseDirectory)!;
                }
                else
                {
                    currentDirectory = Directory.GetCurrentDirectory();
                }

                unityPackageFolder = Path.Combine(currentDirectory, _registryOptions.RootPersistentFolder!);
            }

            logger.LogInformation("Using Unity Package folder `{UnityPackageFolder}`", unityPackageFolder);

            // Add the cache accessible from the services
            _registryCacheSingleton.UnityPackageFolder = unityPackageFolder;
            _registryCacheSingleton.ServerUri = uri;
            _registryCacheSingleton.NuGetRedirectLogger = loggerRedirect;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
