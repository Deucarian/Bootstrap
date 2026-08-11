using System;
using System.Collections.Generic;
using System.Linq;

namespace Deucarian.Bootstrap.Editor
{
    internal enum BootstrapPresentationTone
    {
        Neutral,
        Info,
        Success,
        Warning,
        Error
    }

    internal enum BootstrapStepPresentationState
    {
        Pending,
        Ready,
        Current,
        Complete,
        Failed
    }

    internal sealed class BootstrapStepPresentation
    {
        public BootstrapStepPresentation(
            int number,
            string title,
            string detail,
            string technicalDetail,
            BootstrapStepPresentationState state)
        {
            Number = number;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            TechnicalDetail = technicalDetail ?? string.Empty;
            State = state;
        }

        public int Number { get; }

        public string Title { get; }

        public string Detail { get; }

        public string TechnicalDetail { get; }

        public BootstrapStepPresentationState State { get; }
    }

    internal sealed class BootstrapDetailPresentation
    {
        public BootstrapDetailPresentation(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Label { get; }

        public string Value { get; }
    }

    internal sealed class BootstrapPresentationModel
    {
        public BootstrapPresentationModel(
            BootstrapChannel channel,
            BootstrapSetupPhase phase,
            string stateTitle,
            string stateMessage,
            string statusText,
            BootstrapPresentationTone tone,
            string iconClass,
            BootstrapSetupAction primaryAction,
            string primaryActionLabel,
            string primaryActionTooltip,
            bool primaryActionEnabled,
            bool channelEnabled,
            string actionSummary,
            string actionDetail,
            IReadOnlyList<BootstrapStepPresentation> steps,
            IReadOnlyList<BootstrapDetailPresentation> details,
            string offlineNotice,
            int completedStepCount,
            string installedSummary)
        {
            Channel = channel;
            Phase = phase;
            StateTitle = stateTitle ?? string.Empty;
            StateMessage = stateMessage ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            Tone = tone;
            IconClass = iconClass ?? string.Empty;
            PrimaryAction = primaryAction;
            PrimaryActionLabel = primaryActionLabel ?? string.Empty;
            PrimaryActionTooltip = primaryActionTooltip ?? string.Empty;
            PrimaryActionEnabled = primaryActionEnabled;
            ChannelEnabled = channelEnabled;
            ActionSummary = actionSummary ?? string.Empty;
            ActionDetail = actionDetail ?? string.Empty;
            Steps = steps ?? Array.Empty<BootstrapStepPresentation>();
            Details = details ?? Array.Empty<BootstrapDetailPresentation>();
            OfflineNotice = offlineNotice ?? string.Empty;
            CompletedStepCount = Math.Max(0, completedStepCount);
            InstalledSummary = installedSummary ?? string.Empty;
        }

        public BootstrapChannel Channel { get; }
        public BootstrapSetupPhase Phase { get; }
        public string StateTitle { get; }
        public string StateMessage { get; }
        public string StatusText { get; }
        public BootstrapPresentationTone Tone { get; }
        public string IconClass { get; }
        public BootstrapSetupAction PrimaryAction { get; }
        public string PrimaryActionLabel { get; }
        public string PrimaryActionTooltip { get; }
        public bool PrimaryActionEnabled { get; }
        public bool ChannelEnabled { get; }
        public string ActionSummary { get; }
        public string ActionDetail { get; }
        public IReadOnlyList<BootstrapStepPresentation> Steps { get; }
        public IReadOnlyList<BootstrapDetailPresentation> Details { get; }
        public string OfflineNotice { get; }
        public int CompletedStepCount { get; }
        public string InstalledSummary { get; }
    }

    internal static class BootstrapPresentationModelFactory
    {
        public static BootstrapPresentationModel Create(
            BootstrapSetupSnapshot snapshot,
            string transientMessage = null)
        {
            BootstrapSetupSnapshot state = snapshot ??
                BootstrapSetupSnapshot.Loading(BootstrapChannel.Stable, "Checking setup...");
            BootstrapSetupAction action = ResolveVisibleAction(state);
            string title = GetTitle(state, action);
            string message = GetMessage(state, action);
            string status = !string.IsNullOrWhiteSpace(transientMessage)
                ? transientMessage
                : !string.IsNullOrWhiteSpace(state.Error)
                    ? state.Error
                    : state.Status;
            BootstrapPresentationTone tone = string.IsNullOrWhiteSpace(transientMessage)
                ? GetTone(state)
                : BootstrapPresentationTone.Error;
            string label = GetActionLabel(state, action);
            bool actionEnabled = !state.IsBusy && action != BootstrapSetupAction.None;
            IReadOnlyList<BootstrapStepPresentation> steps = BuildSteps(state);
            int completed = steps.Count(step => step.State == BootstrapStepPresentationState.Complete);

            return new BootstrapPresentationModel(
                state.Channel,
                state.Phase,
                title,
                message,
                status,
                tone,
                GetIconClass(state, action),
                action,
                label,
                GetActionTooltip(action),
                actionEnabled,
                !state.IsBusy,
                GetActionSummary(state, action),
                GetActionDetail(state, action),
                steps,
                BuildDetails(state),
                state.CatalogOrigin == BootstrapCatalogOrigin.BundledFallback
                    ? state.CatalogNotice
                    : string.Empty,
                completed,
                GetInstalledSummary(state));
        }

        private static BootstrapSetupAction ResolveVisibleAction(BootstrapSetupSnapshot state)
        {
            if (state.IsBusy)
            {
                return BootstrapSetupAction.None;
            }

            return state.Phase == BootstrapSetupPhase.Failed
                ? BootstrapSetupAction.Refresh
                : state.Health.RecommendedAction;
        }

        private static string GetTitle(BootstrapSetupSnapshot state, BootstrapSetupAction action)
        {
            switch (state.Phase)
            {
                case BootstrapSetupPhase.Loading:
                    return "Checking setup";
                case BootstrapSetupPhase.Installing:
                    return "Installing setup";
                case BootstrapSetupPhase.WaitingForUnity:
                    return "Waiting for Unity";
                case BootstrapSetupPhase.Verifying:
                    return "Verifying Package Installer";
                case BootstrapSetupPhase.Healthy:
                    return "Setup complete";
                case BootstrapSetupPhase.ReviewRequired:
                    return "Revision review needed";
                case BootstrapSetupPhase.Failed:
                    return "Setup stopped";
            }

            switch (action)
            {
                case BootstrapSetupAction.Install:
                    return "Ready for first-time setup";
                case BootstrapSetupAction.Migrate:
                    return "Legacy Package Installer source detected";
                case BootstrapSetupAction.SwitchChannel:
                    return "Package Installer is on another channel";
                default:
                    if (state.Health.PackageInstallerState == BootstrapPackageInstallerSetupState.Outdated)
                    {
                        return "Package Installer is outdated";
                    }

                    if (state.Health.PackageInstallerState == BootstrapPackageInstallerSetupState.WrongSource)
                    {
                        return "Package Installer source needs repair";
                    }

                    if (state.Health.PackageInstallerState == BootstrapPackageInstallerSetupState.Missing)
                    {
                        return "Package Installer is missing";
                    }

                    return "Setup dependencies need repair";
            }
        }

        private static string GetMessage(BootstrapSetupSnapshot state, BootstrapSetupAction action)
        {
            if (state.Phase == BootstrapSetupPhase.Healthy)
            {
                return "Package Installer matches the selected channel and lock revision.";
            }

            if (state.Phase == BootstrapSetupPhase.Loading)
            {
                return "Checking channel, packages, and revision.";
            }

            if (state.Phase == BootstrapSetupPhase.Installing ||
                state.Phase == BootstrapSetupPhase.WaitingForUnity ||
                state.Phase == BootstrapSetupPhase.Verifying)
            {
                return "Safe to close. Setup resumes after Unity reloads.";
            }

            if (state.Phase == BootstrapSetupPhase.ReviewRequired)
            {
                return "Bootstrap cannot verify the selected remote revision yet.";
            }

            if (state.Phase == BootstrapSetupPhase.Failed)
            {
                return "Nothing else will change until you retry.";
            }

            switch (action)
            {
                case BootstrapSetupAction.Install:
                    return "Installs Editor, Logging, then Package Installer.";
                case BootstrapSetupAction.Migrate:
                    return "Moves Package Installer from the legacy registry to the selected Git channel.";
                case BootstrapSetupAction.SwitchChannel:
                    return "Moves Package Installer to the selected branch after checking its setup dependencies.";
                default:
                    if (state.Health.PackageInstallerState == BootstrapPackageInstallerSetupState.Outdated)
                    {
                        return "The installed revision does not match the selected branch.";
                    }

                    if (state.Health.PackageInstallerState == BootstrapPackageInstallerSetupState.WrongSource)
                    {
                        return "The installed Git source does not match this channel.";
                    }

                    return "Missing setup packages will be repaired in order.";
            }
        }

        private static IReadOnlyList<BootstrapStepPresentation> BuildSteps(BootstrapSetupSnapshot state)
        {
            List<BootstrapStepPresentation> steps = new List<BootstrapStepPresentation>();
            for (int index = 0; index < state.Plan.Count; index++)
            {
                BootstrapPackageStep step = state.Plan[index];
                BootstrapStepPresentationState stepState = GetStepState(state, step);
                steps.Add(new BootstrapStepPresentation(
                    index + 1,
                    step.DisplayName,
                    GetStepDetail(step, stepState),
                    step.PackageId + "\n" + step.PackageReference,
                    stepState));
            }

            return steps;
        }

        private static BootstrapStepPresentationState GetStepState(
            BootstrapSetupSnapshot state,
            BootstrapPackageStep step)
        {
            if (state.CompletedPackageIds.Contains(step.PackageId, StringComparer.OrdinalIgnoreCase) ||
                (state.Phase == BootstrapSetupPhase.Healthy && state.InstalledState.Contains(step.PackageId)))
            {
                return BootstrapStepPresentationState.Complete;
            }

            if (string.Equals(state.PendingPackageId, step.PackageId, StringComparison.OrdinalIgnoreCase))
            {
                return state.Phase == BootstrapSetupPhase.Failed
                    ? BootstrapStepPresentationState.Failed
                    : BootstrapStepPresentationState.Current;
            }

            return state.InstalledState.Contains(step.PackageId)
                ? BootstrapStepPresentationState.Ready
                : BootstrapStepPresentationState.Pending;
        }

        private static string GetStepDetail(
            BootstrapPackageStep step,
            BootstrapStepPresentationState state)
        {
            if (state == BootstrapStepPresentationState.Complete)
            {
                return "Completed from the selected Git channel.";
            }

            if (state == BootstrapStepPresentationState.Current)
            {
                return "Current durable operation.";
            }

            if (state == BootstrapStepPresentationState.Failed)
            {
                return "This step needs attention before setup can continue.";
            }

            if (string.Equals(step.PackageId, DeucarianBootstrapPackageConstants.EditorPackageId, StringComparison.OrdinalIgnoreCase))
            {
                return "Shared editor shell and resources, installed first.";
            }

            if (string.Equals(step.PackageId, DeucarianBootstrapPackageConstants.LoggingPackageId, StringComparison.OrdinalIgnoreCase))
            {
                return "Installer diagnostics facade, installed after Editor.";
            }

            return "Package management destination, installed or migrated last.";
        }

        private static IReadOnlyList<BootstrapDetailPresentation> BuildDetails(BootstrapSetupSnapshot state)
        {
            BootstrapInstalledPackageInfo installer = state.InstalledState.Get(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
            List<BootstrapDetailPresentation> details = new List<BootstrapDetailPresentation>
            {
                new BootstrapDetailPresentation("Selected channel", BootstrapChannelUtility.GetDisplayName(state.Channel) + " · Git #" + BootstrapChannelUtility.GetGitBranch(state.Channel)),
                new BootstrapDetailPresentation("Registry source", string.IsNullOrWhiteSpace(state.CatalogSource) ? "Checking..." : state.CatalogSource),
                new BootstrapDetailPresentation("Package Installer target", state.TargetGitUrl),
                new BootstrapDetailPresentation("Target branch revision", ExactRevision(state.TargetRevision, "Unverifiable")),
                new BootstrapDetailPresentation("Installed source", installer != null ? installer.BestReference : "Not installed"),
                new BootstrapDetailPresentation("Installed lock revision", installer != null ? ExactRevision(installer.LockRevision, "Unverifiable") : "Not installed"),
                new BootstrapDetailPresentation("Legacy scoped registry", state.LegacyRegistryStatus.Detail),
                new BootstrapDetailPresentation("Registry notice", string.IsNullOrWhiteSpace(state.CatalogNotice) ? "Remote metadata validated." : state.CatalogNotice)
            };

            foreach (BootstrapPackageStep step in state.Plan)
            {
                details.Add(new BootstrapDetailPresentation(
                    step.DisplayName + " Git reference",
                    step.PackageReference));
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
                case BootstrapSetupPhase.Review:
                    return BootstrapPresentationTone.Warning;
                case BootstrapSetupPhase.Loading:
                case BootstrapSetupPhase.Installing:
                case BootstrapSetupPhase.WaitingForUnity:
                case BootstrapSetupPhase.Verifying:
                    return BootstrapPresentationTone.Info;
                default:
                    return BootstrapPresentationTone.Neutral;
            }
        }

        private static string GetIconClass(BootstrapSetupSnapshot state, BootstrapSetupAction action)
        {
            if (state.Phase == BootstrapSetupPhase.Healthy) return "bootstrap-icon--success";
            if (state.Phase == BootstrapSetupPhase.Failed) return "bootstrap-icon--error";
            if (state.IsBusy) return "bootstrap-icon--loading";
            if (state.Phase == BootstrapSetupPhase.ReviewRequired) return "bootstrap-icon--review";
            if (action == BootstrapSetupAction.Install) return "bootstrap-icon--install";
            if (action == BootstrapSetupAction.OpenPackageInstaller) return "bootstrap-icon--open";
            return "bootstrap-icon--repair";
        }

        private static string GetActionLabel(BootstrapSetupSnapshot state, BootstrapSetupAction action)
        {
            if (state.Phase == BootstrapSetupPhase.Loading) return "Checking...";
            if (state.Phase == BootstrapSetupPhase.Installing) return "Installing...";
            if (state.Phase == BootstrapSetupPhase.WaitingForUnity) return "Waiting for Unity...";
            if (state.Phase == BootstrapSetupPhase.Verifying) return "Verifying...";

            switch (action)
            {
                case BootstrapSetupAction.Install: return "Install";
                case BootstrapSetupAction.Repair: return "Repair";
                case BootstrapSetupAction.SwitchChannel: return "Switch Channel";
                case BootstrapSetupAction.Migrate: return "Migrate";
                case BootstrapSetupAction.Refresh: return "Refresh Status";
                case BootstrapSetupAction.OpenPackageInstaller: return "Open Package Installer";
                default: return "Working...";
            }
        }

        private static string GetActionTooltip(BootstrapSetupAction action)
        {
            return action == BootstrapSetupAction.OpenPackageInstaller
                ? "Open Tools > Deucarian > Tools and Quality > Package Installer."
                : action == BootstrapSetupAction.Refresh
                    ? "Read registry, UPM, source, and revision state again without changing packages."
                    : "Run the reviewed setup closure. Package changes begin only after this click.";
        }

        private static string GetActionSummary(BootstrapSetupSnapshot state, BootstrapSetupAction action)
        {
            return action == BootstrapSetupAction.OpenPackageInstaller
                ? "Setup complete"
                : state.IsBusy
                    ? "Durable setup progress"
                    : "One explicit action";
        }

        private static string GetActionDetail(BootstrapSetupSnapshot state, BootstrapSetupAction action)
        {
            return action == BootstrapSetupAction.OpenPackageInstaller
                ? GetInstalledSummary(state)
                : state.IsBusy
                    ? Math.Min(state.CompletedPackageIds.Count, state.Plan.Count) + " of " + state.Plan.Count + " steps completed"
                    : "Opening Bootstrap never installs or changes packages automatically.";
        }

        private static string GetInstalledSummary(BootstrapSetupSnapshot state)
        {
            BootstrapInstalledPackageInfo package = state.InstalledState.Get(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
            if (package == null)
            {
                return "Package Installer is not installed.";
            }

            string version = string.IsNullOrWhiteSpace(package.Version) ? "version unknown" : "v" + package.Version;
            return version + " · Git #" + BootstrapChannelUtility.GetGitBranch(state.Channel) +
                " · " + ShortRevision(package.LockRevision, "revision unknown");
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
