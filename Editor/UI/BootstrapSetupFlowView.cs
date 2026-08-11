using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Deucarian.Bootstrap.Editor
{
    /// <summary>
    /// Renders the destination-first setup path without owning workflow decisions.
    /// </summary>
    internal sealed class BootstrapSetupFlowView
    {
        private readonly VisualElement _items;

        public BootstrapSetupFlowView()
        {
            Root = Element("bootstrap-setup-flow", "bootstrap-setup-flow");

            VisualElement heading = Element("bootstrap-setup-flow-heading", "bootstrap-setup-flow__heading");
            heading.Add(Label("SETUP PATH", "bootstrap-section-kicker"));
            heading.Add(Label(
                "Package Installer, with what it needs",
                "bootstrap-setup-flow__title"));
            Root.Add(heading);

            _items = Element("bootstrap-setup-items", "bootstrap-setup-items");
            Root.Add(_items);
        }

        public VisualElement Root { get; }

        public void Render(
            IReadOnlyList<BootstrapStepPresentation> steps,
            bool busy)
        {
            Root.EnableInClassList("bootstrap-setup-flow--busy", busy);
            _items.Clear();

            foreach (BootstrapStepPresentation step in
                     steps ?? Array.Empty<BootstrapStepPresentation>())
            {
                _items.Add(BuildItem(step));
            }
        }

        private static VisualElement BuildItem(BootstrapStepPresentation step)
        {
            VisualElement item = Element(
                "bootstrap-setup-item-" + step.Number,
                "bootstrap-setup-item",
                "bootstrap-setup-item--" + RoleClass(step.Role),
                "bootstrap-setup-item--" + StateClass(step.State),
                "bootstrap-setup-item--index-" + step.Number);
            item.tooltip = step.TechnicalDetail;

            VisualElement marker = Element(
                "bootstrap-setup-item-marker-" + step.Number,
                "bootstrap-setup-item__marker");
            Label number = Label(step.Number.ToString(), "bootstrap-setup-item__number");
            marker.Add(number);

            VisualElement stateIcon = Element(
                "bootstrap-setup-item-icon-" + step.Number,
                "bootstrap-icon",
                "bootstrap-setup-item__state-icon",
                IconClass(step.State));
            stateIcon.pickingMode = PickingMode.Ignore;
            marker.Add(stateIcon);
            item.Add(marker);

            VisualElement copy = Element(
                "bootstrap-setup-item-copy-" + step.Number,
                "bootstrap-setup-item__copy");
            copy.Add(Label(
                step.Role == BootstrapSetupItemRole.Destination ? "DESTINATION" : "REQUIREMENT",
                "bootstrap-setup-item__role"));
            copy.Add(Label(step.Title, "bootstrap-setup-item__title"));
            copy.Add(Label(step.Detail, "bootstrap-setup-item__detail"));
            item.Add(copy);

            Label state = Label(step.Label, "bootstrap-setup-item__state");
            state.tooltip = step.TechnicalDetail;
            item.Add(state);
            return item;
        }

        private static string RoleClass(BootstrapSetupItemRole role)
        {
            return role == BootstrapSetupItemRole.Destination
                ? "destination"
                : "requirement";
        }

        private static string StateClass(BootstrapStepPresentationState state)
        {
            switch (state)
            {
                case BootstrapStepPresentationState.Current: return "current";
                case BootstrapStepPresentationState.Complete: return "complete";
                case BootstrapStepPresentationState.Attention: return "attention";
                case BootstrapStepPresentationState.Failed: return "failed";
                case BootstrapStepPresentationState.Ready: return "ready";
                default: return "pending";
            }
        }

        private static string IconClass(BootstrapStepPresentationState state)
        {
            switch (state)
            {
                case BootstrapStepPresentationState.Current: return "bootstrap-icon--loading";
                case BootstrapStepPresentationState.Complete: return "bootstrap-icon--success";
                case BootstrapStepPresentationState.Attention: return "bootstrap-icon--warning";
                case BootstrapStepPresentationState.Failed: return "bootstrap-icon--error";
                case BootstrapStepPresentationState.Ready: return "bootstrap-icon--success";
                default: return "bootstrap-icon--pending";
            }
        }

        private static VisualElement Element(string name, params string[] classes)
        {
            VisualElement element = new VisualElement { name = name };
            foreach (string className in classes)
            {
                element.AddToClassList(className);
            }

            return element;
        }

        private static Label Label(string text, string className)
        {
            Label label = new Label(text ?? string.Empty);
            label.AddToClassList(className);
            return label;
        }
    }
}
