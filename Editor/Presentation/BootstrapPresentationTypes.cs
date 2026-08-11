using System;
using System.Collections.Generic;

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

    internal enum BootstrapSetupItemRole
    {
        Requirement,
        Destination
    }

    internal enum BootstrapStepPresentationState
    {
        Pending,
        Ready,
        Current,
        Complete,
        Attention,
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
            : this(
                number,
                string.Empty,
                number == 3
                    ? BootstrapSetupItemRole.Destination
                    : BootstrapSetupItemRole.Requirement,
                title,
                GetLegacyLabel(state),
                detail,
                technicalDetail,
                state)
        {
        }

        public BootstrapStepPresentation(
            int number,
            string packageId,
            BootstrapSetupItemRole role,
            string title,
            string label,
            string detail,
            string technicalDetail,
            BootstrapStepPresentationState state)
        {
            Number = number;
            PackageId = packageId ?? string.Empty;
            Role = role;
            Title = title ?? string.Empty;
            Label = label ?? string.Empty;
            Detail = detail ?? string.Empty;
            TechnicalDetail = technicalDetail ?? string.Empty;
            State = state;
        }

        public int Number { get; }

        public string PackageId { get; }

        public BootstrapSetupItemRole Role { get; }

        public string Title { get; }

        public string Label { get; }

        public string Detail { get; }

        public string TechnicalDetail { get; }

        public BootstrapStepPresentationState State { get; }

        private static string GetLegacyLabel(BootstrapStepPresentationState state)
        {
            switch (state)
            {
                case BootstrapStepPresentationState.Current: return "Now";
                case BootstrapStepPresentationState.Complete: return "Installed";
                case BootstrapStepPresentationState.Attention:
                case BootstrapStepPresentationState.Failed: return "Needs attention";
                case BootstrapStepPresentationState.Ready: return "Ready";
                default: return "Required";
            }
        }
    }

    internal sealed class BootstrapReceiptPresentation
    {
        public BootstrapReceiptPresentation(string packageId, string title, string summary)
        {
            PackageId = packageId ?? string.Empty;
            Title = title ?? string.Empty;
            Summary = summary ?? string.Empty;
        }

        public string PackageId { get; }

        public string Title { get; }

        public string Summary { get; }
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
            : this(
                channel,
                phase,
                stateTitle,
                stateMessage,
                statusText,
                tone,
                iconClass,
                primaryAction,
                primaryActionLabel,
                primaryActionTooltip,
                primaryActionEnabled,
                channelEnabled,
                actionSummary,
                actionDetail,
                steps,
                details,
                offlineNotice,
                completedStepCount,
                installedSummary,
                Array.Empty<BootstrapReceiptPresentation>(),
                ShouldShowSetupFlow(phase, steps),
                phase == BootstrapSetupPhase.Healthy,
                !IsBusy(phase) && primaryAction != BootstrapSetupAction.None,
                actionDetail,
                IsBusy(phase))
        {
        }

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
            string installedSummary,
            IReadOnlyList<BootstrapReceiptPresentation> receipt,
            bool showSetupFlow,
            bool showCompletionReceipt,
            bool isActionVisible,
            string footerText,
            bool footerIsPassive)
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
            Steps = new List<BootstrapStepPresentation>(
                steps ?? Array.Empty<BootstrapStepPresentation>()).AsReadOnly();
            Details = new List<BootstrapDetailPresentation>(
                details ?? Array.Empty<BootstrapDetailPresentation>()).AsReadOnly();
            OfflineNotice = offlineNotice ?? string.Empty;
            CompletedStepCount = Math.Max(0, completedStepCount);
            InstalledSummary = installedSummary ?? string.Empty;
            Receipt = new List<BootstrapReceiptPresentation>(
                receipt ?? Array.Empty<BootstrapReceiptPresentation>()).AsReadOnly();
            ShowSetupFlow = showSetupFlow;
            ShowCompletionReceipt = showCompletionReceipt;
            IsActionVisible = isActionVisible;
            FooterText = footerText ?? string.Empty;
            FooterIsPassive = footerIsPassive;
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
        public IReadOnlyList<BootstrapReceiptPresentation> Receipt { get; }
        public bool ShowSetupFlow { get; }
        public bool ShowCompletionReceipt { get; }
        public bool IsActionVisible { get; }
        public string FooterText { get; }
        public bool FooterIsPassive { get; }

        private static bool ShouldShowSetupFlow(
            BootstrapSetupPhase phase,
            IReadOnlyList<BootstrapStepPresentation> steps)
        {
            return steps != null &&
                   steps.Count > 0 &&
                   phase != BootstrapSetupPhase.Loading &&
                   phase != BootstrapSetupPhase.Healthy;
        }

        private static bool IsBusy(BootstrapSetupPhase phase)
        {
            return phase == BootstrapSetupPhase.Loading ||
                   phase == BootstrapSetupPhase.Installing ||
                   phase == BootstrapSetupPhase.WaitingForUnity ||
                   phase == BootstrapSetupPhase.Verifying;
        }
    }
}
