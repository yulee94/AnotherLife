#!/usr/bin/env python3
"""Build and validate the held integrated four-realm production taxonomy."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any

import realm_character_taxonomy as realm_contract


CATALOG_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_four_realm_production_taxonomy.json"
)
SCHEMA_PATH = Path(
    "unity/SharedContracts/Schemas/al-four-realm-production-taxonomy.schema.json"
)
REALMS = ("crownlands", "eldergrove", "stonehold", "umbral")
SOURCE_PATHS = {
    realm: Path(
        f"unity/Assets/AL/StreamingAssets/GameData/al_{realm}_realm_character_taxonomy.json"
    )
    for realm in REALMS
}
ENTITY_SECTIONS = {
    "playableRaces": "playable_race",
    "npcArchetypes": "npc",
    "championFamilies": "champion",
    "beastFamilies": "beast",
    "monsterFamilies": "monster",
}
COST_DIMENSIONS = (
    "geometry",
    "materials",
    "textures",
    "bones",
    "physics",
    "animation",
    "vfx",
    "colliders",
    "hitboxes",
)
VISUAL_DECISION_DIMENSIONS = {
    "morphology",
    "culture",
    "silhouette",
    "anatomy",
    "clothing",
    "armor",
}
MOTION_DECISION_DIMENSIONS = {"animation_personality", "magical_grammar"}


class FourRealmTaxonomyValidationError(RuntimeError):
    """Raised when the integrated taxonomy fails closed."""

    def __init__(self, issues: list[str]):
        self.issues = sorted(set(issues))
        super().__init__("\n".join(self.issues))


def _catalog_sha256(catalog: dict[str, Any]) -> str:
    """Hash semantic JSON content independently of checkout formatting."""

    canonical = json.dumps(
        catalog,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def _catalogs(repo_root: Path) -> dict[str, dict[str, Any]]:
    catalogs: dict[str, dict[str, Any]] = {}
    for realm, relative_path in SOURCE_PATHS.items():
        absolute_path = repo_root / relative_path
        realm_contract.validate_path(repo_root, absolute_path)
        catalog = realm_contract.load_json(absolute_path)
        if catalog.get("realmId") != realm:
            raise FourRealmTaxonomyValidationError(
                [f"SourceRealmMismatch: {relative_path} declares {catalog.get('realmId')!r}"]
            )
        catalogs[realm] = catalog
    return catalogs


def _record_index(catalog: dict[str, Any]) -> dict[str, tuple[str, dict[str, Any]]]:
    return {
        row["id"]: (section, row)
        for section in realm_contract.SECTIONS
        for row in catalog[section]
    }


def _entities(catalog: dict[str, Any]):
    for section, entity_kind in ENTITY_SECTIONS.items():
        for row in catalog[section]:
            yield section, entity_kind, row


def _sorted_rows(rows: list[dict[str, Any]], key: str) -> list[dict[str, Any]]:
    return sorted(rows, key=lambda row: str(row[key]).encode("utf-8"))


def _metric_rows(profile: dict[str, Any]) -> list[dict[str, Any]]:
    rows = []
    for dimension in COST_DIMENSIONS:
        for metric_name, metric in sorted(profile[dimension].items()):
            rows.append(
                {
                    "path": f"{dimension}.{metric_name}",
                    "dimension": dimension,
                    "state": metric["state"],
                    "limitKind": metric["limitKind"],
                    "value": metric["value"],
                    "secondaryValue": metric["secondaryValue"],
                    "unit": metric["unit"],
                    "sourceRefs": metric["sourceRefs"],
                    "decisionPacketIds": metric["decisionPacketIds"],
                    "rationale": metric["rationale"],
                }
            )
    return rows


def _source_decision_groups(catalogs: dict[str, dict[str, Any]]) -> dict[str, list[dict[str, Any]]]:
    groups: dict[str, list[dict[str, Any]]] = {
        "identity": [],
        "technical_mobile": [],
        "motion_effect": [],
    }
    for realm in REALMS:
        catalog = catalogs[realm]
        for packet in catalog["decisionPackets"]:
            item = {"realmId": realm, "catalogId": catalog["catalogId"], **packet}
            dimensions = set(packet["decisionDimensions"])
            question = packet["question"].lower()
            if dimensions & VISUAL_DECISION_DIMENSIONS:
                groups["identity"].append(item)
            if (
                any(
                    token in question
                    for token in (
                        "budget",
                        "technical",
                        "rig",
                        "platform",
                        "lod",
                        "collider",
                        "hitbox",
                        "physics",
                        "production realization",
                    )
                )
                or dimensions & {"silhouette", "anatomy"}
            ):
                groups["technical_mobile"].append(item)
            if dimensions & MOTION_DECISION_DIMENSIONS or any(
                token in question for token in ("motion", "effect", "animation", "magical")
            ):
                groups["motion_effect"].append(item)
    return groups


def _owner_packets(catalogs: dict[str, dict[str, Any]]) -> list[dict[str, Any]]:
    groups = _source_decision_groups(catalogs)
    descriptions = {
        "identity": (
            "frt_decision_identity_v001",
            "APPROVE, REVISE, or REJECT the source-bounded realm morphology, culture, "
            "silhouette, anatomy, clothing, and armor proposals without creating new canon.",
        ),
        "technical_mobile": (
            "frt_decision_technical_mobile_v001",
            "APPROVE, REVISE, or REJECT the held modularity, rig, platform-variant, and "
            "measured mobile-budget plans; unknown limits remain gated.",
        ),
        "motion_effect": (
            "frt_decision_motion_effect_v001",
            "APPROVE, REVISE, or REJECT the held animation personality, skill-motion, "
            "telegraph, and VFX grammar proposals while gameplay authority remains external.",
        ),
    }
    packets = []
    for area in ("identity", "technical_mobile", "motion_effect"):
        packet_id, question = descriptions[area]
        sources = groups[area]
        packets.append(
            {
                "id": packet_id,
                "decisionArea": area,
                "question": question,
                "affectedCatalogIds": sorted({row["catalogId"] for row in sources}),
                "affectedSubjectIds": sorted(
                    {subject for row in sources for subject in row["subjectIds"]}
                ),
                "sourceDecisionIds": sorted({row["id"] for row in sources}),
                "alternatives": [
                    {
                        "response": "APPROVE",
                        "consequence": "Approves only this bounded decision area; generation, activation, accessibility, performance, and release gates remain held.",
                    },
                    {
                        "response": "REVISE",
                        "consequence": "Returns all affected source records for revision and keeps every downstream gate held.",
                    },
                    {
                        "response": "REJECT",
                        "consequence": "Rejects the affected proposals without deleting source identity; replacement work requires new evidence and owner review.",
                    },
                ],
                "ownerStatus": "PENDING",
                "approvedResponse": None,
                "ownerResponse": None,
                "decidedAtUtc": None,
            }
        )
    return packets


def _build_taxonomy(repo_root: Path) -> dict[str, Any]:
    """Build the deterministic integrated taxonomy from the four validated sources."""

    catalogs = _catalogs(repo_root)
    indexes = {realm: _record_index(catalog) for realm, catalog in catalogs.items()}

    source_catalogs = []
    for realm in REALMS:
        relative_path = SOURCE_PATHS[realm]
        catalog = catalogs[realm]
        source_catalogs.append(
            {
                "realmId": realm,
                "catalogId": catalog["catalogId"],
                "path": relative_path.as_posix(),
                "sha256": _catalog_sha256(catalog),
                "schemaVersion": catalog["schemaVersion"],
                "contentVersion": catalog["contentVersion"],
                "authorityStatus": catalog["authority"]["status"],
                "generationState": catalog["gatePolicy"]["generationState"],
                "activationState": catalog["gatePolicy"]["activationState"],
                "recordCount": sum(len(catalog[section]) for section in realm_contract.SECTIONS),
                "skillCount": len(catalog["skills"]),
            }
        )

    roster = []
    rigs = []
    motions = []
    platform_rows = []
    skill_motion = []
    skill_vfx = []
    budget_rows = []
    provenance_rows = []

    for realm in REALMS:
        catalog = catalogs[realm]
        index = indexes[realm]
        platforms = {row["id"]: row for row in catalog["platformProfiles"]}
        variants = {row["id"]: row for row in catalog["platformVariants"]}
        budgets = {row["id"]: row for row in catalog["budgetProfiles"]}
        motions_by_subject: dict[str, set[str]] = defaultdict(set)
        for motion in catalog["motions"]:
            for subject_id in motion["subjectIds"]:
                motions_by_subject[subject_id].add(motion["motionKey"])

        for section, entity_kind, entity in _entities(catalog):
            entity_id = entity["id"]
            roster.append(
                {
                    "realmId": realm,
                    "catalogId": catalog["catalogId"],
                    "entityKind": entity_kind,
                    "sourceSection": section,
                    "entityId": entity_id,
                    "displayName": entity["displayName"],
                    "role": entity.get("role"),
                    "rank": entity.get("rank"),
                    "classSourceIds": entity.get("classSourceIds", []),
                    "skillIds": entity.get("skillIds", []),
                }
            )
            rig_records = [index[rig_id][1] for rig_id in entity["rigFamilyIds"]]
            feasible = bool(rig_records) and all(
                rig.get("skeletonFamily")
                and rig.get("rootBone")
                and rig.get("retargetGroup")
                and (
                    rig.get("bindPoseRef")
                    or rig.get("authority", {}).get("decisionPacketIds")
                )
                for rig in rig_records
            )
            bind_pose_refs = sorted(
                {rig.get("bindPoseRef") for rig in rig_records}, key=lambda value: str(value)
            )
            rigs.append(
                {
                    "realmId": realm,
                    "entityId": entity_id,
                    "entityKind": entity_kind,
                    "rigFamilyIds": entity["rigFamilyIds"],
                    "skeletonFamilies": sorted(
                        {rig["skeletonFamily"] for rig in rig_records}
                    ),
                    "bindPoseRefs": bind_pose_refs,
                    "bindPoseDisposition": (
                        "specified"
                        if all(value is not None for value in bind_pose_refs)
                        else "owner_gated"
                    ),
                    "retargetGroups": sorted({rig["retargetGroup"] for rig in rig_records}),
                    "bodyModuleIds": entity["bodyModuleIds"],
                    "equipmentModuleIds": entity["equipmentModuleIds"],
                    "facialSystemIds": entity["facialSystemIds"],
                    "secondaryPhysicsProfileIds": entity["secondaryPhysicsProfileIds"],
                    "feasible": feasible,
                    "disposition": "held_feasible_plan" if feasible else "unriggable",
                }
            )

            if entity_kind != "playable_race":
                required: set[str] = set()
                template_kinds = []
                for template_id in entity["motionMatrixTemplateIds"]:
                    template = index[template_id][1]
                    required.update(template["requiredMotionKeys"])
                    template_kinds.append(template["subjectKind"])
                required.update(
                    f"locomotion.{mode}" for mode in entity.get("locomotionModes", [])
                )
                required.update(entity.get("roleActionKeys", []))
                required.update(entity.get("bossTransitionKeys", []))
                present = motions_by_subject[entity_id]
                motions.append(
                    {
                        "realmId": realm,
                        "entityId": entity_id,
                        "entityKind": entity_kind,
                        "templateSubjectKinds": sorted(set(template_kinds)),
                        "requiredMotionKeys": sorted(required),
                        "presentMotionKeys": sorted(present),
                        "missingMotionKeys": sorted(required - present),
                        "bossTransitionKeys": sorted(entity.get("bossTransitionKeys", [])),
                        "missingBossTransitionKeys": sorted(
                            set(entity.get("bossTransitionKeys", [])) - present
                        ),
                    }
                )

            budget_ids = entity["budgetProfileIds"]
            tier_coverage = {
                platforms[budgets[budget_id]["platformProfileId"]]["tier"]
                for budget_id in budget_ids
            }
            tier_coverage.update(
                platforms[variants[variant_id]["platformProfileId"]]["tier"]
                for variant_id in entity["platformVariantIds"]
            )
            mobile_budget_ids = sorted(
                budget_id
                for budget_id in budget_ids
                if platforms[budgets[budget_id]["platformProfileId"]]["tier"]
                == "mobile_floor"
            )
            cost_treatments = {}
            for dimension in COST_DIMENSIONS:
                cost_treatments[dimension] = sorted(
                    f"{budget_id}:{dimension}.{metric_name}"
                    for budget_id in mobile_budget_ids
                    for metric_name in budgets[budget_id][dimension]
                )
            platform_rows.append(
                {
                    "realmId": realm,
                    "entityId": entity_id,
                    "entityKind": entity_kind,
                    "explicitPlatformVariantIds": entity["platformVariantIds"],
                    "budgetProfileIds": budget_ids,
                    "mobileFloorBudgetProfileIds": mobile_budget_ids,
                    "platformTierCoverage": sorted(tier_coverage),
                    "deliveryMode": (
                        "explicit_platform_variants_and_budgets"
                        if entity["platformVariantIds"]
                        else "budget_profile_only"
                    ),
                    "costDimensionTreatments": cost_treatments,
                }
            )

        traces = {row["skillId"]: row for row in catalog["skillTraceability"]}
        for skill in catalog["skills"]:
            trace = traces[skill["id"]]
            skill_motion.append(
                {
                    "realmId": realm,
                    "skillId": skill["id"],
                    "canonicalSkillKey": skill["externalSourceId"],
                    "subjectIds": skill["subjectIds"],
                    "phases": trace["motionPhases"],
                }
            )
            skill_vfx.append(
                {
                    "realmId": realm,
                    "skillId": skill["id"],
                    "canonicalSkillKey": skill["externalSourceId"],
                    "subjectIds": skill["subjectIds"],
                    "effects": trace["effects"],
                }
            )

        for profile in catalog["budgetProfiles"]:
            budget_rows.append(
                {
                    "realmId": realm,
                    "budgetProfileId": profile["id"],
                    "platformTier": platforms[profile["platformProfileId"]]["tier"],
                    "scopeKinds": profile["scopeKinds"],
                    "metrics": _metric_rows(profile),
                    "allCostDimensionsExplicit": set(COST_DIMENSIONS)
                    == {row["dimension"] for row in _metric_rows(profile)},
                }
            )

        claim_ids: dict[str, set[str]] = defaultdict(set)
        for section in realm_contract.SECTIONS:
            if section == "provenance":
                continue
            for row in catalog[section]:
                if section == "decisionPackets":
                    provenance_ids = row["provenanceIds"]
                else:
                    provenance_ids = row["authority"]["provenanceIds"]
                for provenance_id in provenance_ids:
                    claim_ids[provenance_id].add(row["id"])
        for provenance in catalog["provenance"]:
            provenance_rows.append(
                {
                    "realmId": realm,
                    "provenanceId": provenance["id"],
                    "sourceKind": provenance["sourceKind"],
                    "sourceRef": provenance["sourceRef"],
                    "creator": provenance["creator"],
                    "tool": provenance["tool"],
                    "toolVersion": provenance["toolVersion"],
                    "rightsState": provenance["rightsState"],
                    "sha256": provenance["sha256"],
                    "claimIds": sorted(claim_ids[provenance["id"]]),
                }
            )

    aliases: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in skill_motion:
        aliases[row["canonicalSkillKey"]].append(
            {"realmId": row["realmId"], "recordId": row["skillId"]}
        )

    records_by_id: dict[str, list[tuple[str, str, dict[str, Any]]]] = defaultdict(list)
    for realm in REALMS:
        for section in realm_contract.SECTIONS:
            for row in catalogs[realm][section]:
                records_by_id[row["id"]].append((realm, section, row))
    duplicate_audit = []
    semantic_fields = (
        "subjectKind",
        "requiredMotionKeys",
        "requiredSkillPhases",
        "requiredEffectCategories",
    )
    for record_id, occurrences in sorted(records_by_id.items()):
        if len(occurrences) < 2:
            continue
        comparable = {
            json.dumps(
                {field: row.get(field) for field in semantic_fields},
                sort_keys=True,
                separators=(",", ":"),
            )
            for _, _, row in occurrences
        }
        classification = (
            "compatible_contract_alias" if len(comparable) == 1 else "incompatible"
        )
        kind = occurrences[0][2].get("subjectKind", "record")
        duplicate_audit.append(
            {
                "sourceId": record_id,
                "normalizedId": f"frt_shared_motion_matrix_{kind}_complete_v001",
                "realms": sorted({realm for realm, _, _ in occurrences}),
                "sourceSections": sorted({section for _, section, _ in occurrences}),
                "classification": classification,
                "semanticBasis": list(semantic_fields),
                "resolution": "One canonical contract meaning; provenance and display metadata remain realm-local.",
            }
        )

    all_ids = {
        section: sorted(
            row["id"]
            for realm in REALMS
            for row in catalogs[realm][section]
        )
        for section in (
            "bodyModules",
            "equipmentModules",
            "rigFamilies",
            "motionMatrixTemplates",
        )
    }
    equipment_slots = sorted(
        {
            row["slot"]
            for realm in REALMS
            for row in catalogs[realm]["equipmentModules"]
        }
    )
    shared_external_skills = sorted(
        key for key, rows in aliases.items() if len({row["realmId"] for row in rows}) > 1
    )
    sharing_matrix = [
        {
            "assetClass": "body_modules",
            "sharingScope": "realm_scoped_no_cross_realm_compatibility_claim",
            "sourceIds": all_ids["bodyModules"],
            "rationale": "Source catalogs authorize realm-scoped modular bodies only; empty lists remain explicit and do not imply hidden sharing.",
        },
        {
            "assetClass": "equipment_slots",
            "sharingScope": "realm_scoped_no_cross_realm_compatibility_claim",
            "sourceIds": [f"slot:{slot}" for slot in equipment_slots],
            "rationale": "Equipment records and socket assumptions stay realm-scoped until owner and technical review approve compatibility.",
        },
        {
            "assetClass": "skeletons_rigs",
            "sharingScope": "realm_scoped_no_cross_realm_compatibility_claim",
            "sourceIds": all_ids["rigFamilies"],
            "rationale": "Similar humanoid labels do not prove a shared bind pose, skeleton, or retarget package across realms.",
        },
        {
            "assetClass": "animations",
            "sharingScope": "contract_only",
            "sourceIds": all_ids["motionMatrixTemplates"],
            "rationale": "Required motion keys are shared contract terminology; animation clips and personality remain realm-scoped and held.",
        },
        {
            "assetClass": "skill_identities",
            "sharingScope": "gameplay_identity_only",
            "sourceIds": shared_external_skills,
            "rationale": "Approved gameplay skill identities normalize by externalSourceId; motion and effect realization does not automatically transfer.",
        },
        {
            "assetClass": "vfx",
            "sharingScope": "realm_scoped_no_cross_realm_compatibility_claim",
            "sourceIds": list(realm_contract.VFX_CATEGORIES),
            "rationale": "VFX category names are shared audit vocabulary, not authorization to share particles, materials, magical grammar, or budgets.",
        },
    ]

    owner_packets = _owner_packets(catalogs)
    taxonomy = {
        "gameId": "another-life",
        "taxonomyId": "frt_four_realm_production_taxonomy_v001",
        "schemaVersion": 1,
        "contentVersion": "1.0.0",
        "authority": {
            "status": "preparation_held",
            "finalCreativeOwner": "project_owner",
            "generationState": "held",
            "activationState": "held",
            "releaseState": "held",
            "runtimeAuthority": False,
        },
        "normalization": {
            "canonicalRealmIds": list(REALMS),
            "canonicalEntityKinds": list(ENTITY_SECTIONS.values()),
            "stableIdPolicy": "Preserve source record IDs; use realmId plus entityKind plus source ID as the cross-realm compound key.",
            "terminologyMappings": [
                {
                    "sourceTerm": source,
                    "canonicalTerm": target,
                    "rationale": "Normalizes section vocabulary without renaming source records.",
                }
                for source, target in ENTITY_SECTIONS.items()
            ],
            "skillAliases": [
                {
                    "canonicalSkillKey": key,
                    "realmRecordIds": sorted(
                        aliases[key], key=lambda row: (row["realmId"], row["recordId"])
                    ),
                }
                for key in sorted(aliases)
            ],
        },
        "sourceCatalogs": source_catalogs,
        "matrices": {
            "rosterCoverage": sorted(
                roster, key=lambda row: (row["realmId"], row["entityKind"], row["entityId"])
            ),
            "rigFeasibility": sorted(
                rigs, key=lambda row: (row["realmId"], row["entityId"])
            ),
            "motionCoverage": sorted(
                motions, key=lambda row: (row["realmId"], row["entityId"])
            ),
            "skillToMotion": sorted(
                skill_motion, key=lambda row: (row["realmId"], row["skillId"])
            ),
            "skillToVfx": sorted(
                skill_vfx, key=lambda row: (row["realmId"], row["skillId"])
            ),
            "platformVariants": sorted(
                platform_rows, key=lambda row: (row["realmId"], row["entityId"])
            ),
            "budgets": sorted(
                budget_rows, key=lambda row: (row["realmId"], row["budgetProfileId"])
            ),
            "provenance": sorted(
                provenance_rows, key=lambda row: (row["realmId"], row["provenanceId"])
            ),
        },
        "sharingMatrix": sharing_matrix,
        "duplicateAudit": duplicate_audit,
        "ownerDecisionPackets": owner_packets,
        "acceptance": {
            "orphanSkillCount": 0,
            "missingRequiredCellCount": sum(
                len(row["missingMotionKeys"]) + len(row["missingBossTransitionKeys"])
                for row in motions
            ),
            "unriggableConceptCount": sum(not row["feasible"] for row in rigs),
            "undocumentedProvenanceCount": 0,
            "unbudgetedMobileCostCount": sum(
                not row["mobileFloorBudgetProfileIds"]
                or any(not row["costDimensionTreatments"][key] for key in COST_DIMENSIONS)
                for row in platform_rows
            ),
            "incompatibleDuplicateCount": sum(
                row["classification"] == "incompatible" for row in duplicate_audit
            ),
            "unresolvedOwnerDecisionPacketCount": sum(
                row["ownerStatus"] == "PENDING" for row in owner_packets
            ),
        },
    }
    return taxonomy


def build_taxonomy(repo_root: Path) -> dict[str, Any]:
    """Build the canonical source-derived projection."""

    return _build_taxonomy(repo_root)


def validate_taxonomy(repo_root: Path, taxonomy: dict[str, Any]) -> dict[str, int]:
    """Validate integrated evidence against the live source catalogs."""

    issues: list[str] = []
    catalogs = _catalogs(repo_root)
    source_skill_ids = {
        skill["id"] for realm in REALMS for skill in catalogs[realm]["skills"]
    }
    source_entity_ids = {
        entity["id"]
        for realm in REALMS
        for _, _, entity in _entities(catalogs[realm])
    }
    source_motion_entity_ids = {
        entity["id"]
        for realm in REALMS
        for _, kind, entity in _entities(catalogs[realm])
        if kind != "playable_race"
    }
    source_budget_ids = {
        row["id"] for realm in REALMS for row in catalogs[realm]["budgetProfiles"]
    }
    source_provenance_ids = {
        row["id"] for realm in REALMS for row in catalogs[realm]["provenance"]
    }

    if taxonomy.get("gameId") != "another-life" or taxonomy.get("taxonomyId") != "frt_four_realm_production_taxonomy_v001":
        issues.append("MalformedIdentity: integrated taxonomy identity drift")
    authority = taxonomy.get("authority", {})
    if any(
        authority.get(key) != "held"
        for key in ("generationState", "activationState", "releaseState")
    ) or authority.get("status") != "preparation_held" or authority.get("runtimeAuthority") is not False:
        issues.append("GateConflict: integrated taxonomy must remain preparation-held")

    source_rows = taxonomy.get("sourceCatalogs", [])
    source_by_realm = {row.get("realmId"): row for row in source_rows if isinstance(row, dict)}
    if set(source_by_realm) != set(REALMS):
        issues.append("UndocumentedProvenance: every realm source catalog must be recorded")
    for realm in REALMS:
        row = source_by_realm.get(realm, {})
        path = SOURCE_PATHS[realm]
        if (
            row.get("catalogId") != catalogs[realm]["catalogId"]
            or row.get("path") != path.as_posix()
            or row.get("sha256") != _catalog_sha256(catalogs[realm])
        ):
            issues.append(f"UndocumentedProvenance: {realm} source identity or hash drift")

    matrices = taxonomy.get("matrices", {})
    roster = matrices.get("rosterCoverage", [])
    roster_ids = [row.get("entityId") for row in roster if isinstance(row, dict)]
    if len(roster_ids) != len(set(roster_ids)) or set(roster_ids) != source_entity_ids:
        issues.append("RosterCoverageMismatch: roster matrix must exactly cover source entities")

    rig_rows = matrices.get("rigFeasibility", [])
    rig_ids = [row.get("entityId") for row in rig_rows if isinstance(row, dict)]
    if len(rig_ids) != len(set(rig_ids)) or set(rig_ids) != source_entity_ids:
        issues.append("UnriggableConcept: rig matrix must exactly cover source entities")
    for row in rig_rows:
        if (
            not row.get("rigFamilyIds")
            or not row.get("skeletonFamilies")
            or not isinstance(row.get("bindPoseRefs"), list)
            or row.get("bindPoseDisposition") not in {"specified", "owner_gated"}
            or not row.get("retargetGroups")
            or row.get("feasible") is not True
            or row.get("disposition") != "held_feasible_plan"
        ):
            issues.append(f"UnriggableConcept: {row.get('entityId')}")

    motion_rows = matrices.get("motionCoverage", [])
    motion_ids = [row.get("entityId") for row in motion_rows if isinstance(row, dict)]
    if len(motion_ids) != len(set(motion_ids)) or set(motion_ids) != source_motion_entity_ids:
        issues.append("MissingRequiredMotion: motion matrix must exactly cover animated entities")
    for row in motion_rows:
        if row.get("missingMotionKeys") or row.get("missingBossTransitionKeys"):
            issues.append(f"MissingRequiredMotion: {row.get('entityId')}")

    skill_motion = matrices.get("skillToMotion", [])
    skill_vfx = matrices.get("skillToVfx", [])
    motion_skill_ids = [row.get("skillId") for row in skill_motion if isinstance(row, dict)]
    vfx_skill_ids = [row.get("skillId") for row in skill_vfx if isinstance(row, dict)]
    if len(motion_skill_ids) != len(set(motion_skill_ids)) or set(motion_skill_ids) != source_skill_ids:
        issues.append("OrphanSkill: skill-to-motion matrix must exactly cover source skills")
    if len(vfx_skill_ids) != len(set(vfx_skill_ids)) or set(vfx_skill_ids) != source_skill_ids:
        issues.append("OrphanSkill: skill-to-VFX matrix must exactly cover source skills")
    missing_required_cells = 0
    for row in skill_motion:
        phases = row.get("phases", {})
        for phase in realm_contract.SKILL_PHASES:
            cell = phases.get(phase)
            if not isinstance(cell, dict):
                issues.append(f"MissingSkillMotionCell: {row.get('skillId')}.{phase}")
                missing_required_cells += 1
                continue
            if cell.get("state") == "required" and not cell.get("recordIds"):
                issues.append(f"MissingSkillMotionCell: {row.get('skillId')}.{phase}")
                missing_required_cells += 1
            if cell.get("state") != "required" and (
                cell.get("recordIds") or not str(cell.get("rationale", "")).strip()
            ):
                issues.append(f"MissingSkillMotionCell: {row.get('skillId')}.{phase}")
                missing_required_cells += 1
    for row in skill_vfx:
        effects = row.get("effects", {})
        for category in realm_contract.VFX_CATEGORIES:
            cell = effects.get(category)
            if not isinstance(cell, dict):
                issues.append(f"MissingSkillVfxCell: {row.get('skillId')}.{category}")
                missing_required_cells += 1
                continue
            if cell.get("state") == "required" and not cell.get("recordIds"):
                issues.append(f"MissingSkillVfxCell: {row.get('skillId')}.{category}")
                missing_required_cells += 1
            if cell.get("state") != "required" and (
                cell.get("recordIds") or not str(cell.get("rationale", "")).strip()
            ):
                issues.append(f"MissingSkillVfxCell: {row.get('skillId')}.{category}")
                missing_required_cells += 1

    platform_rows = matrices.get("platformVariants", [])
    platform_ids = [row.get("entityId") for row in platform_rows if isinstance(row, dict)]
    if len(platform_ids) != len(set(platform_ids)) or set(platform_ids) != source_entity_ids:
        issues.append("UnbudgetedMobileCost: platform matrix must exactly cover source entities")
    unbudgeted = 0
    for row in platform_rows:
        treatments = row.get("costDimensionTreatments", {})
        if not row.get("mobileFloorBudgetProfileIds") or any(
            not treatments.get(dimension) for dimension in COST_DIMENSIONS
        ):
            issues.append(f"UnbudgetedMobileCost: {row.get('entityId')}")
            unbudgeted += 1

    budgets = matrices.get("budgets", [])
    matrix_budget_ids = [row.get("budgetProfileId") for row in budgets if isinstance(row, dict)]
    if len(matrix_budget_ids) != len(set(matrix_budget_ids)) or set(matrix_budget_ids) != source_budget_ids:
        issues.append("UnbudgetedMobileCost: budget matrix must exactly cover source profiles")
    for row in budgets:
        dimensions = {metric.get("dimension") for metric in row.get("metrics", [])}
        if dimensions != set(COST_DIMENSIONS) or row.get("allCostDimensionsExplicit") is not True:
            issues.append(f"UnbudgetedMobileCost: {row.get('budgetProfileId')}")

    provenance = matrices.get("provenance", [])
    matrix_provenance_ids = [row.get("provenanceId") for row in provenance if isinstance(row, dict)]
    undocumented = 0
    if len(matrix_provenance_ids) != len(set(matrix_provenance_ids)) or set(matrix_provenance_ids) != source_provenance_ids:
        issues.append("UndocumentedProvenance: provenance matrix must exactly cover source records")
        undocumented += 1
    for row in provenance:
        if not str(row.get("sourceRef", "")).strip():
            issues.append(f"UndocumentedProvenance: {row.get('provenanceId')}")
            undocumented += 1

    expected_duplicates: dict[str, list[tuple[str, dict[str, Any]]]] = defaultdict(list)
    for realm in REALMS:
        for section in realm_contract.SECTIONS:
            for row in catalogs[realm][section]:
                expected_duplicates[row["id"]].append((realm, row))
    expected_duplicate_ids = {
        record_id for record_id, rows in expected_duplicates.items() if len(rows) > 1
    }
    duplicate_rows = taxonomy.get("duplicateAudit", [])
    audited_ids = [row.get("sourceId") for row in duplicate_rows if isinstance(row, dict)]
    if len(audited_ids) != len(set(audited_ids)) or set(audited_ids) != expected_duplicate_ids:
        issues.append("DuplicateAuditMismatch: every cross-realm duplicate must be classified")
    incompatible = 0
    for row in duplicate_rows:
        if row.get("classification") == "incompatible":
            issues.append(f"IncompatibleDuplicate: {row.get('sourceId')}")
            incompatible += 1
        elif row.get("classification") != "compatible_contract_alias":
            issues.append(f"DuplicateAuditMismatch: {row.get('sourceId')}")

    packets = taxonomy.get("ownerDecisionPackets", [])
    areas = [row.get("decisionArea") for row in packets if isinstance(row, dict)]
    unresolved_source_ids = {
        row["id"]
        for realm in REALMS
        for row in catalogs[realm]["decisionPackets"]
        if row["ownerStatus"] in {"PENDING", "REVISE"}
    }
    mapped_source_ids = {
        source_id for row in packets for source_id in row.get("sourceDecisionIds", [])
    }
    if set(areas) != {"identity", "technical_mobile", "motion_effect"} or len(areas) != 3:
        issues.append("OwnerDecisionMissing: exactly three deduplicated decision areas are required")
    if not unresolved_source_ids.issubset(mapped_source_ids):
        issues.append("OwnerDecisionMissing: unresolved source packets are not fully mapped")
    for row in packets:
        if (
            row.get("ownerStatus") != "PENDING"
            or [item.get("response") for item in row.get("alternatives", [])]
            != ["APPROVE", "REVISE", "REJECT"]
            or not row.get("affectedCatalogIds")
            or not row.get("affectedSubjectIds")
            or not row.get("sourceDecisionIds")
        ):
            issues.append(f"OwnerDecisionMissing: {row.get('id')}")

    acceptance = taxonomy.get("acceptance", {})
    calculated = {
        "orphanSkillCount": int(
            set(motion_skill_ids) != source_skill_ids
            or set(vfx_skill_ids) != source_skill_ids
            or len(motion_skill_ids) != len(set(motion_skill_ids))
            or len(vfx_skill_ids) != len(set(vfx_skill_ids))
        ),
        "missingRequiredCellCount": missing_required_cells
        + sum(
            len(row.get("missingMotionKeys", []))
            + len(row.get("missingBossTransitionKeys", []))
            for row in motion_rows
        ),
        "unriggableConceptCount": sum(
            row.get("feasible") is not True for row in rig_rows
        ),
        "undocumentedProvenanceCount": undocumented,
        "unbudgetedMobileCostCount": unbudgeted,
        "incompatibleDuplicateCount": incompatible,
        "unresolvedOwnerDecisionPacketCount": sum(
            row.get("ownerStatus") == "PENDING" for row in packets
        ),
    }
    for key, value in calculated.items():
        if acceptance.get(key) != value:
            issues.append(f"AcceptanceMismatch: {key} expected {value}, got {acceptance.get(key)!r}")

    if taxonomy != _build_taxonomy(repo_root):
        issues.append(
            "ProjectionMismatch: integrated claims must exactly match the deterministic source-derived projection"
        )

    if issues:
        raise FourRealmTaxonomyValidationError(issues)
    return {
        "rosterRows": len(roster),
        "rigRows": len(rig_rows),
        "motionRows": len(motion_rows),
        "skillMotionRows": len(skill_motion),
        "skillVfxRows": len(skill_vfx),
        "platformRows": len(platform_rows),
        "budgetRows": len(budgets),
        "provenanceRows": len(provenance),
        **calculated,
    }


def render_taxonomy(repo_root: Path) -> str:
    return json.dumps(
        build_taxonomy(repo_root),
        ensure_ascii=False,
        indent=2,
        sort_keys=False,
    ) + "\n"


def validate_path(repo_root: Path, catalog_path: Path | None = None) -> dict[str, int]:
    path = catalog_path or repo_root / CATALOG_PATH
    taxonomy = json.loads(path.read_text(encoding="utf-8"))
    try:
        from jsonschema import Draft202012Validator, FormatChecker
    except ImportError as error:
        raise FourRealmTaxonomyValidationError(
            ["DependencyMissing: install jsonschema"]
        ) from error
    schema = realm_contract.load_json(repo_root / SCHEMA_PATH)
    errors = sorted(
        Draft202012Validator(schema, format_checker=FormatChecker()).iter_errors(taxonomy),
        key=lambda error: list(error.absolute_path),
    )
    if errors:
        error = errors[0]
        location = ".".join(str(part) for part in error.absolute_path) or "<root>"
        raise FourRealmTaxonomyValidationError(
            [f"SchemaViolation: {location}: {error.message}"]
        )
    return validate_taxonomy(repo_root, taxonomy)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root", type=Path, default=Path(__file__).resolve().parents[3]
    )
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    rendered = render_taxonomy(args.repo_root)
    output_path = args.repo_root / CATALOG_PATH
    if args.check:
        if not output_path.is_file() or output_path.read_text(encoding="utf-8") != rendered:
            print(f"FAIL: generated taxonomy differs from {output_path}", file=sys.stderr)
            return 1
    else:
        output_path.write_text(rendered, encoding="utf-8", newline="\n")
    evidence = validate_path(args.repo_root, output_path)
    print("PASS: integrated four-realm production taxonomy validates")
    print(json.dumps(evidence, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
