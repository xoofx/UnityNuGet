using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UnityNuGet
{
    public class RegistryOptions
    {
        [Required]
        public Uri RootHttpUrl { get; set; }

        [Required]
        [RegularExpression(@"[a-z]+\.[a-z]+")]
        public string UnityScope { get; set; }

        [Required]
        [RegularExpression(@"\d+\.\d+")]
        public string MinimumUnityVersion { get; set; }

        [Required]
        public string PackageNameNuGetPostFix { get; set; }

        [Required]
        public string RootPersistentFolder { get; set; }

        [Required]
        public TimeSpan UpdateInterval { get; set; }

        [Required]
        public RegistryTargetFramework TargetFramework { get; set; }
    }

    public class RegistryTargetFramework
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string DefineConstraint { get; set; }
    }
}
