using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Deucarian.Bootstrap.Editor
{
    /// <summary>
    /// Renders the compact, read-only receipt shown after authoritative verification.
    /// </summary>
    internal sealed class BootstrapCompletionReceiptView
    {
        private readonly VisualElement _items;

        public BootstrapCompletionReceiptView()
        {
            Root = Element("bootstrap-completion-receipt", "bootstrap-completion-receipt");
            Root.Add(Label("SETUP RECEIPT", "bootstrap-section-kicker"));
            _items = Element("bootstrap-receipt-items", "bootstrap-receipt-items");
            Root.Add(_items);
        }

        public VisualElement Root { get; }

        public void Render(IReadOnlyList<BootstrapReceiptPresentation> receipt)
        {
            _items.Clear();
            int index = 0;
            foreach (BootstrapReceiptPresentation entry in
                     receipt ?? Array.Empty<BootstrapReceiptPresentation>())
            {
                index++;
                VisualElement item = Element(
                    "bootstrap-receipt-item-" + index,
                    "bootstrap-receipt-item",
                    "bootstrap-receipt-item--index-" + index);
                item.tooltip = entry.PackageId;

                VisualElement icon = Element(
                    "bootstrap-receipt-icon-" + index,
                    "bootstrap-icon",
                    "bootstrap-icon--success",
                    "bootstrap-receipt-item__icon");
                icon.pickingMode = PickingMode.Ignore;
                item.Add(icon);

                VisualElement copy = Element(
                    "bootstrap-receipt-copy-" + index,
                    "bootstrap-receipt-item__copy");
                copy.Add(Label(entry.Title, "bootstrap-receipt-item__title"));
                copy.Add(Label(entry.Summary, "bootstrap-receipt-item__summary"));
                item.Add(copy);
                _items.Add(item);
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
