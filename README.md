# Deucarian Bootstrap

## Overview

Deucarian Bootstrap is the official first-time setup and repair entry point for Deucarian Unity packages.

It is intentionally small, editor-only, and self-contained. It does not depend on `com.deucarian.editor`, `com.deucarian.package-installer`, `com.deucarian.logging`, or any other Deucarian package.

Current package version: `1.1.0`.

## Installation

Install Deucarian Bootstrap first by Git URL.

Stable:

```text
https://github.com/Deucarian/Bootstrap.git#main
```

Development:

```text
https://github.com/Deucarian/Bootstrap.git#develop
```

After Unity imports the package, Bootstrap opens the Deucarian Setup hub automatically on editor startup or project load. You can also open it manually:

```text
Tools/Deucarian/Bootstrap/Open Bootstrapper
```

Bootstrap does not install anything automatically on startup. The user must explicitly click the setup action.

## Channel Dropdown

Bootstrap has a `Channel` dropdown.

- Stable: `Recommended. Installs Deucarian packages from Git #main.`
- Development: `For testing current package work. Installs from Git #develop.`

Changing the channel refreshes the Package Registry catalog, recomputes the setup plan, recomputes the Package Installer target version, refreshes installed status, and updates the action button. It does not install anything.

Stable uses:

```text
https://raw.githubusercontent.com/Deucarian/Package-Registry/main/packages.json
```

Development uses:

```text
https://raw.githubusercontent.com/Deucarian/Package-Registry/develop/packages.json
```

## Git-Only Setup

Bootstrap installs the setup packages from Git URLs resolved from Package Registry metadata. It does not install Package Installer by package name during normal setup.

Stable Package Installer target:

```text
https://github.com/Deucarian/Package-Installer.git#main
```

Development Package Installer target:

```text
https://github.com/Deucarian/Package-Installer.git#develop
```

Bootstrap resolves dependencies first, installs Package Installer last, avoids duplicate plan entries, detects missing dependency entries, detects dependency cycles, and stores in-progress setup state so it can continue after Unity domain reloads.

## Scoped Registry

npm/scoped registry distribution is deferred.

Scoped registry support remains only as deferred, advanced, legacy manifest tooling. Git URLs are the supported distribution path for now. Bootstrap does not configure the scoped registry automatically during normal setup and does not install `com.deucarian.package-installer` by package name in the primary flow.

## Status Detection

Bootstrap detects Package Installer with Unity Package Manager package data and, when available, `Packages/packages-lock.json`.

Setup can report:

- Missing: Package Installer is not installed.
- Outdated: Package Installer is installed, but an update is available for the selected channel.
- Wrong channel: Package Installer is installed from a different Git channel or from scoped registry.
- Healthy: Package Installer is installed and matches the selected channel.
- Review required: Package Installer is installed, but the source cannot be trusted.

If the remote Package Registry cannot be loaded, Bootstrap uses the bundled fallback catalog. If the target Package Installer version cannot be read, Bootstrap still installs or repairs using the selected Git URL and shows `Target version unknown`.

## Troubleshooting

Old scoped-registry Package Installer installed:

Use Bootstrap repair. It switches Package Installer to the selected Git channel.

Wrong channel installed:

Select the desired channel. Bootstrap offers `Switch Package Installer Channel` instead of reporting healthy.

Remote registry unavailable:

Bootstrap shows `Using bundled fallback catalog because the remote Package Registry could not be loaded.` and uses its package-local fallback catalog.

Bundled fallback catalog used:

Confirm the selected channel and target Git URL in Setup Details, then run the setup action.

## Assets

Bootstrap keeps its visual assets package-local:

```text
Editor/Assets/DeucarianBootstrapLogo.png
Editor/Assets/DeucarianBootstrapHeroBackground.png
```

Replace these PNGs with the final Deucarian mark and hero artwork when they are ready. Keep the file names and `.meta` files if possible so existing package references stay stable. If the assets move later, update `DeucarianBootstrapPackageConstants.LogoAssetRelativePath`, `LogoAssetPath`, `HeroBackgroundAssetRelativePath`, and `HeroBackgroundAssetPath`.

These assets are intentionally package-local while Bootstrap remains self-contained. Shared editor icons currently live in `com.deucarian.editor` as `Editor/DeucarianEditorIcons.cs`, which resolves Unity built-in icons and generates fallback textures in code. Bootstrap mirrors the clear package-local naming style now so the assets can move into a future shared Deucarian Editor asset location without creating a second long-term icon system.

## Architecture / Contributor Notes

- [AGENTS.md](AGENTS.md) contains repository-specific ownership and Codex guidance.
- Deucarian architecture rules live in [Package Registry](https://github.com/Deucarian/Package-Registry/blob/develop/ARCHITECTURE.md).
- Capability ownership is tracked in [CAPABILITY_OWNERSHIP.md](https://github.com/Deucarian/Package-Registry/blob/develop/CAPABILITY_OWNERSHIP.md).
