using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace UnityNuGet
{
    /// <summary>
    /// Loads the `registry.json` file at startup
    /// </summary>
    [Serializable]
    public sealed class Registry : Dictionary<string, RegistryEntry>
    {
        private const string RegistryFileName = "registry.json";
        private static readonly object LockRead = new();
        private static Registry? _registry = null;

        public Registry()
        {
        }

        private Registry(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public static Registry Parse(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            return JsonConvert.DeserializeObject<Registry>(json, JsonCommonExtensions.Settings)!;
        }

        public static Registry GetInstance()
        {
            lock (LockRead)
            {
                _registry ??= Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(typeof(Registry).Assembly.Location)!, RegistryFileName)));
            }
            return _registry;
        }
    }
}
