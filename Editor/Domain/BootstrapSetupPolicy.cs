using System;
using System.Collections.Generic;

namespace Deucarian.Bootstrap.Editor
{
    internal static class BootstrapSetupPolicy
    {
        public static BootstrapHealthReport Evaluate(
            BootstrapChannel channel,
            BootstrapInstalledState installedState,
            string targetRevision)
        {
            return Evaluate(
                channel,
                installedState,
                BootstrapChannelUtility.GetPackageInstallerGitUrl(channel),
                targetRevision);
        }

        public static BootstrapHealthReport Evaluate(
            BootstrapChannel channel,
            BootstrapInstalledState installedState,
            string targetGitUrl,
            string targetRevision)
        {
            BootstrapInstalledState installed = installedState ?? BootstrapInstalledState.Empty;
            bool editorInstalled = installed.Contains(DeucarianBootstrapPackageConstants.EditorPackageId);
            bool loggingInstalled = installed.Contains(DeucarianBootstrapPackageConstants.LoggingPackageId);
            BootstrapInstalledPackageInfo packageInstaller =
                installed.Get(DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
            BootstrapPackageInstallerSetupState packageInstallerState =
                BootstrapPackageInstallerStatus.Evaluate(
                    channel,
                    packageInstaller,
                    targetGitUrl,
                    targetRevision);
            bool anyInstalled = editorInstalled || loggingInstalled || packageInstaller != null;
            BootstrapSetupAction action = ResolveAction(
                editorInstalled,
                loggingInstalled,
                packageInstaller,
                packageInstallerState,
                anyInstalled);

            return new BootstrapHealthReport(
                editorInstalled,
                loggingInstalled,
                packageInstallerState,
                action,
                anyInstalled);
        }

        public static int FindNextStep(
            IReadOnlyList<BootstrapPackageStep> plan,
            ISet<string> completedPackageIds,
            BootstrapHealthReport health,
            bool targetRevisionKnown)
        {
            if (plan == null || plan.Count == 0)
            {
                return 0;
            }

            for (int index = 0; index < plan.Count; index++)
            {
                BootstrapPackageStep step = plan[index];
                if (step == null)
                {
                    return index;
                }

                if (completedPackageIds != null && completedPackageIds.Contains(step.PackageId))
                {
                    continue;
                }

                if (IsPackageInstallerStep(step))
                {
                    if (health != null && health.PackageInstallerState == BootstrapPackageInstallerSetupState.Healthy)
                    {
                        continue;
                    }

                    if (health != null &&
                        health.PackageInstallerState == BootstrapPackageInstallerSetupState.UnknownReviewRequired &&
                        !targetRevisionKnown)
                    {
                        return plan.Count;
                    }
                }

                return index;
            }

            return plan.Count;
        }

        public static bool IsResolvedForStep(
            BootstrapInstalledPackageInfo installedPackage,
            BootstrapPackageStep step)
        {
            return installedPackage != null &&
                   step != null &&
                   installedPackage.IsGit &&
                   string.Equals(
                       NormalizeGitReference(installedPackage.BestReference),
                       NormalizeGitReference(step.PackageReference),
                       StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldRemoveBeforeAdd(
            BootstrapPackageStep step,
            BootstrapInstalledPackageInfo installedPackage)
        {
            if (!IsPackageInstallerStep(step) || installedPackage == null)
            {
                return false;
            }

            return installedPackage.IsRegistry || !installedPackage.IsGit;
        }

        public static int FindStepIndex(IReadOnlyList<BootstrapPackageStep> plan, string packageId)
        {
            if (plan == null || string.IsNullOrWhiteSpace(packageId))
            {
                return -1;
            }

            for (int index = 0; index < plan.Count; index++)
            {
                if (plan[index] != null && string.Equals(
                        plan[index].PackageId,
                        packageId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static BootstrapSetupAction ResolveAction(
            bool editorInstalled,
            bool loggingInstalled,
            BootstrapInstalledPackageInfo packageInstaller,
            BootstrapPackageInstallerSetupState packageInstallerState,
            bool anyInstalled)
        {
            if (editorInstalled &&
                loggingInstalled &&
                packageInstallerState == BootstrapPackageInstallerSetupState.Healthy)
            {
                return BootstrapSetupAction.OpenPackageInstaller;
            }

            if (packageInstaller != null && packageInstaller.IsRegistry)
            {
                return BootstrapSetupAction.Migrate;
            }

            if (packageInstallerState == BootstrapPackageInstallerSetupState.WrongChannel)
            {
                return BootstrapSetupAction.SwitchChannel;
            }

            if (editorInstalled &&
                loggingInstalled &&
                packageInstallerState == BootstrapPackageInstallerSetupState.UnknownReviewRequired &&
                packageInstaller != null)
            {
                return BootstrapSetupAction.Refresh;
            }

            return anyInstalled ? BootstrapSetupAction.Repair : BootstrapSetupAction.Install;
        }

        private static bool IsPackageInstallerStep(BootstrapPackageStep step)
        {
            return step != null && string.Equals(
                step.PackageId,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeGitReference(string reference)
        {
            string normalized = (reference ?? string.Empty).Trim().Replace('\\', '/');
            int packagePrefix = normalized.IndexOf("@http", StringComparison.OrdinalIgnoreCase);
            if (packagePrefix > 0)
            {
                normalized = normalized.Substring(packagePrefix + 1);
            }

            return normalized.StartsWith("git+", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring("git+".Length)
                : normalized;
        }
    }
}
