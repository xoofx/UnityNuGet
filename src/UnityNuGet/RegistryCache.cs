using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Newtonsoft.Json;
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

        private static readonly Encoding Utf8EncodingNoBom = new UTF8Encoding(false, false);
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
        private readonly Registry _registry;
        private readonly NpmPackageRegistry _npmPackageRegistry;

        public RegistryCache(RegistryCache registryCache) : this(registryCache._rootPersistentFolder, registryCache._rootHttpUri, registryCache._unityScope,
            registryCache._minimumUnityVersion, registryCache._packageNameNuGetPostFix, registryCache._targetFrameworks, registryCache._logger)
        { }

        public RegistryCache(string rootPersistentFolder, Uri rootHttpUri, string unityScope, string minimumUnityVersion,
            string packageNameNuGetPostFix, RegistryTargetFramework[] targetFrameworks, ILogger logger)
        {
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
                var nugetFolder = Path.Combine(_rootPersistentFolder, ".nuget");
                Environment.SetEnvironmentVariable("NUGET_PACKAGES", nugetFolder);
            }

            _settings = Settings.LoadDefaultSettings(root: null);
            var sourceRepositoryProvider = new SourceRepositoryProvider(new PackageSourceProvider(_settings), Repository.Provider.GetCoreV3());
            _sourceRepositories = sourceRepositoryProvider.GetRepositories();
            _logger = logger;
            _registry = Registry.GetInstance();

            // Initialize target framework
            foreach (var registryTargetFramework in _targetFrameworks)
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
            _npmPackageRegistry.Packages.TryGetValue(packageId, out var package);
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
            var packageFilePath = Path.Combine(_rootPersistentFolder, packageFileName);
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

        private async Task<IEnumerable<IPackageSearchMetadata>?> GetMetadataFromSources(string packageName)
        {
            foreach (var source in _sourceRepositories)
            {
                var packageMetadataResource = source.GetResource<PackageMetadataResource>();

                var result = await packageMetadataResource.GetMetadataAsync(packageName, includePrerelease: false, includeUnlisted: false, _sourceCacheContext, _logger, CancellationToken.None);

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
            var versionPath = Path.Combine(_rootPersistentFolder, "version.txt");
            var forceUpdate = !File.Exists(versionPath) || await File.ReadAllTextAsync(versionPath) != CurrentRegistryVersion;
            if (forceUpdate)
            {
                LogInformation($"Registry version changed to {CurrentRegistryVersion} - Regenerating all packages");

                // Clear the cache entirely
                _npmPackageRegistry.Reset();
            }

            var regexFilter = Filter != null ? new Regex(Filter, RegexOptions.IgnoreCase) : null;
            if (Filter != null)
            {
                LogInformation($"Filtering with regex: {Filter}");
            }

            var onProgress = OnProgress;

            var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(_settings);

            var progressCount = 0;
            foreach (var packageDesc in _registry)
            {
                var packageName = packageDesc.Key;
                var packageEntry = packageDesc.Value;

                // Log progress count
                onProgress?.Invoke(++progressCount, _registry.Count);

                // A package entry is ignored but allowed in the registry (case of Microsoft.CSharp)
                if (packageEntry.Ignored || (regexFilter != null && !regexFilter.IsMatch(packageName)))
                {
                    continue;
                }

                var packageId = packageName.ToLowerInvariant();
                var npmPackageId = $"{_unityScope}.{packageId}";
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

                var packageMetaIt = await GetMetadataFromSources(packageName);
                var packageMetas = packageMetaIt != null ? packageMetaIt.ToArray() : Array.Empty<IPackageSearchMetadata>();
                foreach (var packageMeta in packageMetas)
                {
                    var packageIdentity = packageMeta.Identity;
                    // Update latest version
                    var currentVersion = packageIdentity.Version;
                    string npmCurrentVersion = GetNpmVersion(currentVersion);

                    if (packageEntry.Version == null || !packageEntry.Version.Satisfies(packageMeta.Identity.Version))
                    {
                        continue;
                    }

                    // If the package id is cached already, we don't need to generate it again
                    if (npmPackage != null && npmPackage.Versions.TryGetValue(npmCurrentVersion, out var existingVersion))
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
                        using var downloadResult = await GetPackageDownloadResourceResult(packageIdentity);

                        var hasNativeLib = await NativeLibraries.GetSupportedNativeLibsAsync(downloadResult.PackageReader, _logger).AnyAsync();

                        if (!hasNativeLib)
                        {
                            LogWarning($"The package `{packageIdentity}` doesn't support `{string.Join(",", _targetFrameworks.Select(x => x.Name))}`");
                            continue;
                        }
                    }

                    npmPackage ??= new NpmPackage();
                    npmPackageInfo ??= new NpmPackageInfo();

                    var update = !npmPackage.DistTags.TryGetValue("latest", out var latestVersion)
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
                        DisplayName = packageMeta.Title + _packageNameNuGetPostFix
                    };
                    npmVersion.Distribution.Tarball = new Uri(_rootHttpUri, $"{npmPackage.Id}/-/{GetUnityPackageFileName(packageIdentity, npmVersion)}");
                    npmVersion.Unity = _minimumUnityVersion;
                    npmPackage.Versions[npmVersion.Version] = npmVersion;

                    bool hasDependencyErrors = false;
                    foreach (var resolvedDependencyGroup in resolvedDependencyGroups)
                    {
                        foreach (var deps in resolvedDependencyGroup.Packages)
                        {
                            if (DotNetHelper.IsNetStandard20Assembly(deps.Id))
                            {
                                continue;
                            }

                            PackageDependency resolvedDeps = deps;

                            if (!_registry.TryGetValue(deps.Id, out var packageEntryDep))
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
                                var dependencyPackageMetaIt = await GetMetadataFromSources(deps.Id);
                                var dependencyPackageMetas = dependencyPackageMetaIt != null ? dependencyPackageMetaIt.ToArray() : Array.Empty<IPackageSearchMetadata>();

                                PackageDependency? packageDependency = null;

                                foreach (var dependencyPackageMeta in dependencyPackageMetas)
                                {
                                    var dependencyResolvedDependencyGroups = NuGetHelper.GetCompatiblePackageDependencyGroups(dependencyPackageMeta.DependencySets, _targetFrameworks, includeAny: false);

                                    if (dependencyResolvedDependencyGroups.Any())
                                    {
                                        _registry.TryGetValue(dependencyPackageMeta.Identity.Id, out var registryEntry);

                                        var registryMinimumVersion = registryEntry?.Version?.MinVersion!;

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
                            var depsId = resolvedDeps.Id.ToLowerInvariant();
                            var key = $"{_unityScope}.{depsId}";
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

        private static string GetNpmVersion(NuGetVersion currentVersion)
        {
            string npmCurrentVersion = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Patch}";

            if (currentVersion.Revision != 0)
            {
                npmCurrentVersion += $"-{currentVersion.Revision}";
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
            var unityPackageFileName = GetUnityPackageFileName(identity, npmPackageVersion);
            var unityPackageFilePath = Path.Combine(_rootPersistentFolder, unityPackageFileName);

            LogInformation($"Converting NuGet package {identity} to Unity `{unityPackageFileName}`");

            using var downloadResult = await GetPackageDownloadResourceResult(identity);

            using var packageReader = downloadResult.PackageReader;

            // Update Repository metadata if necessary
            var repoMeta = packageReader.NuspecReader.GetRepositoryMetadata();
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

                using (var outStream = File.Create(unityPackageFilePath))
                using (var gzoStream = new GZipOutputStream(outStream)
                {
                    ModifiedTime = packageMeta.Published?.UtcDateTime
                })
                using (var tarArchive = new TarOutputStream(gzoStream, Encoding.UTF8))
                {
                    // Select the framework version that is the closest or equal to the latest configured framework version
                    var versions = await packageReader.GetLibItemsAsync(CancellationToken.None);

                    var closestVersions = NuGetHelper.GetClosestFrameworkSpecificGroups(versions, _targetFrameworks);

                    var collectedItems = new Dictionary<FrameworkSpecificGroup, HashSet<RegistryTargetFramework>>();

                    foreach (var (item, targetFramework) in closestVersions)
                    {
                        if (!collectedItems.TryGetValue(item, out var frameworksPerGroup))
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

                    var isPackageNetStandard21Assembly = DotNetHelper.IsNetStandard21Assembly(identity.Id);
                    var hasMultiNetStandard = collectedItems.Count > 1;
                    var hasOnlyNetStandard21 = collectedItems.Count == 1 && collectedItems.Values.First().All(x => x.Name == "netstandard2.1");
                    var modTime = packageMeta.Published?.DateTime ?? DateTime.UnixEpoch;

                    if (isPackageNetStandard21Assembly)
                    {
                        LogInformation($"Package {identity.Id} is a system package for netstandard2.1 and will be only used for netstandard 2.0");
                    }

                    if (packageEntry.Analyzer)
                    {
                        var packageFiles = await packageReader.GetItemsAsync(PackagingConstants.Folders.Analyzers, CancellationToken.None);

                        // https://learn.microsoft.com/en-us/nuget/guides/analyzers-conventions#analyzers-path-format
                        var analyzerFiles = packageFiles
                            .SelectMany(p => p.Items)
                            .Where(p => NuGetHelper.IsApplicableUnitySupportedRoslynVersionFolder(p) && (NuGetHelper.IsApplicableAnalyzer(p) || NuGetHelper.IsApplicableAnalyzerResource(p)))
                            .ToArray();

                        var createdDirectoryList = new List<string>();

                        foreach (var analyzerFile in analyzerFiles)
                        {
                            var folderPrefix = $"{Path.GetDirectoryName(analyzerFile)!.Replace($"analyzers{Path.DirectorySeparatorChar}", string.Empty)}{Path.DirectorySeparatorChar}";

                            // Write folder meta
                            if (!string.IsNullOrEmpty(folderPrefix))
                            {
                                var directoryNameBuilder = new StringBuilder();

                                foreach (var directoryName in folderPrefix.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    directoryNameBuilder.Append(directoryName);
                                    directoryNameBuilder.Append(Path.DirectorySeparatorChar);

                                    var processedDirectoryName = directoryNameBuilder.ToString()[0..^1];

                                    if (createdDirectoryList.Any(d => d.Equals(processedDirectoryName)))
                                    {
                                        continue;
                                    }

                                    createdDirectoryList.Add(processedDirectoryName);

                                    // write meta file for the folder
                                    await WriteTextFileToTar(tarArchive, $"{processedDirectoryName}.meta", UnityMeta.GetMetaForFolder(GetStableGuid(identity, processedDirectoryName)), modTime);
                                }
                            }

                            var fileInUnityPackage = $"{folderPrefix}{Path.GetFileName(analyzerFile)}";
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

                            using var stream = await packageReader.GetStreamAsync(analyzerFile, CancellationToken.None);
                            await stream.CopyToAsync(memStream);
                            var buffer = memStream.ToArray();

                            // write content
                            await WriteBufferToTar(tarArchive, fileInUnityPackage, buffer, modTime);

                            // write meta file
                            await WriteTextFileToTar(tarArchive, $"{fileInUnityPackage}.meta", meta, modTime);
                        }

                        // Write analyzer asmdef
                        // Check Analyzer Scope section: https://docs.unity3d.com/Manual/roslyn-analyzers.html
                        var analyzerAsmdef = CreateAnalyzerAmsdef(identity);
                        var analyzerAsmdefAsJson = analyzerAsmdef.ToJson();
                        string analyzerAsmdefFileName = $"{identity.Id}.asmdef";
                        await WriteTextFileToTar(tarArchive, analyzerAsmdefFileName, analyzerAsmdefAsJson, modTime);
                        await WriteTextFileToTar(tarArchive, $"{analyzerAsmdefFileName}.meta", UnityMeta.GetMetaForExtension(GetStableGuid(identity, analyzerAsmdefFileName), ".asmdef")!, modTime);

                        // Write empty script (Necessary to compile the asmdef file)
                        var emptyScriptContent = UnityScript.GetEmptyScript();
                        const string emptyScriptFileName = "EmptyScript.cs";
                        await WriteTextFileToTar(tarArchive, emptyScriptFileName, emptyScriptContent, modTime);
                        await WriteTextFileToTar(tarArchive, $"{emptyScriptFileName}.meta", UnityMeta.GetMetaForExtension(GetStableGuid(identity, emptyScriptFileName), ".cs")!, modTime);
                    }

                    // Get all known platform definitions
                    var platformDefs = PlatformDefinition.CreateAllPlatforms();
                    var packageFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var (item, frameworks) in collectedItems)
                    {
                        var folderPrefix = hasMultiNetStandard ? $"{frameworks.First().Name}/" : "";
                        var filesToWrite = new List<PlatformFile>();

                        // Get any available runtime library groups
                        var runtimeLibs = await RuntimeLibraries
                            .GetSupportedRuntimeLibsAsync(packageReader, item.TargetFramework, _logger)
                            .ToListAsync();

                        // Mark-up the platforms of all runtime libraries
                        var runtimePlatforms = new HashSet<PlatformDefinition>();
                        foreach (var (file, os, cpu) in runtimeLibs)
                        {
                            // Reject resource dlls since Unity can't use them and we're not handling paths
                            if (file.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var platformDef = platformDefs.Find(os, cpu);
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
                        var libPlatforms = platformDefs.GetRemainingPlatforms(runtimePlatforms);

                        // Add the lib files
                        foreach (var libPlatform in libPlatforms)
                        {
                            foreach (var file in item.Items)
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
                        foreach (var file in filesToWrite)
                        {
                            // Get the destination path
                            var fileInUnityPackage = file.GetDestinationPath(folderPrefix);

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

                                foreach (var relative in folders)
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
                                var defineConstraints = hasMultiNetStandard
                                    || hasOnlyNetStandard21
                                    || isPackageNetStandard21Assembly ? frameworks.First(x => x.Framework == item.TargetFramework).DefineConstraints : [];

                                meta = UnityMeta.GetMetaForDll(
                                    GetStableGuid(identity, fileInUnityPackage),
                                    file.Platform,
                                    Array.Empty<string>(),
                                    defineConstraints != null ? defineConstraints.Concat(packageEntry.DefineConstraints) : Array.Empty<string>());
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

                            using var stream = await packageReader.GetStreamAsync(file.SourcePath, CancellationToken.None);
                            await stream.CopyToAsync(memStream);
                            var buffer = memStream.ToArray();

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
                    var nativeFiles = NativeLibraries.GetSupportedNativeLibsAsync(packageReader, _logger);

                    await foreach (var (file, folders, os, cpu) in nativeFiles)
                    {
                        var platformDef = platformDefs.Find(os, cpu);
                        if (platformDef == null)
                        {
                            LogInformation($"Failed to find a platform definition for: {os}, {cpu}");
                            continue;
                        }

                        string extension = Path.GetExtension(file);
                        var guid = GetStableGuid(identity, file);
                        string? meta = extension switch
                        {
                            ".dll" or ".so" or ".dylib" => UnityMeta.GetMetaForDll(guid, platformDef!, Array.Empty<string>(), Array.Empty<string>()),
                            _ => UnityMeta.GetMetaForExtension(guid, extension)
                        };

                        if (meta == null)
                        {
                            LogInformation($"Skipping file without meta: {file} ...");
                            continue;
                        }

                        memStream.SetLength(0);
                        using var stream = await packageReader.GetStreamAsync(file, CancellationToken.None);
                        await stream.CopyToAsync(memStream);
                        var buffer = memStream.ToArray();

                        await WriteBufferToTar(tarArchive, file, buffer, modTime);
                        await WriteTextFileToTar(tarArchive, $"{file}.meta", meta, modTime);

                        // Remember all folders for meta files
                        string folder = string.Empty;

                        foreach (var relative in folders)
                        {
                            folder = Path.Combine(folder, relative);
                            packageFolders.Add(folder);
                        }
                    }

                    foreach (var folder in packageFolders)
                    {
                        await WriteTextFileToTar(tarArchive, $"{folder}.meta", UnityMeta.GetMetaForFolder(GetStableGuid(identity, folder)), modTime);
                    }

                    // Write the package,json
                    var unityPackage = CreateUnityPackage(npmPackageInfo, npmPackageVersion);
                    var unityPackageAsJson = unityPackage.ToJson();
                    const string packageJsonFileName = "package.json";
                    await WriteTextFileToTar(tarArchive, packageJsonFileName, unityPackageAsJson, modTime);
                    await WriteTextFileToTar(tarArchive, $"{packageJsonFileName}.meta", UnityMeta.GetMetaForExtension(GetStableGuid(identity, packageJsonFileName), ".json")!, modTime);

                    // Write the license to the package if any
                    string? license = null;
                    string? licenseUrlText = null;

                    var licenseUrl = packageMeta.LicenseMetadata?.LicenseUrl.ToString() ?? packageMeta.LicenseUrl?.ToString();
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

                using (var stream = File.OpenRead(unityPackageFilePath))
                {
                    var sha1 = Sha1sum(stream);
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
            var path = GetUnityPackageDescPath(packageName);

            if (!File.Exists(path)) return false;

            try
            {
                var cacheEntryAsJson = File.ReadAllText(path);
                cacheEntry = JsonConvert.DeserializeObject<NpmPackageCacheEntry>(cacheEntryAsJson, JsonCommonExtensions.Settings);
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
            var path = GetUnityPackageDescPath(packageName);
            var newJson = cacheEntry.ToJson();
            // Only update if entry is different
            if (!string.Equals(newJson, cacheEntry.Json, StringComparison.InvariantCulture))
            {
                await File.WriteAllTextAsync(path, cacheEntry.ToJson());
            }
        }

        private static async Task WriteTextFileToTar(TarOutputStream tarOut, string filePath, string content, DateTime modTime, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tarOut);
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(content);

            var buffer = Utf8EncodingNoBom.GetBytes(content);
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
            var guid = new byte[16];
            var inputBytes = Encoding.UTF8.GetBytes(text);
            var hash = SHA1.HashData(inputBytes);
            Array.Copy(hash, 0, guid, 0, guid.Length);

            // Follow UUID for SHA1 based GUID
            const int version = 5; // SHA1 (3 for MD5)
            guid[6] = (byte)((guid[6] & 0x0F) | (version << 4));
            guid[8] = (byte)((guid[8] & 0x3F) | 0x80);
            return new Guid(guid);
        }

        private static string Sha1sum(Stream stream)
        {
            var hash = SHA1.HashData(stream);
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

            foreach (var entry in input.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                list.Add(entry.Trim());
            }

            return list;
        }
    }
}
