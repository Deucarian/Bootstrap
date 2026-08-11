using System;
using System.Collections.Generic;

namespace Deucarian.Bootstrap.Editor.Tests
{
    /// <summary>
    /// Named, production-shaped snapshots used by presentation and geometry contracts.
    /// Tests deliberately enter through BootstrapPresentationModelFactory instead of
    /// constructing presentation models that can drift away from coordinator state.
    /// </summary>
    internal static class BootstrapPresentationSnapshotFixtures
    {
        internal const string CurrentRevision =
            "0123456789abcdef0123456789abcdef01234567";
        internal const string PreviousRevision =
            "fedcba9876543210fedcba9876543210fedcba98";

        public static BootstrapSetupSnapshot Loading()
        {
            return BootstrapSetupSnapshot.Loading(
                BootstrapChannel.Stable,
                "Checking this project...");
        }

        public static BootstrapSetupSnapshot CleanReview(
            BootstrapChannel channel = BootstrapChannel.Stable)
        {
            return Snapshot(
                channel,
                BootstrapSetupPhase.Review,
                BootstrapInstalledState.Empty,
                Health(false, false, BootstrapPackageInstallerSetupState.Missing,
                    BootstrapSetupAction.Install, false),
                "Ready to install Package Installer.");
        }

        public static BootstrapSetupSnapshot MissingEditor()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Review,
                InstalledState(Logging(), Installer()),
                Health(false, true, BootstrapPackageInstallerSetupState.Healthy,
                    BootstrapSetupAction.Repair, true),
                "Editor is missing.");
        }

        public static BootstrapSetupSnapshot MissingLogging()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Review,
                InstalledState(Editor(), Installer()),
                Health(true, false, BootstrapPackageInstallerSetupState.Healthy,
                    BootstrapSetupAction.Repair, true),
                "Logging is missing.");
        }

        public static BootstrapSetupSnapshot MissingPackageInstaller()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Review,
                InstalledState(Editor(), Logging()),
                Health(true, true, BootstrapPackageInstallerSetupState.Missing,
                    BootstrapSetupAction.Repair, true),
                "Package Installer is missing.");
        }

        public static BootstrapSetupSnapshot WrongChannel(
            BootstrapChannel selectedChannel = BootstrapChannel.Development)
        {
            BootstrapChannel installedChannel = selectedChannel == BootstrapChannel.Stable
                ? BootstrapChannel.Development
                : BootstrapChannel.Stable;
            return Snapshot(
                selectedChannel,
                BootstrapSetupPhase.Review,
                InstalledState(Editor(), Logging(), Installer(installedChannel)),
                Health(true, true, BootstrapPackageInstallerSetupState.WrongChannel,
                    BootstrapSetupAction.SwitchChannel, true),
                "Package Installer is on a different channel.");
        }

        public static BootstrapSetupSnapshot OutdatedRevision()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Review,
                InstalledState(Editor(), Logging(), Installer(
                    BootstrapChannel.Stable,
                    PreviousRevision)),
                Health(true, true, BootstrapPackageInstallerSetupState.Outdated,
                    BootstrapSetupAction.Repair, true),
                "Package Installer is behind the selected branch.");
        }

        public static BootstrapSetupSnapshot LegacyMigration()
        {
            BootstrapInstalledPackageInfo legacyInstaller =
                new BootstrapInstalledPackageInfo(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    "1.1.83",
                    "registry",
                    "1.1.83",
                    string.Empty,
                    string.Empty);
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Review,
                InstalledState(Editor(), Logging(), legacyInstaller),
                Health(true, true, BootstrapPackageInstallerSetupState.WrongSource,
                    BootstrapSetupAction.Migrate, true),
                "A legacy scoped-registry source was found.",
                legacyStatus: BootstrapScopedRegistryStatus.CreateConfigured(
                    "Packages/manifest.json",
                    "Legacy Deucarian scoped registry is configured."));
        }

        public static BootstrapSetupSnapshot BundledFallbackReview()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Review,
                BootstrapInstalledState.Empty,
                Health(false, false, BootstrapPackageInstallerSetupState.Missing,
                    BootstrapSetupAction.Install, false),
                "Ready using bundled setup metadata.",
                BootstrapCatalogOrigin.BundledFallback,
                "Bundled setup fallback",
                "Remote Package Registry is unavailable. Using the exact bundled setup closure.");
        }

        public static BootstrapSetupSnapshot InstallingEditor()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Installing,
                BootstrapInstalledState.Empty,
                Health(false, false, BootstrapPackageInstallerSetupState.Missing,
                    BootstrapSetupAction.Install, false),
                "Installing Editor.",
                pendingPackageId: DeucarianBootstrapPackageConstants.EditorPackageId,
                pendingKind: BootstrapPersistedOperationKind.Add);
        }

        public static BootstrapSetupSnapshot InstallingLogging()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Installing,
                InstalledState(Editor()),
                Health(true, false, BootstrapPackageInstallerSetupState.Missing,
                    BootstrapSetupAction.Repair, true),
                "Installing Logging.",
                completedPackageIds: new[]
                {
                    DeucarianBootstrapPackageConstants.EditorPackageId
                },
                pendingPackageId: DeucarianBootstrapPackageConstants.LoggingPackageId,
                pendingKind: BootstrapPersistedOperationKind.Add);
        }

        public static BootstrapSetupSnapshot InstallingPackageInstaller()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Installing,
                InstalledState(Editor(), Logging()),
                Health(true, true, BootstrapPackageInstallerSetupState.Missing,
                    BootstrapSetupAction.Repair, true),
                "Installing Package Installer.",
                completedPackageIds: new[]
                {
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId
                },
                pendingPackageId:
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                pendingKind: BootstrapPersistedOperationKind.Add);
        }

        public static BootstrapSetupSnapshot RemovingLegacyPackageInstaller()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Installing,
                InstalledState(Editor(), Logging()),
                Health(true, true, BootstrapPackageInstallerSetupState.WrongSource,
                    BootstrapSetupAction.Migrate, true),
                "Removing legacy Package Installer source.",
                completedPackageIds: new[]
                {
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId
                },
                pendingPackageId:
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                pendingKind: BootstrapPersistedOperationKind.Remove);
        }

        public static BootstrapSetupSnapshot WaitingForUnity()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.WaitingForUnity,
                InstalledState(Editor()),
                Health(true, false, BootstrapPackageInstallerSetupState.Missing,
                    BootstrapSetupAction.Repair, true),
                "Waiting for Unity to resolve Logging.",
                completedPackageIds: new[]
                {
                    DeucarianBootstrapPackageConstants.EditorPackageId
                },
                pendingPackageId: DeucarianBootstrapPackageConstants.LoggingPackageId,
                pendingKind: BootstrapPersistedOperationKind.List);
        }

        public static BootstrapSetupSnapshot Verifying()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Verifying,
                InstalledState(Editor(), Logging(), Installer()),
                Health(true, true, BootstrapPackageInstallerSetupState.Healthy,
                    BootstrapSetupAction.OpenPackageInstaller, true),
                "Verifying Package Installer.",
                completedPackageIds: AllPackageIds(),
                pendingKind: BootstrapPersistedOperationKind.List);
        }

        public static BootstrapSetupSnapshot Healthy()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Healthy,
                InstalledState(Editor(), Logging(), Installer()),
                Health(true, true, BootstrapPackageInstallerSetupState.Healthy,
                    BootstrapSetupAction.OpenPackageInstaller, true),
                "Package Installer is healthy.",
                completedPackageIds: AllPackageIds());
        }

        public static BootstrapSetupSnapshot ReviewRequired()
        {
            BootstrapInstalledPackageInfo installer = Installer(
                BootstrapChannel.Stable,
                string.Empty);
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.ReviewRequired,
                InstalledState(Editor(), Logging(), installer),
                Health(true, true,
                    BootstrapPackageInstallerSetupState.UnknownReviewRequired,
                    BootstrapSetupAction.Refresh, true),
                "Remote revision could not be verified.",
                targetRevision: string.Empty);
        }

        public static BootstrapSetupSnapshot Failed()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Failed,
                InstalledState(Editor()),
                Health(true, false, BootstrapPackageInstallerSetupState.Missing,
                    BootstrapSetupAction.Repair, true),
                "Setup stopped.",
                error: "Unity Package Manager could not add Logging.",
                completedPackageIds: new[]
                {
                    DeucarianBootstrapPackageConstants.EditorPackageId
                },
                pendingPackageId: DeucarianBootstrapPackageConstants.LoggingPackageId,
                pendingKind: BootstrapPersistedOperationKind.Add);
        }

        public static BootstrapSetupSnapshot DetectionFailureWithoutPlan()
        {
            return Snapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Failed,
                BootstrapInstalledState.Empty,
                Health(false, false, BootstrapPackageInstallerSetupState.Missing,
                    BootstrapSetupAction.Refresh, false),
                "Setup status could not be detected.",
                error: "Unity Package Manager could not list installed packages.",
                includePlan: false);
        }

        private static BootstrapSetupSnapshot Snapshot(
            BootstrapChannel channel,
            BootstrapSetupPhase phase,
            BootstrapInstalledState installedState,
            BootstrapHealthReport health,
            string status,
            BootstrapCatalogOrigin catalogOrigin = BootstrapCatalogOrigin.Remote,
            string catalogSource = "Remote Package Registry",
            string catalogNotice = "Remote metadata validated.",
            string error = "",
            IEnumerable<string> completedPackageIds = null,
            string pendingPackageId = "",
            BootstrapPersistedOperationKind pendingKind =
                BootstrapPersistedOperationKind.None,
            BootstrapScopedRegistryStatus legacyStatus = null,
            string targetRevision = CurrentRevision,
            bool includePlan = true)
        {
            return new BootstrapSetupSnapshot(
                channel,
                phase,
                catalogOrigin,
                catalogSource,
                catalogNotice,
                status,
                error,
                TargetUrl(channel),
                targetRevision,
                includePlan ? Plan(channel) : Array.Empty<BootstrapPackageStep>(),
                completedPackageIds ?? Array.Empty<string>(),
                pendingPackageId,
                installedState,
                health,
                legacyStatus ?? BootstrapScopedRegistryStatus.NotInspected,
                pendingKind);
        }

        private static IReadOnlyList<BootstrapPackageStep> Plan(BootstrapChannel channel)
        {
            string branch = BootstrapChannelUtility.GetGitBranch(channel);
            return new[]
            {
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.EditorPackageDisplayName,
                    "https://github.com/Deucarian/Editor.git#" + branch),
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageDisplayName,
                    "https://github.com/Deucarian/Logging.git#" + branch),
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageDisplayName,
                    TargetUrl(channel))
            };
        }

        private static BootstrapHealthReport Health(
            bool editorInstalled,
            bool loggingInstalled,
            BootstrapPackageInstallerSetupState installerState,
            BootstrapSetupAction action,
            bool anyInstalled)
        {
            return new BootstrapHealthReport(
                editorInstalled,
                loggingInstalled,
                installerState,
                action,
                anyInstalled);
        }

        private static BootstrapInstalledState InstalledState(
            params BootstrapInstalledPackageInfo[] packages)
        {
            return new BootstrapInstalledState(packages);
        }

        private static BootstrapInstalledPackageInfo Editor()
        {
            return Requirement(
                DeucarianBootstrapPackageConstants.EditorPackageId,
                "https://github.com/Deucarian/Editor.git#main");
        }

        private static BootstrapInstalledPackageInfo Logging()
        {
            return Requirement(
                DeucarianBootstrapPackageConstants.LoggingPackageId,
                "https://github.com/Deucarian/Logging.git#main");
        }

        private static BootstrapInstalledPackageInfo Requirement(
            string packageId,
            string packageReference)
        {
            return new BootstrapInstalledPackageInfo(
                packageId,
                "1.2.0",
                "git",
                packageReference,
                packageReference,
                CurrentRevision);
        }

        private static BootstrapInstalledPackageInfo Installer(
            BootstrapChannel channel = BootstrapChannel.Stable,
            string revision = CurrentRevision)
        {
            string packageReference = TargetUrl(channel);
            return new BootstrapInstalledPackageInfo(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                "1.1.83",
                "git",
                packageReference,
                packageReference,
                revision);
        }

        private static string TargetUrl(BootstrapChannel channel)
        {
            return channel == BootstrapChannel.Development
                ? DeucarianBootstrapPackageConstants.PackageInstallerDevelopmentGitUrl
                : DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl;
        }

        private static string[] AllPackageIds()
        {
            return new[]
            {
                DeucarianBootstrapPackageConstants.EditorPackageId,
                DeucarianBootstrapPackageConstants.LoggingPackageId,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId
            };
        }
    }
}
