using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Describes a version of a <see cref="NpmPackage"/>
    /// </summary>
    public class NpmPackageVersion : NpmObject
    {
        public NpmPackageVersion()
        {
            Dependencies = [];
            Distribution = new();
            Scripts = [];
        }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }

        [JsonProperty("dist")]
        public NpmDistribution Distribution { get; }

        [JsonProperty("dependencies")]
        public Dictionary<string, string> Dependencies { get; }

        [JsonProperty("_id")]
        public string? Id { get; set; }

        [JsonProperty("unity", NullValueHandling = NullValueHandling.Ignore)]
        public string? Unity { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string? Description { get; set; }

        [JsonProperty("displayName", NullValueHandling = NullValueHandling.Ignore)]
        public string? DisplayName { get; set; }

        [JsonProperty("scripts", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> Scripts { get; }

        [JsonProperty("repository", NullValueHandling = NullValueHandling.Ignore)]
        public NpmSourceRepository? Repository { get; set; }

        [JsonProperty("author", NullValueHandling = NullValueHandling.Ignore)]
        public string? Author { get; set; }
    }
}
