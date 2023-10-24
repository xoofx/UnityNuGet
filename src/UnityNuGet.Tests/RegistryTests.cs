using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NUnit.Framework;
using static NuGet.Frameworks.FrameworkConstants;
using System.IO;

namespace UnityNuGet.Tests
{
    public class RegistryTests
    {
        [Test]
        public void Make_Sure_That_The_Order_In_The_Registry_Is_Respected()
        {
            var registry = Registry.GetInstance();
            var originalPackageNames = registry.Select(r => r.Key).ToArray();
            var sortedPackageNames = originalPackageNames.OrderBy(p => p).ToArray();

            Assert.AreEqual(sortedPackageNames, originalPackageNames);
        }

        [Test]
        public void Ensure_That_Packages_Already_Included_In_Net_Standard_Are_not_Included_In_The_Registry()
        {
            var registry = Registry.GetInstance();
            var packageNames = registry.Select(r => r.Key).Where(DotNetHelper.IsNetStandard20Assembly).ToArray();

            Assert.IsEmpty(packageNames);
        }

        [Test]
        public async Task CanParse_PackageWithRuntimes()
        {
            var logger = NullLogger.Instance;
            var cancellationToken = CancellationToken.None;

            var cache = new SourceCacheContext();
            var settings = Settings.LoadDefaultSettings(root: null);
            var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");

            // Fetch a package that has runtime overrides as described here: https://learn.microsoft.com/en-us/nuget/create-packages/supporting-multiple-target-frameworks
            var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(
                    new SourceRepository[] { repository },
                    new PackageIdentity("System.Security.Cryptography.ProtectedData", new NuGet.Versioning.NuGetVersion(6, 0, 0)),
                    new PackageDownloadContext(cache),
                    SettingsUtility.GetGlobalPackagesFolder(settings),
                    logger, cancellationToken);

            // Make sure we have runtime libraries
            var runtimeLibs = await RuntimeLibraries
                .GetSupportedRuntimeLibsAsync(downloadResult.PackageReader, CommonFrameworks.NetStandard20, logger)
                .ToListAsync();
            Assert.IsTrue(runtimeLibs.Any());

            // Make sure these runtime libraries are only for Windows
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var win = platformDefs.Find(UnityOs.Windows);
            foreach (var (file, os, cpu) in runtimeLibs)
            {
                Assert.AreEqual(platformDefs.Find(os, cpu), win);
            }

            // Get the lib files
            var versions = await downloadResult.PackageReader.GetLibItemsAsync(cancellationToken);
            var closestVersions = NuGetHelper.GetClosestFrameworkSpecificGroups(
                versions,
                new RegistryTargetFramework[]
                {
                    new RegistryTargetFramework
                    {
                        Framework = CommonFrameworks.NetStandard20,
                    },
                });
            var libFiles = closestVersions
                .Single()
                .Item1.Items
                .Select(i => Path.GetFileName(i))
                .ToHashSet();

            // Make sure the runtime files fully replace the lib files (note that this is generally not a requirement)
            var runtimeFiles = runtimeLibs
                .Select(l => Path.GetFileName(l.file))
                .ToHashSet();
            Assert.IsTrue(libFiles.SetEquals(runtimeFiles));
        }

        [Test]
        public async Task Ensure_Min_Version_Is_Correct_Ignoring_Analyzers_And_Native_Libs()
        {
            var registry = Registry.GetInstance();

            var logger = NullLogger.Instance;
            var cancellationToken = CancellationToken.None;

            var cache = new SourceCacheContext();
            var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = await repository.GetResourceAsync<PackageMetadataResource>();

            var nuGetFrameworks = new RegistryTargetFramework[] { new RegistryTargetFramework { Framework = CommonFrameworks.NetStandard20 } };

            var excludedPackages = new string[] {
                // All versions target "Any" and not .netstandard2.0 / 2.1
                @"AWSSDK.CognitoIdentity",
                // All versions target "Any" and not .netstandard2.0 / 2.1
                @"AWSSDK.CognitoIdentityProvider",
                // All versions target "Any" and not .netstandard2.0 / 2.1
                @"AWSSDK.S3",
                // All versions target "Any" and not .netstandard2.0 / 2.1
                @"AWSSDK.SecurityToken",
                // Although 2.x targets .netstandard2.0 it has an abandoned dependency (Remotion.Linq) that does not target .netstandard2.0.
                // 3.1.0 is set because 3.0.x only targets .netstandard2.1.
                @"Microsoft.EntityFrameworkCore.*",
                // Monomod Versions < 18.11.9.9 depend on System.Runtime.Loader which doesn't ship .netstandard2.0.
                @"MonoMod.Utils",
                @"MonoMod.RuntimeDetour",
                // Versions < 1.4.1 has dependencies on Microsoft.AspNetCore.*.
                @"StrongInject.Extensions.DependencyInjection",
                // Versions < 4.6.0 in theory supports .netstandard2.0 but it doesn't have a lib folder with assemblies and it makes it fail.
                @"System.Private.ServiceModel",
                // Versions < 0.8.6 depend on LiteGuard, a deprecated dependency.
                @"Telnet"
            };

            var excludedPackagesRegex = new Regex(@$"^{string.Join('|', excludedPackages)}$");

            foreach (var registryKvp in registry.Where(r => !r.Value.Analyzer && !r.Value.Ignored))
            {
                var packageId = registryKvp.Key;

                if (excludedPackagesRegex.IsMatch(packageId))
                {
                    continue;
                }

                var versionRange = registryKvp.Value.Version;

                var dependencyPackageMetas = await resource.GetMetadataAsync(
                    packageId,
                    includePrerelease: false,
                    includeUnlisted: false,
                    cache,
                    logger,
                    cancellationToken);

                var packageIdentity = NuGetHelper.GetMinimumCompatiblePackageIdentity(dependencyPackageMetas, nuGetFrameworks, includeAny: false);

                if (packageIdentity != null)
                {
                    Assert.AreEqual(packageIdentity.Version, versionRange.MinVersion, $"Package {packageId}");
                }
                else
                {
                    var settings = Settings.LoadDefaultSettings(root: null);

                    var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(
                            new SourceRepository[] { repository },
                            new PackageIdentity(registryKvp.Key, registryKvp.Value.Version.MinVersion),
                            new PackageDownloadContext(cache),
                            SettingsUtility.GetGlobalPackagesFolder(settings),
                            logger, cancellationToken);

                    var hasNativeLib = await NativeLibraries.GetSupportedNativeLibsAsync(downloadResult.PackageReader, logger).AnyAsync();

                    if (hasNativeLib)
                    {
                        continue;
                    }
                    else
                    {
                        Assert.Fail(packageId);
                    }
                }
            }
        }
    }
}
