using System;

namespace Deucarian.Bootstrap.Editor
{
    internal enum BootstrapChannel
    {
        Stable = 0,
        Development = 1
    }

    internal static class BootstrapChannelUtility
    {
        public static string GetDisplayName(BootstrapChannel channel)
        {
            return channel == BootstrapChannel.Development ? "Development" : "Stable";
        }

        public static string GetDescription(BootstrapChannel channel)
        {
            return channel == BootstrapChannel.Development
                ? "For testing current package work. Installs from Git #develop."
                : "Recommended. Installs Deucarian packages from Git #main.";
        }

        public static string GetGitBranch(BootstrapChannel channel)
        {
            return channel == BootstrapChannel.Development ? "develop" : "main";
        }

        public static string GetRegistryCatalogUrl(BootstrapChannel channel)
        {
            return channel == BootstrapChannel.Development
                ? DeucarianBootstrapPackageConstants.DevelopmentRegistryCatalogUrl
                : DeucarianBootstrapPackageConstants.StableRegistryCatalogUrl;
        }

        public static string GetPackageInstallerGitUrl(BootstrapChannel channel)
        {
            return channel == BootstrapChannel.Development
                ? DeucarianBootstrapPackageConstants.PackageInstallerDevelopmentGitUrl
                : DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl;
        }

        public static string GetPackageInstallerRawPackageJsonUrl(BootstrapChannel channel)
        {
            return "https://raw.githubusercontent.com/Deucarian/Package-Installer/" +
                GetGitBranch(channel) +
                "/package.json";
        }

        public static bool TryDetectFromGitReference(string reference, out BootstrapChannel channel)
        {
            channel = BootstrapChannel.Stable;

            if (string.IsNullOrWhiteSpace(reference))
            {
                return false;
            }

            string normalized = reference.Trim().Replace('\\', '/');
            if (normalized.EndsWith("#develop", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("?path=/develop", StringComparison.OrdinalIgnoreCase) ||
                normalized.IndexOf("/develop/package.json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                channel = BootstrapChannel.Development;
                return true;
            }

            if (normalized.EndsWith("#main", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("?path=/main", StringComparison.OrdinalIgnoreCase) ||
                normalized.IndexOf("/main/package.json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                channel = BootstrapChannel.Stable;
                return true;
            }

            return false;
        }
    }
}
