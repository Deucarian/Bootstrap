using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    [Serializable]
    internal sealed class BootstrapPackageCatalog
    {
        public int schemaVersion;
        public string updatedAt;
        public BootstrapPackageDefinition[] packages;
    }

    [Serializable]
    internal sealed class BootstrapPackageDefinition
    {
        public string id;
        public string displayName;
        public string category;
        public string description;
        public string stableUrl;
        public string developmentUrl;
        public string[] dependencies;
    }

    internal static class BootstrapCatalogParser
    {
        public static bool TryParse(string json, out BootstrapPackageCatalog catalog, out string errorMessage)
        {
            catalog = null;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "Catalog JSON is empty.";
                return false;
            }

            try
            {
                catalog = JsonUtility.FromJson<BootstrapPackageCatalog>(json);
            }
            catch (Exception exception)
            {
                errorMessage = "Catalog JSON could not be parsed: " + exception.Message;
                return false;
            }

            if (catalog == null)
            {
                errorMessage = "Catalog JSON did not produce a catalog.";
                return false;
            }

            if (catalog.schemaVersion != 1)
            {
                errorMessage = "Unsupported catalog schema version " + catalog.schemaVersion + ".";
                return false;
            }

            if (catalog.packages == null || catalog.packages.Length == 0)
            {
                errorMessage = "Catalog does not contain any packages.";
                return false;
            }

            return true;
        }
    }

    internal static class BootstrapInstallPlanner
    {
        public static BootstrapInstallPlanResult BuildPlan(BootstrapPackageCatalog catalog, string targetPackageId)
        {
            if (catalog == null)
            {
                return BootstrapInstallPlanResult.CreateFailure("Catalog is not loaded.");
            }

            if (string.IsNullOrWhiteSpace(targetPackageId))
            {
                return BootstrapInstallPlanResult.CreateFailure("Target package id is empty.");
            }

            Dictionary<string, BootstrapPackageDefinition> packagesById = new Dictionary<string, BootstrapPackageDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (BootstrapPackageDefinition package in catalog.packages ?? Array.Empty<BootstrapPackageDefinition>())
            {
                if (package == null || string.IsNullOrWhiteSpace(package.id))
                {
                    return BootstrapInstallPlanResult.CreateFailure("Catalog contains a package without an id.");
                }

                if (packagesById.ContainsKey(package.id))
                {
                    return BootstrapInstallPlanResult.CreateFailure("Catalog contains duplicate package id " + package.id + ".");
                }

                packagesById.Add(package.id, package);
            }

            List<BootstrapPackageStep> steps = new List<BootstrapPackageStep>();
            HashSet<string> visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Stack<string> stack = new Stack<string>();
            string errorMessage;

            if (!Visit(targetPackageId, packagesById, visiting, visited, stack, steps, out errorMessage))
            {
                return BootstrapInstallPlanResult.CreateFailure(errorMessage);
            }

            return BootstrapInstallPlanResult.CreateSuccess(steps);
        }

        private static bool Visit(
            string packageId,
            IReadOnlyDictionary<string, BootstrapPackageDefinition> packagesById,
            ISet<string> visiting,
            ISet<string> visited,
            Stack<string> stack,
            ICollection<BootstrapPackageStep> steps,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (visited.Contains(packageId))
            {
                return true;
            }

            if (!packagesById.TryGetValue(packageId, out BootstrapPackageDefinition package))
            {
                errorMessage = "Missing dependency " + packageId + ".";
                return false;
            }

            if (visiting.Contains(packageId))
            {
                errorMessage = "Circular dependency detected: " + FormatCycle(stack, packageId) + ".";
                return false;
            }

            visiting.Add(packageId);
            stack.Push(packageId);

            foreach (string dependencyId in package.dependencies ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(dependencyId))
                {
                    errorMessage = "Package " + packageId + " contains an empty dependency id.";
                    return false;
                }

                if (!Visit(dependencyId, packagesById, visiting, visited, stack, steps, out errorMessage))
                {
                    return false;
                }
            }

            stack.Pop();
            visiting.Remove(packageId);
            visited.Add(packageId);

            if (string.IsNullOrWhiteSpace(package.stableUrl))
            {
                errorMessage = "Package " + packageId + " does not define a stable Git URL.";
                return false;
            }

            steps.Add(new BootstrapPackageStep(
                package.id,
                string.IsNullOrWhiteSpace(package.displayName) ? package.id : package.displayName,
                package.stableUrl));

            return true;
        }

        private static string FormatCycle(IEnumerable<string> stack, string repeatedPackageId)
        {
            List<string> path = new List<string>(stack);
            path.Reverse();
            path.Add(repeatedPackageId);
            return string.Join(" -> ", path.ToArray());
        }
    }

    internal sealed class BootstrapInstallPlanResult
    {
        private BootstrapInstallPlanResult(bool success, IReadOnlyList<BootstrapPackageStep> steps, string errorMessage)
        {
            Success = success;
            Steps = steps ?? Array.Empty<BootstrapPackageStep>();
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool Success { get; }

        public IReadOnlyList<BootstrapPackageStep> Steps { get; }

        public string ErrorMessage { get; }

        public static BootstrapInstallPlanResult CreateSuccess(IReadOnlyList<BootstrapPackageStep> steps)
        {
            return new BootstrapInstallPlanResult(true, steps, string.Empty);
        }

        public static BootstrapInstallPlanResult CreateFailure(string errorMessage)
        {
            return new BootstrapInstallPlanResult(false, Array.Empty<BootstrapPackageStep>(), errorMessage);
        }
    }
}
