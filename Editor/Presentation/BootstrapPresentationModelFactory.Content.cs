using System;
using System.Collections.Generic;

namespace Deucarian.Bootstrap.Editor
{
    internal static partial class BootstrapPresentationModelFactory
    {
        private static IReadOnlyList<BootstrapDetailPresentation> BuildDetails(
            BootstrapSetupSnapshot state)
        {
            BootstrapInstalledPackageInfo installer = state.InstalledState.Get(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
            List<BootstrapDetailPresentation> details = new List<BootstrapDetailPresentation>
            {
                new BootstrapDetailPresentation(
                    "Selected channel",
                    BootstrapChannelUtility.GetDisplayName(state.Channel) +
                    " | Git #" + BootstrapChannelUtility.GetGitBranch(state.Channel)),
                new BootstrapDetailPresentation(
                    "Registry source",
                    string.IsNullOrWhiteSpace(state.CatalogSource)
                        ? "Checking..."
                        : state.CatalogSource),
                new BootstrapDetailPresentation("Package Installer target", state.TargetGitUrl),
                new BootstrapDetailPresentation(
                    "Target branch revision",
                    ExactRevision(state.TargetRevision, "Unverifiable")),
                new BootstrapDetailPresentation(
                    "Installed source",
                    installer != null ? installer.BestReference : "Not installed"),
                new BootstrapDetailPresentation(
                    "Installed lock revision",
                    installer != null
                        ? ExactRevision(installer.LockRevision, "Unverifiable")
                        : "Not installed"),
                new BootstrapDetailPresentation(
                    "Legacy scoped registry",
                    state.LegacyRegistryStatus.Detail),
                new BootstrapDetailPresentation(
                    "Registry notice",
                    string.IsNullOrWhiteSpace(state.CatalogNotice)
                        ? "Remote metadata validated."
                        : state.CatalogNotice)
            };

            foreach (BootstrapPackageStep step in state.Plan)
            {
                if (step != null)
                {
                    details.Add(new BootstrapDetailPresentation(
                        GetItemTitle(step.PackageId, step.DisplayName) + " Git reference",
                        step.PackageReference));
                }
            }

            return details;
        }

        private static BootstrapPresentationTone GetTone(BootstrapSetupSnapshot state)
        {
            switch (state.Phase)
            {
                case BootstrapSetupPhase.Healthy:
                    return BootstrapPresentationTone.Success;
                case BootstrapSetupPhase.Failed:
                    return BootstrapPresentationTone.Error;
                case BootstrapSetupPhase.ReviewRequired:
                    return BootstrapPresentationTone.Warning;
                case BootstrapSetupPhase.Review:
                    return state.Health.AnySetupPackageInstalled
                        ? BootstrapPresentationTone.Warning
                        : BootstrapPresentationTone.Info;
                case BootstrapSetupPhase.Loading:
                case BootstrapSetupPhase.Installing:
                case BootstrapSetupPhase.WaitingForUnity:
                case BootstrapSetupPhase.Verifying:
                    return BootstrapPresentationTone.Info;
                default:
                    return BootstrapPresentationTone.Neutral;
            }
        }

        private static string GetIconClass(
            BootstrapSetupSnapshot state,
            BootstrapSetupAction action)
        {
            if (state.Phase == BootstrapSetupPhase.Healthy) return "bootstrap-icon--success";
            if (state.Phase == BootstrapSetupPhase.Failed) return "bootstrap-icon--error";
            if (state.IsBusy) return "bootstrap-icon--loading";
            if (state.Phase == BootstrapSetupPhase.ReviewRequired) return "bootstrap-icon--review";
            if (action == BootstrapSetupAction.Install) return "bootstrap-icon--install";
            if (action == BootstrapSetupAction.OpenPackageInstaller) return "bootstrap-icon--open";
            return "bootstrap-icon--repair";
        }

        private static string GetActionLabel(
            BootstrapSetupSnapshot state,
            BootstrapSetupAction action)
        {
            switch (action)
            {
                case BootstrapSetupAction.Install: return "Install Package Installer";
                case BootstrapSetupAction.Repair: return "Repair Package Installer";
                case BootstrapSetupAction.SwitchChannel:
                    return "Switch to " + BootstrapChannelUtility.GetDisplayName(state.Channel);
                case BootstrapSetupAction.Migrate: return "Migrate Package Installer";
                case BootstrapSetupAction.Refresh: return "Refresh Status";
                case BootstrapSetupAction.OpenPackageInstaller: return "Open Package Installer";
                default: return string.Empty;
            }
        }

        private static string GetActionTooltip(BootstrapSetupAction action)
        {
            if (action == BootstrapSetupAction.OpenPackageInstaller)
            {
                return "Open Tools > Deucarian > Tools and Quality > Package Installer.";
            }

            return action == BootstrapSetupAction.Refresh
                ? "Read Registry, Package Manager, source, and revision state again without changing packages."
                : "Run the reviewed setup closure. Package changes begin only after this click.";
        }

        private static string GetActionSummary(
            BootstrapSetupSnapshot state,
            BootstrapSetupAction action)
        {
            if (action == BootstrapSetupAction.OpenPackageInstaller) return "Ready for handoff";
            if (state.IsBusy) return "Setup continues automatically";
            return "Package Installer setup";
        }

        private static string GetFooterText(
            BootstrapSetupSnapshot state,
            string actionLabel)
        {
            switch (state.Phase)
            {
                case BootstrapSetupPhase.Installing:
                    return "Safe to close - setup resumes after Unity reloads.";
                case BootstrapSetupPhase.WaitingForUnity:
                    return "Unity is resolving packages. Bootstrap will continue automatically.";
                case BootstrapSetupPhase.Verifying:
                    return "Checking source, channel, and lock revision.";
                case BootstrapSetupPhase.Healthy:
                    return "Bootstrap's work is complete.";
                case BootstrapSetupPhase.Failed:
                case BootstrapSetupPhase.ReviewRequired:
                    return "Refresh checks the project again without changing packages.";
                default:
                    return string.IsNullOrWhiteSpace(actionLabel)
                        ? string.Empty
                        : "Nothing changes until you choose " + actionLabel + ".";
            }
        }

        private static string GetInstalledSummary(BootstrapSetupSnapshot state)
        {
            BootstrapInstalledPackageInfo package = state.InstalledState.Get(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
            if (package == null)
            {
                return "Package Installer is not installed.";
            }

            string version = string.IsNullOrWhiteSpace(package.Version)
                ? "Version unknown"
                : "v" + package.Version;
            return version + " | Git #" + BootstrapChannelUtility.GetGitBranch(state.Channel) +
                " | " + ShortRevision(package.LockRevision, "Revision unknown");
        }

        private static BootstrapSetupItemRole GetRole(string packageId)
        {
            return string.Equals(
                packageId,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                StringComparison.OrdinalIgnoreCase)
                ? BootstrapSetupItemRole.Destination
                : BootstrapSetupItemRole.Requirement;
        }

        private static string GetItemTitle(string packageId, string fallback)
        {
            if (string.Equals(
                    packageId,
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Editor";
            }

            if (string.Equals(
                    packageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Logging";
            }

            if (string.Equals(
                    packageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Package Installer";
            }

            return fallback ?? string.Empty;
        }

        private static bool IsDurableBusyPhase(BootstrapSetupPhase phase)
        {
            return phase == BootstrapSetupPhase.Installing ||
                   phase == BootstrapSetupPhase.WaitingForUnity ||
                   phase == BootstrapSetupPhase.Verifying;
        }

        private static string ShortRevision(string revision, string fallback)
        {
            return string.IsNullOrWhiteSpace(revision)
                ? fallback
                : revision.Length <= 12
                    ? revision
                    : revision.Substring(0, 12);
        }

        private static string ExactRevision(string revision, string fallback)
        {
            return string.IsNullOrWhiteSpace(revision) ? fallback : revision.Trim();
        }
    }
}
