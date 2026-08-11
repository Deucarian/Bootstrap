using System;
using System.Collections.Generic;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed partial class BootstrapSetupCoordinator
    {
        private const int MaxPackageListRetries = 90;
        private const double PackageListRetryDelaySeconds = 1d;

        private void ContinueOperation(BootstrapHealthReport health)
        {
            if (!_operation.Active || _operation.Verifying)
            {
                return;
            }

            IReadOnlyList<BootstrapPackageStep> plan = _operation.GetPlan();
            if (_operation.PendingKind != BootstrapPersistedOperationKind.None)
            {
                ResumePendingOperation(plan);
                return;
            }

            HashSet<string> completed = new HashSet<string>(
                _operation.CompletedPackageIds,
                StringComparer.OrdinalIgnoreCase);
            int nextIndex = BootstrapSetupPolicy.FindNextStep(
                plan,
                completed,
                health,
                !string.IsNullOrWhiteSpace(_targetRevision));

            if (nextIndex >= plan.Count)
            {
                BeginVerification();
                return;
            }

            StartStep(plan[nextIndex]);
        }

        private void ResumePendingOperation(IReadOnlyList<BootstrapPackageStep> plan)
        {
            int index = BootstrapSetupPolicy.FindStepIndex(plan, _operation.PendingPackageId);
            if (index < 0)
            {
                FailOperation(
                    "Saved setup progress cannot be resumed safely.",
                    "The pending package is not present in the authoritative setup plan.");
                return;
            }

            BootstrapPackageStep step = plan[index];
            BootstrapInstalledPackageInfo installed = _installedState.Get(step.PackageId);

            switch (_operation.PendingKind)
            {
                case BootstrapPersistedOperationKind.Add:
                    if (BootstrapSetupPolicy.IsResolvedForStep(installed, step))
                    {
                        CompletePendingStep(step);
                    }
                    else if (BootstrapSetupPolicy.ShouldRemoveBeforeAdd(step, installed))
                    {
                        _operation.SetPending(step, BootstrapPersistedOperationKind.Remove);
                        _operationStore.Save(_operation);
                        StartRemove(step);
                    }
                    else
                    {
                        StartAdd(step);
                    }

                    break;

                case BootstrapPersistedOperationKind.Remove:
                    if (installed == null)
                    {
                        _operation.SetPending(step, BootstrapPersistedOperationKind.Add);
                        _operationStore.Save(_operation);
                        StartAdd(step);
                    }
                    else
                    {
                        StartRemove(step);
                    }

                    break;

                case BootstrapPersistedOperationKind.List:
                    if (BootstrapSetupPolicy.IsResolvedForStep(installed, step))
                    {
                        CompletePendingStep(step);
                    }
                    else
                    {
                        SchedulePackageListRetry(step);
                    }

                    break;

                default:
                    FailOperation("Saved setup progress is invalid.", "The pending operation kind is unknown.");
                    break;
            }
        }

        private void StartStep(BootstrapPackageStep step)
        {
            if (step == null)
            {
                FailOperation("Setup plan is invalid.", "A setup step was empty.");
                return;
            }

            BootstrapInstalledPackageInfo installed = _installedState.Get(step.PackageId);
            if (BootstrapSetupPolicy.ShouldRemoveBeforeAdd(step, installed))
            {
                _operation.SetPending(step, BootstrapPersistedOperationKind.Remove);
                _operation.Status = "Migrating Package Installer to the selected Git channel...";
                _operationStore.Save(_operation);
                StartRemove(step);
                return;
            }

            _operation.SetPending(step, BootstrapPersistedOperationKind.Add);
            _operation.Status = "Installing " + step.DisplayName + "...";
            _operationStore.Save(_operation);
            StartAdd(step);
        }

        private void StartAdd(BootstrapPackageStep step)
        {
            if (_operationRequest != null || _listRequest != null)
            {
                return;
            }

            _operation.SetPending(step, BootstrapPersistedOperationKind.Add);
            _operation.Status = "Installing " + step.DisplayName + " from the selected Git channel...";
            _operation.Error = string.Empty;
            _operationStore.Save(_operation);
            Publish(BootstrapSetupPhase.Installing, _operation.Status, string.Empty);

            try
            {
                _operationRequest = _packageManager.Add(step.PackageReference);
                if (_operationRequest == null)
                {
                    throw new InvalidOperationException(
                        "Unity Package Manager did not start an add request.");
                }
            }
            catch (Exception exception)
            {
                FailOperation(
                    "Unity Package Manager add failed to start for " + step.DisplayName + ".",
                    exception.GetBaseException().Message);
            }
        }

        private void StartRemove(BootstrapPackageStep step)
        {
            if (_operationRequest != null || _listRequest != null)
            {
                return;
            }

            _operation.SetPending(step, BootstrapPersistedOperationKind.Remove);
            _operation.Status = "Removing the legacy Package Installer source before migration...";
            _operation.Error = string.Empty;
            _operationStore.Save(_operation);
            Publish(BootstrapSetupPhase.Installing, _operation.Status, string.Empty);

            try
            {
                _operationRequest = _packageManager.Remove(step.PackageId);
                if (_operationRequest == null)
                {
                    throw new InvalidOperationException(
                        "Unity Package Manager did not start a remove request.");
                }
            }
            catch (Exception exception)
            {
                FailOperation(
                    "Unity Package Manager remove failed to start for " + step.DisplayName + ".",
                    exception.GetBaseException().Message);
            }
        }

        private void PollOperationRequest()
        {
            if (_operationRequest == null || !_operationRequest.IsCompleted)
            {
                return;
            }

            IBootstrapPackageManagerRequest request = _operationRequest;
            _operationRequest = null;
            BootstrapPersistedOperationKind completedKind = _operation.PendingKind;

            if (!request.Succeeded)
            {
                string error = request.ErrorMessage;
                request.Dispose();
                FailOperation(
                    completedKind == BootstrapPersistedOperationKind.Remove
                        ? "Package Installer migration failed during removal."
                        : "Package installation failed.",
                    error);
                return;
            }

            request.Dispose();
            BootstrapPackageStep step = GetPendingStep();
            if (step == null)
            {
                FailOperation(
                    "Setup progress was lost.",
                    "Unity completed an operation, but its authoritative setup step is missing.");
                return;
            }

            if (completedKind == BootstrapPersistedOperationKind.Remove)
            {
                _operation.SetPending(step, BootstrapPersistedOperationKind.Add);
                _operation.Status = "Legacy source removed. Installing Package Installer from Git...";
                _operationStore.Save(_operation);
                StartAdd(step);
                return;
            }

            if (completedKind != BootstrapPersistedOperationKind.Add)
            {
                FailOperation("Setup progress is invalid.", "An unexpected Package Manager operation completed.");
                return;
            }

            _operation.SetPending(step, BootstrapPersistedOperationKind.List);
            _operation.RetryCount = 0;
            _operation.Status = "Waiting for Unity to resolve " + step.DisplayName + "...";
            _operationStore.Save(_operation);
            Publish(BootstrapSetupPhase.WaitingForUnity, _operation.Status, string.Empty);
            StartPackageList(true);
        }

        private void HandleOperationList()
        {
            if (!_operation.Active)
            {
                return;
            }

            BootstrapPackageStep step = GetPendingStep();
            if (step == null)
            {
                FailOperation(
                    "Setup progress was lost.",
                    "The pending package is not present in the saved setup plan.");
                return;
            }

            BootstrapInstalledPackageInfo installed = _installedState.Get(step.PackageId);
            if (BootstrapSetupPolicy.IsResolvedForStep(installed, step))
            {
                CompletePendingStep(step);
                return;
            }

            SchedulePackageListRetry(step);
        }

        private void SchedulePackageListRetry(BootstrapPackageStep step)
        {
            if (_operation.RetryCount >= MaxPackageListRetries)
            {
                FailOperation(
                    "Setup stopped while waiting for Unity package resolution.",
                    "Unity did not resolve " + step.DisplayName + " from the selected Git reference after " +
                    MaxPackageListRetries + " checks. Retry after Unity finishes package processing.");
                return;
            }

            _operation.SetPending(step, BootstrapPersistedOperationKind.List);
            _operation.RetryCount++;
            _operation.Status = "Waiting for Unity to resolve " + step.DisplayName +
                " (check " + _operation.RetryCount + "/" + MaxPackageListRetries + ")...";
            _operationStore.Save(_operation);
            _listRetryScheduled = true;
            _nextListRetryTime = _clock.Now + PackageListRetryDelaySeconds;
            Publish(BootstrapSetupPhase.WaitingForUnity, _operation.Status, string.Empty);
        }

        private void CompletePendingStep(BootstrapPackageStep step)
        {
            _operation.MarkCompleted(step.PackageId);
            _operation.ClearPending();
            _operation.Status = step.DisplayName + " completed.";
            _operationStore.Save(_operation);
            _health = BootstrapSetupPolicy.Evaluate(
                _channel,
                _installedState,
                _targetGitUrl,
                _targetRevision);
            Publish(BootstrapSetupPhase.Installing, _operation.Status, string.Empty);
            ContinueOperation(_health);
        }

        private void BeginVerification()
        {
            _operation.Verifying = true;
            _operation.ClearPending();
            _operation.Status = "Verifying Package Installer source, channel, and revision...";
            _operationStore.Save(_operation);
            BeginDetection(_operation.Status);
        }

        private void FinalizeVerification(BootstrapHealthReport report)
        {
            if (report != null && report.IsHealthy)
            {
                _operationStore.Clear();
                _operation = new BootstrapOperationState();
                _health = report;
                Publish(BootstrapSetupPhase.Healthy, "Setup completed and verified.", string.Empty);
                return;
            }

            if (report != null && report.RecommendedAction == BootstrapSetupAction.Refresh)
            {
                _operationStore.Clear();
                _operation = new BootstrapOperationState();
                _health = report;
                Publish(
                    BootstrapSetupPhase.ReviewRequired,
                    "Setup completed, but the Package Installer revision could not be verified.",
                    string.Empty);
                return;
            }

            FailOperation(
                "Setup verification failed.",
                "Package Installer did not resolve to the selected Git source, channel, and revision.");
        }

        private void FailOperation(string status, string error)
        {
            _detecting = false;
            _listRetryScheduled = false;
            _operationRequest?.Dispose();
            _operationRequest = null;
            _listRequest?.Dispose();
            _listRequest = null;
            _operation.Active = false;
            _operation.Verifying = false;
            _operation.Status = status ?? "Setup failed.";
            _operation.Error = error ?? string.Empty;
            _operationStore.Save(_operation);
            Publish(BootstrapSetupPhase.Failed, _operation.Status, _operation.Error);
        }

        private BootstrapPackageStep GetPendingStep()
        {
            IReadOnlyList<BootstrapPackageStep> plan = _operation.GetPlan();
            int index = BootstrapSetupPolicy.FindStepIndex(plan, _operation.PendingPackageId);
            return index >= 0 ? plan[index] : null;
        }
    }
}
