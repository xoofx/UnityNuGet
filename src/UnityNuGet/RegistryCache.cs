using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NUglify.Html;
using UnityNuGet.Npm;

namespace UnityNuGet
{
    /// <summary>
    /// Main class used to build the unity packages and create the NPM object responses.
    /// </summary>
    public class RegistryCache
    {
        public static readonly bool IsRunningOnAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

        // Change this version number if the content of the packages are changed by an update of this class
        private const string CurrentRegistryVersion = "1.8.0";

        private static readonly Encoding s_utf8EncodingNoBom = new UTF8Encoding(false, false);
        private readonly Registry _registry;
        private readonly string _rootPersistentFolder;
        private readonly Uri _rootHttpUri;
        private readonly string _unityScope;
        private readonly string _minimumUnityVersion;
        private readonly string _packageNameNuGetPostFix;
        private readonly RegistryTargetFramework[] _targetFrameworks;
        private readonly ILogger _logger;
        private readonly ISettings _settings;
        private readonly IEnumerable<SourceRepository> _sourceRepositories;
        private readonly SourceCacheContext _sourceCacheContext;
        private readonly NpmPackageRegistry _npmPackageRegistry;

        public RegistryCache(Registry registry, RegistryCache registryCache) : this(registry, registryCache._rootPersistentFolder, registryCache._rootHttpUri, registryCache._unityScope,
            registryCache._minimumUnityVersion, registryCache._packageNameNuGetPostFix, registryCache._targetFrameworks, registryCache._logger)
        { }

        public RegistryCache(Registry registry, string rootPersistentFolder, Uri rootHttpUri, string unityScope, string minimumUnityVersion,
            string packageNameNuGetPostFix, RegistryTargetFramework[] targetFrameworks, ILogger logger)
        {
            _registry = registry;
            _rootPersistentFolder = rootPersistentFolder ?? throw new ArgumentNullException(nameof(rootPersistentFolder));
            _rootHttpUri = rootHttpUri ?? throw new ArgumentNullException(nameof(rootHttpUri));
            _unityScope = unityScope ?? throw new ArgumentNullException(nameof(unityScope));
            _minimumUnityVersion = minimumUnityVersion ?? throw new ArgumentNullException(nameof(minimumUnityVersion));
            _packageNameNuGetPostFix = packageNameNuGetPostFix ?? throw new ArgumentNullException(nameof(packageNameNuGetPostFix));
            _targetFrameworks = targetFrameworks ?? throw new ArgumentNullException(nameof(targetFrameworks));

            if (!Directory.Exists(_rootPersistentFolder))
            {
                Directory.CreateDirectory(_rootPersistentFolder);
            }

            // Force NuGet packages to be in the same directory to avoid storage full on Azure.
            if (IsRunningOnAzure)
            {
                string nugetFolder = Path.Combine(_rootPersistentFolder, ".nuget");
                Environment.SetEnvironmentVariable("NUGET_PACKAGES", nugetFolder);
            }

            _settings = Settings.LoadDefaultSettings(root: null);
            var sourceRepositoryProvider = new SourceRepositoryProvider(new PackageSourceProvider(_settings), Repository.Provider.GetCoreV3());
            _sourceRepositories = sourceRepositoryProvider.GetRepositories();
            _logger = logger;

            // Initialize target framework
            foreach (RegistryTargetFramework registryTargetFramework in _targetFrameworks)
            {
                registryTargetFramework.Framework = NuGetFramework.Parse(registryTargetFramework.Name!);
            }

            _sourceCacheContext = new SourceCacheContext();
            _npmPackageRegistry = new NpmPackageRegistry();
        }

        /// <summary>
        /// Gets or sets a regex filter (contains) on the NuGet package, case insensitive. Default is null (no filter).
        /// </summary>
        /// <remarks>
        /// This property is used for testing purpose only
        /// </remarks>
        public string? Filter { get; set; }

        /// <summary>
        /// OnProgress event (number of packages initialized, total number of packages)
        /// </summary>
        public Action<int, int>? OnProgress { get; set; }

        /// <summary>
        /// OnInformation event (information message)
        /// </summary>
        public Action<string>? OnInformation { get; set; }

        /// <summary>
        /// OnWarning event (warning message)
        /// </summary>
        public Action<string>? OnWarning { get; set; }

        /// <summary>
        /// OnError event (error message)
        /// </summary>
        public Action<string>? OnError { get; set; }

        /// <summary>
        /// Get all packages registered.
        /// </summary>
        /// <returns>A list of packages registered</returns>
        public NpmPackageListAllResponse All()
        {
            return _npmPackageRegistry.ListedPackageInfos;
        }

        /// <summary>
        /// Get a specific package for the specified package id.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns>A npm package or null if not found</returns>
        public NpmPackage? GetPackage(string packageId)
        {
            ArgumentNullException.ThrowIfNull(packageId);
            _npmPackageRegistry.Packages.TryGetValue(packageId, out NpmPackage? package);
            return package;
        }

        /// <summary>
        /// Gets the path for the specified package file to download
        /// </summary>
        /// <param name="packageFileName">The name of the package to download</param>
        /// <returns>The file path of the package on the disk</returns>
        public string GetPackageFilePath(string packageFileName)
        {
            ArgumentNullException.ThrowIfNull(packageFileName);
            packageFileName = packageFileName.Replace("/", packageFileName.Replace(".", string.Empty));
            string packageFilePath = Path.Combine(_rootPersistentFolder, packageFileName);
            return packageFilePath;
        }

        /// <summary>
        /// Build the registry cache.
        /// </summary>
        public async Task Build()
        {
            try
            {
                await BuildInternal();
            }
            catch (Exception ex)
            {
                LogError($"Unexpected error {ex}");
            }
        }

        private async Task<IEnumerable<IPackageSearchMetadata>?> GetMetadataFromSources(string packageName, bool includeUnlisted, bool includePrerelease)
        {
            foreach (SourceRepository source in _sourceRepositories)
            {
                PackageMetadataResource packageMetadataResource = source.GetResource<PackageMetadataResource>();

                IEnumerable<IPackageSearchMetadata> result = await packageMetadataResource.GetMetadataAsync(packageName, includePrerelease, includeUnlisted, _sourceCacheContext, _logger, CancellationToken.None);

                if (result.Any())
                {
                    return result;
                }
            }

            return null;
        }

        private async Task<DownloadResourceResult> GetPackageDownloadResourceResult(PackageIdentity packageIdentity)
        {
            return await PackageDownloader.GetDownloadResourceResultAsync(
                _sourceRepositories,
                packageIdentity,
                new PackageDownloadContext(_sourceCacheContext),
                SettingsUtility.GetGlobalPackagesFolder(_settings),
                _logger, CancellationToken.None);
        }

        /// <summary>
        /// For each package in our registry.json, query NuGet, extract package metadata, and convert them to unity packages.
        /// </summary>
        private async Task BuildInternal()
        {
            string versionPath = Path.Combine(_rootPersistentFolder, "version.txt");
            bool forceUpdate = !File.Exists(versionPath) || await File.ReadAllTextAsync(versionPath) != CurrentRegistryVersion;
            if (forceUpdate)
            {
                LogInformation($"Registry version changed to {CurrentRegistryVersion} - Regenerating all packages");

                // Clear the cache entirely
                _npmPackageRegistry.Reset();
            }

            Regex? regexFilter = !string.IsNullOrEmpty(Filter) ? new Regex(Filter, RegexOptions.IgnoreCase) : null;
            if (regexFilter != null)
            {
                LogInformation($"Filtering with regex: {Filter}");
            }

            Action<int, int>? onProgress = OnProgress;

            string globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(_settings);

            int progressCount = 0;
            foreach (KeyValuePair<string, RegistryEntry> packageDesc in _registry)
            {
                string packageName = packageDesc.Key;
                RegistryEntry packageEntry = packageDesc.Value;

                // Log progress count
                onProgress?.Invoke(++progressCount, _registry.Count);

                // A package entry is ignored but allowed in the registry (case of Microsoft.CSharp)
                if (packageEntry.Ignored || (regexFilter != null && !regexFilter.IsMatch(packageName)))
                {
                    continue;
                }

                string packageId = packageName.ToLowerInvariant();
                string npmPackageId = $"{_unityScope}.{packageId}";
                NpmPackageCacheEntry? cacheEntry = null;
                NpmPackage? npmPackage = null;
                NpmPackageInfo? npmPackageInfo = null;

                if (!forceUpdate && !_npmPackageRegistry.Packages.TryGetValue(npmPackageId, out npmPackage))
                {
                    if (TryReadPackageCacheEntry(packageId, out cacheEntry))
                    {
                        npmPackage = cacheEntry.Package!;
                        npmPackageInfo = cacheEntry.Info!;
                        _npmPackageRegistry.AddPackage(cacheEntry, packageEntry.Listed);
                    }
                }

                IEnumerable<IPackageSearchMetadata>? packageMetaIt = await GetMetadataFromSources(packageName, packageEntry.IncludeUnlisted, packageEntry.IncludePrerelease);
                IPackageSearchMetadata[] packageMetas = packageMetaIt != null ? packageMetaIt.ToArray() : [];
                foreach (IPackageSearchMetadata? packageMeta in packageMetas)
                {
                    PackageIdentity packageIdentity = packageMeta.Identity;
                    // Update latest version
                    NuGetVersion currentVersion = packageIdentity.Version;
                    string npmCurrentVersion = GetNpmVersion(currentVersion);

                    if (packageEntry.Version == null || !packageEntry.Version.Satisfies(packageMeta.Identity.Version))
                    {
                        continue;
                    }

                    // If the package id is cached already, we don't need to generate it again
                    if (npmPackage != null && npmPackage.Versions.TryGetValue(npmCurrentVersion, out NpmPackageVersion? existingVersion))
                    {
                        // If the package tgz exists, we don't need to regenerate it again
                        var packageTgz = new FileInfo(GetUnityPackagePath(packageIdentity, existingVersion));
                        var packageSha1 = new FileInfo(GetUnityPackageSha1Path(packageIdentity, existingVersion));

                        if (packageTgz.Exists && packageTgz.Length > 0 && packageSha1.Exists && packageSha1.Length > 0)
                        {
                            continue;
                        }
                    }

                    var resolvedDependencyGroups = NuGetHelper.GetCompatiblePackageDependencyGroups(packageMeta.DependencySets, _targetFrameworks).ToList();

                    if (!packageEntry.Analyzer && resolvedDependencyGroups.Count == 0)
                    {
                        using DownloadResourceResult downloadResult = await GetPackageDownloadResourceResult(packageIdentity);

                        bool hasNativeLib = await NativeLibraries.GetSupportedNativeLibsAsync(downloadResult.PackageReader, _logger).AnyAsync();

                        if (!hasNativeLib)
                        {
                            LogWarning($"The package `{packageIdentity}` doesn't support `{string.Join(",", _targetFrameworks.Select(x => x.Name))}`");
                            continue;
                        }
                    }

                    npmPackage ??= new NpmPackage();
                    npmPackageInfo ??= new NpmPackageInfo();

                    bool update = !npmPackage.DistTags.TryGetValue("latest", out string? latestVersion)
                                 || (currentVersion > NuGetVersion.Parse(latestVersion))
                                 || forceUpdate;

                    if (update)
                    {
                        npmPackage.DistTags["latest"] = npmCurrentVersion;

                        npmPackageInfo.Versions.Clear();
                        npmPackageInfo.Versions[npmCurrentVersion] = "latest";

                        npmPackage.Id = npmPackageId;
                        npmPackage.License = packageMeta.LicenseMetadata?.License ?? packageMeta.LicenseUrl?.ToString();

                        npmPackage.Name = npmPackageId;
                        npmPackageInfo.Name = npmPackageId;

                        npmPackage.Description = packageMeta.Description;
                        npmPackageInfo.Description = packageMeta.Description;

                        npmPackageInfo.Author = packageMeta.Authors;
                        if (packageMeta.Owners != null)
                        {
                            npmPackageInfo.Maintainers.Clear();
                            npmPackageInfo.Maintainers.AddRange(SplitCommaSeparatedString(packageMeta.Owners));
                        }

                        if (packageMeta.Tags != null)
                        {
                            npmPackageInfo.Keywords.Clear();
                            npmPackageInfo.Keywords.Add("nuget");
                            npmPackageInfo.Keywords.AddRange(SplitCommaSeparatedString(packageMeta.Tags));
                        }
                    }

                    if (cacheEntry is null)
                    {
                        cacheEntry = new NpmPackageCacheEntry { Package = npmPackage, Info = npmPackageInfo };
                        _npmPackageRegistry.AddPackage(cacheEntry, packageEntry.Listed);
                    }

                    var npmVersion = new NpmPackageVersion
                    {
                        Id = $"{npmPackageId}@{npmCurrentVersion}",
                        Version = npmCurrentVersion,
                        Name = npmPackageId,
                        Description = packageMeta.Description,
                        Author = npmPackageInfo.Author,
                        DisplayName = packageMeta.Title + _packageNameNuGetPostFix,
                        Repository = npmPackage.Repository
                    };
                    npmVersion.Distribution.Tarball = new Uri(_rootHttpUri, $"{npmPackage.Id}/-/{GetUnityPackageFileName(packageIdentity, npmVersion)}");
                    npmVersion.Unity = _minimumUnityVersion;
                    npmPackage.Versions[npmVersion.Version] = npmVersion;

                    bool hasDependencyErrors = false;
                    foreach (PackageDependencyGroup? resolvedDependencyGroup in resolvedDependencyGroups)
                    {
                        foreach (PackageDependency? deps in resolvedDependencyGroup.Packages)
                        {
                            if (DotNetHelper.IsNetStandard20Assembly(deps.Id))
                            {
                                continue;
                            }

                            PackageDependency resolvedDeps = deps;

                            if (!_registry.TryGetValue(deps.Id, out RegistryEntry? packageEntryDep))
                            {
                                LogError($"The package `{packageIdentity}` has a dependency on `{deps.Id}` which is not in the registry. You must add this dependency to the registry.json file.");
                                hasDependencyErrors = true;
                            }
                            else if (packageEntryDep.Ignored)
                            {
                                // A package that is ignored is not declared as an explicit dependency
                                continue;
                            }
                            else if (!deps.VersionRange.IsSubSetOrEqualTo(packageEntryDep.Version))
                            {
                                IEnumerable<IPackageSearchMetadata>? dependencyPackageMetaIt = await GetMetadataFromSources(deps.Id, packageEntryDep.IncludeUnlisted, packageEntryDep.IncludePrerelease);
                                IPackageSearchMetadata[] dependencyPackageMetas = dependencyPackageMetaIt != null ? dependencyPackageMetaIt.ToArray() : [];

                                PackageDependency? packageDependency = null;

                                foreach (IPackageSearchMetadata? dependencyPackageMeta in dependencyPackageMetas)
                                {
                                    IEnumerable<PackageDependencyGroup> dependencyResolvedDependencyGroups = NuGetHelper.GetCompatiblePackageDependencyGroups(dependencyPackageMeta.DependencySets, _targetFrameworks, includeAny: false);

                                    if (dependencyResolvedDependencyGroups.Any())
                                    {
                                        _registry.TryGetValue(dependencyPackageMeta.Identity.Id, out RegistryEntry? registryEntry);

                                        NuGetVersion registryMinimumVersion = registryEntry?.Version?.MinVersion!;

                                        NuGetVersion newVersion = registryMinimumVersion > dependencyPackageMeta.Identity.Version ? registryMinimumVersion : dependencyPackageMeta.Identity.Version;

                                        packageDependency = new PackageDependency(dependencyPackageMeta.Identity.Id, new VersionRange(newVersion));

                                        break;
                                    }
                                }

                                if (packageDependency != null)
                                {
                                    if (deps.VersionRange.MinVersion! > packageDependency.VersionRange.MinVersion!)
                                    {
                                        packageDependency = new PackageDependency(packageDependency.Id, deps.VersionRange);
                                        resolvedDeps = packageDependency;
                                        continue;
                                    }
                                    else
                                    {
                                        resolvedDeps = packageDependency;
                                        LogWarning($"Overwriting dependency `{deps.Id} {deps.VersionRange}` of the package `{packageIdentity}` in favor of `{resolvedDeps.Id} {resolvedDeps.VersionRange}` because it is not compatible with {string.Join(",", _targetFrameworks.Select(x => x.Name))}.");
                                    }
                                }
                                else
                                {
                                    LogWarning($"The version range `{deps.VersionRange}` for the dependency `{deps.Id}` for the package `{packageIdentity}` doesn't match the range allowed from the registry.json: `{packageEntryDep.Version}`");
                                    hasDependencyErrors = true;
                                    continue;
                                }
                            }

                            // Otherwise add the package as a dependency
                            string depsId = resolvedDeps.Id.ToLowerInvariant();
                            string key = $"{_unityScope}.{depsId}";
                            if (!npmVersion.Dependencies.ContainsKey(key))
                            {
                                npmVersion.Dependencies.Add(key, GetNpmVersion(resolvedDeps.VersionRange.MinVersion!));
                            }
                        }
                    }

                    // If we don't have any dependencies error, generate the package
                    if (!hasDependencyErrors)
                    {
                        bool packageConverted = await ConvertNuGetToUnityPackageIfDoesNotExist(packageIdentity, npmPackageInfo, npmVersion, packageMeta, forceUpdate, packageEntry);
                        npmPackage.Time[npmCurrentVersion] = packageMeta.Published?.UtcDateTime ?? GetUnityPackageFileInfo(packageIdentity, npmVersion).CreationTimeUtc;

                        // Copy repository info if necessary
                        if (update)
                        {
                            npmPackage.Repository = npmVersion.Repository?.Clone();
                        }

                        // Update the cache entry
                        await WritePackageCacheEntry(packageId, cacheEntry);

                        if (packageConverted && IsRunningOnAzure)
                        {
                            string localPackagePath = Path.Combine(globalPackagesFolder, packageIdentity.Id.ToLowerInvariant(), packageIdentity.Version.ToString());

                            if (Directory.Exists(localPackagePath))
                            {
                                Directory.Delete(localPackagePath, true);
                            }
                            else
                            {
                                LogWarning($"The NuGet package cache folder could not be deleted because it does not exist: {localPackagePath}");
                            }
                        }
                    }
                }
            }

            if (forceUpdate)
            {
                await File.WriteAllTextAsync(versionPath, CurrentRegistryVersion);
            }
        }

        // Unity only supports the SemVer format: https://docs.unity3d.com/6000.1/Documentation/Manual/upm-lifecycle.html
        internal static string GetNpmVersion(NuGetVersion currentVersion)
        {
            string npmCurrentVersion = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Patch}";

            if (currentVersion.IsPrerelease || currentVersion.Revision != 0)
            {
                StringBuilder stringBuilder = new();

                if (currentVersion.Revision != 0)
                {
                    stringBuilder.Append(currentVersion.Revision);
                }

                if (currentVersion.IsPrerelease)
                {
                    if (stringBuilder.Length > 0)
                    {
                        stringBuilder.Append('.');
                    }

                    stringBuilder.Append(currentVersion.Release);
                }

                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Insert(0, '-');
                }

                npmCurrentVersion += stringBuilder.ToString();
            }

            return npmCurrentVersion;
        }

        /// <summary>
        /// Converts a NuGet package to Unity package if not already
        /// </summary>
        private async Task<bool> ConvertNuGetToUnityPackageIfDoesNotExist
        (
            PackageIdentity identity,
            NpmPackageInfo npmPackageInfo,
            NpmPackageVersion npmPackageVersion,
            IPackageSearchMetadata packageMeta,
            bool forceUpdate,
            RegistryEntry packageEntry
        )
        {
            // If we need to force the update, we delete the previous package+sha1 files
            if (forceUpdate)
            {
                DeleteUnityPackage(identity, npmPackageVersion);
            }

            if (!IsUnityPackageValid(identity, npmPackageVersion) || !IsUnityPackageSha1Valid(identity, npmPackageVersion))
            {
                await ConvertNuGetPackageToUnity(identity, npmPackageInfo, npmPackageVersion, packageMeta, packageEntry);

                return true;
            }
            else
            {
                npmPackageVersion.Distribution.Shasum = await ReadUnityPackageSha1(identity, npmPackageVersion);

                return false;
            }
        }

        /// <summary>
        /// Converts a NuGet package to a Unity package.
        /// </summary>
        private async Task ConvertNuGetPackageToUnity
        (
            PackageIdentity identity,
            NpmPackageInfo npmPackageInfo,
            NpmPackageVersion npmPackageVersion,
            IPackageSearchMetadata packageMeta,
            RegistryEntry packageEntry
        )
        {
            string unityPackageFileName = GetUnityPackageFileName(identity, npmPackageVersion);
            string unityPackageFilePath = Path.Combine(_rootPersistentFolder, unityPackageFileName);

            LogInformation($"Converting NuGet package {identity} to Unity `{unityPackageFileName}`");

            using DownloadResourceResult downloadResult = await GetPackageDownloadResourceResult(identity);

            using PackageReaderBase packageReader = downloadResult.PackageReader;

            // Update Repository metadata if necessary
            RepositoryMetadata repoMeta = packageReader.NuspecReader.GetRepositoryMetadata();
            if (repoMeta != null && repoMeta.Url != null && repoMeta.Commit != null && repoMeta.Type != null)
            {
                npmPackageVersion.Repository = new NpmSourceRepository()
                {
                    Revision = repoMeta.Commit,
                    Type = repoMeta.Type,
                    Url = repoMeta.Url,
                };
            }
            else
            {
                npmPackageVersion.Repository = null;
            }

            try
            {
                using var memStream = new MemoryStream();

                using (FileStream outStream = File.Create(unityPackageFilePath))
                using (var gzoStream = new GZipOutputStream(outStream)
                {
                    ModifiedTime = packageMeta.Published?.UtcDateTime
                })
                using (var tarArchive = new TarOutputStream(gzoStream, Encoding.UTF8))
                {
                    // Select the framework version that is the closest or equal to the latest configured framework version
                    IEnumerable<FrameworkSpecificGroup> versions = await packageReader.GetLibItemsAsync(CancellationToken.None);

                    IEnumerable<(FrameworkSpecificGroup, RegistryTargetFramework)> closestVersions = NuGetHelper.GetClosestFrameworkSpecificGroups(versions, _targetFrameworks);

                    var collectedItems = new Dictionary<FrameworkSpecificGroup, HashSet<RegistryTargetFramework>>();

                    foreach ((FrameworkSpecificGroup item, RegistryTargetFramework targetFramework) in closestVersions)
                    {
                        if (!collectedItems.TryGetValue(item, out HashSet<RegistryTargetFramework>? frameworksPerGroup))
                        {
                            frameworksPerGroup = [];
                            collectedItems.Add(item, frameworksPerGroup);
                        }
                        frameworksPerGroup.Add(targetFramework);
                    }

                    if (!packageEntry.Analyzer && collectedItems.Count == 0)
                    {
                        throw new InvalidOperationException($"The package does not contain a compatible .NET assembly {string.Join(",", _targetFrameworks.Select(x => x.Name))}");
                    }

                    bool isPackageNetStandard21Assembly = DotNetHelper.IsNetStandard21Assembly(identity.Id);
                    bool hasMultiNetStandard = collectedItems.Count > 1;
                    bool hasOnlyNetStandard21 = collectedItems.Count == 1 && collectedItems.Values.First().All(x => x.Name == "netstandard2.1");

                    // https://learn.microsoft.com/en-us/nuget/api/registration-base-url-resource#catalog-entry
                    // Unlisted packages published value is set to 1900
                    DateTime modTime = packageMeta.IsListed ? packageMeta.Published?.DateTime ?? DateTime.UnixEpoch : DateTime.UnixEpoch;

                    if (isPackageNetStandard21Assembly)
                    {
                        LogInformation($"Package {identity.Id} is a system package for netstandard2.1 and will be only used for netstandard 2.0");
                    }

                    if (packageEntry.Analyzer)
                    {
                        IEnumerable<FrameworkSpecificGroup> packageFiles = await packageReader.GetItemsAsync(PackagingConstants.Folders.Analyzers, CancellationToken.None);

                        // https://learn.microsoft.com/en-us/nuget/guides/analyzers-conventions#analyzers-path-format
                        string[] analyzerFiles = packageFiles
                            .SelectMany(p => p.Items)
                            .Where(p => NuGetHelper.IsApplicableUnitySupportedRoslynVersionFolder(p) && (NuGetHelper.IsApplicableAnalyzer(p) || NuGetHelper.IsApplicableAnalyzerResource(p)))
                            .ToArray();

                        var createdDirectoryList = new List<string>();

                        foreach (string? analyzerFile in analyzerFiles)
                        {
                            string folderPrefix = $"{Path.GetDirectoryName(analyzerFile)!.Replace($"analyzers{Path.DirectorySeparatorChar}", string.Empty)}{Path.DirectorySeparatorChar}";

                            // Write folder meta
                            if (!string.IsNullOrEmpty(folderPrefix))
                            {
                                var directoryNameBuilder = new StringBuilder();

                                foreach (string directoryName in folderPrefix.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    directoryNameBuilder.Append(directoryName);
                                    directoryNameBuilder.Append(Path.DirectorySeparatorChar);

                                    string processedDirectoryName = directoryNameBuilder.ToString()[0..^1];

                                    if (createdDirectoryList.Any(d => d.Equals(processedDirectoryName)))
                                    {
                                        continue;
                                    }

                                    createdDirectoryList.Add(processedDirectoryName);

                                    // write meta file for the folder
                                    await WriteTextFileToTar(tarArchive, $"{processedDirectoryName}.meta", UnityMeta.GetMetaForFolder(GetStableGuid(identity, processedDirectoryName)), modTime);
                                }
                            }

                            string fileInUnityPackage = $"{folderPrefix}{Path.GetFileName(analyzerFile)}";
                            string? meta;

                            string fileExtension = Path.GetExtension(fileInUnityPackage);

                            if (fileExtension == ".dll")
                            {
                                if (NuGetHelper.IsApplicableAnalyzer(analyzerFile))
                                {
                                    meta = UnityMeta.GetMetaForDll(
                                        GetStableGuid(identity, fileInUnityPackage),
                                        new PlatformDefinition(UnityOs.AnyOs, UnityCpu.None, isEditorConfig: false),
                                        ["RoslynAnalyzer"],
                                        []);
                                }
                                else
                                {
                                    meta = UnityMeta.GetMetaForDll(
                                        GetStableGuid(identity, fileInUnityPackage),
                                        new PlatformDefinition(UnityOs.AnyOs, UnityCpu.None, isEditorConfig: false),
                                        [],
                                        []);
                                }
                            }
                            else
                            {
                                meta = UnityMeta.GetMetaForExtension(GetStableGuid(identity, fileInUnityPackage), fileExtension);
                            }

                            if (meta == null)
                            {
                                continue;
                            }

                            memStream.Position = 0;
                            memStream.SetLength(0);

                            using Stream stream = await packageReader.GetStreamAsync(analyzerFile, CancellationToken.None);
                            await stream.CopyToAsync(memStream);
                            byte[] buffer = memStream.ToArray();

                            // write content
                            await WriteBufferToTar(tarArchive, fileInUnityPackage, buffer, modTime);

                            // write meta file
                            await WriteTextFileToTar(tarArchive, $"{fileInUnityPackage}.meta", meta, modTime);
                        }

                        // Write analyzer asmdef
                        // Check Analyzer Scope section: https://docs.unity3d.com/Manual/roslyn-analyzers.html
                        UnityAsmdef analyzerAsmdef = CreateAnalyzerAmsdef(identity);
                        string analyzerAsmdefAsJson = await analyzerAsmdef.ToJson(UnityNugetJsonSerializerContext.Default.UnityAsmdef);
                        string analyzerAsmdefFileName = $"{identity.Id}.asmdef";
                        await WriteTextFileToTar(tarArchive, analyzerAsmdefFileName, analyzerAsmdefAsJson, modTime);
                        await WriteTextFileToTar(tarArchive, $"{analyzerAsmdefFileName}.meta", UnityMeta.GetMetaForExtension(GetStableGuid(identity, analyzerAsmdefFileName), ".asmdef")!, modTime);

                        // Write empty script (Necessary to compile the asmdef file)
                        string emptyScriptContent = UnityScript.GetEmptyScript();
                        const string emptyScriptFileName = "EmptyScript.cs";
                        await WriteTextFileToTar(tarArchive, emptyScriptFileName, emptyScriptContent, modTime);
                        await WriteTextFileToTar(tarArchive, $"{emptyScriptFileName}.meta", UnityMeta.GetMetaForExtension(GetStableGuid(identity, emptyScriptFileName), ".cs")!, modTime);
                    }

                    // Get all known platform definitions
                    var platformDefs = PlatformDefinition.CreateAllPlatforms();
                    var packageFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach ((FrameworkSpecificGroup item, HashSet<RegistryTargetFramework> frameworks) in collectedItems)
                    {
                        string folderPrefix = hasMultiNetStandard ? $"{frameworks.First().Name}/" : "";
                        var filesToWrite = new List<PlatformFile>();

                        // Get any available runtime library groups
                        List<(string file, UnityOs, UnityCpu?)> runtimeLibs = await RuntimeLibraries
                            .GetSupportedRuntimeLibsAsync(packageReader, item.TargetFramework, _logger)
                            .ToListAsync();

                        // Mark-up the platforms of all runtime libraries
                        var runtimePlatforms = new HashSet<PlatformDefinition>();
                        foreach ((string file, UnityOs os, UnityCpu? cpu) in runtimeLibs)
                        {
                            // Reject resource dlls since Unity can't use them and we're not handling paths
                            if (file.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            PlatformDefinition? platformDef = platformDefs.Find(os, cpu);
                            if (platformDef == null)
                            {
                                LogInformation($"Failed to find a platform definition for: {os}, {cpu}");
                                continue;
                            }

                            // We have a platform, add this file to the set of files to write
                            runtimePlatforms.Add(platformDef!);
                            filesToWrite.Add(new PlatformFile(file, platformDef!));
                        }

                        // Compute the set of platforms covered by the lib dlls
                        HashSet<PlatformDefinition> libPlatforms = platformDefs.GetRemainingPlatforms(runtimePlatforms);

                        // Add the lib files
                        foreach (PlatformDefinition libPlatform in libPlatforms)
                        {
                            foreach (string? file in item.Items)
                            {
                                // Reject resource dlls since Unity can't use them and we're not handling paths
                                if (file.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                filesToWrite.Add(new PlatformFile(file, libPlatform));
                            }
                        }

                        // Write the files
                        foreach (PlatformFile file in filesToWrite)
                        {
                            // Get the destination path
                            string fileInUnityPackage = file.GetDestinationPath(folderPrefix);

                            // Collect the folders' metas
                            {
                                string? fullPath = Path.GetDirectoryName(fileInUnityPackage);
                                string[] folders;

                                if (!string.IsNullOrEmpty(fullPath))
                                {
                                    folders = fullPath.Split(Path.DirectorySeparatorChar);
                                }
                                else
                                {
                                    folders = [];
                                }

                                string folder = string.Empty;

                                foreach (string relative in folders)
                                {
                                    folder = Path.Combine(folder, relative);
                                    packageFolders.Add(folder);
                                }
                            }

                            string? meta;

                            string fileExtension = Path.GetExtension(fileInUnityPackage);
                            if (fileExtension == ".dll")
                            {
                                // If we have multiple .NETStandard supported or there is just netstandard2.1 or the package can
                                // only be used when it is not netstandard 2.1
                                // We will use the define coming from the configuration file
                                // Otherwise, it means that the assembly is compatible with whatever netstandard, and we can simply
                                // use NET_STANDARD
                                string[]? defineConstraints = hasMultiNetStandard
                                    || hasOnlyNetStandard21
                                    || isPackageNetStandard21Assembly ? frameworks.First(x => x.Framework == item.TargetFramework).DefineConstraints : [];

                                meta = UnityMeta.GetMetaForDll(
                                    GetStableGuid(identity, fileInUnityPackage),
                                    file.Platform,
                                    [],
                                    defineConstraints != null ? defineConstraints.Concat(packageEntry.DefineConstraints) : []);
                            }
                            else
                            {
                                meta = UnityMeta.GetMetaForExtension(GetStableGuid(identity, fileInUnityPackage), fileExtension);
                            }

                            if (meta == null)
                            {
                                continue;
                            }

                            memStream.Position = 0;
                            memStream.SetLength(0);

                            using Stream stream = await packageReader.GetStreamAsync(file.SourcePath, CancellationToken.None);
                            await stream.CopyToAsync(memStream);
                            byte[] buffer = memStream.ToArray();

                            // write content
                            await WriteBufferToTar(tarArchive, fileInUnityPackage, buffer, modTime);

                            // write meta file
                            await WriteTextFileToTar(tarArchive, $"{fileInUnityPackage}.meta", meta, modTime);
                        }
                    }

                    if (!packageEntry.Analyzer && collectedItems.Count == 0)
                    {
                        throw new InvalidOperationException($"The package does not contain a compatible .NET assembly {string.Join(",", _targetFrameworks.Select(x => x.Name))}");
                    }

                    // Write the native libraries
                    IAsyncEnumerable<(string file, string[] folders, UnityOs os, UnityCpu cpu)> nativeFiles = NativeLibraries.GetSupportedNativeLibsAsync(packageReader, _logger);

                    await foreach ((string file, string[] folders, UnityOs os, UnityCpu cpu) in nativeFiles)
                    {
                        PlatformDefinition? platformDef = platformDefs.Find(os, cpu);
                        if (platformDef == null)
                        {
                            LogInformation($"Failed to find a platform definition for: {os}, {cpu}");
                            continue;
                        }

                        string extension = Path.GetExtension(file);
                        Guid guid = GetStableGuid(identity, file);
                        string? meta = extension switch
                        {
                            ".dll" or ".so" or ".dylib" => UnityMeta.GetMetaForDll(guid, platformDef!, [], []),
                            _ => UnityMeta.GetMetaForExtension(guid, extension)
                        };

                        if (meta == null)
                        {
                            LogInformation($"Skipping file without meta: {file} ...");
                            continue;
                        }

                        memStream.SetLength(0);
                        using Stream stream = await packageReader.GetStreamAsync(file, CancellationToken.None);
                        await stream.CopyToAsync(memStream);
                        byte[] buffer = memStream.ToArray();

                        await WriteBufferToTar(tarArchive, file, buffer, modTime);
                        await WriteTextFileToTar(tarArchive, $"{file}.meta", meta, modTime);

                        // Remember all folders for meta files
                        string folder = string.Empty;

                        foreach (string relative in folders)
                        {
                            folder = Path.Combine(folder, relative);
                            packageFolders.Add(folder);
                        }
                    }

                    foreach (string folder in packageFolders)
                    {
                        await WriteTextFileToTar(tarArchive, $"{folder}.meta", UnityMeta.GetMetaForFolder(GetStableGuid(identity, folder)), modTime);
                    }

                    // Write the package,json
                    UnityPackage unityPackage = CreateUnityPackage(npmPackageInfo, npmPackageVersion);
                    string unityPackageAsJson = await unityPackage.ToJson(UnityNugetJsonSerializerContext.Default.UnityPackage);
                    const string packageJsonFileName = "package.json";
                    await WriteTextFileToTar(tarArchive, packageJsonFileName, unityPackageAsJson, modTime);
                    await WriteTextFileToTar(tarArchive, $"{packageJsonFileName}.meta", UnityMeta.GetMetaForExtension(GetStableGuid(identity, packageJsonFileName), ".json")!, modTime);

                    // Write the license to the package if any
                    string? license = null;
                    string? licenseUrlText = null;

                    string? licenseUrl = packageMeta.LicenseMetadata?.LicenseUrl.ToString() ?? packageMeta.LicenseUrl?.ToString();
                    if (!string.IsNullOrEmpty(licenseUrl))
                    {
                        try
                        {
                            // Try to fetch the license from an URL
                            using (var httpClient = new HttpClient())
                            {
                                licenseUrlText = await httpClient.GetStringAsync(licenseUrl);
                            }

                            // If the license text is HTML, try to remove all text
                            if (licenseUrlText != null)
                            {
                                licenseUrlText = licenseUrlText.Trim();
                                if (licenseUrlText.StartsWith('<'))
                                {
                                    try
                                    {
                                        licenseUrlText = NUglify.Uglify.HtmlToText(licenseUrlText, HtmlToTextOptions.KeepStructure).Code ?? licenseUrlText;
                                    }
                                    catch
                                    {
                                        // ignored
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    if (!string.IsNullOrEmpty(packageMeta.LicenseMetadata?.License))
                    {
                        license = packageMeta.LicenseMetadata.License;
                    }

                    // If the license fetched from the URL is bigger, use that one to put into the file
                    if (licenseUrlText != null && (license == null || licenseUrlText.Length > license.Length))
                    {
                        license = licenseUrlText;
                    }

                    if (!string.IsNullOrEmpty(license))
                    {
                        const string licenseMdFile = "License.md";
                        await WriteTextFileToTar(tarArchive, licenseMdFile, license, modTime);
                        await WriteTextFileToTar(tarArchive, $"{licenseMdFile}.meta", UnityMeta.GetMetaForExtension(GetStableGuid(identity, licenseMdFile), ".md")!, modTime);
                    }
                }

                using (FileStream stream = File.OpenRead(unityPackageFilePath))
                {
                    string sha1 = Sha1sum(stream);
                    await WriteUnityPackageSha1(identity, npmPackageVersion, sha1);
                    npmPackageVersion.Distribution.Shasum = sha1;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.Delete(unityPackageFilePath);
                }
                catch
                {
                    // ignored
                }

                LogWarning($"Error while processing package `{identity}`. Reason: {ex}");
            }
        }

        private static Guid GetStableGuid(PackageIdentity identity, string name)
        {
            return StringToGuid($"{identity.Id}/{name}*");
        }

        private FileInfo GetUnityPackageFileInfo(PackageIdentity identity, NpmPackageVersion packageVersion)
        {
            return new FileInfo(GetUnityPackagePath(identity, packageVersion));
        }

        private string GetUnityPackageFileName(PackageIdentity identity, NpmPackageVersion packageVersion)
        {
            return $"{_unityScope}.{identity.Id.ToLowerInvariant()}-{packageVersion.Version}.tgz";
        }

        private string GetUnityPackageDescFileName(string packageName)
        {
            return $"{_unityScope}.{packageName}.json";
        }

        private string GetUnityPackageSha1FileName(PackageIdentity identity, NpmPackageVersion packageVersion)
        {
            return $"{_unityScope}.{identity.Id.ToLowerInvariant()}-{packageVersion.Version}.sha1";
        }

        private void DeleteUnityPackage(PackageIdentity identity, NpmPackageVersion packageVersion)
        {
            var packageFile = new FileInfo(GetUnityPackagePath(identity, packageVersion));
            if (packageFile.Exists)
            {
                packageFile.Delete();
            }
            var sha1File = new FileInfo(GetUnityPackageSha1Path(identity, packageVersion));
            if (sha1File.Exists)
            {
                sha1File.Delete();
            }
        }

        private bool IsUnityPackageValid(PackageIdentity identity, NpmPackageVersion packageVersion)
        {
            var packageFile = new FileInfo(GetUnityPackagePath(identity, packageVersion));
            return packageFile.Exists && packageFile.Length > 0;
        }

        private bool IsUnityPackageSha1Valid(PackageIdentity identity, NpmPackageVersion packageVersion)
        {
            var sha1File = new FileInfo(GetUnityPackageSha1Path(identity, packageVersion));
            return sha1File.Exists && sha1File.Length > 0;
        }

        private async Task<string> ReadUnityPackageSha1(PackageIdentity identity, NpmPackageVersion packageVersion)
        {
            return await File.ReadAllTextAsync(GetUnityPackageSha1Path(identity, packageVersion));
        }

        private async Task WriteUnityPackageSha1(PackageIdentity identity, NpmPackageVersion packageVersion, string sha1)
        {
            await File.WriteAllTextAsync(GetUnityPackageSha1Path(identity, packageVersion), sha1);
        }

        private string GetUnityPackagePath(PackageIdentity identity, NpmPackageVersion packageVersion) => Path.Combine(_rootPersistentFolder, GetUnityPackageFileName(identity, packageVersion));

        private string GetUnityPackageSha1Path(PackageIdentity identity, NpmPackageVersion packageVersion) => Path.Combine(_rootPersistentFolder, GetUnityPackageSha1FileName(identity, packageVersion));

        private string GetUnityPackageDescPath(string packageName) => Path.Combine(_rootPersistentFolder, GetUnityPackageDescFileName(packageName));

        private bool TryReadPackageCacheEntry(string packageName, [NotNullWhen(true)] out NpmPackageCacheEntry? cacheEntry)
        {
            cacheEntry = null;
            string path = GetUnityPackageDescPath(packageName);

            if (!File.Exists(path)) return false;

            try
            {
                string cacheEntryAsJson = File.ReadAllText(path);
                cacheEntry = JsonSerializer.Deserialize(cacheEntryAsJson, UnityNugetJsonSerializerContext.Default.NpmPackageCacheEntry);
                if (cacheEntry != null)
                {
                    cacheEntry.Json = cacheEntryAsJson;
                }
                return cacheEntry != null;
            }
            catch
            {
                // ignore
            }

            return false;
        }

        private async Task WritePackageCacheEntry(string packageName, NpmPackageCacheEntry cacheEntry)
        {
            string path = GetUnityPackageDescPath(packageName);
            string newJson = await cacheEntry.ToJson(UnityNugetJsonSerializerContext.Default.NpmPackageCacheEntry);
            // Only update if entry is different
            if (!string.Equals(newJson, cacheEntry.Json, StringComparison.InvariantCulture))
            {
                await File.WriteAllTextAsync(path, await cacheEntry.ToJson(UnityNugetJsonSerializerContext.Default.NpmPackageCacheEntry));
            }
        }

        private static async Task WriteTextFileToTar(TarOutputStream tarOut, string filePath, string content, DateTime modTime, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tarOut);
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(content);

            byte[] buffer = s_utf8EncodingNoBom.GetBytes(content);
            await WriteBufferToTar(tarOut, filePath, buffer, modTime, cancellationToken);
        }

        private static async Task WriteBufferToTar(TarOutputStream tarOut, string filePath, byte[] buffer, DateTime modTime, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tarOut);
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(buffer);

            filePath = filePath.Replace(@"\", "/");
            filePath = filePath.TrimStart('/');

            var tarEntry = TarEntry.CreateTarEntry($"package/{filePath}");
            tarEntry.ModTime = modTime;
            tarEntry.Size = buffer.Length;
            await tarOut.PutNextEntryAsync(tarEntry, cancellationToken);
            await tarOut.WriteAsync(buffer, cancellationToken);
            await tarOut.CloseEntryAsync(cancellationToken);
        }

        private static UnityPackage CreateUnityPackage(NpmPackageInfo npmPackageInfo, NpmPackageVersion npmPackageVersion)
        {
            var unityPackage = new UnityPackage
            {
                Name = npmPackageInfo.Name,
                DisplayName = npmPackageVersion.DisplayName,
                Version = npmPackageVersion.Version,
                Description = npmPackageInfo.Description,
                Unity = npmPackageVersion.Unity
            };
            unityPackage.Dependencies.AddRange(npmPackageVersion.Dependencies);
            unityPackage.Keywords.AddRange(npmPackageInfo.Keywords);
            return unityPackage;
        }

        private static UnityAsmdef CreateAnalyzerAmsdef(PackageIdentity packageIdentity)
        {
            return new()
            {
                Name = $"{packageIdentity.Id}_Unity", // Add _Unity suffix because Unity has a validation so that assemblies names do not collide with asmdefs assembly names
                IncludePlatforms = ["Editor"]
            };
        }

        private static Guid StringToGuid(string text)
        {
            byte[] guid = new byte[16];
            byte[] inputBytes = Encoding.UTF8.GetBytes(text);
            byte[] hash = SHA1.HashData(inputBytes);
            Array.Copy(hash, 0, guid, 0, guid.Length);

            // Follow UUID for SHA1 based GUID
            const int version = 5; // SHA1 (3 for MD5)
            guid[6] = (byte)((guid[6] & 0x0F) | (version << 4));
            guid[8] = (byte)((guid[8] & 0x3F) | 0x80);
            return new Guid(guid);
        }

        private static string Sha1sum(Stream stream)
        {
            byte[] hash = SHA1.HashData(stream);
            var sb = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        private void LogInformation(string message)
        {
            _logger.LogInformation(message);
            OnInformation?.Invoke(message);
        }

        private void LogWarning(string message)
        {
            _logger.LogWarning(message);
            OnWarning?.Invoke(message);
        }

        private void LogError(string message)
        {
            _logger.LogError(message);
            OnError?.Invoke(message);
        }

        private static List<string> SplitCommaSeparatedString(string input)
        {
            var list = new List<string>();

            if (input == null)
            {
                return list;
            }

            char[] separators = [',', ';'];

            foreach (string entry in input.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                list.Add(entry.Trim());
            }

            return list;
        }
    }
}
