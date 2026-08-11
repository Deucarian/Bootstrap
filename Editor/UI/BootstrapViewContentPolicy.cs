using System;

namespace Deucarian.Bootstrap.Editor
{
    /// <summary>
    /// Pure display policy for the minimal Bootstrap hero and setup rail.
    /// </summary>
    internal static class BootstrapViewContentPolicy
    {
        public static string GetHeroTitle(BootstrapPresentationModel model)
        {
            if (model == null)
            {
                return string.Empty;
            }

            if (model.Phase == BootstrapSetupPhase.Installing)
            {
                foreach (BootstrapStepPresentation step in model.Steps)
                {
                    if (step.State == BootstrapStepPresentationState.Current)
                    {
                        return "Installing " + step.Title;
                    }
                }
            }

            return model.StateTitle;
        }

        public static string GetContextText(BootstrapPresentationModel model)
        {
            if (model == null)
            {
                return string.Empty;
            }

            if (model.Tone == BootstrapPresentationTone.Error)
            {
                return model.StatusText;
            }

            if (model.Phase == BootstrapSetupPhase.Healthy)
            {
                return model.InstalledSummary;
            }

            return model.Phase == BootstrapSetupPhase.ReviewRequired
                ? model.StatusText
                : string.Empty;
        }

        public static string GetProgressText(BootstrapPresentationModel model)
        {
            if (model == null || model.Steps.Count == 0)
            {
                return string.Empty;
            }

            int currentStep = Math.Min(model.Steps.Count, model.CompletedStepCount + 1);
            foreach (BootstrapStepPresentation step in model.Steps)
            {
                if (step.State == BootstrapStepPresentationState.Current)
                {
                    currentStep = step.Number;
                    break;
                }
            }

            return "Step " + Math.Max(1, currentStep) + " of " + model.Steps.Count;
        }

        public static bool ShouldShowPlan(BootstrapPresentationModel model)
        {
            return model != null &&
                   model.Steps.Count > 0 &&
                   model.Phase != BootstrapSetupPhase.Loading &&
                   model.Phase != BootstrapSetupPhase.Healthy;
        }

        public static bool IsBusyPhase(BootstrapSetupPhase phase)
        {
            return phase == BootstrapSetupPhase.Installing ||
                   phase == BootstrapSetupPhase.WaitingForUnity ||
                   phase == BootstrapSetupPhase.Verifying;
        }

        public static string GetActionIconClass(BootstrapSetupAction action)
        {
            if (action == BootstrapSetupAction.Install) return "bootstrap-icon--install";
            if (action == BootstrapSetupAction.OpenPackageInstaller) return "bootstrap-icon--open";
            if (action == BootstrapSetupAction.None || action == BootstrapSetupAction.Refresh)
            {
                return "bootstrap-icon--loading";
            }

            return "bootstrap-icon--repair";
        }

        public static string GetStepClass(BootstrapStepPresentationState state)
        {
            switch (state)
            {
                case BootstrapStepPresentationState.Current: return "bootstrap-step--current";
                case BootstrapStepPresentationState.Complete: return "bootstrap-step--complete";
                case BootstrapStepPresentationState.Failed: return "bootstrap-step--failed";
                default: return "bootstrap-step--pending";
            }
        }

        public static string GetStepStateLabel(BootstrapStepPresentationState state)
        {
            switch (state)
            {
                case BootstrapStepPresentationState.Current: return "Now";
                case BootstrapStepPresentationState.Complete: return "Done";
                case BootstrapStepPresentationState.Failed: return "Needs attention";
                case BootstrapStepPresentationState.Ready: return "Ready";
                default: return "Next";
            }
        }
    }
}
