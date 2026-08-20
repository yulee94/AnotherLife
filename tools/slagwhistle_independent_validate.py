"""Independent Slagwhistle validation (t_ec244ffa).

Measures on-disk Unity/DCC deliverables against the A1 budget table.
Does not import or reuse producer verify scripts.
"""
from __future__ import annotations

import hashlib
import json
import struct
from pathlib import Path

IMPORT_ROOT = Path(r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_bb2a487f")
CLEANUP_ROOT = Path(r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_1690c393")
THIS_ROOT = Path(r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_ec244ffa")
OUT = THIS_ROOT / "unity" / "ArtSource" / "Terrestrials" / "Stonehold" / "SlagfallQuarry" / "Fauna" / "Slagwhistle" / "tdf_fauna_stonehold_slagwhistle_burrower_independent_disk.json"

PINNED_SOURCES = {
    "identity": {
        "rel": "unity/Docs/Terrestrials/Ecosystems/SlagfallQuarryV002/ConceptSheets/tdf_fauna_stonehold_slagwhistle_burrower_identity_v002.png",
        "sha256": "1a08581ef2a49d56f3e3b5a9925a88ee7eebcb6df2895de61691f74b820eaa05",
        "bytes": 2521039,
    },
    "motion": {
        "rel": "unity/Docs/Terrestrials/Ecosystems/SlagfallQuarryV002/ConceptSheets/tdf_fauna_stonehold_slagwhistle_burrower_motion_contact_v002.png",
        "sha256": "1099937075dba7012545afb7636e100c592c561c30c2fe68ce7a434ca4ff2d92",
        "bytes": 2617228,
    },
}

RUNTIME_FILES = {
    "blend": "unity/ArtSource/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/tdf_fauna_stonehold_slagwhistle_burrower_working_v001.blend",
    "fbx": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Meshes/tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.fbx",
    "glb": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Meshes/tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.glb",
    "color": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Textures/tdf_fauna_stonehold_slagwhistle_burrower_color_1k_v001.png",
    "normal": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Textures/tdf_fauna_stonehold_slagwhistle_burrower_normal_1k_v001.png",
    "packed": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Textures/tdf_fauna_stonehold_slagwhistle_burrower_packed_1k_v001.png",
    "metallic_gloss_derived": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Materials/tdf_fauna_stonehold_slagwhistle_burrower_metallicgloss_derived_1k_v001.png",
    "material": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Materials/M_Slagwhistle_LOD0.mat",
    "prefab": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Prefabs/tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.prefab",
    "scene": "unity/Assets/AL/Scenes/Prototype/Terrestrials/SlagfallQuarryRepresentativeSlice.unity",
    "fbx_meta": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Meshes/tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.fbx.meta",
    "prefab_meta": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Prefabs/tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.prefab.meta",
    "color_meta": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Textures/tdf_fauna_stonehold_slagwhistle_burrower_color_1k_v001.png.meta",
    "normal_meta": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Textures/tdf_fauna_stonehold_slagwhistle_burrower_normal_1k_v001.png.meta",
    "packed_meta": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Textures/tdf_fauna_stonehold_slagwhistle_burrower_packed_1k_v001.png.meta",
    "metallic_meta": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Materials/tdf_fauna_stonehold_slagwhistle_burrower_metallicgloss_derived_1k_v001.png.meta",
    "material_meta": "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Materials/M_Slagwhistle_LOD0.mat.meta",
}


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def png_ihdr(path: Path):
    with path.open("rb") as fh:
        if fh.read(8) != b"\x89PNG\r\n\x1a\n":
            return None
        length, ctype = struct.unpack(">I4s", fh.read(8))
        if ctype != b"IHDR":
            return None
        w, h, bit, color_type = struct.unpack(">IIBB", fh.read(10))
        return {"width": w, "height": h, "bit_depth": bit, "color_type": color_type, "ihdr_length": length}


def read_guid(meta_path: Path) -> str | None:
    for line in meta_path.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("guid:"):
            return line.split(":", 1)[1].strip()
    return None


def extract_meta_keys(meta_path: Path, keys: list[str]) -> dict:
    out = {}
    for line in meta_path.read_text(encoding="utf-8", errors="replace").splitlines():
        stripped = line.strip()
        for key in keys:
            prefix = f"{key}:"
            if stripped.startswith(prefix):
                out[key] = stripped[len(prefix) :].strip()
    return out


def search_text(path: Path, needle: str) -> int:
    if not path.exists():
        return 0
    return path.read_text(encoding="utf-8", errors="replace").count(needle)


def walk_matches(root: Path, needle: str, globs: list[str]) -> list[str]:
    hits = []
    for pattern in globs:
        for p in root.glob(pattern):
            try:
                text = p.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            if needle.lower() in text.lower():
                hits.append(str(p.relative_to(root)).replace("\\", "/"))
    return sorted(set(hits))


def measure_file(root: Path, rel: str) -> dict:
    path = root / rel
    rec = {"rel": rel, "exists": path.exists(), "bytes": None, "sha256": None}
    if path.exists():
        rec["bytes"] = path.stat().st_size
        rec["sha256"] = sha256_file(path)
        if path.suffix.lower() == ".png":
            rec["png"] = png_ihdr(path)
    return rec


def main() -> None:
    report = {
        "task": "t_ec244ffa",
        "validator": "independent disk/yaml/png (not producer scripts)",
        "import_root": str(IMPORT_ROOT),
        "cleanup_root": str(CLEANUP_ROOT),
        "files": {},
        "guids": {},
        "fbx_importer": {},
        "unity_binding": {},
        "catalog_hits": {},
        "tests_hits": {},
        "build_settings": {},
        "source_pins": {},
        "unique_compressed": {},
        "lod_presence": {},
        "animation_folder": {},
    }

    for key, rel in RUNTIME_FILES.items():
        report["files"][key] = measure_file(IMPORT_ROOT, rel)

    # Cross-check cleanup worktree blend/fbx hashes vs import worktree.
    report["cleanup_vs_import"] = {}
    for key in ("blend", "fbx", "glb", "color", "normal", "packed"):
        a = measure_file(CLEANUP_ROOT, RUNTIME_FILES[key])
        b = report["files"][key]
        report["cleanup_vs_import"][key] = {
            "cleanup_exists": a["exists"],
            "import_exists": b["exists"],
            "sha_match": bool(a["exists"] and b["exists"] and a["sha256"] == b["sha256"]),
            "cleanup_sha256": a["sha256"],
            "import_sha256": b["sha256"],
        }

    for key in ("fbx", "prefab", "color", "normal", "packed", "metallic_gloss_derived", "material"):
        meta_key = {
            "fbx": "fbx_meta",
            "prefab": "prefab_meta",
            "color": "color_meta",
            "normal": "normal_meta",
            "packed": "packed_meta",
            "metallic_gloss_derived": "metallic_meta",
            "material": "material_meta",
        }[key]
        meta_path = IMPORT_ROOT / RUNTIME_FILES[meta_key]
        report["guids"][key] = read_guid(meta_path) if meta_path.exists() else None

    fbx_meta = IMPORT_ROOT / RUNTIME_FILES["fbx_meta"]
    report["fbx_importer"] = extract_meta_keys(
        fbx_meta,
        [
            "globalScale",
            "bakeAxisConversion",
            "useFileUnits",
            "useFileScale",
            "importAnimation",
            "animationType",
            "avatarSetup",
            "maxBonesPerVertex",
            "materialImportMode",
        ],
    )
    fbx_meta_text = fbx_meta.read_text(encoding="utf-8", errors="replace")
    report["fbx_importer"]["clipAnimations_empty"] = "clipAnimations: []" in fbx_meta_text
    report["fbx_importer"]["referencedClips_empty"] = "referencedClips: []" in fbx_meta_text

    prefab_text = (IMPORT_ROOT / RUNTIME_FILES["prefab"]).read_text(encoding="utf-8", errors="replace")
    scene_text = (IMPORT_ROOT / RUNTIME_FILES["scene"]).read_text(encoding="utf-8", errors="replace")
    mat_text = (IMPORT_ROOT / RUNTIME_FILES["material"]).read_text(encoding="utf-8", errors="replace")
    build_text = (IMPORT_ROOT / "unity/ProjectSettings/EditorBuildSettings.asset").read_text(
        encoding="utf-8", errors="replace"
    )

    prefab_guid = report["guids"]["prefab"]
    fbx_guid = report["guids"]["fbx"]
    report["unity_binding"] = {
        "prefab_sources_fbx_guid": fbx_guid is not None and f"guid: {fbx_guid}" in prefab_text,
        "prefab_identity_transform": all(
            token in prefab_text
            for token in (
                "propertyPath: m_LocalPosition.x\n      value: 0",
                "propertyPath: m_LocalPosition.y\n      value: 0",
                "propertyPath: m_LocalPosition.z\n      value: 0",
                "propertyPath: m_LocalRotation.w\n      value: 1",
                "propertyPath: m_LocalRotation.x\n      value: 0",
                "propertyPath: m_LocalRotation.y\n      value: 0",
                "propertyPath: m_LocalRotation.z\n      value: 0",
            )
        ),
        "prefab_material_override_guid": report["guids"]["material"],
        "prefab_material_assigned": report["guids"]["material"] is not None
        and report["guids"]["material"] in prefab_text,
        "scene_instances_prefab_guid": prefab_guid is not None and scene_text.count(f"guid: {prefab_guid}") > 0,
        "scene_prefab_guid_count": scene_text.count(f"guid: {prefab_guid}") if prefab_guid else 0,
        "scene_identity_transform": all(
            token in scene_text
            for token in (
                "propertyPath: m_LocalPosition.x\n      value: 0",
                "propertyPath: m_LocalPosition.y\n      value: 0",
                "propertyPath: m_LocalPosition.z\n      value: 0",
                "propertyPath: m_LocalRotation.w\n      value: 1",
            )
        ),
        "scene_instance_name": "Slagwhistle_Burrower_LOD0" in scene_text,
        "material_maintex_color": report["guids"]["color"] is not None and report["guids"]["color"] in mat_text,
        "material_bump_normal": report["guids"]["normal"] is not None and report["guids"]["normal"] in mat_text,
        "material_occlusion_packed": report["guids"]["packed"] is not None and report["guids"]["packed"] in mat_text,
        "material_metallic_derived": report["guids"]["metallic_gloss_derived"] is not None
        and report["guids"]["metallic_gloss_derived"] in mat_text,
        "material_shader": "guid: 0000000000000000f000000000000000" in mat_text,
        "material_keywords": ["_METALLICGLOSSMAP" in mat_text, "_NORMALMAP" in mat_text],
    }

    gamedata = IMPORT_ROOT / "unity/Assets/AL/StreamingAssets/GameData"
    report["catalog_hits"]["slagwhistle"] = walk_matches(gamedata, "slagwhistle", ["**/*.json", "**/*.txt"])
    report["catalog_hits"]["tdf_fauna_stonehold"] = walk_matches(
        gamedata, "tdf_fauna_stonehold_slagwhistle", ["**/*.json", "**/*.txt"]
    )

    tests = IMPORT_ROOT / "unity/Assets/AL/Tests"
    report["tests_hits"]["slagwhistle"] = walk_matches(tests, "slagwhistle", ["**/*.cs", "**/*.asmdef"])
    report["tests_hits"]["editmode_terrestrials_dir"] = (
        IMPORT_ROOT / "unity/Assets/AL/Tests/EditMode/Terrestrials"
    ).exists()
    report["tests_hits"]["playmode_terrestrials_dir"] = (
        IMPORT_ROOT / "unity/Assets/AL/Tests/PlayMode/Terrestrials"
    ).exists()

    report["build_settings"] = {
        "mentions_slagfall_slice": "SlagfallQuarryRepresentativeSlice" in build_text,
        "mentions_slagwhistle": "slagwhistle" in build_text.lower(),
        "enabled_scene_paths": [
            line.split(":", 1)[1].strip()
            for line in build_text.splitlines()
            if line.strip().startswith("path:")
        ],
    }

    for name, spec in PINNED_SOURCES.items():
        rec = measure_file(THIS_ROOT if (THIS_ROOT / spec["rel"]).exists() else IMPORT_ROOT, spec["rel"])
        rec["pinned_sha256"] = spec["sha256"]
        rec["pinned_bytes"] = spec["bytes"]
        rec["sha_match"] = rec["sha256"] == spec["sha256"]
        rec["bytes_match"] = rec["bytes"] == spec["bytes"]
        report["source_pins"][name] = rec

    authored = ["fbx", "color", "normal", "packed"]
    authored_bytes = sum(report["files"][k]["bytes"] or 0 for k in authored)
    plus_derived = authored_bytes + (report["files"]["metallic_gloss_derived"]["bytes"] or 0)
    report["unique_compressed"] = {
        "authored_runtime_bytes": authored_bytes,
        "authored_runtime_mib": round(authored_bytes / 1024 / 1024, 4),
        "plus_derived_bytes": plus_derived,
        "plus_derived_mib": round(plus_derived / 1024 / 1024, 4),
        "target_mib": [3, 4],
        "hard_max_mib": 7,
        "excludes": ["blend (editable source)", "glb (duplicate export)", "prefab/mat yaml"],
    }

    fauna = IMPORT_ROOT / "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle"
    names = [p.name.lower() for p in fauna.rglob("*") if p.is_file()]
    report["lod_presence"] = {
        "lod0": any("lod0" in n for n in names),
        "lod1": any("lod1" in n for n in names),
        "lod2": any("lod2" in n for n in names),
        "impostor": any("impostor" in n or "imposter" in n for n in names),
        "file_count": len(names),
    }

    anim_dir = fauna / "Animations"
    report["animation_folder"] = {
        "exists": anim_dir.exists(),
        "entries": sorted(p.name for p in anim_dir.iterdir()) if anim_dir.exists() else [],
    }

    # Required path layout
    required_dirs = [
        "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Meshes",
        "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Materials",
        "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Textures",
        "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Animations",
        "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Prefabs",
        "unity/Assets/AL/Scenes/Prototype/Terrestrials",
    ]
    report["required_paths"] = {d: (IMPORT_ROOT / d).is_dir() for d in required_dirs}

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    print(f"WROTE {OUT}")


if __name__ == "__main__":
    main()
