#!/usr/bin/env python3
"""Build the tracked, non-runtime realm-creature source promotion packet."""

from __future__ import annotations

import json
import shutil
from pathlib import Path

from PIL import Image

from tools.terrestrial.promote_realm_creature_source_packet import (
    PacketError,
    copy_asset,
    file_record,
    has_owner_tier_texture_packet,
    validate_input_manifests,
    validate_promoted_files,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = REPO_ROOT / "archive"
PRODUCTION = ARCHIVE / "production_creatures_v001"
APPROVAL_PATH = ARCHIVE / "creature_2d_approval/final_2d_gate_manifest.json"
READINESS_PATH = PRODUCTION / "production_readiness_manifest.json"
DOC_ROOT = REPO_ROOT / "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001"
ART_ROOT = REPO_ROOT / "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001"
SCHEMA_TEMPLATE = Path(__file__).resolve().parent / "templates/realm_creature_3d_source_manifest.schema.json"
SOURCE_VERSION = "al-rcreature-2026-09-02-v001"
CREATED_AT_UTC = "2026-09-02T06:07:11Z"

SOURCE_TO_MODEL = {
    "tdf_boss_stonehold_fault_crowned_colossus": "boss_stonehold_fault_crowned_colossus",
    "tdf_elite_stonehold_oreblind_delver": "elite_stonehold_oreblind_delver",
    "tdf_elite_stonehold_rimehorn_breaker": "elite_stonehold_rimehorn_breaker",
    "tdf_elite_stonehold_slaghide_gorer": "elite_stonehold_slaghide_gorer",
    "tdf_boss_eldergrove_mere_root_leviathan": "boss_eldergrove_mere_root_leviathan",
    "tdf_elite_eldergrove_hollowbark_stalker": "elite_eldergrove_hollowbark_stalker",
    "tdf_elite_eldergrove_mirrorfin_lurker": "elite_eldergrove_mirrorfin_lurker",
    "tdf_elite_eldergrove_sunmane_thornstag": "elite_eldergrove_sunmane_thornstag",
    "tdf_boss_crownlands_meridian_tempest_roc": "boss_crownlands_meridian_tempest_roc",
    "tdf_elite_crownlands_crownstep_lion": "elite_crownlands_crownstep",
    "tdf_elite_crownlands_galeclaw_courser": "elite_crownlands_galeclaw_courser",
    "tdf_elite_crownlands_reliquary_basilisk": "elite_crownlands_reliquary_basilisk",
    "tdf_boss_umbral_ashvein_triarch": "boss_umbral_ashvein_triarch",
    "tdf_elite_umbral_cindermaw_salamander": "elite_umbral_cindermaw_salamander",
    "tdf_elite_umbral_gravewing_siphon": "elite_umbral_gravewing_siphon",
    "tdf_elite_umbral_veilspine_widow": "elite_umbral_veilspine_widow",
    "dragon_crownlands_dawn_regent": "dragon_crownlands_dawn_regent",
    "dragon_stonehold_iron_wyrm": "dragon_stonehold_iron_wyrm",
    "dragon_eldergrove_moonbough": "dragon_eldergrove_moonbough",
    "dragon_umbral_void_seraph": "dragon_umbral_void_seraph",
    "NPC_VAELORYN": "wish_dragon_vaeloryn",
}

TASK_IDS = {
    "dragon_crownlands_dawn_regent": ["01a05b49-b07c-7198-b593-1168cb7a303b"],
    "dragon_stonehold_iron_wyrm": ["01a05b1b-b1f8-735f-88c6-4727256a1d8f"],
    "dragon_eldergrove_moonbough": ["01a05b45-bb84-76ca-95e5-1714cfe5ce39"],
    "dragon_umbral_void_seraph": ["01a05b1b-bece-767e-bbba-48a1cfe6f40c"],
    "wish_dragon_vaeloryn": ["01a05b2c-92c6-7329-939f-a538fdaa859b"],
    "boss_stonehold_fault_crowned_colossus": ["01a05b45-c3f7-73cc-91dc-dd5b775afa7f"],
    "boss_eldergrove_mere_root_leviathan": ["01a05b45-caf4-7239-991b-3f3f940abc39"],
    "boss_crownlands_meridian_tempest_roc": ["01a05b56-8881-72f3-8860-6aec86577692"],
    "boss_umbral_ashvein_triarch": ["01a05b2c-8cd0-7699-9429-289c1dc1de69"],
    "elite_stonehold_oreblind_delver": ["01a05b59-7f34-72c3-8923-a917e686a496"],
    "elite_stonehold_rimehorn_breaker": ["01a05b41-cbfd-7600-805a-66c8a290e6ec"],
    "elite_stonehold_slaghide_gorer": ["01a05b59-8a11-7369-8614-53c2cba364b7"],
    "elite_eldergrove_hollowbark_stalker": ["01a05c27-845a-7534-b4c5-f5774948b37b", "01a05fa3-0969-73cd-98fb-2a9c09ac865b"],
    "elite_eldergrove_mirrorfin_lurker": ["01a05b36-338c-72d6-a650-bb5adc18da9d"],
    "elite_eldergrove_sunmane_thornstag": ["01a05b41-d430-7509-a8d4-21b3f6048d75"],
    "elite_crownlands_crownstep": ["01a05b41-dd68-757c-a4c3-2f5dbd67a16e"],
    "elite_crownlands_galeclaw_courser": ["01a05b41-e560-7087-bfdf-1c882e85a933"],
    "elite_crownlands_reliquary_basilisk": ["01a05c27-e53f-708a-9cf4-7fdd94a8fa25", "01a05fa3-1012-76a6-829d-fc2299233cbe"],
    "elite_umbral_cindermaw_salamander": ["01a05c28-150e-7126-a408-5140a978d549", "01a05fa3-16b8-70f5-a0bd-cca9f316e455"],
    "elite_umbral_gravewing_siphon": ["01a05b59-9221-7660-a413-4342aeda8048"],
    "elite_umbral_veilspine_widow": ["01a05b50-9679-7402-886c-eb0c4508cb11"],
}

BLOCKED_REVIEWS = {
    "boss_eldergrove_mere_root_leviathan": PRODUCTION / "aligned_generated/renders/boss_eldergrove_mere_root_leviathan__threequarter.png",
    "boss_crownlands_meridian_tempest_roc": PRODUCTION / "alternate_generated/renders/boss_crownlands_meridian_tempest_roc__threequarter.png",
    "elite_eldergrove_sunmane_thornstag": PRODUCTION / "aligned_generated/renders/elite_eldergrove_sunmane_thornstag__threequarter.png",
    "elite_crownlands_crownstep": PRODUCTION / "aligned_generated/renders/elite_crownlands_crownstep__threequarter.png",
}

REVIEW_BOARDS = [
    ARCHIVE / "creature_2d_approval/realm_dragons_2d_revisions_board.png",
    ARCHIVE / "creature_2d_approval/boss_elite_2d_revisions_board.png",
    PRODUCTION / "quality_8k_final/quality_8k_peer_review_board.png",
    PRODUCTION / "consistency_compare/global_consistency_board.png",
    PRODUCTION / "consistency_compare/eldergrove_peer_board.png",
    PRODUCTION / "consistency_compare/crownlands_peer_board.png",
    PRODUCTION / "consistency_compare/umbral_peer_board.png",
]


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def image_record(path: Path, root: Path) -> dict[str, object]:
    record = file_record(path, root)
    with Image.open(path) as image:
        record["dimensions"] = list(image.size)
        record["mediaType"] = Image.MIME.get(image.format, "application/octet-stream")
    return record


def prepare_runtime_maps(texture_root: Path) -> None:
    for base in [p for p in texture_root.rglob("base_color.png") if "runtime_2k" not in p.parts]:
        folder = base.parent
        roughness = folder / "roughness.png"
        metallic = folder / "metallic.png"
        normal = folder / "normal.png"
        if not all(path.is_file() for path in (roughness, metallic, normal)):
            raise PacketError(f"PBR family is incomplete: {folder}")
        metal = Image.open(metallic).convert("L")
        rough = Image.open(roughness).convert("L")
        if rough.size != metal.size:
            rough = rough.resize(metal.size, Image.Resampling.LANCZOS)
        packed = folder / "metallic_smoothness.png"
        Image.merge("RGBA", (metal, metal, metal, Image.eval(rough, lambda value: 255 - value))).save(packed, optimize=True)
        ao = folder / "ao.png"
        if not ao.exists():
            Image.new("L", metal.size, 255).save(ao, optimize=True)
        runtime = folder / "runtime_2k"
        runtime.mkdir(exist_ok=True)
        for name in ("base_color.png", "normal.png", "metallic_smoothness.png", "ao.png"):
            image = Image.open(folder / name)
            image.thumbnail((2048, 2048), Image.Resampling.LANCZOS)
            image.save(runtime / name, optimize=True)


def copy_tree(source: Path, destination: Path) -> None:
    if not source.is_dir():
        raise PacketError(f"selected texture tree is missing: {source}")
    shutil.copytree(source, destination, dirs_exist_ok=True)


def copy_schema(source: Path, doc_root: Path) -> Path:
    if not source.is_file():
        raise PacketError(f"retained schema template is missing: {source}")
    destination = doc_root / "realm_creature_3d_source_manifest.schema.json"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    return destination


def selected_review(model_id: str) -> Path:
    blocked = BLOCKED_REVIEWS.get(model_id)
    if blocked:
        return blocked
    return PRODUCTION / f"consistency_compare/renders/{model_id}.png"


def build() -> None:
    approval = json.loads(APPROVAL_PATH.read_text(encoding="utf-8"))
    readiness = json.loads(READINESS_PATH.read_text(encoding="utf-8"))
    summary = validate_input_manifests(approval, readiness, SOURCE_TO_MODEL)
    for root in (DOC_ROOT, ART_ROOT):
        if root.exists():
            shutil.rmtree(root)
        root.mkdir(parents=True)
    copy_schema(SCHEMA_TEMPLATE, DOC_ROOT)

    docs_records: list[dict[str, object]] = []
    promoted_approval = dict(approval)
    promoted_entries = []
    for entry in approval["entries"]:
        promoted = dict(entry)
        promoted_sources = []
        for source in entry["sources"]:
            source_path = Path(source["path"])
            if not source_path.is_file():
                raise PacketError(f"approved 2D source is missing: {source_path}")
            if ARCHIVE in source_path.parents:
                destination = DOC_ROOT / "ConceptSheets" / source_path.name
                copy_asset(source_path, destination, DOC_ROOT)
                repo_path = destination.relative_to(REPO_ROOT).as_posix()
            else:
                repo_path = source_path.relative_to(REPO_ROOT).as_posix()
            verified = image_record(REPO_ROOT / repo_path, REPO_ROOT)
            promoted_sources.append({
                "path": repo_path,
                "sha256": verified["sha256"],
                "bytes": verified["bytes"],
                "dimensions": verified["dimensions"],
            })
        promoted["sources"] = promoted_sources
        promoted_entries.append(promoted)
    promoted_approval["entries"] = promoted_entries
    promoted_approval["documentKind"] = "anotherlife.creature-2d-approved-source.v2"
    approval_destination = DOC_ROOT / "realm_creature_2d_approval_manifest_v002.json"
    write_json(approval_destination, promoted_approval)

    for board in REVIEW_BOARDS:
        destination = DOC_ROOT / "Review" / board.name
        docs_records.append(copy_asset(board, destination, DOC_ROOT))

    model_by_id = {model["id"]: model for model in readiness["models"]}
    model_entries = []
    all_art_records: list[dict[str, object]] = []
    for source_id, model_id in SOURCE_TO_MODEL.items():
        model = model_by_id[model_id]
        selected = PRODUCTION / (model.get("file") or model.get("bestBase"))
        model_destination = ART_ROOT / "Models" / model_id / f"{model_id}_source_v001.fbx"
        model_record = copy_asset(selected, model_destination, ART_ROOT)
        all_art_records.append(model_record)

        source_textures = selected.parent / f"{selected.stem}_textures"
        texture_destination = ART_ROOT / "Textures" / model_id
        copy_tree(source_textures, texture_destination)
        if "pass" in model["status"]:
            prepare_runtime_maps(texture_destination)
        texture_records = []
        for texture in sorted(texture_destination.rglob("*.png")):
            record = image_record(texture, ART_ROOT)
            texture_records.append(record)
            all_art_records.append(record)

        review_source = selected_review(model_id)
        review_destination = ART_ROOT / "Review" / f"{model_id}_threequarter.png"
        review_record = copy_asset(review_source, review_destination, ART_ROOT)
        review_record.update(image_record(review_destination, ART_ROOT))
        all_art_records.append(review_record)

        model_entries.append({
            "source2dId": source_id,
            "modelId": model_id,
            "status": model["status"],
            "blocker": model.get("blocker"),
            "selectedSource": model_record,
            "textures": texture_records,
            "review": review_record,
            "meshyTaskIds": TASK_IDS.get(model_id, []),
            "rigged": False,
            "runtimeIntegrationState": "Blocked",
            "productionReady": False,
        })

    owner_tier_texture_packets = sum(
        has_owner_tier_texture_packet(model["textures"])
        for model in model_entries
    )
    summary["ownerTierTexturePackets"] = owner_tier_texture_packets
    summary["belowOwnerTierTexturePackets"] = len(model_entries) - owner_tier_texture_packets

    packet = {
        "schemaVersion": 1,
        "packetId": "anotherlife-realm-creature-production-source",
        "sourceVersion": SOURCE_VERSION,
        "createdAtUtc": CREATED_AT_UTC,
        "authority": {
            "finalCreativeApprover": "user",
            "approved2DSourceVersion": "anotherlife.creature-2d-approved-source.v2",
            "runtimeAuthority": False,
            "gameplayAuthority": False,
            "narrativeAuthority": False,
        },
        "readiness": {
            "technicalPacketState": "TechnicalReviewReady",
            "userCreativeState": "ApprovedSourceVersion",
            "runtimeIntegrationState": "Blocked",
            "narrativeNamingState": "WorkingLabelsOnly",
        },
        "qualityBar": {
            "style": "mystical medieval naturalism",
            "authoringTextureIntent": "8192x8192 native base color and 4096x4096 native normal/roughness/metallic for approved hero/elite production sources; 2048x2048 runtime maps are derivatives",
            "ownerTierTexturePackets": owner_tier_texture_packets,
            "belowOwnerTierTexturePackets": len(model_entries) - owner_tier_texture_packets,
            "coverageDisposition": "Only Hollowbark Stalker, Reliquary Basilisk, and Cindermaw Salamander currently carry the 8K/4K authoring dimensions. Cindermaw remains UV/bake-blocked. All lower-resolution packets are retained as review/cleanup sources, not owner-tier production textures.",
            "runtimeVfxSeparate": True,
        },
        "provenance": {
            "generatorProduct": "Meshy",
            "generatorModel": "Meshy-7",
            "taskBinding": "Each model records the Meshy task ID or IDs used for its selected candidate/rebuild.",
            "exactGenerationPromptState": "not_retained_in_this_packet",
            "promptDisposition": "Approved 2D sheets and their recorded requirements are authoritative inputs; exact Meshy 3D request payloads were not retained as a complete immutable prompt ledger, so this packet remains TechnicalReviewReady rather than TechnicalHandoffComplete.",
            "externalInputs": [
                {
                    "kind": "AnotherLife owner-approved 2D creature source",
                    "thirdParty": False,
                    "record": "realm_creature_2d_approval_manifest_v002.json"
                }
            ],
            "editingSteps": [
                "Meshy candidate generation from approved 2D source evidence",
                "Blender structural inspection and candidate selection",
                "PBR packet retention; premium procedural map rebuilds for Hollowbark, Reliquary, and Cindermaw",
                "Fail-closed review disposition with superseded/failed outputs excluded"
            ],
            "licenseEvidence": "Generated through the project owner's Meshy account from AnotherLife-approved source evidence. No third-party source image is declared as an input. This is an evidence statement, not a universal legal-clearance claim.",
            "editableSourceAvailability": "FBX and image maps are retained; native sculpt, rig, and complete editable DCC authoring files are not delivered."
        },
        "unityImportBoundary": {
            "path": "outside_unity_assets",
            "metaFilesRequired": False,
            "importSettingsRequired": False,
            "reason": "Models and textures are retained under unity/ArtSource and review media under unity/Docs; neither tree is imported by Unity. A separate runtime integration PR must create Unity Assets, .meta GUIDs, importer settings, LODs, rigs, and packaging evidence."
        },
        "lodPolicy": {
            "sourceTierMaximumTriangles": 75000,
            "balancedLodMaximumTriangles": 45000,
            "lowMobileLodMaximumTriangles": 22000,
            "requiredDerivatives": ["LOD1", "LOD2"],
            "deliveryState": "Blocked_not_generated_or_validated"
        },
        "summary": summary,
        "models": model_entries,
        "excludedSupersededOutputs": True,
    }
    manifest_destination = DOC_ROOT / "realm_creature_3d_source_manifest_v001.json"
    write_json(manifest_destination, packet)
    validate_promoted_files(ART_ROOT, all_art_records)

    readme = f"""# Realm Creature Production Source V001

This tracked packet promotes the owner-approved 2D source identities and the selected 3D review/cleanup sources for four realm dragons, Vaeloryn, four realm bosses, and twelve elites.

- Approved 2D entries: **{summary['approved2D']} / 21**
- Structural 3D passes: **{summary['structuralPass']} / 21**
- 3D cleanup/quality blockers: **{summary['blocked3D']} / 21**
- Owner-tier 8K/4K texture packets: **{summary['ownerTierTexturePackets']} / 21**
- Below owner-tier texture packets: **{summary['belowOwnerTierTexturePackets']} / 21**
- Runtime integration: **Blocked**
- Rigging: **not delivered**
- Runtime VFX: **not delivered and must remain separate**

The packet is source/review authority only. It creates no spawn, combat, reward, save, narrative, or runtime catalog authority. Entries with blockers remain useful cleanup sources but are not production-ready. Eighteen retained PBR packets are below the owner-mandated 8K-base/4K-support authoring tier and are review/cleanup evidence only. Runtime textures under `runtime_2k` are derived convenience packets, not permission to integrate the models before texture uplift, LOD, rig, animation, device, and failure-path gates pass.

All packet media is outside `unity/Assets`, so Unity `.meta` files and importer settings are intentionally absent. Runtime import is a separate engineering change.

See `realm_creature_2d_approval_manifest_v002.json` and `realm_creature_3d_source_manifest_v001.json` for immutable hashes and status.
"""
    (DOC_ROOT / "README.md").write_text(readme, encoding="utf-8")
    print(json.dumps({
        "approved2D": summary["approved2D"],
        "models": len(model_entries),
        "artFiles": len(all_art_records),
        "docReviewFiles": len(docs_records),
        "docRoot": str(DOC_ROOT),
        "artRoot": str(ART_ROOT),
    }))


if __name__ == "__main__":
    build()
