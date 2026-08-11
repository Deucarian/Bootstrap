using System.Linq;
using NUnit.Framework;

namespace Deucarian.Bootstrap.Editor.Tests
{
    internal sealed class BootstrapSetupCoordinatorOperationTests
    {
        [Test]
        public void CleanFirstInstallationRunsDependencyFirstAndVerifiesHealthy()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.True(coordinator.BeginSetup());
                Assert.AreEqual(1, environment.ChannelStore.SetCount,
                    "The explicit setup action must make the reviewed project channel authoritative.");
                Assert.AreEqual(BootstrapSetupPhase.Installing, coordinator.Snapshot.Phase);
                Assert.AreEqual(DeucarianBootstrapPackageConstants.EditorPackageId,
                    coordinator.Snapshot.PendingPackageId);

                environment.CompleteLatestAddAndAdvance(coordinator);
                Assert.AreEqual(DeucarianBootstrapPackageConstants.LoggingPackageId,
                    coordinator.Snapshot.PendingPackageId);

                environment.CompleteLatestAddAndAdvance(coordinator);
                Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    coordinator.Snapshot.PendingPackageId);

                environment.PackageManager.LastAddRequest.CompleteSuccess();
                environment.FinishSetup(coordinator);

                CollectionAssert.AreEqual(
                    BootstrapCoordinatorTestEnvironment.CreatePlan(BootstrapChannel.Stable)
                        .Select(step => "Add:" + step.PackageReference)
                        .ToArray(),
                    environment.PackageManager.OperationLog);
                Assert.AreEqual(BootstrapSetupPhase.Healthy, coordinator.Snapshot.Phase);
                Assert.True(coordinator.Snapshot.Health.IsHealthy);
                Assert.False(environment.OperationStore.Peek().Active);
                Assert.GreaterOrEqual(environment.OperationStore.SaveCount, 10);
                Assert.Greater(environment.OperationStore.ClearCount, 0);
            }
        }

        [TestCase(DeucarianBootstrapPackageConstants.EditorPackageId, 2)]
        [TestCase(DeucarianBootstrapPackageConstants.LoggingPackageId, 2)]
        [TestCase(DeucarianBootstrapPackageConstants.PackageInstallerPackageId, 3)]
        public void RepairReconcilesDependenciesBeforePackageInstaller(
            string missingPackageId,
            int expectedAddCount)
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            InstallAllExcept(environment, missingPackageId);

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.AreEqual(BootstrapSetupAction.Repair,
                    coordinator.Snapshot.Health.RecommendedAction);
                Assert.True(coordinator.BeginSetup());

                environment.FinishSetup(coordinator);

                Assert.AreEqual(BootstrapSetupPhase.Healthy, coordinator.Snapshot.Phase);
                CollectionAssert.AreEqual(
                    BootstrapCoordinatorTestEnvironment.CreatePlan(BootstrapChannel.Stable)
                        .Take(expectedAddCount)
                        .Select(step => step.PackageReference)
                        .ToArray(),
                    environment.PackageManager.AddedReferences);
                Assert.Zero(environment.PackageManager.RemoveRequests.Count);
            }
        }

        [Test]
        public void WrongChannelRepairAddsSelectedGitReferenceWithoutLegacyRemoval()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            environment.InstallHealthy();
            environment.PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                BootstrapChannelUtility.GetPackageInstallerGitUrl(BootstrapChannel.Development),
                BootstrapCoordinatorTestEnvironment.TargetRevision);

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.AreEqual(BootstrapSetupAction.SwitchChannel,
                    coordinator.Snapshot.Health.RecommendedAction);
                Assert.True(coordinator.BeginSetup());
                Assert.Zero(environment.PackageManager.RemoveRequests.Count);

                environment.FinishSetup(coordinator);

                CollectionAssert.AreEqual(
                    BootstrapCoordinatorTestEnvironment.CreatePlan(BootstrapChannel.Stable)
                        .Select(step => step.PackageReference)
                        .ToArray(),
                    environment.PackageManager.AddedReferences);
                Assert.AreEqual(BootstrapSetupPhase.Healthy, coordinator.Snapshot.Phase);
            }
        }

        [Test]
        public void OutdatedRevisionRepairReinstallsPackageInstallerAndVerifiesLockRevision()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            environment.InstallHealthy();
            environment.PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                BootstrapChannelUtility.GetPackageInstallerGitUrl(BootstrapChannel.Stable),
                BootstrapCoordinatorTestEnvironment.PreviousRevision);

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.AreEqual(BootstrapPackageInstallerSetupState.Outdated,
                    coordinator.Snapshot.Health.PackageInstallerState);
                Assert.True(coordinator.BeginSetup());

                environment.FinishSetup(coordinator);

                Assert.AreEqual(BootstrapSetupPhase.Healthy, coordinator.Snapshot.Phase);
                Assert.AreEqual(3, environment.PackageManager.AddRequests.Count);
                Assert.AreEqual(BootstrapCoordinatorTestEnvironment.TargetRevision,
                    coordinator.Snapshot.InstalledState
                        .Get(DeucarianBootstrapPackageConstants.PackageInstallerPackageId)
                        .LockRevision);
            }
        }

        [Test]
        public void UnverifiableRevisionStillInstallsCleanSetupThenEndsReviewRequired()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            environment.RevisionResolver.ResultFactory =
                () => BootstrapRevisionResult.CreateFailure("offline");

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.True(coordinator.BeginSetup());
                environment.FinishSetup(coordinator);

                Assert.AreEqual(3, environment.PackageManager.AddRequests.Count);
                Assert.AreEqual(BootstrapSetupPhase.ReviewRequired, coordinator.Snapshot.Phase);
                Assert.AreEqual(BootstrapSetupAction.Refresh,
                    coordinator.Snapshot.Health.RecommendedAction);
                Assert.False(environment.OperationStore.Peek().Active);
            }
        }

        [Test]
        public void LegacyRegistryMigrationRemovesBeforeAddingPackageInstallerFromGit()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            environment.PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.EditorPackageId,
                BootstrapCoordinatorTestEnvironment.GetEditorReference(BootstrapChannel.Stable),
                BootstrapCoordinatorTestEnvironment.PreviousRevision);
            environment.PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.LoggingPackageId,
                BootstrapCoordinatorTestEnvironment.GetLoggingReference(BootstrapChannel.Stable),
                BootstrapCoordinatorTestEnvironment.PreviousRevision);
            environment.PackageState.InstallRegistryPackageInstaller();

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.True(coordinator.BeginSetup());
                environment.FinishSetup(coordinator);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "Add:" + BootstrapCoordinatorTestEnvironment.GetEditorReference(BootstrapChannel.Stable),
                        "Add:" + BootstrapCoordinatorTestEnvironment.GetLoggingReference(BootstrapChannel.Stable),
                        "Remove:" + DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                        "Add:" + BootstrapChannelUtility.GetPackageInstallerGitUrl(BootstrapChannel.Stable)
                    },
                    environment.PackageManager.OperationLog);
                Assert.AreEqual(BootstrapSetupPhase.Healthy, coordinator.Snapshot.Phase);
            }
        }

        [Test]
        public void DuplicateBeginSetupIsRejectedWhileOperationIsActive()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.True(coordinator.BeginSetup());
                Assert.False(coordinator.BeginSetup());
                Assert.AreEqual(1, environment.PackageManager.AddRequests.Count);
            }
        }

        [Test]
        public void PackageManagerListFailureStopsDetectionWithoutMutation()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            environment.PackageManager.AutoCompleteLists = false;

            using (BootstrapSetupCoordinator coordinator = environment.CreateCoordinator())
            {
                coordinator.Initialize();
                environment.PackageManager.LastListRequest.CompleteFailure("list-failure");
                coordinator.Tick();

                Assert.AreEqual(BootstrapSetupPhase.Failed, coordinator.Snapshot.Phase);
                StringAssert.Contains("list-failure", coordinator.Snapshot.Error);
                Assert.AreEqual(
                    BootstrapSetupAction.Refresh,
                    BootstrapPresentationModelFactory.Create(coordinator.Snapshot).PrimaryAction);
                Assert.False(coordinator.BeginSetup(),
                    "A failed inspection must never reuse a catalog without a verified package list.");
                Assert.Zero(environment.PackageManager.AddRequests.Count);
                Assert.Zero(environment.PackageManager.RemoveRequests.Count);
            }
        }

        [Test]
        public void PackageManagerAddFailurePersistsInactiveFailure()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.True(coordinator.BeginSetup());
                environment.PackageManager.LastAddRequest.CompleteFailure("add-failure");
                coordinator.Tick();

                Assert.AreEqual(BootstrapSetupPhase.Failed, coordinator.Snapshot.Phase);
                StringAssert.Contains("add-failure", coordinator.Snapshot.Error);
                Assert.False(environment.OperationStore.Peek().Active);
                Assert.AreEqual(
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    environment.OperationStore.Peek().PendingPackageId);
            }
        }

        [Test]
        public void PackageManagerRemoveFailurePersistsInactiveFailure()
        {
            BootstrapCoordinatorTestEnvironment environment = CreateLegacyMigrationEnvironment();

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.True(coordinator.BeginSetup());
                environment.CompleteLatestAddAndAdvance(coordinator);
                environment.CompleteLatestAddAndAdvance(coordinator);
                environment.PackageManager.LastRemoveRequest.CompleteFailure("remove-failure");
                coordinator.Tick();

                Assert.AreEqual(BootstrapSetupPhase.Failed, coordinator.Snapshot.Phase);
                StringAssert.Contains("remove-failure", coordinator.Snapshot.Error);
                Assert.False(environment.OperationStore.Peek().Active);
            }
        }

        [Test]
        public void PostAddPackageListFailureStopsActiveOperation()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.True(coordinator.BeginSetup());
                environment.PackageManager.AutoCompleteLists = false;
                environment.PackageManager.LastAddRequest.CompleteSuccess();
                coordinator.Tick();
                environment.PackageManager.LastListRequest.CompleteFailure("post-add-list-failure");
                coordinator.Tick();

                Assert.AreEqual(BootstrapSetupPhase.Failed, coordinator.Snapshot.Phase);
                StringAssert.Contains("post-add-list-failure", coordinator.Snapshot.Error);
                Assert.False(environment.OperationStore.Peek().Active);
            }
        }

        [TestCase("list")]
        [TestCase("add")]
        [TestCase("remove")]
        public void PackageManagerStartExceptionBecomesFailureState(string operation)
        {
            BootstrapCoordinatorTestEnvironment environment = operation == "remove"
                ? CreateLegacyMigrationEnvironment()
                : new BootstrapCoordinatorTestEnvironment();

            if (operation == "list")
            {
                environment.PackageManager.ThrowOnNextList = true;
            }

            using (BootstrapSetupCoordinator coordinator = environment.CreateCoordinator())
            {
                coordinator.Initialize();

                if (operation == "add")
                {
                    coordinator.Tick();
                    environment.PackageManager.ThrowOnNextAdd = true;
                    Assert.True(coordinator.BeginSetup());
                }
                else if (operation == "remove")
                {
                    coordinator.Tick();
                    environment.PackageManager.ThrowOnNextRemove = true;
                    Assert.True(coordinator.BeginSetup());
                    environment.CompleteLatestAddAndAdvance(coordinator);
                    environment.CompleteLatestAddAndAdvance(coordinator);
                }

                Assert.AreEqual(BootstrapSetupPhase.Failed, coordinator.Snapshot.Phase);
                StringAssert.Contains(operation + "-start-failure", coordinator.Snapshot.Error);
            }
        }

        private static BootstrapCoordinatorTestEnvironment CreateLegacyMigrationEnvironment()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            environment.PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.EditorPackageId,
                BootstrapCoordinatorTestEnvironment.GetEditorReference(BootstrapChannel.Stable),
                BootstrapCoordinatorTestEnvironment.PreviousRevision);
            environment.PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.LoggingPackageId,
                BootstrapCoordinatorTestEnvironment.GetLoggingReference(BootstrapChannel.Stable),
                BootstrapCoordinatorTestEnvironment.PreviousRevision);
            environment.PackageState.InstallRegistryPackageInstaller();
            return environment;
        }

        private static void InstallAllExcept(
            BootstrapCoordinatorTestEnvironment environment,
            string missingPackageId)
        {
            foreach (BootstrapPackageStep step in BootstrapCoordinatorTestEnvironment.CreatePlan(
                         BootstrapChannel.Stable))
            {
                if (step.PackageId == missingPackageId)
                {
                    continue;
                }

                environment.PackageState.InstallGit(
                    step.PackageId,
                    step.PackageReference,
                    step.PackageId == DeucarianBootstrapPackageConstants.PackageInstallerPackageId
                        ? BootstrapCoordinatorTestEnvironment.TargetRevision
                        : BootstrapCoordinatorTestEnvironment.PreviousRevision);
            }
        }
    }
}
