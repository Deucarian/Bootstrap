using System;
using System.Globalization;

namespace Deucarian.Bootstrap.Editor
{
    internal enum BootstrapPackageInstallerSetupState
    {
        Missing,
        Outdated,
        WrongChannel,
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
            string lockGitUrl)
        {
            PackageId = packageId ?? string.Empty;
            Version = version ?? string.Empty;
            Source = source ?? string.Empty;
            PackageReference = packageReference ?? string.Empty;
            LockGitUrl = lockGitUrl ?? string.Empty;
        }

        public string PackageId { get; }

        public string Version { get; }

        public string Source { get; }

        public string PackageReference { get; }

        public string LockGitUrl { get; }

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
            string targetVersion)
        {
            if (installedPackage == null)
            {
                return BootstrapPackageInstallerSetupState.Missing;
            }

            bool targetVersionKnown = !string.IsNullOrWhiteSpace(targetVersion);
            bool installedVersionKnown = !string.IsNullOrWhiteSpace(installedPackage.Version);
            bool versionBehind = targetVersionKnown &&
                (!installedVersionKnown || CompareVersions(installedPackage.Version, targetVersion) < 0);

            if (installedPackage.IsRegistry)
            {
                return BootstrapPackageInstallerSetupState.WrongChannel;
            }

            if (!installedPackage.IsGit)
            {
                return versionBehind
                    ? BootstrapPackageInstallerSetupState.WrongChannel
                    : BootstrapPackageInstallerSetupState.UnknownReviewRequired;
            }

            if (!installedPackage.TryGetGitChannel(out BootstrapChannel installedChannel))
            {
                return versionBehind
                    ? BootstrapPackageInstallerSetupState.Outdated
                    : BootstrapPackageInstallerSetupState.UnknownReviewRequired;
            }

            if (installedChannel != selectedChannel)
            {
                return BootstrapPackageInstallerSetupState.WrongChannel;
            }

            if (versionBehind)
            {
                return BootstrapPackageInstallerSetupState.Outdated;
            }

            return BootstrapPackageInstallerSetupState.Healthy;
        }

        public static int CompareVersions(string left, string right)
        {
            Version leftVersion = ParseVersion(left);
            Version rightVersion = ParseVersion(right);

            if (leftVersion != null && rightVersion != null)
            {
                return leftVersion.CompareTo(rightVersion);
            }

            return string.Compare(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static Version ParseVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string[] pieces = value.Trim().Split('.');
            int[] numeric = new int[4];

            for (int i = 0; i < numeric.Length; i++)
            {
                if (i >= pieces.Length)
                {
                    numeric[i] = 0;
                    continue;
                }

                string piece = pieces[i];
                int suffix = piece.IndexOfAny(new[] { '-', '+' });
                if (suffix >= 0)
                {
                    piece = piece.Substring(0, suffix);
                }

                if (!int.TryParse(piece, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric[i]))
                {
                    return null;
                }
            }

            return new Version(numeric[0], numeric[1], numeric[2], numeric[3]);
        }
    }
}
