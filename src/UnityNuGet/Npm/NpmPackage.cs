using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Describes a full NPM package (used as a response to `{packageId}`)
    /// </summary>
    public class NpmPackage : NpmObject
    {
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [JsonPropertyName("_rev")]
        public string Revision { get; set; } = "1-0";

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("license")]
        public string? License { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("dist-tags")]
        public Dictionary<string, string> DistTags { get; } = [];

        [JsonPropertyName("versions")]
        public Dictionary<string, NpmPackageVersion> Versions { get; } = [];

        [JsonPropertyName("time")]
        public Dictionary<string, DateTime> Time { get; } = [];

        [JsonPropertyName("repository")]
        public NpmSourceRepository? Repository { get; set; }

        [JsonPropertyName("users")]
        public Dictionary<string, string> Users { get; } = [];
    }
}
