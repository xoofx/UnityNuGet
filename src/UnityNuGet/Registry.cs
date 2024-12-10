using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace UnityNuGet
{
    /// <summary>
    /// Loads the `registry.json` file at startup
    /// </summary>
    public sealed class Registry(IOptions<RegistryOptions> registryOptionsAccessor) : IHostedService, IReadOnlyCollection<KeyValuePair<string, RegistryEntry>>, IEnumerable<KeyValuePair<string, RegistryEntry>>
    {
        private IDictionary<string, RegistryEntry>? _data;

        private readonly RegistryOptions registryOptions = registryOptionsAccessor.Value;

        public int Count => _data!.Count;

        IEnumerator IEnumerable.GetEnumerator() => _data!.GetEnumerator();

        public IEnumerator<KeyValuePair<string, RegistryEntry>> GetEnumerator() => _data!.GetEnumerator();

        public bool TryGetValue(string key, out RegistryEntry value) => _data!.TryGetValue(key, out value!);

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            string registryFilePath;

            if (Path.IsPathRooted(registryOptions.RegistryFilePath))
            {
                registryFilePath = registryOptions.RegistryFilePath;
            }
            else
            {
                registryFilePath = Path.Combine(Directory.GetCurrentDirectory(), registryOptions.RegistryFilePath!);
            }

            string json = await File.ReadAllTextAsync(registryFilePath, cancellationToken);

            _data = JsonConvert.DeserializeObject<IDictionary<string, RegistryEntry>>(json, JsonCommonExtensions.Settings)!;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
