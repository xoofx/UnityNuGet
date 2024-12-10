using System;
using System.Linq;
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
    internal sealed class RegistryCacheUpdater(Registry registry, RegistryCacheReport registryCacheReport, RegistryCacheSingleton currentRegistryCache, ILogger<RegistryCacheUpdater> logger, IOptions<RegistryOptions> registryOptionsAccessor) : BackgroundService
    {
        private readonly Registry _registry = registry;
        private readonly RegistryCacheReport _registryCacheReport = registryCacheReport;
        private readonly RegistryCacheSingleton _currentRegistryCache = currentRegistryCache;
        private readonly ILogger _logger = logger;
        private readonly RegistryOptions _registryOptions = registryOptionsAccessor.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Starting to update RegistryCache");

                    _registryCacheReport.Start();

                    var newRegistryCache = new RegistryCache(_registry, _currentRegistryCache.UnityPackageFolder!, _currentRegistryCache.ServerUri!, _registryOptions.UnityScope!, _registryOptions.MinimumUnityVersion!, _registryOptions.PackageNameNuGetPostFix!, _registryOptions.TargetFrameworks!, _currentRegistryCache.NuGetRedirectLogger!)
                    {
                        Filter = _registryOptions.Filter,
                        // Update progress
                        OnProgress = (current, total) =>
                        {
                            _currentRegistryCache.ProgressTotalPackageCount = total;
                            _currentRegistryCache.ProgressPackageIndex = current;
                        },
                        OnInformation = _registryCacheReport.AddInformation,
                        OnWarning = _registryCacheReport.AddWarning,
                        OnError = _registryCacheReport.AddError
                    };

                    await newRegistryCache.Build();

                    if (_registryCacheReport.ErrorMessages.Any())
                    {
                        _logger.LogInformation("RegistryCache not updated due to errors. See previous logs");
                    }
                    else
                    {
                        // Update the registry cache in the services
                        _currentRegistryCache.Instance = newRegistryCache;

                        _logger.LogInformation("RegistryCache successfully updated");
                    }

                    _registryCacheReport.Complete();

                    await Task.Delay((int)_registryOptions.UpdateInterval.TotalMilliseconds, stoppingToken);
                }
            }
            catch (TaskCanceledException)
            {
                string message = "RegistryCache update canceled";

                _logger.LogInformation("{Message}", message);

                _registryCacheReport.AddInformation($"{message}.");
                _registryCacheReport.Complete();
            }
            catch (Exception ex)
            {
                string message = "Error while building a new registry cache";

                _logger.LogError(ex, "{Message}", message);

                _registryCacheReport.AddError($"{message}. Reason: {ex}");
                _registryCacheReport.Complete();
            }
        }
    }
}
