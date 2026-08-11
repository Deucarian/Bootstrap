using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    internal interface IBootstrapOperationStore
    {
        BootstrapOperationState Load();

        void Save(BootstrapOperationState state);

        void Clear();
    }

    internal sealed class BootstrapSessionOperationStore : IBootstrapOperationStore
    {
        internal const string OperationStateKey = "Deucarian.Bootstrap.OperationState.v2";

        private const string LegacyActiveKey = "Deucarian.Bootstrap.Active";
        private const string LegacyStatusKey = "Deucarian.Bootstrap.Status";
        private const string LegacyErrorKey = "Deucarian.Bootstrap.Error";
        private const string LegacyChannelKey = "Deucarian.Bootstrap.Channel";
        private const string LegacyPlanKey = "Deucarian.Bootstrap.Plan";
        private const string LegacyCompletedKey = "Deucarian.Bootstrap.CompletedPlanPackageIds";
        private const string LegacyPendingPackageKey = "Deucarian.Bootstrap.PendingPackageId";
        private const string LegacyWaitingKey = "Deucarian.Bootstrap.WaitingForPackageRefresh";
        private const string LegacyRetryKey = "Deucarian.Bootstrap.PackageListRetryCount";
        private const string LegacyInterruptedKey = "Deucarian.Bootstrap.Interrupted";
        private const char LegacySeparator = '|';

        public BootstrapOperationState Load()
        {
            string json = SessionState.GetString(OperationStateKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    BootstrapOperationState state = JsonUtility.FromJson<BootstrapOperationState>(json);
                    if (state != null && state.SchemaVersion == 2)
                    {
                        return state;
                    }
                }
                catch (ArgumentException)
                {
                }
            }

            return LoadLegacyState();
        }

        public void Save(BootstrapOperationState state)
        {
            BootstrapOperationState safeState = state ?? new BootstrapOperationState();
            SessionState.SetString(OperationStateKey, JsonUtility.ToJson(safeState));
            SessionState.SetBool(LegacyActiveKey, false);
        }

        public void Clear()
        {
            SessionState.SetString(OperationStateKey, string.Empty);
            SessionState.SetBool(LegacyActiveKey, false);
            SessionState.SetBool(LegacyInterruptedKey, false);
        }

        private static BootstrapOperationState LoadLegacyState()
        {
            bool active = SessionState.GetBool(LegacyActiveKey, false);
            bool interrupted = SessionState.GetBool(LegacyInterruptedKey, false);
            if (!active && !interrupted)
            {
                return new BootstrapOperationState();
            }

            BootstrapChannel channel = SessionState.GetInt(LegacyChannelKey, 0) == 1
                ? BootstrapChannel.Development
                : BootstrapChannel.Stable;
            string[] planIds = Split(SessionState.GetString(LegacyPlanKey, string.Empty));
            List<BootstrapPackageStep> steps = planIds
                .Select(packageId => new BootstrapPackageStep(packageId, packageId, string.Empty))
                .ToList();
            BootstrapOperationState state = active
                ? BootstrapOperationState.CreateActive(channel, steps)
                : new BootstrapOperationState { Channel = channel };

            state.SetCompleted(Split(SessionState.GetString(LegacyCompletedKey, string.Empty)));
            state.Status = SessionState.GetString(LegacyStatusKey, string.Empty);
            state.Error = SessionState.GetString(LegacyErrorKey, string.Empty);
            state.RetryCount = SessionState.GetInt(LegacyRetryKey, 0);

            string pendingPackageId = SessionState.GetString(LegacyPendingPackageKey, string.Empty);
            bool waiting = SessionState.GetBool(LegacyWaitingKey, false);
            if (active && waiting && !string.IsNullOrWhiteSpace(pendingPackageId))
            {
                BootstrapPackageStep pending = steps.FirstOrDefault(step => string.Equals(
                    step.PackageId,
                    pendingPackageId,
                    StringComparison.OrdinalIgnoreCase));
                state.SetPending(
                    pending ?? new BootstrapPackageStep(pendingPackageId, pendingPackageId, string.Empty),
                    BootstrapPersistedOperationKind.List);
            }

            return state;
        }

        private static string[] Split(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { LegacySeparator }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
