# Third-party notices

This notice describes the dependency and distribution inventory for `com.deucarian.bootstrap` `1.1.1`. It does not replace the repository's [MIT license](LICENSE.md), and it does not grant rights to software supplied separately.

## Review basis

The reviewed baseline is `origin/main` commit `8688fd4391d2f04ad2df54a510dc74d7b8f0c76f`. Its `npm pack --dry-run` inventory contained 66 package files. The tracked and packed inventories were checked for common vendor/third-party directories, compiled binaries and archives, Git submodules, Git LFS pointers, separate license markers, and media/font assets.

That inventory identified no files marked or located as vendored third-party source, no compiled binary/archive candidates, no submodules, and no LFS pointers.

## Direct package dependencies

The reviewed `package.json` declares no direct package dependencies. Bootstrap is intentionally self-contained and its fallback package catalog contains references rather than bundled copies of other packages.

## Included visual assets

The distribution includes six PNG assets under `Editor/Assets`. Repository history records their addition under the repository owner's identity, and no separate third-party license or attribution marker accompanies them.

| Content | SHA-256 / evidence |
|---|---|
| `Icons/DeucarianPackagePlaceholderIcon.png` | `496927481ff3d31f9b317ea98f2260baf14861d2d921d297803fae1ef4f9963c` |
| `Images/DeucarianBootstrapHeroBackground.png` | `11427277305e0e3958e00149645e062155844602a800c15804aca588612dda76` |
| `Images/DeucarianInstallerBackground.png` | `3939dcd950cf688438e84b5bd460d3d4c75cf4309024d61bf968e73d1e4956bd` |
| `Images/DeucarianPackageInstallerHero.png` | `754ba3487973604cc22156e7051ce4fb85905a2fffb2c5c09181df91e27d91db` |
| `Logos/DeucarianBootstrapLogo.png` | `423398b7081e97532454a07d04145770ff707b19aa96202495bcd5906a58718b` |
| `Logos/DeucarianPackageInstallerLogo.png` | `aa5df75de81ff70c4fdf69eb94b8d9c316ab8786a1d3483ce72a19a968c0f43f` |

The identical hashes shared with Editor trace to the same editor asset set. The repository does not contain independent source files, purchase records, or a provenance declaration proving how the PNGs were created, so this notice does not claim conclusive third-party-free provenance. Owner confirmation or replacement with approved final brand assets remains a publication and commercial-reuse gate.

## Host platform

The manifest requires Unity `2021.3`. Unity is not included in this package and is governed by the applicable [Unity Editor Software Terms](https://unity.com/legal/editor-terms-of-service/software).

Re-run the inventory and update this notice whenever dependencies or distributed content change.
