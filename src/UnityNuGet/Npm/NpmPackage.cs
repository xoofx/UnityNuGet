using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Describes a full NPM package (used as a response to `{packageId}`)
    /// </summary>
    public class NpmPackage : NpmObject
    {
        public NpmPackage()
        {
            Revision = "1-0";
            DistTags = [];
            Versions = [];
            Time = [];
            Users = [];
        }

        [JsonProperty("_id")]
        public string? Id { get; set; }

        [JsonProperty("_rev")]
        public string Revision { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("license")]
        public string? License { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("dist-tags")]
        public Dictionary<string, string> DistTags { get; }

        [JsonProperty("versions")]
        public Dictionary<string, NpmPackageVersion> Versions { get; }

        [JsonProperty("time")]
        public Dictionary<string, DateTime> Time { get; }

        [JsonProperty("repository", NullValueHandling = NullValueHandling.Ignore)]
        public NpmSourceRepository? Repository { get; set; }

        [JsonProperty("users")]
        public Dictionary<string, string> Users { get; }
    }
}
