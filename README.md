# Deucarian Bootstrap

## What this is

`com.deucarian.bootstrap` is the official first-time setup and repair entry point for Deucarian Unity packages.

It is intentionally small, editor-only, and self-contained. It does not depend on `com.deucarian.editor`, `com.deucarian.package-installer`, `com.deucarian.logging`, or any other Deucarian package.

Current package version: `1.1.5`.

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
- Bundled fallback catalog assets: schema-v2 package kinds, functional groups, and dependency metadata used when the remote registry cannot be loaded.

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

Changing the channel refreshes the Package Registry catalog, recomputes the setup plan, resolves the selected Package Installer branch revision and informational package version, refreshes installed status, and updates the action button. It does not install anything.

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

Bootstrap resolves dependencies first, installs Package Installer last, avoids duplicate plan entries, detects missing dependency entries, detects dependency cycles, and stores in-progress setup state so it can continue after Unity domain reloads. Hot-reloaded Package Manager request wrappers are discarded; Bootstrap resumes from persisted progress and a fresh package list. Every fresh repair resolves Editor and then Logging once from the selected Git URLs even when those package ids were already present; persisted completion markers prevent either step from repeating after a reload. Only Package Installer is removed during source migration. The bundled fallback contains only the exact setup closure: Deucarian Editor, Deucarian Logging, and Package Installer. It deliberately carries no moving version claims.

## Scoped registry

npm/scoped registry distribution is legacy and unsupported by Bootstrap's setup flow.

Bootstrap may detect an existing Deucarian scoped-registry entry to explain an older installation, but that inspection is read-only. Bootstrap never adds, repairs, removes, or otherwise changes `scopedRegistries`, and it never installs `com.deucarian.package-installer` by package name. Repair migrates an old registry-installed Package Installer to the selected Git channel while preserving unrelated manifest configuration.

## Status detection

Bootstrap detects Package Installer with Unity Package Manager package data and `Packages/packages-lock.json`. For Git installs it compares the installed lockfile `hash` with the latest commit returned for the selected Package Installer branch. The version read from Package Installer's `package.json` is informational and does not decide health.

Setup can report:

- Missing: Package Installer is not installed.
- Outdated: Package Installer is on the selected Git channel, but its lock revision differs from the selected remote branch revision.
- Wrong channel: Package Installer is installed from a different Git channel or from scoped registry.
- Healthy: Package Installer is on the selected Git channel and its installed lock revision equals the selected remote branch revision.
- Review required: Package Installer is installed, but its Git source, installed lock revision, or selected remote branch revision cannot be verified.

If the remote Package Registry cannot be loaded, Bootstrap uses the bundled three-package fallback closure. If the target Package Installer version cannot be read, Bootstrap can still report Healthy when the Git channel and revisions match. If the remote target revision cannot be resolved for an existing Git install, Bootstrap reports Review required and offers Refresh Status instead of reinstalling. A legacy registry install may still migrate once through the bundled Git fallback; after migration it stops at Review required until the remote revision can be refreshed.

## Troubleshooting

Old scoped-registry Package Installer installed:

Use `Migrate Package Installer to Git`. Bootstrap removes Package Installer first and then installs the selected Git URL; existing `scopedRegistries` remain untouched.

Wrong channel installed:

Select the desired channel. Bootstrap offers `Switch Package Installer Channel` instead of reporting healthy.

Remote registry unavailable:

Bootstrap shows `Using bundled fallback catalog because the remote Package Registry could not be loaded.` and uses its package-local fallback catalog.

Bundled fallback catalog used:

Confirm the selected channel and target Git URL in Setup Details, then run the setup action.

Target revision unavailable:

Bootstrap reports `Review required`. Restore network/Git access and refresh status; it will not report Healthy solely from the selected URL or informational version.

Compile-blocked first self-update from Package Installer 1.1.60:

The embedded assembly MVID and `Reload pending` recovery UI first ship in Package Installer `1.1.61`. An editor still executing `1.1.60` cannot gain that behavior before its first successful script reload. If the `1.1.60 -> 1.1.61` hop resolves in UPM but compilation blocks the reload, fix the compiler error and use Bootstrap or manually select the Package Installer Git URL for recovery. The same limitation applies to the legacy npm `1.1.12` assembly; migrate it through Bootstrap or a manual Git manifest change.

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

CI also calls the Package Registry's reusable Bootstrap projection check, which verifies that the bundled fallback remains the canonical Editor + Logging + Package Installer setup closure.

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
