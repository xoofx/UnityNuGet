using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        public NpmPackageListAllResponse()
        {
            Unused = 99999;
            Packages = [];
        }

        [JsonProperty("_updated")]
        public int Unused { get; set; }

        [JsonIgnore]
        public Dictionary<string, NpmPackageInfo> Packages { get; }

        // Everything else gets stored here
        [JsonExtensionData]
        private IDictionary<string, JToken> AdditionalData
        {
            get
            {
                var marshalPackages = new Dictionary<string, JToken>();
                foreach (var packagePair in Packages)
                {
                    marshalPackages.Add(packagePair.Key, JObject.FromObject(packagePair.Value));
                }

                return marshalPackages;
            }
        }

        public void Reset()
        {
            Packages.Clear();
        }
    }
}
