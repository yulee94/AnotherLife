#!/usr/bin/env python3
"""Validate the non-production Phase C building v004 asset-source overlay."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import struct
import subprocess
import sys
from pathlib import Path
from typing import Any, Callable


ERROR_PREFIX = "Phase C building v004 asset-source validation failed"
V001_ID = "game-data-phase-c-six-family-technical-source-2026-07-23-v001"
V002_ID = "game-data-phase-c-six-family-technical-source-2026-07-29-v002"
V003_ID = "game-data-phase-c-six-family-technical-source-2026-07-29-v003"
V004_ID = "game-data-phase-c-six-family-technical-source-2026-07-30-v004"
V001_PATH = (
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
V004_PATH = (
    "unity/Docs/GameDataCatalog/PhaseC/"
    "phase-c-six-family-technical-source-v004.json"
)
V001_SHA256 = (
    "5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b"
)
V002_SHA256 = (
    "60498d1a071ea79eb37c1b8889a1faaa5c7aee69679c1043256535ef4d3c1685"
)
V003_SHA256 = (
    "984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439"
)
V003_REVISION = "779e7363fca9ffed9e412f43cc74b20665fa4e9c"
SOURCE_REVISION = "b5c8472c71f0d9dd7b832e780e235ecf5e70e099"
ATLAS_PATH = (
    "unity/Assets/AL/Art/Buildings/RuntimeExports/"
    "S_Building_Icon_Atlas_1536x1024_v001.png"
)
UNITY_ATLAS_PATH = (
    "Assets/AL/Art/Buildings/RuntimeExports/"
    "S_Building_Icon_Atlas_1536x1024_v001.png"
)
ATLAS_GUID = "8cfa4b19fc1e4475873c4ea7560dc9ad"
ATLAS_SHA256 = (
    "874bba1c9fa9ba8435dcf61b29eca2786c049e0abf7d899680011a22e481b3a8"
)
ATLAS_META_SHA256 = (
    "663f14d76bdf5381cd0b8fb293db68212a01065e7eefec5cd16f78ab20be6d7c"
)
FAMILY_ORDER = [
    "realms",
    "buildings",
    "research",
    "troops",
    "champions",
    "skills",
]
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
CELL_WIDTHS = [307, 307, 308, 307, 307]
CELL_Y = [0, 341, 683]
CELL_HEIGHTS = [341, 342, 341]
BUILDING_RESOLVED = [
    "buildings.max_level_review",
    "buildings.cost_profiles",
    "buildings.duration_profiles",
    "buildings.asset_refs",
]
BUILDING_BLOCKERS = ["buildings.production_profiles"]
OTHER_BLOCKERS = {
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
PROVENANCE = [
    {
        "role": "building_asset_authority_decision",
        "path": (
            "unity/Docs/GameDataCatalog/PhaseC/"
            "Phase_C4D_Building_Asset_Reference_Authority.md"
        ),
        "sourceRevision": SOURCE_REVISION,
        "rawSha256": (
            "ea811e83ba98be7b87d7595aaf7db7ca8900d2668f4bfde2349f6778caa64579"
        ),
    },
    {
        "role": "building_asset_registry",
        "path": (
            "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
            "GameDataBuildingAssetReferences.cs"
        ),
        "sourceRevision": SOURCE_REVISION,
        "rawSha256": (
            "d229bb20fee3634ffd5004008e40d0fe83f77a301920755945185b5fb785a98b"
        ),
    },
    {
        "role": "six_family_schema",
        "path": (
            "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
            "GameDataSixFamilySchemas.cs"
        ),
        "sourceRevision": SOURCE_REVISION,
        "rawSha256": (
            "cf13e84a4d27e25fb037e4bf4a1fc7eb597d3bb836cefef00c1d062ec8b74d21"
        ),
    },
    {
        "role": "building_asset_registry_tests",
        "path": (
            "unity/Assets/AL/Tests/EditMode/GameDataCatalog/"
            "GameDataBuildingAssetReferenceTests.cs"
        ),
        "sourceRevision": SOURCE_REVISION,
        "rawSha256": (
            "215c22dfcc25dd64cdc694a3bde6fc4aa244aa239b8701db2e7f8b25af4f6e41"
        ),
    },
    {
        "role": "building_icon_atlas",
        "path": ATLAS_PATH,
        "sourceRevision": SOURCE_REVISION,
        "rawSha256": ATLAS_SHA256,
    },
    {
        "role": "building_icon_atlas_import",
        "path": f"{ATLAS_PATH}.meta",
        "sourceRevision": SOURCE_REVISION,
        "rawSha256": ATLAS_META_SHA256,
    },
]


class ValidationError(RuntimeError):
    """Raised when the source does not satisfy the v004 contract."""


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
    if not raw or raw.startswith(b"\xef\xbb\xbf"):
        fail(f"{label} must be nonempty UTF-8 without a BOM")
    if not raw.endswith(b"\n") or raw.endswith(b"\n\n"):
        fail(f"{label} must end with exactly one LF")
    try:
        value = json.loads(
            raw.decode("utf-8"),
            object_pairs_hook=strict_object,
            parse_constant=lambda token: fail(
                f"{label} contains non-finite number {token!r}"
            ),
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        fail(f"{label} is not strict UTF-8 JSON: {error}")
    if not isinstance(value, dict):
        fail(f"{label} root must be an object")
    if require_canonical and canonical_json(value) != raw:
        fail(f"{label} bytes are not canonical deterministic JSON")
    return value, raw


def assert_keys(value: dict[str, Any], keys: list[str], path: str) -> None:
    if list(value) != keys:
        fail(f"{path} expected ordered properties {keys}, found {list(value)}")


def git_blob(repo_root: Path, revision: str, path: str) -> bytes:
    result = subprocess.run(
        ["git", "cat-file", "blob", f"{revision}:{path}"],
        cwd=repo_root,
        check=False,
        capture_output=True,
    )
    if result.returncode != 0:
        message = result.stderr.decode("utf-8", errors="replace").strip()
        fail(f"Git could not read {revision}:{path}: {message}")
    return result.stdout


def validate_pinned_source(
    repo_root: Path,
    source: dict[str, str],
) -> None:
    current_path = repo_root / source["path"]
    if not current_path.is_file():
        fail(f"pinned source is missing: {source['path']}")
    committed = git_blob(
        repo_root,
        source["sourceRevision"],
        source["path"],
    )
    if sha256(committed) != source["rawSha256"]:
        fail(f"pinned Git blob hash drifted for {source['path']}")
    if current_path.read_bytes() != committed:
        fail(f"working source differs from pinned bytes: {source['path']}")


def family_by_name(source: dict[str, Any], family: str) -> dict[str, Any]:
    matches = [
        row for row in source["families"] if row.get("family") == family
    ]
    if len(matches) != 1:
        fail(f"expected one {family!r} family row")
    return matches[0]


def expected_binding(order: int) -> dict[str, Any]:
    stable_id, legacy_id = BUILDINGS[order]
    column = order % 5
    row = order // 5
    x = sum(CELL_WIDTHS[:column])
    return {
        "order": order,
        "canonicalId": stable_id,
        "legacyBuildingId": legacy_id,
        "assetRef": f"{UNITY_ATLAS_PATH}#{stable_id}",
        "column": column,
        "row": row,
        "pixelRect": [
            x,
            CELL_Y[row],
            CELL_WIDTHS[column],
            CELL_HEIGHTS[row],
        ],
    }


def validate_immutable_chain(
    repo_root: Path,
) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any]]:
    v001, v001_raw = load_strict_json(
        repo_root / V001_PATH,
        "v001 source",
        require_canonical=False,
    )
    v002, v002_raw = load_strict_json(
        repo_root / V002_PATH,
        "v002 source",
        require_canonical=False,
    )
    v003, v003_raw = load_strict_json(repo_root / V003_PATH, "v003 source")
    if sha256(v001_raw) != V001_SHA256 or v001.get("candidateId") != V001_ID:
        fail("frozen v001 identity or bytes changed")
    if sha256(v002_raw) != V002_SHA256 or v002.get("candidateId") != V002_ID:
        fail("frozen v002 identity or bytes changed")
    if sha256(v003_raw) != V003_SHA256 or v003.get("candidateId") != V003_ID:
        fail("frozen v003 identity or bytes changed")
    if v002.get("supersedes", {}).get("candidateId") != V001_ID:
        fail("v002 no longer supersedes frozen v001")
    if v003.get("supersedes", {}).get("candidateId") != V002_ID:
        fail("v003 no longer supersedes frozen v002")
    return v001, v002, v003


def validate_atlas(repo_root: Path, atlas: dict[str, Any]) -> None:
    expected = {
        "assetPath": UNITY_ATLAS_PATH,
        "guid": ATLAS_GUID,
        "rawSha256": ATLAS_SHA256,
        "metaRawSha256": ATLAS_META_SHA256,
        "width": 1536,
        "height": 1024,
        "columns": 5,
        "rows": 3,
        "pixelYAxis": "top_origin",
        "spriteImportMode": "single",
        "mipmaps": False,
        "srgb": True,
        "maxTextureSize": 2048,
    }
    if atlas != expected:
        fail("building atlas metadata drifted")

    png = (repo_root / ATLAS_PATH).read_bytes()
    if sha256(png) != ATLAS_SHA256:
        fail("building atlas raw bytes drifted")
    if png[:8] != b"\x89PNG\r\n\x1a\n" or png[12:16] != b"IHDR":
        fail("building atlas is not a PNG with an IHDR header")
    width, height = struct.unpack(">II", png[16:24])
    if (width, height) != (1536, 1024):
        fail("building atlas dimensions drifted")

    meta_path = repo_root / f"{ATLAS_PATH}.meta"
    meta = meta_path.read_bytes()
    if sha256(meta) != ATLAS_META_SHA256:
        fail("building atlas import bytes drifted")
    text = meta.decode("utf-8")
    required_lines = [
        f"guid: {ATLAS_GUID}",
        "    enableMipMap: 0",
        "    sRGBTexture: 1",
        "  maxTextureSize: 2048",
        "  spriteMode: 1",
        "  textureType: 8",
    ]
    for line in required_lines:
        if line not in text:
            fail(f"building atlas import setting is missing: {line.strip()}")


def validate_candidate(
    repo_root: Path,
    candidate: dict[str, Any],
    v003: dict[str, Any],
) -> None:
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
    if (
        candidate["schemaVersion"] != 4
        or candidate["candidateId"] != V004_ID
        or candidate["sourceKind"] != "versioned_overlay"
        or candidate["productionEligible"] is not False
    ):
        fail("root identity, source kind, or production eligibility drifted")

    expected_supersedes = {
        "candidateId": V003_ID,
        "path": V003_PATH,
        "sourceRevision": V003_REVISION,
        "rawSha256": V003_SHA256,
    }
    if candidate["supersedes"] != expected_supersedes:
        fail("v004 supersedes pin drifted")
    if candidate["provenance"] != PROVENANCE:
        fail("ordered provenance drifted")
    for source in candidate["provenance"]:
        validate_pinned_source(repo_root, source)
    if candidate["upstream"] != v003["upstream"]:
        fail("upstream source packet changed from v003")
    if candidate["approval"] != v003["approval"]:
        fail("approval state changed from v003")
    if candidate["evidencePolicy"] != v003["evidencePolicy"]:
        fail("evidence policy changed from v003")
    if candidate["productionFamilyOrder"] != FAMILY_ORDER:
        fail("production family order drifted")

    families = candidate["families"]
    if [row.get("family") for row in families] != FAMILY_ORDER:
        fail("family rows are not exact or ordered")
    v003_families = {
        row["family"]: row for row in v003["families"]
    }
    for family in FAMILY_ORDER:
        row = family_by_name(candidate, family)
        if row["requiredForProduction"] is not True:
            fail(f"{family} is no longer required for production")
        if family == "buildings":
            continue
        assert_keys(
            row,
            [
                "family",
                "requiredForProduction",
                "artifactDisposition",
                "inherits",
                "blockingIds",
            ],
            f"$.families[{family}]",
        )
        if row["artifactDisposition"] != v003_families[family][
            "artifactDisposition"
        ]:
            fail(f"{family} disposition changed")
        if row["inherits"] != {
            "candidateId": V003_ID,
            "family": family,
            "fields": ["*"],
        }:
            fail(f"{family} inheritance changed")
        expected_blockers = [] if family == "realms" else OTHER_BLOCKERS[family]
        if row["blockingIds"] != expected_blockers:
            fail(f"{family} blockers changed")

    building = family_by_name(candidate, "buildings")
    assert_keys(
        building,
        [
            "family",
            "requiredForProduction",
            "artifactDisposition",
            "registryVersion",
            "inherits",
            "atlas",
            "assetBindings",
            "resolvedBlockingIds",
            "blockingIds",
        ],
        "$.families[buildings]",
    )
    if (
        building["artifactDisposition"] != "blocked_required"
        or building["registryVersion"] != 1
    ):
        fail("building disposition or registry version changed")
    if building["inherits"] != {
        "candidateId": V003_ID,
        "family": "buildings",
        "fields": [
            "mappings",
            "unavailableAnchors",
            "targetLevels",
            "baseBudgets",
            "costComputation",
            "progressionBindings",
            "costProfiles",
            "durationProfiles",
            "prerequisiteProfiles",
            "realmEligibilityProfiles",
        ],
    }:
        fail("building v003 inheritance changed")
    validate_atlas(repo_root, building["atlas"])

    expected_bindings = [
        expected_binding(order) for order in range(len(BUILDINGS))
    ]
    if building["assetBindings"] != expected_bindings:
        fail("building asset bindings drifted")
    if len({row["assetRef"] for row in expected_bindings}) != len(BUILDINGS):
        fail("building asset references are not unique")
    for row in range(3):
        cells = expected_bindings[row * 5 : row * 5 + 5]
        if sum(cell["pixelRect"][2] for cell in cells) != 1536:
            fail(f"atlas row {row} does not span the image width")
        if cells[0]["pixelRect"][0] != 0:
            fail(f"atlas row {row} does not start at zero")
        for column in range(1, 5):
            previous = cells[column - 1]["pixelRect"]
            current = cells[column]["pixelRect"]
            if current[0] != previous[0] + previous[2]:
                fail(f"atlas row {row} has a gap or overlap")
    last_rect = expected_bindings[-1]["pixelRect"]
    if last_rect[1] + last_rect[3] != 1024:
        fail("atlas cells do not span the image height")

    progression = v003_families["buildings"]["progressionBindings"]
    progression_identity = [
        (row["canonicalId"], row["legacyBuildingId"]) for row in progression
    ]
    if progression_identity != BUILDINGS:
        fail("asset bindings no longer match v003 building progression order")
    if building["resolvedBlockingIds"] != BUILDING_RESOLVED:
        fail("resolved building blockers drifted")
    if building["blockingIds"] != BUILDING_BLOCKERS:
        fail("building production blocker changed")

    expected_global = ["approval.user_creative_balance", *BUILDING_BLOCKERS]
    for family in ["research", "troops", "champions", "skills"]:
        expected_global.extend(OTHER_BLOCKERS[family])
    if candidate["blockingIds"] != expected_global:
        fail("global blockers do not equal the exact effective family state")
    if len(candidate["blockingIds"]) != 25:
        fail("v004 must retain exactly 25 blockers")
    if "buildings.asset_refs" in candidate["blockingIds"]:
        fail("resolved building asset blocker remains globally active")

    expected_gate = {
        "status": "blocked",
        "requireProductionEligibleResult": "refused_without_writes",
        "outputPaths": [],
    }
    if candidate["generationGate"] != expected_gate:
        fail("generation gate changed or gained output paths")


def expect_failure(
    label: str,
    candidate: dict[str, Any],
    mutate: Callable[[dict[str, Any]], None],
    repo_root: Path,
    v003: dict[str, Any],
) -> None:
    fixture = copy.deepcopy(candidate)
    mutate(fixture)
    try:
        validate_candidate(repo_root, fixture, v003)
    except ValidationError:
        return
    fail(f"negative fixture unexpectedly passed: {label}")


def run_negative_fixtures(
    repo_root: Path,
    candidate: dict[str, Any],
    v003: dict[str, Any],
) -> None:
    fixtures: list[tuple[str, Callable[[dict[str, Any]], None]]] = [
        (
            "production eligibility",
            lambda value: value.__setitem__("productionEligible", True),
        ),
        (
            "swapped asset references",
            lambda value: value["families"][1]["assetBindings"][0].__setitem__(
                "assetRef",
                value["families"][1]["assetBindings"][1]["assetRef"],
            ),
        ),
        (
            "atlas coordinate drift",
            lambda value: value["families"][1]["assetBindings"][0][
                "pixelRect"
            ].__setitem__(2, 306),
        ),
        (
            "atlas hash drift",
            lambda value: value["families"][1]["atlas"].__setitem__(
                "rawSha256",
                "0" * 64,
            ),
        ),
        (
            "asset blocker reintroduced",
            lambda value: value["families"][1]["blockingIds"].append(
                "buildings.asset_refs"
            ),
        ),
        (
            "production blocker removed",
            lambda value: value["families"][1].__setitem__(
                "blockingIds",
                [],
            ),
        ),
        (
            "approval changed",
            lambda value: value["approval"].__setitem__(
                "userFinalCreativeAcceptance",
                "approved",
            ),
        ),
        (
            "production output introduced",
            lambda value: value["generationGate"]["outputPaths"].append(
                "unity/Assets/AL/GameData/Generated/buildings.json"
            ),
        ),
    ]
    for label, mutate in fixtures:
        expect_failure(label, candidate, mutate, repo_root, v003)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--run-negative-fixtures",
        action="store_true",
        help="prove representative drift and activation mutations fail closed",
    )
    parser.add_argument(
        "--require-production-eligible",
        action="store_true",
        help="validate, then deterministically refuse production generation",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parents[2]
    try:
        _, _, v003 = validate_immutable_chain(repo_root)
        candidate, raw = load_strict_json(repo_root / V004_PATH, "v004 source")
        validate_candidate(repo_root, candidate, v003)
        if args.run_negative_fixtures:
            run_negative_fixtures(repo_root, candidate, v003)
        if args.require_production_eligible:
            fail(
                "production generation refused without writes: "
                "productionEligible is false and 25 blockers remain"
            )
    except ValidationError as error:
        print(str(error), file=sys.stderr)
        return 2

    suffix = " negative-fixtures=passed" if args.run_negative_fixtures else ""
    print(
        f"validated {V004_ID} rawSha256={sha256(raw)} "
        f"blockers=25{suffix}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
