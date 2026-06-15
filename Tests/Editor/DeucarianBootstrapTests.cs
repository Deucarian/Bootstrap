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
        public void SetupInstallsEditorBeforePackageInstaller()
        {
            BootstrapPackageStep[] steps = DeucarianBootstrapWindow.Steps.ToArray();

            Assert.AreEqual(2, steps.Length);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.EditorPackageId, steps[0].PackageId);
            Assert.AreEqual(DeucarianBootstrapPackageConstants.PackageInstallerPackageId, steps[1].PackageId);
        }
    }
}
