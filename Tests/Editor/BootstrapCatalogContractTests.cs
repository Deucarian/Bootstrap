using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Deucarian.Bootstrap.Editor.Tests
{
    [TestFixture]
    internal sealed class BootstrapCatalogContractTests
    {
        [Test]
        public void BundledFallback_ContainsSetupClosureAndViewerReviewEntriesForBothChannels()
        {
            string json = ReadBundledFallback();

            Assert.That(
                BootstrapCatalogParser.TryParse(
                    json,
                    out BootstrapPackageCatalog catalog,
                    out string parseError),
                Is.True,
                parseError);
            Assert.That(catalog.schemaVersion, Is.EqualTo(2));
            string[] packageIds = catalog.packages.Select(package => package.id).ToArray();
            Assert.That(packageIds, Does.Contain("com.deucarian.activity-visualization"));
            Assert.That(packageIds, Does.Contain("com.deucarian.command-routing.webgl-integration"));
            Assert.That(packageIds, Does.Contain("com.deucarian.viewer-navigation"));
            Assert.That(packageIds, Does.Contain("com.deucarian.web-viewer-suite"));
            Assert.That(packageIds, Does.Contain("com.deucarian.template.viewer.web"));
            Assert.That(packageIds, Does.Contain(DeucarianBootstrapPackageConstants.EditorPackageId));
            Assert.That(packageIds, Does.Contain(DeucarianBootstrapPackageConstants.LoggingPackageId));
            Assert.That(packageIds, Does.Contain(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId));

            Assert.That(catalog.packages.Where(package => new[]
            {
                DeucarianBootstrapPackageConstants.EditorPackageId,
                DeucarianBootstrapPackageConstants.LoggingPackageId,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId
            }.Contains(package.id)).Select(package => package.id), Is.EquivalentTo(new[]
            {
                DeucarianBootstrapPackageConstants.EditorPackageId,
                DeucarianBootstrapPackageConstants.LoggingPackageId,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId
            }));

            BootstrapInstallPlanResult stable = BootstrapSetupPlanner.Build(
                catalog,
                BootstrapChannel.Stable);
            BootstrapInstallPlanResult development = BootstrapSetupPlanner.Build(
                catalog,
                BootstrapChannel.Development);

            AssertAll(() =>
            {
                Assert.That(stable.Success, Is.True, stable.ErrorMessage);
                Assert.That(development.Success, Is.True, development.ErrorMessage);
                Assert.That(stable.Steps.Select(step => step.PackageId), Is.EqualTo(ExpectedOrder()));
                Assert.That(development.Steps.Select(step => step.PackageId), Is.EqualTo(ExpectedOrder()));
                Assert.That(stable.Steps.All(step => step.PackageReference.EndsWith("#main")),
                    Is.True);
                Assert.That(development.Steps.All(step => step.PackageReference.EndsWith("#develop")),
                    Is.True);
                Assert.That(stable.Steps[2].PackageReference,
                    Is.EqualTo(DeucarianBootstrapPackageConstants.PackageInstallerStableGitUrl));
                Assert.That(development.Steps[2].PackageReference,
                    Is.EqualTo(DeucarianBootstrapPackageConstants.PackageInstallerDevelopmentGitUrl));
            });
        }

        [Test]
        public void SetupPlanner_FailsWhenDependencyEntryIsMissing()
        {
            BootstrapPackageCatalog catalog = Catalog(
                Package(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId));

            BootstrapInstallPlanResult result = BootstrapSetupPlanner.Build(
                catalog,
                BootstrapChannel.Stable);

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("Missing dependency " +
                DeucarianBootstrapPackageConstants.LoggingPackageId, result.ErrorMessage);
        }

        [Test]
        public void SetupPlanner_FailsWithReadableCyclePath()
        {
            const string packageA = "com.deucarian.a";
            BootstrapPackageCatalog catalog = Catalog(
                Package(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    packageA),
                Package(packageA, DeucarianBootstrapPackageConstants.PackageInstallerPackageId));

            BootstrapInstallPlanResult result = BootstrapSetupPlanner.Build(
                catalog,
                BootstrapChannel.Development);

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("Circular dependency detected", result.ErrorMessage);
            StringAssert.Contains(DeucarianBootstrapPackageConstants.PackageInstallerPackageId, result.ErrorMessage);
            StringAssert.Contains(packageA, result.ErrorMessage);
        }

        [Test]
        public void SetupPlanner_FailsWhenClosureContainsAnUnownedFourthPackage()
        {
            const string unrelated = "com.deucarian.unrelated";
            BootstrapPackageCatalog catalog = Catalog(
                Package(DeucarianBootstrapPackageConstants.EditorPackageId),
                Package(
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    unrelated),
                Package(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId),
                Package(unrelated));

            BootstrapInstallPlanResult result = BootstrapSetupPlanner.Build(
                catalog,
                BootstrapChannel.Stable);

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("exactly Editor, Logging, and Package Installer", result.ErrorMessage);
        }

        [Test]
        public void InstallPlanner_UsesDependencyFirstOrderAndSelectedChannelUrls()
        {
            BootstrapPackageCatalog catalog = Catalog(
                Package(DeucarianBootstrapPackageConstants.EditorPackageId),
                Package(
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.EditorPackageId),
                Package(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId));

            BootstrapInstallPlanResult stable = BootstrapInstallPlanner.BuildPlan(
                catalog,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                BootstrapChannel.Stable);
            BootstrapInstallPlanResult development = BootstrapInstallPlanner.BuildPlan(
                catalog,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                BootstrapChannel.Development);

            AssertAll(() =>
            {
                Assert.That(stable.Success, Is.True, stable.ErrorMessage);
                Assert.That(stable.Steps.Select(step => step.PackageId), Is.EqualTo(ExpectedOrder()));
                Assert.That(stable.Steps.All(step => step.PackageReference.EndsWith("#main")), Is.True);
                Assert.That(development.Success, Is.True, development.ErrorMessage);
                Assert.That(development.Steps.Select(step => step.PackageId), Is.EqualTo(ExpectedOrder()));
                Assert.That(development.Steps.All(step => step.PackageReference.EndsWith("#develop")), Is.True);
            });
        }

        [Test]
        public void InstallPlanner_FailsWhenSelectedChannelUrlIsMissing()
        {
            BootstrapPackageDefinition target = Package(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId);
            target.developmentUrl = string.Empty;

            BootstrapInstallPlanResult result = BootstrapInstallPlanner.BuildPlan(
                Catalog(target),
                target.id,
                BootstrapChannel.Development);

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("does not define a Development Git URL", result.ErrorMessage);
        }

        [Test]
        public void SetupPlannerRejectsASelectedChannelUrlThatTargetsTheOtherBranch()
        {
            BootstrapPackageDefinition editor = Package(
                DeucarianBootstrapPackageConstants.EditorPackageId);
            editor.stableUrl = editor.developmentUrl;
            BootstrapPackageCatalog catalog = Catalog(
                editor,
                Package(
                    DeucarianBootstrapPackageConstants.LoggingPackageId,
                    DeucarianBootstrapPackageConstants.EditorPackageId),
                Package(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    DeucarianBootstrapPackageConstants.EditorPackageId,
                    DeucarianBootstrapPackageConstants.LoggingPackageId));

            BootstrapInstallPlanResult result = BootstrapSetupPlanner.Build(
                catalog,
                BootstrapChannel.Stable);

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("must target Git #main", result.ErrorMessage);
        }

        [Test]
        public void CatalogLoader_RemoteUnavailableRetainsValidatedFallback()
        {
            FakeFallbackCatalogSource fallback = new FakeFallbackCatalogSource(ReadBundledFallback());
            FakeRemoteTextRequest remote = FakeRemoteTextRequest.Failed("offline");
            using (BootstrapCatalogLoader loader = new BootstrapCatalogLoader(
                       fallback,
                       new FakeRemoteTextRequestFactory(remote)))
            {
                loader.Begin(BootstrapChannel.Stable);
                loader.Tick();

                AssertAll(() =>
                {
                    Assert.That(loader.IsCompleted, Is.True);
                    Assert.That(loader.Selection.Success, Is.True, loader.Selection.ErrorMessage);
                    Assert.That(loader.Selection.Origin, Is.EqualTo(BootstrapCatalogOrigin.BundledFallback));
                    Assert.That(loader.Selection.Source, Is.EqualTo("Bundled setup fallback"));
                    StringAssert.Contains("unavailable", loader.Selection.Notice.ToLowerInvariant());
                    StringAssert.Contains("validated bundled setup fallback", loader.Selection.Notice);
                    Assert.That(loader.Selection.Plan.Steps.Select(step => step.PackageId),
                        Is.EqualTo(ExpectedOrder()));
                });
            }

            Assert.That(remote.Disposed, Is.True);
        }

        [Test]
        public void CatalogLoader_InvalidRemoteDoesNotReplaceValidatedFallback()
        {
            FakeFallbackCatalogSource fallback = new FakeFallbackCatalogSource(ReadBundledFallback());
            FakeRemoteTextRequest remote = FakeRemoteTextRequest.CreateSuccess(
                "{\"schemaVersion\":99,\"packages\":[]}");
            using (BootstrapCatalogLoader loader = new BootstrapCatalogLoader(
                       fallback,
                       new FakeRemoteTextRequestFactory(remote)))
            {
                loader.Begin(BootstrapChannel.Development);
                loader.Tick();

                AssertAll(() =>
                {
                    Assert.That(loader.IsCompleted, Is.True);
                    Assert.That(loader.Selection.Success, Is.True, loader.Selection.ErrorMessage);
                    Assert.That(loader.Selection.Origin, Is.EqualTo(BootstrapCatalogOrigin.BundledFallback));
                    StringAssert.Contains("Remote Package Registry was invalid", loader.Selection.Notice);
                    Assert.That(loader.Selection.Plan.Steps.Select(step => step.PackageId),
                        Is.EqualTo(ExpectedOrder()));
                    Assert.That(loader.Selection.Plan.Steps.All(
                        step => step.PackageReference.EndsWith("#develop")), Is.True);
                });
            }

            Assert.That(remote.Disposed, Is.True);
        }

        [Test]
        public void CatalogLoader_WrongBranchRemoteDoesNotReplaceValidatedFallback()
        {
            string fallbackJson = ReadBundledFallback();
            string wrongBranchRemote = fallbackJson.Replace(
                "Editor.git#develop",
                "Editor.git#main");
            FakeRemoteTextRequest remote = FakeRemoteTextRequest.CreateSuccess(wrongBranchRemote);

            using (BootstrapCatalogLoader loader = new BootstrapCatalogLoader(
                       new FakeFallbackCatalogSource(fallbackJson),
                       new FakeRemoteTextRequestFactory(remote)))
            {
                loader.Begin(BootstrapChannel.Development);
                loader.Tick();

                Assert.That(loader.Selection.Success, Is.True, loader.Selection.ErrorMessage);
                Assert.That(loader.Selection.Origin, Is.EqualTo(BootstrapCatalogOrigin.BundledFallback));
                StringAssert.Contains("Remote Package Registry was invalid", loader.Selection.Notice);
                Assert.That(loader.Selection.Plan.Steps.All(
                    step => step.PackageReference.EndsWith("#develop")), Is.True);
            }
        }

        private static string[] ExpectedOrder()
        {
            return new[]
            {
                DeucarianBootstrapPackageConstants.EditorPackageId,
                DeucarianBootstrapPackageConstants.LoggingPackageId,
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId
            };
        }

        private static BootstrapPackageCatalog Catalog(params BootstrapPackageDefinition[] packages)
        {
            return new BootstrapPackageCatalog
            {
                schemaVersion = 2,
                packages = packages ?? Array.Empty<BootstrapPackageDefinition>(),
                groups = Array.Empty<BootstrapPackageGroup>()
            };
        }

        private static BootstrapPackageDefinition Package(string id, params string[] dependencies)
        {
            string repositoryName = id.Substring(id.LastIndexOf('.') + 1);
            return new BootstrapPackageDefinition
            {
                id = id,
                displayName = id,
                kind = "Tool",
                groupId = "test",
                stableUrl = "https://github.com/Deucarian/" + repositoryName + ".git#main",
                developmentUrl = "https://github.com/Deucarian/" + repositoryName + ".git#develop",
                dependencies = dependencies ?? Array.Empty<string>()
            };
        }

        private static string ReadBundledFallback()
        {
            PackageInfo package = PackageInfo.FindForAssembly(
                typeof(DeucarianBootstrapPackageConstants).Assembly);
            Assert.That(package, Is.Not.Null);
            return File.ReadAllText(Path.Combine(
                package.resolvedPath,
                DeucarianBootstrapPackageConstants.FallbackCatalogRelativePath
                    .Replace('/', Path.DirectorySeparatorChar)));
        }

        private sealed class FakeFallbackCatalogSource : IBootstrapFallbackCatalogSource
        {
            private readonly string _json;

            public FakeFallbackCatalogSource(string json)
            {
                _json = json;
            }

            public bool TryRead(out string json, out string errorMessage)
            {
                json = _json;
                errorMessage = string.Empty;
                return true;
            }
        }

        private sealed class FakeRemoteTextRequestFactory : IBootstrapRemoteTextRequestFactory
        {
            private readonly IBootstrapRemoteTextRequest _request;

            public FakeRemoteTextRequestFactory(IBootstrapRemoteTextRequest request)
            {
                _request = request;
            }

            public IBootstrapRemoteTextRequest Start(string url, int timeoutSeconds)
            {
                Assert.That(url, Does.StartWith("https://raw.githubusercontent.com/Deucarian/Package-Registry/"));
                Assert.That(timeoutSeconds, Is.GreaterThan(0));
                return _request;
            }
        }

        private sealed class FakeRemoteTextRequest : IBootstrapRemoteTextRequest
        {
            private FakeRemoteTextRequest(bool succeeded, string text, string errorMessage)
            {
                Succeeded = succeeded;
                Text = text;
                ErrorMessage = errorMessage;
            }

            public bool IsCompleted => true;

            public bool Succeeded { get; }

            public string Text { get; }

            public string ErrorMessage { get; }

            public bool Disposed { get; private set; }

            public static FakeRemoteTextRequest CreateSuccess(string text)
            {
                return new FakeRemoteTextRequest(true, text, string.Empty);
            }

            public static FakeRemoteTextRequest Failed(string errorMessage)
            {
                return new FakeRemoteTextRequest(false, string.Empty, errorMessage);
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }
}
