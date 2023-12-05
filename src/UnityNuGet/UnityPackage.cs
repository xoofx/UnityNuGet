using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnityNuGet
{
    /// <summary>
    /// 
    /// </summary>
    public class UnityPackage : JsonObjectBase
    {
        public UnityPackage()
        {
            Keywords = [];
            Dependencies = [];
        }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("displayName")]
        public string? DisplayName { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }

        [JsonProperty("unity")]
        public string? Unity { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("keywords")]
        public List<string> Keywords { get; }

        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("dependencies")]
        public Dictionary<string, string> Dependencies { get; }
    }
}
