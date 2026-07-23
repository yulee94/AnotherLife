# Arcane Axis Runtime Exports

**Status:** Cross-platform geometry derivatives `v001`; final color and runtime mapping remain open

**Source authority:** [`../VectorMasters/README.md`](../VectorMasters/README.md)

**Application contract:** [`../../../../../Docs/Cross_Platform_Design_Handoff.md`](../../../../../Docs/Cross_Platform_Design_Handoff.md)

## Purpose

Provide Unity `2022.3.62f3` Sprite assets that Android and Windows contributors can consume without installing an SVG package. Each PNG is a white shape on transparent alpha so downstream UI can tint the approved geometry without creating color authority inside the texture.

## Asset matrix

| Realm | `48 px+` flat sprite | `24–47 px` micro sprite |
| --- | --- | --- |
| Stonehold | `S_ArcaneAxis_Stonehold_Flat_256_v001.png` | `S_ArcaneAxis_Stonehold_Micro_32_v001.png` |
| Eldergrove | `S_ArcaneAxis_Eldergrove_Flat_256_v001.png` | `S_ArcaneAxis_Eldergrove_Micro_32_v001.png` |
| Crownlands | `S_ArcaneAxis_Crownlands_Flat_256_v001.png` | `S_ArcaneAxis_Crownlands_Micro_32_v001.png` |
| Umbral | `S_ArcaneAxis_Umbral_Flat_256_v001.png` | `S_ArcaneAxis_Umbral_Micro_32_v001.png` |

## Import contract

- Single full-rectangle Sprite, centered pivot, `100` pixels per unit.
- sRGB, input alpha, alpha-is-transparency enabled.
- Bilinear filtering, clamp wrapping, no mipmaps, no read/write access.
- Exact-size RGBA32 override for Android and Standalone.
- No sprite atlas, realm catalog, final color, material, shader, or gameplay authority.

All eight textures occupy `1,064,960` raw RGBA bytes (`1.02 MiB`) if resident together, before engine overhead. This deliberate first-pass ceiling protects thin transparent edges from block artifacts. Compression or atlas changes require visual and device evidence.

## Checksums

```text
f5c7e351ec930aac69f6df02d03034bc38c465ed8dfa787dd4feba044f33f82b  S_ArcaneAxis_Crownlands_Flat_256_v001.png
5f604f91b3e18a891154421bb2b339a5c9b0ffa4a3a127e2410732e34e8390c3  S_ArcaneAxis_Crownlands_Micro_32_v001.png
1d45fc8fba82ebb3fdc1c4f819026ea8e45b11c248378371c7b2b6923c6e0cac  S_ArcaneAxis_Eldergrove_Flat_256_v001.png
3aba07673473d2d8cc15827a2f3b02880441d6ae4c82742deeda19b1f3d6e768  S_ArcaneAxis_Eldergrove_Micro_32_v001.png
53d220dc8b938d212963286133ca39e1968fa1421126559dd56bdfde9c437946  S_ArcaneAxis_Stonehold_Flat_256_v001.png
7b3446d52e09bff87d007d1e118283db75de37827c72bbf27a94cc084045d547  S_ArcaneAxis_Stonehold_Micro_32_v001.png
a9daefa3ea6445ba2db680dad92a456db75becebec8848c678b29d5ea2c85aaa  S_ArcaneAxis_Umbral_Flat_256_v001.png
ccb01ebc5eb68fbccd9951bd038fd99bca55626d83f9e00345f658065e9e8578  S_ArcaneAxis_Umbral_Micro_32_v001.png
```
