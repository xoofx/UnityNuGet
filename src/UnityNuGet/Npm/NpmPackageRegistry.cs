using System.Collections.Generic;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Used to store all type of responses (all packages, or single packages but also unlisted packages)
    /// </summary>
    public class NpmPackageRegistry : NpmObject
    {
        public NpmPackageRegistry()
        {
            Packages = [];
            ListedPackageInfos = new();
            UnlistedPackageInfos = new();
        }

        public Dictionary<string, NpmPackage> Packages { get; }

        public NpmPackageListAllResponse ListedPackageInfos { get; }

        public NpmPackageListAllResponse UnlistedPackageInfos { get; }

        public void AddPackage(NpmPackageCacheEntry entry, bool isListed)
        {
            var package = entry.Package!;
            Packages.Add(package.Id!, package);
            var packageInfos = isListed ? ListedPackageInfos : UnlistedPackageInfos;
            packageInfos.Packages.Add(package.Id!, entry.Info!);
        }

        public void Reset()
        {
            Packages.Clear();
            ListedPackageInfos.Reset();
            UnlistedPackageInfos.Reset();
        }
    }
}
