#!/usr/bin/env python3
"""Validate one boss + one skill presentation catalog without gameplay authority."""

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
QUALIFICATION_PATH = (
    REPO_ROOT
    / "unity"
    / "Docs"
    / "Terrestrials"
    / "RealmCreatureProductionSourceV001"
    / "ProductionSlices"
    / "FaultCrownedColossusV001"
    / "fault_crowned_colossus_qualification_manifest_v001.json"
)
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
    weather = load_json(SKILL_WEATHER_PATH)
    ids: set[str] = set()
    for record in weather.get("records") or []:
        if record.get("kind") != "skill_cast_binding":
            continue
        skill_id = record.get("skill_id") or record.get("id")
        if isinstance(skill_id, str) and skill_id:
            ids.add(skill_id)
    return ids


def _qualification() -> dict[str, Any]:
    return load_json(QUALIFICATION_PATH)


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


def _skill_harness_report(skill_profile: dict[str, Any]) -> dict[str, Any]:
    sys.path.insert(0, str(HARNESS_TESTS))
    import model_motion_skill_vfx_harness as harness

    catalog = harness.validate_catalog(
        harness.load_json(REPO_ROOT / harness.HARNESS_PATH),
        REPO_ROOT,
    )
    phases = {
        name: str((skill_profile.get("phases") or {}).get(name) or "")
        for name in REQUIRED_PHASES
    }
    effects = {
        name: str((skill_profile.get("effects") or {}).get(name) or "")
        for name in REQUIRED_EFFECTS
    }
    skill_report = harness.evaluate_skill(
        catalog,
        {
            "id": skill_profile.get("skillId"),
            "actorFamily": skill_profile.get("actorFamily") or "boss",
            "phases": phases,
            "effects": effects,
        },
    )
    return {
        "harnessId": catalog.get("harnessId"),
        "schemaVersion": 1,
        "packetId": "boss-skill-presentation-slice",
        "overall": skill_report["verdict"],
        "skills": [skill_report],
        "weightedScore": None,
    }


def validate_catalog_payload(catalog: dict[str, Any]) -> dict[str, Any]:
    issues: list[str] = []
    issues.extend(_schema_issues(catalog))
    _collect_forbidden(catalog, issues)
    if catalog.get("schemaVersion") != 1:
        issues.append(f"UnsupportedSchemaVersion:{catalog.get('schemaVersion')}")
    if catalog.get("gameplayAuthority") is True or catalog.get("runtimeSpawn") is True:
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
    skill_ids = [row.get("id") for row in skills]
    if len(skill_ids) != len(set(skill_ids)):
        issues.append("DuplicateSkillProfile")

    weather_skills = _weather_skill_ids()
    qualification = _qualification()
    expected_hash = str(qualification.get("sourceSha256") or "")
    boss_report: dict[str, Any] = {}
    present_motion: set[str] = set()
    for boss in bosses:
        if boss.get("id") != EXPECTED_BOSS_ID:
            issues.append(f"UnexpectedBossProfile:{boss.get('id')}")
        if boss.get("modelId") != EXPECTED_MODEL_ID:
            issues.append(f"UnexpectedModelId:{boss.get('modelId')}")
        if boss.get("sourceProfileId") != EXPECTED_SOURCE_ID:
            issues.append(f"UnexpectedSourceProfileId:{boss.get('sourceProfileId')}")
        if boss.get("sourceSha256") != expected_hash:
            issues.append("SourceHashMismatch")
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
        present_motion = {
            str(key) for key in (boss.get("motionKeys") or []) if isinstance(key, str)
        }
        for key in REQUIRED_MOTION:
            if key not in present_motion:
                issues.append(f"MissingMotionKey:{key}")
        boss_report = {
            "id": boss.get("id"),
            "modelId": boss.get("modelId"),
            "sourceProfileId": boss.get("sourceProfileId"),
        }

    skill_report: dict[str, Any] = {}
    skill_profile: dict[str, Any] = {}
    for skill in skills:
        skill_profile = skill
        if skill.get("id") != EXPECTED_SKILL_PROFILE_ID:
            issues.append(f"UnexpectedSkillProfile:{skill.get('id')}")
        if skill.get("skillId") not in weather_skills:
            issues.append("UnknownSkillId")
        if skill.get("skillId") != EXPECTED_SKILL_ID:
            issues.append(f"UnexpectedSkillId:{skill.get('skillId')}")
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
        skill_report = {
            "id": skill.get("id"),
            "skillId": skill.get("skillId"),
        }

    motion_verdict = "FAIL"
    motion_issues: list[str] = []
    packet_report: dict[str, Any] = {
        "skills": [],
        "weightedScore": None,
        "overall": "FAIL",
    }
    skill_verdict = "FAIL"
    if not issues and skill_profile:
        motion_verdict, motion_issues = _motion_axis_verdict(present_motion)
        issues.extend(motion_issues)
        packet_report = _skill_harness_report(skill_profile)
        skill_verdict = str(packet_report.get("skills", [{}])[0].get("verdict") or "FAIL")
        if skill_verdict != "PASS":
            issues.extend(packet_report.get("skills", [{}])[0].get("reasons") or ["HarnessSkillFail"])
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
        "deferred": ["device_evidence", "user_readability", "scale_out"],
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
) -> dict[str, Any]:
    if quality not in REQUIRED_QUALITY:
        raise ValueError(f"unknown quality: {quality}")
    if distance not in REQUIRED_DISTANCE:
        raise ValueError(f"unknown distance: {distance}")
    boss = (catalog.get("bossProfiles") or [None])[0] or {}
    skill = (catalog.get("skillProfiles") or [None])[0] or {}
    pooling = skill.get("pooling") or boss.get("pooling") or {}
    return {
        "quality": quality,
        "distance": distance,
        "gameplay": dict(gameplay or FROZEN_GAMEPLAY),
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
