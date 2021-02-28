using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
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
        private static readonly Encoding Utf8EncodingNoBom = new UTF8Encoding(false, false);
        private const string UnityScope = "org.nuget";
        private const string MinimumUnityVersion = "2019.1";
        private const string PackageNameNuGetPostFix = " (NuGet)";
        private readonly ISettings _settings;
        private readonly SourceRepository _sourceRepository;
        private static readonly NuGetFramework NuGetFrameworkNetStandard20 = NuGetFramework.Parse("netstandard2.0");
        private readonly SourceCacheContext _sourceCacheContext;
        private readonly Registry _registry;
        private readonly NpmPackageRegistry _npmPackageRegistry;

        public RegistryCache(string rootPersistentFolder, string rootHttpUrl, ILogger logger = null)
        {
            RootUnityPackageFolder = rootPersistentFolder ?? throw new ArgumentNullException(nameof(rootPersistentFolder));
            RootHttpUrl = rootHttpUrl ?? throw new ArgumentNullException(nameof(rootHttpUrl)) ; 

            if (!Directory.Exists(RootUnityPackageFolder))
            {
                Directory.CreateDirectory(RootUnityPackageFolder);
            }
            _settings = Settings.LoadDefaultSettings(root: null);
            var sourceRepositoryProvider = new SourceRepositoryProvider(new PackageSourceProvider(_settings), Repository.Provider.GetCoreV3());
            _sourceRepository = sourceRepositoryProvider.GetRepositories().FirstOrDefault();
            Logger = logger ?? new NuGetConsoleLogger();
            _registry = Registry.GetInstance();
            _sourceCacheContext = new SourceCacheContext();
            _npmPackageRegistry = new NpmPackageRegistry();
        }
        
        public bool HasErrors { get; private set; }

        public string RootUnityPackageFolder { get; }

        public string RootHttpUrl { get; }

        public ILogger Logger { get; }

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
        public NpmPackage GetPackage(string packageId)
        {
            if (packageId == null) throw new ArgumentNullException(nameof(packageId));
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
            if (packageFileName == null) throw new ArgumentNullException(nameof(packageFileName));
            packageFileName = packageFileName.Replace("/", packageFileName.Replace(".", string.Empty));
            var packageFilePath = Path.Combine(RootUnityPackageFolder, packageFileName);
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

        /// <summary>
        /// For each package in our registry.json, query NuGet, extract package metadata, and convert them to unity packages.
        /// </summary>
        private async Task BuildInternal()
        {
            var packageMetadataResource = _sourceRepository.GetResource<PackageMetadataResource>();

            foreach (var packageDesc in _registry)
            {
                var packageName = packageDesc.Key;
                var packageEntry = packageDesc.Value;
                // A package entry is ignored but allowed in the registry (case of Microsoft.CSharp)
                if (packageEntry.Ignored)
                {
                    continue;
                }

                var packageMetaIt = await packageMetadataResource.GetMetadataAsync(packageName, false, false, _sourceCacheContext, Logger, CancellationToken.None);
                var packageMetas = packageMetaIt.ToList();
                foreach (var packageMeta in packageMetas)
                {
                    var packageIdentity = packageMeta.Identity;
                    var packageId = packageIdentity.Id.ToLowerInvariant();
                    var npmPackageId = $"{UnityScope}.{packageId}";                  

                    if (!packageEntry.Version.Satisfies(packageMeta.Identity.Version))
                    {
                        continue;
                    }

                    PackageDependencyGroup netstd20Dependency = null;

                    foreach (var dependencySet in packageMeta.DependencySets)
                    {
                        if (dependencySet.TargetFramework == NuGetFrameworkNetStandard20)
                        {
                            netstd20Dependency = dependencySet;
                            break;
                        }
                    }

                    if (netstd20Dependency == null)
                    {
                        Logger.LogWarning($"The package `{packageIdentity}` doesn't support `netstandard2.0`");
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
                                 || (currentVersion > NuGetVersion.Parse(latestVersion));


                    if (update)
                    {
                        npmPackage.DistTags["latest"] = currentVersion.ToString();

                        npmPackageInfo.Versions.Clear();
                        npmPackageInfo.Versions[currentVersion.ToString()] = "latest";

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
                        Id = $"{npmPackageId}@{currentVersion}",
                        Version = currentVersion.ToString(),
                        Name = npmPackageId,
                        Description = packageMeta.Description,
                        Author = npmPackageInfo.Author,
                        DisplayName = packageMeta.Title + PackageNameNuGetPostFix
                    };
                    npmVersion.Distribution.Tarball = new Uri($"{RootHttpUrl}/{npmPackage.Id}/-/{GetUnityPackageFileName(packageIdentity)}");
                    npmVersion.Unity = MinimumUnityVersion;
                    npmPackage.Versions[npmVersion.Version] = npmVersion;

                    bool hasDependencyErrors = false;
                    foreach (var deps in netstd20Dependency.Packages)
                    {
                        var depsId = deps.Id.ToLowerInvariant();

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
                        }

                        // Otherwise add the package as a dependency
                        npmVersion.Dependencies.Add($"{UnityScope}.{depsId}", deps.VersionRange.MinVersion.ToString());
                    }

                    // If we don't have any dependencies error, generate the package
                    if (!hasDependencyErrors)
                    {
                        await ConvertNuGetToUnityPackageIfDoesNotExist(packageIdentity, npmPackageInfo, npmVersion, packageMeta);
                        npmPackage.Time[currentVersion.ToString()] = packageMeta.Published?.UtcDateTime ?? GetUnityPackageFileInfo(packageIdentity).CreationTimeUtc;

                        // Copy repository info if necessary
                        if (update)
                        {
                            npmPackage.Repository = npmVersion.Repository?.Clone();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Converts a NuGet package to Unity package if not already
        /// </summary>
        private async Task ConvertNuGetToUnityPackageIfDoesNotExist(PackageIdentity identity, NpmPackageInfo npmPackageInfo, NpmPackageVersion npmPackageVersion, IPackageSearchMetadata packageMeta)
        {
            if (!IsUnityPackageExists(identity))
            {
                await ConvertNuGetPackageToUnity(identity, npmPackageInfo, npmPackageVersion, packageMeta);
            }
            else
            {
                npmPackageVersion.Distribution.Shasum = ReadUnityPackageSha1(identity);
            }
        }

        /// <summary>
        /// Converts a NuGet package to a Unity package.
        /// </summary>
        private async Task ConvertNuGetPackageToUnity(PackageIdentity identity, NpmPackageInfo npmPackageInfo, NpmPackageVersion npmPackageVersion, IPackageSearchMetadata packageMeta)
        {
            var unityPackageFileName = GetUnityPackageFileName(identity);
            var unityPackageFilePath = Path.Combine(RootUnityPackageFolder, unityPackageFileName);

            Logger.LogInformation($"Converting NuGet package {identity} to Unity `{unityPackageFileName}`");

            var downloadResource = await _sourceRepository.GetResourceAsync<DownloadResource>(CancellationToken.None);
            var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                identity,
                new PackageDownloadContext(_sourceCacheContext),
                SettingsUtility.GetGlobalPackagesFolder(_settings),
                Logger, CancellationToken.None);
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
                    foreach (var item in await packageReader.GetLibItemsAsync(CancellationToken.None))
                    {
                        if (item.TargetFramework != NuGetFrameworkNetStandard20)
                        {
                            continue;
                        }

                        foreach (var file in item.Items)
                        {
                            var fileInUnityPackage = Path.GetFileName(file);
                            var meta = UnityMeta.GetMetaForExtension(GetStableGuid(identity, fileInUnityPackage), Path.GetExtension(fileInUnityPackage));
                            if (meta == null)
                            {
                                continue;
                            }

                            memStream.Position = 0;
                            memStream.SetLength(0);

                            var stream = packageReader.GetStream(file);
                            stream.CopyTo(memStream);
                            var buffer = memStream.ToArray();

                            // write content
                            WriteBufferToTar(tarArchive, fileInUnityPackage, buffer);

                            // write meta file
                            WriteTextFileToTar(tarArchive, fileInUnityPackage + ".meta", meta);
                        }
                    }

                    // Write the package,json
                    var unityPackage = CreateUnityPackage(npmPackageInfo, npmPackageVersion);
                    var unityPackageAsJson = unityPackage.ToJson();
                    const string packageJsonFileName = "package.json";
                    WriteTextFileToTar(tarArchive, packageJsonFileName, unityPackageAsJson);
                    WriteTextFileToTar(tarArchive, $"{packageJsonFileName}.meta", UnityMeta.GetMetaForExtension(GetStableGuid(identity, packageJsonFileName), ".json"));

                    // Write the license to the package if any
                    string license = null;
                    string licenseUrlText = null;
                    
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
                        WriteTextFileToTar(tarArchive, $"{licenseMdFile}.meta", UnityMeta.GetMetaForExtension(GetStableGuid(identity, licenseMdFile), ".md"));
                    }
                }
                
                using (var stream = File.OpenRead(unityPackageFilePath))
                {
                    var sha1 = Sha1sum(stream);
                    WriteUnityPackageSha1(identity, sha1);
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
            return StringToGuid(identity.Id + $"/{name}*");
        }

        private FileInfo GetUnityPackageFileInfo(PackageIdentity identity)
        {
            return new FileInfo(Path.Combine(RootUnityPackageFolder, GetUnityPackageFileName(identity)));
        }

        private static string GetUnityPackageFileName(PackageIdentity identity)
        {
            return $"{UnityScope}.{identity.Id.ToLowerInvariant()}-{identity.Version}.tgz";
        }

        private static string GetUnityPackageSha1FileName(PackageIdentity identity)
        {
            return $"{UnityScope}.{identity.Id.ToLowerInvariant()}-{identity.Version}.sha1";
        }

        private bool IsUnityPackageExists(PackageIdentity identity)
        {
            return File.Exists(Path.Combine(RootUnityPackageFolder, GetUnityPackageFileName(identity)));
        }

        private string ReadUnityPackageSha1(PackageIdentity identity)
        {
            return File.ReadAllText(Path.Combine(RootUnityPackageFolder, GetUnityPackageSha1FileName(identity)));
        }

        private void WriteUnityPackageSha1(PackageIdentity identity, string sha1)
        {
            File.WriteAllText(Path.Combine(RootUnityPackageFolder, GetUnityPackageSha1FileName(identity)), sha1);
        }

        private void WriteTextFileToTar(TarOutputStream tarOut, string filePath, string content)
        {
            if (tarOut == null) throw new ArgumentNullException(nameof(tarOut));
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (content == null) throw new ArgumentNullException(nameof(content));
            
            var buffer = Utf8EncodingNoBom.GetBytes(content);
            WriteBufferToTar(tarOut, filePath, buffer);
        }
        
        private void WriteBufferToTar(TarOutputStream tarOut, string filePath, byte[] buffer)
        {
            if (tarOut == null) throw new ArgumentNullException(nameof(tarOut));
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            filePath = filePath.Replace(@"\", "/");
            filePath = filePath.TrimStart('/');
            
            var tarEntry = TarEntry.CreateTarEntry("package/"+ filePath);
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
            guid[6] = (byte) ((guid[6] & 0x0F) | (version << 4));
            guid[8] = (byte) ((guid[8] & 0x3F) | 0x80);
            return new Guid(guid);
        }

        private static string Sha1sum(Stream stream)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);

                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        private void LogError(string message, Exception ex = null)
        {
            Logger.LogError(message);
            HasErrors = true;
        }

        private static List<string> SplitCommaSeparatedString(string input)
        {
            var list = new List<string>();
            if (input == null) return list;
            foreach (var entry in input.Split(new[] {',', ';'}, StringSplitOptions.RemoveEmptyEntries))
            {
                list.Add(entry.Trim());
            }

            return list;
        }
    }
}