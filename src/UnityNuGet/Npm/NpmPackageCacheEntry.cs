// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Newtonsoft.Json;

namespace UnityNuGet.Npm
{
    public class NpmPackageCacheEntry : NpmObject
    {
        [JsonProperty("package")]
        public NpmPackage? Package { get; set; }

        [JsonProperty("info")]
        public NpmPackageInfo? Info { get; set; }

        [JsonIgnore]
        public string? Json { get; set; }
    }
}
