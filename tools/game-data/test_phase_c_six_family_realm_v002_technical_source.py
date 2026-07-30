#!/usr/bin/env python3
"""Validate the non-production Phase C realm v002 technical-source overlay."""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path
from typing import Any


ERROR_PREFIX = "Phase C realm v002 technical-source validation failed"
BASE_CANDIDATE_ID = (
    "game-data-phase-c-six-family-technical-source-2026-07-23-v001"
)
V002_CANDIDATE_ID = (
    "game-data-phase-c-six-family-technical-source-2026-07-29-v002"
)
BASE_PATH = (
    "unity/Docs/GameDataCatalog/PhaseC/"
    "phase-c-six-family-technical-source.json"
)
V002_PATH = (
    "unity/Docs/GameDataCatalog/PhaseC/"
    "phase-c-six-family-technical-source-v002.json"
)
BASE_REVISION = "5858967b17a8c802ba4aca6225e1b61e45cdf5d9"
BASE_RAW_SHA256 = (
    "5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b"
)
FAMILY_ORDER = [
    "realms",
    "buildings",
    "research",
    "troops",
    "champions",
    "skills",
]
RESOLVED_REALM_BLOCKERS = [
    "realms.rare_resource_catalog",
    "realms.capability_profiles",
    "realms.asset_refs",
]
RETAINED_FAMILY_BLOCKERS = {
    "buildings": [
        "buildings.max_level_review",
        "buildings.production_profiles",
        "buildings.cost_profiles",
        "buildings.duration_profiles",
        "buildings.asset_refs",
    ],
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
        "role": "realm_authority_decision",
        "path": (
            "unity/Docs/GameDataCatalog/PhaseC/"
            "Phase_C3A_Realm_Authority_Convergence.md"
        ),
        "sourceRevision": "27e2d477ad98f131593b991c85ce8388d9216641",
        "rawSha256": (
            "4d220976127f1c80c407a42ce987aa50a38dbee268ecc7a80258f3178c77925f"
        ),
    },
    {
        "role": "resource_authority_decision",
        "path": (
            "unity/Docs/GameDataCatalog/PhaseC/"
            "Phase_C3B_Resource_Reference_Authority.md"
        ),
        "sourceRevision": "4122483dda7cbba5d87577e70b4280786849be1d",
        "rawSha256": (
            "c068ec6d24e10d94fe7466355987e9c1161fa4a390443fa77a93c136216121f0"
        ),
    },
    {
        "role": "realm_blocker_decision",
        "path": (
            "unity/Docs/GameDataCatalog/PhaseC/"
            "Phase_C3E_Realm_Blocker_Ledger_Convergence.md"
        ),
        "sourceRevision": "4edbac7a4f53f8b7287da3c9ecf1299286e8d6fc",
        "rawSha256": (
            "0eb1f95b22e00b7ffd66f9cfb729a0456beb85bb161a0cd64aa7cfca40257955"
        ),
    },
    {
        "role": "capability_authority_decision",
        "path": (
            "unity/Docs/GameDataCatalog/PhaseC/"
            "Phase_C3F_Realm_Capability_Profile_Authority.md"
        ),
        "sourceRevision": "36b2395477acd40c1ffc9f75498d2ac55f9e0fd9",
        "rawSha256": (
            "9a3094e191746b51395b48b4ee3254911f8e79edd02087db91818c1b7d4290b8"
        ),
    },
    {
        "role": "resource_reference_registry",
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
        "role": "capability_profile_registry",
        "path": (
            "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
            "GameDataRealmCapabilityProfiles.cs"
        ),
        "sourceRevision": "59b99aa94e45d5564e373b8824e2aa3b30b8a754",
        "rawSha256": (
            "8413f45a32cad1bf71107c0c6cea18e4c8e86b7f8191a19ff0bcc0875e89b427"
        ),
    },
    {
        "role": "six_family_schema",
        "path": (
            "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
            "GameDataSixFamilySchemas.cs"
        ),
        "sourceRevision": "59b99aa94e45d5564e373b8824e2aa3b30b8a754",
        "rawSha256": (
            "ff2c91c3b27a370f5f89812927f4211461a5b4d523dbf1167f277c2be5affdb6"
        ),
    },
]
REALM_MAPPINGS = [
    {
        "canonicalId": "crownlands",
        "technicalAnchor": "RealmId.Crownlands",
        "legacyEnumValue": 3,
        "contentRefs": [
            "realm.crownlands.name",
            "realm.crownlands.description",
        ],
        "aliases": [],
        "rareResourceId": "royal_sigil",
        "capabilityProfileIds": ["battle_realm_crownlands"],
        "innerRealmId": "inner_crownlands",
        "mainGateId": "gate_crownlands_meridian",
        "outerWarzoneId": "warzone_crownlands",
        "assetRef": (
            "Assets/AL/Art/Heraldry/RuntimeExports/"
            "S_ArcaneAxis_Crownlands_Flat_256_v001.png"
        ),
        "assetGuid": "ba4dfcc7b514049f79f6ec3424193b46",
        "assetRawSha256": (
            "f5c7e351ec930aac69f6df02d03034bc38c465ed8dfa787dd4feba044f33f82b"
        ),
        "observed": {"rareResourceAnchor": "ResourceType.RoyalSigil"},
    },
    {
        "canonicalId": "stonehold",
        "technicalAnchor": "RealmId.Stonehold",
        "legacyEnumValue": 1,
        "contentRefs": [
            "realm.stonehold.name",
            "realm.stonehold.description",
        ],
        "aliases": [],
        "rareResourceId": "deep_ore",
        "capabilityProfileIds": ["battle_realm_stonehold"],
        "innerRealmId": "inner_stonehold",
        "mainGateId": "gate_stonehold_faultline",
        "outerWarzoneId": "warzone_stonehold",
        "assetRef": (
            "Assets/AL/Art/Heraldry/RuntimeExports/"
            "S_ArcaneAxis_Stonehold_Flat_256_v001.png"
        ),
        "assetGuid": "94d8d9e2cf04a4b769c213a13c164b8e",
        "assetRawSha256": (
            "53d220dc8b938d212963286133ca39e1968fa1421126559dd56bdfde9c437946"
        ),
        "observed": {"rareResourceAnchor": "ResourceType.DeepOre"},
    },
    {
        "canonicalId": "eldergrove",
        "technicalAnchor": "RealmId.Eldergrove",
        "legacyEnumValue": 2,
        "contentRefs": [
            "realm.eldergrove.name",
            "realm.eldergrove.description",
        ],
        "aliases": [],
        "rareResourceId": "world_sap",
        "capabilityProfileIds": ["battle_realm_eldergrove"],
        "innerRealmId": "inner_eldergrove",
        "mainGateId": "gate_eldergrove_greenveil",
        "outerWarzoneId": "warzone_eldergrove",
        "assetRef": (
            "Assets/AL/Art/Heraldry/RuntimeExports/"
            "S_ArcaneAxis_Eldergrove_Flat_256_v001.png"
        ),
        "assetGuid": "53001b27fd9d14914984211765be4391",
        "assetRawSha256": (
            "1d45fc8fba82ebb3fdc1c4f819026ea8e45b11c248378371c7b2b6923c6e0cac"
        ),
        "observed": {"rareResourceAnchor": "ResourceType.WorldSap"},
    },
    {
        "canonicalId": "umbral",
        "technicalAnchor": "RealmId.Umbral",
        "legacyEnumValue": 4,
        "contentRefs": [
            "realm.umbral.name",
            "realm.umbral.description",
        ],
        "aliases": [],
        "rareResourceId": "dark_crystal",
        "capabilityProfileIds": ["battle_realm_umbral"],
        "innerRealmId": "inner_umbral",
        "mainGateId": "gate_umbral_ashvein",
        "outerWarzoneId": "warzone_umbral",
        "assetRef": (
            "Assets/AL/Art/Heraldry/RuntimeExports/"
            "S_ArcaneAxis_Umbral_Flat_256_v001.png"
        ),
        "assetGuid": "a426041e03b0742999a34b8b5e198406",
        "assetRawSha256": (
            "a9daefa3ea6445ba2db680dad92a456db75becebec8848c678b29d5ea2c85aaa"
        ),
        "observed": {"rareResourceAnchor": "ResourceType.DarkCrystal"},
    },
]


class ValidationError(RuntimeError):
    """Raised when the source does not satisfy the C3H contract."""


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
    canonical = (
        json.dumps(value, ensure_ascii=False, indent=2, separators=(",", ": "))
        + "\n"
    ).encode("utf-8")
    if require_canonical and canonical != raw:
        fail(f"{label} bytes are not in canonical deterministic JSON format")
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
    committed = git_blob(
        repo_root,
        source["sourceRevision"],
        source["path"],
    )
    if sha256(committed) != source["rawSha256"]:
        fail(f"pinned Git blob hash drifted for {source['path']}")
    if current != committed:
        fail(f"working source differs from pinned bytes: {source['path']}")


def validate_base(repo_root: Path) -> tuple[dict[str, Any], bytes]:
    base, raw = load_strict_json(
        repo_root / BASE_PATH,
        "frozen v001 source",
        require_canonical=False,
    )
    if sha256(raw) != BASE_RAW_SHA256:
        fail("frozen v001 raw SHA-256 drifted")
    if base.get("schemaVersion") != 1:
        fail("frozen v001 schemaVersion must remain 1")
    if base.get("candidateId") != BASE_CANDIDATE_ID:
        fail("frozen v001 candidateId drifted")
    if base.get("productionEligible") is not False:
        fail("frozen v001 must remain production-ineligible")
    if base.get("productionFamilyOrder") != FAMILY_ORDER:
        fail("frozen v001 family order drifted")
    base_families = base.get("families")
    if not isinstance(base_families, list):
        fail("frozen v001 families must be an array")
    if [family.get("family") for family in base_families] != FAMILY_ORDER:
        fail("frozen v001 family rows drifted")
    return base, raw


def validate_provenance(repo_root: Path, candidate: dict[str, Any]) -> None:
    if candidate["provenance"] != PROVENANCE:
        fail("$.provenance differs from the accepted ordered source chain")
    for source in PROVENANCE:
        validate_pinned_source(repo_root, source)


def validate_realm_assets(repo_root: Path) -> None:
    for mapping in REALM_MAPPINGS:
        asset_path = repo_root / "unity/Assets/AL" / mapping["assetRef"][10:]
        if not asset_path.is_file():
            fail(f"realm asset is missing: {mapping['assetRef']}")
        if sha256(asset_path.read_bytes()) != mapping["assetRawSha256"]:
            fail(f"realm asset raw SHA-256 drifted: {mapping['assetRef']}")
        meta_path = Path(f"{asset_path}.meta")
        if not meta_path.is_file():
            fail(f"realm asset meta is missing: {meta_path}")
        expected_guid_line = f"guid: {mapping['assetGuid']}"
        if expected_guid_line not in meta_path.read_text(encoding="utf-8").splitlines():
            fail(f"realm asset GUID drifted: {mapping['assetRef']}")


def validate_realm_family(family: dict[str, Any]) -> None:
    assert_keys(
        family,
        [
            "family",
            "requiredForProduction",
            "artifactDisposition",
            "mappings",
            "unavailableAnchors",
            "resolvedBlockingIds",
            "blockingIds",
        ],
        "$.families[0]",
    )
    if family["family"] != "realms":
        fail("$.families[0] must be realms")
    if family["requiredForProduction"] is not True:
        fail("realms must remain required for production")
    if (
        family["artifactDisposition"]
        != "ready_for_non_production_shadow_generation"
    ):
        fail("realm artifact disposition drifted")
    if family["mappings"] != REALM_MAPPINGS:
        fail("realm mappings differ from the exact accepted four-record tuple")
    for index, mapping in enumerate(family["mappings"]):
        assert_keys(
            mapping,
            [
                "canonicalId",
                "technicalAnchor",
                "legacyEnumValue",
                "contentRefs",
                "aliases",
                "rareResourceId",
                "capabilityProfileIds",
                "innerRealmId",
                "mainGateId",
                "outerWarzoneId",
                "assetRef",
                "assetGuid",
                "assetRawSha256",
                "observed",
            ],
            f"$.families[0].mappings[{index}]",
        )
    if family["unavailableAnchors"] != []:
        fail("realm unavailableAnchors must remain empty")
    if family["resolvedBlockingIds"] != RESOLVED_REALM_BLOCKERS:
        fail("realm resolved blocker order drifted")
    if family["blockingIds"] != []:
        fail("v002 realms must have zero effective source blockers")


def validate_retained_families(
    candidate_families: list[dict[str, Any]],
    base: dict[str, Any],
) -> None:
    base_by_family = {family["family"]: family for family in base["families"]}
    for index, family_name in enumerate(FAMILY_ORDER[1:], start=1):
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
        if family["family"] != family_name:
            fail(f"family order drifted at index {index}")
        if family["requiredForProduction"] is not True:
            fail(f"{family_name} must remain required for production")
        if family["artifactDisposition"] != "blocked_required":
            fail(f"{family_name} must remain blocked-required")
        inherited_source = family["inherits"]
        assert_keys(
            inherited_source,
            ["candidateId", "family", "fields"],
            f"$.families[{index}].inherits",
        )
        if inherited_source != {
            "candidateId": BASE_CANDIDATE_ID,
            "family": family_name,
            "fields": ["mappings", "unavailableAnchors"],
        }:
            fail(f"{family_name} inherited source drifted")
        expected_blockers = RETAINED_FAMILY_BLOCKERS[family_name]
        if family["blockingIds"] != expected_blockers:
            fail(f"{family_name} blockers differ from the frozen v001 family")
        base_family = base_by_family[family_name]
        if base_family["artifactDisposition"] != "blocked_required":
            fail(f"frozen v001 {family_name} disposition drifted")
        if base_family["blockingIds"] != expected_blockers:
            fail(f"frozen v001 {family_name} blocker source drifted")


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
            fail(f"v002 must not create production artifact {relative_path!r}")


def validate_candidate(
    repo_root: Path,
    source_path: Path,
) -> tuple[dict[str, Any], bytes]:
    base, _ = validate_base(repo_root)
    candidate, raw = load_strict_json(source_path, "realm v002 source")
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
    if candidate["schemaVersion"] != 2:
        fail("schemaVersion must be 2")
    if candidate["candidateId"] != V002_CANDIDATE_ID:
        fail("candidateId drifted")
    if candidate["sourceKind"] != "versioned_overlay":
        fail("sourceKind must be versioned_overlay")
    if candidate["productionEligible"] is not False:
        fail("v002 must remain production-ineligible")
    expected_supersedes = {
        "candidateId": BASE_CANDIDATE_ID,
        "path": BASE_PATH,
        "sourceRevision": BASE_REVISION,
        "rawSha256": BASE_RAW_SHA256,
    }
    if candidate["supersedes"] != expected_supersedes:
        fail("$.supersedes does not pin the frozen v001 candidate")
    validate_pinned_source(repo_root, candidate["supersedes"])
    validate_provenance(repo_root, candidate)
    for key in ["upstream", "approval", "evidencePolicy"]:
        if candidate[key] != base[key]:
            fail(f"$.{key} must remain byte-equivalent in meaning to v001")
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
    validate_realm_family(families[0])
    validate_retained_families(families, base)
    expected_blockers = ["approval.user_creative_balance"]
    for family_name in FAMILY_ORDER[1:]:
        expected_blockers.extend(RETAINED_FAMILY_BLOCKERS[family_name])
    if candidate["blockingIds"] != expected_blockers:
        fail("$.blockingIds must contain the 29 exact retained blockers")
    if len(candidate["blockingIds"]) != 29:
        fail("v002 must retain exactly 29 explicit blockers")
    if any(
        blocker in candidate["blockingIds"]
        for blocker in RESOLVED_REALM_BLOCKERS
    ):
        fail("resolved realm blockers must not remain in v002 blockingIds")
    expected_gate = {
        "status": "blocked",
        "requireProductionEligibleResult": "refused_without_writes",
        "outputPaths": [],
    }
    if candidate["generationGate"] != expected_gate:
        fail("production generation gate drifted")
    validate_realm_assets(repo_root)
    validate_no_production_outputs(repo_root)
    return candidate, raw


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source",
        default=V002_PATH,
        help="repository-relative or absolute v002 candidate path",
    )
    parser.add_argument(
        "--require-production-eligible",
        action="store_true",
        help="validate first, then prove production generation is refused",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[2]
    source_path = Path(args.source)
    if not source_path.is_absolute():
        source_path = repo_root / source_path

    try:
        candidate, raw = validate_candidate(repo_root, source_path)
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
        "PASS: realm v002 carries four exact authored-order records, "
        "zero realm source blockers, and eight pinned provenance inputs"
    )
    print(
        "PASS: 29 non-realm/global blockers remain; production generation "
        "is blocked with zero output paths and unchanged runtime authority"
    )
    print(f"PASS: candidate raw SHA-256 {sha256(raw)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
