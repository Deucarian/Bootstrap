using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

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

            Assert.True(File.Exists(logoPath), logoPath);
            Assert.True(File.Exists(backgroundPath), backgroundPath);
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
            StringAssert.Contains("github.com/Deucarian/Bootstrap", DeucarianBootstrapPackageConstants.GitHubUrl);
            StringAssert.Contains("github.com/Deucarian/Bootstrap", DeucarianBootstrapPackageConstants.DocumentationUrl);
        }

        [Test]
        public void BootstrapHeroCopyUsesFunctionalWording()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            string windowSourcePath = Path.Combine(packageInfo.resolvedPath, "Editor", "DeucarianBootstrapWindow.cs");
            string windowSource = File.ReadAllText(windowSourcePath);

            StringAssert.Contains("Install and manage Deucarian Unity packages.", windowSource);
            StringAssert.Contains("\"Refresh\"", windowSource);
            StringAssert.Contains("\"Repair Registry\"", windowSource);
            StringAssert.Contains("\"GitHub\"", windowSource);
            StringAssert.Contains("\"Docs\"", windowSource);
            StringAssert.Contains("Install source: npm scoped registry", windowSource);
            StringAssert.Contains("Setup progress", windowSource);
            StringAssert.Contains("Setup looks healthy.", windowSource);
            StringAssert.Contains("Show Bootstrap on startup", windowSource);
            StringAssert.Contains("Registry source, package IDs, install plan, and diagnostics are available here when needed.", windowSource);
            StringAssert.Contains("Bootstrap only sets up and repairs the Deucarian package ecosystem.", windowSource);
            StringAssert.Contains("Recommended. Uses npmjs scoped registry and lets Unity resolve dependencies.", windowSource);
            StringAssert.Contains("Advanced fallback mode for development or registry outages.", windowSource);

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
            Assert.GreaterOrEqual(DeucarianBootstrapWindow.PreferredWindowWidth, 760f);
            Assert.GreaterOrEqual(DeucarianBootstrapWindow.PreferredWindowHeight, 860f);
            Assert.GreaterOrEqual(
                DeucarianBootstrapWindow.HeroCardHeight / DeucarianBootstrapWindow.PreferredWindowHeight,
                0.60f);
            Assert.LessOrEqual(
                DeucarianBootstrapWindow.HeroCardHeight / DeucarianBootstrapWindow.PreferredWindowHeight,
                0.70f);
            Assert.GreaterOrEqual(DeucarianBootstrapWindow.MinWindowWidth, 740f);
            Assert.GreaterOrEqual(DeucarianBootstrapWindow.MinWindowHeight, 720f);
            Assert.GreaterOrEqual(DeucarianBootstrapWindow.PreferredWindowWidth, DeucarianBootstrapWindow.MinWindowWidth);
            Assert.GreaterOrEqual(DeucarianBootstrapWindow.PreferredWindowHeight, DeucarianBootstrapWindow.MinWindowHeight);
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
                Assert.AreEqual("Continue Setup", window.GetHeroPrimaryActionLabel());
                Assert.False(window.IsHeroPrimaryActionDisabled());

                SetField(window, "_error", "Package Manager failed.");
                Assert.AreEqual(DeucarianBootstrapWindow.BootstrapHeroState.NeedsRepair, window.GetHeroState());
                Assert.AreEqual("Repair Setup", window.GetHeroPrimaryActionLabel());
                Assert.False(window.IsHeroPrimaryActionDisabled());
                Assert.AreEqual("Setup needs repair", window.GetPackageInstallerProductStatusText());

                SetField(window, "_setupInterrupted", false);
                SetField(window, "_error", string.Empty);
                SetInstalledPackages(
                    window,
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
                SetField(
                    window,
                    "_scopedRegistryStatus",
                    BootstrapScopedRegistryStatus.CreateConfigured("Packages/manifest.json", DeucarianBootstrapPackageConstants.ScopedRegistryUrl));
                Assert.AreEqual(DeucarianBootstrapWindow.BootstrapHeroState.Ready, window.GetHeroState());
                Assert.AreEqual("Open Package Installer", window.GetHeroPrimaryActionLabel());
                Assert.False(window.IsHeroPrimaryActionDisabled());
                Assert.AreEqual("Ready", window.GetPackageInstallerProductStatusText());
                Assert.AreEqual("Installed and available", window.GetPackageInstallerProductStatusDetail());
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
        public void SetupResolvesDependencyFirstPlanFromBundledFallback()
        {
            BootstrapPackageStep[] steps = BuildPlanFromFallbackCatalog();

            Assert.AreEqual(3, steps.Length);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.EditorPackageId, steps[0].PackageId);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.LoggingPackageId, steps[1].PackageId);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerPackageId, steps[2].PackageId);
        }

        [Test]
        public void ContinuationSkipsInstalledPackages()
        {
            BootstrapPackageStep[] steps = BuildPlanFromFallbackCatalog();
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
            BootstrapPackageStep[] steps = BuildPlanFromFallbackCatalog();
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
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);

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
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);

            Assert.False(result.Success);
            StringAssert.Contains("Circular dependency detected", result.ErrorMessage);
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

        private static BootstrapPackageStep[] BuildPlanFromFallbackCatalog()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(DeucarianBootstrapWindow).Assembly);
            string fallbackPath = Path.Combine(packageInfo.resolvedPath, DeucarianBootstrapPackageConstants.FallbackCatalogRelativePath);
            BootstrapPackageCatalog catalog = ParseCatalog(File.ReadAllText(fallbackPath));
            BootstrapInstallPlanResult result = BootstrapInstallPlanner.BuildPlan(
                catalog,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);

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

        private static void SetInstalledPackages(DeucarianBootstrapWindow window, params string[] packageIds)
        {
            SetField(
                window,
                "_installedPackageIds",
                new HashSet<string>(packageIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase));
        }

        private static void SetField(DeucarianBootstrapWindow window, string fieldName, object value)
        {
            FieldInfo field = typeof(DeucarianBootstrapWindow).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, fieldName);
            field.SetValue(window, value);
        }
    }
}
