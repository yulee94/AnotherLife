#!/usr/bin/env python3
"""Validate the non-production Phase C building v003 technical-source overlay."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any, Callable


ERROR_PREFIX = "Phase C building v003 technical-source validation failed"
BASE_CANDIDATE_ID = (
    "game-data-phase-c-six-family-technical-source-2026-07-23-v001"
)
V002_CANDIDATE_ID = (
    "game-data-phase-c-six-family-technical-source-2026-07-29-v002"
)
V003_CANDIDATE_ID = (
    "game-data-phase-c-six-family-technical-source-2026-07-29-v003"
)
BASE_PATH = (
    "unity/Docs/GameDataCatalog/PhaseC/"
    "phase-c-six-family-technical-source.json"
)
V002_PATH = (
    "unity/Docs/GameDataCatalog/PhaseC/"
    "phase-c-six-family-technical-source-v002.json"
)
V003_PATH = (
    "unity/Docs/GameDataCatalog/PhaseC/"
    "phase-c-six-family-technical-source-v003.json"
)
BASE_RAW_SHA256 = (
    "5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b"
)
V002_REVISION = "d219472073bee9fcd420d0cac1d94412019b865b"
V002_RAW_SHA256 = (
    "60498d1a071ea79eb37c1b8889a1faaa5c7aee69679c1043256535ef4d3c1685"
)
FAMILY_ORDER = [
    "realms",
    "buildings",
    "research",
    "troops",
    "champions",
    "skills",
]
TARGET_LEVELS = list(range(1, 11))
BASE_BUDGETS = [100, 175, 300, 475, 700, 1000, 1400, 1900, 2500, 3250]
DURATIONS = [10, 30, 120, 300, 900, 1800, 3600, 7200, 14400, 28800]
REALM_IDS = ["crownlands", "stonehold", "eldergrove", "umbral"]
RESOLVED_BUILDING_BLOCKERS = [
    "buildings.max_level_review",
    "buildings.cost_profiles",
    "buildings.duration_profiles",
]
RETAINED_BUILDING_BLOCKERS = [
    "buildings.production_profiles",
    "buildings.asset_refs",
]
RETAINED_FAMILY_BLOCKERS = {
    "research": [
        "research.max_levels",
        "research.cost_profiles",
        "research.duration_profiles",
        "research.effects",
        "research.prerequisites",
    ],
    "troops": [
        "troops.records",
        "troops.localization",
        "troops.base_stats",
        "troops.training_profiles",
        "troops.asset_refs",
    ],
    "champions": [
        "champions.records",
        "champions.localization",
        "champions.realm_class_assignments",
        "champions.asset_refs",
        "champions.base_skill_refs",
        "champions.stat_profiles",
    ],
    "skills": [
        "skills.slot_policy",
        "skills.behavior_profiles",
        "skills.presentation_profiles",
        "skills.target_authority",
        "skills.audio_asset_refs",
        "skills.vfx_asset_refs",
        "skills.balance_acceptance",
    ],
}
BUILDINGS = [
    ("town_hall", "TownHall"),
    ("farm", "Farm"),
    ("lumber_mill", "LumberMill"),
    ("quarry", "Quarry"),
    ("gold_mine", "GoldMine"),
    ("barracks", "Barracks"),
    ("academy", "Academy"),
    ("market", "Market"),
    ("storehouse", "Storehouse"),
    ("forge", "Forge"),
    ("stable", "Stable"),
    ("workshop", "Workshop"),
    ("embassy", "Embassy"),
    ("wall", "Wall"),
    ("watchtower", "Watchtower"),
]
COST_PROFILE_INPUTS = [
    (140, [("stone", 45), ("wood", 35), ("gold", 20)]),
    (80, [("wood", 70), ("stone", 30)]),
    (80, [("wood", 70), ("stone", 30)]),
    (90, [("wood", 40), ("stone", 60)]),
    (100, [("wood", 40), ("stone", 60)]),
    (110, [("stone", 55), ("wood", 30), ("gold", 15)]),
    (120, [("stone", 40), ("wood", 25), ("mana_stone", 35)]),
    (90, [("wood", 45), ("stone", 25), ("gold", 30)]),
    (85, [("wood", 60), ("stone", 40)]),
    (115, [("stone", 45), ("wood", 25), ("ore", 30)]),
    (100, [("wood", 55), ("stone", 25), ("gold", 20)]),
    (110, [("stone", 45), ("wood", 25), ("ore", 30)]),
    (120, [("wood", 45), ("stone", 25), ("gold", 30)]),
    (95, [("stone", 55), ("wood", 30), ("gold", 15)]),
    (100, [("stone", 55), ("wood", 30), ("gold", 15)]),
]
PROVENANCE = [
    {
        "role": "building_authority_decision",
        "path": (
            "unity/Docs/GameDataCatalog/PhaseC/"
            "Phase_C4A_Building_Authority_Convergence.md"
        ),
        "sourceRevision": "c0d27a4c247615e33f1ed189b789e99bbf1355ac",
        "rawSha256": (
            "b94895911e46cfd03dfb08b15e3c4ccf860a028ffe62d922c95e564fd2e5e039"
        ),
    },
    {
        "role": "wallet_resource_registry",
        "path": (
            "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
            "GameDataWalletResourceReferences.cs"
        ),
        "sourceRevision": "f44e22cd3a0e334062f1ef8e487ffca1ecba6261",
        "rawSha256": (
            "07ef09c4bca55278a7db6dd09c9740352829bb677eb1ea4c817b8646ac02c699"
        ),
    },
    {
        "role": "realm_reference_registry",
        "path": (
            "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
            "GameDataRealmReferences.cs"
        ),
        "sourceRevision": "f44e22cd3a0e334062f1ef8e487ffca1ecba6261",
        "rawSha256": (
            "4bb8457c9831756a8cf6c2ddf3f14a5fd5c51866370c870cb074a53313bbdf4f"
        ),
    },
    {
        "role": "building_progression_registry",
        "path": (
            "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
            "GameDataBuildingProgressionRegistry.cs"
        ),
        "sourceRevision": "a2e6a9a0dddfb7522d880d4db9d17222adcbbffe",
        "rawSha256": (
            "319cb9f97cff850c3e0f79c30ae877c2876ecab6cf70d9fa681a672be4b430c4"
        ),
    },
    {
        "role": "building_progression_registry_tests",
        "path": (
            "unity/Assets/AL/Tests/EditMode/GameDataCatalog/"
            "GameDataBuildingProgressionRegistryTests.cs"
        ),
        "sourceRevision": "a2e6a9a0dddfb7522d880d4db9d17222adcbbffe",
        "rawSha256": (
            "8e911d59f0884c1d4ef7201f35579c6f2b257008d1c853345bea68d28d50ab29"
        ),
    },
    {
        "role": "six_family_schema",
        "path": (
            "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
            "GameDataSixFamilySchemas.cs"
        ),
        "sourceRevision": "a2e6a9a0dddfb7522d880d4db9d17222adcbbffe",
        "rawSha256": (
            "3c759d9ea2f1b2d6aca53d1e5f213bf0edb057eb0751bf3c9bfe9ae94b15d9bb"
        ),
    },
]


class ValidationError(RuntimeError):
    """Raised when the source does not satisfy the C4C contract."""


def fail(message: str) -> None:
    raise ValidationError(f"{ERROR_PREFIX}: {message}")


def sha256(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail(f"JSON contains duplicate property {key!r}")
        result[key] = value
    return result


def canonical_json(value: dict[str, Any]) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, separators=(",", ": "))
        + "\n"
    ).encode("utf-8")


def load_strict_json(
    path: Path,
    label: str,
    *,
    require_canonical: bool = True,
) -> tuple[dict[str, Any], bytes]:
    if not path.is_file():
        fail(f"{label} is missing: {path}")
    raw = path.read_bytes()
    if not raw:
        fail(f"{label} is empty")
    if raw.startswith(b"\xef\xbb\xbf"):
        fail(f"{label} must be UTF-8 without a BOM")
    if not raw.endswith(b"\n") or raw.endswith(b"\n\n"):
        fail(f"{label} must end with exactly one LF")
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as error:
        fail(f"{label} is not valid UTF-8: {error}")
    try:
        value = json.loads(
            text,
            object_pairs_hook=strict_object,
            parse_constant=lambda token: fail(
                f"{label} contains non-finite number {token!r}"
            ),
        )
    except json.JSONDecodeError as error:
        fail(f"{label} is not strict JSON: {error}")
    if not isinstance(value, dict):
        fail(f"{label} root must be an object")
    if require_canonical and canonical_json(value) != raw:
        fail(f"{label} bytes are not canonical deterministic JSON")
    return value, raw


def assert_keys(value: dict[str, Any], keys: list[str], json_path: str) -> None:
    if list(value) != keys:
        fail(f"{json_path} expected ordered properties {keys}, found {list(value)}")


def git_blob(repo_root: Path, revision: str, relative_path: str) -> bytes:
    result = subprocess.run(
        ["git", "cat-file", "blob", f"{revision}:{relative_path}"],
        cwd=repo_root,
        check=False,
        capture_output=True,
    )
    if result.returncode != 0:
        fail(
            f"Git could not read {revision}:{relative_path}: "
            f"{result.stderr.decode('utf-8', errors='replace').strip()}"
        )
    return result.stdout


def validate_pinned_source(repo_root: Path, source: dict[str, str]) -> None:
    source_path = repo_root / source["path"]
    if not source_path.is_file():
        fail(f"pinned source is missing: {source['path']}")
    current = source_path.read_bytes()
    committed = git_blob(repo_root, source["sourceRevision"], source["path"])
    if sha256(committed) != source["rawSha256"]:
        fail(f"pinned Git blob hash drifted for {source['path']}")
    if current != committed:
        fail(f"working source differs from pinned bytes: {source['path']}")


def family_by_name(source: dict[str, Any], family_name: str) -> dict[str, Any]:
    families = source.get("families")
    if not isinstance(families, list):
        fail("source families must be an array")
    matches = [
        family
        for family in families
        if isinstance(family, dict) and family.get("family") == family_name
    ]
    if len(matches) != 1:
        fail(f"source must contain one exact {family_name!r} family")
    return matches[0]


def load_ancestors(
    repo_root: Path,
) -> tuple[dict[str, Any], dict[str, Any]]:
    base, base_raw = load_strict_json(
        repo_root / BASE_PATH,
        "frozen v001 source",
        require_canonical=False,
    )
    if sha256(base_raw) != BASE_RAW_SHA256:
        fail("frozen v001 raw SHA-256 drifted")
    if base.get("schemaVersion") != 1:
        fail("frozen v001 schemaVersion must remain 1")
    if base.get("candidateId") != BASE_CANDIDATE_ID:
        fail("frozen v001 candidateId drifted")
    if base.get("productionFamilyOrder") != FAMILY_ORDER:
        fail("frozen v001 family order drifted")

    v002, v002_raw = load_strict_json(
        repo_root / V002_PATH,
        "frozen v002 source",
    )
    if sha256(v002_raw) != V002_RAW_SHA256:
        fail("frozen v002 raw SHA-256 drifted")
    if v002.get("schemaVersion") != 2:
        fail("frozen v002 schemaVersion must remain 2")
    if v002.get("candidateId") != V002_CANDIDATE_ID:
        fail("frozen v002 candidateId drifted")
    if v002.get("productionEligible") is not False:
        fail("frozen v002 must remain production-ineligible")
    if v002.get("productionFamilyOrder") != FAMILY_ORDER:
        fail("frozen v002 family order drifted")
    return base, v002


def expected_binding(
    order: int,
    canonical_id: str,
    legacy_id: str,
) -> dict[str, Any]:
    return {
        "order": order,
        "canonicalId": canonical_id,
        "legacyBuildingId": legacy_id,
        "nameRef": f"building.{canonical_id}.name",
        "initialLevel": 0,
        "maxLevel": 10,
        "costProfileId": f"building_upgrade_cost_{canonical_id}",
        "durationProfileId": "building_upgrade_duration_common",
        "prerequisiteProfileId": "building_prerequisite_none",
        "realmEligibilityProfileId": "building_realm_eligibility_all",
    }


def expected_cost_profile(
    order: int,
    canonical_id: str,
) -> dict[str, Any]:
    scale_percent, shares = COST_PROFILE_INPUTS[order]
    return {
        "order": order,
        "stableId": f"building_upgrade_cost_{canonical_id}",
        "scalePercent": scale_percent,
        "shares": [
            {"resourceStableId": resource_id, "percent": percent}
            for resource_id, percent in shares
        ],
    }


def validate_inherited_identity(
    binding: dict[str, Any],
    base_mapping: dict[str, Any],
    index: int,
) -> None:
    alias_rows = base_mapping.get("aliases")
    if not isinstance(alias_rows, list) or len(alias_rows) != 1:
        fail(f"v001 building mapping {index} must retain one exact alias")
    alias = alias_rows[0]
    if binding["canonicalId"] != base_mapping.get("canonicalId"):
        fail(f"building canonical identity drifted at order {index}")
    if binding["legacyBuildingId"] != base_mapping.get("technicalAnchor"):
        fail(f"building technical anchor drifted at order {index}")
    if binding["legacyBuildingId"] != alias.get("legacyId"):
        fail(f"building legacy alias drifted at order {index}")
    if alias.get("canonicalId") != binding["canonicalId"]:
        fail(f"building alias relation drifted at order {index}")
    if base_mapping.get("contentRefs") != [binding["nameRef"]]:
        fail(f"building content reference drifted at order {index}")
    if base_mapping.get("observed") != {"maxLevel": binding["maxLevel"]}:
        fail(f"building observed maximum drifted at order {index}")


def validate_cost_vectors(cost_profiles: list[dict[str, Any]]) -> int:
    vector_count = 0
    for profile in cost_profiles:
        shares = profile["shares"]
        if len(shares) not in {2, 3}:
            fail(f"{profile['stableId']} must contain two or three shares")
        if sum(share["percent"] for share in shares) != 100:
            fail(f"{profile['stableId']} shares must total exactly 100")
        resource_ids = [share["resourceStableId"] for share in shares]
        if len(resource_ids) != len(set(resource_ids)):
            fail(f"{profile['stableId']} resource IDs must be unique")
        if any(share["percent"] <= 0 for share in shares):
            fail(f"{profile['stableId']} shares must be positive")

        for base_budget in BASE_BUDGETS:
            budget = (
                base_budget * profile["scalePercent"] + 99
            ) // 100
            assigned = 0
            amounts: list[int] = []
            for share_index, share in enumerate(shares):
                if share_index == len(shares) - 1:
                    amount = budget - assigned
                else:
                    amount = max(1, budget * share["percent"] // 100)
                if amount <= 0:
                    fail(f"{profile['stableId']} produced non-positive cost")
                assigned += amount
                amounts.append(amount)
            if assigned != budget or sum(amounts) != budget:
                fail(f"{profile['stableId']} vector does not sum exactly")
            vector_count += 1
    if vector_count != 150:
        fail(f"building source must produce 150 vectors, found {vector_count}")
    return vector_count


def validate_realm_family(
    family: dict[str, Any],
    v002: dict[str, Any],
) -> None:
    assert_keys(
        family,
        [
            "family",
            "requiredForProduction",
            "artifactDisposition",
            "inherits",
            "blockingIds",
        ],
        "$.families[0]",
    )
    if family != {
        "family": "realms",
        "requiredForProduction": True,
        "artifactDisposition": "ready_for_non_production_shadow_generation",
        "inherits": {
            "candidateId": V002_CANDIDATE_ID,
            "family": "realms",
            "fields": [
                "mappings",
                "unavailableAnchors",
                "resolvedBlockingIds",
            ],
        },
        "blockingIds": [],
    }:
        fail("realm v002 inheritance drifted")
    v002_realm = family_by_name(v002, "realms")
    if v002_realm.get("blockingIds") != []:
        fail("v002 realm source must remain blocker-free")


def validate_building_family(
    family: dict[str, Any],
    base: dict[str, Any],
) -> int:
    assert_keys(
        family,
        [
            "family",
            "requiredForProduction",
            "artifactDisposition",
            "registryVersion",
            "inherits",
            "targetLevels",
            "baseBudgets",
            "costComputation",
            "progressionBindings",
            "costProfiles",
            "durationProfiles",
            "prerequisiteProfiles",
            "realmEligibilityProfiles",
            "resolvedBlockingIds",
            "blockingIds",
        ],
        "$.families[1]",
    )
    if family["family"] != "buildings":
        fail("$.families[1] must be buildings")
    if family["requiredForProduction"] is not True:
        fail("buildings must remain required for production")
    if family["artifactDisposition"] != "blocked_required":
        fail("buildings must remain blocked-required")
    if family["registryVersion"] != 1:
        fail("building registryVersion must remain 1")
    expected_inheritance = {
        "candidateId": BASE_CANDIDATE_ID,
        "family": "buildings",
        "fields": ["mappings", "unavailableAnchors"],
    }
    if family["inherits"] != expected_inheritance:
        fail("building v001 identity inheritance drifted")
    if family["targetLevels"] != TARGET_LEVELS:
        fail("building target levels must remain exact Levels 1-10")
    if family["baseBudgets"] != BASE_BUDGETS:
        fail("building base budgets drifted")
    if family["costComputation"] != {
        "scaleRounding": "ceil_base_budget_times_scale_percent_div_100",
        "orderedShareRounding": (
            "max_1_floor_budget_times_share_percent_div_100"
        ),
        "finalShareRounding": "positive_exact_remainder",
    }:
        fail("building cost-computation contract drifted")

    base_building = family_by_name(base, "buildings")
    if base_building.get("unavailableAnchors") != ["ManaShrine", "Mine"]:
        fail("v001 unavailable building anchors drifted")
    base_mappings = base_building.get("mappings")
    if not isinstance(base_mappings, list) or len(base_mappings) != 15:
        fail("v001 building source must retain 15 mappings")

    expected_bindings = [
        expected_binding(index, canonical_id, legacy_id)
        for index, (canonical_id, legacy_id) in enumerate(BUILDINGS)
    ]
    if family["progressionBindings"] != expected_bindings:
        fail("building progression bindings differ from the accepted C4B tuple")
    for index, binding in enumerate(family["progressionBindings"]):
        assert_keys(
            binding,
            [
                "order",
                "canonicalId",
                "legacyBuildingId",
                "nameRef",
                "initialLevel",
                "maxLevel",
                "costProfileId",
                "durationProfileId",
                "prerequisiteProfileId",
                "realmEligibilityProfileId",
            ],
            f"$.families[1].progressionBindings[{index}]",
        )
        validate_inherited_identity(binding, base_mappings[index], index)

    expected_cost_profiles = [
        expected_cost_profile(index, canonical_id)
        for index, (canonical_id, _) in enumerate(BUILDINGS)
    ]
    if family["costProfiles"] != expected_cost_profiles:
        fail("building cost profiles differ from the accepted C4B authority")
    for index, profile in enumerate(family["costProfiles"]):
        assert_keys(
            profile,
            ["order", "stableId", "scalePercent", "shares"],
            f"$.families[1].costProfiles[{index}]",
        )
        for share_index, share in enumerate(profile["shares"]):
            assert_keys(
                share,
                ["resourceStableId", "percent"],
                f"$.families[1].costProfiles[{index}].shares[{share_index}]",
            )
    vector_count = validate_cost_vectors(family["costProfiles"])

    if family["durationProfiles"] != [
        {
            "stableId": "building_upgrade_duration_common",
            "durationSeconds": DURATIONS,
        }
    ]:
        fail("building duration profile drifted")
    if family["prerequisiteProfiles"] != [
        {
            "stableId": "building_prerequisite_none",
            "requiredBuildingIds": [],
        }
    ]:
        fail("building prerequisite profile drifted")
    if family["realmEligibilityProfiles"] != [
        {
            "stableId": "building_realm_eligibility_all",
            "eligibleRealmIds": REALM_IDS,
        }
    ]:
        fail("building realm-eligibility profile drifted")
    if family["resolvedBlockingIds"] != RESOLVED_BUILDING_BLOCKERS:
        fail("resolved building blocker order drifted")
    if family["blockingIds"] != RETAINED_BUILDING_BLOCKERS:
        fail("production and asset blockers must remain exact")
    return vector_count


def validate_retained_families(
    candidate_families: list[dict[str, Any]],
    base: dict[str, Any],
) -> None:
    for index, family_name in enumerate(FAMILY_ORDER[2:], start=2):
        family = candidate_families[index]
        assert_keys(
            family,
            [
                "family",
                "requiredForProduction",
                "artifactDisposition",
                "inherits",
                "blockingIds",
            ],
            f"$.families[{index}]",
        )
        expected = {
            "family": family_name,
            "requiredForProduction": True,
            "artifactDisposition": "blocked_required",
            "inherits": {
                "candidateId": BASE_CANDIDATE_ID,
                "family": family_name,
                "fields": ["mappings", "unavailableAnchors"],
            },
            "blockingIds": RETAINED_FAMILY_BLOCKERS[family_name],
        }
        if family != expected:
            fail(f"{family_name} inherited source or blockers drifted")
        base_family = family_by_name(base, family_name)
        if base_family.get("blockingIds") != RETAINED_FAMILY_BLOCKERS[family_name]:
            fail(f"frozen v001 {family_name} blockers drifted")


def validate_no_production_outputs(repo_root: Path) -> None:
    roots = [
        "unity/Assets/StreamingAssets/GameData",
        "unity/Assets/Resources/GameData",
    ]
    forbidden = [
        "unity/Assets/StreamingAssets/GameData/catalog-set.json",
        "unity/Assets/Resources/GameData/catalog-set.json",
        "unity/Docs/GameDataCatalog/PhaseC/Generated/catalog-set.json",
    ]
    for root in roots:
        for family in FAMILY_ORDER:
            forbidden.append(f"{root}/Catalogs/{family}.v1.json")
    for relative_path in forbidden:
        if (repo_root / relative_path).is_file():
            fail(f"v003 must not create production artifact {relative_path!r}")


def validate_candidate(
    repo_root: Path,
    source_path: Path,
) -> tuple[dict[str, Any], bytes, int]:
    base, v002 = load_ancestors(repo_root)
    candidate, raw = load_strict_json(source_path, "building v003 source")
    assert_keys(
        candidate,
        [
            "schemaVersion",
            "candidateId",
            "sourceKind",
            "productionEligible",
            "supersedes",
            "provenance",
            "upstream",
            "approval",
            "evidencePolicy",
            "productionFamilyOrder",
            "families",
            "blockingIds",
            "generationGate",
        ],
        "$",
    )
    if candidate["schemaVersion"] != 3:
        fail("schemaVersion must be 3")
    if candidate["candidateId"] != V003_CANDIDATE_ID:
        fail("candidateId drifted")
    if candidate["sourceKind"] != "versioned_overlay":
        fail("sourceKind must remain versioned_overlay")
    if candidate["productionEligible"] is not False:
        fail("v003 must remain production-ineligible")
    expected_supersedes = {
        "candidateId": V002_CANDIDATE_ID,
        "path": V002_PATH,
        "sourceRevision": V002_REVISION,
        "rawSha256": V002_RAW_SHA256,
    }
    if candidate["supersedes"] != expected_supersedes:
        fail("$.supersedes does not pin the frozen v002 candidate")
    validate_pinned_source(repo_root, candidate["supersedes"])
    if candidate["provenance"] != PROVENANCE:
        fail("$.provenance differs from the accepted ordered C4 source chain")
    for source in PROVENANCE:
        validate_pinned_source(repo_root, source)
    for key in ["upstream", "approval", "evidencePolicy"]:
        if candidate[key] != v002[key]:
            fail(f"$.{key} must remain equivalent to v002")
    if candidate["approval"] != {
        "userFinalCreativeAcceptance": "pending",
        "userBalanceAcceptance": "pending",
        "runtimeAuthority": "unchanged",
    }:
        fail("approval state drifted")
    if candidate["productionFamilyOrder"] != FAMILY_ORDER:
        fail("production family order drifted")

    families = candidate["families"]
    if not isinstance(families, list) or len(families) != len(FAMILY_ORDER):
        fail("$.families must contain exactly six ordered rows")
    if [family.get("family") for family in families] != FAMILY_ORDER:
        fail("$.families order drifted")
    validate_realm_family(families[0], v002)
    vector_count = validate_building_family(families[1], base)
    validate_retained_families(families, base)

    expected_blockers = ["approval.user_creative_balance"]
    expected_blockers.extend(RETAINED_BUILDING_BLOCKERS)
    for family_name in FAMILY_ORDER[2:]:
        expected_blockers.extend(RETAINED_FAMILY_BLOCKERS[family_name])
    if candidate["blockingIds"] != expected_blockers:
        fail("$.blockingIds must contain the 26 exact retained blockers")
    if len(candidate["blockingIds"]) != 26:
        fail("v003 must retain exactly 26 explicit blockers")
    if any(
        blocker in candidate["blockingIds"]
        for blocker in RESOLVED_BUILDING_BLOCKERS
    ):
        fail("resolved building blockers must not remain in v003")
    if candidate["generationGate"] != {
        "status": "blocked",
        "requireProductionEligibleResult": "refused_without_writes",
        "outputPaths": [],
    }:
        fail("production generation gate drifted")
    validate_no_production_outputs(repo_root)
    return candidate, raw, vector_count


def run_negative_fixtures(
    repo_root: Path,
    candidate: dict[str, Any],
) -> int:
    mutations: list[tuple[str, Callable[[dict[str, Any]], None]]] = [
        (
            "production eligibility",
            lambda value: value.__setitem__("productionEligible", True),
        ),
        (
            "cross-building cost profile",
            lambda value: value["families"][1]["progressionBindings"][0].__setitem__(
                "costProfileId",
                "building_upgrade_cost_farm",
            ),
        ),
        (
            "cost scale drift",
            lambda value: value["families"][1]["costProfiles"][0].__setitem__(
                "scalePercent",
                141,
            ),
        ),
        (
            "retained blocker removal",
            lambda value: value["families"][1]["blockingIds"].pop(),
        ),
        (
            "resolved blocker reintroduction",
            lambda value: value["blockingIds"].insert(
                1,
                "buildings.max_level_review",
            ),
        ),
        (
            "realm inheritance drift",
            lambda value: value["families"][0]["inherits"].__setitem__(
                "candidateId",
                BASE_CANDIDATE_ID,
            ),
        ),
        (
            "unavailable building invention",
            lambda value: value["families"][1].__setitem__(
                "unavailableAnchors",
                ["ManaShrine", "Mine", "NewBuilding"],
            ),
        ),
    ]
    with tempfile.TemporaryDirectory(prefix="anotherlife-c4c-") as temp_dir:
        for index, (name, mutate) in enumerate(mutations):
            mutated = copy.deepcopy(candidate)
            mutate(mutated)
            fixture_path = Path(temp_dir) / f"negative-{index}.json"
            fixture_path.write_bytes(canonical_json(mutated))
            try:
                validate_candidate(repo_root, fixture_path)
            except ValidationError:
                continue
            fail(f"negative fixture unexpectedly passed: {name}")
    return len(mutations)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source",
        default=V003_PATH,
        help="repository-relative or absolute v003 candidate path",
    )
    parser.add_argument(
        "--require-production-eligible",
        action="store_true",
        help="validate first, then prove production generation is refused",
    )
    parser.add_argument(
        "--run-negative-fixtures",
        action="store_true",
        help="prove seven representative source mutations fail closed",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[2]
    source_path = Path(args.source)
    if not source_path.is_absolute():
        source_path = repo_root / source_path

    try:
        candidate, raw, vector_count = validate_candidate(
            repo_root,
            source_path,
        )
        negative_count = 0
        if args.run_negative_fixtures:
            negative_count = run_negative_fixtures(repo_root, candidate)
        if args.require_production_eligible:
            fail(
                "production generation refused without writes: candidate "
                f"{candidate['candidateId']!r} is blocked by "
                f"{len(candidate['blockingIds'])} explicit blockers and "
                "pending user approval"
            )
    except ValidationError as error:
        print(error, file=sys.stderr)
        return 1

    print(
        "PASS: building v003 carries 15 exact progression bindings and "
        f"{vector_count} deterministic cost vectors"
    )
    print(
        "PASS: three building blockers are resolved; 26 non-production "
        "blockers remain with zero output paths"
    )
    print(
        "PASS: six building provenance inputs and the frozen v001/v002 "
        "source chain are exact"
    )
    if args.run_negative_fixtures:
        print(f"PASS: {negative_count} negative fixtures failed closed")
    print(f"PASS: candidate raw SHA-256 {sha256(raw)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
