# Deucarian Bootstrap Agent Notes

Package ID: `com.deucarian.bootstrap`
Repository: `Deucarian/Bootstrap`

Follow the canonical Deucarian governance docs in [Package Registry](https://github.com/Deucarian/Package-Registry/blob/develop/ARCHITECTURE.md), especially capability ownership and dependency rules.

## Ownership

This package owns:

- First-time Deucarian setup, repair, and local fallback catalog bootstrapping.

This package must not own:

- Depend on Editor, Logging, Common, Package Installer, or any normal Deucarian package graph package.
- Grow broad ecosystem features beyond setup/repair.

## Dependencies

Allowed dependency shape:

- Self-contained only. New dependencies are not allowed without a governance decision.

Required dependencies and why:

- None.

Optional/version-defined dependencies:

- None.

Architecture exceptions:

- Bootstrap is the approved self-contained exception and may keep minimal local setup/fallback code.

## Policies

- Logging: Do not add Logging dependency; keep setup output minimal and local.
- Common: Do not add Common dependency; Bootstrap remains self-contained.
- Editor UI: May use local setup UI only; do not import shared Editor shell unless the self-contained exception is retired.
- Diagnostics: No Diagnostics dependency or upload/telemetry behavior.
- Testing: Tests may use direct Unity teardown for fixtures; do not create shared testing helpers.

## Validation

Run the shared validator before committing:

```powershell
python C:/Repositories/Package-Registry/Tools/deucarian_package_validator.py --registry-root C:/Repositories/Package-Registry --repository-root . --config deucarian-package.json
```

Also run existing repository tests when changing code or asmdefs. Documentation-only updates should still run `git diff --check`.

## Codex Guidance

- Inspect current files before changing anything.
- Work on `develop`; do not edit or merge `main` unless the task is promotion-only.
- Do not edit `Library/PackageCache`.
- Do not guess package versions or dependency versions.
- Do not add package dependencies casually; update asmdefs, `package.json`, `deucarian-package.json`, Package Registry, and fallback catalogs together when a dependency is truly required.
- Do not create local copies of shared helpers.
- Keep commits focused and report exactly what changed and what was validated.

## Before Adding Code

- Confirm the change fits this package's ownership boundary.
- Reuse existing local patterns and helpers.
- Avoid broad refactors without audit support.
- Preserve runtime/editor behavior unless the task explicitly asks to change it.

## Before Adding A Dependency

- Is the capability already owned by that package?
- Is it used by production code, editor code, sample code, or tests?
- Does the asmdef reference match `package.json`?
- Does `deucarian-package.json` need updating?
- Does Package Registry need updating?
- Does Package Installer fallback catalog need updating?
- Does Bootstrap fallback catalog need updating?
- Are exact versions propagated without guessing?

## Before Adding A Helper

- Is this package the capability owner?
- Is this behavior repeated in at least three production packages?
- Is there an existing owner package?
- Should this remain local?
- Has the audit been updated?

## Debug And Unity Object Lifetime

- Bootstrap remains self-contained; do not add Logging dependency or broad Debug usage without updating the Bootstrap exception.
- Bootstrap may keep minimal local setup cleanup under its self-contained exception; do not add Common casually.
- Test fixture teardown may use `DestroyImmediate` directly.
