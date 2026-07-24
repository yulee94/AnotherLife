#!/usr/bin/env python3
"""Validate the canonical-candidate AnotherLife class identity packet."""

from __future__ import annotations

import copy
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any

PACKET_VERSION = "anotherlife-class-identity-skill-trees-2026-07-24-v001"
REALMS = ["STONEHOLD", "ELDERGROVE", "CROWNLANDS", "UMBRAL"]
MILESTONES = [10, 20, 30, 40, 50]
PIECE_SLOTS = [
    "weapon",
    "helm",
    "chest",
    "gloves",
    "boots",
    "cape",
    "ring",
    "amulet",
    "mount_armor",
    "class_relic",
]
FAMILY_MAPPING = {
    "family_warrior": [
        "class_vanguard",
        "class_guardian",
        "class_berserker",
        "class_paladin",
    ],
    "family_mage": [
        "class_pyromancer",
        "class_cryomancer",
        "class_archmage",
        "class_necromancer",
    ],
    "family_ranger": [
        "class_sharpshooter",
        "class_stalker",
        "class_beastmaster",
        "class_druid",
    ],
    "family_assassin": [
        "class_shadowblade",
        "class_infiltrator",
        "class_nightstalker",
        "class_slayer",
    ],
}
FAMILY_ENUMS = {
    "family_warrior": ("Warrior", 0),
    "family_mage": ("Mage", 1),
    "family_ranger": ("Ranger", 2),
    "family_assassin": ("Assassin", 3),
}
CLASS_ENUMS = {
    "class_vanguard": ("Vanguard", 1),
    "class_guardian": ("Guardian", 2),
    "class_berserker": ("Berserker", 3),
    "class_pyromancer": ("Pyromancer", 4),
    "class_cryomancer": ("Cryomancer", 5),
    "class_archmage": ("Archmage", 6),
    "class_sharpshooter": ("Sharpshooter", 7),
    "class_stalker": ("Stalker", 8),
    "class_beastmaster": ("Beastmaster", 9),
    "class_shadowblade": ("Shadowblade", 10),
    "class_infiltrator": ("Infiltrator", 11),
    "class_nightstalker": ("Nightstalker", 12),
    "class_paladin": ("Paladin", 13),
    "class_necromancer": ("Necromancer", 14),
    "class_slayer": ("Slayer", 15),
    "class_druid": ("Druid", 16),
}
EXPECTED_COUNTS = {
    "components": 4,
    "families": 4,
    "classes": 16,
    "branches": 48,
    "milestoneSkills": 80,
    "masteryTrials": 16,
    "warmasterSets": 16,
    "warmasterRelics": 16,
    "trueWarmasterSkills": 16,
    "localizedNames": 244,
    "legacyVisualLabels": 12,
    "forgePresets": 9,
    "prototypeSkills": 4,
}
EXPECTED_VISUAL_LABELS = {
    "Guardian",
    "Barbarian",
    "Swordsman",
    "Enchanter",
    "Warlock",
    "Healer",
    "Hunter",
    "Marksman",
    "Forest Ranger",
    "Nightmare",
    "Shadow Assassin",
    "Cursor",
}
EXPECTED_FORGE_PRESET_IDS = {
    "vanguard",
    "arcanist",
    "nightblade",
    "dreadknight",
    "oracle",
    "duelist",
    "inquisitor",
    "warden",
    "spellblade",
}
EXPECTED_PROTOTYPE_SKILL_IDS = {
    "realm_strike",
    "renewing_guard",
    "warzone_burst",
    "warmaster_breaker",
}
STABLE_ID = re.compile(r"^[a-z][a-z0-9_]*$")
ALLOWED_COMPONENT_NUMBER_PATHS = {
    "components.[].schemaVersion",
    "components.[].family.legacyEnum.value",
    "components.[].family.classes.[].legacySubclass.value",
    "components.[].family.classes.[].milestones.[].level",
}
COUNTERPLAY_RESPONSE_TERMS = (
    "avoid",
    "break",
    "bypass",
    "cleanse",
    "cover",
    "destruct",
    "dispel",
    "disrupt",
    "dodge",
    "evacuat",
    "focus",
    "interrupt",
    "leave",
    "line-of-sight",
    "movement",
    "outmaneuver",
    "recover",
    "resistan",
    "target",
    "wait",
)


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


def stable_id(value: Any, context: str) -> str:
    identifier = nonblank(value, context)
    require(bool(STABLE_ID.fullmatch(identifier)), f"{context} is not lower-snake ASCII: {identifier}")
    return identifier


def collect_localized(value: Any, authority: dict[str, str], context: str) -> None:
    if isinstance(value, dict):
        if set(value) == {"key", "text"}:
            key = nonblank(value["key"], f"{context} localization key")
            text = nonblank(value["text"], f"{context} localization text")
            require(key not in authority, f"duplicate localization authority: {key}")
            authority[key] = text
            return
        for key, child in value.items():
            collect_localized(child, authority, f"{context}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            collect_localized(child, authority, f"{context}[{index}]")


def reject_unapproved_component_numbers(value: Any, path: tuple[str | int, ...]) -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            reject_unapproved_component_numbers(child, (*path, key))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            reject_unapproved_component_numbers(child, (*path, index))
    elif isinstance(value, (int, float)) and not isinstance(value, bool):
        normalized_path = ".".join("[]" if isinstance(part, int) else part for part in path)
        require(
            normalized_path in ALLOWED_COMPONENT_NUMBER_PATHS,
            f"gameplay tuning number is out of narrative scope: {normalized_path}",
        )
        require(isinstance(value, int), f"schema/index number must be an integer: {normalized_path}")


def require_localized_key(value: dict[str, Any], expected: str, context: str) -> None:
    require(value["key"] == expected, f"{context} localization ownership drift")
    nonblank(value["text"], f"{context} localization text")


def owned_suffix(identifier: str, prefix: str, context: str) -> str:
    require(identifier.startswith(prefix), f"{context} ID ownership drift")
    suffix = identifier[len(prefix) :]
    require(bool(suffix), f"{context} ID has no owned suffix")
    return suffix


def require_true_warmaster_eligibility(value: Any, context: str) -> str:
    eligibility = nonblank(value, context)
    normalized = eligibility.casefold()
    for phrase in (
        "level 50",
        "approved realm contract",
        "warzone points",
        "ten unique valid pieces",
    ):
        require(phrase in normalized, f"{context} omits {phrase}")
    return eligibility


def require_counterplay(value: Any, context: str) -> str:
    counterplay = nonblank(value, context)
    require(len(counterplay.strip()) >= 80, f"{context} is not implementation-useful prose")
    normalized = counterplay.casefold()
    require(
        any(term in normalized for term in COUNTERPLAY_RESPONSE_TERMS),
        f"{context} has no explicit opponent response mechanism",
    )
    return counterplay


def load_packet_set(manifest_path: Path) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    repo_root = manifest_path.resolve().parents[4]
    components: list[dict[str, Any]] = []

    for metadata in manifest["components"]:
        component_path = repo_root / metadata["path"]
        require(component_path.is_file(), f"missing component: {metadata['path']}")
        raw = component_path.read_bytes()
        canonical_raw = raw.replace(b"\r\n", b"\n")
        require(
            hashlib.sha256(canonical_raw).hexdigest() == metadata["sha256"],
            f"component hash mismatch: {metadata['path']}",
        )
        component = json.loads(canonical_raw.decode("utf-8"))
        require(component["componentId"] == metadata["componentId"], f"component ID mismatch: {metadata['path']}")
        require(component["family"]["id"] == metadata["familyId"], f"component family mismatch: {metadata['path']}")
        components.append(component)

    return manifest, components


def validate_model(manifest: dict[str, Any], components: list[dict[str, Any]]) -> dict[str, int]:
    require(manifest["schemaVersion"] == 1, "unsupported manifest schema")
    require(manifest["packetVersion"] == PACKET_VERSION, "packet version drift")
    require(manifest["packetId"] == "ANOTHERLIFE_CLASS_IDENTITY_SKILL_TREES", "packet ID drift")
    require(manifest["primaryMode"] == "codex_narrative_content", "primary mode drift")
    require(
        manifest["sourceStatus"]
        == "codex_authored_canonical_candidate_ready_for_user_creative_acceptance_runtime_not_wired",
        "source status drift",
    )
    require(manifest["expectedCounts"] == EXPECTED_COUNTS, "expected count contract drift")
    require(manifest["canonicalDecisions"]["familyMapping"] == FAMILY_MAPPING, "canonical family mapping drift")
    require(
        manifest["canonicalDecisions"]["treeTopology"]["milestoneLevels"] == MILESTONES,
        "milestone policy drift",
    )
    require(
        manifest["canonicalDecisions"]["trueWarmasterPolicy"]["pieceSlots"] == PIECE_SLOTS,
        "Warmaster piece-slot policy drift",
    )
    require_true_warmaster_eligibility(
        manifest["canonicalDecisions"]["trueWarmasterPolicy"]["eligibility"],
        "Warmaster policy eligibility",
    )
    require(len(components) == 4, "component count drift")

    class_index = manifest["classIndex"]
    require(len(class_index) == 16, "class index count drift")
    require([entry["order"] for entry in class_index] == list(range(16)), "class index order drift")
    index_by_id = {entry["id"]: entry for entry in class_index}
    require(set(index_by_id) == set(CLASS_ENUMS), "class index identity drift")

    family_ids: list[str] = []
    class_ids: list[str] = []
    branch_ids: list[str] = []
    skill_ids: list[str] = []
    trial_ids: list[str] = []
    trial_aliases: list[str] = []
    resource_ids: list[str] = []
    title_ids: list[str] = []
    set_ids: list[str] = []
    relic_ids: list[str] = []
    ultimate_ids: list[str] = []
    localization: dict[str, str] = {}
    class_by_id: dict[str, dict[str, Any]] = {}

    reject_unapproved_component_numbers(components, ("components",))

    for component in components:
        require(component["schemaVersion"] == 1, f"component schema drift: {component['componentId']}")
        require(
            component["parentPacketVersion"] == PACKET_VERSION,
            f"component parent version drift: {component['componentId']}",
        )
        family = component["family"]
        family_id = stable_id(family["id"], "family ID")
        family_ids.append(family_id)
        require(family_id in FAMILY_MAPPING, f"unknown family: {family_id}")
        require(
            (family["legacyEnum"]["name"], family["legacyEnum"]["value"]) == FAMILY_ENUMS[family_id],
            f"family enum mapping drift: {family_id}",
        )
        family_token = family_id.removeprefix("family_")
        require_localized_key(
            family["name"],
            f"class_family.{family_token}.name",
            f"family {family_id} name",
        )
        require(family["realmAvailability"] == REALMS, f"realm availability drift: {family_id}")
        classes = family["classes"]
        require(len(classes) == 4, f"family does not contain four classes: {family_id}")
        require(family["classIds"] == [item["id"] for item in classes], f"family class index drift: {family_id}")
        require(family["classIds"] == FAMILY_MAPPING[family_id], f"canonical family membership drift: {family_id}")
        collect_localized(family["name"], localization, f"family {family_id} name")

        for class_record in classes:
            class_id = stable_id(class_record["id"], "class ID")
            class_token = class_id.removeprefix("class_")
            class_ids.append(class_id)
            class_by_id[class_id] = class_record
            require(class_id in CLASS_ENUMS, f"unknown class: {class_id}")
            require(
                (class_record["legacySubclass"]["name"], class_record["legacySubclass"]["value"])
                == CLASS_ENUMS[class_id],
                f"class enum mapping drift: {class_id}",
            )
            indexed = index_by_id[class_id]
            require(indexed["familyId"] == family_id, f"class index family drift: {class_id}")
            require(indexed["legacyEnumName"] == CLASS_ENUMS[class_id][0], f"class index enum-name drift: {class_id}")
            require(indexed["legacyEnumValue"] == CLASS_ENUMS[class_id][1], f"class index enum-value drift: {class_id}")
            require(indexed["componentId"] == component["componentId"], f"class index component drift: {class_id}")
            require(
                indexed["displayName"] == class_record["name"]["text"],
                f"class index display-name drift: {class_id}",
            )
            require_localized_key(
                class_record["name"],
                f"class.{class_token}.name",
                f"class {class_id} name",
            )
            nonblank(class_record["identity"], f"class identity {class_id}")
            nonblank(class_record["roles"]["primary"], f"primary role {class_id}")
            require(len(class_record["roles"]["secondary"]) >= 2, f"secondary-role coverage drift: {class_id}")
            require(len(class_record["roles"]["contribution"]) >= 4, f"contribution coverage drift: {class_id}")
            require(class_record["equipmentIdentity"]["mainHand"], f"main-hand identity missing: {class_id}")
            require(class_record["equipmentIdentity"]["offHand"], f"off-hand identity missing: {class_id}")

            resource = class_record["resource"]
            resource_id = stable_id(resource["id"], f"resource ID {class_id}")
            resource_ids.append(resource_id)
            resource_suffix = owned_suffix(
                resource_id,
                f"class_resource_{class_token}_",
                f"resource {class_id}",
            )
            require_localized_key(
                resource["name"],
                f"class_resource.{class_token}.{resource_suffix}.name",
                f"resource {resource_id}",
            )
            nonblank(resource["gain"], f"resource gain {class_id}")
            nonblank(resource["spend"], f"resource spend {class_id}")

            branches = class_record["branches"]
            require(len(branches) == 3, f"class does not contain three branches: {class_id}")
            for branch in branches:
                branch_id = stable_id(branch["id"], f"branch ID {class_id}")
                branch_ids.append(branch_id)
                branch_suffix = owned_suffix(
                    branch_id,
                    f"skill_branch_{class_token}_",
                    f"branch {class_id}",
                )
                require_localized_key(
                    branch["name"],
                    f"skill_branch.{class_token}.{branch_suffix}.name",
                    f"branch {branch_id}",
                )
                nonblank(branch["identity"], f"branch identity {class_id}")

            milestones = class_record["milestones"]
            require([item["level"] for item in milestones] == MILESTONES, f"milestone levels drift: {class_id}")
            for milestone in milestones:
                skill_id = stable_id(milestone["skillId"], f"milestone skill ID {class_id}")
                skill_ids.append(skill_id)
                skill_suffix = owned_suffix(
                    skill_id,
                    f"skill_{class_token}_",
                    f"milestone skill {class_id}",
                )
                require_localized_key(
                    milestone["name"],
                    f"skill.{class_token}.{skill_suffix}.name",
                    f"milestone skill {skill_id}",
                )
                nonblank(milestone["identity"], f"milestone identity {class_id}")

            trial = class_record["masteryTrial"]
            trial_id = stable_id(trial["id"], f"trial ID {class_id}")
            trial_ids.append(trial_id)
            trial_suffix = owned_suffix(
                trial_id,
                f"class_trial_{class_token}_",
                f"mastery trial {class_id}",
            )
            require_localized_key(
                trial["name"],
                f"class_trial.{class_token}.{trial_suffix}.name",
                f"mastery trial {trial_id}",
            )
            expected_alias = f"SQ_{CLASS_ENUMS[class_id][0]}"
            require(trial["legacyAlias"] == expected_alias, f"legacy trial alias drift: {class_id}")
            trial_aliases.append(trial["legacyAlias"])
            require(trial["name"]["text"] == milestones[-1]["name"]["text"], f"trial/capstone name drift: {class_id}")
            require("does not grant or gate" in trial["boundary"], f"trial gate boundary drift: {class_id}")

            warmaster = class_record["warmaster"]
            title_id = stable_id(warmaster["titleId"], f"Warmaster title ID {class_id}")
            set_id = stable_id(warmaster["setId"], f"Warmaster set ID {class_id}")
            relic_id = stable_id(warmaster["relicId"], f"Warmaster relic ID {class_id}")
            ultimate_id = stable_id(warmaster["ultimateSkillId"], f"Warmaster skill ID {class_id}")
            title_ids.append(title_id)
            set_ids.append(set_id)
            relic_ids.append(relic_id)
            ultimate_ids.append(ultimate_id)
            title_suffix = owned_suffix(
                title_id,
                f"warmaster_title_{class_token}_",
                f"Warmaster title {class_id}",
            )
            set_suffix = owned_suffix(
                set_id,
                f"warmaster_set_{class_token}_",
                f"Warmaster set {class_id}",
            )
            relic_suffix = owned_suffix(
                relic_id,
                f"warmaster_relic_{class_token}_",
                f"Warmaster relic {class_id}",
            )
            ultimate_suffix = owned_suffix(
                ultimate_id,
                f"skill_{class_token}_true_warmaster_",
                f"Warmaster skill {class_id}",
            )
            require_localized_key(
                warmaster["title"],
                f"warmaster_title.{class_token}.{title_suffix}.name",
                f"Warmaster title {title_id}",
            )
            require_localized_key(
                warmaster["setName"],
                f"warmaster_set.{class_token}.{set_suffix}.name",
                f"Warmaster set {set_id}",
            )
            require_localized_key(
                warmaster["relicName"],
                f"warmaster_relic.{class_token}.{relic_suffix}.name",
                f"Warmaster relic {relic_id}",
            )
            require_localized_key(
                warmaster["ultimateName"],
                f"skill.{class_token}.true_warmaster.{ultimate_suffix}.name",
                f"Warmaster skill {ultimate_id}",
            )
            require(warmaster["pieceSlots"] == PIECE_SLOTS, f"Warmaster piece slots drift: {class_id}")
            nonblank(warmaster["identity"], f"Warmaster identity {class_id}")
            require_counterplay(warmaster["counterplay"], f"Warmaster counterplay {class_id}")
            require_true_warmaster_eligibility(
                warmaster["eligibility"],
                f"Warmaster eligibility {class_id}",
            )

            collect_localized(class_record, localization, f"class {class_id}")

    unique(family_ids, "family IDs")
    unique(class_ids, "class IDs")
    unique(branch_ids, "branch IDs")
    unique(skill_ids, "milestone skill IDs")
    unique(trial_ids, "mastery trial IDs")
    unique(trial_aliases, "mastery trial aliases")
    unique(resource_ids, "class resource IDs")
    unique(title_ids, "Warmaster title IDs")
    unique(set_ids, "Warmaster set IDs")
    unique(relic_ids, "Warmaster relic IDs")
    unique(ultimate_ids, "True Warmaster skill IDs")

    require(set(family_ids) == set(FAMILY_MAPPING), "family inventory drift")
    require(set(class_ids) == set(CLASS_ENUMS), "class inventory drift")
    require(class_by_id["class_druid"]["roles"]["primary"] == "healer", "Druid primary-healer decision drift")
    require("healer" in class_by_id["class_paladin"]["roles"]["secondary"], "Paladin healer role drift")
    require(
        class_by_id["class_necromancer"]["roles"]["primary"] != "healer"
        and "healer" not in class_by_id["class_necromancer"]["roles"]["secondary"],
        "Necromancer non-healer decision drift",
    )
    require(manifest["canonicalDecisions"]["supportCoverage"]["primaryHealer"] == "class_druid", "healer policy drift")
    require(
        manifest["canonicalDecisions"]["supportCoverage"]["secondaryHealers"]
        == ["class_paladin"],
        "secondary-healer policy drift",
    )

    visual_labels = manifest["legacyDispositions"]["visualClassLabels"]
    forge_presets = manifest["legacyDispositions"]["forgePresets"]
    prototype_skills = manifest["legacyDispositions"]["prototypeSkills"]
    require(len(visual_labels) == 12, "legacy visual-label count drift")
    require(len(forge_presets) == 9, "Forge preset count drift")
    require(len(prototype_skills) == 4, "prototype skill count drift")
    unique([item["legacyLabel"] for item in visual_labels], "legacy visual labels")
    unique([item["presetId"] for item in forge_presets], "Forge preset IDs")
    unique([item["id"] for item in prototype_skills], "prototype skill IDs")
    require(
        {item["legacyLabel"] for item in visual_labels} == EXPECTED_VISUAL_LABELS,
        "legacy visual-label inventory drift",
    )
    require(
        {item["presetId"] for item in forge_presets} == EXPECTED_FORGE_PRESET_IDS,
        "Forge preset inventory drift",
    )
    require(
        {item["id"] for item in prototype_skills} == EXPECTED_PROTOTYPE_SKILL_IDS,
        "prototype skill inventory drift",
    )
    require(
        next(item for item in visual_labels if item["legacyLabel"] == "Cursor")["disposition"]
        == "rejected_or_superseded",
        "Cursor disposition drift",
    )
    require(
        all("prototype_only" in item["disposition"] for item in prototype_skills),
        "prototype skill authority drift",
    )

    require(len(localization) == 244, "localized-name authority count drift")
    counts = {
        "components": len(components),
        "families": len(family_ids),
        "classes": len(class_ids),
        "branches": len(branch_ids),
        "milestoneSkills": len(skill_ids),
        "masteryTrials": len(trial_ids),
        "warmasterSets": len(set_ids),
        "warmasterRelics": len(relic_ids),
        "trueWarmasterSkills": len(ultimate_ids),
        "localizedNames": len(localization),
        "legacyVisualLabels": len(visual_labels),
        "forgePresets": len(forge_presets),
        "prototypeSkills": len(prototype_skills),
    }
    require(counts == EXPECTED_COUNTS, "validated counts do not match the manifest contract")
    return counts


def run_negative_fixtures(manifest: dict[str, Any], components: list[dict[str, Any]]) -> int:
    cases = [
        (
            "duplicate class ID",
            lambda m, c: c[0]["family"]["classes"][1].__setitem__(
                "id", c[0]["family"]["classes"][0]["id"]
            ),
        ),
        (
            "fourth branch removed",
            lambda m, c: c[0]["family"]["classes"][0].__setitem__(
                "branches", c[0]["family"]["classes"][0]["branches"][:2]
            ),
        ),
        (
            "milestone level drift",
            lambda m, c: c[1]["family"]["classes"][0]["milestones"][2].__setitem__("level", 31),
        ),
        (
            "Warmaster piece duplicated",
            lambda m, c: c[2]["family"]["classes"][0]["warmaster"].__setitem__(
                "pieceSlots", ["weapon"] * 10
            ),
        ),
        (
            "mastery trial becomes gate",
            lambda m, c: c[3]["family"]["classes"][0]["masteryTrial"].__setitem__(
                "boundary", "Required for True Warmaster."
            ),
        ),
        (
            "Druid healer removed",
            lambda m, c: c[2]["family"]["classes"][3]["roles"].__setitem__(
                "primary", "area_damage"
            ),
        ),
        (
            "realm availability removed",
            lambda m, c: c[0]["family"].__setitem__("realmAvailability", REALMS[:3]),
        ),
        (
            "family mapping changed",
            lambda m, c: m["canonicalDecisions"]["familyMapping"]["family_warrior"].__setitem__(
                3, "class_slayer"
            ),
        ),
        (
            "localization key duplicated",
            lambda m, c: c[0]["family"]["classes"][1]["name"].__setitem__(
                "key", c[0]["family"]["classes"][0]["name"]["key"]
            ),
        ),
        (
            "numeric balance introduced",
            lambda m, c: c[0]["family"]["classes"][0]["milestones"][0].__setitem__(
                "damageMultiplier", 9.0
            ),
        ),
        (
            "class index display name drift",
            lambda m, c: m["classIndex"][0].__setitem__("displayName", "Not Vanguard"),
        ),
        (
            "foreign branch owner prefix",
            lambda m, c: c[0]["family"]["classes"][0]["branches"][0].__setitem__(
                "id", "skill_branch_paladin_linebreaker"
            ),
        ),
        (
            "localization key does not match owned ID",
            lambda m, c: c[0]["family"]["classes"][0]["resource"]["name"].__setitem__(
                "key", "class_resource.vanguard.orders.name"
            ),
        ),
        (
            "realm contract omitted from eligibility",
            lambda m, c: c[0]["family"]["classes"][0]["warmaster"].__setitem__(
                "eligibility",
                "Level 50, sufficient committed Warzone points, and all ten unique valid pieces of this class set.",
            ),
        ),
        (
            "counterplay has no response mechanism",
            lambda m, c: c[0]["family"]["classes"][0]["warmaster"].__setitem__(
                "counterplay",
                "The effect has a visible presentation and a clearly marked area for every participant.",
            ),
        ),
        (
            "secondary healer policy drift",
            lambda m, c: m["canonicalDecisions"]["supportCoverage"][
                "secondaryHealers"
            ].append("class_necromancer"),
        ),
        (
            "legacy visual-label inventory drift",
            lambda m, c: c
            and m["legacyDispositions"]["visualClassLabels"][0].__setitem__(
                "legacyLabel", "Guard"
            ),
        ),
        (
            "Forge preset inventory drift",
            lambda m, c: c
            and m["legacyDispositions"]["forgePresets"][0].__setitem__(
                "presetId", "fighter"
            ),
        ),
        (
            "prototype skill inventory drift",
            lambda m, c: c
            and m["legacyDispositions"]["prototypeSkills"][0].__setitem__(
                "id", "starter_strike"
            ),
        ),
    ]
    for name, mutate in cases:
        manifest_copy = copy.deepcopy(manifest)
        components_copy = copy.deepcopy(components)
        mutate(manifest_copy, components_copy)
        try:
            validate_model(manifest_copy, components_copy)
        except ValidationError:
            continue
        raise ValidationError(f"negative fixture accepted: {name}")
    return len(cases)


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    default_manifest = (
        repo_root
        / "unity/Docs/Narrative/Classes/ANOTHERLIFE_CLASS_IDENTITY_SKILL_TREES.packet.json"
    )
    manifest_path = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else default_manifest
    manifest, components = load_packet_set(manifest_path)
    counts = validate_model(manifest, components)
    negatives = run_negative_fixtures(manifest, components)
    print(
        "Class identity packet accepted: "
        + ", ".join(f"{key}={value}" for key, value in counts.items())
        + f", negativeFixtures={negatives}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
