using System;

namespace Deucarian.Bootstrap.Editor
{
    internal enum BootstrapPackageInstallerSetupState
    {
        Missing,
        Outdated,
        WrongChannel,
        WrongSource,
        Healthy,
        UnknownReviewRequired
    }

    internal sealed class BootstrapInstalledPackageInfo
    {
        public BootstrapInstalledPackageInfo(
            string packageId,
            string version,
            string source,
            string packageReference,
            string lockGitUrl,
            string lockRevision)
        {
            PackageId = packageId ?? string.Empty;
            Version = version ?? string.Empty;
            Source = source ?? string.Empty;
            PackageReference = packageReference ?? string.Empty;
            LockGitUrl = lockGitUrl ?? string.Empty;
            LockRevision = lockRevision ?? string.Empty;
        }

        public string PackageId { get; }

        public string Version { get; }

        public string Source { get; }

        public string PackageReference { get; }

        public string LockGitUrl { get; }

        public string LockRevision { get; }

        public string BestReference
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(LockGitUrl))
                {
                    return LockGitUrl;
                }

                return PackageReference;
            }
        }

        public bool IsGit
        {
            get
            {
                return Source.IndexOf("git", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    BestReference.IndexOf(".git", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    BestReference.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public bool IsRegistry
        {
            get
            {
                return Source.IndexOf("registry", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public bool TryGetGitChannel(out BootstrapChannel channel)
        {
            return BootstrapChannelUtility.TryDetectFromGitReference(BestReference, out channel);
        }
    }

    internal static class BootstrapPackageInstallerStatus
    {
        public static BootstrapPackageInstallerSetupState Evaluate(
            BootstrapChannel selectedChannel,
            BootstrapInstalledPackageInfo installedPackage,
            string targetRevision)
        {
            return Evaluate(
                selectedChannel,
                installedPackage,
                BootstrapChannelUtility.GetPackageInstallerGitUrl(selectedChannel),
                targetRevision);
        }

        public static BootstrapPackageInstallerSetupState Evaluate(
            BootstrapChannel selectedChannel,
            BootstrapInstalledPackageInfo installedPackage,
            string targetGitUrl,
            string targetRevision)
        {
            if (installedPackage == null)
            {
                return BootstrapPackageInstallerSetupState.Missing;
            }

            if (installedPackage.IsRegistry)
            {
                return BootstrapPackageInstallerSetupState.WrongChannel;
            }

            if (!installedPackage.IsGit)
            {
                return BootstrapPackageInstallerSetupState.UnknownReviewRequired;
            }

            if (!installedPackage.TryGetGitChannel(out BootstrapChannel installedChannel))
            {
                return BootstrapPackageInstallerSetupState.UnknownReviewRequired;
            }

            if (installedChannel != selectedChannel)
            {
                return BootstrapPackageInstallerSetupState.WrongChannel;
            }

            if (string.IsNullOrWhiteSpace(targetGitUrl) ||
                !string.Equals(
                    NormalizeGitReference(installedPackage.BestReference),
                    NormalizeGitReference(targetGitUrl),
                    StringComparison.OrdinalIgnoreCase))
            {
                return BootstrapPackageInstallerSetupState.WrongSource;
            }

            if (string.IsNullOrWhiteSpace(installedPackage.LockRevision) ||
                string.IsNullOrWhiteSpace(targetRevision))
            {
                return BootstrapPackageInstallerSetupState.UnknownReviewRequired;
            }

            return string.Equals(
                installedPackage.LockRevision.Trim(),
                targetRevision.Trim(),
                StringComparison.OrdinalIgnoreCase)
                ? BootstrapPackageInstallerSetupState.Healthy
                : BootstrapPackageInstallerSetupState.Outdated;
        }

        private static string NormalizeGitReference(string reference)
        {
            string normalized = (reference ?? string.Empty).Trim().Replace('\\', '/');
            int packagePrefix = normalized.IndexOf("@http", StringComparison.OrdinalIgnoreCase);
            if (packagePrefix > 0)
            {
                normalized = normalized.Substring(packagePrefix + 1);
            }

            return normalized.StartsWith("git+", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring("git+".Length)
                : normalized;
        }
    }
}
