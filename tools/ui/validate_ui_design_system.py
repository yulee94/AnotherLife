#!/usr/bin/env python
"""Validate and render the AnotherLife UI design-system assets."""

from __future__ import annotations

import argparse
import hashlib
import html
import json
from pathlib import Path
from typing import Any

FORM_FACTOR_NAMES = {
    0: "PhoneLandscape",
    1: "TabletLandscape",
    2: "Pc16By9",
    3: "PcUltrawide",
}

SLOT_NAMES = {
    0: "PLAYER VITALS / CONTROL",
    1: "CURRENT TARGET / CAST / BREAK",
    2: "HOSTILE TELEGRAPHS — WORLD CUES ONLY",
    3: "PARTY / SUPPORT STATE",
    4: "OBJECTIVE / CONTEST / TIMER",
    5: "ROUTE / NEXT ANCHOR",
    6: "ALLEGIANCE / COMMAND",
}

SLOT_STATE = {
    0: 0,
    1: 7,
    2: 2,
    3: 1,
    4: 3,
    5: 0,
    6: 1,
}


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def rect_bounds(rect: dict[str, Any]) -> tuple[float, float, float, float]:
    x = float(rect["x"])
    y = float(rect["y"])
    width = float(rect["width"])
    height = float(rect["height"])
    return x, y, x + width, y + height


def overlaps(left: dict[str, Any], right: dict[str, Any]) -> bool:
    lx0, ly0, lx1, ly1 = rect_bounds(left)
    rx0, ry0, rx1, ry1 = rect_bounds(right)
    return lx0 < rx1 and lx1 > rx0 and ly0 < ry1 and ly1 > ry0


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def validate_tokens(tokens: dict[str, Any]) -> None:
    require(tokens.get("SystemId") == "al.ui.production.v1", "Unexpected token system ID")
    require(
        {entry["Role"] for entry in tokens.get("Typography", [])} == set(range(6)),
        "Typography roles 0-5 are required",
    )
    require(tokens.get("Spacing") == sorted(tokens.get("Spacing", [])), "Spacing must be ordered")
    require(
        tokens.get("ElevationLevels") == sorted(tokens.get("ElevationLevels", [])),
        "Elevation levels must be ordered",
    )
    require(float(tokens.get("MinimumHitTarget", 0)) >= 48, "Hit target is below 48 units")
    states = tokens.get("StateTreatments", [])
    require({entry["State"] for entry in states} == set(range(8)), "Semantic states 0-7 are required")
    for state in states:
        require(int(state.get("NonColorCue", 0)) != 0, f"State {state['State']} lacks a shape cue")
        require(int(state.get("Pattern", 0)) != 0, f"State {state['State']} lacks a pattern")
        require(bool(state.get("LabelPrefix")), f"State {state['State']} lacks a label prefix")
    reduced = tokens["ReducedMotion"]
    standard = tokens["Motion"]
    require(float(reduced["PanelTransitionSeconds"]) < float(standard["PanelTransitionSeconds"]),
            "Reduced motion must shorten panel transitions")
    require(float(reduced["AmbientMotionScale"]) == 0, "Reduced motion must stop ambient motion")
    require(float(reduced["FlashOpacity"]) <= 0.08, "Reduced flash exceeds 8 percent")
    require(float(reduced["VfxDensity"]) <= 0.35, "Reduced VFX exceeds 35 percent")


def validate_compositions(data: dict[str, Any]) -> list[dict[str, Any]]:
    require(data.get("SystemId") == "al.ui.hud.compositions.v1", "Unexpected composition system ID")
    compositions = data.get("Compositions", [])
    require({entry["FormFactor"] for entry in compositions} == set(range(4)),
            "Phone, tablet, PC 16:9, and PC ultrawide compositions are required")
    signatures: set[str] = set()
    for composition in compositions:
        form = FORM_FACTOR_NAMES[composition["FormFactor"]]
        scan = composition["ProtectedScanPath"]
        sx0, sy0, sx1, sy1 = rect_bounds(scan)
        require(0 <= sx0 < sx1 <= 1 and 0 <= sy0 < sy1 <= 1, f"{form} scan path is invalid")
        require(float(composition["TextScaleMinimum"]) <= 0.85, f"{form} lacks 85 percent text")
        require(float(composition["TextScaleMaximum"]) >= 2.0, f"{form} lacks 200 percent text")
        slots = composition.get("Slots", [])
        require({entry["Id"] for entry in slots} == set(range(7)), f"{form} slot set is incomplete")
        signature_parts = [str(composition["FormFactor"]), json.dumps(scan, sort_keys=True)]
        for slot in slots:
            x0, y0, x1, y1 = rect_bounds(slot["NormalizedRect"])
            require(0 <= x0 < x1 <= 1 and 0 <= y0 < y1 <= 1, f"{form}/{slot['Id']} is out of bounds")
            if slot["Id"] == 2:
                require(bool(slot["IsWorldCueLayer"]), f"{form} telegraph slot must be transparent world cues")
                require(slot["NormalizedRect"] == scan, f"{form} telegraph slot must match the scan path")
            else:
                require(not slot["IsWorldCueLayer"], f"{form}/{slot['Id']} cannot be a world-cue layer")
                require(not overlaps(slot["NormalizedRect"], scan),
                        f"{form}/{SLOT_NAMES[slot['Id']]} overlaps the protected scan path")
            signature_parts.append(json.dumps(slot["NormalizedRect"], sort_keys=True))
        signature = "|".join(signature_parts)
        require(signature not in signatures, f"{form} duplicates another authored composition")
        signatures.add(signature)
    return compositions


def validate_components(data: dict[str, Any]) -> list[dict[str, Any]]:
    require(
        data.get("SystemId") == "al.ui.hud.components.v1",
        "Unexpected production HUD component system ID",
    )
    components = data.get("Components", [])
    require(
        {entry["Slot"] for entry in components} == set(range(7)) and len(components) == 7,
        "Production HUD components must define each required slot exactly once",
    )
    expected_layers = {0: 1, 1: 1, 2: 2, 3: 0, 4: 0, 5: 0, 6: 0}
    for component in components:
        slot = int(component["Slot"])
        require(int(component["Template"]) == slot, f"Component {slot} uses the wrong purpose template")
        require(int(component["Layer"]) == expected_layers[slot], f"Component {slot} uses an unsafe layer")
        require(int(component["DefaultState"]) in range(8), f"Component {slot} has an invalid state")
        for role in ("HeaderRole", "PrimaryRole", "SecondaryRole"):
            require(int(component[role]) in range(6), f"Component {slot} has an invalid {role}")
        require(int(component["MaxVisibleRows"]) > 0, f"Component {slot} has no row capacity")
        require(
            float(component["LocalizationExpansion"]) >= 1.5,
            f"Component {slot} lacks localization expansion allowance",
        )

        if slot in (0, 1, 2):
            require(bool(component["ProtectFromOcclusion"]), f"Critical component {slot} is not protected")
        if slot == 2:
            require(not bool(component["ShowSurface"]), "Hostile telegraphs must not create an opaque plate")
        else:
            require(bool(component["ShowSurface"]), f"Panel component {slot} lacks a readable surface")
        if slot in (3, 4, 5, 6):
            require(bool(component["AggregateOverflow"]), f"Secondary component {slot} cannot aggregate")
    return components


def to_hex(color: dict[str, Any]) -> str:
    channels = [round(max(0.0, min(1.0, float(color[channel]))) * 255) for channel in ("r", "g", "b")]
    return "#" + "".join(f"{channel:02x}" for channel in channels)


def render_svg(composition: dict[str, Any], tokens: dict[str, Any], output: Path) -> None:
    width = int(composition["ReferenceResolution"]["x"])
    height = int(composition["ReferenceResolution"]["y"])
    canvas = to_hex(tokens["CanvasColor"])
    surface = to_hex(tokens["SurfaceColor"])
    edge = to_hex(tokens["EdgeColor"])
    text = to_hex(tokens["TextPrimaryColor"])
    states = {entry["State"]: entry for entry in tokens["StateTreatments"]}
    form_name = FORM_FACTOR_NAMES[composition["FormFactor"]]

    def pixel_rect(rect: dict[str, Any]) -> tuple[float, float, float, float]:
        x = float(rect["x"]) * width
        y = (1.0 - float(rect["y"]) - float(rect["height"])) * height
        return x, y, float(rect["width"]) * width, float(rect["height"]) * height

    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" role="img" aria-labelledby="title desc">',
        f'<title id="title">AnotherLife {html.escape(form_name)} HUD composition</title>',
        '<desc id="desc">Deterministic composition evidence. Dashed center is the protected PvP scan path; authored HUD plates remain outside it.</desc>',
        '<defs>',
        '<pattern id="stone" width="18" height="18" patternUnits="userSpaceOnUse"><path d="M0 17L17 0" stroke="#ffffff" stroke-opacity="0.025" stroke-width="2"/></pattern>',
        '</defs>',
        f'<rect width="{width}" height="{height}" fill="{canvas}"/>',
        f'<rect width="{width}" height="{height}" fill="url(#stone)"/>',
    ]

    scan_x, scan_y, scan_w, scan_h = pixel_rect(composition["ProtectedScanPath"])
    lines.extend([
        f'<rect x="{scan_x:.1f}" y="{scan_y:.1f}" width="{scan_w:.1f}" height="{scan_h:.1f}" fill="none" stroke="{edge}" stroke-width="3" stroke-dasharray="18 14" opacity="0.58"/>',
        f'<text x="{scan_x + scan_w / 2:.1f}" y="{scan_y + 30:.1f}" text-anchor="middle" fill="{edge}" font-family="sans-serif" font-size="18" letter-spacing="2">PROTECTED PvP SCAN PATH</text>',
    ])

    for slot in composition["Slots"]:
        slot_id = int(slot["Id"])
        if slot_id == 2:
            continue
        x, y, slot_width, slot_height = pixel_rect(slot["NormalizedRect"])
        state = states[SLOT_STATE[slot_id]]
        accent = to_hex(state["Color"])
        prefix = html.escape(state["LabelPrefix"])
        label = html.escape(SLOT_NAMES[slot_id])
        label_length = len(state["LabelPrefix"]) + 2 + len(SLOT_NAMES[slot_id])
        width_limited_size = (slot_width - 36.0) / max(1.0, label_length * 0.56)
        font_size = max(11.0, min(24.0, slot_height * 0.16, width_limited_size))
        lines.extend([
            f'<rect x="{x:.1f}" y="{y:.1f}" width="{slot_width:.1f}" height="{slot_height:.1f}" rx="4" fill="{surface}" fill-opacity="0.92" stroke="{accent}" stroke-width="3"/>',
            f'<path d="M{x + 14:.1f} {y + 12:.1f}H{x + slot_width - 14:.1f}" stroke="{accent}" stroke-width="2" opacity="0.7"/>',
            f'<text x="{x + 18:.1f}" y="{y + slot_height / 2 + font_size * 0.34:.1f}" fill="{text}" font-family="sans-serif" font-size="{font_size:.1f}" font-weight="600">{prefix}  {label}</text>',
        ])

    lines.extend([
        f'<text x="{width * 0.5:.1f}" y="{height - 12:.1f}" text-anchor="middle" fill="{edge}" opacity="0.72" font-family="sans-serif" font-size="12">ASHEN RELIQUARY · {html.escape(form_name.upper())} · COMPOSITION EVIDENCE · NOT SHIPPING TYPOGRAPHY OR CREATIVE APPROVAL</text>',
        '</svg>',
    ])
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(("\n".join(lines) + "\n").encode("utf-8"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", nargs="?", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    token_path = root / "unity/Assets/AL/Resources/UI/DesignSystem/AL_UI_ProductionDesignTokens.json"
    composition_path = root / "unity/Assets/AL/Resources/UI/DesignSystem/AL_UI_HudResponsiveCompositions.json"
    component_path = root / "unity/Assets/AL/Resources/UI/DesignSystem/AL_UI_HudComponentAuthoring.json"
    output = args.output.resolve() if args.output else root / "unity/Docs/UI/Evidence"

    tokens = load_json(token_path)
    compositions = validate_compositions(load_json(composition_path))
    components = validate_components(load_json(component_path))
    validate_tokens(tokens)

    manifest_entries = []
    for composition in compositions:
        name = FORM_FACTOR_NAMES[composition["FormFactor"]]
        target = output / f"AL_UI_Hud_Composition_{name}.svg"
        render_svg(composition, tokens, target)
        manifest_entries.append({
            "formFactor": name,
            "referenceResolution": composition["ReferenceResolution"],
            "protectedScanPath": composition["ProtectedScanPath"],
            "slotCount": len(composition["Slots"]),
            "artifact": target.name,
            "sha256": sha256(target),
        })

    manifest = {
        "schemaVersion": 1,
        "systemId": "al.ui.hud.composition.evidence.v1",
        "tokenAsset": token_path.relative_to(root).as_posix(),
        "compositionAsset": composition_path.relative_to(root).as_posix(),
        "componentAsset": component_path.relative_to(root).as_posix(),
        "artifacts": manifest_entries,
    }
    manifest_path = output / "AL_UI_Hud_Composition_Evidence_Manifest.json"
    manifest_path.write_bytes(
        (json.dumps(manifest, indent=2) + "\n").encode("utf-8"))
    print(
        "PASS: 8 semantic states, 4 authored form factors, "
        f"{len(components)} reusable HUD components, 7 required slots each, "
        "protected scan paths clear, 4 SVG renders"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
