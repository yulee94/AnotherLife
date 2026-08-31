#!/usr/bin/env python3
"""Fail-closed tests for the realm character/creature taxonomy contract."""

from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

import realm_character_taxonomy as contract
import build_eldergrove_realm_catalog as eldergrove_builder


PROVENANCE_ID = "rct_shared_provenance_fixture_source_v001"
DECISION_ID = "rct_shared_decision_fixture_budget_v001"
PLATFORM_ID = "rct_shared_platform_mobile_floor_v001"
BUDGET_ID = "rct_shared_budget_champion_mobile_floor_v001"
RIG_ID = "rct_shared_rig_humanoid_fixture_v001"
CHAMPION_ID = "rct_stonehold_champion_fixture_v001"
SKILL_ID = "rct_stonehold_skill_fixture_v001"
ELDERGROVE_CATALOG_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_eldergrove_realm_character_taxonomy.json"
)
STONEHOLD_CATALOG_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_stonehold_realm_character_taxonomy.json"
)
ELDERGROVE_CLASS_SOURCE_IDS = {
    "ClassFamily.Assassin",
    "ClassFamily.Mage",
    "ClassFamily.Ranger",
    "ClassFamily.Warrior",
}
ELDERGROVE_EXTERNAL_SKILL_IDS = {
    "realm_strike",
    "renewing_guard",
    "skill_arcane_bolt",
    "skill_verdant_nova",
    "tdf_boss_eldergrove_mere_root_leviathan:jaw_led_lunge",
    "tdf_elite_eldergrove_hollowbark_stalker:sudden_low_pounce",
    "tdf_elite_eldergrove_mirrorfin_lurker:jaw_scoop",
    "tdf_elite_eldergrove_sunmane_thornstag:bounding_charge",
    "warmaster_breaker",
    "warzone_burst",
}


def approved_authority() -> dict:
    return {
        "status": "approved_fact",
        "ownerStatus": "APPROVE",
        "provenanceIds": [PROVENANCE_ID],
        "decisionPacketIds": [],
        "approvalEvidenceRefs": ["test-fixture-only"],
    }


def gated_authority() -> dict:
    return {
        "status": "owner_decision_required",
        "ownerStatus": "PENDING",
        "provenanceIds": [PROVENANCE_ID],
        "decisionPacketIds": [DECISION_ID],
        "approvalEvidenceRefs": [],
    }


def unknown_metric(unit: str) -> dict:
    return {
        "state": "owner_decision_required",
        "limitKind": "owner_decision_required",
        "value": None,
        "secondaryValue": None,
        "unit": unit,
        "sourceRefs": [],
        "decisionPacketIds": [DECISION_ID],
        "rationale": "Fixture proves unknown limits remain owner decisions.",
    }


def held_gate_evidence() -> dict:
    return {
        name: {
            "state": "held",
            "reviewer": None,
            "decidedAtUtc": None,
            "evidenceRefs": [],
            "openIssues": ["Synthetic fixture remains held."],
        }
        for name in contract.POSITIVE_GATE_STATES
    }


def passed_gate_evidence() -> dict:
    return {
        name: {
            "state": state,
            "reviewer": "fixture-reviewer",
            "decidedAtUtc": "2026-08-31T00:00:00Z",
            "evidenceRefs": [f"fixture:{name}:evidence"],
            "openIssues": [],
        }
        for name, state in contract.POSITIVE_GATE_STATES.items()
    }


def approved_creative_decisions() -> dict:
    return {
        key: {
            "state": "approved",
            "summary": "Synthetic fixture only; not production authority.",
            "sourceRefs": ["test-fixture-only"],
            "decisionPacketIds": [],
        }
        for key in (
            "morphology",
            "culture",
            "silhouette",
            "anatomy",
            "clothing",
            "armor",
            "animationPersonality",
            "magicalGrammar",
        )
    }


def budget_profile() -> dict:
    return {
        "id": BUDGET_ID,
        "displayName": "Fixture Champion Mobile Floor Budget",
        "authority": gated_authority(),
        "platformProfileId": PLATFORM_ID,
        "scopeKinds": ["champion"],
        "geometry": {
            "lod0Triangles": unknown_metric("triangles"),
            "lod1ReductionPercent": unknown_metric("percent"),
            "lod2ReductionPercent": unknown_metric("percent"),
            "lod3ReductionPercent": unknown_metric("percent"),
        },
        "materials": {
            "materialSlots": unknown_metric("count"),
            "shaderPasses": unknown_metric("count"),
        },
        "textures": {
            "textureLongEdge": unknown_metric("pixels"),
            "residentTextureMemory": unknown_metric("mib"),
        },
        "bones": {
            "deformingBones": unknown_metric("count"),
            "influencesPerVertex": unknown_metric("count"),
        },
        "physics": {
            "simulatedBones": unknown_metric("count"),
            "clothVertices": unknown_metric("count"),
            "activeRigidbodies": unknown_metric("count"),
        },
        "animation": {
            "compressedMemoryTarget": unknown_metric("mib"),
            "compressedMemoryMaximum": unknown_metric("mib"),
            "clipCount": unknown_metric("count"),
            "runtimeAnimatorLayers": unknown_metric("layers"),
        },
        "vfx": {
            "liveParticles": unknown_metric("count"),
            "transparentLayers": unknown_metric("layers"),
            "overdrawCoveragePercent": unknown_metric("percent"),
            "concurrentEffects": unknown_metric("count"),
            "dynamicLights": unknown_metric("lights"),
        },
        "colliders": {
            "primitiveColliders": unknown_metric("count"),
            "proxyTriangles": unknown_metric("triangles"),
        },
        "hitboxes": {"activeHitboxes": unknown_metric("count")},
    }


def motion_id(key: str) -> str:
    return "rct_stonehold_motion_fixture_" + key.replace(".", "_") + "_v001"


def vfx_id(category: str) -> str:
    return f"rct_stonehold_vfx_fixture_{category}_v001"


def build_catalog() -> dict:
    template_ids = {
        kind: f"rct_shared_motion_matrix_{kind}_complete_v001"
        for kind in contract.MOTION_TEMPLATE_REQUIREMENTS
    }
    templates = [
        {
            "id": template_ids[kind],
            "displayName": f"Fixture {kind.title()} Complete Motion Matrix",
            "authority": approved_authority(),
            "subjectKind": kind,
            "requiredMotionKeys": sorted(contract.MOTION_TEMPLATE_REQUIREMENTS[kind]),
            "requiredSkillPhases": contract.SKILL_PHASES,
            "requiredEffectCategories": contract.VFX_CATEGORIES,
            "roleSpecificPolicy": "Realm catalogs add explicit role and habitat motions without removing this floor.",
        }
        for kind in sorted(contract.MOTION_TEMPLATE_REQUIREMENTS)
    ]
    motions = []
    for key in sorted(contract.CHAMPION_MOTIONS):
        motions.append(
            {
                "id": motion_id(key),
                "displayName": f"Fixture {key}",
                "authority": approved_authority(),
                "subjectIds": [CHAMPION_ID],
                "skillId": None,
                "motionKey": key,
                "skillPhase": None,
                "rigFamilyId": RIG_ID,
                "clipRef": f"fixture:{key}",
                "rootMotionMode": "in_place",
                "timingAuthority": "presentation_only",
                "eventMarkers": [],
            }
        )
    phase_motion_ids = {}
    for phase in contract.SKILL_PHASES:
        record_id = motion_id(f"skill.{phase}")
        phase_motion_ids[phase] = record_id
        motions.append(
            {
                "id": record_id,
                "displayName": f"Fixture Skill {phase}",
                "authority": approved_authority(),
                "subjectIds": [CHAMPION_ID],
                "skillId": SKILL_ID,
                "motionKey": f"skill.{phase}",
                "skillPhase": phase,
                "rigFamilyId": RIG_ID,
                "clipRef": f"fixture:skill:{phase}",
                "rootMotionMode": "gameplay_driven",
                "timingAuthority": "gameplay_committed",
                "eventMarkers": ["complete"],
            }
        )
    effects = [
        {
            "id": vfx_id(category),
            "displayName": f"Fixture {category.title()} Effect",
            "authority": approved_authority(),
            "category": category,
            "subjectIds": [CHAMPION_ID],
            "skillIds": [SKILL_ID],
            "source": "Fixture source",
            "direction": "Fixture direction",
            "timing": "Gameplay-committed timing",
            "area": "Fixture area",
            "endState": "Explicit cleanup",
            "gameplayAuthorityRef": "fixture:gameplay-authority",
            "qualityVariants": {
                "off": "Physical cue remains",
                "low": "Protected cue only",
                "balanced": "Measured default",
                "high": "Measured enhancement",
            },
            "reducedMotionVariant": "Static protected cue",
            "offStateCue": "Shape and material cue",
            "budgetProfileIds": [BUDGET_ID],
        }
        for category in contract.VFX_CATEGORIES
    ]
    trace = {
        "skillId": SKILL_ID,
        "motionPhases": {
            phase: {
                "state": "required",
                "recordIds": [phase_motion_ids[phase]],
                "rationale": "Every fixture skill phase is explicitly traced.",
            }
            for phase in contract.SKILL_PHASES
        },
        "effects": {
            category: {
                "state": "required",
                "recordIds": [vfx_id(category)],
                "rationale": "Every fixture effect category is explicitly traced.",
            }
            for category in contract.VFX_CATEGORIES
        },
        "audioSyncRefs": ["fixture:audio-sync"],
        "cameraSyncRefs": ["fixture:camera-sync"],
        "accessibilityEvidenceRefs": ["fixture:accessibility"],
    }
    sections = {
        "provenance": [
            {
                "id": PROVENANCE_ID,
                "sourceKind": "repo_document",
                "sourceRef": "unity/SharedContracts/Tests/test_realm_character_taxonomy.py",
                "creator": "Another Life test fixture",
                "tool": "human-authored test",
                "toolVersion": "1",
                "createdAtUtc": None,
                "rightsState": "project_internal",
                "promptOrBriefRef": None,
                "sha256": None,
                "notes": "Synthetic validation data; not creative or runtime authority.",
            }
        ],
        "decisionPackets": [
            {
                "id": DECISION_ID,
                "displayName": "Fixture Budget Decision",
                "subjectIds": [BUDGET_ID],
                "decisionDimensions": ["animation_personality"],
                "question": "Which measured budget should replace this fixture unknown?",
                "provenanceIds": [PROVENANCE_ID],
                "alternatives": [
                    {
                        "alternativeId": "measure_mobile_floor",
                        "summary": "Measure the physical mobile floor.",
                        "evidenceRefs": ["fixture:measurement-plan"],
                        "risks": [],
                    },
                    {
                        "alternativeId": "retain_hold",
                        "summary": "Retain the production hold.",
                        "evidenceRefs": ["fixture:hold-policy"],
                        "risks": ["No production admission."],
                    },
                ],
                "downstreamImpacts": [
                    {"discipline": "performance", "impact": "Controls admission limits."}
                ],
                "ownerStatus": "PENDING",
                "approvedAlternativeId": None,
                "ownerResponse": None,
                "decidedAtUtc": None,
            }
        ],
        "platformProfiles": [
            {
                "id": PLATFORM_ID,
                "displayName": "Fixture Mobile Floor",
                "authority": approved_authority(),
                "tier": "mobile_floor",
                "targetFps": 30,
                "hardwareFloor": "Synthetic fixture only",
                "qualityIntent": "Validation fixture; not a product decision.",
            }
        ],
        "budgetProfiles": [budget_profile()],
        "motionMatrixTemplates": templates,
        "playableRaces": [],
        "npcArchetypes": [],
        "championFamilies": [
            {
                "id": CHAMPION_ID,
                "displayName": "Fixture Champion",
                "authority": approved_authority(),
                "creativeDecisions": approved_creative_decisions(),
                "bodyModuleIds": [],
                "equipmentModuleIds": [],
                "rigFamilyIds": [RIG_ID],
                "facialSystemIds": [],
                "secondaryPhysicsProfileIds": [],
                "lodProfileIds": [],
                "colliderProfileIds": [],
                "hitboxProfileIds": [],
                "platformVariantIds": [],
                "budgetProfileIds": [BUDGET_ID],
                "motionMatrixTemplateIds": [template_ids["champion"]],
                "playableRaceIds": [],
                "classSourceIds": ["fixture:class"],
                "skillIds": [SKILL_ID],
                "weaponFamilyIds": [],
            }
        ],
        "beastFamilies": [],
        "monsterFamilies": [],
        "bodyModules": [],
        "equipmentModules": [],
        "rigFamilies": [
            {
                "id": RIG_ID,
                "displayName": "Fixture Humanoid Rig",
                "authority": approved_authority(),
                "skeletonFamily": "fixture_humanoid",
                "bindPoseRef": "fixture:bind-pose",
                "rootBone": "root",
                "rootMotionPolicy": "mixed_by_motion",
                "deformingBoneCount": None,
                "socketIds": [],
                "retargetGroup": "fixture_humanoid",
                "budgetProfileIds": [BUDGET_ID],
            }
        ],
        "facialSystems": [],
        "secondaryPhysicsProfiles": [],
        "lodProfiles": [],
        "colliderProfiles": [],
        "hitboxProfiles": [],
        "platformVariants": [],
        "skills": [
            {
                "id": SKILL_ID,
                "displayName": "Fixture Skill",
                "authority": approved_authority(),
                "externalSourceId": "fixture_skill",
                "sourceCatalogRef": "fixture:skills",
                "subjectIds": [CHAMPION_ID],
                "timingAuthorityRef": "fixture:timing",
                "resultAuthorityRef": "fixture:result",
            }
        ],
        "motions": sorted(motions, key=lambda row: row["id"].encode("utf-8")),
        "vfxEffects": sorted(effects, key=lambda row: row["id"].encode("utf-8")),
        "skillTraceability": [trace],
    }
    return {
        "gameId": "another-life",
        "catalogId": "rct_stonehold_catalog_fixture_v001",
        "schemaVersion": 1,
        "contentVersion": "0.0.1",
        "realmId": "stonehold",
        "idFormat": "rct_scope_kind_slug_vNNN",
        "authority": {
            "catalogOwner": "fixture",
            "finalCreativeOwner": "project_owner",
            "ownerDecisionRef": "test-fixture-only",
            "status": "preparation_held",
        },
        "gatePolicy": {
            "generationState": "held",
            "activationState": "held",
            "requiredGateIds": contract.GATE_IDS,
            "gateEvidence": held_gate_evidence(),
        },
        **sections,
    }


class RealmCharacterTaxonomyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repo_root = Path(__file__).resolve().parents[3]
        cls.schema = json.loads((cls.repo_root / contract.SCHEMA_PATH).read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.schema_validator = Draft202012Validator(cls.schema, format_checker=FormatChecker())

    def assert_semantic_failure(self, catalog: dict, token: str) -> None:
        with self.assertRaises(contract.RealmTaxonomyValidationError) as caught:
            contract.validate_catalog(catalog)
        self.assertIn(token, str(caught.exception))

    def test_fixture_passes_schema_and_semantics(self) -> None:
        catalog = build_catalog()
        self.assertEqual([], list(self.schema_validator.iter_errors(catalog)))
        evidence = contract.validate_catalog(catalog)
        self.assertEqual(1, evidence["skillCount"])
        self.assertEqual(0, evidence["orphanReferenceCount"])

    def test_eldergrove_production_catalog_is_complete_and_held(self) -> None:
        catalog_path = self.repo_root / ELDERGROVE_CATALOG_PATH
        self.assertEqual(
            eldergrove_builder.render_catalog(self.repo_root),
            catalog_path.read_text(encoding="utf-8"),
        )
        catalog = contract.load_json(catalog_path)
        stonehold_catalog = contract.load_json(
            self.repo_root / STONEHOLD_CATALOG_PATH
        )
        approved_progression_ids = {
            skill["externalSourceId"]
            for skill in stonehold_catalog["skills"]
            if skill["externalSourceId"].startswith(
                "anotherlife.class_progression."
            )
        }
        self.assertEqual(96, len(approved_progression_ids))
        self.assertEqual([], list(self.schema_validator.iter_errors(catalog)))
        evidence = contract.validate_catalog(catalog)

        self.assertEqual("eldergrove", catalog["realmId"])
        self.assertEqual("preparation_held", catalog["authority"]["status"])
        self.assertEqual("held", catalog["gatePolicy"]["generationState"])
        self.assertEqual("held", catalog["gatePolicy"]["activationState"])
        self.assertEqual(0, evidence["orphanReferenceCount"])
        self.assertEqual(0, evidence["missingMotionCount"])

        self.assertTrue(
            ELDERGROVE_CLASS_SOURCE_IDS.issubset(
                {
                    source_id
                    for family in catalog["championFamilies"]
                    for source_id in family["classSourceIds"]
                }
            )
        )
        self.assertEqual(
            ELDERGROVE_EXTERNAL_SKILL_IDS | approved_progression_ids,
            {skill["externalSourceId"] for skill in catalog["skills"]},
        )
        skill_by_id = {skill["id"]: skill for skill in catalog["skills"]}
        for family in catalog["championFamilies"]:
            class_source = next(
                source_id
                for source_id in family["classSourceIds"]
                if source_id.startswith("ClassFamily.")
            )
            class_slug = class_source.split(".")[-1].lower()
            progression_skills = [
                skill_by_id[skill_id]
                for skill_id in family["skillIds"]
                if skill_by_id[skill_id]["externalSourceId"].startswith(
                    f"anotherlife.class_progression.{class_slug}."
                )
            ]
            self.assertEqual(24, len(progression_skills), family["id"])
            progression_sources = [
                source_id
                for source_id in family["classSourceIds"]
                if source_id.startswith("anotherlife.")
            ]
            self.assertEqual(4, len(progression_sources), family["id"])
        self.assertEqual(
            {
                "tdf_grove_strider",
                "tdf_mire_lumenback",
                "tdf_fauna_eldergrove_thornburrow_hare",
                "tdf_fauna_eldergrove_moonshell_cicada",
            },
            {beast["habitatSourceRef"].split("#")[-1] for beast in catalog["beastFamilies"]},
        )
        self.assertEqual(
            {
                "dragon_eldergrove_moonbough",
                "tdf_boss_eldergrove_mere_root_leviathan",
                "tdf_elite_eldergrove_hollowbark_stalker",
                "tdf_elite_eldergrove_mirrorfin_lurker",
                "tdf_elite_eldergrove_sunmane_thornstag",
            },
            {monster["habitatSourceRef"].split("#")[-1] for monster in catalog["monsterFamilies"]},
        )

        variants = {row["id"]: row for row in catalog["platformVariants"]}
        platforms = {row["id"]: row["tier"] for row in catalog["platformProfiles"]}
        packets = {row["id"]: row for row in catalog["decisionPackets"]}
        decision_dimension_names = {
            "animationPersonality": "animation_personality",
            "magicalGrammar": "magical_grammar",
        }
        for section in (
            "playableRaces",
            "npcArchetypes",
            "championFamilies",
            "beastFamilies",
            "monsterFamilies",
        ):
            for entity in catalog[section]:
                self.assertTrue(entity["rigFamilyIds"], entity["id"])
                self.assertTrue(entity["budgetProfileIds"], entity["id"])
                self.assertEqual(
                    {"mobile_floor", "mobile_high", "pc_high"},
                    {
                        platforms[variants[variant_id]["platformProfileId"]]
                        for variant_id in entity["platformVariantIds"]
                    },
                    entity["id"],
                )
                for dimension_name, dimension in entity["creativeDecisions"].items():
                    self.assertEqual("owner_decision_required", dimension["state"])
                    self.assertTrue(dimension["decisionPacketIds"])
                    packet_dimension = decision_dimension_names.get(
                        dimension_name, dimension_name
                    )
                    for packet_id in dimension["decisionPacketIds"]:
                        self.assertIn(
                            packet_dimension,
                            packets[packet_id]["decisionDimensions"],
                            f"{entity['id']}.{dimension_name}",
                        )

        for section in contract.SECTIONS:
            for row in catalog[section]:
                if section not in {"provenance", "decisionPackets"}:
                    self.assertTrue(row["authority"]["provenanceIds"], row["id"])
                    if row["authority"]["status"] != "approved_fact":
                        self.assertTrue(row["authority"]["decisionPacketIds"], row["id"])

    def test_duplicate_id_fails_closed(self) -> None:
        catalog = build_catalog()
        duplicate = copy.deepcopy(catalog["rigFamilies"][0])
        catalog["rigFamilies"].append(duplicate)
        self.assert_semantic_failure(catalog, "DuplicateId")

    def test_orphan_reference_fails_closed(self) -> None:
        catalog = build_catalog()
        catalog["championFamilies"][0]["rigFamilyIds"] = [
            "rct_shared_rig_missing_fixture_v001"
        ]
        self.assert_semantic_failure(catalog, "MissingReference")

    def test_missing_motion_fails_closed(self) -> None:
        catalog = build_catalog()
        catalog["motions"] = [
            row for row in catalog["motions"] if row["motionKey"] != "combat.parry"
        ]
        self.assert_semantic_failure(catalog, "MissingMotion")

    def test_skill_phase_trace_mismatch_fails_closed(self) -> None:
        catalog = build_catalog()
        catalog["skillTraceability"][0]["motionPhases"]["cast"]["recordIds"] = [
            motion_id("skill.release")
        ]
        self.assert_semantic_failure(catalog, "TraceMismatch")

    def test_vfx_category_trace_mismatch_fails_closed(self) -> None:
        catalog = build_catalog()
        catalog["skillTraceability"][0]["effects"]["impact"]["recordIds"] = [
            vfx_id("telegraph")
        ]
        self.assert_semantic_failure(catalog, "TraceMismatch")

    def test_motion_template_drift_fails_closed(self) -> None:
        catalog = build_catalog()
        champion_template = next(
            row for row in catalog["motionMatrixTemplates"] if row["subjectKind"] == "champion"
        )
        champion_template["requiredMotionKeys"].remove("combat.parry")
        self.assert_semantic_failure(catalog, "MotionTemplateDrift")

    def test_gate_packet_must_name_subject(self) -> None:
        catalog = build_catalog()
        catalog["decisionPackets"][0]["subjectIds"] = [CHAMPION_ID]
        self.assert_semantic_failure(catalog, "GateConflict")

    def test_release_cannot_bypass_generation_gate(self) -> None:
        catalog = build_catalog()
        catalog["gatePolicy"]["activationState"] = "release_approved"
        self.assert_semantic_failure(catalog, "GateConflict")

    def test_release_requires_positive_gate_evidence(self) -> None:
        catalog = build_catalog()
        catalog["gatePolicy"]["generationState"] = "owner_approved"
        catalog["gatePolicy"]["activationState"] = "release_approved"
        self.assert_semantic_failure(catalog, "ownerCreative=approved with reviewer")

    def test_release_requires_valid_utc_gate_timestamps(self) -> None:
        catalog = build_catalog()
        catalog["gatePolicy"]["generationState"] = "owner_approved"
        catalog["gatePolicy"]["activationState"] = "release_approved"
        catalog["gatePolicy"]["gateEvidence"] = passed_gate_evidence()
        catalog["gatePolicy"]["gateEvidence"]["ownerCreative"]["decidedAtUtc"] = ""
        self.assertNotEqual([], list(self.schema_validator.iter_errors(catalog)))
        self.assert_semantic_failure(catalog, "UTC timestamp")

    def test_release_cannot_retain_unknown_budget_metrics(self) -> None:
        catalog = build_catalog()
        catalog["gatePolicy"]["generationState"] = "owner_approved"
        catalog["gatePolicy"]["activationState"] = "release_approved"
        catalog["gatePolicy"]["gateEvidence"] = passed_gate_evidence()
        catalog["budgetProfiles"][0]["authority"] = approved_authority()
        packet = catalog["decisionPackets"][0]
        packet.update(
            {
                "ownerStatus": "APPROVE",
                "approvedAlternativeId": packet["alternatives"][0]["alternativeId"],
                "ownerResponse": "Synthetic fixture approval.",
                "decidedAtUtc": "2026-08-31T00:00:00Z",
            }
        )
        self.assert_semantic_failure(catalog, "textures.textureLongEdge")

    def test_release_cannot_retain_provisional_budget_metrics(self) -> None:
        catalog = build_catalog()
        catalog["gatePolicy"]["generationState"] = "owner_approved"
        catalog["gatePolicy"]["activationState"] = "release_approved"
        catalog["gatePolicy"]["gateEvidence"] = passed_gate_evidence()
        catalog["budgetProfiles"][0]["authority"] = approved_authority()
        for _, metric in contract._iter_metric_objects(catalog["budgetProfiles"][0]):
            metric.update(
                {
                    "state": "documented_provisional",
                    "limitKind": "maximum_inclusive",
                    "value": 1,
                    "secondaryValue": None,
                    "sourceRefs": ["fixture:provisional-source"],
                    "decisionPacketIds": [],
                }
            )
        packet = catalog["decisionPackets"][0]
        packet.update(
            {
                "ownerStatus": "APPROVE",
                "approvedAlternativeId": packet["alternatives"][0]["alternativeId"],
                "ownerResponse": "Synthetic fixture approval.",
                "decidedAtUtc": "2026-08-31T00:00:00Z",
            }
        )
        self.assertEqual([], list(self.schema_validator.iter_errors(catalog)))
        self.assert_semantic_failure(catalog, "textures.textureLongEdge")

    def test_subjectless_skill_fails_closed(self) -> None:
        catalog = build_catalog()
        catalog["skills"][0]["subjectIds"] = []
        self.assertNotEqual([], list(self.schema_validator.iter_errors(catalog)))
        self.assert_semantic_failure(catalog, "subjectIds must not be empty")

    def test_untraced_skill_motion_fails_closed(self) -> None:
        catalog = build_catalog()
        extra = copy.deepcopy(next(row for row in catalog["motions"] if row["skillPhase"] == "cast"))
        extra["id"] = "rct_stonehold_motion_fixture_skill_cast_variant_v001"
        extra["displayName"] = "Untraced fixture cast variant"
        catalog["motions"].append(extra)
        catalog["motions"].sort(key=lambda row: row["id"].encode("utf-8"))
        self.assert_semantic_failure(catalog, "OrphanSkillMotion")

    def test_untraced_skill_effect_fails_closed(self) -> None:
        catalog = build_catalog()
        extra = copy.deepcopy(next(row for row in catalog["vfxEffects"] if row["category"] == "impact"))
        extra["id"] = "rct_stonehold_vfx_fixture_impact_variant_v001"
        extra["displayName"] = "Untraced fixture impact variant"
        catalog["vfxEffects"].append(extra)
        catalog["vfxEffects"].sort(key=lambda row: row["id"].encode("utf-8"))
        self.assert_semantic_failure(catalog, "OrphanSkillEffect")

    def test_revise_state_is_representable(self) -> None:
        catalog = build_catalog()
        catalog["budgetProfiles"][0]["authority"]["ownerStatus"] = "REVISE"
        packet = catalog["decisionPackets"][0]
        packet.update(
            {
                "ownerStatus": "REVISE",
                "ownerResponse": "Revise the synthetic budget proposal.",
                "decidedAtUtc": "2026-08-31T00:00:00Z",
            }
        )
        self.assertEqual([], list(self.schema_validator.iter_errors(catalog)))
        contract.validate_catalog(catalog)

    def test_revise_state_keeps_generation_held(self) -> None:
        catalog = build_catalog()
        catalog["gatePolicy"]["generationState"] = "owner_approved"
        catalog["budgetProfiles"][0]["authority"]["ownerStatus"] = "REVISE"
        packet = catalog["decisionPackets"][0]
        packet.update(
            {
                "ownerStatus": "REVISE",
                "ownerResponse": "Revise the synthetic budget proposal.",
                "decidedAtUtc": "2026-08-31T00:00:00Z",
            }
        )
        self.assertEqual([], list(self.schema_validator.iter_errors(catalog)))
        self.assert_semantic_failure(catalog, DECISION_ID)

    def test_trace_subject_mismatch_fails_closed(self) -> None:
        catalog = build_catalog()
        cast = next(row for row in catalog["motions"] if row["skillPhase"] == "cast")
        cast["subjectIds"] = [BUDGET_ID]
        self.assert_semantic_failure(catalog, "TraceSubjectMismatch")

    def test_invalid_documented_budget_range_fails_closed(self) -> None:
        catalog = build_catalog()
        metric = catalog["budgetProfiles"][0]["textures"]["textureLongEdge"]
        metric.update(
            {
                "state": "documented_provisional",
                "limitKind": "range_inclusive",
                "value": 2048,
                "secondaryValue": 1024,
                "sourceRefs": ["fixture:source"],
                "decisionPacketIds": [],
            }
        )
        self.assert_semantic_failure(catalog, "BudgetRangeInvalid")

    def test_approved_catalog_cannot_retain_pending_records(self) -> None:
        catalog = build_catalog()
        catalog["authority"]["status"] = "approved"
        self.assert_semantic_failure(catalog, "GateConflict")


if __name__ == "__main__":
    unittest.main()
