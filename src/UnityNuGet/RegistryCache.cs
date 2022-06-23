using System;
using System.Collections.Generic;
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
        // Change this version number if the content of the packages are changed by an update of this class
        private const string CurrentRegistryVersion = "1.4.0";

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
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
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
                registryTargetFramework.Framework = NuGetFramework.Parse(registryTargetFramework.Name);
            }

            _sourceCacheContext = new SourceCacheContext();
            _npmPackageRegistry = new NpmPackageRegistry();
        }

        public bool HasErrors { get; private set; }

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

        /// <summary>
        /// For each package in our registry.json, query NuGet, extract package metadata, and convert them to unity packages.
        /// </summary>
        private async Task BuildInternal()
        {
            var versionPath = Path.Combine(_rootPersistentFolder, "version.txt");
            var forceUpdate = !File.Exists(versionPath) || await File.ReadAllTextAsync(versionPath) != CurrentRegistryVersion;
            if (forceUpdate)
            {
                _logger.LogInformation($"Registry version changed to {CurrentRegistryVersion} - Regenerating all packages");
            }

            var regexFilter = Filter != null ? new Regex(Filter, RegexOptions.IgnoreCase) : null;
            if (Filter != null)
            {
                _logger.LogInformation($"Filtering with regex: {Filter}");
            }

            var onProgress = OnProgress;

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

                var packageMetaIt = await GetMetadataFromSources(packageName);
                var packageMetas = packageMetaIt != null ? packageMetaIt.ToArray() : Array.Empty<IPackageSearchMetadata>();
                foreach (var packageMeta in packageMetas)
                {
                    var packageIdentity = packageMeta.Identity;
                    var packageId = packageIdentity.Id.ToLowerInvariant();
                    var npmPackageId = $"{_unityScope}.{packageId}";

                    if (packageEntry.Version == null || !packageEntry.Version.Satisfies(packageMeta.Identity.Version))
                    {
                        continue;
                    }

                    var resolvedDependencyGroups = packageMeta.DependencySets.Where(dependencySet => dependencySet.TargetFramework.IsAny || _targetFrameworks.Any(targetFramework => dependencySet.TargetFramework == targetFramework.Framework)).ToList();
                    
                    var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(
                        _sourceRepositories,
                        packageIdentity,
                        new PackageDownloadContext(_sourceCacheContext),
                        SettingsUtility.GetGlobalPackagesFolder(_settings),
                        _logger, CancellationToken.None);

                    var hasNativeLib = await NativeLibraries.GetSupportedNativeLibsAsync(downloadResult.PackageReader, _logger).AnyAsync();

                    if (!packageEntry.Analyzer && resolvedDependencyGroups.Count == 0 && !hasNativeLib)
                    {
                        _logger.LogWarning($"The package `{packageIdentity}` doesn't support `{string.Join(",", _targetFrameworks.Select(x => x.Name))}`");
                        continue;
                    }

                    if (!_npmPackageRegistry.Packages.TryGetValue(npmPackageId, out var npmPackage))
                    {
                        npmPackage = new NpmPackage();
                        _npmPackageRegistry.Packages.Add(npmPackageId, npmPackage);
                    }

                    // One NpmPackage (for package request)

                    var packageInfoList = packageEntry.Listed ? _npmPackageRegistry.ListedPackageInfos : _npmPackageRegistry.UnlistedPackageInfos;

                    if (!packageInfoList.Packages.TryGetValue(npmPackageId, out var npmPackageInfo))
                    {
                        npmPackageInfo = new NpmPackageInfo();
                        packageInfoList.Packages.Add(npmPackageId, npmPackageInfo);
                    }

                    // Update latest version
                    var currentVersion = packageIdentity.Version;

                    var update = !npmPackage.DistTags.TryGetValue("latest", out var latestVersion)
                                 || (currentVersion > NuGetVersion.Parse(latestVersion))
                                 || forceUpdate;

                    string npmCurrentVersion = GetNpmVersion(currentVersion);

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
                                LogError($"The version range `{deps.VersionRange}` for the dependency `{deps.Id}` for the package `{packageIdentity}` doesn't match the range allowed from the registry.json: `{packageEntryDep.Version}`");
                                hasDependencyErrors = true;
                                continue;
                            }

                            // Otherwise add the package as a dependency
                            var depsId = deps.Id.ToLowerInvariant();
                            var key = $"{_unityScope}.{depsId}";
                            if (!npmVersion.Dependencies.ContainsKey(key))
                            {
                                npmVersion.Dependencies.Add(key, GetNpmVersion(deps.VersionRange.MinVersion));
                            }
                        }
                    }

                    // If we don't have any dependencies error, generate the package
                    if (!hasDependencyErrors)
                    {
                        await ConvertNuGetToUnityPackageIfDoesNotExist(packageIdentity, npmPackageInfo, npmVersion, packageMeta, forceUpdate, packageEntry, downloadResult);
                        npmPackage.Time[npmCurrentVersion] = packageMeta.Published?.UtcDateTime ?? GetUnityPackageFileInfo(packageIdentity, npmVersion).CreationTimeUtc;

                        // Copy repository info if necessary
                        if (update)
                        {
                            npmPackage.Repository = npmVersion.Repository?.Clone();
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
        private async Task ConvertNuGetToUnityPackageIfDoesNotExist
        (
            PackageIdentity identity,
            NpmPackageInfo npmPackageInfo,
            NpmPackageVersion npmPackageVersion,
            IPackageSearchMetadata packageMeta,
            bool forceUpdate,
            RegistryEntry packageEntry,
            DownloadResourceResult downloadResult
        )
        {
            // If we need to force the update, we delete the previous package+sha1 files
            if (forceUpdate)
            {
                DeleteUnityPackage(identity, npmPackageVersion);
            }

            if (!IsUnityPackageValid(identity, npmPackageVersion) || !IsUnityPackageSha1Valid(identity, npmPackageVersion))
            {
                await ConvertNuGetPackageToUnity(identity, npmPackageInfo, npmPackageVersion, packageMeta, packageEntry, downloadResult);
            }
            else
            {
                npmPackageVersion.Distribution.Shasum = ReadUnityPackageSha1(identity, npmPackageVersion);
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
            RegistryEntry packageEntry,
            DownloadResourceResult downloadResult
        )
        {
            var unityPackageFileName = GetUnityPackageFileName(identity, npmPackageVersion);
            var unityPackageFilePath = Path.Combine(_rootPersistentFolder, unityPackageFileName);

            _logger.LogInformation($"Converting NuGet package {identity} to Unity `{unityPackageFileName}`");

            var packageReader = downloadResult.PackageReader;

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
                var memStream = new MemoryStream();

                using (var outStream = File.Create(unityPackageFilePath))
                using (var gzoStream = new GZipOutputStream(outStream))
                using (var tarArchive = new TarOutputStream(gzoStream, Encoding.UTF8))
                {
                    // Select the framework version that is the closest or equal to the latest configured framework version
                    var versions = (await packageReader.GetLibItemsAsync(CancellationToken.None)).ToList();

                    var collectedItems = new Dictionary<FrameworkSpecificGroup, HashSet<RegistryTargetFramework>>();

                    foreach (var targetFramework in _targetFrameworks)
                    {
                        var item = versions.Where(x => x.TargetFramework.Framework == targetFramework.Framework?.Framework && x.TargetFramework.Version <= targetFramework.Framework.Version).OrderByDescending(x => x.TargetFramework.Version)
                            .FirstOrDefault();
                        if (item == null) continue;
                        if (!collectedItems.TryGetValue(item, out var frameworksPerGroup))
                        {
                            frameworksPerGroup = new HashSet<RegistryTargetFramework>();
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

                    if (isPackageNetStandard21Assembly)
                    {
                        _logger.LogInformation($"Package {identity.Id} is a system package for netstandard2.1 and will be only used for netstandard 2.0");
                    }

                    if (packageEntry.Analyzer)
                    {
                        var packageFiles = (await packageReader.GetPackageFilesAsync(PackageSaveMode.Files, CancellationToken.None)).ToList();

                        var analyzerFiles = packageFiles.Where(p => p.StartsWith("analyzers/dotnet/cs")).ToArray();

                        if (analyzerFiles.Length == 0)
                        {
                            analyzerFiles = packageFiles.Where(p => p.StartsWith("analyzers")).ToArray();
                        }

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
                                    WriteTextFileToTar(tarArchive, $"{processedDirectoryName}.meta", UnityMeta.GetMetaForFolder(GetStableGuid(identity, processedDirectoryName)));
                                }
                            }

                            var fileInUnityPackage = $"{folderPrefix}{Path.GetFileName(analyzerFile)}";
                            string? meta;

                            string fileExtension = Path.GetExtension(fileInUnityPackage);
                            if (fileExtension == ".dll")
                            {
                                meta = UnityMeta.GetMetaForDll(GetStableGuid(identity, fileInUnityPackage), false, new string[] { "RoslynAnalyzer" }, Array.Empty<string>());
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

                            var stream = packageReader.GetStream(analyzerFile);
                            await stream.CopyToAsync(memStream);
                            var buffer = memStream.ToArray();

                            // write content
                            WriteBufferToTar(tarArchive, fileInUnityPackage, buffer);

                            // write meta file
                            WriteTextFileToTar(tarArchive, $"{fileInUnityPackage}.meta", meta);
                        }
                    }

                    foreach (var groupToFrameworks in collectedItems)
                    {
                        var item = groupToFrameworks.Key;
                        var frameworks = groupToFrameworks.Value;

                        var folderPrefix = hasMultiNetStandard ? $"{frameworks.First().Name}/" : "";
                        foreach (var file in item.Items)
                        {
                            var fileInUnityPackage = $"{folderPrefix}{Path.GetFileName(file)}";
                            string? meta;

                            string fileExtension = Path.GetExtension(fileInUnityPackage);
                            if (fileExtension == ".dll")
                            {
                                // If we have multiple .NETStandard supported or there is just netstandard2.1 or the package can
                                // only be used when it is not netstandard 2.1
                                // We will use the define coming from the configuration file
                                // Otherwise, it means that the assembly is compatible with whatever netstandard, and we can simply
                                // use NET_STANDARD
                                var defineConstraints = hasMultiNetStandard || hasOnlyNetStandard21 || isPackageNetStandard21Assembly ? frameworks.First(x => x.Framework == item.TargetFramework).DefineConstraints : Array.Empty<string>();
                                meta = UnityMeta.GetMetaForDll(GetStableGuid(identity, fileInUnityPackage), true, Array.Empty<string>(), defineConstraints != null ? defineConstraints.Concat(packageEntry.DefineConstraints) : Array.Empty<string>());
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

                            var stream = packageReader.GetStream(file);
                            await stream.CopyToAsync(memStream);
                            var buffer = memStream.ToArray();

                            // write content
                            WriteBufferToTar(tarArchive, fileInUnityPackage, buffer);

                            // write meta file
                            WriteTextFileToTar(tarArchive, $"{fileInUnityPackage}.meta", meta);
                        }

                        // Write folder meta
                        if (!string.IsNullOrEmpty(folderPrefix) && item.Items.Any())
                        {
                            // write meta file for the folder
                            WriteTextFileToTar(tarArchive, $"{folderPrefix[0..^1]}.meta", UnityMeta.GetMetaForFolder(GetStableGuid(identity, folderPrefix)));
                        }
                    }

                    if (!packageEntry.Analyzer && collectedItems.Count == 0)
                    {
                        throw new InvalidOperationException($"The package does not contain a compatible .NET assembly {string.Join(",", _targetFrameworks.Select(x => x.Name))}");
                    }

                    // Write the native libraries
                    var nativeFiles = NativeLibraries.GetSupportedNativeLibsAsync(packageReader, _logger);
                    var nativeFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    await foreach (var (file, folders, platform, architecture) in nativeFiles)
                    {
                        string extension = Path.GetExtension(file);
                        var guid = GetStableGuid(identity, file);
                        string? meta = extension switch
                        {
                            ".dll" or ".so" or ".dylib" => NativeLibraries.GetMetaForNative(guid, platform, architecture, Array.Empty<string>()),
                            _ => UnityMeta.GetMetaForExtension(guid, extension)
                        };

                        if (meta == null)
                        {
                            _logger.LogInformation($"Skipping file without meta: {file} ...");
                            continue;
                        }

                        memStream.SetLength(0);
                        using var stream = await packageReader.GetStreamAsync(file, CancellationToken.None);
                        await stream.CopyToAsync(memStream);
                        var buffer = memStream.ToArray();

                        WriteBufferToTar(tarArchive, file, buffer);
                        WriteTextFileToTar(tarArchive, $"{file}.meta", meta);

                        // Remember all folders for meta files
                        string folder = "";

                        foreach (var relative in folders)
                        {
                            folder = Path.Combine(folder, relative);
                            nativeFolders.Add(folder);
                        }
                    }

                    foreach (var folder in nativeFolders)
                    {
                        WriteTextFileToTar(tarArchive, $"{folder}.meta", UnityMeta.GetMetaForFolder(GetStableGuid(identity, folder)));
                    }

                    // Write the package,json
                    var unityPackage = CreateUnityPackage(npmPackageInfo, npmPackageVersion);
                    var unityPackageAsJson = unityPackage.ToJson();
                    const string packageJsonFileName = "package.json";
                    WriteTextFileToTar(tarArchive, packageJsonFileName, unityPackageAsJson);
                    WriteTextFileToTar(tarArchive, $"{packageJsonFileName}.meta", UnityMeta.GetMetaForExtension(GetStableGuid(identity, packageJsonFileName), ".json")!);

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
                                if (licenseUrlText.StartsWith("<"))
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
                        WriteTextFileToTar(tarArchive, licenseMdFile, license);
                        WriteTextFileToTar(tarArchive, $"{licenseMdFile}.meta", UnityMeta.GetMetaForExtension(GetStableGuid(identity, licenseMdFile), ".md")!);
                    }
                }

                using (var stream = File.OpenRead(unityPackageFilePath))
                {
                    var sha1 = Sha1sum(stream);
                    WriteUnityPackageSha1(identity, npmPackageVersion, sha1);
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

                LogError($"Error while processing package `{identity}`. Reason: {ex}");
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

        private string ReadUnityPackageSha1(PackageIdentity identity, NpmPackageVersion packageVersion)
        {
            return File.ReadAllText(GetUnityPackageSha1Path(identity, packageVersion));
        }

        private void WriteUnityPackageSha1(PackageIdentity identity, NpmPackageVersion packageVersion, string sha1)
        {
            File.WriteAllText(GetUnityPackageSha1Path(identity, packageVersion), sha1);
        }

        private string GetUnityPackagePath(PackageIdentity identity, NpmPackageVersion packageVersion) => Path.Combine(_rootPersistentFolder, GetUnityPackageFileName(identity, packageVersion));

        private string GetUnityPackageSha1Path(PackageIdentity identity, NpmPackageVersion packageVersion) => Path.Combine(_rootPersistentFolder, GetUnityPackageSha1FileName(identity, packageVersion));

        private static void WriteTextFileToTar(TarOutputStream tarOut, string filePath, string content)
        {
            ArgumentNullException.ThrowIfNull(tarOut);
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(content);

            var buffer = Utf8EncodingNoBom.GetBytes(content);
            WriteBufferToTar(tarOut, filePath, buffer);
        }

        private static void WriteBufferToTar(TarOutputStream tarOut, string filePath, byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(tarOut);
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(buffer);

            filePath = filePath.Replace(@"\", "/");
            filePath = filePath.TrimStart('/');

            var tarEntry = TarEntry.CreateTarEntry($"package/{filePath}");
            tarEntry.Size = buffer.Length;
            tarOut.PutNextEntry(tarEntry);
            tarOut.Write(buffer, 0, buffer.Length);
            tarOut.CloseEntry();
        }

        private static UnityPackage CreateUnityPackage(NpmPackageInfo npmPackageInfo, NpmPackageVersion npmPackageVersion)
        {
            var unityPackage = new UnityPackage
            {
                Name = npmPackageInfo.Name,
                Version = npmPackageVersion.Version,
                Description = npmPackageInfo.Description,
                Unity = npmPackageVersion.Unity
            };
            unityPackage.Dependencies.AddRange(npmPackageVersion.Dependencies);
            unityPackage.Keywords.AddRange(npmPackageInfo.Keywords);
            return unityPackage;
        }

        private static Guid StringToGuid(string text)
        {
            var guid = new byte[16];
            var inputBytes = Encoding.UTF8.GetBytes(text);
            using (var algorithm = SHA1.Create())
            {
                var hash = algorithm.ComputeHash(inputBytes);
                Array.Copy(hash, 0, guid, 0, guid.Length);
            }

            // Follow UUID for SHA1 based GUID 
            const int version = 5; // SHA1 (3 for MD5)
            guid[6] = (byte)((guid[6] & 0x0F) | (version << 4));
            guid[8] = (byte)((guid[8] & 0x3F) | 0x80);
            return new Guid(guid);
        }

        private static string Sha1sum(Stream stream)
        {
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        private void LogError(string message)
        {
            _logger.LogError(message);
            HasErrors = true;
        }

        private static List<string> SplitCommaSeparatedString(string input)
        {
            var list = new List<string>();
            if (input == null) return list;
            foreach (var entry in input.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                list.Add(entry.Trim());
            }

            return list;
        }
    }
}
