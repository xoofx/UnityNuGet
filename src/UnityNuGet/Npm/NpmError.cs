using Newtonsoft.Json;

namespace UnityNuGet.Npm
{
    /// <summary>
    /// A simple object to return npm errors. Used mainly for returning <see cref="NotFound"/>
    /// </summary>
    public class NpmError : NpmObject
    {
        public static readonly NpmError NotFound = new NpmError("not_found", "document not found"); 
        
        public NpmError(string error, string reason)
        {
            Error = error;
            Reason = reason;
        }

        [JsonProperty("error")]
        public string Error { get; }
        
        [JsonProperty("reason")]
        public string Reason { get; }
    }
}