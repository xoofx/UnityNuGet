using Newtonsoft.Json;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// A simple object to return npm errors. Used mainly for returning <see cref="NotFound"/>
    /// </summary>
    public class NpmError(string error, string reason) : NpmObject
    {
        public static readonly NpmError NotFound = new("not_found", "document not found");

        [JsonProperty("error")]
        public string Error { get; } = error;

        [JsonProperty("reason")]
        public string Reason { get; } = reason;
    }
}
