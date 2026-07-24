# Another Life Android Adaptive Icon

**Status:** Runtime candidate; project-owner mask review pending

**Unity baseline:** `2022.3.62f3`

**Brand authority:** [`../README.md`](../README.md) and the approved full application icon

## Purpose

Translate the approved mystical-medieval `AL` identity into Android's adaptive launcher format without stretching the ornate square icon or baking in one launcher mask.

Android launchers compose two independent `108 × 108 dp` layers and may crop them to a circle, squircle, rounded square, or device-specific shape. The essential foreground mark therefore remains inside the centered `66 × 66 dp` safe zone. At the committed `432 × 432` xxxhdpi source size, that safe zone is `264 × 264` pixels.

## Runtime assets

| Asset | Role | Import / assignment |
| --- | --- | --- |
| [`App_Icon_Android_Adaptive_Foreground_AL_432_v001.png`](../../../Assets/AL/Art/Branding/AndroidAdaptive/App_Icon_Android_Adaptive_Foreground_AL_432_v001.png) | Transparent engraved silver-and-gold `AL` | Unity adaptive layer `1` / exported Foreground |
| [`App_Icon_Android_Adaptive_Background_432_v001.png`](../../../Assets/AL/Art/Branding/AndroidAdaptive/App_Icon_Android_Adaptive_Background_432_v001.png) | Opaque midnight-indigo stone and restrained celestial geometry | Unity adaptive layer `0` / exported Background |
| [`App_Icon_Android_Monochrome_AL_432_v001.png`](../../../Assets/AL/Art/Branding/AndroidAdaptive/App_Icon_Android_Monochrome_AL_432_v001.png) | White-alpha themed-icon silhouette | Forward-compatible source; Unity `2022.3` exposes two adaptive Player Settings layers and does not assign this third source |

The foreground visible bounds are `258 × 228` pixels at `x 87–344`, `y 102–329`, fully inside the `x/y 84–347` safe zone.

The setup command `Another Life > Design > Apply Cross-Platform Asset Settings` assigns the colored adaptive foreground and background to every two-layer Android icon slot. Legacy and round single-layer slots continue to use the approved full application icon.

## Mask review

![Circle, squircle, and rounded-square launcher previews](Previews/App_Icon_Android_Adaptive_Mask_Preview_Sheet_v001.png)

The previews are review evidence only. Do not use masked preview PNGs as launcher inputs.

## Design contract

- Preserve the exact readable `AL` relationship: silver `A`, gold `L`, engraved medieval metal, and one dominant monogram.
- Keep the foreground independent and transparent. No cast shadow, glow, arch, frame, rune ring, or background texture belongs in this layer.
- Keep the background full-bleed and opaque. No hard frame, rounded corner, monogram, readable text, or critical edge detail.
- Keep foreground identity inside the `66/108` safe-zone ratio.
- Do not replace the approved full square application icon. This packet is an Android-specific derivative.
- Create `v002` or later for future changes; never overwrite this candidate or its provenance.

## Performance and compatibility

- The launcher layers are build metadata, not gameplay textures. They add no per-frame renderer, draw-call, scene-memory, or save-data cost.
- Mipmaps and CPU read/write are disabled.
- Android import overrides are uncompressed `RGBA32` for alpha layers and `RGB24` for the background to avoid edge artifacts during launcher export.
- No Unity package, Android plugin, custom manifest, or native dependency is added.
- The monochrome source is retained for a later Unity upgrade or an explicitly approved Android launcher-template path.

## Validation

`AL.Tests.EditMode.DesignAssets.CrossPlatformDesignAssetTests` verifies:

- all two-layer Android icon slots use the intended foreground/background order;
- all three runtime sources are exactly `432 × 432`;
- the foreground visible bounds remain inside the `264 × 264` safe zone;
- the background is fully opaque;
- the monochrome alpha matches the colored foreground silhouette;
- importer settings remain non-readable, mip-free, clamp-wrapped, and Android-compatible.

Before store release, also inspect an APK/AAB on at least one Pixel-style launcher and one manufacturer launcher, and verify themed-icon behavior after the build pipeline explicitly consumes the monochrome source.

## References

- [Android adaptive icon design](https://developer.android.com/develop/ui/compose/system/icon_design_adaptive)
- [Unity `PlayerSettings.SetPlatformIcons`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PlayerSettings.SetPlatformIcons.html)
- [Generation and provenance record](GENERATION.md)

## Integrity

```text
72c55a6188b8645278abe2018e6acc81fa97d5d77fb1ffbe55be0e7d8de9a691  App_Icon_Android_Adaptive_Background_432_v001.png
af7de5292981fc00665dcd0e46709462761c6d50197374033d9f9383f42fcd6a  App_Icon_Android_Adaptive_Foreground_AL_432_v001.png
ee8c6f92d11e21ab23bd7bb1950edcd3654eda4d8967ee6a625135f01721d585  App_Icon_Android_Monochrome_AL_432_v001.png
137251c94ea844636d66355e0425b2e24bb696ab3d5275c43d4e97e166d532cd  Previews/App_Icon_Android_Adaptive_Mask_Preview_Sheet_v001.png
```
