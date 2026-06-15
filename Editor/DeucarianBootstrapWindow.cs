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

        private const float MinWindowWidth = 720f;
        private const float MinWindowHeight = 540f;
        private const float ActionColumnWidth = 250f;

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
        private int _stepIndex;
        private string _registrySource;
        private string _catalogNotice;
        private string _status;
        private string _error;
        private string _packageInstallerOpenMessage;
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
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Deucarian Bootstrap");
            minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            LoadState();
            BeginCatalogLoad(_setupActive ? "Resuming Deucarian setup..." : "Loading Deucarian package catalog...");
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateRequests;
            DisposeCatalogRequest();
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

                using (new EditorGUI.DisabledScope(IsRequestActive))
                {
                    string primaryLabel = _setupActive
                        ? "Continue Deucarian Setup"
                        : "Install / Repair Deucarian Setup";

                    if (GUILayout.Button(primaryLabel, _primaryButtonStyle, GUILayout.Height(36f)))
                    {
                        StartSetup();
                    }
                }

                GUILayout.Space(6f);

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
                        step.PackageId,
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
                return "Setup is in progress. Bootstrap will continue in dependency order.";
            }

            if (_installedPackageIds != null && RequiredSetupPackages.All(package => IsPackageInstalled(package.PackageId)))
            {
                return "Setup is complete. Use Package Installer for day-to-day package work.";
            }

            return "Install or repair the minimum ecosystem needed to launch Package Installer.";
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

        private string GetPackageStatusText(string packageId)
        {
            if (_installedPackageIds == null)
            {
                return _listRequest != null ? "Checking" : "Unknown";
            }

            return IsPackageInstalled(packageId) ? "Installed" : "Missing";
        }

        private BootstrapStatusKind GetPackageStatusKind(string packageId)
        {
            if (_installedPackageIds == null)
            {
                return _listRequest != null ? BootstrapStatusKind.Info : BootstrapStatusKind.Neutral;
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

        private BootstrapStatusKind GetPlanStepStatusKind(int index, BootstrapPackageStep step)
        {
            if (_installedPackageIds != null && _installedPackageIds.Contains(step.PackageId))
            {
                return BootstrapStatusKind.Success;
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
            _stepIndex = Mathf.Clamp(_stepIndex, 0, _installPlan.Count);
            _packageInstallerOpenMessage = string.Empty;
            ReloadCatalog();
        }

        private void ReloadCatalog()
        {
            DisposeCatalogRequest();
            _catalogLoaded = false;
            _continueSetupAfterPackageList = false;
            _installPlan.Clear();
            _installedPackageIds = null;
            _stepIndex = 0;
            _error = string.Empty;
            _catalogNotice = string.Empty;
            _registrySource = string.Empty;
            SaveState();
            BeginCatalogLoad("Reloading Deucarian package catalog...");
        }

        private void StartSetup()
        {
            _setupActive = true;
            _stepIndex = Mathf.Clamp(_stepIndex, 0, _installPlan.Count);
            _error = string.Empty;
            _packageInstallerOpenMessage = string.Empty;

            if (!_catalogLoaded)
            {
                _status = "Loading package catalog before setup...";
                SaveState();
                BeginCatalogLoad(_status);
                return;
            }

            _status = "Checking installed packages...";
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
                Fail("Installed package check failed.", request.Error != null ? request.Error.message : "Package Manager returned an unknown error.");
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
            while (_stepIndex < _installPlan.Count && installedPackageIds.Contains(_installPlan[_stepIndex].PackageId))
            {
                _stepIndex++;
            }

            if (_stepIndex >= _installPlan.Count)
            {
                CompleteSetup();
                return;
            }

            StartInstall(_installPlan[_stepIndex]);
        }

        private void StartInstall(BootstrapPackageStep step)
        {
            try
            {
                _status = "Installing " + step.DisplayName + " (" + (_stepIndex + 1) + "/" + _installPlan.Count + ")...";
                _error = string.Empty;
                SaveState();
                _addRequest = Client.Add(step.GitUrl);
                EditorApplication.update -= UpdateRequests;
                EditorApplication.update += UpdateRequests;
                Repaint();
            }
            catch (Exception exception)
            {
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

            _stepIndex++;
            _status = completedStep != null
                ? "Installed " + completedStep.DisplayName + "."
                : "Installed package.";
            SaveState();
            RefreshInstalledPackages("Checking next setup step...", true);
        }

        private void CompleteSetup()
        {
            _setupActive = false;
            _stepIndex = _installPlan.Count;
            _error = string.Empty;
            _status = "Deucarian setup completed. Package Installer is ready.";
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
            _continueSetupAfterPackageList = false;
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

        private bool IsPackageInstallerInstalled =>
            IsPackageInstalled(DeucarianBootstrapPackageConstants.PackageInstallerPackageId);

        private bool IsRequestActive =>
            (_catalogRequest != null && !_catalogRequest.isDone) ||
            (_listRequest != null && !_listRequest.IsCompleted) ||
            (_addRequest != null && !_addRequest.IsCompleted);

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
        {
            PackageId = packageId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            GitUrl = gitUrl ?? string.Empty;
        }

        public string PackageId { get; }

        public string DisplayName { get; }

        public string GitUrl { get; }
    }
}
