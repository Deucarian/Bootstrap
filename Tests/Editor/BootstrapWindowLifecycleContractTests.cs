using System;
using NUnit.Framework;

namespace Deucarian.Bootstrap.Editor.Tests
{
    [TestFixture]
    internal sealed class BootstrapWindowLifecycleContractTests
    {
        private string _firstProjectRoot;
        private string _secondProjectRoot;

        [SetUp]
        public void SetUp()
        {
            string identity = Guid.NewGuid().ToString("N");
            _firstProjectRoot = "C:/BootstrapTests/First-" + identity;
            _secondProjectRoot = "C:/BootstrapTests/Second-" + identity;
        }

        [TearDown]
        public void TearDown()
        {
            BootstrapStartupPreferences.DeleteForProjectForTests(_firstProjectRoot);
            BootstrapStartupPreferences.DeleteForProjectForTests(_secondProjectRoot);
        }

        [Test]
        public void AutomaticStartupRetirement_RequiresAuthoritativeHealthySnapshot()
        {
            BootstrapSetupSnapshot healthy = Snapshot(
                BootstrapSetupPhase.Healthy,
                HealthyReport());
            BootstrapSetupSnapshot healthyPhaseWithUnhealthyReport = Snapshot(
                BootstrapSetupPhase.Healthy,
                UnhealthyReport());

            AssertAll(() =>
            {
                Assert.That(
                    BootstrapWindowLifecyclePolicy.ShouldRetireAutomaticStartup(healthy),
                    Is.True);
                Assert.That(
                    BootstrapWindowLifecyclePolicy.ShouldRetireAutomaticStartup(
                        healthyPhaseWithUnhealthyReport),
                    Is.False);
                Assert.That(
                    BootstrapWindowLifecyclePolicy.ShouldRetireAutomaticStartup(null),
                    Is.False);
            });
        }

        [Test]
        public void AutomaticStartupRetirement_IsIdempotentAndProjectScoped()
        {
            BootstrapSetupSnapshot healthy = Snapshot(
                BootstrapSetupPhase.Healthy,
                HealthyReport());
            BootstrapStartupPreferences.SetShouldShowForProject(_firstProjectRoot, true);

            bool firstRetirement = BootstrapStartupPreferences
                .RetireIfAuthoritativelyHealthyForProject(healthy, _firstProjectRoot);
            bool repeatedRetirement = BootstrapStartupPreferences
                .RetireIfAuthoritativelyHealthyForProject(healthy, _firstProjectRoot);

            AssertAll(() =>
            {
                Assert.That(firstRetirement, Is.True);
                Assert.That(repeatedRetirement, Is.False);
                Assert.That(
                    BootstrapStartupPreferences.ShouldShowForProject(_firstProjectRoot),
                    Is.False);
                Assert.That(
                    BootstrapStartupPreferences.ShouldShowForProject(_secondProjectRoot),
                    Is.True,
                    "Completing setup in one project must not retire another project's welcome.");
            });
        }

        [TestCase(BootstrapSetupPhase.Loading)]
        [TestCase(BootstrapSetupPhase.Review)]
        [TestCase(BootstrapSetupPhase.Installing)]
        [TestCase(BootstrapSetupPhase.WaitingForUnity)]
        [TestCase(BootstrapSetupPhase.Verifying)]
        [TestCase(BootstrapSetupPhase.ReviewRequired)]
        [TestCase(BootstrapSetupPhase.Failed)]
        public void NonHealthyPhase_NeverChangesAutomaticStartupPreference(
            BootstrapSetupPhase phase)
        {
            BootstrapStartupPreferences.SetShouldShowForProject(_firstProjectRoot, true);

            bool retired = BootstrapStartupPreferences
                .RetireIfAuthoritativelyHealthyForProject(
                    Snapshot(phase, HealthyReport()),
                    _firstProjectRoot);

            AssertAll(() =>
            {
                Assert.That(retired, Is.False);
                Assert.That(
                    BootstrapStartupPreferences.ShouldShowForProject(_firstProjectRoot),
                    Is.True);
            });
        }

        [Test]
        public void ActiveOperationResume_IsIndependentFromAutomaticStartupPreference()
        {
            BootstrapStartupPreferences.SetShouldShowForProject(_firstProjectRoot, false);
            BootstrapOperationState active = BootstrapOperationState.CreateActive(
                BootstrapChannel.Stable,
                new[]
                {
                    new BootstrapPackageStep(
                        DeucarianBootstrapPackageConstants.EditorPackageId,
                        "Deucarian Editor",
                        "https://github.com/Deucarian/Editor.git#main")
                });

            AssertAll(() =>
            {
                Assert.That(BootstrapStartupPreferences.ShouldShowForProject(_firstProjectRoot),
                    Is.False);
                Assert.That(
                    BootstrapWindowLifecyclePolicy.ShouldResumeAfterReload(active),
                    Is.True);
                Assert.That(
                    BootstrapWindowLifecyclePolicy.ShouldResumeAfterReload(
                        new BootstrapOperationState()),
                    Is.False);
                Assert.That(
                    BootstrapWindowLifecyclePolicy.ShouldResumeAfterReload(null),
                    Is.False);
            });
        }

        [Test]
        public void HandoffDecision_ClosesOnlyOnSuccessAndKeepsActionableFailure()
        {
            BootstrapWindowHandoffDecision success =
                BootstrapWindowLifecyclePolicy.EvaluateHandoff(
                    new BootstrapHandoffResult(true, string.Empty));
            BootstrapWindowHandoffDecision failure =
                BootstrapWindowLifecyclePolicy.EvaluateHandoff(
                    new BootstrapHandoffResult(false, "Package Installer is still compiling."));
            BootstrapWindowHandoffDecision missingResult =
                BootstrapWindowLifecyclePolicy.EvaluateHandoff(null);

            AssertAll(() =>
            {
                Assert.That(success.CloseWindow, Is.True);
                Assert.That(success.Message, Is.Empty);
                Assert.That(failure.CloseWindow, Is.False);
                Assert.That(failure.Message, Is.EqualTo("Package Installer is still compiling."));
                Assert.That(missingResult.CloseWindow, Is.False);
                Assert.That(missingResult.Message, Does.Contain("refresh status"));
            });
        }

        private static BootstrapSetupSnapshot Snapshot(
            BootstrapSetupPhase phase,
            BootstrapHealthReport health)
        {
            return new BootstrapSetupSnapshot(
                BootstrapChannel.Stable,
                phase,
                BootstrapCatalogOrigin.Remote,
                "registry",
                string.Empty,
                "status",
                string.Empty,
                "https://github.com/Deucarian/Package-Installer.git#main",
                "0123456789abcdef",
                Array.Empty<BootstrapPackageStep>(),
                Array.Empty<string>(),
                string.Empty,
                BootstrapInstalledState.Empty,
                health,
                BootstrapScopedRegistryStatus.NotInspected);
        }

        private static BootstrapHealthReport HealthyReport()
        {
            return new BootstrapHealthReport(
                true,
                true,
                BootstrapPackageInstallerSetupState.Healthy,
                BootstrapSetupAction.OpenPackageInstaller,
                true);
        }

        private static BootstrapHealthReport UnhealthyReport()
        {
            return new BootstrapHealthReport(
                false,
                true,
                BootstrapPackageInstallerSetupState.Missing,
                BootstrapSetupAction.Repair,
                true);
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }
}
