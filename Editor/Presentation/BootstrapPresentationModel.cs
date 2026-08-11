using System;
using System.Collections.Generic;
using System.Linq;

namespace Deucarian.Bootstrap.Editor
{
    internal static partial class BootstrapPresentationModelFactory
    {
        public static BootstrapPresentationModel Create(
            BootstrapSetupSnapshot snapshot,
            string transientMessage = null)
        {
            BootstrapSetupSnapshot state = snapshot ??
                BootstrapSetupSnapshot.Loading(BootstrapChannel.Stable, "Checking setup...");
            bool handoffRecovery = !string.IsNullOrWhiteSpace(transientMessage);
            BootstrapSetupAction action = handoffRecovery
                ? BootstrapSetupAction.Refresh
                : ResolveVisibleAction(state);
            IReadOnlyList<BootstrapStepPresentation> steps = BuildSteps(state);
            IReadOnlyList<BootstrapReceiptPresentation> receipt = BuildReceipt(state);
            string actionLabel = GetActionLabel(state, action);
            string footerText = handoffRecovery
                ? "Refresh checks the menu and package state again without changing packages."
                : GetFooterText(state, actionLabel);
            string status = handoffRecovery
                ? transientMessage
                : !string.IsNullOrWhiteSpace(state.Error)
                    ? state.Error
                    : state.Status;
            BootstrapPresentationTone tone = handoffRecovery
                ? BootstrapPresentationTone.Error
                : GetTone(state);
            bool showSetupFlow = steps.Count > 0 &&
                state.Phase != BootstrapSetupPhase.Loading &&
                state.Phase != BootstrapSetupPhase.Healthy;
            bool showReceipt = state.Phase == BootstrapSetupPhase.Healthy &&
                state.Health.IsHealthy;
            bool showAction = !state.IsBusy && action != BootstrapSetupAction.None;
            int completed = steps.Count(step =>
                step.State == BootstrapStepPresentationState.Complete);

            return new BootstrapPresentationModel(
                state.Channel,
                state.Phase,
                handoffRecovery
                    ? "Package Installer is still starting"
                    : GetTitle(state, action),
                handoffRecovery
                    ? "Unity has not registered the Package Installer menu yet. No package changes will happen while you wait."
                    : GetMessage(state, action),
                status,
                tone,
                handoffRecovery ? "bootstrap-icon--error" : GetIconClass(state, action),
                action,
                actionLabel,
                GetActionTooltip(action),
                showAction,
                !state.IsBusy,
                GetActionSummary(state, action),
                footerText,
                steps,
                BuildDetails(state),
                state.CatalogOrigin == BootstrapCatalogOrigin.BundledFallback
                    ? state.CatalogNotice
                    : string.Empty,
                completed,
                GetInstalledSummary(state),
                receipt,
                showSetupFlow,
                showReceipt,
                showAction,
                footerText,
                IsDurableBusyPhase(state.Phase));
        }

        private static BootstrapSetupAction ResolveVisibleAction(BootstrapSetupSnapshot state)
        {
            if (state.IsBusy)
            {
                return BootstrapSetupAction.None;
            }

            if (state.Phase == BootstrapSetupPhase.Failed ||
                state.Phase == BootstrapSetupPhase.ReviewRequired)
            {
                return BootstrapSetupAction.Refresh;
            }

            return state.Health.RecommendedAction;
        }

        private static string GetTitle(
            BootstrapSetupSnapshot state,
            BootstrapSetupAction action)
        {
            switch (state.Phase)
            {
                case BootstrapSetupPhase.Loading:
                    return "Checking Package Installer setup";
                case BootstrapSetupPhase.Installing:
                    return "Installing Package Installer";
                case BootstrapSetupPhase.WaitingForUnity:
                    return "Waiting for Unity";
                case BootstrapSetupPhase.Verifying:
                    return "Verifying Package Installer";
                case BootstrapSetupPhase.Healthy:
                    return "Package Installer is ready";
                case BootstrapSetupPhase.ReviewRequired:
                    return "Package Installer needs review";
                case BootstrapSetupPhase.Failed:
                    return "Setup needs attention";
            }

            switch (action)
            {
                case BootstrapSetupAction.Install:
                    return "Install Package Installer";
                case BootstrapSetupAction.Migrate:
                    return "Migrate Package Installer";
                case BootstrapSetupAction.SwitchChannel:
                    return "Switch Package Installer to " +
                        BootstrapChannelUtility.GetDisplayName(state.Channel);
                default:
                    return "Repair Package Installer";
            }
        }

        private static string GetMessage(
            BootstrapSetupSnapshot state,
            BootstrapSetupAction action)
        {
            switch (state.Phase)
            {
                case BootstrapSetupPhase.Loading:
                    return "Checking this project and the selected channel. Nothing is being installed.";
                case BootstrapSetupPhase.Installing:
                case BootstrapSetupPhase.WaitingForUnity:
                case BootstrapSetupPhase.Verifying:
                    return "Bootstrap is preparing the requirements, then Package Installer, in a durable order.";
                case BootstrapSetupPhase.Healthy:
                    return "Setup is complete. Continue all Deucarian package work in Package Installer.";
                case BootstrapSetupPhase.ReviewRequired:
                    return "The installed source is present, but its selected-branch revision cannot be verified.";
                case BootstrapSetupPhase.Failed:
                    return "No further package changes will happen until the project is checked again.";
            }

            switch (action)
            {
                case BootstrapSetupAction.Install:
                    return "Editor and Logging are installed first. Package Installer becomes your home for ongoing package work.";
                case BootstrapSetupAction.Migrate:
                    return "The legacy registry source will be replaced with Package Installer from the selected Git channel.";
                case BootstrapSetupAction.SwitchChannel:
                    return "Requirements stay in place while Package Installer moves to the selected Git branch.";
                default:
                    return "Bootstrap repairs the requirements first, then restores Package Installer as the destination.";
            }
        }

        private static IReadOnlyList<BootstrapStepPresentation> BuildSteps(
            BootstrapSetupSnapshot state)
        {
            IReadOnlyList<BootstrapPackageStep> plan = GetPresentationPlan(state);
            List<BootstrapStepPresentation> steps = new List<BootstrapStepPresentation>();
            for (int index = 0; index < plan.Count; index++)
            {
                BootstrapPackageStep step = plan[index];
                if (step == null)
                {
                    continue;
                }

                BootstrapSetupItemRole role = GetRole(step.PackageId);
                BootstrapStepPresentationState stepState = GetStepState(state, step, role);
                steps.Add(new BootstrapStepPresentation(
                    index + 1,
                    step.PackageId,
                    role,
                    GetItemTitle(step.PackageId, step.DisplayName),
                    GetStepLabel(state, stepState, role),
                    GetStepDetail(state, step, stepState, role),
                    GetTechnicalDetail(step),
                    stepState));
            }

            return steps;
        }

        private static IReadOnlyList<BootstrapPackageStep> GetPresentationPlan(
            BootstrapSetupSnapshot state)
        {
            if (state.Plan.Count > 0 || state.Phase != BootstrapSetupPhase.Failed)
            {
                return state.Plan;
            }

            return new[]
            {
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.EditorPackageDisplayName,
                    string.Empty),
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageDisplayName,
                    string.Empty),
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageDisplayName,
                    string.Empty)
            };
        }

        private static string GetTechnicalDetail(BootstrapPackageStep step)
        {
            return string.IsNullOrWhiteSpace(step.PackageReference)
                ? step.PackageId + "\nExact Git reference unavailable until setup metadata validates."
                : step.PackageId + "\n" + step.PackageReference;
        }

        private static BootstrapStepPresentationState GetStepState(
            BootstrapSetupSnapshot state,
            BootstrapPackageStep step,
            BootstrapSetupItemRole role)
        {
            if (state.Phase == BootstrapSetupPhase.Failed &&
                state.Plan.Count == 0 &&
                string.IsNullOrWhiteSpace(state.PendingPackageId) &&
                role == BootstrapSetupItemRole.Destination)
            {
                return BootstrapStepPresentationState.Failed;
            }

            if (state.Phase == BootstrapSetupPhase.Verifying &&
                role == BootstrapSetupItemRole.Destination)
            {
                return BootstrapStepPresentationState.Current;
            }

            if (string.Equals(
                    state.PendingPackageId,
                    step.PackageId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return state.Phase == BootstrapSetupPhase.Failed
                    ? BootstrapStepPresentationState.Failed
                    : BootstrapStepPresentationState.Current;
            }

            if (state.CompletedPackageIds.Contains(
                    step.PackageId,
                    StringComparer.OrdinalIgnoreCase) ||
                (state.Phase == BootstrapSetupPhase.Healthy &&
                 state.InstalledState.Contains(step.PackageId)))
            {
                return BootstrapStepPresentationState.Complete;
            }

            if (role == BootstrapSetupItemRole.Destination &&
                state.InstalledState.Contains(step.PackageId) &&
                state.Health.PackageInstallerState != BootstrapPackageInstallerSetupState.Healthy)
            {
                return BootstrapStepPresentationState.Attention;
            }

            return state.InstalledState.Contains(step.PackageId)
                ? BootstrapStepPresentationState.Ready
                : BootstrapStepPresentationState.Pending;
        }

        private static string GetStepLabel(
            BootstrapSetupSnapshot state,
            BootstrapStepPresentationState stepState,
            BootstrapSetupItemRole role)
        {
            switch (stepState)
            {
                case BootstrapStepPresentationState.Complete:
                    return "Installed";
                case BootstrapStepPresentationState.Ready:
                    return "Installed";
                case BootstrapStepPresentationState.Failed:
                    if (state.Plan.Count == 0 &&
                        string.IsNullOrWhiteSpace(state.PendingPackageId))
                    {
                        return "Status unavailable";
                    }

                    return "Needs attention";
                case BootstrapStepPresentationState.Attention:
                    return GetAttentionLabel(state);
                case BootstrapStepPresentationState.Current:
                    if (state.Phase == BootstrapSetupPhase.Verifying) return "Verifying";
                    if (state.PendingKind == BootstrapPersistedOperationKind.Remove)
                    {
                        return "Removing legacy source";
                    }

                    if (state.PendingKind == BootstrapPersistedOperationKind.List ||
                        state.Phase == BootstrapSetupPhase.WaitingForUnity)
                    {
                        return "Waiting for Unity";
                    }

                    return state.PendingKind == BootstrapPersistedOperationKind.Add
                        ? "Installing"
                        : "Preparing";
                default:
                    return role == BootstrapSetupItemRole.Destination
                        ? "Will install last"
                        : "Will install";
            }
        }

        private static string GetStepDetail(
            BootstrapSetupSnapshot state,
            BootstrapPackageStep step,
            BootstrapStepPresentationState stepState,
            BootstrapSetupItemRole role)
        {
            if (stepState == BootstrapStepPresentationState.Current)
            {
                if (state.Phase == BootstrapSetupPhase.Verifying)
                {
                    return "Checking Package Installer source, channel, and lock revision.";
                }

                if (state.PendingKind == BootstrapPersistedOperationKind.Remove)
                {
                    return "Removing the legacy Package Installer source before the Git installation.";
                }

                if (state.PendingKind == BootstrapPersistedOperationKind.List ||
                    state.Phase == BootstrapSetupPhase.WaitingForUnity)
                {
                    return "Unity is resolving " + GetItemTitle(step.PackageId, step.DisplayName) + ".";
                }

                return role == BootstrapSetupItemRole.Destination
                    ? "Installing Package Installer from the selected Git channel."
                    : "Installing this requirement from the selected Git channel.";
            }

            if (stepState == BootstrapStepPresentationState.Complete)
            {
                return role == BootstrapSetupItemRole.Destination
                    ? "Installed and verified as the package-management destination."
                    : "Installed and ready for Package Installer.";
            }

            if (stepState == BootstrapStepPresentationState.Ready)
            {
                return role == BootstrapSetupItemRole.Destination
                    ? "Already installed from the selected Git channel."
                    : "Already installed and ready for Package Installer.";
            }

            if (stepState == BootstrapStepPresentationState.Failed)
            {
                if (state.Plan.Count == 0 &&
                    string.IsNullOrWhiteSpace(state.PendingPackageId))
                {
                    return "Setup metadata or installed state could not be confirmed. Refresh retries detection without changing packages.";
                }

                return "Setup stopped at this item. Check the message above, then refresh.";
            }

            if (stepState == BootstrapStepPresentationState.Attention)
            {
                return GetAttentionDetail(state);
            }

            if (string.Equals(
                    step.PackageId,
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Shared editor foundation required by Package Installer.";
            }

            if (string.Equals(
                    step.PackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Setup diagnostics required by Package Installer.";
            }

            return "Your destination for ongoing Deucarian package management.";
        }

        private static string GetAttentionLabel(BootstrapSetupSnapshot state)
        {
            if (state.Phase == BootstrapSetupPhase.ReviewRequired) return "Review needed";
            if (state.Health.RecommendedAction == BootstrapSetupAction.Migrate) return "Migration needed";
            if (state.Health.PackageInstallerState == BootstrapPackageInstallerSetupState.WrongChannel)
            {
                return "Wrong channel";
            }

            if (state.Health.PackageInstallerState == BootstrapPackageInstallerSetupState.Outdated)
            {
                return "Update needed";
            }

            return "Source repair needed";
        }

        private static string GetAttentionDetail(BootstrapSetupSnapshot state)
        {
            if (state.Health.RecommendedAction == BootstrapSetupAction.Migrate)
            {
                return "Installed from the legacy scoped registry; move it to the selected Git channel.";
            }

            switch (state.Health.PackageInstallerState)
            {
                case BootstrapPackageInstallerSetupState.WrongChannel:
                    return "Installed from a different Git branch than the selected channel.";
                case BootstrapPackageInstallerSetupState.Outdated:
                    return "The lock revision does not match the selected branch head.";
                case BootstrapPackageInstallerSetupState.UnknownReviewRequired:
                    return "The selected remote revision cannot currently be verified.";
                default:
                    return "The installed source does not match the selected Git source.";
            }
        }

        private static IReadOnlyList<BootstrapReceiptPresentation> BuildReceipt(
            BootstrapSetupSnapshot state)
        {
            if (state.Phase != BootstrapSetupPhase.Healthy || !state.Health.IsHealthy)
            {
                return Array.Empty<BootstrapReceiptPresentation>();
            }

            return new[]
            {
                new BootstrapReceiptPresentation(
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    "Editor",
                    "Requirement installed"),
                new BootstrapReceiptPresentation(
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    "Logging",
                    "Requirement installed"),
                new BootstrapReceiptPresentation(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    "Package Installer",
                    "Destination ready")
            };
        }

    }
}
