# Deucarian Bootstrap

## Overview

Deucarian Bootstrap is the first-time setup entry point for Deucarian Unity packages.

It is intentionally small, editor-only, and self-contained. It does not depend on `com.deucarian.editor`, `com.deucarian.package-installer`, `com.deucarian.logging`, or any other Deucarian package.

## Installation

Install Bootstrap by Git URL:

```text
https://github.com/Deucarian/Bootstrap.git#main
```

Then open:

```text
Tools/Deucarian/Bootstrap/Open Bootstrapper
```

Click the setup button. Bootstrap can run in two setup modes:

- Git fallback mode loads the Deucarian package catalog, resolves the dependency graph for `com.deucarian.package-installer`, shows the planned install order, and installs the plan sequentially from Git URLs.
- Scoped registry mode configures the Unity scoped registry for `com.deucarian`, installs `com.deucarian.package-installer` by package name from npmjs, and lets Unity resolve its declared dependencies.

Bootstrap uses the remote Deucarian Package Registry catalog when available:

```text
https://raw.githubusercontent.com/Deucarian/Package-Registry/main/packages.json
```

If the remote catalog cannot be loaded, Bootstrap uses its bundled fallback catalog.

With the current catalog, Bootstrap installs:

1. `com.deucarian.editor`
2. `com.deucarian.logging`
3. `com.deucarian.package-installer`

After setup completes, use Package Installer as the normal package manager for Deucarian packages.

## Scoped Registry Mode

Scoped registry mode adds or repairs this Unity project manifest entry:

```json
{
  "name": "Deucarian",
  "url": "https://registry.npmjs.org",
  "scopes": ["com.deucarian"]
}
```

Use the repair button to configure the scoped registry without installing packages. Use the setup button to repair the scoped registry, then install Package Installer by package name.

## Behavior

Bootstrap uses `UnityEditor.PackageManager.Client.Add` with Git URLs for the fallback setup flow and with `com.deucarian.package-installer` for the scoped registry setup flow. It detects missing dependencies and circular dependencies before Git fallback installs, avoids duplicate plan entries, skips packages that are already installed, and stores an in-progress setup marker in Unity `SessionState` so it can continue after a domain reload when the Bootstrap window is open.

Bootstrap does not auto-install packages on editor startup. The user must explicitly open the Bootstrapper and click setup.

## Versioning

Current package version: `0.1.4`.

## Bootstrap Logo Placeholder

The setup hub uses a local placeholder logo at:

```text
Editor/Assets/DeucarianBootstrapLogo.png
```

Replace this PNG with the final Deucarian mark when it is ready. Keep the file name and `.meta` file if possible so existing package references stay stable. If the asset moves later, update `DeucarianBootstrapPackageConstants.LogoAssetRelativePath` and `LogoAssetPath`.

This logo is intentionally package-local while Bootstrap remains self-contained. Shared editor icons currently live in `com.deucarian.editor` as `Editor/DeucarianEditorIcons.cs`, which resolves Unity built-in icons and generates fallback textures in code. Bootstrap mirrors the clear package-local naming style now so the asset can move into a future shared Deucarian Editor asset location without creating a second long-term icon system.
