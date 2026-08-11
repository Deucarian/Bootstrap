using System;
using System.Collections.Generic;
using System.Linq;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed partial class BootstrapSetupCoordinator
    {
        private bool PreparePersistedPlan()
        {
            IReadOnlyList<BootstrapPackageStep> catalogPlan = _catalogSelection.Plan.Steps;
            if (_operation.Plan.Count == 0)
            {
                _operation.SetPlan(catalogPlan);
            }
            else
            {
                _operation.HydratePlan(catalogPlan);
            }

            IReadOnlyList<BootstrapPackageStep> persistedPlan = _operation.GetPlan();
            if (!BootstrapSetupPlanner.IsExactSetupClosure(persistedPlan, out string planError) ||
                persistedPlan.Any(step => step == null || string.IsNullOrWhiteSpace(step.PackageReference)))
            {
                FailOperation(
                    "Saved setup progress cannot be resumed safely.",
                    string.IsNullOrWhiteSpace(planError)
                        ? "The saved plan is missing an authoritative Git reference."
                        : planError);
                return false;
            }

            for (int index = 0; index < persistedPlan.Count; index++)
            {
                BootstrapPackageStep persisted = persistedPlan[index];
                BootstrapPackageStep validated = catalogPlan[index];
                if (!string.Equals(
                        persisted.PackageReference.Trim(),
                        validated.PackageReference.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    FailOperation(
                        "Saved setup progress requires review.",
                        "The validated Registry Git reference changed for " + persisted.DisplayName +
                        ". Refresh and review a new plan before continuing.");
                    return false;
                }
            }

            _operationStore.Save(_operation);
            return true;
        }

        private void Publish(BootstrapSetupPhase phase, string status, string error)
        {
            IReadOnlyList<BootstrapPackageStep> plan = GetCurrentPlan();
            BootstrapHealthReport health = _health ?? BootstrapSetupPolicy.Evaluate(
                _channel,
                _installedState,
                _targetGitUrl,
                _targetRevision);
            string notice = JoinNotice(
                _catalogSelection != null ? _catalogSelection.Notice : string.Empty,
                _revisionNotice);

            Snapshot = new BootstrapSetupSnapshot(
                _channel,
                phase,
                _catalogSelection != null ? _catalogSelection.Origin : BootstrapCatalogOrigin.None,
                _catalogSelection != null ? _catalogSelection.Source : string.Empty,
                notice,
                status,
                error,
                _targetGitUrl,
                _targetRevision,
                plan,
                _operation.CompletedPackageIds,
                _operation.PendingPackageId,
                _installedState,
                health,
                _legacyRegistryStatus,
                _operation.PendingKind);
            Changed?.Invoke();
        }

        private IReadOnlyList<BootstrapPackageStep> GetCurrentPlan()
        {
            if (_operation.Active && _operation.Plan.Count > 0)
            {
                return _operation.GetPlan();
            }

            return _catalogSelection != null && _catalogSelection.Success
                ? _catalogSelection.Plan.Steps
                : Array.Empty<BootstrapPackageStep>();
        }

        private static string JoinNotice(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                return second ?? string.Empty;
            }

            return string.IsNullOrWhiteSpace(second) ? first : first + " " + second;
        }
    }
}
