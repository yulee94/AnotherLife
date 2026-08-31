#!/usr/bin/env python3
"""Build the held, source-traceable Eldergrove production taxonomy."""

from __future__ import annotations

import argparse
import hashlib
import json
from collections import defaultdict
from pathlib import Path
from typing import Any

import realm_character_taxonomy as contract

OUTPUT_REL = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_eldergrove_realm_character_taxonomy.json"
)

IDENTITY_DECISION = "rct_eldergrove_decision_identity_review_v001"
TECHNICAL_DECISION = "rct_eldergrove_decision_technical_review_v001"
PRESENTATION_DECISION = "rct_eldergrove_decision_motion_effect_review_v001"

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
        "Global Eldergrove identity, production rules, technical baseline, and provisional model budgets.",
    ),
    "realm": (
        "unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json",
        "runtime_catalog",
        "Eldergrove people, realm identity, role vocabulary, and unresolved realm-dragon identifier.",
    ),
    "customization": (
        "unity/Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json",
        "runtime_catalog",
        "Observed customization slots and Eldergrove material/customization focus; not final production identity.",
    ),
    "classes": (
        "unity/Assets/AL/Scripts/Core/Enums/Enums.cs",
        "repo_document",
        "Compiled ClassFamily identifiers Warrior, Mage, Ranger, and Assassin.",
    ),
    "first_user": (
        "unity/Docs/Narrative/First_User_Playable_Spine_Source_Delta.md",
        "repo_document",
        "Realm-derived Elves and exact four selectable ClassFamily evidence; subclass and loadout authority excluded.",
    ),
    "champion_convergence": (
        "unity/Docs/GameDataCatalog/PhaseC/Phase_C7A_Champion_Authority_Convergence.md",
        "repo_document",
        "Champion definitions remain blocked; visual anchors and observed runtime rows do not create production identity.",
    ),
    "skill_convergence": (
        "unity/Docs/GameDataCatalog/PhaseC/Phase_C8A_Skill_Authority_Convergence.md",
        "repo_document",
        "Four exact preserved skill identities and explicit absence of complete production behavior/presentation authority.",
    ),
    "champions_observed": (
        "unity/Assets/AL/StreamingAssets/GameData/champions.json",
        "runtime_catalog",
        "Observed Eldergrove archmage row and Arcane Bolt/Verdant Nova associations; migration evidence only.",
    ),
    "skills_observed": (
        "unity/Assets/AL/StreamingAssets/GameData/skills.json",
        "runtime_catalog",
        "Observed Arcane Bolt and Verdant Nova record identities and profile references; production authority remains gated.",
    ),
    "approved_class_progression": (
        "unity/Assets/AL/StreamingAssets/GameData/al_stonehold_realm_character_taxonomy.json",
        "runtime_catalog",
        "Merged owner-approved cross-realm Level 1-50 class/subclass progression identities and class-family source references.",
    ),
    "skill_weather": (
        "unity/Assets/AL/StreamingAssets/GameData/al_skill_weather_catalog.json",
        "runtime_catalog",
        "Observed four generic loadout rows and provisional Eldergrove healing/protection effect palette.",
    ),
    "champion_anchor": (
        "unity/Assets/AL/Art/Designs/FourRealmChampionAnchor.md",
        "repo_document",
        "Bounded Eldergrove Vanguard visual direction; not class, model, rig, or runtime authority.",
    ),
    "modular_champion": (
        "unity/Assets/AL/Art/Designs/ModularChampionCustomization.md",
        "repo_document",
        "Shared modular construction direction and class-archetype visual precursors; production identity remains gated.",
    ),
    "champion_handoff": (
        "unity/Docs/champion-character-sheets-blender-handoff.v1.json",
        "repo_document",
        "Owner-approved Eldergrove Vanguard turnaround plus explicit exclusions and shared Humanoid modeling envelope.",
    ),
    "ecosystem": (
        "unity/Docs/Terrestrials/Ecosystems/Four_Realm_Ecosystem_And_Habitat_Source.md",
        "repo_document",
        "Complete four-family Eldergrove supporting-fauna roster, habitat mapping, motion intent, rig intent, and approval state.",
    ),
    "terrestrial": (
        "unity/Docs/Terrestrials/Terrestrial_Design_Brief.md",
        "repo_document",
        "Grove Strider and Mire Lumenback design constraints, variants, scale, material, and motion intent.",
    ),
    "boss_elite": (
        "unity/Docs/Terrestrials/RealmBossesAndElites/Realm_Boss_Elite_Design_Source.md",
        "repo_document",
        "Eldergrove Leviathan and three elite visual/motion/effect sources; combat skills and production approval explicitly excluded.",
    ),
    "dragon_unresolved": (
        "unity/Docs/Terrestrials/Cinematics/Cinematic_Terrestrial_Asset_Priority_and_Meshy_Authorization.md",
        "repo_document",
        "Explicitly identifies dragon_eldergrove_moonbough as an unresolved realm-dragon reference with no visual-source selection.",
    ),
}


def rid(kind: str, slug: str, scope: str = "eldergrove") -> str:
    return f"rct_{scope}_{kind}_{slug}_v001"


def provenance_id(key: str) -> str:
    return rid("provenance", key)


def approved_authority(*source_keys: str) -> dict[str, Any]:
    return {
        "status": "approved_fact",
        "ownerStatus": "APPROVE",
        "provenanceIds": sorted(provenance_id(key) for key in source_keys),
        "decisionPacketIds": [],
        "approvalEvidenceRefs": [
            f"{SOURCE_SPECS[key][0]}#bounded-source-fact" for key in source_keys
        ],
    }


def gated_authority(packet_id: str, *source_keys: str) -> dict[str, Any]:
    return {
        "status": "owner_decision_required",
        "ownerStatus": "PENDING",
        "provenanceIds": sorted(provenance_id(key) for key in source_keys),
        "decisionPacketIds": [packet_id],
        "approvalEvidenceRefs": [],
    }


def gated_creative(packet_id: str, source_refs: list[str]) -> dict[str, Any]:
    return {
        key: {
            "state": "owner_decision_required",
            "summary": None,
            "sourceRefs": source_refs,
            "decisionPacketIds": [packet_id],
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


def metric(
    unit: str,
    value: float,
    source_ref: str,
    *,
    limit_kind: str = "maximum_inclusive",
    secondary: float | None = None,
) -> dict[str, Any]:
    return {
        "state": "documented_provisional",
        "limitKind": limit_kind,
        "value": value,
        "secondaryValue": secondary,
        "unit": unit,
        "sourceRefs": [source_ref],
        "decisionPacketIds": [],
        "rationale": "Preserves a documented starting point without promoting it to an approved production limit.",
    }


def unknown_metric(unit: str, rationale: str) -> dict[str, Any]:
    return {
        "state": "owner_decision_required",
        "limitKind": "owner_decision_required",
        "value": None,
        "secondaryValue": None,
        "unit": unit,
        "sourceRefs": [],
        "decisionPacketIds": [TECHNICAL_DECISION],
        "rationale": rationale,
    }


def budget_profile(
    group: str,
    tier: str,
    platform_id: str,
    scope_kinds: list[str],
    documented: dict[str, tuple[float, str, str, float | None]],
) -> dict[str, Any]:
    units = {
        "geometry.lod0Triangles": "triangles",
        "geometry.lod1ReductionPercent": "percent",
        "geometry.lod2ReductionPercent": "percent",
        "geometry.lod3ReductionPercent": "percent",
        "materials.materialSlots": "count",
        "materials.shaderPasses": "count",
        "textures.textureLongEdge": "pixels",
        "textures.residentTextureMemory": "mib",
        "bones.deformingBones": "count",
        "bones.influencesPerVertex": "count",
        "physics.simulatedBones": "count",
        "physics.clothVertices": "count",
        "physics.activeRigidbodies": "count",
        "animation.compressedMemoryTarget": "mib",
        "animation.compressedMemoryMaximum": "mib",
        "animation.clipCount": "count",
        "animation.runtimeAnimatorLayers": "layers",
        "vfx.liveParticles": "count",
        "vfx.transparentLayers": "layers",
        "vfx.overdrawCoveragePercent": "percent",
        "vfx.concurrentEffects": "count",
        "vfx.dynamicLights": "lights",
        "colliders.primitiveColliders": "count",
        "colliders.proxyTriangles": "triangles",
        "hitboxes.activeHitboxes": "count",
    }
    groups: dict[str, dict[str, Any]] = defaultdict(dict)
    for path, unit in units.items():
        section, name = path.split(".")
        if path in documented:
            value, limit_kind, source_ref, secondary = documented[path]
            groups[section][name] = metric(
                unit,
                value,
                source_ref,
                limit_kind=limit_kind,
                secondary=secondary,
            )
        else:
            groups[section][name] = unknown_metric(
                unit,
                f"The {group} {tier} {path} value requires owner-approved measurement evidence.",
            )
    return {
        "id": rid("budget", f"{group}_{tier}"),
        "displayName": f"Eldergrove {group.replace('_', ' ').title()} {tier.replace('_', ' ').title()} Budget",
        "authority": gated_authority(TECHNICAL_DECISION, "contract", "design", "boss_elite"),
        "platformProfileId": platform_id,
        "scopeKinds": scope_kinds,
        **groups,
    }


def entity_refs(
    group: str,
    body_ids: list[str],
    equipment_ids: list[str],
    rig_ids: list[str],
    face_ids: list[str],
    physics_ids: list[str],
    template_ids: list[str],
) -> dict[str, Any]:
    tiers = ("mobile_floor", "mobile_high", "pc_high")
    return {
        "creativeDecisions": gated_creative(
            IDENTITY_DECISION,
            [
                "unity/Docs/AssetLibrary/PostMVP_Realm_Character_Creature_Catalog_Contract_v1.md#creative-authority-boundary",
                "owner decision pending",
            ],
        ),
        "bodyModuleIds": body_ids,
        "equipmentModuleIds": equipment_ids,
        "rigFamilyIds": rig_ids,
        "facialSystemIds": face_ids,
        "secondaryPhysicsProfileIds": physics_ids,
        "lodProfileIds": [rid("lod", group)],
        "colliderProfileIds": [rid("collider", group)],
        "hitboxProfileIds": [rid("hitbox", group)],
        "platformVariantIds": [rid("platform", f"{group}_{tier}") for tier in tiers],
        "budgetProfileIds": [rid("budget", f"{group}_{tier}") for tier in tiers],
        "motionMatrixTemplateIds": template_ids,
    }


def motion_record(
    slug: str,
    display_name: str,
    subject_ids: list[str],
    motion_key: str,
    rig_id: str,
    *,
    skill_id: str | None = None,
    skill_phase: str | None = None,
) -> dict[str, Any]:
    return {
        "id": rid("motion", slug),
        "displayName": display_name,
        "authority": gated_authority(PRESENTATION_DECISION, "contract", "champion_anchor", "ecosystem", "boss_elite"),
        "subjectIds": subject_ids,
        "skillId": skill_id,
        "motionKey": motion_key,
        "skillPhase": skill_phase,
        "rigFamilyId": rig_id,
        "clipRef": None,
        "rootMotionMode": "gameplay_driven" if skill_id else "in_place",
        "timingAuthority": "presentation_only",
        "eventMarkers": [],
    }


def vfx_record(
    skill: dict[str, Any],
    category: str,
    budget_ids: list[str],
    source_direction: str,
) -> dict[str, Any]:
    skill_slug = skill["id"].removeprefix("rct_eldergrove_skill_").removesuffix("_v001")
    return {
        "id": rid("vfx", f"{skill_slug}_{category}"),
        "displayName": f"{skill['displayName']} {category.title()} Effect Requirement",
        "authority": gated_authority(PRESENTATION_DECISION, "contract", "skill_convergence", "skill_weather", "boss_elite"),
        "category": category,
        "subjectIds": skill["subjectIds"],
        "skillIds": [skill["id"]],
        "source": source_direction,
        "direction": "Owner-gated production direction; preserve timing, threat, target, and realm readability without inventing final magical grammar.",
        "timing": "Gameplay timing remains external; presentation must bind to reviewed gameplay events.",
        "area": "Use only the gameplay-authoritative affected area; never enlarge the effect to imply a different result.",
        "endState": "Deterministic cleanup removes transient presentation while preserving committed gameplay state.",
        "gameplayAuthorityRef": skill["timingAuthorityRef"],
        "qualityVariants": {
            "off": "Retain a non-color geometry, pose, material, or UI cue for the committed result.",
            "low": "Retain the protected telegraph/result cue with minimal particles and transparency.",
            "balanced": "Use the measured mobile-high presentation only after budget admission.",
            "high": "Add non-authoritative detail only after the protected cue and timing remain identical.",
        },
        "reducedMotionVariant": "Replace rapid pulses, camera impulses, and dense motion with a stable protected cue.",
        "offStateCue": "Silhouette, pose, decal, material-value, or UI state communicates the result without color or particles.",
        "budgetProfileIds": budget_ids,
    }


def build_catalog(repo_root: Path) -> dict[str, Any]:
    provenance = []
    for key, (source_ref, source_kind, notes) in SOURCE_SPECS.items():
        source_path = repo_root / source_ref
        digest = hashlib.sha256(source_path.read_bytes()).hexdigest()
        provenance.append(
            {
                "id": provenance_id(key),
                "sourceKind": source_kind,
                "sourceRef": source_ref,
                "creator": "Another Life repository authors",
                "tool": "git-tracked source",
                "toolVersion": "repository revision at catalog generation",
                "createdAtUtc": None,
                "rightsState": "project_internal",
                "promptOrBriefRef": None,
                "sha256": digest,
                "notes": notes,
            }
        )

    platform_profiles = [
        {
            "id": rid("platform", "mobile_floor"),
            "displayName": "Eldergrove Mobile Floor",
            "authority": approved_authority("contract", "world_budget"),
            "tier": "mobile_floor",
            "targetFps": 30,
            "hardwareFloor": "Galaxy A54 5G 6 GB / Exynos 1380 / Mali-G68 candidate; 2340x1080 landscape at 60 Hz; Vulkan",
            "qualityIntent": "Stable 30 FPS floor with combat, interaction, identity, and accessibility cues protected.",
        },
        {
            "id": rid("platform", "mobile_high"),
            "displayName": "Eldergrove High Mobile",
            "authority": gated_authority(TECHNICAL_DECISION, "contract", "design"),
            "tier": "mobile_high",
            "targetFps": 60,
            "hardwareFloor": None,
            "qualityIntent": "Optional scalable 60 FPS mobile tier; device floor and measured costs remain owner decisions.",
        },
        {
            "id": rid("platform", "pc_high"),
            "displayName": "Eldergrove High PC",
            "authority": gated_authority(TECHNICAL_DECISION, "contract", "design"),
            "tier": "pc_high",
            "targetFps": 60,
            "hardwareFloor": None,
            "qualityIntent": "Optional high-PC presentation; hardware floor and measured costs remain owner decisions.",
        },
    ]
    platform_by_tier = {row["tier"]: row["id"] for row in platform_profiles}

    source_design = "DESIGN.md#lines-673-705"
    source_contract_lod = "unity/Docs/AssetLibrary/PostMVP_Realm_Character_Creature_Catalog_Contract_v1.md#lines-185-205"
    source_elite = "unity/Docs/Terrestrials/RealmBossesAndElites/Realm_Boss_Elite_Design_Source.md#lines-328-340"

    def lod_metrics() -> dict[str, tuple[float, str, str, float | None]]:
        return {
            "geometry.lod1ReductionPercent": (40, "range_inclusive", source_contract_lod, 50),
            "geometry.lod2ReductionPercent": (70, "range_inclusive", source_contract_lod, 80),
            "geometry.lod3ReductionPercent": (90, "range_inclusive", source_contract_lod, 95),
            "bones.influencesPerVertex": (4, "maximum_inclusive", source_design, None),
        }

    budget_profiles = []
    budget_specs = {
        "character": ["playable_race", "npc", "champion", "body_module", "equipment_module"],
        "beast": ["beast", "body_module"],
        "elite": ["monster", "body_module"],
        "boss": ["boss", "body_module"],
    }
    for group, scopes in budget_specs.items():
        for tier in ("mobile_floor", "mobile_high", "pc_high"):
            values = lod_metrics()
            if group == "character" and tier == "mobile_floor":
                values.update(
                    {
                        "geometry.lod0Triangles": (60000, "maximum_inclusive", source_design, None),
                        "materials.materialSlots": (3, "maximum_inclusive", source_design, None),
                        "textures.textureLongEdge": (2048, "maximum_inclusive", source_design, None),
                        "bones.deformingBones": (90, "strictly_less_than", source_design, None),
                    }
                )
            elif group == "beast" and tier == "mobile_floor":
                values.update(
                    {
                        "geometry.lod0Triangles": (25000, "maximum_inclusive", source_design, None),
                        "materials.materialSlots": (2, "maximum_inclusive", source_design, None),
                        "textures.textureLongEdge": (1024, "target", source_design, None),
                    }
                )
            elif group == "elite":
                elite_values = {
                    "mobile_floor": (22000, 64, 2, 2048, 80, 0),
                    "mobile_high": (45000, 96, 3, 2048, 160, 1),
                    "pc_high": (75000, 128, 4, 4096, 320, 1),
                }[tier]
                values.update(
                    {
                        "geometry.lod0Triangles": (elite_values[0], "maximum_inclusive", source_elite, None),
                        "bones.deformingBones": (elite_values[1], "maximum_inclusive", source_elite, None),
                        "materials.materialSlots": (elite_values[2], "maximum_inclusive", source_elite, None),
                        "textures.textureLongEdge": (elite_values[3], "maximum_inclusive", source_elite, None),
                        "vfx.liveParticles": (elite_values[4], "maximum_inclusive", source_elite, None),
                        "vfx.dynamicLights": (elite_values[5], "maximum_inclusive", source_elite, None),
                    }
                )
            elif group == "boss":
                boss_values = {
                    "mobile_floor": (45000, 96, 3, 2048, 180, 0),
                    "mobile_high": (80000, 128, 4, 2048, 350, 1),
                    "pc_high": (130000, 180, 4, 4096, 700, 2),
                }[tier]
                values.update(
                    {
                        "geometry.lod0Triangles": (boss_values[0], "maximum_inclusive", source_elite, None),
                        "bones.deformingBones": (boss_values[1], "maximum_inclusive", source_elite, None),
                        "materials.materialSlots": (boss_values[2], "maximum_inclusive", source_elite, None),
                        "textures.textureLongEdge": (boss_values[3], "maximum_inclusive", source_elite, None),
                        "vfx.liveParticles": (boss_values[4], "maximum_inclusive", source_elite, None),
                        "vfx.dynamicLights": (boss_values[5], "maximum_inclusive", source_elite, None),
                    }
                )
            budget_profiles.append(
                budget_profile(group, tier, platform_by_tier[tier], scopes, values)
            )

    template_ids = {
        kind: rid("motion_matrix", f"{kind}_complete")
        for kind in contract.MOTION_TEMPLATE_REQUIREMENTS
    }
    motion_templates = [
        {
            "id": template_ids[kind],
            "displayName": f"Eldergrove {kind.title()} Complete Motion Matrix",
            "authority": approved_authority("contract"),
            "subjectKind": kind,
            "requiredMotionKeys": sorted(contract.MOTION_TEMPLATE_REQUIREMENTS[kind]),
            "requiredSkillPhases": contract.SKILL_PHASES,
            "requiredEffectCategories": contract.VFX_CATEGORIES,
            "roleSpecificPolicy": "Add explicit role, habitat, locomotion, charged attack, skill-use, and boss-transition motions without removing the canonical floor.",
        }
        for kind in sorted(contract.MOTION_TEMPLATE_REQUIREMENTS)
    ]

    race_id = rid("race", "elves")
    npc_specs = {
        "caretaker": ("Eldergrove Caretaker", "civilian_service", ["role.sanctuary_care"]),
        "hunter": ("Eldergrove Hunter", "service_combat", ["role.habitat_patrol"]),
        "oracle": ("Eldergrove Oracle", "quest_service", ["role.council_guidance"]),
        "warden": ("Eldergrove Warden", "combat_border", ["role.border_watch"]),
    }
    npc_ids = {slug: rid("npc", slug) for slug in npc_specs}
    champion_specs = {
        "assassin": "ClassFamily.Assassin",
        "mage": "ClassFamily.Mage",
        "ranger": "ClassFamily.Ranger",
        "warrior": "ClassFamily.Warrior",
    }
    champion_ids = {slug: rid("champion", slug) for slug in champion_specs}

    beast_specs = {
        "grove_strider": ("Grove Strider", "tdf_habitat_eldergrove_hollowbark_oldgrowth#tdf_grove_strider", ["walk", "run"], "quad_tall_browser"),
        "mire_lumenback": ("Mire Lumenback", "tdf_habitat_eldergrove_mirrorroot_littoral#tdf_mire_lumenback", ["walk", "swim"], "amphibious_low"),
        "moonshell_cicada": ("Moonshell Cicada", "tdf_habitat_eldergrove_moonroot_floodbasin#tdf_fauna_eldergrove_moonshell_cicada", ["crawl", "fly"], "invertebrate_winged"),
        "thornburrow_hare": ("Thornburrow Hare", "tdf_habitat_eldergrove_sunmane_edge_meadow#tdf_fauna_eldergrove_thornburrow_hare", ["walk", "run"], "quad_hind_drive"),
    }
    beast_ids = {slug: rid("beast", slug) for slug in beast_specs}
    monster_specs = {
        "hollowbark_stalker": ("Hollowbark Stalker", "elite", "tdf_habitat_eldergrove_hollowbark_oldgrowth#tdf_elite_eldergrove_hollowbark_stalker", ["walk", "run"], "flexible_quadruped"),
        "mere_root_leviathan": ("Mere-Root Leviathan", "boss", "tdf_habitat_eldergrove_moonroot_floodbasin#tdf_boss_eldergrove_mere_root_leviathan", ["walk", "swim"], "semi_aquatic_leviathan"),
        "mirrorfin_lurker": ("Mirrorfin Lurker", "elite", "tdf_habitat_eldergrove_mirrorroot_littoral#tdf_elite_eldergrove_mirrorfin_lurker", ["walk", "swim"], "amphibious_broad"),
        "moonbough_dragon": ("Moonbough Realm Dragon (Unresolved Reference)", "boss", "unresolved_realm_dragon#dragon_eldergrove_moonbough", ["walk", "fly"], "dragon_unresolved"),
        "sunmane_thornstag": ("Sunmane Thornstag", "elite", "tdf_habitat_eldergrove_sunmane_edge_meadow#tdf_elite_eldergrove_sunmane_thornstag", ["walk", "run"], "cervid_thornstag"),
    }
    monster_ids = {slug: rid("monster", slug) for slug in monster_specs}

    humanoid_rig = rid("rig", "humanoid_shared")
    creature_rig_ids = {
        rig_slug: rid("rig", rig_slug)
        for *_, rig_slug in list(beast_specs.values()) + list(monster_specs.values())
    }
    all_creature_rigs = sorted(creature_rig_ids.values())
    character_entities = sorted([race_id, *npc_ids.values(), *champion_ids.values()])
    beast_entities = sorted(beast_ids.values())
    elite_entities = sorted(
        monster_ids[slug] for slug, spec in monster_specs.items() if spec[1] == "elite"
    )
    boss_entities = sorted(
        monster_ids[slug] for slug, spec in monster_specs.items() if spec[1] == "boss"
    )

    body_modules = []
    character_body_slots = {
        "base": "base_body",
        "ears": "other",
        "feet": "feet",
        "hair": "hair",
        "hands": "hands",
        "head": "head",
    }
    character_body_ids = []
    for slug, slot in character_body_slots.items():
        module_id = rid("body", f"character_{slug}")
        character_body_ids.append(module_id)
        body_modules.append(
            {
                "id": module_id,
                "displayName": f"Eldergrove Character {slug.title()} Module Family",
                "authority": gated_authority(IDENTITY_DECISION, "customization", "modular_champion", "champion_handoff"),
                "slot": slot,
                "compatibleEntityIds": character_entities,
                "rigFamilyIds": [humanoid_rig],
                "meshSourceRef": None,
                "hiddenSurfaceMaskIds": [],
                "budgetProfileIds": [rid("budget", f"character_{tier}") for tier in ("mobile_floor", "mobile_high", "pc_high")],
            }
        )
    creature_body_ids = {}
    for group, subjects, rigs in (
        ("beast", beast_entities, sorted(creature_rig_ids[spec[3]] for spec in beast_specs.values())),
        ("elite", elite_entities, sorted(creature_rig_ids[monster_specs[slug][4]] for slug in monster_specs if monster_specs[slug][1] == "elite")),
        ("boss", boss_entities, sorted(creature_rig_ids[monster_specs[slug][4]] for slug in monster_specs if monster_specs[slug][1] == "boss")),
    ):
        module_id = rid("body", f"{group}_source_family")
        creature_body_ids[group] = module_id
        body_modules.append(
            {
                "id": module_id,
                "displayName": f"Eldergrove {group.title()} Source Body Family",
                "authority": gated_authority(IDENTITY_DECISION, "ecosystem", "terrestrial", "boss_elite", "dragon_unresolved"),
                "slot": "base_body",
                "compatibleEntityIds": subjects,
                "rigFamilyIds": rigs,
                "meshSourceRef": None,
                "hiddenSurfaceMaskIds": [],
                "budgetProfileIds": [rid("budget", f"{group}_{tier}") for tier in ("mobile_floor", "mobile_high", "pc_high")],
            }
        )

    equipment_modules = []
    equipment_specs = {
        "armor_chest": "chest",
        "armor_trim": "accessory",
        "back_attachment": "back",
        "belt": "waist",
        "boots": "feet",
        "cape": "back",
        "gloves": "hands",
        "hood": "head",
        "mount_anchor": "accessory",
        "pet_anchor": "accessory",
        "robe": "chest",
        "weapon_main": "main_hand",
        "weapon_off": "off_hand",
    }
    equipment_ids = []
    for slug, slot in equipment_specs.items():
        module_id = rid("equipment", slug)
        equipment_ids.append(module_id)
        equipment_modules.append(
            {
                "id": module_id,
                "displayName": f"Eldergrove {slug.replace('_', ' ').title()} Family",
                "authority": gated_authority(IDENTITY_DECISION, "customization", "modular_champion", "champion_anchor"),
                "slot": slot,
                "compatibleEntityIds": character_entities,
                "compatibleBodyModuleIds": sorted(character_body_ids),
                "rigFamilyIds": [humanoid_rig],
                "attachmentSocketIds": {
                    "main_hand": ["hand_main"],
                    "off_hand": ["hand_off"],
                    "back": ["back"],
                    "head": ["head"],
                }.get(slot, []),
                "meshSourceRef": None,
                "budgetProfileIds": [rid("budget", f"character_{tier}") for tier in ("mobile_floor", "mobile_high", "pc_high")],
            }
        )

    rig_families = [
        {
            "id": humanoid_rig,
            "displayName": "Eldergrove Shared Humanoid Production Rig",
            "authority": gated_authority(TECHNICAL_DECISION, "champion_handoff", "modular_champion", "contract"),
            "skeletonFamily": "Unity Humanoid compatible shared skeleton candidate",
            "bindPoseRef": None,
            "rootBone": "root",
            "rootMotionPolicy": "mixed_by_motion",
            "deformingBoneCount": None,
            "socketIds": ["back", "hand_main", "hand_off", "head", "mount_anchor", "pet_anchor"],
            "retargetGroup": "al_humanoid_shared_candidate",
            "budgetProfileIds": [rid("budget", f"character_{tier}") for tier in ("mobile_floor", "mobile_high", "pc_high")],
        }
    ]
    rig_source_ref = {
        "quad_tall_browser": "tdf_rig_quad_tall_browser",
        "amphibious_low": "tdf_rig_amphibious_low",
        "invertebrate_winged": "tdf_rig_invertebrate_winged",
        "quad_hind_drive": "tdf_rig_quad_hind_drive",
        "flexible_quadruped": "owner-gated rig candidate derived from Hollowbark Stalker motion source",
        "semi_aquatic_leviathan": "owner-gated rig candidate derived from Mere-Root Leviathan anatomy source",
        "amphibious_broad": "owner-gated rig candidate derived from Mirrorfin Lurker anatomy source",
        "dragon_unresolved": "owner-gated unresolved realm-dragon rig candidate",
        "cervid_thornstag": "owner-gated rig candidate derived from Sunmane Thornstag anatomy source",
    }
    rig_group = {}
    for rig_slug, rig_id in creature_rig_ids.items():
        if rig_slug in {spec[3] for spec in beast_specs.values()}:
            group = "beast"
        elif rig_slug in {monster_specs[slug][4] for slug in monster_specs if monster_specs[slug][1] == "elite"}:
            group = "elite"
        else:
            group = "boss"
        rig_group[rig_id] = group
        rig_families.append(
            {
                "id": rig_id,
                "displayName": f"Eldergrove {rig_slug.replace('_', ' ').title()} Rig Candidate",
                "authority": gated_authority(TECHNICAL_DECISION, "ecosystem", "terrestrial", "boss_elite", "dragon_unresolved"),
                "skeletonFamily": rig_source_ref[rig_slug],
                "bindPoseRef": None,
                "rootBone": "root",
                "rootMotionPolicy": "mixed_by_motion",
                "deformingBoneCount": None,
                "socketIds": [],
                "retargetGroup": f"eldergrove_{rig_slug}_candidate",
                "budgetProfileIds": [rid("budget", f"{group}_{tier}") for tier in ("mobile_floor", "mobile_high", "pc_high")],
            }
        )

    facial_systems = [
        {
            "id": rid("face", "humanoid_hybrid"),
            "displayName": "Eldergrove Humanoid Facial System Candidate",
            "authority": gated_authority(TECHNICAL_DECISION, "modular_champion", "champion_handoff", "contract"),
            "systemType": "hybrid",
            "rigFamilyIds": [humanoid_rig],
            "expressionSetIds": [],
            "visemeCount": None,
            "gazeSupported": True,
            "budgetProfileIds": [rid("budget", f"character_{tier}") for tier in ("mobile_floor", "mobile_high", "pc_high")],
        },
        {
            "id": rid("face", "creature_none_candidate"),
            "displayName": "Eldergrove Creature Facial-System Exclusion Candidate",
            "authority": gated_authority(TECHNICAL_DECISION, "ecosystem", "boss_elite", "contract"),
            "systemType": "none",
            "rigFamilyIds": all_creature_rigs,
            "expressionSetIds": [],
            "visemeCount": 0,
            "gazeSupported": False,
            "budgetProfileIds": sorted(
                rid("budget", f"{group}_{tier}")
                for group in ("beast", "elite", "boss")
                for tier in ("mobile_floor", "mobile_high", "pc_high")
            ),
        },
    ]

    secondary_physics = [
        {
            "id": rid("physics", "character_cloth"),
            "displayName": "Eldergrove Character Cloth Candidate",
            "authority": gated_authority(TECHNICAL_DECISION, "customization", "modular_champion", "contract"),
            "kind": "cloth",
            "solver": "Unity production solver owner decision required",
            "affectedIds": ["cape", "robe", "armor_trim"],
            "disableFallback": "Baked or rigid silhouette-preserving deformation with no gameplay-state change.",
            "collisionPolicy": "Owner-approved simple proxies only; render-mesh collision forbidden.",
            "budgetProfileIds": [rid("budget", f"character_{tier}") for tier in ("mobile_floor", "mobile_high", "pc_high")],
        },
        {
            "id": rid("physics", "character_hair"),
            "displayName": "Eldergrove Character Hair Candidate",
            "authority": gated_authority(TECHNICAL_DECISION, "customization", "modular_champion", "contract"),
            "kind": "hair",
            "solver": "Unity production solver owner decision required",
            "affectedIds": ["hair", "braid", "feather_cape"],
            "disableFallback": "Authored static shape preserves face, class, and realm read.",
            "collisionPolicy": "Head and shoulder proxy collision only after measured admission.",
            "budgetProfileIds": [rid("budget", f"character_{tier}") for tier in ("mobile_floor", "mobile_high", "pc_high")],
        },
    ]
    for group in ("beast", "elite", "boss"):
        secondary_physics.append(
            {
                "id": rid("physics", f"{group}_secondary"),
                "displayName": f"Eldergrove {group.title()} Secondary Motion Candidate",
                "authority": gated_authority(TECHNICAL_DECISION, "ecosystem", "terrestrial", "boss_elite", "contract"),
                "kind": "secondary_bone",
                "solver": "Source-specific appendage solver owner decision required",
                "affectedIds": [f"{group}_source_specific_appendages"],
                "disableFallback": "Authored static source silhouette and pose state remain readable.",
                "collisionPolicy": "Measured simple proxies only; no render-mesh collision.",
                "budgetProfileIds": [rid("budget", f"{group}_{tier}") for tier in ("mobile_floor", "mobile_high", "pc_high")],
            }
        )

    lod_profiles = []
    collider_profiles = []
    hitbox_profiles = []
    for group, rigs in (
        ("character", [humanoid_rig]),
        ("beast", sorted(rig_id for rig_id, value in rig_group.items() if value == "beast")),
        ("elite", sorted(rig_id for rig_id, value in rig_group.items() if value == "elite")),
        ("boss", sorted(rig_id for rig_id, value in rig_group.items() if value == "boss")),
    ):
        budget_ids = [rid("budget", f"{group}_{tier}") for tier in ("mobile_floor", "mobile_high", "pc_high")]
        lod_profiles.append(
            {
                "id": rid("lod", group),
                "displayName": f"Eldergrove {group.title()} LOD Candidate",
                "authority": gated_authority(TECHNICAL_DECISION, "contract", "design"),
                "levels": [
                    {"index": 0, "triangleRatio": 1.0, "screenRelativeTransitionHeight": 0.6, "boneReductionPolicy": "Full candidate rig within tier budget.", "materialReductionPolicy": "Source material families within tier budget."},
                    {"index": 1, "triangleRatio": 0.55, "screenRelativeTransitionHeight": 0.3, "boneReductionPolicy": "Remove only measured secondary deformation.", "materialReductionPolicy": "Merge non-protected material detail."},
                    {"index": 2, "triangleRatio": 0.25, "screenRelativeTransitionHeight": 0.12, "boneReductionPolicy": "Preserve root, locomotion, attack origin, face/focal region, and silhouette controls.", "materialReductionPolicy": "Use opaque packed materials while preserving realm and threat cues."},
                    {"index": 3, "triangleRatio": 0.075, "screenRelativeTransitionHeight": 0.035, "boneReductionPolicy": "Marker or silhouette state preserves gameplay information.", "materialReductionPolicy": "Single opaque state where measurement permits."},
                ],
                "protectedCues": ["attack_origin", "face_or_focal_region", "primary_silhouette", "realm_identity", "threat_or_interaction_state"],
                "normalPlayUsesReducedLod": True,
            }
        )
        collider_profiles.append(
            {
                "id": rid("collider", group),
                "displayName": f"Eldergrove {group.title()} Collider Candidate",
                "authority": gated_authority(TECHNICAL_DECISION, "contract"),
                "rigFamilyIds": rigs,
                "lodIndependent": True,
                "renderMeshColliderForbidden": True,
                "colliders": [
                    {"colliderId": "movement_root", "shape": "capsule", "purpose": "movement", "boneOrSocket": "root"},
                    {"colliderId": "interaction_focus", "shape": "sphere", "purpose": "interaction", "boneOrSocket": "root"},
                ],
                "budgetProfileIds": budget_ids,
            }
        )
        hitbox_profiles.append(
            {
                "id": rid("hitbox", group),
                "displayName": f"Eldergrove {group.title()} Hitbox Candidate",
                "authority": gated_authority(TECHNICAL_DECISION, "contract"),
                "rigFamilyIds": rigs,
                "gameplayAuthorityRef": "External gameplay timing and result authority; dimensions require owner-approved measurement.",
                "hitboxes": [
                    {"hitboxId": "hurt_root", "shape": "capsule", "purpose": "hurt", "boneOrSocket": "root", "activationAuthority": "always_on"},
                    {"hitboxId": "target_root", "shape": "sphere", "purpose": "target", "boneOrSocket": "root", "activationAuthority": "always_on"},
                ],
                "budgetProfileIds": budget_ids,
            }
        )

    group_subjects = {
        "character": character_entities,
        "beast": beast_entities,
        "elite": elite_entities,
        "boss": boss_entities,
    }
    platform_variants = []
    for group, subjects in group_subjects.items():
        for tier in ("mobile_floor", "mobile_high", "pc_high"):
            platform_variants.append(
                {
                    "id": rid("platform", f"{group}_{tier}"),
                    "displayName": f"Eldergrove {group.title()} {tier.replace('_', ' ').title()} Variant",
                    "authority": gated_authority(TECHNICAL_DECISION, "contract", "world_budget", "design"),
                    "platformProfileId": platform_by_tier[tier],
                    "subjectIds": subjects,
                    "budgetProfileIds": [rid("budget", f"{group}_{tier}")],
                    "meshPolicy": "Use measured LODs; preserve silhouette, face/focal region, attack origin, and interaction/threat state.",
                    "texturePolicy": "Pack and downscale only within the tier budget; color cannot be the only identity cue.",
                    "rigPolicy": "Reduce only measured secondary controls; gameplay and protected deformation remain intact.",
                    "physicsPolicy": "Disable optional secondary simulation before changing collision, timing, or readable state.",
                    "vfxPolicy": "Reduce density, transparency, lights, and secondary effects before protected telegraph/result cues.",
                    "protectedCues": ["attack_origin", "interaction_state", "primary_silhouette", "realm_identity", "target_and_danger", "threat_state"],
                }
            )

    char_refs = entity_refs(
        "character",
        sorted(character_body_ids),
        sorted(equipment_ids),
        [humanoid_rig],
        [rid("face", "humanoid_hybrid")],
        [rid("physics", "character_cloth"), rid("physics", "character_hair")],
        [],
    )
    playable_races = [
        {
            "id": race_id,
            "displayName": "Eldergrove Elves",
            "authority": gated_authority(IDENTITY_DECISION, "realm", "first_user", "customization", "contract"),
            **char_refs,
            "customizationContractRef": "unity/Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json#realms/Eldergrove",
        }
    ]

    npc_archetypes = []
    for slug, (name, role, role_actions) in npc_specs.items():
        refs = entity_refs(
            "character",
            sorted(character_body_ids),
            sorted(equipment_ids),
            [humanoid_rig],
            [rid("face", "humanoid_hybrid")],
            [rid("physics", "character_cloth"), rid("physics", "character_hair")],
            [template_ids["npc"]],
        )
        npc_archetypes.append(
            {
                "id": npc_ids[slug],
                "displayName": name,
                "authority": gated_authority(IDENTITY_DECISION, "realm", "design", "contract"),
                **refs,
                "role": role,
                "roleActionKeys": role_actions,
                "skillIds": [],
            }
        )

    approved_progression_catalog = json.loads(
        (
            repo_root
            / "unity/Assets/AL/StreamingAssets/GameData/al_stonehold_realm_character_taxonomy.json"
        ).read_text(encoding="utf-8")
    )
    progression_source_rows = [
        row
        for row in approved_progression_catalog["skills"]
        if row["externalSourceId"].startswith("anotherlife.class_progression.")
    ]
    if len(progression_source_rows) != 96:
        raise ValueError(
            "The approved cross-realm class progression must contain exactly 96 skills."
        )
    progression_ids_by_family: dict[str, list[str]] = defaultdict(list)
    progression_class_sources_by_family: dict[str, list[str]] = {}
    for family_slug in champion_specs:
        source_family = next(
            row
            for row in approved_progression_catalog["championFamilies"]
            if any(
                source_id.startswith(f"anotherlife.class.{family_slug}.")
                for source_id in row["classSourceIds"]
            )
        )
        progression_class_sources_by_family[family_slug] = list(
            source_family["classSourceIds"]
        )
    for row in progression_source_rows:
        family_slug = row["externalSourceId"].split(".")[2]
        progression_ids_by_family[family_slug].append(
            row["id"].replace(
                "rct_stonehold_skill_", "rct_eldergrove_skill_", 1
            )
        )

    generic_skill_slugs = ["realm_strike", "renewing_guard", "warmaster_breaker", "warzone_burst"]
    skill_ids = {slug: rid("skill", slug) for slug in generic_skill_slugs}
    skill_ids.update(
        {
            "arcane_bolt": rid("skill", "arcane_bolt"),
            "verdant_nova": rid("skill", "verdant_nova"),
            "hollowbark_pounce": rid("skill", "hollowbark_stalker_low_pounce"),
            "leviathan_lunge": rid("skill", "mere_root_leviathan_jaw_lunge"),
            "mirrorfin_scoop": rid("skill", "mirrorfin_lurker_jaw_scoop"),
            "thornstag_charge": rid("skill", "sunmane_thornstag_bounding_charge"),
        }
    )
    generic_skill_ids = sorted(skill_ids[slug] for slug in generic_skill_slugs)
    champion_families = []
    for slug, class_source_id in champion_specs.items():
        family_skill_ids = list(generic_skill_ids)
        family_skill_ids.extend(progression_ids_by_family[slug])
        if slug == "mage":
            family_skill_ids.extend([skill_ids["arcane_bolt"], skill_ids["verdant_nova"]])
        refs = entity_refs(
            "character",
            sorted(character_body_ids),
            sorted(equipment_ids),
            [humanoid_rig],
            [rid("face", "humanoid_hybrid")],
            [rid("physics", "character_cloth"), rid("physics", "character_hair")],
            [template_ids["champion"]],
        )
        champion_families.append(
            {
                "id": champion_ids[slug],
                "displayName": f"Eldergrove {slug.title()} Production Family",
                "authority": gated_authority(
                    IDENTITY_DECISION,
                    "classes",
                    "first_user",
                    "champion_convergence",
                    "champion_anchor",
                    "approved_class_progression",
                    "contract",
                ),
                **refs,
                "playableRaceIds": [race_id],
                "classSourceIds": [
                    class_source_id,
                    *progression_class_sources_by_family[slug],
                ],
                "skillIds": sorted(family_skill_ids),
                "weaponFamilyIds": sorted(
                    module_id
                    for module_id in equipment_ids
                    if module_id in {rid("equipment", "weapon_main"), rid("equipment", "weapon_off")}
                ),
            }
        )

    beast_families = []
    for slug, (name, habitat, locomotion, rig_slug) in beast_specs.items():
        refs = entity_refs(
            "beast",
            [creature_body_ids["beast"]],
            [],
            [creature_rig_ids[rig_slug]],
            [rid("face", "creature_none_candidate")],
            [rid("physics", "beast_secondary")],
            [template_ids["beast"]],
        )
        beast_families.append(
            {
                "id": beast_ids[slug],
                "displayName": name,
                "authority": gated_authority(IDENTITY_DECISION, "ecosystem", "terrestrial", "contract"),
                **refs,
                "habitatSourceRef": habitat,
                "locomotionModes": locomotion,
                "skillIds": [],
            }
        )

    special_skill_by_monster = {
        "hollowbark_stalker": skill_ids["hollowbark_pounce"],
        "mere_root_leviathan": skill_ids["leviathan_lunge"],
        "mirrorfin_lurker": skill_ids["mirrorfin_scoop"],
        "sunmane_thornstag": skill_ids["thornstag_charge"],
    }
    monster_families = []
    for slug, (name, rank, habitat, locomotion, rig_slug) in monster_specs.items():
        group = "boss" if rank == "boss" else "elite"
        templates = [template_ids["monster"]]
        transitions: list[str] = []
        if rank == "boss":
            templates.append(template_ids["boss"])
            transitions = ["boss.transition"]
        refs = entity_refs(
            group,
            [creature_body_ids[group]],
            [],
            [creature_rig_ids[rig_slug]],
            [rid("face", "creature_none_candidate")],
            [rid("physics", f"{group}_secondary")],
            templates,
        )
        monster_families.append(
            {
                "id": monster_ids[slug],
                "displayName": name,
                "authority": gated_authority(IDENTITY_DECISION, "ecosystem", "boss_elite", "realm", "dragon_unresolved", "contract"),
                **refs,
                "rank": rank,
                "habitatSourceRef": habitat,
                "locomotionModes": locomotion,
                "skillIds": [special_skill_by_monster[slug]] if slug in special_skill_by_monster else [],
                "bossTransitionKeys": transitions,
            }
        )

    all_champions = sorted(champion_ids.values())
    skills = []
    progression_skill_ids: set[str] = set()
    for source_row in progression_source_rows:
        family_slug = source_row["externalSourceId"].split(".")[2]
        skill_id = source_row["id"].replace(
            "rct_stonehold_skill_", "rct_eldergrove_skill_", 1
        )
        progression_skill_ids.add(skill_id)
        authority = approved_authority("approved_class_progression")
        authority["approvalEvidenceRefs"] = sorted(
            {
                *source_row["authority"]["approvalEvidenceRefs"],
                (
                    "unity/Assets/AL/StreamingAssets/GameData/"
                    f"al_stonehold_realm_character_taxonomy.json#{source_row['id']}"
                ),
            }
        )
        skills.append(
            {
                "id": skill_id,
                "displayName": source_row["displayName"],
                "authority": authority,
                "externalSourceId": source_row["externalSourceId"],
                "sourceCatalogRef": source_row["sourceCatalogRef"],
                "subjectIds": [champion_ids[family_slug]],
                "timingAuthorityRef": source_row["timingAuthorityRef"],
                "resultAuthorityRef": source_row["resultAuthorityRef"],
            }
        )
    champion_skill_specs = {
        "arcane_bolt": ("Arcane Bolt", "skill_arcane_bolt", [champion_ids["mage"]], "skills_observed", "unity/Assets/AL/StreamingAssets/GameData/skills.json#skill_arcane_bolt"),
        "realm_strike": ("Realm Strike", "realm_strike", all_champions, "skill_convergence", "unity/Docs/GameDataCatalog/PhaseC/Phase_C8A_Skill_Authority_Convergence.md#realm_strike"),
        "renewing_guard": ("Renewing Guard", "renewing_guard", all_champions, "skill_convergence", "unity/Docs/GameDataCatalog/PhaseC/Phase_C8A_Skill_Authority_Convergence.md#renewing_guard"),
        "verdant_nova": ("Verdant Nova", "skill_verdant_nova", [champion_ids["mage"]], "skills_observed", "unity/Assets/AL/StreamingAssets/GameData/skills.json#skill_verdant_nova"),
        "warmaster_breaker": ("Warmaster Breaker", "warmaster_breaker", all_champions, "skill_convergence", "unity/Docs/GameDataCatalog/PhaseC/Phase_C8A_Skill_Authority_Convergence.md#warmaster_breaker"),
        "warzone_burst": ("Warzone Burst", "warzone_burst", all_champions, "skill_convergence", "unity/Docs/GameDataCatalog/PhaseC/Phase_C8A_Skill_Authority_Convergence.md#warzone_burst"),
    }
    for slug, (name, external_id, subjects, source_key, source_ref) in champion_skill_specs.items():
        skills.append(
            {
                "id": skill_ids[slug],
                "displayName": name,
                "authority": gated_authority(PRESENTATION_DECISION, source_key, "skill_weather", "contract"),
                "externalSourceId": external_id,
                "sourceCatalogRef": source_ref,
                "subjectIds": subjects,
                "timingAuthorityRef": "External gameplay timing authority; identity-to-class assignment remains owner-gated.",
                "resultAuthorityRef": "External gameplay result authority; this catalog owns presentation requirements only.",
            }
        )
    creature_skill_specs = {
        "hollowbark_pounce": ("Hollowbark Stalker Low Pounce", "tdf_elite_eldergrove_hollowbark_stalker:sudden_low_pounce", monster_ids["hollowbark_stalker"]),
        "leviathan_lunge": ("Mere-Root Leviathan Jaw-Led Lunge", "tdf_boss_eldergrove_mere_root_leviathan:jaw_led_lunge", monster_ids["mere_root_leviathan"]),
        "mirrorfin_scoop": ("Mirrorfin Lurker Jaw Scoop", "tdf_elite_eldergrove_mirrorfin_lurker:jaw_scoop", monster_ids["mirrorfin_lurker"]),
        "thornstag_charge": ("Sunmane Thornstag Bounding Charge", "tdf_elite_eldergrove_sunmane_thornstag:bounding_charge", monster_ids["sunmane_thornstag"]),
    }
    for slug, (name, external_id, subject_id) in creature_skill_specs.items():
        skills.append(
            {
                "id": skill_ids[slug],
                "displayName": name,
                "authority": gated_authority(PRESENTATION_DECISION, "boss_elite", "contract"),
                "externalSourceId": external_id,
                "sourceCatalogRef": "unity/Docs/Terrestrials/RealmBossesAndElites/Realm_Boss_Elite_Design_Source.md#visual-motion-only",
                "subjectIds": [subject_id],
                "timingAuthorityRef": "Not authorized by the visual source; gameplay timing requires owner-approved design.",
                "resultAuthorityRef": "Not authorized by the visual source; gameplay result requires owner-approved design.",
            }
        )

    motions = []
    for key in sorted(contract.CHAMPION_MOTIONS | {"attack.charged", "skill.use"}):
        motions.append(
            motion_record(
                f"champion_shared_{key.replace('.', '_')}",
                f"Eldergrove Champion {key}",
                all_champions,
                key,
                humanoid_rig,
            )
        )
    all_npcs = sorted(npc_ids.values())
    for key in sorted(contract.NPC_MOTIONS | {"attack.charged", "skill.use"}):
        motions.append(
            motion_record(
                f"npc_shared_{key.replace('.', '_')}",
                f"Eldergrove NPC {key}",
                all_npcs,
                key,
                humanoid_rig,
            )
        )
    for slug, (_, _, role_actions) in npc_specs.items():
        for key in role_actions:
            motions.append(
                motion_record(
                    f"npc_{slug}_{key.replace('.', '_')}",
                    f"Eldergrove {slug.title()} {key}",
                    [npc_ids[slug]],
                    key,
                    humanoid_rig,
                )
            )
    for slug, (_, _, locomotion, rig_slug) in beast_specs.items():
        keys = set(contract.BEAST_MOTIONS) | {f"locomotion.{mode}" for mode in locomotion}
        for key in sorted(keys):
            motions.append(
                motion_record(
                    f"beast_{slug}_{key.replace('.', '_')}",
                    f"{beast_specs[slug][0]} {key}",
                    [beast_ids[slug]],
                    key,
                    creature_rig_ids[rig_slug],
                )
            )
    for slug, (_, rank, _, locomotion, rig_slug) in monster_specs.items():
        keys = set(contract.BOSS_MOTIONS if rank == "boss" else contract.MONSTER_MOTIONS)
        keys.update(f"locomotion.{mode}" for mode in locomotion)
        if rank == "boss":
            keys.add("boss.transition")
        for key in sorted(keys):
            motions.append(
                motion_record(
                    f"monster_{slug}_{key.replace('.', '_')}",
                    f"{monster_specs[slug][0]} {key}",
                    [monster_ids[slug]],
                    key,
                    creature_rig_ids[rig_slug],
                )
            )

    skill_by_id = {row["id"]: row for row in skills}
    skill_rig = {
        **{skill_id: humanoid_rig for skill_id in progression_skill_ids},
        **{skill_ids[slug]: humanoid_rig for slug in champion_skill_specs},
        skill_ids["hollowbark_pounce"]: creature_rig_ids["flexible_quadruped"],
        skill_ids["leviathan_lunge"]: creature_rig_ids["semi_aquatic_leviathan"],
        skill_ids["mirrorfin_scoop"]: creature_rig_ids["amphibious_broad"],
        skill_ids["thornstag_charge"]: creature_rig_ids["cervid_thornstag"],
    }
    skill_phase_ids: dict[str, dict[str, str]] = defaultdict(dict)
    for skill_id, skill in skill_by_id.items():
        is_creature_skill = skill_id in {
            skill_ids["hollowbark_pounce"],
            skill_ids["leviathan_lunge"],
            skill_ids["mirrorfin_scoop"],
            skill_ids["thornstag_charge"],
        }
        phases = (
            []
            if skill_id in progression_skill_ids
            else (
                ["anticipation", "release", "recovery"]
                if is_creature_skill
                else contract.SKILL_PHASES
            )
        )
        skill_slug = skill_id.removeprefix("rct_eldergrove_skill_").removesuffix("_v001")
        for phase in phases:
            record = motion_record(
                f"skill_{skill_slug}_{phase}",
                f"{skill['displayName']} {phase.title()}",
                skill["subjectIds"],
                f"skill.{phase}",
                skill_rig[skill_id],
                skill_id=skill_id,
                skill_phase=phase,
            )
            motions.append(record)
            skill_phase_ids[skill_id][phase] = record["id"]

    vfx_effects = []
    vfx_ids: dict[str, dict[str, str]] = defaultdict(dict)
    creature_effects = {
        skill_ids["hollowbark_pounce"]: {"telegraph", "release", "trail", "impact", "environmental", "result", "cleanup"},
        skill_ids["leviathan_lunge"]: {"telegraph", "release", "trail", "impact", "area", "environmental", "result", "cleanup"},
        skill_ids["mirrorfin_scoop"]: {"telegraph", "release", "trail", "impact", "area", "environmental", "result", "cleanup"},
        skill_ids["thornstag_charge"]: {"telegraph", "release", "trail", "impact", "area", "environmental", "result", "cleanup"},
    }
    group_by_skill = {
        **{skill_id: "character" for skill_id in progression_skill_ids},
        **{skill_ids[slug]: "character" for slug in champion_skill_specs},
        skill_ids["hollowbark_pounce"]: "elite",
        skill_ids["leviathan_lunge"]: "boss",
        skill_ids["mirrorfin_scoop"]: "elite",
        skill_ids["thornstag_charge"]: "elite",
    }
    for skill_id, skill in skill_by_id.items():
        if skill_id in progression_skill_ids:
            continue
        categories = creature_effects.get(skill_id, set(contract.VFX_CATEGORIES))
        group = group_by_skill[skill_id]
        budget_ids = [rid("budget", f"{group}_{tier}") for tier in ("mobile_floor", "mobile_high", "pc_high")]
        source_direction = (
            "Visual motion/effect language from Realm_Boss_Elite_Design_Source.md; combat behavior remains unauthorized."
            if skill_id in creature_effects
            else "Canonical/observed skill identity plus provisional Eldergrove healing/protection palette; final grammar remains unauthorized."
        )
        for category in sorted(categories):
            record = vfx_record(skill, category, budget_ids, source_direction)
            vfx_effects.append(record)
            vfx_ids[skill_id][category] = record["id"]

    skill_traceability = []
    for skill_id in sorted(skill_by_id):
        is_identity_only = skill_id in progression_skill_ids
        required_phases = set(skill_phase_ids[skill_id])
        required_effects = set(vfx_ids[skill_id])
        skill_traceability.append(
            {
                "skillId": skill_id,
                "motionPhases": {
                    phase: {
                        "state": "required" if phase in required_phases else "not_applicable",
                        "recordIds": [skill_phase_ids[skill_id][phase]] if phase in required_phases else [],
                        "rationale": (
                            "Explicit owner-gated production phase requirement."
                            if phase in required_phases
                            else (
                                "The owner-approved progression establishes identity and order, not a production motion or gameplay-timing requirement."
                                if is_identity_only
                                else "The visual source documents a physical creature action, not a cast or channel phase."
                            )
                        ),
                    }
                    for phase in contract.SKILL_PHASES
                },
                "effects": {
                    category: {
                        "state": "required" if category in required_effects else "not_applicable",
                        "recordIds": [vfx_ids[skill_id][category]] if category in required_effects else [],
                        "rationale": (
                            "Explicit owner-gated production effect requirement."
                            if category in required_effects
                            else (
                                "The owner-approved progression establishes identity and order, not production effect or magical-grammar authority."
                                if is_identity_only
                                else "No documented source or gameplay result makes this category applicable to the physical creature action."
                            )
                        ),
                    }
                    for category in contract.VFX_CATEGORIES
                },
                "audioSyncRefs": ["Owner-gated audio synchronization; no sound asset is authorized."],
                "cameraSyncRefs": ["No camera impulse is authorized; any future cue requires reduced-motion parity."],
                "accessibilityEvidenceRefs": [
                    "unity/Docs/AssetLibrary/PostMVP_Realm_Character_Creature_Catalog_Contract_v1.md#protected-degradation-rules",
                    "Every required effect records reduced-motion and off-state cues.",
                ],
            }
        )

    sections: dict[str, list[dict[str, Any]]] = {
        "provenance": provenance,
        "decisionPackets": [],
        "platformProfiles": platform_profiles,
        "budgetProfiles": budget_profiles,
        "motionMatrixTemplates": motion_templates,
        "playableRaces": playable_races,
        "npcArchetypes": npc_archetypes,
        "championFamilies": champion_families,
        "beastFamilies": beast_families,
        "monsterFamilies": monster_families,
        "bodyModules": body_modules,
        "equipmentModules": equipment_modules,
        "rigFamilies": rig_families,
        "facialSystems": facial_systems,
        "secondaryPhysicsProfiles": secondary_physics,
        "lodProfiles": lod_profiles,
        "colliderProfiles": collider_profiles,
        "hitboxProfiles": hitbox_profiles,
        "platformVariants": platform_variants,
        "skills": skills,
        "motions": motions,
        "vfxEffects": vfx_effects,
    }

    packet_subjects: dict[str, set[str]] = defaultdict(set)
    for section_name, rows in sections.items():
        if section_name in {"provenance", "decisionPackets"}:
            continue
        for row in rows:
            for packet_id in row.get("authority", {}).get("decisionPacketIds", []):
                packet_subjects[packet_id].add(row["id"])
            for dimension in row.get("creativeDecisions", {}).values():
                for packet_id in dimension.get("decisionPacketIds", []):
                    packet_subjects[packet_id].add(row["id"])
            if section_name == "budgetProfiles":
                for _, budget_metric in contract._iter_metric_objects(row):
                    for packet_id in budget_metric.get("decisionPacketIds", []):
                        packet_subjects[packet_id].add(row["id"])

    packet_specs = {
        IDENTITY_DECISION: (
            "Eldergrove Identity and Modularity Review",
            [
                "morphology",
                "culture",
                "silhouette",
                "anatomy",
                "clothing",
                "armor",
                "animation_personality",
                "magical_grammar",
            ],
            "APPROVE, REVISE, or REJECT the bounded entity roster, class/race links, modular body/equipment plan, and source-constrained visual direction without treating proposals as canon.",
            [
                "realm",
                "customization",
                "champion_convergence",
                "approved_class_progression",
                "ecosystem",
                "boss_elite",
                "dragon_unresolved",
            ],
        ),
        TECHNICAL_DECISION: (
            "Eldergrove Rig, Platform, and Budget Review",
            ["silhouette", "anatomy", "animation_personality"],
            "APPROVE, REVISE, or REJECT the proposed production rigs, profiles, LODs, colliders, hitboxes, physics fallbacks, target variants, and unresolved measured limits.",
            ["contract", "world_budget", "design", "champion_handoff", "ecosystem", "boss_elite"],
        ),
        PRESENTATION_DECISION: (
            "Eldergrove Motion and Effect Review",
            ["animation_personality", "magical_grammar"],
            "APPROVE, REVISE, or REJECT the held motion, skill-assignment, special-attack, and VFX requirements while gameplay timing/results remain external.",
            ["contract", "skill_convergence", "skill_weather", "boss_elite", "champion_anchor"],
        ),
    }
    decision_packets = []
    for packet_id, (name, dimensions, question, source_keys) in packet_specs.items():
        decision_packets.append(
            {
                "id": packet_id,
                "displayName": name,
                "subjectIds": sorted(packet_subjects[packet_id]),
                "decisionDimensions": dimensions,
                "question": question,
                "provenanceIds": sorted(provenance_id(key) for key in source_keys),
                "alternatives": [
                    {
                        "alternativeId": "approve_bounded_source_direction",
                        "summary": "Approve only the source-bounded proposal and retain every downstream technical/release gate.",
                        "evidenceRefs": ["Catalog subjects and provenance listed in this packet."],
                        "risks": ["Approval still requires measured production evidence before generation or release."],
                    },
                    {
                        "alternativeId": "revise_before_approval",
                        "summary": "Revise named subjects while all affected generation and activation remain held.",
                        "evidenceRefs": ["Owner response must name affected catalog IDs and requested changes."],
                        "risks": ["Dependent concept, rig, animation, and VFX work remains blocked."],
                    },
                    {
                        "alternativeId": "reject_direction",
                        "summary": "Reject the proposal and retire or replace the named subjects in a new catalog revision.",
                        "evidenceRefs": ["Owner response must identify the rejected direction."],
                        "risks": ["Replacement taxonomy and provenance are required before production can resume."],
                    },
                ],
                "downstreamImpacts": [
                    {"discipline": "concept", "impact": "Controls whether concept refinement may begin."},
                    {"discipline": "rigging", "impact": "Controls skeleton, deformation, socket, and physics implementation."},
                    {"discipline": "animation", "impact": "Controls motion personality and source-specific production clips."},
                    {"discipline": "vfx", "impact": "Controls final Eldergrove grammar and effect asset production."},
                    {"discipline": "performance", "impact": "Controls measured tier admission and budget replacement."},
                    {"discipline": "runtime", "impact": "Does not authorize activation or replace gameplay authority."},
                    {"discipline": "qa", "impact": "Requires motion/effect, accessibility, provenance, and platform evidence."},
                ],
                "ownerStatus": "PENDING",
                "approvedAlternativeId": None,
                "ownerResponse": None,
                "decidedAtUtc": None,
            }
        )
    sections["decisionPackets"] = decision_packets

    for rows in sections.values():
        rows.sort(key=lambda row: row["id"].encode("utf-8"))

    held_gate = {
        "state": "held",
        "reviewer": None,
        "decidedAtUtc": None,
        "evidenceRefs": [],
        "openIssues": ["Owner, technical, provenance, motion/effect, performance, accessibility, and release admission remain pending."],
    }
    return {
        "gameId": "another-life",
        "catalogId": "rct_eldergrove_catalog_production_v001",
        "schemaVersion": 1,
        "contentVersion": "1.0.0",
        "realmId": "eldergrove",
        "idFormat": "rct_scope_kind_slug_vNNN",
        "authority": {
            "catalogOwner": "Another Life character and creature production preparation",
            "finalCreativeOwner": "project_owner",
            "ownerDecisionRef": f"{IDENTITY_DECISION}; {TECHNICAL_DECISION}; {PRESENTATION_DECISION}",
            "status": "preparation_held",
        },
        "gatePolicy": {
            "generationState": "held",
            "activationState": "held",
            "requiredGateIds": contract.GATE_IDS,
            "gateEvidence": {
                "ownerCreative": dict(held_gate),
                "technical": dict(held_gate),
                "provenance": dict(held_gate),
                "motionEffectCoverage": dict(held_gate),
                "performanceMobileFloor": dict(held_gate),
                "accessibility": dict(held_gate),
                "release": dict(held_gate),
            },
        },
        **sections,
        "skillTraceability": skill_traceability,
    }


def render_catalog(repo_root: Path) -> str:
    return json.dumps(build_catalog(repo_root), indent=2, ensure_ascii=False) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument("--check", action="store_true", help="Fail if the committed output differs.")
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
