# AnotherLife Collaborator Asset Library

Status: `PUBLIC_COLLABORATOR_SOURCE / NON_PRODUCTION`

This index separates AnotherLife's published visual sources by domain so collaborators do not need to download one monolithic archive. The GitHub repository and the releases below are public. Publication makes the files available for collaboration; it does not turn review material into an approved Unity runtime asset.

## Download map

| Domain | Contents | GitHub release | State |
| --- | --- | --- | --- |
| Terrestrial 2D | Current creature/fauna concepts, corrected and rejected review evidence, and historical queues | [terrestrial 2D v001](https://github.com/yulee94/AnotherLife/releases/tag/collab-assets-terrestrial-2d-2026-08-17-v001) | Mixed review states; read the manifest before reuse |
| Terrestrial 3D | Creature GLBs, source packages, and model-review evidence | [terrestrial 3D v001](https://github.com/yulee94/AnotherLife/releases/tag/collab-assets-terrestrial-3d-2026-08-17-v001) | Mixed rejected, changes-required, provisional, incomplete, and source-only states |
| Kingdom 2D / 2.5D | Private-Kingdom management-mode structure, settlement, and role planning images | [Kingdom 2D and 2.5D v001](https://github.com/yulee94/AnotherLife/releases/tag/collab-assets-kingdom-2d-25d-2026-08-17-v001) | Planning and visual review source; not a runtime Kingdom kit |
| Realm architecture 3D | Four retained realm gate models and review evidence | [realm architecture 3D v001](https://github.com/yulee94/AnotherLife/releases/tag/collab-assets-realm-architecture-3d-2026-08-17-v001) | Review required; a retained model is not an approved castle or fortress |
| World-kit 3D | Common pebble, grass, pine, rock prototype/source material and textures | [world-kit 3D v001](https://github.com/yulee94/AnotherLife/releases/tag/collab-assets-worldkit-3d-2026-08-17-v001) | Source evidence only; v002 Unity/runtime preparation remains separate |
| Champion 3D | Crownlands champion T-pose and model variants | [champion 3D v001](https://github.com/yulee94/AnotherLife/releases/tag/collab-assets-champion-3d-2026-08-17-v001) | Review required; no topology, rig, customization, or production approval |
| Realm dragons 3D | Stonehold, Eldergrove, Crownlands, Umbral, Wish Dragon, and comparison sources | [realm dragons 3D v001](https://github.com/yulee94/AnotherLife/releases/tag/collab-assets-realm-dragons-3d-2026-08-17-v001) | Review and source lineage only |
| Equipment 3D | Nine previously published weapon/equipment GLBs, referenced without uploading duplicates | [equipment 3D index v001](https://github.com/yulee94/AnotherLife/releases/tag/collab-assets-equipment-3d-2026-08-17-v001) | Manifest points to the original retained release |
| First-user planning | First-user visual planning assets and presentation references | [first-user planning v001](https://github.com/yulee94/AnotherLife/releases/tag/collab-assets-first-user-planning-2026-08-17-v001) | Planning only; not an admitted first-user environment |

The earlier mixed retained release remains available at [Meshy review assets 2026-08-12 v001](https://github.com/yulee94/AnotherLife/releases/tag/meshy-review-assets-2026-08-12-v001). Its nine terrestrial and nine equipment GLBs are referenced from the new category manifests instead of being uploaded again.

## Assets already stored in GitHub

These versioned repository paths were already public and remain the canonical in-repository sources. They were not duplicated into the downloadable releases.

| Repository path | Non-meta files | What it contains |
| --- | ---: | --- |
| `unity/Assets/AL/Art/Champions/` | 7 | Four realm Champion turnaround sources plus provenance |
| `unity/Assets/AL/Art/Generated/Architecture/` | 351 | 268 Unity mesh assets, 48 materials, 27 prefabs, and 8 textures, including existing realm TownHall/Workshop production families |
| `unity/Assets/AL/Art/Heraldry/` | 22 | Eight exact Arcane Axis SVG masters, ten PNG exports/review sheets, and provenance/readmes |
| `unity/Docs/Terrestrials/` | 116 | 56 concept PNGs, 18 manifests/contracts, and 42 design or review documents |
| `unity/Assets/AL/Art/Designs/` | 9 | Approved and provisional visual-language documents |

## Review-state rules

- `REJECTED`, `NOGO`, `REPLACEMENT_REQUIRED`, and `REJECTED_*` files are shared as correction evidence. Do not bind them to a runtime identity.
- `HISTORICAL`, `SUPERSEDED`, and `SOURCE_ONLY` files are reference material. They do not replace the latest source packet.
- `PROVISIONAL`, `REVIEW_PENDING`, `CHANGES_REQUIRED`, and `INCOMPLETE` files remain open review candidates.
- `NON_PRODUCTION_COLLABORATOR_SOURCE` means a collaborator may inspect, discuss, branch from, or improve the source while preserving lineage. It does not grant final visual, terrestrial-source, runtime, rights, balance, milestone, integrated-playtest, or release approval.
- Use the SHA-256 values in each release manifest when downloading or handing off a file. Do not substitute a similarly named file.

## Known planned or missing asset families

The following requested families are not falsely represented as completed assets in this library:

- complete plain-body and mixed-origin character-customization model coverage across Human, Dwarf, Elf, and Dark Elf;
- final realm-symbol 3D extrusions and realm-specific Unity VFX bindings;
- complete flowers, shrubs, ores, biome dressing, roads, trails, bridges, fences, ruins, and settlement-decoration runtime kits;
- a complete private 2.5D Kingdom structure kit for the bounded unlocked-territory cell grid;
- traversable public-world castles and fortresses for all four realms and habitats;
- standard guards, one-per-site Guard Captains, and distinct Castellan/Aristocrat variants for every realm;
- admitted first-user champion, enemy, private-Kingdom, and neutral-environment assets that satisfy the sealed Game Test gate.

## Collaborator workflow

1. Pick one domain release instead of downloading every archive.
2. Read the category manifest first and verify its hash.
3. Preserve the source asset ID, task/model lineage, disposition, and rejection reason when creating a derivative.
4. Put a derivative on a focused branch and PR. Never overwrite rejected or historical evidence.
5. Keep large binaries in Git LFS or a GitHub release, not ordinary Git blobs.
6. Require source/design fidelity review and Unity technical admission separately before runtime use.

The machine-readable release catalog and coverage audit are adjacent to this document. `tools/assets/Build-CollaboratorAssetRelease.ps1` records the deterministic packaging and deduplication procedure; its three roots are required parameters so no local workstation path is part of the published contract.
