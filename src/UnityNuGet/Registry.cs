using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace UnityNuGet
{
    /// <summary>
    /// Loads the `registry.json` file at startup
    /// </summary>
    public sealed class Registry(IHostEnvironment hostEnvironment, ILoggerFactory loggerFactory, IOptions<RegistryOptions> registryOptionsAccessor) : IHostedService, IReadOnlyCollection<KeyValuePair<string, RegistryEntry>>, IEnumerable<KeyValuePair<string, RegistryEntry>>
    {
        private IDictionary<string, RegistryEntry>? _data;

        private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
        private readonly ILoggerFactory _loggerFactory = loggerFactory;
        private readonly RegistryOptions _registryOptions = registryOptionsAccessor.Value;

        public int Count => _data!.Count;

        IEnumerator IEnumerable.GetEnumerator() => _data!.GetEnumerator();

        public IEnumerator<KeyValuePair<string, RegistryEntry>> GetEnumerator() => _data!.GetEnumerator();

        public bool TryGetValue(string key, out RegistryEntry value) => _data!.TryGetValue(key, out value!);

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            string registryFilePath;

            if (Path.IsPathRooted(_registryOptions.RegistryFilePath))
            {
                registryFilePath = _registryOptions.RegistryFilePath;
            }
            else
            {
                bool isDevelopment = _hostEnvironment.IsDevelopment();

                string currentDirectory;

                if (isDevelopment)
                {
                    currentDirectory = Path.GetDirectoryName(AppContext.BaseDirectory)!;
                }
                else
                {
                    currentDirectory = Directory.GetCurrentDirectory();
                }

                registryFilePath = Path.Combine(currentDirectory, _registryOptions.RegistryFilePath!);
            }

            ILogger logger = _loggerFactory.CreateLogger("NuGet");

            logger.LogInformation("Using Unity registry file `{UnityRegistryFile}`", registryFilePath);

            string json = await File.ReadAllTextAsync(registryFilePath, cancellationToken);

            IDictionary<string, RegistryEntry> data = JsonConvert.DeserializeObject<IDictionary<string, RegistryEntry>>(json, JsonCommonExtensions.Settings)!;

            _data = new Dictionary<string, RegistryEntry>(data, StringComparer.OrdinalIgnoreCase);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
