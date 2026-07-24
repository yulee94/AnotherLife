# Cross-Platform Design Handoff

**Status:** Active Android and Windows application contract

**Unity baseline:** `2022.3.62f3`

**Primary design authority:** [`../../DESIGN.md`](../../DESIGN.md)

**Approved source:** PRs `#276` through `#279` and the project-owner-approved mystical-medieval `AL` icon

## Purpose

Give every contributor one platform-neutral way to apply the approved Another Life visual direction without depending on the iOS branch, an SVG package, a macOS path, or a private generation session.

This handoff does not redesign any approved source. It distinguishes production reference from build-ready derivatives:

| Source group | Repository role | Android / Windows use |
| --- | --- | --- |
| [`DESIGN.md`](../../DESIGN.md) | Canonical style and production contract | Read before creating or integrating visual work |
| [`App_Icon_Mystic_Medieval_AL.png`](../Assets/AL/Art/App_Icon_Mystic_Medieval_AL.png) | Approved 1024 × 1024 RGB application icon | Assigned to Windows Standalone and Android single-layer launcher slots |
| [`AndroidAdaptive`](Branding/AndroidAdaptive/README.md) | Android-specific foreground, background, monochrome, mask review, and provenance | Colored layers assigned to Android adaptive slots; monochrome retained for a later themed-icon integration path |
| [`App_Icon_Mystic_Medieval_AL_Source_1254.png`](Branding/README.md) | Retained owner-supplied source and provenance | Source/archive only; never reference from a Player build |
| [`FourRealmChampionAnchor.md`](../Assets/AL/Art/Designs/FourRealmChampionAnchor.md) and its sheets | Approved Champion model direction | Modeling reference only; not a runtime texture or finished model |
| [`FourRealmHeraldry.md`](../Assets/AL/Art/Designs/FourRealmHeraldry.md) | Approved realm-symbol direction | Apply through the committed tintable PNG sprites |
| [`VectorMasters`](../Assets/AL/Art/Heraldry/VectorMasters/README.md) | Locked editable heraldry geometry | Source only; no Unity SVG dependency is required |
| [`RuntimeExports`](../Assets/AL/Art/Heraldry/RuntimeExports/README.md) | Platform-ready white-alpha sprites | Direct Unity Sprite assets for Android and Standalone |
| [`Terrestrials`](Terrestrials/README.md) and concept sheets | Approved creature design source | Modeling reference only; not a runtime texture or finished creature |

## Teammate application

1. Clone the repository and run `git lfs pull`.
2. Open the `unity` folder with Unity `2022.3.62f3`.
3. Use `Another Life > Design > Apply Cross-Platform Asset Settings` only if import or Player Settings were locally reset. The committed metadata and `ProjectSettings.asset` already contain the expected result.
4. Use `S_ArcaneAxis_*_Micro_32_v001.png` for `24–47 px` presentation and `S_ArcaneAxis_*_Flat_256_v001.png` for `48 px` and larger presentation.
5. Tint heraldry through the consuming UI or sprite renderer. Do not bake a realm color into these geometry assets while final color tokens remain unapproved.
6. Treat all Champion and terrestrial concept sheets as source reference. Build version-linked meshes, materials, textures, rigs, LODs, and prefabs before runtime use.
7. Preserve every committed `.meta` file. Do not regenerate GUIDs, rename approved sources, auto-trace the raster review sheets, or add an SVG importer merely to consume the realm marks.

## Platform settings

### Heraldry sprites

- Texture type: `Sprite (2D and UI)`, single sprite, full-rectangle mesh.
- Color: sRGB white RGB with transparent alpha; realm color is applied downstream.
- Mipmaps: disabled because these are fixed-scale interface glyphs.
- Read/write: disabled.
- Wrap: clamp.
- Android override: RGBA32, uncompressed, exact `32` or `256` maximum size.
- Standalone override: RGBA32, uncompressed, exact `32` or `256` maximum size.
- Runtime memory ceiling if all eight textures are resident: `1,064,960` bytes (`1.02 MiB`) before engine overhead.

The uncompressed override is intentional for this first eight-glyph set. It avoids block-compression damage on high-contrast transparent edges and avoids unsupported-format fallback on older Android hardware. Revisit compression only with visual comparison and device memory evidence. Atlas construction remains deferred until the first consuming surface and color/accessibility tokens are approved.

### Application icon

- Shared Unity icon: `1024 × 1024`, RGB, no alpha, mipmaps disabled.
- Windows: assigned to every Standalone application-icon size; Unity derives the executable icon during the Windows build.
- Android: assigned to every supported single-layer legacy and round slot in Unity `2022.3.62f3`.
- Android adaptive foreground: `432 × 432` RGBA, transparent outside the engraved `AL`, with a `258 × 228` visible mark inside the centered `264 × 264` safe zone.
- Android adaptive background: `432 × 432` opaque RGB, full-bleed midnight-indigo stone and restrained celestial geometry.
- Android monochrome: `432 × 432` white-alpha source matching the foreground silhouette. It is not assigned because Unity `2022.3` exposes only the two colored adaptive layers.
- Stretching or duplicating the full ornate square icon into both adaptive layers remains prohibited.

The adaptive packet is integrated as a runtime candidate. Its committed circle, squircle, and rounded-square preview is the project-owner approval checkpoint before promotion to the approved brand set.

## Compatibility and performance boundary

- No runtime C#, gameplay, scene, save, catalog, or material authority is added.
- No Unity package or native dependency is added.
- PNG and `.meta` paths use portable casing and separators; no machine-local path is committed.
- Large Champion, terrestrial, heraldry-review, and provenance images remain unreferenced source art and therefore are not included in a Player merely because they are stored under `Assets`.
- The eight heraldry PNGs add approximately `31 KB` of LFS source data and at most `1.02 MiB` of uncompressed texture memory if a future surface loads every sprite simultaneously.
- Application icon and adaptive launcher sources add no per-frame runtime texture or draw-call cost.

## Validation contract

The EditMode fixture `AL.Tests.EditMode.DesignAssets.CrossPlatformDesignAssetTests` proves:

- the approved 1024px icon is present, no-alpha, non-readable, and assigned to Standalone;
- all supported single-layer Android icon slots use the approved icon;
- all two-layer Android adaptive slots use the mask-safe foreground and full-bleed background in Unity's expected layer order;
- Android adaptive source dimensions, safe-zone bounds, background opacity, monochrome silhouette, and importer settings remain valid;
- all eight heraldry sprites have the required dimensions, white-alpha pixels, sprite settings, and Android/Standalone RGBA32 overrides.

Before production release, additionally validate:

- a Windows x64 Player build and executable icon on Windows;
- an Android APK/AAB launcher icon on representative launchers;
- project-owner approval of the Android adaptive mask preview;
- Android themed-icon behavior after the build pipeline explicitly consumes the monochrome source;
- the first real heraldry surface at compact Android and Windows UI sizes;
- final realm colors in grayscale and common color-vision simulations;
- measured atlas, memory, loading, and overdraw behavior.

## Critical direction choices still open

- Project-owner promotion of the Android adaptive runtime candidate after mask review.
- The Unity upgrade or Android launcher-template route that will consume the monochrome themed-icon source.
- Final realm color tokens and accessibility alternatives.
- First heraldry runtime surface and realm-catalog mapping.
- Sprite atlas grouping after a real consumer exists.
- Minimum Android and Windows hardware floors and measured budgets.
- Production 3D models, textures, rigs, LODs, materials, and prefab integration for the approved Champion and terrestrial source.
