using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    [Serializable]
    internal sealed class BootstrapPersistedPackageStep
    {
        [SerializeField] private string packageId;
        [SerializeField] private string displayName;
        [SerializeField] private string packageReference;

        public BootstrapPersistedPackageStep()
        {
        }

        public BootstrapPersistedPackageStep(BootstrapPackageStep step)
        {
            packageId = step != null ? step.PackageId : string.Empty;
            displayName = step != null ? step.DisplayName : string.Empty;
            packageReference = step != null ? step.PackageReference : string.Empty;
        }

        public string PackageId => packageId ?? string.Empty;

        public string DisplayName => displayName ?? string.Empty;

        public string PackageReference => packageReference ?? string.Empty;

        public BootstrapPackageStep ToStep()
        {
            return new BootstrapPackageStep(PackageId, DisplayName, PackageReference);
        }

        public void Hydrate(BootstrapPackageStep step)
        {
            if (step == null || !string.Equals(PackageId, step.PackageId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = step.DisplayName;
            }

            if (string.IsNullOrWhiteSpace(packageReference))
            {
                packageReference = step.PackageReference;
            }
        }
    }

    [Serializable]
    internal sealed class BootstrapOperationState
    {
        private const int CurrentSchemaVersion = 2;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private bool active;
        [SerializeField] private bool verifying;
        [SerializeField] private BootstrapChannel channel;
        [SerializeField] private BootstrapPersistedPackageStep[] plan = Array.Empty<BootstrapPersistedPackageStep>();
        [SerializeField] private string[] completedPackageIds = Array.Empty<string>();
        [SerializeField] private string pendingPackageId = string.Empty;
        [SerializeField] private string pendingPackageReference = string.Empty;
        [SerializeField] private BootstrapPersistedOperationKind pendingKind;
        [SerializeField] private int retryCount;
        [SerializeField] private string status = string.Empty;
        [SerializeField] private string error = string.Empty;

        public int SchemaVersion => schemaVersion;

        public bool Active
        {
            get { return active; }
            set { active = value; }
        }

        public bool Verifying
        {
            get { return verifying; }
            set { verifying = value; }
        }

        public BootstrapChannel Channel
        {
            get { return channel; }
            set { channel = value == BootstrapChannel.Development ? value : BootstrapChannel.Stable; }
        }

        public IReadOnlyList<BootstrapPersistedPackageStep> Plan =>
            plan ?? Array.Empty<BootstrapPersistedPackageStep>();

        public IReadOnlyList<string> CompletedPackageIds =>
            completedPackageIds ?? Array.Empty<string>();

        public string PendingPackageId => pendingPackageId ?? string.Empty;

        public string PendingPackageReference => pendingPackageReference ?? string.Empty;

        public BootstrapPersistedOperationKind PendingKind => pendingKind;

        public int RetryCount
        {
            get { return Math.Max(0, retryCount); }
            set { retryCount = Math.Max(0, value); }
        }

        public string Status
        {
            get { return status ?? string.Empty; }
            set { status = value ?? string.Empty; }
        }

        public string Error
        {
            get { return error ?? string.Empty; }
            set { error = value ?? string.Empty; }
        }

        public bool HasFailure => !Active && !string.IsNullOrWhiteSpace(Error);

        public static BootstrapOperationState CreateActive(
            BootstrapChannel channel,
            IReadOnlyList<BootstrapPackageStep> setupPlan)
        {
            BootstrapOperationState state = new BootstrapOperationState
            {
                Active = true,
                Channel = channel,
                Status = "Preparing setup..."
            };
            state.SetPlan(setupPlan);
            return state;
        }

        public void SetPlan(IReadOnlyList<BootstrapPackageStep> setupPlan)
        {
            plan = (setupPlan ?? Array.Empty<BootstrapPackageStep>())
                .Where(step => step != null)
                .Select(step => new BootstrapPersistedPackageStep(step))
                .ToArray();
        }

        public IReadOnlyList<BootstrapPackageStep> GetPlan()
        {
            return (plan ?? Array.Empty<BootstrapPersistedPackageStep>())
                .Where(step => step != null)
                .Select(step => step.ToStep())
                .ToArray();
        }

        public void HydratePlan(IReadOnlyList<BootstrapPackageStep> currentPlan)
        {
            foreach (BootstrapPersistedPackageStep persisted in plan ?? Array.Empty<BootstrapPersistedPackageStep>())
            {
                if (persisted == null)
                {
                    continue;
                }

                BootstrapPackageStep match = (currentPlan ?? Array.Empty<BootstrapPackageStep>())
                    .FirstOrDefault(step => step != null && string.Equals(
                        step.PackageId,
                        persisted.PackageId,
                        StringComparison.OrdinalIgnoreCase));
                persisted.Hydrate(match);
            }
        }

        public bool IsCompleted(string packageId)
        {
            return !string.IsNullOrWhiteSpace(packageId) &&
                   (completedPackageIds ?? Array.Empty<string>()).Contains(
                       packageId,
                       StringComparer.OrdinalIgnoreCase);
        }

        public void MarkCompleted(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId) || IsCompleted(packageId))
            {
                return;
            }

            List<string> values = new List<string>(completedPackageIds ?? Array.Empty<string>())
            {
                packageId
            };
            completedPackageIds = values.ToArray();
        }

        public void SetCompleted(IEnumerable<string> packageIds)
        {
            completedPackageIds = (packageIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public void SetPending(BootstrapPackageStep step, BootstrapPersistedOperationKind kind)
        {
            pendingPackageId = step != null ? step.PackageId : string.Empty;
            pendingPackageReference = step != null ? step.PackageReference : string.Empty;
            pendingKind = kind;
        }

        public void SetPendingKind(BootstrapPersistedOperationKind kind)
        {
            pendingKind = kind;
        }

        public void ClearPending()
        {
            pendingPackageId = string.Empty;
            pendingPackageReference = string.Empty;
            pendingKind = BootstrapPersistedOperationKind.None;
            retryCount = 0;
        }
    }
}
