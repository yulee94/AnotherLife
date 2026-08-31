#!/usr/bin/env python3
"""Build the held, source-traceable Umbral production taxonomy."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import subprocess
from collections import defaultdict
from pathlib import Path
from typing import Any

import build_eldergrove_realm_catalog as eldergrove_builder
import realm_character_taxonomy as contract

OUTPUT_REL = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_umbral_realm_character_taxonomy.json"
)

IDENTITY_DECISION = "rct_umbral_decision_identity_review_v001"
TECHNICAL_DECISION = "rct_umbral_decision_technical_review_v001"
PRESENTATION_DECISION = "rct_umbral_decision_motion_effect_review_v001"

SOURCE_SPECS = {
    "contract": (
        "unity/Docs/AssetLibrary/PostMVP_Realm_Character_Creature_Catalog_Contract_v1.md",
        "repo_document",
        "Shared taxonomy, mobile-floor context, provisional budgets, motion matrices, and gate policy.",
    ),
    "world_budget": (
        "unity/Docs/AssetLibrary/PostMVP_World_Asset_Budgets_And_Readability_v1.md",
        "repo_document",
        "Binding Galaxy A54 mobile-floor measurement context and protected-cue policy.",
    ),
    "design": (
        "DESIGN.md",
        "repo_document",
        "Global Umbral identity, production rules, technical baseline, and provisional model budgets.",
    ),
    "realm": (
        "unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json",
        "runtime_catalog",
        "Umbral Dark Elves, realm identity, claimant/agent/archivist/blade role vocabulary, and unresolved Void Seraph reference.",
    ),
    "customization": (
        "unity/Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json",
        "runtime_catalog",
        "Observed Umbral material and customization families; final implementation and production identity remain gated.",
    ),
    "classes": (
        "unity/Assets/AL/Scripts/Core/Enums/Enums.cs",
        "repo_document",
        "Compiled ClassFamily identifiers Warrior, Mage, Ranger, and Assassin.",
    ),
    "first_user": (
        "unity/Docs/Narrative/First_User_Playable_Spine_Source_Delta.md",
        "repo_document",
        "Realm-derived playable people and exact four selectable ClassFamily evidence; subclass and loadout authority excluded.",
    ),
    "champion_convergence": (
        "unity/Docs/GameDataCatalog/PhaseC/Phase_C7A_Champion_Authority_Convergence.md",
        "repo_document",
        "Champion definitions remain blocked; visual anchors and observed runtime rows do not create production identity.",
    ),
    "skill_convergence": (
        "unity/Docs/GameDataCatalog/PhaseC/Phase_C8A_Skill_Authority_Convergence.md",
        "repo_document",
        "Four exact preserved generic skill identities and explicit absence of complete production behavior/presentation authority.",
    ),
    "champions_observed": (
        "unity/Assets/AL/StreamingAssets/GameData/champions.json",
        "runtime_catalog",
        "Observed Umbral Shadowblade row and Shadowstep/Umbral Execute associations; migration evidence only.",
    ),
    "champion_runtime": (
        "unity/Assets/AL/StreamingAssets/GameData/champion_runtime.json",
        "runtime_catalog",
        "Current packaged Vex Nocturne / Umbral Shadowblade identity, Assassin assignment, twinblades/shroud equipment styles, and two observed skills.",
    ),
    "skills_observed": (
        "unity/Assets/AL/StreamingAssets/GameData/skills.json",
        "runtime_catalog",
        "Observed Shadowstep and Umbral Execute identities and profile references; production authority remains gated.",
    ),
    "approved_class_progression": (
        "unity/Assets/AL/StreamingAssets/GameData/al_stonehold_realm_character_taxonomy.json",
        "runtime_catalog",
        "Merged owner-approved cross-realm Level 1-50 class/subclass progression identities and class-family source references.",
    ),
    "skill_weather": (
        "unity/Assets/AL/StreamingAssets/GameData/al_skill_weather_catalog.json",
        "runtime_catalog",
        "Observed four generic loadout rows and provisional Umbral curse/debuff/stealth/poison/anti-heal palette plus ashfall environment.",
    ),
    "champion_anchor": (
        "unity/Assets/AL/Art/Designs/FourRealmChampionAnchor.md",
        "repo_document",
        "Owner-approved Umbral Vanguard visual direction; not class, model, rig, or runtime authority.",
    ),
    "modular_champion": (
        "unity/Assets/AL/Art/Designs/ModularChampionCustomization.md",
        "repo_document",
        "Shared modular construction direction and class-archetype visual precursors; production identity remains gated.",
    ),
    "champion_handoff": (
        "unity/Docs/champion-character-sheets-blender-handoff.v1.json",
        "repo_document",
        "Owner-approved Umbral Vanguard turnaround plus explicit exclusions and shared Humanoid modeling envelope.",
    ),
    "ecosystem": (
        "unity/Docs/Terrestrials/Ecosystems/Four_Realm_Ecosystem_And_Habitat_Source.md",
        "repo_document",
        "Complete four-family Umbral supporting-fauna roster, habitat mapping, motion intent, rig intent, and proposal state.",
    ),
    "terrestrial": (
        "unity/Docs/Terrestrials/Terrestrial_Design_Brief.md",
        "repo_document",
        "Global terrestrial originality, material, motion, LOD, and non-authority guardrails.",
    ),
    "boss_elite": (
        "unity/Docs/Terrestrials/RealmBossesAndElites/Realm_Boss_Elite_Design_Source.md",
        "repo_document",
        "Ashvein Triarch and three Umbral elite visual/motion/effect sources; combat skills and production approval explicitly excluded.",
    ),
    "dragon_unresolved": (
        "unity/Docs/Terrestrials/Cinematics/Cinematic_Terrestrial_Asset_Priority_and_Meshy_Authorization.md",
        "repo_document",
        "Explicitly identifies dragon_umbral_void_seraph as an unresolved realm-dragon reference with no visual-source selection.",
    ),
    "umbral_authoring": (
        "unity/Docs/Narrative/GameData/umbral-content-authoring-map.json",
        "repo_document",
        "Fail-closed Umbral authoring map: production eligibility is false and unsupported Champion identity/mechanics remain unavailable.",
    ),
}

ID_REPLACEMENTS = (
    ("rct_eldergrove", "rct_umbral"),
    ("npc_caretaker", "npc_claimant"),
    ("npc_hunter", "npc_archivist"),
    ("npc_oracle", "npc_agent"),
    ("npc_warden", "npc_blade"),
    ("grove_strider", "sootsail_carrioner"),
    ("mire_lumenback", "cinderplate_scarab"),
    ("moonshell_cicada", "graveglass_sheller"),
    ("thornburrow_hare", "ashstep_bounder"),
    ("hollowbark_stalker_low_pounce", "veilspine_widow_controlled_drop"),
    ("mere_root_leviathan_jaw_lunge", "ashvein_triarch_coordinated_neck_recoil"),
    ("mirrorfin_lurker_jaw_scoop", "cindermaw_salamander_jaw_surge"),
    ("sunmane_thornstag_bounding_charge", "gravewing_siphon_claw_brace"),
    ("hollowbark_stalker", "veilspine_widow"),
    ("mere_root_leviathan", "ashvein_triarch"),
    ("mirrorfin_lurker", "cindermaw_salamander"),
    ("moonbough_dragon", "void_seraph_dragon"),
    ("sunmane_thornstag", "gravewing_siphon"),
    ("quad_tall_browser", "avian_soarer"),
    ("amphibious_low", "invertebrate_six_limb"),
    ("invertebrate_winged", "gastropod_shell"),
    ("flexible_quadruped", "arachnid_high_unequal"),
    ("semi_aquatic_leviathan", "triarch_four_limb_winged"),
    ("amphibious_broad", "amphibious_lateral"),
    ("cervid_thornstag", "chiropteran_heavy"),
    ("skill_arcane_bolt", "skill_shadowstep"),
    ("skill_verdant_nova", "skill_umbral_execute"),
    ("arcane_bolt", "shadowstep"),
    ("verdant_nova", "umbral_execute"),
    ("equipment_back_attachment", "equipment_spike_attachment"),
    ("equipment_armor_trim", "equipment_fracture_accent"),
    ("equipment_cape", "equipment_assassin_cloak"),
    ("equipment_hood", "equipment_mask"),
    ("equipment_robe", "equipment_ash_cloth_layer"),
)

DISPLAY_REPLACEMENTS = (
    ("Eldergrove", "Umbral"),
    ("eldergrove", "umbral"),
    ("Arcane Bolt", "Shadowstep"),
    ("Verdant Nova", "Umbral Execute"),
    ("Hollowbark Stalker Low Pounce", "Veilspine Widow Controlled Drop"),
    ("Mere-Root Leviathan Jaw-Led Lunge", "Ashvein Triarch Coordinated Neck Recoil"),
    ("Mirrorfin Lurker Jaw Scoop", "Cindermaw Salamander Sudden Jaw Surge"),
    ("Sunmane Thornstag Bounding Charge", "Gravewing Siphon Claw Brace"),
)


def rid(kind: str, slug: str) -> str:
    return f"rct_umbral_{kind}_{slug}_v001"


def provenance_id(key: str) -> str:
    return rid("provenance", key)


def replace_tree(value: Any) -> Any:
    if isinstance(value, dict):
        return {key: replace_tree(item) for key, item in value.items()}
    if isinstance(value, list):
        return [replace_tree(item) for item in value]
    if isinstance(value, str):
        result = value
        for old, new in DISPLAY_REPLACEMENTS:
            result = result.replace(old, new)
        for old, new in ID_REPLACEMENTS:
            result = result.replace(old, new)
        return result
    return value


def by_id(rows: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    return {row["id"]: row for row in rows}


def source_commit(repo_root: Path, source_ref: str) -> str:
    result = subprocess.run(
        [
            "git",
            "-C",
            str(repo_root),
            "log",
            "-1",
            "--format=%H",
            "--",
            source_ref,
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    commit = result.stdout.strip()
    if len(commit) != 40 or any(character not in "0123456789abcdef" for character in commit):
        raise ValueError(f"No committed source revision resolves for {source_ref}")
    return commit


def source_blob_sha256(repo_root: Path, commit: str, source_ref: str) -> str:
    dirty_check = subprocess.run(
        ["git", "-C", str(repo_root), "diff", "--quiet", "HEAD", "--", source_ref],
        check=False,
    )
    if dirty_check.returncode != 0:
        raise ValueError(f"Source provenance requires a clean tracked input: {source_ref}")
    result = subprocess.run(
        ["git", "-C", str(repo_root), "show", f"{commit}:{source_ref}"],
        check=True,
        capture_output=True,
    )
    return hashlib.sha256(result.stdout).hexdigest()


def build_catalog(repo_root: Path) -> dict[str, Any]:
    catalog = replace_tree(eldergrove_builder.build_catalog(repo_root))
    catalog.update(
        {
            "catalogId": "rct_umbral_catalog_production_v001",
            "contentVersion": "1.0.0",
            "realmId": "umbral",
        }
    )

    catalog["provenance"] = []
    for key, (source_ref, source_kind, notes) in SOURCE_SPECS.items():
        source_path = repo_root / source_ref
        commit = source_commit(repo_root, source_ref)
        digest = source_blob_sha256(repo_root, commit, source_ref)
        lineage = ""
        if key == "umbral_authoring":
            authoring_map = json.loads(source_path.read_text(encoding="utf-8"))
            content_map = authoring_map["sources"]["contentMap"]
            lineage = (
                f" lineageSourceCommit={content_map['sourceCommit']};"
                f" lineageSourceBlobSha256={content_map['gitBlobSha256']};"
            )
        catalog["provenance"].append(
            {
                "id": provenance_id(key),
                "sourceKind": source_kind,
                "sourceRef": source_ref,
                "creator": "Another Life repository authors",
                "tool": "git-tracked source",
                "toolVersion": f"git commit {commit}",
                "createdAtUtc": None,
                "rightsState": "project_internal",
                "promptOrBriefRef": None,
                "sha256": digest,
                "notes": (
                    f"sourceCommit={commit}; sourceBlobSha256={digest};"
                    f"{lineage} {notes}"
                ),
            }
        )

    race = catalog["playableRaces"][0]
    race["displayName"] = "Umbral Dark Elves"
    race["customizationContractRef"] = (
        "unity/Assets/AL/StreamingAssets/GameData/"
        "al_character_customization_catalog.json#realms/Umbral"
    )

    npc_specs = {
        "claimant": ("Umbral Claimant", "civilian", ["role.claimant_presentation"]),
        "archivist": ("Umbral Archivist", "service", ["role.archive_service"]),
        "agent": ("Umbral Agent", "quest", ["role.quest_briefing"]),
        "blade": ("Umbral Blade", "combat", ["role.precision_defense"]),
    }
    npcs = by_id(catalog["npcArchetypes"])
    for slug, (display_name, role, actions) in npc_specs.items():
        row = npcs[rid("npc", slug)]
        row.update(
            {
                "displayName": display_name,
                "role": role,
                "roleActionKeys": actions,
            }
        )

    observed_skill_ids = {
        rid("skill", "shadowstep"),
        rid("skill", "umbral_execute"),
    }
    champions = by_id(catalog["championFamilies"])
    for slug in ("assassin", "mage", "ranger", "warrior"):
        family = champions[rid("champion", slug)]
        family["displayName"] = f"Umbral {slug.title()} Production Family"
        family["skillIds"] = sorted(
            skill_id
            for skill_id in family["skillIds"]
            if skill_id not in observed_skill_ids
        )
    champions[rid("champion", "assassin")]["skillIds"] = sorted(
        champions[rid("champion", "assassin")]["skillIds"] + list(observed_skill_ids)
    )
    assassin_family = champions[rid("champion", "assassin")]
    assassin_family["displayName"] = (
        "Umbral Assassin / Shadowblade Production Family — Vex Nocturne Observed"
    )
    assassin_family["classSourceIds"] = sorted(
        set(
            assassin_family["classSourceIds"]
            + [
                "champion_runtime:champion_umbral_shadowblade",
                "champion_runtime:display_name:Vex Nocturne",
                "champion_runtime:subclass_id:shadowblade",
                "champion_runtime:weapon_style_id:twinblades",
                "champion_runtime:offhand_style_id:shroud",
            ]
        )
    )
    umbral_authoring_id = provenance_id("umbral_authoring")
    for family in champions.values():
        family["authority"]["provenanceIds"] = sorted(
            set(family["authority"]["provenanceIds"] + [umbral_authoring_id])
        )
    champion_runtime_id = provenance_id("champion_runtime")
    assassin_family["authority"]["provenanceIds"] = sorted(
        set(assassin_family["authority"]["provenanceIds"] + [champion_runtime_id])
    )

    beast_specs = {
        "sootsail_carrioner": (
            "Sootsail Carrioner",
            "tdf_habitat_umbral_ashvein_three_fault_rift#tdf_fauna_umbral_sootsail_carrioner",
            ["walk", "run", "fly"],
            "avian_soarer",
        ),
        "cinderplate_scarab": (
            "Cinderplate Scarab",
            "tdf_habitat_umbral_cinder_runoff_shelf#tdf_fauna_umbral_cinderplate_scarab",
            ["crawl", "fly"],
            "invertebrate_six_limb",
        ),
        "ashstep_bounder": (
            "Ashstep Bounder",
            "tdf_habitat_umbral_ashwood_veil_ravine#tdf_fauna_umbral_ashstep_bounder",
            ["walk", "run"],
            "quad_hind_drive",
        ),
        "graveglass_sheller": (
            "Graveglass Sheller",
            "tdf_habitat_umbral_graveglass_cavern_vale#tdf_fauna_umbral_graveglass_sheller",
            ["crawl"],
            "gastropod_shell",
        ),
    }
    beasts = by_id(catalog["beastFamilies"])
    for slug, (display_name, habitat, locomotion, _) in beast_specs.items():
        beasts[rid("beast", slug)].update(
            {
                "displayName": display_name,
                "habitatSourceRef": habitat,
                "locomotionModes": locomotion,
            }
        )

    monster_specs = {
        "veilspine_widow": (
            "Veilspine Widow",
            "elite",
            "tdf_habitat_umbral_ashwood_veil_ravine#tdf_elite_umbral_veilspine_widow",
            ["walk", "run"],
            "arachnid_high_unequal",
        ),
        "ashvein_triarch": (
            "Ashvein Triarch",
            "boss",
            "tdf_habitat_umbral_ashvein_three_fault_rift#tdf_boss_umbral_ashvein_triarch",
            ["walk", "fly"],
            "triarch_four_limb_winged",
        ),
        "cindermaw_salamander": (
            "Cindermaw Salamander",
            "elite",
            "tdf_habitat_umbral_cinder_runoff_shelf#tdf_elite_umbral_cindermaw_salamander",
            ["walk", "swim"],
            "amphibious_lateral",
        ),
        "void_seraph_dragon": (
            "Void Seraph Realm Dragon (Unresolved Reference)",
            "boss",
            "unresolved_realm_dragon#dragon_umbral_void_seraph",
            ["walk", "fly"],
            "dragon_unresolved",
        ),
        "gravewing_siphon": (
            "Gravewing Siphon",
            "elite",
            "tdf_habitat_umbral_graveglass_cavern_vale#tdf_elite_umbral_gravewing_siphon",
            ["walk", "crawl", "fly"],
            "chiropteran_heavy",
        ),
    }
    monsters = by_id(catalog["monsterFamilies"])
    for slug, (display_name, rank, habitat, locomotion, _) in monster_specs.items():
        row = monsters[rid("monster", slug)]
        row.update(
            {
                "displayName": display_name,
                "rank": rank,
                "habitatSourceRef": habitat,
                "locomotionModes": locomotion,
                "bossTransitionKeys": ["boss.transition"] if rank == "boss" else [],
            }
        )

    rig_sources = {
        "avian_soarer": "tdf_rig_avian_soarer",
        "invertebrate_six_limb": "tdf_rig_invertebrate_six_limb",
        "quad_hind_drive": "tdf_rig_quad_hind_drive",
        "gastropod_shell": "tdf_rig_gastropod_shell",
        "arachnid_high_unequal": "owner-gated rig candidate derived from Veilspine Widow motion/anatomy source",
        "triarch_four_limb_winged": "owner-gated rig candidate derived from Ashvein Triarch anatomy source",
        "amphibious_lateral": "owner-gated rig candidate derived from Cindermaw Salamander anatomy source",
        "dragon_unresolved": "owner-gated unresolved realm-dragon rig candidate",
        "chiropteran_heavy": "owner-gated rig candidate derived from Gravewing Siphon anatomy source",
    }
    rigs = by_id(catalog["rigFamilies"])
    for slug, skeleton_source in rig_sources.items():
        row = rigs[rid("rig", slug)]
        row.update(
            {
                "displayName": f"Umbral {slug.replace('_', ' ').title()} Rig Candidate",
                "skeletonFamily": skeleton_source,
                "retargetGroup": f"umbral_{slug}_candidate",
            }
        )

    equipment_display = {
        "armor_chest": "Armor Chest",
        "fracture_accent": "Fracture Accent",
        "spike_attachment": "Spike Attachment",
        "belt": "Belt",
        "boots": "Boots",
        "assassin_cloak": "Assassin Cloak",
        "gloves": "Gloves",
        "mask": "Mask",
        "mount_anchor": "Mount Anchor",
        "pet_anchor": "Pet Anchor",
        "ash_cloth_layer": "Ash Cloth Layer",
        "weapon_main": "Main-Hand Weapon",
        "weapon_off": "Off-Hand Weapon",
    }
    for slug, display_name in equipment_display.items():
        by_id(catalog["equipmentModules"])[rid("equipment", slug)]["displayName"] = (
            f"Umbral {display_name} Family"
        )

    equipment_rows = by_id(catalog["equipmentModules"])
    twinblades = copy.deepcopy(equipment_rows[rid("equipment", "weapon_main")])
    twinblades.update(
        {
            "id": rid("equipment", "twinblades"),
            "displayName": "Vex Nocturne Observed Twinblades Equipment Family",
        }
    )
    shroud = copy.deepcopy(equipment_rows[rid("equipment", "weapon_off")])
    shroud.update(
        {
            "id": rid("equipment", "shroud"),
            "displayName": "Vex Nocturne Observed Shroud Equipment Family",
        }
    )
    for equipment in (twinblades, shroud):
        equipment["authority"]["provenanceIds"] = sorted(
            set(equipment["authority"]["provenanceIds"] + [champion_runtime_id])
        )
        catalog["equipmentModules"].append(equipment)
    assassin_family["equipmentModuleIds"] = sorted(
        set(
            assassin_family["equipmentModuleIds"]
            + [twinblades["id"], shroud["id"]]
        )
    )
    assassin_family["weaponFamilyIds"] = sorted(
        set(assassin_family["weaponFamilyIds"] + [twinblades["id"], shroud["id"]])
    )

    character_entities = {
        race["id"],
        *npcs,
        *champions,
    }
    body_rows = by_id(catalog["bodyModules"])
    curse_mark = copy.deepcopy(body_rows[rid("body", "character_ears")])
    curse_mark.update(
        {
            "id": rid("body", "character_curse_mark"),
            "displayName": "Umbral Character Curse Mark Module Family",
            "slot": "other",
        }
    )
    catalog["bodyModules"].append(curse_mark)
    for section in ("playableRaces", "npcArchetypes", "championFamilies"):
        for entity in catalog[section]:
            entity["bodyModuleIds"] = sorted(
                set(entity["bodyModuleIds"] + [curse_mark["id"]])
            )
    for equipment in catalog["equipmentModules"]:
        if set(equipment["compatibleEntityIds"]).issubset(character_entities):
            equipment["compatibleBodyModuleIds"] = sorted(
                set(equipment["compatibleBodyModuleIds"] + [curse_mark["id"]])
            )

    physics = by_id(catalog["secondaryPhysicsProfiles"])
    physics[rid("physics", "character_cloth")]["affectedIds"] = [
        "ash_cloth",
        "assassin_cloak",
        "armor_fracture_trim",
    ]
    physics[rid("physics", "character_hair")]["affectedIds"] = ["hair"]

    skills = by_id(catalog["skills"])
    skills[rid("skill", "shadowstep")].update(
        {
            "displayName": "Shadowstep",
            "externalSourceId": "skill_shadowstep",
            "sourceCatalogRef": "unity/Assets/AL/StreamingAssets/GameData/skills.json#skill_shadowstep",
            "subjectIds": [rid("champion", "assassin")],
        }
    )
    skills[rid("skill", "umbral_execute")].update(
        {
            "displayName": "Umbral Execute",
            "externalSourceId": "skill_umbral_execute",
            "sourceCatalogRef": "unity/Assets/AL/StreamingAssets/GameData/skills.json#skill_umbral_execute",
            "subjectIds": [rid("champion", "assassin")],
        }
    )
    for motion in catalog["motions"]:
        if motion["skillId"] in observed_skill_ids:
            motion["subjectIds"] = [rid("champion", "assassin")]
    for effect in catalog["vfxEffects"]:
        if set(effect["skillIds"]) & observed_skill_ids:
            effect["subjectIds"] = [rid("champion", "assassin")]
    creature_skill_specs = {
        "veilspine_widow_controlled_drop": (
            "Veilspine Widow Controlled Drop",
            "tdf_elite_umbral_veilspine_widow:controlled_drop",
            rid("monster", "veilspine_widow"),
        ),
        "ashvein_triarch_coordinated_neck_recoil": (
            "Ashvein Triarch Coordinated Neck Recoil",
            "tdf_boss_umbral_ashvein_triarch:coordinated_neck_recoil",
            rid("monster", "ashvein_triarch"),
        ),
        "cindermaw_salamander_jaw_surge": (
            "Cindermaw Salamander Sudden Jaw Surge",
            "tdf_elite_umbral_cindermaw_salamander:sudden_jaw_surge",
            rid("monster", "cindermaw_salamander"),
        ),
        "gravewing_siphon_claw_brace": (
            "Gravewing Siphon Claw Brace",
            "tdf_elite_umbral_gravewing_siphon:claw_brace",
            rid("monster", "gravewing_siphon"),
        ),
    }
    for slug, (display_name, external_id, subject_id) in creature_skill_specs.items():
        skills[rid("skill", slug)].update(
            {
                "displayName": display_name,
                "externalSourceId": external_id,
                "subjectIds": [subject_id],
                "sourceCatalogRef": "unity/Docs/Terrestrials/RealmBossesAndElites/Realm_Boss_Elite_Design_Source.md#visual-motion-only",
            }
        )

    creature_ids = set(beasts) | set(monsters)
    motion_authority = copy.deepcopy(
        next(
            motion["authority"]
            for motion in catalog["motions"]
            if motion["skillId"] is None
            and set(motion["subjectIds"]) & creature_ids
        )
    )
    catalog["motions"] = [
        motion
        for motion in catalog["motions"]
        if motion["skillId"] is not None
        or not (set(motion["subjectIds"]) & creature_ids)
    ]

    def add_creature_motion(
        prefix: str,
        display_name: str,
        subject_id: str,
        motion_key: str,
        rig_id: str,
    ) -> None:
        catalog["motions"].append(
            {
                "id": rid("motion", f"{prefix}_{motion_key.replace('.', '_')}"),
                "displayName": f"{display_name} {motion_key}",
                "authority": copy.deepcopy(motion_authority),
                "subjectIds": [subject_id],
                "skillId": None,
                "motionKey": motion_key,
                "skillPhase": None,
                "rigFamilyId": rig_id,
                "clipRef": None,
                "rootMotionMode": "in_place",
                "timingAuthority": "presentation_only",
                "eventMarkers": [],
            }
        )

    for slug, (display_name, _, locomotion, rig_slug) in beast_specs.items():
        for key in sorted(
            set(contract.BEAST_MOTIONS)
            | {f"locomotion.{mode}" for mode in locomotion}
        ):
            add_creature_motion(
                f"beast_{slug}",
                display_name,
                rid("beast", slug),
                key,
                rid("rig", rig_slug),
            )
    for slug, (display_name, rank, _, locomotion, rig_slug) in monster_specs.items():
        keys = set(
            contract.BOSS_MOTIONS if rank == "boss" else contract.MONSTER_MOTIONS
        )
        keys.update(f"locomotion.{mode}" for mode in locomotion)
        if rank == "boss":
            keys.add("boss.transition")
        for key in sorted(keys):
            add_creature_motion(
                f"monster_{slug}",
                display_name,
                rid("monster", slug),
                key,
                rid("rig", rig_slug),
            )

    role_action_by_subject = {
        rid("npc", slug): action
        for slug, (_, _, actions) in npc_specs.items()
        for action in actions
    }
    role_motions = [
        motion
        for motion in catalog["motions"]
        if motion["skillId"] is None
        and len(motion["subjectIds"]) == 1
        and motion["subjectIds"][0] in role_action_by_subject
        and motion["motionKey"].startswith("role.")
    ]
    for motion in role_motions:
        subject_id = motion["subjectIds"][0]
        action = role_action_by_subject[subject_id]
        npc_slug = subject_id.removeprefix("rct_umbral_npc_").removesuffix("_v001")
        motion.update(
            {
                "id": rid("motion", f"npc_{npc_slug}_{action.replace('.', '_')}"),
                "displayName": f"{npcs[subject_id]['displayName']} {action}",
                "motionKey": action,
            }
        )

    umbral_direction = (
        "Canonical or observed skill identity plus provisional Umbral curse, debuff, "
        "stealth, poison, anti-heal, and ashfall cues; final magical grammar remains unauthorized."
    )
    generic_and_observed = {
        rid("skill", slug)
        for slug in (
            "realm_strike",
            "renewing_guard",
            "warmaster_breaker",
            "warzone_burst",
            "shadowstep",
            "umbral_execute",
        )
    }
    for effect in catalog["vfxEffects"]:
        if set(effect["skillIds"]) & generic_and_observed:
            effect["source"] = umbral_direction

    gravewing_skill = rid("skill", "gravewing_siphon_claw_brace")
    removed_effect_ids = {
        effect["id"]
        for effect in catalog["vfxEffects"]
        if gravewing_skill in effect["skillIds"] and effect["category"] == "area"
    }
    catalog["vfxEffects"] = [
        effect
        for effect in catalog["vfxEffects"]
        if effect["id"] not in removed_effect_ids
    ]
    trace_by_skill = {
        row["skillId"]: row for row in catalog["skillTraceability"]
    }
    gravewing_area = trace_by_skill[gravewing_skill]["effects"]["area"]
    gravewing_area.update(
        {
            "state": "not_applicable",
            "recordIds": [],
            "rationale": "The source documents a physical claw brace and displaced cave material, not a persistent or bounded gameplay area.",
        }
    )

    packet_subjects: dict[str, set[str]] = defaultdict(set)
    for section_name in contract.SECTIONS:
        if section_name in {"provenance", "decisionPackets"}:
            continue
        for row in catalog[section_name]:
            for packet_id in row.get("authority", {}).get("decisionPacketIds", []):
                packet_subjects[packet_id].add(row["id"])
            for dimension in row.get("creativeDecisions", {}).values():
                for packet_id in dimension.get("decisionPacketIds", []):
                    packet_subjects[packet_id].add(row["id"])
            if section_name == "budgetProfiles":
                for _, budget_metric in contract._iter_metric_objects(row):
                    for packet_id in budget_metric.get("decisionPacketIds", []):
                        packet_subjects[packet_id].add(row["id"])
    packets = by_id(catalog["decisionPackets"])
    for packet_id, subjects in packet_subjects.items():
        packets[packet_id]["subjectIds"] = sorted(subjects)
    packets[IDENTITY_DECISION]["provenanceIds"] = sorted(
        set(
            packets[IDENTITY_DECISION]["provenanceIds"]
            + [umbral_authoring_id, champion_runtime_id]
        )
    )

    for section in contract.SECTIONS:
        catalog[section].sort(key=lambda row: row["id"].encode("utf-8"))
    catalog["skillTraceability"].sort(
        key=lambda row: row["skillId"].encode("utf-8")
    )
    return catalog


def render_catalog(repo_root: Path) -> str:
    return json.dumps(build_catalog(repo_root), indent=2, ensure_ascii=False) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[3],
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Fail if the committed output differs.",
    )
    args = parser.parse_args()
    output_path = args.repo_root / OUTPUT_REL
    rendered = render_catalog(args.repo_root)
    if args.check:
        if not output_path.exists() or output_path.read_text(encoding="utf-8") != rendered:
            print(f"FAIL: {OUTPUT_REL} is missing or stale")
            return 1
        print(f"PASS: {OUTPUT_REL} is byte-stable")
        return 0
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(rendered, encoding="utf-8", newline="\n")
    print(f"WROTE: {OUTPUT_REL}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
