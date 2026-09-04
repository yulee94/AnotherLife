#!/usr/bin/env python3
"""Validate scaled boss/elite and skill presentation without gameplay authority."""

from __future__ import annotations

import argparse
import json
import sys
from collections.abc import Iterable
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[2]
CATALOG_PATH = (
    REPO_ROOT
    / "unity"
    / "Assets"
    / "AL"
    / "StreamingAssets"
    / "GameData"
    / "al_boss_skill_presentation_catalog.v1.json"
)
SCHEMA_PATH = (
    REPO_ROOT
    / "unity"
    / "SharedContracts"
    / "Schemas"
    / "al-boss-skill-presentation.schema.json"
)
SOURCE_ROOT = (
    REPO_ROOT
    / "unity"
    / "Docs"
    / "Terrestrials"
    / "RealmCreatureProductionSourceV001"
)
APPROVAL_PATH = SOURCE_ROOT / "realm_creature_2d_approval_manifest_v002.json"
SOURCE_MANIFEST_PATH = SOURCE_ROOT / "realm_creature_3d_source_manifest_v001.json"
QUALIFICATION_ROOT = SOURCE_ROOT / "ProductionSlices"
SKILL_WEATHER_PATH = (
    REPO_ROOT
    / "unity"
    / "Assets"
    / "AL"
    / "StreamingAssets"
    / "GameData"
    / "skill_weather.v1.json"
)
HARNESS_TESTS = REPO_ROOT / "unity" / "SharedContracts" / "Tests"

EXPECTED_BOSS_ID = "boss_presentation_stonehold_fault_crowned_colossus_v001"
EXPECTED_MODEL_ID = "boss_stonehold_fault_crowned_colossus"
EXPECTED_SOURCE_ID = "tdf_boss_stonehold_fault_crowned_colossus"
EXPECTED_SKILL_ID = "boss_faultline_slam"
EXPECTED_SKILL_PROFILE_ID = "skill_presentation_boss_faultline_slam_v001"
REQUIRED_QUALITY = ("low", "balanced", "high")
REQUIRED_DISTANCE = ("hero", "nearby", "distant")
REQUIRED_PHASES = ("anticipation", "cast", "channel", "release", "recovery")
REQUIRED_EFFECTS = ("telegraph", "active", "impact", "cleanup", "accessibility")
REQUIRED_MOTION = (
    "locomotion.walk",
    "locomotion.run",
    "attack.basic",
    "attack.special",
    "skill.anticipation",
)
FORBIDDEN_FIELDS = {
    "slot",
    "ItemGrade",
    "itemGrade",
    "item_grade",
    "power",
    "damage",
    "cooldown_seconds",
    "mana_cost",
    "threat",
    "loot",
    "spawn",
}
FROZEN_GAMEPLAY = {
    "skillId": EXPECTED_SKILL_ID,
    "source": "skill_weather_cast_binding",
    "presentationCannotMutate": True,
}


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def load_default_catalog() -> dict[str, Any]:
    return load_json(CATALOG_PATH)


def _collect_forbidden(payload: Any, found: list[str]) -> None:
    if isinstance(payload, dict):
        for key, value in payload.items():
            if key in FORBIDDEN_FIELDS:
                found.append(f"ForbiddenField:{key}")
            _collect_forbidden(value, found)
    elif isinstance(payload, list):
        for item in payload:
            _collect_forbidden(item, found)


def _tier_ids(rows: Any) -> list[str]:
    if not isinstance(rows, list):
        return []
    return [str(row.get("id") or "") for row in rows if isinstance(row, dict)]


def _schema_issues(payload: dict[str, Any]) -> list[str]:
    if not SCHEMA_PATH.is_file():
        return ["MissingSchema"]
    try:
        from jsonschema import Draft202012Validator, FormatChecker
    except ImportError:
        return []
    schema = load_json(SCHEMA_PATH)
    validator = Draft202012Validator(schema, format_checker=FormatChecker())
    issues: list[str] = []
    for error in validator.iter_errors(payload):
        location = ".".join(str(part) for part in error.absolute_path) or "<root>"
        issues.append(f"SchemaViolation:{location}:{error.message}")
    return issues


def _weather_skill_ids() -> set[str]:
    return set(_weather_skill_bindings())


def _weather_skill_bindings() -> dict[str, dict[str, Any]]:
    weather = load_json(SKILL_WEATHER_PATH)
    return {
        str(record["skill_id"]): record
        for record in weather.get("records") or []
        if record.get("kind") == "skill_cast_binding" and record.get("skill_id")
    }


def _approved_sources() -> dict[str, dict[str, Any]]:
    approval = load_json(APPROVAL_PATH)
    return {
        str(row["id"]): row
        for row in approval.get("entries") or []
        if str(row.get("id") or "").startswith(("tdf_boss_", "tdf_elite_"))
        and row.get("status") == "APPROVED_2D"
    }


def _source_models() -> dict[str, dict[str, Any]]:
    source = load_json(SOURCE_MANIFEST_PATH)
    return {
        str(row["source2dId"]): {**row, "sourceVersion": source.get("sourceVersion")}
        for row in source.get("models") or []
        if row.get("source2dId")
    }


def _qualifications() -> dict[str, dict[str, Any]]:
    rows: dict[str, dict[str, Any]] = {}
    for path in sorted(QUALIFICATION_ROOT.glob("*/*_qualification_manifest_v001.json")):
        qualification = load_json(path)
        if qualification.get("sourceQualification") == "PASS":
            rows[str(qualification["source2dId"])] = qualification
    return rows


def _motion_axis_verdict(present: set[str]) -> tuple[str, list[str]]:
    sys.path.insert(0, str(HARNESS_TESTS))
    import model_motion_skill_vfx_harness as harness

    catalog = harness.validate_catalog(
        harness.load_json(REPO_ROOT / harness.HARNESS_PATH),
        REPO_ROOT,
    )
    issues: list[str] = []
    for axis in catalog["requiredMotionAxes"]:
        if not harness._axis_present(axis, present):
            issues.append(f"missing_motion_axis:{axis['id']}")
    return ("FAIL" if issues else "PASS", issues)


def _skill_harness_report(skill_profiles: list[dict[str, Any]]) -> dict[str, Any]:
    sys.path.insert(0, str(HARNESS_TESTS))
    import model_motion_skill_vfx_harness as harness

    catalog = harness.validate_catalog(
        harness.load_json(REPO_ROOT / harness.HARNESS_PATH),
        REPO_ROOT,
    )
    skill_reports = []
    for skill_profile in skill_profiles:
        phases = {
            name: str((skill_profile.get("phases") or {}).get(name) or "")
            for name in REQUIRED_PHASES
        }
        effects = {
            name: str((skill_profile.get("effects") or {}).get(name) or "")
            for name in REQUIRED_EFFECTS
        }
        skill_reports.append(
            harness.evaluate_skill(
                catalog,
                {
                    "id": skill_profile.get("skillId"),
                    "actorFamily": skill_profile.get("actorFamily") or "boss",
                    "phases": phases,
                    "effects": effects,
                },
            )
        )
    return {
        "harnessId": catalog.get("harnessId"),
        "schemaVersion": 1,
        "packetId": "boss-skill-presentation-scale-out",
        "overall": "PASS" if all(row["verdict"] == "PASS" for row in skill_reports) else "FAIL",
        "skills": skill_reports,
        "weightedScore": None,
    }


def validate_catalog_payload(catalog: dict[str, Any]) -> dict[str, Any]:
    issues: list[str] = []
    issues.extend(_schema_issues(catalog))
    _collect_forbidden(catalog, issues)
    if catalog.get("schemaVersion") != 1:
        issues.append(f"UnsupportedSchemaVersion:{catalog.get('schemaVersion')}")
    authority = catalog.get("authority") or {}
    if authority.get("gameplayAuthority") is True or authority.get("runtimeSpawn") is True:
        issues.append("GameplayOrSpawnAuthorityForbidden")

    bosses = list(catalog.get("bossProfiles") or [])
    skills = list(catalog.get("skillProfiles") or [])
    if not bosses:
        issues.append("MissingBossProfile")
    if not skills:
        issues.append("MissingSkillProfile")

    boss_ids = [row.get("id") for row in bosses]
    if len(boss_ids) != len(set(boss_ids)):
        issues.append("DuplicateBossProfile")
    source_ids = [row.get("sourceProfileId") for row in bosses]
    if len(source_ids) != len(set(source_ids)):
        issues.append("DuplicateBossSourceProfile")
    model_ids = [row.get("modelId") for row in bosses]
    if len(model_ids) != len(set(model_ids)):
        issues.append("DuplicateBossModel")
    skill_ids = [row.get("id") for row in skills]
    if len(skill_ids) != len(set(skill_ids)):
        issues.append("DuplicateSkillProfile")
    gameplay_skill_ids = [row.get("skillId") for row in skills]
    if len(gameplay_skill_ids) != len(set(gameplay_skill_ids)):
        issues.append("DuplicateSkillId")

    approved_sources = _approved_sources()
    source_models = _source_models()
    qualifications = _qualifications()
    if set(source_ids) != set(approved_sources):
        issues.append("ApprovedBossEliteCoverageMismatch")

    boss_report: dict[str, Any] = {}
    qualified_motion_sets: list[tuple[str, set[str]]] = []
    for boss in bosses:
        source_id = str(boss.get("sourceProfileId") or "")
        approved = approved_sources.get(source_id)
        source = source_models.get(source_id)
        qualification = qualifications.get(source_id)
        if approved is None or source is None:
            issues.append(f"UnknownBossEliteSource:{source_id}")
            continue
        expected_kind = "boss" if source_id.startswith("tdf_boss_") else "elite"
        expected_realm = source_id.split("_")[2]
        if boss.get("kind") != expected_kind:
            issues.append(f"KindMismatch:{source_id}")
        if boss.get("realmId") != expected_realm:
            issues.append(f"RealmMismatch:{source_id}")
        if boss.get("modelId") != source.get("modelId"):
            issues.append(f"ModelIdMismatch:{source_id}")
        selected_source = source.get("selectedSource") or {}
        if boss.get("sourceSha256") != selected_source.get("sha256"):
            issues.append(f"SourceHashMismatch:{source_id}")
            issues.append("SourceHashMismatch")
        approved_asset = (approved.get("sources") or [{}])[0]
        if boss.get("source2dPath") != approved_asset.get("path"):
            issues.append(f"Source2dPathMismatch:{source_id}")
        if boss.get("source2dSha256") != approved_asset.get("sha256"):
            issues.append(f"Source2dHashMismatch:{source_id}")
        if boss.get("sourceRequirements") != approved.get("requirements"):
            issues.append(f"SourceRequirementsMismatch:{source_id}")

        motion = {
            str(key) for key in (boss.get("motionKeys") or []) if isinstance(key, str)
        }
        if qualification is None:
            if boss.get("assetState") != "source_only":
                issues.append(f"UnqualifiedAssetStateMismatch:{source_id}")
            if boss.get("sourceVersion") != source.get("sourceVersion"):
                issues.append(f"SourceVersionMismatch:{source_id}")
            for field in ("qualificationId", "prefabRef", "rigId", "materialId"):
                if boss.get(field) != "explicit_unavailable":
                    issues.append(f"UnavailableAssetMustFailClosed:{source_id}:{field}")
            if motion:
                issues.append(f"UnavailableAssetHasMotion:{source_id}")
        else:
            if boss.get("assetState") != "source_qualified":
                issues.append(f"QualifiedAssetStateMismatch:{source_id}")
            expected_fields = {
                "qualificationId": qualification.get("qualificationId"),
                "sourceVersion": qualification.get("sourceVersion"),
                "sourceSha256": qualification.get("sourceSha256"),
                "prefabRef": (qualification.get("artifacts") or {}).get("fbx", {}).get("path"),
                "rigId": (qualification.get("rig") or {}).get("armatureObject"),
                "materialId": (qualification.get("material") or {}).get("id"),
            }
            for field, expected in expected_fields.items():
                if boss.get(field) != expected:
                    issues.append(f"QualificationMismatch:{source_id}:{field}")
            expected_motion = {
                str(row.get("motionKey"))
                for row in qualification.get("motions") or []
                if row.get("motionKey")
            }
            if motion != expected_motion:
                issues.append(f"MotionSetMismatch:{source_id}")
            for key in REQUIRED_MOTION:
                if key not in motion:
                    issues.append(f"MissingMotionKey:{source_id}:{key}")
            qualified_motion_sets.append((source_id, motion))

        if "pooling" not in boss:
            issues.append("MissingPooling")
        if "accessibility" not in boss:
            issues.append("MissingAccessibility")
        quality = _tier_ids(boss.get("qualityTiers"))
        distance = _tier_ids(boss.get("distanceContexts"))
        if tuple(quality) != REQUIRED_QUALITY:
            issues.append(f"QualityTierMismatch:{quality}")
        if tuple(distance) != REQUIRED_DISTANCE:
            issues.append(f"DistanceContextMismatch:{distance}")
        accessibility = boss.get("accessibility") or {}
        if not accessibility.get("reducedFlash") or len(accessibility.get("nonColorCues") or []) < 2:
            issues.append(f"AccessibilityCueMismatch:{source_id}")
        if boss.get("modelId") == EXPECTED_MODEL_ID:
            boss_report = {
                "id": boss.get("id"),
                "modelId": boss.get("modelId"),
                "sourceProfileId": source_id,
            }

    skill_report: dict[str, Any] = {}
    weather_bindings = _weather_skill_bindings()
    if set(gameplay_skill_ids) != set(weather_bindings):
        issues.append("SkillBindingCoverageMismatch")
    for skill in skills:
        skill_id = str(skill.get("skillId") or "")
        binding = weather_bindings.get(skill_id)
        if binding is None:
            issues.append("UnknownSkillId")
            continue
        if skill.get("id") != f"skill_presentation_{skill_id}_v001":
            issues.append(f"SkillProfileIdMismatch:{skill_id}")
        if skill.get("actorFamily") != binding.get("actor_family"):
            issues.append(f"SkillActorFamilyMismatch:{skill_id}")
        expected_phases = {
            "anticipation": binding.get("motion_anticipation_id"),
            "cast": binding.get("motion_cast_id"),
            "channel": binding.get("motion_channel_id"),
            "release": binding.get("motion_release_id"),
            "recovery": binding.get("motion_recovery_id"),
        }
        expected_effects = {
            "telegraph": binding.get("telegraph_module_id"),
            "active": binding.get("active_effect_module_id"),
            "impact": binding.get("impact_module_id"),
            "cleanup": binding.get("cleanup_module_id"),
            "accessibility": binding.get("accessibility_variant_id"),
        }
        if skill.get("phases") != expected_phases:
            issues.append(f"SkillPhaseBindingMismatch:{skill_id}")
        if skill.get("effects") != expected_effects:
            issues.append(f"SkillEffectBindingMismatch:{skill_id}")
        if skill.get("telegraphChannel") != binding.get("telegraph_module_id"):
            issues.append(f"TelegraphChannelMismatch:{skill_id}")
        if skill.get("cosmeticChannel") != binding.get("active_effect_module_id"):
            issues.append(f"CosmeticChannelMismatch:{skill_id}")
        if "pooling" not in skill:
            issues.append("MissingPooling")
        if "accessibility" not in skill:
            issues.append("MissingAccessibility")
        quality = _tier_ids(skill.get("qualityTiers"))
        distance = _tier_ids(skill.get("distanceContexts"))
        if tuple(quality) != REQUIRED_QUALITY:
            issues.append(f"QualityTierMismatch:{quality}")
        if tuple(distance) != REQUIRED_DISTANCE:
            issues.append(f"DistanceContextMismatch:{distance}")
        phases = skill.get("phases") or {}
        for phase in REQUIRED_PHASES:
            if not str(phases.get(phase) or "").strip():
                issues.append(f"MissingSkillPhase:{phase}")
        effects = skill.get("effects") or {}
        for axis in REQUIRED_EFFECTS:
            if not str(effects.get(axis) or "").strip():
                issues.append(f"MissingSkillEffect:{axis}")
        accessibility = skill.get("accessibility") or {}
        if not accessibility.get("reducedFlash") or len(accessibility.get("nonColorCues") or []) < 2:
            issues.append(f"AccessibilityCueMismatch:{skill_id}")
        if skill_id == EXPECTED_SKILL_ID:
            skill_report = {
                "id": skill.get("id"),
                "skillId": skill_id,
            }

    motion_verdict = "FAIL"
    motion_issues: list[str] = []
    packet_report: dict[str, Any] = {
        "skills": [],
        "weightedScore": None,
        "overall": "FAIL",
    }
    skill_verdict = "FAIL"
    if not issues and skills and qualified_motion_sets:
        for source_id, present_motion in qualified_motion_sets:
            _, axis_issues = _motion_axis_verdict(present_motion)
            motion_issues.extend(f"{source_id}:{issue}" for issue in axis_issues)
        issues.extend(motion_issues)
        motion_verdict = "PASS" if not motion_issues else "FAIL"
        packet_report = _skill_harness_report(skills)
        skill_verdict = str(packet_report.get("overall") or "FAIL")
        if skill_verdict != "PASS":
            for row in packet_report.get("skills") or []:
                if row.get("verdict") != "PASS":
                    issues.extend(row.get("reasons") or [f"HarnessSkillFail:{row.get('id')}"])
        if packet_report.get("weightedScore") is not None:
            issues.append("weighted_score_forbidden")

    overall = "PASS" if not issues else "FAIL"
    return {
        "overall": overall,
        "scope": "presentation_catalog_runtime_contract",
        "issues": sorted(set(issues)),
        "boss": boss_report,
        "skill": skill_report,
        "gameplayAuthority": False,
        "runtimeSpawn": False,
        "scaled": {
            "bossEliteProfiles": len(bosses),
            "skillProfiles": len(skills),
            "sourceQualifiedProfiles": len(qualified_motion_sets),
            "sourceOnlyProfiles": len(bosses) - len(qualified_motion_sets),
        },
        "deferred": ["device_evidence", "player_build_capture", "user_readability"],
        "harness": {
            "motionAxes": motion_verdict if overall == "PASS" else ("FAIL" if motion_issues or issues else motion_verdict),
            "skill": skill_verdict if overall == "PASS" else skill_verdict,
            "packetReport": packet_report,
        },
        "runtimeSnapshot": {
            "gameplay": dict(FROZEN_GAMEPLAY),
        },
    }


def validate_default_catalog() -> dict[str, Any]:
    return validate_catalog_payload(load_default_catalog())


def resolve_presentation(
    catalog: dict[str, Any],
    quality: str,
    distance: str,
    gameplay: dict[str, Any] | None = None,
    *,
    model_id: str = EXPECTED_MODEL_ID,
    skill_id: str = EXPECTED_SKILL_ID,
) -> dict[str, Any]:
    if quality not in REQUIRED_QUALITY:
        raise ValueError(f"unknown quality: {quality}")
    if distance not in REQUIRED_DISTANCE:
        raise ValueError(f"unknown distance: {distance}")
    boss = next(
        (row for row in catalog.get("bossProfiles") or [] if row.get("modelId") == model_id),
        None,
    )
    if boss is None:
        raise ValueError(f"unknown model id: {model_id}")
    if boss.get("assetState") != "source_qualified":
        raise ValueError(f"source-only presentation asset: {model_id}")
    skill = next(
        (row for row in catalog.get("skillProfiles") or [] if row.get("skillId") == skill_id),
        None,
    )
    if skill is None:
        raise ValueError(f"unknown skill id: {skill_id}")
    pooling = skill.get("pooling") or boss.get("pooling") or {}
    gameplay_snapshot = dict(
        gameplay
        or {
            "skillId": skill_id,
            "source": "skill_weather_cast_binding",
            "presentationCannotMutate": True,
        }
    )
    return {
        "quality": quality,
        "distance": distance,
        "modelId": model_id,
        "skillId": skill_id,
        "gameplay": gameplay_snapshot,
        "protectedCuesPreserved": True,
        "bossLod": next(
            (
                row.get("lodId")
                for row in (boss.get("qualityTiers") or [])
                if row.get("id") == quality
            ),
            "",
        ),
        "skillCosmeticScale": next(
            (
                row.get("cosmeticScale")
                for row in (skill.get("distanceContexts") or [])
                if row.get("id") == distance
            ),
            0,
        ),
        "pool": {
            "maxActive": int(pooling.get("maxActive") or 0),
            "maxPooled": int(pooling.get("maxPooled") or 0),
        },
    }


class PresentationPool:
    def __init__(self, max_active: int, max_pooled: int):
        self.max_active = max_active
        self.max_pooled = max_pooled
        self.created = 0
        self.active = 0
        self._free: list[dict[str, Any]] = []
        self._next_id = 1

    def acquire(self, key: str) -> dict[str, Any] | None:
        if self.active >= self.max_active:
            return None
        if self._free:
            item = self._free.pop()
        else:
            if self.created >= self.max_active + self.max_pooled:
                return None
            item = {"instanceId": self._next_id, "key": key}
            self._next_id += 1
            self.created += 1
        item["key"] = key
        self.active += 1
        return item

    def release(self, item: dict[str, Any] | None) -> None:
        if item is None:
            return
        self.active = max(0, self.active - 1)
        if len(self._free) < self.max_pooled:
            self._free.append(item)


def evaluate_live_model_harness() -> dict[str, Any]:
    sys.path.insert(0, str(HARNESS_TESTS))
    import model_motion_skill_vfx_harness as harness

    return harness.evaluate_repo(REPO_ROOT)


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, default=None)
    args = parser.parse_args(list(argv) if argv is not None else None)
    if args.repo_root is not None:
        raise SystemExit("repo-root override is not supported; run from the repository root")
    report = validate_default_catalog()
    print(json.dumps(report, indent=2))
    return 0 if report["overall"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
