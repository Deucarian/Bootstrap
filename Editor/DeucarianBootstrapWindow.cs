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

        private readonly List<BootstrapPackageStep> _installPlan = new List<BootstrapPackageStep>();

        private UnityWebRequest _catalogRequest;
        private ListRequest _listRequest;
        private AddRequest _addRequest;
        private HashSet<string> _installedPackageIds;
        private bool _catalogLoaded;
        private bool _setupActive;
        private int _stepIndex;
        private string _registrySource;
        private string _catalogNotice;
        private string _status;
        private string _error;
        private Vector2 _scrollPosition;

        internal IReadOnlyList<BootstrapPackageStep> InstallPlan => _installPlan;

        internal string RegistrySource => _registrySource ?? string.Empty;

        [MenuItem(DeucarianBootstrapPackageConstants.MenuPath)]
        public static void Open()
        {
            DeucarianBootstrapWindow window = GetWindow<DeucarianBootstrapWindow>();
            window.titleContent = new GUIContent("Deucarian Bootstrap");
            window.minSize = new Vector2(460f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Deucarian Bootstrap");
            minSize = new Vector2(460f, 360f);
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
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawHeader();
            DrawRegistryStatus();
            DrawSetupStatus();
            DrawInstallPlan();
            DrawActions();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Deucarian Bootstrap", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "First-time setup for Deucarian Editor, dependencies, and Package Installer.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8f);
        }

        private void DrawRegistryStatus()
        {
            string source = string.IsNullOrWhiteSpace(_registrySource) ? "Loading..." : _registrySource;

            EditorGUILayout.LabelField("Registry Source", source);

            if (!string.IsNullOrWhiteSpace(_catalogNotice))
            {
                EditorGUILayout.HelpBox(_catalogNotice, MessageType.Warning);
            }
        }

        private void DrawSetupStatus()
        {
            string status = string.IsNullOrWhiteSpace(_status)
                ? "Ready to install Deucarian packages."
                : _status;

            if (_setupActive && _installPlan.Count > 0 && _stepIndex < _installPlan.Count)
            {
                BootstrapPackageStep currentStep = _installPlan[_stepIndex];
                status += "\nCurrent step: " + (_stepIndex + 1) + "/" + _installPlan.Count + " - " + currentStep.DisplayName + ".";
            }

            EditorGUILayout.HelpBox(status, string.IsNullOrWhiteSpace(_error) ? MessageType.Info : MessageType.Error);

            if (!string.IsNullOrWhiteSpace(_error))
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }
        }

        private void DrawInstallPlan()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Resolved Install Plan", EditorStyles.boldLabel);

            if (!_catalogLoaded)
            {
                EditorGUILayout.LabelField("Loading package catalog...");
                return;
            }

            if (_installPlan.Count == 0)
            {
                EditorGUILayout.HelpBox("No install plan is available.", MessageType.Warning);
                return;
            }

            for (int i = 0; i < _installPlan.Count; i++)
            {
                BootstrapPackageStep step = _installPlan[i];
                string state = GetPlanStepState(i, step);
                EditorGUILayout.LabelField((i + 1) + ". " + step.DisplayName, state + " - " + step.PackageId);
                EditorGUILayout.LabelField(string.Empty, step.GitUrl, EditorStyles.miniLabel);
            }
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

        private void DrawActions()
        {
            EditorGUILayout.Space(12f);

            using (new EditorGUI.DisabledScope(IsRequestActive))
            {
                string buttonLabel = _setupActive ? "Continue Setup" : "Install Resolved Plan";

                if (GUILayout.Button(buttonLabel, GUILayout.Height(32f)))
                {
                    StartSetup();
                }
            }

            using (new EditorGUI.DisabledScope(_setupActive || IsRequestActive))
            {
                if (GUILayout.Button("Reload Catalog"))
                {
                    ReloadCatalog();
                }

                if (GUILayout.Button("Open Package Installer"))
                {
                    OpenPackageInstaller();
                }
            }
        }

        private void ReloadCatalog()
        {
            _catalogLoaded = false;
            _installPlan.Clear();
            _installedPackageIds = null;
            _stepIndex = 0;
            _error = string.Empty;
            _catalogNotice = string.Empty;
            BeginCatalogLoad("Reloading Deucarian package catalog...");
        }

        private void StartSetup()
        {
            _setupActive = true;
            _stepIndex = Mathf.Clamp(_stepIndex, 0, _installPlan.Count);
            _error = string.Empty;

            if (!_catalogLoaded)
            {
                _status = "Loading package catalog before setup...";
                SaveState();
                BeginCatalogLoad(_status);
                return;
            }

            _status = "Checking installed packages...";
            SaveState();
            RefreshInstalledPackages(_status);
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
                    : "Install plan ready from remote catalog.");
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
                : "Install plan ready from bundled fallback catalog.");
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
                RefreshInstalledPackages(status);
                return;
            }

            _status = status;
            SaveState();
            EditorApplication.update -= UpdateRequests;
            Repaint();
        }

        private void RefreshInstalledPackages(string status)
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
                _listRequest = Client.List(true, true);
                EditorApplication.update -= UpdateRequests;
                EditorApplication.update += UpdateRequests;
                Repaint();
            }
            catch (Exception exception)
            {
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

            ContinueFromInstalledPackages(_installedPackageIds);
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
            RefreshInstalledPackages("Checking next setup step...");
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

        private static void OpenPackageInstaller()
        {
            if (EditorApplication.ExecuteMenuItem(DeucarianBootstrapPackageConstants.PackageInstallerMenuPath))
            {
                return;
            }

            EditorApplication.ExecuteMenuItem(DeucarianBootstrapPackageConstants.LegacyPackageInstallerMenuPath);
        }

        private bool IsRequestActive =>
            (_catalogRequest != null && !_catalogRequest.isDone) ||
            (_listRequest != null && !_listRequest.IsCompleted) ||
            (_addRequest != null && !_addRequest.IsCompleted);
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
