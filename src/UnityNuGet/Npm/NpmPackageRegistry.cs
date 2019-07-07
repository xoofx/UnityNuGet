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
            Packages = new Dictionary<string, NpmPackage>();
            ListedPackageInfos = new NpmPackageListAllResponse();
            UnlistedPackageInfos = new NpmPackageListAllResponse();
        }

        public Dictionary<string, NpmPackage> Packages { get; }
        
        public NpmPackageListAllResponse ListedPackageInfos { get; }

        public NpmPackageListAllResponse UnlistedPackageInfos { get; }
    }
}