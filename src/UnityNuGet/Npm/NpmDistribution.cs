using System;
using System.Text.Json.Serialization;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Describes how a NPM package is distributed, used by <see cref="NpmPackageVersion"/>
    /// </summary>
    public class NpmDistribution : NpmObject
    {
        [JsonPropertyName("tarball")]
        public Uri? Tarball { get; set; }

        [JsonPropertyName("shasum")]
        public string? Shasum { get; set; }
    }
}
