using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UnityNuGet.Server
{
    /// <summary>
    /// Update the RegistryCache at a regular internal (default is 10min)
    /// </summary>
    internal sealed class RegistryCacheUpdater : IHostedService, IDisposable
    {
        private readonly RegistryCacheSingleton _currentRegistryCache;
        private readonly ILogger _logger;
        private Timer _timer;
        // Update the RegistryCache very 10min
        private static readonly TimeSpan DefaultIntervalUpdate = TimeSpan.FromMinutes(10);

        public RegistryCacheUpdater(RegistryCacheSingleton currentRegistryCache, ILogger<RegistryCacheUpdater> logger)
        {
            _currentRegistryCache = currentRegistryCache;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(DoWork, null, DefaultIntervalUpdate, DefaultIntervalUpdate);
            return Task.CompletedTask;
        }

        private async void DoWork(object state)
        {
            try
            {
                _logger.LogInformation("Starting to update RegistryCache");

                var previousRegistryCache = _currentRegistryCache.Instance;
                Debug.Assert(previousRegistryCache != null);
                var newRegistryCache = new RegistryCache(previousRegistryCache.RootUnityPackageFolder, previousRegistryCache.RootHttpUrl, previousRegistryCache.Logger);
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
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while building a new registry cache. Reason: {ex}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}