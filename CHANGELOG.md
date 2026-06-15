# Changelog

## 0.1.4 - 2026-06-15

- Increased the default Bootstrap setup hub window size and minimum size so the normal layout opens without immediate scrolling.
- Persisted pending setup state across domain reloads and package refreshes so setup continues from Editor to Logging to Package Installer.
- Added retry and timeout status messaging while Unity refreshes packages after each install.

## 0.1.3 - 2026-06-15

- Added an explicit setup mode selector for Git fallback and npmjs scoped registry installs.
- Added scoped registry manifest inspection and repair for the `com.deucarian` Unity scope.
- Added scoped registry setup that installs `com.deucarian.package-installer` by package name and lets Unity resolve its dependencies.
- Added tests for scoped registry manifest repair and idempotency.

## 0.1.2 - 2026-06-15

- Reworked the Bootstrap window into a Deucarian setup hub with a hero, status card, setup actions, Package Installer launcher, documentation links, and footer status.
- Added a package-local placeholder Bootstrap logo asset and documented how to replace it.
- Added setup status refresh behavior that checks installed packages without starting install until the user clicks setup.

## 0.1.1 - 2026-06-15

- Added remote Deucarian Package Registry catalog loading with a bundled fallback catalog.
- Added dependency-first install planning for Package Installer setup.
- Added plan display, missing dependency detection, and circular dependency detection.

## 0.1.0 - 2026-06-15

- Added the initial self-contained Deucarian Bootstrap editor package.
- Added a user-clicked setup flow that installs Deucarian Editor before Deucarian Package Installer.
