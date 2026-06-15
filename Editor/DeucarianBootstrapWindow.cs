using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class DeucarianBootstrapWindow : EditorWindow
    {
        private const string ActiveKey = "Deucarian.Bootstrap.Active";
        private const string StepIndexKey = "Deucarian.Bootstrap.StepIndex";
        private const string StatusKey = "Deucarian.Bootstrap.Status";
        private const string ErrorKey = "Deucarian.Bootstrap.Error";

        private static readonly BootstrapPackageStep[] SetupSteps =
        {
            new BootstrapPackageStep(
                DeucarianBootstrapPackageConstants.EditorPackageId,
                DeucarianBootstrapPackageConstants.EditorPackageDisplayName,
                DeucarianBootstrapPackageConstants.EditorPackageGitUrl),
            new BootstrapPackageStep(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageDisplayName,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageGitUrl)
        };

        private ListRequest _listRequest;
        private AddRequest _addRequest;
        private bool _setupActive;
        private int _stepIndex;
        private string _status;
        private string _error;
        private Vector2 _scrollPosition;

        internal static IReadOnlyList<BootstrapPackageStep> Steps => SetupSteps;

        [MenuItem(DeucarianBootstrapPackageConstants.MenuPath)]
        public static void Open()
        {
            DeucarianBootstrapWindow window = GetWindow<DeucarianBootstrapWindow>();
            window.titleContent = new GUIContent("Deucarian Bootstrap");
            window.minSize = new Vector2(420f, 300f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadState();

            if (_setupActive && !IsRequestActive)
            {
                RefreshInstalledPackages("Resuming Deucarian setup...");
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateRequests;
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawHeader();
            DrawSetupStatus();
            DrawSetupSteps();
            DrawActions();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Deucarian Bootstrap", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "First-time setup for Deucarian Editor and Package Installer.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8f);
        }

        private void DrawSetupStatus()
        {
            string status = string.IsNullOrWhiteSpace(_status)
                ? "Ready to install Deucarian packages."
                : _status;

            EditorGUILayout.HelpBox(status, string.IsNullOrWhiteSpace(_error) ? MessageType.Info : MessageType.Error);

            if (!string.IsNullOrWhiteSpace(_error))
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }
        }

        private void DrawSetupSteps()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Setup Order", EditorStyles.boldLabel);

            for (int i = 0; i < SetupSteps.Length; i++)
            {
                BootstrapPackageStep step = SetupSteps[i];
                string prefix = i < _stepIndex
                    ? "Done"
                    : i == _stepIndex && _setupActive
                        ? "Now"
                        : "Next";

                EditorGUILayout.LabelField(prefix + " - " + step.DisplayName, step.PackageId);
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(12f);

            using (new EditorGUI.DisabledScope(IsRequestActive))
            {
                string buttonLabel = _setupActive ? "Continue Setup" : "Install Editor and Package Installer";

                if (GUILayout.Button(buttonLabel, GUILayout.Height(32f)))
                {
                    StartSetup();
                }
            }

            using (new EditorGUI.DisabledScope(_setupActive || IsRequestActive))
            {
                if (GUILayout.Button("Open Package Installer"))
                {
                    OpenPackageInstaller();
                }
            }
        }

        private void StartSetup()
        {
            _setupActive = true;
            _stepIndex = Mathf.Clamp(_stepIndex, 0, SetupSteps.Length);
            _error = string.Empty;
            _status = "Checking installed packages...";
            SaveState();
            RefreshInstalledPackages(_status);
        }

        private void RefreshInstalledPackages(string status)
        {
            if (_listRequest != null || _addRequest != null)
            {
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

        private void UpdateRequests()
        {
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

            HashSet<string> installedPackageIds = new HashSet<string>(
                request.Result.Where(packageInfo => packageInfo != null)
                    .Select(packageInfo => packageInfo.name),
                StringComparer.OrdinalIgnoreCase);

            ContinueFromInstalledPackages(installedPackageIds);
        }

        private void ContinueFromInstalledPackages(ISet<string> installedPackageIds)
        {
            while (_stepIndex < SetupSteps.Length && installedPackageIds.Contains(SetupSteps[_stepIndex].PackageId))
            {
                _stepIndex++;
            }

            if (_stepIndex >= SetupSteps.Length)
            {
                CompleteSetup();
                return;
            }

            StartInstall(SetupSteps[_stepIndex]);
        }

        private void StartInstall(BootstrapPackageStep step)
        {
            try
            {
                _status = "Installing " + step.DisplayName + "...";
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
            BootstrapPackageStep completedStep = _stepIndex < SetupSteps.Length ? SetupSteps[_stepIndex] : null;
            _addRequest = null;

            if (request.Status != StatusCode.Success)
            {
                string packageName = completedStep != null ? completedStep.DisplayName : "package";
                Fail("Install failed for " + packageName + ".", request.Error != null ? request.Error.message : "Package Manager returned an unknown error.");
                return;
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
            _stepIndex = SetupSteps.Length;
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
        }

        private void SaveState()
        {
            SessionState.SetBool(ActiveKey, _setupActive);
            SessionState.SetInt(StepIndexKey, _stepIndex);
            SessionState.SetString(StatusKey, _status ?? string.Empty);
            SessionState.SetString(ErrorKey, _error ?? string.Empty);
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
