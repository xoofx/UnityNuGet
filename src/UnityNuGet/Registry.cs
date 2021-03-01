using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace UnityNuGet
{
    /// <summary>
    /// Loads the `registry.json` file at startup
    /// </summary>
    public sealed class Registry : Dictionary<string, RegistryEntry>
    {
        private const string RegistryFileName = "registry.json";
        private static readonly object LockRead = new object();
        private static Registry _registry = null;

        public static Registry Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            return JsonConvert.DeserializeObject<Registry>(json, JsonCommonExtensions.Settings);
        }

        public static Registry GetInstance()
        {
            lock (LockRead)
            {
                if (_registry == null)
                {
                    _registry = Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(typeof(Registry).Assembly.Location), RegistryFileName)));
                }
            }
            return _registry;
        }
    }
}
