using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Describes a package for "all" listing, used by <see cref="NpmPackageListAllResponse"/>
    /// </summary>
    public class NpmPackageInfo : NpmObject
    {
        public NpmPackageInfo()
        {
            Maintainers = [];
            Versions = [];
            Keywords = [];
        }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("maintainers")]
        public List<string> Maintainers { get; }

        [JsonProperty("versions")]
        public Dictionary<string, string> Versions { get; }

        [JsonProperty("time")]
        public DateTimeOffset? Time { get; set; }

        [JsonProperty("keywords")]
        public List<string> Keywords { get; }

        [JsonProperty("author")]
        public string? Author { get; set; }
    }
}
