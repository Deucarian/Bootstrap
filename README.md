# Deucarian Bootstrap

## What this is

`com.deucarian.bootstrap` is the official first-time setup and repair entry point for Deucarian Unity packages.

It is intentionally small, editor-only, and self-contained. It does not depend on `com.deucarian.editor`, `com.deucarian.package-installer`, `com.deucarian.logging`, or any other Deucarian package.

Current package version: `1.1.1`.

## When to use it

- You are setting up Deucarian packages in a Unity project for the first time.
- You need to install or repair the Deucarian Package Installer from the selected Git channel.
- You need a package-local fallback catalog when the remote Package Registry is unavailable.
- You need to switch Package Installer between the stable and development Git channels.

## When not to use it

- Do not use Bootstrap as a normal package management UI after setup; use `com.deucarian.package-installer`.
- Do not add shared editor chrome, logging, diagnostics, or runtime package dependencies here.
- Do not use Bootstrap to publish releases, configure npm/scoped registry distribution, or manage package governance.

## Install

Install Deucarian Bootstrap first by Git URL through Unity Package Manager.

Stable:

```json
"com.deucarian.bootstrap": "https://github.com/Deucarian/Bootstrap.git#main"
```

Development:

```json
"com.deucarian.bootstrap": "https://github.com/Deucarian/Bootstrap.git#develop"
```

## Unity compatibility

Requires Unity 2021.3 or newer.

## 60-second quick start

Install Bootstrap from the stable Git URL, let Unity import the package, then open the setup hub:

```text
Tools/Deucarian/Bootstrap/Open Bootstrapper
```

Choose the `Stable` or `Development` channel, review the setup plan, and click the setup action when you are ready. Bootstrap installs setup dependencies first and installs Package Installer last.

Bootstrap can open automatically on editor startup or project load, but it does not install anything automatically. The user must explicitly click the setup action.

## Public API map

- `DeucarianBootstrapWindow`: editor setup hub and repair UI.
- `Tools/Deucarian/Bootstrap/Open Bootstrapper`: Unity menu entry declared by the Bootstrap window.
- `DeucarianBootstrapPackageConstants`: package-local paths, URLs, and setup identifiers.
- `BootstrapPackageInstallerStateRepository`: project-scoped stable/development channel state shared with Package Installer.
- Bundled fallback catalog assets: package-local registry metadata used when the remote registry cannot be loaded.

## Integrations

Works with:

- Package Registry `packages.json` from the selected stable or development branch.
- Package Installer as the final setup destination.
- Unity Package Manager Git URL dependencies.

Does not own:

- normal package management after setup,
- shared editor chrome,
- shared logging or diagnostics,
- package governance metadata,
- release publishing.

## Channel dropdown

Bootstrap has a `Channel` dropdown.

- Stable: `Recommended. Installs Deucarian packages from Git #main.`
- Development: `For testing current package work. Installs from Git #develop.`

Changing the channel refreshes the Package Registry catalog, recomputes the setup plan, recomputes the Package Installer target version, refreshes installed status, and updates the action button. It does not install anything.

The selected channel is stored in the shared project-scoped Deucarian package-management preference, so Package Installer and Bootstrap read the same stable/development state when opened or refreshed.

Stable uses:

```text
https://raw.githubusercontent.com/Deucarian/Package-Registry/main/packages.json
```

Development uses:

```text
https://raw.githubusercontent.com/Deucarian/Package-Registry/develop/packages.json
```

## Git-only setup

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

## Scoped registry

npm/scoped registry distribution is deferred.

Scoped registry support remains only as deferred, advanced, legacy manifest tooling. Git URLs are the supported distribution path for now. Bootstrap does not configure the scoped registry automatically during normal setup and does not install `com.deucarian.package-installer` by package name in the primary flow.

## Status detection

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
Editor/Assets/Logos/DeucarianBootstrapLogo.png
Editor/Assets/Logos/DeucarianPackageInstallerLogo.png
Editor/Assets/Images/DeucarianInstallerBackground.png
Editor/Assets/Images/DeucarianPackageInstallerHero.png
Editor/Assets/Images/DeucarianBootstrapHeroBackground.png
Editor/Assets/Icons/DeucarianPackagePlaceholderIcon.png
```

The wallpaper, Package Installer hero, logo, and package icon are copied package-locally so Bootstrap can use the Deucarian premium visual family without depending on `com.deucarian.editor` or `com.deucarian.package-installer`. If the assets move later, update `DeucarianBootstrapPackageConstants.LogoAssetRelativePath`, `LogoAssetPath`, `WallpaperAssetRelativePath`, `WallpaperAssetPath`, `HeroBackgroundAssetRelativePath`, `HeroBackgroundAssetPath`, `PackageIconAssetRelativePath`, and `PackageIconAssetPath`.

These assets are intentionally package-local while Bootstrap remains self-contained. Shared editor icons currently live in `com.deucarian.editor` as `Editor/DeucarianEditorIcons.cs`, which resolves Unity built-in icons and generates fallback textures in code. Bootstrap mirrors the clear package-local naming style now so the assets can move into a future shared Deucarian Editor asset location without creating a second long-term icon system.

## Validation

Run the shared package validator from the repository root:

```powershell
python C:/Repositories/Package-Registry/Tools/deucarian_package_validator.py --registry-root C:/Repositories/Package-Registry --repository-root . --config deucarian-package.json
```

Run existing Unity EditMode tests after code or assembly definition changes.

Documentation-only updates should still pass:

```powershell
git diff --check
```

## Architecture / Contributor Notes

- [AGENTS.md](AGENTS.md) contains repository-specific ownership and Codex guidance.
- Deucarian architecture rules live in [Package Registry](https://github.com/Deucarian/Package-Registry/blob/develop/ARCHITECTURE.md).
- Capability ownership is tracked in [CAPABILITY_OWNERSHIP.md](https://github.com/Deucarian/Package-Registry/blob/develop/CAPABILITY_OWNERSHIP.md).

## License

See [LICENSE.md](LICENSE.md).
