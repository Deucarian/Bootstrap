using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Deucarian.Bootstrap.Editor.Tests
{
    public sealed class DeucarianBootstrapTests
    {
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
            StringAssert.Contains("Package Installer is installed and matches the selected channel.", windowSource);
            StringAssert.Contains("Show Bootstrap on startup", windowSource);
            StringAssert.Contains("Full Git URLs, install plan, status log, and deferred scoped-registry diagnostics are available here.", windowSource);
            StringAssert.Contains("Stable: Git #main", windowSource);
            StringAssert.Contains("Development: Git #develop", windowSource);
            StringAssert.Contains("npm/scoped registry deferred", windowSource);
            StringAssert.Contains("Deferred. Git URLs are the supported distribution path for now.", windowSource);
            StringAssert.Contains("DrawStatusCard", windowSource);
            StringAssert.Contains("GUILayout.Width(320f)", windowSource);
            Assert.False(windowSource.Contains("Recommended. Uses npmjs scoped registry"));
            Assert.False(windowSource.Contains("\"Repair Registry\""));

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
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl));
                SetField(window, "_catalogLoaded", true);
                SetField(window, "_registrySource", "Remote: " + DeucarianBootstrapPackageConstants.StableRegistryCatalogUrl);
                SetField(window, "_targetPackageInstallerVersion", "1.1.58");

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
                        DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl));
                Assert.AreEqual(DeucarianBootstrapWindow.BootstrapHeroState.Ready, window.GetHeroState());
                Assert.AreEqual("Open Package Installer", window.GetHeroPrimaryActionLabel());
                Assert.False(window.IsHeroPrimaryActionDisabled());
                Assert.AreEqual("Healthy", window.GetPackageInstallerProductStatusText());
                Assert.AreEqual("Package Installer is installed and matches the selected channel.", window.GetPackageInstallerProductStatusDetail());
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
        public void FallbackCatalogIncludesCommonAndRuntimeConsumerDependencies()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            string fallbackPath = Path.Combine(packageInfo.resolvedPath, DeucarianBootstrapPackageConstants.FallbackCatalogRelativePath);
            BootstrapPackageCatalog catalog = ParseCatalog(File.ReadAllText(fallbackPath));

            BootstrapPackageDefinition common = catalog.packages.Single(package => package.id == "com.deucarian.common");
            Assert.AreEqual("Deucarian Common", common.displayName);
            Assert.AreEqual("Core", common.category);
            Assert.IsEmpty(common.dependencies);

            BootstrapPackageDefinition objectLoading = catalog.packages.Single(package => package.id == "com.deucarian.object-loading");
            BootstrapPackageDefinition uiBinding = catalog.packages.Single(package => package.id == "com.deucarian.ui-binding");
            BootstrapPackageDefinition uiFlow = catalog.packages.Single(package => package.id == "com.deucarian.ui-flow");

            CollectionAssert.Contains(objectLoading.dependencies, "com.deucarian.common");
            CollectionAssert.Contains(uiBinding.dependencies, "com.deucarian.common");
            CollectionAssert.Contains(uiFlow.dependencies, "com.deucarian.common");
            CollectionAssert.Contains(uiFlow.dependencies, "com.deucarian.logging");
        }

        [Test]
        public void ContinuationSkipsInstalledPackages()
        {
            BootstrapPackageStep[] steps = BuildPlanFromFallbackCatalog(BootstrapChannel.Stable);
            HashSet<string> installed = new HashSet<string>
            {
                DeucarianBootstrapPackageConstants.EditorPackageId
            };

            int nextIndex = DeucarianBootstrapWindow.FindNextMissingStepIndex(steps, installed);

            Assert.AreEqual(1, nextIndex);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.LoggingPackageId, steps[nextIndex].PackageId);

            installed.Add(DeucarianBootstrapPackageConstants.LoggingPackageId);
            nextIndex = DeucarianBootstrapWindow.FindNextMissingStepIndex(steps, installed);

            Assert.AreEqual(2, nextIndex);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerPackageId, steps[nextIndex].PackageId);
        }

        [Test]
        public void ContinuationReportsCompleteWhenAllPackagesInstalled()
        {
            BootstrapPackageStep[] steps = BuildPlanFromFallbackCatalog(BootstrapChannel.Stable);
            HashSet<string> installed = new HashSet<string>(steps.Select(step => step.PackageId));

            int nextIndex = DeucarianBootstrapWindow.FindNextMissingStepIndex(steps, installed);

            Assert.AreEqual(steps.Length, nextIndex);
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
        public void PackageInstallerStatusDetectsMissingOutdatedWrongChannelAndHealthy()
        {
            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.Missing,
                BootstrapPackageInstallerStatus.Evaluate(BootstrapChannel.Stable, null, "1.1.53"));

            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.Healthy,
                BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    InstalledPackage("1.1.53", "Git", DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl),
                    "1.1.53"));

            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.Outdated,
                BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    InstalledPackage("1.1.52", "Git", DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl),
                    "1.1.53"));

            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.WrongChannel,
                BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    InstalledPackage("1.1.55", "Git", DeucarianBootstrapPackageConstants.PackageInstallerDevelopmentGitUrl),
                    "1.1.53"));

            Assert.AreEqual(
                BootstrapPackageInstallerSetupState.WrongChannel,
                BootstrapPackageInstallerStatus.Evaluate(
                    BootstrapChannel.Stable,
                    InstalledPackage("1.1.53", "Registry", DeucarianBootstrapPackageConstants.PackageInstallerPackageId),
                    "1.1.53"));
        }

        [Test]
        public void PackageLockInspectorReadsGitUrlAndChannel()
        {
            string lockJson =
                "{\"dependencies\":{\"com.deucarian.package-installer\":{\"version\":\"https://github.com/Deucarian/Package-Installer.git#develop\",\"depth\":0,\"source\":\"git\",\"dependencies\":{}}}}";

            Assert.True(BootstrapPackageLockInspector.TryGetPackage(
                lockJson,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                out BootstrapPackageLockEntry entry));
            Assert.AreEqual("git", entry.Source);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerDevelopmentGitUrl, entry.GitUrl);
            Assert.True(BootstrapChannelUtility.TryDetectFromGitReference(entry.GitUrl, out BootstrapChannel channel));
            Assert.AreEqual(BootstrapChannel.Development, channel);
        }

        [Test]
        public void ScopedRegistryRepairAddsNpmjsRegistryToManifest()
        {
            string manifestPath = CreateTempManifest(
                "{\"dependencies\":{\"com.unity.textmeshpro\":\"3.0.6\"}}");

            try
            {
                BootstrapScopedRegistryStatus status = BootstrapScopedRegistryManifest.GetStatus(manifestPath);
                Assert.False(status.Configured);
                Assert.True(status.NeedsRepair);

                BootstrapScopedRegistryRepairResult result =
                    BootstrapScopedRegistryManifest.EnsureConfigured(manifestPath);

                Assert.True(result.Success, result.ErrorMessage);
                Assert.True(result.Changed);

                string manifest = File.ReadAllText(manifestPath);
                StringAssert.Contains("\"scopedRegistries\"", manifest);
                StringAssert.Contains("\"name\": \"Deucarian\"", manifest);
                StringAssert.Contains("\"url\": \"https://registry.npmjs.org\"", manifest);
                StringAssert.Contains("\"com.deucarian\"", manifest);
                StringAssert.Contains("\"com.unity.textmeshpro\": \"3.0.6\"", manifest);

                BootstrapScopedRegistryRepairResult secondResult =
                    BootstrapScopedRegistryManifest.EnsureConfigured(manifestPath);

                Assert.True(secondResult.Success, secondResult.ErrorMessage);
                Assert.False(secondResult.Changed);
            }
            finally
            {
                DeleteTempManifest(manifestPath);
            }
        }

        [Test]
        public void ScopedRegistryRepairFixesWrongDeucarianRegistry()
        {
            string manifestPath = CreateTempManifest(
                "{\"scopedRegistries\":[{\"name\":\"Old Deucarian\",\"url\":\"https://example.com\",\"scopes\":[\"com.deucarian\"]},{\"name\":\"Other\",\"url\":\"https://example.org\",\"scopes\":[\"com.deucarian\",\"com.example\"]}],\"dependencies\":{}}");

            try
            {
                BootstrapScopedRegistryRepairResult result =
                    BootstrapScopedRegistryManifest.EnsureConfigured(manifestPath);

                Assert.True(result.Success, result.ErrorMessage);
                Assert.True(result.Changed);

                string manifest = File.ReadAllText(manifestPath);
                StringAssert.Contains("\"name\": \"Deucarian\"", manifest);
                StringAssert.Contains("\"url\": \"https://registry.npmjs.org\"", manifest);
                Assert.AreEqual(1, CountOccurrences(manifest, "\"com.deucarian\""));

                BootstrapScopedRegistryStatus status = BootstrapScopedRegistryManifest.GetStatus(manifestPath);
                Assert.True(status.Configured, status.Detail);
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

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
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

        private static BootstrapInstalledPackageInfo InstalledPackage(string version, string source, string reference)
        {
            return new BootstrapInstalledPackageInfo(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                version,
                source,
                reference,
                reference);
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
    }
}
