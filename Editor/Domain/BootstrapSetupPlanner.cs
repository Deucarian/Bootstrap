using System;
using System.Collections.Generic;

namespace Deucarian.Bootstrap.Editor
{
    internal static class BootstrapSetupPlanner
    {
        private static readonly string[] RequiredOrder =
        {
            DeucarianBootstrapPackageConstants.EditorPackageId,
            DeucarianBootstrapPackageConstants.LoggingPackageId,
            DeucarianBootstrapPackageConstants.PackageInstallerPackageId
        };

        public static BootstrapInstallPlanResult Build(
            BootstrapPackageCatalog catalog,
            BootstrapChannel channel)
        {
            BootstrapInstallPlanResult result = BootstrapInstallPlanner.BuildPlan(
                catalog,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                channel);

            if (!result.Success)
            {
                return result;
            }

            if (!IsExactSetupClosure(result.Steps, out string error))
            {
                return BootstrapInstallPlanResult.CreateFailure(error);
            }

            foreach (BootstrapPackageStep step in result.Steps)
            {
                if (!BootstrapChannelUtility.TryDetectFromGitReference(
                        step.PackageReference,
                        out BootstrapChannel referenceChannel) ||
                    referenceChannel != channel)
                {
                    return BootstrapInstallPlanResult.CreateFailure(
                        step.DisplayName + " must target Git #" +
                        BootstrapChannelUtility.GetGitBranch(channel) +
                        " for the selected channel.");
                }
            }

            return result;
        }

        public static bool IsExactSetupClosure(
            IReadOnlyList<BootstrapPackageStep> steps,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (steps == null || steps.Count != RequiredOrder.Length)
            {
                errorMessage = "Setup catalog must resolve exactly Editor, Logging, and Package Installer.";
                return false;
            }

            for (int index = 0; index < RequiredOrder.Length; index++)
            {
                BootstrapPackageStep step = steps[index];
                if (step == null || !string.Equals(
                        step.PackageId,
                        RequiredOrder[index],
                        StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "Setup catalog dependency order must be Editor, Logging, then Package Installer.";
                    return false;
                }
            }

            return true;
        }
    }
}
