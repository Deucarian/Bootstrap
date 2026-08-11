namespace Deucarian.Bootstrap.Editor
{
    internal sealed class BootstrapWindowHandoffDecision
    {
        public BootstrapWindowHandoffDecision(bool closeWindow, string message)
        {
            CloseWindow = closeWindow;
            Message = message ?? string.Empty;
        }

        public bool CloseWindow { get; }

        public string Message { get; }
    }

    internal static class BootstrapWindowLifecyclePolicy
    {
        public static bool ShouldRetireAutomaticStartup(BootstrapSetupSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.Phase == BootstrapSetupPhase.Healthy &&
                   snapshot.Health != null &&
                   snapshot.Health.IsHealthy;
        }

        public static bool ShouldResumeAfterReload(BootstrapOperationState operation)
        {
            return operation != null && operation.Active;
        }

        public static BootstrapWindowHandoffDecision EvaluateHandoff(
            BootstrapHandoffResult result)
        {
            if (result != null && result.Success)
            {
                return new BootstrapWindowHandoffDecision(true, string.Empty);
            }

            string message = result != null ? result.Message : string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                message =
                    "Package Installer could not be opened. Let Unity finish compiling, then refresh status.";
            }

            return new BootstrapWindowHandoffDecision(false, message);
        }
    }
}
