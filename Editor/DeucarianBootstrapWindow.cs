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
        private const string InstallModeKey = "Deucarian.Bootstrap.InstallMode";
        private const string PlanKey = "Deucarian.Bootstrap.Plan";
        private const string PendingPackageIdKey = "Deucarian.Bootstrap.PendingPackageId";
        private const string WaitingForPackageRefreshKey = "Deucarian.Bootstrap.WaitingForPackageRefresh";
        private const string PackageListRetryCountKey = "Deucarian.Bootstrap.PackageListRetryCount";
        private const string InterruptedKey = "Deucarian.Bootstrap.Interrupted";
        private const string StartupShownThisSessionKey = "Deucarian.Bootstrap.StartupShownThisSession";
        private const string ShowOnStartupPreferencePrefix = "Deucarian.Bootstrap.ShowOnStartup.";
        private const string SetupDetailsExpandedKey = "Deucarian.Bootstrap.SetupDetailsExpanded";
        private const char PlanSeparator = '|';

        internal const float PreferredWindowWidth = 760f;
        internal const float PreferredWindowHeight = 860f;
        internal const float MinWindowWidth = 740f;
        internal const float MinWindowHeight = 720f;
        internal const float HeroCardHeight = 540f;
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

        private static readonly Dictionary<string, Texture2D> TextureCache =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        private readonly List<BootstrapPackageStep> _installPlan = new List<BootstrapPackageStep>();

        private UnityWebRequest _catalogRequest;
        private ListRequest _listRequest;
        private AddRequest _addRequest;
        private HashSet<string> _installedPackageIds;
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
        private BootstrapInstallMode _installMode;
        private BootstrapScopedRegistryStatus _scopedRegistryStatus;
        private Vector2 _scrollPosition;

        private bool _stylesInitialized;
        private bool _lastProSkin;
        private Color _windowBackgroundColor;
        private Color _heroBackgroundColor;
        private Color _cardBackgroundColor;
        private Color _rowBackgroundColor;
        private Color _rowAlternateBackgroundColor;
        private Color _borderColor;
        private Color _titleTextColor;
        private Color _bodyTextColor;
        private Color _mutedTextColor;
        private Color _successColor;
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
        private GUIStyle _timelineLabelStyle;
        private GUIStyle _foldoutStyle;

        private Texture2D _logoTexture;
        private Texture2D _heroBackgroundTexture;
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

            if (_installMode == BootstrapInstallMode.ScopedRegistry)
            {
                SetScopedRegistryInstallPlan();
                RefreshInstalledPackages(_setupActive ? status : "Checking setup status...", _setupActive);
                return;
            }

            BeginCatalogLoad(status);
        }

        private void EnsureActiveSetupHasResolvablePlan()
        {
            if (!_setupActive ||
                _installMode == BootstrapInstallMode.ScopedRegistry ||
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
            if (_catalogRequest != null || _listRequest != null || _addRequest != null || _packageListRetryQueued)
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

        private void DrawCompactSetupSummary()
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSummaryItem(
                        "Registry configured",
                        GetRegistryConfiguredSummaryText(),
                        GetScopedRegistryStatusKind(),
                        GUILayout.ExpandWidth(true));
                    GUILayout.Space(8f);
                    DrawSummaryItem(
                        "Required packages installed",
                        GetRequiredPackagesSummaryText(),
                        GetRequiredPackagesSummaryKind(),
                        GUILayout.ExpandWidth(true));
                    GUILayout.Space(8f);
                    DrawSummaryItem(
                        "Package Installer ready",
                        IsPackageInstallerInstalled ? "Yes" : "No",
                        GetPackageInstallerAvailabilityKind(),
                        GUILayout.ExpandWidth(true));
                    GUILayout.Space(8f);
                    DrawInstallSourceIndicator(GUILayout.Width(238f));
                }

                if (!string.IsNullOrWhiteSpace(_error))
                {
                    GUILayout.Space(8f);
                    EditorGUILayout.HelpBox(_error, MessageType.Error);
                }
            }
        }

        private void DrawSetupActions()
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Mode", _miniMutedStyle, GUILayout.Width(34f));
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(230f)))
                    {
                        DrawSetupModeSelector();
                    }

                    GUILayout.Space(6f);

                    using (new EditorGUI.DisabledScope(_setupActive || IsRequestActive))
                    {
                        GUIContent refreshContent = new GUIContent(
                            "Refresh",
                            "Refresh installed packages and setup status.");
                        if (GUILayout.Button(refreshContent, _utilityButtonStyle, GUILayout.Width(70f), GUILayout.Height(24f)))
                        {
                            RefreshStatus();
                        }
                    }

                    using (new EditorGUI.DisabledScope(IsRequestActive))
                    {
                        GUIContent repairContent = new GUIContent(
                            "Repair Registry",
                            "Adds or repairs the Deucarian scoped registry entry in Packages/manifest.json.");
                        if (GUILayout.Button(repairContent, _utilityButtonStyle, GUILayout.Width(108f), GUILayout.Height(24f)))
                        {
                            RepairScopedRegistry();
                        }
                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(new GUIContent("GitHub", "Open the Bootstrap repository."), _utilityButtonStyle, GUILayout.Width(64f), GUILayout.Height(24f)))
                    {
                        Application.OpenURL(DeucarianBootstrapPackageConstants.GitHubUrl);
                    }

                    if (GUILayout.Button(new GUIContent("Docs", "Open Bootstrap documentation."), _utilityButtonStyle, GUILayout.Width(54f), GUILayout.Height(24f)))
                    {
                        Application.OpenURL(DeucarianBootstrapPackageConstants.DocumentationUrl);
                    }
                }

                GUILayout.Space(4f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    DrawStartupPreferenceToggle(true);
                }
            }
        }

        private void DrawSetupDetails()
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
            {
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
                        "Registry source, package IDs, install plan, and diagnostics are available here when needed.",
                        _miniMutedStyle);
                    return;
                }

                GUILayout.Space(8f);
                DrawDetailedStatusRows();
                DrawStatusMessages();

                GUILayout.Space(10f);
                DrawInstallPlanContents();
            }
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

        private void DrawInstallSourceIndicator(params GUILayoutOption[] options)
        {
            BootstrapStatusKind kind = _installMode == BootstrapInstallMode.ScopedRegistry
                ? BootstrapStatusKind.Success
                : BootstrapStatusKind.Info;
            Rect itemRect = GUILayoutUtility.GetRect(1f, 32f, options);
            EditorGUI.DrawRect(itemRect, _rowBackgroundColor);

            bool scopedRegistry = _installMode == BootstrapInstallMode.ScopedRegistry;
            float badgeWidth = scopedRegistry ? 90f : 0f;
            Rect sourceRect = new Rect(itemRect.x + 8f, itemRect.y + 7f, itemRect.width - 16f - badgeWidth, 18f);
            string source = _installMode == BootstrapInstallMode.ScopedRegistry
                ? "Install source: npm scoped registry"
                : "Install source: Git fallback";
            GUI.Label(sourceRect, source, _summaryLabelStyle);

            if (scopedRegistry)
            {
                Rect badgeRect = new Rect(itemRect.xMax - 92f, itemRect.y + 7f, 84f, 18f);
                GUIStyle badgeStyle = new GUIStyle(_badgeStyle);
                badgeStyle.normal.background = TextureForColor("summary-source-" + kind, GetStatusColor(kind));
                GUI.Label(badgeRect, "Recommended", badgeStyle);
            }
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
                "Registry mode",
                GetInstallModeLabel(),
                GetInstallModeDetail(),
                BootstrapStatusKind.Info,
                true);

            DrawStatusRow(
                "Scoped registry configured",
                GetScopedRegistryStatusText(),
                GetScopedRegistryStatusDetail(),
                GetScopedRegistryStatusKind(),
                false);

            DrawStatusRow(
                "Package Installer install source",
                GetPackageInstallerInstallSourceText(),
                GetPackageInstallerInstallSourceDetail(),
                BootstrapStatusKind.Info,
                true);

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

            DrawStatusRow(
                "Package Installer available",
                GetPackageInstallerAvailabilityText(),
                GetPackageInstallerAvailabilityDetail(),
                GetPackageInstallerAvailabilityKind(),
                true);
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

        private void DrawSetupModeSelector()
        {
            EditorGUI.BeginChangeCheck();
            int selectedMode = _installMode == BootstrapInstallMode.ScopedRegistry ? 0 : 1;
            selectedMode = GUILayout.Toolbar(
                selectedMode,
                new[]
                {
                    new GUIContent(
                        "Scoped registry",
                        "Recommended. Uses npmjs scoped registry and lets Unity resolve dependencies."),
                    new GUIContent(
                        "Git fallback",
                        "Advanced fallback mode for development or registry outages.")
                },
                GUILayout.Height(26f));
            if (EditorGUI.EndChangeCheck())
            {
                SetInstallMode(selectedMode == 0
                    ? BootstrapInstallMode.ScopedRegistry
                    : BootstrapInstallMode.GitFallback);
            }

            EditorGUILayout.LabelField(
                _installMode == BootstrapInstallMode.ScopedRegistry
                    ? "Recommended. Uses npmjs scoped registry and lets Unity resolve dependencies."
                    : "Advanced fallback. Uses Git URLs when scoped registry setup is unavailable.",
                _miniMutedStyle);
        }

        private void DrawPackageInstallerProductCard()
        {
            bool ready = GetHeroState() == BootstrapHeroState.Ready;

            using (new EditorGUILayout.VerticalScope(_heroStyle))
            {
                Rect heroRect = GUILayoutUtility.GetRect(1f, HeroCardHeight, GUILayout.ExpandWidth(true), GUILayout.MinHeight(420f));
                DrawHeroBackground(heroRect, ready);

                Rect badgeRect = new Rect(heroRect.x + 18f, heroRect.y + 16f, 148f, 22f);
                GUI.Label(badgeRect, "DEUCARIAN SETUP", _badgeStyle);

                Rect logoArea = new Rect(heroRect.x, heroRect.y + 84f, heroRect.width, 154f);
                DrawCenteredPackageInstallerLogo(logoArea, GetPackageInstallerLogoAlpha(), 148f);

                if (ready)
                {
                    EditorGUIUtility.AddCursorRect(logoArea, MouseCursor.Link);
                    if (Event.current.type == EventType.MouseDown && logoArea.Contains(Event.current.mousePosition))
                    {
                        OpenPackageInstaller();
                        Event.current.Use();
                    }
                }

                float contentWidth = Mathf.Min(560f, Mathf.Max(320f, heroRect.width - 48f));
                float contentX = heroRect.x + (heroRect.width - contentWidth) * 0.5f;

                Rect titleRect = new Rect(contentX, heroRect.y + 258f, contentWidth, 42f);
                GUI.Label(titleRect, DeucarianBootstrapPackageConstants.PackageInstallerPackageDisplayName, _heroTitleStyle);

                Rect subtitleRect = new Rect(contentX, titleRect.yMax + 2f, contentWidth, 26f);
                GUI.Label(subtitleRect, "Install and manage Deucarian Unity packages.", _heroSubtitleLargeStyle);

                Rect noteRect = new Rect(contentX, subtitleRect.yMax + 6f, contentWidth, 34f);
                GUI.Label(noteRect, "Bootstrap installs Package Installer and the required setup packages.", _heroEyebrowStyle);

                Rect timelineRect = new Rect(contentX, noteRect.yMax + 12f, contentWidth, 56f);
                DrawHeroSetupTimeline(timelineRect);

                Rect stripRect = new Rect(contentX, heroRect.yMax - 92f, contentWidth, 32f);
                DrawPackageInstallerStatusStrip(stripRect);

                Rect buttonRect = new Rect(
                    contentX + Mathf.Max(0f, (contentWidth - 260f) * 0.5f),
                    heroRect.yMax - 48f,
                    Mathf.Min(260f, contentWidth),
                    34f);

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
                    EditorGUILayout.HelpBox(_packageInstallerOpenMessage, MessageType.Info);
                }
            }
        }

        private void DrawHeroBackground(Rect heroRect, bool ready)
        {
            Texture2D background = GetHeroBackgroundTexture();
            if (background != null)
            {
                GUI.DrawTexture(heroRect, background, ScaleMode.ScaleAndCrop, false);
            }
            else
            {
                EditorGUI.DrawRect(heroRect, _heroBackgroundColor);
            }

            Color vignette = ready
                ? new Color(0f, 0f, 0f, 0.10f)
                : new Color(0f, 0f, 0f, 0.28f);
            EditorGUI.DrawRect(heroRect, vignette);
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
            EditorGUI.DrawRect(stripRect, GetStatusColor(kind));

            Rect iconRect = new Rect(stripRect.x + 10f, stripRect.y + 6f, 20f, 20f);
            Rect statusRect = new Rect(stripRect.x + 38f, stripRect.y + 6f, 150f, 20f);
            Rect detailRect = new Rect(stripRect.x + 196f, stripRect.y + 6f, Mathf.Max(40f, stripRect.width - 206f), 20f);

            GUI.Label(iconRect, GetStatusMarker(kind), _productStatusStyle);
            GUI.Label(statusRect, GetPackageInstallerProductStatusText(), _productStatusStyle);
            GUI.Label(detailRect, GetPackageInstallerProductStatusDetail(), _productStatusDetailStyle);
        }

        private void DrawHeroSetupTimeline(Rect timelineRect)
        {
            if (GetHeroState() == BootstrapHeroState.Ready)
            {
                DrawHeroReadySummary(timelineRect);
                return;
            }

            Rect titleRect = new Rect(timelineRect.x, timelineRect.y, timelineRect.width, 16f);
            GUI.Label(titleRect, "Setup progress", _heroEyebrowStyle);

            Rect rowRect = new Rect(timelineRect.x, timelineRect.y + 20f, timelineRect.width, 32f);
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
            background.a = 0.35f;
            EditorGUI.DrawRect(timelineRect, background);

            Rect iconRect = new Rect(timelineRect.x + 12f, timelineRect.y + 17f, 22f, 22f);
            GUIStyle iconStyle = new GUIStyle(_statusIconStyle);
            iconStyle.normal.background = TextureForColor("hero-ready-summary", GetStatusColor(BootstrapStatusKind.Success));
            GUI.Label(iconRect, GetStatusMarker(BootstrapStatusKind.Success), iconStyle);

            Rect labelRect = new Rect(iconRect.xMax + 10f, timelineRect.y + 9f, timelineRect.width - 54f, 18f);
            Rect detailRect = new Rect(iconRect.xMax + 10f, labelRect.yMax + 1f, timelineRect.width - 54f, 18f);
            GUI.Label(labelRect, "Setup looks healthy.", _productStatusStyle);
            GUI.Label(detailRect, "Package Installer is installed and available.", _productStatusDetailStyle);
        }

        private void DrawTimelineItem(Rect itemRect, BootstrapTimelineItem item)
        {
            BootstrapStatusKind kind = GetTimelineStatusKind(item.State);
            Color background = GetStatusColor(kind);
            background.a = item.State == BootstrapTimelineState.Pending ? 0.26f : 0.42f;
            EditorGUI.DrawRect(itemRect, background);

            Rect markerRect = new Rect(itemRect.x + 6f, itemRect.y + 7f, 18f, 18f);
            GUIStyle markerStyle = new GUIStyle(_statusIconStyle);
            markerStyle.normal.background = TextureForColor("timeline-" + item.State, GetStatusColor(kind));
            GUI.Label(markerRect, GetTimelineMarker(item.State), markerStyle);

            Rect labelRect = new Rect(markerRect.xMax + 5f, itemRect.y + 7f, itemRect.width - 31f, 18f);
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
            return _installMode == BootstrapInstallMode.ScopedRegistry ? "Registry" : "Git fallback";
        }

        private string GetRegistryTimelineTooltip()
        {
            return _installMode == BootstrapInstallMode.ScopedRegistry
                ? "Scoped registry configured."
                : "Git fallback mode selected.";
        }

        private BootstrapTimelineState GetRegistryTimelineState()
        {
            if (_installMode == BootstrapInstallMode.GitFallback)
            {
                return (_catalogRequest != null || (_setupActive && !_catalogLoaded))
                    ? BootstrapTimelineState.Current
                    : BootstrapTimelineState.Done;
            }

            if (_scopedRegistryStatus != null && _scopedRegistryStatus.Configured)
            {
                return BootstrapTimelineState.Done;
            }

            if (!string.IsNullOrWhiteSpace(_error) && _scopedRegistryStatus != null && !_scopedRegistryStatus.NeedsRepair)
            {
                return BootstrapTimelineState.Failed;
            }

            if (_setupActive || (_scopedRegistryStatus != null && _scopedRegistryStatus.NeedsRepair))
            {
                return BootstrapTimelineState.Current;
            }

            return BootstrapTimelineState.Pending;
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

            return _installMode == BootstrapInstallMode.ScopedRegistry &&
                string.Equals(packageId, DeucarianBootstrapPackageConstants.PackageInstallerPackageId, StringComparison.OrdinalIgnoreCase);
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

            if (AreRequiredPackagesInstalled())
            {
                EditorGUILayout.LabelField("Required setup packages are installed.", _mutedStyle);
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
            Rect lineRect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, _borderColor);
            GUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "Bootstrap only sets up and repairs the Deucarian package ecosystem. Use Package Installer for day-to-day package management.",
                    _footerStyle,
                    GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    "Bootstrap " + DeucarianBootstrapPackageConstants.Version + " | Registry: " + GetRegistrySourceSummary(),
                    _footerRightStyle,
                    GUILayout.Width(320f));
            }
        }

        private void DrawStatusRow(
            string label,
            string status,
            string detail,
            BootstrapStatusKind kind,
            bool alternate)
        {
            Rect rowRect = GUILayoutUtility.GetRect(1f, 32f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rowRect, alternate ? _rowAlternateBackgroundColor : _rowBackgroundColor);

            Rect iconRect = new Rect(rowRect.x + 8f, rowRect.y + 6f, 22f, 20f);
            Rect labelRect = new Rect(iconRect.xMax + 8f, rowRect.y + 5f, Mathf.Max(120f, rowRect.width * 0.42f), 22f);
            Rect statusRect = new Rect(labelRect.xMax + 8f, rowRect.y + 5f, 86f, 22f);
            Rect detailRect = new Rect(statusRect.xMax + 8f, rowRect.y + 5f, Mathf.Max(80f, rowRect.xMax - statusRect.xMax - 16f), 22f);

            Color statusColor = GetStatusColor(kind);
            GUIStyle iconStyle = new GUIStyle(_statusIconStyle);
            iconStyle.normal.background = TextureForColor("status-" + kind, statusColor);
            GUI.Label(iconRect, GetStatusMarker(kind), iconStyle);
            GUI.Label(labelRect, label ?? string.Empty, _statusLabelStyle);
            GUI.Label(statusRect, status ?? string.Empty, _statusDetailStyle);
            GUI.Label(detailRect, detail ?? string.Empty, _statusDetailStyle);
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
                    return "Checking Setup...";
                case BootstrapHeroState.Interrupted:
                    return "Continue Setup";
                case BootstrapHeroState.NeedsRepair:
                    return "Repair Setup";
                default:
                    return "Install Deucarian Setup";
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
                    return "Repair missing setup packages or registry configuration.";
                default:
                    return "Install Package Installer and required setup packages.";
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
            if (IsSetupHealthy())
            {
                return BootstrapHeroState.Ready;
            }

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

            if (HasSetupProblem())
            {
                return BootstrapHeroState.NeedsRepair;
            }

            return BootstrapHeroState.NotSetUp;
        }

        private string GetRegistryConfiguredSummaryText()
        {
            if (_scopedRegistryStatus == null)
            {
                return "Checking";
            }

            return _scopedRegistryStatus.Configured ? "Yes" : "No";
        }

        private string GetRequiredPackagesSummaryText()
        {
            if (_installedPackageIds == null)
            {
                return "Checking";
            }

            int installed = RequiredSetupPackages.Count(package => IsPackageInstalled(package.PackageId));
            return installed == RequiredSetupPackages.Length
                ? "Yes"
                : "No (" + installed + "/" + RequiredSetupPackages.Length + ")";
        }

        private BootstrapStatusKind GetRequiredPackagesSummaryKind()
        {
            if (_installedPackageIds == null)
            {
                return BootstrapStatusKind.Info;
            }

            return AreRequiredPackagesInstalled() ? BootstrapStatusKind.Success : BootstrapStatusKind.Neutral;
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

        private string GetInstallModeLabel()
        {
            return _installMode == BootstrapInstallMode.ScopedRegistry ? "Scoped" : "Git";
        }

        private string GetInstallModeDetail()
        {
            return _installMode == BootstrapInstallMode.ScopedRegistry
                ? "Recommended npmjs scoped registry setup"
                : "Advanced Git fallback URLs";
        }

        private string GetPackageInstallerInstallSourceText()
        {
            return _installMode == BootstrapInstallMode.ScopedRegistry ? "npm registry" : "Git fallback";
        }

        private string GetPackageInstallerInstallSourceDetail()
        {
            return _installMode == BootstrapInstallMode.ScopedRegistry
                ? DeucarianBootstrapPackageConstants.PackageInstallerPackageId
                : DeucarianBootstrapPackageConstants.PackageInstallerPackageGitUrl;
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

        private string GetPackageInstallerAvailabilityText()
        {
            if (_installedPackageIds == null)
            {
                return _listRequest != null ? "Checking" : "Unknown";
            }

            return IsPackageInstallerInstalled ? "Available" : "Unavailable";
        }

        private string GetPackageInstallerAvailabilityDetail()
        {
            if (_installedPackageIds == null)
            {
                return "Waiting for package list";
            }

            return IsPackageInstallerInstalled
                ? DeucarianBootstrapPackageConstants.PackageInstallerMenuPath
                : "Install or repair setup first";
        }

        private BootstrapStatusKind GetPackageInstallerAvailabilityKind()
        {
            if (_installedPackageIds == null)
            {
                return _listRequest != null ? BootstrapStatusKind.Info : BootstrapStatusKind.Neutral;
            }

            return IsPackageInstallerInstalled ? BootstrapStatusKind.Success : BootstrapStatusKind.Neutral;
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
                    return "Ready";
                case BootstrapHeroState.WaitingForUnity:
                    return "Waiting for Unity";
                case BootstrapHeroState.Installing:
                    return "Installing";
                case BootstrapHeroState.Checking:
                    return "Checking";
                case BootstrapHeroState.Interrupted:
                case BootstrapHeroState.NeedsRepair:
                    return "Setup needs repair";
                default:
                    return "Not installed";
            }
        }

        internal string GetPackageInstallerProductStatusDetail()
        {
            switch (GetHeroState())
            {
                case BootstrapHeroState.Ready:
                    return "Installed and available";
                case BootstrapHeroState.WaitingForUnity:
                    return "Unity is resolving packages";
                case BootstrapHeroState.Installing:
                    return "Setup is installing required packages";
                case BootstrapHeroState.Checking:
                    return "Checking installed packages";
                case BootstrapHeroState.Interrupted:
                    return "Continue setup to finish installation";
                case BootstrapHeroState.NeedsRepair:
                    return "Run repair to restore setup";
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
                ? "npm registry: " + step.PackageReference
                : "Git: " + step.PackageId;
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

            if (_installMode == BootstrapInstallMode.ScopedRegistry)
            {
                SetScopedRegistryInstallPlan();
                RefreshInstalledPackages("Refreshing scoped registry setup status...", false);
                return;
            }

            ReloadCatalog();
        }

        private void SetInstallMode(BootstrapInstallMode installMode)
        {
            _installMode = installMode;
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

            if (_installMode == BootstrapInstallMode.ScopedRegistry)
            {
                SetScopedRegistryInstallPlan();
                _status = "Scoped registry mode selected. Bootstrap will repair the registry, install Package Installer, and let Unity resolve dependencies.";
            }
            else
            {
                _status = "Git fallback mode selected. Bootstrap will use catalog Git URLs for advanced or emergency setup.";
                if (_catalogLoaded)
                {
                    ReloadCatalog();
                }
            }

            SaveState();
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
            _catalogNotice = "Scoped registry mode installs Package Installer by package name and lets Unity resolve Editor and Logging dependencies.";
            _stepIndex = Mathf.Clamp(_stepIndex, 0, _installPlan.Count);
            _savedPlanPackageIds = _installPlan.Select(step => step.PackageId).ToArray();
        }

        private void ReloadCatalog()
        {
            DisposeCatalogRequest();
            _catalogLoaded = false;
            _continueSetupAfterPackageList = false;
            _waitingForPackageRefresh = false;
            _packageListRetryQueued = false;
            _installPlan.Clear();
            _installedPackageIds = null;
            _stepIndex = 0;
            _pendingPackageId = string.Empty;
            _packageListRetryCount = 0;
            _error = string.Empty;
            _catalogNotice = string.Empty;
            _registrySource = string.Empty;
            SaveState();
            BeginCatalogLoad("Reloading Deucarian package catalog...");
        }

        private void StartSetup()
        {
            _setupActive = true;
            _setupInterrupted = false;
            _packageListRetryQueued = false;
            _stepIndex = Mathf.Clamp(_stepIndex, 0, _installPlan.Count);
            _error = string.Empty;
            _packageInstallerOpenMessage = string.Empty;

            if (_installMode == BootstrapInstallMode.ScopedRegistry)
            {
                if (!ConfigureScopedRegistryForSetup())
                {
                    return;
                }

                SetScopedRegistryInstallPlan();
                _status = string.IsNullOrWhiteSpace(_pendingPackageId)
                    ? "Checking installed packages before scoped registry setup..."
                    : "Waiting for Unity package refresh...";
                SaveState();
                RefreshInstalledPackages(_status, true);
                return;
            }

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
                _catalogRequest = UnityWebRequest.Get(DeucarianBootstrapPackageConstants.RegistryCatalogUrl);
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

            if (_listRequest != null)
            {
                UpdateListRequest();
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

            if (success && TryUseCatalog(responseText, "Remote: " + DeucarianBootstrapPackageConstants.RegistryCatalogUrl, out parseError))
            {
                FinishCatalogLoad(_setupActive
                    ? "Remote catalog loaded. Checking installed packages..."
                    : "Remote catalog loaded. Checking setup status...");
                return;
            }

            if (success)
            {
                remoteError = parseError;
            }

            LoadFallbackCatalog("Remote registry unavailable; using bundled fallback. " + remoteError);
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
            FinishCatalogLoad(_setupActive
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

            if (_installMode == BootstrapInstallMode.ScopedRegistry)
            {
                SetScopedRegistryInstallPlan();
                return true;
            }

            BootstrapInstallPlanResult planResult = BootstrapInstallPlanner.BuildPlan(
                catalog,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);

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
            return true;
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
            if (_listRequest != null || _addRequest != null)
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

            _installedPackageIds = new HashSet<string>(
                request.Result.Where(packageInfo => packageInfo != null)
                    .Select(packageInfo => packageInfo.name),
                StringComparer.OrdinalIgnoreCase);

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

            _stepIndex = FindNextMissingStepIndex(_installPlan, installedPackageIds);

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
            DisposeCatalogRequest();
            SaveState();
            EditorApplication.update -= UpdateRequests;
            Repaint();
        }

        private void StartInstall(BootstrapPackageStep step)
        {
            if (_listRequest != null || _addRequest != null || _packageListRetryQueued)
            {
                return;
            }

            try
            {
                _pendingPackageId = step.PackageId;
                _waitingForPackageRefresh = true;
                _packageListRetryCount = 0;
                _setupInterrupted = false;
                _status = "Installing " + step.PackageId + "...";
                _error = string.Empty;
                SaveState();
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
                if (_waitingForPackageRefresh && !string.IsNullOrWhiteSpace(_pendingPackageId))
                {
                    _status = "Waiting for Unity package refresh after installing " + _pendingPackageId + "...";
                    _error = string.Empty;
                    SaveState();
                    RefreshInstalledPackages(_status, true);
                    return;
                }

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
            DisposeCatalogRequest();
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
            if (string.IsNullOrWhiteSpace(_pendingPackageId))
            {
                _waitingForPackageRefresh = false;
            }
            _installMode = (BootstrapInstallMode)Mathf.Clamp(
                SessionState.GetInt(InstallModeKey, (int)BootstrapInstallMode.ScopedRegistry),
                (int)BootstrapInstallMode.GitFallback,
                (int)BootstrapInstallMode.ScopedRegistry);
            _registrySource = string.Empty;
            _catalogNotice = string.Empty;
            _packageInstallerOpenMessage = string.Empty;
        }

        private void SaveState()
        {
            SessionState.SetBool(ActiveKey, _setupActive);
            SessionState.SetInt(StepIndexKey, _stepIndex);
            SessionState.SetString(StatusKey, _status ?? string.Empty);
            SessionState.SetString(ErrorKey, _error ?? string.Empty);
            SessionState.SetInt(InstallModeKey, (int)_installMode);
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

        private bool AreRequiredPackagesInstalled()
        {
            return _installedPackageIds != null && RequiredSetupPackages.All(package => IsPackageInstalled(package.PackageId));
        }

        private bool IsSetupHealthy()
        {
            if (!AreRequiredPackagesInstalled())
            {
                return false;
            }

            return _installMode != BootstrapInstallMode.ScopedRegistry ||
                (_scopedRegistryStatus != null && _scopedRegistryStatus.Configured);
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

            if (_installMode == BootstrapInstallMode.ScopedRegistry &&
                _scopedRegistryStatus != null &&
                !_scopedRegistryStatus.Configured &&
                AreRequiredPackagesInstalled())
            {
                return true;
            }

            return HasSomeRequiredPackagesInstalled() && !AreRequiredPackagesInstalled();
        }

        private bool HasSomeRequiredPackagesInstalled()
        {
            return _installedPackageIds != null && RequiredSetupPackages.Any(package => IsPackageInstalled(package.PackageId));
        }

        private bool IsPackageInstallerInstalled =>
            IsPackageInstalled(DeucarianBootstrapPackageConstants.PackageInstallerPackageId);

        private bool IsRequestActive =>
            (_catalogRequest != null && !_catalogRequest.isDone) ||
            (_listRequest != null && !_listRequest.IsCompleted) ||
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
                _logoTexture = GetBuiltInIcon("d_Package Manager", "Package Manager", "d_Folder Icon", "Folder Icon");
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

            _windowBackgroundColor = proSkin ? FromRgb(33, 39, 44) : FromRgb(221, 227, 230);
            _heroBackgroundColor = proSkin ? FromRgb(13, 30, 43) : FromRgb(24, 78, 88);
            _cardBackgroundColor = proSkin ? FromRgb(45, 52, 58) : FromRgb(238, 241, 243);
            _rowBackgroundColor = proSkin ? FromRgb(39, 46, 51) : FromRgb(232, 236, 239);
            _rowAlternateBackgroundColor = proSkin ? FromRgb(43, 50, 56) : FromRgb(241, 244, 246);
            _borderColor = proSkin ? FromRgb(58, 72, 80) : FromRgb(190, 201, 208);
            _titleTextColor = proSkin ? FromRgb(232, 237, 240) : FromRgb(31, 43, 50);
            _bodyTextColor = proSkin ? FromRgb(207, 216, 222) : FromRgb(46, 56, 63);
            _mutedTextColor = proSkin ? FromRgb(155, 166, 174) : FromRgb(91, 105, 114);
            _successColor = FromRgb(71, 137, 104);
            _infoColor = FromRgb(76, 121, 165);
            _neutralColor = proSkin ? FromRgb(90, 96, 101) : FromRgb(145, 153, 159);
            _errorColor = FromRgb(163, 82, 82);

            _windowStyle = new GUIStyle
            {
                padding = new RectOffset(12, 12, 12, 10)
            };

            _heroStyle = CopyStyle(() => EditorStyles.helpBox);
            _heroStyle.padding = new RectOffset(0, 0, 0, 0);
            _heroStyle.margin = new RectOffset(0, 0, 0, 10);
            _heroStyle.normal.background = TextureForColor("hero", _heroBackgroundColor);

            _cardStyle = CopyStyle(() => EditorStyles.helpBox);
            _cardStyle.padding = new RectOffset(12, 12, 10, 10);
            _cardStyle.margin = new RectOffset(0, 0, 0, 8);
            _cardStyle.normal.background = TextureForColor("card", _cardBackgroundColor);

            _heroTitleStyle = CopyStyle(() => EditorStyles.boldLabel);
            _heroTitleStyle.fontSize = 28;
            _heroTitleStyle.fontStyle = FontStyle.Bold;
            _heroTitleStyle.wordWrap = true;
            _heroTitleStyle.alignment = TextAnchor.MiddleCenter;
            _heroTitleStyle.normal.textColor = Color.white;

            _heroSubtitleLargeStyle = CopyStyle(() => EditorStyles.label);
            _heroSubtitleLargeStyle.fontSize = 15;
            _heroSubtitleLargeStyle.fontStyle = FontStyle.Bold;
            _heroSubtitleLargeStyle.wordWrap = true;
            _heroSubtitleLargeStyle.alignment = TextAnchor.MiddleCenter;
            _heroSubtitleLargeStyle.normal.textColor = FromRgb(154, 238, 226);

            _heroEyebrowStyle = CopyStyle(() => EditorStyles.wordWrappedMiniLabel);
            _heroEyebrowStyle.wordWrap = true;
            _heroEyebrowStyle.alignment = TextAnchor.UpperCenter;
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
            _productStatusStyle.clipping = TextClipping.Clip;
            _productStatusStyle.normal.textColor = Color.white;

            _productStatusDetailStyle = CopyStyle(() => EditorStyles.miniLabel);
            _productStatusDetailStyle.alignment = TextAnchor.MiddleLeft;
            _productStatusDetailStyle.clipping = TextClipping.Clip;
            _productStatusDetailStyle.normal.textColor = Color.white;

            _statusIconStyle = CopyStyle(() => EditorStyles.miniBoldLabel);
            _statusIconStyle.alignment = TextAnchor.MiddleCenter;
            _statusIconStyle.normal.textColor = Color.white;
            _statusIconStyle.fontSize = 11;
            _statusIconStyle.padding = new RectOffset(0, 0, 0, 1);

            _statusLabelStyle = CopyStyle(() => EditorStyles.label);
            _statusLabelStyle.clipping = TextClipping.Clip;
            _statusLabelStyle.normal.textColor = _bodyTextColor;

            _statusDetailStyle = CopyStyle(() => EditorStyles.miniLabel);
            _statusDetailStyle.clipping = TextClipping.Clip;
            _statusDetailStyle.normal.textColor = _mutedTextColor;

            _summaryValueStyle = CopyStyle(() => EditorStyles.boldLabel);
            _summaryValueStyle.fontSize = 11;
            _summaryValueStyle.clipping = TextClipping.Clip;
            _summaryValueStyle.normal.textColor = _titleTextColor;

            _summaryLabelStyle = CopyStyle(() => EditorStyles.miniLabel);
            _summaryLabelStyle.clipping = TextClipping.Clip;
            _summaryLabelStyle.normal.textColor = _mutedTextColor;

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
            _primaryButtonStyle.normal.background = TextureForColor("primary", _infoColor);
            _primaryButtonStyle.hover.background = TextureForColor("primary-hover", _successColor);
            _primaryButtonStyle.active.background = TextureForColor("primary-active", _successColor);

            _secondaryButtonStyle = CopyStyle(() => GUI.skin.button);
            _secondaryButtonStyle.alignment = TextAnchor.MiddleCenter;
            _secondaryButtonStyle.normal.textColor = _bodyTextColor;

            _utilityButtonStyle = CopyStyle(() => EditorStyles.miniButton);
            _utilityButtonStyle.alignment = TextAnchor.MiddleCenter;
            _utilityButtonStyle.clipping = TextClipping.Clip;
            _utilityButtonStyle.fontSize = 11;
            _utilityButtonStyle.normal.textColor = _bodyTextColor;

            _badgeStyle = CopyStyle(() => EditorStyles.miniBoldLabel);
            _badgeStyle.alignment = TextAnchor.MiddleCenter;
            _badgeStyle.normal.textColor = Color.white;
            _badgeStyle.normal.background = TextureForColor("badge", _successColor);
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
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), _windowBackgroundColor);
        }

        private Color GetStatusColor(BootstrapStatusKind kind)
        {
            switch (kind)
            {
                case BootstrapStatusKind.Success:
                    return _successColor;
                case BootstrapStatusKind.Info:
                    return _infoColor;
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
            string key = name + "-" + ColorUtility.ToHtmlStringRGBA(color);

            if (TextureCache.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "Deucarian Bootstrap " + name
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            TextureCache[key] = texture;
            return texture;
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

        private enum BootstrapStatusKind
        {
            Success,
            Neutral,
            Info,
            Error
        }

        private enum BootstrapInstallMode
        {
            GitFallback,
            ScopedRegistry
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
