using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NUnit.Framework;
using static NuGet.Frameworks.FrameworkConstants;

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

            Assert.That(originalPackageNames, Is.EqualTo(sortedPackageNames));
        }

        [Test]
        public void Ensure_That_Packages_Already_Included_In_Net_Standard_Are_not_Included_In_The_Registry()
        {
            var registry = Registry.GetInstance();
            var packageNames = registry.Select(r => r.Key).Where(DotNetHelper.IsNetStandard20Assembly).ToArray();

            Assert.That(packageNames, Is.Empty);
        }

        [Test]
        public async Task CanParse_PackageWithRuntimes()
        {
            var logger = new NuGetConsoleTestLogger();
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
            Assert.That(runtimeLibs, Is.Not.Empty);

            // Make sure these runtime libraries are only for Windows
            var platformDefs = PlatformDefinition.CreateAllPlatforms();
            var win = platformDefs.Find(UnityOs.Windows);
            foreach (var (file, os, cpu) in runtimeLibs)
            {
                Assert.That(platformDefs.Find(os, cpu), Is.EqualTo(win));
            }

            // Get the lib files
            var versions = await downloadResult.PackageReader.GetLibItemsAsync(cancellationToken);
            var closestVersions = NuGetHelper.GetClosestFrameworkSpecificGroups(
                versions,
                new RegistryTargetFramework[]
                {
                    new()
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
            Assert.That(libFiles.SetEquals(runtimeFiles), Is.True);
        }

        [Test]
        public async Task Ensure_Min_Version_Is_Correct_Ignoring_Analyzers_And_Native_Libs()
        {
            var registry = Registry.GetInstance();

            var logger = new NuGetConsoleTestLogger();
            var cancellationToken = CancellationToken.None;

            var cache = new SourceCacheContext();
            var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = await repository.GetResourceAsync<PackageMetadataResource>();

            var nuGetFrameworks = new RegistryTargetFramework[] { new() { Framework = CommonFrameworks.NetStandard20 } };

            var excludedPackages = new string[] {
                // All versions target "Any" and not .netstandard2.0 / 2.1
                // It has too many versions, the minimum version is lifted so as not to process so many versions
                @"AWSSDK.*",
                // It has too many versions, the minimum version is lifted so as not to process so many versions
                @"CSharpFunctionalExtensions",
                // Some versions between 5.6.4 and 6.3.0 doesn't ship .netstandard2.0.
                @"Elasticsearch.Net",
                // It has too many versions, the minimum version is lifted so as not to process so many versions
                @"Google.Apis.AndroidPublisher.v3",
                // Versions prior to 1.11.24 depend on System.Xml.XPath.XmlDocument which does not target .netstandard2.0
                @"HtmlAgilityPack",
                // Although 2.x targets .netstandard2.0 it has an abandoned dependency (Remotion.Linq) that does not target .netstandard2.0.
                // 3.1.0 is set because 3.0.x only targets .netstandard2.1.
                @"Microsoft.EntityFrameworkCore.*",
                // Monomod Versions < 18.11.9.9 depend on System.Runtime.Loader which doesn't ship .netstandard2.0.
                @"MonoMod.Utils",
                @"MonoMod.RuntimeDetour",
                // Versions < 2.0.0 depend on NAudio which doesn't ship .netstandard2.0.
                @"MumbleSharp",
                // Versions < 3.2.1 depend on Nullable which doesn't ship .netstandard2.0.
                @"Serilog.Expressions",
                // Versions < 1.4.1 has dependencies on Microsoft.AspNetCore.*.
                @"StrongInject.Extensions.DependencyInjection",
                // Versions < 4.6.0 in theory supports .netstandard2.0 but it doesn't have a lib folder with assemblies and it makes it fail.
                @"System.Private.ServiceModel",
                // Versions < 0.8.6 depend on LiteGuard, a deprecated dependency.
                @"Telnet",
                // Version < 1.0.26 depends on Microsoft.Windows.Compatibility, this one has tons of dependencies that don't target .netstandard2.0. And one of them is System.Speech that doesn't work in Unity.
                @"Dapplo.Windows.Common",
                @"Dapplo.Windows.Input",
                @"Dapplo.Windows.Messages",
                @"Dapplo.Windows.User32",
                // It has too many versions, the minimum version is lifted so as not to process so many versions
                @"UnitsNet.*"
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
                    Assert.That(versionRange!.MinVersion, Is.EqualTo(packageIdentity.Version), $"Package {packageId}");
                }
                else
                {
                    var settings = Settings.LoadDefaultSettings(root: null);

                    var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(
                            new SourceRepository[] { repository },
                            new PackageIdentity(registryKvp.Key, registryKvp.Value.Version!.MinVersion),
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

        [Test]
        public async Task Ensure_Do_Not_Exceed_The_Maximum_Number_Of_Allowed_Versions()
        {
            const int maxAllowedVersions = 100;

            var registry = Registry.GetInstance();

            var logger = new NuGetConsoleTestLogger();
            var cancellationToken = CancellationToken.None;

            var cache = new SourceCacheContext();
            var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = await repository.GetResourceAsync<PackageMetadataResource>();

            List<(string packageId, int versionCount)> packages = [];

            foreach (var registryKvp in registry.Where(r => !r.Value.Analyzer && !r.Value.Ignored))
            {
                var packageId = registryKvp.Key;

                var versionRange = registryKvp.Value.Version;

                var dependencyPackageMetas = await resource.GetMetadataAsync(
                    packageId,
                    includePrerelease: false,
                    includeUnlisted: false,
                    cache,
                    logger,
                    cancellationToken);

                var versions = dependencyPackageMetas.Where(v => versionRange!.Satisfies(v.Identity.Version)).ToArray();

                if (versions.Length > maxAllowedVersions)
                {
                    packages.Add((registryKvp.Key, versions.Length));
                }
            }

            StringBuilder stringBuilder = new();

            foreach (var (packageId, versionCount) in packages.OrderByDescending(p => p.versionCount))
            {
                stringBuilder.AppendLine($"{packageId} -> {versionCount}");
            }

            if (stringBuilder.Length == 0)
            {
                const bool trueConstant = true;

                Assert.That(trueConstant, Is.True);
            }
            else
            {
                Assert.Inconclusive(stringBuilder.ToString());
            }
        }
    }
}
