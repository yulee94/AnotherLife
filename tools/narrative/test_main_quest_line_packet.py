#!/usr/bin/env python3
"""Validate the canonical AnotherLife main-quest narrative packet set."""

from __future__ import annotations

import copy
import hashlib
import json
import sys
from pathlib import Path
from typing import Any

PACKET_VERSION = "anotherlife-main-quest-line-2026-07-23-v001"
OMEN_1_PACKET_VERSION = "omen1-a1-2026-08-13-v004"
BLOCKED_COPY_STATUS = "UNAPPROVED_COPY_BLOCKED"
C1_OBJECTIVE_IDS = [
    "OBJ_C1_MEET_REALM_GUIDE",
    "OBJ_C1_RESTORE_COVENANT",
    "OBJ_C1_FACE_GUARDIAN",
    "OBJ_C1_ACCEPT_MARK",
    "OBJ_C1_RECEIVE_LORD_APPOINTMENT",
    "OBJ_C1_RECEIVE_KINGDOM_GRANT",
    "OBJ_C1_REVIEW_KINGDOM_MANAGEMENT",
    "OBJ_C1_ENTER_KINGDOM_MANAGEMENT",
    "OBJ_C1_RETURN_TO_CHARACTER_MODE",
]
C1_BLOCKED_OBJECTIVE_KEYS = [
    ("OBJ_C1_RECEIVE_LORD_APPOINTMENT", "objective.obj_c1_receive_lord_appointment"),
    ("OBJ_C1_RECEIVE_KINGDOM_GRANT", "objective.obj_c1_receive_kingdom_grant"),
    ("OBJ_C1_REVIEW_KINGDOM_MANAGEMENT", "objective.obj_c1_review_kingdom_management"),
    ("OBJ_C1_ENTER_KINGDOM_MANAGEMENT", "objective.obj_c1_enter_kingdom_management"),
    ("OBJ_C1_RETURN_TO_CHARACTER_MODE", "objective.obj_c1_return_to_character_mode"),
]
C1_BLOCKED_LOCALIZATION_KEYS = {key for _, key in C1_BLOCKED_OBJECTIVE_KEYS}
C1_HANDOFFS = [
    "HOOK_REALM_GUARDIAN_TRIAL",
    "EVENT_REALM_COVENANT_RESTORED",
    "EVENT_LORD_APPOINTMENT_COMMITTED",
    "EVENT_KINGDOM_GRANT_COMMITTED",
    "EVENT_KINGDOM_MANAGEMENT_UNLOCK_COMMITTED",
    "ACTION_ENTER_KINGDOM_MANAGEMENT",
    "ACTION_RETURN_TO_CHARACTER_MODE",
]
REALMS = {"CROWNLANDS", "STONEHOLD", "ELDERGROVE", "UMBRAL"}
VARIANT_QUESTS = {
    "MQ_C1_PROOF_OF_WORTH",
    "MQ_C2_BORDER_OATHS",
    "MQ_C3_FIRST_RESONANCE",
    "MQ_C7_ANCIENT_LEGACY",
    "MQ_C10_CELESTIAL_RIFT",
    "MQ_C11_HIGH_SKY_TRIALS",
    "MQ_C12_EIGHT_LIGHTS",
}
FEEDBACK_TYPES = {
    "additional_dialogue",
    "witness_present",
    "epilogue_card",
    "cosmetic",
    "route_hint",
    "training_convenience",
}
PROHIBITED_SIDE_EFFECTS = {
    "critical_path_unlock",
    "required_level_progress",
    "realm_access",
    "gem_custody",
    "warmaster_eligibility",
    "true_warmaster_eligibility",
    "final_wish_access",
    "canonical_ending",
}
MILESTONES = [
    "realm_selection",
    "realm_specific_character_start",
    "main_questline_start",
    "3d_inner_realm_champion_start",
    "2_5d_inner_kingdom_progression",
    "return_to_3d_inner_realm",
    "party_hunting",
    "support_role_progression",
    "main_gate_approach",
    "outer_warzone_entry",
    "warzone_save_pillars",
    "realm_v_realm_gate_conflict",
    "connected_continents",
    "crossroads_conflict",
    "level_50",
    "warzone_points",
    "warmaster_gear",
    "true_warmaster",
    "all_eight_gems",
    "center_neutral_island",
    "shared_language",
    "cross_realm_trade",
    "wish_dragon",
    "final_wish",
]


class ValidationError(RuntimeError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValidationError(message)


def nonblank(value: Any, context: str) -> str:
    text = "" if value is None else str(value)
    require(bool(text.strip()), f"{context} is blank")
    return text


def unique(values: list[str], context: str) -> None:
    require(len(values) == len(set(values)), f"{context} contains duplicates")
    require(all(v.strip() for v in values), f"{context} contains a blank value")


def add_localized(authority: dict[str, str], localized: dict[str, Any], context: str) -> None:
    shape = set(localized)
    require(
        shape in ({"key", "text"}, {"key", "copyStatus"}),
        f"{context} has invalid localized shape",
    )
    key = nonblank(localized["key"], f"{context} key")
    if shape == {"key", "text"}:
        value = nonblank(localized["text"], f"{context} text")
    else:
        require(key in C1_BLOCKED_LOCALIZATION_KEYS, f"unauthorized copy-blocked key: {key}")
        require(localized["copyStatus"] == BLOCKED_COPY_STATUS, f"{context} copy status drift")
        value = BLOCKED_COPY_STATUS
    require(key not in authority, f"duplicate localization authority: {key}")
    authority[key] = value


def load_packet_set(manifest_path: Path) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    repo_root = manifest_path.resolve().parents[4]
    chapters: list[dict[str, Any]] = []

    for meta in manifest["components"]:
        component_path = repo_root / meta["path"]
        require(component_path.is_file(), f"missing component: {meta['path']}")
        raw = component_path.read_bytes()
        hash_bytes = raw.replace(b"\r\n", b"\n")
        require(
            hashlib.sha256(hash_bytes).hexdigest() == meta["sha256"],
            f"component hash mismatch: {meta['path']}",
        )
        component = json.loads(raw.decode("utf-8"))
        require(component["schemaVersion"] == 1, f"component schema drift: {meta['path']}")
        require(
            component["parentPacketVersion"] == manifest["packetVersion"],
            f"component parent version mismatch: {meta['path']}",
        )
        require(component["componentId"] == meta["componentId"], f"component ID mismatch: {meta['path']}")
        chapter = component["chapter"]
        require(chapter["id"] == meta["chapterId"], f"component chapter ID mismatch: {meta['path']}")
        require(chapter["order"] == meta["chapterOrder"], f"component chapter order mismatch: {meta['path']}")
        chapters.append(chapter)

    return manifest, sorted(chapters, key=lambda item: item["order"])


def validate_model(manifest: dict[str, Any], chapters: list[dict[str, Any]]) -> dict[str, int]:
    require(manifest["schemaVersion"] == 1, "unsupported manifest schema")
    require(manifest["packetVersion"] == PACKET_VERSION, "packet version drift")
    require(manifest["packetId"] == "ANOTHERLIFE_MAIN_QUEST_LINE", "packet ID drift")
    require(manifest["trackedIssue"] == 274, "tracked issue drift")
    require(manifest["primaryMode"] == "codex_narrative_content", "primary mode drift")
    prologue_authority = manifest["authorities"]["prologueQuest"]
    require(prologue_authority["questId"] == "OMEN_1", "OMEN_1 authority ID drift")
    require(
        prologue_authority["packetVersion"] == OMEN_1_PACKET_VERSION,
        "OMEN_1 authority version drift",
    )
    require(
        manifest["sourceStatus"] == "canonical_narrative_source_complete_runtime_wired",
        "source status drift",
    )
    require(manifest["requiredProductMilestones"] == MILESTONES, "required milestone inventory drift")
    require(
        manifest["expectedCounts"]
        == {
            "components": 15,
            "chapters": 15,
            "mainQuests": 15,
            "sideQuests": 30,
            "realmGems": 8,
            "realmVariantQuestFamilies": 7,
        },
        "expected count contract drift",
    )
    require(len(manifest["components"]) == 15, "component count drift")
    require(len(chapters) == 15, "chapter count drift")
    require([c["order"] for c in chapters] == list(range(15)), "chapter order is not contiguous")

    chapter_ids = [c["id"] for c in chapters]
    unique(chapter_ids, "chapter IDs")
    chapter_by_id = {c["id"]: c for c in chapters}
    index = manifest["chapterIndex"]
    require(len(index) == 15, "chapter index count drift")

    main_quests: list[dict[str, Any]] = []
    side_quests: list[dict[str, Any]] = []
    objectives: list[str] = []
    legacy_refs: list[str] = []
    localization: dict[str, str] = {}
    covered_milestones: set[str] = set()

    for order, (chapter, indexed) in enumerate(zip(chapters, index)):
        require(indexed["id"] == chapter["id"] and indexed["order"] == order, f"chapter index drift: {order}")
        require(indexed["title"] == chapter["title"], f"chapter title index drift: {chapter['id']}")
        require(indexed["mainQuestId"] == chapter["mainQuest"]["id"], f"main quest index drift: {chapter['id']}")
        require(
            indexed["sideQuestIds"] == [quest["id"] for quest in chapter["sideQuests"]],
            f"side quest index drift: {chapter['id']}",
        )
        require(indexed["milestones"] == chapter["milestones"], f"milestone index drift: {chapter['id']}")
        require(len(chapter["sideQuests"]) >= 2, f"chapter lacks two side quests: {chapter['id']}")

        add_localized(localization, chapter["title"], f"chapter {chapter['id']} title")
        add_localized(localization, chapter["summary"], f"chapter {chapter['id']} summary")
        nonblank(chapter["playMode"], f"chapter {chapter['id']} play mode")
        nonblank(chapter["progressionIntent"], f"chapter {chapter['id']} progression intent")
        covered_milestones.update(chapter["milestones"])
        legacy_refs.extend(chapter["legacyChapterReferences"])

        main = chapter["mainQuest"]
        main_quests.append(main)
        require(main["type"] == "main" and main["criticalPath"] is True, f"invalid main quest: {main['id']}")
        add_localized(localization, main["title"], f"main quest {main['id']} title")
        add_localized(localization, main["summary"], f"main quest {main['id']} summary")
        nonblank(main["completionOutcome"], f"main quest {main['id']} completion")
        nonblank(main["failurePolicy"], f"main quest {main['id']} failure policy")
        nonblank(main["abandonmentPolicy"], f"main quest {main['id']} abandonment policy")
        nonblank(main["resumePolicy"], f"main quest {main['id']} resume policy")
        for objective in main["objectives"]:
            objectives.append(nonblank(objective["id"], f"main objective in {main['id']}"))
            add_localized(localization, objective["text"], f"main objective {objective['id']}")
        for variant in main.get("realmVariants", []):
            require(variant["legacyChapterId"] in chapter["legacyChapterReferences"], f"legacy variant drift: {main['id']}")
            add_localized(localization, variant["title"], f"variant title {main['id']}")
            add_localized(localization, variant["summary"], f"variant summary {main['id']}")

        for side in chapter["sideQuests"]:
            side_quests.append(side)
            require(side["type"] == "side" and side["optional"] is True, f"side quest not optional: {side['id']}")
            require(side["criticalPath"] is False, f"side quest is critical: {side['id']}")
            require(len(side["purposeTags"]) >= 2, f"side quest lacks purpose tags: {side['id']}")
            add_localized(localization, side["title"], f"side quest {side['id']} title")
            add_localized(localization, side["summary"], f"side quest {side['id']} summary")
            add_localized(localization, side["mainStoryContribution"], f"side quest {side['id']} contribution")
            add_localized(localization, side["feedback"]["text"], f"side quest {side['id']} feedback")
            require(side["feedback"]["type"] in FEEDBACK_TYPES, f"forbidden side feedback: {side['id']}")
            require(side["feedback"]["requiredForCriticalPath"] is False, f"side quest gates main path: {side['id']}")
            require(
                side["rewardBoundary"] == "optional_cosmetic_insight_relationship_or_convenience_only",
                f"side reward boundary drift: {side['id']}",
            )
            require(set(side["prohibitedEffects"]) == PROHIBITED_SIDE_EFFECTS, f"side prohibition drift: {side['id']}")
            for objective in side["objectives"]:
                objectives.append(nonblank(objective["id"], f"side objective in {side['id']}"))
                add_localized(localization, objective["text"], f"side objective {objective['id']}")

    main_ids = [q["id"] for q in main_quests]
    side_ids = [q["id"] for q in side_quests]
    unique(main_ids, "main quest IDs")
    unique(side_ids, "side quest IDs")
    require(set(main_ids).isdisjoint(side_ids), "main and side quest IDs overlap")
    require(len(main_ids) == 15 and len(side_ids) == 30, "quest counts drift")

    for position, main in enumerate(main_quests):
        if position == 0:
            require(main["id"] == "OMEN_1" and main["prerequisites"] == [], "OMEN_1 entry drift")
            require(
                main["sourceAuthority"]["path"] == manifest["authorities"]["prologueQuest"]["path"],
                "OMEN_1 source authority drift",
            )
            require(
                main["sourceAuthority"]["version"] == prologue_authority["packetVersion"],
                "OMEN_1 source authority version drift",
            )
        else:
            previous = main_quests[position - 1]
            require(previous["id"] in main["prerequisites"], f"main chain prerequisite drift: {main['id']}")
            require(previous["unlocks"] == main["id"], f"main chain unlock drift: {previous['id']}")
    require(main_quests[-1]["unlocks"] is None, "final quest unlock drift")

    c1 = chapter_by_id["CH01_PROOF_OF_WORTH"]["mainQuest"]
    require(c1["prerequisites"] == ["OMEN_1"], "C1 prerequisite drift")
    require(c1["unlocks"] == "MQ_C2_BORDER_OATHS", "C1 unlock drift")
    require([item["id"] for item in c1["objectives"]] == C1_OBJECTIVE_IDS, "C1 objective order drift")
    require(
        [(item["id"], item["text"]["key"]) for item in c1["objectives"][4:]]
        == C1_BLOCKED_OBJECTIVE_KEYS,
        "C1 copy-blocked objective key/order drift",
    )
    require(c1["handoffs"] == C1_HANDOFFS, "C1 handoff drift")
    require(
        c1["summary"]["text"]
        == "Restore a failing realm covenant, face the realm guardian, and earn the right to lead.",
        "C1 appointment-order summary drift",
    )
    require(
        c1["completionOutcome"]
        == "After proof of worth, the realm formally appoints the champion as Lord, grants one kingdom, and unlocks the bounded Kingdom Management introduction and shared-menu round trip without replacing the later strategic chapter.",
        "C1 completion outcome drift",
    )

    for main in main_quests:
        realms = {v["realmId"] for v in main.get("realmVariants", [])}
        if main["id"] in VARIANT_QUESTS:
            require(realms == REALMS, f"realm variants incomplete: {main['id']}")
        else:
            require(not realms, f"unauthorized realm variants: {main['id']}")
    require(all(side["feedback"]["targetMainQuestId"] in main_ids for side in side_quests), "side feedback target missing")

    unique(objectives, "objective IDs")
    unique(legacy_refs, "legacy chapter references")
    require(len(objectives) == 158, "objective count drift")
    require(len(legacy_refs) == 29, "legacy reference count drift")
    require(set(MILESTONES).issubset(covered_milestones), "product milestone coverage incomplete")

    gems = manifest["realmGems"]
    unique([gem["id"] for gem in gems], "realm gem IDs")
    require(len(gems) == 8, "realm gem count drift")
    for gem in gems:
        add_localized(localization, gem["name"], f"gem {gem['id']} name")
        require("restored to its realm custodian" in gem["custodyRule"], f"gem return rule drift: {gem['id']}")
    for realm in REALMS:
        require(sum(gem["realmId"] == realm for gem in gems) == 2, f"gem count drift for {realm}")

    world = manifest["worldConstants"]
    require(world["wishDragon"]["id"] == "NPC_VAELORYN", "Wish Dragon identity drift")
    require(world["centerIsland"]["id"] == "LOCATION_ACCORDANT_ISLE", "center island identity drift")
    require(world["sharedLanguage"]["id"] == "EFFECT_DRAGONS_CONCORDANCE", "shared language identity drift")
    require(world["antagonist"]["id"] == "NPC_EDRAS_VEYR", "antagonist identity drift")
    add_localized(localization, world["veilWatch"]["name"], "Veil Watch name")
    add_localized(localization, world["wishDragon"]["name"], "Wish Dragon name")
    add_localized(localization, world["wishDragon"]["title"], "Wish Dragon title")
    add_localized(localization, world["centerIsland"]["name"], "center island name")
    add_localized(localization, world["sharedLanguage"]["name"], "shared language name")
    add_localized(localization, world["antagonist"]["name"], "antagonist name")
    add_localized(localization, world["antagonist"]["title"], "antagonist title")

    ending = manifest["ending"]
    require(ending["finalQuestId"] == "MQ_C14_FINAL_WISH", "final quest identity drift")
    require(len(ending["wishOptions"]) == 3, "wish option count drift")
    unique([option["id"] for option in ending["wishOptions"]], "wish option IDs")
    for option in ending["wishOptions"]:
        add_localized(localization, option["name"], f"wish {option['id']} name")
        add_localized(localization, option["epilogue"], f"wish {option['id']} epilogue")
        require(
            option["mechanicalDivergence"] == "none_beyond_cosmetic_and_epilogue_presentation",
            f"wish mechanics drift: {option['id']}",
        )
    require(all(ending["canonicalInvariants"].values()), "ending invariant drift")
    require(len(localization) == 415, "localization authority count drift")
    require(
        {key for key, value in localization.items() if value == BLOCKED_COPY_STATUS}
        == C1_BLOCKED_LOCALIZATION_KEYS,
        "copy-blocked localization inventory drift",
    )

    require(chapter_by_id["CH08_GATE_UNSEALED"]["order"] < chapter_by_id["CH12_EIGHT_LIGHTS"]["order"], "gate order drift")
    require(chapter_by_id["CH12_EIGHT_LIGHTS"]["order"] < chapter_by_id["CH13_ACCORDANT_ISLE"]["order"], "isle order drift")
    require(chapter_by_id["CH13_ACCORDANT_ISLE"]["order"] < chapter_by_id["CH14_FINAL_WISH"]["order"], "final order drift")

    return {
        "components": 15,
        "chapters": len(chapters),
        "mainQuests": len(main_quests),
        "sideQuests": len(side_quests),
        "objectives": len(objectives),
        "realmGems": len(gems),
        "localizationAuthorities": len(localization),
        "copyBlockedAuthorities": sum(value == BLOCKED_COPY_STATUS for value in localization.values()),
    }


def run_negative_fixtures(manifest: dict[str, Any], chapters: list[dict[str, Any]]) -> int:
    cases = [
        ("duplicate main ID", lambda m, c: c[1]["mainQuest"].__setitem__("id", c[0]["mainQuest"]["id"])),
        ("chapter order gap", lambda m, c: c[2].__setitem__("order", 99)),
        ("side quest removed", lambda m, c: c[0].__setitem__("sideQuests", c[0]["sideQuests"][:1])),
        ("side quest not optional", lambda m, c: c[0]["sideQuests"][0].__setitem__("optional", False)),
        ("side quest gates main", lambda m, c: c[0]["sideQuests"][0]["feedback"].__setitem__("requiredForCriticalPath", True)),
        ("side reward boundary drift", lambda m, c: c[0]["sideQuests"][0].__setitem__("rewardBoundary", "critical_path_unlock")),
        ("only seven gems", lambda m, c: m.__setitem__("realmGems", m["realmGems"][:7])),
        ("realm gem imbalance", lambda m, c: m["realmGems"][0].__setitem__("realmId", "STONEHOLD")),
        ("milestone removed", lambda m, c: m.__setitem__("requiredProductMilestones", m["requiredProductMilestones"][:-1])),
        ("localization duplicated", lambda m, c: c[0]["sideQuests"][0]["title"].__setitem__("key", c[0]["title"]["key"])),
        ("ending invariant false", lambda m, c: m["ending"]["canonicalInvariants"].__setitem__("allEightGemsReturned", False)),
        ("realm variant removed", lambda m, c: c[1]["mainQuest"].__setitem__("realmVariants", c[1]["mainQuest"]["realmVariants"][:3])),
        ("main chain broken", lambda m, c: c[1]["mainQuest"].__setitem__("prerequisites", ["MISSING"])),
        ("OMEN authority version drift", lambda m, c: m["authorities"]["prologueQuest"].__setitem__("packetVersion", "omen1-a1-2026-07-29-v003")),
        ("C1 objective order drift", lambda m, c: c[1]["mainQuest"]["objectives"].reverse()),
        ("C1 copy status approved without copy", lambda m, c: c[1]["mainQuest"]["objectives"][4]["text"].__setitem__("copyStatus", "APPROVED")),
        ("C1 blocked objective gains text", lambda m, c: c[1]["mainQuest"]["objectives"][4]["text"].__setitem__("text", "Unauthorized copy")),
        ("C1 Kingdom handoff removed", lambda m, c: c[1]["mainQuest"].__setitem__("handoffs", c[1]["mainQuest"]["handoffs"][:-1])),
        ("C1 pre-appointment summary restored", lambda m, c: c[1]["mainQuest"]["summary"].__setitem__("text", "Restore a failing realm covenant, face the realm guardian, and earn the right to lead beyond ceremonial title.")),
    ]
    for name, mutate in cases:
        manifest_copy = copy.deepcopy(manifest)
        chapters_copy = copy.deepcopy(chapters)
        mutate(manifest_copy, chapters_copy)
        try:
            validate_model(manifest_copy, chapters_copy)
        except ValidationError:
            continue
        raise ValidationError(f"negative fixture accepted: {name}")
    return len(cases)


def main() -> int:
    default_manifest = Path(__file__).resolve().parents[2] / "unity/Docs/Narrative/MainQuestLine/ANOTHERLIFE_MAIN_QUEST_LINE.packet.json"
    manifest_path = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else default_manifest
    manifest, chapters = load_packet_set(manifest_path)
    counts = validate_model(manifest, chapters)
    negatives = run_negative_fixtures(manifest, chapters)
    print(
        "Main quest packet accepted: "
        + ", ".join(f"{key}={value}" for key, value in counts.items())
        + f", negativeFixtures={negatives}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
