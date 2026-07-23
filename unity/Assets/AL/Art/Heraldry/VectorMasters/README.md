# Arcane Axis Vector Masters

**Status:** Project-owner-approved flat and micro geometry, reconstruction `v001`

**Design authority:** [`FourRealmHeraldry.md`](../../Designs/FourRealmHeraldry.md)

**Approved raster reference:** [`heraldry_four_realm_arcane_axis_master_v001.png`](../ConceptSheets/heraldry_four_realm_arcane_axis_master_v001.png)

## Purpose

Provide deterministic, manually constructed source vectors for the approved Stonehold, Eldergrove, Crownlands, and Umbral Arcane Axis identities.

These files establish the approved editable geometry for flat and micro applications. They are not runtime sprites, final color authority, Unity realm mappings, atlas inputs, or ceremonial material renders.

**Owner approval:** 2026-07-23

**Reconstruction record:** [`Vector_Reconstruction_Provenance.md`](Vector_Reconstruction_Provenance.md)

**Approved review sheet:** [`arcane_axis_vector_review_v001.png`](ReviewSheets/arcane_axis_vector_review_v001.png)

## Files

| Realm | Standard flat master | Mobile micro master |
| --- | --- | --- |
| Stonehold | `arcane_axis_stonehold_flat_v001.svg` | `arcane_axis_stonehold_micro_v001.svg` |
| Eldergrove | `arcane_axis_eldergrove_flat_v001.svg` | `arcane_axis_eldergrove_micro_v001.svg` |
| Crownlands | `arcane_axis_crownlands_flat_v001.svg` | `arcane_axis_crownlands_micro_v001.svg` |
| Umbral | `arcane_axis_umbral_flat_v001.svg` | `arcane_axis_umbral_micro_v001.svg` |

Every file uses:

- A `256 × 256` coordinate system.
- Transparent background.
- `currentColor` for geometry so color can be assigned downstream without changing paths.
- No filters, raster images, text, external references, or blur-dependent identity.

## Reconstruction rules

- Flat masters target `48 px` and above.
- Micro masters target `24–47 px` and intentionally simplify the same protected identity.
- Geometry is manually rebuilt from the approved identity rather than automatically tracing raster irregularities.
- The micro masters enlarge central voids and widen critical gaps for a target minimum opening of approximately `2 px` at `32 px`.
- All marks maintain comparable optical footprint while retaining realm-specific symmetry and cadence.

## Protected geometry represented

### Stonehold

- Four orthogonal strata masses.
- Stable cardinal axes.
- Circular protected center with a detached ember-diamond focal shape.
- Micro version opens the inner corners and reduces secondary block steps.

### Eldergrove

- Three asymmetric rotating growth arcs.
- Central seed-shaped protected void.
- Uneven organic cadence within a circular overall flow.
- Micro version enlarges the seed void and broadens separation between arcs.

### Crownlands

- Four-point celestial meridian with stronger vertical authority.
- Centered diamond-like void.
- Four open orbital arc segments.
- Micro version broadens the void and orbital spacing.

### Umbral

- Offset eclipse ring.
- Diagonal severance with a narrow central shard.
- Two broad broken outer orbit arcs.
- No satellites, detached stars, or ceremonial nodes in either source master.

## Approval and runtime boundary

The geometry approval is complete. Before these masters become runtime production assets:

1. Confirm final realm colors and accessibility alternatives separately.
2. Select the first runtime surface.
3. Define rasterization, sprite, atlas, import, and catalog requirements.
4. Profile representative assets on the minimum supported iPhone.

Unity SVG import packages, sprite conversion, atlas grouping, catalog mapping, compression, mip behavior, device profiling, and runtime usage remain a later coordination and engineering decision.
