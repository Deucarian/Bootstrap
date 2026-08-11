using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    internal interface IBootstrapChannelStore
    {
        BootstrapChannel Get();

        void Set(BootstrapChannel channel);
    }

    internal sealed class BootstrapSharedChannelStore : IBootstrapChannelStore
    {
        public BootstrapChannel Get()
        {
            return BootstrapPackageInstallerStateRepository.GetProjectChannel();
        }

        public void Set(BootstrapChannel channel)
        {
            BootstrapPackageInstallerStateRepository.SetProjectChannel(channel);
        }
    }

    internal interface IBootstrapLegacyRegistryInspector
    {
        BootstrapScopedRegistryStatus Inspect();
    }

    internal sealed class BootstrapLegacyRegistryInspector : IBootstrapLegacyRegistryInspector
    {
        public BootstrapScopedRegistryStatus Inspect()
        {
            return BootstrapScopedRegistryManifest.GetStatus();
        }
    }

    internal static class BootstrapStartupPreferences
    {
        private const string Prefix = "Deucarian.Bootstrap.ShowOnStartup.";

        public static bool ShouldShow()
        {
            return EditorPrefs.GetBool(GetPreferenceKey(GetProjectRoot()), true);
        }

        public static void SetShouldShow(bool value)
        {
            EditorPrefs.SetBool(GetPreferenceKey(GetProjectRoot()), value);
        }

        internal static string GetPreferenceKey(string projectRoot)
        {
            string normalized = (projectRoot ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/')
                .ToLowerInvariant();
            return Prefix + ComputeStableHash(normalized);
        }

        private static string GetProjectRoot()
        {
            if (string.IsNullOrWhiteSpace(Application.dataPath))
            {
                return string.Empty;
            }

            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Application.dataPath;
        }

        private static string ComputeStableHash(string value)
        {
            unchecked
            {
                const uint offsetBasis = 2166136261;
                const uint prime = 16777619;
                uint hash = offsetBasis;

                for (int index = 0; index < (value ?? string.Empty).Length; index++)
                {
                    hash ^= value[index];
                    hash *= prime;
                }

                return hash.ToString("x8");
            }
        }
    }
}
