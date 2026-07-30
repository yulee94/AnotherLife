# Phase C4D Building Asset-Reference Authority

## Status

- Date: 2026-07-30
- Baseline: `main@a9a334fd92d24790efdbb2f3838342b0157a1c07`
- Primary mode: Codex engineering
- Related issue: #183
- Shared-file lock: none
- Runtime, save, balance, and production activation: unchanged
- User direction: begin building the remaining assets

This phase supplies one complete, common, non-production icon source for the
15 approved building identities. It resolves the technical-source input needed
by `buildings.asset_refs` without collapsing the existing realm-specific Town
Hall and Workshop production-model catalog into a singular common reference.

The user's direction authorizes creation of this reviewable source candidate.
Final visible Kingdom presentation, integrated playtest acceptance, and
release approval remain user-owned.

## 1. Decision

The common building `asset_ref` is a logical atlas-cell reference:

```text
<atlas Unity asset path>#<canonical building ID>
```

The selected atlas is:

| Property | Exact value |
| --- | --- |
| Unity path | `Assets/AL/Art/Buildings/RuntimeExports/S_Building_Icon_Atlas_1536x1024_v001.png` |
| Unity GUID | `8cfa4b19fc1e4475873c4ea7560dc9ad` |
| Raw PNG SHA-256 | `874bba1c9fa9ba8435dcf61b29eca2786c049e0abf7d899680011a22e481b3a8` |
| Dimensions | 1536 × 1024 |
| Grid | 5 columns × 3 rows |
| Color space | sRGB |
| Mipmaps | disabled |
| Maximum import size | 2048 |

`GameDataBuildingAssetReferences` owns the exact logical references, atlas
coordinates, GUID, byte hash, and ordinal relations. The common schema accepts
only those 15 references and rejects a correct icon paired with the wrong
building identity.

The atlas remains one Unity sprite asset in this source phase. Its exact
integer cell rectangles are authority for a later reviewed slicing or runtime
resolver. This phase does not add a loader, allocate runtime sprites, change a
scene, or select a first consuming UI.

## 2. Exact authored order and references

Pixel Y is measured from the top of the committed PNG.

| Order | Canonical ID | Legacy ID | Cell | Pixel rectangle `(x, y, w, h)` | Exact `asset_ref` suffix |
| ---: | --- | --- | --- | --- | --- |
| 0 | `town_hall` | `TownHall` | row 1, col 1 | `0, 0, 307, 341` | `#town_hall` |
| 1 | `farm` | `Farm` | row 1, col 2 | `307, 0, 307, 341` | `#farm` |
| 2 | `lumber_mill` | `LumberMill` | row 1, col 3 | `614, 0, 308, 341` | `#lumber_mill` |
| 3 | `quarry` | `Quarry` | row 1, col 4 | `922, 0, 307, 341` | `#quarry` |
| 4 | `gold_mine` | `GoldMine` | row 1, col 5 | `1229, 0, 307, 341` | `#gold_mine` |
| 5 | `barracks` | `Barracks` | row 2, col 1 | `0, 341, 307, 342` | `#barracks` |
| 6 | `academy` | `Academy` | row 2, col 2 | `307, 341, 307, 342` | `#academy` |
| 7 | `market` | `Market` | row 2, col 3 | `614, 341, 308, 342` | `#market` |
| 8 | `storehouse` | `Storehouse` | row 2, col 4 | `922, 341, 307, 342` | `#storehouse` |
| 9 | `forge` | `Forge` | row 2, col 5 | `1229, 341, 307, 342` | `#forge` |
| 10 | `stable` | `Stable` | row 3, col 1 | `0, 683, 307, 341` | `#stable` |
| 11 | `workshop` | `Workshop` | row 3, col 2 | `307, 683, 307, 341` | `#workshop` |
| 12 | `embassy` | `Embassy` | row 3, col 3 | `614, 683, 308, 341` | `#embassy` |
| 13 | `wall` | `Wall` | row 3, col 4 | `922, 683, 307, 341` | `#wall` |
| 14 | `watchtower` | `Watchtower` | row 3, col 5 | `1229, 683, 307, 341` | `#watchtower` |

The full reference for `farm`, for example, is:

```text
Assets/AL/Art/Buildings/RuntimeExports/S_Building_Icon_Atlas_1536x1024_v001.png#farm
```

Unknown IDs, case variants, separator variants, legacy IDs used as fragments,
wrong-building swaps, changed coordinates, another GUID, or changed image
bytes are invalid.

## 3. Visual-source boundary

The atlas is intentionally:

- common across all four realms;
- readable at small mobile UI sizes;
- neutral in faction identity;
- ordered exactly like the accepted building progression registry;
- free of text, logos, watermarks, characters, and unapproved narrative;
- separate from realm-specific production architecture.

The committed Town Hall and Workshop models remain authoritative for their
current realm/building/level production presentation. This common icon atlas
does not replace, flatten, recolor, or redirect those model bindings.

The procedural Kingdom board fallback remains current runtime presentation for
other building types. This source phase does not promote that procedural
implementation into game-data authority.

## 4. Generation prompt

The built-in image-generation path produced the committed atlas from this
prompt:

```text
Use case: stylized-concept
Asset type: game UI sprite atlas for the Unity project Another Life
Primary request: create one cohesive production-quality atlas containing exactly fifteen distinct medieval-fantasy kingdom building icons arranged in a strict 5-column by 3-row grid.
Scene/backdrop: one perfectly uniform very dark charcoal background (#11151d) across the full canvas, with clear equal gutters between cells; no scenery outside the icons.
Subjects and exact grid order, left to right:
Row 1: Town Hall, Farm, Lumber Mill, Quarry, Gold Mine.
Row 2: Barracks, Academy, Market, Storehouse, Forge.
Row 3: Stable, Workshop, Embassy, Wall, Watchtower.
Style/medium: polished hand-painted 3D-isometric mobile strategy-game UI icons; neutral shared kingdom language that works across Stonehold, Eldergrove, Crownlands, and Umbral; readable at small size; grounded materials; strong silhouettes; restrained gold accents; no faction-specific crests.
Composition/framing: every icon centered in an equal square cell, same isometric camera angle and scale, generous internal padding, no overlap, no cropped buildings, identical lighting direction, visually consistent set.
Lighting/mood: warm readable key light with cool subtle rim light; adventurous and premium but not ornate.
Color palette: stone, timber, iron, muted earth tones, restrained gold; preserve distinct semantic cues for each building.
Materials/textures: believable masonry, wood, metal, cloth, vegetation, ore and market goods where relevant; avoid tiny clutter.
Constraints: exactly 15 icons and exactly the specified grid order; no words, letters, numbers, labels, banners with writing, logos, trademarks, watermarks, characters, creatures, weapons floating outside buildings, decorative border around the entire atlas, or extra icons. Each cell must depict only its named building. The Town Hall and Workshop should feel compatible with existing four-realm architectural families without copying a single realm-specific model. Keep the background flat and consistent so the atlas can be reviewed and sliced later.
Avoid: photorealism, cartoon chibi proportions, neon colors, excessive glow, illegible silhouettes, repeated buildings, merged cells, perspective inconsistency, UI text.
```

Changing the prompt, image bytes, cell order, protected identity relation, or
atlas path requires a new version and review.

## 5. Blocker disposition

| Blocking ID | C4D disposition |
| --- | --- |
| `buildings.asset_refs` | **Source resolved for future non-production generation.** Fifteen exact logical references map the accepted building identities to one committed, pinned atlas. Runtime slicing/loading, first consumer, residency, compression, and final visible approval remain later activation decisions. |
| `buildings.production_profiles` | **Open.** No production rate, profile, live/offline migration outcome, or balance value is changed or approved here. |
| `approval.user_creative_balance` | **Open.** The user authorized starting the asset work, while final creative, balance, integrated playtest, milestone, and release acceptance remain separate. |

A future v004 technical-source overlay may remove only
`buildings.asset_refs`. The building family remains `blocked_required` because
`buildings.production_profiles` remains open, and the six-family candidate
remains non-production because other family blockers and the top-level user
gate remain.

## 6. Pinned prior evidence

| Source | Source revision | Raw SHA-256 | Role |
| --- | --- | --- | --- |
| `Phase_C4A_Building_Authority_Convergence.md` | `c0d27a4c247615e33f1ed189b789e99bbf1355ac` | `b94895911e46cfd03dfb08b15e3c4ccf860a028ffe62d922c95e564fd2e5e039` | Prior building authority and retained blocker boundary |
| `GameDataBuildingProgressionRegistry.cs` | `a2e6a9a0dddfb7522d880d4db9d17222adcbbffe` | `319cb9f97cff850c3e0f79c30ae877c2876ecab6cf70d9fa681a672be4b430c4` | Exact 15-building order, IDs, aliases, and content relations |
| `CityLayoutEngine.cs` | `1ed89d147b324345abe8703e88c84146ff533a44` | `5dbc56783778f804f2c122bc78f76f030b6ae8fc4a37cf4d6e1f39c9d1196647` | Current procedural fallback boundary, not promoted |
| `KingdomBuildingModelCatalog.asset` | `8104d0cb58c6cf38b64dadd1b6ec452e007dd091` | `917dfa8febd26f34b7d1d87b0f1aff821121b9aa3b52c585e8114bcb8170fd55` | Current realm-specific Town Hall/Workshop model boundary |

## 7. Acceptance

- [x] all 15 approved building identities have one exact logical asset
  reference;
- [x] atlas order matches the progression registry exactly;
- [x] the committed GUID, dimensions, importer limits, and raw bytes are
  pinned;
- [x] cell rectangles cover the full atlas without gaps or overflow;
- [x] case, separator, wrong-building, and unavailable-anchor variants fail
  closed;
- [x] the common schema accepts only reviewed building asset relations;
- [x] realm-specific Town Hall/Workshop model bindings are unchanged;
- [x] no runtime loader, scene, save, balance, production provider, package,
  dependency, or consumer changed.

## Impact

This phase adds one 1536 × 1024 PNG plus immutable registry/schema/test and
coordination evidence. Mipmaps are disabled and the import cap is 2048.
Because no runtime consumer is added, there is no new frame-loop, allocation,
draw-call, network, save, or gameplay cost in this phase. Player residency,
atlas slicing, compressed device bytes, low-end memory, and visible UI
evidence remain required before activation.
