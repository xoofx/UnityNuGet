using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// Represents a `-/all` listing package NPM response
    /// </summary>
    /// <remarks>
    /// NOTE: only used to serialize from C# to JSON (JSON To C# is not implemented)
    /// </remarks>
    public class NpmPackageListAllResponse : NpmObject
    {
        [JsonPropertyName("_updated")]
        public int Updated { get; set; } = 99999;

        [JsonIgnore]
        public Dictionary<string, NpmPackageInfo> Packages { get; } = [];

        public void Reset()
        {
            Packages.Clear();
        }
    }
}
