#!/usr/bin/env python3
"""Assemble and validate the held post-MVP world-asset inventory.

The inventory is preparation authority only. It preserves eight existing prefab
bindings but neither generates nor activates runtime content.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path, PurePosixPath
from typing import Any, Callable, Iterable


CATALOG_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_world_asset_inventory.json"
)
EVIDENCE_PATH = Path(
    "unity/Docs/AssetLibrary/PostMVP_World_Asset_Inventory_Acceptance_v1.json"
)
SCHEMA_PATH = Path(
    "unity/SharedContracts/Schemas/al-world-asset-inventory.schema.json"
)
TAXONOMY_PATH = Path(
    "unity/Docs/AssetLibrary/PostMVP_World_Asset_Taxonomy_v1.md"
)
BUDGET_PATH = Path(
    "unity/Docs/AssetLibrary/PostMVP_World_Asset_Budgets_And_Readability_v1.md"
)
STANDARD_PATH = Path(
    "unity/Docs/AssetLibrary/PostMVP_World_Asset_Catalog_Binding_And_Production_Standard_v1.md"
)
BUILDING_CATALOG_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_building_catalog.json"
)
GENERATOR_PATH = Path("unity/SharedContracts/Tests/world_asset_inventory.py")
CANONICAL_TEXT_SOURCE_PATHS = {
    TAXONOMY_PATH,
    BUDGET_PATH,
    STANDARD_PATH,
}

REALMS = ["crownlands", "stonehold", "eldergrove", "umbral"]
REALM_COLUMNS = ["CRN", "STH", "ELD", "UMB"]
GATE_IDS = [
    "owner_creative",
    "technical",
    "provenance",
    "performance_mobile_floor",
    "accessibility",
    "release",
]
ASSET_CLASSES = [
    "terrain_surface",
    "terrain_decal",
    "water",
    "vegetation",
    "geology",
    "traversal",
    "architecture",
    "interior_module",
    "prop",
    "interactable",
    "harvestable",
    "signage",
    "banner",
    "vfx_anchor",
    "technical_helper",
    "derivative_25d",
    "fantasy_beast",
    "monster",
    "dragon",
]
PROFILE_GROUPS = [
    "coordinateProfiles",
    "pivotProfiles",
    "snapGridProfiles",
    "materialShaderProfiles",
    "texelDensityProfiles",
    "lodProfiles",
    "colliderProfiles",
    "navigationProfiles",
    "occlusionProfiles",
    "streamingProfiles",
]
STANDARD_REF_IDS = {
    "coordinateProfileId": "coord_unity_meter_y_up_z_forward_v001",
    "pivotProfileId": "pivot_base_center_finished_ground_v001",
    "snapGridProfileId": "snap_world_modular_default_v001",
    "materialShaderProfileId": "material_builtin_pbr_opaque_shared_v001",
    "texelDensityProfileId": "texel_shared_trim_measured_v001",
    "lodProfileId": "lod_static_world_standard_v001",
    "colliderProfileId": "collider_static_simple_v001",
    "navigationProfileId": "nav_static_structure_v001",
    "occlusionProfileId": "occlusion_structure_groups_v001",
    "streamingProfileId": "stream_chunk_owned_packaged_v001",
}
OWNER_MAP = {
    "WORLD": "world",
    "ARCH": "architecture",
    "GAME": "gameplay",
    "NARR": "narrative",
    "TA": "technical_art",
    "ECO": "ecosystem",
    "UXA": "accessibility",
    "ENG": "runtime_engineering",
}
OWNER_ORDER = list(OWNER_MAP.values())
FAMILY_ID_RE = re.compile(r"^waf_[a-z0-9]+(?:_[a-z0-9]+)+$")
ASSET_ID_RE = re.compile(
    r"^(?:wa_(?:shared|neutral|crownlands|stonehold|eldergrove|umbral|"
    r"kingdom_crownlands|kingdom_stonehold|kingdom_eldergrove|kingdom_umbral|"
    r"event_[a-z0-9]+(?:_[a-z0-9]+)*)_[a-z0-9]+(?:_[a-z0-9]+)*_v[0-9]{3}|"
    r"wad_[a-z0-9]+(?:_[a-z0-9]+)*_v[0-9]{3})$"
)

TAXONOMY_CANONICAL_SHA256 = (
    "9908e4031e94d37ae8590291072e7b444f1e29c72b0db13d41512eb8496f0062"
)
EXPECTED_FAMILY_COUNT = 242
EXPECTED_BINDING_COUNT = 8
EXPECTED_ALIAS_COUNT = 16

TERRAIN_MACRO = {"waf_terrain_surface_macro_landform"}
WATER = {"waf_terrain_water_surface", "waf_terrain_water_edge_module"}
DECALS = {
    "waf_terrain_decal_material_transition",
    "waf_terrain_decal_erosion_drainage",
    "waf_terrain_decal_wetness_stain",
    "waf_terrain_decal_wear_tracks",
    "waf_terrain_decal_damage_debris",
    "waf_terrain_decal_route_marking",
}
FOLIAGE_MAJOR = {
    "waf_vegetation_tree_canopy",
    "waf_vegetation_tree_understory",
    "waf_vegetation_root_structural",
    "waf_vegetation_deadwood",
}
STATIC_LARGE = {
    "waf_geology_boulder",
    "waf_geology_cliff_face",
    "waf_geology_ledge_overhang",
    "waf_geology_cave_entrance",
    "waf_geology_cave_tunnel_module",
    "waf_geology_cavern_room_landmark",
    "waf_geology_crystal_formation",
    "waf_geology_magical_crystal_node",
    "waf_geology_mine_quarry_dressing",
}
ARCHITECTURE_HERO = {
    "waf_architecture_castle_enterable",
    "waf_architecture_fortress_enterable",
    "waf_architecture_city_capital_kit",
    "waf_architecture_building_town_hall",
    "waf_architecture_event_accordant_isle",
}
PROP_LARGE = {
    "waf_prop_seating_bench_pew",
    "waf_prop_surface_table_desk",
    "waf_prop_sleep_bed_cot_bunk",
    "waf_prop_storage_shelf_bookcase",
    "waf_prop_storage_cabinet_cupboard",
    "waf_prop_lighting_brazier_hearth_fireplace",
    "waf_prop_lighting_chandelier_hanging",
    "waf_prop_market_stall_canopy",
    "waf_prop_market_counter_display",
    "waf_prop_forge_hearth_anvil",
    "waf_prop_kitchen_oven_cookfire",
    "waf_prop_military_weapon_rack",
    "waf_prop_military_armor_stand",
    "waf_prop_military_training_dummy_target",
    "waf_prop_military_war_map_table",
    "waf_prop_royal_throne_dais",
    "waf_prop_royal_council_lectern",
    "waf_prop_royal_ceremonial_screen",
    "waf_prop_guild_contract_board",
    "waf_prop_guild_trophy_display",
    "waf_prop_guild_meeting_contract_desk",
    "waf_prop_religious_altar_shrine",
    "waf_prop_textile_rug_tapestry",
    "waf_prop_utility_cart_wagon",
    "waf_prop_utility_scaffold_ladder",
}
DERIVATIVE_LARGE = {
    "waf_derivative_25d_building_render",
    "waf_derivative_25d_building_state",
    "waf_derivative_25d_castle_core",
    "waf_derivative_25d_terrain_tile",
    "waf_derivative_25d_wall_gate_tile",
}
RESERVED_BUDGETS = {
    "waf_ecosystem_fantasy_beast_supporting": "wbud_reserved_fantasy_beast_supporting",
    "waf_ecosystem_fantasy_beast_ambient_flying": "wbud_reserved_fantasy_beast_supporting",
    "waf_ecosystem_fantasy_beast_aquatic_littoral": "wbud_reserved_fantasy_beast_supporting",
    "waf_ecosystem_monster_common": "wbud_reserved_monster_common",
    "waf_ecosystem_monster_elite": "wbud_reserved_monster_elite",
    "waf_ecosystem_monster_boss": "wbud_reserved_monster_boss",
    "waf_ecosystem_dragon_realm": "wbud_reserved_dragon",
    "waf_ecosystem_dragon_wish": "wbud_reserved_dragon",
    "waf_ecosystem_fantasy_beast_deep_pelagic": "wbud_reserved_deferred_zero",
}
EXPECTED_BUDGET_COUNTS = {
    "wbud_architecture_common": 24,
    "wbud_architecture_hero": 5,
    "wbud_decal": 6,
    "wbud_derivative_25d_large": 5,
    "wbud_derivative_25d_small": 10,
    "wbud_foliage_major": 4,
    "wbud_foliage_minor": 10,
    "wbud_interactable": 16,
    "wbud_interior_module": 21,
    "wbud_prop_large": 25,
    "wbud_prop_small": 24,
    "wbud_reserved_deferred_zero": 1,
    "wbud_reserved_dragon": 2,
    "wbud_reserved_fantasy_beast_supporting": 3,
    "wbud_reserved_monster_boss": 1,
    "wbud_reserved_monster_common": 1,
    "wbud_reserved_monster_elite": 1,
    "wbud_signage_banner": 10,
    "wbud_static_large": 9,
    "wbud_static_small": 5,
    "wbud_surface_layer": 9,
    "wbud_technical_helper": 19,
    "wbud_terrain_macro": 1,
    "wbud_traversal_module": 18,
    "wbud_vfx_anchor": 10,
    "wbud_water": 2,
}
MIB = 1024 * 1024
GIB = 1024 * MIB
BUDGET_CEILINGS = {
    "wbud_terrain_macro": (1, 4, 131072, 65536, 4, 4, 32, 8, 16, 2.0),
    "wbud_surface_layer": (6, 2, 8192, 4096, 1, 1, 8, 2, 4, 0.25),
    "wbud_decal": (6, 3, 512, 256, 1, 1, 2, 0.5, 1, 0.15),
    "wbud_water": (2, 5, 32768, 16384, 2, 2, 16, 4, 8, 1.0),
    "wbud_foliage_major": (8, 4, 20000, 8000, 2, 2, 10, 3, 6, 0.6),
    "wbud_foliage_minor": (12, 4, 5000, 1500, 1, 1, 3, 0.75, 1.5, 0.2),
    "wbud_static_large": (8, 5, 20000, 8000, 2, 2, 10, 3, 6, 0.6),
    "wbud_static_small": (12, 5, 5000, 1500, 1, 1, 3, 0.75, 1.5, 0.2),
    "wbud_traversal_module": (12, 5, 20000, 8000, 2, 2, 10, 3, 6, 0.6),
    "wbud_architecture_common": (4, 12, 20000, 12000, 2, 2, 16, 5, 10, 1.0),
    "wbud_architecture_hero": (2, 12, 40000, 24000, 3, 2, 32, 10, 20, 1.5),
    "wbud_interior_module": (8, 6, 12000, 5000, 2, 1, 8, 2.5, 5, 0.5),
    "wbud_prop_large": (8, 5, 10000, 4000, 2, 1, 5, 1.5, 3, 0.3),
    "wbud_prop_small": (12, 4, 5000, 1500, 1, 1, 2, 0.5, 1, 0.1),
    "wbud_interactable": (8, 6, 8000, 3000, 2, 1, 5, 1.5, 3, 0.3),
    "wbud_signage_banner": (8, 6, 5000, 1500, 1, 1, 2, 0.5, 1, 0.2),
    "wbud_vfx_anchor": (5, 5, 1000, 500, 1, 2, 4, 1, 2, 0.2),
    "wbud_technical_helper": (4, 4, 0, 0, 0, 0, 1, 0.25, 0.5, 0.05),
    "wbud_derivative_25d_large": (4, 12, 20000, 8000, 2, 1, 8, 2.5, 5, 0.5),
    "wbud_derivative_25d_small": (8, 12, 2000, 500, 1, 1, 2, 0.5, 1, 0.15),
}


class InventoryValidationError(RuntimeError):
    """Raised when assembly or validation cannot prove the inventory contract."""


def fail(category: str, message: str) -> None:
    raise InventoryValidationError(f"{category}: {message}")


def sha256(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def canonical_json(value: dict[str, Any]) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, separators=(",", ": "))
        + "\n"
    ).encode("utf-8")


def strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    value: dict[str, Any] = {}
    for key, item in pairs:
        if key in value:
            fail("MalformedCatalog", f"duplicate JSON property {key!r}")
        value[key] = item
    return value


def load_json(path: Path) -> tuple[dict[str, Any], bytes]:
    if not path.is_file():
        fail("MalformedCatalog", f"missing JSON file: {path}")
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        fail("CanonicalOrderViolation", f"UTF-8 BOM is forbidden: {path}")
    try:
        value = json.loads(
            raw.decode("utf-8"),
            object_pairs_hook=strict_object,
            parse_constant=lambda token: fail(
                "MalformedCatalog", f"non-finite JSON number {token!r}"
            ),
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        fail("MalformedCatalog", f"invalid JSON at {path}: {error}")
    if not isinstance(value, dict):
        fail("MalformedCatalog", f"JSON root must be an object: {path}")
    return value, raw


def read_required(repo_root: Path, relative: Path) -> bytes:
    path = repo_root / relative
    if not path.is_file():
        fail("MissingReference", f"required source is missing: {relative.as_posix()}")
    return path.read_bytes()


def canonical_source_bytes(relative: Path, raw: bytes) -> bytes:
    """Normalize authored Markdown for cross-platform logical-source hashing."""

    if relative not in CANONICAL_TEXT_SOURCE_PATHS:
        return raw
    try:
        raw.decode("utf-8")
    except UnicodeDecodeError as error:
        fail("MalformedCatalog", f"required text source is not UTF-8: {relative.as_posix()}")
        raise AssertionError("unreachable") from error
    return raw.replace(b"\r\n", b"\n").replace(b"\r", b"\n")


def read_canonical_source(repo_root: Path, relative: Path) -> bytes:
    return canonical_source_bytes(relative, read_required(repo_root, relative))


def source_input_evidence(repo_root: Path, relative: Path) -> dict[str, str]:
    canonical = read_canonical_source(repo_root, relative)
    return {
        "path": relative.as_posix(),
        "sha256": sha256(canonical),
        "canonicalization": (
            "utf8_lf" if relative in CANONICAL_TEXT_SOURCE_PATHS else "raw"
        ),
    }


def parse_taxonomy(repo_root: Path) -> list[dict[str, Any]]:
    raw = read_canonical_source(repo_root, TAXONOMY_PATH)
    if sha256(raw) != TAXONOMY_CANONICAL_SHA256:
        fail(
            "MalformedCatalog",
            "canonical taxonomy content drifted from the budget standard's source",
        )
    text = raw.decode("utf-8")
    rows: list[dict[str, Any]] = []
    for line_number, line in enumerate(text.splitlines(), start=1):
        if not line.startswith("| `waf_"):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) != 11:
            fail(
                "MalformedCatalog",
                f"taxonomy line {line_number} has {len(cells)} columns, expected 11",
            )
        family_id = cells[0].strip("`")
        if not FAMILY_ID_RE.fullmatch(family_id):
            fail("MalformedCatalog", f"malformed family ID {family_id!r}")
        realm_cells = []
        for realm_id, token in zip(REALMS, cells[2:6]):
            if token == "R":
                realm_cells.append(
                    {"realmId": realm_id, "mode": "realm_variant", "reason": None}
                )
            elif token == "S":
                realm_cells.append(
                    {"realmId": realm_id, "mode": "shared", "reason": None}
                )
            elif token.startswith("0:") and token[2:].strip():
                realm_cells.append(
                    {
                        "realmId": realm_id,
                        "mode": "excluded",
                        "reason": token[2:].strip(),
                    }
                )
            else:
                fail(
                    "CategoryRealmGap",
                    f"{family_id} has invalid {realm_id} applicability {token!r}",
                )
        owner_codes = cells[7].split("+")
        unknown_owners = sorted(
            code for code in owner_codes if code != "OWNER" and code not in OWNER_MAP
        )
        if unknown_owners:
            fail(
                "OwnerAuthorityMissing",
                f"{family_id} has unknown owner codes {unknown_owners}",
            )
        accountable = [owner for owner in OWNER_ORDER if owner in {OWNER_MAP.get(code) for code in owner_codes}]
        if not accountable:
            fail(
                "OwnerAuthorityMissing",
                f"{family_id} has no accountable implementation owner",
            )
        dependencies = [value.strip().lower() for value in cells[9].split(",")]
        if not dependencies or any(not value for value in dependencies):
            fail("MissingReference", f"{family_id} has malformed dependencies")
        rows.append(
            {
                "familyId": family_id,
                "displayLabel": family_id.removeprefix("waf_").replace("_", " ").title(),
                "assetClass": classify_family(family_id),
                "purpose": cells[1],
                "requiredVariants": cells[6],
                "ownerAuthority": {
                    "finalCreativeOwner": "project_owner",
                    "accountableOwners": accountable,
                    "ownerDecisionRef": (
                        f"{TAXONOMY_PATH.as_posix()}#L{line_number}"
                    ),
                },
                "realmApplicability": realm_cells,
                "taxonomyStatus": cells[8].lower(),
                "dependencies": dependencies,
                "schedule": cells[10],
                "biomeIds": [],
                "budgetClassId": resolve_budget_class(family_id),
                "standards": standard_refs(),
                "provenance": {
                    "state": "logical_authority_only",
                    "sourceReferences": [
                        TAXONOMY_PATH.as_posix(),
                        BUDGET_PATH.as_posix(),
                        STANDARD_PATH.as_posix(),
                    ],
                    "humanDecisionRef": "kanban:t_981d50c8",
                    "openIssues": [
                        "Production source, provenance, measurements, and owner review remain required before generation or activation."
                    ],
                },
                "approvalState": "preparation_held",
                "generationState": "held",
                "activationState": "held",
            }
        )
    if len(rows) != EXPECTED_FAMILY_COUNT:
        fail(
            "CategoryRealmGap",
            f"taxonomy produced {len(rows)} families, expected {EXPECTED_FAMILY_COUNT}",
        )
    ids = [row["familyId"] for row in rows]
    if len(set(ids)) != len(ids):
        fail("DuplicateId", "taxonomy contains duplicate family IDs")
    rows.sort(key=lambda row: row["familyId"].encode("utf-8"))
    counts = Counter(row["budgetClassId"] for row in rows)
    if dict(sorted(counts.items())) != EXPECTED_BUDGET_COUNTS:
        fail(
            "BudgetUnassigned",
            f"family-to-budget coverage drifted: {dict(sorted(counts.items()))}",
        )
    return rows


def classify_family(family_id: str) -> str:
    if family_id.startswith("waf_terrain_water_"):
        return "water"
    if family_id.startswith("waf_terrain_decal_"):
        return "terrain_decal"
    if family_id.startswith("waf_terrain_"):
        return "terrain_surface"
    rules = [
        ("waf_vegetation_", "vegetation"),
        ("waf_geology_", "geology"),
        ("waf_traversal_", "traversal"),
        ("waf_architecture_", "architecture"),
        ("waf_interior_", "interior_module"),
        ("waf_prop_", "prop"),
        ("waf_interactable_", "interactable"),
        ("waf_harvestable_", "harvestable"),
        ("waf_sign_", "signage"),
        ("waf_banner_", "banner"),
        ("waf_vfx_", "vfx_anchor"),
        ("waf_technical_", "technical_helper"),
        ("waf_derivative_25d_", "derivative_25d"),
        ("waf_ecosystem_fantasy_beast_", "fantasy_beast"),
        ("waf_ecosystem_monster_", "monster"),
        ("waf_ecosystem_dragon_", "dragon"),
    ]
    for prefix, asset_class in rules:
        if family_id.startswith(prefix):
            return asset_class
    fail("CategoryRealmGap", f"no asset class for family {family_id}")
    raise AssertionError("unreachable")


def resolve_budget_class(family_id: str) -> str:
    if family_id in RESERVED_BUDGETS:
        return RESERVED_BUDGETS[family_id]
    if family_id in TERRAIN_MACRO:
        return "wbud_terrain_macro"
    if family_id in WATER:
        return "wbud_water"
    if family_id in DECALS:
        return "wbud_decal"
    if family_id in FOLIAGE_MAJOR:
        return "wbud_foliage_major"
    if family_id in STATIC_LARGE:
        return "wbud_static_large"
    if family_id in ARCHITECTURE_HERO:
        return "wbud_architecture_hero"
    if family_id in PROP_LARGE:
        return "wbud_prop_large"
    if family_id in DERIVATIVE_LARGE:
        return "wbud_derivative_25d_large"
    defaults = [
        ("waf_terrain_", "wbud_surface_layer"),
        ("waf_vegetation_", "wbud_foliage_minor"),
        ("waf_geology_", "wbud_static_small"),
        ("waf_traversal_", "wbud_traversal_module"),
        ("waf_architecture_", "wbud_architecture_common"),
        ("waf_interior_", "wbud_interior_module"),
        ("waf_prop_", "wbud_prop_small"),
        ("waf_interactable_", "wbud_interactable"),
        ("waf_harvestable_", "wbud_interactable"),
        ("waf_sign_", "wbud_signage_banner"),
        ("waf_banner_", "wbud_signage_banner"),
        ("waf_vfx_", "wbud_vfx_anchor"),
        ("waf_technical_", "wbud_technical_helper"),
        ("waf_derivative_25d_", "wbud_derivative_25d_small"),
    ]
    matches = [budget for prefix, budget in defaults if family_id.startswith(prefix)]
    if len(matches) != 1:
        fail(
            "BudgetUnassigned",
            f"{family_id} resolves {len(matches)} active budget classes",
        )
    return matches[0]


def build_profiles() -> dict[str, Any]:
    return {
        "coordinateProfiles": [
            {
                "id": "coord_unity_meter_y_up_z_forward_v001",
                "distanceUnit": "meter",
                "unityUnitsPerMeter": 1,
                "sourceUpAxis": "positive_z",
                "sourceForwardAxis": "negative_y",
                "runtimeUpAxis": "positive_y",
                "runtimeForwardAxis": "positive_z",
                "chunkLocalCoordinatesRequired": True,
            }
        ],
        "pivotProfiles": [
            {
                "id": "pivot_base_center_finished_ground_v001",
                "origin": "base_center",
                "verticalDatum": "finished_ground",
                "forwardRule": "semantic_forward",
                "exceptionRequiresApproval": True,
            }
        ],
        "snapGridProfiles": [
            {
                "id": "snap_world_modular_default_v001",
                "authoringSubgridMeters": 0.5,
                "placementCellMeters": 2,
                "structuralBayMeters": 4,
                "verticalTierMeters": 1,
                "rotationIncrementDegrees": 90,
            }
        ],
        "materialShaderProfiles": [
            {
                "id": "material_builtin_pbr_opaque_shared_v001",
                "renderPipeline": "builtin",
                "allowedSurfaceTypes": ["opaque", "alpha_clip"],
                "blendedTransparency": "exception_only",
                "sharedMaterialsRequired": True,
                "runtimeReadWriteDefault": False,
                "requiredMapRoles": ["base_color", "normal", "packed_mask"],
                "forbiddenShaderTraits": [
                    "unbounded_screen_space_sampling",
                    "always_on_edge_emission",
                ],
            }
        ],
        "texelDensityProfiles": [
            {
                "id": "texel_shared_trim_measured_v001",
                "targetPixelsPerMeter": 256,
                "tolerancePercent": 20,
                "sourceResolutionMax": 4096,
                "runtimeResolutionMax": 2048,
                "mipmapsRequired": True,
                "platformCompressionRequired": True,
            }
        ],
        "lodProfiles": [
            {
                "id": "lod_static_world_standard_v001",
                "minimumLevels": 3,
                "normalPlayUsesReducedLod": True,
                "initialTriangleRatioCeilings": [1, 0.6, 0.3, 0.1],
                "crossFadeMustBeProfiled": True,
                "protectedCuePolicy": "silhouette_gameplay_and_realm_cues_survive",
            }
        ],
        "colliderProfiles": [
            {
                "id": "collider_static_simple_v001",
                "allowedModes": [
                    "primitive",
                    "compound_primitive",
                    "low_complexity_static_mesh",
                ],
                "renderMeshColliderForbidden": True,
                "lodIndependent": True,
            }
        ],
        "navigationProfiles": [
            {
                "id": "nav_static_structure_v001",
                "participation": "source_and_exclusions",
                "renderGeometryIsAuthority": False,
                "authoredLinksRequireSockets": True,
                "gameplayTraversalRequiresSeparateAuthority": True,
            }
        ],
        "occlusionProfiles": [
            {
                "id": "occlusion_structure_groups_v001",
                "mode": "separate_groups",
                "requiredGroups": ["roof", "upper_wall"],
                "cameraReachableBackingRequired": True,
            }
        ],
        "streamingProfiles": [
            {
                "id": "stream_chunk_owned_packaged_v001",
                "residency": "chunk_owned",
                "addressablesPolicy": "optional_when_measured",
                "sourceAndPreviewExcludedFromPlayer": True,
                "prefetchMetadataRequired": True,
            }
        ],
    }


def standard_refs() -> dict[str, Any]:
    return {**STANDARD_REF_IDS, "exceptionRefs": []}


def empty_approval(state: str, issue: str | None = None) -> dict[str, Any]:
    return {
        "state": state,
        "reviewer": None,
        "decisionAtUtc": None,
        "evidenceRefs": [],
        "openIssues": [issue] if issue else [],
    }


def empty_budget_measurements() -> dict[str, Any]:
    return {
        "measurementState": "not_measured",
        "baseVariants": None,
        "stateDerivatives": None,
        "lod0Triangles": None,
        "mobileNormalTriangles": None,
        "lod0MaterialSlots": None,
        "mobileDraws": None,
        "textureLongEdgePixels": None,
        "textureFormat": None,
        "colliderPrimitives": None,
        "colliderProxyTriangles": None,
        "navSourceTriangles": None,
        "navLinkPairs": None,
        "navDataBytes": None,
        "activeVfxSources": None,
        "liveParticles": None,
        "transparentDraws": None,
        "dynamicLights": None,
        "activationP95Ms": None,
        "loadReadyP95Ms": None,
        "artifacts": [],
        "placements": [],
    }


def build_bound_asset_records(repo_root: Path) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    source, _ = load_json(repo_root / BUILDING_CATALOG_PATH)
    buildings = source.get("buildings")
    if not isinstance(buildings, list):
        fail("MalformedCatalog", "al_building_catalog buildings must be an array")
    records: list[dict[str, Any]] = []
    aliases: list[dict[str, Any]] = []
    by_id = {building.get("id"): building for building in buildings if isinstance(building, dict)}
    for building_id in ["town_hall", "workshop"]:
        building = by_id.get(building_id)
        if not isinstance(building, dict):
            fail("MissingReference", f"building catalog lacks {building_id}")
        models = building.get("models")
        if not isinstance(models, list) or len(models) != 4:
            fail(
                "CategoryRealmGap",
                f"{building_id} must preserve exactly four realm bindings",
            )
        for model in models:
            realm_id = model.get("realm_id")
            if realm_id not in REALMS:
                fail("MissingReference", f"invalid binding realm {realm_id!r}")
            model_id = model.get("model_id")
            asset_ref = model.get("asset_ref")
            expected_model = f"building_{realm_id}_{building_id}_production_v1"
            if model_id != expected_model or not isinstance(asset_ref, dict):
                fail(
                    "BrokenPrefabBinding",
                    f"unexpected {building_id} model record for {realm_id}",
                )
            asset_id = (
                f"wa_{realm_id}_architecture_building_{building_id}_base_v001"
            )
            family_id = f"waf_architecture_building_{building_id}"
            realm_title = realm_id.title()
            building_title = "Town Hall" if building_id == "town_hall" else "Workshop"
            handoff = (
                "unity/Docs/Architecture/"
                f"{realm_title}_{'TownHall' if building_id == 'town_hall' else 'Workshop'}"
                "_Final_Model_And_Runtime_Binding.md"
            )
            records.append(
                {
                    "assetId": asset_id,
                    "familyId": family_id,
                    "kitId": None,
                    "assetClass": "architecture",
                    "displayLabel": f"{realm_title} {building_title} existing binding",
                    "ownerAuthority": {
                        "finalCreativeOwner": "project_owner",
                        "accountableOwners": ["architecture", "technical_art"],
                        "ownerDecisionRef": handoff,
                    },
                    "placementScope": {
                        "scope": "realm",
                        "realmIds": [realm_id],
                        "taxonomyStatus": "unresolved",
                        "biomeIds": [],
                        "dimensionIds": [],
                        "worldIds": [],
                        "chunkIds": [],
                        "replacementSocketIds": [],
                    },
                    "source": {
                        "status": "legacy_runtime_only",
                        "sourcePacketId": f"{realm_id}_{building_id}_final_runtime_binding",
                        "sourceVersion": "1",
                        "artifacts": [
                            {"kind": "handoff", "path": handoff, "sha256": None}
                        ],
                        "derivationRevision": "existing_binding_preserved",
                    },
                    "provenance": {
                        "state": "incomplete",
                        "creatorIdentities": [],
                        "authoringTools": [],
                        "sourceReferences": [f"al_building_catalog:model:{model_id}"],
                        "rights": {
                            "state": "unknown",
                            "declaration": (
                                "Retained existing runtime binding; source chain, rights, and similarity evidence remain required before post-MVP promotion."
                            ),
                        },
                        "aiUse": "unknown",
                        "cleanupStatus": "not_recorded",
                        "similarityReview": "not_reviewed",
                        "humanDecisionRef": handoff,
                        "openIssues": [
                            "Complete source chain of custody, rights, and physical mobile-floor evidence before promotion."
                        ],
                    },
                    "binding": {
                        "bindingState": "candidate",
                        "prefab": {
                            "path": asset_ref.get("path"),
                            "guid": asset_ref.get("guid"),
                            "sha256": asset_ref.get("sha256"),
                        },
                        "addressable": None,
                        "runtimeDependencies": [],
                        "bindingEvidenceRefs": [
                            f"al_building_catalog:model:{model_id}"
                        ],
                        "unboundReason": None,
                    },
                    "standards": standard_refs(),
                    "geometry": {
                        "representation": "spatial_3d",
                        "dimensionsMeters": None,
                        "measuredScale": None,
                        "pivotVerified": False,
                    },
                    "materials": {
                        "materialSlotCount": None,
                        "materialFamilyIds": [],
                        "shaderIds": [],
                        "surfaceTypes": [],
                        "sharedOrInstanced": "unverified",
                    },
                    "texelDensity": {
                        "status": "not_measured",
                        "measuredPixelsPerMeter": None,
                        "evidenceRefs": [],
                    },
                    "lod": {
                        "status": "not_verified",
                        "levels": [],
                        "protectedCues": [
                            "footprint",
                            "entrance",
                            "roofline",
                            "realm_construction",
                        ],
                        "notApplicableReason": None,
                    },
                    "impostor": {
                        "mode": "unresolved",
                        "reason": (
                            "Long-range representation requires measured camera and mobile-floor evidence."
                        ),
                        "sourceAssetId": None,
                    },
                    "collider": {
                        "participation": "unverified",
                        "sourceObjectIds": [],
                        "lodIndependent": True,
                        "reason": "Existing binding must be inspected before promotion.",
                    },
                    "navigation": {
                        "participation": "unverified",
                        "sourceObjectIds": [],
                        "linkSocketIds": [],
                        "navmeshAreaProfileId": None,
                        "reason": "Traversal authority remains outside render geometry.",
                    },
                    "occlusion": {
                        "participation": "unverified",
                        "groupIds": [],
                        "portalIds": [],
                        "reason": "Occlusion grouping requires inspection.",
                    },
                    "streaming": {
                        "residency": "packaged_current",
                        "chunkIds": [],
                        "bundleId": None,
                        "prefetchRing": "not_assigned",
                        "estimatedResidentBytes": None,
                        "measuredResidentBytes": None,
                        "measuredBuildSizeBytes": None,
                    },
                    "modularity": {
                        "isModular": True,
                        "socketIds": [],
                        "gridExceptionRef": None,
                    },
                    "anchors": [],
                    "vfxAnchor": None,
                    "derivative25d": None,
                    "budgetClassId": resolve_budget_class(family_id),
                    "budgetMeasurements": empty_budget_measurements(),
                    "approvals": {
                        "technical": empty_approval("not_tested"),
                        "creative": empty_approval("not_reviewed"),
                        "provenance": empty_approval("incomplete"),
                        "performance": empty_approval("not_measured"),
                        "accessibility": empty_approval("not_tested"),
                        "releaseGate": empty_approval(
                            "held",
                            "MVP review and integrated roadmap approval gate do not grant generation or activation; positive admission remains blocked.",
                        ),
                    },
                    "lifecycle": {
                        "inventoryState": "current_bound_partial",
                        "preserveMvpBinding": True,
                        "replacesAssetIds": [],
                        "replacedByAssetId": None,
                        "deprecationReason": None,
                    },
                }
            )
            dotted_kind = "townhall" if building_id == "town_hall" else "workshop"
            for legacy_id in [
                f"building.{realm_id}.{dotted_kind}.production.v1",
                model_id,
            ]:
                aliases.append(
                    {
                        "legacyId": legacy_id,
                        "canonicalAssetId": asset_id,
                        "introducedContentVersion": "0.1.0",
                        "retirementContentVersion": None,
                        "migrationRef": TAXONOMY_PATH.as_posix(),
                    }
                )
    records.sort(key=lambda row: row["assetId"].encode("utf-8"))
    aliases.sort(key=lambda row: row["legacyId"].encode("utf-8"))
    return records, aliases


def combined_source_revision(repo_root: Path) -> str:
    digest = hashlib.sha256()
    for path in [TAXONOMY_PATH, BUDGET_PATH, STANDARD_PATH, BUILDING_CATALOG_PATH]:
        raw = read_canonical_source(repo_root, path)
        digest.update(path.as_posix().encode("utf-8"))
        digest.update(b"\0")
        digest.update(raw)
        digest.update(b"\0")
    return "sha256_" + digest.hexdigest()


def build_catalog(repo_root: Path) -> dict[str, Any]:
    families = parse_taxonomy(repo_root)
    records, aliases = build_bound_asset_records(repo_root)
    return {
        "gameId": "another-life",
        "catalogId": "al_world_asset_inventory",
        "family": "world_assets",
        "schemaVersion": 1,
        "contentVersion": "0.1.0",
        "sourceRevision": combined_source_revision(repo_root),
        "idFormat": "lowercase_ascii_snake_case",
        "authority": {
            "catalogOwner": "world_asset_inventory",
            "finalCreativeOwner": "project_owner",
            "ownerDecisionRef": "kanban:t_981d50c8",
            "status": "preparation_held",
        },
        "gatePolicy": {
            "generationState": "held",
            "activationState": "held",
            "requiredGateIds": GATE_IDS,
        },
        "profiles": build_profiles(),
        "familyRecords": families,
        "records": records,
        "aliases": aliases,
    }


def profile_ids(catalog: dict[str, Any]) -> set[str]:
    profiles = catalog.get("profiles")
    if not isinstance(profiles, dict):
        fail("ProfileMissing", "profiles must be an object")
    ids: set[str] = set()
    for group in PROFILE_GROUPS:
        rows = profiles.get(group)
        if not isinstance(rows, list) or not rows:
            fail("ProfileMissing", f"profile group {group} is missing or empty")
        group_ids = [row.get("id") for row in rows if isinstance(row, dict)]
        if len(group_ids) != len(rows) or any(not isinstance(value, str) for value in group_ids):
            fail("ProfileMissing", f"profile group {group} has malformed IDs")
        if group_ids != sorted(group_ids, key=lambda value: value.encode("utf-8")):
            fail("CanonicalOrderViolation", f"profile group {group} is unsorted")
        for value in group_ids:
            if value in ids:
                fail("DuplicateId", f"duplicate profile ID {value}")
            ids.add(value)
    return ids


def validate_standard_refs(row: dict[str, Any], known_profiles: set[str], label: str) -> None:
    standards = row.get("standards")
    if not isinstance(standards, dict):
        fail("ProfileMissing", f"{label} lacks standards")
    for field, expected_id in STANDARD_REF_IDS.items():
        value = standards.get(field)
        if value != expected_id or value not in known_profiles:
            fail("ProfileMissing", f"{label}.{field} does not resolve: {value!r}")
    if standards.get("exceptionRefs") != []:
        fail("GateConflict", f"{label} has unapproved standard exceptions")


def validate_prefab(repo_root: Path, asset_id: str, prefab: dict[str, Any]) -> None:
    path_value = prefab.get("path")
    guid = prefab.get("guid")
    expected_hash = prefab.get("sha256")
    if not isinstance(path_value, str) or not path_value.startswith("Assets/"):
        fail("BrokenPrefabBinding", f"{asset_id} has invalid prefab path")
    logical_path = PurePosixPath(path_value)
    if (
        "\\" in path_value
        or logical_path.is_absolute()
        or path_value != logical_path.as_posix()
        or any(part in {"", ".", ".."} for part in logical_path.parts)
    ):
        fail("UnsafeBindingPath", f"{asset_id} prefab path is not canonical and contained")
    asset_root = (repo_root / "unity" / "Assets").resolve()
    prefab_path = (repo_root / "unity" / Path(*logical_path.parts)).resolve()
    try:
        prefab_path.relative_to(asset_root)
    except ValueError:
        fail("UnsafeBindingPath", f"{asset_id} prefab path escapes Unity Assets")
    meta_path = Path(str(prefab_path) + ".meta")
    if not prefab_path.is_file() or not meta_path.is_file():
        fail("BrokenPrefabBinding", f"{asset_id} prefab or .meta is missing")
    meta_text = meta_path.read_text(encoding="utf-8")
    match = re.search(r"(?m)^guid: ([0-9a-f]{32})$", meta_text)
    if match is None or match.group(1) != guid:
        fail("BrokenPrefabBinding", f"{asset_id} prefab GUID does not match .meta")
    actual_hash = sha256(prefab_path.read_bytes())
    if actual_hash != expected_hash:
        fail("HashMismatch", f"{asset_id} prefab SHA-256 drifted")


def validate_budget_measurements(record: dict[str, Any]) -> dict[str, Any] | None:
    measurement = record.get("budgetMeasurements")
    asset_id = record.get("assetId", "<unknown>")
    if not isinstance(measurement, dict):
        fail("BudgetUnassigned", f"{asset_id} lacks budgetMeasurements")
    state = measurement.get("measurementState")
    scalar_fields = [
        "baseVariants",
        "stateDerivatives",
        "lod0Triangles",
        "mobileNormalTriangles",
        "lod0MaterialSlots",
        "mobileDraws",
        "textureLongEdgePixels",
        "colliderPrimitives",
        "colliderProxyTriangles",
        "navSourceTriangles",
        "navLinkPairs",
        "navDataBytes",
        "activeVfxSources",
        "liveParticles",
        "transparentDraws",
        "dynamicLights",
        "activationP95Ms",
        "loadReadyP95Ms",
    ]
    if state == "not_measured":
        if any(measurement.get(field) is not None for field in scalar_fields):
            fail("BudgetUnassigned", f"{asset_id} has partial unmeasured scalar costs")
        if measurement.get("textureFormat") is not None:
            fail("BudgetUnassigned", f"{asset_id} has partial unmeasured texture format")
        if measurement.get("artifacts") != [] or measurement.get("placements") != []:
            fail("BudgetUnassigned", f"{asset_id} has partial unmeasured aggregates")
        if record.get("lifecycle", {}).get("inventoryState") == "production_approved":
            fail("GateConflict", f"{asset_id} is approved without measured budgets")
        return None
    if state != "measured":
        fail("BudgetUnassigned", f"{asset_id} has invalid measurement state {state!r}")
    if record.get("budgetClassId", "").startswith("wbud_reserved_"):
        fail("GateConflict", f"reserved family asset {asset_id} cannot be measured active")
    for field in scalar_fields:
        value = measurement.get(field)
        if not isinstance(value, (int, float)) or isinstance(value, bool) or value < 0:
            fail("BudgetUnassigned", f"{asset_id}.{field} is not measured")
    if not isinstance(measurement.get("textureFormat"), str):
        fail("BudgetUnassigned", f"{asset_id}.textureFormat is not measured")
    artifacts = measurement.get("artifacts")
    placements = measurement.get("placements")
    if not isinstance(artifacts, list) or not artifacts:
        fail("BudgetUnassigned", f"{asset_id} lacks measured artifacts")
    if not isinstance(placements, list) or not placements:
        fail("BudgetUnassigned", f"{asset_id} lacks measured placements")
    budget_class = record.get("budgetClassId")
    ceilings = BUDGET_CEILINGS.get(budget_class)
    if ceilings is None:
        fail("BudgetUnassigned", f"{asset_id} class {budget_class!r} has no active ceiling")
    fields = [
        "baseVariants",
        "stateDerivatives",
        "lod0Triangles",
        "mobileNormalTriangles",
        "lod0MaterialSlots",
        "mobileDraws",
    ]
    for field, ceiling in zip(fields, ceilings[:6]):
        if measurement[field] > ceiling:
            fail("BudgetOverrun", f"{asset_id}.{field} exceeds {budget_class}")
    resident = sum(item.get("residentBytes", -1) for item in artifacts if isinstance(item, dict))
    delivery = sum(item.get("compressedDeliveryBytes", -1) for item in artifacts if isinstance(item, dict))
    installed = sum(item.get("installedBytes", -1) for item in artifacts if isinstance(item, dict))
    if min(resident, delivery, installed) < 0:
        fail("BudgetUnassigned", f"{asset_id} artifact byte metrics are incomplete")
    if resident > ceilings[6] * MIB:
        fail("BudgetOverrun", f"{asset_id} resident bytes exceed {budget_class}")
    if delivery > ceilings[7] * MIB:
        fail("BudgetOverrun", f"{asset_id} compressed delivery exceeds {budget_class}")
    if installed > ceilings[8] * MIB:
        fail("BudgetOverrun", f"{asset_id} installed bytes exceed {budget_class}")
    if measurement["activationP95Ms"] > ceilings[9]:
        fail("BudgetOverrun", f"{asset_id} activation p95 exceeds {budget_class}")
    return measurement


def deduplicated_artifact_totals(
    records: list[dict[str, Any]],
    predicate: Callable[[dict[str, Any], dict[str, Any]], bool],
) -> dict[str, int]:
    artifacts: dict[str, dict[str, int]] = {}
    for record in records:
        measurement = record.get("budgetMeasurements", {})
        if measurement.get("measurementState") != "measured":
            continue
        placements = measurement.get("placements", [])
        if not any(predicate(record, placement) for placement in placements):
            continue
        for artifact in measurement.get("artifacts", []):
            digest = artifact.get("sha256")
            metrics = {
                "compressedDeliveryBytes": artifact.get("compressedDeliveryBytes"),
                "installedBytes": artifact.get("installedBytes"),
                "residentBytes": artifact.get("residentBytes"),
                "loadIoBytes": artifact.get("loadIoBytes"),
            }
            if digest in artifacts and artifacts[digest] != metrics:
                fail("HashMismatch", f"artifact {digest} has conflicting byte metrics")
            artifacts[digest] = metrics
    return {
        metric: sum(item[metric] for item in artifacts.values())
        for metric in [
            "compressedDeliveryBytes",
            "installedBytes",
            "residentBytes",
            "loadIoBytes",
        ]
    }


def validate_aggregate_budgets(records: list[dict[str, Any]]) -> dict[str, Any]:
    measured = [
        record
        for record in records
        if record.get("budgetMeasurements", {}).get("measurementState") == "measured"
    ]
    if not measured:
        return {
            "measurementState": "blocked_no_measured_runtime_costs",
            "measuredRecordCount": 0,
            "globalCompressedDeliveryBytes": None,
            "globalInstalledBytes": None,
            "globalResidentBytes": None,
            "globalLoadIoBytes": None,
        }
    global_totals = deduplicated_artifact_totals(
        measured, lambda _record, _placement: True
    )
    if global_totals["compressedDeliveryBytes"] > 1.25 * GIB:
        fail("AggregateBudgetOverrun", "global world-asset compressed delivery exceeds 1.25 GiB")
    if global_totals["installedBytes"] > 2.5 * GIB:
        fail("AggregateBudgetOverrun", "global installed world assets exceed 2.5 GiB")
    for realm_id in REALMS:
        totals = deduplicated_artifact_totals(
            measured,
            lambda _record, placement, realm_id=realm_id: placement.get("realmId")
            == realm_id,
        )
        if totals["compressedDeliveryBytes"] > 256 * MIB:
            fail("AggregateBudgetOverrun", f"{realm_id} compressed delivery exceeds 256 MiB")
        if totals["installedBytes"] > 512 * MIB:
            fail("AggregateBudgetOverrun", f"{realm_id} installed assets exceed 512 MiB")
    scene_ids = sorted(
        {
            placement["sceneId"]
            for record in measured
            for placement in record["budgetMeasurements"]["placements"]
        }
    )
    for scene_id in scene_ids:
        scene_records = [
            (record, placement)
            for record in measured
            for placement in record["budgetMeasurements"]["placements"]
            if placement["sceneId"] == scene_id
        ]
        triangles = sum(
            record["budgetMeasurements"]["mobileNormalTriangles"]
            * placement["visibleInstances"]
            for record, placement in scene_records
        )
        draws = sum(
            record["budgetMeasurements"]["mobileDraws"]
            * placement["visibleInstances"]
            for record, placement in scene_records
        )
        totals = deduplicated_artifact_totals(
            measured,
            lambda _record, placement, scene_id=scene_id: placement.get("sceneId")
            == scene_id,
        )
        if triangles > 1_200_000 or draws > 650 or totals["residentBytes"] > 768 * MIB:
            fail("AggregateBudgetOverrun", f"scene {scene_id} exceeds mobile-floor envelope")
    cell_limits = {
        "interaction": (64, 128, 64, 192),
        "prefetch": (32, 64, 32, 64),
        "horizon": (8, 16, 8, 16),
    }
    cell_keys = sorted(
        {
            (placement["cellId"], placement["ring"])
            for record in measured
            for placement in record["budgetMeasurements"]["placements"]
        }
    )
    for cell_id, ring in cell_keys:
        totals = deduplicated_artifact_totals(
            measured,
            lambda _record, placement, cell_id=cell_id, ring=ring: (
                placement.get("cellId") == cell_id and placement.get("ring") == ring
            ),
        )
        limits = cell_limits[ring]
        values = [
            totals["compressedDeliveryBytes"],
            totals["installedBytes"],
            totals["loadIoBytes"],
            totals["residentBytes"],
        ]
        if any(value > limit * MIB for value, limit in zip(values, limits)):
            fail("AggregateBudgetOverrun", f"cell {cell_id} {ring} envelope exceeded")
    return {
        "measurementState": "measured",
        "measuredRecordCount": len(measured),
        "globalCompressedDeliveryBytes": global_totals["compressedDeliveryBytes"],
        "globalInstalledBytes": global_totals["installedBytes"],
        "globalResidentBytes": global_totals["residentBytes"],
        "globalLoadIoBytes": global_totals["loadIoBytes"],
    }


def validate_catalog(repo_root: Path, catalog: dict[str, Any]) -> dict[str, Any]:
    if not isinstance(catalog, dict):
        fail("MalformedCatalog", "catalog root must be an object")
    gate = catalog.get("gatePolicy")
    if not isinstance(gate, dict) or gate.get("generationState") != "held" or gate.get("activationState") != "held":
        fail("GateConflict", "schema v1 generation and activation must remain held")
    if gate.get("requiredGateIds") != GATE_IDS:
        fail("GateConflict", "required gate IDs or canonical order drifted")
    authority = catalog.get("authority")
    if not isinstance(authority, dict) or authority.get("finalCreativeOwner") != "project_owner" or not authority.get("ownerDecisionRef"):
        fail("OwnerAuthorityMissing", "catalog owner authority is incomplete")
    families = catalog.get("familyRecords")
    records = catalog.get("records")
    aliases = catalog.get("aliases")
    if not isinstance(families, list) or not isinstance(records, list) or not isinstance(aliases, list):
        fail("MalformedCatalog", "familyRecords, records, and aliases must be arrays")
    preliminary_family_ids = [
        row.get("familyId") for row in families if isinstance(row, dict)
    ]
    if len(preliminary_family_ids) != len(families):
        fail("MalformedCatalog", "family record must be an object")
    if len(set(preliminary_family_ids)) != len(preliminary_family_ids):
        fail("DuplicateId", "duplicate family ID")
    if any(
        not isinstance(family_id, str)
        or not FAMILY_ID_RE.fullmatch(family_id)
        for family_id in preliminary_family_ids
    ):
        fail("MalformedCatalog", "one or more family IDs are malformed")
    if preliminary_family_ids != sorted(
        preliminary_family_ids,
        key=lambda value: value.encode("utf-8"),
    ):
        fail("CanonicalOrderViolation", "family records are not bytewise sorted")
    family_ids: list[str] = []
    known_profiles = profile_ids(catalog)
    expected_families = {row["familyId"]: row for row in parse_taxonomy(repo_root)}
    category_counts: Counter[str] = Counter()
    realm_counts: dict[str, Counter[str]] = {realm: Counter() for realm in REALMS}
    for row in families:
        if not isinstance(row, dict):
            fail("MalformedCatalog", "family record must be an object")
        family_id = row.get("familyId")
        if not isinstance(family_id, str) or not FAMILY_ID_RE.fullmatch(family_id):
            fail("MalformedCatalog", f"malformed family ID {family_id!r}")
        family_ids.append(family_id)
        expected = expected_families.get(family_id)
        if expected is None:
            fail("CategoryRealmGap", f"unknown family {family_id}")
        if row.get("ownerAuthority") is None:
            fail("OwnerAuthorityMissing", f"{family_id} lacks owner authority")
        owner = row["ownerAuthority"]
        if not isinstance(owner, dict) or owner.get("finalCreativeOwner") != "project_owner" or not owner.get("accountableOwners") or not owner.get("ownerDecisionRef"):
            fail("OwnerAuthorityMissing", f"{family_id} owner authority is incomplete")
        provenance = row.get("provenance")
        if not isinstance(provenance, dict) or provenance.get("state") != "logical_authority_only" or len(provenance.get("sourceReferences", [])) < 3 or not provenance.get("humanDecisionRef"):
            fail("ProvenanceBlocked", f"{family_id} logical provenance is incomplete")
        validate_standard_refs(row, known_profiles, family_id)
        budget_class = row.get("budgetClassId")
        if budget_class != resolve_budget_class(family_id) or "unassigned" in str(budget_class):
            fail("BudgetUnassigned", f"{family_id} budget assignment drifted")
        realm_cells = row.get("realmApplicability")
        if not isinstance(realm_cells, list) or len(realm_cells) != 4 or [cell.get("realmId") for cell in realm_cells if isinstance(cell, dict)] != REALMS:
            fail("CategoryRealmGap", f"{family_id} lacks canonical four-realm coverage")
        for cell in realm_cells:
            mode = cell.get("mode")
            reason = cell.get("reason")
            if mode not in {"realm_variant", "shared", "excluded"} or (mode == "excluded" and not reason) or (mode != "excluded" and reason is not None):
                fail("CategoryRealmGap", f"{family_id} has malformed realm applicability")
            realm_counts[cell["realmId"]][mode] += 1
        if row.get("generationState") != "held" or row.get("activationState") != "held" or row.get("approvalState") != "preparation_held":
            fail("GateConflict", f"{family_id} is not held")
        for field in [
            "displayLabel",
            "assetClass",
            "purpose",
            "requiredVariants",
            "ownerAuthority",
            "realmApplicability",
            "taxonomyStatus",
            "dependencies",
            "schedule",
            "biomeIds",
        ]:
            if row.get(field) != expected.get(field):
                fail("CategoryRealmGap", f"{family_id}.{field} drifted from taxonomy")
        category_counts[row["assetClass"]] += 1
    if len(family_ids) != EXPECTED_FAMILY_COUNT or set(family_ids) != set(expected_families):
        fail("CategoryRealmGap", "family coverage is not exactly 242/242")
    if len(set(family_ids)) != len(family_ids):
        fail("DuplicateId", "duplicate family ID")
    if set(category_counts) != set(ASSET_CLASSES):
        fail("CategoryRealmGap", "one or more requested asset classes are absent")

    asset_ids = [row.get("assetId") for row in records if isinstance(row, dict)]
    if asset_ids != sorted(asset_ids, key=lambda value: str(value).encode("utf-8")):
        fail("CanonicalOrderViolation", "asset records are not bytewise sorted")
    if len(records) != EXPECTED_BINDING_COUNT:
        fail("CategoryRealmGap", f"expected 8 preserved asset bindings, found {len(records)}")
    if len(set(asset_ids)) != len(asset_ids):
        fail("DuplicateId", "duplicate asset ID")
    prefab_paths: set[str] = set()
    prefab_guids: set[str] = set()
    measured_records: list[dict[str, Any]] = []
    for record in records:
        asset_id = record.get("assetId")
        if not isinstance(asset_id, str) or not ASSET_ID_RE.fullmatch(asset_id):
            fail("MalformedCatalog", f"malformed asset ID {asset_id!r}")
        family_id = record.get("familyId")
        if family_id not in expected_families:
            fail("MissingReference", f"{asset_id} references unknown family {family_id}")
        owner = record.get("ownerAuthority")
        if not isinstance(owner, dict) or owner.get("finalCreativeOwner") != "project_owner" or not owner.get("accountableOwners") or not owner.get("ownerDecisionRef"):
            fail("OwnerAuthorityMissing", f"{asset_id} owner authority is incomplete")
        provenance = record.get("provenance")
        if not isinstance(provenance, dict) or not provenance.get("humanDecisionRef") or not provenance.get("sourceReferences"):
            fail("ProvenanceBlocked", f"{asset_id} provenance is incomplete")
        validate_standard_refs(record, known_profiles, asset_id)
        if record.get("budgetClassId") != resolve_budget_class(family_id):
            fail("BudgetUnassigned", f"{asset_id} budget assignment drifted")
        binding = record.get("binding")
        if not isinstance(binding, dict) or binding.get("bindingState") != "candidate":
            fail("BrokenPrefabBinding", f"{asset_id} binding state drifted")
        prefab = binding.get("prefab")
        if not isinstance(prefab, dict) or binding.get("addressable") is not None:
            fail("BrokenPrefabBinding", f"{asset_id} must preserve prefab-only binding")
        validate_prefab(repo_root, asset_id, prefab)
        if prefab["path"] in prefab_paths or prefab["guid"] in prefab_guids:
            fail("DuplicateId", f"{asset_id} duplicates prefab path or GUID")
        prefab_paths.add(prefab["path"])
        prefab_guids.add(prefab["guid"])
        if record.get("lifecycle", {}).get("inventoryState") != "current_bound_partial" or record.get("lifecycle", {}).get("preserveMvpBinding") is not True:
            fail("GateConflict", f"{asset_id} does not preserve current MVP binding")
        release = record.get("approvals", {}).get("releaseGate", {})
        if release.get("state") != "held":
            fail("GateConflict", f"{asset_id} release gate is not held")
        measurement = validate_budget_measurements(record)
        if measurement is not None:
            measured_records.append(record)

    legacy_ids = [row.get("legacyId") for row in aliases if isinstance(row, dict)]
    if legacy_ids != sorted(legacy_ids, key=lambda value: str(value).encode("utf-8")):
        fail("CanonicalOrderViolation", "aliases are not bytewise sorted")
    if len(aliases) != EXPECTED_ALIAS_COUNT or len(set(legacy_ids)) != len(legacy_ids):
        fail("DuplicateId", "alias coverage must be 16 unique IDs")
    if set(legacy_ids) & set(asset_ids):
        fail("DuplicateId", "alias shadows a canonical asset ID")
    for alias in aliases:
        if alias.get("canonicalAssetId") not in set(asset_ids):
            fail("MissingReference", f"alias {alias.get('legacyId')} has no target")

    aggregate = validate_aggregate_budgets(records)
    try:
        from jsonschema import Draft202012Validator
    except ImportError:
        Draft202012Validator = None
    if Draft202012Validator is not None:
        schema, _ = load_json(repo_root / SCHEMA_PATH)
        validator = Draft202012Validator(schema)
        errors = sorted(validator.iter_errors(catalog), key=lambda error: list(error.absolute_path))
        if errors:
            error = errors[0]
            location = ".".join(str(part) for part in error.absolute_path) or "<root>"
            fail("MalformedCatalog", f"schema rejected {location}: {error.message}")

    for source_root in [repo_root / "unity/Assets/AL/Scripts"]:
        if not source_root.exists():
            continue
        for source_file in sorted(source_root.rglob("*.cs")):
            text = source_file.read_text(encoding="utf-8", errors="replace")
            if re.search(r"\b(?:waf_|wak_|wad_|wa_(?:shared|neutral|crownlands|stonehold|eldergrove|umbral|kingdom_|event_))", text):
                fail(
                    "MalformedCatalog",
                    f"hardcoded world-asset identity found in {source_file.relative_to(repo_root)}",
                )

    duplicate_count = (
        len(family_ids) - len(set(family_ids))
        + len(asset_ids) - len(set(asset_ids))
        + len(legacy_ids) - len(set(legacy_ids))
    )
    class_counts = dict(sorted(Counter(row["budgetClassId"] for row in families).items()))
    return {
        "schemaVersion": 1,
        "evidenceId": "post_mvp_world_asset_inventory_acceptance_v1",
        "inventory": {
            "path": CATALOG_PATH.as_posix(),
            "contentVersion": catalog.get("contentVersion"),
            "sourceRevision": catalog.get("sourceRevision"),
            "rawSha256": sha256(canonical_json(catalog)),
            "familyRecordCount": len(families),
            "assetRecordCount": len(records),
            "aliasCount": len(aliases),
        },
        "sourceInputs": [
            source_input_evidence(repo_root, path)
            for path in [
                TAXONOMY_PATH,
                BUDGET_PATH,
                STANDARD_PATH,
                SCHEMA_PATH,
                BUILDING_CATALOG_PATH,
                GENERATOR_PATH,
            ]
        ],
        "familyCoverage": {
            "required": EXPECTED_FAMILY_COUNT,
            "covered": len(families),
            "missing": [],
            "assetClassCounts": dict(sorted(category_counts.items())),
        },
        "realmCoverage": {
            "cells": len(families) * len(REALMS),
            "realms": [
                {
                    "realmId": realm,
                    "realmVariant": realm_counts[realm]["realm_variant"],
                    "shared": realm_counts[realm]["shared"],
                    "excluded": realm_counts[realm]["excluded"],
                }
                for realm in REALMS
            ],
        },
        "duplicateAndAliasReport": {
            "duplicateCount": duplicate_count,
            "canonicalFamilyIds": len(family_ids),
            "canonicalAssetIds": len(asset_ids),
            "uniqueAliases": len(legacy_ids),
            "aliasChains": 0,
            "canonicalShadowingAliases": 0,
        },
        "ownerAuthorityCoverage": {
            "required": len(families) + len(records),
            "complete": len(families) + len(records),
            "finalCreativeOwner": "project_owner",
        },
        "bindingCoverage": {
            "preservedCurrentBindings": len(records),
            "verifiedPrefabTuples": len(prefab_paths),
            "addressableBindings": 0,
            "unresolvedOrBrokenBindings": 0,
        },
        "budgetRollup": {
            "assignedFamilies": len(families),
            "classCount": len(class_counts),
            "classCounts": class_counts,
            **aggregate,
        },
        "approvalState": {
            "catalogStatus": catalog.get("authority", {}).get("status"),
            "generationState": gate.get("generationState"),
            "activationState": gate.get("activationState"),
            "positiveFamilyStates": 0,
            "positiveReleaseStates": 0,
            "note": (
                "Preparation only. MVP review and integrated roadmap approval permit catalog assembly but do not authorize asset generation, activation, or runtime migration."
            ),
        },
    }


def make_measured_budget_overrun(record: dict[str, Any]) -> None:
    measurement = record["budgetMeasurements"]
    measurement.update(
        {
            "measurementState": "measured",
            "baseVariants": 1,
            "stateDerivatives": 1,
            "lod0Triangles": 100,
            "mobileNormalTriangles": 50,
            "lod0MaterialSlots": 1,
            "mobileDraws": 1,
            "textureLongEdgePixels": 1024,
            "textureFormat": "astc_6x6",
            "colliderPrimitives": 1,
            "colliderProxyTriangles": 0,
            "navSourceTriangles": 0,
            "navLinkPairs": 0,
            "navDataBytes": 0,
            "activeVfxSources": 0,
            "liveParticles": 0,
            "transparentDraws": 0,
            "dynamicLights": 0,
            "activationP95Ms": 0.1,
            "loadReadyP95Ms": 100,
            "artifacts": [
                {
                    "sha256": "f" * 64,
                    "compressedDeliveryBytes": 100 * GIB,
                    "installedBytes": 100 * GIB,
                    "residentBytes": 100 * GIB,
                    "loadIoBytes": 100 * GIB,
                }
            ],
            "placements": [
                {
                    "realmId": record["placementScope"]["realmIds"][0],
                    "sceneId": "negative_budget_scene",
                    "cellId": "negative_budget_cell",
                    "ring": "interaction",
                    "visibleInstances": 1,
                }
            ],
        }
    )


def write_exact(path: Path, raw: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(raw)
    os.replace(temporary, path)


def validate_committed_outputs(repo_root: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    expected = build_catalog(repo_root)
    expected_raw = canonical_json(expected)
    generated_again = canonical_json(build_catalog(repo_root))
    if expected_raw != generated_again:
        fail("CanonicalOrderViolation", "two independent generations differ")
    committed, committed_raw = load_json(repo_root / CATALOG_PATH)
    if committed_raw != expected_raw or canonical_json(committed) != committed_raw:
        fail("CanonicalOrderViolation", "committed inventory is not canonical generated JSON")
    expected_evidence = validate_catalog(repo_root, committed)
    evidence, evidence_raw = load_json(repo_root / EVIDENCE_PATH)
    expected_evidence_raw = canonical_json(expected_evidence)
    if evidence_raw != expected_evidence_raw or evidence != expected_evidence:
        fail("CanonicalOrderViolation", "committed acceptance evidence drifted")
    return committed, evidence


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--write",
        action="store_true",
        help="write the canonical held inventory and acceptance evidence",
    )
    args = parser.parse_args()
    repo_root = Path(__file__).resolve().parents[3]
    try:
        catalog = build_catalog(repo_root)
        catalog_raw = canonical_json(catalog)
        evidence = validate_catalog(repo_root, catalog)
        evidence_raw = canonical_json(evidence)
        if args.write:
            write_exact(repo_root / CATALOG_PATH, catalog_raw)
            write_exact(repo_root / EVIDENCE_PATH, evidence_raw)
        else:
            validate_committed_outputs(repo_root)
    except InventoryValidationError as error:
        print(f"World asset inventory validation failed: {error}", file=sys.stderr)
        return 1
    print("PASS: authoritative world-asset inventory validates")
    print("PASS: 242 families, 8 preserved prefab bindings, and 16 aliases covered")
    print("PASS: generation and activation remain held")
    print(f"PASS: inventory raw SHA-256 {sha256(catalog_raw)}")
    print(f"PASS: evidence raw SHA-256 {sha256(evidence_raw)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
