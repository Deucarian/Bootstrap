using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    internal readonly struct BootstrapChannelSelection
    {
        public BootstrapChannelSelection(
            BootstrapChannel channel,
            long changedAtUtcTicks,
            bool hasValue)
        {
            Channel = channel == BootstrapChannel.Development
                ? BootstrapChannel.Development
                : BootstrapChannel.Stable;
            ChangedAtUtcTicks = Math.Max(0L, changedAtUtcTicks);
            HasValue = hasValue;
        }

        public BootstrapChannel Channel { get; }

        public long ChangedAtUtcTicks { get; }

        public bool HasValue { get; }

        public static BootstrapChannelSelection None =>
            new BootstrapChannelSelection(BootstrapChannel.Stable, 0L, false);

        public static BootstrapChannelSelection Create(
            BootstrapChannel channel,
            long changedAtUtcTicks)
        {
            return new BootstrapChannelSelection(channel, changedAtUtcTicks, true);
        }
    }

    // Keep Bootstrap self-contained while writing the same project-scoped channel key
    // and timestamp that Package Installer uses for stable/development selection.
    internal static class BootstrapPackageInstallerStateRepository
    {
        internal const string ProjectChannelPreferencePrefix =
            "Deucarian.PackageManagement.SelectedChannel.";
        internal const string ProjectChannelChangedAtPreferencePrefix =
            "Deucarian.PackageManagement.SelectedChannelChangedAt.";
        private const string LegacyBootstrapChannelPreferencePrefix =
            "Deucarian.Bootstrap.Channel.";

        public static BootstrapChannel GetProjectChannel()
        {
            return GetProjectChannel(GetProjectRoot());
        }

        public static BootstrapChannelSelection GetProjectChannelSelection()
        {
            return GetProjectChannelSelection(GetProjectRoot());
        }

        public static void SetProjectChannel(BootstrapChannel channel)
        {
            SetProjectChannel(GetProjectRoot(), channel);
        }

        internal static BootstrapChannel GetProjectChannelForTests(string projectRoot)
        {
            return GetProjectChannel(projectRoot);
        }

        internal static BootstrapChannelSelection GetProjectChannelSelectionForTests(string projectRoot)
        {
            return GetProjectChannelSelection(projectRoot);
        }

        internal static void SetProjectChannelForTests(string projectRoot, BootstrapChannel channel)
        {
            SetProjectChannel(projectRoot, channel);
        }

        internal static void SetProjectChannelForTests(
            string projectRoot,
            BootstrapChannel channel,
            long changedAtUtcTicks)
        {
            SetProjectChannel(projectRoot, channel, changedAtUtcTicks);
        }

        internal static string GetProjectChannelPreferenceKeyForTests(string projectRoot)
        {
            return GetProjectChannelPreferenceKey(projectRoot);
        }

        internal static string GetProjectChannelChangedAtPreferenceKeyForTests(string projectRoot)
        {
            return GetProjectChannelChangedAtPreferenceKey(projectRoot);
        }

        internal static long GetProjectChannelChangedAtUtcTicksForTests(string projectRoot)
        {
            return GetStoredChangedAtUtcTicks(GetProjectChannelChangedAtPreferenceKey(projectRoot));
        }

        internal static string GetLegacyBootstrapChannelPreferenceKeyForTests(string projectRoot)
        {
            return GetLegacyBootstrapChannelPreferenceKey(projectRoot);
        }

        internal static void DeleteProjectChannelForTests(string projectRoot)
        {
            EditorPrefs.DeleteKey(GetProjectChannelPreferenceKey(projectRoot));
            EditorPrefs.DeleteKey(GetProjectChannelChangedAtPreferenceKey(projectRoot));
            EditorPrefs.DeleteKey(GetLegacyBootstrapChannelPreferenceKey(projectRoot));
        }

        private static BootstrapChannel GetProjectChannel(string projectRoot)
        {
            return GetProjectChannelSelection(projectRoot).Channel;
        }

        private static BootstrapChannelSelection GetProjectChannelSelection(string projectRoot)
        {
            string key = GetProjectChannelPreferenceKey(projectRoot);

            if (EditorPrefs.HasKey(key))
            {
                return BootstrapChannelSelection.Create(
                    ParseStoredProjectChannel(EditorPrefs.GetInt(key, (int)BootstrapChannel.Stable)),
                    GetStoredChangedAtUtcTicks(GetProjectChannelChangedAtPreferenceKey(projectRoot)));
            }

            string legacyBootstrapKey = GetLegacyBootstrapChannelPreferenceKey(projectRoot);

            if (EditorPrefs.HasKey(legacyBootstrapKey))
            {
                return BootstrapChannelSelection.Create(
                    ParseStoredProjectChannel(
                        EditorPrefs.GetInt(legacyBootstrapKey, (int)BootstrapChannel.Stable)),
                    0L);
            }

            return BootstrapChannelSelection.None;
        }

        private static void SetProjectChannel(string projectRoot, BootstrapChannel channel)
        {
            SetProjectChannel(projectRoot, channel, DateTime.UtcNow.Ticks);
        }

        private static void SetProjectChannel(
            string projectRoot,
            BootstrapChannel channel,
            long changedAtUtcTicks)
        {
            BootstrapChannel safeChannel = channel == BootstrapChannel.Development
                ? BootstrapChannel.Development
                : BootstrapChannel.Stable;
            EditorPrefs.SetInt(GetProjectChannelPreferenceKey(projectRoot), (int)safeChannel);
            EditorPrefs.SetString(
                GetProjectChannelChangedAtPreferenceKey(projectRoot),
                NormalizeChangedAtUtcTicks(changedAtUtcTicks).ToString());
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

        private static string GetProjectChannelChangedAtPreferenceKey(string projectRoot)
        {
            return ProjectChannelChangedAtPreferencePrefix + ComputeStableProjectHash(projectRoot);
        }

        private static string GetLegacyBootstrapChannelPreferenceKey(string projectRoot)
        {
            return LegacyBootstrapChannelPreferencePrefix + ComputeStableProjectHash(projectRoot);
        }

        private static long GetStoredChangedAtUtcTicks(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !EditorPrefs.HasKey(key))
            {
                return 0L;
            }

            return long.TryParse(EditorPrefs.GetString(key, "0"), out long changedAtUtcTicks)
                ? Math.Max(0L, changedAtUtcTicks)
                : 0L;
        }

        private static long NormalizeChangedAtUtcTicks(long changedAtUtcTicks)
        {
            return Math.Max(1L, changedAtUtcTicks);
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
