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
        [Test]
        public void WindowFootprint_DefaultsToTheNarrowHeroAndKeepsAUsableMinimum()
        {
            AssertAll(() =>
            {
                Assert.That(DeucarianBootstrapWindow.PreferredWindowWidth, Is.EqualTo(560f));
                Assert.That(DeucarianBootstrapWindow.PreferredWindowHeight, Is.EqualTo(820f));
                Assert.That(DeucarianBootstrapWindow.MinWindowWidth, Is.EqualTo(480f));
                Assert.That(DeucarianBootstrapWindow.MinWindowHeight, Is.EqualTo(460f));
                Assert.That(
                    BootstrapResponsiveLayout.ResolveMode(
                        DeucarianBootstrapWindow.PreferredWindowWidth),
                    Is.EqualTo(BootstrapResponsiveMode.Narrow));
                Assert.That(
                    BootstrapResponsiveLayout.ResolveMode(
                        DeucarianBootstrapWindow.MinWindowWidth),
                    Is.EqualTo(BootstrapResponsiveMode.Narrow));
            });
        }

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

        [TestCase(480f, 460f, BootstrapResponsiveMode.Narrow, 1, false, false, true, 58f)]
        [TestCase(560f, 820f, BootstrapResponsiveMode.Narrow, 1, false, false, true, 58f)]
        [TestCase(1024f, 600f, BootstrapResponsiveMode.Compact, 2, false, false, false, 58f)]
        [TestCase(1280f, 720f, BootstrapResponsiveMode.Wide, 3, false, false, false, 58f)]
        public void LayoutState_ReservesNonOverlappingBodyAndPersistentActionSpace(
            float width,
            float height,
            BootstrapResponsiveMode expectedMode,
            int expectedColumns,
            bool expectHeaderStacked,
            bool expectActionsStacked,
            bool expectPrimaryActionFillsRow,
            float expectedActionBarHeight)
        {
            BootstrapResponsiveLayoutState layout =
                BootstrapResponsiveLayout.Calculate(width, height);

            AssertAll(() =>
            {
                Assert.That(layout.Mode, Is.EqualTo(expectedMode));
                Assert.That(layout.StepColumns, Is.EqualTo(expectedColumns));
                Assert.That(layout.HeaderStacked, Is.EqualTo(expectHeaderStacked));
                Assert.That(layout.ActionsStacked, Is.EqualTo(expectActionsStacked));
                Assert.That(layout.PrimaryActionFillsRow,
                    Is.EqualTo(expectPrimaryActionFillsRow));
                Assert.That(layout.ActionBarMinimumHeight, Is.EqualTo(expectedActionBarHeight));
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
        public void View_KeepsOnePrimaryActionInTheBarAndRefreshInsideCollapsedDetails()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);

            VisualElement scroll = root.Q<ScrollView>("bootstrap-content-scroll");
            VisualElement actionBar = root.Q<VisualElement>("bootstrap-action-bar");
            Foldout details = root.Q<Foldout>("bootstrap-details-foldout");
            VisualElement detailsContent = root.Q<VisualElement>("bootstrap-details-content");
            Button primary = root.Q<Button>("bootstrap-primary-action");
            Button refresh = root.Q<Button>("bootstrap-refresh-button");

            AssertAll(() =>
            {
                Assert.That(scroll, Is.Not.Null);
                Assert.That(actionBar, Is.Not.Null);
                Assert.That(details, Is.Not.Null);
                Assert.That(detailsContent, Is.Not.Null);
                Assert.That(primary, Is.Not.Null);
                Assert.That(refresh, Is.Not.Null);
                Assert.That(CountNamedElements(root, "bootstrap-primary-action"), Is.EqualTo(1));
                Assert.That(CountNamedElements(root, "bootstrap-refresh-button"), Is.EqualTo(1));
                Assert.That(IsDescendantOf(primary, actionBar), Is.True);
                Assert.That(IsDescendantOf(primary, scroll), Is.False,
                    "The primary action must stay visible while the setup details scroll.");
                Assert.That(IsDescendantOf(refresh, detailsContent), Is.True);
                Assert.That(IsDescendantOf(refresh, scroll), Is.True);
                Assert.That(IsDescendantOf(refresh, actionBar), Is.False);
                Assert.That(details.value, Is.False,
                    "Technical controls remain progressively disclosed by default.");
                Assert.That(primary.ClassListContains("bootstrap-button--primary"), Is.True);
                Assert.That(refresh.ClassListContains("bootstrap-button--primary"), Is.False);
                Assert.That(primary.focusable, Is.True);
                Assert.That(refresh.focusable, Is.True);
            });
        }

        [TestCase(BootstrapSetupPhase.Loading, false)]
        [TestCase(BootstrapSetupPhase.Installing, true)]
        [TestCase(BootstrapSetupPhase.WaitingForUnity, true)]
        [TestCase(BootstrapSetupPhase.Verifying, true)]
        public void BusyAndLoadingStates_HideTheActionBarAndOnlyBusyPhasesShowThePlan(
            BootstrapSetupPhase phase,
            bool expectPlan)
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);
            view.Render(BootstrapTestPresentationModels.Create(
                phase,
                BootstrapSetupAction.None,
                false));

            VisualElement plan = root.Q<VisualElement>("bootstrap-plan");
            VisualElement actionBar = root.Q<VisualElement>("bootstrap-action-bar");
            Label progress = root.Q<Label>(className: "bootstrap-progress-meta");

            AssertAll(() =>
            {
                Assert.That(actionBar.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(
                    plan.style.display.value,
                    Is.EqualTo(expectPlan ? DisplayStyle.Flex : DisplayStyle.None));
                Assert.That(
                    progress.style.display.value,
                    Is.EqualTo(
                        BootstrapViewContentPolicy.IsBusyPhase(phase)
                            ? DisplayStyle.Flex
                            : DisplayStyle.None));
            });
        }

        [Test]
        public void Review_ShowsThePlanAndExactlyOnePrimaryAction()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);
            view.Render(BootstrapTestPresentationModels.Create(
                BootstrapSetupPhase.Review,
                BootstrapSetupAction.Repair,
                true));

            VisualElement plan = root.Q<VisualElement>("bootstrap-plan");
            VisualElement actionBar = root.Q<VisualElement>("bootstrap-action-bar");
            Button primary = root.Q<Button>("bootstrap-primary-action");
            Label primaryLabel = primary.Q<Label>(className: "bootstrap-button__label");

            AssertAll(() =>
            {
                Assert.That(plan.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(actionBar.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(CountNamedElements(root, "bootstrap-primary-action"), Is.EqualTo(1));
                Assert.That(primary.enabledSelf, Is.True);
                Assert.That(primaryLabel.text, Is.EqualTo("Repair"));
            });
        }

        [Test]
        public void Healthy_HidesThePlanAndShowsOnlyTheHandoffAction()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);
            view.Render(BootstrapTestPresentationModels.Create(
                BootstrapSetupPhase.Healthy,
                BootstrapSetupAction.OpenPackageInstaller,
                true));

            VisualElement plan = root.Q<VisualElement>("bootstrap-plan");
            VisualElement actionBar = root.Q<VisualElement>("bootstrap-action-bar");
            Button primary = root.Q<Button>("bootstrap-primary-action");
            Label primaryLabel = primary.Q<Label>(className: "bootstrap-button__label");

            AssertAll(() =>
            {
                Assert.That(plan.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(actionBar.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(CountNamedElements(root, "bootstrap-primary-action"), Is.EqualTo(1));
                Assert.That(primary.enabledSelf, Is.True);
                Assert.That(primaryLabel.text, Is.EqualTo("Open Package Installer"));
            });
        }

        [Test]
        public void View_DoesNotBuildTheLegacyDuplicateProgressActionSummaryOrStepDetails()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);
            view.Render(BootstrapTestPresentationModels.Create(
                BootstrapSetupPhase.Review,
                BootstrapSetupAction.Repair,
                true));

            AssertAll(() =>
            {
                Assert.That(root.Q<VisualElement>("bootstrap-progress-surface"), Is.Null);
                Assert.That(root.Q<VisualElement>("bootstrap-action-summary"), Is.Null);
                Assert.That(root.Q<Label>(className: "bootstrap-step__detail"), Is.Null);
                Assert.That(root.Q<VisualElement>("bootstrap-hero"), Is.Not.Null);
                Assert.That(root.Q<VisualElement>("bootstrap-plan"), Is.Not.Null);
                Assert.That(root.Q<Label>(className: "bootstrap-progress-meta"), Is.Not.Null);
            });
        }

        [Test]
        public void RepeatedRendering_DoesNotDuplicateOrRelocatePrimaryControls()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);
            BootstrapPresentationModel model = BootstrapTestPresentationModels.Create(
                BootstrapSetupPhase.Review,
                BootstrapSetupAction.Repair,
                true);

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
        public void RefreshPrimaryHidesTheDetailsRefreshControlWithoutDuplicatingActions()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);
            view.Render(BootstrapPresentationModelFactory.Create(
                BootstrapPresentationContractTestsSnapshot.RefreshRequired()));

            Button refresh = root.Q<Button>("bootstrap-refresh-button");
            Button primary = root.Q<Button>("bootstrap-primary-action");
            VisualElement actionBar = root.Q<VisualElement>("bootstrap-action-bar");
            AssertAll(() =>
            {
                Assert.That(refresh.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(primary.style.display.value, Is.Not.EqualTo(DisplayStyle.None));
                Assert.That(actionBar.style.display.value, Is.EqualTo(DisplayStyle.Flex));
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

    [TestFixture]
    internal sealed class BootstrapViewContentPolicyContractTests
    {
        [TestCase(BootstrapSetupPhase.Loading, false)]
        [TestCase(BootstrapSetupPhase.Review, true)]
        [TestCase(BootstrapSetupPhase.Installing, true)]
        [TestCase(BootstrapSetupPhase.WaitingForUnity, true)]
        [TestCase(BootstrapSetupPhase.Verifying, true)]
        [TestCase(BootstrapSetupPhase.Healthy, false)]
        [TestCase(BootstrapSetupPhase.Failed, true)]
        public void PlanVisibility_IsAStateOnlyMinimalContentDecision(
            BootstrapSetupPhase phase,
            bool expected)
        {
            BootstrapPresentationModel model = BootstrapTestPresentationModels.Create(
                phase,
                phase == BootstrapSetupPhase.Healthy
                    ? BootstrapSetupAction.OpenPackageInstaller
                    : BootstrapSetupAction.Repair,
                phase == BootstrapSetupPhase.Healthy || phase == BootstrapSetupPhase.Review);

            Assert.That(BootstrapViewContentPolicy.ShouldShowPlan(model), Is.EqualTo(expected));
        }

        [TestCase(BootstrapSetupPhase.Loading, false)]
        [TestCase(BootstrapSetupPhase.Review, false)]
        [TestCase(BootstrapSetupPhase.Installing, true)]
        [TestCase(BootstrapSetupPhase.WaitingForUnity, true)]
        [TestCase(BootstrapSetupPhase.Verifying, true)]
        [TestCase(BootstrapSetupPhase.Healthy, false)]
        [TestCase(BootstrapSetupPhase.Failed, false)]
        public void BusyPhasePolicy_OnlyMarksDurableOperationPhases(
            BootstrapSetupPhase phase,
            bool expected)
        {
            Assert.That(BootstrapViewContentPolicy.IsBusyPhase(phase), Is.EqualTo(expected));
        }

        [Test]
        public void InstallingCopy_FocusesTheCurrentStepAndOneCompactProgressLine()
        {
            BootstrapPresentationModel model = BootstrapTestPresentationModels.Create(
                BootstrapSetupPhase.Installing,
                BootstrapSetupAction.None,
                false);

            AssertAll(() =>
            {
                Assert.That(BootstrapViewContentPolicy.GetHeroTitle(model),
                    Is.EqualTo("Installing Logging"));
                Assert.That(BootstrapViewContentPolicy.GetProgressText(model),
                    Is.EqualTo("Step 2 of 3"));
                Assert.That(BootstrapViewContentPolicy.GetContextText(model), Is.Empty);
            });
        }

        [Test]
        public void ContextPolicy_OnlySurfacesHealthyReviewOrFailureEssentials()
        {
            BootstrapPresentationModel healthy = BootstrapTestPresentationModels.Create(
                BootstrapSetupPhase.Healthy,
                BootstrapSetupAction.OpenPackageInstaller,
                true);
            BootstrapPresentationModel reviewRequired = BootstrapTestPresentationModels.Create(
                BootstrapSetupPhase.ReviewRequired,
                BootstrapSetupAction.Refresh,
                true);
            BootstrapPresentationModel failed = BootstrapTestPresentationModels.Create(
                BootstrapSetupPhase.Failed,
                BootstrapSetupAction.Refresh,
                true);

            AssertAll(() =>
            {
                Assert.That(BootstrapViewContentPolicy.GetContextText(healthy),
                    Is.EqualTo(healthy.InstalledSummary));
                Assert.That(BootstrapViewContentPolicy.GetContextText(reviewRequired),
                    Is.EqualTo(reviewRequired.StatusText));
                Assert.That(BootstrapViewContentPolicy.GetContextText(failed),
                    Is.EqualTo(failed.StatusText));
            });
        }

        [Test]
        public void ActionAndStepPolicies_UseLucideAndConciseSemanticLabels()
        {
            AssertAll(() =>
            {
                Assert.That(
                    BootstrapViewContentPolicy.GetActionIconClass(BootstrapSetupAction.Install),
                    Is.EqualTo("bootstrap-icon--install"));
                Assert.That(
                    BootstrapViewContentPolicy.GetActionIconClass(
                        BootstrapSetupAction.OpenPackageInstaller),
                    Is.EqualTo("bootstrap-icon--open"));
                Assert.That(
                    BootstrapViewContentPolicy.GetStepClass(
                        BootstrapStepPresentationState.Current),
                    Is.EqualTo("bootstrap-step--current"));
                Assert.That(
                    BootstrapViewContentPolicy.GetStepStateLabel(
                        BootstrapStepPresentationState.Complete),
                    Is.EqualTo("Done"));
                Assert.That(
                    BootstrapViewContentPolicy.GetStepStateLabel(
                        BootstrapStepPresentationState.Failed),
                    Is.EqualTo("Needs attention"));
            });
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }

    internal static class BootstrapTestPresentationModels
    {
        public static BootstrapPresentationModel Create(
            BootstrapSetupPhase phase,
            BootstrapSetupAction action,
            bool primaryActionEnabled)
        {
            bool busy = BootstrapViewContentPolicy.IsBusyPhase(phase);
            BootstrapStepPresentationState[] states = ResolveStepStates(phase);
            BootstrapStepPresentation[] steps =
            {
                Step(1, "Editor", states[0]),
                Step(2, "Logging", states[1]),
                Step(3, "Package Installer", states[2])
            };

            BootstrapPresentationTone tone = ResolveTone(phase);
            string status = tone == BootstrapPresentationTone.Error
                ? "Setup stopped before another package change."
                : phase == BootstrapSetupPhase.ReviewRequired
                    ? "Remote revision could not be verified."
                    : "Ready";

            return new BootstrapPresentationModel(
                BootstrapChannel.Stable,
                phase,
                ResolveTitle(phase),
                "A concise setup and repair state.",
                status,
                tone,
                phase == BootstrapSetupPhase.Healthy
                    ? "bootstrap-icon--success"
                    : busy
                        ? "bootstrap-icon--loading"
                        : "bootstrap-icon--repair",
                action,
                ResolveActionLabel(action),
                "Run the current primary action.",
                primaryActionEnabled,
                phase != BootstrapSetupPhase.Loading && !busy,
                string.Empty,
                string.Empty,
                steps,
                new[]
                {
                    new BootstrapDetailPresentation(
                        "Target source",
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl)
                },
                string.Empty,
                CountComplete(states),
                "v1.1.83 - Git #main - 8edb9125");
        }

        private static BootstrapStepPresentation Step(
            int number,
            string title,
            BootstrapStepPresentationState state)
        {
            return new BootstrapStepPresentation(
                number,
                title,
                "Durable setup step detail.",
                "com.deucarian." + title.ToLowerInvariant().Replace(" ", "-"),
                state);
        }

        private static BootstrapStepPresentationState[] ResolveStepStates(
            BootstrapSetupPhase phase)
        {
            if (phase == BootstrapSetupPhase.Healthy || phase == BootstrapSetupPhase.Verifying)
            {
                return new[]
                {
                    BootstrapStepPresentationState.Complete,
                    BootstrapStepPresentationState.Complete,
                    BootstrapStepPresentationState.Complete
                };
            }

            if (phase == BootstrapSetupPhase.Installing ||
                phase == BootstrapSetupPhase.WaitingForUnity)
            {
                return new[]
                {
                    BootstrapStepPresentationState.Complete,
                    BootstrapStepPresentationState.Current,
                    BootstrapStepPresentationState.Pending
                };
            }

            if (phase == BootstrapSetupPhase.Failed)
            {
                return new[]
                {
                    BootstrapStepPresentationState.Complete,
                    BootstrapStepPresentationState.Failed,
                    BootstrapStepPresentationState.Pending
                };
            }

            return new[]
            {
                BootstrapStepPresentationState.Ready,
                BootstrapStepPresentationState.Pending,
                BootstrapStepPresentationState.Pending
            };
        }

        private static BootstrapPresentationTone ResolveTone(BootstrapSetupPhase phase)
        {
            if (phase == BootstrapSetupPhase.Healthy) return BootstrapPresentationTone.Success;
            if (phase == BootstrapSetupPhase.Failed) return BootstrapPresentationTone.Error;
            if (phase == BootstrapSetupPhase.Review ||
                phase == BootstrapSetupPhase.ReviewRequired)
            {
                return BootstrapPresentationTone.Warning;
            }

            return BootstrapPresentationTone.Info;
        }

        private static string ResolveTitle(BootstrapSetupPhase phase)
        {
            switch (phase)
            {
                case BootstrapSetupPhase.Loading: return "Checking setup";
                case BootstrapSetupPhase.Installing: return "Installing setup";
                case BootstrapSetupPhase.WaitingForUnity: return "Waiting for Unity";
                case BootstrapSetupPhase.Verifying: return "Verifying setup";
                case BootstrapSetupPhase.Healthy: return "Setup is healthy";
                case BootstrapSetupPhase.Failed: return "Setup stopped";
                default: return "Setup needs repair";
            }
        }

        private static string ResolveActionLabel(BootstrapSetupAction action)
        {
            switch (action)
            {
                case BootstrapSetupAction.Install: return "Install";
                case BootstrapSetupAction.Repair: return "Repair";
                case BootstrapSetupAction.SwitchChannel: return "Switch Channel";
                case BootstrapSetupAction.Migrate: return "Migrate";
                case BootstrapSetupAction.Refresh: return "Refresh Status";
                case BootstrapSetupAction.OpenPackageInstaller: return "Open Package Installer";
                default: return "Working...";
            }
        }

        private static int CountComplete(IEnumerable<BootstrapStepPresentationState> states)
        {
            return states.Count(state => state == BootstrapStepPresentationState.Complete);
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
