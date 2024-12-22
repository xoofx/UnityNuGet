using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnityNuGet
{
    /// <summary>
    ///
    /// </summary>
    public class UnityPackage : JsonObjectBase
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("unity")]
        public string? Unity { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; } = [];

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("dependencies")]
        public Dictionary<string, string> Dependencies { get; } = [];
    }
}
