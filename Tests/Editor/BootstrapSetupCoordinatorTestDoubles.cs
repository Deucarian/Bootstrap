using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor.Tests
{
    internal sealed class BootstrapCoordinatorTestEnvironment
    {
        public const string TargetRevision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        public const string PreviousRevision = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        public BootstrapCoordinatorTestEnvironment()
        {
            PackageState = new BootstrapCoordinatorPackageState
            {
                PackageInstallerRevision = TargetRevision
            };
            CatalogLoader = new BootstrapCoordinatorCatalogLoader(CreateCatalogSelection);
            PackageManager = new BootstrapCoordinatorPackageManager(PackageState);
            InstalledStateInspector = new BootstrapInstalledStateInspector(PackageState);
            RevisionResolver = new BootstrapCoordinatorRevisionResolver(
                () => BootstrapRevisionResult.CreateSuccess(TargetRevision));
            OperationStore = new BootstrapCoordinatorOperationStore();
            ChannelStore = new BootstrapCoordinatorChannelStore(BootstrapChannel.Stable);
            LegacyRegistryInspector = new BootstrapCoordinatorLegacyRegistryInspector();
            Clock = new BootstrapCoordinatorClock();
        }

        public BootstrapCoordinatorPackageState PackageState { get; }

        public BootstrapCoordinatorCatalogLoader CatalogLoader { get; }

        public BootstrapCoordinatorPackageManager PackageManager { get; }

        public IBootstrapInstalledStateInspector InstalledStateInspector { get; }

        public BootstrapCoordinatorRevisionResolver RevisionResolver { get; }

        public BootstrapCoordinatorOperationStore OperationStore { get; }

        public BootstrapCoordinatorChannelStore ChannelStore { get; }

        public BootstrapCoordinatorLegacyRegistryInspector LegacyRegistryInspector { get; }

        public BootstrapCoordinatorClock Clock { get; }

        public BootstrapCatalogOrigin CatalogOrigin { get; set; } = BootstrapCatalogOrigin.Remote;

        public string CatalogNotice { get; set; } = string.Empty;

        public BootstrapSetupCoordinator CreateCoordinator()
        {
            return new BootstrapSetupCoordinator(
                CatalogLoader,
                PackageManager,
                InstalledStateInspector,
                RevisionResolver,
                OperationStore,
                ChannelStore,
                LegacyRegistryInspector,
                Clock);
        }

        public BootstrapSetupCoordinator InitializeAndDetect()
        {
            BootstrapSetupCoordinator coordinator = CreateCoordinator();
            coordinator.Initialize();
            coordinator.Tick();
            return coordinator;
        }

        public BootstrapCatalogSelection CreateCatalogSelection(BootstrapChannel channel)
        {
            IReadOnlyList<BootstrapPackageStep> plan = CreatePlan(channel);
            return BootstrapCatalogSelection.CreateSuccess(
                new BootstrapPackageCatalog
                {
                    schemaVersion = 2,
                    groups = Array.Empty<BootstrapPackageGroup>(),
                    packages = Array.Empty<BootstrapPackageDefinition>()
                },
                BootstrapInstallPlanResult.CreateSuccess(plan),
                CatalogOrigin,
                CatalogOrigin == BootstrapCatalogOrigin.BundledFallback
                    ? "Bundled setup fallback"
                    : "Remote Package Registry",
                CatalogNotice);
        }

        public static IReadOnlyList<BootstrapPackageStep> CreatePlan(BootstrapChannel channel)
        {
            return new[]
            {
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.EditorPackageDisplayName,
                    GetEditorReference(channel)),
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageDisplayName,
                    GetLoggingReference(channel)),
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageDisplayName,
                    BootstrapChannelUtility.GetPackageInstallerGitUrl(channel))
            };
        }

        public static string GetEditorReference(BootstrapChannel channel)
        {
            return "https://github.com/Deucarian/Editor.git#" +
                BootstrapChannelUtility.GetGitBranch(channel);
        }

        public static string GetLoggingReference(BootstrapChannel channel)
        {
            return "https://github.com/Deucarian/Logging.git#" +
                BootstrapChannelUtility.GetGitBranch(channel);
        }

        public void InstallHealthy(BootstrapChannel channel = BootstrapChannel.Stable)
        {
            PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.EditorPackageId,
                GetEditorReference(channel),
                PreviousRevision);
            PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.LoggingPackageId,
                GetLoggingReference(channel),
                PreviousRevision);
            PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                BootstrapChannelUtility.GetPackageInstallerGitUrl(channel),
                TargetRevision);
        }

        public void CompleteLatestAddAndAdvance(BootstrapSetupCoordinator coordinator)
        {
            PackageManager.LastAddRequest.CompleteSuccess();
            coordinator.Tick();
            coordinator.Tick();
        }

        public void FinishSetup(BootstrapSetupCoordinator coordinator, int maximumTicks = 80)
        {
            for (int tick = 0; tick < maximumTicks; tick++)
            {
                if (coordinator.Snapshot.Phase == BootstrapSetupPhase.Healthy ||
                    coordinator.Snapshot.Phase == BootstrapSetupPhase.ReviewRequired ||
                    coordinator.Snapshot.Phase == BootstrapSetupPhase.Failed)
                {
                    return;
                }

                BootstrapCoordinatorPackageManagerRequest add = PackageManager.LastAddRequest;
                if (add != null && !add.IsCompleted)
                {
                    add.CompleteSuccess();
                }

                BootstrapCoordinatorPackageManagerRequest remove = PackageManager.LastRemoveRequest;
                if (remove != null && !remove.IsCompleted)
                {
                    remove.CompleteSuccess();
                }

                BootstrapCoordinatorPackageManagerRequest list = PackageManager.LastListRequest;
                if (list != null && !list.IsCompleted)
                {
                    PackageManager.CompleteLatestListSuccess();
                }

                coordinator.Tick();
                Clock.Advance(1d);
            }

            AssertTerminalPhase(coordinator);
        }

        private static void AssertTerminalPhase(BootstrapSetupCoordinator coordinator)
        {
            throw new InvalidOperationException(
                "Coordinator did not reach a terminal phase. Current phase: " +
                coordinator.Snapshot.Phase + ". Status: " + coordinator.Snapshot.Status);
        }
    }

    internal sealed class BootstrapCoordinatorPackageState : IBootstrapPackageLockReader
    {
        private readonly Dictionary<string, BootstrapPackageRecord> _records =
            new Dictionary<string, BootstrapPackageRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BootstrapPackageLockEntry> _lockEntries =
            new Dictionary<string, BootstrapPackageLockEntry>(StringComparer.OrdinalIgnoreCase);

        public string PackageInstallerRevision { get; set; } =
            BootstrapCoordinatorTestEnvironment.TargetRevision;

        public IReadOnlyList<BootstrapPackageRecord> Records => _records.Values
            .OrderBy(record => record.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        public BootstrapPackageLockEntry GetPackage(string packageId)
        {
            return packageId != null && _lockEntries.TryGetValue(packageId, out BootstrapPackageLockEntry entry)
                ? entry
                : null;
        }

        public bool Contains(string packageId)
        {
            return !string.IsNullOrWhiteSpace(packageId) && _records.ContainsKey(packageId);
        }

        public void InstallGit(string packageId, string packageReference, string revision)
        {
            _records[packageId] = new BootstrapPackageRecord(
                packageId,
                "1.0.0",
                "Git",
                packageId + "@" + packageReference);
            _lockEntries[packageId] = new BootstrapPackageLockEntry(
                packageId,
                "git",
                packageReference,
                revision);
        }

        public void InstallRegistryPackageInstaller()
        {
            string packageId = DeucarianBootstrapPackageConstants.PackageInstallerPackageId;
            _records[packageId] = new BootstrapPackageRecord(
                packageId,
                "1.0.0",
                "Registry",
                packageId + "@1.0.0");
            _lockEntries[packageId] = new BootstrapPackageLockEntry(
                packageId,
                "registry",
                "1.0.0",
                string.Empty);
        }

        public void InstallFromReference(string packageReference)
        {
            BootstrapPackageStep step = BootstrapCoordinatorTestEnvironment
                .CreatePlan(packageReference != null && packageReference.EndsWith("#develop", StringComparison.OrdinalIgnoreCase)
                    ? BootstrapChannel.Development
                    : BootstrapChannel.Stable)
                .FirstOrDefault(candidate => string.Equals(
                    candidate.PackageReference,
                    packageReference,
                    StringComparison.OrdinalIgnoreCase));

            if (step == null)
            {
                throw new InvalidOperationException("Unknown setup reference: " + packageReference);
            }

            string revision = string.Equals(
                step.PackageId,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                StringComparison.OrdinalIgnoreCase)
                ? PackageInstallerRevision
                : BootstrapCoordinatorTestEnvironment.PreviousRevision;
            InstallGit(step.PackageId, step.PackageReference, revision);
        }

        public void Remove(string packageId)
        {
            _records.Remove(packageId);
            _lockEntries.Remove(packageId);
        }
    }

    internal sealed class BootstrapCoordinatorCatalogLoader : IBootstrapCatalogLoader
    {
        private readonly Func<BootstrapChannel, BootstrapCatalogSelection> _selectionFactory;

        public BootstrapCoordinatorCatalogLoader(
            Func<BootstrapChannel, BootstrapCatalogSelection> selectionFactory)
        {
            _selectionFactory = selectionFactory;
        }

        public bool IsCompleted { get; private set; }

        public BootstrapCatalogSelection Selection { get; private set; }

        public int BeginCount { get; private set; }

        public int TickCount { get; private set; }

        public int DisposeCount { get; private set; }

        public BootstrapChannel LastChannel { get; private set; }

        public bool CompleteOnBegin { get; set; } = true;

        public void Begin(BootstrapChannel channel)
        {
            BeginCount++;
            LastChannel = channel;
            Selection = _selectionFactory(channel);
            IsCompleted = CompleteOnBegin;
        }

        public void Complete()
        {
            IsCompleted = true;
        }

        public void Tick()
        {
            TickCount++;
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    internal sealed class BootstrapCoordinatorPackageManager : IBootstrapPackageManager
    {
        private readonly BootstrapCoordinatorPackageState _state;

        public BootstrapCoordinatorPackageManager(BootstrapCoordinatorPackageState state)
        {
            _state = state;
        }

        public bool AutoCompleteLists { get; set; } = true;

        public bool ThrowOnNextList { get; set; }

        public bool ThrowOnNextAdd { get; set; }

        public bool ThrowOnNextRemove { get; set; }

        public List<BootstrapCoordinatorPackageManagerRequest> ListRequests { get; } =
            new List<BootstrapCoordinatorPackageManagerRequest>();

        public List<BootstrapCoordinatorPackageManagerRequest> AddRequests { get; } =
            new List<BootstrapCoordinatorPackageManagerRequest>();

        public List<BootstrapCoordinatorPackageManagerRequest> RemoveRequests { get; } =
            new List<BootstrapCoordinatorPackageManagerRequest>();

        public List<string> AddedReferences { get; } = new List<string>();

        public List<string> RemovedPackageIds { get; } = new List<string>();

        public List<string> OperationLog { get; } = new List<string>();

        public BootstrapCoordinatorPackageManagerRequest LastListRequest => ListRequests.LastOrDefault();

        public BootstrapCoordinatorPackageManagerRequest LastAddRequest => AddRequests.LastOrDefault();

        public BootstrapCoordinatorPackageManagerRequest LastRemoveRequest => RemoveRequests.LastOrDefault();

        public IBootstrapPackageManagerRequest List()
        {
            if (ThrowOnNextList)
            {
                ThrowOnNextList = false;
                throw new InvalidOperationException("list-start-failure");
            }

            BootstrapCoordinatorPackageManagerRequest request =
                new BootstrapCoordinatorPackageManagerRequest();
            ListRequests.Add(request);
            if (AutoCompleteLists)
            {
                request.CompleteSuccess(_state.Records);
            }

            return request;
        }

        public IBootstrapPackageManagerRequest Add(string packageReference)
        {
            if (ThrowOnNextAdd)
            {
                ThrowOnNextAdd = false;
                throw new InvalidOperationException("add-start-failure");
            }

            AddedReferences.Add(packageReference);
            OperationLog.Add("Add:" + packageReference);
            BootstrapCoordinatorPackageManagerRequest request =
                new BootstrapCoordinatorPackageManagerRequest(
                    () => _state.InstallFromReference(packageReference));
            AddRequests.Add(request);
            return request;
        }

        public IBootstrapPackageManagerRequest Remove(string packageId)
        {
            if (ThrowOnNextRemove)
            {
                ThrowOnNextRemove = false;
                throw new InvalidOperationException("remove-start-failure");
            }

            RemovedPackageIds.Add(packageId);
            OperationLog.Add("Remove:" + packageId);
            BootstrapCoordinatorPackageManagerRequest request =
                new BootstrapCoordinatorPackageManagerRequest(() => _state.Remove(packageId));
            RemoveRequests.Add(request);
            return request;
        }

        public void CompleteLatestListSuccess()
        {
            LastListRequest.CompleteSuccess(_state.Records);
        }
    }

    internal sealed class BootstrapCoordinatorPackageManagerRequest : IBootstrapPackageManagerRequest
    {
        private readonly Action _onSuccess;

        public BootstrapCoordinatorPackageManagerRequest(Action onSuccess = null)
        {
            _onSuccess = onSuccess;
            Packages = Array.Empty<BootstrapPackageRecord>();
        }

        public bool IsCompleted { get; private set; }

        public bool Succeeded { get; private set; }

        public string ErrorMessage { get; private set; } = string.Empty;

        public IReadOnlyList<BootstrapPackageRecord> Packages { get; private set; }

        public bool Disposed { get; private set; }

        public int DisposeCount { get; private set; }

        public void CompleteSuccess(IReadOnlyList<BootstrapPackageRecord> packages = null)
        {
            if (IsCompleted)
            {
                return;
            }

            _onSuccess?.Invoke();
            Packages = packages ?? Array.Empty<BootstrapPackageRecord>();
            Succeeded = true;
            IsCompleted = true;
        }

        public void CompleteFailure(string errorMessage)
        {
            if (IsCompleted)
            {
                return;
            }

            ErrorMessage = errorMessage ?? "request-failure";
            Succeeded = false;
            IsCompleted = true;
        }

        public void Dispose()
        {
            Disposed = true;
            DisposeCount++;
        }
    }

    internal sealed class BootstrapCoordinatorRevisionResolver : IBootstrapRevisionResolver
    {
        public BootstrapCoordinatorRevisionResolver(Func<BootstrapRevisionResult> resultFactory)
        {
            ResultFactory = resultFactory;
        }

        public Func<BootstrapRevisionResult> ResultFactory { get; set; }

        public List<BootstrapCoordinatorRevisionRequest> Requests { get; } =
            new List<BootstrapCoordinatorRevisionRequest>();

        public int ResolveCount => Requests.Count;

        public bool CompleteImmediately { get; set; } = true;

        public IBootstrapRevisionRequest Resolve(string gitUrl, string branch)
        {
            BootstrapCoordinatorRevisionRequest request =
                new BootstrapCoordinatorRevisionRequest(ResultFactory(), CompleteImmediately);
            Requests.Add(request);
            return request;
        }
    }

    internal sealed class BootstrapCoordinatorRevisionRequest : IBootstrapRevisionRequest
    {
        public BootstrapCoordinatorRevisionRequest(BootstrapRevisionResult result, bool completed)
        {
            Result = result;
            IsCompleted = completed;
        }

        public bool IsCompleted { get; private set; }

        public BootstrapRevisionResult Result { get; }

        public bool Disposed { get; private set; }

        public void Complete()
        {
            IsCompleted = true;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    internal sealed class BootstrapCoordinatorOperationStore : IBootstrapOperationStore
    {
        private string _json = string.Empty;

        public int LoadCount { get; private set; }

        public int SaveCount { get; private set; }

        public int ClearCount { get; private set; }

        public BootstrapOperationState Load()
        {
            LoadCount++;
            return string.IsNullOrWhiteSpace(_json)
                ? new BootstrapOperationState()
                : JsonUtility.FromJson<BootstrapOperationState>(_json);
        }

        public void Save(BootstrapOperationState state)
        {
            SaveCount++;
            _json = JsonUtility.ToJson(state ?? new BootstrapOperationState());
        }

        public void Clear()
        {
            ClearCount++;
            _json = string.Empty;
        }

        public BootstrapOperationState Peek()
        {
            return string.IsNullOrWhiteSpace(_json)
                ? new BootstrapOperationState()
                : JsonUtility.FromJson<BootstrapOperationState>(_json);
        }
    }

    internal sealed class BootstrapCoordinatorChannelStore : IBootstrapChannelStore
    {
        public BootstrapCoordinatorChannelStore(BootstrapChannel channel)
        {
            Channel = channel;
        }

        public BootstrapChannel Channel { get; set; }

        public int SetCount { get; private set; }

        public BootstrapChannel Get()
        {
            return Channel;
        }

        public void Set(BootstrapChannel channel)
        {
            Channel = channel;
            SetCount++;
        }
    }

    internal sealed class BootstrapCoordinatorLegacyRegistryInspector : IBootstrapLegacyRegistryInspector
    {
        public BootstrapScopedRegistryStatus Status { get; set; } =
            BootstrapScopedRegistryStatus.CreateMissing(
                "Packages/manifest.json",
                "No legacy scoped registry entry is configured.");

        public int InspectCount { get; private set; }

        public BootstrapScopedRegistryStatus Inspect()
        {
            InspectCount++;
            return Status;
        }
    }

    internal sealed class BootstrapCoordinatorClock : IBootstrapClock
    {
        public double Now { get; private set; }

        public void Advance(double seconds)
        {
            Now += Math.Max(0d, seconds);
        }
    }
}
