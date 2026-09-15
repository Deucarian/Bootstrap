using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Deucarian.Bootstrap.Editor.Tests
{
    [TestFixture]
    internal sealed class BootstrapVisualLayoutContractTests
    {
        private static readonly IReadOnlyList<LayoutCase> Cases = BuildCases();

        [UnityTest]
        public IEnumerator TechnicalDetailsRemainReachableWithoutOccupyingTheInitialPage()
        {
            int originalScale = EditorPrefs.GetInt(BootstrapSetupShell.ScaleKey, 100);
            var window = ScriptableObject.CreateInstance<BootstrapLayoutHostWindow>();
            try
            {
                window.Configure(new LayoutCase("Details interaction", VisualState.Review, 820, 650, true,
                    BootstrapResponsiveMode.Narrow, false, 150));
                yield return null; yield return null;
                var root = window.rootVisualElement;
                var details = root.Q("bootstrap-details");
                var button = root.Q<Button>(className: "bootstrap-page-details-button");
                Assert.That(details.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                button.Focus();
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
                for (int i = 0; i < 8; i++) yield return null;
                Assert.That(details.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q<Foldout>("bootstrap-details-foldout").value, Is.True);
                Assert.That(root.Q<Toggle>("bootstrap-startup-toggle"), Is.Not.Null);
                Assert.That(details.worldBound.yMin, Is.LessThan(window.View.ContentScroll.contentViewport.worldBound.yMax));
                Assert.That(window.View.PrimaryButton.enabledInHierarchy, Is.True, "Showing details does not start an operation.");
            }
            finally { EditorPrefs.SetInt(BootstrapSetupShell.ScaleKey, originalScale); window.Close(); }
        }

        [UnityTest]
        public IEnumerator DestinationFirstStatesRemainContainedAndNonOverlapping()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("UI Toolkit window geometry requires a graphics device.");
            }

            int originalScale = EditorPrefs.GetInt(BootstrapSetupShell.ScaleKey, 100);
            try
            {
            foreach (LayoutCase layoutCase in Cases)
            {
                BootstrapLayoutHostWindow window =
                    ScriptableObject.CreateInstance<BootstrapLayoutHostWindow>();
                try
                {
                    window.Configure(layoutCase);
                    yield return null;
                    yield return null;

                    AssertLayout(window, layoutCase);
                }
                finally
                {
                    window.Close();
                }
            }
            }
            finally { EditorPrefs.SetInt(BootstrapSetupShell.ScaleKey, originalScale); }
        }

        private static void AssertLayout(
            BootstrapLayoutHostWindow window,
            LayoutCase layoutCase)
        {
            VisualElement root = window.rootVisualElement;
            VisualElement shell = Required(root, "bootstrap-canvas", layoutCase);
            VisualElement header = Required(root, "bootstrap-workspace-header", layoutCase);
            VisualElement brand = Required(root, "bootstrap-brand-icon", layoutCase);
            VisualElement channel = Required(root, "bootstrap-channel", layoutCase);
            ScrollView scroll = root.Q<ScrollView>("bootstrap-content-scroll");
            Assert.That(scroll, Is.Not.Null, layoutCase.Name);
            VisualElement viewport = scroll.contentViewport;
            VisualElement content = Required(root, "bootstrap-content", layoutCase);
            VisualElement hero = Required(root, "bootstrap-hero", layoutCase);
            VisualElement details = Required(root, "bootstrap-details", layoutCase);
            VisualElement actionBar = Required(root, "bootstrap-action-bar", layoutCase);
            VisualElement scaleFooter = Required(root, "bootstrap-scale-footer", layoutCase);

            AssertAll(() =>
            {
                Assert.That(window.View.ResponsiveMode, Is.EqualTo(layoutCase.Mode),
                    layoutCase.Name);
                Assert.That(root.ClassListContains(layoutCase.Dark
                    ? "deucarian-bootstrap--dark"
                    : "deucarian-bootstrap--light"), Is.True, layoutCase.Name);
                Assert.That(root.ClassListContains(
                    BootstrapResponsiveLayout.ShortHeightClassName),
                    Is.EqualTo(layoutCase.Height <
                        BootstrapResponsiveLayout.ShortHeightBreakpoint),
                    layoutCase.Name);
                Assert.That(root.worldBound.width, Is.GreaterThan(0f), layoutCase.Name);
                Assert.That(root.worldBound.height, Is.GreaterThan(0f), layoutCase.Name);
                AssertContained(shell.worldBound, root.worldBound, layoutCase, "shell");
                AssertContained(header.worldBound, shell.worldBound, layoutCase, "header");
                AssertContained(brand.worldBound, header.worldBound, layoutCase, "brand");
                AssertHorizontallyContained(channel.worldBound, content.worldBound, layoutCase, "channel");
                AssertContained(scroll.worldBound, shell.worldBound, layoutCase, "scroll");
                AssertHorizontallyContained(actionBar.worldBound, content.worldBound, layoutCase, "actions");
                AssertContained(scaleFooter.worldBound, root.worldBound, layoutCase, "scale footer");
                Assert.That(scroll.worldBound.yMax,
                    Is.LessThanOrEqualTo(scaleFooter.worldBound.yMin + 0.5f),
                    layoutCase.Name + " scrolling body overlaps fixed footer");
                Assert.That(scaleFooter.worldBound.height, Is.EqualTo(70f).Within(0.5f), layoutCase.Name);
                AssertHorizontallyContained(content.worldBound, viewport.worldBound,
                    layoutCase, "content");
                AssertHorizontallyContained(hero.worldBound, viewport.worldBound,
                    layoutCase, "hero");
                AssertNotDisplayed(details, layoutCase);
            });

            switch (layoutCase.State)
            {
                case VisualState.Review:
                    AssertReview(root, viewport, hero, details, actionBar, layoutCase);
                    break;
                case VisualState.Installing:
                    AssertInstalling(root, viewport, hero, details, actionBar, layoutCase);
                    break;
                case VisualState.Healthy:
                    AssertHealthy(root, viewport, hero, details, actionBar, layoutCase);
                    break;
            }
        }

        private static void AssertReview(
            VisualElement root,
            VisualElement viewport,
            VisualElement hero,
            VisualElement details,
            VisualElement actionBar,
            LayoutCase layoutCase)
        {
            VisualElement flow = Required(root, "bootstrap-setup-flow", layoutCase);
            AssertDisplayed(flow, layoutCase);
            AssertNotDisplayed(Required(root, "bootstrap-completion-receipt", layoutCase),
                layoutCase);
            AssertFlowGeometry(root, viewport, flow, false, layoutCase);
            AssertActionGeometry(root, actionBar, "Set up Deucarian", layoutCase);
            AssertOrderedSections(new[] { hero, flow }, layoutCase);
            AssertInitialContentVisibility(viewport, new[] { hero, flow, actionBar }, layoutCase);
        }

        private static void AssertInstalling(
            VisualElement root,
            VisualElement viewport,
            VisualElement hero,
            VisualElement details,
            VisualElement actionBar,
            LayoutCase layoutCase)
        {
            VisualElement flow = Required(root, "bootstrap-setup-flow", layoutCase);
            AssertDisplayed(flow, layoutCase);
            Assert.That(flow.ClassListContains("bootstrap-setup-flow--busy"), Is.True,
                layoutCase.Name);
            AssertNotDisplayed(Required(root, "bootstrap-completion-receipt", layoutCase),
                layoutCase);
            AssertFlowGeometry(root, viewport, flow, true, layoutCase);
            AssertPassiveGeometry(root, actionBar, layoutCase);
            AssertOrderedSections(new[] { hero, flow }, layoutCase);
            AssertInitialContentVisibility(viewport, new[] { hero, flow, actionBar }, layoutCase);
        }

        private static void AssertHealthy(
            VisualElement root,
            VisualElement viewport,
            VisualElement hero,
            VisualElement details,
            VisualElement actionBar,
            LayoutCase layoutCase)
        {
            AssertNotDisplayed(Required(root, "bootstrap-setup-flow", layoutCase), layoutCase);
            VisualElement receipt = Required(
                root,
                "bootstrap-completion-receipt",
                layoutCase);
            AssertDisplayed(receipt, layoutCase);
            AssertReceiptGeometry(root, viewport, receipt, layoutCase);
            AssertActionGeometry(root, actionBar, "Open Package Installer", layoutCase);
            AssertOrderedSections(new[] { hero, receipt }, layoutCase);
            AssertInitialContentVisibility(viewport, new[] { hero, receipt, actionBar }, layoutCase);
        }

        private static void AssertFlowGeometry(
            VisualElement root,
            VisualElement viewport,
            VisualElement flow,
            bool busy,
            LayoutCase layoutCase)
        {
            VisualElement[] items =
            {
                Required(root, "bootstrap-setup-item-1", layoutCase),
                Required(root, "bootstrap-setup-item-2", layoutCase),
                Required(root, "bootstrap-setup-item-3", layoutCase)
            };

            AssertAll(() =>
            {
                AssertHorizontallyContained(flow.worldBound, viewport.worldBound,
                    layoutCase, "setup flow");
                Assert.That(items[0].ClassListContains(
                    "bootstrap-setup-item--requirement"), Is.True, layoutCase.Name);
                Assert.That(items[1].ClassListContains(
                    "bootstrap-setup-item--requirement"), Is.True, layoutCase.Name);
                Assert.That(items[2].ClassListContains(
                    "bootstrap-setup-item--destination"), Is.True, layoutCase.Name);
                if (busy)
                {
                    Assert.That(items[1].ClassListContains(
                        "bootstrap-setup-item--current"), Is.True, layoutCase.Name);
                }
            });

            foreach (VisualElement item in items)
            {
                AssertDisplayed(item, layoutCase);
                AssertHorizontallyContained(item.worldBound, viewport.worldBound,
                    layoutCase, item.name);
                AssertItemInternals(root, item, layoutCase);
            }

            AssertPairwiseNonOverlapping(items, layoutCase, "setup items");
        }

        private static void AssertItemInternals(
            VisualElement root,
            VisualElement item,
            LayoutCase layoutCase)
        {
            string suffix = item.name.Substring(item.name.LastIndexOf('-') + 1);
            VisualElement marker = Required(
                root, "bootstrap-setup-item-marker-" + suffix, layoutCase);
            VisualElement copy = Required(
                root, "bootstrap-setup-item-copy-" + suffix, layoutCase);
            Label state = item.Q<Label>(className: "bootstrap-setup-item__state");

            AssertAll(() =>
            {
                Assert.That(state, Is.Not.Null, layoutCase.Name + " " + item.name);
                AssertContained(marker.worldBound, item.worldBound,
                    layoutCase, item.name + " marker");
                AssertContained(copy.worldBound, item.worldBound,
                    layoutCase, item.name + " copy");
                AssertContained(state.worldBound, item.worldBound,
                    layoutCase, item.name + " status");
                Assert.That(marker.worldBound.Overlaps(copy.worldBound), Is.False,
                    layoutCase.Name + " " + item.name + " marker overlaps copy");
                Assert.That(copy.worldBound.Overlaps(state.worldBound), Is.False,
                    layoutCase.Name + " " + item.name + " copy overlaps status");
            });
        }

        private static void AssertReceiptGeometry(
            VisualElement root,
            VisualElement viewport,
            VisualElement receipt,
            LayoutCase layoutCase)
        {
            VisualElement[] items =
            {
                Required(root, "bootstrap-receipt-item-1", layoutCase),
                Required(root, "bootstrap-receipt-item-2", layoutCase),
                Required(root, "bootstrap-receipt-item-3", layoutCase)
            };
            AssertHorizontallyContained(receipt.worldBound, viewport.worldBound,
                layoutCase, "receipt");
            foreach (VisualElement item in items)
            {
                AssertDisplayed(item, layoutCase);
                AssertHorizontallyContained(item.worldBound, viewport.worldBound,
                    layoutCase, item.name);
                string suffix = item.name.Substring(item.name.LastIndexOf('-') + 1);
                AssertContained(
                    Required(root, "bootstrap-receipt-icon-" + suffix, layoutCase).worldBound,
                    item.worldBound,
                    layoutCase,
                    item.name + " icon");
                AssertContained(
                    Required(root, "bootstrap-receipt-copy-" + suffix, layoutCase).worldBound,
                    item.worldBound,
                    layoutCase,
                    item.name + " copy");
            }

            AssertPairwiseNonOverlapping(items, layoutCase, "receipt items");
        }

        private static void AssertActionGeometry(
            VisualElement root,
            VisualElement actionBar,
            string expectedLabel,
            LayoutCase layoutCase)
        {
            VisualElement actions = Required(root, "bootstrap-action-actions", layoutCase);
            Button primary = root.Q<Button>("bootstrap-primary-action");
            Assert.That(primary, Is.Not.Null, layoutCase.Name);
            Label label = primary.Q<Label>(className: "bootstrap-button__label");
            Assert.That(label, Is.Not.Null, layoutCase.Name);

            AssertAll(() =>
            {
                AssertDisplayed(actionBar, layoutCase);
                AssertDisplayed(actions, layoutCase);
                AssertNotDisplayed(Required(root, "bootstrap-passive-footer", layoutCase),
                    layoutCase);
                AssertContained(actions.worldBound, actionBar.worldBound,
                    layoutCase, "action area");
                AssertContained(primary.worldBound, actions.worldBound,
                    layoutCase, "primary button");
                AssertContained(label.worldBound, primary.worldBound,
                    layoutCase, "primary label");
                Assert.That(label.text, Is.EqualTo(expectedLabel), layoutCase.Name);
                Assert.That(primary.enabledSelf, Is.True, layoutCase.Name);
            });
        }

        private static void AssertPassiveGeometry(
            VisualElement root,
            VisualElement actionBar,
            LayoutCase layoutCase)
        {
            VisualElement passive = Required(root, "bootstrap-passive-footer", layoutCase);
            Label text = root.Q<Label>(className: "bootstrap-action-bar__passive-text");
            Assert.That(text, Is.Not.Null, layoutCase.Name);

            AssertAll(() =>
            {
                AssertDisplayed(actionBar, layoutCase);
                AssertDisplayed(passive, layoutCase);
                AssertNotDisplayed(Required(root, "bootstrap-action-actions", layoutCase),
                    layoutCase);
                Assert.That(actionBar.ClassListContains("bootstrap-action-bar--passive"),
                    Is.True, layoutCase.Name);
                AssertContained(passive.worldBound, actionBar.worldBound,
                    layoutCase, "passive footer");
                AssertContained(text.worldBound, passive.worldBound,
                    layoutCase, "passive footer text");
                Assert.That(text.text, Is.Not.Empty, layoutCase.Name);
                Assert.That(root.Q<Button>("bootstrap-primary-action").enabledSelf,
                    Is.False, layoutCase.Name);
            });
        }

        private static void AssertInitialContentVisibility(
            VisualElement viewport,
            VisualElement[] sections,
            LayoutCase layoutCase)
        {
            if (layoutCase.Width < 1280 || layoutCase.Height < 900 || layoutCase.Scale > 100)
            {
                return;
            }

            foreach (VisualElement section in sections)
            {
                Assert.That(
                    section.worldBound.yMin,
                    Is.GreaterThanOrEqualTo(viewport.worldBound.yMin - 0.5f),
                    layoutCase.Name + " " + section.name + " starts above viewport");
                Assert.That(
                    section.worldBound.yMax,
                    Is.LessThanOrEqualTo(viewport.worldBound.yMax + 0.5f),
                    layoutCase.Name + " " + section.name + " is below the initial viewport");
            }
        }

        private static void AssertOrderedSections(
            VisualElement[] sections,
            LayoutCase layoutCase)
        {
            AssertPairwiseNonOverlapping(sections, layoutCase, "content sections");
            for (int index = 1; index < sections.Length; index++)
            {
                Assert.That(sections[index - 1].worldBound.yMax,
                    Is.LessThanOrEqualTo(sections[index].worldBound.yMin + 0.5f),
                    layoutCase.Name + " content order");
            }
        }

        private static VisualElement Required(
            VisualElement root,
            string name,
            LayoutCase layoutCase)
        {
            VisualElement element = root.Q<VisualElement>(name);
            Assert.That(element, Is.Not.Null, layoutCase.Name + " missing " + name);
            return element;
        }

        private static void AssertDisplayed(VisualElement element, LayoutCase layoutCase)
        {
            Assert.That(element.style.display.value, Is.Not.EqualTo(DisplayStyle.None),
                layoutCase.Name + " " + element.name);
            Assert.That(element.worldBound.width, Is.GreaterThan(0f),
                layoutCase.Name + " " + element.name);
            Assert.That(element.worldBound.height, Is.GreaterThan(0f),
                layoutCase.Name + " " + element.name);
        }

        private static void AssertNotDisplayed(VisualElement element, LayoutCase layoutCase)
        {
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None),
                layoutCase.Name + " " + element.name);
        }

        private static void AssertContained(
            Rect inner,
            Rect outer,
            LayoutCase layoutCase,
            string elementName)
        {
            const float tolerance = 0.5f;
            Assert.That(inner.width, Is.GreaterThan(0f),
                layoutCase.Name + " " + elementName + " width");
            Assert.That(inner.height, Is.GreaterThan(0f),
                layoutCase.Name + " " + elementName + " height");
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - tolerance),
                layoutCase.Name + " " + elementName + " left clipping");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - tolerance),
                layoutCase.Name + " " + elementName + " top clipping");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + tolerance),
                layoutCase.Name + " " + elementName + " right clipping");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + tolerance),
                layoutCase.Name + " " + elementName + " bottom clipping");
        }

        private static void AssertHorizontallyContained(
            Rect inner,
            Rect outer,
            LayoutCase layoutCase,
            string elementName)
        {
            const float tolerance = 0.5f;
            Assert.That(inner.width, Is.GreaterThan(0f),
                layoutCase.Name + " " + elementName + " width");
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - tolerance),
                layoutCase.Name + " " + elementName + " left overflow");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + tolerance),
                layoutCase.Name + " " + elementName + " horizontal overflow");
        }

        private static void AssertPairwiseNonOverlapping(
            VisualElement[] elements,
            LayoutCase layoutCase,
            string group)
        {
            for (int index = 0; index < elements.Length; index++)
            {
                Assert.That(elements[index].worldBound.width, Is.GreaterThan(0f),
                    layoutCase.Name + " " + elements[index].name);
                Assert.That(elements[index].worldBound.height, Is.GreaterThan(0f),
                    layoutCase.Name + " " + elements[index].name);
                for (int other = index + 1; other < elements.Length; other++)
                {
                    Assert.That(
                        elements[index].worldBound.Overlaps(elements[other].worldBound),
                        Is.False,
                        layoutCase.Name + " " + group + ": " +
                        elements[index].name + " overlaps " + elements[other].name);
                }
            }
        }

        private static IReadOnlyList<LayoutCase> BuildCases()
        {
            List<LayoutCase> cases = new List<LayoutCase>();
            VisualState[] states =
            {
                VisualState.Review,
                VisualState.Installing,
                VisualState.Healthy
            };
            foreach (VisualState state in states)
            {
                AddSkins(cases, state, "Narrow", 560f, 820f,
                    BootstrapResponsiveMode.Narrow, false);
                AddSkins(cases, state, "Compact", 1024f, 720f,
                    BootstrapResponsiveMode.Compact, false);
                AddSkins(cases, state, "Wide", 1280f, 720f,
                    BootstrapResponsiveMode.Wide, false);
                AddSkins(cases, state, "Reference", 1586f, 940f,
                    BootstrapResponsiveMode.Wide, false);
                AddSkins(cases, state, "Minimum", 480f, 460f,
                    BootstrapResponsiveMode.Narrow, true);
            }

            return cases;
        }

        private static void AddSkins(
            ICollection<LayoutCase> cases,
            VisualState state,
            string sizeName,
            float width,
            float height,
            BootstrapResponsiveMode mode,
            bool minimum)
        {
            foreach (int scale in new[] { 75, 100, 125, 150 })
            {
            cases.Add(new LayoutCase(
                state + " " + sizeName + " Light",
                state,
                width,
                height,
                false,
                mode,
                minimum, scale));
            cases.Add(new LayoutCase(
                state + " " + sizeName + " Dark",
                state,
                width,
                height,
                true,
                mode,
                minimum, scale));
            }
        }

        private enum VisualState
        {
            Review,
            Installing,
            Healthy
        }

        private readonly struct LayoutCase
        {
            public LayoutCase(
                string name,
                VisualState state,
                float width,
                float height,
                bool dark,
                BootstrapResponsiveMode mode,
                bool isMinimum, int scale)
            {
                Name = name + " " + scale + "%";
                State = state;
                Width = width;
                Height = height;
                Dark = dark;
                Mode = mode;
                IsMinimum = isMinimum;
                Scale = scale;
            }

            public string Name { get; }
            public VisualState State { get; }
            public float Width { get; }
            public float Height { get; }
            public bool Dark { get; }
            public BootstrapResponsiveMode Mode { get; }
            public bool IsMinimum { get; }
            public int Scale { get; }

            public BootstrapSetupSnapshot Snapshot()
            {
                switch (State)
                {
                    case VisualState.Installing:
                        return BootstrapPresentationSnapshotFixtures.InstallingLogging();
                    case VisualState.Healthy:
                        return BootstrapPresentationSnapshotFixtures.Healthy();
                    default:
                        return BootstrapPresentationSnapshotFixtures.CleanReview();
                }
            }
        }

        private sealed class BootstrapLayoutHostWindow : EditorWindow
        {
            internal BootstrapSetupView View { get; private set; }

            internal void Configure(LayoutCase layoutCase)
            {
                EditorPrefs.SetInt(BootstrapSetupShell.ScaleKey, layoutCase.Scale);
                titleContent = new GUIContent(layoutCase.Name);
                position = new Rect(40f, 40f, layoutCase.Width, layoutCase.Height);
                minSize = new Vector2(layoutCase.Width, layoutCase.Height);
                maxSize = new Vector2(layoutCase.Width, layoutCase.Height);
                ShowUtility();

                View = new BootstrapSetupView(_ => { }, _ => { }, () => { }, _ => { });
                View.Build(rootVisualElement);
                View.Render(BootstrapPresentationModelFactory.Create(layoutCase.Snapshot()));
                View.SetSkin(layoutCase.Dark);
                View.ApplyResponsiveLayout(layoutCase.Width, layoutCase.Height);
                Repaint();
            }
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }
}
