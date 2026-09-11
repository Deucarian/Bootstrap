using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class BootstrapSetupShell
    {
        internal const string ScaleKey = "Deucarian.Bootstrap.WorkspaceScale";
        private readonly VisualElement canvas;
        private readonly SliderInt scale;
        private readonly Button reset;
        private readonly VisualElement fill;
        private readonly Button setup, repair;
        internal VisualElement Body { get; }
        internal Label Title { get; }
        internal Label Subtitle { get; }

        internal BootstrapSetupShell(VisualElement root, Action<bool> navigate)
        {
            root.AddToClassList("bootstrap-fidelity");
            var viewport = Element("bootstrap-viewport"); root.Add(viewport);
            canvas = Element("bootstrap-canvas"); viewport.Add(canvas);
            canvas.Add(new BootstrapSetupBackdrop());
            var header = Element("bootstrap-workspace-header"); canvas.Add(header);
            header.Add(new BootstrapBrandMark());
            header.Add(Text("Deucarian", "bootstrap-brand-name"));
            header.Add(Text(Application.productName, "bootstrap-project-name"));
            var main = Element("bootstrap-workspace-main"); canvas.Add(main);
            var rail = Element("bootstrap-workspace-rail"); main.Add(rail);
            setup = Button(string.Empty, () => navigate(false), "bootstrap-nav-item"); setup.Add(Icon("cog")); setup.Add(Text("Setup", "bootstrap-nav-label")); rail.Add(setup);
            repair = Button(string.Empty, () => navigate(true), "bootstrap-nav-item"); repair.Add(Icon("wrench")); repair.Add(Text("Repair", "bootstrap-nav-label")); rail.Add(repair);
            Body = Element("bootstrap-workspace-body"); main.Add(Body);
            Title = Text("Set up Deucarian", "bootstrap-page-title"); Body.Add(Title);
            Subtitle = Text("Get the tools for your project.", "bootstrap-page-subtitle"); Body.Add(Subtitle);
            var footer = Element("bootstrap-scale-footer"); root.Add(footer);
            footer.Add(Text("UI scale", "bootstrap-scale-label"));
            scale = new SliderInt(75, 150) { name = "bootstrap-scale-slider" };
            scale.tooltip = "Resize setup controls. This scale control stays in place.";
            fill = Element("bootstrap-scale-fill");
            scale.Q(className: "unity-base-slider__tracker").Add(fill); footer.Add(scale);
            reset = Button("100%", () => SetScale(100), "bootstrap-scale-reset"); footer.Add(reset);
            scale.RegisterValueChangedCallback(evt => SetScale(evt.newValue));
            SetScale(Mathf.Clamp(EditorPrefs.GetInt(ScaleKey, 100), 75, 150), false);
            viewport.RegisterCallback<GeometryChangedEvent>(_ => Adapt());
        }

        internal void Select(bool isRepair)
        {
            setup.EnableInClassList("bootstrap-nav-selected", !isRepair);
            repair.EnableInClassList("bootstrap-nav-selected", isRepair);
            Title.text = isRepair ? "Repair setup" : "Set up Deucarian";
            Subtitle.text = isRepair ? "Restore the tools your project needs." : "Get the tools for your project.";
        }

        private void SetScale(int percent, bool save = true)
        {
            scale.SetValueWithoutNotify(percent); reset.text = percent + "%";
            if (save) EditorPrefs.SetInt(ScaleKey, percent);
            float factor = percent / 100f;
            canvas.style.transformOrigin = new TransformOrigin(0, 0);
            canvas.style.scale = new Scale(new Vector3(factor, factor, 1));
            canvas.style.width = Length.Percent(100 / factor); canvas.style.height = Length.Percent(100 / factor);
            Adapt();
            fill.style.width = Length.Percent((percent - 75) * 100f / 75);
        }

        private void Adapt()
        {
            float width = canvas.parent.resolvedStyle.width * 100 / scale.value;
            canvas.EnableInClassList("bootstrap-shell-narrow", width < 950);
            canvas.EnableInClassList("bootstrap-shell-tiny", width < 640);
        }

        internal static VisualElement Element(string css)
        {
            var element = new VisualElement { name = css }; element.AddToClassList(css); return element;
        }
        internal static Label Text(string text, string css)
        {
            var label = new Label(text); label.AddToClassList(css); return label;
        }
        internal static Button Button(string text, Action action, string css)
        {
            var button = new Button(action) { text = text }; button.AddToClassList("bootstrap-button"); button.AddToClassList(css); return button;
        }
        private static VisualElement Icon(string name)
        {
            var icon = Element("bootstrap-nav-icon");
            icon.style.backgroundImage = new StyleBackground(AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/com.deucarian.bootstrap/Editor/Assets/Icons/Lucide/" + name + ".png"));
            return icon;
        }
    }
}
