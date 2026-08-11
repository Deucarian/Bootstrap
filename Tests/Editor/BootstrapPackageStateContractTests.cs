using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace Deucarian.Bootstrap.Editor.Tests
{
    [TestFixture]
    internal sealed class BootstrapPackageInstallerHealthContractTests
    {
        private const string StableTarget =
            "https://github.com/Deucarian/Package-Installer.git#main";
        private const string DevelopmentTarget =
            "https://github.com/Deucarian/Package-Installer.git#develop";
        private const string CurrentRevision = "0123456789abcdef";

        [Test]
        public void HealthEvaluation_DistinguishesAllSourceChannelAndRevisionStates()
        {
            AssertAll(() =>
            {
                Assert.That(
                    BootstrapPackageInstallerStatus.Evaluate(
                        BootstrapChannel.Stable,
                        null,
                        StableTarget,
                        CurrentRevision),
                    Is.EqualTo(BootstrapPackageInstallerSetupState.Missing));
                Assert.That(
                    BootstrapPackageInstallerStatus.Evaluate(
                        BootstrapChannel.Stable,
                        GitPackage(DevelopmentTarget, CurrentRevision),
                        StableTarget,
                        CurrentRevision),
                    Is.EqualTo(BootstrapPackageInstallerSetupState.WrongChannel));
                Assert.That(
                    BootstrapPackageInstallerStatus.Evaluate(
                        BootstrapChannel.Stable,
                        GitPackage("https://github.com/SomeoneElse/Package-Installer.git#main", CurrentRevision),
                        StableTarget,
                        CurrentRevision),
                    Is.EqualTo(BootstrapPackageInstallerSetupState.WrongSource));
                Assert.That(
                    BootstrapPackageInstallerStatus.Evaluate(
                        BootstrapChannel.Stable,
                        GitPackage(StableTarget, "older-revision"),
                        StableTarget,
                        CurrentRevision),
                    Is.EqualTo(BootstrapPackageInstallerSetupState.Outdated));
                Assert.That(
                    BootstrapPackageInstallerStatus.Evaluate(
                        BootstrapChannel.Stable,
                        GitPackage(StableTarget, CurrentRevision),
                        StableTarget,
                        string.Empty),
                    Is.EqualTo(BootstrapPackageInstallerSetupState.UnknownReviewRequired),
                    "An unverifiable remote revision must never be reported healthy.");
                Assert.That(
                    BootstrapPackageInstallerStatus.Evaluate(
                        BootstrapChannel.Stable,
                        GitPackage(StableTarget, CurrentRevision),
                        StableTarget,
                        CurrentRevision),
                    Is.EqualTo(BootstrapPackageInstallerSetupState.Healthy));
            });
        }

        [Test]
        public void HealthPolicy_MapsLegacyRegistrySourceToMigration()
        {
            BootstrapInstalledState installed = new BootstrapInstalledState(new[]
            {
                Installed(DeucarianBootstrapPackageConstants.EditorPackageId, "git", "editor"),
                Installed(DeucarianBootstrapPackageConstants.LoggingPackageId, "git", "logging"),
                new BootstrapInstalledPackageInfo(
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    "1.0.0",
                    "registry",
                    "1.0.0",
                    string.Empty,
                    string.Empty)
            });

            BootstrapHealthReport health = BootstrapSetupPolicy.Evaluate(
                BootstrapChannel.Stable,
                installed,
                StableTarget,
                CurrentRevision);

            AssertAll(() =>
            {
                Assert.That(health.PackageInstallerState,
                    Is.EqualTo(BootstrapPackageInstallerSetupState.WrongChannel));
                Assert.That(health.RecommendedAction, Is.EqualTo(BootstrapSetupAction.Migrate));
                Assert.That(health.IsHealthy, Is.False);
            });
        }

        [Test]
        public void HealthPolicy_UsesSwitchRefreshRepairAndOpenActionsTruthfully()
        {
            BootstrapInstalledPackageInfo editor =
                Installed(DeucarianBootstrapPackageConstants.EditorPackageId, "git", "editor");
            BootstrapInstalledPackageInfo logging =
                Installed(DeucarianBootstrapPackageConstants.LoggingPackageId, "git", "logging");

            AssertAll(() =>
            {
                Assert.That(
                    BootstrapSetupPolicy.Evaluate(
                        BootstrapChannel.Stable,
                        new BootstrapInstalledState(new[]
                        {
                            editor,
                            logging,
                            GitPackage(DevelopmentTarget, CurrentRevision)
                        }),
                        StableTarget,
                        CurrentRevision).RecommendedAction,
                    Is.EqualTo(BootstrapSetupAction.SwitchChannel));
                Assert.That(
                    BootstrapSetupPolicy.Evaluate(
                        BootstrapChannel.Stable,
                        new BootstrapInstalledState(new[]
                        {
                            editor,
                            logging,
                            GitPackage(StableTarget, CurrentRevision)
                        }),
                        StableTarget,
                        string.Empty).RecommendedAction,
                    Is.EqualTo(BootstrapSetupAction.Refresh));
                Assert.That(
                    BootstrapSetupPolicy.Evaluate(
                        BootstrapChannel.Stable,
                        new BootstrapInstalledState(new[] { editor }),
                        StableTarget,
                        CurrentRevision).RecommendedAction,
                    Is.EqualTo(BootstrapSetupAction.Repair));
                Assert.That(
                    BootstrapSetupPolicy.Evaluate(
                        BootstrapChannel.Stable,
                        new BootstrapInstalledState(new[]
                        {
                            editor,
                            logging,
                            GitPackage(StableTarget, CurrentRevision)
                        }),
                        StableTarget,
                        CurrentRevision).RecommendedAction,
                    Is.EqualTo(BootstrapSetupAction.OpenPackageInstaller));
            });
        }

        private static BootstrapInstalledPackageInfo GitPackage(string gitUrl, string revision)
        {
            return new BootstrapInstalledPackageInfo(
                DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                "1.2.0",
                "git",
                gitUrl,
                gitUrl,
                revision);
        }

        private static BootstrapInstalledPackageInfo Installed(
            string packageId,
            string source,
            string packageReference)
        {
            return new BootstrapInstalledPackageInfo(
                packageId,
                "1.0.0",
                source,
                packageReference,
                string.Empty,
                string.Empty);
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }

    [TestFixture]
    internal sealed class BootstrapPackageLockContractTests
    {
        [Test]
        public void PackageLockParser_ReadsGitSourceReferenceAndRevisionFromNestedObject()
        {
            const string packageId = "com.deucarian.package-installer";
            const string json = @"{
  ""dependencies"": {
    ""com.deucarian.package-installer"": {
      ""version"": ""https://github.com/Deucarian/Package-Installer.git#main"",
      ""depth"": 0,
      ""source"": ""git"",
      ""dependencies"": {
        ""com.deucarian.editor"": ""https://github.com/Deucarian/Editor.git#main""
      },
      ""note"": ""a closing brace } inside a JSON string is not structural"",
      ""hash"": ""0123456789abcdef""
    },
    ""com.deucarian.package-installer-extension"": {
      ""version"": ""2.0.0"",
      ""source"": ""registry""
    }
  }
}";

            bool found = BootstrapPackageLockInspector.TryGetPackage(
                json,
                packageId,
                out BootstrapPackageLockEntry entry);

            Assert.That(found, Is.True);
            AssertAll(() =>
            {
                Assert.That(entry.PackageId, Is.EqualTo(packageId));
                Assert.That(entry.Source, Is.EqualTo("git"));
                Assert.That(entry.VersionReference,
                    Is.EqualTo("https://github.com/Deucarian/Package-Installer.git#main"));
                Assert.That(entry.GitUrl, Is.EqualTo(entry.VersionReference));
                Assert.That(entry.RevisionHash, Is.EqualTo("0123456789abcdef"));
            });
        }

        [Test]
        public void PackageLockParser_RejectsMissingAndTruncatedEntries()
        {
            AssertAll(() =>
            {
                Assert.That(
                    BootstrapPackageLockInspector.TryGetPackage(
                        "{\"dependencies\":{}}",
                        "com.deucarian.package-installer",
                        out _),
                    Is.False);
                Assert.That(
                    BootstrapPackageLockInspector.TryGetPackage(
                        "{\"com.deucarian.package-installer\":{\"source\":\"git\"",
                        "com.deucarian.package-installer",
                        out _),
                    Is.False);
                Assert.That(
                    BootstrapPackageLockInspector.TryGetPackage(
                        string.Empty,
                        "com.deucarian.package-installer",
                        out _),
                    Is.False);
            });
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }

    [TestFixture]
    internal sealed class BootstrapLegacyRegistryContractTests
    {
        private string _temporaryRoot;
        private string _manifestPath;

        [SetUp]
        public void SetUp()
        {
            _temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "DeucarianBootstrapLegacyRegistryTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryRoot);
            _manifestPath = Path.Combine(_temporaryRoot, "manifest.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryRoot))
            {
                Directory.Delete(_temporaryRoot, true);
            }
        }

        [Test]
        public void ScopedRegistryInspection_IsReadOnlyForValidLegacyConfiguration()
        {
            const string json = @"{
  ""dependencies"": {},
  ""scopedRegistries"": [
    {
      ""name"": ""Deucarian"",
      ""url"": ""https://registry.npmjs.org"",
      ""scopes"": [""com.deucarian""]
    }
  ]
}";
            File.WriteAllText(_manifestPath, json);
            byte[] before = File.ReadAllBytes(_manifestPath);

            BootstrapScopedRegistryStatus status =
                BootstrapScopedRegistryManifest.GetStatus(_manifestPath);

            AssertAll(() =>
            {
                Assert.That(status.State, Is.EqualTo(BootstrapScopedRegistryState.Valid));
                Assert.That(status.Configured, Is.True);
                Assert.That(status.ManifestPath, Is.EqualTo(_manifestPath));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(_manifestPath),
                    "Legacy registry inspection must never rewrite the project manifest.");
            });
        }

        [TestCase("{\"scopedRegistries\":[]}", BootstrapScopedRegistryState.Missing)]
        [TestCase(
            "{\"scopedRegistries\":[{\"name\":\"Deucarian\",\"url\":\"https://example.invalid\",\"scopes\":[\"com.deucarian\"]}]}",
            BootstrapScopedRegistryState.Invalid)]
        [TestCase(
            "{\"scopedRegistries\":[{\"name\":\"Deucarian\",\"url\":\"https://registry.npmjs.org\",\"scopes\":[\"com.deucarian\"]},{\"name\":\"Other\",\"url\":\"https://registry.npmjs.org\",\"scopes\":[\"com.deucarian\"]}]}",
            BootstrapScopedRegistryState.Duplicate)]
        public void ScopedRegistryInspection_ClassifiesLegacyConfigurationWithoutMutation(
            string json,
            BootstrapScopedRegistryState expectedState)
        {
            File.WriteAllText(_manifestPath, json);
            byte[] before = File.ReadAllBytes(_manifestPath);

            BootstrapScopedRegistryStatus status =
                BootstrapScopedRegistryManifest.GetStatus(_manifestPath);

            Assert.That(status.State, Is.EqualTo(expectedState));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_manifestPath));
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }

    [TestFixture]
    internal sealed class BootstrapChannelSynchronizationContractTests
    {
        private string _projectRoot;

        [SetUp]
        public void SetUp()
        {
            _projectRoot = Path.Combine(
                "C:\\Projects",
                "BootstrapChannelContract-" + Guid.NewGuid().ToString("N"));
            BootstrapPackageInstallerStateRepository.DeleteProjectChannelForTests(_projectRoot);
        }

        [TearDown]
        public void TearDown()
        {
            BootstrapPackageInstallerStateRepository.DeleteProjectChannelForTests(_projectRoot);
        }

        [Test]
        public void SharedProjectChannelKeyAndTimestampMatchPackageInstallerContract()
        {
            const long changedAtUtcTicks = 638905104000000000L;
            string channelKey = BootstrapPackageInstallerStateRepository
                .GetProjectChannelPreferenceKeyForTests(_projectRoot);
            string timestampKey = BootstrapPackageInstallerStateRepository
                .GetProjectChannelChangedAtPreferenceKeyForTests(_projectRoot);

            BootstrapPackageInstallerStateRepository.SetProjectChannelForTests(
                _projectRoot,
                BootstrapChannel.Development,
                changedAtUtcTicks);
            BootstrapChannelSelection selection = BootstrapPackageInstallerStateRepository
                .GetProjectChannelSelectionForTests(_projectRoot);

            AssertAll(() =>
            {
                StringAssert.StartsWith(
                    "Deucarian.PackageManagement.SelectedChannel.",
                    channelKey);
                StringAssert.StartsWith(
                    "Deucarian.PackageManagement.SelectedChannelChangedAt.",
                    timestampKey);
                Assert.That(channelKey.Substring(channelKey.LastIndexOf('.') + 1),
                    Does.Match("^[0-9a-f]{8}$"));
                Assert.That(timestampKey.Substring(timestampKey.LastIndexOf('.') + 1),
                    Is.EqualTo(channelKey.Substring(channelKey.LastIndexOf('.') + 1)));
                Assert.That(EditorPrefs.GetInt(channelKey),
                    Is.EqualTo((int)BootstrapChannel.Development));
                Assert.That(EditorPrefs.GetString(timestampKey),
                    Is.EqualTo(changedAtUtcTicks.ToString()));
                Assert.That(selection.HasValue, Is.True);
                Assert.That(selection.Channel, Is.EqualTo(BootstrapChannel.Development));
                Assert.That(selection.ChangedAtUtcTicks, Is.EqualTo(changedAtUtcTicks));
            });
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }

    [TestFixture]
    internal sealed class BootstrapPackageInstallerHandoffContractTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void Handoff_ReportsMenuExecutionSuccessOrActionableFailure(bool menuResult)
        {
            FakeMenuExecutor menu = new FakeMenuExecutor(menuResult);
            BootstrapPackageInstallerHandoff handoff =
                new BootstrapPackageInstallerHandoff(menu);

            BootstrapHandoffResult result = handoff.Open();

            AssertAll(() =>
            {
                Assert.That(menu.CallCount, Is.EqualTo(1));
                Assert.That(menu.LastMenuPath,
                    Is.EqualTo(DeucarianBootstrapPackageConstants.PackageInstallerMenuPath));
                Assert.That(result.Success, Is.EqualTo(menuResult));
                if (menuResult)
                {
                    Assert.That(result.Message, Is.Empty);
                }
                else
                {
                    Assert.That(result.Message, Is.Not.Empty);
                }
            });
        }

        private sealed class FakeMenuExecutor : IBootstrapMenuExecutor
        {
            private readonly bool _result;

            public FakeMenuExecutor(bool result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public string LastMenuPath { get; private set; }

            public bool Execute(string menuPath)
            {
                CallCount++;
                LastMenuPath = menuPath;
                return _result;
            }
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }
}
