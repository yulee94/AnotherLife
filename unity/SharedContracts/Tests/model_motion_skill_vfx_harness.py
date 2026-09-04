#!/usr/bin/env python3
"""Fail-closed model, motion-coverage, and skill-VFX validation harness.

Every model and skill emits an explicit PASS, FAIL, or BLOCKED verdict.
Missing walking, running, attacking, special attack, or cast/use motion or
effect is FAIL. Absent evidence is BLOCKED. Weighted scores are rejected.
Owner creative approval is a separate gate and cannot make this harness PASS.
"""

from __future__ import annotations

import argparse
import json
from collections.abc import Iterable
from pathlib import Path
from typing import Any

HARNESS_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_model_motion_skill_vfx_harness.v1.json"
)
HARNESS_SCHEMA_PATH = Path(
    "unity/SharedContracts/Schemas/al-model-motion-skill-vfx-harness.schema.json"
)
REQUIRED_MOTION_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_required_motion_manifest.json"
)
MOTION_CATALOG_PATH = Path(
    "unity/ArtSource/MotionLibrary/al_motion_library_catalog.v1.json"
)
SKILL_WEATHER_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/skill_weather.v1.json"
)

ALLOWED_VERDICTS = ("PASS", "FAIL", "BLOCKED")
REQUIRED_KINDS = ("champion", "npc", "beast", "monster")
PLAYER_BUILD_EVIDENCE_CANDIDATES = (
    Path("unity/Logs/ModelMotionSkillVfx/player_build_presentation.json"),
    Path("unity/Logs/MotionRoundTrip/player_build_presentation.json"),
)


class HarnessValidationError(RuntimeError):
    """Raised when the harness catalog or an evidence packet fails closed."""

    def __init__(self, issues: Iterable[str]):
        self.issues = sorted(set(issues))
        super().__init__("\n".join(self.issues))


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _schema_issues(instance: dict[str, Any], schema_path: Path) -> list[str]:
    try:
        from jsonschema import Draft202012Validator, FormatChecker
    except ImportError:
        return ["DependencyMissing: install jsonschema"]
    schema = load_json(schema_path)
    validator = Draft202012Validator(schema, format_checker=FormatChecker())
    issues: list[str] = []
    for error in validator.iter_errors(instance):
        location = ".".join(str(part) for part in error.absolute_path) or "<root>"
        issues.append(f"SchemaViolation: {location}: {error.message}")
    return issues


def validate_catalog(catalog: dict[str, Any], repo_root: Path) -> dict[str, Any]:
    issues = _schema_issues(catalog, repo_root / HARNESS_SCHEMA_PATH)
    if catalog.get("requiredKinds") != list(REQUIRED_KINDS):
        issues.append("RequiredKindMismatch: champion, npc, beast, and monster are mandatory")
    axes = [row.get("id") for row in catalog.get("requiredMotionAxes", [])]
    if axes != ["walking", "running", "attacking", "special_attack", "cast_use"]:
        issues.append("RequiredMotionAxisMismatch: five independent motion axes are mandatory")
    policy = catalog.get("verdictPolicy") or {}
    if policy.get("weightedScoreForbidden") is not True:
        issues.append("WeightedScoreForbidden: catalog must reject weighted scores")
    if issues:
        raise HarnessValidationError(issues)
    return catalog


def _combine(verdicts: Iterable[str]) -> str:
    values = list(verdicts)
    if any(value == "FAIL" for value in values):
        return "FAIL"
    if any(value == "BLOCKED" for value in values):
        return "BLOCKED"
    return "PASS"


def _check_map(checks: Any) -> dict[str, dict[str, Any]]:
    mapped: dict[str, dict[str, Any]] = {}
    if isinstance(checks, dict):
        for key, value in checks.items():
            if isinstance(value, dict):
                mapped[str(key)] = value
            else:
                mapped[str(key)] = {"verdict": str(value)}
        return mapped
    if isinstance(checks, list):
        for row in checks:
            if isinstance(row, dict) and row.get("id"):
                mapped[str(row["id"])] = row
    return mapped


def _axis_present(axis: dict[str, Any], present: set[str]) -> bool:
    keys = [str(key) for key in axis.get("keys") or [] if key]
    if not keys:
        return False
    if axis.get("rule") == "all":
        return all(key in present for key in keys)
    return any(key in present for key in keys)


def _reject_scores(payload: dict[str, Any], prefix: str) -> list[str]:
    issues: list[str] = []
    for key in ("score", "weightedScore", "weighted_score", "qualityScore"):
        if key in payload and payload[key] is not None:
            issues.append(f"{prefix}:weighted_score_forbidden:{key}")
    return issues


def evaluate_model(catalog: dict[str, Any], model: dict[str, Any]) -> dict[str, Any]:
    model_id = str(model.get("id") or "unnamed_model")
    kind = str(model.get("kind") or "")
    reasons: list[str] = []
    checks_out: list[dict[str, str]] = []
    reasons.extend(_reject_scores(model, f"model:{model_id}"))
    if kind not in REQUIRED_KINDS:
        reasons.append(f"model:{model_id}:unknown_kind:{kind}")
    if model.get("missingRepresentative") is True:
        reasons.append(f"model:{model_id}:missing_representative:{kind or 'unknown'}")

    present = {
        str(key)
        for key in (model.get("presentMotionKeys") or [])
        if isinstance(key, str) and key
    }
    for axis in catalog.get("requiredMotionAxes", []):
        axis_id = str(axis.get("id"))
        if _axis_present(axis, present):
            checks_out.append({"id": f"motion:{axis_id}", "verdict": "PASS", "reason": ""})
        else:
            reason = f"missing_motion_axis:{axis_id}"
            reasons.append(f"model:{model_id}:{reason}")
            checks_out.append({"id": f"motion:{axis_id}", "verdict": "FAIL", "reason": reason})

    check_map = _check_map(model.get("checks"))
    for family in catalog.get("modelCheckFamilies", []):
        family_id = str(family.get("id"))
        row = check_map.get(family_id)
        if row is None:
            reason = f"missing_evidence:{family_id}"
            reasons.append(f"model:{model_id}:{reason}")
            checks_out.append({"id": family_id, "verdict": "BLOCKED", "reason": reason})
            continue
        verdict = str(row.get("verdict") or "")
        reason = str(row.get("reason") or "")
        if verdict not in ALLOWED_VERDICTS:
            reason = f"invalid_verdict:{family_id}"
            reasons.append(f"model:{model_id}:{reason}")
            checks_out.append({"id": family_id, "verdict": "FAIL", "reason": reason})
            continue
        if verdict == "FAIL":
            reasons.append(f"model:{model_id}:{family_id}:{reason or 'failed'}")
        elif verdict == "BLOCKED":
            reasons.append(f"model:{model_id}:{family_id}:{reason or 'blocked'}")
        checks_out.append({"id": family_id, "verdict": verdict, "reason": reason})

    player_build = str(model.get("playerBuildVerdict") or "")
    if player_build not in ALLOWED_VERDICTS:
        player_build = "BLOCKED"
        reasons.append(f"model:{model_id}:missing_evidence:player_build_presentation")
    elif player_build != "PASS":
        reasons.append(f"model:{model_id}:player_build:{player_build}")

    verdicts = [row["verdict"] for row in checks_out] + [player_build]
    if any(item.startswith(f"model:{model_id}:missing_motion_axis:") for item in reasons):
        verdicts.append("FAIL")
    if any("missing_representative" in item or "weighted_score_forbidden" in item for item in reasons):
        verdicts.append("FAIL")
    verdict = _combine(verdicts)
    return {
        "id": model_id,
        "kind": kind,
        "subjectType": "model",
        "verdict": verdict,
        "checks": checks_out,
        "reasons": sorted(set(reasons)),
    }


def evaluate_skill(catalog: dict[str, Any], skill: dict[str, Any]) -> dict[str, Any]:
    skill_id = str(skill.get("id") or "unnamed_skill")
    reasons: list[str] = []
    checks_out: list[dict[str, str]] = []
    reasons.extend(_reject_scores(skill, f"skill:{skill_id}"))
    phases = skill.get("phases") or {}
    effects = skill.get("effects") or {}
    for phase in catalog.get("requiredSkillMotionPhases", []):
        value = str(phases.get(phase) or "").strip()
        if value:
            checks_out.append({"id": f"motion:{phase}", "verdict": "PASS", "reason": ""})
        else:
            reason = f"missing_skill_motion:{phase}"
            reasons.append(f"skill:{skill_id}:{reason}")
            checks_out.append({"id": f"motion:{phase}", "verdict": "FAIL", "reason": reason})
    for axis in catalog.get("requiredSkillEffectAxes", []):
        axis_id = str(axis.get("id"))
        fields = axis.get("fields") or [axis_id]
        present = any(str(effects.get(field) or "").strip() for field in fields)
        if present:
            checks_out.append({"id": f"effect:{axis_id}", "verdict": "PASS", "reason": ""})
        else:
            reason = f"missing_skill_effect:{axis_id}"
            reasons.append(f"skill:{skill_id}:{reason}")
            checks_out.append({"id": f"effect:{axis_id}", "verdict": "FAIL", "reason": reason})
    telegraph = str(effects.get("telegraph") or "").strip()
    impact = str(effects.get("impact") or "").strip()
    if telegraph and impact:
        checks_out.append({"id": "telegraph_result_accord", "verdict": "PASS", "reason": ""})
    else:
        reason = "telegraph_result_accord"
        reasons.append(f"skill:{skill_id}:{reason}")
        checks_out.append({"id": "telegraph_result_accord", "verdict": "FAIL", "reason": reason})
    verdict = _combine(row["verdict"] for row in checks_out)
    if any("weighted_score_forbidden" in item for item in reasons):
        verdict = "FAIL"
    return {
        "id": skill_id,
        "kind": str(skill.get("actorFamily") or skill.get("kind") or ""),
        "subjectType": "skill",
        "verdict": verdict,
        "checks": checks_out,
        "reasons": sorted(set(reasons)),
    }


def evaluate_packet(catalog: dict[str, Any], packet: dict[str, Any]) -> dict[str, Any]:
    issues = _reject_scores(packet, "packet")
    models = list(packet.get("models") or [])
    skills = list(packet.get("skills") or [])
    model_reports = [evaluate_model(catalog, model) for model in models]
    skill_reports = [evaluate_skill(catalog, skill) for skill in skills]
    present_kinds = {report["kind"] for report in model_reports}
    for kind in REQUIRED_KINDS:
        if kind in present_kinds:
            continue
        model_reports.append(
            {
                "id": f"missing_{kind}_representative",
                "kind": kind,
                "subjectType": "model",
                "verdict": "FAIL",
                "checks": [
                    {
                        "id": "representative",
                        "verdict": "FAIL",
                        "reason": f"missing_representative:{kind}",
                    }
                ],
                "reasons": [f"model:missing_{kind}_representative:missing_representative:{kind}"],
            }
        )
        issues.append(f"missing_representative:{kind}")
    if not skills:
        issues.append("missing_skill_coverage")
        skill_reports.append(
            {
                "id": "missing_skill_coverage",
                "kind": "",
                "subjectType": "skill",
                "verdict": "FAIL",
                "checks": [
                    {
                        "id": "coverage",
                        "verdict": "FAIL",
                        "reason": "missing_skill_coverage",
                    }
                ],
                "reasons": ["skill:missing_skill_coverage:missing_skill_coverage"],
            }
        )

    subjects = model_reports + skill_reports
    overall = _combine(
        [row["verdict"] for row in subjects]
        + (["FAIL"] if issues else [])
    )
    return {
        "harnessId": catalog.get("harnessId"),
        "schemaVersion": 1,
        "packetId": str(packet.get("packetId") or "unspecified"),
        "overall": overall,
        "models": model_reports,
        "skills": skill_reports,
        "reasons": sorted(set(issues + [item for row in subjects for item in row["reasons"]])),
        "ownerCreativeApproval": "separate_gate_not_harness_pass",
        "weightedScore": None,
    }


def render_markdown(report: dict[str, Any]) -> str:
    lines = [
        "# Model / Motion / Skill-VFX Validation Report",
        "",
        f"- Harness: `{report.get('harnessId')}`",
        f"- Packet: `{report.get('packetId')}`",
        f"- Overall: **{report.get('overall')}**",
        "- Weighted score: forbidden (not computed)",
        "- Owner creative/visual approval: separate gate",
        "",
        "## Models",
        "",
        "| Id | Kind | Verdict | Failures |",
        "| --- | --- | --- | --- |",
    ]
    for row in report.get("models") or []:
        failures = "; ".join(item for item in row.get("reasons") or [] if item) or "—"
        lines.append(
            f"| `{row.get('id')}` | {row.get('kind')} | {row.get('verdict')} | {failures} |"
        )
    lines.extend(
        [
            "",
            "## Skills",
            "",
            "| Id | Family | Verdict | Failures |",
            "| --- | --- | --- | --- |",
        ]
    )
    for row in report.get("skills") or []:
        failures = "; ".join(item for item in row.get("reasons") or [] if item) or "—"
        lines.append(
            f"| `{row.get('id')}` | {row.get('kind')} | {row.get('verdict')} | {failures} |"
        )
    lines.extend(["", "## Notes", "", "- PASS requires every required axis and check."])
    lines.append("- FAIL is recorded per missing walk/run/attack/special/cast-use motion or effect.")
    lines.append("- BLOCKED is recorded when evidence is absent; it is not a pass.")
    return "\n".join(lines) + "\n"


def _coverage_keys(coverage: dict[str, Any]) -> set[str]:
    present: set[str] = set()
    for row in coverage.get("requirements") or []:
        status = row.get("status")
        key = row.get("motionKey")
        if status in {"available_source_candidate", "qualified"} and isinstance(key, str):
            present.add(key)
    return present


def _blocked_check(reason: str) -> dict[str, str]:
    return {"verdict": "BLOCKED", "reason": reason}


def build_repo_packet(repo_root: Path, catalog: dict[str, Any]) -> dict[str, Any]:
    required = load_json(repo_root / REQUIRED_MOTION_PATH)
    motion_catalog = load_json(repo_root / MOTION_CATALOG_PATH)
    weather = load_json(repo_root / SKILL_WEATHER_PATH)
    bound: dict[str, set[str]] = {}
    for binding in motion_catalog.get("bindings") or []:
        profile = binding.get("representativeProfileId")
        key = binding.get("motionKey")
        if isinstance(profile, str) and isinstance(key, str):
            bound.setdefault(profile, set()).add(key)
    coverage_by_kind: dict[str, dict[str, Any]] = {}
    kind_by_profile = {
        row.get("representativeProfileId"): None
        for row in required.get("representativeCoverage") or []
    }
    standard_profiles = {
        "rmc_representative_champion_vanguard_v001": "champion",
        "rmc_representative_npc_covenant_sentinel_v001": "npc",
        "rmc_representative_beast_slagwhistle_v001": "beast",
    }
    models: list[dict[str, Any]] = []
    for row in required.get("representativeCoverage") or []:
        profile_id = str(row.get("representativeProfileId") or "")
        kind = standard_profiles.get(profile_id)
        if kind is None:
            continue
        present = set(bound.get(profile_id) or []) | _coverage_keys(row)
        checks = {
            family["id"]: _blocked_check("admission_blocked_until_validated")
            for family in catalog["modelCheckFamilies"]
        }
        models.append(
            {
                "id": profile_id,
                "kind": kind,
                "presentMotionKeys": sorted(present),
                "checks": checks,
                "playerBuildVerdict": "BLOCKED",
                "admissionState": row.get("admissionState"),
            }
        )
        coverage_by_kind[kind] = row
        kind_by_profile[profile_id] = kind
    if "monster" not in coverage_by_kind:
        checks = {
            family["id"]: _blocked_check("missing_representative")
            for family in catalog["modelCheckFamilies"]
        }
        models.append(
            {
                "id": "missing_monster_representative",
                "kind": "monster",
                "missingRepresentative": True,
                "presentMotionKeys": [],
                "checks": checks,
                "playerBuildVerdict": "BLOCKED",
            }
        )

    modules = {
        str(record.get("id"))
        for record in weather.get("records") or []
        if record.get("kind") == "effect_module"
    }
    skills: list[dict[str, Any]] = []
    for record in weather.get("records") or []:
        kind = record.get("kind")
        if kind not in {"skill_loadout", "skill_cast_binding"}:
            continue
        skill_id = str(record.get("skill_id") or record.get("id") or "")
        phases = {
            "anticipation": record.get("motion_anticipation_id"),
            "cast": record.get("motion_cast_id"),
            "channel": record.get("motion_channel_id"),
            "release": record.get("motion_release_id"),
            "recovery": record.get("motion_recovery_id"),
        }
        effects = {
            "telegraph": record.get("telegraph_module_id") if record.get("telegraph_module_id") in modules else "",
            "active": record.get("active_effect_module_id") if record.get("active_effect_module_id") in modules else "",
            "impact": record.get("impact_module_id") if record.get("impact_module_id") in modules else "",
            "cleanup": record.get("cleanup_module_id") if record.get("cleanup_module_id") in modules else "",
            "accessibility": record.get("accessibility_variant_id") if record.get("accessibility_variant_id") in modules else "",
        }
        skills.append(
            {
                "id": skill_id,
                "actorFamily": record.get("actor_family") or "champion",
                "phases": phases,
                "effects": effects,
            }
        )

    player_build_present = any(
        (repo_root / path).is_file() for path in PLAYER_BUILD_EVIDENCE_CANDIDATES
    )
    if player_build_present:
        for model in models:
            if model.get("missingRepresentative") is not True:
                model["playerBuildVerdict"] = "PASS"

    return {
        "packetId": "repo-live-evaluation",
        "models": models,
        "skills": skills,
    }


def evaluate_repo(repo_root: Path) -> dict[str, Any]:
    catalog = validate_catalog(load_json(repo_root / HARNESS_PATH), repo_root)
    packet = build_repo_packet(repo_root, catalog)
    return evaluate_packet(catalog, packet)


def complete_model(kind: str, model_id: str | None = None) -> dict[str, Any]:
    locomotion = {
        "champion": ["locomotion.walk", "locomotion.run", "attack.basic", "attack.heavy", "skill.cast"],
        "npc": ["locomotion.walk", "locomotion.run", "attack.basic", "attack.heavy", "skill.cast"],
        "beast": ["locomotion.walk", "locomotion.run", "attack.basic", "attack.special", "skill.cast"],
        "monster": ["locomotion.crawl", "locomotion.run", "attack.basic", "attack.special", "skill.anticipation"],
    }
    checks = {
        family: {"verdict": "PASS", "reason": "fixture"}
        for family in (
            "mesh_topology",
            "uv_material",
            "scale_pivot",
            "skeleton_bind_pose",
            "skin_deformation",
            "animation_clips",
            "equipment_sockets",
            "colliders_hitboxes",
            "lod_impostor_budget",
            "catalog_references",
            "pooling_cleanup",
            "capture_anchors",
            "performance_memory_thermal_overdraw",
        )
    }
    return {
        "id": model_id or f"{kind}_representative",
        "kind": kind,
        "presentMotionKeys": list(locomotion[kind]),
        "checks": checks,
        "playerBuildVerdict": "PASS",
    }


def complete_skill(skill_id: str, family: str = "champion") -> dict[str, Any]:
    return {
        "id": skill_id,
        "actorFamily": family,
        "phases": {
            "anticipation": "motion_skill_anticipation",
            "cast": "motion_skill_cast",
            "channel": "motion_skill_channel",
            "release": "motion_skill_release",
            "recovery": "motion_skill_recovery",
        },
        "effects": {
            "telegraph": "telegraph_ground_ring",
            "active": "active_melee_slash",
            "impact": "impact_hit_flash",
            "cleanup": "cleanup_release",
            "accessibility": "a11y_high_contrast_shape",
        },
    }


def complete_packet() -> dict[str, Any]:
    return {
        "packetId": "fixture-complete",
        "models": [complete_model(kind) for kind in REQUIRED_KINDS],
        "skills": [
            complete_skill("champion_skill", "champion"),
            complete_skill("npc_skill", "npc"),
            complete_skill("beast_skill", "beast"),
            complete_skill("monster_skill", "monster"),
        ],
    }


def write_report(report: dict[str, Any], output_json: Path) -> None:
    output_json.parent.mkdir(parents=True, exist_ok=True)
    output_json.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    output_json.with_suffix(".md").write_text(render_markdown(report), encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--packet", type=Path)
    parser.add_argument(
        "--out",
        type=Path,
        default=Path("unity/Logs/ModelMotionSkillVfx/harness_report.json"),
    )
    args = parser.parse_args(argv)
    repo_root = args.repo_root.resolve()
    catalog = validate_catalog(load_json(repo_root / HARNESS_PATH), repo_root)
    if args.packet:
        packet = load_json(args.packet)
    else:
        packet = build_repo_packet(repo_root, catalog)
    report = evaluate_packet(catalog, packet)
    write_report(report, args.out if args.out.is_absolute() else repo_root / args.out)
    print(f"overall={report['overall']} models={len(report['models'])} skills={len(report['skills'])}")
    return 0 if report["overall"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
