# Changelog

## Unreleased

## 1.1.2 - 2026-07-13

- Moved selected stable/development channel persistence to the shared project-scoped package-management preference used by Package Installer.
- Removed the legacy fallback launcher for the old top-level Package Installer menu path.
- Replaced unsupported IMGUI text clipping values so Bootstrap compiles on the declared Unity version line.
- Changed Package Installer health to compare the installed package-lock revision with the selected remote Git branch revision; unknown target or lock revisions now require review.
- Kept Package Installer package.json version lookup informational so a matching Git revision can still be healthy when version metadata is unavailable.
- Removed all Bootstrap manifest-writing and scoped-registry install paths while retaining read-only detection for legacy projects.
- Reduced the bundled fallback catalog to the exact Editor, Logging, and Package Installer setup closure with no moving version claims.
- Added the reusable Package Registry Bootstrap projection check as a separate validation job.
- Made every repair resolve Editor and Logging once from the selected Git channel before Package Installer, with completed steps persisted across domain reloads and removal restricted to Package Installer.
- Discarded hot-reloaded UPM request wrappers so repair continuation is rebuilt from persisted progress and a fresh package list instead of failing on stale request objects.
- Made an unavailable remote Package Installer revision a refresh-only review state, while still allowing legacy registry installs to migrate once and then stop at Review required.
- Documented that Package Installer's MVID-based reload-pending recovery begins in `1.1.61`; an already-running `1.1.60` or legacy npm `1.1.12` assembly needs Bootstrap or manual Git recovery for its first compile-blocked hop.

## 1.1.1 - 2026-06-23

- Copied the Deucarian wallpaper, Package Installer hero, logo, and icon assets into Bootstrap-owned paths.
- Rebalanced the Bootstrap window around a larger minimum size, compact hero, 2x2 status grid, and aligned channel selector.
- Reworked major panels, details rows, status cards, and footer into a darker frosted-glass visual treatment with procedural fallbacks.
- Hardened IMGUI layout scopes and status-card drawing against null styles, null text, and resize-time layout exceptions.
- Added stable `.meta` files for root/editor-imported text files that Unity can import from immutable package folders.
- Kept Git-channel setup logic intact with Stable using Package Installer `#main` and Development using `#develop`.

## 1.1.0 - 2026-06-23

- Changed the primary setup flow to install Package Installer and setup dependencies from selected Git channel URLs.
- Added Stable and Development channels for Package Registry catalog selection and Package Installer target URLs.
- Added Package Installer source, channel, and version status detection with missing, outdated, wrong-channel, healthy, and review-required states.
- Added safe Package Installer repair behavior for stale scoped-registry or untrusted installs.
- Refreshed the Bootstrap window with a premium dark Deucarian setup header, channel dropdown, status cards, target diagnostics, and Git-channel footer copy.
- Updated the bundled fallback catalog with development setup URLs and Package Installer target versions.
- Marked npm/scoped registry setup as deferred, advanced, and legacy instead of the recommended primary path.

## 1.0.2 - 2026-06-22

- Added Deucarian Common to the bundled fallback catalog.
- Updated bundled Object Loading and UI Binding dependency metadata to include Common.

## 1.0.1 - 2026-06-17

- Updated the bundled package catalog fallback to use Integration package IDs and categories instead of Bridge package IDs.

## 0.1.7 - 2026-06-16

- Made the Package Installer hero action context-aware for install, continue, repair, waiting, and open states.
- Added a compact setup progress timeline to the hero and collapsed the complete state into a short healthy setup summary.
- Reworked the visible setup summary into compact status chips with a subtle install source indicator.
- Added tooltips for setup mode, utility actions, the startup toggle, and the hero action.
- Clarified the footer note that Bootstrap is only for first-time setup and repair.

## 0.1.6 - 2026-06-16

- Refocused the Bootstrap window around a Package Installer hero card with a local Deucarian-style background asset.
- Demoted setup diagnostics into a compact summary and collapsible setup details section.
- Made Bootstrap the official Deucarian Setup entry point with startup welcome behavior and a project-scoped startup toggle.
- Made scoped registry mode the recommended default while keeping Git URL fallback available.
- Clarified that startup opens the setup hub only and never installs packages automatically.

## 0.1.5 - 2026-06-16

- Added a large Package Installer destination card that shows setup availability, progress, and the final open action.
- Moved the Package Installer launch affordance out of the setup action card so Bootstrap stays focused on setup and repair.
- Increased the preferred setup hub window height to fit the destination card in the default floating window.

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
