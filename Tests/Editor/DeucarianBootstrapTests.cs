using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Deucarian.Bootstrap.Editor.Tests
{
    public sealed class DeucarianBootstrapTests
    {
        private const string CurrentRevision = "1111111111111111111111111111111111111111";
        private const string PreviousRevision = "2222222222222222222222222222222222222222";

        [Test]
        public void PackageConstantsMatchBootstrapManifest()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            Assert.NotNull(packageInfo);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageName, packageInfo.name);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.Version, packageInfo.version);
        }

        [Test]
        public void BootstrapManifestHasNoPackageDependencies()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            string manifestPath = Path.Combine(packageInfo.resolvedPath, "package.json");
            string manifest = File.ReadAllText(manifestPath);

            StringAssert.Contains("\"dependencies\": {}", manifest);
            Assert.False(manifest.Contains("com.deucarian.editor"));
            Assert.False(manifest.Contains("com.deucarian.package-installer"));
            Assert.False(manifest.Contains("com.deucarian.logging"));
            Assert.False(manifest.Contains("com.deucarian.common"));
        }

        [Test]
        public void BootstrapHeroAssetsExistInPackage()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            string logoPath = Path.Combine(
                packageInfo.resolvedPath,
                DeucarianBootstrapPackageConstants.LogoAssetRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string backgroundPath = Path.Combine(
                packageInfo.resolvedPath,
                DeucarianBootstrapPackageConstants.HeroBackgroundAssetRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string wallpaperPath = Path.Combine(
                packageInfo.resolvedPath,
                DeucarianBootstrapPackageConstants.WallpaperAssetRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string iconPath = Path.Combine(
                packageInfo.resolvedPath,
                DeucarianBootstrapPackageConstants.PackageIconAssetRelativePath.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(logoPath), logoPath);
            Assert.True(File.Exists(backgroundPath), backgroundPath);
            Assert.True(File.Exists(wallpaperPath), wallpaperPath);
            Assert.True(File.Exists(iconPath), iconPath);
            Assert.True(DeucarianBootstrapWindow.ArePackageVisualAssetsAvailable());
        }

        [Test]
        public void BootstrapLinksAndAssetsStayPackageLocal()
        {
            StringAssert.StartsWith(
                "Packages/" + DeucarianBootstrapPackageConstants.PackageName + "/",
                DeucarianBootstrapPackageConstants.LogoAssetPath);
            StringAssert.StartsWith(
                "Packages/" + DeucarianBootstrapPackageConstants.PackageName + "/",
                DeucarianBootstrapPackageConstants.HeroBackgroundAssetPath);
            StringAssert.StartsWith(
                "Packages/" + DeucarianBootstrapPackageConstants.PackageName + "/",
                DeucarianBootstrapPackageConstants.WallpaperAssetPath);
            StringAssert.StartsWith(
                "Packages/" + DeucarianBootstrapPackageConstants.PackageName + "/",
                DeucarianBootstrapPackageConstants.PackageIconAssetPath);
            StringAssert.Contains("github.com/Deucarian/Bootstrap", DeucarianBootstrapPackageConstants.GitHubUrl);
            StringAssert.Contains("github.com/Deucarian/Bootstrap", DeucarianBootstrapPackageConstants.DocumentationUrl);
        }

        [Test]
        public void BootstrapUnityImportedRootFilesHaveMeta()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            string[] relativePaths =
            {
                "AGENTS.md",
                "CHANGELOG.md",
                "deucarian-package.json",
                "LICENSE.md",
                "package.json",
                "README.md",
                Path.Combine(".github", "copilot-instructions.md")
            };

            foreach (string relativePath in relativePaths)
            {
                string filePath = Path.Combine(packageInfo.resolvedPath, relativePath);
                if (!File.Exists(filePath))
                {
                    continue;
                }

                Assert.True(File.Exists(filePath + ".meta"), filePath + ".meta");
            }
        }

        [Test]
        public void BootstrapMetaGuidsAreUniqueInsidePackage()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            Dictionary<string, string> seenGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string metaPath in Directory.GetFiles(packageInfo.resolvedPath, "*.meta", SearchOption.AllDirectories))
            {
                string guid = ReadMetaGuid(metaPath);
                if (string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                Assert.False(seenGuids.TryGetValue(guid, out string existingPath), guid + " is used by both " + existingPath + " and " + metaPath);
                seenGuids[guid] = metaPath;
            }
        }

        [Test]
        public void BootstrapHeroCopyUsesFunctionalWording()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            string windowSourcePath = Path.Combine(packageInfo.resolvedPath, "Editor", "DeucarianBootstrapWindow.cs");
            string windowSource = File.ReadAllText(windowSourcePath);

            StringAssert.Contains("Install or repair the Deucarian package setup.", windowSource);
            StringAssert.Contains("\"Channel\"", windowSource);
            StringAssert.Contains("\"Stable\"", windowSource);
            StringAssert.Contains("\"Development\"", windowSource);
            StringAssert.Contains("\"Refresh\"", windowSource);
            StringAssert.Contains("\"GitHub\"", windowSource);
            StringAssert.Contains("\"Docs\"", windowSource);
            StringAssert.Contains("Setup progress", windowSource);
            StringAssert.Contains("Package Installer lock revision matches the selected remote Git branch.", windowSource);
            StringAssert.Contains("Show Bootstrap on startup", windowSource);
            StringAssert.Contains("Full Git URLs, install plan, status log, and read-only legacy scoped-registry detection are available here.", windowSource);
            StringAssert.Contains("Stable: Git #main", windowSource);
            StringAssert.Contains("Development: Git #develop", windowSource);
            StringAssert.Contains("npm/scoped registry is legacy and read-only", windowSource);
            StringAssert.Contains("Bootstrap leaves scopedRegistries unchanged.", windowSource);
            StringAssert.Contains("DrawStatusCard", windowSource);
            StringAssert.Contains("GUILayout.Width(320f)", windowSource);
            Assert.False(windowSource.Contains("Recommended. Uses npmjs scoped registry"));
            Assert.False(windowSource.Contains("\"Repair Registry\""));
            Assert.False(windowSource.Contains("EnsureConfigured"));

            int heroIndex = windowSource.IndexOf("DrawPackageInstallerProductCard();", StringComparison.Ordinal);
            int summaryIndex = windowSource.IndexOf("DrawCompactSetupSummary();", StringComparison.Ordinal);
            int detailsIndex = windowSource.IndexOf("DrawSetupDetails();", StringComparison.Ordinal);
            int actionsIndex = windowSource.IndexOf("DrawSetupActions();", StringComparison.Ordinal);
            Assert.Less(heroIndex, summaryIndex);
            Assert.Less(summaryIndex, detailsIndex);
            Assert.Less(detailsIndex, actionsIndex);
        }

        [Test]
        public void BootstrapWindowSizingDefaultsFitSetupHub()
        {
            Assert.AreEqual("Tools/Deucarian/Bootstrap/Open Bootstrapper", DeucarianBootstrapPackageConstants.MenuPath);
            Assert.GreaterOrEqual(DeucarianBootstrapWindow.MinWindowWidth, 1180f);
            Assert.GreaterOrEqual(DeucarianBootstrapWindow.MinWindowHeight, 820f);
            Assert.GreaterOrEqual(DeucarianBootstrapWindow.ContentMaxWidth, 1180f);
            Assert.GreaterOrEqual(DeucarianBootstrapWindow.PreferredWindowWidth, DeucarianBootstrapWindow.MinWindowWidth);
            Assert.GreaterOrEqual(DeucarianBootstrapWindow.PreferredWindowHeight, DeucarianBootstrapWindow.MinWindowHeight);
            Assert.LessOrEqual(
                DeucarianBootstrapWindow.HeroCardHeight / DeucarianBootstrapWindow.PreferredWindowHeight,
                0.34f);
            Assert.GreaterOrEqual(
                DeucarianBootstrapWindow.HeroCardHeight / DeucarianBootstrapWindow.PreferredWindowHeight,
                0.28f);
            Assert.AreEqual(166f, DeucarianBootstrapWindow.StatusGridHeight);
        }

        [Test]
        public void BootstrapWindowOpensAtMinimumAndLargerSizes()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("Window opening requires a graphics device.");
            }

            DeucarianBootstrapWindow window = EditorWindow.GetWindow<DeucarianBootstrapWindow>(
                false,
                "Bootstrap Visual Test",
                false);

            try
            {
                Assert.NotNull(window);
                Assert.GreaterOrEqual(window.minSize.x, DeucarianBootstrapWindow.MinWindowWidth);
                Assert.GreaterOrEqual(window.minSize.y, DeucarianBootstrapWindow.MinWindowHeight);

                window.position = new Rect(
                    100f,
                    100f,
                    DeucarianBootstrapWindow.MinWindowWidth,
                    DeucarianBootstrapWindow.MinWindowHeight);
                Assert.GreaterOrEqual(window.position.width, DeucarianBootstrapWindow.MinWindowWidth);
                Assert.GreaterOrEqual(window.position.height, DeucarianBootstrapWindow.MinWindowHeight);

                window.position = new Rect(
                    100f,
                    100f,
                    DeucarianBootstrapWindow.PreferredWindowWidth,
                    DeucarianBootstrapWindow.PreferredWindowHeight);
                Assert.GreaterOrEqual(window.position.width, DeucarianBootstrapWindow.PreferredWindowWidth);
                Assert.GreaterOrEqual(window.position.height, DeucarianBootstrapWindow.PreferredWindowHeight);
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                }
            }
        }

        [Test]
        public void HeroSummaryUsesShortTargetText()
        {
            string stableSummary = DeucarianBootstrapWindow.GetHeroShortTargetText(BootstrapChannel.Stable);
            string developmentSummary = DeucarianBootstrapWindow.GetHeroShortTargetText(BootstrapChannel.Development);

            Assert.AreEqual("Stable \u00b7 Package Installer #main", stableSummary);
            Assert.AreEqual("Development \u00b7 Package Installer #develop", developmentSummary);
            Assert.False(stableSummary.Contains("github.com"));
            Assert.False(developmentSummary.Contains("github.com"));
        }

        [Test]
        public void VisualFallbackTexturesDoNotThrow()
        {
            Texture2D logo = BootstrapVisualResources.CreateFallbackLogoTexture();
            Texture2D wallpaper = BootstrapVisualResources.CreateFallbackWallpaperTexture();

            Assert.NotNull(logo);
            Assert.NotNull(wallpaper);
            Assert.GreaterOrEqual(logo.width, 32);
            Assert.GreaterOrEqual(wallpaper.width, 64);
        }

        [Test]
        public void StatusCardsProvideCompactLabelValueAndSubtext()
        {
            DeucarianBootstrapWindow window = ScriptableObject.CreateInstance<DeucarianBootstrapWindow>();

            try
            {
                SetInstalledPackages(
                    window,
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
                SetInstalledPackageInfo(
                    window,
                    new BootstrapInstalledPackageInfo(
                        DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                        "1.1.58",
                        "Git",
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl,
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl,
                        CurrentRevision));
                SetField(window, "_catalogLoaded", true);
                SetField(window, "_registrySource", "Remote: " + DeucarianBootstrapPackageConstants.StableRegistryCatalogUrl);
                SetField(window, "_targetPackageInstallerVersion", "1.1.58");
                SetField(window, "_targetPackageInstallerRevision", CurrentRevision);

                DeucarianBootstrapWindow.BootstrapStatusCardModel[] cards = window.BuildStatusCards();

                Assert.AreEqual(4, cards.Length);
                Assert.AreEqual("Registry", cards[0].Label);
                Assert.AreEqual("Remote", cards[0].Value);
                Assert.AreEqual("Package Registry #main", cards[0].Subtext);
                Assert.AreEqual("Setup packages", cards[1].Label);
                Assert.AreEqual("Ready", cards[1].Value);
                Assert.AreEqual("Editor + Logging", cards[1].Subtext);
                Assert.AreEqual("Package Installer", cards[2].Label);
                Assert.AreEqual("Healthy", cards[2].Value);
                Assert.AreEqual("1.1.58 \u00b7 Git #main", cards[2].Subtext);
                Assert.AreEqual("Startup", cards[3].Label);
                Assert.IsNotEmpty(cards[3].Value);
                Assert.IsNotEmpty(cards[3].Subtext);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void StatusCardDrawingAcceptsNullAndEmptyModels()
        {
            DeucarianBootstrapWindow window = ScriptableObject.CreateInstance<DeucarianBootstrapWindow>();

            try
            {
                Assert.DoesNotThrow(() => window.DrawStatusCard(Rect.zero, null));
                Assert.DoesNotThrow(
                    () => window.DrawStatusCard(
                        Rect.zero,
                        new DeucarianBootstrapWindow.BootstrapStatusCardModel(
                            null,
                            null,
                            null,
                            DeucarianBootstrapWindow.BootstrapStatusKind.Neutral,
                            null)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void HeroPrimaryButtonLabelsFollowSetupState()
        {
            DeucarianBootstrapWindow window = ScriptableObject.CreateInstance<DeucarianBootstrapWindow>();

            try
            {
                SetInstalledPackages(window);
                Assert.AreEqual(DeucarianBootstrapWindow.BootstrapHeroState.NotSetUp, window.GetHeroState());
                Assert.AreEqual("Install Deucarian Setup", window.GetHeroPrimaryActionLabel());
                Assert.False(window.IsHeroPrimaryActionDisabled());
                Assert.AreEqual("Not installed", window.GetPackageInstallerProductStatusText());
                Assert.AreEqual("Setup required", window.GetPackageInstallerProductStatusDetail());

                SetField(window, "_setupActive", true);
                Assert.AreEqual(DeucarianBootstrapWindow.BootstrapHeroState.Installing, window.GetHeroState());
                Assert.AreEqual("Installing...", window.GetHeroPrimaryActionLabel());
                Assert.True(window.IsHeroPrimaryActionDisabled());
                Assert.AreEqual("Installing", window.GetPackageInstallerProductStatusText());

                SetField(window, "_waitingForPackageRefresh", true);
                Assert.AreEqual(DeucarianBootstrapWindow.BootstrapHeroState.WaitingForUnity, window.GetHeroState());
                Assert.AreEqual("Waiting for Unity...", window.GetHeroPrimaryActionLabel());
                Assert.True(window.IsHeroPrimaryActionDisabled());
                Assert.AreEqual("Waiting for Unity", window.GetPackageInstallerProductStatusText());

                SetField(window, "_setupActive", false);
                SetField(window, "_waitingForPackageRefresh", false);
                SetField(window, "_setupInterrupted", true);
                SetField(window, "_error", string.Empty);
                Assert.AreEqual(DeucarianBootstrapWindow.BootstrapHeroState.Interrupted, window.GetHeroState());
                Assert.AreEqual("Repair Package Installer", window.GetHeroPrimaryActionLabel());
                Assert.False(window.IsHeroPrimaryActionDisabled());

                SetField(window, "_error", "Package Manager failed.");
                Assert.AreEqual(DeucarianBootstrapWindow.BootstrapHeroState.NeedsRepair, window.GetHeroState());
                Assert.AreEqual("Repair Package Installer", window.GetHeroPrimaryActionLabel());
                Assert.False(window.IsHeroPrimaryActionDisabled());
                Assert.AreEqual("Missing", window.GetPackageInstallerProductStatusText());

                SetField(window, "_setupInterrupted", false);
                SetField(window, "_error", string.Empty);
                SetInstalledPackages(
                    window,
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
                SetInstalledPackageInfo(
                    window,
                    new BootstrapInstalledPackageInfo(
                        DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                        "1.1.53",
                        "Git",
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl,
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl,
                        CurrentRevision));
                SetField(window, "_targetPackageInstallerRevision", CurrentRevision);
                Assert.AreEqual(DeucarianBootstrapWindow.BootstrapHeroState.Ready, window.GetHeroState());
                Assert.AreEqual("Open Package Installer", window.GetHeroPrimaryActionLabel());
                Assert.False(window.IsHeroPrimaryActionDisabled());
                Assert.AreEqual("Healthy", window.GetPackageInstallerProductStatusText());
                Assert.AreEqual("Package Installer lock revision matches the selected remote Git branch.", window.GetPackageInstallerProductStatusDetail());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void UnknownRemoteRevisionOffersRefreshInsteadOfRepair()
        {
            DeucarianBootstrapWindow window = ScriptableObject.CreateInstance<DeucarianBootstrapWindow>();

            try
            {
                SetInstalledPackages(
                    window,
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
                SetInstalledPackageInfo(
                    window,
                    InstalledPackage(
                        "1.1.61",
                        "Git",
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl,
                        CurrentRevision));
                SetField(window, "_selectedChannel", BootstrapChannel.Stable);
                SetField(window, "_targetPackageInstallerRevision", string.Empty);

                Assert.AreEqual(DeucarianBootstrapWindow.BootstrapHeroState.NeedsRepair, window.GetHeroState());
                Assert.AreEqual("Review required", window.GetPackageInstallerProductStatusText());
                Assert.AreEqual("Refresh Status", window.GetHeroPrimaryActionLabel());
                Assert.False(window.IsHeroPrimaryActionDisabled());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ReloadCleanupDiscardsStalePackageManagerRequests()
        {
            DeucarianBootstrapWindow window = ScriptableObject.CreateInstance<DeucarianBootstrapWindow>();

            try
            {
                SetField(window, "_listRequest", FormatterServices.GetUninitializedObject(typeof(ListRequest)));
                SetField(window, "_addRequest", FormatterServices.GetUninitializedObject(typeof(AddRequest)));
                SetField(window, "_removeRequest", FormatterServices.GetUninitializedObject(typeof(RemoveRequest)));
                SetField(
                    window,
                    "_removeThenAddStep",
                    new BootstrapPackageStep(
                        DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                        DeucarianBootstrapPackageConstants.PackageInstallerPackageDisplayName,
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl));

                window.DiscardTransientPackageManagerRequestsAfterReload();

                Assert.IsNull(GetField(window, "_listRequest"));
                Assert.IsNull(GetField(window, "_addRequest"));
                Assert.IsNull(GetField(window, "_removeRequest"));
                Assert.IsNull(GetField(window, "_removeThenAddStep"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void StartupPreferenceKeyIsProjectScopedAndStable()
        {
            string firstKey = DeucarianBootstrapWindow.GetProjectShowOnStartupPreferenceKey("C:/Projects/First");
            string firstKeyWithSlashes = DeucarianBootstrapWindow.GetProjectShowOnStartupPreferenceKey("C:\\Projects\\First\\");
            string secondKey = DeucarianBootstrapWindow.GetProjectShowOnStartupPreferenceKey("C:/Projects/Second");

            StringAssert.StartsWith("Deucarian.Bootstrap.ShowOnStartup.", firstKey);
            Assert.AreEqual(firstKey, firstKeyWithSlashes);
            Assert.AreNotEqual(firstKey, secondKey);
        }

        [Test]
        public void ChannelPreferenceKeyIsProjectScopedAndStable()
        {
            string firstKey = DeucarianBootstrapWindow.GetProjectChannelPreferenceKey("C:/Projects/First");
            string firstKeyWithSlashes = DeucarianBootstrapWindow.GetProjectChannelPreferenceKey("C:\\Projects\\First\\");
            string secondKey = DeucarianBootstrapWindow.GetProjectChannelPreferenceKey("C:/Projects/Second");

            StringAssert.StartsWith(BootstrapPackageInstallerStateRepository.ProjectChannelPreferencePrefix, firstKey);
            Assert.AreEqual(firstKey, firstKeyWithSlashes);
            Assert.AreNotEqual(firstKey, secondKey);
        }

        [Test]
        public void ChannelPreferenceReadsLegacyBootstrapKeyUntilSharedKeyExists()
        {
            const string projectRoot = "C:/Projects/LegacyBootstrapChannel";

            try
            {
                BootstrapPackageInstallerStateRepository.DeleteProjectChannelForTests(projectRoot);
                string legacyKey = BootstrapPackageInstallerStateRepository.GetLegacyBootstrapChannelPreferenceKeyForTests(projectRoot);
                EditorPrefs.SetInt(legacyKey, (int)BootstrapChannel.Development);

                Assert.AreEqual(
                    BootstrapChannel.Development,
                    BootstrapPackageInstallerStateRepository.GetProjectChannelForTests(projectRoot));

                BootstrapPackageInstallerStateRepository.SetProjectChannelForTests(projectRoot, BootstrapChannel.Stable);

                Assert.AreEqual(
                    BootstrapChannel.Stable,
                    BootstrapPackageInstallerStateRepository.GetProjectChannelForTests(projectRoot));
            }
            finally
            {
                BootstrapPackageInstallerStateRepository.DeleteProjectChannelForTests(projectRoot);
            }
        }

        [Test]
        public void StartupPreferenceCanBeToggledForCurrentProject()
        {
            bool original = DeucarianBootstrapWindow.ShouldShowOnStartup();

            try
            {
                DeucarianBootstrapWindow.SetShowOnStartup(false);
                Assert.False(DeucarianBootstrapWindow.ShouldShowOnStartup());

                DeucarianBootstrapWindow.SetShowOnStartup(true);
                Assert.True(DeucarianBootstrapWindow.ShouldShowOnStartup());
            }
            finally
            {
                DeucarianBootstrapWindow.SetShowOnStartup(original);
            }
        }

        [Test]
        public void SelectedChannelPersistsForCurrentProject()
        {
            BootstrapChannel original = DeucarianBootstrapWindow.GetPersistedChannel();

            try
            {
                DeucarianBootstrapWindow.SetPersistedChannel(BootstrapChannel.Development);
                Assert.AreEqual(BootstrapChannel.Development, DeucarianBootstrapWindow.GetPersistedChannel());

                DeucarianBootstrapWindow.SetPersistedChannel(BootstrapChannel.Stable);
                Assert.AreEqual(BootstrapChannel.Stable, DeucarianBootstrapWindow.GetPersistedChannel());
            }
            finally
            {
                DeucarianBootstrapWindow.SetPersistedChannel(original);
            }
        }

        [Test]
        public void SetupResolvesDependencyFirstPlanFromBundledFallback()
        {
            BootstrapPackageStep[] steps = BuildPlanFromFallbackCatalog(BootstrapChannel.Stable);

            Assert.AreEqual(3, steps.Length);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.EditorPackageId, steps[0].PackageId);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.LoggingPackageId, steps[1].PackageId);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerPackageId, steps[2].PackageId);
        }

        [Test]
        public void LegacyRegistry112RecoveryRunsGitClosureOnceAndPreservesScopedRegistries()
        {
            const string manifestJson =
                "{\"scopedRegistries\":[{\"name\":\"Deucarian\",\"url\":\"https://registry.npmjs.org\",\"scopes\":[\"com.deucarian\"]},{\"name\":\"Company Packages\",\"url\":\"https://packages.example.com\",\"scopes\":[\"com.company\"],\"custom\":\"preserve-me\"}],\"dependencies\":{\"com.deucarian.package-installer\":\"1.1.12\",\"com.company.product\":\"2.0.0\"}}";
            string manifestPath = CreateTempManifest(manifestJson);

            try
            {
                BootstrapScopedRegistryStatus registryStatus = BootstrapScopedRegistryManifest.GetStatus(manifestPath);
                Assert.True(registryStatus.Configured, registryStatus.Detail);

                BootstrapPackageStep[] steps = BuildPlanFromFallbackCatalog(BootstrapChannel.Stable);
                BootstrapInstalledPackageInfo legacyInstaller = InstalledPackage(
                    "1.1.12",
                    "Registry",
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    string.Empty);
                BootstrapPackageInstallerSetupState legacyState = BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    legacyInstaller,
                    string.Empty);
                Assert.AreEqual(BootstrapPackageInstallerSetupState.WrongChannel, legacyState);

                HashSet<string> completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int nextIndex = DeucarianBootstrapWindow.FindNextRepairStepIndex(
                    steps,
                    completed,
                    legacyState,
                    false);
                Assert.AreEqual(0, nextIndex);
                Assert.AreEqual(DeucarianBootstrapPackageConstants.EditorPackageId, steps[nextIndex].PackageId);

                BootstrapInstalledPackageInfo legacyEditor = new BootstrapInstalledPackageInfo(
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    "1.0.0",
                    "Registry",
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    string.Empty,
                    string.Empty);
                Assert.False(DeucarianBootstrapWindow.ShouldRemovePackageInstallerBeforeAdd(
                    steps[nextIndex],
                    legacyEditor,
                    legacyState));
                Assert.False(DeucarianBootstrapWindow.IsInstalledPackageResolvedForStep(
                    legacyEditor,
                    steps[nextIndex]));
                Assert.True(DeucarianBootstrapWindow.IsInstalledPackageResolvedForStep(
                    new BootstrapInstalledPackageInfo(
                        DeucarianBootstrapPackageConstants.EditorPackageId,
                        "1.1.0",
                        "Git",
                        steps[nextIndex].PackageReference,
                        steps[nextIndex].PackageReference,
                        CurrentRevision),
                    steps[nextIndex]));

                completed.Add(steps[nextIndex].PackageId);
                completed = DeucarianBootstrapWindow.DeserializeRepairProgress(
                    DeucarianBootstrapWindow.SerializeRepairProgress(completed));
                nextIndex = DeucarianBootstrapWindow.FindNextRepairStepIndex(
                    steps,
                    completed,
                    legacyState,
                    false);
                Assert.AreEqual(1, nextIndex);
                Assert.AreEqual(DeucarianBootstrapPackageConstants.LoggingPackageId, steps[nextIndex].PackageId);

                BootstrapInstalledPackageInfo legacyLogging = new BootstrapInstalledPackageInfo(
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    "1.0.1",
                    "Registry",
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    string.Empty,
                    string.Empty);
                Assert.False(DeucarianBootstrapWindow.ShouldRemovePackageInstallerBeforeAdd(
                    steps[nextIndex],
                    legacyLogging,
                    legacyState));

                completed.Add(steps[nextIndex].PackageId);
                completed = DeucarianBootstrapWindow.DeserializeRepairProgress(
                    DeucarianBootstrapWindow.SerializeRepairProgress(completed));
                nextIndex = DeucarianBootstrapWindow.FindNextRepairStepIndex(
                    steps,
                    completed,
                    legacyState,
                    false);
                Assert.AreEqual(2, nextIndex);
                Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerPackageId, steps[nextIndex].PackageId);
                Assert.True(DeucarianBootstrapWindow.ShouldRemovePackageInstallerBeforeAdd(
                    steps[nextIndex],
                    legacyInstaller,
                    legacyState));

                completed.Add(steps[nextIndex].PackageId);
                completed = DeucarianBootstrapWindow.DeserializeRepairProgress(
                    DeucarianBootstrapWindow.SerializeRepairProgress(completed));
                BootstrapPackageInstallerSetupState migratedUnverifiedState = BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    InstalledPackage(
                        "1.1.61",
                        "Git",
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl,
                        CurrentRevision),
                    string.Empty);
                Assert.AreEqual(
                    BootstrapPackageInstallerSetupState.UnknownReviewRequired,
                    migratedUnverifiedState);
                Assert.AreEqual(
                    steps.Length,
                    DeucarianBootstrapWindow.FindNextRepairStepIndex(
                        steps,
                        completed,
                        migratedUnverifiedState,
                        false));
                Assert.AreEqual(
                    steps.Length,
                    DeucarianBootstrapWindow.FindNextRepairStepIndex(
                        steps,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        migratedUnverifiedState,
                        false),
                    "An unknown remote SHA must be terminal instead of restarting Client.Add.");

                Assert.AreEqual(manifestJson, File.ReadAllText(manifestPath));
            }
            finally
            {
                DeleteTempManifest(manifestPath);
            }
        }

        [Test]
        public void StableChannelUsesMainGitUrls()
        {
            BootstrapPackageStep[] steps = BuildPlanFromFallbackCatalog(BootstrapChannel.Stable);

            Assert.True(steps.All(step => step.PackageReference.EndsWith("#main", StringComparison.OrdinalIgnoreCase)));
            Assert.AreEqual(DeucarianBootstrapPackageConstants.StableRegistryCatalogUrl, BootstrapChannelUtility.GetRegistryCatalogUrl(BootstrapChannel.Stable));
            Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl, steps.Last().PackageReference);
        }

        [Test]
        public void DevelopmentChannelUsesDevelopGitUrls()
        {
            BootstrapPackageStep[] steps = BuildPlanFromFallbackCatalog(BootstrapChannel.Development);

            Assert.True(steps.All(step => step.PackageReference.EndsWith("#develop", StringComparison.OrdinalIgnoreCase)));
            Assert.AreEqual(DeucarianBootstrapPackageConstants.DevelopmentRegistryCatalogUrl, BootstrapChannelUtility.GetRegistryCatalogUrl(BootstrapChannel.Development));
            Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerDevelopmentGitUrl, steps.Last().PackageReference);
        }

        [Test]
        public void LegacyCatalogDerivesArtifactKindFromLegacyFields()
        {
            BootstrapPackageCatalog catalog = ParseCatalog(
                "{\"schemaVersion\":1,\"packages\":[{\"id\":\"com.deucarian.package-installer\",\"displayName\":\"Installer\",\"category\":\"Tools\",\"stableUrl\":\"https://example.com/installer.git\",\"dependencies\":[]}]}");

            Assert.AreEqual(BootstrapPackageKind.Tool, catalog.packages[0].resolvedKind);
        }

        [Test]
        public void BridgedSchemaV2PrefersCanonicalKindOverLegacyFields()
        {
            BootstrapPackageCatalog catalog = ParseCatalog(
                "{\"schemaVersion\":2,\"groups\":[{\"id\":\"tools-quality\",\"displayName\":\"Tools & Quality\",\"sortOrder\":50}],\"packages\":[{\"id\":\"com.deucarian.package-installer\",\"displayName\":\"Installer\",\"kind\":\"Tool\",\"groupId\":\"tools-quality\",\"category\":\"Core\",\"type\":\"Core\",\"stableUrl\":\"https://example.com/installer.git\",\"dependencies\":[]}]}");

            Assert.AreEqual(BootstrapPackageKind.Tool, catalog.packages[0].resolvedKind);
            Assert.AreEqual("tools-quality", catalog.packages[0].groupId);
        }

        [Test]
        public void SchemaV2AcceptsCanonicalFieldsWithoutLegacyProjection()
        {
            BootstrapPackageCatalog catalog = ParseCatalog(
                "{\"schemaVersion\":2,\"packages\":[{\"id\":\"com.deucarian.package-installer\",\"displayName\":\"Installer\",\"kind\":\"Tool\",\"groupId\":\"tools-quality\",\"stableUrl\":\"https://example.com/installer.git\",\"dependencies\":[]}]}");

            Assert.AreEqual(BootstrapPackageKind.Tool, catalog.packages[0].resolvedKind);
        }

        [Test]
        public void SchemaV2RejectsUnknownArtifactKind()
        {
            bool parsed = BootstrapCatalogParser.TryParse(
                "{\"schemaVersion\":2,\"packages\":[{\"id\":\"com.deucarian.package-installer\",\"kind\":\"Core\",\"groupId\":\"tools-quality\",\"dependencies\":[]}]}",
                out BootstrapPackageCatalog _,
                out string errorMessage);

            Assert.False(parsed);
            StringAssert.Contains("unsupported kind Core", errorMessage);
        }

        [Test]
        public void FallbackCatalogContainsOnlyExactSetupClosureWithoutMovingVersionClaims()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            string fallbackPath = Path.Combine(packageInfo.resolvedPath, DeucarianBootstrapPackageConstants.FallbackCatalogRelativePath);
            string fallbackJson = File.ReadAllText(fallbackPath);
            BootstrapPackageCatalog catalog = ParseCatalog(fallbackJson);

            CollectionAssert.AreEqual(
                new[]
                {
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId
                },
                catalog.packages.Select(package => package.id).ToArray());
            Assert.IsEmpty(catalog.packages[0].dependencies);
            CollectionAssert.AreEqual(
                new[] { DeucarianBootstrapPackageConstants.EditorPackageId },
                catalog.packages[1].dependencies);
            CollectionAssert.AreEqual(
                new[]
                {
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId
                },
                catalog.packages[2].dependencies);
            Assert.False(fallbackJson.Contains("stableVersion"));
            Assert.False(fallbackJson.Contains("developmentVersion"));
            Assert.AreEqual(2, catalog.schemaVersion);
            Assert.True(catalog.groups.Length > 0);
            Assert.True(catalog.packages.All(package => !string.IsNullOrWhiteSpace(package.kind)));
            Assert.True(catalog.packages.All(package => !string.IsNullOrWhiteSpace(package.groupId)));
        }

        [Test]
        public void PlannerDetectsMissingDependencies()
        {
            BootstrapPackageCatalog catalog = ParseCatalog(
                "{\"schemaVersion\":1,\"packages\":[{\"id\":\"com.deucarian.package-installer\",\"displayName\":\"Installer\",\"stableUrl\":\"https://example.com/installer.git\",\"dependencies\":[\"com.deucarian.missing\"]}]}");

            BootstrapInstallPlanResult result = BootstrapInstallPlanner.BuildPlan(
                catalog,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                BootstrapChannel.Stable);

            Assert.False(result.Success);
            StringAssert.Contains("Missing dependency com.deucarian.missing", result.ErrorMessage);
        }

        [Test]
        public void PlannerDetectsCircularDependencies()
        {
            BootstrapPackageCatalog catalog = ParseCatalog(
                "{\"schemaVersion\":1,\"packages\":[{\"id\":\"com.deucarian.package-installer\",\"displayName\":\"Installer\",\"stableUrl\":\"https://example.com/installer.git\",\"dependencies\":[\"com.deucarian.logging\"]},{\"id\":\"com.deucarian.logging\",\"displayName\":\"Logging\",\"stableUrl\":\"https://example.com/logging.git\",\"dependencies\":[\"com.deucarian.package-installer\"]}]}");

            BootstrapInstallPlanResult result = BootstrapInstallPlanner.BuildPlan(
                catalog,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                BootstrapChannel.Stable);

            Assert.False(result.Success);
            StringAssert.Contains("Circular dependency detected", result.ErrorMessage);
        }

        [Test]
        public void PlannerFailsClearlyWhenSelectedChannelUrlIsMissing()
        {
            BootstrapPackageCatalog catalog = ParseCatalog(
                "{\"schemaVersion\":1,\"packages\":[{\"id\":\"com.deucarian.package-installer\",\"displayName\":\"Installer\",\"stableUrl\":\"https://example.com/installer.git\",\"developmentUrl\":\"\",\"dependencies\":[]}]}");

            BootstrapInstallPlanResult result = BootstrapInstallPlanner.BuildPlan(
                catalog,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                BootstrapChannel.Development);

            Assert.False(result.Success);
            StringAssert.Contains("does not define a Development Git URL", result.ErrorMessage);
        }

        [Test]
        public void PackageInstallerStatusUsesGitRevisionsAndRequiresReviewWhenUnknown()
        {
            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.Missing,
                BootstrapPackageInstallerStatus.Evaluate(BootstrapChannel.Stable, null, CurrentRevision));

            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.Healthy,
                BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    InstalledPackage("0.0.1", "Git", DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl, CurrentRevision),
                    CurrentRevision));

            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.Outdated,
                BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    InstalledPackage("99.0.0", "Git", DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl, PreviousRevision),
                    CurrentRevision));

            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.WrongChannel,
                BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    InstalledPackage("1.1.55", "Git", DeucarianBootstrapPackageConstants.PackageInstallerDevelopmentGitUrl, CurrentRevision),
                    CurrentRevision));

            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.WrongChannel,
                BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    InstalledPackage("1.1.53", "Registry", DeucarianBootstrapPackageConstants.PackageInstallerPackageId, string.Empty),
                    CurrentRevision));

            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.UnknownReviewRequired,
                BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    InstalledPackage("1.1.53", "Git", DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl, CurrentRevision),
                    string.Empty));

            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.UnknownReviewRequired,
                BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    InstalledPackage("1.1.53", "Git", DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl, string.Empty),
                    CurrentRevision));
        }

        [Test]
        public void PackageLockInspectorReadsGitUrlAndChannel()
        {
            string lockJson =
                "{\"dependencies\":{\"com.deucarian.package-installer\":{\"version\":\"https://github.com/Deucarian/Package-Installer.git#develop\",\"depth\":0,\"source\":\"git\",\"hash\":\"" + CurrentRevision + "\",\"dependencies\":{}}}}";

            Assert.True(BootstrapPackageLockInspector.TryGetPackage(
                lockJson,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                out BootstrapPackageLockEntry entry));
            Assert.AreEqual("git", entry.Source);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerDevelopmentGitUrl, entry.GitUrl);
            Assert.AreEqual(CurrentRevision, entry.RevisionHash);
            Assert.True(BootstrapChannelUtility.TryDetectFromGitReference(entry.GitUrl, out BootstrapChannel channel));
            Assert.AreEqual(BootstrapChannel.Development, channel);
        }

        [Test]
        public void ScopedRegistryInspectionDoesNotAddMissingLegacyConfiguration()
        {
            const string manifestJson = "{\"dependencies\":{\"com.unity.textmeshpro\":\"3.0.6\"}}";
            string manifestPath = CreateTempManifest(manifestJson);

            try
            {
                BootstrapScopedRegistryStatus status = BootstrapScopedRegistryManifest.GetStatus(manifestPath);
                Assert.False(status.Configured);
                Assert.True(status.NeedsRepair);
                Assert.AreEqual(manifestJson, File.ReadAllText(manifestPath));
            }
            finally
            {
                DeleteTempManifest(manifestPath);
            }
        }

        [Test]
        public void ScopedRegistryInspectionDetectsValidLegacyConfigurationWithoutChangingIt()
        {
            const string manifestJson =
                "{\"scopedRegistries\":[{\"name\":\"Deucarian\",\"url\":\"https://registry.npmjs.org\",\"scopes\":[\"com.deucarian\"]}],\"dependencies\":{}}";
            string manifestPath = CreateTempManifest(manifestJson);

            try
            {
                BootstrapScopedRegistryStatus status = BootstrapScopedRegistryManifest.GetStatus(manifestPath);
                Assert.True(status.Configured, status.Detail);
                Assert.False(status.NeedsRepair);
                Assert.AreEqual(manifestJson, File.ReadAllText(manifestPath));
            }
            finally
            {
                DeleteTempManifest(manifestPath);
            }
        }

        private static BootstrapPackageCatalog ParseCatalog(string json)
        {
            Assert.True(BootstrapCatalogParser.TryParse(json, out BootstrapPackageCatalog catalog, out string errorMessage), errorMessage);
            return catalog;
        }

        private static BootstrapPackageStep[] BuildPlanFromFallbackCatalog(BootstrapChannel channel)
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            string fallbackPath = Path.Combine(packageInfo.resolvedPath, DeucarianBootstrapPackageConstants.FallbackCatalogRelativePath);
            BootstrapPackageCatalog catalog = ParseCatalog(File.ReadAllText(fallbackPath));
            BootstrapInstallPlanResult result = BootstrapInstallPlanner.BuildPlan(
                catalog,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                channel);

            Assert.True(result.Success, result.ErrorMessage);
            return result.Steps.ToArray();
        }

        private static string CreateTempManifest(string json)
        {
            string directory = Path.Combine(Path.GetTempPath(), "DeucarianBootstrapTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string manifestPath = Path.Combine(directory, "manifest.json");
            File.WriteAllText(manifestPath, json);
            return manifestPath;
        }

        private static void DeleteTempManifest(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private static string ReadMetaGuid(string metaPath)
        {
            foreach (string line in File.ReadAllLines(metaPath))
            {
                if (line.StartsWith("guid:", StringComparison.Ordinal))
                {
                    return line.Substring("guid:".Length).Trim();
                }
            }

            return string.Empty;
        }

        private static void SetInstalledPackages(DeucarianBootstrapWindow window, params string[] packageIds)
        {
            SetField(
                window,
                "_installedPackageIds",
                new HashSet<string>(packageIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase));
        }

        private static BootstrapInstalledPackageInfo InstalledPackage(
            string version,
            string source,
            string reference,
            string lockRevision)
        {
            return new BootstrapInstalledPackageInfo(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                version,
                source,
                reference,
                reference,
                lockRevision);
        }

        private static void SetInstalledPackageInfo(DeucarianBootstrapWindow window, params BootstrapInstalledPackageInfo[] packages)
        {
            Dictionary<string, BootstrapInstalledPackageInfo> packagesById =
                new Dictionary<string, BootstrapInstalledPackageInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (BootstrapInstalledPackageInfo package in packages ?? Array.Empty<BootstrapInstalledPackageInfo>())
            {
                packagesById[package.PackageId] = package;
            }

            SetField(window, "_installedPackagesById", packagesById);
        }

        private static void SetField(DeucarianBootstrapWindow window, string fieldName, object value)
        {
            FieldInfo field = typeof(DeucarianBootstrapWindow).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, fieldName);
            field.SetValue(window, value);
        }

        private static object GetField(DeucarianBootstrapWindow window, string fieldName)
        {
            FieldInfo field = typeof(DeucarianBootstrapWindow).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, fieldName);
            return field.GetValue(window);
        }
    }
}
