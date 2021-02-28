using Newtonsoft.Json;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Describes a source repository used both by <see cref="NpmPackageVersion.Repository"/> and <see cref="NpmPackage.Repository"/>
    /// </summary>
    public partial class NpmSourceRepository : NpmObject
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("revision")]
        public string Revision { get; set; }

        public NpmSourceRepository Clone()
        {
            return (NpmSourceRepository)MemberwiseClone();
        }
    }
}
