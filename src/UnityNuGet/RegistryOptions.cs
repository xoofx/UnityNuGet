using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGet.Frameworks;

namespace UnityNuGet
{
    public class RegistryOptions
    {
        [Required]
        public Uri? RootHttpUrl { get; set; }

        [Required]
        [RegularExpression(@"[a-z]+\.[a-z]+")]
        public string? UnityScope { get; set; }

        [Required]
        [RegularExpression(@"\d+\.\d+")]
        public string? MinimumUnityVersion { get; set; }

        [Required]
        public string? PackageNameNuGetPostFix { get; set; }

        [Required]
        public string? RootPersistentFolder { get; set; }

        [Required]
        public TimeSpan UpdateInterval { get; set; }

        [Required]
        [ValidateEnumeratedItems]
        public RegistryTargetFramework[]? TargetFrameworks { get; set; }
    }

    public class RegistryTargetFramework
    {
        [Required]
        public string? Name { get; set; }

        [Required]
        public string[]? DefineConstraints { get; set; }

        [JsonIgnore]
        internal NuGetFramework? Framework { get; set; }
    }
}
