using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Deucarian.Bootstrap.Editor
{
    internal enum BootstrapSetupPhase
    {
        Loading,
        Review,
        Installing,
        WaitingForUnity,
        Verifying,
        Healthy,
        ReviewRequired,
        Failed
    }

    internal enum BootstrapCatalogOrigin
    {
        None,
        Remote,
        BundledFallback
    }

    internal enum BootstrapSetupAction
    {
        None,
        Install,
        Repair,
        SwitchChannel,
        Migrate,
        Refresh,
        OpenPackageInstaller
    }

    internal enum BootstrapPersistedOperationKind
    {
        None,
        Add,
        Remove,
        List
    }

    internal sealed class BootstrapPackageStep
    {
        public BootstrapPackageStep(string packageId, string displayName, string packageReference)
        {
            PackageId = packageId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? PackageId : displayName;
            PackageReference = packageReference ?? string.Empty;
        }

        public string PackageId { get; }

        public string DisplayName { get; }

        public string PackageReference { get; }
    }

    internal sealed class BootstrapInstalledState
    {
        private readonly IReadOnlyDictionary<string, BootstrapInstalledPackageInfo> _packages;

        public BootstrapInstalledState(IEnumerable<BootstrapInstalledPackageInfo> packages)
        {
            Dictionary<string, BootstrapInstalledPackageInfo> copy =
                new Dictionary<string, BootstrapInstalledPackageInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (BootstrapInstalledPackageInfo package in packages ?? Array.Empty<BootstrapInstalledPackageInfo>())
            {
                if (package != null && !string.IsNullOrWhiteSpace(package.PackageId))
                {
                    copy[package.PackageId] = package;
                }
            }

            _packages = new ReadOnlyDictionary<string, BootstrapInstalledPackageInfo>(copy);
        }

        public IReadOnlyDictionary<string, BootstrapInstalledPackageInfo> Packages => _packages;

        public bool Contains(string packageId)
        {
            return !string.IsNullOrWhiteSpace(packageId) && _packages.ContainsKey(packageId);
        }

        public BootstrapInstalledPackageInfo Get(string packageId)
        {
            return !string.IsNullOrWhiteSpace(packageId) &&
                   _packages.TryGetValue(packageId, out BootstrapInstalledPackageInfo package)
                ? package
                : null;
        }

        public static BootstrapInstalledState Empty { get; } =
            new BootstrapInstalledState(Array.Empty<BootstrapInstalledPackageInfo>());
    }

    internal sealed class BootstrapHealthReport
    {
        public BootstrapHealthReport(
            bool editorInstalled,
            bool loggingInstalled,
            BootstrapPackageInstallerSetupState packageInstallerState,
            BootstrapSetupAction recommendedAction,
            bool anySetupPackageInstalled)
        {
            EditorInstalled = editorInstalled;
            LoggingInstalled = loggingInstalled;
            PackageInstallerState = packageInstallerState;
            RecommendedAction = recommendedAction;
            AnySetupPackageInstalled = anySetupPackageInstalled;
        }

        public bool EditorInstalled { get; }

        public bool LoggingInstalled { get; }

        public BootstrapPackageInstallerSetupState PackageInstallerState { get; }

        public BootstrapSetupAction RecommendedAction { get; }

        public bool AnySetupPackageInstalled { get; }

        public bool IsHealthy =>
            EditorInstalled &&
            LoggingInstalled &&
            PackageInstallerState == BootstrapPackageInstallerSetupState.Healthy;
    }

    internal sealed class BootstrapSetupSnapshot
    {
        public BootstrapSetupSnapshot(
            BootstrapChannel channel,
            BootstrapSetupPhase phase,
            BootstrapCatalogOrigin catalogOrigin,
            string catalogSource,
            string catalogNotice,
            string status,
            string error,
            string targetGitUrl,
            string targetRevision,
            IReadOnlyList<BootstrapPackageStep> plan,
            IEnumerable<string> completedPackageIds,
            string pendingPackageId,
            BootstrapInstalledState installedState,
            BootstrapHealthReport health,
            BootstrapScopedRegistryStatus legacyRegistryStatus,
            BootstrapPersistedOperationKind pendingKind = BootstrapPersistedOperationKind.None)
        {
            Channel = channel;
            Phase = phase;
            CatalogOrigin = catalogOrigin;
            CatalogSource = catalogSource ?? string.Empty;
            CatalogNotice = catalogNotice ?? string.Empty;
            Status = status ?? string.Empty;
            Error = error ?? string.Empty;
            TargetGitUrl = targetGitUrl ?? string.Empty;
            TargetRevision = targetRevision ?? string.Empty;
            Plan = new List<BootstrapPackageStep>(plan ?? Array.Empty<BootstrapPackageStep>()).AsReadOnly();
            CompletedPackageIds = new ReadOnlyCollection<string>(
                (completedPackageIds ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList());
            PendingPackageId = pendingPackageId ?? string.Empty;
            PendingKind = pendingKind;
            InstalledState = installedState ?? BootstrapInstalledState.Empty;
            Health = health ?? BootstrapSetupPolicy.Evaluate(
                channel,
                InstalledState,
                targetGitUrl,
                targetRevision);
            LegacyRegistryStatus = legacyRegistryStatus ?? BootstrapScopedRegistryStatus.NotInspected;
        }

        public BootstrapChannel Channel { get; }

        public BootstrapSetupPhase Phase { get; }

        public BootstrapCatalogOrigin CatalogOrigin { get; }

        public string CatalogSource { get; }

        public string CatalogNotice { get; }

        public string Status { get; }

        public string Error { get; }

        public string TargetGitUrl { get; }

        public string TargetRevision { get; }

        public IReadOnlyList<BootstrapPackageStep> Plan { get; }

        public IReadOnlyList<string> CompletedPackageIds { get; }

        public string PendingPackageId { get; }

        public BootstrapPersistedOperationKind PendingKind { get; }

        public BootstrapInstalledState InstalledState { get; }

        public BootstrapHealthReport Health { get; }

        public BootstrapScopedRegistryStatus LegacyRegistryStatus { get; }

        public bool IsBusy =>
            Phase == BootstrapSetupPhase.Loading ||
            Phase == BootstrapSetupPhase.Installing ||
            Phase == BootstrapSetupPhase.WaitingForUnity ||
            Phase == BootstrapSetupPhase.Verifying;

        public static BootstrapSetupSnapshot Loading(BootstrapChannel channel, string status)
        {
            BootstrapInstalledState installed = BootstrapInstalledState.Empty;
            return new BootstrapSetupSnapshot(
                channel,
                BootstrapSetupPhase.Loading,
                BootstrapCatalogOrigin.None,
                string.Empty,
                string.Empty,
                status,
                string.Empty,
                BootstrapChannelUtility.GetPackageInstallerGitUrl(channel),
                string.Empty,
                Array.Empty<BootstrapPackageStep>(),
                Array.Empty<string>(),
                string.Empty,
                installed,
                BootstrapSetupPolicy.Evaluate(channel, installed, string.Empty),
                BootstrapScopedRegistryStatus.NotInspected);
        }
    }
}
