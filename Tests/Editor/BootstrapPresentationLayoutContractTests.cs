using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Deucarian.Bootstrap.Editor.Tests
{
    [TestFixture]
    internal sealed class BootstrapPresentationContractTests
    {
        [TestCase(BootstrapSetupAction.Install, "Install")]
        [TestCase(BootstrapSetupAction.Repair, "Repair")]
        [TestCase(BootstrapSetupAction.SwitchChannel, "Switch Channel")]
        [TestCase(BootstrapSetupAction.Migrate, "Migrate")]
        [TestCase(BootstrapSetupAction.Refresh, "Refresh Status")]
        [TestCase(BootstrapSetupAction.OpenPackageInstaller, "Open Package Installer")]
        public void ReviewAndHealthyStates_ExposeExactPrimaryActionLabel(
            BootstrapSetupAction action,
            string expectedLabel)
        {
            BootstrapSetupPhase phase = action == BootstrapSetupAction.OpenPackageInstaller
                ? BootstrapSetupPhase.Healthy
                : action == BootstrapSetupAction.Refresh
                    ? BootstrapSetupPhase.ReviewRequired
                    : BootstrapSetupPhase.Review;

            BootstrapPresentationModel model = BootstrapPresentationModelFactory.Create(
                Snapshot(action, phase));

            AssertAll(() =>
            {
                Assert.That(model.PrimaryAction, Is.EqualTo(action));
                Assert.That(model.PrimaryActionLabel, Is.EqualTo(expectedLabel));
                Assert.That(model.PrimaryActionEnabled, Is.True);
                Assert.That(model.PrimaryActionTooltip, Is.Not.Empty);
            });
        }

        [TestCase(BootstrapSetupPhase.Loading, "Checking...")]
        [TestCase(BootstrapSetupPhase.Installing, "Installing...")]
        [TestCase(BootstrapSetupPhase.WaitingForUnity, "Waiting for Unity...")]
        [TestCase(BootstrapSetupPhase.Verifying, "Verifying...")]
        public void BusyStates_HaveOneTruthfulDisabledPrimaryLabel(
            BootstrapSetupPhase phase,
            string expectedLabel)
        {
            BootstrapPresentationModel model = BootstrapPresentationModelFactory.Create(
                Snapshot(BootstrapSetupAction.Repair, phase));

            AssertAll(() =>
            {
                Assert.That(model.PrimaryAction, Is.EqualTo(BootstrapSetupAction.None));
                Assert.That(model.PrimaryActionLabel, Is.EqualTo(expectedLabel));
                Assert.That(model.PrimaryActionEnabled, Is.False);
                Assert.That(model.ChannelEnabled, Is.False);
            });
        }

        [Test]
        public void AdvancedDetailsExposeFullRevisionHashes()
        {
            const string revision = "0123456789abcdef0123456789abcdef01234567";
            BootstrapSetupSnapshot snapshot = Snapshot(
                BootstrapSetupAction.OpenPackageInstaller,
                BootstrapSetupPhase.Healthy);
            BootstrapSetupSnapshot withFullRevision = new BootstrapSetupSnapshot(
                snapshot.Channel,
                snapshot.Phase,
                snapshot.CatalogOrigin,
                snapshot.CatalogSource,
                snapshot.CatalogNotice,
                snapshot.Status,
                snapshot.Error,
                snapshot.TargetGitUrl,
                revision,
                snapshot.Plan,
                snapshot.CompletedPackageIds,
                snapshot.PendingPackageId,
                snapshot.InstalledState,
                snapshot.Health,
                snapshot.LegacyRegistryStatus);

            BootstrapPresentationModel model = BootstrapPresentationModelFactory.Create(withFullRevision);

            Assert.That(
                model.Details.Single(detail => detail.Label == "Target branch revision").Value,
                Is.EqualTo(revision));
        }

        private static BootstrapSetupSnapshot Snapshot(
            BootstrapSetupAction action,
            BootstrapSetupPhase phase)
        {
            BootstrapPackageStep[] plan =
            {
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.EditorPackageDisplayName,
                    "https://github.com/Deucarian/Editor.git#main"),
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageDisplayName,
                    "https://github.com/Deucarian/Logging.git#main"),
                new BootstrapPackageStep(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageDisplayName,
                    DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl)
            };
            BootstrapInstalledState installed = action == BootstrapSetupAction.OpenPackageInstaller
                ? new BootstrapInstalledState(new[]
                {
                    Installed(DeucarianBootstrapPackageConstants.EditorPackageId),
                    Installed(DeucarianBootstrapPackageConstants.LoggingPackageId),
                    new BootstrapInstalledPackageInfo(
                        DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                        "1.2.0",
                        "git",
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl,
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl,
                        "0123456789abcdef")
                })
                : BootstrapInstalledState.Empty;
            BootstrapPackageInstallerSetupState installerState =
                action == BootstrapSetupAction.OpenPackageInstaller
                    ? BootstrapPackageInstallerSetupState.Healthy
                    : BootstrapPackageInstallerSetupState.Missing;
            BootstrapHealthReport health = new BootstrapHealthReport(
                action == BootstrapSetupAction.OpenPackageInstaller,
                action == BootstrapSetupAction.OpenPackageInstaller,
                installerState,
                action,
                action != BootstrapSetupAction.Install);

            return new BootstrapSetupSnapshot(
                BootstrapChannel.Stable,
                phase,
                BootstrapCatalogOrigin.Remote,
                "Remote Package Registry #main",
                string.Empty,
                "Ready",
                string.Empty,
                DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl,
                "0123456789abcdef",
                plan,
                Array.Empty<string>(),
                string.Empty,
                installed,
                health,
                BootstrapScopedRegistryStatus.NotInspected);
        }

        private static BootstrapInstalledPackageInfo Installed(string packageId)
        {
            return new BootstrapInstalledPackageInfo(
                packageId,
                "1.0.0",
                "git",
                packageId,
                string.Empty,
                string.Empty);
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }

    [TestFixture]
    internal sealed class BootstrapResponsiveLayoutContractTests
    {
        [TestCase(0f, BootstrapResponsiveMode.Narrow)]
        [TestCase(899.999f, BootstrapResponsiveMode.Narrow)]
        [TestCase(900f, BootstrapResponsiveMode.Compact)]
        [TestCase(1179.999f, BootstrapResponsiveMode.Compact)]
        [TestCase(1180f, BootstrapResponsiveMode.Wide)]
        [TestCase(1600f, BootstrapResponsiveMode.Wide)]
        public void Breakpoints_ResolveExactNarrowCompactWideBoundaries(
            float width,
            BootstrapResponsiveMode expected)
        {
            Assert.That(BootstrapResponsiveLayout.ResolveMode(width), Is.EqualTo(expected));
        }

        [Test]
        public void BreakpointConstants_MatchSharedWorkbenchContract()
        {
            AssertAll(() =>
            {
                Assert.That(BootstrapResponsiveLayout.NarrowBreakpoint, Is.EqualTo(900f));
                Assert.That(BootstrapResponsiveLayout.WideBreakpoint, Is.EqualTo(1180f));
                Assert.That(BootstrapResponsiveLayout.ResolveMode(float.NaN),
                    Is.EqualTo(BootstrapResponsiveMode.Narrow));
                Assert.That(BootstrapResponsiveLayout.ResolveMode(float.PositiveInfinity),
                    Is.EqualTo(BootstrapResponsiveMode.Narrow));
            });
        }

        [TestCase(640f, 520f, BootstrapResponsiveMode.Narrow, 1, true)]
        [TestCase(1024f, 600f, BootstrapResponsiveMode.Compact, 2, false)]
        [TestCase(1280f, 720f, BootstrapResponsiveMode.Wide, 3, false)]
        public void LayoutState_ReservesNonOverlappingBodyAndPersistentActionSpace(
            float width,
            float height,
            BootstrapResponsiveMode expectedMode,
            int expectedColumns,
            bool expectStackedActions)
        {
            BootstrapResponsiveLayoutState layout =
                BootstrapResponsiveLayout.Calculate(width, height);

            AssertAll(() =>
            {
                Assert.That(layout.Mode, Is.EqualTo(expectedMode));
                Assert.That(layout.StepColumns, Is.EqualTo(expectedColumns));
                Assert.That(layout.ActionsStacked, Is.EqualTo(expectStackedActions));
                Assert.That(layout.PrimaryActionFillsRow, Is.EqualTo(expectStackedActions));
                Assert.That(layout.ActionBarMinimumHeight, Is.GreaterThan(0f));
                Assert.That(layout.ActionBarMinimumHeight, Is.LessThan(height));
                Assert.That(layout.AvailableBodyHeight, Is.GreaterThan(0f));
                Assert.That(layout.AvailableBodyHeight + layout.ActionBarMinimumHeight,
                    Is.EqualTo(height).Within(0.001f));
                Assert.That(width - (layout.ContentPadding * 2f), Is.GreaterThan(0f));
            });
        }

        [Test]
        public void View_UsesMutuallyExclusiveLightAndDarkSkinClasses()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);

            view.SetSkin(false);
            AssertAll(() =>
            {
                Assert.That(root.ClassListContains("deucarian-bootstrap--light"), Is.True);
                Assert.That(root.ClassListContains("deucarian-bootstrap--dark"), Is.False);
            });

            view.SetSkin(true);
            AssertAll(() =>
            {
                Assert.That(root.ClassListContains("deucarian-bootstrap--dark"), Is.True);
                Assert.That(root.ClassListContains("deucarian-bootstrap--light"), Is.False);
            });
        }

        [Test]
        public void View_AppliesOnlyTheResponsiveClassForTheCurrentWidth()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);

            AssertResponsiveClass(view, root, 899.999f, BootstrapResponsiveLayout.NarrowClassName);
            AssertResponsiveClass(view, root, 900f, BootstrapResponsiveLayout.CompactClassName);
            AssertResponsiveClass(view, root, 1180f, BootstrapResponsiveLayout.WideClassName);
        }

        [Test]
        public void View_HasExactlyOnePrimaryActionOutsideScrollableContent()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);

            VisualElement scroll = root.Q<ScrollView>("bootstrap-content-scroll");
            VisualElement actionBar = root.Q<VisualElement>("bootstrap-action-bar");
            Button primary = root.Q<Button>("bootstrap-primary-action");
            Button refresh = root.Q<Button>("bootstrap-refresh-button");

            AssertAll(() =>
            {
                Assert.That(scroll, Is.Not.Null);
                Assert.That(actionBar, Is.Not.Null);
                Assert.That(primary, Is.Not.Null);
                Assert.That(refresh, Is.Not.Null);
                Assert.That(CountNamedElements(root, "bootstrap-primary-action"), Is.EqualTo(1));
                Assert.That(IsDescendantOf(primary, actionBar), Is.True);
                Assert.That(IsDescendantOf(primary, scroll), Is.False,
                    "The primary action must stay visible while the setup details scroll.");
                Assert.That(IsDescendantOf(refresh, actionBar), Is.True);
                Assert.That(primary.ClassListContains("bootstrap-button--primary"), Is.True);
                Assert.That(refresh.ClassListContains("bootstrap-button--primary"), Is.False);
                Assert.That(primary.focusable, Is.True);
                Assert.That(refresh.focusable, Is.True);
            });
        }

        [Test]
        public void RepeatedRendering_DoesNotDuplicateOrRelocatePrimaryControls()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);
            BootstrapPresentationModel model = new BootstrapPresentationModel(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.Review,
                "Setup needs repair",
                "Review the setup closure.",
                "Ready",
                BootstrapPresentationTone.Warning,
                "bootstrap-icon--repair",
                BootstrapSetupAction.Repair,
                "Repair",
                "Repair the setup closure.",
                true,
                true,
                "One explicit action",
                "Opening Bootstrap never changes packages automatically.",
                Array.Empty<BootstrapStepPresentation>(),
                Array.Empty<BootstrapDetailPresentation>(),
                string.Empty,
                0,
                "Package Installer needs repair.");

            view.Render(model);
            view.Render(model);

            Button primary = root.Q<Button>("bootstrap-primary-action");
            VisualElement actionBar = root.Q<VisualElement>("bootstrap-action-bar");
            AssertAll(() =>
            {
                Assert.That(CountNamedElements(root, "bootstrap-primary-action"), Is.EqualTo(1));
                Assert.That(IsDescendantOf(primary, actionBar), Is.True);
                Assert.That(primary.enabledSelf, Is.True);
                Assert.That(primary.tooltip, Is.Not.Empty);
            });
        }

        [Test]
        public void RefreshPrimaryHidesTheDuplicateQuietRefreshControl()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);
            view.Render(BootstrapPresentationModelFactory.Create(
                BootstrapPresentationContractTestsSnapshot.RefreshRequired()));

            Button refresh = root.Q<Button>("bootstrap-refresh-button");
            Button primary = root.Q<Button>("bootstrap-primary-action");
            AssertAll(() =>
            {
                Assert.That(refresh.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(primary.style.display.value, Is.Not.EqualTo(DisplayStyle.None));
                Assert.That(primary.enabledSelf, Is.True);
            });
        }

        private static BootstrapSetupView CreateView()
        {
            return new BootstrapSetupView(
                _ => { },
                _ => { },
                () => { },
                _ => { });
        }

        private static void AssertResponsiveClass(
            BootstrapSetupView view,
            VisualElement root,
            float width,
            string expectedClass)
        {
            view.ApplyResponsiveLayout(width, 600f);
            string[] responsiveClasses =
            {
                BootstrapResponsiveLayout.NarrowClassName,
                BootstrapResponsiveLayout.CompactClassName,
                BootstrapResponsiveLayout.WideClassName
            };

            Assert.That(
                responsiveClasses.Where(root.ClassListContains),
                Is.EqualTo(new[] { expectedClass }));
        }

        private static int CountNamedElements(VisualElement root, string name)
        {
            int count = string.Equals(root.name, name, StringComparison.Ordinal) ? 1 : 0;
            foreach (VisualElement child in root.Children())
            {
                count += CountNamedElements(child, name);
            }

            return count;
        }

        private static bool IsDescendantOf(VisualElement element, VisualElement ancestor)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }

    internal static class BootstrapPresentationContractTestsSnapshot
    {
        public static BootstrapSetupSnapshot RefreshRequired()
        {
            BootstrapInstalledState installed = BootstrapInstalledState.Empty;
            BootstrapHealthReport health = new BootstrapHealthReport(
                true,
                true,
                BootstrapPackageInstallerSetupState.UnknownReviewRequired,
                BootstrapSetupAction.Refresh,
                true);
            return new BootstrapSetupSnapshot(
                BootstrapChannel.Stable,
                BootstrapSetupPhase.ReviewRequired,
                BootstrapCatalogOrigin.BundledFallback,
                "Bundled setup fallback",
                "Remote unavailable.",
                "Review required.",
                string.Empty,
                DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl,
                string.Empty,
                Array.Empty<BootstrapPackageStep>(),
                Array.Empty<string>(),
                string.Empty,
                installed,
                health,
                BootstrapScopedRegistryStatus.NotInspected);
        }
    }
}
