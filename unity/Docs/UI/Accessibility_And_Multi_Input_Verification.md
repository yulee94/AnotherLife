# Accessibility and multi-input verification

This checklist covers the production HUD, inner-realm minimap, and expanded world map. Automated gates are authoritative where listed. Device and visual observations must be recorded against a PR-identical build; unchecked manual rows are not evidence of a pass.

## Automated gates

Run with Unity 6000.3.22f1 and read the NUnit XML totals rather than relying on the process exit code.

- `AL.Tests.EditMode.UI`: fixed safe-area hierarchy and protected PvP scan path on phone, tablet, 16:9 PC, and ultrawide; 2x text stress; semantic non-color cues; deterministic focus filtering, wrap navigation, keyboard submit, restored focus, and stable text scaling.
- `AL.Tests.EditMode.WorldMap`: progressive disclosure and authority, semantic labels/shapes, responsive map layout, large minimap text, and reduced motion/flash/VFX suppression for nonessential objective effects.
- `AL.Tests.PlayMode.WorldMap`: modal initial focus, visible focus treatment, minimum close-target size, submit activation, cancel/back close, restored prior focus, and overlay lifecycle repair.
- `tools/ui/validate_ui_design_system.py`: authored form factors, semantic states, reusable HUD components, and protected scan-path geometry.

## Manual device matrix

Record build identifier, device/viewport, input device, observer, result, and evidence path for every row.

| Surface | Phone landscape | Tablet landscape | 16:9 PC | Supported ultrawide |
|---|---|---|---|---|
| HUD at 100% and 200% text | [ ] | [ ] | [ ] | [ ] |
| Minimap standard and expanded | [ ] | [ ] | [ ] | [ ] |
| World map open/close and safe area | [ ] | [ ] | [ ] | [ ] |
| Dense combat with protected cues | [ ] | [ ] | [ ] | [ ] |
| Reduced motion + flash + VFX | [ ] | [ ] | [ ] | [ ] |

For each applicable surface:

1. Touch every interactive target at its center and near each edge. Confirm one activation per press and no overlap with adjacent targets.
2. Navigate with keyboard and controller from the prior gameplay/menu control into the world-map modal. Confirm the close control receives initial focus, focus remains visibly contained, directional navigation does not escape, submit activates it, and closing restores the prior valid control.
3. Use Escape, controller cancel/back, and the world-map toggle. Confirm each closes the expanded map without leaving gameplay suppressed or focus missing.
4. Resize or rotate through the four supported compositions while focus is active. Confirm hidden, disabled, and off-screen variants are skipped.
5. Enable 200% text. Confirm vitals, hostile telegraphs, allegiance, objectives, and route cues remain readable and the protected central PvP scan path stays clear.
6. Check health, hostility, allegiance, objective, route, discovery, and correction/awaiting states in grayscale. Confirm label prefix, shape, border, or pattern communicates each state without color.
7. Enable reduced motion, reduced flash, and reduced VFX separately and together. Confirm decorative/pulsing map effects become static while semantic labels and essential combat warnings remain present.
8. Confirm focus outlines remain visible against the darkest and brightest supported scene backgrounds and are not clipped by safe-area edges.

## Stop-ship conditions

- Any supported input cannot reach or activate an interactive control.
- Focus escapes the modal, lands on hidden/disabled/off-screen content, or is not restored after close.
- A touch target is smaller than the production token minimum or overlaps another target.
- 200% text obscures protected PvP cues or becomes clipped/unreadable.
- A semantic state depends on color alone.
- Reduced-effect preferences leave nonessential motion/flash/VFX active or remove an essential combat warning.
