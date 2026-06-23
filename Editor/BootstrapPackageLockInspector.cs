using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class BootstrapPackageLockEntry
    {
        public BootstrapPackageLockEntry(string packageId, string source, string versionReference)
        {
            PackageId = packageId ?? string.Empty;
            Source = source ?? string.Empty;
            VersionReference = versionReference ?? string.Empty;
        }

        public string PackageId { get; }

        public string Source { get; }

        public string VersionReference { get; }

        public string GitUrl
        {
            get
            {
                return Source.IndexOf("git", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    VersionReference.IndexOf(".git", StringComparison.OrdinalIgnoreCase) >= 0
                    ? VersionReference
                    : string.Empty;
            }
        }
    }

    internal static class BootstrapPackageLockInspector
    {
        public static BootstrapPackageLockEntry GetPackage(string packageId)
        {
            string path = GetProjectPackageLockPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                return TryGetPackage(File.ReadAllText(path), packageId, out BootstrapPackageLockEntry entry)
                    ? entry
                    : null;
            }
            catch
            {
                return null;
            }
        }

        public static bool TryGetPackage(string packageLockJson, string packageId, out BootstrapPackageLockEntry entry)
        {
            entry = null;

            if (string.IsNullOrWhiteSpace(packageLockJson) || string.IsNullOrWhiteSpace(packageId))
            {
                return false;
            }

            string quotedPackageId = "\"" + Regex.Escape(packageId) + "\"";
            Match packageMatch = Regex.Match(packageLockJson, quotedPackageId + "\\s*:\\s*\\{");
            if (!packageMatch.Success)
            {
                return false;
            }

            int objectStart = packageLockJson.IndexOf('{', packageMatch.Index);
            if (objectStart < 0 || !TryFindMatchingBrace(packageLockJson, objectStart, out int objectEnd))
            {
                return false;
            }

            string packageObject = packageLockJson.Substring(objectStart, objectEnd - objectStart + 1);
            string source = ReadJsonStringProperty(packageObject, "source");
            string version = ReadJsonStringProperty(packageObject, "version");

            entry = new BootstrapPackageLockEntry(packageId, source, version);
            return true;
        }

        private static string GetProjectPackageLockPath()
        {
            if (string.IsNullOrWhiteSpace(Application.dataPath))
            {
                return string.Empty;
            }

            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            return projectRoot == null
                ? string.Empty
                : Path.Combine(projectRoot.FullName, "Packages", "packages-lock.json");
        }

        private static bool TryFindMatchingBrace(string text, int openBraceIndex, out int closeBraceIndex)
        {
            closeBraceIndex = -1;
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = openBraceIndex; i < text.Length; i++)
            {
                char c = text[i];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeBraceIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private static string ReadJsonStringProperty(string jsonObject, string propertyName)
        {
            Match match = Regex.Match(
                jsonObject,
                "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"");

            return match.Success ? Regex.Unescape(match.Groups["value"].Value) : string.Empty;
        }
    }
}
