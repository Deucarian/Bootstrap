# Deucarian Bootstrap

## Overview

Deucarian Bootstrap is the official first-time setup and repair entry point for Deucarian Unity packages.

It is intentionally small, editor-only, and self-contained. It does not depend on `com.deucarian.editor`, `com.deucarian.package-installer`, `com.deucarian.logging`, or any other Deucarian package.

## Installation

Install Deucarian Bootstrap first by Git URL:

```text
https://github.com/Deucarian/Bootstrap.git#main
```

After Unity imports the package, Bootstrap opens the Deucarian Setup hub automatically on editor startup or project load. You can also open it manually:

```text
Tools/Deucarian/Bootstrap/Open Bootstrapper
```

Use the primary button in the Package Installer hero card. Bootstrap does not install anything automatically on startup; it only opens the setup hub and shows setup/repair status until the user starts setup.

Recommended setup mode:

- Scoped registry mode configures the Unity scoped registry for `com.deucarian`, installs `com.deucarian.package-installer` by package name from npmjs, and lets Unity resolve `com.deucarian.editor` and `com.deucarian.logging`.

Fallback setup mode:

- Git fallback mode loads the Deucarian package catalog, resolves the dependency graph for `com.deucarian.package-installer`, shows the planned install order, and installs the plan sequentially from Git URLs.

Bootstrap uses the remote Deucarian Package Registry catalog for Git fallback mode when available:

```text
https://raw.githubusercontent.com/Deucarian/Package-Registry/main/packages.json
```

If the remote catalog cannot be loaded, Bootstrap uses its bundled fallback catalog.

With the current catalog, Bootstrap installs:

1. `com.deucarian.editor`
2. `com.deucarian.logging`
3. `com.deucarian.package-installer`

After setup completes, use Package Installer to install and manage Deucarian packages.

The default Bootstrap view keeps setup information compact. It shows whether the registry is configured, whether required setup packages are installed, whether Package Installer is ready, and which install source is active. Detailed package rows, registry source, catalog status, and install plan diagnostics remain under Setup Details.

## Scoped Registry Mode

Scoped registry mode adds or repairs this Unity project manifest entry:

```json
{
  "name": "Deucarian",
  "url": "https://registry.npmjs.org",
  "scopes": ["com.deucarian"]
}
```

Use the repair button to configure the scoped registry without installing packages. Use the setup button to repair the scoped registry, install Package Installer by package name, and let Unity resolve Package Installer dependencies.

## Behavior

Bootstrap uses `UnityEditor.PackageManager.Client.Add` with Git URLs for the fallback setup flow and with `com.deucarian.package-installer` for the scoped registry setup flow. It detects missing dependencies and circular dependencies before Git fallback installs, avoids duplicate plan entries, skips packages that are already installed, and stores an in-progress setup marker in Unity `SessionState` so it can continue after a domain reload when the Bootstrap window is open.

Bootstrap opens its Deucarian Setup hub on startup by default. The window includes a project-scoped `Show Bootstrap on startup` toggle. Startup only opens the hub and refreshes setup status; it does not auto-install packages. The user must explicitly click setup.

The hero button changes with setup state:

- `Install Deucarian Setup` when setup has not been installed.
- `Installing...` or `Waiting for Unity...` while setup is running.
- `Continue Setup` or `Repair Setup` when setup was interrupted or needs repair.
- `Open Package Installer` when setup is healthy.

## Versioning

Current package version: `0.1.8`.

## Bootstrap Placeholder Assets

The Package Installer hero card uses local placeholder assets at:

```text
Editor/Assets/DeucarianBootstrapLogo.png
Editor/Assets/DeucarianBootstrapHeroBackground.png
```

Replace these PNGs with the final Deucarian mark and hero artwork when they are ready. Keep the file names and `.meta` files if possible so existing package references stay stable. If the assets move later, update `DeucarianBootstrapPackageConstants.LogoAssetRelativePath`, `LogoAssetPath`, `HeroBackgroundAssetRelativePath`, and `HeroBackgroundAssetPath`.

These assets are intentionally package-local while Bootstrap remains self-contained. Shared editor icons currently live in `com.deucarian.editor` as `Editor/DeucarianEditorIcons.cs`, which resolves Unity built-in icons and generates fallback textures in code. Bootstrap mirrors the clear package-local naming style now so the assets can move into a future shared Deucarian Editor asset location without creating a second long-term icon system.
