using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Deucarian.Bootstrap.Editor.Tests
{
    public sealed class BootstrapWarningAlignmentTests
    {
        [UnityTest]
        public IEnumerator RepairWarningIsCenteredAtEverySupportedScale()
        {
            int original = EditorPrefs.GetInt(BootstrapSetupShell.ScaleKey, 100);
            var host = ScriptableObject.CreateInstance<WarningLayoutHost>();
            try
            {
                host.Show();
                foreach (int scale in new[] { 75, 100, 150 })
                {
                    EditorPrefs.SetInt(BootstrapSetupShell.ScaleKey, scale);
                    host.position = new Rect(30, 30, 900, 650);
                    var view = new BootstrapSetupView(_ => { }, _ => { }, () => { }, _ => { });
                    view.Build(host.rootVisualElement);
                    view.Render(BootstrapPresentationModelFactory.Create(BootstrapPresentationSnapshotFixtures.MissingEditor()));
                    for (int i = 0; i < 5; i++) yield return null;
                    var root = host.rootVisualElement;
                    Assert.IsTrue(root.ClassListContains("bootstrap-repair-review"));
                    AssertCentered(root.Q("bootstrap-summary-icon"), root.Q("bootstrap-hero-visual"));
                    foreach (var icon in root.Query(className: "bootstrap-setup-item__state-icon").ToList())
                        if (icon.resolvedStyle.display != DisplayStyle.None) AssertCentered(icon, icon.parent);
                }
            }
            finally { EditorPrefs.SetInt(BootstrapSetupShell.ScaleKey, original); host.Close(); }
        }

        private static void AssertCentered(VisualElement icon, VisualElement container)
        {
            Assert.Greater(icon.worldBound.width, 0, icon.name);
            Assert.That(Vector2.Distance(icon.worldBound.center, container.worldBound.center), Is.LessThan(1), icon.name);
            Assert.LessOrEqual(icon.worldBound.width, container.worldBound.width, icon.name);
        }
        private sealed class WarningLayoutHost : EditorWindow { }
    }
}
