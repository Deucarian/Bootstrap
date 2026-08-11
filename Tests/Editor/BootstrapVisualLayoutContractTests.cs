using System.Collections;
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
        private static readonly LayoutCase[] Cases =
        {
            new LayoutCase("Default Light", 560f, 820f, false, BootstrapResponsiveMode.Narrow),
            new LayoutCase("Default Dark", 560f, 820f, true, BootstrapResponsiveMode.Narrow),
            new LayoutCase("Minimum Light", 480f, 460f, false, BootstrapResponsiveMode.Narrow),
            new LayoutCase("Minimum Dark", 480f, 460f, true, BootstrapResponsiveMode.Narrow),
            new LayoutCase("Compact Light", 1024f, 640f, false, BootstrapResponsiveMode.Compact),
            new LayoutCase("Compact Dark", 1024f, 640f, true, BootstrapResponsiveMode.Compact),
            new LayoutCase("Wide Light", 1280f, 720f, false, BootstrapResponsiveMode.Wide),
            new LayoutCase("Wide Dark", 1280f, 720f, true, BootstrapResponsiveMode.Wide)
        };

        [UnityTest]
        public IEnumerator PrimaryControlsRemainVisibleAndNonOverlappingInEveryLayoutAndSkin()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("UI Toolkit window geometry requires a graphics device.");
            }

            foreach (LayoutCase layoutCase in Cases)
            {
                BootstrapLayoutHostWindow window = ScriptableObject.CreateInstance<BootstrapLayoutHostWindow>();
                try
                {
                    window.Configure(layoutCase);
                    yield return null;
                    yield return null;

                    VisualElement root = window.rootVisualElement;
                    VisualElement header = root.Q<VisualElement>("bootstrap-header");
                    VisualElement brand = root.Q<VisualElement>("bootstrap-header-brand");
                    VisualElement channel = root.Q<VisualElement>("bootstrap-channel");
                    VisualElement scroll = root.Q<ScrollView>("bootstrap-content-scroll");
                    VisualElement hero = root.Q<VisualElement>("bootstrap-hero");
                    VisualElement plan = root.Q<VisualElement>("bootstrap-plan");
                    VisualElement actionBar = root.Q<VisualElement>("bootstrap-action-bar");
                    VisualElement actions = root.Q<VisualElement>("bootstrap-action-actions");
                    Button refresh = root.Q<Button>("bootstrap-refresh-button");
                    Button primary = root.Q<Button>("bootstrap-primary-action");
                    Label primaryLabel = primary == null
                        ? null
                        : primary.Q<Label>(className: "bootstrap-button__label");
                    VisualElement[] steps =
                    {
                        root.Q<VisualElement>("bootstrap-step-1"),
                        root.Q<VisualElement>("bootstrap-step-2"),
                        root.Q<VisualElement>("bootstrap-step-3")
                    };

                    Assert.That(window.View.ResponsiveMode, Is.EqualTo(layoutCase.Mode), layoutCase.Name);
                    Assert.That(root.ClassListContains(layoutCase.Dark
                        ? "deucarian-bootstrap--dark"
                        : "deucarian-bootstrap--light"), Is.True, layoutCase.Name);
                    Assert.That(header, Is.Not.Null, layoutCase.Name);
                    Assert.That(brand, Is.Not.Null, layoutCase.Name);
                    Assert.That(channel, Is.Not.Null, layoutCase.Name);
                    Assert.That(scroll, Is.Not.Null, layoutCase.Name);
                    Assert.That(hero, Is.Not.Null, layoutCase.Name);
                    Assert.That(plan, Is.Not.Null, layoutCase.Name);
                    Assert.That(actionBar, Is.Not.Null, layoutCase.Name);
                    Assert.That(actions, Is.Not.Null, layoutCase.Name);
                    Assert.That(refresh, Is.Not.Null, layoutCase.Name);
                    Assert.That(primary, Is.Not.Null, layoutCase.Name);
                    Assert.That(primaryLabel, Is.Not.Null, layoutCase.Name);
                    Assert.That(steps, Has.All.Not.Null, layoutCase.Name);

                    Rect rootBounds = root.worldBound;
                    Rect headerBounds = header.worldBound;
                    Rect brandBounds = brand.worldBound;
                    Rect channelBounds = channel.worldBound;
                    Rect scrollBounds = scroll.worldBound;
                    Rect heroBounds = hero.worldBound;
                    Rect actionBounds = actionBar.worldBound;
                    Rect actionsBounds = actions.worldBound;
                    Rect primaryBounds = primary.worldBound;
                    Rect labelBounds = primaryLabel.worldBound;

                    Assert.That(rootBounds.width, Is.GreaterThan(0f), layoutCase.Name);
                    Assert.That(rootBounds.height, Is.GreaterThan(0f), layoutCase.Name);
                    Assert.That(actionBounds.height, Is.GreaterThanOrEqualTo(
                        BootstrapResponsiveLayout.Calculate(layoutCase.Width, layoutCase.Height)
                            .ActionBarMinimumHeight), layoutCase.Name);
                    Assert.That(IsContained(headerBounds, rootBounds), Is.True, layoutCase.Name);
                    Assert.That(IsContained(brandBounds, headerBounds), Is.True, layoutCase.Name);
                    Assert.That(IsContained(channelBounds, headerBounds), Is.True, layoutCase.Name);
                    Assert.That(brandBounds.Overlaps(channelBounds), Is.False, layoutCase.Name);
                    Assert.That(IsContained(heroBounds, scrollBounds), Is.True, layoutCase.Name);
                    Assert.That(IsContained(actionBounds, rootBounds), Is.True, layoutCase.Name);
                    Assert.That(IsContained(actionsBounds, actionBounds), Is.True, layoutCase.Name);
                    Assert.That(
                        IsContained(primaryBounds, actionsBounds),
                        Is.True,
                        layoutCase.Name + " primary " + primaryBounds +
                        " must remain inside actions " + actionsBounds);
                    Assert.That(IsContained(labelBounds, primaryBounds), Is.True, layoutCase.Name);
                    Assert.That(scroll.worldBound.yMax, Is.LessThanOrEqualTo(actionBounds.yMin + 0.5f),
                        layoutCase.Name);
                    Assert.That(plan.style.display.value, Is.EqualTo(DisplayStyle.Flex),
                        layoutCase.Name);
                    Assert.That(refresh.style.display.value, Is.EqualTo(DisplayStyle.Flex),
                        layoutCase.Name);
                    AssertPairwiseNonOverlapping(steps, layoutCase.Name);
                }
                finally
                {
                    window.Close();
                }
            }
        }

        private static bool IsContained(Rect inner, Rect outer)
        {
            const float tolerance = 0.5f;
            return inner.width > 0f &&
                   inner.height > 0f &&
                   inner.xMin >= outer.xMin - tolerance &&
                   inner.yMin >= outer.yMin - tolerance &&
                   inner.xMax <= outer.xMax + tolerance &&
                   inner.yMax <= outer.yMax + tolerance;
        }

        private static void AssertPairwiseNonOverlapping(
            VisualElement[] elements,
            string caseName)
        {
            for (int index = 0; index < elements.Length; index++)
            {
                Rect bounds = elements[index].worldBound;
                Assert.That(bounds.width, Is.GreaterThan(0f), caseName);
                Assert.That(bounds.height, Is.GreaterThan(0f), caseName);
                for (int other = index + 1; other < elements.Length; other++)
                {
                    Assert.That(
                        bounds.Overlaps(elements[other].worldBound),
                        Is.False,
                        caseName + " step " + (index + 1) + " overlaps step " + (other + 1));
                }
            }
        }

        private readonly struct LayoutCase
        {
            public LayoutCase(
                string name,
                float width,
                float height,
                bool dark,
                BootstrapResponsiveMode mode)
            {
                Name = name;
                Width = width;
                Height = height;
                Dark = dark;
                Mode = mode;
            }

            public string Name { get; }
            public float Width { get; }
            public float Height { get; }
            public bool Dark { get; }
            public BootstrapResponsiveMode Mode { get; }
        }

        private sealed class BootstrapLayoutHostWindow : EditorWindow
        {
            internal BootstrapSetupView View { get; private set; }

            internal void Configure(LayoutCase layoutCase)
            {
                titleContent = new GUIContent(layoutCase.Name);
                position = new Rect(40f, 40f, layoutCase.Width, layoutCase.Height);
                minSize = new Vector2(layoutCase.Width, layoutCase.Height);
                maxSize = new Vector2(layoutCase.Width, layoutCase.Height);
                ShowUtility();

                View = new BootstrapSetupView(_ => { }, _ => { }, () => { }, _ => { });
                View.Build(rootVisualElement);
                View.Render(BootstrapTestPresentationModels.Create(
                    BootstrapSetupPhase.Review,
                    BootstrapSetupAction.Repair,
                    true));
                View.SetSkin(layoutCase.Dark);
                View.ApplyResponsiveLayout(layoutCase.Width, layoutCase.Height);
                Repaint();
            }
        }
    }
}
