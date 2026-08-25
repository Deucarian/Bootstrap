using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor.Tests
{
    [TestFixture]
    internal sealed class BootstrapPackageAssetContractTests
    {
        private const string ExpectedPackageVersion = "1.2.7";

        [Serializable]
        private sealed class PackageManifestDto
        {
            public string name;
            public string version;
            public string displayName;
            public string unity;
        }

        [Serializable]
        private sealed class GovernanceManifestDto
        {
            public string packageId;
            public string[] runtimeAssemblies;
            public string[] editorAssemblies;
            public string[] requiredDependencies;
            public string[] optionalVersionDefinedDependencies;
        }

        [Serializable]
        private sealed class AssemblyDefinitionDto
        {
            public string name;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
        }

        [Test]
        public void PackageIdentityVersionAndDependencyContract_IsSelfContainedEditorOnlyPackage()
        {
            string packageRoot = GetPackageRoot();
            string packageJson = File.ReadAllText(Path.Combine(packageRoot, "package.json"));
            string governanceJson = File.ReadAllText(Path.Combine(packageRoot, "deucarian-package.json"));
            string assemblyJson = File.ReadAllText(
                Path.Combine(packageRoot, "Editor", "Deucarian.Bootstrap.Editor.asmdef"));
            PackageManifestDto package = JsonUtility.FromJson<PackageManifestDto>(packageJson);
            GovernanceManifestDto governance = JsonUtility.FromJson<GovernanceManifestDto>(governanceJson);
            AssemblyDefinitionDto assembly = JsonUtility.FromJson<AssemblyDefinitionDto>(assemblyJson);

            AssertAll(() =>
            {
                Assert.That(package.name, Is.EqualTo("com.deucarian.bootstrap"));
                Assert.That(package.displayName, Is.EqualTo("Deucarian Bootstrap"));
                Assert.That(package.version, Is.EqualTo(ExpectedPackageVersion),
                    "The generated catalog synchronization is a patch release from 1.2.0.");
                Assert.That(package.version, Is.EqualTo(DeucarianBootstrapPackageConstants.Version));
                Assert.That(package.unity, Is.EqualTo("2021.3"));
                Assert.That(ExtractObjectBody(packageJson, "dependencies"), Is.Empty,
                    "Bootstrap must have zero package dependencies.");

                Assert.That(governance.packageId, Is.EqualTo(package.name));
                Assert.That(governance.runtimeAssemblies, Is.Empty);
                Assert.That(governance.editorAssemblies,
                    Is.EqualTo(new[] { "Deucarian.Bootstrap.Editor" }));
                Assert.That(governance.requiredDependencies, Is.Empty);
                Assert.That(governance.optionalVersionDefinedDependencies, Is.Empty);

                Assert.That(assembly.name, Is.EqualTo("Deucarian.Bootstrap.Editor"));
                Assert.That(assembly.references, Is.Empty,
                    "The production editor assembly must not reference any Deucarian package.");
                Assert.That(assembly.includePlatforms, Is.EqualTo(new[] { "Editor" }));
                Assert.That(assembly.excludePlatforms, Is.Empty);
                Assert.That(Directory.Exists(Path.Combine(packageRoot, "Runtime")), Is.False);
            });
        }

        [Test]
        public void VersionReferences_AgreeAcrossManifestConstantsAndChangelog()
        {
            string packageRoot = GetPackageRoot();
            string changelog = File.ReadAllText(Path.Combine(packageRoot, "CHANGELOG.md"));

            AssertAll(() =>
            {
                StringAssert.Contains("## " + ExpectedPackageVersion, changelog);
                Assert.That(DeucarianBootstrapPackageConstants.PackageName,
                    Is.EqualTo("com.deucarian.bootstrap"));
                Assert.That(DeucarianBootstrapPackageConstants.DisplayName,
                    Is.EqualTo("Deucarian Bootstrap"));
                Assert.That(DeucarianBootstrapPackageConstants.Version,
                    Is.EqualTo(ExpectedPackageVersion));
            });
        }

        [Test]
        public void PackageOwnedUnityAssets_AllHaveMetaFiles()
        {
            string packageRoot = GetPackageRoot();
            string[] ownedRoots =
            {
                Path.Combine(packageRoot, "Editor"),
                Path.Combine(packageRoot, "Tests")
            };
            List<string> missing = new List<string>();

            foreach (string ownedRoot in ownedRoots)
            {
                foreach (string directory in Directory.GetDirectories(
                             ownedRoot,
                             "*",
                             SearchOption.AllDirectories))
                {
                    if (!File.Exists(directory + ".meta"))
                    {
                        missing.Add(ToPackageRelativePath(packageRoot, directory) + ".meta");
                    }
                }

                foreach (string file in Directory.GetFiles(ownedRoot, "*", SearchOption.AllDirectories))
                {
                    if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) &&
                        !File.Exists(file + ".meta"))
                    {
                        missing.Add(ToPackageRelativePath(packageRoot, file) + ".meta");
                    }
                }
            }

            Assert.That(missing, Is.Empty,
                "Every package-owned Unity directory and asset needs a committed meta file: " +
                string.Join(", ", missing.ToArray()));
        }

        [Test]
        public void LucideStatusAndActionIcons_AreValidPackageLocalPngsWithAttribution()
        {
            string packageRoot = GetPackageRoot();
            string iconRoot = Path.Combine(packageRoot, "Editor", "Assets", "Icons", "Lucide");
            string[] iconNames =
            {
                "circle-check-big.png",
                "triangle-alert.png",
                "circle-x.png",
                "loader-circle.png",
                "download.png",
                "wrench.png",
                "external-link.png",
                "package-open.png"
            };
            byte[] pngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

            foreach (string iconName in iconNames)
            {
                string path = Path.Combine(iconRoot, iconName);
                Assert.That(File.Exists(path), Is.True, iconName + " is missing.");
                Assert.That(File.Exists(path + ".meta"), Is.True, iconName + ".meta is missing.");
                CollectionAssert.AreEqual(
                    pngSignature,
                    File.ReadAllBytes(path).Take(pngSignature.Length).ToArray(),
                    iconName + " is not a PNG asset.");
            }

            string attributionPath = Path.Combine(iconRoot, "LICENSE.md");
            string attribution = File.ReadAllText(attributionPath);
            string packageLicense = File.ReadAllText(Path.Combine(packageRoot, "LICENSE.md"));

            AssertAll(() =>
            {
                StringAssert.Contains("Lucide ISC License", attribution);
                StringAssert.Contains("Feather MIT License", attribution);
                StringAssert.Contains("all rights are reserved", packageLicense.ToLowerInvariant());
                Assert.That(File.Exists(attributionPath + ".meta"), Is.True);
                Assert.That(File.Exists(Path.Combine(packageRoot, "LICENSE.md.meta")), Is.True);
            });
        }

        [Test]
        public void BootstrapStyleSheets_ArePackageLocalCompleteAndReferenceVendoredIcons()
        {
            string packageRoot = GetPackageRoot();
            string[] assetPaths =
            {
                DeucarianBootstrapPackageConstants.StyleTokensAssetPath,
                DeucarianBootstrapPackageConstants.StyleShellAssetPath,
                DeucarianBootstrapPackageConstants.StyleComponentsAssetPath,
                DeucarianBootstrapPackageConstants.StyleResponsiveAssetPath
            };

            foreach (string assetPath in assetPaths)
            {
                StringAssert.StartsWith("Packages/com.deucarian.bootstrap/", assetPath);
                string absolutePath = Path.Combine(
                    packageRoot,
                    assetPath.Substring("Packages/com.deucarian.bootstrap/".Length)
                        .Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.Exists(absolutePath), Is.True, assetPath + " is missing.");
                Assert.That(File.Exists(absolutePath + ".meta"), Is.True, assetPath + ".meta is missing.");
                Assert.That(new FileInfo(absolutePath).Length, Is.GreaterThan(0));
            }

            string tokens = File.ReadAllText(Path.Combine(
                packageRoot,
                "Editor",
                "Assets",
                "Styles",
                "DeucarianBootstrapTokens.uss"));
            string components = File.ReadAllText(Path.Combine(
                packageRoot,
                "Editor",
                "Assets",
                "Styles",
                "DeucarianBootstrapComponents.uss"));

            AssertAll(() =>
            {
                StringAssert.Contains(".deucarian-bootstrap--dark", tokens);
                StringAssert.Contains(".deucarian-bootstrap--light", tokens);
                StringAssert.Contains("url(\"../Icons/Lucide/circle-check-big.png\")", components);
                StringAssert.Contains("url(\"../Icons/Lucide/triangle-alert.png\")", components);
                StringAssert.Contains("url(\"../Icons/Lucide/circle-x.png\")", components);
                StringAssert.Contains("url(\"../Icons/Lucide/loader-circle.png\")", components);
                StringAssert.Contains("url(\"../Icons/Lucide/download.png\")", components);
                StringAssert.Contains("url(\"../Icons/Lucide/wrench.png\")", components);
                StringAssert.Contains("url(\"../Icons/Lucide/external-link.png\")", components);
                StringAssert.Contains("url(\"../Icons/Lucide/package-open.png\")", components);
            });
        }

        [Test]
        public void LightSkinSmallTextTokensMeetAccessibleContrast()
        {
            string tokens = File.ReadAllText(Path.Combine(
                GetPackageRoot(),
                "Editor",
                "Assets",
                "Styles",
                "DeucarianBootstrapTokens.uss"));
            string lightBlock = Regex.Match(
                tokens,
                "\\.deucarian-bootstrap--light\\s*\\{(?<body>[^}]*)\\}",
                RegexOptions.CultureInvariant | RegexOptions.Singleline).Groups["body"].Value;
            string background = ReadHexToken(lightBlock, "--bootstrap-bg");
            string[] foregroundTokens =
            {
                "--bootstrap-text",
                "--bootstrap-text-muted",
                "--bootstrap-text-faint",
                "--bootstrap-accent",
                "--bootstrap-info",
                "--bootstrap-success",
                "--bootstrap-warning",
                "--bootstrap-error",
                "--bootstrap-neutral",
                "--bootstrap-focus"
            };

            foreach (string token in foregroundTokens)
            {
                double ratio = ContrastRatio(
                    ReadHexToken(lightBlock, token),
                    background);
                Assert.That(ratio, Is.GreaterThanOrEqualTo(4.5d),
                    token + " must meet WCAG normal-text contrast in the light skin.");
            }
        }

        [Test]
        public void ProductionUiStaysWithinTheUnity2021_3CompatibilitySurface()
        {
            string packageRoot = GetPackageRoot();
            string stylesRoot = Path.Combine(packageRoot, "Editor", "Assets", "Styles");
            string allStyles = string.Join(
                "\n",
                Directory.GetFiles(stylesRoot, "*.uss", SearchOption.TopDirectoryOnly)
                    .Select(File.ReadAllText));
            string allProductionCode = string.Join(
                "\n",
                Directory.GetFiles(
                        Path.Combine(packageRoot, "Editor"),
                        "*.cs",
                        SearchOption.AllDirectories)
                    .Select(File.ReadAllText));

            string[] unsupportedUssPatterns =
            {
                @"(^|[\s;{])(row-|column-)?gap\s*:",
                @"display\s*:\s*grid",
                @"grid-template",
                @"color-mix\s*\(",
                @"light-dark\s*\(",
                @"(^|[\s;{])transition(-[a-z]+)?\s*:",
                @"(^|[\s;{])animation(-[a-z]+)?\s*:"
            };
            string[] unsupportedCodePatterns =
            {
                @"\bFindFirstObjectByType\b",
                @"\bFindAnyObjectByType\b",
                @"\bMultiColumnListView\b",
                @"\.dataSource\b",
                @"\.SetBinding\s*\(",
                @"\brecord\s+(class|struct|[A-Za-z_])",
                @"\brequired\s+(string|bool|int|float|double|long|object|[A-Z][A-Za-z0-9_<>,?]*)\s+[A-Za-z_]",
                @"\binit\s*;"
            };

            foreach (string pattern in unsupportedUssPatterns)
            {
                Assert.That(Regex.IsMatch(
                    allStyles,
                    pattern,
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline),
                    Is.False,
                    "USS uses a construct outside Bootstrap's Unity 2021.3 compatibility subset: " +
                    pattern);
            }

            foreach (string pattern in unsupportedCodePatterns)
            {
                Assert.That(Regex.IsMatch(
                    allProductionCode,
                    pattern,
                    RegexOptions.CultureInvariant),
                    Is.False,
                    "Production code uses an API or language construct unavailable on Unity 2021.3: " +
                    pattern);
            }
        }

        private static string GetPackageRoot()
        {
            PackageInfo package = PackageInfo.FindForAssembly(
                typeof(DeucarianBootstrapPackageConstants).Assembly);
            Assert.That(package, Is.Not.Null,
                "Unity Package Manager could not resolve the Bootstrap package root.");
            Assert.That(package.resolvedPath, Is.Not.Empty);
            return package.resolvedPath;
        }

        private static string ExtractObjectBody(string json, string propertyName)
        {
            Match match = Regex.Match(
                json ?? string.Empty,
                "\\\"" + Regex.Escape(propertyName) + "\\\"\\s*:\\s*\\{(?<body>[^}]*)\\}",
                RegexOptions.CultureInvariant);
            Assert.That(match.Success, Is.True, "Missing JSON object property " + propertyName + ".");
            return match.Groups["body"].Value.Trim();
        }

        private static string ReadHexToken(string block, string token)
        {
            Match match = Regex.Match(
                block ?? string.Empty,
                Regex.Escape(token) + "\\s*:\\s*#(?<hex>[0-9a-fA-F]{6})\\s*;",
                RegexOptions.CultureInvariant);
            Assert.That(match.Success, Is.True, "Missing color token " + token + ".");
            return match.Groups["hex"].Value;
        }

        private static double ContrastRatio(string foreground, string background)
        {
            double foregroundLuminance = RelativeLuminance(foreground);
            double backgroundLuminance = RelativeLuminance(background);
            return (Math.Max(foregroundLuminance, backgroundLuminance) + 0.05d) /
                   (Math.Min(foregroundLuminance, backgroundLuminance) + 0.05d);
        }

        private static double RelativeLuminance(string hex)
        {
            double red = LinearChannel(Convert.ToInt32(hex.Substring(0, 2), 16) / 255d);
            double green = LinearChannel(Convert.ToInt32(hex.Substring(2, 2), 16) / 255d);
            double blue = LinearChannel(Convert.ToInt32(hex.Substring(4, 2), 16) / 255d);
            return (0.2126d * red) + (0.7152d * green) + (0.0722d * blue);
        }

        private static double LinearChannel(double channel)
        {
            return channel <= 0.04045d
                ? channel / 12.92d
                : Math.Pow((channel + 0.055d) / 1.055d, 2.4d);
        }

        private static string ToPackageRelativePath(string packageRoot, string path)
        {
            return path.Substring(packageRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/');
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }
}
