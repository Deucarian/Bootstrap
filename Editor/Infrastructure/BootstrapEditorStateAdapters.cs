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
            return ShouldShowForProject(GetProjectRoot());
        }

        public static void SetShouldShow(bool value)
        {
            SetShouldShowForProject(GetProjectRoot(), value);
        }

        public static bool RetireIfAuthoritativelyHealthy(BootstrapSetupSnapshot snapshot)
        {
            return RetireIfAuthoritativelyHealthyForProject(snapshot, GetProjectRoot());
        }

        internal static bool ShouldShowForProject(string projectRoot)
        {
            return EditorPrefs.GetBool(GetPreferenceKey(projectRoot), true);
        }

        internal static void SetShouldShowForProject(string projectRoot, bool value)
        {
            EditorPrefs.SetBool(GetPreferenceKey(projectRoot), value);
        }

        internal static bool RetireIfAuthoritativelyHealthyForProject(
            BootstrapSetupSnapshot snapshot,
            string projectRoot)
        {
            if (!BootstrapWindowLifecyclePolicy.ShouldRetireAutomaticStartup(snapshot) ||
                !ShouldShowForProject(projectRoot))
            {
                return false;
            }

            SetShouldShowForProject(projectRoot, false);
            return true;
        }

        internal static void DeleteForProjectForTests(string projectRoot)
        {
            EditorPrefs.DeleteKey(GetPreferenceKey(projectRoot));
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
