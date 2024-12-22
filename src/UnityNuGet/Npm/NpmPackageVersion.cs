using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Describes a version of a <see cref="NpmPackage"/>
    /// </summary>
    public class NpmPackageVersion : NpmObject
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("dist")]
        public NpmDistribution Distribution { get; } = new();

        [JsonPropertyName("dependencies")]
        public Dictionary<string, string> Dependencies { get; } = [];

        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [JsonPropertyName("unity")]
        public string? Unity { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("scripts")]
        public Dictionary<string, string> Scripts { get; } = [];

        [JsonPropertyName("repository")]
        public NpmSourceRepository? Repository { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }
    }
}
