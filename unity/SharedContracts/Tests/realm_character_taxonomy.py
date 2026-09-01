#!/usr/bin/env python3
"""Semantic validation for the gated realm character/creature catalog.

JSON Schema enforces record shape. This module enforces cross-record identity,
reference, motion-matrix, skill-phase, VFX-category, and approval-gate rules that
JSON Schema cannot express.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from datetime import datetime
from pathlib import Path
from typing import Any, Iterable


SCHEMA_PATH = Path("unity/SharedContracts/Schemas/al-realm-character-taxonomy.schema.json")
SECTIONS = (
    "provenance",
    "decisionPackets",
    "platformProfiles",
    "budgetProfiles",
    "motionMatrixTemplates",
    "playableRaces",
    "npcArchetypes",
    "championFamilies",
    "beastFamilies",
    "monsterFamilies",
    "bodyModules",
    "equipmentModules",
    "rigFamilies",
    "facialSystems",
    "secondaryPhysicsProfiles",
    "lodProfiles",
    "colliderProfiles",
    "hitboxProfiles",
    "platformVariants",
    "skills",
    "motions",
    "vfxEffects",
)
SECTION_KINDS = {
    "provenance": "provenance",
    "decisionPackets": "decision",
    "platformProfiles": "platform",
    "budgetProfiles": "budget",
    "motionMatrixTemplates": "motion_matrix",
    "playableRaces": "race",
    "npcArchetypes": "npc",
    "championFamilies": "champion",
    "beastFamilies": "beast",
    "monsterFamilies": "monster",
    "bodyModules": "body",
    "equipmentModules": "equipment",
    "rigFamilies": "rig",
    "facialSystems": "face",
    "secondaryPhysicsProfiles": "physics",
    "lodProfiles": "lod",
    "colliderProfiles": "collider",
    "hitboxProfiles": "hitbox",
    "platformVariants": "platform",
    "skills": "skill",
    "motions": "motion",
    "vfxEffects": "vfx",
}
STABLE_ID_RE = re.compile(
    r"^rct_(shared|stonehold|eldergrove|crownlands|umbral)_"
    r"(race|npc|champion|beast|monster|body|equipment|rig|face|physics|lod|"
    r"collider|hitbox|platform|budget|skill|motion_matrix|motion|vfx|provenance|decision)_"
    r"[a-z0-9]+(?:_[a-z0-9]+)*_v[0-9]{3}$"
)
CATALOG_ID_RE = re.compile(
    r"^rct_(stonehold|eldergrove|crownlands|umbral)_catalog_"
    r"[a-z0-9]+(?:_[a-z0-9]+)*_v[0-9]{3}$"
)
GATE_IDS = [
    "owner_creative",
    "technical",
    "provenance",
    "motion_effect_coverage",
    "performance_mobile_floor",
    "accessibility",
    "release",
]
POSITIVE_GATE_STATES = {
    "ownerCreative": "approved",
    "technical": "passed",
    "provenance": "cleared",
    "motionEffectCoverage": "passed",
    "performanceMobileFloor": "passed",
    "accessibility": "passed",
    "release": "admitted",
}
SKILL_PHASES = ["anticipation", "cast", "channel", "release", "recovery"]
VFX_CATEGORIES = [
    "telegraph",
    "cast",
    "channel",
    "release",
    "trail",
    "projectile",
    "impact",
    "area",
    "buff",
    "debuff",
    "status",
    "environmental",
    "result",
    "cleanup",
]
CHAMPION_MOTIONS = {
    "idle.neutral",
    "idle.variant",
    "locomotion.walk",
    "locomotion.run",
    "locomotion.sprint",
    "locomotion.start",
    "locomotion.stop",
    "locomotion.turn",
    "locomotion.jump",
    "locomotion.fall",
    "locomotion.land",
    "combat.dodge",
    "combat.block",
    "combat.parry",
    "weapon.draw",
    "weapon.stow",
    "attack.basic",
    "attack.chain",
    "attack.heavy",
    "reaction.hit",
    "reaction.knockdown",
    "reaction.get_up",
    "defeat",
    "traversal",
    "interaction",
    "emote",
}
CHAMPION_PRODUCTION_MOTIONS = CHAMPION_MOTIONS | {
    "attack.charged",
    "skill.use",
}
NPC_MOTIONS = CHAMPION_MOTIONS | {
    "social.talk",
    "social.gesture",
    "daily.sit",
    "daily.sleep",
    "daily.work",
    "daily.carry",
    "daily.gather",
    "daily.trade",
    "daily.craft",
    "reaction.react",
    "reaction.flee",
    "combat.defend",
}
BEAST_MOTIONS = {
    "locomotion.turn",
    "idle.neutral",
    "idle.variant",
    "attack.basic",
    "attack.special",
    "reaction.hit",
    "reaction.stagger",
    "defeat",
}
MONSTER_MOTIONS = BEAST_MOTIONS | {"combat.alert"}
BOSS_MOTIONS = MONSTER_MOTIONS | {
    "boss.enter",
    "boss.phase",
    "boss.transition",
}
MOTION_TEMPLATE_REQUIREMENTS = {
    "champion": CHAMPION_PRODUCTION_MOTIONS,
    "npc": NPC_MOTIONS,
    "beast": BEAST_MOTIONS,
    "monster": MONSTER_MOTIONS,
    "boss": BOSS_MOTIONS,
}


class RealmTaxonomyValidationError(RuntimeError):
    """Raised when a catalog violates semantic production rules."""

    def __init__(self, issues: list[str]):
        self.issues = issues
        super().__init__("\n".join(issues))


def _is_non_blank(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def _is_utc_timestamp(value: Any) -> bool:
    if not isinstance(value, str) or not (value.endswith("Z") or value.endswith("+00:00")):
        return False
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return False
    offset = parsed.utcoffset()
    return offset is not None and offset.total_seconds() == 0


def strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    value: dict[str, Any] = {}
    for key, item in pairs:
        if key in value:
            raise RealmTaxonomyValidationError([f"DuplicateProperty: {key}"])
        value[key] = item
    return value


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=strict_object)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise RealmTaxonomyValidationError([f"MalformedCatalog: {path}: {error}"]) from error
    if not isinstance(value, dict):
        raise RealmTaxonomyValidationError(["MalformedCatalog: root must be an object"])
    return value


def _rows(catalog: dict[str, Any], section: str, issues: list[str]) -> list[dict[str, Any]]:
    value = catalog.get(section)
    if not isinstance(value, list):
        issues.append(f"MissingField: {section} must be an array")
        return []
    if any(not isinstance(row, dict) for row in value):
        issues.append(f"MalformedRecord: every {section} entry must be an object")
    return [row for row in value if isinstance(row, dict)]


def _iter_metric_objects(profile: dict[str, Any]) -> Iterable[tuple[str, dict[str, Any]]]:
    for group in (
        "geometry",
        "materials",
        "textures",
        "bones",
        "physics",
        "animation",
        "vfx",
        "colliders",
        "hitboxes",
    ):
        metrics = profile.get(group)
        if not isinstance(metrics, dict):
            continue
        for name, metric in metrics.items():
            if isinstance(metric, dict):
                yield f"{group}.{name}", metric


def validate_catalog(catalog: dict[str, Any]) -> dict[str, Any]:
    """Validate cross-record semantics and return deterministic coverage evidence."""

    issues: list[str] = []
    realm_id = catalog.get("realmId")
    catalog_match = CATALOG_ID_RE.fullmatch(str(catalog.get("catalogId", "")))
    if catalog_match is None or catalog_match.group(1) != realm_id:
        issues.append("MalformedId: catalogId must carry the catalog realm scope")
    gate = catalog.get("gatePolicy")
    if not isinstance(gate, dict) or gate.get("requiredGateIds") != GATE_IDS:
        issues.append("GateConflict: requiredGateIds must use the canonical ordered seven-gate set")
    gate_evidence = gate.get("gateEvidence") if isinstance(gate, dict) else None
    if not isinstance(gate_evidence, dict):
        issues.append("MissingField: gatePolicy.gateEvidence")

    rows_by_section = {section: _rows(catalog, section, issues) for section in SECTIONS}
    all_records: dict[str, tuple[str, dict[str, Any]]] = {}
    for section, rows in rows_by_section.items():
        ids = [row.get("id") for row in rows]
        if ids != sorted(ids, key=lambda value: str(value).encode("utf-8")):
            issues.append(f"CanonicalOrderViolation: {section} records are not bytewise ID-sorted")
        for row in rows:
            record_id = row.get("id")
            match = STABLE_ID_RE.fullmatch(str(record_id or ""))
            if match is None:
                issues.append(f"MalformedId: {section} contains {record_id!r}")
                continue
            scope, kind = match.groups()
            if kind != SECTION_KINDS[section]:
                issues.append(f"MalformedId: {record_id} uses kind {kind}, expected {SECTION_KINDS[section]}")
            if scope not in {"shared", realm_id}:
                issues.append(f"RealmScopeMismatch: {record_id} cannot appear in {realm_id} catalog")
            if record_id in all_records:
                issues.append(f"DuplicateId: {record_id}")
            else:
                all_records[record_id] = (section, row)

    def require_ref(owner: str, field: str, target: Any, sections: set[str] | None = None) -> None:
        if not isinstance(target, str) or target not in all_records:
            issues.append(f"MissingReference: {owner}.{field} -> {target!r}")
            return
        if sections is not None and all_records[target][0] not in sections:
            issues.append(
                f"ReferenceTypeMismatch: {owner}.{field} -> {target} ({all_records[target][0]})"
            )

    def require_refs(owner: str, field: str, targets: Any, sections: set[str] | None = None) -> None:
        if not isinstance(targets, list):
            issues.append(f"MissingField: {owner}.{field} must be an array")
            return
        for target in targets:
            require_ref(owner, field, target, sections)

    header_sections = set(SECTIONS) - {"provenance", "decisionPackets"}
    for section in sorted(header_sections):
        for row in rows_by_section[section]:
            record_id = str(row.get("id"))
            authority = row.get("authority")
            if not isinstance(authority, dict):
                issues.append(f"MissingField: {record_id}.authority")
                continue
            require_refs(record_id, "authority.provenanceIds", authority.get("provenanceIds"), {"provenance"})
            require_refs(record_id, "authority.decisionPacketIds", authority.get("decisionPacketIds"), {"decisionPackets"})
            if authority.get("status") in {"proposal", "owner_decision_required", "rejected"}:
                for packet_id in authority.get("decisionPacketIds", []):
                    packet = all_records.get(packet_id, (None, {}))[1]
                    if record_id not in packet.get("subjectIds", []):
                        issues.append(f"GateConflict: {packet_id} does not name gated subject {record_id}")

    for packet in rows_by_section["decisionPackets"]:
        packet_id = str(packet.get("id"))
        require_refs(packet_id, "subjectIds", packet.get("subjectIds"), header_sections)
        require_refs(packet_id, "provenanceIds", packet.get("provenanceIds"), {"provenance"})
        alternatives = packet.get("alternatives", [])
        alternative_ids = [item.get("alternativeId") for item in alternatives if isinstance(item, dict)]
        if len(alternative_ids) != len(set(alternative_ids)):
            issues.append(f"DuplicateId: {packet_id} has duplicate alternativeId values")
        approved = packet.get("approvedAlternativeId")
        if approved is not None and approved not in alternative_ids:
            issues.append(f"MissingReference: {packet_id}.approvedAlternativeId -> {approved!r}")

    platform_ids = {row.get("id") for row in rows_by_section["platformProfiles"]}
    decision_ids = {row.get("id") for row in rows_by_section["decisionPackets"]}
    for profile in rows_by_section["budgetProfiles"]:
        profile_id = str(profile.get("id"))
        require_ref(profile_id, "platformProfileId", profile.get("platformProfileId"), {"platformProfiles"})
        for path, metric in _iter_metric_objects(profile):
            if metric.get("state") == "owner_decision_required":
                refs = metric.get("decisionPacketIds")
                if not isinstance(refs, list) or not refs:
                    issues.append(f"OwnerDecisionMissing: {profile_id}.{path}")
                for packet_id in refs or []:
                    if packet_id not in decision_ids:
                        issues.append(f"MissingReference: {profile_id}.{path} -> {packet_id!r}")
            elif metric.get("state") in {"documented_provisional", "approved_limit"}:
                if not metric.get("sourceRefs") or metric.get("value") is None:
                    issues.append(f"BudgetSourceMissing: {profile_id}.{path}")
                if (
                    metric.get("limitKind") == "range_inclusive"
                    and (
                        not isinstance(metric.get("secondaryValue"), (int, float))
                        or metric["secondaryValue"] < metric["value"]
                    )
                ):
                    issues.append(f"BudgetRangeInvalid: {profile_id}.{path}")

    templates_by_kind: dict[str, dict[str, Any]] = {}
    for template in rows_by_section["motionMatrixTemplates"]:
        template_id = str(template.get("id"))
        kind = template.get("subjectKind")
        if kind in templates_by_kind:
            issues.append(f"DuplicateMotionTemplate: {kind}")
        elif isinstance(kind, str):
            templates_by_kind[kind] = template
        expected = MOTION_TEMPLATE_REQUIREMENTS.get(str(kind))
        if expected is not None and set(template.get("requiredMotionKeys", [])) != expected:
            missing = sorted(expected - set(template.get("requiredMotionKeys", [])))
            extra = sorted(set(template.get("requiredMotionKeys", [])) - expected)
            issues.append(f"MotionTemplateDrift: {template_id} missing={missing} extra={extra}")
        if set(template.get("requiredSkillPhases", [])) != set(SKILL_PHASES):
            issues.append(f"SkillPhaseTemplateDrift: {template_id}")
        if set(template.get("requiredEffectCategories", [])) != set(VFX_CATEGORIES):
            issues.append(f"VfxTemplateDrift: {template_id}")
    if set(templates_by_kind) != set(MOTION_TEMPLATE_REQUIREMENTS):
        issues.append(
            f"MotionTemplateCoverage: expected {sorted(MOTION_TEMPLATE_REQUIREMENTS)}, got {sorted(templates_by_kind)}"
        )

    entity_sections = {
        "playableRaces": None,
        "npcArchetypes": "npc",
        "championFamilies": "champion",
        "beastFamilies": "beast",
        "monsterFamilies": "monster",
    }
    entity_ids: set[str] = set()
    for section, subject_kind in entity_sections.items():
        for row in rows_by_section[section]:
            record_id = str(row.get("id"))
            entity_ids.add(record_id)
            creative = row.get("creativeDecisions")
            if not isinstance(creative, dict):
                issues.append(f"MissingField: {record_id}.creativeDecisions")
            else:
                for dimension, decision in creative.items():
                    if not isinstance(decision, dict):
                        issues.append(
                            f"MissingField: {record_id}.creativeDecisions.{dimension}"
                        )
                        continue
                    require_refs(
                        record_id,
                        f"creativeDecisions.{dimension}.decisionPacketIds",
                        decision.get("decisionPacketIds"),
                        {"decisionPackets"},
                    )
                    if decision.get("state") == "owner_decision_required":
                        for packet_id in decision.get("decisionPacketIds", []):
                            packet = all_records.get(packet_id, (None, {}))[1]
                            if record_id not in packet.get("subjectIds", []):
                                issues.append(
                                    f"GateConflict: {packet_id} does not name gated subject {record_id}"
                                )
            for field, targets, accepted in [
                ("bodyModuleIds", row.get("bodyModuleIds"), {"bodyModules"}),
                ("equipmentModuleIds", row.get("equipmentModuleIds"), {"equipmentModules"}),
                ("rigFamilyIds", row.get("rigFamilyIds"), {"rigFamilies"}),
                ("facialSystemIds", row.get("facialSystemIds"), {"facialSystems"}),
                ("secondaryPhysicsProfileIds", row.get("secondaryPhysicsProfileIds"), {"secondaryPhysicsProfiles"}),
                ("lodProfileIds", row.get("lodProfileIds"), {"lodProfiles"}),
                ("colliderProfileIds", row.get("colliderProfileIds"), {"colliderProfiles"}),
                ("hitboxProfileIds", row.get("hitboxProfileIds"), {"hitboxProfiles"}),
                ("platformVariantIds", row.get("platformVariantIds"), {"platformVariants"}),
                ("budgetProfileIds", row.get("budgetProfileIds"), {"budgetProfiles"}),
                ("motionMatrixTemplateIds", row.get("motionMatrixTemplateIds"), {"motionMatrixTemplates"}),
            ]:
                require_refs(record_id, field, targets, accepted)
            if section != "playableRaces" and not row.get("rigFamilyIds"):
                issues.append(f"MissingField: {record_id}.rigFamilyIds must not be empty")
            if subject_kind is not None:
                assigned_kinds = {
                    all_records.get(template_id, (None, {}))[1].get("subjectKind")
                    for template_id in row.get("motionMatrixTemplateIds", [])
                }
                required_kinds = {subject_kind}
                if section == "monsterFamilies" and row.get("rank") == "boss":
                    required_kinds.add("boss")
                if not required_kinds.issubset(assigned_kinds):
                    issues.append(f"MotionTemplateMissing: {record_id} needs {sorted(required_kinds)}")
            if section in {"npcArchetypes", "championFamilies", "beastFamilies", "monsterFamilies"}:
                require_refs(record_id, "skillIds", row.get("skillIds"), {"skills"})
            if section == "championFamilies":
                require_refs(record_id, "playableRaceIds", row.get("playableRaceIds"), {"playableRaces"})
                require_refs(record_id, "weaponFamilyIds", row.get("weaponFamilyIds"), {"equipmentModules"})

    for section, fields in {
        "bodyModules": [("compatibleEntityIds", entity_sections.keys()), ("rigFamilyIds", {"rigFamilies"}), ("budgetProfileIds", {"budgetProfiles"})],
        "equipmentModules": [("compatibleEntityIds", set(entity_sections)), ("compatibleBodyModuleIds", {"bodyModules"}), ("rigFamilyIds", {"rigFamilies"}), ("budgetProfileIds", {"budgetProfiles"})],
        "rigFamilies": [("budgetProfileIds", {"budgetProfiles"})],
        "facialSystems": [("rigFamilyIds", {"rigFamilies"}), ("budgetProfileIds", {"budgetProfiles"})],
        "secondaryPhysicsProfiles": [("budgetProfileIds", {"budgetProfiles"})],
        "colliderProfiles": [("rigFamilyIds", {"rigFamilies"}), ("budgetProfileIds", {"budgetProfiles"})],
        "hitboxProfiles": [("rigFamilyIds", {"rigFamilies"}), ("budgetProfileIds", {"budgetProfiles"})],
    }.items():
        for row in rows_by_section[section]:
            record_id = str(row.get("id"))
            for field, accepted in fields:
                accepted_sections = set(entity_sections) if field == "compatibleEntityIds" else set(accepted)
                require_refs(record_id, field, row.get(field), accepted_sections)

    for variant in rows_by_section["platformVariants"]:
        variant_id = str(variant.get("id"))
        require_ref(variant_id, "platformProfileId", variant.get("platformProfileId"), {"platformProfiles"})
        require_refs(variant_id, "subjectIds", variant.get("subjectIds"), header_sections)
        require_refs(variant_id, "budgetProfileIds", variant.get("budgetProfileIds"), {"budgetProfiles"})

    for skill in rows_by_section["skills"]:
        skill_id = str(skill.get("id"))
        if not skill.get("subjectIds"):
            issues.append(f"MissingField: {skill_id}.subjectIds must not be empty")
        require_refs(skill_id, "subjectIds", skill.get("subjectIds"), set(entity_sections))

    motions_by_subject: dict[str, set[str]] = defaultdict(set)
    for motion in rows_by_section["motions"]:
        motion_id = str(motion.get("id"))
        if not motion.get("subjectIds"):
            issues.append(f"MissingField: {motion_id}.subjectIds must not be empty")
        require_refs(motion_id, "subjectIds", motion.get("subjectIds"), set(entity_sections))
        require_ref(motion_id, "rigFamilyId", motion.get("rigFamilyId"), {"rigFamilies"})
        if motion.get("skillId") is not None:
            require_ref(motion_id, "skillId", motion.get("skillId"), {"skills"})
        for subject_id in motion.get("subjectIds", []):
            motions_by_subject[subject_id].add(str(motion.get("motionKey")))

    for section in ("npcArchetypes", "championFamilies", "beastFamilies", "monsterFamilies"):
        for entity in rows_by_section[section]:
            entity_id = str(entity.get("id"))
            required: set[str] = set()
            for template_id in entity.get("motionMatrixTemplateIds", []):
                template = all_records.get(template_id, (None, {}))[1]
                required.update(template.get("requiredMotionKeys", []))
            for locomotion in entity.get("locomotionModes", []):
                required.add(f"locomotion.{locomotion}")
            required.update(entity.get("roleActionKeys", []))
            required.update(entity.get("bossTransitionKeys", []))
            missing = sorted(required - motions_by_subject.get(entity_id, set()))
            if missing:
                issues.append(f"MissingMotion: {entity_id} -> {missing}")

    for effect in rows_by_section["vfxEffects"]:
        effect_id = str(effect.get("id"))
        if not effect.get("subjectIds"):
            issues.append(f"MissingField: {effect_id}.subjectIds must not be empty")
        require_refs(effect_id, "subjectIds", effect.get("subjectIds"), set(entity_sections))
        require_refs(effect_id, "skillIds", effect.get("skillIds"), {"skills"})
        require_refs(effect_id, "budgetProfileIds", effect.get("budgetProfileIds"), {"budgetProfiles"})

    skills = {row.get("id"): row for row in rows_by_section["skills"]}
    traces = catalog.get("skillTraceability")
    if not isinstance(traces, list):
        issues.append("MissingField: skillTraceability must be an array")
        traces = []
    trace_ids = [row.get("skillId") for row in traces if isinstance(row, dict)]
    if len(trace_ids) != len(set(trace_ids)):
        issues.append("DuplicateTraceability: skillTraceability contains duplicate skillId rows")
    if set(trace_ids) != set(skills):
        issues.append(f"SkillTraceabilityCoverage: missing={sorted(set(skills) - set(trace_ids))} orphan={sorted(set(trace_ids) - set(skills))}")
    traced_motion_ids: set[str] = set()
    traced_effects_by_skill: dict[str, set[str]] = defaultdict(set)
    for trace in [row for row in traces if isinstance(row, dict)]:
        skill_id = str(trace.get("skillId"))
        require_ref(skill_id, "skillId", trace.get("skillId"), {"skills"})
        skill_subjects = set(skills.get(skill_id, {}).get("subjectIds", []))
        for phase in SKILL_PHASES:
            requirement = trace.get("motionPhases", {}).get(phase, {})
            for motion_id in requirement.get("recordIds", []):
                traced_motion_ids.add(motion_id)
                require_ref(skill_id, f"motionPhases.{phase}", motion_id, {"motions"})
                motion = all_records.get(motion_id, (None, {}))[1]
                if motion.get("skillId") != skill_id or motion.get("skillPhase") != phase:
                    issues.append(f"TraceMismatch: {skill_id}.{phase} -> {motion_id}")
                if not set(motion.get("subjectIds", [])).issubset(skill_subjects):
                    issues.append(f"TraceSubjectMismatch: {skill_id}.{phase} -> {motion_id}")
        for category in VFX_CATEGORIES:
            requirement = trace.get("effects", {}).get(category, {})
            for effect_id in requirement.get("recordIds", []):
                traced_effects_by_skill[skill_id].add(effect_id)
                require_ref(skill_id, f"effects.{category}", effect_id, {"vfxEffects"})
                effect = all_records.get(effect_id, (None, {}))[1]
                if skill_id not in effect.get("skillIds", []) or effect.get("category") != category:
                    issues.append(f"TraceMismatch: {skill_id}.{category} -> {effect_id}")
                if not set(effect.get("subjectIds", [])).issubset(skill_subjects):
                    issues.append(f"TraceSubjectMismatch: {skill_id}.{category} -> {effect_id}")

    for motion in rows_by_section["motions"]:
        if motion.get("skillId") is not None and motion.get("id") not in traced_motion_ids:
            issues.append(f"OrphanSkillMotion: {motion.get('id')}")
    for effect in rows_by_section["vfxEffects"]:
        for skill_id in effect.get("skillIds", []):
            if effect.get("id") not in traced_effects_by_skill.get(skill_id, set()):
                issues.append(f"OrphanSkillEffect: {effect.get('id')} for {skill_id}")

    authority = catalog.get("authority", {})
    if isinstance(gate, dict) and gate.get("activationState") == "release_approved" and gate.get("generationState") != "owner_approved":
        issues.append("GateConflict: release approval requires owner-approved generation")
    if isinstance(gate, dict) and gate.get("activationState") == "release_approved":
        for gate_name, expected_state in POSITIVE_GATE_STATES.items():
            evidence = gate_evidence.get(gate_name, {}) if isinstance(gate_evidence, dict) else {}
            if (
                evidence.get("state") != expected_state
                or not evidence.get("evidenceRefs")
                or not _is_non_blank(evidence.get("reviewer"))
                or not _is_utc_timestamp(evidence.get("decidedAtUtc"))
                or evidence.get("openIssues")
            ):
                issues.append(
                    f"GateConflict: release approval requires {gate_name}={expected_state} with reviewer, UTC timestamp, evidence, and no open issues"
                )
    if authority.get("status") == "approved" or (isinstance(gate, dict) and gate.get("generationState") == "owner_approved"):
        pending = [
            record_id
            for record_id, (section, row) in all_records.items()
            if section not in {"provenance", "decisionPackets"}
            and row.get("authority", {}).get("status") != "approved_fact"
        ]
        pending += [
            row.get("id")
            for row in rows_by_section["decisionPackets"]
            if row.get("ownerStatus") in {"PENDING", "REVISE"}
        ]
        pending += [
            f"{profile.get('id')}.{path}"
            for profile in rows_by_section["budgetProfiles"]
            for path, metric in _iter_metric_objects(profile)
            if metric.get("state") in {"owner_decision_required", "documented_provisional"}
        ]
        if pending:
            issues.append(f"GateConflict: approved catalog still has pending records {sorted(str(item) for item in pending)}")

    if issues:
        raise RealmTaxonomyValidationError(sorted(set(issues)))
    return {
        "recordCount": len(all_records),
        "uniqueIdCount": len(all_records),
        "motionTemplateKinds": sorted(templates_by_kind),
        "skillCount": len(skills),
        "traceabilityRows": len(trace_ids),
        "orphanReferenceCount": 0,
        "missingMotionCount": 0,
    }


def validate_path(repo_root: Path, catalog_path: Path) -> dict[str, Any]:
    catalog = load_json(catalog_path)
    try:
        from jsonschema import Draft202012Validator, FormatChecker
    except ImportError as error:
        raise RealmTaxonomyValidationError(["DependencyMissing: install jsonschema"]) from error
    schema = load_json(repo_root / SCHEMA_PATH)
    errors = sorted(
        Draft202012Validator(schema, format_checker=FormatChecker()).iter_errors(catalog),
        key=lambda error: list(error.absolute_path),
    )
    if errors:
        first = errors[0]
        location = ".".join(str(part) for part in first.absolute_path) or "<root>"
        raise RealmTaxonomyValidationError([f"SchemaViolation: {location}: {first.message}"])
    return validate_catalog(catalog)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("catalog", type=Path, help="realm catalog JSON to validate")
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[3])
    args = parser.parse_args()
    catalog_path = args.catalog if args.catalog.is_absolute() else args.repo_root / args.catalog
    try:
        evidence = validate_path(args.repo_root, catalog_path)
    except RealmTaxonomyValidationError as error:
        print("Realm taxonomy validation failed:", file=sys.stderr)
        for issue in error.issues:
            print(f"  - {issue}", file=sys.stderr)
        return 1
    print("PASS: realm character/creature taxonomy schema and semantics validate")
    print(json.dumps(evidence, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
