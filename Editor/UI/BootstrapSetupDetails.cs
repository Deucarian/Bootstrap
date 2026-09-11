using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class BootstrapSetupDetails
    {
        private readonly VisualElement rows;
        private readonly Toggle startup;
        internal VisualElement Root { get; }
        internal Button RefreshButton { get; }

        internal BootstrapSetupDetails(Action refresh, Action<bool> setStartup)
        {
            Root = BootstrapSetupShell.Element("bootstrap-details");
            var foldout = new Foldout { name = "bootstrap-details-foldout", text = "Details", value = false,
                tooltip = "Show exact Git sources, revisions, fallback state, and legacy detection." };
            foldout.AddToClassList("bootstrap-details__foldout"); Root.Add(foldout);
            var content = BootstrapSetupShell.Element("bootstrap-details__content"); content.name = "bootstrap-details-content";
            rows = BootstrapSetupShell.Element("bootstrap-details__rows"); rows.name = "bootstrap-details-rows"; content.Add(rows);
            var controls = BootstrapSetupShell.Element("bootstrap-details__controls"); controls.name = "bootstrap-details-controls";
            startup = new Toggle("Show Bootstrap on startup") { name = "bootstrap-startup-toggle",
                tooltip = "Opens this read-only setup window on startup. It never installs packages automatically." };
            startup.RegisterValueChangedCallback(evt => setStartup?.Invoke(evt.newValue)); controls.Add(startup);
            RefreshButton = BootstrapSetupShell.Button(string.Empty, () => refresh?.Invoke(), "bootstrap-button--quiet");
            RefreshButton.name = "bootstrap-refresh-button";
            RefreshButton.tooltip = "Refresh package, source, and revision status without changing packages.";
            var icon = BootstrapSetupShell.Element("bootstrap-icon"); icon.AddToClassList("bootstrap-refresh-icon");
            RefreshButton.Add(icon); RefreshButton.Add(BootstrapSetupShell.Text("Refresh", "bootstrap-button__label")); controls.Add(RefreshButton);
            content.Add(controls); foldout.Add(content);
        }

        internal void Render(IReadOnlyList<BootstrapDetailPresentation> details)
        {
            rows.Clear();
            foreach (var detail in details ?? Array.Empty<BootstrapDetailPresentation>())
            {
                var row = BootstrapSetupShell.Element("bootstrap-detail-row");
                row.Add(BootstrapSetupShell.Text(detail.Label, "bootstrap-detail-row__label"));
                var value = BootstrapSetupShell.Text(detail.Value, "bootstrap-detail-row__value"); value.tooltip = detail.Value;
                row.Add(value); rows.Add(row);
            }
            startup.SetValueWithoutNotify(BootstrapStartupPreferences.ShouldShow());
        }
    }
}
