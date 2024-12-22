using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Describes a package for "all" listing, used by <see cref="NpmPackageListAllResponse"/>
    /// </summary>
    public class NpmPackageInfo : NpmObject
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("maintainers")]
        public List<string> Maintainers { get; } = [];

        [JsonPropertyName("versions")]
        public Dictionary<string, string> Versions { get; } = [];

        [JsonPropertyName("time")]
        public DateTimeOffset? Time { get; set; }

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; } = [];

        [JsonPropertyName("author")]
        public string? Author { get; set; }
    }
}
