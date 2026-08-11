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
        [Test]
        public void PrimaryActionsUseDestinationSpecificCopy()
        {
            AssertAction(
                BootstrapPresentationSnapshotFixtures.CleanReview(),
                BootstrapSetupAction.Install,
                "Install Package Installer");
            AssertAction(
                BootstrapPresentationSnapshotFixtures.MissingPackageInstaller(),
                BootstrapSetupAction.Repair,
                "Repair Package Installer");
            AssertAction(
                BootstrapPresentationSnapshotFixtures.WrongChannel(),
                BootstrapSetupAction.SwitchChannel,
                "Switch to Development");
            AssertAction(
                BootstrapPresentationSnapshotFixtures.LegacyMigration(),
                BootstrapSetupAction.Migrate,
                "Migrate Package Installer");
            AssertAction(
                BootstrapPresentationSnapshotFixtures.ReviewRequired(),
                BootstrapSetupAction.Refresh,
                "Refresh Status");
            AssertAction(
                BootstrapPresentationSnapshotFixtures.Healthy(),
                BootstrapSetupAction.OpenPackageInstaller,
                "Open Package Installer");
        }

        [Test]
        public void SetupFlowIdentifiesTwoRequirementsAndOneDestination()
        {
            BootstrapPresentationModel model = BootstrapPresentationModelFactory.Create(
                BootstrapPresentationSnapshotFixtures.CleanReview());

            AssertAll(() =>
            {
                Assert.That(model.Steps.Count, Is.EqualTo(3));
                AssertStep(
                    model.Steps[0],
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    "Editor",
                    BootstrapSetupItemRole.Requirement);
                AssertStep(
                    model.Steps[1],
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    "Logging",
                    BootstrapSetupItemRole.Requirement);
                AssertStep(
                    model.Steps[2],
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    "Package Installer",
                    BootstrapSetupItemRole.Destination);
                Assert.That(model.Steps[0].Label, Is.EqualTo("Will install"));
                Assert.That(model.Steps[1].Label, Is.EqualTo("Will install"));
                Assert.That(model.Steps[2].Label, Is.EqualTo("Will install last"));
                Assert.That(model.StateTitle, Does.Contain("Package Installer"));
                Assert.That(model.StateMessage, Does.Contain("Editor and Logging"));
            });
        }

        [Test]
        public void RepairScenariosKeepEveryDependencyVisibleAndMarkItsTruth()
        {
            BootstrapPresentationModel missingEditor = Create(
                BootstrapPresentationSnapshotFixtures.MissingEditor());
            BootstrapPresentationModel missingLogging = Create(
                BootstrapPresentationSnapshotFixtures.MissingLogging());
            BootstrapPresentationModel missingInstaller = Create(
                BootstrapPresentationSnapshotFixtures.MissingPackageInstaller());
            BootstrapPresentationModel wrongChannel = Create(
                BootstrapPresentationSnapshotFixtures.WrongChannel());
            BootstrapPresentationModel outdated = Create(
                BootstrapPresentationSnapshotFixtures.OutdatedRevision());
            BootstrapPresentationModel migration = Create(
                BootstrapPresentationSnapshotFixtures.LegacyMigration());

            AssertAll(() =>
            {
                Assert.That(missingEditor.Steps[0].State,
                    Is.EqualTo(BootstrapStepPresentationState.Pending));
                Assert.That(missingLogging.Steps[1].State,
                    Is.EqualTo(BootstrapStepPresentationState.Pending));
                Assert.That(missingInstaller.Steps[2].State,
                    Is.EqualTo(BootstrapStepPresentationState.Pending));
                Assert.That(wrongChannel.Steps[2].State,
                    Is.EqualTo(BootstrapStepPresentationState.Attention));
                Assert.That(wrongChannel.Steps[2].Label, Is.EqualTo("Wrong channel"));
                Assert.That(outdated.Steps[2].State,
                    Is.EqualTo(BootstrapStepPresentationState.Attention));
                Assert.That(outdated.Steps[2].Label, Is.EqualTo("Update needed"));
                Assert.That(migration.Steps[2].State,
                    Is.EqualTo(BootstrapStepPresentationState.Attention));
                Assert.That(migration.Steps[2].Label, Is.EqualTo("Migration needed"));
                Assert.That(new[]
                {
                    missingEditor, missingLogging, missingInstaller,
                    wrongChannel, outdated, migration
                }, Has.All.Matches<BootstrapPresentationModel>(value =>
                    value.ShowSetupFlow && value.Steps.Count == 3));
            });
        }

        [Test]
        public void PendingOperationKindProducesTruthfulCurrentStepCopy()
        {
            BootstrapPresentationModel addingEditor = Create(
                BootstrapPresentationSnapshotFixtures.InstallingEditor());
            BootstrapPresentationModel addingLogging = Create(
                BootstrapPresentationSnapshotFixtures.InstallingLogging());
            BootstrapPresentationModel addingInstaller = Create(
                BootstrapPresentationSnapshotFixtures.InstallingPackageInstaller());
            BootstrapPresentationModel removingLegacy = Create(
                BootstrapPresentationSnapshotFixtures.RemovingLegacyPackageInstaller());
            BootstrapPresentationModel waiting = Create(
                BootstrapPresentationSnapshotFixtures.WaitingForUnity());
            BootstrapPresentationModel verifying = Create(
                BootstrapPresentationSnapshotFixtures.Verifying());

            AssertAll(() =>
            {
                AssertCurrent(addingEditor, 0, "Installing");
                AssertCurrent(addingLogging, 1, "Installing");
                AssertCurrent(addingInstaller, 2, "Installing");
                AssertCurrent(removingLegacy, 2, "Removing legacy source");
                AssertCurrent(waiting, 1, "Waiting for Unity");
                AssertCurrent(verifying, 2, "Verifying");
                Assert.That(addingInstaller.Steps[2].Detail,
                    Does.Contain("selected Git channel"));
                Assert.That(removingLegacy.Steps[2].Detail,
                    Does.Contain("Removing the legacy"));
                Assert.That(waiting.Steps[1].Detail,
                    Is.EqualTo("Unity is resolving Logging."));
                Assert.That(verifying.Steps[2].Detail,
                    Does.Contain("source, channel, and lock revision"));
            });
        }

        [Test]
        public void LifecycleCompositionIsExclusiveAndActionSafe()
        {
            AssertComposition(
                BootstrapPresentationSnapshotFixtures.Loading(),
                false, false, false, false);
            AssertComposition(
                BootstrapPresentationSnapshotFixtures.CleanReview(),
                true, false, true, false);
            AssertComposition(
                BootstrapPresentationSnapshotFixtures.InstallingLogging(),
                true, false, false, true);
            AssertComposition(
                BootstrapPresentationSnapshotFixtures.WaitingForUnity(),
                true, false, false, true);
            AssertComposition(
                BootstrapPresentationSnapshotFixtures.Verifying(),
                true, false, false, true);
            AssertComposition(
                BootstrapPresentationSnapshotFixtures.Healthy(),
                false, true, true, false);
            AssertComposition(
                BootstrapPresentationSnapshotFixtures.ReviewRequired(),
                true, false, true, false);
            AssertComposition(
                BootstrapPresentationSnapshotFixtures.Failed(),
                true, false, true, false);
        }

        [Test]
        public void BusyFooterIsPassiveTruthfulAndNeverExposesAnAction()
        {
            BootstrapPresentationModel installing = Create(
                BootstrapPresentationSnapshotFixtures.InstallingLogging());
            BootstrapPresentationModel waiting = Create(
                BootstrapPresentationSnapshotFixtures.WaitingForUnity());
            BootstrapPresentationModel verifying = Create(
                BootstrapPresentationSnapshotFixtures.Verifying());

            AssertAll(() =>
            {
                AssertPassive(installing, "resumes after Unity reloads");
                AssertPassive(waiting, "continue automatically");
                AssertPassive(verifying, "source, channel, and lock revision");
                Assert.That(new[] { installing, waiting, verifying },
                    Has.All.Matches<BootstrapPresentationModel>(model =>
                        model.PrimaryAction == BootstrapSetupAction.None &&
                        string.IsNullOrEmpty(model.PrimaryActionLabel)));
            });
        }

        [Test]
        public void HealthyStateBecomesAThreeCheckReceiptAndDirectHandoff()
        {
            BootstrapPresentationModel model = Create(
                BootstrapPresentationSnapshotFixtures.Healthy());

            AssertAll(() =>
            {
                Assert.That(model.StateTitle, Is.EqualTo("Package Installer is ready"));
                Assert.That(model.ShowSetupFlow, Is.False);
                Assert.That(model.ShowCompletionReceipt, Is.True);
                Assert.That(model.Receipt.Count, Is.EqualTo(3));
                Assert.That(model.Receipt.Select(item => item.Title), Is.EqualTo(new[]
                {
                    "Editor", "Logging", "Package Installer"
                }));
                Assert.That(model.Receipt[0].Summary, Is.EqualTo("Requirement installed"));
                Assert.That(model.Receipt[1].Summary, Is.EqualTo("Requirement installed"));
                Assert.That(model.Receipt[2].Summary, Is.EqualTo("Destination ready"));
                Assert.That(model.InstalledSummary,
                    Is.EqualTo("v1.1.83 | Git #main | 0123456789ab"));
                Assert.That(model.PrimaryAction,
                    Is.EqualTo(BootstrapSetupAction.OpenPackageInstaller));
            });
        }

        [Test]
        public void FailureAndReviewRequiredOnlyOfferAReadOnlyRefresh()
        {
            BootstrapPresentationModel reviewRequired = Create(
                BootstrapPresentationSnapshotFixtures.ReviewRequired());
            BootstrapPresentationModel failed = Create(
                BootstrapPresentationSnapshotFixtures.Failed());

            AssertAll(() =>
            {
                Assert.That(reviewRequired.PrimaryAction,
                    Is.EqualTo(BootstrapSetupAction.Refresh));
                Assert.That(failed.PrimaryAction,
                    Is.EqualTo(BootstrapSetupAction.Refresh));
                Assert.That(reviewRequired.PrimaryActionLabel, Is.EqualTo("Refresh Status"));
                Assert.That(failed.PrimaryActionLabel, Is.EqualTo("Refresh Status"));
                Assert.That(reviewRequired.Steps[2].State,
                    Is.EqualTo(BootstrapStepPresentationState.Attention));
                Assert.That(failed.Steps[1].State,
                    Is.EqualTo(BootstrapStepPresentationState.Failed));
                Assert.That(failed.StatusText,
                    Is.EqualTo("Unity Package Manager could not add Logging."));
                Assert.That(reviewRequired.PrimaryActionTooltip,
                    Does.Contain("without changing packages"));
            });
        }

        [Test]
        public void DetectionFailureWithoutAValidatedPlanPreservesTheSetupPathTruth()
        {
            BootstrapPresentationModel model = Create(
                BootstrapPresentationSnapshotFixtures.DetectionFailureWithoutPlan());

            AssertAll(() =>
            {
                Assert.That(model.ShowSetupFlow, Is.True);
                Assert.That(model.Steps.Count, Is.EqualTo(3));
                Assert.That(model.Steps[0].Role,
                    Is.EqualTo(BootstrapSetupItemRole.Requirement));
                Assert.That(model.Steps[1].Role,
                    Is.EqualTo(BootstrapSetupItemRole.Requirement));
                Assert.That(model.Steps[2].Role,
                    Is.EqualTo(BootstrapSetupItemRole.Destination));
                Assert.That(model.Steps[2].State,
                    Is.EqualTo(BootstrapStepPresentationState.Failed));
                Assert.That(model.Steps[2].Label, Is.EqualTo("Status unavailable"));
                Assert.That(model.Steps[2].Detail,
                    Does.Contain("could not be confirmed"));
                Assert.That(model.Steps[2].TechnicalDetail,
                    Does.Contain("Exact Git reference unavailable"));
                Assert.That(model.PrimaryAction, Is.EqualTo(BootstrapSetupAction.Refresh));
                Assert.That(model.PrimaryActionLabel, Is.EqualTo("Refresh Status"));
            });
        }

        [Test]
        public void HealthyHandoffFailureKeepsTheReceiptAndOffersOnlyRefresh()
        {
            const string handoffError =
                "Package Installer is installed, but its menu is not available yet.";
            BootstrapPresentationModel model = BootstrapPresentationModelFactory.Create(
                BootstrapPresentationSnapshotFixtures.Healthy(),
                handoffError);

            AssertAll(() =>
            {
                Assert.That(model.StateTitle,
                    Is.EqualTo("Package Installer is still starting"));
                Assert.That(model.Tone, Is.EqualTo(BootstrapPresentationTone.Error));
                Assert.That(model.StatusText, Is.EqualTo(handoffError));
                Assert.That(model.ShowSetupFlow, Is.False);
                Assert.That(model.ShowCompletionReceipt, Is.True);
                Assert.That(model.Receipt.Count, Is.EqualTo(3));
                Assert.That(model.PrimaryAction, Is.EqualTo(BootstrapSetupAction.Refresh));
                Assert.That(model.PrimaryActionLabel, Is.EqualTo("Refresh Status"));
                Assert.That(model.IsActionVisible, Is.True);
                Assert.That(model.FooterIsPassive, Is.False);
                Assert.That(model.StateMessage, Does.Contain("No package changes"));
            });
        }

        [Test]
        public void BundledFallbackIsVisibleWithoutReplacingTechnicalRegistryTruth()
        {
            BootstrapPresentationModel model = Create(
                BootstrapPresentationSnapshotFixtures.BundledFallbackReview());

            AssertAll(() =>
            {
                Assert.That(model.OfflineNotice,
                    Does.Contain("exact bundled setup closure"));
                Assert.That(model.PrimaryAction,
                    Is.EqualTo(BootstrapSetupAction.Install));
                Assert.That(model.Steps.Count, Is.EqualTo(3));
                Assert.That(model.Details.Single(item => item.Label == "Registry source").Value,
                    Is.EqualTo("Bundled setup fallback"));
            });
        }

        [Test]
        public void AdvancedDetailsExposeFullRevisionHashes()
        {
            BootstrapPresentationModel model = Create(
                BootstrapPresentationSnapshotFixtures.Healthy());

            AssertAll(() =>
            {
                Assert.That(model.Details.Single(
                        detail => detail.Label == "Target branch revision").Value,
                    Is.EqualTo(BootstrapPresentationSnapshotFixtures.CurrentRevision));
                Assert.That(model.Details.Single(
                        detail => detail.Label == "Installed lock revision").Value,
                    Is.EqualTo(BootstrapPresentationSnapshotFixtures.CurrentRevision));
                Assert.That(model.Steps, Has.All.Matches<BootstrapStepPresentation>(step =>
                    step.TechnicalDetail.Contains(step.PackageId) &&
                    step.TechnicalDetail.Contains("github.com")));
            });
        }

        [Test]
        public void PresentationCollectionsAreReadOnlySnapshots()
        {
            BootstrapPresentationModel model = Create(
                BootstrapPresentationSnapshotFixtures.Healthy());
            IList<BootstrapStepPresentation> steps =
                model.Steps as IList<BootstrapStepPresentation>;
            IList<BootstrapDetailPresentation> details =
                model.Details as IList<BootstrapDetailPresentation>;
            IList<BootstrapReceiptPresentation> receipt =
                model.Receipt as IList<BootstrapReceiptPresentation>;

            AssertAll(() =>
            {
                Assert.That(steps, Is.Not.Null);
                Assert.That(details, Is.Not.Null);
                Assert.That(receipt, Is.Not.Null);
                Assert.That(steps.IsReadOnly, Is.True);
                Assert.That(details.IsReadOnly, Is.True);
                Assert.That(receipt.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => steps.Add(model.Steps[0]));
                Assert.Throws<NotSupportedException>(() => details.Clear());
                Assert.Throws<NotSupportedException>(() => receipt.Clear());
            });
        }

        private static BootstrapPresentationModel Create(BootstrapSetupSnapshot snapshot)
        {
            return BootstrapPresentationModelFactory.Create(snapshot);
        }

        private static void AssertAction(
            BootstrapSetupSnapshot snapshot,
            BootstrapSetupAction expectedAction,
            string expectedLabel)
        {
            BootstrapPresentationModel model = Create(snapshot);
            AssertAll(() =>
            {
                Assert.That(model.PrimaryAction, Is.EqualTo(expectedAction));
                Assert.That(model.PrimaryActionLabel, Is.EqualTo(expectedLabel));
                Assert.That(model.PrimaryActionEnabled, Is.True);
                Assert.That(model.IsActionVisible, Is.True);
                Assert.That(model.PrimaryActionTooltip, Is.Not.Empty);
            });
        }

        private static void AssertStep(
            BootstrapStepPresentation step,
            string packageId,
            string title,
            BootstrapSetupItemRole role)
        {
            AssertAll(() =>
            {
                Assert.That(step.PackageId, Is.EqualTo(packageId));
                Assert.That(step.Title, Is.EqualTo(title));
                Assert.That(step.Role, Is.EqualTo(role));
                Assert.That(step.Detail, Is.Not.Empty);
                Assert.That(step.TechnicalDetail, Does.Contain(packageId));
            });
        }

        private static void AssertCurrent(
            BootstrapPresentationModel model,
            int index,
            string expectedLabel)
        {
            Assert.That(model.Steps[index].State,
                Is.EqualTo(BootstrapStepPresentationState.Current));
            Assert.That(model.Steps[index].Label, Is.EqualTo(expectedLabel));
        }

        private static void AssertComposition(
            BootstrapSetupSnapshot snapshot,
            bool flow,
            bool receipt,
            bool action,
            bool passiveFooter)
        {
            BootstrapPresentationModel model = Create(snapshot);
            AssertAll(() =>
            {
                Assert.That(model.ShowSetupFlow, Is.EqualTo(flow), model.Phase.ToString());
                Assert.That(model.ShowCompletionReceipt, Is.EqualTo(receipt),
                    model.Phase.ToString());
                Assert.That(model.IsActionVisible, Is.EqualTo(action), model.Phase.ToString());
                Assert.That(model.FooterIsPassive, Is.EqualTo(passiveFooter),
                    model.Phase.ToString());
                Assert.That(model.ShowSetupFlow && model.ShowCompletionReceipt, Is.False,
                    model.Phase.ToString());
            });
        }

        private static void AssertPassive(
            BootstrapPresentationModel model,
            string expectedText)
        {
            Assert.That(model.FooterIsPassive, Is.True);
            Assert.That(model.IsActionVisible, Is.False);
            Assert.That(model.FooterText, Does.Contain(expectedText).IgnoreCase);
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
        public void WindowFootprintDefaultsToNarrowAndKeepsAUsableMinimum()
        {
            AssertAll(() =>
            {
                Assert.That(DeucarianBootstrapWindow.PreferredWindowWidth, Is.EqualTo(560f));
                Assert.That(DeucarianBootstrapWindow.PreferredWindowHeight, Is.EqualTo(820f));
                Assert.That(DeucarianBootstrapWindow.MinWindowWidth, Is.EqualTo(480f));
                Assert.That(DeucarianBootstrapWindow.MinWindowHeight, Is.EqualTo(460f));
                Assert.That(BootstrapResponsiveLayout.ResolveMode(560f),
                    Is.EqualTo(BootstrapResponsiveMode.Narrow));
                Assert.That(BootstrapResponsiveLayout.ResolveMode(480f),
                    Is.EqualTo(BootstrapResponsiveMode.Narrow));
            });
        }

        [TestCase(0f, BootstrapResponsiveMode.Narrow)]
        [TestCase(899.999f, BootstrapResponsiveMode.Narrow)]
        [TestCase(900f, BootstrapResponsiveMode.Compact)]
        [TestCase(1179.999f, BootstrapResponsiveMode.Compact)]
        [TestCase(1180f, BootstrapResponsiveMode.Wide)]
        [TestCase(1600f, BootstrapResponsiveMode.Wide)]
        public void BreakpointsResolveExactWorkbenchBoundaries(
            float width,
            BootstrapResponsiveMode expected)
        {
            Assert.That(BootstrapResponsiveLayout.ResolveMode(width), Is.EqualTo(expected));
        }

        [Test]
        public void LayoutPolicyExposesShortHeightWithoutChangingWidthMode()
        {
            BootstrapResponsiveLayoutState shortLayout =
                BootstrapResponsiveLayout.Calculate(480f, 599.999f);
            BootstrapResponsiveLayoutState regularLayout =
                BootstrapResponsiveLayout.Calculate(480f, 600f);

            AssertAll(() =>
            {
                Assert.That(BootstrapResponsiveLayout.NarrowBreakpoint, Is.EqualTo(900f));
                Assert.That(BootstrapResponsiveLayout.WideBreakpoint, Is.EqualTo(1180f));
                Assert.That(BootstrapResponsiveLayout.ShortHeightBreakpoint, Is.EqualTo(600f));
                Assert.That(shortLayout.Mode, Is.EqualTo(BootstrapResponsiveMode.Narrow));
                Assert.That(shortLayout.IsShortHeight, Is.True);
                Assert.That(regularLayout.IsShortHeight, Is.False);
                Assert.That(shortLayout.ActionBarMinimumHeight, Is.GreaterThan(0f));
                Assert.That(shortLayout.AvailableBodyHeight, Is.GreaterThan(0f));
                Assert.That(BootstrapResponsiveLayout.ResolveMode(float.NaN),
                    Is.EqualTo(BootstrapResponsiveMode.Narrow));
            });
        }

        [Test]
        public void ViewBuildsAnOpenDestinationFirstHierarchy()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);

            VisualElement hero = root.Q<VisualElement>("bootstrap-hero");
            VisualElement channel = root.Q<VisualElement>("bootstrap-channel");
            VisualElement flow = root.Q<VisualElement>("bootstrap-setup-flow");
            VisualElement receipt = root.Q<VisualElement>("bootstrap-completion-receipt");
            Foldout details = root.Q<Foldout>("bootstrap-details-foldout");
            VisualElement actionBar = root.Q<VisualElement>("bootstrap-action-bar");
            Button primary = root.Q<Button>("bootstrap-primary-action");
            Button refresh = root.Q<Button>("bootstrap-refresh-button");

            AssertAll(() =>
            {
                Assert.That(root.Q<VisualElement>("bootstrap-shell"), Is.Not.Null);
                Assert.That(root.Q<ScrollView>("bootstrap-content-scroll"), Is.Not.Null);
                Assert.That(hero, Is.Not.Null);
                Assert.That(hero.ClassListContains("bootstrap-surface"), Is.False);
                Assert.That(channel, Is.Not.Null);
                Assert.That(channel.ClassListContains("bootstrap-surface"), Is.False);
                Assert.That(flow, Is.Not.Null);
                Assert.That(receipt, Is.Not.Null);
                Assert.That(details, Is.Not.Null);
                Assert.That(details.value, Is.False);
                Assert.That(primary, Is.Not.Null);
                Assert.That(refresh, Is.Not.Null);
                Assert.That(IsDescendantOf(primary, actionBar), Is.True);
                Assert.That(IsDescendantOf(refresh,
                    root.Q<VisualElement>("bootstrap-details-content")), Is.True);
                Assert.That(root.Q<VisualElement>("bootstrap-plan"), Is.Null);
                Assert.That(root.Q<VisualElement>("bootstrap-progress-surface"), Is.Null);
                Assert.That(root.Q<VisualElement>("bootstrap-action-summary"), Is.Null);
                Assert.That(root.Q<Label>(className: "bootstrap-step__detail"), Is.Null);
            });
        }

        [Test]
        public void ViewUsesExclusiveWidthSkinAndShortHeightClasses()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);

            view.SetSkin(false);
            Assert.That(root.ClassListContains("deucarian-bootstrap--light"), Is.True);
            Assert.That(root.ClassListContains("deucarian-bootstrap--dark"), Is.False);
            view.SetSkin(true);
            Assert.That(root.ClassListContains("deucarian-bootstrap--dark"), Is.True);
            Assert.That(root.ClassListContains("deucarian-bootstrap--light"), Is.False);

            AssertResponsiveClass(view, root, 899.999f,
                BootstrapResponsiveLayout.NarrowClassName);
            AssertResponsiveClass(view, root, 900f,
                BootstrapResponsiveLayout.CompactClassName);
            AssertResponsiveClass(view, root, 1180f,
                BootstrapResponsiveLayout.WideClassName);

            view.ApplyResponsiveLayout(480f, 459f);
            Assert.That(root.ClassListContains(BootstrapResponsiveLayout.ShortHeightClassName),
                Is.True);
            view.ApplyResponsiveLayout(480f, 600f);
            Assert.That(root.ClassListContains(BootstrapResponsiveLayout.ShortHeightClassName),
                Is.False);
            Assert.That(root.Q<Label>(className: "bootstrap-summary__title"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("bootstrap-details"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("bootstrap-action-bar"), Is.Not.Null);
        }

        [Test]
        public void LoadingRendersOnlyTheCalmHeroWithoutAnActionFooter()
        {
            RenderedView rendered = Render(BootstrapPresentationSnapshotFixtures.Loading());

            AssertAll(() =>
            {
                AssertHidden(rendered.Root, "bootstrap-setup-flow");
                AssertHidden(rendered.Root, "bootstrap-completion-receipt");
                AssertHidden(rendered.Root, "bootstrap-details");
                AssertHidden(rendered.Root, "bootstrap-action-bar");
                AssertHidden(rendered.Root, "bootstrap-passive-footer");
                AssertHidden(rendered.Root, "bootstrap-action-actions");
                Assert.That(rendered.Root.Q<Label>(className: "bootstrap-summary__title").text,
                    Is.EqualTo("Checking Package Installer setup"));
            });
        }

        [Test]
        public void ReviewRendersThreeTransformingItemsAndOneAction()
        {
            RenderedView rendered = Render(
                BootstrapPresentationSnapshotFixtures.CleanReview());
            VisualElement requirementOne = rendered.Root.Q<VisualElement>(
                "bootstrap-setup-item-1");
            VisualElement requirementTwo = rendered.Root.Q<VisualElement>(
                "bootstrap-setup-item-2");
            VisualElement destination = rendered.Root.Q<VisualElement>(
                "bootstrap-setup-item-3");

            AssertAll(() =>
            {
                AssertVisible(rendered.Root, "bootstrap-setup-flow");
                AssertHidden(rendered.Root, "bootstrap-completion-receipt");
                AssertVisible(rendered.Root, "bootstrap-details");
                AssertVisible(rendered.Root, "bootstrap-action-actions");
                AssertHidden(rendered.Root, "bootstrap-passive-footer");
                Assert.That(requirementOne.ClassListContains(
                    "bootstrap-setup-item--requirement"), Is.True);
                Assert.That(requirementTwo.ClassListContains(
                    "bootstrap-setup-item--requirement"), Is.True);
                Assert.That(destination.ClassListContains(
                    "bootstrap-setup-item--destination"), Is.True);
                Assert.That(CountNamedElements(rendered.Root,
                    "bootstrap-primary-action"), Is.EqualTo(1));
                Assert.That(PrimaryLabel(rendered.Root),
                    Is.EqualTo("Install Package Installer"));
            });
        }

        [Test]
        public void BusyStateUsesAVisiblePassiveFooterAndNoClickableAction()
        {
            RenderedView rendered = Render(
                BootstrapPresentationSnapshotFixtures.InstallingLogging());

            AssertAll(() =>
            {
                AssertVisible(rendered.Root, "bootstrap-setup-flow");
                Assert.That(rendered.Root.Q<VisualElement>("bootstrap-setup-flow")
                    .ClassListContains("bootstrap-setup-flow--busy"), Is.True);
                AssertVisible(rendered.Root, "bootstrap-action-bar");
                AssertVisible(rendered.Root, "bootstrap-passive-footer");
                AssertHidden(rendered.Root, "bootstrap-action-actions");
                Assert.That(rendered.Root.Q<Button>("bootstrap-primary-action").enabledSelf,
                    Is.False);
                Assert.That(rendered.Root.Q<Label>(className: "bootstrap-progress-meta")
                    .style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(rendered.Root.Q<VisualElement>("bootstrap-setup-item-2")
                    .ClassListContains("bootstrap-setup-item--current"), Is.True);
            });
        }

        [Test]
        public void HealthyStateReplacesTheFlowWithAThreeCheckReceiptAndHandoff()
        {
            RenderedView rendered = Render(
                BootstrapPresentationSnapshotFixtures.Healthy());

            AssertAll(() =>
            {
                AssertHidden(rendered.Root, "bootstrap-setup-flow");
                AssertVisible(rendered.Root, "bootstrap-completion-receipt");
                Assert.That(rendered.Root.Q<VisualElement>("bootstrap-receipt-item-1"),
                    Is.Not.Null);
                Assert.That(rendered.Root.Q<VisualElement>("bootstrap-receipt-item-2"),
                    Is.Not.Null);
                Assert.That(rendered.Root.Q<VisualElement>("bootstrap-receipt-item-3"),
                    Is.Not.Null);
                AssertVisible(rendered.Root, "bootstrap-action-actions");
                AssertHidden(rendered.Root, "bootstrap-passive-footer");
                Assert.That(PrimaryLabel(rendered.Root),
                    Is.EqualTo("Open Package Installer"));
                Assert.That(rendered.Root.Q<VisualElement>("bootstrap-status-line")
                    .style.display.value, Is.EqualTo(DisplayStyle.Flex));
            });
        }

        [Test]
        public void FailureKeepsTheAffectedItemAndOnlyOffersRefresh()
        {
            RenderedView rendered = Render(BootstrapPresentationSnapshotFixtures.Failed());

            AssertAll(() =>
            {
                AssertVisible(rendered.Root, "bootstrap-setup-flow");
                Assert.That(rendered.Root.Q<VisualElement>("bootstrap-setup-item-2")
                    .ClassListContains("bootstrap-setup-item--failed"), Is.True);
                Assert.That(PrimaryLabel(rendered.Root), Is.EqualTo("Refresh Status"));
                AssertHidden(rendered.Root, "bootstrap-refresh-button");
                AssertVisible(rendered.Root, "bootstrap-action-actions");
                AssertHidden(rendered.Root, "bootstrap-passive-footer");
            });
        }

        [Test]
        public void RepeatedRenderingDoesNotDuplicateOrRelocatePrimaryControls()
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);
            BootstrapPresentationModel review = BootstrapPresentationModelFactory.Create(
                BootstrapPresentationSnapshotFixtures.CleanReview());

            view.Render(review);
            view.Render(BootstrapPresentationModelFactory.Create(
                BootstrapPresentationSnapshotFixtures.Healthy()));
            view.Render(review);

            Button primary = root.Q<Button>("bootstrap-primary-action");
            AssertAll(() =>
            {
                Assert.That(CountNamedElements(root, "bootstrap-primary-action"), Is.EqualTo(1));
                Assert.That(IsDescendantOf(primary,
                    root.Q<VisualElement>("bootstrap-action-bar")), Is.True);
                Assert.That(primary.enabledSelf, Is.True);
                Assert.That(primary.tooltip, Is.Not.Empty);
                Assert.That(root.Q<VisualElement>("bootstrap-setup-item-1"), Is.Not.Null);
                Assert.That(root.Q<VisualElement>("bootstrap-receipt-item-1"), Is.Null);
            });
        }

        private static RenderedView Render(BootstrapSetupSnapshot snapshot)
        {
            VisualElement root = new VisualElement();
            BootstrapSetupView view = CreateView();
            view.Build(root);
            view.Render(BootstrapPresentationModelFactory.Create(snapshot));
            return new RenderedView(root, view);
        }

        private static BootstrapSetupView CreateView()
        {
            return new BootstrapSetupView(_ => { }, _ => { }, () => { }, _ => { });
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
            Assert.That(responsiveClasses.Where(root.ClassListContains),
                Is.EqualTo(new[] { expectedClass }));
        }

        private static void AssertVisible(VisualElement root, string name)
        {
            VisualElement element = root.Q<VisualElement>(name);
            Assert.That(element, Is.Not.Null, name);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex), name);
        }

        private static void AssertHidden(VisualElement root, string name)
        {
            VisualElement element = root.Q<VisualElement>(name);
            Assert.That(element, Is.Not.Null, name);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None), name);
        }

        private static string PrimaryLabel(VisualElement root)
        {
            return root.Q<Button>("bootstrap-primary-action")
                .Q<Label>(className: "bootstrap-button__label").text;
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

        private readonly struct RenderedView
        {
            public RenderedView(VisualElement root, BootstrapSetupView view)
            {
                Root = root;
                View = view;
            }

            public VisualElement Root { get; }
            public BootstrapSetupView View { get; }
        }
    }

    [TestFixture]
    internal sealed class BootstrapViewContentPolicyContractTests
    {
        [TestCase(BootstrapSetupPhase.Loading, false)]
        [TestCase(BootstrapSetupPhase.Review, false)]
        [TestCase(BootstrapSetupPhase.Installing, true)]
        [TestCase(BootstrapSetupPhase.WaitingForUnity, true)]
        [TestCase(BootstrapSetupPhase.Verifying, true)]
        [TestCase(BootstrapSetupPhase.Healthy, false)]
        [TestCase(BootstrapSetupPhase.Failed, false)]
        public void BusyPhasePolicyOnlyMarksDurableOperationPhases(
            BootstrapSetupPhase phase,
            bool expected)
        {
            Assert.That(BootstrapViewContentPolicy.IsBusyPhase(phase), Is.EqualTo(expected));
        }

        [Test]
        public void InstallingHeroFocusesTheCurrentDependency()
        {
            BootstrapPresentationModel model = BootstrapPresentationModelFactory.Create(
                BootstrapPresentationSnapshotFixtures.InstallingLogging());

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
        public void ContextOnlySurfacesHealthyReviewOrFailureEssentials()
        {
            BootstrapPresentationModel healthy = BootstrapPresentationModelFactory.Create(
                BootstrapPresentationSnapshotFixtures.Healthy());
            BootstrapPresentationModel reviewRequired =
                BootstrapPresentationModelFactory.Create(
                    BootstrapPresentationSnapshotFixtures.ReviewRequired());
            BootstrapPresentationModel failed = BootstrapPresentationModelFactory.Create(
                BootstrapPresentationSnapshotFixtures.Failed());

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

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }
}
