using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class DeucarianBootstrapWindow : EditorWindow
    {
        private const string ActiveKey = "Deucarian.Bootstrap.Active";
        private const string StepIndexKey = "Deucarian.Bootstrap.StepIndex";
        private const string StatusKey = "Deucarian.Bootstrap.Status";
        private const string ErrorKey = "Deucarian.Bootstrap.Error";
        private const string ChannelKey = "Deucarian.Bootstrap.Channel";
        private const string PlanKey = "Deucarian.Bootstrap.Plan";
        private const string PendingPackageIdKey = "Deucarian.Bootstrap.PendingPackageId";
        private const string WaitingForPackageRefreshKey = "Deucarian.Bootstrap.WaitingForPackageRefresh";
        private const string PackageListRetryCountKey = "Deucarian.Bootstrap.PackageListRetryCount";
        private const string InterruptedKey = "Deucarian.Bootstrap.Interrupted";
        private const string StartupShownThisSessionKey = "Deucarian.Bootstrap.StartupShownThisSession";
        private const string ShowOnStartupPreferencePrefix = "Deucarian.Bootstrap.ShowOnStartup.";
        private const string ChannelPreferencePrefix = "Deucarian.Bootstrap.Channel.";
        private const string SetupDetailsExpandedKey = "Deucarian.Bootstrap.SetupDetailsExpanded";
        private const char PlanSeparator = '|';

        internal const float PreferredWindowWidth = 1120f;
        internal const float PreferredWindowHeight = 820f;
        internal const float MinWindowWidth = 1080f;
        internal const float MinWindowHeight = 780f;
        internal const float HeroCardHeight = 318f;
        internal const float StatusCardHeight = 72f;
        internal const float StatusGridHeight = StatusCardHeight * 2f + 10f;
        private const int MaxPackageListRefreshAttempts = 90;
        private const double PackageListRetryDelaySeconds = 1.0d;

        private static readonly BootstrapSetupPackage[] RequiredSetupPackages =
        {
            new BootstrapSetupPackage(
                DeucarianBootstrapPackageConstants.EditorPackageId,
                DeucarianBootstrapPackageConstants.EditorPackageDisplayName),
            new BootstrapSetupPackage(
                DeucarianBootstrapPackageConstants.LoggingPackageId,
                DeucarianBootstrapPackageConstants.LoggingPackageDisplayName),
            new BootstrapSetupPackage(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageDisplayName)
        };

        private readonly List<BootstrapPackageStep> _installPlan = new List<BootstrapPackageStep>();

        private UnityWebRequest _catalogRequest;
        private UnityWebRequest _targetVersionRequest;
        private ListRequest _listRequest;
        private AddRequest _addRequest;
        private RemoveRequest _removeRequest;
        private HashSet<string> _installedPackageIds;
        private Dictionary<string, BootstrapInstalledPackageInfo> _installedPackagesById;
        private bool _catalogLoaded;
        private bool _setupActive;
        private bool _continueSetupAfterPackageList;
        private bool _waitingForPackageRefresh;
        private bool _setupInterrupted;
        private bool _packageListRetryQueued;
        private int _stepIndex;
        private int _packageListRetryCount;
        private double _nextPackageListRefreshTime;
        private string _pendingPackageId;
        private string[] _savedPlanPackageIds;
        private string _registrySource;
        private string _catalogNotice;
        private string _status;
        private string _error;
        private string _packageInstallerOpenMessage;
        private string _pendingCatalogFinishStatus;
        private string _targetPackageInstallerGitUrl;
        private string _targetPackageInstallerVersion;
        private string _targetPackageInstallerVersionSource;
        private BootstrapPackageStep _removeThenAddStep;
        private BootstrapChannel _selectedChannel;
        private BootstrapScopedRegistryStatus _scopedRegistryStatus;
        private Vector2 _scrollPosition;

        private bool _stylesInitialized;
        private bool _lastProSkin;
        private Color _windowBackgroundColor;
        private Color _heroBackgroundColor;
        private Color _cardBackgroundColor;
        private Color _glassPanelColor;
        private Color _glassStrongColor;
        private Color _glassInsetColor;
        private Color _rowBackgroundColor;
        private Color _rowAlternateBackgroundColor;
        private Color _borderColor;
        private Color _interactiveBorderColor;
        private Color _titleTextColor;
        private Color _bodyTextColor;
        private Color _mutedTextColor;
        private Color _successColor;
        private Color _warningColor;
        private Color _infoColor;
        private Color _neutralColor;
        private Color _errorColor;

        private GUIStyle _windowStyle;
        private GUIStyle _heroStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _miniMutedStyle;
        private GUIStyle _productStatusStyle;
        private GUIStyle _productStatusDetailStyle;
        private GUIStyle _statusIconStyle;
        private GUIStyle _statusLabelStyle;
        private GUIStyle _statusDetailStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _secondaryButtonStyle;
        private GUIStyle _utilityButtonStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _footerStyle;
        private GUIStyle _footerRightStyle;
        private GUIStyle _heroTitleStyle;
        private GUIStyle _heroSubtitleLargeStyle;
        private GUIStyle _heroEyebrowStyle;
        private GUIStyle _summaryValueStyle;
        private GUIStyle _summaryLabelStyle;
        private GUIStyle _summarySubtextStyle;
        private GUIStyle _timelineLabelStyle;
        private GUIStyle _foldoutStyle;

        private Texture2D _logoTexture;
        private Texture2D _heroBackgroundTexture;
        private Texture2D _wallpaperTexture;
        private Texture2D _packageIconTexture;
        private bool _setupDetailsExpanded;

        internal IReadOnlyList<BootstrapPackageStep> InstallPlan => _installPlan;

        internal string RegistrySource => _registrySource ?? string.Empty;

        [MenuItem(DeucarianBootstrapPackageConstants.MenuPath)]
        public static void Open()
        {
            DeucarianBootstrapWindow window = GetWindow<DeucarianBootstrapWindow>();
            window.titleContent = new GUIContent("Deucarian Setup");
            window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            window.Show();
            EnsurePreferredFloatingWindowSize(window);
        }

        [InitializeOnLoadMethod]
        private static void ScheduleStartupWelcome()
        {
            EditorApplication.delayCall -= OpenStartupWelcomeWhenReady;
            EditorApplication.delayCall += OpenStartupWelcomeWhenReady;
        }

        [InitializeOnLoadMethod]
        private static void ScheduleActiveSetupResume()
        {
            EditorApplication.delayCall -= ResumeActiveSetupAfterReload;
            EditorApplication.delayCall += ResumeActiveSetupAfterReload;
        }

        private static void OpenStartupWelcomeWhenReady()
        {
            EditorApplication.delayCall -= OpenStartupWelcomeWhenReady;

            if (Application.isBatchMode ||
                SessionState.GetBool(StartupShownThisSessionKey, false) ||
                !ShouldShowOnStartup())
            {
                return;
            }

            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += OpenStartupWelcomeWhenReady;
                return;
            }

            if (FindExistingWindow() != null)
            {
                SessionState.SetBool(StartupShownThisSessionKey, true);
                return;
            }

            SessionState.SetBool(StartupShownThisSessionKey, true);
            Open();
        }

        private static void ResumeActiveSetupAfterReload()
        {
            EditorApplication.delayCall -= ResumeActiveSetupAfterReload;

            bool sessionSetupActive = SessionState.GetBool(ActiveKey, false);
            DeucarianBootstrapWindow window = FindExistingWindow();

            if (!sessionSetupActive && (window == null || !window._setupActive))
            {
                return;
            }

            if (window == null)
            {
                window = GetWindow<DeucarianBootstrapWindow>();
            }

            window.titleContent = new GUIContent("Deucarian Setup");
            window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            window.Show();
            window.ResumeActiveSetupWindow(sessionSetupActive);
        }

        private static DeucarianBootstrapWindow FindExistingWindow()
        {
            DeucarianBootstrapWindow[] windows = Resources.FindObjectsOfTypeAll<DeucarianBootstrapWindow>();
            return windows != null ? windows.FirstOrDefault() : null;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Deucarian Setup");
            minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            _setupDetailsExpanded = SessionState.GetBool(SetupDetailsExpandedKey, false);
            RestoreStateForEnable();
            RefreshScopedRegistryStatus();
            EditorApplication.delayCall -= HandleDelayedEnable;
            EditorApplication.delayCall += HandleDelayedEnable;
        }

        private void RestoreStateForEnable()
        {
            if (SessionState.GetBool(ActiveKey, false) || !_setupActive)
            {
                LoadState();
                return;
            }

            SaveState();
        }

        private void ResumeActiveSetupWindow(bool preferSessionState)
        {
            if (preferSessionState)
            {
                LoadState();
            }
            else if (_setupActive)
            {
                SaveState();
            }

            RefreshScopedRegistryStatus();
            HandleDelayedEnable();
            Repaint();
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= HandleDelayedEnable;
            EditorApplication.update -= UpdateRequests;
            DisposeCatalogRequest();
            DisposeTargetVersionRequest();
        }

        private static void EnsurePreferredFloatingWindowSize(DeucarianBootstrapWindow window)
        {
            if (window == null || window.docked)
            {
                return;
            }

            Rect current = window.position;
            float width = Mathf.Max(current.width, PreferredWindowWidth);
            float height = Mathf.Max(current.height, PreferredWindowHeight);

            if (Mathf.Approximately(width, current.width) && Mathf.Approximately(height, current.height))
            {
                return;
            }

            window.position = new Rect(current.x, current.y, width, height);
        }

        private void HandleDelayedEnable()
        {
            if (this == null)
            {
                return;
            }

            EnsureActiveSetupHasResolvablePlan();

            string status = _setupActive
                ? GetResumeStatus()
                : "Loading Deucarian package catalog...";

            BeginCatalogLoad(status);
        }

        private void EnsureActiveSetupHasResolvablePlan()
        {
            if (!_setupActive ||
                !_catalogLoaded ||
                _installPlan.Count > 0)
            {
                return;
            }

            _catalogLoaded = false;
            _registrySource = string.Empty;
            _catalogNotice = string.Empty;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawWindowBackground();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            using (new EditorGUILayout.VerticalScope(_windowStyle))
            {
                DrawHeader();
                DrawPackageInstallerProductCard();
                DrawCompactSetupSummary();
                DrawSetupDetails();
                DrawSetupActions();
                DrawFooter();
            }

            EditorGUILayout.EndScrollView();
        }

        private void Update()
        {
            if (_catalogRequest != null ||
                _targetVersionRequest != null ||
                _listRequest != null ||
                _addRequest != null ||
                _removeRequest != null ||
                _packageListRetryQueued)
            {
                UpdateRequests();
                return;
            }

            if (_setupActive && _installPlan.Count == 0)
            {
                EnsureActiveSetupHasResolvablePlan();
                RefreshScopedRegistryStatus();
                HandleDelayedEnable();
            }
        }

        private void DrawHeader()
        {
            Rect panelRect = EditorGUILayout.BeginHorizontal(_cardStyle);
            DrawGlassPanel(panelRect, _glassStrongColor, _borderColor);

            Texture2D logo = GetLogoTexture();
            Rect logoRect = GUILayoutUtility.GetRect(46f, 46f, GUILayout.Width(46f), GUILayout.Height(46f));
            if (logo != null)
            {
                GUI.DrawTexture(logoRect, logo, ScaleMode.ScaleToFit, true);
            }

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Deucarian Bootstrap", _sectionTitleStyle);
                EditorGUILayout.LabelField(
                    "Git-channel setup for the Deucarian package ecosystem.",
                    _mutedStyle);
            }

            GUILayout.Space(16f);
            DrawChannelSelector(GUILayout.Width(310f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCompactSetupSummary()
        {
            BootstrapStatusCardModel[] cards = BuildStatusCards();
            Rect gridRect = GUILayoutUtility.GetRect(1f, StatusGridHeight, GUILayout.ExpandWidth(true));
            float gap = 10f;
            float cardWidth = (gridRect.width - gap) * 0.5f;
            float cardHeight = (gridRect.height - gap) * 0.5f;

            for (int i = 0; i < cards.Length; i++)
            {
                int row = i / 2;
                int column = i % 2;
                Rect cardRect = new Rect(
                    gridRect.x + column * (cardWidth + gap),
                    gridRect.y + row * (cardHeight + gap),
                    cardWidth,
                    cardHeight);
                DrawStatusCard(cardRect, cards[i]);
            }

            if (!string.IsNullOrWhiteSpace(_error))
            {
                GUILayout.Space(8f);
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }
        }

        private void DrawSetupActions()
        {
            Rect panelRect = EditorGUILayout.BeginVertical(_cardStyle);
            DrawGlassPanel(panelRect, _glassPanelColor, _borderColor);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_setupActive || IsRequestActive))
                {
                    GUIContent refreshContent = new GUIContent(
                        "Refresh",
                        "Refresh installed packages and setup status.");
                    if (GUILayout.Button(refreshContent, _utilityButtonStyle, GUILayout.Width(78f), GUILayout.Height(24f)))
                    {
                        RefreshStatus();
                    }
                }

                GUILayout.Space(12f);
                DrawStartupPreferenceToggle(true);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("GitHub", "Open the Bootstrap repository."), _utilityButtonStyle, GUILayout.Width(66f), GUILayout.Height(24f)))
                {
                    Application.OpenURL(DeucarianBootstrapPackageConstants.GitHubUrl);
                }

                if (GUILayout.Button(new GUIContent("Docs", "Open Bootstrap documentation."), _utilityButtonStyle, GUILayout.Width(56f), GUILayout.Height(24f)))
                {
                    Application.OpenURL(DeucarianBootstrapPackageConstants.DocumentationUrl);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSetupDetails()
        {
            Rect panelRect = EditorGUILayout.BeginVertical(_cardStyle);
            DrawGlassPanel(panelRect, _glassPanelColor, _borderColor);

            EditorGUI.BeginChangeCheck();
            bool expanded = EditorGUILayout.Foldout(_setupDetailsExpanded, "Setup Details", true, _foldoutStyle);
            if (EditorGUI.EndChangeCheck())
            {
                _setupDetailsExpanded = expanded;
                SessionState.SetBool(SetupDetailsExpandedKey, _setupDetailsExpanded);
            }

            if (!_setupDetailsExpanded)
            {
                EditorGUILayout.LabelField(
                    "Full Git URLs, install plan, status log, and deferred scoped-registry diagnostics are available here.",
                    _miniMutedStyle);
                EditorGUILayout.EndVertical();
                return;
            }

            GUILayout.Space(8f);
            DrawDetailedStatusRows();
            DrawStatusMessages();

            GUILayout.Space(10f);
            DrawInstallPlanContents();
            EditorGUILayout.EndVertical();
        }

        internal BootstrapStatusCardModel[] BuildStatusCards()
        {
            return new[]
            {
                new BootstrapStatusCardModel(
                    "Registry",
                    GetRegistryStatusCardValue(),
                    GetRegistryStatusCardSubtext(),
                    GetCatalogStatusKind(),
                    GetCatalogStatusDetail()),
                new BootstrapStatusCardModel(
                    "Setup packages",
                    GetRequiredPackagesStatusCardValue(),
                    GetRequiredPackagesStatusCardSubtext(),
                    GetRequiredPackagesSummaryKind(),
                    GetSetupSummary()),
                new BootstrapStatusCardModel(
                    "Package Installer",
                    GetPackageInstallerSetupStateText(),
                    GetPackageInstallerStatusCardSubtext(),
                    GetPackageInstallerAvailabilityKind(),
                    GetPackageInstallerSetupStateDetail()),
                new BootstrapStatusCardModel(
                    "Startup",
                    ShouldShowOnStartup() ? "Enabled" : "Manual",
                    ShouldShowOnStartup() ? "Opens setup hub" : "Manual launch",
                    ShouldShowOnStartup() ? BootstrapStatusKind.Info : BootstrapStatusKind.Neutral,
                    "Project setting. Startup opens Bootstrap only; it never installs packages automatically.")
            };
        }

        private void DrawStatusCard(Rect cardRect, BootstrapStatusCardModel card)
        {
            DrawGlassPanel(cardRect, _glassPanelColor, GetStatusBorderColor(card.Kind));

            Rect iconRect = new Rect(cardRect.x + 12f, cardRect.y + 13f, 22f, 22f);
            GUIStyle iconStyle = new GUIStyle(_statusIconStyle);
            iconStyle.normal.background = TextureForColor("summary-" + card.Kind, BootstrapVisualResources.WithAlpha(GetStatusColor(card.Kind), 0.82f));
            GUI.Label(iconRect, GetStatusMarker(card.Kind), iconStyle);

            Rect labelRect = new Rect(iconRect.xMax + 10f, cardRect.y + 10f, cardRect.width - 54f, 16f);
            Rect valueRect = new Rect(iconRect.xMax + 10f, labelRect.yMax + 1f, cardRect.width - 54f, 18f);
            Rect subtextRect = new Rect(iconRect.xMax + 10f, valueRect.yMax + 2f, cardRect.width - 54f, 16f);

            GUI.Label(labelRect, new GUIContent(card.Label, card.Tooltip), _summaryLabelStyle);
            GUI.Label(valueRect, new GUIContent(card.Value, card.Tooltip), _summaryValueStyle);
            GUI.Label(subtextRect, new GUIContent(card.Subtext, card.Tooltip), _summarySubtextStyle);
        }

        private void DrawGlassPanel(Rect rect, Color backgroundColor, Color borderColor)
        {
            BootstrapVisualResources.DrawFrostedSurface(rect, backgroundColor, borderColor);
        }

        private Color GetStatusBorderColor(BootstrapStatusKind kind)
        {
            Color color = GetStatusColor(kind);
            color.a = kind == BootstrapStatusKind.Neutral ? 0.26f : 0.52f;
            return color;
        }

        private void DrawSummaryItem(string label, string value, BootstrapStatusKind kind, params GUILayoutOption[] options)
        {
            Rect itemRect = GUILayoutUtility.GetRect(1f, 32f, options);
            EditorGUI.DrawRect(itemRect, _rowBackgroundColor);

            Rect iconRect = new Rect(itemRect.x + 8f, itemRect.y + 7f, 18f, 18f);
            GUIStyle iconStyle = new GUIStyle(_statusIconStyle);
            iconStyle.normal.background = TextureForColor("summary-" + kind, GetStatusColor(kind));
            GUI.Label(iconRect, GetStatusMarker(kind), iconStyle);

            Rect labelRect = new Rect(iconRect.xMax + 7f, itemRect.y + 7f, Mathf.Min(126f, itemRect.width * 0.62f), 18f);
            Rect valueRect = new Rect(labelRect.xMax + 4f, itemRect.y + 7f, Mathf.Max(32f, itemRect.xMax - labelRect.xMax - 12f), 18f);
            GUI.Label(labelRect, label ?? string.Empty, _summaryLabelStyle);
            GUI.Label(valueRect, value ?? string.Empty, _summaryValueStyle);
        }

        private void DrawStartupSummaryIndicator(params GUILayoutOption[] options)
        {
            BootstrapStatusKind kind = ShouldShowOnStartup()
                ? BootstrapStatusKind.Info
                : BootstrapStatusKind.Neutral;
            Rect itemRect = GUILayoutUtility.GetRect(1f, 32f, options);
            EditorGUI.DrawRect(itemRect, _rowBackgroundColor);

            Rect sourceRect = new Rect(itemRect.x + 8f, itemRect.y + 7f, itemRect.width - 16f, 18f);
            GUI.Label(
                sourceRect,
                "Startup: " + (ShouldShowOnStartup() ? "opens setup hub" : "manual"),
                _summaryLabelStyle);
        }

        private void DrawDetailedStatusRows()
        {
            DrawStatusRow(
                "Registry/catalog loaded",
                GetCatalogStatusText(),
                GetCatalogStatusDetail(),
                GetCatalogStatusKind(),
                false);

            DrawStatusRow(
                "Selected channel",
                BootstrapChannelUtility.GetDisplayName(_selectedChannel),
                BootstrapChannelUtility.GetDescription(_selectedChannel),
                BootstrapStatusKind.Info,
                true);

            DrawStatusRow(
                "Visual assets",
                GetVisualAssetsStatusText(),
                GetVisualAssetsStatusDetail(),
                GetVisualAssetsStatusKind(),
                false);

            DrawStatusRow(
                "Package Installer target",
                GetPackageInstallerTargetUrlText(),
                GetPackageInstallerTargetUrlDetail(),
                BootstrapStatusKind.Info,
                true);

            DrawStatusRow(
                "Target Package Installer version",
                GetTargetPackageInstallerVersionText(),
                GetTargetPackageInstallerVersionDetail(),
                GetTargetPackageInstallerVersionKind(),
                false);

            DrawStatusRow(
                "Installed Package Installer version",
                GetInstalledPackageInstallerVersionText(),
                GetInstalledPackageInstallerVersionDetail(),
                GetPackageInstallerAvailabilityKind(),
                true);

            DrawStatusRow(
                "Installed source/channel",
                GetInstalledPackageInstallerSourceText(),
                GetInstalledPackageInstallerSourceDetail(),
                GetPackageInstallerAvailabilityKind(),
                false);

            DrawStatusRow(
                "Setup state",
                GetPackageInstallerSetupStateText(),
                GetPackageInstallerSetupStateDetail(),
                GetPackageInstallerAvailabilityKind(),
                true);

            DrawStatusRow(
                "Scoped registry",
                "Deferred",
                "Deferred. Git URLs are the supported distribution path for now.",
                BootstrapStatusKind.Neutral,
                false);

            for (int i = 0; i < RequiredSetupPackages.Length; i++)
            {
                BootstrapSetupPackage package = RequiredSetupPackages[i];
                DrawStatusRow(
                    package.PackageId,
                    GetPackageStatusText(package.PackageId),
                    package.DisplayName,
                    GetPackageStatusKind(package.PackageId),
                    i % 2 == 0);
            }
        }

        private void DrawStatusMessages()
        {
            if (!string.IsNullOrWhiteSpace(_catalogNotice))
            {
                GUILayout.Space(8f);
                EditorGUILayout.HelpBox(_catalogNotice, MessageType.Info);
            }

            if (!string.IsNullOrWhiteSpace(_error))
            {
                GUILayout.Space(8f);
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }
        }

        private void DrawChannelSelector(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(options))
            {
                EditorGUILayout.LabelField("Channel", _miniMutedStyle);
                EditorGUI.BeginChangeCheck();
                int selectedIndex = EditorGUILayout.Popup(
                    (int)_selectedChannel,
                    new[] { "Stable", "Development" },
                    GUILayout.Width(170f),
                    GUILayout.Height(22f));
                if (EditorGUI.EndChangeCheck())
                {
                    SetChannel((BootstrapChannel)Mathf.Clamp(selectedIndex, 0, 1));
                }

                EditorGUILayout.LabelField(
                    BootstrapChannelUtility.GetDescription(_selectedChannel),
                    _miniMutedStyle,
                    GUILayout.Width(300f));
            }
        }

        private void DrawPackageInstallerProductCard()
        {
            bool ready = GetHeroState() == BootstrapHeroState.Ready;

            Rect heroRect = GUILayoutUtility.GetRect(1f, HeroCardHeight, GUILayout.ExpandWidth(true), GUILayout.Height(HeroCardHeight));
            DrawHeroBackground(heroRect, ready);

            Rect badgeRect = new Rect(heroRect.x + 18f, heroRect.y + 16f, 176f, 22f);
            GUI.Label(badgeRect, BootstrapChannelUtility.GetDisplayName(_selectedChannel).ToUpperInvariant() + " GIT CHANNEL", _badgeStyle);

            float contentWidth = Mathf.Min(620f, Mathf.Max(420f, heroRect.width - 64f));
            float contentX = heroRect.x + (heroRect.width - contentWidth) * 0.5f;

            Rect logoArea = new Rect(contentX, heroRect.y + 42f, contentWidth, 74f);
            DrawCenteredPackageInstallerLogo(logoArea, GetPackageInstallerLogoAlpha(), 72f);

            if (ready)
            {
                EditorGUIUtility.AddCursorRect(logoArea, MouseCursor.Link);
                if (Event.current.type == EventType.MouseDown && logoArea.Contains(Event.current.mousePosition))
                {
                    OpenPackageInstaller();
                    Event.current.Use();
                }
            }

            Rect titleRect = new Rect(contentX, heroRect.y + 120f, contentWidth, 30f);
            GUI.Label(titleRect, DeucarianBootstrapPackageConstants.DisplayName, _heroTitleStyle);

            Rect subtitleRect = new Rect(contentX, titleRect.yMax + 1f, contentWidth, 22f);
            GUI.Label(subtitleRect, "Install or repair the Deucarian package setup.", _heroSubtitleLargeStyle);

            Rect noteRect = new Rect(contentX, subtitleRect.yMax + 3f, contentWidth, 18f);
            GUI.Label(noteRect, GetHeroChannelSummary(), _heroEyebrowStyle);

            Rect timelineRect = new Rect(contentX, noteRect.yMax + 10f, contentWidth, 46f);
            DrawHeroSetupTimeline(timelineRect);

            Rect stripRect = new Rect(contentX, heroRect.yMax - 72f, contentWidth, 36f);
            DrawPackageInstallerStatusStrip(stripRect);

            Rect buttonRect = new Rect(
                contentX + Mathf.Max(0f, (contentWidth - 250f) * 0.5f),
                heroRect.yMax - 30f,
                Mathf.Min(250f, contentWidth),
                28f);

            using (new EditorGUI.DisabledScope(IsHeroPrimaryActionDisabled()))
            {
                GUIStyle buttonStyle = GetHeroState() == BootstrapHeroState.Ready
                    ? _primaryButtonStyle
                    : _secondaryButtonStyle;
                if (GUI.Button(buttonRect, new GUIContent(GetHeroPrimaryActionLabel(), GetHeroPrimaryActionTooltip()), buttonStyle))
                {
                    InvokeHeroPrimaryAction();
                }
            }

            if (!string.IsNullOrWhiteSpace(_packageInstallerOpenMessage))
            {
                GUILayout.Space(6f);
                EditorGUILayout.HelpBox(_packageInstallerOpenMessage, MessageType.Info);
            }
        }

        private void DrawHeroBackground(Rect heroRect, bool ready)
        {
            DrawGlassPanel(heroRect, _glassStrongColor, _borderColor);

            Rect imageRect = new Rect(heroRect.x + 1f, heroRect.y + 1f, heroRect.width - 2f, heroRect.height - 2f);
            Texture2D background = GetHeroBackgroundTexture();
            if (background != null)
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.32f);
                GUI.DrawTexture(imageRect, background, ScaleMode.ScaleAndCrop, false);
                GUI.color = previousColor;
            }

            Color vignette = ready
                ? new Color(0f, 0f, 0f, 0.12f)
                : new Color(0f, 0f, 0f, 0.28f);
            EditorGUI.DrawRect(imageRect, vignette);

            Color glow = _selectedChannel == BootstrapChannel.Development
                ? new Color(0.13f, 0.48f, 0.72f, 0.14f)
                : new Color(0.20f, 0.72f, 0.62f, 0.12f);
            Rect glowRect = new Rect(heroRect.x + 1f, heroRect.yMax - 92f, heroRect.width - 2f, 91f);
            EditorGUI.DrawRect(glowRect, glow);
        }

        private void DrawCenteredPackageInstallerLogo(Rect logoArea, float alpha, float logoSize = 96f)
        {
            Texture2D logo = GetLogoTexture();
            Rect logoRect = new Rect(
                logoArea.x + Mathf.Max(0f, (logoArea.width - logoSize) * 0.5f),
                logoArea.y + Mathf.Max(0f, (logoArea.height - logoSize) * 0.5f),
                logoSize,
                logoSize);

            Color previousColor = GUI.color;
            GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * alpha);
            GUI.DrawTexture(logoRect, logo, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
        }

        private void DrawPackageInstallerStatusStrip()
        {
            Rect stripRect = GUILayoutUtility.GetRect(1f, 32f, GUILayout.ExpandWidth(true));
            DrawPackageInstallerStatusStrip(stripRect);
        }

        private void DrawPackageInstallerStatusStrip(Rect stripRect)
        {
            BootstrapStatusKind kind = GetPackageInstallerProductStatusKind();
            DrawGlassPanel(stripRect, BootstrapVisualResources.WithAlpha(GetStatusColor(kind), 0.28f), GetStatusBorderColor(kind));

            Rect packageIconRect = new Rect(stripRect.x + 10f, stripRect.y + 6f, 24f, 24f);
            Rect markerRect = new Rect(stripRect.x + 42f, stripRect.y + 8f, 18f, 20f);
            Rect statusRect = new Rect(stripRect.x + 64f, stripRect.y + 8f, 136f, 20f);
            Rect detailRect = new Rect(stripRect.x + 208f, stripRect.y + 8f, Mathf.Max(40f, stripRect.width - 220f), 20f);

            GUI.DrawTexture(packageIconRect, GetPackageIconTexture(), ScaleMode.ScaleToFit, true);
            GUI.Label(markerRect, GetStatusMarker(kind), _productStatusStyle);
            GUI.Label(statusRect, new GUIContent(GetPackageInstallerProductStatusText(), GetPackageInstallerSetupStateDetail()), _productStatusStyle);
            GUI.Label(detailRect, new GUIContent(GetPackageInstallerProductStatusDetail(), GetPackageInstallerSetupStateDetail()), _productStatusDetailStyle);
        }

        private void DrawHeroSetupTimeline(Rect timelineRect)
        {
            if (GetHeroState() == BootstrapHeroState.Ready)
            {
                DrawHeroReadySummary(timelineRect);
                return;
            }

            Rect titleRect = new Rect(timelineRect.x, timelineRect.y, timelineRect.width, 14f);
            GUI.Label(titleRect, "Setup progress", _heroEyebrowStyle);

            Rect rowRect = new Rect(timelineRect.x, timelineRect.y + 17f, timelineRect.width, 28f);
            BootstrapTimelineItem[] items = BuildHeroTimeline();
            float gap = 6f;
            float itemWidth = (rowRect.width - gap * (items.Length - 1)) / items.Length;

            for (int i = 0; i < items.Length; i++)
            {
                Rect itemRect = new Rect(rowRect.x + i * (itemWidth + gap), rowRect.y, itemWidth, rowRect.height);
                DrawTimelineItem(itemRect, items[i]);
            }
        }

        private void DrawHeroReadySummary(Rect timelineRect)
        {
            Color background = GetStatusColor(BootstrapStatusKind.Success);
            background.a = 0.22f;
            DrawGlassPanel(timelineRect, background, GetStatusBorderColor(BootstrapStatusKind.Success));

            Rect iconRect = new Rect(timelineRect.x + 12f, timelineRect.y + 12f, 20f, 20f);
            GUIStyle iconStyle = new GUIStyle(_statusIconStyle);
            iconStyle.normal.background = TextureForColor("hero-ready-summary", GetStatusColor(BootstrapStatusKind.Success));
            GUI.Label(iconRect, GetStatusMarker(BootstrapStatusKind.Success), iconStyle);

            Rect labelRect = new Rect(iconRect.xMax + 10f, timelineRect.y + 6f, timelineRect.width - 54f, 18f);
            Rect detailRect = new Rect(iconRect.xMax + 10f, labelRect.yMax + 1f, timelineRect.width - 54f, 18f);
            GUI.Label(labelRect, "Package Installer matches " + BootstrapChannelUtility.GetDisplayName(_selectedChannel) + ".", _productStatusStyle);
            GUI.Label(detailRect, "Package Installer is installed and matches the selected channel.", _productStatusDetailStyle);
        }

        private void DrawTimelineItem(Rect itemRect, BootstrapTimelineItem item)
        {
            BootstrapStatusKind kind = GetTimelineStatusKind(item.State);
            Color background = GetStatusColor(kind);
            background.a = item.State == BootstrapTimelineState.Pending ? 0.16f : 0.26f;
            DrawGlassPanel(itemRect, background, GetStatusBorderColor(kind));

            Rect markerRect = new Rect(itemRect.x + 6f, itemRect.y + 5f, 18f, 18f);
            GUIStyle markerStyle = new GUIStyle(_statusIconStyle);
            markerStyle.normal.background = TextureForColor("timeline-" + item.State, GetStatusColor(kind));
            GUI.Label(markerRect, GetTimelineMarker(item.State), markerStyle);

            Rect labelRect = new Rect(markerRect.xMax + 5f, itemRect.y + 5f, itemRect.width - 31f, 18f);
            GUI.Label(labelRect, new GUIContent(item.Label, item.Tooltip), _timelineLabelStyle);
        }

        private BootstrapTimelineItem[] BuildHeroTimeline()
        {
            return new[]
            {
                new BootstrapTimelineItem(GetRegistryTimelineLabel(), GetRegistryTimelineState(), GetRegistryTimelineTooltip()),
                new BootstrapTimelineItem("Editor", GetPackageTimelineState(DeucarianBootstrapPackageConstants.EditorPackageId), "Deucarian Editor resolved."),
                new BootstrapTimelineItem("Logging", GetPackageTimelineState(DeucarianBootstrapPackageConstants.LoggingPackageId), "Deucarian Logging resolved."),
                new BootstrapTimelineItem("Installer", GetPackageTimelineState(DeucarianBootstrapPackageConstants.PackageInstallerPackageId), "Package Installer package resolved."),
                new BootstrapTimelineItem("Available", GetPackageInstallerAvailableTimelineState(), GetHeroPrimaryActionTooltip())
            };
        }

        private string GetRegistryTimelineLabel()
        {
            return BootstrapChannelUtility.GetDisplayName(_selectedChannel);
        }

        private string GetRegistryTimelineTooltip()
        {
            return BootstrapChannelUtility.GetDescription(_selectedChannel);
        }

        private BootstrapTimelineState GetRegistryTimelineState()
        {
            return (_catalogRequest != null || _targetVersionRequest != null || (_setupActive && !_catalogLoaded))
                ? BootstrapTimelineState.Current
                : BootstrapTimelineState.Done;
        }

        private BootstrapTimelineState GetPackageTimelineState(string packageId)
        {
            if (IsPackageInstalled(packageId))
            {
                return BootstrapTimelineState.Done;
            }

            if (IsPackageTimelineCurrent(packageId))
            {
                return BootstrapTimelineState.Current;
            }

            if (GetHeroState() == BootstrapHeroState.NeedsRepair && !string.IsNullOrWhiteSpace(_error))
            {
                return BootstrapTimelineState.Failed;
            }

            return BootstrapTimelineState.Pending;
        }

        private bool IsPackageTimelineCurrent(string packageId)
        {
            if (!_setupActive || string.IsNullOrWhiteSpace(packageId))
            {
                return false;
            }

            if (string.Equals(_pendingPackageId, packageId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (_stepIndex >= 0 && _stepIndex < _installPlan.Count)
            {
                BootstrapPackageStep step = _installPlan[_stepIndex];
                return step != null && string.Equals(step.PackageId, packageId, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private BootstrapTimelineState GetPackageInstallerAvailableTimelineState()
        {
            BootstrapHeroState state = GetHeroState();
            if (state == BootstrapHeroState.Ready)
            {
                return BootstrapTimelineState.Done;
            }

            if (state == BootstrapHeroState.Installing || state == BootstrapHeroState.WaitingForUnity)
            {
                return BootstrapTimelineState.Current;
            }

            return state == BootstrapHeroState.NeedsRepair && !string.IsNullOrWhiteSpace(_error)
                ? BootstrapTimelineState.Failed
                : BootstrapTimelineState.Pending;
        }

        private BootstrapStatusKind GetTimelineStatusKind(BootstrapTimelineState state)
        {
            switch (state)
            {
                case BootstrapTimelineState.Done:
                    return BootstrapStatusKind.Success;
                case BootstrapTimelineState.Current:
                    return BootstrapStatusKind.Info;
                case BootstrapTimelineState.Failed:
                    return BootstrapStatusKind.Error;
                default:
                    return BootstrapStatusKind.Neutral;
            }
        }

        private static string GetTimelineMarker(BootstrapTimelineState state)
        {
            switch (state)
            {
                case BootstrapTimelineState.Done:
                    return GetStatusMarker(BootstrapStatusKind.Success);
                case BootstrapTimelineState.Current:
                    return "...";
                case BootstrapTimelineState.Failed:
                    return GetStatusMarker(BootstrapStatusKind.Error);
                default:
                    return GetStatusMarker(BootstrapStatusKind.Neutral);
            }
        }

        private void DrawInstallPlanContents()
        {
            EditorGUILayout.LabelField("Install / Repair Flow", _sectionTitleStyle);

            string status = string.IsNullOrWhiteSpace(_status)
                ? "Bootstrap is ready. Click setup when you want to install or repair the required packages."
                : _status;

            if (_setupActive && _installPlan.Count > 0 && _stepIndex < _installPlan.Count)
            {
                BootstrapPackageStep currentStep = _installPlan[_stepIndex];
                status += "\nCurrent step: " + (_stepIndex + 1) + "/" + _installPlan.Count + " - " + currentStep.DisplayName + ".";
            }

            EditorGUILayout.LabelField(status, _bodyStyle);

            if (_setupActive && _installPlan.Count > 0)
            {
                DrawInstallProgressBar();
                GUILayout.Space(4f);
            }

            if (IsSetupHealthy())
            {
                EditorGUILayout.LabelField("Package Installer is installed and matches the selected channel.", _mutedStyle);
                return;
            }

            if (!_catalogLoaded)
            {
                EditorGUILayout.LabelField("Install plan will appear after the catalog loads.", _mutedStyle);
                return;
            }

            if (_installPlan.Count == 0)
            {
                EditorGUILayout.LabelField("No setup plan is available.", _mutedStyle);
                return;
            }

            for (int i = 0; i < _installPlan.Count; i++)
            {
                BootstrapPackageStep step = _installPlan[i];
                DrawStatusRow(
                    (i + 1) + ". " + step.DisplayName,
                    GetPlanStepState(i, step),
                    GetPlanStepDetail(step),
                    GetPlanStepStatusKind(i, step),
                    i % 2 != 0);
            }
        }

        private void DrawInstallProgressBar()
        {
            Rect progressRect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true));
            float progress = Mathf.Clamp01(_installPlan.Count == 0 ? 0f : (float)_stepIndex / _installPlan.Count);
            EditorGUI.ProgressBar(progressRect, progress, _stepIndex + "/" + _installPlan.Count);
        }

        private void DrawFooter()
        {
            GUILayout.Space(2f);
            Rect footerRect = GUILayoutUtility.GetRect(1f, 32f, GUILayout.ExpandWidth(true));
            DrawGlassPanel(footerRect, BootstrapVisualResources.WithAlpha(_glassPanelColor, 0.52f), _borderColor);

            Rect leftRect = new Rect(footerRect.x + 12f, footerRect.y + 8f, footerRect.width - 310f, 16f);
            Rect rightRect = new Rect(footerRect.xMax - 286f, footerRect.y + 8f, 274f, 16f);
            GUI.Label(
                leftRect,
                new GUIContent("Stable: Git #main | Development: Git #develop | scoped registry deferred/legacy", "npm/scoped registry remains deferred and legacy only."),
                _footerStyle);
            GUI.Label(
                rightRect,
                "Bootstrap " + DeucarianBootstrapPackageConstants.Version + " | " + BootstrapChannelUtility.GetDisplayName(_selectedChannel),
                _footerRightStyle);
        }

        private void DrawStatusRow(
            string label,
            string status,
            string detail,
            BootstrapStatusKind kind,
            bool alternate)
        {
            Rect rowRect = GUILayoutUtility.GetRect(1f, 46f, GUILayout.ExpandWidth(true));
            DrawGlassPanel(rowRect, alternate ? _rowAlternateBackgroundColor : _rowBackgroundColor, BootstrapVisualResources.SubtleBorder);

            Rect iconRect = new Rect(rowRect.x + 10f, rowRect.y + 13f, 20f, 20f);
            Rect labelRect = new Rect(iconRect.xMax + 10f, rowRect.y + 7f, Mathf.Min(270f, rowRect.width * 0.32f), 18f);
            Rect statusRect = new Rect(labelRect.xMax + 10f, rowRect.y + 7f, 132f, 18f);
            Rect detailRect = new Rect(iconRect.xMax + 10f, rowRect.y + 25f, Mathf.Max(120f, rowRect.xMax - iconRect.xMax - 22f), 16f);

            Color statusColor = GetStatusColor(kind);
            GUIStyle iconStyle = new GUIStyle(_statusIconStyle);
            iconStyle.normal.background = TextureForColor("status-" + kind, BootstrapVisualResources.WithAlpha(statusColor, 0.84f));
            GUI.Label(iconRect, GetStatusMarker(kind), iconStyle);
            GUI.Label(labelRect, new GUIContent(label ?? string.Empty, detail ?? string.Empty), _statusLabelStyle);
            GUI.Label(statusRect, new GUIContent(status ?? string.Empty, detail ?? string.Empty), _statusDetailStyle);
            GUI.Label(detailRect, new GUIContent(detail ?? string.Empty, detail ?? string.Empty), _statusDetailStyle);
        }

        private string GetSetupSummary()
        {
            if (_installedPackageIds == null)
            {
                return "Checking the setup state for the required Deucarian packages.";
            }

            int missing = RequiredSetupPackages.Count(package => !IsPackageInstalled(package.PackageId));
            if (missing == 0)
            {
                return "Required setup packages are installed. Package Installer can manage Deucarian packages.";
            }

            return missing + " setup package" + (missing == 1 ? " is" : "s are") + " missing. Setup will install only what is needed.";
        }

        internal string GetHeroPrimaryActionLabel()
        {
            switch (GetHeroState())
            {
                case BootstrapHeroState.Ready:
                    return "Open Package Installer";
                case BootstrapHeroState.WaitingForUnity:
                    return "Waiting for Unity...";
                case BootstrapHeroState.Installing:
                    return "Installing...";
                case BootstrapHeroState.Checking:
                    return "Waiting for Unity...";
                case BootstrapHeroState.Interrupted:
                    return GetRepairActionLabel();
                case BootstrapHeroState.NeedsRepair:
                    return GetRepairActionLabel();
                default:
                    return AreSetupDependenciesInstalled() ? "Install Package Installer" : "Install Deucarian Setup";
            }
        }

        internal bool IsHeroPrimaryActionDisabled()
        {
            BootstrapHeroState state = GetHeroState();
            return state == BootstrapHeroState.Installing ||
                state == BootstrapHeroState.WaitingForUnity ||
                state == BootstrapHeroState.Checking ||
                (IsRequestActive && state != BootstrapHeroState.Ready);
        }

        private string GetHeroPrimaryActionTooltip()
        {
            switch (GetHeroState())
            {
                case BootstrapHeroState.Ready:
                    return "Open Deucarian Package Installer.";
                case BootstrapHeroState.WaitingForUnity:
                    return "Unity is resolving packages. Bootstrap will continue automatically.";
                case BootstrapHeroState.Installing:
                    return "Setup is already running.";
                case BootstrapHeroState.Checking:
                    return "Bootstrap is checking installed packages.";
                case BootstrapHeroState.Interrupted:
                    return "Continue the saved setup plan.";
                case BootstrapHeroState.NeedsRepair:
                    return GetPackageInstallerSetupStateDetail();
                default:
                    return "Install Package Installer and required setup packages from selected Git URLs.";
            }
        }

        private void InvokeHeroPrimaryAction()
        {
            if (GetHeroState() == BootstrapHeroState.Ready)
            {
                OpenPackageInstaller();
                return;
            }

            if (IsHeroPrimaryActionDisabled())
            {
                return;
            }

            StartSetup();
        }

        internal BootstrapHeroState GetHeroState()
        {
            if (_setupActive && _waitingForPackageRefresh)
            {
                return BootstrapHeroState.WaitingForUnity;
            }

            if (_setupActive)
            {
                return BootstrapHeroState.Installing;
            }

            if (_setupInterrupted && string.IsNullOrWhiteSpace(_error))
            {
                return BootstrapHeroState.Interrupted;
            }

            if (!string.IsNullOrWhiteSpace(_error))
            {
                return BootstrapHeroState.NeedsRepair;
            }

            if (IsRequestActive || _installedPackageIds == null)
            {
                return BootstrapHeroState.Checking;
            }

            if (IsSetupHealthy())
            {
                return BootstrapHeroState.Ready;
            }

            if (HasSetupProblem())
            {
                return BootstrapHeroState.NeedsRepair;
            }

            return BootstrapHeroState.NotSetUp;
        }

        private string GetHeroChannelSummary()
        {
            return GetHeroShortTargetText(_selectedChannel);
        }

        internal static string GetHeroShortTargetText(BootstrapChannel channel)
        {
            return BootstrapChannelUtility.GetDisplayName(channel) +
                " - Package Installer #" +
                BootstrapChannelUtility.GetGitBranch(channel);
        }

        private string GetRegistryStatusCardValue()
        {
            if (_catalogLoaded)
            {
                return GetRegistrySourceSummary().Equals("remote", StringComparison.OrdinalIgnoreCase)
                    ? "Remote"
                    : "Fallback";
            }

            if (_catalogRequest != null)
            {
                return "Loading";
            }

            return string.IsNullOrWhiteSpace(_error) ? "Pending" : "Error";
        }

        private string GetRegistryStatusCardSubtext()
        {
            if (_catalogLoaded && GetRegistrySourceSummary().Equals("fallback", StringComparison.OrdinalIgnoreCase))
            {
                return "Bundled fallback catalog";
            }

            return "Package Registry #" + BootstrapChannelUtility.GetGitBranch(_selectedChannel);
        }

        private string GetRequiredPackagesStatusCardValue()
        {
            if (_installedPackageIds == null)
            {
                return _listRequest != null ? "Checking" : "Unknown";
            }

            int dependencyCount = RequiredSetupPackages.Count(package =>
                !string.Equals(package.PackageId, DeucarianBootstrapPackageConstants.PackageInstallerPackageId, StringComparison.OrdinalIgnoreCase));
            int installed = RequiredSetupPackages.Count(package =>
                !string.Equals(package.PackageId, DeucarianBootstrapPackageConstants.PackageInstallerPackageId, StringComparison.OrdinalIgnoreCase) &&
                IsPackageInstalled(package.PackageId));

            if (installed == dependencyCount)
            {
                return "Ready";
            }

            return "Missing " + (dependencyCount - installed);
        }

        private string GetRequiredPackagesStatusCardSubtext()
        {
            if (_installedPackageIds == null)
            {
                return "Editor + Logging";
            }

            int dependencyCount = RequiredSetupPackages.Count(package =>
                !string.Equals(package.PackageId, DeucarianBootstrapPackageConstants.PackageInstallerPackageId, StringComparison.OrdinalIgnoreCase));
            int installed = RequiredSetupPackages.Count(package =>
                !string.Equals(package.PackageId, DeucarianBootstrapPackageConstants.PackageInstallerPackageId, StringComparison.OrdinalIgnoreCase) &&
                IsPackageInstalled(package.PackageId));

            return installed == dependencyCount
                ? "Editor + Logging"
                : installed + "/" + dependencyCount + " resolved";
        }

        private string GetPackageInstallerStatusCardSubtext()
        {
            BootstrapInstalledPackageInfo packageInfo = GetInstalledPackageInfo(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);

            if (packageInfo == null)
            {
                return "Package Installer #" + BootstrapChannelUtility.GetGitBranch(_selectedChannel);
            }

            string version = string.IsNullOrWhiteSpace(packageInfo.Version) ? "Version unknown" : packageInfo.Version;
            return version + " - " + GetInstalledPackageInstallerSourceText();
        }

        internal static bool ArePackageVisualAssetsAvailable()
        {
            return IsPackageTextureAvailable(DeucarianBootstrapPackageConstants.LogoAssetRelativePath) &&
                IsPackageTextureAvailable(DeucarianBootstrapPackageConstants.WallpaperAssetRelativePath) &&
                IsPackageTextureAvailable(DeucarianBootstrapPackageConstants.HeroBackgroundAssetRelativePath);
        }

        private string GetVisualAssetsStatusText()
        {
            return ArePackageVisualAssetsAvailable() ? "Loaded" : "Fallback";
        }

        private string GetVisualAssetsStatusDetail()
        {
            if (ArePackageVisualAssetsAvailable())
            {
                return "Package-local wallpaper, hero, and logo assets loaded.";
            }

            return "Optional visual asset missing. Procedural fallback keeps Bootstrap readable.";
        }

        private BootstrapStatusKind GetVisualAssetsStatusKind()
        {
            return ArePackageVisualAssetsAvailable() ? BootstrapStatusKind.Success : BootstrapStatusKind.Neutral;
        }

        private string GetRequiredPackagesSummaryText()
        {
            if (_installedPackageIds == null)
            {
                return "Checking";
            }

            int dependencyCount = RequiredSetupPackages.Count(package =>
                !string.Equals(package.PackageId, DeucarianBootstrapPackageConstants.PackageInstallerPackageId, StringComparison.OrdinalIgnoreCase));
            int installed = RequiredSetupPackages.Count(package =>
                !string.Equals(package.PackageId, DeucarianBootstrapPackageConstants.PackageInstallerPackageId, StringComparison.OrdinalIgnoreCase) &&
                IsPackageInstalled(package.PackageId));
            return installed == dependencyCount
                ? "Yes"
                : "No (" + installed + "/" + dependencyCount + ")";
        }

        private BootstrapStatusKind GetRequiredPackagesSummaryKind()
        {
            if (_installedPackageIds == null)
            {
                return BootstrapStatusKind.Info;
            }

            return AreSetupDependenciesInstalled() ? BootstrapStatusKind.Success : BootstrapStatusKind.Neutral;
        }

        private string GetResumeStatus()
        {
            if (_waitingForPackageRefresh && !string.IsNullOrWhiteSpace(_pendingPackageId))
            {
                return "Waiting for Unity package refresh after installing " + _pendingPackageId + "...";
            }

            return "Continuing setup...";
        }

        private string GetCatalogStatusText()
        {
            if (_catalogLoaded)
            {
                return "Loaded";
            }

            if (_catalogRequest != null)
            {
                return "Loading";
            }

            if (!string.IsNullOrWhiteSpace(_error))
            {
                return "Error";
            }

            return "Pending";
        }

        private string GetCatalogStatusDetail()
        {
            if (_catalogLoaded)
            {
                return GetRegistrySourceSummary();
            }

            if (_catalogRequest != null)
            {
                return "Remote catalog request in progress";
            }

            return "Catalog has not loaded yet";
        }

        private BootstrapStatusKind GetCatalogStatusKind()
        {
            if (_catalogLoaded)
            {
                return BootstrapStatusKind.Success;
            }

            if (_catalogRequest != null)
            {
                return BootstrapStatusKind.Info;
            }

            return string.IsNullOrWhiteSpace(_error) ? BootstrapStatusKind.Neutral : BootstrapStatusKind.Error;
        }

        private string GetScopedRegistryStatusText()
        {
            if (_scopedRegistryStatus == null)
            {
                return "Unknown";
            }

            if (_scopedRegistryStatus.Configured)
            {
                return "Yes";
            }

            return _scopedRegistryStatus.NeedsRepair ? "Repair" : "No";
        }

        private string GetScopedRegistryStatusDetail()
        {
            if (_scopedRegistryStatus == null)
            {
                return "Scoped registry status has not been checked";
            }

            return _scopedRegistryStatus.Detail;
        }

        private BootstrapStatusKind GetScopedRegistryStatusKind()
        {
            if (_scopedRegistryStatus == null)
            {
                return BootstrapStatusKind.Neutral;
            }

            if (_scopedRegistryStatus.Configured)
            {
                return BootstrapStatusKind.Success;
            }

            return _scopedRegistryStatus.NeedsRepair ? BootstrapStatusKind.Info : BootstrapStatusKind.Neutral;
        }

        private string GetPackageStatusText(string packageId)
        {
            if (_installedPackageIds == null)
            {
                return _listRequest != null ? "Checking" : "Unknown";
            }

            if (_setupActive && _waitingForPackageRefresh && string.Equals(_pendingPackageId, packageId, StringComparison.OrdinalIgnoreCase))
            {
                return "Refreshing";
            }

            return IsPackageInstalled(packageId) ? "Installed" : "Missing";
        }

        private BootstrapStatusKind GetPackageStatusKind(string packageId)
        {
            if (_installedPackageIds == null)
            {
                return _listRequest != null ? BootstrapStatusKind.Info : BootstrapStatusKind.Neutral;
            }

            if (_setupActive && _waitingForPackageRefresh && string.Equals(_pendingPackageId, packageId, StringComparison.OrdinalIgnoreCase))
            {
                return BootstrapStatusKind.Info;
            }

            return IsPackageInstalled(packageId) ? BootstrapStatusKind.Success : BootstrapStatusKind.Neutral;
        }

        private string GetPackageInstallerTargetUrlText()
        {
            return string.IsNullOrWhiteSpace(_targetPackageInstallerGitUrl)
                ? BootstrapChannelUtility.GetPackageInstallerGitUrl(_selectedChannel)
                : _targetPackageInstallerGitUrl;
        }

        private string GetPackageInstallerTargetUrlDetail()
        {
            return BootstrapChannelUtility.GetDisplayName(_selectedChannel) + " Package Installer Git URL";
        }

        private string GetTargetPackageInstallerVersionText()
        {
            return string.IsNullOrWhiteSpace(_targetPackageInstallerVersion)
                ? "Target version unknown"
                : _targetPackageInstallerVersion;
        }

        private string GetTargetPackageInstallerVersionDetail()
        {
            if (!string.IsNullOrWhiteSpace(_targetPackageInstallerVersionSource))
            {
                return _targetPackageInstallerVersionSource;
            }

            return "Target version unknown";
        }

        private BootstrapStatusKind GetTargetPackageInstallerVersionKind()
        {
            return string.IsNullOrWhiteSpace(_targetPackageInstallerVersion)
                ? BootstrapStatusKind.Info
                : BootstrapStatusKind.Success;
        }

        private string GetInstalledPackageInstallerVersionText()
        {
            BootstrapInstalledPackageInfo packageInfo = GetInstalledPackageInfo(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
            return packageInfo == null || string.IsNullOrWhiteSpace(packageInfo.Version)
                ? "Not installed"
                : packageInfo.Version;
        }

        private string GetInstalledPackageInstallerVersionDetail()
        {
            BootstrapInstalledPackageInfo packageInfo = GetInstalledPackageInfo(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
            return packageInfo == null
                ? "Package Installer is not installed."
                : DeucarianBootstrapPackageConstants.PackageInstallerPackageDisplayName;
        }

        private string GetInstalledPackageInstallerSourceText()
        {
            BootstrapInstalledPackageInfo packageInfo = GetInstalledPackageInfo(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
            if (packageInfo == null)
            {
                return "Missing";
            }

            if (packageInfo.IsRegistry)
            {
                return "Scoped registry";
            }

            if (packageInfo.IsGit && packageInfo.TryGetGitChannel(out BootstrapChannel installedChannel))
            {
                return "Git #" + BootstrapChannelUtility.GetGitBranch(installedChannel);
            }

            return string.IsNullOrWhiteSpace(packageInfo.Source) ? "Unknown" : packageInfo.Source;
        }

        private string GetInstalledPackageInstallerSourceDetail()
        {
            BootstrapInstalledPackageInfo packageInfo = GetInstalledPackageInfo(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
            if (packageInfo == null)
            {
                return "Install Package Installer from " + GetPackageInstallerTargetUrlText() + ".";
            }

            if (!string.IsNullOrWhiteSpace(packageInfo.BestReference))
            {
                return packageInfo.BestReference;
            }

            return string.IsNullOrWhiteSpace(packageInfo.Source)
                ? "Installed source could not be detected."
                : packageInfo.Source;
        }

        private BootstrapPackageInstallerSetupState GetPackageInstallerSetupState()
        {
            return BootstrapPackageInstallerStatus.Evaluate(
                _selectedChannel,
                GetInstalledPackageInfo(DeucarianBootstrapPackageConstants.PackageInstallerPackageId),
                _targetPackageInstallerVersion);
        }

        private string GetPackageInstallerSetupStateText()
        {
            if (_installedPackageIds == null)
            {
                return _listRequest != null ? "Checking" : "Unknown";
            }

            switch (GetPackageInstallerSetupState())
            {
                case BootstrapPackageInstallerSetupState.Healthy:
                    return "Healthy";
                case BootstrapPackageInstallerSetupState.Outdated:
                    return "Outdated";
                case BootstrapPackageInstallerSetupState.WrongChannel:
                    return "Wrong channel";
                case BootstrapPackageInstallerSetupState.UnknownReviewRequired:
                    return "Review required";
                default:
                    return "Missing";
            }
        }

        private string GetPackageInstallerSetupStateDetail()
        {
            switch (GetPackageInstallerSetupState())
            {
                case BootstrapPackageInstallerSetupState.Healthy:
                    return "Package Installer is installed and matches the selected channel.";
                case BootstrapPackageInstallerSetupState.Outdated:
                    return "Package Installer is installed, but an update is available for this channel.";
                case BootstrapPackageInstallerSetupState.WrongChannel:
                    return "Package Installer is installed from a different channel or source. Repair to switch to the selected Git channel.";
                case BootstrapPackageInstallerSetupState.UnknownReviewRequired:
                    return "Package Installer source could not be trusted. Review or repair to use the selected Git channel.";
                default:
                    return "Package Installer is not installed.";
            }
        }

        private string GetRepairActionLabel()
        {
            switch (GetPackageInstallerSetupState())
            {
                case BootstrapPackageInstallerSetupState.Outdated:
                    return "Update Package Installer";
                case BootstrapPackageInstallerSetupState.WrongChannel:
                    return "Switch Package Installer Channel";
                case BootstrapPackageInstallerSetupState.UnknownReviewRequired:
                    return "Repair Package Installer";
                default:
                    return "Repair Package Installer";
            }
        }

        private string GetPackageInstallerAvailabilityText()
        {
            return GetPackageInstallerSetupStateText();
        }

        private string GetPackageInstallerAvailabilityDetail()
        {
            return GetPackageInstallerSetupStateDetail();
        }

        private BootstrapStatusKind GetPackageInstallerAvailabilityKind()
        {
            if (_installedPackageIds == null)
            {
                return _listRequest != null ? BootstrapStatusKind.Info : BootstrapStatusKind.Neutral;
            }

            switch (GetPackageInstallerSetupState())
            {
                case BootstrapPackageInstallerSetupState.Healthy:
                    return BootstrapStatusKind.Success;
                case BootstrapPackageInstallerSetupState.Outdated:
                case BootstrapPackageInstallerSetupState.WrongChannel:
                    return BootstrapStatusKind.Warning;
                case BootstrapPackageInstallerSetupState.UnknownReviewRequired:
                    return BootstrapStatusKind.Error;
                default:
                    return BootstrapStatusKind.Neutral;
            }
        }

        private float GetPackageInstallerLogoAlpha()
        {
            if (GetHeroState() == BootstrapHeroState.Ready)
            {
                return 1f;
            }

            return _setupActive || _installedPackageIds == null ? 0.55f : 0.32f;
        }

        internal string GetPackageInstallerProductStatusText()
        {
            switch (GetHeroState())
            {
                case BootstrapHeroState.Ready:
                    return "Healthy";
                case BootstrapHeroState.WaitingForUnity:
                    return "Waiting for Unity";
                case BootstrapHeroState.Installing:
                    return "Installing";
                case BootstrapHeroState.Checking:
                    return "Checking";
                case BootstrapHeroState.Interrupted:
                case BootstrapHeroState.NeedsRepair:
                    return GetPackageInstallerSetupStateText();
                default:
                    return "Not installed";
            }
        }

        internal string GetPackageInstallerProductStatusDetail()
        {
            switch (GetHeroState())
            {
                case BootstrapHeroState.Ready:
                    return "Package Installer is installed and matches the selected channel.";
                case BootstrapHeroState.WaitingForUnity:
                    return "Unity is resolving packages";
                case BootstrapHeroState.Installing:
                    return "Setup is installing required packages";
                case BootstrapHeroState.Checking:
                    return "Checking installed packages";
                case BootstrapHeroState.Interrupted:
                    return "Continue setup to finish installation";
                case BootstrapHeroState.NeedsRepair:
                    return GetPackageInstallerSetupStateDetail();
                default:
                    return "Setup required";
            }
        }

        private BootstrapStatusKind GetPackageInstallerProductStatusKind()
        {
            switch (GetHeroState())
            {
                case BootstrapHeroState.Ready:
                    return BootstrapStatusKind.Success;
                case BootstrapHeroState.WaitingForUnity:
                case BootstrapHeroState.Installing:
                case BootstrapHeroState.Checking:
                case BootstrapHeroState.Interrupted:
                    return BootstrapStatusKind.Info;
                case BootstrapHeroState.NeedsRepair:
                    return string.IsNullOrWhiteSpace(_error) ? BootstrapStatusKind.Info : BootstrapStatusKind.Error;
                default:
                    return BootstrapStatusKind.Neutral;
            }
        }

        private string GetPackageInstallerCardButtonText()
        {
            return GetHeroPrimaryActionLabel();
        }

        private string GetPlanStepState(int index, BootstrapPackageStep step)
        {
            if (_installedPackageIds != null && _installedPackageIds.Contains(step.PackageId))
            {
                return "Installed";
            }

            if (_setupActive && string.Equals(_pendingPackageId, step.PackageId, StringComparison.OrdinalIgnoreCase))
            {
                return _waitingForPackageRefresh ? "Waiting" : "Installing";
            }

            if (_setupActive && index == _stepIndex)
            {
                return _addRequest != null ? "Installing" : "Next";
            }

            if (index < _stepIndex)
            {
                return "Done";
            }

            return "Pending";
        }

        private static string GetPlanStepDetail(BootstrapPackageStep step)
        {
            if (step == null)
            {
                return string.Empty;
            }

            return step.InstallSource == BootstrapPackageInstallSource.ScopedRegistry
                ? "Deferred legacy registry: " + step.PackageReference
                : "Git: " + step.PackageReference;
        }

        private BootstrapStatusKind GetPlanStepStatusKind(int index, BootstrapPackageStep step)
        {
            if (_installedPackageIds != null && _installedPackageIds.Contains(step.PackageId))
            {
                return BootstrapStatusKind.Success;
            }

            if (_setupActive && string.Equals(_pendingPackageId, step.PackageId, StringComparison.OrdinalIgnoreCase))
            {
                return BootstrapStatusKind.Info;
            }

            if (_setupActive && index == _stepIndex)
            {
                return BootstrapStatusKind.Info;
            }

            return BootstrapStatusKind.Neutral;
        }

        private void RefreshStatus()
        {
            if (IsRequestActive)
            {
                return;
            }

            _setupActive = false;
            _setupInterrupted = false;
            _continueSetupAfterPackageList = false;
            _waitingForPackageRefresh = false;
            _packageListRetryQueued = false;
            _pendingPackageId = string.Empty;
            _packageListRetryCount = 0;
            _stepIndex = Mathf.Clamp(_stepIndex, 0, _installPlan.Count);
            _packageInstallerOpenMessage = string.Empty;
            RefreshScopedRegistryStatus();

            ReloadCatalog();
        }

        private void SetChannel(BootstrapChannel channel)
        {
            if (_selectedChannel == channel && _catalogLoaded)
            {
                return;
            }

            _selectedChannel = channel;
            _setupActive = false;
            _setupInterrupted = false;
            _continueSetupAfterPackageList = false;
            _waitingForPackageRefresh = false;
            _packageListRetryQueued = false;
            _pendingPackageId = string.Empty;
            _packageListRetryCount = 0;
            _stepIndex = 0;
            _error = string.Empty;
            _packageInstallerOpenMessage = string.Empty;
            _targetPackageInstallerGitUrl = BootstrapChannelUtility.GetPackageInstallerGitUrl(_selectedChannel);
            _targetPackageInstallerVersion = string.Empty;
            _targetPackageInstallerVersionSource = string.Empty;
            SetPersistedChannel(_selectedChannel);

            SaveState();
            ReloadCatalog();
            Repaint();
        }

        private void RefreshScopedRegistryStatus()
        {
            _scopedRegistryStatus = BootstrapScopedRegistryManifest.GetStatus();
        }

        private void RepairScopedRegistry()
        {
            BootstrapScopedRegistryRepairResult result = BootstrapScopedRegistryManifest.EnsureConfigured();
            RefreshScopedRegistryStatus();

            if (!result.Success)
            {
                _status = "Scoped registry repair failed.";
                _error = result.ErrorMessage;
                SaveState();
                Repaint();
                return;
            }

            _status = result.Message;
            _error = string.Empty;
            SaveState();
            Repaint();
        }

        private bool ConfigureScopedRegistryForSetup()
        {
            BootstrapScopedRegistryRepairResult result = BootstrapScopedRegistryManifest.EnsureConfigured();
            RefreshScopedRegistryStatus();

            if (result.Success)
            {
                _status = result.Changed
                    ? "Scoped registry configured. Checking installed packages..."
                    : "Scoped registry already configured. Checking installed packages...";
                _error = string.Empty;
                SaveState();
                return true;
            }

            Fail("Scoped registry setup failed.", result.ErrorMessage);
            return false;
        }

        private void SetScopedRegistryInstallPlan()
        {
            _installPlan.Clear();
            _installPlan.Add(new BootstrapPackageStep(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageDisplayName,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                BootstrapPackageInstallSource.ScopedRegistry));
            _catalogLoaded = true;
            _registrySource = "Scoped registry: " + DeucarianBootstrapPackageConstants.ScopedRegistryUrl;
            _catalogNotice = "Deferred. Git URLs are the supported distribution path for now.";
            _stepIndex = Mathf.Clamp(_stepIndex, 0, _installPlan.Count);
            _savedPlanPackageIds = _installPlan.Select(step => step.PackageId).ToArray();
        }

        private void ReloadCatalog()
        {
            DisposeCatalogRequest();
            DisposeTargetVersionRequest();
            _catalogLoaded = false;
            _continueSetupAfterPackageList = false;
            _waitingForPackageRefresh = false;
            _packageListRetryQueued = false;
            _installPlan.Clear();
            _installedPackageIds = null;
            _installedPackagesById = null;
            _stepIndex = 0;
            _pendingPackageId = string.Empty;
            _packageListRetryCount = 0;
            _error = string.Empty;
            _catalogNotice = string.Empty;
            _registrySource = string.Empty;
            _targetPackageInstallerGitUrl = BootstrapChannelUtility.GetPackageInstallerGitUrl(_selectedChannel);
            _targetPackageInstallerVersion = string.Empty;
            _targetPackageInstallerVersionSource = string.Empty;
            _pendingCatalogFinishStatus = string.Empty;
            SaveState();
            BeginCatalogLoad("Reloading " + BootstrapChannelUtility.GetDisplayName(_selectedChannel) + " Package Registry catalog...");
        }

        private void StartSetup()
        {
            _setupActive = true;
            _setupInterrupted = false;
            _packageListRetryQueued = false;
            _stepIndex = Mathf.Clamp(_stepIndex, 0, _installPlan.Count);
            _error = string.Empty;
            _packageInstallerOpenMessage = string.Empty;

            if (!_catalogLoaded)
            {
                _status = string.IsNullOrWhiteSpace(_pendingPackageId)
                    ? "Loading package catalog before setup..."
                    : "Continuing setup...";
                SaveState();
                BeginCatalogLoad(_status);
                return;
            }

            _status = string.IsNullOrWhiteSpace(_pendingPackageId)
                ? "Checking installed packages..."
                : "Waiting for Unity package refresh...";
            SaveState();
            RefreshInstalledPackages(_status, true);
        }

        private void BeginCatalogLoad(string status)
        {
            EnsureActiveSetupHasResolvablePlan();

            if (_catalogRequest != null || _catalogLoaded)
            {
                return;
            }

            try
            {
                _status = status;
                _error = string.Empty;
                _registrySource = "Loading remote registry...";
                _catalogNotice = string.Empty;
                _targetPackageInstallerVersionSource = string.Empty;
                _catalogRequest = UnityWebRequest.Get(BootstrapChannelUtility.GetRegistryCatalogUrl(_selectedChannel));
                _catalogRequest.timeout = 15;
                _catalogRequest.SendWebRequest();
                EditorApplication.update -= UpdateRequests;
                EditorApplication.update += UpdateRequests;
                Repaint();
            }
            catch (Exception exception)
            {
                DisposeCatalogRequest();
                LoadFallbackCatalog("Could not start remote registry request: " + exception.GetBaseException().Message);
            }
        }

        private void UpdateRequests()
        {
            if (_catalogRequest != null)
            {
                UpdateCatalogRequest();
                return;
            }

            if (_targetVersionRequest != null)
            {
                UpdateTargetPackageInstallerVersionRequest();
                return;
            }

            if (_listRequest != null)
            {
                UpdateListRequest();
                return;
            }

            if (_removeRequest != null)
            {
                UpdateRemoveRequest();
                return;
            }

            if (_addRequest != null)
            {
                UpdateAddRequest();
                return;
            }

            if (_packageListRetryQueued && EditorApplication.timeSinceStartup >= _nextPackageListRefreshTime)
            {
                _packageListRetryQueued = false;
                RefreshInstalledPackages("Continuing setup. Checking installed packages...", true);
            }
        }

        private void UpdateCatalogRequest()
        {
            if (!_catalogRequest.isDone)
            {
                return;
            }

            UnityWebRequest request = _catalogRequest;
            _catalogRequest = null;

            bool success = request.result == UnityWebRequest.Result.Success;
            string responseText = success ? request.downloadHandler.text : string.Empty;
            string remoteError = success
                ? string.Empty
                : string.IsNullOrWhiteSpace(request.error)
                    ? "Remote registry request failed."
                    : request.error;

            request.Dispose();

            string parseError = string.Empty;

            string registryUrl = BootstrapChannelUtility.GetRegistryCatalogUrl(_selectedChannel);
            if (success && TryUseCatalog(responseText, "Remote: " + registryUrl, out parseError))
            {
                BeginTargetPackageInstallerVersionLoad(_setupActive
                    ? "Remote catalog loaded. Checking installed packages..."
                    : "Remote catalog loaded. Checking setup status...");
                return;
            }

            if (success)
            {
                remoteError = parseError;
            }

            LoadFallbackCatalog("Using bundled fallback catalog because the remote Package Registry could not be loaded. " + remoteError);
        }

        private void LoadFallbackCatalog(string notice)
        {
            string fallbackPath = GetFallbackCatalogPath();

            if (!File.Exists(fallbackPath))
            {
                Fail("Catalog load failed.", notice + "\nBundled fallback catalog was not found at " + fallbackPath + ".");
                return;
            }

            string fallbackJson;

            try
            {
                fallbackJson = File.ReadAllText(fallbackPath);
            }
            catch (Exception exception)
            {
                Fail("Catalog load failed.", notice + "\nBundled fallback catalog could not be read: " + exception.GetBaseException().Message);
                return;
            }

            if (!TryUseCatalog(fallbackJson, "Bundled fallback catalog", out string fallbackError))
            {
                Fail("Catalog load failed.", notice + "\nBundled fallback catalog is invalid: " + fallbackError);
                return;
            }

            _catalogNotice = notice;
            BeginTargetPackageInstallerVersionLoad(_setupActive
                ? "Fallback catalog loaded. Checking installed packages..."
                : "Fallback catalog loaded. Checking setup status...");
        }

        private static string GetFallbackCatalogPath()
        {
            UnityEditor.PackageManager.PackageInfo packageInfo =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            string packageRoot = packageInfo != null ? packageInfo.resolvedPath : Application.dataPath;
            string relativePath = DeucarianBootstrapPackageConstants.FallbackCatalogRelativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(packageRoot, relativePath);
        }

        private bool TryUseCatalog(string json, string source, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!BootstrapCatalogParser.TryParse(json, out BootstrapPackageCatalog catalog, out errorMessage))
            {
                return false;
            }

            BootstrapInstallPlanResult planResult = BootstrapInstallPlanner.BuildPlan(
                catalog,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                _selectedChannel);

            if (!planResult.Success)
            {
                errorMessage = planResult.ErrorMessage;
                return false;
            }

            _installPlan.Clear();
            _installPlan.AddRange(planResult.Steps);
            _registrySource = source;
            _catalogLoaded = true;
            _stepIndex = Mathf.Clamp(_stepIndex, 0, _installPlan.Count);
            _savedPlanPackageIds = _installPlan.Select(step => step.PackageId).ToArray();
            BootstrapPackageDefinition packageInstaller = BootstrapInstallPlanner.FindPackage(
                catalog,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
            _targetPackageInstallerGitUrl = BootstrapInstallPlanner.GetUrlForChannel(packageInstaller, _selectedChannel);
            if (string.IsNullOrWhiteSpace(_targetPackageInstallerGitUrl))
            {
                _targetPackageInstallerGitUrl = BootstrapChannelUtility.GetPackageInstallerGitUrl(_selectedChannel);
            }

            _targetPackageInstallerVersion = BootstrapInstallPlanner.GetVersionForChannel(packageInstaller, _selectedChannel);
            _targetPackageInstallerVersionSource = string.IsNullOrWhiteSpace(_targetPackageInstallerVersion)
                ? string.Empty
                : "Package Registry metadata";
            return true;
        }

        private void BeginTargetPackageInstallerVersionLoad(string finishStatus)
        {
            DisposeTargetVersionRequest();
            _pendingCatalogFinishStatus = finishStatus ?? string.Empty;

            string packageJsonUrl = BootstrapChannelUtility.GetPackageInstallerRawPackageJsonUrl(_selectedChannel);
            try
            {
                _status = "Reading target Package Installer version...";
                _targetVersionRequest = UnityWebRequest.Get(packageJsonUrl);
                _targetVersionRequest.timeout = 10;
                _targetVersionRequest.SendWebRequest();
                EditorApplication.update -= UpdateRequests;
                EditorApplication.update += UpdateRequests;
                Repaint();
            }
            catch (Exception exception)
            {
                DisposeTargetVersionRequest();
                if (string.IsNullOrWhiteSpace(_targetPackageInstallerVersion))
                {
                    _catalogNotice = AppendNotice(_catalogNotice, "Target version unknown. Could not start Package Installer package.json request: " + exception.GetBaseException().Message);
                }
                else
                {
                    _catalogNotice = AppendNotice(_catalogNotice, "Could not refresh target version from Package Installer package.json; using catalog metadata.");
                }

                FinishCatalogLoad(_pendingCatalogFinishStatus);
            }
        }

        private void UpdateTargetPackageInstallerVersionRequest()
        {
            if (_targetVersionRequest == null || !_targetVersionRequest.isDone)
            {
                return;
            }

            UnityWebRequest request = _targetVersionRequest;
            _targetVersionRequest = null;

            bool success = request.result == UnityWebRequest.Result.Success;
            string responseText = success ? request.downloadHandler.text : string.Empty;
            string requestError = success
                ? string.Empty
                : string.IsNullOrWhiteSpace(request.error)
                    ? "Package Installer package.json request failed."
                    : request.error;

            request.Dispose();

            if (success && TryReadPackageJsonVersion(responseText, out string version))
            {
                _targetPackageInstallerVersion = version;
                _targetPackageInstallerVersionSource = "Package Installer package.json (" + BootstrapChannelUtility.GetGitBranch(_selectedChannel) + ")";
            }
            else if (string.IsNullOrWhiteSpace(_targetPackageInstallerVersion))
            {
                _catalogNotice = AppendNotice(
                    _catalogNotice,
                    "Target version unknown. " + (string.IsNullOrWhiteSpace(requestError) ? "Package Installer package.json did not contain a readable version." : requestError));
            }
            else
            {
                _catalogNotice = AppendNotice(_catalogNotice, "Could not refresh target version from Package Installer package.json; using catalog metadata.");
            }

            FinishCatalogLoad(_pendingCatalogFinishStatus);
        }

        private static bool TryReadPackageJsonVersion(string json, out string version)
        {
            version = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                BootstrapPackageJson packageJson = JsonUtility.FromJson<BootstrapPackageJson>(json);
                version = packageJson != null ? packageJson.version : string.Empty;
            }
            catch
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(version);
        }

        private static string AppendNotice(string existing, string addition)
        {
            if (string.IsNullOrWhiteSpace(addition))
            {
                return existing ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(existing))
            {
                return addition;
            }

            return existing + "\n" + addition;
        }

        private void FinishCatalogLoad(string status)
        {
            if (_setupActive)
            {
                RefreshInstalledPackages(status, true);
                return;
            }

            RefreshInstalledPackages(status, false);
        }

        private void RefreshInstalledPackages(string status, bool continueSetupAfterRefresh)
        {
            if (_listRequest != null || _addRequest != null || _removeRequest != null || _targetVersionRequest != null)
            {
                return;
            }

            _packageListRetryQueued = false;

            EnsureActiveSetupHasResolvablePlan();

            if (!_catalogLoaded)
            {
                BeginCatalogLoad(status);
                return;
            }

            try
            {
                _status = status;
                _error = string.Empty;
                _continueSetupAfterPackageList = continueSetupAfterRefresh;
                _listRequest = Client.List(true, true);
                EditorApplication.update -= UpdateRequests;
                EditorApplication.update += UpdateRequests;
                Repaint();
            }
            catch (Exception exception)
            {
                _continueSetupAfterPackageList = false;
                if (_setupActive)
                {
                    SchedulePackageListRetry("Waiting for Unity package refresh. Could not start package list check: " + exception.GetBaseException().Message);
                    return;
                }

                Fail("Could not start installed package check.", exception);
            }
        }

        private void UpdateListRequest()
        {
            if (!_listRequest.IsCompleted)
            {
                return;
            }

            ListRequest request = _listRequest;
            bool continueSetup = _continueSetupAfterPackageList;
            _continueSetupAfterPackageList = false;
            _listRequest = null;

            if (request.Status != StatusCode.Success)
            {
                string detail = request.Error != null ? request.Error.message : "Package Manager returned an unknown error.";
                if (_setupActive)
                {
                    SchedulePackageListRetry("Waiting for Unity package refresh. Installed package check is not ready: " + detail);
                    return;
                }

                Fail("Installed package check failed.", detail);
                return;
            }

            _installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _installedPackagesById = new Dictionary<string, BootstrapInstalledPackageInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (UnityEditor.PackageManager.PackageInfo packageInfo in request.Result.Where(packageInfo => packageInfo != null))
            {
                _installedPackageIds.Add(packageInfo.name);

                BootstrapPackageLockEntry lockEntry = BootstrapPackageLockInspector.GetPackage(packageInfo.name);
                _installedPackagesById[packageInfo.name] = new BootstrapInstalledPackageInfo(
                    packageInfo.name,
                    packageInfo.version,
                    packageInfo.source.ToString(),
                    GetPackageInfoReference(packageInfo),
                    lockEntry != null ? lockEntry.GitUrl : string.Empty);
            }

            if (continueSetup)
            {
                ContinueFromInstalledPackages(_installedPackageIds);
                return;
            }

            _status = "Setup status refreshed.";
            SaveState();
            EditorApplication.update -= UpdateRequests;
            Repaint();
        }

        private void ContinueFromInstalledPackages(ISet<string> installedPackageIds)
        {
            if (_installPlan.Count == 0)
            {
                ReloadInstallPlanForContinuation();
                return;
            }

            if (_waitingForPackageRefresh && !string.IsNullOrWhiteSpace(_pendingPackageId))
            {
                int pendingIndex = FindPlanIndex(_installPlan, _pendingPackageId);
                if (pendingIndex >= 0)
                {
                    _stepIndex = pendingIndex;
                }
                else
                {
                    _pendingPackageId = string.Empty;
                    _waitingForPackageRefresh = false;
                    _packageListRetryCount = 0;
                    _status = "Continuing setup...";
                    SaveState();
                }

                if (_waitingForPackageRefresh && !installedPackageIds.Contains(_pendingPackageId))
                {
                    SchedulePackageListRetry("Waiting for Unity package refresh after installing " + _pendingPackageId + "...");
                    return;
                }

                if (_waitingForPackageRefresh)
                {
                    _pendingPackageId = string.Empty;
                    _waitingForPackageRefresh = false;
                    _packageListRetryCount = 0;
                    _status = "Continuing setup...";
                    SaveState();
                }
            }

            _stepIndex = FindNextActionableStepIndex(_installPlan, installedPackageIds);

            if (_stepIndex >= _installPlan.Count)
            {
                CompleteSetup();
                return;
            }

            StartInstall(_installPlan[_stepIndex]);
        }

        private void ReloadInstallPlanForContinuation()
        {
            DisposeCatalogRequest();
            _catalogLoaded = false;
            _installPlan.Clear();
            _status = "Continuing setup. Reloading package catalog...";
            _error = string.Empty;
            SaveState();
            BeginCatalogLoad(_status);
        }

        internal static int FindNextMissingStepIndex(IReadOnlyList<BootstrapPackageStep> installPlan, ISet<string> installedPackageIds)
        {
            if (installPlan == null || installPlan.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < installPlan.Count; i++)
            {
                BootstrapPackageStep step = installPlan[i];
                if (step == null || installedPackageIds == null || !installedPackageIds.Contains(step.PackageId))
                {
                    return i;
                }
            }

            return installPlan.Count;
        }

        private int FindNextActionableStepIndex(IReadOnlyList<BootstrapPackageStep> installPlan, ISet<string> installedPackageIds)
        {
            if (installPlan == null || installPlan.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < installPlan.Count; i++)
            {
                BootstrapPackageStep step = installPlan[i];
                if (step == null)
                {
                    return i;
                }

                bool isPackageInstaller = string.Equals(
                    step.PackageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    StringComparison.OrdinalIgnoreCase);

                if (isPackageInstaller)
                {
                    BootstrapPackageInstallerSetupState state = GetPackageInstallerSetupState();
                    if (state != BootstrapPackageInstallerSetupState.Healthy)
                    {
                        return i;
                    }

                    continue;
                }

                if (installedPackageIds == null || !installedPackageIds.Contains(step.PackageId))
                {
                    return i;
                }
            }

            return installPlan.Count;
        }

        private static int FindPlanIndex(IReadOnlyList<BootstrapPackageStep> installPlan, string packageId)
        {
            if (installPlan == null || string.IsNullOrWhiteSpace(packageId))
            {
                return -1;
            }

            for (int i = 0; i < installPlan.Count; i++)
            {
                BootstrapPackageStep step = installPlan[i];
                if (step != null && string.Equals(step.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private void SchedulePackageListRetry(string status)
        {
            if (_packageListRetryCount >= MaxPackageListRefreshAttempts)
            {
                InterruptSetup(
                    "Setup interrupted while waiting for Unity package refresh.",
                    "Unity did not report the pending package as installed after " + MaxPackageListRefreshAttempts + " checks. Click Continue Deucarian Setup to retry.");
                return;
            }

            _packageListRetryCount++;
            _status = status + " Checking again (" + _packageListRetryCount + "/" + MaxPackageListRefreshAttempts + ").";
            _error = string.Empty;
            _packageListRetryQueued = true;
            _nextPackageListRefreshTime = EditorApplication.timeSinceStartup + PackageListRetryDelaySeconds;
            SaveState();
            EditorApplication.update -= UpdateRequests;
            EditorApplication.update += UpdateRequests;
            Repaint();
        }

        private void InterruptSetup(string status, string error)
        {
            _setupActive = false;
            _setupInterrupted = true;
            _continueSetupAfterPackageList = false;
            _waitingForPackageRefresh = false;
            _packageListRetryQueued = false;
            _pendingPackageId = string.Empty;
            _status = status ?? "Setup interrupted.";
            _error = error ?? string.Empty;
            _listRequest = null;
            _addRequest = null;
            _removeRequest = null;
            _removeThenAddStep = null;
            DisposeCatalogRequest();
            DisposeTargetVersionRequest();
            SaveState();
            EditorApplication.update -= UpdateRequests;
            Repaint();
        }

        private void StartInstall(BootstrapPackageStep step)
        {
            if (_listRequest != null || _addRequest != null || _removeRequest != null || _packageListRetryQueued)
            {
                return;
            }

            try
            {
                _pendingPackageId = step.PackageId;
                _waitingForPackageRefresh = true;
                _packageListRetryCount = 0;
                _setupInterrupted = false;
                _status = "Installing " + step.PackageId + " from " + step.PackageReference + "...";
                _error = string.Empty;
                SaveState();

                if (ShouldRemovePackageInstallerBeforeAdd(step))
                {
                    _removeThenAddStep = step;
                    _status = "Repairing Package Installer source before Git install...";
                    _removeRequest = Client.Remove(step.PackageId);
                    EditorApplication.update -= UpdateRequests;
                    EditorApplication.update += UpdateRequests;
                    Repaint();
                    return;
                }

                _addRequest = Client.Add(step.PackageReference);
                EditorApplication.update -= UpdateRequests;
                EditorApplication.update += UpdateRequests;
                Repaint();
            }
            catch (Exception exception)
            {
                _pendingPackageId = string.Empty;
                _waitingForPackageRefresh = false;
                Fail("Could not start install for " + step.DisplayName + ".", exception);
            }
        }

        private bool ShouldRemovePackageInstallerBeforeAdd(BootstrapPackageStep step)
        {
            if (step == null ||
                !string.Equals(step.PackageId, DeucarianBootstrapPackageConstants.PackageInstallerPackageId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            BootstrapInstalledPackageInfo installedPackage = GetInstalledPackageInfo(step.PackageId);
            if (installedPackage == null)
            {
                return false;
            }

            if (installedPackage.IsRegistry)
            {
                return true;
            }

            return !installedPackage.IsGit &&
                GetPackageInstallerSetupState() != BootstrapPackageInstallerSetupState.Healthy;
        }

        private void UpdateRemoveRequest()
        {
            if (_removeRequest == null || !_removeRequest.IsCompleted)
            {
                return;
            }

            RemoveRequest request = _removeRequest;
            BootstrapPackageStep step = _removeThenAddStep;
            _removeRequest = null;
            _removeThenAddStep = null;

            if (request.Status != StatusCode.Success)
            {
                string packageName = step != null ? step.DisplayName : "Package Installer";
                Fail("Repair failed while removing " + packageName + ".", request.Error != null ? request.Error.message : "Package Manager returned an unknown error.");
                return;
            }

            if (step == null)
            {
                Fail("Repair failed.", "Package Installer remove completed, but the pending Git add step was lost.");
                return;
            }

            _status = "Installing " + step.PackageId + " from " + step.PackageReference + "...";
            _error = string.Empty;
            SaveState();
            _addRequest = Client.Add(step.PackageReference);
            EditorApplication.update -= UpdateRequests;
            EditorApplication.update += UpdateRequests;
            Repaint();
        }

        private void UpdateAddRequest()
        {
            if (!_addRequest.IsCompleted)
            {
                return;
            }

            AddRequest request = _addRequest;
            BootstrapPackageStep completedStep = _stepIndex < _installPlan.Count ? _installPlan[_stepIndex] : null;
            _addRequest = null;

            if (request.Status != StatusCode.Success)
            {
                string packageName = completedStep != null ? completedStep.DisplayName : "package";
                Fail("Install failed for " + packageName + ".", request.Error != null ? request.Error.message : "Package Manager returned an unknown error.");
                return;
            }

            if (completedStep != null && _installedPackageIds != null)
            {
                _installedPackageIds.Add(completedStep.PackageId);
            }

            _status = completedStep != null
                ? "Waiting for Unity package refresh after installing " + completedStep.PackageId + "..."
                : "Waiting for Unity package refresh...";
            SaveState();
            RefreshInstalledPackages("Waiting for Unity package refresh...", true);
        }

        private void CompleteSetup()
        {
            _setupActive = false;
            _setupInterrupted = false;
            _continueSetupAfterPackageList = false;
            _waitingForPackageRefresh = false;
            _packageListRetryQueued = false;
            _pendingPackageId = string.Empty;
            _packageListRetryCount = 0;
            _stepIndex = _installPlan.Count;
            _error = string.Empty;
            _status = "Setup complete.";
            SaveState();
            EditorApplication.update -= UpdateRequests;
            Repaint();
        }

        private void Fail(string summary, Exception exception)
        {
            Fail(summary, exception.GetBaseException().Message);
        }

        private void Fail(string summary, string detail)
        {
            _setupActive = false;
            _setupInterrupted = true;
            _continueSetupAfterPackageList = false;
            _waitingForPackageRefresh = false;
            _packageListRetryQueued = false;
            _pendingPackageId = string.Empty;
            _status = summary;
            _error = detail ?? string.Empty;
            _listRequest = null;
            _addRequest = null;
            _removeRequest = null;
            _removeThenAddStep = null;
            DisposeCatalogRequest();
            DisposeTargetVersionRequest();
            SaveState();
            EditorApplication.update -= UpdateRequests;
            Repaint();
        }

        private void LoadState()
        {
            _setupActive = SessionState.GetBool(ActiveKey, false);
            _stepIndex = SessionState.GetInt(StepIndexKey, 0);
            _status = SessionState.GetString(StatusKey, string.Empty);
            _error = SessionState.GetString(ErrorKey, string.Empty);
            _pendingPackageId = SessionState.GetString(PendingPackageIdKey, string.Empty);
            _waitingForPackageRefresh = SessionState.GetBool(WaitingForPackageRefreshKey, false);
            _packageListRetryCount = SessionState.GetInt(PackageListRetryCountKey, 0);
            _setupInterrupted = SessionState.GetBool(InterruptedKey, false);
            _savedPlanPackageIds = ParseSavedPlan(SessionState.GetString(PlanKey, string.Empty));
            _selectedChannel = (BootstrapChannel)Mathf.Clamp(
                SessionState.GetInt(ChannelKey, (int)GetPersistedChannel()),
                (int)BootstrapChannel.Stable,
                (int)BootstrapChannel.Development);
            if (string.IsNullOrWhiteSpace(_pendingPackageId))
            {
                _waitingForPackageRefresh = false;
            }
            _registrySource = string.Empty;
            _catalogNotice = string.Empty;
            _packageInstallerOpenMessage = string.Empty;
            _targetPackageInstallerGitUrl = BootstrapChannelUtility.GetPackageInstallerGitUrl(_selectedChannel);
            _targetPackageInstallerVersion = string.Empty;
            _targetPackageInstallerVersionSource = string.Empty;
        }

        private void SaveState()
        {
            SessionState.SetBool(ActiveKey, _setupActive);
            SessionState.SetInt(StepIndexKey, _stepIndex);
            SessionState.SetString(StatusKey, _status ?? string.Empty);
            SessionState.SetString(ErrorKey, _error ?? string.Empty);
            SessionState.SetInt(ChannelKey, (int)_selectedChannel);
            SessionState.SetString(PlanKey, GetPlanPackageIdsForState());
            SessionState.SetString(PendingPackageIdKey, _pendingPackageId ?? string.Empty);
            SessionState.SetBool(WaitingForPackageRefreshKey, _waitingForPackageRefresh);
            SessionState.SetInt(PackageListRetryCountKey, _packageListRetryCount);
            SessionState.SetBool(InterruptedKey, _setupInterrupted);
        }

        private string GetPlanPackageIdsForState()
        {
            string activePlan = FormatPlanPackageIds(_installPlan);
            if (!string.IsNullOrWhiteSpace(activePlan))
            {
                return activePlan;
            }

            return _savedPlanPackageIds == null || _savedPlanPackageIds.Length == 0
                ? string.Empty
                : string.Join(PlanSeparator.ToString(), _savedPlanPackageIds);
        }

        private static string FormatPlanPackageIds(IEnumerable<BootstrapPackageStep> installPlan)
        {
            if (installPlan == null)
            {
                return string.Empty;
            }

            return string.Join(
                PlanSeparator.ToString(),
                installPlan.Where(step => step != null)
                    .Select(step => step.PackageId)
                    .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                    .ToArray());
        }

        private static string[] ParseSavedPlan(string savedPlan)
        {
            if (string.IsNullOrWhiteSpace(savedPlan))
            {
                return Array.Empty<string>();
            }

            return savedPlan.Split(new[] { PlanSeparator }, StringSplitOptions.RemoveEmptyEntries);
        }

        private void DisposeCatalogRequest()
        {
            if (_catalogRequest == null)
            {
                return;
            }

            _catalogRequest.Dispose();
            _catalogRequest = null;
        }

        private void DisposeTargetVersionRequest()
        {
            if (_targetVersionRequest == null)
            {
                return;
            }

            _targetVersionRequest.Dispose();
            _targetVersionRequest = null;
        }

        private void OpenPackageInstaller()
        {
            if (!IsPackageInstallerInstalled)
            {
                _packageInstallerOpenMessage = "Install or repair setup before opening Package Installer.";
                return;
            }

            if (EditorApplication.ExecuteMenuItem(DeucarianBootstrapPackageConstants.PackageInstallerMenuPath))
            {
                _packageInstallerOpenMessage = string.Empty;
                return;
            }

            if (EditorApplication.ExecuteMenuItem(DeucarianBootstrapPackageConstants.LegacyPackageInstallerMenuPath))
            {
                _packageInstallerOpenMessage = string.Empty;
                return;
            }

            _packageInstallerOpenMessage =
                "Package Installer is installed, but Unity has not exposed its menu item yet. Let Unity finish compiling, then refresh status.";
        }

        private bool IsPackageInstalled(string packageId)
        {
            return _installedPackageIds != null && _installedPackageIds.Contains(packageId);
        }

        private BootstrapInstalledPackageInfo GetInstalledPackageInfo(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return null;
            }

            if (_installedPackagesById != null &&
                _installedPackagesById.TryGetValue(packageId, out BootstrapInstalledPackageInfo packageInfo))
            {
                return packageInfo;
            }

            return IsPackageInstalled(packageId)
                ? new BootstrapInstalledPackageInfo(packageId, string.Empty, string.Empty, string.Empty, string.Empty)
                : null;
        }

        private static string GetPackageInfoReference(UnityEditor.PackageManager.PackageInfo packageInfo)
        {
            if (packageInfo == null)
            {
                return string.Empty;
            }

            try
            {
                System.Reflection.PropertyInfo property = typeof(UnityEditor.PackageManager.PackageInfo).GetProperty("packageId");
                object value = property != null ? property.GetValue(packageInfo, null) : null;
                return value != null ? value.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool AreRequiredPackagesInstalled()
        {
            return _installedPackageIds != null && RequiredSetupPackages.All(package => IsPackageInstalled(package.PackageId));
        }

        private bool AreSetupDependenciesInstalled()
        {
            return IsPackageInstalled(DeucarianBootstrapPackageConstants.EditorPackageId) &&
                IsPackageInstalled(DeucarianBootstrapPackageConstants.LoggingPackageId);
        }

        private bool IsSetupHealthy()
        {
            if (!AreSetupDependenciesInstalled())
            {
                return false;
            }

            return GetPackageInstallerSetupState() == BootstrapPackageInstallerSetupState.Healthy;
        }

        private bool HasSetupProblem()
        {
            if (!string.IsNullOrWhiteSpace(_error))
            {
                return true;
            }

            if (_setupInterrupted)
            {
                return true;
            }

            BootstrapPackageInstallerSetupState state = GetPackageInstallerSetupState();
            if (state == BootstrapPackageInstallerSetupState.Outdated ||
                state == BootstrapPackageInstallerSetupState.WrongChannel ||
                state == BootstrapPackageInstallerSetupState.UnknownReviewRequired)
            {
                return true;
            }

            return HasSomeRequiredPackagesInstalled() && !IsSetupHealthy();
        }

        private bool HasSomeRequiredPackagesInstalled()
        {
            return _installedPackageIds != null && RequiredSetupPackages.Any(package => IsPackageInstalled(package.PackageId));
        }

        private bool IsPackageInstallerInstalled =>
            IsPackageInstalled(DeucarianBootstrapPackageConstants.PackageInstallerPackageId);

        private bool IsRequestActive =>
            (_catalogRequest != null && !_catalogRequest.isDone) ||
            (_targetVersionRequest != null && !_targetVersionRequest.isDone) ||
            (_listRequest != null && !_listRequest.IsCompleted) ||
            (_removeRequest != null && !_removeRequest.IsCompleted) ||
            (_addRequest != null && !_addRequest.IsCompleted) ||
            _packageListRetryQueued;

        private void DrawStartupPreferenceToggle(bool compact)
        {
            EditorGUI.BeginChangeCheck();
            bool showOnStartup = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Show Bootstrap on startup",
                    "Project setting. Opens Bootstrap on editor startup without installing packages automatically."),
                ShouldShowOnStartup(),
                GUILayout.Width(compact ? 176f : 260f));
            if (EditorGUI.EndChangeCheck())
            {
                SetShowOnStartup(showOnStartup);
            }

            if (compact)
            {
                return;
            }

            EditorGUILayout.LabelField(
                "Project setting. Startup opens this setup hub only; it never installs packages automatically.",
                _miniMutedStyle);
        }

        internal static bool ShouldShowOnStartup()
        {
            return EditorPrefs.GetBool(GetProjectShowOnStartupPreferenceKey(), true);
        }

        internal static void SetShowOnStartup(bool showOnStartup)
        {
            EditorPrefs.SetBool(GetProjectShowOnStartupPreferenceKey(), showOnStartup);
        }

        internal static BootstrapChannel GetPersistedChannel()
        {
            return (BootstrapChannel)Mathf.Clamp(
                EditorPrefs.GetInt(GetProjectChannelPreferenceKey(), (int)BootstrapChannel.Stable),
                (int)BootstrapChannel.Stable,
                (int)BootstrapChannel.Development);
        }

        internal static void SetPersistedChannel(BootstrapChannel channel)
        {
            EditorPrefs.SetInt(
                GetProjectChannelPreferenceKey(),
                Mathf.Clamp((int)channel, (int)BootstrapChannel.Stable, (int)BootstrapChannel.Development));
        }

        internal static string GetProjectShowOnStartupPreferenceKey()
        {
            string projectRoot = string.Empty;

            if (!string.IsNullOrWhiteSpace(Application.dataPath))
            {
                DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                projectRoot = parent != null ? parent.FullName : Application.dataPath;
            }

            return GetProjectShowOnStartupPreferenceKey(projectRoot);
        }

        internal static string GetProjectShowOnStartupPreferenceKey(string projectRoot)
        {
            string normalizedProjectRoot = (projectRoot ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/')
                .ToLowerInvariant();

            return ShowOnStartupPreferencePrefix + ComputeStableHash(normalizedProjectRoot);
        }

        internal static string GetProjectChannelPreferenceKey()
        {
            string projectRoot = string.Empty;

            if (!string.IsNullOrWhiteSpace(Application.dataPath))
            {
                DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                projectRoot = parent != null ? parent.FullName : Application.dataPath;
            }

            return GetProjectChannelPreferenceKey(projectRoot);
        }

        internal static string GetProjectChannelPreferenceKey(string projectRoot)
        {
            string normalizedProjectRoot = (projectRoot ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/')
                .ToLowerInvariant();

            return ChannelPreferencePrefix + ComputeStableHash(normalizedProjectRoot);
        }

        private static string ComputeStableHash(string value)
        {
            unchecked
            {
                const uint offsetBasis = 2166136261;
                const uint prime = 16777619;
                uint hash = offsetBasis;

                for (int i = 0; i < (value ?? string.Empty).Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }

                return hash.ToString("x8");
            }
        }

        private string GetRegistrySourceSummary()
        {
            if (string.IsNullOrWhiteSpace(_registrySource))
            {
                return "loading";
            }

            if (_registrySource.StartsWith("Remote:", StringComparison.OrdinalIgnoreCase))
            {
                return "remote";
            }

            if (_registrySource.IndexOf("fallback", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "fallback";
            }

            return _registrySource;
        }

        private Texture2D GetLogoTexture()
        {
            if (_logoTexture != null)
            {
                return _logoTexture;
            }

            string logoAssetPath = GetPackageAssetPath(DeucarianBootstrapPackageConstants.LogoAssetRelativePath);
            _logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(logoAssetPath);

            if (_logoTexture == null)
            {
                _logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DeucarianBootstrapPackageConstants.LogoAssetPath);
            }

            if (_logoTexture == null)
            {
                _logoTexture = BootstrapVisualResources.CreateFallbackLogoTexture();
            }

            return _logoTexture;
        }

        private Texture2D GetHeroBackgroundTexture()
        {
            if (_heroBackgroundTexture != null)
            {
                return _heroBackgroundTexture;
            }

            string backgroundAssetPath = GetPackageAssetPath(DeucarianBootstrapPackageConstants.HeroBackgroundAssetRelativePath);
            _heroBackgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(backgroundAssetPath);

            if (_heroBackgroundTexture == null)
            {
                _heroBackgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    DeucarianBootstrapPackageConstants.HeroBackgroundAssetPath);
            }

            return _heroBackgroundTexture;
        }

        private Texture2D GetWallpaperTexture()
        {
            if (_wallpaperTexture != null)
            {
                return _wallpaperTexture;
            }

            string wallpaperAssetPath = GetPackageAssetPath(DeucarianBootstrapPackageConstants.WallpaperAssetRelativePath);
            _wallpaperTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(wallpaperAssetPath);

            if (_wallpaperTexture == null)
            {
                _wallpaperTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    DeucarianBootstrapPackageConstants.WallpaperAssetPath);
            }

            return _wallpaperTexture;
        }

        private Texture2D GetPackageIconTexture()
        {
            if (_packageIconTexture != null)
            {
                return _packageIconTexture;
            }

            string iconAssetPath = GetPackageAssetPath(DeucarianBootstrapPackageConstants.PackageIconAssetRelativePath);
            _packageIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconAssetPath);

            if (_packageIconTexture == null)
            {
                _packageIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    DeucarianBootstrapPackageConstants.PackageIconAssetPath);
            }

            if (_packageIconTexture == null)
            {
                _packageIconTexture = BootstrapVisualResources.CreateFallbackLogoTexture(64);
            }

            return _packageIconTexture;
        }

        private static string GetPackageAssetPath(string relativePath)
        {
            UnityEditor.PackageManager.PackageInfo packageInfo =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);

            if (packageInfo != null && !string.IsNullOrWhiteSpace(packageInfo.assetPath))
            {
                return packageInfo.assetPath.TrimEnd('/') + "/" + relativePath.TrimStart('/');
            }

            return "Packages/" + DeucarianBootstrapPackageConstants.PackageName + "/" + relativePath.TrimStart('/');
        }

        private static bool IsPackageTextureAvailable(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            string packageAssetPath = GetPackageAssetPath(relativePath);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(packageAssetPath) != null)
            {
                return true;
            }

            string fallbackAssetPath = "Packages/" + DeucarianBootstrapPackageConstants.PackageName + "/" + relativePath.TrimStart('/');
            return AssetDatabase.LoadAssetAtPath<Texture2D>(fallbackAssetPath) != null;
        }

        private static Texture2D GetBuiltInIcon(params string[] iconNames)
        {
            if (iconNames == null)
            {
                return null;
            }

            for (int i = 0; i < iconNames.Length; i++)
            {
                string iconName = iconNames[i];
                if (string.IsNullOrWhiteSpace(iconName))
                {
                    continue;
                }

                try
                {
                    GUIContent content = EditorGUIUtility.IconContent(iconName);
                    Texture2D texture = content != null ? content.image as Texture2D : null;

                    if (texture != null)
                    {
                        return texture;
                    }
                }
                catch
                {
                    // Built-in icon names vary between Unity versions.
                }
            }

            return Texture2D.whiteTexture;
        }

        private void EnsureStyles()
        {
            bool proSkin = EditorGUIUtility.isProSkin;

            if (_stylesInitialized && _lastProSkin == proSkin)
            {
                return;
            }

            _stylesInitialized = true;
            _lastProSkin = proSkin;

            _windowBackgroundColor = BootstrapVisualResources.DeepBackground;
            _heroBackgroundColor = proSkin ? FromRgb(12, 24, 34) : FromRgb(22, 44, 54);
            _cardBackgroundColor = proSkin ? BootstrapVisualResources.MainPanel : new Color(0.88f, 0.93f, 0.94f, 0.86f);
            _glassPanelColor = proSkin ? BootstrapVisualResources.MainPanel : new Color(0.90f, 0.95f, 0.96f, 0.84f);
            _glassStrongColor = proSkin ? BootstrapVisualResources.HeaderPanel : new Color(0.84f, 0.91f, 0.93f, 0.90f);
            _glassInsetColor = proSkin ? BootstrapVisualResources.NestedSurface : new Color(0.93f, 0.97f, 0.98f, 0.78f);
            _rowBackgroundColor = proSkin ? new Color(32f / 255f, 47f / 255f, 56f / 255f, 0.46f) : new Color(0.90f, 0.94f, 0.95f, 0.72f);
            _rowAlternateBackgroundColor = proSkin ? new Color(32f / 255f, 47f / 255f, 56f / 255f, 0.58f) : new Color(0.94f, 0.97f, 0.98f, 0.76f);
            _borderColor = proSkin ? BootstrapVisualResources.Border : new Color(0.46f, 0.58f, 0.66f, 0.42f);
            _interactiveBorderColor = proSkin ? BootstrapVisualResources.InteractiveBorder : new Color(0.23f, 0.55f, 0.55f, 0.54f);
            _titleTextColor = proSkin ? BootstrapVisualResources.Text : FromRgb(31, 43, 50);
            _bodyTextColor = proSkin ? new Color(0.82f, 0.89f, 0.92f, 1f) : FromRgb(46, 56, 63);
            _mutedTextColor = proSkin ? BootstrapVisualResources.MutedText : FromRgb(91, 105, 114);
            _successColor = proSkin ? new Color(0.30f, 0.72f, 0.64f, 1f) : FromRgb(71, 137, 104);
            _warningColor = proSkin ? BootstrapVisualResources.Amber : FromRgb(170, 128, 57);
            _infoColor = proSkin ? BootstrapVisualResources.Blue : FromRgb(76, 121, 165);
            _neutralColor = proSkin ? new Color(0.42f, 0.50f, 0.58f, 1f) : FromRgb(145, 153, 159);
            _errorColor = proSkin ? BootstrapVisualResources.Red : FromRgb(163, 82, 82);

            _windowStyle = new GUIStyle
            {
                padding = new RectOffset(12, 12, 12, 10)
            };

            _heroStyle = CopyStyle(() => EditorStyles.helpBox);
            _heroStyle.padding = new RectOffset(0, 0, 0, 0);
            _heroStyle.margin = new RectOffset(0, 0, 0, 10);
            _heroStyle.normal.background = TextureForColor("hero", BootstrapVisualResources.WithAlpha(_heroBackgroundColor, 0.01f));

            _cardStyle = CopyStyle(() => EditorStyles.helpBox);
            _cardStyle.padding = new RectOffset(12, 12, 10, 10);
            _cardStyle.margin = new RectOffset(0, 0, 0, 8);
            _cardStyle.normal.background = TextureForColor("card", BootstrapVisualResources.WithAlpha(_cardBackgroundColor, 0.01f));

            _heroTitleStyle = CopyStyle(() => EditorStyles.boldLabel);
            _heroTitleStyle.fontSize = 24;
            _heroTitleStyle.fontStyle = FontStyle.Bold;
            _heroTitleStyle.wordWrap = true;
            _heroTitleStyle.alignment = TextAnchor.MiddleCenter;
            _heroTitleStyle.normal.textColor = Color.white;

            _heroSubtitleLargeStyle = CopyStyle(() => EditorStyles.label);
            _heroSubtitleLargeStyle.fontSize = 13;
            _heroSubtitleLargeStyle.fontStyle = FontStyle.Bold;
            _heroSubtitleLargeStyle.wordWrap = true;
            _heroSubtitleLargeStyle.alignment = TextAnchor.MiddleCenter;
            _heroSubtitleLargeStyle.normal.textColor = new Color(0.66f, 0.90f, 0.88f, 0.95f);

            _heroEyebrowStyle = CopyStyle(() => EditorStyles.wordWrappedMiniLabel);
            _heroEyebrowStyle.wordWrap = true;
            _heroEyebrowStyle.alignment = TextAnchor.MiddleCenter;
            _heroEyebrowStyle.normal.textColor = new Color(0.82f, 0.91f, 0.94f, 0.88f);

            _sectionTitleStyle = CopyStyle(() => EditorStyles.boldLabel);
            _sectionTitleStyle.fontSize = 12;
            _sectionTitleStyle.fontStyle = FontStyle.Bold;
            _sectionTitleStyle.wordWrap = true;
            _sectionTitleStyle.normal.textColor = _titleTextColor;

            _bodyStyle = CopyStyle(() => EditorStyles.label);
            _bodyStyle.wordWrap = true;
            _bodyStyle.normal.textColor = _bodyTextColor;

            _mutedStyle = CopyStyle(() => EditorStyles.wordWrappedMiniLabel);
            _mutedStyle.wordWrap = true;
            _mutedStyle.normal.textColor = _mutedTextColor;

            _miniMutedStyle = CopyStyle(() => EditorStyles.miniLabel);
            _miniMutedStyle.wordWrap = true;
            _miniMutedStyle.normal.textColor = _mutedTextColor;

            _productStatusStyle = CopyStyle(() => EditorStyles.miniBoldLabel);
            _productStatusStyle.alignment = TextAnchor.MiddleLeft;
            _productStatusStyle.clipping = TextClipping.Ellipsis;
            _productStatusStyle.normal.textColor = Color.white;

            _productStatusDetailStyle = CopyStyle(() => EditorStyles.miniLabel);
            _productStatusDetailStyle.alignment = TextAnchor.MiddleLeft;
            _productStatusDetailStyle.clipping = TextClipping.Ellipsis;
            _productStatusDetailStyle.normal.textColor = new Color(0.82f, 0.91f, 0.94f, 0.92f);

            _statusIconStyle = CopyStyle(() => EditorStyles.miniBoldLabel);
            _statusIconStyle.alignment = TextAnchor.MiddleCenter;
            _statusIconStyle.normal.textColor = Color.white;
            _statusIconStyle.fontSize = 11;
            _statusIconStyle.padding = new RectOffset(0, 0, 0, 1);

            _statusLabelStyle = CopyStyle(() => EditorStyles.label);
            _statusLabelStyle.clipping = TextClipping.Ellipsis;
            _statusLabelStyle.normal.textColor = _bodyTextColor;

            _statusDetailStyle = CopyStyle(() => EditorStyles.miniLabel);
            _statusDetailStyle.clipping = TextClipping.Ellipsis;
            _statusDetailStyle.normal.textColor = _mutedTextColor;

            _summaryValueStyle = CopyStyle(() => EditorStyles.boldLabel);
            _summaryValueStyle.fontSize = 13;
            _summaryValueStyle.clipping = TextClipping.Ellipsis;
            _summaryValueStyle.normal.textColor = _titleTextColor;

            _summaryLabelStyle = CopyStyle(() => EditorStyles.miniLabel);
            _summaryLabelStyle.clipping = TextClipping.Ellipsis;
            _summaryLabelStyle.normal.textColor = _mutedTextColor;

            _summarySubtextStyle = CopyStyle(() => EditorStyles.miniLabel);
            _summarySubtextStyle.clipping = TextClipping.Ellipsis;
            _summarySubtextStyle.normal.textColor = _mutedTextColor;

            _timelineLabelStyle = CopyStyle(() => EditorStyles.miniBoldLabel);
            _timelineLabelStyle.alignment = TextAnchor.MiddleLeft;
            _timelineLabelStyle.clipping = TextClipping.Clip;
            _timelineLabelStyle.normal.textColor = Color.white;

            _foldoutStyle = CopyStyle(() => EditorStyles.foldout);
            _foldoutStyle.fontStyle = FontStyle.Bold;
            _foldoutStyle.normal.textColor = _titleTextColor;
            _foldoutStyle.onNormal.textColor = _titleTextColor;

            _primaryButtonStyle = CopyStyle(() => GUI.skin.button);
            _primaryButtonStyle.fontStyle = FontStyle.Bold;
            _primaryButtonStyle.normal.textColor = Color.white;
            _primaryButtonStyle.hover.textColor = Color.white;
            _primaryButtonStyle.active.textColor = Color.white;
            _primaryButtonStyle.normal.background = TextureForColor("primary", new Color(0.16f, 0.42f, 0.48f, 0.92f));
            _primaryButtonStyle.hover.background = TextureForColor("primary-hover", new Color(0.20f, 0.55f, 0.56f, 0.96f));
            _primaryButtonStyle.active.background = TextureForColor("primary-active", new Color(0.24f, 0.64f, 0.60f, 1f));

            _secondaryButtonStyle = CopyStyle(() => GUI.skin.button);
            _secondaryButtonStyle.alignment = TextAnchor.MiddleCenter;
            _secondaryButtonStyle.normal.textColor = _bodyTextColor;
            _secondaryButtonStyle.clipping = TextClipping.Ellipsis;

            _utilityButtonStyle = CopyStyle(() => EditorStyles.miniButton);
            _utilityButtonStyle.alignment = TextAnchor.MiddleCenter;
            _utilityButtonStyle.clipping = TextClipping.Clip;
            _utilityButtonStyle.fontSize = 11;
            _utilityButtonStyle.normal.textColor = _bodyTextColor;

            _badgeStyle = CopyStyle(() => EditorStyles.miniBoldLabel);
            _badgeStyle.alignment = TextAnchor.MiddleCenter;
            _badgeStyle.normal.textColor = Color.white;
            _badgeStyle.normal.background = TextureForColor("badge", new Color(0.13f, 0.36f, 0.38f, 0.88f));
            _badgeStyle.padding = new RectOffset(7, 7, 2, 3);

            _footerStyle = CopyStyle(() => EditorStyles.miniLabel);
            _footerStyle.wordWrap = true;
            _footerStyle.normal.textColor = _mutedTextColor;

            _footerRightStyle = CopyStyle(() => EditorStyles.miniLabel);
            _footerRightStyle.alignment = TextAnchor.MiddleRight;
            _footerRightStyle.clipping = TextClipping.Clip;
            _footerRightStyle.normal.textColor = _mutedTextColor;
        }

        private void DrawWindowBackground()
        {
            BootstrapVisualResources.DrawWindowBackdrop(
                new Rect(0f, 0f, position.width, position.height),
                GetWallpaperTexture(),
                _windowBackgroundColor);
        }

        private Color GetStatusColor(BootstrapStatusKind kind)
        {
            switch (kind)
            {
                case BootstrapStatusKind.Success:
                    return _successColor;
                case BootstrapStatusKind.Info:
                    return _infoColor;
                case BootstrapStatusKind.Warning:
                    return _warningColor;
                case BootstrapStatusKind.Error:
                    return _errorColor;
                default:
                    return _neutralColor;
            }
        }

        private static string GetStatusMarker(BootstrapStatusKind kind)
        {
            switch (kind)
            {
                case BootstrapStatusKind.Success:
                    return "\u2713";
                case BootstrapStatusKind.Info:
                    return "...";
                case BootstrapStatusKind.Warning:
                    return "!";
                case BootstrapStatusKind.Error:
                    return "!";
                default:
                    return "-";
            }
        }

        private static GUIStyle CopyStyle(Func<GUIStyle> styleFactory)
        {
            if (styleFactory != null)
            {
                try
                {
                    GUIStyle style = styleFactory();
                    if (style != null)
                    {
                        return new GUIStyle(style);
                    }
                }
                catch
                {
                    // Some editor styles are unavailable during headless batch-mode startup.
                }
            }

            return new GUIStyle();
        }

        private static Texture2D TextureForColor(string name, Color color)
        {
            return BootstrapVisualResources.TextureForColor(name, color);
        }

        private static Color FromRgb(byte red, byte green, byte blue)
        {
            return new Color32(red, green, blue, 255);
        }

        internal enum BootstrapHeroState
        {
            NotSetUp,
            Installing,
            WaitingForUnity,
            Ready,
            NeedsRepair,
            Interrupted,
            Checking
        }

        internal enum BootstrapStatusKind
        {
            Success,
            Neutral,
            Info,
            Warning,
            Error
        }

        internal struct BootstrapStatusCardModel
        {
            public BootstrapStatusCardModel(
                string label,
                string value,
                string subtext,
                BootstrapStatusKind kind,
                string tooltip)
            {
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Subtext = subtext ?? string.Empty;
                Kind = kind;
                Tooltip = tooltip ?? string.Empty;
            }

            public string Label { get; }

            public string Value { get; }

            public string Subtext { get; }

            public BootstrapStatusKind Kind { get; }

            public string Tooltip { get; }
        }

        private enum BootstrapTimelineState
        {
            Done,
            Current,
            Pending,
            Failed
        }

        private sealed class BootstrapSetupPackage
        {
            public BootstrapSetupPackage(string packageId, string displayName)
            {
                PackageId = packageId ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
            }

            public string PackageId { get; }

            public string DisplayName { get; }
        }

        private sealed class BootstrapTimelineItem
        {
            public BootstrapTimelineItem(string label, BootstrapTimelineState state, string tooltip)
            {
                Label = label ?? string.Empty;
                State = state;
                Tooltip = tooltip ?? string.Empty;
            }

            public string Label { get; }

            public BootstrapTimelineState State { get; }

            public string Tooltip { get; }
        }

        [Serializable]
        private sealed class BootstrapPackageJson
        {
            public string version;
        }
    }

    internal sealed class BootstrapPackageStep
    {
        public BootstrapPackageStep(string packageId, string displayName, string gitUrl)
            : this(packageId, displayName, gitUrl, BootstrapPackageInstallSource.Git)
        {
        }

        public BootstrapPackageStep(
            string packageId,
            string displayName,
            string packageReference,
            BootstrapPackageInstallSource installSource)
        {
            PackageId = packageId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PackageReference = packageReference ?? string.Empty;
            InstallSource = installSource;
        }

        public string PackageId { get; }

        public string DisplayName { get; }

        public string PackageReference { get; }

        public BootstrapPackageInstallSource InstallSource { get; }

        public string GitUrl => InstallSource == BootstrapPackageInstallSource.Git ? PackageReference : string.Empty;
    }

    internal enum BootstrapPackageInstallSource
    {
        Git,
        ScopedRegistry
    }
}
