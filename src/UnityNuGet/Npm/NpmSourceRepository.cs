using System.Text.Json.Serialization;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Describes a source repository used both by <see cref="NpmPackageVersion.Repository"/> and <see cref="NpmPackage.Repository"/>
    /// </summary>
    public partial class NpmSourceRepository : NpmObject
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("revision")]
        public string? Revision { get; set; }

        public NpmSourceRepository Clone() => (NpmSourceRepository)MemberwiseClone();
    }
}
