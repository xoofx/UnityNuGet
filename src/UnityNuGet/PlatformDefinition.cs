using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UnityNuGet
{
    internal enum UnityOs
    {
        AnyOs,
        Windows,
        Linux,
        OSX,
        Android,
        WebGL
    }

    internal enum UnityCpu
    {
        AnyCpu,
        X64,
        X86,
        ARM64,
        ARMv7,
        None,
    }

    internal static class UnityEnumExtensions
    {
        public static string GetPathName(this UnityOs os)
        {
            return os switch
            {
                UnityOs.AnyOs => string.Empty,
                _ => GetName(os),
            };
        }

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
                _ => throw new ArgumentException($"Unknown OS {os}"),
            };
        }

        public static string GetPathName(this UnityCpu cpu)
        {
            return cpu switch
            {
                UnityCpu.AnyCpu => string.Empty,
                _ => GetName(cpu),
            };
        }

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

    internal class PlatformDefinition
    {
        private readonly UnityOs _os;
        private readonly UnityCpu _cpu;
        private readonly bool _isEditor;
        private readonly List<PlatformDefinition> _children;

        private PlatformDefinition? _parent;

        public PlatformDefinition(UnityOs os, UnityCpu cpu, bool isEditorConfig)
        {
            _os = os;
            _cpu = cpu;
            _parent = null;
            _children = new();
            _isEditor = isEditorConfig;
        }

        public PlatformDefinition? Parent
        {
            get => _parent;

            private set
            {
                _parent = value;
            }
        }

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

        public int Depth
            => (_parent == null) ? 0 : (1 + _parent.Depth);

        public UnityOs Os
            => _os;

        public UnityCpu Cpu
            => _cpu;

        public override string ToString()
            => $"{_os}.{_cpu}";

        public PlatformDefinition? Find(UnityOs os, UnityCpu cpu)
        {
            // Test self
            if ((_os == os) && (_cpu == cpu))
            {
                return this;
            }

            // Recurse to children
            return _children
                .Select(c => c.Find(os, cpu))
                .Where(c => c != null)
                .FirstOrDefault();
        }

        public PlatformDefinition? Find(UnityOs os)
        {
            // Test self
            if (_os == os)
            {
                return this;
            }

            // Recurse to children
            return _children
                .Select(c => c.Find(os))
                .Where(c => c != null)
                .FirstOrDefault();
        }

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

        public static PlatformDefinition CreateAllPlatforms()
        {
            var root = new PlatformDefinition(UnityOs.AnyOs, UnityCpu.AnyCpu, isEditorConfig: true)
            {
                Children = new List<PlatformDefinition>()
                {
                    new PlatformDefinition(UnityOs.Windows, UnityCpu.AnyCpu, isEditorConfig: true)
                    {
                        Children = new List<PlatformDefinition>()
                        {
                            new PlatformDefinition(UnityOs.Windows, UnityCpu.X64, isEditorConfig: true),
                            new PlatformDefinition(UnityOs.Windows, UnityCpu.X86, isEditorConfig: false),
                        },
                    },
                    new PlatformDefinition(UnityOs.Linux, UnityCpu.X64, isEditorConfig: true),
                    new PlatformDefinition(UnityOs.Android, UnityCpu.ARMv7, isEditorConfig: false),
                    new PlatformDefinition(UnityOs.WebGL, UnityCpu.AnyCpu, isEditorConfig: false),
                    new PlatformDefinition(UnityOs.OSX, UnityCpu.AnyCpu, isEditorConfig: true)
                    {
                        Children = new List<PlatformDefinition>()
                        {
                            new PlatformDefinition(UnityOs.OSX, UnityCpu.X64, isEditorConfig: true),
                            new PlatformDefinition(UnityOs.OSX, UnityCpu.ARM64, isEditorConfig: true),
                        },
                    }
                }
            };

            return root;
        }

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

    internal class PlatformFile
    {
        private readonly PlatformDefinition _platform;
        private readonly string _sourcePath;

        public PlatformFile(string sourcePath, PlatformDefinition platform)
        {
            _sourcePath = sourcePath;
            _platform = platform;
        }

        public string SourcePath
            => _sourcePath;

        public PlatformDefinition Platform
            => _platform;

        public string GetDestinationPath(string basePath)
        {
            var fullPath = basePath;
            var depth = _platform.Depth;

            if (depth > 0)
            {
                fullPath = Path.Combine(fullPath, _platform.Os.GetPathName());
            }

            if (depth > 1)
            {
                fullPath = Path.Combine(fullPath, _platform.Cpu.GetPathName());
            }

            var fileName = Path.GetFileName(_sourcePath);
            fullPath = Path.Combine(fullPath, fileName);
            return fullPath;
        }
    }
}
