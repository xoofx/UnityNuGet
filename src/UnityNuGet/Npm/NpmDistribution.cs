using System;
using Newtonsoft.Json;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Describes how a NPM package is distributed, used by <see cref="NpmPackageVersion"/>
    /// </summary>
    public class NpmDistribution : NpmObject
    {
        [JsonProperty("tarball")]
        public Uri Tarball { get; set; }

        [JsonProperty("shasum")]
        public string Shasum { get; set; }
    }
}