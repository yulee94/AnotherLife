#!/usr/bin/env python3
"""Mutation tests for the issue #259 terrestrial source validator."""

from __future__ import annotations

import copy
import importlib.util
import json
from pathlib import Path
from typing import Any, Callable


SCRIPT_PATH = Path(__file__).with_name(
    "validate_realm_boss_elite_source.py"
)
SPEC = importlib.util.spec_from_file_location("tdf_validator", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Cannot import validator from {SCRIPT_PATH}")
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


with VALIDATOR.DEFAULT_MANIFEST.open("r", encoding="utf-8") as handle:
    BASE_MANIFEST = json.load(handle)
with VALIDATOR.DEFAULT_SCHEMA.open("r", encoding="utf-8") as handle:
    BASE_SCHEMA = json.load(handle)


Mutation = Callable[[dict[str, Any], dict[str, Any]], None]


def remove_top(field: str) -> Mutation:
    return lambda manifest, schema: manifest.pop(field)


def set_path(path: tuple[Any, ...], value: Any) -> Mutation:
    def mutate(manifest: dict[str, Any], schema: dict[str, Any]) -> None:
        target: Any = manifest
        for part in path[:-1]:
            target = target[part]
        target[path[-1]] = value

    return mutate


def mutate_duplicate_profile(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["profiles"][1]["terrestrialProfileId"] = (
        manifest["profiles"][0]["terrestrialProfileId"]
    )


def mutate_duplicate_variant(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["profiles"][1]["variants"][0]["variantId"] = (
        manifest["profiles"][0]["variants"][0]["variantId"]
    )


def mutate_wrong_realm_distribution(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    tags = manifest["profiles"][0]["designIntentTags"]
    tags[tags.index("realm-source:stonehold")] = "realm-source:eldergrove"


def mutate_duplicate_silhouette(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["profiles"][1]["silhouetteClass"] = (
        manifest["profiles"][0]["silhouetteClass"]
    )


def mutate_bad_variant_profile(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["profiles"][0]["variants"][0]["profileId"] = (
        manifest["profiles"][1]["terrestrialProfileId"]
    )


def mutate_text_variant_claims_asset(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["profiles"][0]["variants"][0]["sourceAssetIds"] = [
        "tdf_asset_missing"
    ]


def mutate_profile_claims_asset(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["profiles"][0]["primarySourceAssetIds"] = [
        "tdf_asset_missing"
    ]


def mutate_missing_research_reference(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["profiles"][0]["acquisitionResearchIds"] = [
        "tdf_research_missing"
    ]


def mutate_budget_order(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["presentationBudgets"]["boss"]["low_mobile"][
        "maximumSkinnedTriangles"
    ] = 999999


def mutate_ratio_overlap(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["presentationBudgets"]["lod2TriangleRatio"] = [0.1, 0.7]


def mutate_unknown_top(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["runtimeCatalog"] = {}


def mutate_profile_count(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["profiles"].pop()


def mutate_role_count(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    manifest["profiles"][0]["profileRole"] = "inner_realm_field_elite"


def mutate_schema_required(
    manifest: dict[str, Any],
    schema: dict[str, Any],
) -> None:
    schema["required"].remove("profiles")


FIXTURES: list[tuple[str, Mutation, str]] = [
    ("missing packetId", remove_top("packetId"), "AL-TDF-MANIFEST-SCHEMA"),
    ("unsupported schema", set_path(("schemaVersion",), 1), "AL-TDF-MANIFEST-SCHEMA"),
    ("wrong packet ID", set_path(("packetId",), "wrong"), "AL-TDF-MANIFEST-SCHEMA"),
    ("invalid source version", set_path(("sourceVersion",), "rbe-v1"), "AL-TDF-MANIFEST-SCHEMA"),
    ("invalid created UTC", set_path(("createdAtUtc",), "2026-07-24"), "AL-TDF-MANIFEST-SCHEMA"),
    ("self supersession", set_path(("supersedesSourceVersion",), BASE_MANIFEST["sourceVersion"]), "AL-TDF-SOURCE-VERSION-MISMATCH"),
    ("runtime authority", set_path(("authority", "runtimeAuthority"), True), "AL-TDF-AUTHORITY-LEAK"),
    ("narrative authority", set_path(("authority", "narrativeAuthority"), True), "AL-TDF-AUTHORITY-LEAK"),
    ("gameplay authority", set_path(("authority", "gameplayAuthority"), True), "AL-TDF-AUTHORITY-LEAK"),
    ("purchase authority", set_path(("authority", "purchaseAuthority"), True), "AL-TDF-AUTHORITY-LEAK"),
    ("runtime advanced", set_path(("readiness", "runtimeIntegrationState"), "Planned"), "AL-TDF-AUTHORITY-LEAK"),
    ("narrative name advanced", set_path(("readiness", "narrativeNamingState"), "ApprovedLocalizationSource"), "AL-TDF-AUTHORITY-LEAK"),
    ("duplicate profile", mutate_duplicate_profile, "AL-TDF-DUPLICATE-ID"),
    ("duplicate variant", mutate_duplicate_variant, "AL-TDF-DUPLICATE-ID"),
    ("invalid profile ID", set_path(("profiles", 0, "terrestrialProfileId"), "boss bad"), "AL-TDF-MANIFEST-SCHEMA"),
    ("profile source mismatch", set_path(("profiles", 0, "sourceVersion"), "tdf-rbe-2026-07-24-v999"), "AL-TDF-SOURCE-VERSION-MISMATCH"),
    ("variant source mismatch", set_path(("profiles", 0, "variants", 0, "sourceVersion"), "tdf-rbe-2026-07-24-v999"), "AL-TDF-SOURCE-VERSION-MISMATCH"),
    ("wrong label authority", set_path(("profiles", 0, "workingLabelStatus"), "final"), "AL-TDF-AUTHORITY-LEAK"),
    ("wrong tag authority", set_path(("profiles", 0, "designIntentStatus"), "runtime"), "AL-TDF-AUTHORITY-LEAK"),
    ("zero scale", set_path(("profiles", 0, "approximateWorldScale", "measurements", "shoulderHeight"), 0), "AL-TDF-MANIFEST-SCHEMA"),
    ("missing anatomy", set_path(("profiles", 0, "anatomyIntent"), []), "AL-TDF-MANIFEST-SCHEMA"),
    ("single material", set_path(("profiles", 0, "materialSlotIntent"), ["hide"]), "AL-TDF-MANIFEST-SCHEMA"),
    ("missing unresolved decisions", set_path(("profiles", 0, "unresolvedDecisions"), []), "AL-TDF-MANIFEST-SCHEMA"),
    ("profile count", mutate_profile_count, "AL-TDF-MANIFEST-SCHEMA"),
    ("role count", mutate_role_count, "AL-TDF-MANIFEST-SCHEMA"),
    ("realm distribution", mutate_wrong_realm_distribution, "AL-TDF-MANIFEST-SCHEMA"),
    ("duplicate silhouette", mutate_duplicate_silhouette, "AL-TDF-DUPLICATE-ID"),
    ("bad variant profile", mutate_bad_variant_profile, "AL-TDF-MISSING-ASSET"),
    ("text variant claims asset", mutate_text_variant_claims_asset, "AL-TDF-VARIANT-SOURCE-MISSING"),
    ("profile claims asset", mutate_profile_claims_asset, "AL-TDF-MISSING-ASSET"),
    ("missing research", mutate_missing_research_reference, "AL-TDF-MISSING-ASSET"),
    ("nonmonotonic budget", mutate_budget_order, "AL-TDF-MANIFEST-SCHEMA"),
    ("mobile excluded", set_path(("presentationBudgets", "boss", "low_mobile", "packagedInPlayer"), False), "AL-TDF-AUTHORITY-LEAK"),
    ("cinematic packaged", set_path(("presentationBudgets", "boss", "cinematic_offline", "packagedInPlayer"), True), "AL-TDF-AUTHORITY-LEAK"),
    ("cinematic byte package", set_path(("presentationBudgets", "boss", "cinematic_offline", "maximumCompressedContentBytes"), 1), "AL-TDF-AUTHORITY-LEAK"),
    ("ratio overlap", mutate_ratio_overlap, "AL-TDF-MANIFEST-SCHEMA"),
    ("market identity", set_path(("qualityTarget", "marketplaceIdentityMayShipUnchanged"), True), "AL-TDF-AUTHORITY-LEAK"),
    ("offline source package", set_path(("optimizationIntent", "playerPackageIncludesOfflineSource"), True), "AL-TDF-AUTHORITY-LEAK"),
    ("purchase approved", set_path(("acquisitionResearch", 0, "purchaseApproved"), True), "AL-TDF-AUTHORITY-LEAK"),
    ("generation input leak", set_path(("acquisitionResearch", 0, "usedAsGenerationInput"), True), "AL-TDF-AUTHORITY-LEAK"),
    ("unknown top field", mutate_unknown_top, "AL-TDF-MANIFEST-SCHEMA"),
    ("schema contract drift", mutate_schema_required, "AL-TDF-MANIFEST-SCHEMA"),
]


base_diagnostics = VALIDATOR.validate_packet(
    copy.deepcopy(BASE_MANIFEST),
    copy.deepcopy(BASE_SCHEMA),
)
if base_diagnostics:
    raise AssertionError(
        "Base manifest must pass before mutation tests:\n"
        + json.dumps(base_diagnostics, indent=2)
    )

for name, mutation, expected_code in FIXTURES:
    manifest = copy.deepcopy(BASE_MANIFEST)
    schema = copy.deepcopy(BASE_SCHEMA)
    mutation(manifest, schema)
    first = VALIDATOR.validate_packet(manifest, schema)
    second = VALIDATOR.validate_packet(manifest, schema)
    if first != second:
        raise AssertionError(f"{name}: diagnostics are not deterministic")
    actual_codes = {item["code"] for item in first}
    if expected_code not in actual_codes:
        raise AssertionError(
            f"{name}: expected {expected_code}, found {sorted(actual_codes)}\n"
            + json.dumps(first, indent=2)
        )

print("AL_TDF_REALM_BOSS_ELITE_VALIDATOR_TESTS_PASS")
print(f"MUTATION_FIXTURES={len(FIXTURES)}")
