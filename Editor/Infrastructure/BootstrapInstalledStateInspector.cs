using System;
using System.Collections.Generic;

namespace Deucarian.Bootstrap.Editor
{
    internal interface IBootstrapPackageLockReader
    {
        BootstrapPackageLockEntry GetPackage(string packageId);
    }

    internal sealed class BootstrapProjectPackageLockReader : IBootstrapPackageLockReader
    {
        public BootstrapPackageLockEntry GetPackage(string packageId)
        {
            return BootstrapPackageLockInspector.GetPackage(packageId);
        }
    }

    internal interface IBootstrapInstalledStateInspector
    {
        BootstrapInstalledState Inspect(IReadOnlyList<BootstrapPackageRecord> packages);
    }

    internal sealed class BootstrapInstalledStateInspector : IBootstrapInstalledStateInspector
    {
        private readonly IBootstrapPackageLockReader _lockReader;

        public BootstrapInstalledStateInspector(IBootstrapPackageLockReader lockReader)
        {
            _lockReader = lockReader ?? throw new ArgumentNullException(nameof(lockReader));
        }

        public BootstrapInstalledState Inspect(IReadOnlyList<BootstrapPackageRecord> packages)
        {
            List<BootstrapInstalledPackageInfo> installed = new List<BootstrapInstalledPackageInfo>();

            foreach (BootstrapPackageRecord package in packages ?? Array.Empty<BootstrapPackageRecord>())
            {
                if (package == null || !IsSetupPackage(package.PackageId))
                {
                    continue;
                }

                BootstrapPackageLockEntry lockEntry = string.Equals(
                    package.PackageId,
                    DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                    StringComparison.OrdinalIgnoreCase)
                    ? _lockReader.GetPackage(package.PackageId)
                    : null;
                installed.Add(new BootstrapInstalledPackageInfo(
                    package.PackageId,
                    package.Version,
                    package.Source,
                    package.PackageReference,
                    lockEntry != null ? lockEntry.GitUrl : string.Empty,
                    lockEntry != null ? lockEntry.RevisionHash : string.Empty));
            }

            return new BootstrapInstalledState(installed);
        }

        private static bool IsSetupPackage(string packageId)
        {
            return string.Equals(
                       packageId,
                       DeucarianBootstrapPackageConstants.EditorPackageId,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       packageId,
                       DeucarianBootstrapPackageConstants.LoggingPackageId,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       packageId,
                       DeucarianBootstrapPackageConstants.PackageInstallerPackageId,
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
