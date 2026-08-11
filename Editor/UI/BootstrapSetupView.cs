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
        private VisualElement _summaryIcon;
        private Label _summaryTitle;
        private Label _summaryMessage;
        private VisualElement _statusStrip;
        private Label _statusText;
        private VisualElement _offlineStrip;
        private Label _offlineText;
        private Label _progressMeta;
        private VisualElement _planSection;
        private VisualElement _stepsContainer;
        private Foldout _detailsFoldout;
        private VisualElement _detailsContent;
        private VisualElement _detailsRows;
        private Toggle _startupToggle;
        private Button _refreshButton;
        private VisualElement _actionBar;
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
            ContentScroll = new ScrollView(ScrollViewMode.Vertical) { name = "bootstrap-content-scroll" };
            ContentScroll.AddToClassList("bootstrap-content-scroll");
            workArea.Add(ContentScroll);

            VisualElement content = Element("bootstrap-content", "bootstrap-content");
            ContentScroll.Add(content);
            content.Add(BuildHero());
            content.Add(BuildStepsSection());
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
            _suppressChannelCallback = true;
            _channelField.SetValueWithoutNotify(BootstrapChannelUtility.GetDisplayName(model.Channel));
            _suppressChannelCallback = false;
            _channelField.SetEnabled(model.ChannelEnabled);
            _channelDescription.text = "#" + BootstrapChannelUtility.GetGitBranch(model.Channel);

            _summaryTitle.text = BootstrapViewContentPolicy.GetHeroTitle(model);
            _summaryMessage.text = model.StateMessage;
            SetIconClass(_summaryIcon, model.IconClass);

            string contextText = BootstrapViewContentPolicy.GetContextText(model);
            bool showContext = !string.IsNullOrWhiteSpace(contextText);
            _statusStrip.style.display = showContext ? DisplayStyle.Flex : DisplayStyle.None;
            _statusText.text = contextText;
            SetToneClass(_statusStrip, model.Tone);

            bool showProgress = BootstrapViewContentPolicy.IsBusyPhase(model.Phase) &&
                                model.Steps.Count > 0;
            _progressMeta.style.display = showProgress ? DisplayStyle.Flex : DisplayStyle.None;
            _progressMeta.text = showProgress
                ? BootstrapViewContentPolicy.GetProgressText(model)
                : string.Empty;

            bool showOffline = !string.IsNullOrWhiteSpace(model.OfflineNotice);
            _offlineStrip.style.display = showOffline ? DisplayStyle.Flex : DisplayStyle.None;
            _offlineText.text = model.OfflineNotice;

            bool showPlan = BootstrapViewContentPolicy.ShouldShowPlan(model);
            _planSection.style.display = showPlan ? DisplayStyle.Flex : DisplayStyle.None;
            RenderSteps(model.Steps);
            RenderDetails(model.Details);
            _startupToggle.SetValueWithoutNotify(BootstrapStartupPreferences.ShouldShow());

            bool showQuietRefresh = model.ChannelEnabled &&
                                    model.PrimaryAction != BootstrapSetupAction.Refresh;
            _refreshButton.style.display = showQuietRefresh ? DisplayStyle.Flex : DisplayStyle.None;
            _refreshButton.SetEnabled(showQuietRefresh);
            bool showPrimaryAction = model.PrimaryAction != BootstrapSetupAction.None &&
                                     model.PrimaryActionEnabled;
            _actionBar.style.display = showPrimaryAction ? DisplayStyle.Flex : DisplayStyle.None;
            _primaryButton.SetEnabled(model.PrimaryActionEnabled);
            _primaryButton.tooltip = model.PrimaryActionTooltip;
            _primaryLabel.text = model.PrimaryActionLabel;
            SetIconClass(_primaryIcon, BootstrapViewContentPolicy.GetActionIconClass(model.PrimaryAction));
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

            VisualElement copy = Element("bootstrap-header-copy", "bootstrap-header__copy");
            copy.Add(Label("Bootstrap", "bootstrap-header__title"));
            copy.Add(Label("Setup & repair", "bootstrap-header__subtitle"));
            brand.Add(copy);

            VisualElement channel = Element("bootstrap-channel", "bootstrap-channel");
            channel.tooltip = "Select the project-wide package-management channel. Changing it never installs packages.";
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
            VisualElement hero = Element("bootstrap-hero", "bootstrap-hero", "bootstrap-surface");

            VisualElement summary = Element("bootstrap-summary", "bootstrap-summary");
            _summaryIcon = Element("bootstrap-summary-icon", "bootstrap-summary__icon", "bootstrap-icon");
            _summaryIcon.pickingMode = PickingMode.Ignore;
            summary.Add(_summaryIcon);
            VisualElement copy = Element("bootstrap-summary-copy", "bootstrap-summary__copy");
            _summaryTitle = Label(string.Empty, "bootstrap-summary__title");
            _summaryMessage = Label(string.Empty, "bootstrap-summary__message");
            copy.Add(_summaryTitle);
            copy.Add(_summaryMessage);
            _progressMeta = Label(string.Empty, "bootstrap-progress-meta");
            _progressMeta.style.display = DisplayStyle.None;
            copy.Add(_progressMeta);
            summary.Add(copy);
            hero.Add(summary);

            _statusStrip = Element("bootstrap-status-strip", "bootstrap-status-strip");
            _statusStrip.style.display = DisplayStyle.None;
            _statusText = Label(string.Empty, "bootstrap-status-strip__text");
            _statusStrip.Add(_statusText);
            hero.Add(_statusStrip);

            _offlineStrip = Element("bootstrap-offline-strip", "bootstrap-status-strip", "bootstrap-status--warning");
            _offlineStrip.style.display = DisplayStyle.None;
            VisualElement warningIcon = Element("bootstrap-offline-icon", "bootstrap-icon", "bootstrap-icon--warning");
            warningIcon.pickingMode = PickingMode.Ignore;
            _offlineStrip.Add(warningIcon);
            _offlineText = Label(string.Empty, "bootstrap-status-strip__text");
            _offlineStrip.Add(_offlineText);
            hero.Add(_offlineStrip);
            return hero;
        }

        private VisualElement BuildStepsSection()
        {
            _planSection = Element("bootstrap-plan", "bootstrap-plan");
            _planSection.style.display = DisplayStyle.None;
            _stepsContainer = Element("bootstrap-steps", "bootstrap-steps");
            _planSection.Add(_stepsContainer);
            return _planSection;
        }

        private VisualElement BuildDetailsSection()
        {
            VisualElement section = Element("bootstrap-details", "bootstrap-details");
            _detailsFoldout = new Foldout
            {
                name = "bootstrap-details-foldout",
                text = "Details",
                value = false,
                tooltip = "Show exact Git sources, lock revisions, registry fallback state, and read-only legacy detection."
            };
            _detailsFoldout.AddToClassList("bootstrap-details__foldout");
            _detailsContent = Element("bootstrap-details-content", "bootstrap-details__content");
            _detailsRows = Element("bootstrap-details-rows", "bootstrap-details__rows");
            _detailsContent.Add(_detailsRows);

            VisualElement controls = Element("bootstrap-details-controls", "bootstrap-details__controls");
            _detailsFoldout.Add(_detailsContent);
            section.Add(_detailsFoldout);

            _startupToggle = new Toggle("Show Bootstrap on startup")
            {
                name = "bootstrap-startup-toggle",
                tooltip = "Opens this read-only setup status window on startup. It never installs packages automatically."
            };
            _startupToggle.RegisterValueChangedCallback(evt => _startupPreferenceChanged?.Invoke(evt.newValue));
            controls.Add(_startupToggle);

            _refreshButton = new Button(() => _refreshInvoked?.Invoke())
            {
                name = "bootstrap-refresh-button",
                text = "Refresh status",
                tooltip = "Refresh registry, installed package, source, and revision status without changing packages."
            };
            _refreshButton.AddToClassList("bootstrap-button");
            _refreshButton.AddToClassList("bootstrap-button--quiet");
            controls.Add(_refreshButton);
            _detailsContent.Add(controls);
            return section;
        }

        private VisualElement BuildActionBar()
        {
            VisualElement bar = Element("bootstrap-action-bar", "bootstrap-action-bar");
            bar.style.display = DisplayStyle.None;
            VisualElement actions = Element("bootstrap-action-actions", "bootstrap-action-bar__actions");
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
            actions.Add(_primaryButton);
            bar.Add(actions);
            return bar;
        }

        private void RenderSteps(IReadOnlyList<BootstrapStepPresentation> steps)
        {
            _stepsContainer.Clear();
            foreach (BootstrapStepPresentation step in steps ?? Array.Empty<BootstrapStepPresentation>())
            {
                VisualElement item = Element("bootstrap-step-" + step.Number, "bootstrap-step");
                item.AddToClassList(BootstrapViewContentPolicy.GetStepClass(step.State));
                item.tooltip = step.Detail;
                item.Add(Label(step.Number.ToString(), "bootstrap-step__number"));
                item.Add(Label(step.Title, "bootstrap-step__title"));
                Label state = Label(
                    BootstrapViewContentPolicy.GetStepStateLabel(step.State),
                    "bootstrap-step__state");
                state.tooltip = step.TechnicalDetail;
                item.Add(state);
                _stepsContainer.Add(item);
            }
        }

        private void RenderDetails(IReadOnlyList<BootstrapDetailPresentation> details)
        {
            _detailsRows.Clear();
            foreach (BootstrapDetailPresentation detail in details ?? Array.Empty<BootstrapDetailPresentation>())
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

        private static void SetToneClass(VisualElement element, BootstrapPresentationTone tone)
        {
            string[] tones = { "neutral", "info", "success", "warning", "error" };
            foreach (string value in tones)
            {
                element.RemoveFromClassList("bootstrap-status--" + value);
            }

            element.AddToClassList("bootstrap-status--" + tone.ToString().ToLowerInvariant());
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
