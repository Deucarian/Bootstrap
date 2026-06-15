using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

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
        public void SetupResolvesDependencyFirstPlanFromBundledFallback()
        {
            BootstrapPackageStep[] steps = BuildPlanFromFallbackCatalog();

            Assert.AreEqual(3, steps.Length);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.EditorPackageId, steps[0].PackageId);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.LoggingPackageId, steps[1].PackageId);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerPackageId, steps[2].PackageId);
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
    }
}
