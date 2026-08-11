using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class BootstrapSetupView
    {
        private static readonly string[] ResponsiveClasses =
        {
            BootstrapResponsiveLayout.NarrowClassName,
            BootstrapResponsiveLayout.CompactClassName,
            BootstrapResponsiveLayout.WideClassName
        };

        private static readonly string[] IconClasses =
        {
            "bootstrap-icon--success",
            "bootstrap-icon--warning",
            "bootstrap-icon--review",
            "bootstrap-icon--error",
            "bootstrap-icon--loading",
            "bootstrap-icon--install",
            "bootstrap-icon--repair",
            "bootstrap-icon--open"
        };

        private readonly Action<BootstrapChannel> _channelChanged;
        private readonly Action<BootstrapSetupAction> _primaryInvoked;
        private readonly Action _refreshInvoked;
        private readonly Action<bool> _startupPreferenceChanged;

        private VisualElement _root;
        private PopupField<string> _channelField;
        private Label _channelDescription;
        private VisualElement _hero;
        private VisualElement _summaryIcon;
        private Label _summaryTitle;
        private Label _summaryMessage;
        private Label _progressMeta;
        private VisualElement _statusLine;
        private Label _statusText;
        private VisualElement _offlineLine;
        private Label _offlineText;
        private BootstrapSetupFlowView _setupFlow;
        private BootstrapCompletionReceiptView _completionReceipt;
        private VisualElement _detailsSection;
        private VisualElement _detailsRows;
        private Toggle _startupToggle;
        private Button _refreshButton;
        private VisualElement _actionBar;
        private VisualElement _passiveFooter;
        private Label _passiveFooterText;
        private VisualElement _actionButtons;
        private Button _primaryButton;
        private VisualElement _primaryIcon;
        private Label _primaryLabel;
        private bool _suppressChannelCallback;
        private BootstrapPresentationModel _model;

        public BootstrapSetupView(
            Action<BootstrapChannel> channelChanged,
            Action<BootstrapSetupAction> primaryInvoked,
            Action refreshInvoked,
            Action<bool> startupPreferenceChanged)
        {
            _channelChanged = channelChanged;
            _primaryInvoked = primaryInvoked;
            _refreshInvoked = refreshInvoked;
            _startupPreferenceChanged = startupPreferenceChanged;
        }

        internal Button PrimaryButton => _primaryButton;
        internal ScrollView ContentScroll { get; private set; }
        internal BootstrapResponsiveMode ResponsiveMode { get; private set; }

        public void Build(VisualElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _root.Clear();
            LoadStyleSheets(_root);
            _root.AddToClassList("deucarian-bootstrap");
            SetSkin(EditorGUIUtility.isProSkin);

            VisualElement shell = Element("bootstrap-shell", "bootstrap-shell");
            _root.Add(shell);
            shell.Add(BuildHeader());
            VisualElement workArea = Element("bootstrap-work-area", "bootstrap-work-area");
            shell.Add(workArea);
            ContentScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "bootstrap-content-scroll"
            };
            ContentScroll.AddToClassList("bootstrap-content-scroll");
            workArea.Add(ContentScroll);
            VisualElement content = Element("bootstrap-content", "bootstrap-content");
            ContentScroll.Add(content);
            content.Add(BuildHero());
            _setupFlow = new BootstrapSetupFlowView();
            _setupFlow.Root.style.display = DisplayStyle.None;
            content.Add(_setupFlow.Root);
            _completionReceipt = new BootstrapCompletionReceiptView();
            _completionReceipt.Root.style.display = DisplayStyle.None;
            content.Add(_completionReceipt.Root);

            content.Add(BuildDetailsSection());
            _actionBar = BuildActionBar();
            shell.Add(_actionBar);

            _root.RegisterCallback<GeometryChangedEvent>(evt =>
                ApplyResponsiveLayout(evt.newRect.width, evt.newRect.height));
            ApplyResponsiveLayout(_root.resolvedStyle.width, _root.resolvedStyle.height);
        }

        public void Render(BootstrapPresentationModel model)
        {
            if (_root == null || model == null)
            {
                return;
            }

            _model = model;
            SetSkin(EditorGUIUtility.isProSkin);
            RenderChannel(model);
            _summaryTitle.text = BootstrapViewContentPolicy.GetHeroTitle(model);
            _summaryMessage.text = model.StateMessage;
            SetIconClass(_summaryIcon, model.IconClass);
            SetToneClass(_hero, "bootstrap-hero--", model.Tone);
            bool showProgress = BootstrapViewContentPolicy.IsBusyPhase(model.Phase) &&
                                model.Steps.Count > 0;
            _progressMeta.style.display = showProgress ? DisplayStyle.Flex : DisplayStyle.None;
            _progressMeta.text = showProgress
                ? BootstrapViewContentPolicy.GetProgressText(model)
                : string.Empty;

            string contextText = BootstrapViewContentPolicy.GetContextText(model);
            bool showContext = !string.IsNullOrWhiteSpace(contextText);
            _statusLine.style.display = showContext ? DisplayStyle.Flex : DisplayStyle.None;
            _statusText.text = contextText;
            SetToneClass(_statusLine, "bootstrap-status--", model.Tone);

            bool showOffline = !string.IsNullOrWhiteSpace(model.OfflineNotice);
            _offlineLine.style.display = showOffline ? DisplayStyle.Flex : DisplayStyle.None;
            _offlineText.text = model.OfflineNotice;

            _setupFlow.Root.style.display = model.ShowSetupFlow
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _setupFlow.Render(
                model.Steps,
                BootstrapViewContentPolicy.IsBusyPhase(model.Phase));

            _completionReceipt.Root.style.display = model.ShowCompletionReceipt
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _completionReceipt.Render(model.Receipt);

            _detailsSection.style.display = model.Phase == BootstrapSetupPhase.Loading
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            RenderDetails(model.Details);
            _startupToggle.SetValueWithoutNotify(BootstrapStartupPreferences.ShouldShow());

            bool showQuietRefresh = model.ChannelEnabled &&
                                    model.PrimaryAction != BootstrapSetupAction.Refresh;
            _refreshButton.style.display = showQuietRefresh ? DisplayStyle.Flex : DisplayStyle.None;
            _refreshButton.SetEnabled(showQuietRefresh);
            RenderFooter(model);
        }

        public void ApplyResponsiveLayout(float width, float height)
        {
            if (_root == null)
            {
                return;
            }

            BootstrapResponsiveLayoutState layout = BootstrapResponsiveLayout.Calculate(width, height);
            foreach (string className in ResponsiveClasses)
            {
                _root.RemoveFromClassList(className);
            }

            _root.AddToClassList(layout.ClassName);
            _root.EnableInClassList(
                BootstrapResponsiveLayout.ShortHeightClassName,
                layout.IsShortHeight);
            ResponsiveMode = layout.Mode;
        }

        public void SetSkin(bool dark)
        {
            if (_root == null)
            {
                return;
            }

            _root.EnableInClassList("deucarian-bootstrap--dark", dark);
            _root.EnableInClassList("deucarian-bootstrap--light", !dark);
        }

        private void RenderChannel(BootstrapPresentationModel model)
        {
            _suppressChannelCallback = true;
            _channelField.SetValueWithoutNotify(BootstrapChannelUtility.GetDisplayName(model.Channel));
            _suppressChannelCallback = false;
            _channelField.SetEnabled(model.ChannelEnabled);
            _channelDescription.text = "#" + BootstrapChannelUtility.GetGitBranch(model.Channel);
        }

        private void RenderFooter(BootstrapPresentationModel model)
        {
            bool showAction = model.IsActionVisible &&
                              model.PrimaryActionEnabled &&
                              model.PrimaryAction != BootstrapSetupAction.None;
            bool showPassive = model.FooterIsPassive &&
                               !string.IsNullOrWhiteSpace(model.FooterText);
            _actionBar.style.display = showAction || showPassive
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _actionBar.EnableInClassList("bootstrap-action-bar--passive", showPassive);
            _passiveFooter.style.display = showPassive ? DisplayStyle.Flex : DisplayStyle.None;
            _passiveFooterText.text = model.FooterText;
            _actionButtons.style.display = showAction ? DisplayStyle.Flex : DisplayStyle.None;

            _primaryButton.SetEnabled(showAction);
            _primaryButton.tooltip = model.PrimaryActionTooltip;
            _primaryLabel.text = model.PrimaryActionLabel;
            SetIconClass(
                _primaryIcon,
                BootstrapViewContentPolicy.GetActionIconClass(model.PrimaryAction));
        }

        private VisualElement BuildHeader()
        {
            VisualElement header = Element("bootstrap-header", "bootstrap-header");
            VisualElement brand = Element("bootstrap-header-brand", "bootstrap-header__brand");
            header.Add(brand);

            Image logo = new Image
            {
                name = "bootstrap-header-logo",
                image = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    DeucarianBootstrapPackageConstants.LogoAssetPath),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            logo.AddToClassList("bootstrap-header__logo");
            brand.Add(logo);
            brand.Add(Label("Bootstrap", "bootstrap-header__title"));

            VisualElement channel = Element("bootstrap-channel", "bootstrap-channel");
            channel.tooltip = "Select the project-wide package-management channel. Changing it never installs packages.";
            channel.Add(Label("CHANNEL", "bootstrap-channel__label"));
            _channelField = new PopupField<string>(
                new List<string> { "Stable", "Development" },
                0)
            {
                name = "bootstrap-channel-field",
                tooltip = "Stable uses Git #main. Development uses Git #develop."
            };
            _channelField.RegisterValueChangedCallback(evt =>
            {
                if (!_suppressChannelCallback)
                {
                    _channelChanged?.Invoke(
                        string.Equals(evt.newValue, "Development", StringComparison.Ordinal)
                            ? BootstrapChannel.Development
                            : BootstrapChannel.Stable);
                }
            });
            channel.Add(_channelField);
            _channelDescription = Label(string.Empty, "bootstrap-channel__value");
            channel.Add(_channelDescription);
            header.Add(channel);
            return header;
        }

        private VisualElement BuildHero()
        {
            _hero = Element("bootstrap-hero", "bootstrap-hero");
            VisualElement visual = Element("bootstrap-hero-visual", "bootstrap-hero__visual");
            visual.Add(Element("bootstrap-hero-ambient", "bootstrap-hero__ambient"));
            VisualElement packageIcon = Element(
                "bootstrap-hero-package-icon",
                "bootstrap-icon",
                "bootstrap-icon--package-open",
                "bootstrap-hero__package-icon");
            packageIcon.pickingMode = PickingMode.Ignore;
            visual.Add(packageIcon);
            _summaryIcon = Element(
                "bootstrap-summary-icon",
                "bootstrap-summary__icon",
                "bootstrap-icon");
            _summaryIcon.pickingMode = PickingMode.Ignore;
            visual.Add(_summaryIcon);
            _hero.Add(visual);

            _hero.Add(Label("PACKAGE INSTALLER SETUP", "bootstrap-hero__eyebrow"));
            _summaryTitle = Label(string.Empty, "bootstrap-summary__title");
            _summaryMessage = Label(string.Empty, "bootstrap-summary__message");
            _hero.Add(_summaryTitle);
            _hero.Add(_summaryMessage);
            _progressMeta = Label(string.Empty, "bootstrap-progress-meta");
            _progressMeta.style.display = DisplayStyle.None;
            _hero.Add(_progressMeta);

            _statusLine = Element("bootstrap-status-line", "bootstrap-status-line");
            _statusLine.style.display = DisplayStyle.None;
            _statusText = Label(string.Empty, "bootstrap-status-line__text");
            _statusLine.Add(_statusText);
            _hero.Add(_statusLine);

            _offlineLine = Element(
                "bootstrap-offline-line",
                "bootstrap-status-line",
                "bootstrap-status--warning");
            _offlineLine.style.display = DisplayStyle.None;
            VisualElement warningIcon = Element(
                "bootstrap-offline-icon",
                "bootstrap-icon",
                "bootstrap-icon--warning");
            warningIcon.pickingMode = PickingMode.Ignore;
            _offlineLine.Add(warningIcon);
            _offlineText = Label(string.Empty, "bootstrap-status-line__text");
            _offlineLine.Add(_offlineText);
            _hero.Add(_offlineLine);
            return _hero;
        }

        private VisualElement BuildDetailsSection()
        {
            _detailsSection = Element("bootstrap-details", "bootstrap-details");
            Foldout foldout = new Foldout
            {
                name = "bootstrap-details-foldout",
                text = "Details",
                value = false,
                tooltip = "Show exact Git sources, revisions, fallback state, and legacy detection."
            };
            foldout.AddToClassList("bootstrap-details__foldout");

            VisualElement content = Element("bootstrap-details-content", "bootstrap-details__content");
            _detailsRows = Element("bootstrap-details-rows", "bootstrap-details__rows");
            content.Add(_detailsRows);

            VisualElement controls = Element("bootstrap-details-controls", "bootstrap-details__controls");
            _startupToggle = new Toggle("Show Bootstrap on startup")
            {
                name = "bootstrap-startup-toggle",
                tooltip = "Opens this read-only setup window on startup. It never installs packages automatically."
            };
            _startupToggle.RegisterValueChangedCallback(
                evt => _startupPreferenceChanged?.Invoke(evt.newValue));
            controls.Add(_startupToggle);

            _refreshButton = new Button(() => _refreshInvoked?.Invoke())
            {
                name = "bootstrap-refresh-button",
                text = "Refresh status",
                tooltip = "Refresh package, source, and revision status without changing packages."
            };
            _refreshButton.AddToClassList("bootstrap-button");
            _refreshButton.AddToClassList("bootstrap-button--quiet");
            controls.Add(_refreshButton);
            content.Add(controls);
            foldout.Add(content);
            _detailsSection.Add(foldout);
            return _detailsSection;
        }

        private VisualElement BuildActionBar()
        {
            VisualElement bar = Element("bootstrap-action-bar", "bootstrap-action-bar");
            bar.style.display = DisplayStyle.None;

            _passiveFooter = Element("bootstrap-passive-footer", "bootstrap-action-bar__passive");
            _passiveFooter.style.display = DisplayStyle.None;
            VisualElement passiveIcon = Element(
                "bootstrap-passive-footer-icon",
                "bootstrap-icon",
                "bootstrap-icon--loading");
            passiveIcon.pickingMode = PickingMode.Ignore;
            _passiveFooter.Add(passiveIcon);
            _passiveFooterText = Label(string.Empty, "bootstrap-action-bar__passive-text");
            _passiveFooter.Add(_passiveFooterText);
            bar.Add(_passiveFooter);

            _actionButtons = Element("bootstrap-action-actions", "bootstrap-action-bar__actions");
            _primaryButton = new Button(() =>
            {
                if (_model != null)
                {
                    _primaryInvoked?.Invoke(_model.PrimaryAction);
                }
            })
            {
                name = "bootstrap-primary-action",
                tooltip = "Run the current primary action."
            };
            _primaryButton.AddToClassList("bootstrap-button");
            _primaryButton.AddToClassList("bootstrap-button--primary");
            _primaryIcon = Element("bootstrap-primary-icon", "bootstrap-icon");
            _primaryIcon.pickingMode = PickingMode.Ignore;
            _primaryLabel = Label(string.Empty, "bootstrap-button__label");
            _primaryButton.Add(_primaryIcon);
            _primaryButton.Add(_primaryLabel);
            _actionButtons.Add(_primaryButton);
            bar.Add(_actionButtons);
            return bar;
        }

        private void RenderDetails(IReadOnlyList<BootstrapDetailPresentation> details)
        {
            _detailsRows.Clear();
            foreach (BootstrapDetailPresentation detail in
                     details ?? Array.Empty<BootstrapDetailPresentation>())
            {
                VisualElement row = Element(null, "bootstrap-detail-row");
                row.Add(Label(detail.Label, "bootstrap-detail-row__label"));
                Label value = Label(detail.Value, "bootstrap-detail-row__value");
                value.tooltip = detail.Value;
                row.Add(value);
                _detailsRows.Add(row);
            }
        }

        private static void LoadStyleSheets(VisualElement root)
        {
            string[] paths =
            {
                DeucarianBootstrapPackageConstants.StyleTokensAssetPath,
                DeucarianBootstrapPackageConstants.StyleShellAssetPath,
                DeucarianBootstrapPackageConstants.StyleComponentsAssetPath,
                DeucarianBootstrapPackageConstants.StyleResponsiveAssetPath
            };

            foreach (string path in paths)
            {
                StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet != null)
                {
                    root.styleSheets.Add(styleSheet);
                }
            }
        }

        private static void SetIconClass(VisualElement element, string className)
        {
            foreach (string iconClass in IconClasses)
            {
                element.RemoveFromClassList(iconClass);
            }

            if (!string.IsNullOrWhiteSpace(className))
            {
                element.AddToClassList(className);
            }
        }

        private static void SetToneClass(
            VisualElement element,
            string prefix,
            BootstrapPresentationTone tone)
        {
            string[] tones = { "neutral", "info", "success", "warning", "error" };
            foreach (string value in tones)
            {
                element.RemoveFromClassList(prefix + value);
            }

            element.AddToClassList(prefix + tone.ToString().ToLowerInvariant());
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
