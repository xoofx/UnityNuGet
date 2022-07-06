using System;

namespace UnityNuGet.Server
{
    public sealed class RegistryCacheSingleton
    {
        public string? UnityPackageFolder { get; set; }

        public Uri? ServerUri { get; set; }

        public NuGetRedirectLogger? NuGetRedirectLogger { get; set; }

        public int ProgressPackageIndex { get; set; }

        public int ProgressTotalPackageCount { get; set; }

        public RegistryCache? Instance { get; set; }
    }
}
