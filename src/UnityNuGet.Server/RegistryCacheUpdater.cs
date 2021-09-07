using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UnityNuGet.Server
{
    /// <summary>
    /// Update the RegistryCache at a regular interval
    /// </summary>
    internal sealed class RegistryCacheUpdater : BackgroundService
    {
        private readonly RegistryCacheSingleton _currentRegistryCache;
        private readonly ILogger _logger;
        private readonly RegistryOptions _registryOptions;

        public RegistryCacheUpdater(RegistryCacheSingleton currentRegistryCache, ILogger<RegistryCacheUpdater> logger, IOptions<RegistryOptions> registryOptionsAccessor)
        {
            _currentRegistryCache = currentRegistryCache;
            _logger = logger;
            _registryOptions = registryOptionsAccessor.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Starting to update RegistryCache");

                    var previousRegistryCache = _currentRegistryCache.Instance;
                    Debug.Assert(previousRegistryCache != null);
                    var newRegistryCache = new RegistryCache(previousRegistryCache);
                    await newRegistryCache.Build();

                    if (newRegistryCache.HasErrors)
                    {
                        _logger.LogInformation("RegistryCache not updated due to errors. See previous logs");
                    }
                    else
                    {
                        // Update the registry cache in the services
                        _currentRegistryCache.Instance = newRegistryCache;

                        _logger.LogInformation("RegistryCache successfully updated");
                    }

                    await Task.Delay((int)_registryOptions.UpdateInterval.TotalMilliseconds, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while building a new registry cache. Reason: {ex}");
            }
        }
    }
}
