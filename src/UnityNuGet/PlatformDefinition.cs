using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnityNuGet
{
    /// <summary>
    /// All operating systems supported by Unity
    /// </summary>
    public enum UnityOs
    {
        AnyOs,
        Windows,
        Linux,
        OSX,
        Android,
        WebGL,
        iOS
    }

    /// <summary>
    /// All CPUs supported by Unity
    /// </summary>
    public enum UnityCpu
    {
        AnyCpu,
        X64,
        X86,
        ARM64,
        ARMv7,
        None,
    }

    /// <summary>
    /// Extensions for <see cref="UnityOs"/> and <see cref="UnityCpu"/>
    /// </summary>
    internal static class UnityEnumExtensions
    {
        /// <summary>
        /// Converts a <see cref="UnityOs"/> to string.
        /// </summary>
        /// <param name="os">The value to be converted.</param>
        /// <remarks>This method should be used for constructing package paths for OS-dependent files.</remarks>
        /// <returns>A string representation of the provided <see cref="UnityOs"/> value.</returns>
        public static string GetPathName(this UnityOs os)
        {
            return os switch
            {
                UnityOs.AnyOs => string.Empty,
                _ => GetName(os),
            };
        }

        /// <summary>
        /// Converts a <see cref="UnityOs"/> to a string representation accepted by the Unity .meta file format.
        /// </summary>
        /// <param name="os">The value to be converted.</param>
        /// <returns>A string representation of the provided <see cref="UnityOs"/> value.</returns>
        public static string GetName(this UnityOs os)
        {
            return os switch
            {
                UnityOs.AnyOs => "AnyOS",
                UnityOs.Windows => "Windows",
                UnityOs.Linux => "Linux",
                UnityOs.OSX => "OSX",
                UnityOs.Android => "Android",
                UnityOs.WebGL => "WebGL",
                UnityOs.iOS => "iOS",
                _ => throw new ArgumentException($"Unknown OS {os}"),
            };
        }

        /// <summary>
        /// Converts a <see cref="UnityCpu"/> to string.
        /// </summary>
        /// <param name="cpu">The value to be converted.</param>
        /// <remarks>This method should be used for constructing package paths for CPU-dependent files.</remarks>
        /// <returns>A string representation of the provided <see cref="UnityCpu"/> value.</returns>
        public static string GetPathName(this UnityCpu cpu)
        {
            return cpu switch
            {
                UnityCpu.AnyCpu => string.Empty,
                _ => GetName(cpu),
            };
        }

        /// <summary>
        /// Converts a <see cref="UnityCpu"/> to a string representation accepted by the Unity .meta file format.
        /// </summary>
        /// <param name="cpu">The value to be converted.</param>
        /// <returns>A string representation of the provided <see cref="UnityCpu"/> value.</returns>
        public static string GetName(this UnityCpu cpu)
        {
            return cpu switch
            {
                UnityCpu.AnyCpu => "AnyCPU",
                UnityCpu.X64 => "x86_64",
                UnityCpu.X86 => "x86",
                UnityCpu.ARM64 => "ARM64",
                UnityCpu.ARMv7 => "ARMv7",
                UnityCpu.None => "None",
                _ => throw new ArgumentException($"Unknown CPU {cpu}"),
            };
        }
    }

    /// <summary>
    /// A subtree of hierarchical (os, cpu) configuration tuples that are supported by Unity.
    /// </summary>
    /// <remarks>
    /// The root <see cref="PlatformDefinition"/> node is typically the most general configuration, i.e.
    /// supporting <see cref="UnityOs.AnyOs"/> and <see cref="UnityCpu.AnyCpu"/>. Leaf nodes are typically specialized,
    /// targeting a specific OS and CPU flavor.
    /// </remarks>
    /// <remarks>
    /// Creates a new <see cref="PlatformDefinition"/>  instance.
    /// </remarks>
    /// <param name="os">The OS.</param>
    /// <param name="cpu">The CPU flavor.</param>
    /// <param name="isEditorConfig">True if the Unity editor is available in this (os, cpu) tuple.</param>
    internal class PlatformDefinition(UnityOs os, UnityCpu cpu, bool isEditorConfig)
    {
        private readonly UnityOs _os = os;
        private readonly UnityCpu _cpu = cpu;
        private readonly bool _isEditor = isEditorConfig;
        private readonly List<PlatformDefinition> _children = [];

        private PlatformDefinition? _parent = null;

        /// <summary>
        /// The parent <see cref="PlatformDefinition"/> that is a superset of <c>this</c>.
        /// </summary>
        public PlatformDefinition? Parent
        {
            get => _parent;

            private set
            {
                _parent = value;
            }
        }

        /// <summary>
        /// The child <see cref="PlatformDefinition"/> configurations <c>this</c> is a superset of.
        /// </summary>
        public IReadOnlyList<PlatformDefinition> Children
        {
            get => _children;

            private set
            {
                _children.AddRange(value);

                foreach (var child in _children)
                {
                    child.Parent = this;
                }
            }
        }

        /// <summary>
        /// The distance from the root <see cref="PlatformDefinition"/> in the configuration tree.
        /// </summary>
        public int Depth
            => (_parent == null) ? 0 : (1 + _parent.Depth);

        /// <summary>
        /// The operating system.
        /// </summary>
        public UnityOs Os
            => _os;

        /// <summary>
        /// The CPU flavor.
        /// </summary>
        public UnityCpu Cpu
            => _cpu;

        /// <inheritdoc/>
        public override string ToString()
            => $"{_os}.{_cpu}";

        /// <summary>
        /// Attempts to find a <see cref="PlatformDefinition"/> that matches the given <see cref="UnityOs"/>
        /// and <see cref="UnityCpu"/> among the descendants of <c>this</c> configuration.
        /// </summary>
        /// <param name="os">The operating system to match.</param>
        /// <param name="cpu">The CPU flavor to match.</param>
        /// <returns>A matching <see cref="PlatformDefinition"/>.</returns>
        public PlatformDefinition? Find(UnityOs os, UnityCpu? cpu = default)
        {
            // Test self
            if ((_os == os) && ((cpu == null) || (_cpu == cpu)))
            {
                return this;
            }

            // Recurse to children
            return _children
                .Select(c => c.Find(os, cpu))
                .Where(c => c != null)
                .FirstOrDefault();
        }

        /// <summary>
        /// Attempts to find a <see cref="PlatformDefinition"/> for which the Unity editor is available
        /// among the descendants of <c>this</c> configuration.
        /// </summary>
        /// <returns>A matching <see cref="PlatformDefinition"/>.</returns>
        public PlatformDefinition? FindEditor()
        {
            // Test self
            if (_isEditor)
            {
                return this;
            }

            // Recurse to children
            return _children
                .Select(c => c.FindEditor())
                .Where(c => c != null)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the difference set of configurations <c>this</c> <see cref="PlatformDefinition"/> is a superset of,
        /// that are not already part of the given visited set.
        /// </summary>
        /// <param name="visitedPlatforms">The set of already visited configurations, that should not be part of the returned set.</param>
        /// <returns>
        /// A set of configurations <c>this</c> <see cref="PlatformDefinition"/> is a superset of,
        /// that are not already part of the given visited set.
        /// </returns>
        public HashSet<PlatformDefinition> GetRemainingPlatforms(IReadOnlySet<PlatformDefinition> visitedPlatforms)
        {
            var remainingPlatforms = new HashSet<PlatformDefinition>
            {
                // Push the root
                this
            };

            for (bool found = true; found;)
            {
                found = false;

                foreach (var p in remainingPlatforms)
                {
                    // Remove p if already visited
                    if (visitedPlatforms.Contains(p))
                    {
                        remainingPlatforms.Remove(p);
                        found = true;
                        break;
                    }

                    // If p has descendants that were visited, we can't use it and need to expand it
                    if (p.HasVisitedDescendants(visitedPlatforms))
                    {
                        remainingPlatforms.Remove(p);

                        foreach (var c in p.Children)
                        {
                            remainingPlatforms.Add(c);
                        }

                        found = true;
                        break;
                    }
                }
            }

            return remainingPlatforms;
        }

        /// <summary>
        /// Creates the tree of all known (os, cpu) configurations supported by Unity
        /// </summary>
        /// <returns></returns>
        public static PlatformDefinition CreateAllPlatforms()
        {
            var root = new PlatformDefinition(UnityOs.AnyOs, UnityCpu.AnyCpu, isEditorConfig: true)
            {
                Children = new List<PlatformDefinition>()
                {
                    new(UnityOs.Windows, UnityCpu.AnyCpu, isEditorConfig: true)
                    {
                        Children = new List<PlatformDefinition>()
                        {
                            new(UnityOs.Windows, UnityCpu.X64, isEditorConfig: true),
                            new(UnityOs.Windows, UnityCpu.X86, isEditorConfig: false),
                        },
                    },
                    new(UnityOs.Linux, UnityCpu.X64, isEditorConfig: true),
                    new(UnityOs.Android, UnityCpu.ARMv7, isEditorConfig: false),
                    new(UnityOs.WebGL, UnityCpu.AnyCpu, isEditorConfig: false),
                    new(UnityOs.iOS, UnityCpu.AnyCpu, isEditorConfig: false),
                    new(UnityOs.OSX, UnityCpu.AnyCpu, isEditorConfig: true)
                    {
                        Children = new List<PlatformDefinition>()
                        {
                            new(UnityOs.OSX, UnityCpu.X64, isEditorConfig: true),
                            new(UnityOs.OSX, UnityCpu.ARM64, isEditorConfig: true),
                        },
                    }
                }
            };

            return root;
        }

        /// <summary>
        /// Returns true if either <c>this</c> <see cref="PlatformDefinition"/> or any of its descendants 
        /// is also part of the given set of visited configurations.</summary>
        /// <param name="visitedPlatforms">The set of visited configurations to test against.</param>
        /// <returns></returns>
        private bool HasVisitedDescendants(IReadOnlySet<PlatformDefinition> visitedPlatforms)
        {
            if (visitedPlatforms.Contains(this))
            {
                return true;
            }

            foreach (var c in _children)
            {
                if (c.HasVisitedDescendants(visitedPlatforms))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Associates a file path with the <see cref="PlatformDefinition"/> the file is compatible with.
    /// </summary>
    /// <remarks>
    /// Creates a new <see cref="PlatformFile"/> instance.
    /// </remarks>
    /// <param name="sourcePath">The full path of the file in the original NuGet package.</param>
    /// <param name="platform">The platform the file is compatible with.</param>
    internal class PlatformFile(string sourcePath, PlatformDefinition platform)
    {
        private readonly PlatformDefinition _platform = platform;
        private readonly string _sourcePath = sourcePath;

        /// <summary>
        /// The full path of the file in the original NuGet package.
        /// </summary>
        public string SourcePath
            => _sourcePath;

        /// <summary>
        /// The <see cref="PlatformDefinition"/> the file is compatible with.
        /// </summary>
        public PlatformDefinition Platform
            => _platform;

        /// <summary>
        /// Returns the full path of the file in the UPM package.
        /// </summary>
        /// <param name="basePath">The file path to start from.</param>
        /// <returns>The full path of the file in the UPM package.</returns>
        public string GetDestinationPath(string basePath)
        {
            // We start with just the base path
            var fullPath = basePath;
            var depth = _platform.Depth;

            if (depth > 0)
            {
                // Our configuration is not AnyOS, add a /<os_name> to our full path
                fullPath = Path.Combine(fullPath, _platform.Os.GetPathName());
            }

            if (depth > 1)
            {
                // Our CPU os not AnyCPU, add a /<cpu-name> to our full path
                fullPath = Path.Combine(fullPath, _platform.Cpu.GetPathName());
            }

            // Finally, append the file name and return
            var fileName = Path.GetFileName(_sourcePath);
            fullPath = Path.Combine(fullPath, fileName);
            return fullPath;
        }
    }
}
