namespace UnityNuGet.Server
{
    public sealed class RegistryCacheSingleton
    {
        public RegistryCacheSingleton(RegistryCache instance)
        {
            Instance = instance;
        }

        public RegistryCache Instance { get; set; }
    }
}