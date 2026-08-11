using NUnit.Framework;

namespace Deucarian.Bootstrap.Editor.Tests
{
    internal sealed class BootstrapSetupCoordinatorDetectionTests
    {
        [Test]
        public void InitializeInspectsOnlyAndNeverStartsPackageMutation()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();

            using (BootstrapSetupCoordinator coordinator = environment.CreateCoordinator())
            {
                coordinator.Initialize();

                Assert.AreEqual(BootstrapSetupPhase.Loading, coordinator.Snapshot.Phase);
                Assert.AreEqual(1, environment.PackageManager.ListRequests.Count);
                Assert.Zero(environment.PackageManager.AddRequests.Count);
                Assert.Zero(environment.PackageManager.RemoveRequests.Count);

                coordinator.Tick();

                Assert.AreEqual(BootstrapSetupPhase.Review, coordinator.Snapshot.Phase);
                Assert.AreEqual(BootstrapSetupAction.Install, coordinator.Snapshot.Health.RecommendedAction);
                Assert.Zero(environment.PackageManager.AddRequests.Count);
                Assert.Zero(environment.PackageManager.RemoveRequests.Count);
                Assert.Zero(environment.ChannelStore.SetCount);
            }
        }

        [Test]
        public void HealthySetupFinishesDetectionWithoutCreatingAnOperation()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            environment.InstallHealthy();

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.AreEqual(BootstrapSetupPhase.Healthy, coordinator.Snapshot.Phase);
                Assert.True(coordinator.Snapshot.Health.IsHealthy);
                Assert.AreEqual(
                    BootstrapSetupAction.OpenPackageInstaller,
                    coordinator.Snapshot.Health.RecommendedAction);
                Assert.Zero(environment.PackageManager.AddRequests.Count);
                Assert.Zero(environment.PackageManager.RemoveRequests.Count);
                Assert.False(environment.OperationStore.Peek().Active);
            }
        }

        [TestCase(false, true, true, BootstrapPackageInstallerSetupState.Healthy)]
        [TestCase(true, false, true, BootstrapPackageInstallerSetupState.Healthy)]
        [TestCase(true, true, false, BootstrapPackageInstallerSetupState.Missing)]
        public void MissingSetupPackageProducesAnExplicitRepairReview(
            bool installEditor,
            bool installLogging,
            bool installPackageInstaller,
            BootstrapPackageInstallerSetupState expectedInstallerState)
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            InstallSelectedPackages(
                environment,
                installEditor,
                installLogging,
                installPackageInstaller);

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.AreEqual(BootstrapSetupPhase.Review, coordinator.Snapshot.Phase);
                Assert.AreEqual(BootstrapSetupAction.Repair, coordinator.Snapshot.Health.RecommendedAction);
                Assert.AreEqual(expectedInstallerState, coordinator.Snapshot.Health.PackageInstallerState);
                Assert.AreEqual(installEditor, coordinator.Snapshot.Health.EditorInstalled);
                Assert.AreEqual(installLogging, coordinator.Snapshot.Health.LoggingInstalled);
            }
        }

        [Test]
        public void WrongPackageInstallerChannelRequiresChannelSwitch()
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
            environment.PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                BootstrapChannelUtility.GetPackageInstallerGitUrl(BootstrapChannel.Development),
                BootstrapCoordinatorTestEnvironment.TargetRevision);

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.AreEqual(BootstrapPackageInstallerSetupState.WrongChannel,
                    coordinator.Snapshot.Health.PackageInstallerState);
                Assert.AreEqual(BootstrapSetupAction.SwitchChannel,
                    coordinator.Snapshot.Health.RecommendedAction);
                Assert.AreEqual(BootstrapSetupPhase.Review, coordinator.Snapshot.Phase);
            }
        }

        [Test]
        public void OutdatedSelectedBranchRevisionRequiresRepair()
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
                Assert.AreEqual(BootstrapSetupAction.Repair,
                    coordinator.Snapshot.Health.RecommendedAction);
                Assert.AreEqual(BootstrapCoordinatorTestEnvironment.TargetRevision,
                    coordinator.Snapshot.TargetRevision);
            }
        }

        [Test]
        public void UnverifiableRemoteRevisionRequiresReviewWithoutClaimingHealthy()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            environment.InstallHealthy();
            environment.RevisionResolver.ResultFactory =
                () => BootstrapRevisionResult.CreateFailure("offline");

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.AreEqual(BootstrapSetupPhase.ReviewRequired, coordinator.Snapshot.Phase);
                Assert.AreEqual(BootstrapPackageInstallerSetupState.UnknownReviewRequired,
                    coordinator.Snapshot.Health.PackageInstallerState);
                Assert.AreEqual(BootstrapSetupAction.Refresh,
                    coordinator.Snapshot.Health.RecommendedAction);
                Assert.IsEmpty(coordinator.Snapshot.TargetRevision);
                StringAssert.Contains("could not be verified", coordinator.Snapshot.CatalogNotice);
            }
        }

        [Test]
        public void UnverifiableRevisionDoesNotBlockRepairOfMissingDependency()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();
            environment.PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.EditorPackageId,
                BootstrapCoordinatorTestEnvironment.GetEditorReference(BootstrapChannel.Stable),
                BootstrapCoordinatorTestEnvironment.PreviousRevision);
            environment.PackageState.InstallGit(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                BootstrapChannelUtility.GetPackageInstallerGitUrl(BootstrapChannel.Stable),
                BootstrapCoordinatorTestEnvironment.TargetRevision);
            environment.RevisionResolver.ResultFactory =
                () => BootstrapRevisionResult.CreateFailure("offline");

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.AreEqual(BootstrapSetupPhase.Review, coordinator.Snapshot.Phase);
                Assert.AreEqual(BootstrapSetupAction.Repair,
                    coordinator.Snapshot.Health.RecommendedAction);
                Assert.False(coordinator.Snapshot.Health.LoggingInstalled);
            }
        }

        [Test]
        public void LegacyRegistryPackageInstallerIsPresentedAsMigration()
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
            environment.LegacyRegistryInspector.Status =
                BootstrapScopedRegistryStatus.CreateConfigured(
                    "Packages/manifest.json",
                    "Legacy source remains configured for compatibility.");

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.AreEqual(BootstrapSetupAction.Migrate,
                    coordinator.Snapshot.Health.RecommendedAction);
                Assert.AreEqual(BootstrapSetupPhase.Review, coordinator.Snapshot.Phase);
                Assert.AreEqual(BootstrapScopedRegistryState.Valid,
                    coordinator.Snapshot.LegacyRegistryStatus.State);
                Assert.Zero(environment.PackageManager.RemoveRequests.Count);
            }
        }

        [TestCase("Remote Package Registry is unavailable: offline.")]
        [TestCase("Remote Package Registry was invalid: missing dependency.")]
        public void ValidBundledFallbackRemainsAuthoritativeWhenRemoteCatalogFails(string notice)
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment
                {
                    CatalogOrigin = BootstrapCatalogOrigin.BundledFallback,
                    CatalogNotice = notice + " Using the validated bundled setup fallback."
                };

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                Assert.AreEqual(BootstrapCatalogOrigin.BundledFallback,
                    coordinator.Snapshot.CatalogOrigin);
                Assert.AreEqual("Bundled setup fallback", coordinator.Snapshot.CatalogSource);
                StringAssert.Contains(notice, coordinator.Snapshot.CatalogNotice);
                Assert.AreEqual(BootstrapSetupPhase.Review, coordinator.Snapshot.Phase);
                Assert.AreEqual(3, coordinator.Snapshot.Plan.Count);
            }
        }

        [Test]
        public void SynchronizeChannelRedetectsWithoutInstalling()
        {
            BootstrapCoordinatorTestEnvironment environment =
                new BootstrapCoordinatorTestEnvironment();

            using (BootstrapSetupCoordinator coordinator = environment.InitializeAndDetect())
            {
                environment.ChannelStore.Channel = BootstrapChannel.Development;

                Assert.True(coordinator.SynchronizeChannel());
                Assert.AreEqual(BootstrapSetupPhase.Loading, coordinator.Snapshot.Phase);
                coordinator.Tick();

                Assert.AreEqual(BootstrapChannel.Development, coordinator.Snapshot.Channel);
                Assert.AreEqual(BootstrapChannel.Development, environment.CatalogLoader.LastChannel);
                Assert.AreEqual(BootstrapSetupPhase.Review, coordinator.Snapshot.Phase);
                Assert.Zero(environment.PackageManager.AddRequests.Count);
                Assert.Zero(environment.PackageManager.RemoveRequests.Count);
                Assert.Zero(environment.ChannelStore.SetCount,
                    "Reading a Package Installer channel change must preserve its timestamp.");
            }
        }

        private static void InstallSelectedPackages(
            BootstrapCoordinatorTestEnvironment environment,
            bool editor,
            bool logging,
            bool packageInstaller)
        {
            if (editor)
            {
                environment.PackageState.InstallGit(
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    BootstrapCoordinatorTestEnvironment.GetEditorReference(BootstrapChannel.Stable),
                    BootstrapCoordinatorTestEnvironment.PreviousRevision);
            }

            if (logging)
            {
                environment.PackageState.InstallGit(
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    BootstrapCoordinatorTestEnvironment.GetLoggingReference(BootstrapChannel.Stable),
                    BootstrapCoordinatorTestEnvironment.PreviousRevision);
            }

            if (packageInstaller)
            {
                environment.PackageState.InstallGit(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    BootstrapChannelUtility.GetPackageInstallerGitUrl(BootstrapChannel.Stable),
                    BootstrapCoordinatorTestEnvironment.TargetRevision);
            }
        }
    }
}
