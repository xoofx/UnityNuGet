using System.Text.Json.Serialization;

namespace UnityNuGet
{
    public class UnityAsmdef : JsonObjectBase
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        // The values come from: https://docs.unity3d.com/ScriptReference/BuildTarget.html
        [JsonPropertyName("includePlatforms")]
        public string[]? IncludePlatforms { get; set; }
    }
}
