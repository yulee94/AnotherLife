# Arcane Axis Vector Reconstruction — Provenance

## Status

- Reconstruction: `v001`
- Geometry status: Project-owner approved
- Owner approval date: 2026-07-23
- Design authority: [`FourRealmHeraldry.md`](../../Designs/FourRealmHeraldry.md)
- Raster identity reference: [`heraldry_four_realm_arcane_axis_master_v001.png`](../ConceptSheets/heraldry_four_realm_arcane_axis_master_v001.png)
- Review sheet: [`arcane_axis_vector_review_v001.png`](ReviewSheets/arcane_axis_vector_review_v001.png)
- Runtime authority: None
- Final color authority: None

## Construction method

Codex manually reconstructed all eight SVG masters from the protected Arcane Axis geometry. No automatic raster trace, raster embedding, external symbol library, or third-party emblem was used.

Every source master:

- Uses a transparent `256 × 256` coordinate system.
- Uses `currentColor` so geometry and color authority remain separate.
- Contains no visible text, raster image, external reference, filter, blur, texture, glow, or animation.
- Retains a separate flat and mobile-micro path treatment.
- Is stored inside the Unity art-source tree as a source-only default asset; no SVG package or runtime importer was added.

## Approved geometry checksums

| Realm | Master | SHA-256 |
| --- | --- | --- |
| Crownlands | `arcane_axis_crownlands_flat_v001.svg` | `d8cffdd7f8233e55036bef709c53e740f2a312972e299030bca2e81727afff16` |
| Crownlands | `arcane_axis_crownlands_micro_v001.svg` | `6c3430905d2368a75a7f11fe357cb0429588519ddaaf9942b16881e2b6813d7e` |
| Eldergrove | `arcane_axis_eldergrove_flat_v001.svg` | `665d4a85cf833dc83d4b45264dd05ee4057d3731a47f79ac4a5eaae4b07f4d06` |
| Eldergrove | `arcane_axis_eldergrove_micro_v001.svg` | `8f36ec9a149892bfaddbe8a21d89a11d64d4c4d407127e0ddfb3b7a910cb0915` |
| Stonehold | `arcane_axis_stonehold_flat_v001.svg` | `a93d594df61e5161c8d846c80411bbd26296f0cfcc572cf4e2c1a3c99623d040` |
| Stonehold | `arcane_axis_stonehold_micro_v001.svg` | `344c3d18ad4ed5d10946c7ee9641d2c2bca751aa35610fbf5325eda98ab22ad0` |
| Umbral | `arcane_axis_umbral_flat_v001.svg` | `2f453dc41a2f87fb83762d798b58d464f6a601a6a34622944b78fe1a69345614` |
| Umbral | `arcane_axis_umbral_micro_v001.svg` | `5780e3222244b08e1cdf5b902ef129a5f7e146188e4873157e2e8a0f22e3147c` |

Review-sheet SHA-256:

`d4377652771a58755ee09ad5b99655be9d1d69282125d9a95a69a563081dd887`

## Review process

The reconstruction was compared directly with the approved raster flat references and rendered at:

- `24 px`
- `32 px`
- `48 px`
- `64 px`
- `128 px`
- `256 px`

Each size was rendered in provisional realm color, neutral grayscale, and inverse white-on-black, producing `72` exact-size review exports. Those generated exports were validation evidence and are not runtime assets.

The first comparison scored `72 / 100` and required:

- Replacing Eldergrove's sharp pinwheel-like arms with continuous living-orbit crescents.
- Rejoining Stonehold's strata toward its central axis.
- Restoring Crownlands' stronger vertical meridian.
- Retaining Umbral's already successful offset eclipse and severed orbit.

The corrected comparison scored `93 / 100` and passed. A final exact `24 px` review then simplified Stonehold's micro master to preserve four corner masses, cardinal axes, and its protected center without maze-like compression.

## Provisional review colors

The review sheet used these non-authoritative display colors:

| Realm | Review value |
| --- | --- |
| Stonehold | `#8E979F` |
| Eldergrove | `#899B55` |
| Crownlands | `#C2A064` |
| Umbral | `#8064A8` |

These values demonstrate geometry only. They must not be copied into runtime tokens until the separate color and accessibility decision is approved.

## Approval boundary

Project-owner approval locks the flat and micro `v001` paths. Future contributors may change value, scale, placement, and approved rasterization settings without changing the paths. Any geometry modification, realm variant, combined fifth symbol, ceremonial extension that changes the protected read, or replacement mark requires renewed project-owner approval.

Ceremonial materials, orbit nodes, satellites, texture, bevel, glow, animation, Unity import strategy, sprite and atlas settings, realm-catalog mapping, memory budgets, iPhone profiling, and commercial emblem clearance remain open.
