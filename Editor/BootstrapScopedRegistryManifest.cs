using System;
using System.IO;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    internal enum BootstrapScopedRegistryState
    {
        NotInspected,
        Missing,
        Valid,
        Invalid,
        Duplicate,
        Error
    }

    /// <summary>
    /// Read-only inspection of the legacy scoped registry configuration.
    /// Bootstrap never creates, repairs, or removes scoped registry entries.
    /// </summary>
    internal static class BootstrapScopedRegistryManifest
    {
        public static BootstrapScopedRegistryStatus GetStatus()
        {
            return GetStatus(GetProjectManifestPath());
        }

        public static BootstrapScopedRegistryStatus GetStatus(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return BootstrapScopedRegistryStatus.CreateError(
                    string.Empty,
                    "Project manifest path is empty.");
            }

            if (!File.Exists(manifestPath))
            {
                return BootstrapScopedRegistryStatus.CreateError(
                    manifestPath,
                    "Packages/manifest.json was not found.");
            }

            if (!TryReadManifest(manifestPath, out BootstrapManifestDto manifest, out string errorMessage))
            {
                return BootstrapScopedRegistryStatus.CreateError(manifestPath, errorMessage);
            }

            return Evaluate(manifestPath, manifest.scopedRegistries);
        }

        private static BootstrapScopedRegistryStatus Evaluate(
            string manifestPath,
            BootstrapScopedRegistryDto[] scopedRegistries)
        {
            if (scopedRegistries == null || scopedRegistries.Length == 0)
            {
                return BootstrapScopedRegistryStatus.CreateMissing(
                    manifestPath,
                    "No legacy Deucarian scoped registry entry was found.");
            }

            BootstrapScopedRegistryDto candidate = null;
            int candidateCount = 0;
            bool duplicateScope = false;

            for (int i = 0; i < scopedRegistries.Length; i++)
            {
                BootstrapScopedRegistryDto registry = scopedRegistries[i];
                if (registry == null)
                {
                    continue;
                }

                int matchingScopeCount = CountMatchingScopes(registry.scopes);
                bool hasDeucarianName = string.Equals(
                    registry.name,
                    DeucarianBootstrapPackageConstants.ScopedRegistryName,
                    StringComparison.Ordinal);

                if (!hasDeucarianName && matchingScopeCount == 0)
                {
                    continue;
                }

                candidateCount++;
                candidate = candidate ?? registry;
                duplicateScope |= matchingScopeCount > 1;
            }

            if (candidateCount == 0)
            {
                return BootstrapScopedRegistryStatus.CreateMissing(
                    manifestPath,
                    "No legacy Deucarian scoped registry entry was found.");
            }

            if (candidateCount > 1 || duplicateScope)
            {
                return BootstrapScopedRegistryStatus.CreateDuplicate(
                    manifestPath,
                    "Multiple legacy Deucarian scoped registry entries or scopes were found.");
            }

            if (IsExpectedConfiguration(candidate))
            {
                return BootstrapScopedRegistryStatus.CreateConfigured(
                    manifestPath,
                    "The legacy Deucarian scoped registry configuration is valid.");
            }

            return BootstrapScopedRegistryStatus.CreateInvalid(
                manifestPath,
                "The legacy Deucarian scoped registry entry has an unexpected name, URL, or scope.");
        }

        private static bool IsExpectedConfiguration(BootstrapScopedRegistryDto registry)
        {
            return registry != null &&
                   string.Equals(
                       registry.name,
                       DeucarianBootstrapPackageConstants.ScopedRegistryName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       registry.url,
                       DeucarianBootstrapPackageConstants.ScopedRegistryUrl,
                       StringComparison.OrdinalIgnoreCase) &&
                   CountMatchingScopes(registry.scopes) == 1;
        }

        private static int CountMatchingScopes(string[] scopes)
        {
            if (scopes == null)
            {
                return 0;
            }

            int count = 0;

            for (int i = 0; i < scopes.Length; i++)
            {
                if (string.Equals(
                    scopes[i],
                    DeucarianBootstrapPackageConstants.ScopedRegistryScope,
                    StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryReadManifest(
            string manifestPath,
            out BootstrapManifestDto manifest,
            out string errorMessage)
        {
            manifest = null;
            errorMessage = string.Empty;

            string json;

            try
            {
                json = File.ReadAllText(manifestPath);
            }
            catch (Exception exception)
            {
                errorMessage = "Could not read Packages/manifest.json: " +
                               exception.GetBaseException().Message;
                return false;
            }

            string trimmedJson = (json ?? string.Empty).Trim();
            if (trimmedJson.Length < 2 ||
                trimmedJson[0] != '{' ||
                trimmedJson[trimmedJson.Length - 1] != '}')
            {
                errorMessage = "Packages/manifest.json must contain a JSON object.";
                return false;
            }

            try
            {
                manifest = JsonUtility.FromJson<BootstrapManifestDto>(trimmedJson);
            }
            catch (Exception exception)
            {
                errorMessage = "Could not parse Packages/manifest.json: " +
                               exception.GetBaseException().Message;
                return false;
            }

            if (manifest == null)
            {
                errorMessage = "Could not parse Packages/manifest.json.";
                return false;
            }

            return true;
        }

        private static string GetProjectManifestPath()
        {
            if (string.IsNullOrWhiteSpace(Application.dataPath))
            {
                return string.Empty;
            }

            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            return projectRoot == null
                ? string.Empty
                : Path.Combine(projectRoot.FullName, "Packages", "manifest.json");
        }

        [Serializable]
        private sealed class BootstrapManifestDto
        {
            public BootstrapScopedRegistryDto[] scopedRegistries;
        }

        [Serializable]
        private sealed class BootstrapScopedRegistryDto
        {
            public string name;
            public string url;
            public string[] scopes;
        }
    }

    internal sealed class BootstrapScopedRegistryStatus
    {
        private BootstrapScopedRegistryStatus(
            string manifestPath,
            BootstrapScopedRegistryState state,
            string detail)
        {
            ManifestPath = manifestPath ?? string.Empty;
            State = state;
            Detail = detail ?? string.Empty;
        }

        public static BootstrapScopedRegistryStatus NotInspected { get; } =
            new BootstrapScopedRegistryStatus(
                string.Empty,
                BootstrapScopedRegistryState.NotInspected,
                "Legacy scoped registry configuration has not been inspected.");

        public string ManifestPath { get; }

        public BootstrapScopedRegistryState State { get; }

        public bool Configured => State == BootstrapScopedRegistryState.Valid;

        // Kept for compatibility with the previous Bootstrap presentation API.
        // Scoped registry repair is migration guidance only; this inspector never writes it.
        public bool NeedsRepair =>
            State == BootstrapScopedRegistryState.Missing ||
            State == BootstrapScopedRegistryState.Invalid ||
            State == BootstrapScopedRegistryState.Duplicate;

        public string Detail { get; }

        public static BootstrapScopedRegistryStatus CreateConfigured(string manifestPath, string detail)
        {
            return new BootstrapScopedRegistryStatus(
                manifestPath,
                BootstrapScopedRegistryState.Valid,
                detail);
        }

        public static BootstrapScopedRegistryStatus CreateRepairNeeded(string manifestPath, string detail)
        {
            return CreateInvalid(manifestPath, detail);
        }

        public static BootstrapScopedRegistryStatus CreateMissing(string manifestPath, string detail)
        {
            return new BootstrapScopedRegistryStatus(
                manifestPath,
                BootstrapScopedRegistryState.Missing,
                detail);
        }

        public static BootstrapScopedRegistryStatus CreateInvalid(string manifestPath, string detail)
        {
            return new BootstrapScopedRegistryStatus(
                manifestPath,
                BootstrapScopedRegistryState.Invalid,
                detail);
        }

        public static BootstrapScopedRegistryStatus CreateDuplicate(string manifestPath, string detail)
        {
            return new BootstrapScopedRegistryStatus(
                manifestPath,
                BootstrapScopedRegistryState.Duplicate,
                detail);
        }

        public static BootstrapScopedRegistryStatus CreateError(string manifestPath, string detail)
        {
            return new BootstrapScopedRegistryStatus(
                manifestPath,
                BootstrapScopedRegistryState.Error,
                detail);
        }
    }
}
