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
            new LayoutCase("Narrow Light", 640f, 560f, false, BootstrapResponsiveMode.Narrow),
            new LayoutCase("Narrow Dark", 640f, 560f, true, BootstrapResponsiveMode.Narrow),
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
                    VisualElement scroll = root.Q<ScrollView>("bootstrap-content-scroll");
                    VisualElement actionBar = root.Q<VisualElement>("bootstrap-action-bar");
                    VisualElement actions = root.Q<VisualElement>("bootstrap-action-actions");
                    Button refresh = root.Q<Button>("bootstrap-refresh-button");
                    Button primary = root.Q<Button>("bootstrap-primary-action");
                    Label primaryLabel = primary == null
                        ? null
                        : primary.Q<Label>(className: "bootstrap-button__label");

                    Assert.That(window.View.ResponsiveMode, Is.EqualTo(layoutCase.Mode), layoutCase.Name);
                    Assert.That(root.ClassListContains(layoutCase.Dark
                        ? "deucarian-bootstrap--dark"
                        : "deucarian-bootstrap--light"), Is.True, layoutCase.Name);
                    Assert.That(scroll, Is.Not.Null, layoutCase.Name);
                    Assert.That(actionBar, Is.Not.Null, layoutCase.Name);
                    Assert.That(actions, Is.Not.Null, layoutCase.Name);
                    Assert.That(refresh, Is.Not.Null, layoutCase.Name);
                    Assert.That(primary, Is.Not.Null, layoutCase.Name);
                    Assert.That(primaryLabel, Is.Not.Null, layoutCase.Name);

                    Rect rootBounds = root.worldBound;
                    Rect actionBounds = actionBar.worldBound;
                    Rect actionsBounds = actions.worldBound;
                    Rect refreshBounds = refresh.worldBound;
                    Rect primaryBounds = primary.worldBound;
                    Rect labelBounds = primaryLabel.worldBound;

                    Assert.That(rootBounds.width, Is.GreaterThan(0f), layoutCase.Name);
                    Assert.That(rootBounds.height, Is.GreaterThan(0f), layoutCase.Name);
                    Assert.That(actionBounds.height, Is.GreaterThanOrEqualTo(
                        BootstrapResponsiveLayout.Calculate(layoutCase.Width, layoutCase.Height)
                            .ActionBarMinimumHeight), layoutCase.Name);
                    Assert.That(IsContained(actionBounds, rootBounds), Is.True, layoutCase.Name);
                    Assert.That(IsContained(actionsBounds, actionBounds), Is.True, layoutCase.Name);
                    Assert.That(IsContained(refreshBounds, actionsBounds), Is.True, layoutCase.Name);
                    Assert.That(IsContained(primaryBounds, actionsBounds), Is.True, layoutCase.Name);
                    Assert.That(IsContained(labelBounds, primaryBounds), Is.True, layoutCase.Name);
                    Assert.That(refreshBounds.Overlaps(primaryBounds), Is.False, layoutCase.Name);
                    Assert.That(scroll.worldBound.yMax, Is.LessThanOrEqualTo(actionBounds.yMin + 0.5f),
                        layoutCase.Name);
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
                View.Render(CreateReviewModel());
                View.SetSkin(layoutCase.Dark);
                View.ApplyResponsiveLayout(layoutCase.Width, layoutCase.Height);
                Repaint();
            }

            private static BootstrapPresentationModel CreateReviewModel()
            {
                BootstrapStepPresentation[] steps =
                {
                    new BootstrapStepPresentation(1, "Editor", "Install the shared editor shell.", string.Empty,
                        BootstrapStepPresentationState.Ready),
                    new BootstrapStepPresentation(2, "Logging", "Install package diagnostics.", string.Empty,
                        BootstrapStepPresentationState.Ready),
                    new BootstrapStepPresentation(3, "Package Installer", "Install package management last.",
                        string.Empty, BootstrapStepPresentationState.Ready)
                };
                BootstrapDetailPresentation[] details =
                {
                    new BootstrapDetailPresentation("Target source",
                        "https://github.com/Deucarian/Package-Installer.git#main")
                };

                return new BootstrapPresentationModel(
                    BootstrapChannel.Stable,
                    BootstrapSetupPhase.Review,
                    "Setup needs repair",
                    "Review the dependency-first setup closure.",
                    "Ready to repair the project setup.",
                    BootstrapPresentationTone.Warning,
                    "bootstrap-icon--repair",
                    BootstrapSetupAction.Repair,
                    "Repair",
                    "Repair the setup closure.",
                    true,
                    true,
                    "One explicit action",
                    "Opening Bootstrap never changes packages automatically.",
                    steps,
                    details,
                    string.Empty,
                    0,
                    "Package Installer needs repair.");
            }
        }
    }
}
