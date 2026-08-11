using System;
using System.Collections.Generic;
using System.Linq;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed partial class BootstrapSetupCoordinator : IDisposable
    {
        private readonly IBootstrapCatalogLoader _catalogLoader;
        private readonly IBootstrapPackageManager _packageManager;
        private readonly IBootstrapInstalledStateInspector _installedStateInspector;
        private readonly IBootstrapRevisionResolver _revisionResolver;
        private readonly IBootstrapOperationStore _operationStore;
        private readonly IBootstrapChannelStore _channelStore;
        private readonly IBootstrapLegacyRegistryInspector _legacyRegistryInspector;
        private readonly IBootstrapClock _clock;

        private BootstrapChannel _channel;
        private BootstrapCatalogSelection _catalogSelection;
        private BootstrapInstalledState _installedState = BootstrapInstalledState.Empty;
        private BootstrapHealthReport _health;
        private BootstrapOperationState _operation = new BootstrapOperationState();
        private BootstrapScopedRegistryStatus _legacyRegistryStatus =
            BootstrapScopedRegistryStatus.NotInspected;
        private IBootstrapPackageManagerRequest _listRequest;
        private IBootstrapPackageManagerRequest _operationRequest;
        private IBootstrapRevisionRequest _revisionRequest;
        private bool _detecting;
        private bool _catalogReady;
        private bool _listReady;
        private bool _revisionReady;
        private bool _listForOperation;
        private bool _listRetryScheduled;
        private double _nextListRetryTime;
        private string _targetGitUrl = string.Empty;
        private string _targetRevision = string.Empty;
        private string _revisionNotice = string.Empty;

        public BootstrapSetupCoordinator(
            IBootstrapCatalogLoader catalogLoader,
            IBootstrapPackageManager packageManager,
            IBootstrapInstalledStateInspector installedStateInspector,
            IBootstrapRevisionResolver revisionResolver,
            IBootstrapOperationStore operationStore,
            IBootstrapChannelStore channelStore,
            IBootstrapLegacyRegistryInspector legacyRegistryInspector,
            IBootstrapClock clock)
        {
            _catalogLoader = catalogLoader ?? throw new ArgumentNullException(nameof(catalogLoader));
            _packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
            _installedStateInspector = installedStateInspector ?? throw new ArgumentNullException(nameof(installedStateInspector));
            _revisionResolver = revisionResolver ?? throw new ArgumentNullException(nameof(revisionResolver));
            _operationStore = operationStore ?? throw new ArgumentNullException(nameof(operationStore));
            _channelStore = channelStore ?? throw new ArgumentNullException(nameof(channelStore));
            _legacyRegistryInspector = legacyRegistryInspector ?? throw new ArgumentNullException(nameof(legacyRegistryInspector));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Snapshot = BootstrapSetupSnapshot.Loading(BootstrapChannel.Stable, "Waiting to inspect setup...");
        }

        public event Action Changed;

        public BootstrapSetupSnapshot Snapshot { get; private set; }

        internal bool HasLivePackageManagerRequest => _listRequest != null || _operationRequest != null;

        public void Initialize()
        {
            _operation = _operationStore.Load() ?? new BootstrapOperationState();
            _channel = _operation.Active ? _operation.Channel : _channelStore.Get();
            BeginDetection(_operation.Active
                ? _operation.Verifying
                    ? "Verifying setup after reload..."
                    : "Resuming setup from saved progress..."
                : "Checking the Deucarian setup...");
        }

        public bool SelectChannel(BootstrapChannel channel)
        {
            BootstrapChannel safeChannel = channel == BootstrapChannel.Development
                ? BootstrapChannel.Development
                : BootstrapChannel.Stable;
            if (Snapshot.IsBusy || _operation.Active)
            {
                return false;
            }

            _channelStore.Set(safeChannel);
            _channel = safeChannel;
            _operationStore.Clear();
            _operation = new BootstrapOperationState();
            BeginDetection("Checking the " + BootstrapChannelUtility.GetDisplayName(_channel) + " channel...");
            return true;
        }

        public bool SynchronizeChannel()
        {
            if (Snapshot.IsBusy || _operation.Active)
            {
                return false;
            }

            BootstrapChannel persisted = _channelStore.Get();
            if (persisted == _channel)
            {
                return false;
            }

            _channel = persisted;
            _operationStore.Clear();
            _operation = new BootstrapOperationState();
            BeginDetection(
                "Checking the " + BootstrapChannelUtility.GetDisplayName(_channel) + " channel...");
            return true;
        }

        public bool Refresh()
        {
            if (Snapshot.IsBusy || _operation.Active)
            {
                return false;
            }

            _channel = _channelStore.Get();
            _operationStore.Clear();
            _operation = new BootstrapOperationState();
            BeginDetection("Refreshing setup status...");
            return true;
        }

        public bool BeginSetup()
        {
            BootstrapSetupAction action = Snapshot.Health.RecommendedAction;
            if (Snapshot.Phase != BootstrapSetupPhase.Review ||
                _catalogSelection == null ||
                !_catalogSelection.Success ||
                (action != BootstrapSetupAction.Install &&
                 action != BootstrapSetupAction.Repair &&
                 action != BootstrapSetupAction.SwitchChannel &&
                 action != BootstrapSetupAction.Migrate))
            {
                return false;
            }

            try
            {
                // The explicit reviewed action makes this project selection newer than
                // any Package Installer package-specific override before handoff.
                _channelStore.Set(_channel);
            }
            catch (Exception exception)
            {
                Publish(
                    BootstrapSetupPhase.Failed,
                    "The selected package-management channel could not be saved.",
                    exception.GetBaseException().Message);
                return false;
            }

            _operation = BootstrapOperationState.CreateActive(_channel, _catalogSelection.Plan.Steps);
            _operation.Status = "Starting the reviewed setup plan...";
            _operationStore.Save(_operation);
            Publish(BootstrapSetupPhase.Installing, _operation.Status, string.Empty);
            ContinueOperation(_health);
            return true;
        }

        public void Tick()
        {
            if (_detecting)
            {
                _catalogLoader.Tick();
                PollCatalog();
                PollRevision();
            }

            PollPackageList();
            PollOperationRequest();

            if (_listRetryScheduled && _clock.Now >= _nextListRetryTime)
            {
                _listRetryScheduled = false;
                StartPackageList(true);
            }

            TryFinishDetection();
        }

        public void Dispose()
        {
            DisposeTransientRequests();
            _catalogLoader.Dispose();
        }

        private void BeginDetection(string status)
        {
            DisposeTransientRequests();
            _detecting = true;
            _catalogReady = false;
            _listReady = false;
            _revisionReady = false;
            _listForOperation = false;
            _listRetryScheduled = false;
            _catalogSelection = null;
            _installedState = BootstrapInstalledState.Empty;
            _health = null;
            _targetGitUrl = BootstrapChannelUtility.GetPackageInstallerGitUrl(_channel);
            _targetRevision = string.Empty;
            _revisionNotice = string.Empty;

            try
            {
                _legacyRegistryStatus = _legacyRegistryInspector.Inspect() ??
                    BootstrapScopedRegistryStatus.NotInspected;
            }
            catch (Exception exception)
            {
                _legacyRegistryStatus = BootstrapScopedRegistryStatus.CreateError(
                    string.Empty,
                    exception.GetBaseException().Message);
            }

            Publish(
                _operation.Active && _operation.Verifying
                    ? BootstrapSetupPhase.Verifying
                    : BootstrapSetupPhase.Loading,
                status,
                string.Empty);

            try
            {
                _catalogLoader.Begin(_channel);
            }
            catch (Exception exception)
            {
                FailDetection("Catalog loading could not start.", exception.GetBaseException().Message);
                return;
            }

            StartPackageList(false);
        }

        private void PollCatalog()
        {
            if (!_detecting || _catalogReady || !_catalogLoader.IsCompleted)
            {
                return;
            }

            _catalogSelection = _catalogLoader.Selection;
            if (_catalogSelection == null || !_catalogSelection.Success)
            {
                FailDetection(
                    "Package Registry metadata could not be loaded.",
                    _catalogSelection != null
                        ? _catalogSelection.ErrorMessage
                        : "Catalog loader returned no result.");
                return;
            }

            _catalogReady = true;
            BootstrapPackageStep packageInstaller = _catalogSelection.Plan.Steps.LastOrDefault();
            _targetGitUrl = packageInstaller != null
                ? packageInstaller.PackageReference
                : BootstrapChannelUtility.GetPackageInstallerGitUrl(_channel);

            try
            {
                _revisionRequest = _revisionResolver.Resolve(
                    _targetGitUrl,
                    BootstrapChannelUtility.GetGitBranch(_channel));
                if (_revisionRequest == null)
                {
                    _revisionReady = true;
                    _revisionNotice = "Target revision could not be verified. " +
                        "The revision resolver did not start a request.";
                }
            }
            catch (Exception exception)
            {
                _revisionReady = true;
                _revisionNotice = "Target revision could not be resolved: " +
                    exception.GetBaseException().Message;
            }
        }

        private void PollRevision()
        {
            if (!_detecting || _revisionReady || _revisionRequest == null || !_revisionRequest.IsCompleted)
            {
                return;
            }

            BootstrapRevisionResult result = _revisionRequest.Result;
            _revisionRequest.Dispose();
            _revisionRequest = null;
            _revisionReady = true;

            if (result != null && result.Success)
            {
                _targetRevision = result.Revision;
                return;
            }

            _targetRevision = string.Empty;
            _revisionNotice = "Target revision could not be verified. " +
                (result != null ? result.ErrorMessage : "Revision resolver returned no result.");
        }

        private void StartPackageList(bool forOperation)
        {
            if (_listRequest != null || _operationRequest != null)
            {
                return;
            }

            _listForOperation = forOperation;
            try
            {
                _listRequest = _packageManager.List();
                if (_listRequest == null)
                {
                    throw new InvalidOperationException(
                        "Unity Package Manager did not start a list request.");
                }
            }
            catch (Exception exception)
            {
                if (_operation.Active)
                {
                    FailOperation("Unity Package Manager list failed to start.", exception.GetBaseException().Message);
                }
                else
                {
                    FailDetection("Installed packages could not be inspected.", exception.GetBaseException().Message);
                }
            }
        }

        private void PollPackageList()
        {
            if (_listRequest == null || !_listRequest.IsCompleted)
            {
                return;
            }

            IBootstrapPackageManagerRequest request = _listRequest;
            bool forOperation = _listForOperation;
            _listRequest = null;
            _listForOperation = false;

            if (!request.Succeeded)
            {
                string error = request.ErrorMessage;
                request.Dispose();
                if (_operation.Active)
                {
                    FailOperation("Unity Package Manager list failed.", error);
                }
                else
                {
                    FailDetection("Installed packages could not be inspected.", error);
                }

                return;
            }

            _installedState = _installedStateInspector.Inspect(request.Packages);
            request.Dispose();

            if (forOperation)
            {
                HandleOperationList();
                return;
            }

            _listReady = true;
        }

        private void TryFinishDetection()
        {
            if (!_detecting || !_catalogReady || !_listReady || !_revisionReady)
            {
                return;
            }

            _detecting = false;
            _health = BootstrapSetupPolicy.Evaluate(
                _channel,
                _installedState,
                _targetGitUrl,
                _targetRevision);

            if (_operation.Active)
            {
                if (!PreparePersistedPlan())
                {
                    return;
                }

                if (_operation.Verifying)
                {
                    FinalizeVerification(_health);
                    return;
                }

                Publish(BootstrapSetupPhase.Installing, "Continuing the saved setup plan...", string.Empty);
                ContinueOperation(_health);
                return;
            }

            if (_health.IsHealthy)
            {
                _operationStore.Clear();
                _operation = new BootstrapOperationState();
                Publish(BootstrapSetupPhase.Healthy, "Setup is healthy.", string.Empty);
            }
            else if (_operation.HasFailure)
            {
                Publish(BootstrapSetupPhase.Failed, _operation.Status, _operation.Error);
            }
            else if (_health.RecommendedAction == BootstrapSetupAction.Refresh)
            {
                Publish(
                    BootstrapSetupPhase.ReviewRequired,
                    "Package Installer health requires review.",
                    string.Empty);
            }
            else
            {
                Publish(BootstrapSetupPhase.Review, "Review the setup or repair plan.", string.Empty);
            }
        }

        private void FailDetection(string status, string error)
        {
            _detecting = false;
            if (_operation.Active)
            {
                FailOperation(status, error);
                return;
            }

            Publish(BootstrapSetupPhase.Failed, status, error);
        }

        private void DisposeTransientRequests()
        {
            _listRequest?.Dispose();
            _listRequest = null;
            _operationRequest?.Dispose();
            _operationRequest = null;
            _revisionRequest?.Dispose();
            _revisionRequest = null;
        }

    }
}
