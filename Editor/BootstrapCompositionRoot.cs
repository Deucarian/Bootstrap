namespace Deucarian.Bootstrap.Editor
{
    internal static class BootstrapCompositionRoot
    {
        public static BootstrapSetupCoordinator CreateCoordinator()
        {
            return new BootstrapSetupCoordinator(
                new BootstrapCatalogLoader(
                    new BootstrapPackageFallbackCatalogSource(),
                    new UnityBootstrapRemoteTextRequestFactory()),
                new UnityBootstrapPackageManager(),
                new BootstrapInstalledStateInspector(new BootstrapProjectPackageLockReader()),
                new GitBootstrapRevisionResolver(),
                new BootstrapSessionOperationStore(),
                new BootstrapSharedChannelStore(),
                new BootstrapLegacyRegistryInspector(),
                new UnityBootstrapClock());
        }

        public static BootstrapPackageInstallerHandoff CreateHandoff()
        {
            return new BootstrapPackageInstallerHandoff(new UnityBootstrapMenuExecutor());
        }
    }
}
