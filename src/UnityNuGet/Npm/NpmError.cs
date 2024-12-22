using System.Text.Json.Serialization;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// A simple object to return npm errors. Used mainly for returning <see cref="NotFound"/>
    /// </summary>
    [method: JsonConstructor]
    public class NpmError(string error, string reason) : NpmObject
    {
        public static readonly NpmError NotFound = new("not_found", "document not found");

        [JsonPropertyName("error")]
        public string Error { get; } = error;

        [JsonPropertyName("reason")]
        public string Reason { get; } = reason;
    }
}
