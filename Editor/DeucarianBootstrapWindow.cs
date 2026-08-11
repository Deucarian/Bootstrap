using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class DeucarianBootstrapWindow : EditorWindow
    {
        private const string StartupShownThisSessionKey =
            "Deucarian.Bootstrap.StartupShownThisSession";

        internal const float MinWindowWidth = 560f;
        internal const float MinWindowHeight = 480f;
        internal const float PreferredWindowWidth = 980f;
        internal const float PreferredWindowHeight = 700f;

        private BootstrapSetupCoordinator _coordinator;
        private BootstrapPackageInstallerHandoff _handoff;
        private BootstrapSetupView _view;
        private string _transientMessage = string.Empty;
        private bool _lastProSkin;

        [MenuItem(DeucarianBootstrapPackageConstants.MenuPath)]
        public static void Open()
        {
            DeucarianBootstrapWindow window = GetWindow<DeucarianBootstrapWindow>();
            window.titleContent = new GUIContent("Deucarian Setup");
            window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            window.Show();
            EnsureUsefulInitialSize(window);
        }

        [InitializeOnLoadMethod]
        private static void ScheduleStartupWelcome()
        {
            EditorApplication.delayCall -= OpenStartupWelcomeWhenReady;
            EditorApplication.delayCall += OpenStartupWelcomeWhenReady;
        }

        [InitializeOnLoadMethod]
        private static void ScheduleOperationResume()
        {
            EditorApplication.delayCall -= ResumeActiveOperationAfterReload;
            EditorApplication.delayCall += ResumeActiveOperationAfterReload;
        }

        private static void OpenStartupWelcomeWhenReady()
        {
            EditorApplication.delayCall -= OpenStartupWelcomeWhenReady;
            if (Application.isBatchMode ||
                SessionState.GetBool(StartupShownThisSessionKey, false) ||
                !BootstrapStartupPreferences.ShouldShow())
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

            SessionState.SetBool(StartupShownThisSessionKey, true);
            if (FindExistingWindow() == null)
            {
                Open();
            }
        }

        private static void ResumeActiveOperationAfterReload()
        {
            EditorApplication.delayCall -= ResumeActiveOperationAfterReload;
            BootstrapOperationState state = new BootstrapSessionOperationStore().Load();
            if (state == null || !state.Active)
            {
                return;
            }

            Open();
        }

        private static DeucarianBootstrapWindow FindExistingWindow()
        {
            return Resources.FindObjectsOfTypeAll<DeucarianBootstrapWindow>().FirstOrDefault();
        }

        private static void EnsureUsefulInitialSize(DeucarianBootstrapWindow window)
        {
            if (window == null || window.docked)
            {
                return;
            }

            Rect current = window.position;
            if (current.width >= MinWindowWidth && current.height >= MinWindowHeight)
            {
                return;
            }

            window.position = new Rect(
                current.x,
                current.y,
                Mathf.Max(current.width, PreferredWindowWidth),
                Mathf.Max(current.height, PreferredWindowHeight));
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Deucarian Setup");
            minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            _lastProSkin = EditorGUIUtility.isProSkin;
            _coordinator = BootstrapCompositionRoot.CreateCoordinator();
            _handoff = BootstrapCompositionRoot.CreateHandoff();
            _coordinator.Changed += HandleCoordinatorChanged;
            _coordinator.Initialize();
        }

        public void CreateGUI()
        {
            _view = new BootstrapSetupView(
                HandleChannelChanged,
                HandlePrimaryAction,
                HandleRefresh,
                BootstrapStartupPreferences.SetShouldShow);
            _view.Build(rootVisualElement);
            Render();
        }

        private void OnFocus()
        {
            if (_coordinator != null && _coordinator.SynchronizeChannel())
            {
                _transientMessage = string.Empty;
            }
        }

        private void Update()
        {
            _coordinator?.Tick();
            bool proSkin = EditorGUIUtility.isProSkin;
            if (_view != null && proSkin != _lastProSkin)
            {
                _lastProSkin = proSkin;
                _view.SetSkin(proSkin);
            }
        }

        private void OnDisable()
        {
            if (_coordinator != null)
            {
                _coordinator.Changed -= HandleCoordinatorChanged;
                _coordinator.Dispose();
                _coordinator = null;
            }

            _handoff = null;
            _view = null;
        }

        private void HandleCoordinatorChanged()
        {
            Render();
            Repaint();
        }

        private void HandleChannelChanged(BootstrapChannel channel)
        {
            _transientMessage = string.Empty;
            _coordinator?.SelectChannel(channel);
        }

        private void HandleRefresh()
        {
            _transientMessage = string.Empty;
            _coordinator?.Refresh();
        }

        private void HandlePrimaryAction(BootstrapSetupAction action)
        {
            _transientMessage = string.Empty;
            if (action == BootstrapSetupAction.OpenPackageInstaller)
            {
                BootstrapHandoffResult result = _handoff.Open();
                _transientMessage = result.Success ? string.Empty : result.Message;
                Render();
                return;
            }

            if (action == BootstrapSetupAction.Refresh)
            {
                _coordinator.Refresh();
                return;
            }

            _coordinator.BeginSetup();
        }

        private void Render()
        {
            if (_view == null || _coordinator == null)
            {
                return;
            }

            _view.Render(BootstrapPresentationModelFactory.Create(
                _coordinator.Snapshot,
                _transientMessage));
        }

        internal static bool ShouldShowOnStartup()
        {
            return BootstrapStartupPreferences.ShouldShow();
        }

        internal static void SetShowOnStartup(bool showOnStartup)
        {
            BootstrapStartupPreferences.SetShouldShow(showOnStartup);
        }

        internal static BootstrapChannel GetPersistedChannel()
        {
            return BootstrapPackageInstallerStateRepository.GetProjectChannel();
        }

        internal static void SetPersistedChannel(BootstrapChannel channel)
        {
            BootstrapPackageInstallerStateRepository.SetProjectChannel(channel);
        }
    }
}
