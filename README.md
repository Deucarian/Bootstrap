# Deucarian Bootstrap

`com.deucarian.bootstrap` is the official clean-project setup and repair route
for the Deucarian Package Installer ecosystem under the current Git-only
distribution model.

Bootstrap is editor-only, self-contained, and has zero Deucarian package
dependencies. It exists because a clean direct Git installation of
`com.deucarian.package-installer` cannot resolve its Git-distributed Deucarian
Editor and Logging dependencies transitively.

Current package version: `1.2.9`.

## Responsibilities

Bootstrap owns only:

- first-time installation of the setup closure;
- repair of missing or outdated setup packages;
- Stable/Development source and channel migration;
- read-only detection and safe migration of a legacy scoped-registry Package
  Installer installation;
- handoff to the normal Package Installer UI.

Bootstrap is not a second package browser. Normal package discovery,
installation, updates, removal, and ecosystem visualization happen in
`com.deucarian.package-installer`.

Bootstrap does not depend on or copy the general-purpose Editor shell, Logging,
Common, Diagnostics, Package Installer, or another Deucarian graph package.

## Install Bootstrap

Add Bootstrap through Unity Package Manager.

Stable:

```json
"com.deucarian.bootstrap": "https://github.com/Deucarian/Bootstrap.git#main"
```

Development:

```json
"com.deucarian.bootstrap": "https://github.com/Deucarian/Bootstrap.git#develop"
```

Unity 2021.3 or newer is supported.

After Unity imports the package, open:

```text
Tools > Deucarian > Bootstrap > Open Bootstrapper
```

Choose Stable or Development, review the three setup steps, and invoke the one
primary action. Opening Bootstrap, changing channel, or refreshing status never
installs, removes, or changes packages automatically.

### Advanced direct Package Installer route

Installing Package Installer directly by Git URL is an advanced/manual route.
Use it only when `com.deucarian.editor` and `com.deucarian.logging` are already
resolvable in the project. Bootstrap remains the supported entry point for a
clean project.

## Deterministic setup workflow

Bootstrap performs the reviewed setup closure in this order:

1. resolve the selected project channel;
2. load and validate remote Package Registry metadata or the exact bundled
   fallback;
3. inspect installed packages and `Packages/packages-lock.json`;
4. persist one authoritative plan with exact Git references;
5. install Deucarian Editor;
6. install Deucarian Logging;
7. install or migrate Package Installer last;
8. re-list packages after every operation or reload;
9. verify Package Installer source, selected branch, and lock revision;
10. offer **Open Package Installer**.

The persisted operation records the selected channel, exact plan references,
completed packages, pending package, action kind, retry count, and final
verification phase. Unity Package Manager request wrappers are treated as
transient. After a script/domain reload, Bootstrap discards them, performs a
fresh package list, accepts an already-observed effect, or safely reissues the
same idempotent operation.

Only Package Installer is removed, and only when a legacy/non-Git source must be
migrated. Editor and Logging are reconciled with `Client.Add` from their selected
Git references.

## Registry and fallback

Stable metadata:

```text
https://raw.githubusercontent.com/Deucarian/Package-Registry/main/packages.json
```

Development metadata:

```text
https://raw.githubusercontent.com/Deucarian/Package-Registry/develop/packages.json
```

Bootstrap validates the dependency graph and requires the exact setup closure:

```text
Deucarian Editor -> Deucarian Logging -> Deucarian Package Installer
```

Every selected setup reference must also target the selected `#main` or
`#develop` branch. A remote catalog with a mismatched branch is invalid and
cannot replace the bundled fallback.

If the remote Registry is unavailable or invalid, Bootstrap keeps and uses its
validated package-local fallback. Invalid remote data never replaces a valid
fallback. The fallback deliberately carries branch URLs rather than moving
package-version or commit claims.

The fallback also carries the dependency-first metadata needed to expose the
Activity Visualization, WebGL Command Routing Integration, Viewer Navigation,
Web Viewer Suite, and Web Viewer Template package IDs to review tooling. These
extra catalog entries do not change Bootstrap's setup plan: Bootstrap still
installs exactly Editor, Logging, and Package Installer, then hands normal
package discovery to Package Installer.

## Channel state

Stable maps to Git `#main`; Development maps to Git `#develop`.

Bootstrap and Package Installer share the same project-scoped selection:

```text
Deucarian.PackageManagement.SelectedChannel.<project-hash>
Deucarian.PackageManagement.SelectedChannelChangedAt.<project-hash>
```

The paired UTC-ticks timestamp keeps Bootstrap changes synchronized with
Package Installer's per-package channel overrides. The older
`Deucarian.Bootstrap.Channel.<project-hash>` key remains a read-only fallback
when no shared selection exists.

The channel is frozen for the duration of an active operation and restored from
persisted authoritative state after reload.

## Health states

Package Installer health uses Unity Package Manager data and
`Packages/packages-lock.json`.

- **Missing** — Package Installer is not installed.
- **Wrong source** — the installed Git repository does not match the selected
  Package Installer target.
- **Wrong channel** — the installed branch differs from Stable/Development.
- **Outdated** — the selected source/channel matches, but the installed lock
  hash differs from the selected remote branch tip.
- **Review required** — Git source, lock hash, or selected remote revision is
  unverifiable.
- **Healthy** — source and branch match, and the full lock revision equals the
  resolved remote branch revision.

The installed `package.json` version is shown for context only. It does not
replace revision-aware verification.

## Legacy scoped registry

Scoped-registry distribution is legacy and read-only in Bootstrap. Bootstrap
may inspect `Packages/manifest.json` to explain an existing installation, but it
never adds, removes, or edits `scopedRegistries`.

When Package Installer itself came from a legacy registry source, the explicit
**Migrate** action removes that package and adds the selected Git reference.
Unrelated manifest and scoped-registry configuration remains untouched.

## User interface

Bootstrap uses a self-contained UI Toolkit surface with package-local resources.
It follows the current Deucarian spacing, surface, border, semantic-status, and
button hierarchy without importing the shared Editor framework.

- Narrow: below 900 px
- Compact: 900 through 1179 px
- Wide: 1180 px or above

The preferred floating footprint is 560 x 820 px, with a 480 x 460 px minimum
for smaller docks. An open, destination-first hero makes Package Installer the
clear outcome. Editor and Logging appear as supporting requirements in three
transforming setup items whose spacing and hairlines imply structure without a
stack of heavy containers. The action dock appears only when there is something
the user can do, so loading and installation never show a duplicate disabled
action.

During review and installation, the setup items explain and then track the
dependency-first closure. Once the closure is authoritatively verified, they
collapse into a quiet three-check receipt with source, channel, and revision
context. **Open Package Installer** is the only primary handoff, and a successful
handoff closes Bootstrap so normal package work continues in Package Installer.
Bootstrap remains available from its menu for an explicit repair or migration.
Exact Git URLs, source, lock revision, fallback notice, startup preference, and
manual refresh are progressively disclosed under **Details**. Light and dark
Unity skins have separate accessible token sets. Action and status imagery
comes from package-local Lucide-derived assets with attribution in
`Editor/Assets/Icons/Lucide/LICENSE.md`.

## Architecture

The EditorWindow is a composition and lifecycle boundary only. Production
responsibilities are split into focused components:

- immutable setup state, health, operation, and plan models under
  `Editor/Domain`;
- pure setup planning and status policy under `Editor/Domain`;
- validated remote/fallback Registry loading under `Editor/Infrastructure`;
- Unity Package Manager list/add/remove adapters under `Editor/Infrastructure`;
- package-lock and installed-source inspection under `Editor/Infrastructure`;
- Git branch revision resolution under `Editor/Infrastructure`;
- versioned SessionState operation persistence under `Editor/Infrastructure`;
- the deterministic setup coordinator under `Editor/Application`;
- handoff through `Tools > Deucarian > Tools and Quality > Package Installer` under
  `Editor/Application`;
- Bootstrap-specific presentation models and responsive UI under
  `Editor/Presentation` and `Editor/UI`.

Ordinary services use constructor injection. Unity static APIs are isolated to
small adapters and the EditorWindow composition boundary.

Automatic startup is project-scoped. Bootstrap retires its welcome only after
the coordinator publishes both the `Healthy` phase and a fully healthy report.
That preference never blocks explicit menu opening or persisted operation
recovery after a script/domain reload, and completing one project does not
change the welcome behavior of another project.

## Public API

Bootstrap is primarily a Unity editor workflow rather than a runtime API.

- `DeucarianBootstrapPackageConstants` is the public identity, version, menu,
  documentation, and package-local asset-path contract.
- `Tools/Deucarian/Bootstrap/Open Bootstrapper` is the public editor entry.

The coordinator, persistence, Registry/UPM adapters, presentation model, view,
and window implementation are internal and may evolve without becoming a
general editor framework.

## Validation

Run the shared package validator from the repository root:

```powershell
python C:/Repositories/Package-Registry/Tools/deucarian_package_validator.py --registry-root C:/Repositories/Package-Registry --repository-root . --config deucarian-package.json
```

Verify the generated fallback projection:

```powershell
python C:/Repositories/Package-Registry/Tools/project_package_catalogs.py --registry-root C:/Repositories/Package-Registry --bootstrap-root . --check
```

Also run the complete Bootstrap Unity EditMode suite, compile the package in the
available supported Unity baseline, inspect Narrow/Compact/Wide in both editor
skins, and run:

```powershell
git diff --check
```

Canonical architecture and capability ownership are maintained in the
[Deucarian Package Registry](https://github.com/Deucarian/Package-Registry/blob/develop/ARCHITECTURE.md).

## License

See [LICENSE.md](LICENSE.md). Third-party icon attribution is recorded beside
the vendored icon assets.
