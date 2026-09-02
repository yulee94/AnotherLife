#!/usr/bin/env python
"""Generate deterministic GS-04 HUD/map release-readiness evidence."""

from __future__ import annotations

import argparse
import hashlib
import html
import json
from pathlib import Path
from typing import Any

import validate_ui_design_system as design_system

SCENARIOS = (
    "dense_combat",
    "accessibility_stress",
    "expanded_map",
    "input_focus_paths",
)

FORM_FACTOR_SAFE_AREAS = {
    0: {"x": 0.075, "y": 0.055, "width": 0.85, "height": 0.89},
    1: {"x": 0.045, "y": 0.04, "width": 0.91, "height": 0.92},
    2: {"x": 0.035, "y": 0.035, "width": 0.93, "height": 0.93},
    3: {"x": 0.055, "y": 0.035, "width": 0.89, "height": 0.93},
}

DENSE_CONTENT = {
    0: ("PLAYER READY", "Health 84% · Resolve 62%"),
    1: ("HOSTILE CAST", "Break vulnerable · 1.2 s"),
    2: ("DODGE", "Forward cleave · impact 0.8 s"),
    3: ("PARTY", "Revive priority · +5 aggregated"),
    4: ("CONTESTED", "Reliquary 62% · 00:48"),
    5: ("ROUTE", "North bridge · confirmed"),
    6: ("ALLEGIANCE", "Stonehold · commander nearby"),
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical_source_bytes(path: Path) -> bytes:
    """Return stable source bytes across LF and CRLF worktrees."""
    return path.read_bytes().replace(b"\r\n", b"\n").replace(b"\r", b"\n")


def rect_bounds(rect: dict[str, Any]) -> tuple[float, float, float, float]:
    x = float(rect["x"])
    y = float(rect["y"])
    return x, y, x + float(rect["width"]), y + float(rect["height"])


def project_rect(
    rect: dict[str, Any],
    safe: dict[str, Any],
    width: int,
    height: int,
) -> tuple[float, float, float, float]:
    safe_x = float(safe["x"])
    safe_y = float(safe["y"])
    safe_width = float(safe["width"])
    safe_height = float(safe["height"])
    x = safe_x + float(rect["x"]) * safe_width
    y = safe_y + float(rect["y"]) * safe_height
    rect_width = float(rect["width"]) * safe_width
    rect_height = float(rect["height"]) * safe_height
    return (
        x * width,
        (1.0 - y - rect_height) * height,
        rect_width * width,
        rect_height * height,
    )


def normalized_overlap(left: dict[str, Any], right: dict[str, Any]) -> bool:
    lx0, ly0, lx1, ly1 = rect_bounds(left)
    rx0, ry0, rx1, ry1 = rect_bounds(right)
    return lx0 < rx1 and lx1 > rx0 and ly0 < ry1 and ly1 > ry0


def svg_text(
    lines: list[str],
    x: float,
    y: float,
    value: str,
    color: str,
    size: float,
    anchor: str = "start",
    weight: int = 500,
) -> None:
    lines.append(
        f'<text x="{x:.1f}" y="{y:.1f}" text-anchor="{anchor}" '
        f'fill="{color}" font-family="sans-serif" font-size="{size:.1f}" '
        f'font-weight="{weight}">{html.escape(value)}</text>'
    )


def add_hud(
    lines: list[str],
    composition: dict[str, Any],
    safe: dict[str, Any],
    tokens: dict[str, Any],
    text_scale: float,
) -> None:
    width = int(composition["ReferenceResolution"]["x"])
    height = int(composition["ReferenceResolution"]["y"])
    surface = design_system.to_hex(tokens["SurfaceColor"])
    text = design_system.to_hex(tokens["TextPrimaryColor"])
    secondary = design_system.to_hex(tokens["TextSecondaryColor"])
    states = {entry["State"]: entry for entry in tokens["StateTreatments"]}
    scan = composition["ProtectedScanPath"]

    for slot in composition["Slots"]:
        slot_id = int(slot["Id"])
        x, y, slot_width, slot_height = project_rect(
            slot["NormalizedRect"], safe, width, height
        )
        state = states[design_system.SLOT_STATE[slot_id]]
        accent = design_system.to_hex(state["Color"])
        title, detail = DENSE_CONTENT[slot_id]
        if slot_id == 2:
            lines.append(
                f'<path d="M{x + slot_width * 0.25:.1f} {y + slot_height * 0.72:.1f} '
                f'L{x + slot_width * 0.5:.1f} {y + slot_height * 0.26:.1f} '
                f'L{x + slot_width * 0.75:.1f} {y + slot_height * 0.72:.1f} Z" '
                f'fill="none" stroke="{accent}" stroke-width="5" stroke-dasharray="16 10"/>'
            )
            svg_text(
                lines,
                x + slot_width * 0.5,
                y + slot_height * 0.53,
                f"{state['LabelPrefix']} {title}",
                accent,
                18.0 * text_scale,
                "middle",
                700,
            )
            svg_text(
                lines,
                x + slot_width * 0.5,
                y + slot_height * 0.59,
                detail,
                text,
                13.0 * text_scale,
                "middle",
                600,
            )
            continue

        lines.append(
            f'<rect x="{x:.1f}" y="{y:.1f}" width="{slot_width:.1f}" '
            f'height="{slot_height:.1f}" rx="4" fill="{surface}" fill-opacity="0.92" '
            f'stroke="{accent}" stroke-width="3"/>'
        )
        authored_slot_height = float(slot["NormalizedRect"]["height"]) * height
        base_font_size = round(max(11.0, min(18.0, authored_slot_height * 0.095)), 1)
        font_size = base_font_size * text_scale
        detail_base_size = round(max(10.0, min(14.0, base_font_size * 0.7)), 1)
        detail_font_size = detail_base_size * text_scale
        svg_text(
            lines,
            x + 16,
            y + max(22.0 * text_scale, slot_height * 0.34),
            f"{state['LabelPrefix']} {title}",
            text,
            font_size,
            weight=700,
        )
        svg_text(
            lines,
            x + 16,
            y + max(40.0 * text_scale, slot_height * 0.68),
            detail,
            secondary,
            detail_font_size,
        )

    for slot in composition["Slots"]:
        if int(slot["Id"]) != 2:
            require(
                not normalized_overlap(slot["NormalizedRect"], scan),
                "A persistent HUD plate overlaps the protected scan path",
            )


def add_expanded_map(
    lines: list[str],
    composition: dict[str, Any],
    safe: dict[str, Any],
    tokens: dict[str, Any],
) -> None:
    width = int(composition["ReferenceResolution"]["x"])
    height = int(composition["ReferenceResolution"]["y"])
    canvas = design_system.to_hex(tokens["CanvasColor"])
    surface = design_system.to_hex(tokens["RaisedSurfaceColor"])
    edge = design_system.to_hex(tokens["EdgeColor"])
    text = design_system.to_hex(tokens["TextPrimaryColor"])
    x, y, map_width, map_height = project_rect(
        {"x": 0.08, "y": 0.08, "width": 0.84, "height": 0.84},
        safe,
        width,
        height,
    )
    lines.append(
        f'<rect x="{x:.1f}" y="{y:.1f}" width="{map_width:.1f}" height="{map_height:.1f}" '
        f'rx="8" fill="{surface}" stroke="{edge}" stroke-width="4"/>'
    )
    svg_text(lines, x + 24, y + 42, "EXPANDED WORLD MAP · GAMEPLAY INPUT SUPPRESSED", text, 22, weight=700)
    svg_text(lines, x + 24, y + 72, "Authority epoch 12 · revision 48 · surface rules agree", edge, 16)

    markers = (
        (0.22, 0.34, "SELF · stonehold", "shield"),
        (0.58, 0.28, "OBJECTIVE · crossroads_control", "four_point"),
        (0.76, 0.65, "ROUTE · stonehold_to_accordant", "double_line"),
    )
    for index, (mx, my, label, shape) in enumerate(markers, start=1):
        px = x + map_width * mx
        py = y + map_height * my
        lines.append(
            f'<rect x="{px - 10:.1f}" y="{py - 10:.1f}" width="20" height="20" '
            f'transform="rotate(45 {px:.1f} {py:.1f})" fill="none" stroke="{edge}" stroke-width="3"/>'
        )
        svg_text(lines, px + 18, py + 6, f"{index}. {label} · {shape}", text, 15)

    mini_width = map_width * 0.27
    mini_height = map_height * 0.28
    mini_x = x + map_width - mini_width - 22
    mini_y = y + 88
    lines.append(
        f'<rect x="{mini_x:.1f}" y="{mini_y:.1f}" width="{mini_width:.1f}" '
        f'height="{mini_height:.1f}" fill="{canvas}" stroke="{edge}" stroke-width="2"/>'
    )
    svg_text(lines, mini_x + 12, mini_y + 24, "MINIMAP PROJECTION", edge, 13, weight=700)
    svg_text(lines, mini_x + 12, mini_y + 48, "shared objective / allegiance", text, 11)
    svg_text(lines, mini_x + 12, mini_y + 68, "world-map-only route omitted", text, 11)


def add_focus_paths(
    lines: list[str],
    composition: dict[str, Any],
    safe: dict[str, Any],
    tokens: dict[str, Any],
) -> None:
    width = int(composition["ReferenceResolution"]["x"])
    height = int(composition["ReferenceResolution"]["y"])
    edge = design_system.to_hex(tokens["EdgeColor"])
    text = design_system.to_hex(tokens["TextPrimaryColor"])
    surface = design_system.to_hex(tokens["SurfaceColor"])
    x, y, panel_width, panel_height = project_rect(
        {"x": 0.16, "y": 0.16, "width": 0.68, "height": 0.68},
        safe,
        width,
        height,
    )
    lines.append(
        f'<rect x="{x:.1f}" y="{y:.1f}" width="{panel_width:.1f}" height="{panel_height:.1f}" '
        f'rx="8" fill="{surface}" stroke="{edge}" stroke-width="3"/>'
    )
    svg_text(lines, x + 24, y + 42, "INPUT FOCUS PATHS · ONE AUTHORITATIVE STATE", text, 22, weight=700)
    rows = (
        ("TOUCH", "map toggle → filter → marker → close", "56-unit targets; no hover dependency"),
        ("CONTROLLER", "prior control → close → filters → marker → close", "contained explicit navigation; restore prior focus"),
        ("KEYBOARD", "prior control → close → filters → marker → Escape", "submit/cancel parity; hidden controls skipped"),
    )
    for index, (mode, path, note) in enumerate(rows, start=1):
        row_y = y + 78 + index * panel_height * 0.20
        lines.append(
            f'<circle cx="{x + 52:.1f}" cy="{row_y:.1f}" r="22" fill="none" '
            f'stroke="{edge}" stroke-width="3"/>'
        )
        svg_text(lines, x + 52, row_y + 7, str(index), edge, 18, "middle", 700)
        svg_text(lines, x + 88, row_y - 5, f"{mode}: {path}", text, 17, weight=700)
        svg_text(lines, x + 88, row_y + 22, note, edge, 13)


def render_release_svg(
    composition: dict[str, Any],
    tokens: dict[str, Any],
    scenario: str,
    output: Path,
) -> dict[str, Any]:
    require(scenario in SCENARIOS, "Unknown GS-04 evidence scenario")
    width = int(composition["ReferenceResolution"]["x"])
    height = int(composition["ReferenceResolution"]["y"])
    form_factor = int(composition["FormFactor"])
    form_name = design_system.FORM_FACTOR_NAMES[form_factor]
    safe = FORM_FACTOR_SAFE_AREAS[form_factor] if scenario == "accessibility_stress" else {
        "x": 0.0,
        "y": 0.0,
        "width": 1.0,
        "height": 1.0,
    }
    canvas = design_system.to_hex(tokens["CanvasColor"])
    edge = design_system.to_hex(tokens["EdgeColor"])
    text = design_system.to_hex(tokens["TextPrimaryColor"])
    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" role="img">',
        f'<title>AnotherLife GS-04 {html.escape(form_name)} {html.escape(scenario)}</title>',
        f'<rect width="{width}" height="{height}" fill="{canvas}"/>',
    ]
    safe_x = float(safe["x"]) * width
    safe_y = (1.0 - float(safe["y"]) - float(safe["height"])) * height
    safe_width = float(safe["width"]) * width
    safe_height = float(safe["height"]) * height
    lines.append(
        f'<rect x="{safe_x:.1f}" y="{safe_y:.1f}" width="{safe_width:.1f}" '
        f'height="{safe_height:.1f}" fill="none" stroke="{edge}" stroke-width="2" '
        f'stroke-dasharray="12 10" opacity="0.65"/>'
    )

    if scenario in ("dense_combat", "accessibility_stress"):
        add_hud(
            lines,
            composition,
            safe,
            tokens,
            2.0 if scenario == "accessibility_stress" else 1.0,
        )
    elif scenario == "expanded_map":
        add_expanded_map(lines, composition, safe, tokens)
    else:
        add_focus_paths(lines, composition, safe, tokens)

    footer = {
        "dense_combat": "DENSE COMBAT · HOSTILE TELEGRAPH · CENTRAL SCAN PATH CLEAR",
        "accessibility_stress": "200% TEXT · EXTREME SAFE AREA · REDUCED MOTION/FLASH/VFX",
        "expanded_map": "EXPANDED MAP · MINIMAP AUTHORITY AGREEMENT · MODAL FOCUS",
        "input_focus_paths": "TOUCH · CONTROLLER · KEYBOARD · RESTORATION PATHS",
    }[scenario]
    svg_text(lines, width * 0.5, height - 14, footer, text, 13, "middle", 700)
    svg_text(
        lines,
        width * 0.5,
        height - 36,
        "DETERMINISTIC LAYOUT EVIDENCE · NOT DEVICE, PERFORMANCE, FONT, OR OWNER APPROVAL",
        edge,
        11,
        "middle",
    )
    lines.append("</svg>")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(("\n".join(lines) + "\n").encode("utf-8"))
    return {
        "formFactor": form_name,
        "scenarioId": scenario,
        "referenceResolution": composition["ReferenceResolution"],
        "safeAreaNormalized": safe,
        "textScale": 2.0 if scenario == "accessibility_stress" else 1.0,
        "reducedMotion": scenario == "accessibility_stress",
        "reducedFlash": scenario == "accessibility_stress",
        "reducedVfx": scenario == "accessibility_stress",
        "mapAuthorityAgreement": scenario == "expanded_map",
        "inputModes": ["touch", "controller", "keyboard"] if scenario == "input_focus_paths" else [],
        "artifact": output.name,
        "sha256": sha256(output),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", nargs="?", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    output = args.output.resolve() if args.output else root / "unity/Docs/UI/Evidence/GS-04"
    token_path = root / "unity/Assets/AL/Resources/UI/DesignSystem/AL_UI_ProductionDesignTokens.json"
    composition_path = root / "unity/Assets/AL/Resources/UI/DesignSystem/AL_UI_HudResponsiveCompositions.json"
    component_path = root / "unity/Assets/AL/Resources/UI/DesignSystem/AL_UI_HudComponentAuthoring.json"
    map_catalog_path = root / "unity/Assets/AL/StreamingAssets/GameData/al_map_disclosure_catalog.json"
    validator_dependency_path = root / "tools/ui/validate_ui_design_system.py"

    tokens = design_system.load_json(token_path)
    compositions = design_system.validate_compositions(design_system.load_json(composition_path))
    design_system.validate_components(design_system.load_json(component_path))
    design_system.validate_tokens(tokens)
    map_catalog = design_system.load_json(map_catalog_path)
    require(map_catalog.get("catalogId") == "al_map_disclosure_catalog", "Map authority catalog is missing")
    require(map_catalog.get("authority", {}).get("owner") == "server", "Map authority must remain server-owned")
    expected_projections = {
        "objectives": ("map_objective_crossroads_control", ["minimap", "world_map"]),
        "routes": ("route_stonehold_to_accordant", ["world_map"]),
        "allegianceMarkers": ("allegiance_stonehold", ["minimap", "world_map"]),
    }
    for section, (projection_id, expected_surfaces) in expected_projections.items():
        projection = next(
            (entry for entry in map_catalog.get(section, []) if entry.get("id") == projection_id),
            None,
        )
        require(projection is not None, f"Missing authoritative map projection {projection_id}")
        require(
            projection.get("surfaces") == expected_surfaces,
            f"Authoritative surfaces drifted for {projection_id}",
        )

    artifacts = []
    for composition in compositions:
        form_name = design_system.FORM_FACTOR_NAMES[int(composition["FormFactor"])]
        for scenario in SCENARIOS:
            target = output / f"AL_GS04_{form_name}_{scenario}.svg"
            artifacts.append(render_release_svg(composition, tokens, scenario, target))

    require(len(artifacts) == 16, "GS-04 evidence must contain four scenarios for four form factors")
    require(
        {(entry["formFactor"], entry["scenarioId"]) for entry in artifacts}
        == {(name, scenario) for name in design_system.FORM_FACTOR_NAMES.values() for scenario in SCENARIOS},
        "GS-04 evidence matrix is incomplete",
    )
    input_digest = hashlib.sha256()
    for source_path in (
        token_path,
        composition_path,
        component_path,
        map_catalog_path,
        validator_dependency_path,
        Path(__file__),
    ):
        input_digest.update(source_path.relative_to(root).as_posix().encode("utf-8"))
        input_digest.update(b"\0")
        input_digest.update(canonical_source_bytes(source_path))
        input_digest.update(b"\0")
    manifest = {
        "schemaVersion": 1,
        "systemId": "al.ui.gs04.release_readiness_evidence.v1",
        "status": "automated_layout_evidence_not_final_release_approval",
        "goldenScene": "GS-04",
        "evidenceInputSha256": input_digest.hexdigest(),
        "tokenAsset": token_path.relative_to(root).as_posix(),
        "compositionAsset": composition_path.relative_to(root).as_posix(),
        "componentAsset": component_path.relative_to(root).as_posix(),
        "validatorDependency": validator_dependency_path.relative_to(root).as_posix(),
        "mapAuthorityCatalog": map_catalog_path.relative_to(root).as_posix(),
        "mapAuthorityOwner": "server",
        "authoritativeProjectionIds": {
            "sharedObjective": "map_objective_crossroads_control",
            "worldMapOnlyRoute": "route_stonehold_to_accordant",
            "sharedAllegiance": "allegiance_stonehold",
        },
        "artifacts": artifacts,
        "limitations": [
            "SVGs are deterministic layout evidence, not runtime device captures.",
            "The commissioned production font binaries are not present.",
            "Physical-device performance, thermal, touch, controller, keyboard, blinded-participant, and owner-creative gates remain external.",
        ],
    }
    manifest_path = output / "AL_GS04_Release_Readiness_Evidence_Manifest.json"
    manifest_path.write_bytes((json.dumps(manifest, indent=2) + "\n").encode("utf-8"))
    print(
        "PASS: GS-04 automated evidence matrix has 4 form factors x 4 scenarios = 16 SVGs; "
        "protected scan paths, extreme safe areas, 200% text metadata, reduced effects, "
        "map authority agreement, and touch/controller/keyboard focus paths recorded"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
