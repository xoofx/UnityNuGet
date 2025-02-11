using System.Collections.Generic;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace UnityNuGet
{
    /// <summary>
    /// An entry in the <see cref="Registry"/>
    /// </summary>
    public class RegistryEntry
    {
        [JsonPropertyName("ignore")]
        public bool Ignored { get; set; }

        [JsonPropertyName("listed")]
        public bool Listed { get; set; }

        [JsonPropertyName("version")]
        public VersionRange? Version { get; set; }

        [JsonPropertyName("defineConstraints")]
        public List<string> DefineConstraints { get; set; } = [];

        [JsonPropertyName("analyzer")]
        public bool Analyzer { get; set; }

        [JsonPropertyName("includePrerelease")]
        public bool IncludePrerelease { get; set; }

        [JsonPropertyName("includeUnlisted")]
        public bool IncludeUnlisted { get; set; }
    }
}
