using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Deucarian.Bootstrap.Editor.Tests
{
    internal sealed class BootstrapSetupCoordinatorReloadTests
    {
        [TestCase(DeucarianBootstrapPackageConstants.EditorPackageId)]
        [TestCase(DeucarianBootstrapPackageConstants.LoggingPackageId)]
        [TestCase(DeucarianBootstrapPackageConstants.PackageInstallerPackageId)]
        public void ReloadDuringPackageAddDisposesStaleWrapperAndResumesFromSavedStep(
            string packageId)
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            BootstrapSetupCoordinator first = environment.InitializeAndDetect();
            Assert.True(first.BeginSetup());
            AdvanceToPendingAdd(environment, first, packageId);

            BootstrapCoordinatorPackageManagerRequest staleRequest =
                environment.PackageManager.LastAddRequest;
            string packageReference = BootstrapCoordinatorTestEnvironment
                .CreatePlan(BootstrapChannel.Stable)
                .Single(step => step.PackageId == packageId)
                .PackageReference;

            first.Dispose();

            Assert.True(staleRequest.Disposed);
            Assert.True(environment.OperationStore.Peek().Active);
            Assert.AreEqual(BootstrapPersistedOperationKind.Add,
                environment.OperationStore.Peek().PendingKind);
            Assert.AreEqual(packageId, environment.OperationStore.Peek().PendingPackageId);

            using (BootstrapSetupCoordinator resumed = environment.CreateCoordinator())
            {
                resumed.Initialize();
                resumed.Tick();

                Assert.AreEqual(BootstrapSetupPhase.Installing, resumed.Snapshot.Phase);
                Assert.AreEqual(packageId, resumed.Snapshot.PendingPackageId);
                Assert.AreEqual(2,
                    environment.PackageManager.AddedReferences.Count(reference =>
                        string.Equals(reference, packageReference, StringComparison.OrdinalIgnoreCase)));

                environment.PackageManager.LastAddRequest.CompleteSuccess();
                environment.FinishSetup(resumed);

                Assert.AreEqual(BootstrapSetupPhase.Healthy, resumed.Snapshot.Phase);
                Assert.True(resumed.Snapshot.Health.IsHealthy);
            }
        }

        [Test]
        public void ReloadDuringLegacyRemoveRepeatsRemovalThenContinuesWithGitAdd()
        {
            BootstrapCoordinatorTestEnvironment environment = CreateLegacyEnvironment();
            BootstrapSetupCoordinator first = environment.InitializeAndDetect();
            Assert.True(first.BeginSetup());
            environment.CompleteLatestAddAndAdvance(first);
            environment.CompleteLatestAddAndAdvance(first);
            BootstrapCoordinatorPackageManagerRequest staleRemove =
                environment.PackageManager.LastRemoveRequest;

            first.Dispose();

            Assert.True(staleRemove.Disposed);
            Assert.AreEqual(BootstrapPersistedOperationKind.Remove,
                environment.OperationStore.Peek().PendingKind);

            using (BootstrapSetupCoordinator resumed = environment.CreateCoordinator())
            {
                resumed.Initialize();
                resumed.Tick();

                Assert.AreEqual(2, environment.PackageManager.RemoveRequests.Count);
                Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    resumed.Snapshot.PendingPackageId);
                environment.PackageManager.LastRemoveRequest.CompleteSuccess();
                resumed.Tick();

                Assert.AreEqual(
                    BootstrapChannelUtility.GetPackageInstallerGitUrl(BootstrapChannel.Stable),
                    environment.PackageManager.AddedReferences.Last());

                environment.PackageManager.LastAddRequest.CompleteSuccess();
                environment.FinishSetup(resumed);
                Assert.AreEqual(BootstrapSetupPhase.Healthy, resumed.Snapshot.Phase);
            }
        }

        [Test]
        public void ReloadDuringPostAddListUsesRelistedStateWithoutDuplicateAdd()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            BootstrapSetupCoordinator first = environment.InitializeAndDetect();
            Assert.True(first.BeginSetup());
            string editorReference = BootstrapCoordinatorTestEnvironment.GetEditorReference(
                BootstrapChannel.Stable);

            environment.PackageManager.AutoCompleteLists = false;
            environment.PackageManager.LastAddRequest.CompleteSuccess();
            first.Tick();

            Assert.AreEqual(BootstrapSetupPhase.WaitingForUnity, first.Snapshot.Phase);
            Assert.AreEqual(BootstrapPersistedOperationKind.List,
                environment.OperationStore.Peek().PendingKind);
            BootstrapCoordinatorPackageManagerRequest staleList =
                environment.PackageManager.LastListRequest;

            first.Dispose();

            Assert.True(staleList.Disposed);

            using (BootstrapSetupCoordinator resumed = environment.CreateCoordinator())
            {
                resumed.Initialize();
                environment.PackageManager.CompleteLatestListSuccess();
                resumed.Tick();

                Assert.AreEqual(1,
                    environment.PackageManager.AddedReferences.Count(reference =>
                        string.Equals(reference, editorReference, StringComparison.OrdinalIgnoreCase)));
                Assert.Contains(
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    resumed.Snapshot.CompletedPackageIds.ToArray());
                Assert.AreEqual(DeucarianBootstrapPackageConstants.LoggingPackageId,
                    resumed.Snapshot.PendingPackageId);

                environment.PackageManager.AutoCompleteLists = true;
                environment.FinishSetup(resumed);
                Assert.AreEqual(BootstrapSetupPhase.Healthy, resumed.Snapshot.Phase);
            }
        }

        [Test]
        public void ReloadDuringVerificationDisposesRevisionWrapperAndReverifiesAuthoritatively()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            BootstrapSetupCoordinator first = environment.InitializeAndDetect();
            Assert.True(first.BeginSetup());
            environment.CompleteLatestAddAndAdvance(first);
            environment.CompleteLatestAddAndAdvance(first);

            environment.RevisionResolver.CompleteImmediately = false;
            environment.PackageManager.LastAddRequest.CompleteSuccess();
            first.Tick();
            first.Tick();
            first.Tick();

            Assert.AreEqual(BootstrapSetupPhase.Verifying, first.Snapshot.Phase);
            Assert.True(environment.OperationStore.Peek().Verifying);
            BootstrapCoordinatorRevisionRequest staleRevision =
                environment.RevisionResolver.Requests.Last();
            Assert.False(staleRevision.IsCompleted);

            first.Dispose();

            Assert.True(staleRevision.Disposed);
            environment.RevisionResolver.CompleteImmediately = true;

            using (BootstrapSetupCoordinator resumed = environment.CreateCoordinator())
            {
                resumed.Initialize();
                resumed.Tick();

                Assert.AreEqual(BootstrapSetupPhase.Healthy, resumed.Snapshot.Phase);
                Assert.True(resumed.Snapshot.Health.IsHealthy);
                Assert.AreEqual(3, environment.PackageManager.AddRequests.Count);
                Assert.False(environment.OperationStore.Peek().Active);
            }
        }

        [Test]
        public void RepeatedResumeRecognizesCompletedAddResultsWithoutReinstallingPackages()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            IReadOnlyList<BootstrapPackageStep> plan =
                BootstrapCoordinatorTestEnvironment.CreatePlan(BootstrapChannel.Stable);

            BootstrapSetupCoordinator editorCoordinator = environment.InitializeAndDetect();
            Assert.True(editorCoordinator.BeginSetup());
            BootstrapCoordinatorPackageManagerRequest editorRequest =
                environment.PackageManager.LastAddRequest;
            editorRequest.CompleteSuccess();
            editorCoordinator.Dispose();
            Assert.True(editorRequest.Disposed);

            BootstrapSetupCoordinator loggingCoordinator = environment.CreateCoordinator();
            loggingCoordinator.Initialize();
            loggingCoordinator.Tick();
            Assert.AreEqual(DeucarianBootstrapPackageConstants.LoggingPackageId,
                loggingCoordinator.Snapshot.PendingPackageId);
            Assert.AreEqual(1, CountAdds(environment, plan[0].PackageReference));
            BootstrapCoordinatorPackageManagerRequest loggingRequest =
                environment.PackageManager.LastAddRequest;
            loggingRequest.CompleteSuccess();
            loggingCoordinator.Dispose();
            Assert.True(loggingRequest.Disposed);

            BootstrapSetupCoordinator installerCoordinator = environment.CreateCoordinator();
            installerCoordinator.Initialize();
            installerCoordinator.Tick();
            Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                installerCoordinator.Snapshot.PendingPackageId);
            Assert.AreEqual(1, CountAdds(environment, plan[1].PackageReference));
            BootstrapCoordinatorPackageManagerRequest installerRequest =
                environment.PackageManager.LastAddRequest;
            installerRequest.CompleteSuccess();
            installerCoordinator.Dispose();
            Assert.True(installerRequest.Disposed);

            using (BootstrapSetupCoordinator verificationCoordinator = environment.CreateCoordinator())
            {
                verificationCoordinator.Initialize();
                verificationCoordinator.Tick();
                Assert.AreEqual(BootstrapSetupPhase.Verifying,
                    verificationCoordinator.Snapshot.Phase);
                verificationCoordinator.Tick();

                Assert.AreEqual(BootstrapSetupPhase.Healthy,
                    verificationCoordinator.Snapshot.Phase);
                Assert.AreEqual(1, CountAdds(environment, plan[0].PackageReference));
                Assert.AreEqual(1, CountAdds(environment, plan[1].PackageReference));
                Assert.AreEqual(1, CountAdds(environment, plan[2].PackageReference));
            }

            using (BootstrapSetupCoordinator repeatedHealthyDetection =
                   environment.InitializeAndDetect())
            {
                Assert.AreEqual(BootstrapSetupPhase.Healthy,
                    repeatedHealthyDetection.Snapshot.Phase);
                Assert.AreEqual(3, environment.PackageManager.AddRequests.Count);
            }
        }

        private static void AdvanceToPendingAdd(
            BootstrapCoordinatorTestEnvironment environment,
            BootstrapSetupCoordinator coordinator,
            string packageId)
        {
            for (int step = 0; step < 3; step++)
            {
                if (string.Equals(
                    coordinator.Snapshot.PendingPackageId,
                    packageId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                environment.CompleteLatestAddAndAdvance(coordinator);
            }

            Assert.Fail("Coordinator did not reach pending Add for " + packageId + ".");
        }

        private static int CountAdds(
            BootstrapCoordinatorTestEnvironment environment,
            string packageReference)
        {
            return environment.PackageManager.AddedReferences.Count(reference => string.Equals(
                reference,
                packageReference,
                StringComparison.OrdinalIgnoreCase));
        }

        private static BootstrapCoordinatorTestEnvironment CreateLegacyEnvironment()
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
    }
}
