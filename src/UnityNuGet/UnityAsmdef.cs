using Newtonsoft.Json;

namespace UnityNuGet
{
    public class UnityAsmdef : JsonObjectBase
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        // The values come from: https://docs.unity3d.com/ScriptReference/BuildTarget.html
        [JsonProperty("includePlatforms")]
        public string[]? IncludePlatforms { get; set; }
    }
}
