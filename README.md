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

Click the setup button. Bootstrap installs these packages in order:

1. `com.deucarian.editor`
2. `com.deucarian.package-installer`

After setup completes, use Package Installer as the normal package manager for Deucarian packages.

## Behavior

Bootstrap uses `UnityEditor.PackageManager.Client.Add` with Git URLs for the short-term setup flow. It installs packages sequentially and stores an in-progress setup marker in Unity `SessionState` so it can continue after a domain reload when the Bootstrap window is open.

Bootstrap does not auto-install packages on editor startup. The user must explicitly open the Bootstrapper and click setup.

## Versioning

Current package version: `0.1.0`.
