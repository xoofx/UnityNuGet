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
            Packages = new();
            ListedPackageInfos = new();
            UnlistedPackageInfos = new();
        }

        public Dictionary<string, NpmPackage> Packages { get; }

        public NpmPackageListAllResponse ListedPackageInfos { get; }

        public NpmPackageListAllResponse UnlistedPackageInfos { get; }
    }
}
