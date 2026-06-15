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
        private const char PlanSeparator = '|';

        internal const float PreferredWindowWidth = 760f;
        internal const float PreferredWindowHeight = 860f;
        internal const float MinWindowWidth = 740f;
        internal const float MinWindowHeight = 720f;
        private const float ActionColumnWidth = 250f;
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
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _taglineStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _miniMutedStyle;
        private GUIStyle _statusIconStyle;
        private GUIStyle _statusLabelStyle;
        private GUIStyle _statusDetailStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _secondaryButtonStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _footerStyle;
        private GUIStyle _footerRightStyle;

        private Texture2D _logoTexture;

        internal IReadOnlyList<BootstrapPackageStep> InstallPlan => _installPlan;

        internal string RegistrySource => _registrySource ?? string.Empty;

        [MenuItem(DeucarianBootstrapPackageConstants.MenuPath)]
        public static void Open()
        {
            DeucarianBootstrapWindow window = GetWindow<DeucarianBootstrapWindow>();
            window.titleContent = new GUIContent("Deucarian Bootstrap");
            window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            window.Show();
            EnsurePreferredFloatingWindowSize(window);
        }

        [InitializeOnLoadMethod]
        private static void ScheduleActiveSetupResume()
        {
            EditorApplication.delayCall -= ResumeActiveSetupAfterReload;
            EditorApplication.delayCall += ResumeActiveSetupAfterReload;
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

            window.titleContent = new GUIContent("Deucarian Bootstrap");
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
            titleContent = new GUIContent("Deucarian Bootstrap");
            minSize = new Vector2(MinWindowWidth, MinWindowHeight);
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

        private void OnGUI()
        {
            EnsureStyles();
            DrawWindowBackground();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            using (new EditorGUILayout.VerticalScope(_windowStyle))
            {
                DrawHero();
                DrawStatusAndActions();
                DrawInstallPlan();
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
                RefreshScopedRegistryStatus();
                HandleDelayedEnable();
            }
        }

        private void DrawHero()
        {
            using (new EditorGUILayout.HorizontalScope(_heroStyle, GUILayout.MinHeight(112f)))
            {
                Texture2D logo = GetLogoTexture();
                Rect logoRect = GUILayoutUtility.GetRect(88f, 88f, GUILayout.Width(88f), GUILayout.Height(88f));
                GUI.DrawTexture(logoRect, logo, ScaleMode.ScaleToFit, true);

                GUILayout.Space(12f);

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Deucarian", _titleStyle);
                    EditorGUILayout.LabelField("Unity package ecosystem setup", _subtitleStyle);
                    EditorGUILayout.LabelField(
                        "Install, repair, and launch the Deucarian package ecosystem.",
                        _taglineStyle);
                    GUILayout.FlexibleSpace();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("BOOTSTRAP", _badgeStyle, GUILayout.Width(92f), GUILayout.Height(22f));
            }
        }

        private void DrawStatusAndActions()
        {
            bool stacked = position.width < 760f;

            if (stacked)
            {
                DrawStatusCard();
                DrawActionCard(GUILayout.ExpandWidth(true));
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStatusCard(GUILayout.ExpandWidth(true));
                GUILayout.Space(8f);
                DrawActionCard(GUILayout.Width(ActionColumnWidth));
            }
        }

        private void DrawStatusCard(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle, options))
            {
                EditorGUILayout.LabelField("Setup Status", _sectionTitleStyle);
                EditorGUILayout.LabelField(GetSetupSummary(), _mutedStyle);
                GUILayout.Space(8f);

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
        }

        private void DrawActionCard(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle, options))
            {
                EditorGUILayout.LabelField("Setup Hub", _sectionTitleStyle);
                EditorGUILayout.LabelField(GetActionSummary(), _mutedStyle);
                GUILayout.Space(10f);

                EditorGUI.BeginChangeCheck();
                int selectedMode = GUILayout.Toolbar(
                    (int)_installMode,
                    new[] { "Git fallback", "Scoped registry" },
                    GUILayout.Height(26f));
                if (EditorGUI.EndChangeCheck())
                {
                    SetInstallMode((BootstrapInstallMode)selectedMode);
                }

                GUILayout.Space(6f);

                using (new EditorGUI.DisabledScope(IsPrimaryActionDisabled()))
                {
                    if (GUILayout.Button(GetPrimaryActionLabel(), _primaryButtonStyle, GUILayout.Height(36f)))
                    {
                        StartSetup();
                    }
                }

                GUILayout.Space(6f);

                using (new EditorGUI.DisabledScope(IsRequestActive))
                {
                    if (GUILayout.Button("Repair Scoped Registry", _secondaryButtonStyle, GUILayout.Height(28f)))
                    {
                        RepairScopedRegistry();
                    }
                }

                using (new EditorGUI.DisabledScope(_setupActive || IsRequestActive))
                {
                    if (GUILayout.Button("Refresh Status", _secondaryButtonStyle, GUILayout.Height(28f)))
                    {
                        RefreshStatus();
                    }
                }

                using (new EditorGUI.DisabledScope(!IsPackageInstallerInstalled))
                {
                    if (GUILayout.Button("Open Package Installer", _secondaryButtonStyle, GUILayout.Height(28f)))
                    {
                        OpenPackageInstaller();
                    }
                }

                string packageInstallerHelp = GetPackageInstallerHelpText();
                if (!string.IsNullOrWhiteSpace(packageInstallerHelp))
                {
                    EditorGUILayout.LabelField(packageInstallerHelp, _miniMutedStyle);
                }

                if (!string.IsNullOrWhiteSpace(_packageInstallerOpenMessage))
                {
                    EditorGUILayout.HelpBox(_packageInstallerOpenMessage, MessageType.Info);
                }

                GUILayout.Space(4f);

                if (GUILayout.Button("Open GitHub", _secondaryButtonStyle, GUILayout.Height(26f)))
                {
                    Application.OpenURL(DeucarianBootstrapPackageConstants.GitHubUrl);
                }

                if (GUILayout.Button("Open Documentation", _secondaryButtonStyle, GUILayout.Height(26f)))
                {
                    Application.OpenURL(DeucarianBootstrapPackageConstants.DocumentationUrl);
                }
            }
        }

        private void DrawInstallPlan()
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
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
                    Rect progressRect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true));
                    float progress = Mathf.Clamp01(_installPlan.Count == 0 ? 0f : (float)_stepIndex / _installPlan.Count);
                    EditorGUI.ProgressBar(progressRect, progress, _stepIndex + "/" + _installPlan.Count);
                    GUILayout.Space(4f);
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
                    "Bootstrap is only the first-time setup and repair tool.",
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
                return "Required setup packages are installed. Package Installer can manage the broader ecosystem.";
            }

            return missing + " setup package" + (missing == 1 ? " is" : "s are") + " missing. Setup will install only what is needed.";
        }

        private string GetActionSummary()
        {
            if (_setupActive)
            {
                return _waitingForPackageRefresh
                    ? "Setup is waiting for Unity to finish refreshing packages before continuing."
                    : "Setup is in progress. Bootstrap will continue in dependency order.";
            }

            if (_installedPackageIds != null && RequiredSetupPackages.All(package => IsPackageInstalled(package.PackageId)))
            {
                return "Setup is complete. Use Package Installer for day-to-day package work.";
            }

            return _installMode == BootstrapInstallMode.ScopedRegistry
                ? "Configure the Unity scoped registry and install Package Installer from npmjs."
                : "Install or repair the minimum ecosystem through Git fallback URLs.";
        }

        private string GetPrimaryActionLabel()
        {
            if (AreRequiredPackagesInstalled())
            {
                return "Setup Complete";
            }

            if (_setupActive)
            {
                return _waitingForPackageRefresh ? "Continuing Deucarian Setup" : "Setup In Progress";
            }

            if (_setupInterrupted)
            {
                return "Continue Deucarian Setup";
            }

            if (HasSomeRequiredPackagesInstalled())
            {
                return "Repair Deucarian Setup";
            }

            return "Install Deucarian Setup";
        }

        private bool IsPrimaryActionDisabled()
        {
            return IsRequestActive || _setupActive || AreRequiredPackagesInstalled();
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
                ? "Use npmjs scoped registry setup"
                : "Use catalog Git URLs";
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

        private string GetPackageInstallerHelpText()
        {
            if (_installedPackageIds == null)
            {
                return "Package Installer opens after Unity finishes checking installed packages.";
            }

            if (!IsPackageInstallerInstalled)
            {
                return "Install or repair setup before opening Package Installer.";
            }

            return string.Empty;
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
                _status = "Scoped registry mode selected. Repair the registry if needed, then run setup.";
            }
            else
            {
                _status = "Git fallback mode selected. Bootstrap will use catalog Git URLs.";
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
                InterruptSetup("Setup plan is not available.", "Bootstrap could not resolve an install plan to continue.");
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
                SessionState.GetInt(InstallModeKey, (int)BootstrapInstallMode.GitFallback),
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
            _heroBackgroundColor = proSkin ? FromRgb(38, 47, 53) : FromRgb(227, 232, 235);
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
            _heroStyle.padding = new RectOffset(14, 14, 12, 12);
            _heroStyle.margin = new RectOffset(0, 0, 0, 10);
            _heroStyle.normal.background = TextureForColor("hero", _heroBackgroundColor);

            _cardStyle = CopyStyle(() => EditorStyles.helpBox);
            _cardStyle.padding = new RectOffset(12, 12, 10, 10);
            _cardStyle.margin = new RectOffset(0, 0, 0, 8);
            _cardStyle.normal.background = TextureForColor("card", _cardBackgroundColor);

            _titleStyle = CopyStyle(() => EditorStyles.boldLabel);
            _titleStyle.fontSize = 24;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.wordWrap = true;
            _titleStyle.alignment = TextAnchor.MiddleLeft;
            _titleStyle.normal.textColor = _titleTextColor;

            _subtitleStyle = CopyStyle(() => EditorStyles.label);
            _subtitleStyle.fontSize = 13;
            _subtitleStyle.fontStyle = FontStyle.Bold;
            _subtitleStyle.wordWrap = true;
            _subtitleStyle.normal.textColor = _bodyTextColor;

            _taglineStyle = CopyStyle(() => EditorStyles.wordWrappedMiniLabel);
            _taglineStyle.wordWrap = true;
            _taglineStyle.normal.textColor = _mutedTextColor;

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
