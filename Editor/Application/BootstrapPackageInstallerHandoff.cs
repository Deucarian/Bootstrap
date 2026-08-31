using System;
using UnityEditor;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class BootstrapHandoffResult
    {
        public BootstrapHandoffResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }

        public string Message { get; }
    }

    internal interface IBootstrapMenuExecutor
    {
        bool Execute(string menuPath);
    }

    internal sealed class UnityBootstrapMenuExecutor : IBootstrapMenuExecutor
    {
        public bool Execute(string menuPath)
        {
            if (!string.Equals(
                    menuPath,
                    DeucarianBootstrapPackageConstants.PackageInstallerMenuPath,
                    StringComparison.Ordinal))
            {
                return false;
            }

            // Bootstrap intentionally has no package dependencies, so this is the
            // single governed literal-menu bridge allowed by menu-policy.json.
            return EditorApplication.ExecuteMenuItem(
                DeucarianBootstrapPackageConstants.PackageInstallerMenuPath);
        }
    }

    internal sealed class BootstrapPackageInstallerHandoff
    {
        private readonly IBootstrapMenuExecutor _menuExecutor;

        public BootstrapPackageInstallerHandoff(IBootstrapMenuExecutor menuExecutor)
        {
            _menuExecutor = menuExecutor ?? throw new ArgumentNullException(nameof(menuExecutor));
        }

        public BootstrapHandoffResult Open()
        {
            bool opened = _menuExecutor.Execute(
                DeucarianBootstrapPackageConstants.PackageInstallerMenuPath);
            return opened
                ? new BootstrapHandoffResult(true, string.Empty)
                : new BootstrapHandoffResult(
                    false,
                    "Package Installer is installed, but its menu is not ready yet. Let Unity finish compiling, then refresh status.");
        }
    }
}
