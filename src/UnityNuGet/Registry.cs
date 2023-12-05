using System;
using System.Collections.Generic;
using System.IO;
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

        // A comparer is established for cases where the dependency name is not set to the correct case.
        // Example: https://www.nuget.org/packages/NeoSmart.Caching.Sqlite/0.1.0#dependencies-body-tab
        public Registry() : base(StringComparer.OrdinalIgnoreCase)
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
