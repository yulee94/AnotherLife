#!/usr/bin/env python3
"""Validate the non-runtime terrestrial source packet for AnotherLife issue #259."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import struct
import sys
from pathlib import Path, PurePosixPath
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_PACKET_DIR = (
    REPO_ROOT
    / "unity"
    / "Docs"
    / "Terrestrials"
    / "RealmBossesAndElites"
)
DEFAULT_MANIFEST = DEFAULT_PACKET_DIR / "realm_boss_elite_profiles_manifest.json"
DEFAULT_SCHEMA = DEFAULT_PACKET_DIR / "realm_boss_elite_source_packet.schema.json"

SUPPORTED_SCHEMA_VERSION = 2
PACKET_ID = "anotherlife-terrestrial-realm-boss-elite-source"
SOURCE_VERSION_PATTERN = re.compile(r"^tdf-rbe-\d{4}-\d{2}-\d{2}-v\d{3}$")
SOURCE_ID_PATTERN = re.compile(r"^tdf_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
SHA256_PATTERN = re.compile(r"^[a-f0-9]{64}$")
UTC_PATTERN = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")

TOP_LEVEL_FIELDS = {
    "schemaVersion",
    "packetId",
    "sourceVersion",
    "createdAtUtc",
    "authority",
    "readiness",
    "qualityTarget",
    "presentationBudgets",
    "optimizationIntent",
    "profiles",
    "sourceAssets",
    "generationRecords",
    "externalInputs",
    "acquisitionResearch",
    "supersedesSourceVersion",
}

PROFILE_FIELDS = {
    "terrestrialProfileId",
    "sourceVersion",
    "workingReviewLabel",
    "workingLabelStatus",
    "designIntentTags",
    "designIntentStatus",
    "profileRole",
    "approximateWorldScale",
    "silhouetteClass",
    "anatomyIntent",
    "materialSlotIntent",
    "motionIntent",
    "effectIntent",
    "rigOrSkeletonIntent",
    "requiredAnimationIntent",
    "lodIntent",
    "colliderIntent",
    "vfxAnchorIntent",
    "accessibilityNotes",
    "reducedMotionIntent",
    "explicitExclusions",
    "unresolvedDecisions",
    "primarySourceAssetIds",
    "conceptSourceState",
    "variants",
    "acquisitionResearchIds",
}

NONBLANK_PROFILE_TEXT_FIELDS = (
    "workingReviewLabel",
    "silhouetteClass",
    "rigOrSkeletonIntent",
    "lodIntent",
    "colliderIntent",
    "accessibilityNotes",
    "reducedMotionIntent",
)

NONEMPTY_PROFILE_LIST_FIELDS = (
    "designIntentTags",
    "anatomyIntent",
    "materialSlotIntent",
    "motionIntent",
    "effectIntent",
    "requiredAnimationIntent",
    "vfxAnchorIntent",
    "explicitExclusions",
    "unresolvedDecisions",
    "variants",
)

AUTHORITY_EXPECTED = {
    "sourceOwnerMode": "Codex terrestrial-design",
    "technicalReviewOwnerMode": "Codex coordination/review",
    "narrativeOwnerMode": "Codex narrative/content",
    "engineeringOwnerMode": "Codex engineering",
    "finalCreativeApprover": "user",
    "issue": 259,
    "runtimeAuthority": False,
    "narrativeAuthority": False,
    "gameplayAuthority": False,
    "purchaseAuthority": False,
    "requiresUserCreativeApprovalBeforeIntegration": True,
}

TECHNICAL_STATES = {
    "Draft",
    "SourceIncomplete",
    "ValidationFailed",
    "TechnicalReviewReady",
    "TechnicalHandoffComplete",
    "Superseded",
}
USER_STATES = {
    "NotRequested",
    "ReadyForReview",
    "ChangesRequested",
    "ApprovedSourceVersion",
    "Rejected",
    "Superseded",
}
VARIANT_STATES = {
    "DeliveredReference",
    "ProposedTextOnly",
    "ReadyForUserReview",
    "ChangesRequested",
    "UserApproved",
    "Rejected",
    "Superseded",
}
CONCEPT_STATES = {
    "NotGenerated",
    "GeneratedUnreviewed",
    "ReadyForUserReview",
    "ChangesRequested",
    "UserApproved",
    "Rejected",
    "Superseded",
}
SOURCE_ASSET_STATES = {
    "GeneratedUnreviewed",
    "ReadyForUserReview",
    "ChangesRequested",
    "UserApproved",
    "Rejected",
    "Superseded",
}

EXPECTED_REALMS = ("stonehold", "eldergrove", "crownlands", "umbral")
EXPECTED_ROLE_COUNTS = {
    "outer_warzone_boss": 4,
    "inner_realm_field_elite": 12,
}
_FILE_EVIDENCE_CACHE: dict[
    tuple[str, int, int],
    tuple[bool, int, str, str, int, int],
] = {}


def diagnostic(
    code: str,
    field_path: str,
    message: str,
    *,
    profile_id: str | None = None,
    variant_id: str | None = None,
    asset_id: str | None = None,
) -> dict[str, Any]:
    return {
        "code": code,
        "severity": "error",
        "sourceVersion": None,
        "profileId": profile_id,
        "variantId": variant_id,
        "assetId": asset_id,
        "fieldPath": field_path,
        "message": message,
        "blocksTechnicalHandoff": True,
    }


def is_nonblank(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def is_nonempty_unique_string_list(value: Any, minimum: int = 1) -> bool:
    return (
        isinstance(value, list)
        and len(value) >= minimum
        and all(is_nonblank(item) for item in value)
        and len(value) == len(set(value))
    )


def add_if(
    diagnostics: list[dict[str, Any]],
    condition: bool,
    code: str,
    field_path: str,
    message: str,
    **identity: Any,
) -> None:
    if condition:
        diagnostics.append(
            diagnostic(code, field_path, message, **identity)
        )


def read_json(path: Path, label: str) -> tuple[Any | None, list[dict[str, Any]]]:
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(handle), []
    except FileNotFoundError:
        return None, [
            diagnostic(
                "AL-TDF-MISSING-ASSET",
                label,
                f"{label} does not exist: {path}",
            )
        ]
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        return None, [
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                label,
                f"{label} is not valid UTF-8 JSON: {exc}",
            )
        ]


def validate_schema_document(schema: Any) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    add_if(
        diagnostics,
        not isinstance(schema, dict),
        "AL-TDF-MANIFEST-SCHEMA",
        "schema",
        "Schema root must be an object.",
    )
    if not isinstance(schema, dict):
        return diagnostics

    add_if(
        diagnostics,
        schema.get("$schema") != "https://json-schema.org/draft/2020-12/schema",
        "AL-TDF-MANIFEST-SCHEMA",
        "schema.$schema",
        "Schema must declare JSON Schema draft 2020-12.",
    )
    add_if(
        diagnostics,
        schema.get("type") != "object",
        "AL-TDF-MANIFEST-SCHEMA",
        "schema.type",
        "Schema root type must be object.",
    )
    required = schema.get("required")
    add_if(
        diagnostics,
        not isinstance(required, list) or set(required) != TOP_LEVEL_FIELDS,
        "AL-TDF-MANIFEST-SCHEMA",
        "schema.required",
        "Schema required fields must exactly match the retained packet contract.",
    )
    add_if(
        diagnostics,
        not isinstance(schema.get("$defs"), dict),
        "AL-TDF-MANIFEST-SCHEMA",
        "schema.$defs",
        "Schema must retain reusable definitions.",
    )
    return diagnostics


def validate_top_level(manifest: Any) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    add_if(
        diagnostics,
        not isinstance(manifest, dict),
        "AL-TDF-MANIFEST-SCHEMA",
        "$",
        "Manifest root must be an object.",
    )
    if not isinstance(manifest, dict):
        return diagnostics

    actual_fields = set(manifest)
    missing = sorted(TOP_LEVEL_FIELDS - actual_fields)
    unknown = sorted(actual_fields - TOP_LEVEL_FIELDS)
    for field in missing:
        diagnostics.append(
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                field,
                f"Required top-level field is missing: {field}",
            )
        )
    for field in unknown:
        diagnostics.append(
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                field,
                f"Unknown top-level field is not allowed: {field}",
            )
        )

    add_if(
        diagnostics,
        manifest.get("schemaVersion") != SUPPORTED_SCHEMA_VERSION,
        "AL-TDF-MANIFEST-SCHEMA",
        "schemaVersion",
        f"schemaVersion must equal {SUPPORTED_SCHEMA_VERSION}.",
    )
    add_if(
        diagnostics,
        manifest.get("packetId") != PACKET_ID,
        "AL-TDF-MANIFEST-SCHEMA",
        "packetId",
        f"packetId must equal {PACKET_ID}.",
    )

    source_version = manifest.get("sourceVersion")
    add_if(
        diagnostics,
        not isinstance(source_version, str)
        or SOURCE_VERSION_PATTERN.fullmatch(source_version) is None,
        "AL-TDF-MANIFEST-SCHEMA",
        "sourceVersion",
        "sourceVersion must match tdf-rbe-YYYY-MM-DD-vNNN.",
    )
    add_if(
        diagnostics,
        not isinstance(manifest.get("createdAtUtc"), str)
        or UTC_PATTERN.fullmatch(manifest["createdAtUtc"]) is None,
        "AL-TDF-MANIFEST-SCHEMA",
        "createdAtUtc",
        "createdAtUtc must be a second-precision UTC timestamp ending in Z.",
    )
    add_if(
        diagnostics,
        manifest.get("supersedesSourceVersion") == source_version,
        "AL-TDF-SOURCE-VERSION-MISMATCH",
        "supersedesSourceVersion",
        "A source version cannot supersede itself.",
    )
    return diagnostics


def validate_authority(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    authority = manifest.get("authority")
    if not isinstance(authority, dict):
        return [
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                "authority",
                "authority must be an object.",
            )
        ]

    for field, expected in AUTHORITY_EXPECTED.items():
        actual = authority.get(field)
        add_if(
            diagnostics,
            actual != expected,
            "AL-TDF-AUTHORITY-LEAK",
            f"authority.{field}",
            f"authority.{field} must equal {expected!r}; found {actual!r}.",
        )
    add_if(
        diagnostics,
        set(authority) != set(AUTHORITY_EXPECTED),
        "AL-TDF-MANIFEST-SCHEMA",
        "authority",
        "authority fields must exactly match the retained ownership contract.",
    )
    return diagnostics


def validate_readiness(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    readiness = manifest.get("readiness")
    if not isinstance(readiness, dict):
        return [
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                "readiness",
                "readiness must be an object.",
            )
        ]

    technical_state = readiness.get("technicalPacketState")
    user_state = readiness.get("userCreativeState")
    add_if(
        diagnostics,
        technical_state not in TECHNICAL_STATES,
        "AL-TDF-MANIFEST-SCHEMA",
        "readiness.technicalPacketState",
        "technicalPacketState is not recognized.",
    )
    add_if(
        diagnostics,
        user_state not in USER_STATES,
        "AL-TDF-MANIFEST-SCHEMA",
        "readiness.userCreativeState",
        "userCreativeState is not recognized.",
    )
    add_if(
        diagnostics,
        readiness.get("runtimeIntegrationState") != "Blocked",
        "AL-TDF-AUTHORITY-LEAK",
        "readiness.runtimeIntegrationState",
        "Runtime integration must remain Blocked in a terrestrial source packet.",
    )
    add_if(
        diagnostics,
        readiness.get("narrativeNamingState") != "WorkingLabelsOnly",
        "AL-TDF-AUTHORITY-LEAK",
        "readiness.narrativeNamingState",
        "Narrative naming must remain WorkingLabelsOnly.",
    )
    add_if(
        diagnostics,
        technical_state == "TechnicalHandoffComplete"
        and user_state == "ApprovedSourceVersion",
        "AL-TDF-AUTHORITY-LEAK",
        "readiness",
        "Technical handoff and user approval require separately retained decisions.",
    )
    return diagnostics


def validate_quality_and_optimization(
    manifest: dict[str, Any],
) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    quality = manifest.get("qualityTarget")
    if not isinstance(quality, dict):
        diagnostics.append(
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                "qualityTarget",
                "qualityTarget must be an object.",
            )
        )
    else:
        expected = {
            "audience": "adult",
            "silhouetteRequiredWithoutColorOrVfx": True,
            "normalSpeedMotionReviewRequired": True,
            "neutralLightAnatomyReviewRequired": True,
            "marketplaceIdentityMayShipUnchanged": False,
            "paletteSwapCountsAsUnique": False,
        }
        for field, value in expected.items():
            add_if(
                diagnostics,
                quality.get(field) != value,
                "AL-TDF-AUTHORITY-LEAK",
                f"qualityTarget.{field}",
                f"qualityTarget.{field} must equal {value!r}.",
            )
        add_if(
            diagnostics,
            not is_nonblank(quality.get("visualStyle")),
            "AL-TDF-MANIFEST-SCHEMA",
            "qualityTarget.visualStyle",
            "visualStyle must be nonblank.",
        )

    optimization = manifest.get("optimizationIntent")
    if not isinstance(optimization, dict):
        diagnostics.append(
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                "optimizationIntent",
                "optimizationIntent must be an object.",
            )
        )
    else:
        expected = {
            "playerPackageIncludesOfflineSource": False,
            "playerPackageIncludesMarketplaceArchives": False,
            "defaultRuntimeTierRequiresOnlyOneSelectedPresentation": True,
            "sharedMaterialAndPackedTextureChannelsRequired": True,
            "pooledBoundedVfxRequired": True,
            "lodAndCullingRequiredBeforeRuntimeAcceptance": True,
            "lowTierThreatAndObjectiveReadMustMatchHigherTiers": True,
            "representativeLowTierMobileAndPcProfilingRequired": True,
        }
        for field, value in expected.items():
            add_if(
                diagnostics,
                optimization.get(field) != value,
                "AL-TDF-AUTHORITY-LEAK",
                f"optimizationIntent.{field}",
                f"optimizationIntent.{field} must equal {value!r}.",
            )
    return diagnostics


def validate_budgets(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    budgets = manifest.get("presentationBudgets")
    if not isinstance(budgets, dict):
        return [
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                "presentationBudgets",
                "presentationBudgets must be an object.",
            )
        ]

    add_if(
        diagnostics,
        budgets.get("cinematicSourcePackagedInPlayer") is not False,
        "AL-TDF-AUTHORITY-LEAK",
        "presentationBudgets.cinematicSourcePackagedInPlayer",
        "Offline cinematic source must never be packaged in Player.",
    )

    numeric_fields = (
        "maximumSkinnedTriangles",
        "maximumDeformBones",
        "maximumMaterialSlots",
        "maximumActiveParticles",
        "maximumDynamicLights",
    )
    runtime_tiers = ("low_mobile", "balanced", "high_pc")
    all_tiers = (*runtime_tiers, "cinematic_offline")

    for role in ("boss", "elite"):
        role_budget = budgets.get(role)
        if not isinstance(role_budget, dict):
            diagnostics.append(
                diagnostic(
                    "AL-TDF-MANIFEST-SCHEMA",
                    f"presentationBudgets.{role}",
                    f"{role} budgets must be an object.",
                )
            )
            continue

        for tier_name in all_tiers:
            tier = role_budget.get(tier_name)
            path = f"presentationBudgets.{role}.{tier_name}"
            if not isinstance(tier, dict):
                diagnostics.append(
                    diagnostic(
                        "AL-TDF-MANIFEST-SCHEMA",
                        path,
                        f"Required tier is missing: {tier_name}",
                    )
                )
                continue
            for field in (*numeric_fields, "maximumCompressedContentBytes"):
                value = tier.get(field)
                minimum = 0 if field in {
                    "maximumActiveParticles",
                    "maximumDynamicLights",
                    "maximumCompressedContentBytes",
                } else 1
                add_if(
                    diagnostics,
                    not isinstance(value, int) or isinstance(value, bool) or value < minimum,
                    "AL-TDF-MANIFEST-SCHEMA",
                    f"{path}.{field}",
                    f"{field} must be an integer >= {minimum}.",
                )
            add_if(
                diagnostics,
                not is_nonblank(tier.get("textureIntent")),
                "AL-TDF-MANIFEST-SCHEMA",
                f"{path}.textureIntent",
                "textureIntent must be nonblank.",
            )
            should_package = tier_name != "cinematic_offline"
            add_if(
                diagnostics,
                tier.get("packagedInPlayer") is not should_package,
                "AL-TDF-AUTHORITY-LEAK",
                f"{path}.packagedInPlayer",
                f"{tier_name} packagedInPlayer must be {should_package}.",
            )
            if tier_name == "cinematic_offline":
                add_if(
                    diagnostics,
                    tier.get("maximumCompressedContentBytes") != 0,
                    "AL-TDF-AUTHORITY-LEAK",
                    f"{path}.maximumCompressedContentBytes",
                    "Offline cinematic source must have a Player package budget of zero bytes.",
                )

        for field in numeric_fields:
            values = []
            valid = True
            for tier_name in all_tiers:
                tier = role_budget.get(tier_name)
                value = tier.get(field) if isinstance(tier, dict) else None
                if not isinstance(value, int) or isinstance(value, bool):
                    valid = False
                    break
                values.append(value)
            add_if(
                diagnostics,
                valid and values != sorted(values),
                "AL-TDF-MANIFEST-SCHEMA",
                f"presentationBudgets.{role}.{field}",
                f"{field} must be monotonic from low/mobile through cinematic.",
            )

        compressed = [
            role_budget.get(tier, {}).get("maximumCompressedContentBytes")
            for tier in runtime_tiers
        ]
        add_if(
            diagnostics,
            all(isinstance(value, int) for value in compressed)
            and compressed != sorted(compressed),
            "AL-TDF-MANIFEST-SCHEMA",
            f"presentationBudgets.{role}.maximumCompressedContentBytes",
            "Runtime compressed-content budgets must be monotonic.",
        )

    ratios: dict[str, tuple[float, float]] = {}
    for field in (
        "lod1TriangleRatio",
        "lod2TriangleRatio",
        "distantTriangleRatioOrImpostor",
    ):
        value = budgets.get(field)
        valid = (
            isinstance(value, list)
            and len(value) == 2
            and all(
                isinstance(item, (int, float))
                and not isinstance(item, bool)
                and math.isfinite(item)
                and 0 < item <= 1
                for item in value
            )
            and value[0] <= value[1]
        )
        add_if(
            diagnostics,
            not valid,
            "AL-TDF-MANIFEST-SCHEMA",
            f"presentationBudgets.{field}",
            f"{field} must be an ordered two-value ratio range in (0, 1].",
        )
        if valid:
            ratios[field] = (float(value[0]), float(value[1]))

    if len(ratios) == 3:
        add_if(
            diagnostics,
            not (
                ratios["distantTriangleRatioOrImpostor"][1]
                < ratios["lod2TriangleRatio"][0]
                < ratios["lod2TriangleRatio"][1]
                < ratios["lod1TriangleRatio"][0]
            ),
            "AL-TDF-MANIFEST-SCHEMA",
            "presentationBudgets",
            "Distant, LOD2, and LOD1 ratio ranges must be strictly separated in ascending cost.",
        )
    return diagnostics


def validate_profile_shape(
    profile: Any,
    index: int,
    source_version: Any,
) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    path = f"profiles[{index}]"
    if not isinstance(profile, dict):
        return [
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                path,
                "Profile must be an object.",
            )
        ]

    profile_id = profile.get("terrestrialProfileId")
    identity = {"profile_id": profile_id if isinstance(profile_id, str) else None}
    missing = sorted(PROFILE_FIELDS - set(profile))
    unknown = sorted(set(profile) - PROFILE_FIELDS)
    for field in missing:
        diagnostics.append(
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                f"{path}.{field}",
                f"Required profile field is missing: {field}",
                **identity,
            )
        )
    for field in unknown:
        diagnostics.append(
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                f"{path}.{field}",
                f"Unknown profile field is not allowed: {field}",
                **identity,
            )
        )

    add_if(
        diagnostics,
        not isinstance(profile_id, str)
        or SOURCE_ID_PATTERN.fullmatch(profile_id) is None,
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.terrestrialProfileId",
        "terrestrialProfileId must match the stable tdf_* source-ID pattern.",
        **identity,
    )
    add_if(
        diagnostics,
        profile.get("sourceVersion") != source_version,
        "AL-TDF-SOURCE-VERSION-MISMATCH",
        f"{path}.sourceVersion",
        "Profile sourceVersion must match the packet sourceVersion.",
        **identity,
    )
    add_if(
        diagnostics,
        profile.get("workingLabelStatus") != "nonlocalized_review_only",
        "AL-TDF-AUTHORITY-LEAK",
        f"{path}.workingLabelStatus",
        "Working label must remain nonlocalized review-only text.",
        **identity,
    )
    add_if(
        diagnostics,
        profile.get("designIntentStatus") != "source_design_only",
        "AL-TDF-AUTHORITY-LEAK",
        f"{path}.designIntentStatus",
        "Design tags must remain source-design-only.",
        **identity,
    )
    add_if(
        diagnostics,
        profile.get("profileRole") not in EXPECTED_ROLE_COUNTS,
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.profileRole",
        "profileRole is not recognized.",
        **identity,
    )

    for field in NONBLANK_PROFILE_TEXT_FIELDS:
        add_if(
            diagnostics,
            not is_nonblank(profile.get(field)),
            "AL-TDF-MANIFEST-SCHEMA",
            f"{path}.{field}",
            f"{field} must be nonblank.",
            **identity,
        )
    for field in NONEMPTY_PROFILE_LIST_FIELDS:
        add_if(
            diagnostics,
            not is_nonempty_unique_string_list(profile.get(field))
            if field != "variants"
            else not isinstance(profile.get(field), list)
            or len(profile.get(field, [])) == 0,
            "AL-TDF-MANIFEST-SCHEMA",
            f"{path}.{field}",
            f"{field} must be a nonempty deterministic list.",
            **identity,
        )
    add_if(
        diagnostics,
        not is_nonempty_unique_string_list(profile.get("materialSlotIntent"), minimum=2),
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.materialSlotIntent",
        "At least two unique material families are required.",
        **identity,
    )
    add_if(
        diagnostics,
        profile.get("conceptSourceState") not in CONCEPT_STATES,
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.conceptSourceState",
        "conceptSourceState is not recognized.",
        **identity,
    )

    scale = profile.get("approximateWorldScale")
    if not isinstance(scale, dict):
        diagnostics.append(
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                f"{path}.approximateWorldScale",
                "approximateWorldScale must be an object.",
                **identity,
            )
        )
    else:
        add_if(
            diagnostics,
            scale.get("referenceBasis") != "adult_champion_body_height_equals_1_0",
            "AL-TDF-MANIFEST-SCHEMA",
            f"{path}.approximateWorldScale.referenceBasis",
            "All profiles must use the same Champion-relative scale basis.",
            **identity,
        )
        add_if(
            diagnostics,
            scale.get("units") != "champion_body_height_multiples",
            "AL-TDF-MANIFEST-SCHEMA",
            f"{path}.approximateWorldScale.units",
            "Scale units must be Champion body-height multiples.",
            **identity,
        )
        measurements = scale.get("measurements")
        valid_measurements = (
            isinstance(measurements, dict)
            and len(measurements) >= 2
            and all(
                isinstance(value, (int, float))
                and not isinstance(value, bool)
                and math.isfinite(value)
                and value > 0
                for value in measurements.values()
            )
        )
        add_if(
            diagnostics,
            not valid_measurements,
            "AL-TDF-MANIFEST-SCHEMA",
            f"{path}.approximateWorldScale.measurements",
            "Scale needs at least two finite, strictly positive measurements.",
            **identity,
        )
        add_if(
            diagnostics,
            not is_nonblank(scale.get("massClass")),
            "AL-TDF-MANIFEST-SCHEMA",
            f"{path}.approximateWorldScale.massClass",
            "massClass must be nonblank.",
            **identity,
        )
        add_if(
            diagnostics,
            scale.get("measurementStatus")
            != "source_estimate_pending_user_approval_and_engineering_mapping",
            "AL-TDF-AUTHORITY-LEAK",
            f"{path}.approximateWorldScale.measurementStatus",
            "Scale must remain a source estimate pending user approval and engineering mapping.",
            **identity,
        )

    tags = profile.get("designIntentTags")
    if isinstance(tags, list):
        realm_tags = [
            tag for tag in tags
            if isinstance(tag, str) and tag.startswith("realm-source:")
        ]
        add_if(
            diagnostics,
            len(realm_tags) != 1
            or realm_tags[0].removeprefix("realm-source:") not in EXPECTED_REALMS,
            "AL-TDF-MANIFEST-SCHEMA",
            f"{path}.designIntentTags",
            "Each profile requires exactly one recognized realm-source tag.",
            **identity,
        )
        add_if(
            diagnostics,
            not any(
                isinstance(tag, str) and tag.startswith("biome:")
                for tag in tags
            ),
            "AL-TDF-MANIFEST-SCHEMA",
            f"{path}.designIntentTags",
            "Each profile requires a source-only biome tag.",
            **identity,
        )

    primary_assets = profile.get("primarySourceAssetIds")
    add_if(
        diagnostics,
        not isinstance(primary_assets, list)
        or not all(
            isinstance(asset_id, str)
            and SOURCE_ID_PATTERN.fullmatch(asset_id) is not None
            for asset_id in primary_assets
        )
        or len(primary_assets) != len(set(primary_assets)),
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.primarySourceAssetIds",
        "primarySourceAssetIds must be a unique list of stable source IDs.",
        **identity,
    )
    research_ids = profile.get("acquisitionResearchIds")
    add_if(
        diagnostics,
        not isinstance(research_ids, list)
        or not all(
            isinstance(research_id, str)
            and SOURCE_ID_PATTERN.fullmatch(research_id) is not None
            for research_id in research_ids
        )
        or len(research_ids) != len(set(research_ids)),
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.acquisitionResearchIds",
        "acquisitionResearchIds must be a unique list of stable source IDs.",
        **identity,
    )
    add_if(
        diagnostics,
        profile.get("conceptSourceState") == "NotGenerated"
        and isinstance(primary_assets, list)
        and len(primary_assets) > 0,
        "AL-TDF-PROVENANCE-INCOMPLETE",
        f"{path}.conceptSourceState",
        "A NotGenerated profile cannot reference primary source assets.",
        **identity,
    )
    return diagnostics


def validate_variant(
    variant: Any,
    profile_id: str | None,
    source_version: Any,
    path: str,
) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    if not isinstance(variant, dict):
        return [
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                path,
                "Variant must be an object.",
                profile_id=profile_id,
            )
        ]
    variant_id = variant.get("variantId")
    identity = {"profile_id": profile_id, "variant_id": variant_id}
    add_if(
        diagnostics,
        not isinstance(variant_id, str)
        or SOURCE_ID_PATTERN.fullmatch(variant_id) is None,
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.variantId",
        "variantId must match the stable tdf_* source-ID pattern.",
        **identity,
    )
    add_if(
        diagnostics,
        variant.get("profileId") != profile_id,
        "AL-TDF-MISSING-ASSET",
        f"{path}.profileId",
        "Variant must belong to its containing profile.",
        **identity,
    )
    add_if(
        diagnostics,
        variant.get("sourceVersion") != source_version,
        "AL-TDF-SOURCE-VERSION-MISMATCH",
        f"{path}.sourceVersion",
        "Variant sourceVersion must match the packet sourceVersion.",
        **identity,
    )
    status = variant.get("status")
    add_if(
        diagnostics,
        status not in VARIANT_STATES,
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.status",
        "Variant status is not recognized.",
        **identity,
    )
    source_asset_ids = variant.get("sourceAssetIds")
    add_if(
        diagnostics,
        not isinstance(source_asset_ids, list)
        or not all(
            isinstance(asset_id, str)
            and SOURCE_ID_PATTERN.fullmatch(asset_id) is not None
            for asset_id in source_asset_ids
        )
        or len(source_asset_ids) != len(set(source_asset_ids)),
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.sourceAssetIds",
        "Variant sourceAssetIds must be a unique list of stable source IDs.",
        **identity,
    )
    add_if(
        diagnostics,
        status == "ProposedTextOnly"
        and isinstance(source_asset_ids, list)
        and len(source_asset_ids) > 0,
        "AL-TDF-VARIANT-SOURCE-MISSING",
        f"{path}.sourceAssetIds",
        "A ProposedTextOnly variant cannot claim delivered source assets.",
        **identity,
    )
    add_if(
        diagnostics,
        status in {"DeliveredReference", "ReadyForUserReview", "UserApproved"}
        and isinstance(source_asset_ids, list)
        and len(source_asset_ids) == 0,
        "AL-TDF-VARIANT-SOURCE-MISSING",
        f"{path}.sourceAssetIds",
        f"{status} requires at least one source asset.",
        **identity,
    )
    add_if(
        diagnostics,
        status == "UserApproved" and variant.get("userCreativeDecision") != "Approved",
        "AL-TDF-AUTHORITY-LEAK",
        f"{path}.userCreativeDecision",
        "UserApproved status requires an explicit Approved user decision.",
        **identity,
    )
    for field in ("variantKind", "intent"):
        add_if(
            diagnostics,
            not is_nonblank(variant.get(field)),
            "AL-TDF-MANIFEST-SCHEMA",
            f"{path}.{field}",
            f"{field} must be nonblank.",
            **identity,
        )
    add_if(
        diagnostics,
        not is_nonempty_unique_string_list(variant.get("changedDesignDimensions")),
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.changedDesignDimensions",
        "changedDesignDimensions must be a nonempty unique list.",
        **identity,
    )
    add_if(
        diagnostics,
        not isinstance(variant.get("requiresSeparateEngineeringSource"), bool),
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.requiresSeparateEngineeringSource",
        "requiresSeparateEngineeringSource must be boolean.",
        **identity,
    )
    return diagnostics


def media_identity(path: Path) -> tuple[str, int, int]:
    with path.open("rb") as handle:
        header = handle.read(32)
        if header.startswith(b"\x89PNG\r\n\x1a\n"):
            if len(header) < 24:
                raise ValueError("truncated PNG header")
            width, height = struct.unpack(">II", header[16:24])
            return "image/png", width, height
        if header.startswith(b"\xff\xd8"):
            handle.seek(2)
            while True:
                marker_start = handle.read(1)
                if not marker_start:
                    raise ValueError("JPEG dimensions not found")
                if marker_start != b"\xff":
                    continue
                marker = handle.read(1)
                while marker == b"\xff":
                    marker = handle.read(1)
                if marker in {bytes([value]) for value in range(0xC0, 0xC4)} | {
                    bytes([value]) for value in range(0xC5, 0xC8)
                } | {
                    bytes([value]) for value in range(0xC9, 0xCC)
                } | {
                    bytes([value]) for value in range(0xCD, 0xD0)
                }:
                    length_bytes = handle.read(2)
                    if len(length_bytes) != 2:
                        raise ValueError("truncated JPEG segment")
                    handle.read(1)
                    height, width = struct.unpack(">HH", handle.read(4))
                    return "image/jpeg", width, height
                length_bytes = handle.read(2)
                if len(length_bytes) != 2:
                    raise ValueError("truncated JPEG segment")
                segment_length = struct.unpack(">H", length_bytes)[0]
                handle.seek(max(0, segment_length - 2), 1)
        if header.startswith(b"RIFF") and header[8:12] == b"WEBP":
            chunk = header[12:16]
            if chunk == b"VP8X":
                width = 1 + int.from_bytes(header[24:27], "little")
                height = 1 + int.from_bytes(header[27:30], "little")
                return "image/webp", width, height
            raise ValueError("unsupported WEBP header variant")
    raise ValueError("unsupported media signature")


def file_evidence(path: Path) -> tuple[bool, int, str, str, int, int]:
    stat = path.stat()
    cache_key = (str(path.resolve()), stat.st_mtime_ns, stat.st_size)
    cached = _FILE_EVIDENCE_CACHE.get(cache_key)
    if cached is not None:
        return cached

    raw = path.read_bytes()
    media_type, width, height = media_identity(path)
    evidence = (
        raw.startswith(b"version https://git-lfs.github.com/spec/"),
        len(raw),
        hashlib.sha256(raw).hexdigest(),
        media_type,
        width,
        height,
    )
    _FILE_EVIDENCE_CACHE[cache_key] = evidence
    return evidence


def safe_repository_path(value: Any) -> Path | None:
    if not isinstance(value, str) or not value:
        return None
    posix = PurePosixPath(value)
    if posix.is_absolute() or ".." in posix.parts or "." in posix.parts:
        return None
    if not (
        value.startswith("unity/Assets/")
        or value.startswith("unity/Docs/")
    ):
        return None
    resolved = (REPO_ROOT / Path(*posix.parts)).resolve()
    try:
        resolved.relative_to(REPO_ROOT.resolve())
    except ValueError:
        return None
    return resolved


def validate_asset(
    asset: Any,
    index: int,
    source_version: Any,
) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    path = f"sourceAssets[{index}]"
    if not isinstance(asset, dict):
        return [
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                path,
                "Source asset must be an object.",
            )
        ]
    asset_id = asset.get("assetId")
    identity = {"asset_id": asset_id if isinstance(asset_id, str) else None}
    add_if(
        diagnostics,
        not isinstance(asset_id, str)
        or SOURCE_ID_PATTERN.fullmatch(asset_id) is None,
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.assetId",
        "assetId must match the stable tdf_* source-ID pattern.",
        **identity,
    )
    add_if(
        diagnostics,
        asset.get("sourceVersion") != source_version,
        "AL-TDF-SOURCE-VERSION-MISMATCH",
        f"{path}.sourceVersion",
        "Asset sourceVersion must match the packet sourceVersion.",
        **identity,
    )
    add_if(
        diagnostics,
        asset.get("status") not in SOURCE_ASSET_STATES,
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.status",
        "Source asset status is not recognized.",
        **identity,
    )

    repository_path = safe_repository_path(asset.get("path"))
    add_if(
        diagnostics,
        repository_path is None,
        "AL-TDF-MISSING-ASSET",
        f"{path}.path",
        "Asset path must be a traversal-free repository-relative unity/Assets or unity/Docs path.",
        **identity,
    )
    if repository_path is None:
        return diagnostics
    add_if(
        diagnostics,
        not repository_path.exists(),
        "AL-TDF-MISSING-ASSET",
        f"{path}.path",
        f"Source asset does not exist: {asset.get('path')}",
        **identity,
    )
    if not repository_path.exists():
        return diagnostics

    try:
        (
            pointer_only,
            actual_length,
            actual_hash,
            media_type,
            width,
            height,
        ) = file_evidence(repository_path)
    except ValueError as exc:
        diagnostics.append(
            diagnostic(
                "AL-TDF-MEDIA-TYPE",
                f"{path}.mediaType",
                str(exc),
                **identity,
            )
        )
        return diagnostics

    add_if(
        diagnostics,
        pointer_only,
        "AL-TDF-LFS-POINTER-ONLY",
        f"{path}.path",
        "Retrieved source is an LFS pointer rather than media bytes.",
        **identity,
    )
    add_if(
        diagnostics,
        not isinstance(asset.get("sha256"), str)
        or SHA256_PATTERN.fullmatch(asset["sha256"]) is None
        or asset["sha256"] != actual_hash,
        "AL-TDF-HASH-MISMATCH",
        f"{path}.sha256",
        f"Manifest SHA-256 does not match retrieved bytes ({actual_hash}).",
        **identity,
    )
    add_if(
        diagnostics,
        asset.get("byteLength") != actual_length,
        "AL-TDF-HASH-MISMATCH",
        f"{path}.byteLength",
        f"Manifest byteLength does not match retrieved bytes ({actual_length}).",
        **identity,
    )

    add_if(
        diagnostics,
        asset.get("mediaType") != media_type,
        "AL-TDF-MEDIA-TYPE",
        f"{path}.mediaType",
        f"Manifest mediaType does not match bytes ({media_type}).",
        **identity,
    )
    add_if(
        diagnostics,
        asset.get("pixelWidth") != width
        or asset.get("pixelHeight") != height,
        "AL-TDF-DIMENSION-MISMATCH",
        f"{path}.pixelWidth",
        f"Manifest dimensions do not match media header ({width}x{height}).",
        **identity,
    )

    storage = asset.get("repositoryStorage")
    add_if(
        diagnostics,
        storage not in {"git", "git_lfs"},
        "AL-TDF-MANIFEST-SCHEMA",
        f"{path}.repositoryStorage",
        "repositoryStorage must be git or git_lfs.",
        **identity,
    )
    if storage == "git_lfs":
        add_if(
            diagnostics,
            asset.get("gitLfsOid") != actual_hash,
            "AL-TDF-HASH-MISMATCH",
            f"{path}.gitLfsOid",
            "Git LFS OID must match the retrieved binary SHA-256.",
            **identity,
        )
        add_if(
            diagnostics,
            asset.get("gitLfsSize") != actual_length,
            "AL-TDF-HASH-MISMATCH",
            f"{path}.gitLfsSize",
            "Git LFS size must match retrieved byte length.",
            **identity,
        )
    if str(asset.get("path", "")).startswith("unity/Assets/"):
        add_if(
            diagnostics,
            not isinstance(asset.get("unityAssetGuid"), str)
            or re.fullmatch(r"[a-f0-9]{32}", asset["unityAssetGuid"]) is None,
            "AL-TDF-UNITY-IMPORT",
            f"{path}.unityAssetGuid",
            "Assets-path source requires a 32-character Unity GUID.",
            **identity,
        )
    else:
        add_if(
            diagnostics,
            asset.get("unityAssetGuid") is not None,
            "AL-TDF-UNITY-IMPORT",
            f"{path}.unityAssetGuid",
            "Docs-path source must not claim a Unity asset GUID.",
            **identity,
        )

    add_if(
        diagnostics,
        asset.get("status") in {"ReadyForUserReview", "UserApproved"}
        and not is_nonblank(asset.get("reviewSurfaceUrlOrPrAnchor")),
        "AL-TDF-REVIEW-SURFACE-MISSING",
        f"{path}.reviewSurfaceUrlOrPrAnchor",
        "Review-ready source requires a direct review surface.",
        **identity,
    )
    return diagnostics


def validate_references(
    manifest: dict[str, Any],
    profiles: list[dict[str, Any]],
    assets: list[dict[str, Any]],
    generations: list[dict[str, Any]],
    external_inputs: list[dict[str, Any]],
    research: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    profile_ids = {
        profile.get("terrestrialProfileId")
        for profile in profiles
        if isinstance(profile, dict)
    }
    variants = [
        variant
        for profile in profiles
        if isinstance(profile, dict)
        for variant in profile.get("variants", [])
        if isinstance(variant, dict)
    ]
    variant_ids = {variant.get("variantId") for variant in variants}
    asset_ids = {
        asset.get("assetId")
        for asset in assets
        if isinstance(asset, dict)
    }
    generation_ids = {
        record.get("generationRecordId")
        for record in generations
        if isinstance(record, dict)
    }
    external_ids = {
        record.get("externalInputId")
        for record in external_inputs
        if isinstance(record, dict)
    }
    research_ids = {
        record.get("researchId")
        for record in research
        if isinstance(record, dict)
    }

    all_ids: list[tuple[str, str]] = []
    all_ids.extend(("profile", value) for value in profile_ids if isinstance(value, str))
    all_ids.extend(("variant", value) for value in variant_ids if isinstance(value, str))
    all_ids.extend(("asset", value) for value in asset_ids if isinstance(value, str))
    all_ids.extend(("generation", value) for value in generation_ids if isinstance(value, str))
    all_ids.extend(("external", value) for value in external_ids if isinstance(value, str))
    all_ids.extend(("research", value) for value in research_ids if isinstance(value, str))
    seen: dict[str, str] = {}
    for kind, value in sorted(all_ids, key=lambda item: (item[1], item[0])):
        if value in seen:
            diagnostics.append(
                diagnostic(
                    "AL-TDF-DUPLICATE-ID",
                    value,
                    f"ID {value!r} is reused by {seen[value]} and {kind}.",
                )
            )
        else:
            seen[value] = kind

    for profile_index, profile in enumerate(profiles):
        if not isinstance(profile, dict):
            continue
        profile_id = profile.get("terrestrialProfileId")
        for asset_id in profile.get("primarySourceAssetIds", []):
            add_if(
                diagnostics,
                asset_id not in asset_ids,
                "AL-TDF-MISSING-ASSET",
                f"profiles[{profile_index}].primarySourceAssetIds",
                f"Profile references missing source asset {asset_id!r}.",
                profile_id=profile_id,
                asset_id=asset_id,
            )
        for research_id in profile.get("acquisitionResearchIds", []):
            add_if(
                diagnostics,
                research_id not in research_ids,
                "AL-TDF-MISSING-ASSET",
                f"profiles[{profile_index}].acquisitionResearchIds",
                f"Profile references missing acquisition research {research_id!r}.",
                profile_id=profile_id,
            )
        for variant_index, variant in enumerate(profile.get("variants", [])):
            if not isinstance(variant, dict):
                continue
            variant_id = variant.get("variantId")
            for asset_id in variant.get("sourceAssetIds", []):
                add_if(
                    diagnostics,
                    asset_id not in asset_ids,
                    "AL-TDF-VARIANT-SOURCE-MISSING",
                    f"profiles[{profile_index}].variants[{variant_index}].sourceAssetIds",
                    f"Variant references missing source asset {asset_id!r}.",
                    profile_id=profile_id,
                    variant_id=variant_id,
                    asset_id=asset_id,
                )

    for asset_index, asset in enumerate(assets):
        if not isinstance(asset, dict):
            continue
        asset_id = asset.get("assetId")
        for profile_id in asset.get("profileIds", []):
            add_if(
                diagnostics,
                profile_id not in profile_ids,
                "AL-TDF-MISSING-ASSET",
                f"sourceAssets[{asset_index}].profileIds",
                f"Asset references missing profile {profile_id!r}.",
                profile_id=profile_id,
                asset_id=asset_id,
            )
        for variant_id in asset.get("variantIds", []):
            add_if(
                diagnostics,
                variant_id not in variant_ids,
                "AL-TDF-MISSING-ASSET",
                f"sourceAssets[{asset_index}].variantIds",
                f"Asset references missing variant {variant_id!r}.",
                variant_id=variant_id,
                asset_id=asset_id,
            )
        generation_id = asset.get("generationRecordId")
        add_if(
            diagnostics,
            generation_id not in generation_ids,
            "AL-TDF-PROVENANCE-INCOMPLETE",
            f"sourceAssets[{asset_index}].generationRecordId",
            f"Asset references missing generation record {generation_id!r}.",
            asset_id=asset_id,
        )
        license_id = asset.get("licenseRecordId")
        add_if(
            diagnostics,
            license_id not in external_ids,
            "AL-TDF-PROVENANCE-INCOMPLETE",
            f"sourceAssets[{asset_index}].licenseRecordId",
            f"Asset references missing provenance/license record {license_id!r}.",
            asset_id=asset_id,
        )

    for generation_index, record in enumerate(generations):
        if not isinstance(record, dict):
            continue
        generation_id = record.get("generationRecordId")
        add_if(
            diagnostics,
            record.get("sourceVersion") != manifest.get("sourceVersion"),
            "AL-TDF-SOURCE-VERSION-MISMATCH",
            f"generationRecords[{generation_index}].sourceVersion",
            "Generation sourceVersion must match the packet.",
        )
        for asset_id in record.get("inputAssetIds", []):
            add_if(
                diagnostics,
                asset_id not in asset_ids,
                "AL-TDF-PROVENANCE-INCOMPLETE",
                f"generationRecords[{generation_index}].inputAssetIds",
                f"Generation {generation_id!r} references missing input asset {asset_id!r}.",
                asset_id=asset_id,
            )
        for external_id in record.get("externalInputIds", []):
            add_if(
                diagnostics,
                external_id not in external_ids,
                "AL-TDF-PROVENANCE-INCOMPLETE",
                f"generationRecords[{generation_index}].externalInputIds",
                f"Generation {generation_id!r} references missing external input {external_id!r}.",
            )
        for asset_id in record.get("outputAssetIds", []):
            add_if(
                diagnostics,
                asset_id not in asset_ids,
                "AL-TDF-PROVENANCE-INCOMPLETE",
                f"generationRecords[{generation_index}].outputAssetIds",
                f"Generation {generation_id!r} references missing output asset {asset_id!r}.",
                asset_id=asset_id,
            )

    for research_index, record in enumerate(research):
        if not isinstance(record, dict):
            diagnostics.append(
                diagnostic(
                    "AL-TDF-MANIFEST-SCHEMA",
                    f"acquisitionResearch[{research_index}]",
                    "Acquisition research record must be an object.",
                )
            )
            continue
        research_id = record.get("researchId")
        add_if(
            diagnostics,
            not isinstance(research_id, str)
            or SOURCE_ID_PATTERN.fullmatch(research_id) is None,
            "AL-TDF-MANIFEST-SCHEMA",
            f"acquisitionResearch[{research_index}].researchId",
            "researchId must match the stable tdf_* pattern.",
        )
        for profile_id in record.get("profileIds", []):
            add_if(
                diagnostics,
                profile_id not in profile_ids,
                "AL-TDF-MISSING-ASSET",
                f"acquisitionResearch[{research_index}].profileIds",
                f"Research references missing profile {profile_id!r}.",
                profile_id=profile_id,
            )
        expected_false = (
            "usedAsGenerationInput",
            "unchangedIdentityAllowed",
            "purchaseApproved",
        )
        for field in expected_false:
            add_if(
                diagnostics,
                record.get(field) is not False,
                "AL-TDF-AUTHORITY-LEAK",
                f"acquisitionResearch[{research_index}].{field}",
                f"{field} must remain false.",
            )
    return diagnostics


def validate_counts_and_distinction(
    profiles: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    add_if(
        diagnostics,
        len(profiles) != 16,
        "AL-TDF-MANIFEST-SCHEMA",
        "profiles",
        f"Packet must contain exactly 16 profiles; found {len(profiles)}.",
    )

    role_counts = {
        role: sum(
            1
            for profile in profiles
            if isinstance(profile, dict) and profile.get("profileRole") == role
        )
        for role in EXPECTED_ROLE_COUNTS
    }
    for role, expected in EXPECTED_ROLE_COUNTS.items():
        add_if(
            diagnostics,
            role_counts[role] != expected,
            "AL-TDF-MANIFEST-SCHEMA",
            "profiles",
            f"Expected {expected} {role} profiles; found {role_counts[role]}.",
        )

    for realm in EXPECTED_REALMS:
        realm_profiles = [
            profile
            for profile in profiles
            if isinstance(profile, dict)
            and f"realm-source:{realm}" in profile.get("designIntentTags", [])
        ]
        bosses = sum(
            profile.get("profileRole") == "outer_warzone_boss"
            for profile in realm_profiles
        )
        elites = sum(
            profile.get("profileRole") == "inner_realm_field_elite"
            for profile in realm_profiles
        )
        add_if(
            diagnostics,
            bosses != 1 or elites != 3,
            "AL-TDF-MANIFEST-SCHEMA",
            f"profiles.realm-source:{realm}",
            f"{realm} must contain one boss and three elites; found {bosses} boss(es) and {elites} elite(s).",
        )

    silhouettes: dict[str, str] = {}
    for profile in profiles:
        if not isinstance(profile, dict):
            continue
        profile_id = profile.get("terrestrialProfileId")
        silhouette = profile.get("silhouetteClass")
        if not is_nonblank(silhouette):
            continue
        if silhouette in silhouettes:
            diagnostics.append(
                diagnostic(
                    "AL-TDF-DUPLICATE-ID",
                    "profiles.silhouetteClass",
                    f"Silhouette class {silhouette!r} is reused by {silhouettes[silhouette]!r} and {profile_id!r}.",
                    profile_id=profile_id,
                )
            )
        else:
            silhouettes[silhouette] = profile_id
    return diagnostics


def validate_packet(
    manifest: Any,
    schema: Any,
) -> list[dict[str, Any]]:
    diagnostics: list[dict[str, Any]] = []
    diagnostics.extend(validate_schema_document(schema))
    diagnostics.extend(validate_top_level(manifest))
    if not isinstance(manifest, dict):
        return sorted_diagnostics(diagnostics)

    source_version = manifest.get("sourceVersion")
    diagnostics.extend(validate_authority(manifest))
    diagnostics.extend(validate_readiness(manifest))
    diagnostics.extend(validate_quality_and_optimization(manifest))
    diagnostics.extend(validate_budgets(manifest))

    profiles_value = manifest.get("profiles")
    profiles = profiles_value if isinstance(profiles_value, list) else []
    if not isinstance(profiles_value, list):
        diagnostics.append(
            diagnostic(
                "AL-TDF-MANIFEST-SCHEMA",
                "profiles",
                "profiles must be an array.",
            )
        )
    diagnostics.extend(validate_counts_and_distinction(profiles))

    profile_ids: list[str] = []
    variant_ids: list[str] = []
    for profile_index, profile in enumerate(profiles):
        diagnostics.extend(
            validate_profile_shape(profile, profile_index, source_version)
        )
        if not isinstance(profile, dict):
            continue
        profile_id = profile.get("terrestrialProfileId")
        if isinstance(profile_id, str):
            profile_ids.append(profile_id)
        for variant_index, variant in enumerate(profile.get("variants", [])):
            diagnostics.extend(
                validate_variant(
                    variant,
                    profile_id if isinstance(profile_id, str) else None,
                    source_version,
                    f"profiles[{profile_index}].variants[{variant_index}]",
                )
            )
            if isinstance(variant, dict) and isinstance(variant.get("variantId"), str):
                variant_ids.append(variant["variantId"])

    for values, field in (
        (profile_ids, "profiles.terrestrialProfileId"),
        (variant_ids, "profiles.variants.variantId"),
    ):
        duplicates = sorted(
            value for value in set(values) if values.count(value) > 1
        )
        for value in duplicates:
            diagnostics.append(
                diagnostic(
                    "AL-TDF-DUPLICATE-ID",
                    field,
                    f"Duplicate ID: {value}",
                )
            )

    raw_assets = manifest.get("sourceAssets")
    raw_generations = manifest.get("generationRecords")
    raw_external = manifest.get("externalInputs")
    raw_research = manifest.get("acquisitionResearch")
    list_fields = (
        ("sourceAssets", raw_assets),
        ("generationRecords", raw_generations),
        ("externalInputs", raw_external),
        ("acquisitionResearch", raw_research),
    )
    for field, value in list_fields:
        add_if(
            diagnostics,
            not isinstance(value, list),
            "AL-TDF-MANIFEST-SCHEMA",
            field,
            f"{field} must be an array.",
        )
    assets = raw_assets if isinstance(raw_assets, list) else []
    generations = raw_generations if isinstance(raw_generations, list) else []
    external_inputs = raw_external if isinstance(raw_external, list) else []
    research = raw_research if isinstance(raw_research, list) else []

    for asset_index, asset in enumerate(assets):
        diagnostics.extend(validate_asset(asset, asset_index, source_version))

    diagnostics.extend(
        validate_references(
            manifest,
            profiles,
            assets,
            generations,
            external_inputs,
            research,
        )
    )

    technical_state = (
        manifest.get("readiness", {}).get("technicalPacketState")
        if isinstance(manifest.get("readiness"), dict)
        else None
    )
    add_if(
        diagnostics,
        technical_state
        in {"TechnicalReviewReady", "TechnicalHandoffComplete"}
        and (
            len(assets) < len(profiles)
            or any(
                isinstance(profile, dict)
                and (
                    profile.get("conceptSourceState")
                    not in {"ReadyForUserReview", "UserApproved"}
                    or not profile.get("primarySourceAssetIds")
                )
                for profile in profiles
            )
        ),
        "AL-TDF-REVIEW-SURFACE-MISSING",
        "readiness.technicalPacketState",
        "Technical review/handoff cannot advance until every profile has review-ready source.",
    )
    add_if(
        diagnostics,
        technical_state == "SourceIncomplete"
        and all(
            isinstance(profile, dict)
            and profile.get("conceptSourceState") in {"ReadyForUserReview", "UserApproved"}
            and profile.get("primarySourceAssetIds")
            for profile in profiles
        )
        and len(assets) >= len(profiles),
        "AL-TDF-MANIFEST-SCHEMA",
        "readiness.technicalPacketState",
        "SourceIncomplete is stale after every profile has review-ready source.",
    )

    for item in diagnostics:
        item["sourceVersion"] = (
            source_version if isinstance(source_version, str) else None
        )
    return sorted_diagnostics(diagnostics)


def sorted_diagnostics(
    diagnostics: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    return sorted(
        diagnostics,
        key=lambda item: (
            item["code"],
            item["fieldPath"],
            item.get("profileId") or "",
            item.get("variantId") or "",
            item.get("assetId") or "",
            item["message"],
        ),
    )


def summary(manifest: dict[str, Any], diagnostics: list[dict[str, Any]]) -> dict[str, Any]:
    profiles = manifest.get("profiles", [])
    profiles = profiles if isinstance(profiles, list) else []
    return {
        "result": "PASS" if not diagnostics else "FAIL",
        "sourceVersion": manifest.get("sourceVersion"),
        "technicalPacketState": (
            manifest.get("readiness", {}).get("technicalPacketState")
            if isinstance(manifest.get("readiness"), dict)
            else None
        ),
        "profileCount": len(profiles),
        "bossCount": sum(
            isinstance(profile, dict)
            and profile.get("profileRole") == "outer_warzone_boss"
            for profile in profiles
        ),
        "eliteCount": sum(
            isinstance(profile, dict)
            and profile.get("profileRole") == "inner_realm_field_elite"
            for profile in profiles
        ),
        "sourceAssetCount": len(manifest.get("sourceAssets", []))
        if isinstance(manifest.get("sourceAssets"), list)
        else 0,
        "generationRecordCount": len(manifest.get("generationRecords", []))
        if isinstance(manifest.get("generationRecords"), list)
        else 0,
        "diagnosticCount": len(diagnostics),
        "diagnostics": diagnostics,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--schema", type=Path, default=DEFAULT_SCHEMA)
    parser.add_argument("--json", action="store_true", dest="as_json")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    manifest, manifest_errors = read_json(args.manifest, "manifest")
    schema, schema_errors = read_json(args.schema, "schema")
    diagnostics = manifest_errors + schema_errors
    if not diagnostics:
        diagnostics = validate_packet(manifest, schema)
    result = summary(manifest if isinstance(manifest, dict) else {}, diagnostics)

    if args.as_json:
        print(json.dumps(result, indent=2, sort_keys=True))
    elif diagnostics:
        print("AL_TDF_REALM_BOSS_ELITE_SOURCE_VALIDATION_FAIL")
        for item in diagnostics:
            print(
                f"{item['code']} {item['fieldPath']}: {item['message']}"
            )
    else:
        print("AL_TDF_REALM_BOSS_ELITE_SOURCE_VALIDATION_PASS")
        print(f"SOURCE_VERSION={result['sourceVersion']}")
        print(
            "PROFILES="
            f"{result['profileCount']} "
            f"BOSSES={result['bossCount']} "
            f"ELITES={result['eliteCount']}"
        )
        print(
            "SOURCE_ASSETS="
            f"{result['sourceAssetCount']} "
            f"GENERATION_RECORDS={result['generationRecordCount']}"
        )
        print(f"TECHNICAL_STATE={result['technicalPacketState']}")
    return 1 if diagnostics else 0


if __name__ == "__main__":
    sys.exit(main())
