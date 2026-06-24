using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    // Keep Bootstrap self-contained while writing the same project-scoped channel key
    // that the Package Installer uses for stable/development selection.
    internal static class BootstrapPackageInstallerStateRepository
    {
        internal const string ProjectChannelPreferencePrefix =
            "Deucarian.PackageManagement.SelectedChannel.";
        private const string LegacyBootstrapChannelPreferencePrefix =
            "Deucarian.Bootstrap.Channel.";

        public static BootstrapChannel GetProjectChannel()
        {
            return GetProjectChannel(GetProjectRoot());
        }

        public static void SetProjectChannel(BootstrapChannel channel)
        {
            SetProjectChannel(GetProjectRoot(), channel);
        }

        internal static BootstrapChannel GetProjectChannelForTests(string projectRoot)
        {
            return GetProjectChannel(projectRoot);
        }

        internal static void SetProjectChannelForTests(string projectRoot, BootstrapChannel channel)
        {
            SetProjectChannel(projectRoot, channel);
        }

        internal static string GetProjectChannelPreferenceKeyForTests(string projectRoot)
        {
            return GetProjectChannelPreferenceKey(projectRoot);
        }

        internal static string GetLegacyBootstrapChannelPreferenceKeyForTests(string projectRoot)
        {
            return GetLegacyBootstrapChannelPreferenceKey(projectRoot);
        }

        internal static void DeleteProjectChannelForTests(string projectRoot)
        {
            EditorPrefs.DeleteKey(GetProjectChannelPreferenceKey(projectRoot));
            EditorPrefs.DeleteKey(GetLegacyBootstrapChannelPreferenceKey(projectRoot));
        }

        private static BootstrapChannel GetProjectChannel(string projectRoot)
        {
            string key = GetProjectChannelPreferenceKey(projectRoot);

            if (EditorPrefs.HasKey(key))
            {
                return ParseStoredProjectChannel(EditorPrefs.GetInt(key, (int)BootstrapChannel.Stable));
            }

            string legacyBootstrapKey = GetLegacyBootstrapChannelPreferenceKey(projectRoot);

            if (EditorPrefs.HasKey(legacyBootstrapKey))
            {
                return ParseStoredProjectChannel(EditorPrefs.GetInt(legacyBootstrapKey, (int)BootstrapChannel.Stable));
            }

            return BootstrapChannel.Stable;
        }

        private static void SetProjectChannel(string projectRoot, BootstrapChannel channel)
        {
            BootstrapChannel safeChannel = channel == BootstrapChannel.Development
                ? BootstrapChannel.Development
                : BootstrapChannel.Stable;
            EditorPrefs.SetInt(GetProjectChannelPreferenceKey(projectRoot), (int)safeChannel);
        }

        private static BootstrapChannel ParseStoredProjectChannel(int value)
        {
            return value == (int)BootstrapChannel.Development
                ? BootstrapChannel.Development
                : BootstrapChannel.Stable;
        }

        private static string GetProjectChannelPreferenceKey(string projectRoot)
        {
            return ProjectChannelPreferencePrefix + ComputeStableProjectHash(projectRoot);
        }

        private static string GetLegacyBootstrapChannelPreferenceKey(string projectRoot)
        {
            return LegacyBootstrapChannelPreferencePrefix + ComputeStableProjectHash(projectRoot);
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

        private static string ComputeStableProjectHash(string projectRoot)
        {
            string normalizedProjectRoot = (projectRoot ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/')
                .ToLowerInvariant();

            unchecked
            {
                const uint offsetBasis = 2166136261;
                const uint prime = 16777619;
                uint hash = offsetBasis;

                for (int i = 0; i < normalizedProjectRoot.Length; i++)
                {
                    hash ^= normalizedProjectRoot[i];
                    hash *= prime;
                }

                return hash.ToString("x8");
            }
        }
    }
}
