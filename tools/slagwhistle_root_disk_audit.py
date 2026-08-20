"""Root-task independent disk/YAML/SHA audit for Slagwhistle LOD0 (t_b8b483e2)."""
from __future__ import annotations

import hashlib
import json
import os
import struct

ROOT = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_b8b483e2"
OUT = os.path.join(
    ROOT,
    r"unity\ArtSource\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\tdf_fauna_stonehold_slagwhistle_burrower_root_disk.json",
)

PIN_IDENTITY = "1a08581ef2a49d56f3e3b5a9925a88ee7eebcb6df2895de61691f74b820eaa05"
PIN_MOTION = "1099937075dba7012545afb7636e100c592c561c30c2fe68ce7a434ca4ff2d92"
PIN_HABITAT = "600a76d983f0cb63abf1169b7a9cdf34477b60ebf2e10ca9f74883efd899d195"

FILES = {
    "blend": r"unity\ArtSource\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\tdf_fauna_stonehold_slagwhistle_burrower_working_v001.blend",
    "fbx": r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Meshes\tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.fbx",
    "glb": r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Meshes\tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.glb",
    "color": r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Textures\tdf_fauna_stonehold_slagwhistle_burrower_color_1k_v001.png",
    "normal": r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Textures\tdf_fauna_stonehold_slagwhistle_burrower_normal_1k_v001.png",
    "packed": r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Textures\tdf_fauna_stonehold_slagwhistle_burrower_packed_1k_v001.png",
    "metallic_derived": r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Materials\tdf_fauna_stonehold_slagwhistle_burrower_metallicgloss_derived_1k_v001.png",
    "material": r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Materials\M_Slagwhistle_LOD0.mat",
    "prefab": r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Prefabs\tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.prefab",
    "scene": r"unity\Assets\AL\Scenes\Prototype\Terrestrials\SlagfallQuarryRepresentativeSlice.unity",
    "prefab_meta": r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Prefabs\tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.prefab.meta",
    "identity": r"unity\Docs\Terrestrials\Ecosystems\SlagfallQuarryV002\ConceptSheets\tdf_fauna_stonehold_slagwhistle_burrower_identity_v002.png",
    "motion": r"unity\Docs\Terrestrials\Ecosystems\SlagfallQuarryV002\ConceptSheets\tdf_fauna_stonehold_slagwhistle_burrower_motion_contact_v002.png",
    "habitat": r"unity\Docs\Terrestrials\Ecosystems\SlagfallQuarryV002\ConceptSheets\tdf_habitat_stonehold_slagfall_quarry_master_v002.png",
    "build_settings": r"unity\ProjectSettings\EditorBuildSettings.asset",
}


def sha256_file(path: str) -> tuple[int, str]:
    h = hashlib.sha256()
    size = 0
    with open(path, "rb") as fh:
        while True:
            chunk = fh.read(1024 * 1024)
            if not chunk:
                break
            size += len(chunk)
            h.update(chunk)
    return size, h.hexdigest()


def png_ihdr(path: str) -> dict | None:
    with open(path, "rb") as fh:
        sig = fh.read(8)
        if sig != b"\x89PNG\r\n\x1a\n":
            return None
        length = struct.unpack(">I", fh.read(4))[0]
        ctype = fh.read(4)
        if ctype != b"IHDR" or length != 13:
            return None
        data = fh.read(13)
        w, h, bit, color, *_ = struct.unpack(">IIBBBBB", data)
        return {"width": w, "height": h, "bit_depth": bit, "color_type": color}


def grep_dir(root: str, needles: list[str]) -> dict[str, list[str]]:
    hits: dict[str, list[str]] = {n: [] for n in needles}
    if not os.path.isdir(root):
        return hits
    for dirpath, _, filenames in os.walk(root):
        for name in filenames:
            path = os.path.join(dirpath, name)
            try:
                text = open(path, "r", encoding="utf-8", errors="ignore").read()
            except OSError:
                continue
            lower = text.lower()
            for n in needles:
                if n.lower() in lower:
                    rel = os.path.relpath(path, ROOT).replace("\\", "/")
                    hits[n].append(rel)
    return hits


def main() -> None:
    files = {}
    for key, rel in FILES.items():
        path = os.path.join(ROOT, rel)
        rec: dict = {"rel": rel.replace("\\", "/"), "exists": os.path.isfile(path)}
        if rec["exists"]:
            size, digest = sha256_file(path)
            rec["bytes"] = size
            rec["sha256"] = digest
            if path.lower().endswith(".png"):
                rec["png"] = png_ihdr(path)
        files[key] = rec

    prefab_guid = None
    meta = os.path.join(ROOT, FILES["prefab_meta"])
    if os.path.isfile(meta):
        for line in open(meta, encoding="utf-8"):
            if line.startswith("guid:"):
                prefab_guid = line.split(":", 1)[1].strip()

    scene_text = ""
    scene_path = os.path.join(ROOT, FILES["scene"])
    if os.path.isfile(scene_path):
        scene_text = open(scene_path, encoding="utf-8").read()

    gamedata = os.path.join(ROOT, r"unity\Assets\AL\StreamingAssets\GameData")
    catalog = grep_dir(gamedata, ["slagwhistle", "tdf_fauna_stonehold_slagwhistle"])

    tests_edit = os.path.join(ROOT, r"unity\Assets\AL\Tests\EditMode\Terrestrials")
    tests_play = os.path.join(ROOT, r"unity\Assets\AL\Tests\PlayMode\Terrestrials")

    anim_dir = os.path.join(
        ROOT, r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Animations"
    )
    anim_entries = sorted(os.listdir(anim_dir)) if os.path.isdir(anim_dir) else []

    fauna_root = os.path.join(
        ROOT, r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle"
    )
    lod_hits = []
    if os.path.isdir(fauna_root):
        for dirpath, _, filenames in os.walk(fauna_root):
            for name in filenames:
                low = name.lower()
                if any(k in low for k in ("lod1", "lod2", "impostor", "imposter")):
                    lod_hits.append(os.path.join(dirpath, name))

    authored = 0
    for key in ("fbx", "color", "normal", "packed"):
        authored += files[key].get("bytes", 0)
    plus_derived = authored + files["metallic_derived"].get("bytes", 0)

    payload = {
        "task": "t_b8b483e2",
        "files": files,
        "prefab_guid": prefab_guid,
        "scene_has_prefab_guid": bool(prefab_guid and prefab_guid in scene_text),
        "scene_prefab_guid_count": scene_text.count(prefab_guid) if prefab_guid else 0,
        "scene_instance_name": "Slagwhistle_Burrower_LOD0" in scene_text,
        "catalog_hits": catalog,
        "tests": {
            "editmode_terrestrials_dir": os.path.isdir(tests_edit),
            "playmode_terrestrials_dir": os.path.isdir(tests_play),
        },
        "animation_folder": anim_entries,
        "lod_named_files": lod_hits,
        "source_pins": {
            "identity": {
                "pinned": PIN_IDENTITY,
                "match": files["identity"].get("sha256") == PIN_IDENTITY,
                "bytes_match": files["identity"].get("bytes") == 2521039,
            },
            "motion": {
                "pinned": PIN_MOTION,
                "match": files["motion"].get("sha256") == PIN_MOTION,
                "bytes_match": files["motion"].get("bytes") == 2617228,
            },
            "habitat": {
                "pinned": PIN_HABITAT,
                "match": files["habitat"].get("sha256") == PIN_HABITAT,
                "bytes_match": files["habitat"].get("bytes") == 3133264,
            },
        },
        "unique_compressed": {
            "authored_runtime_bytes": authored,
            "authored_runtime_mib": round(authored / (1024 * 1024), 4),
            "plus_derived_bytes": plus_derived,
            "plus_derived_mib": round(plus_derived / (1024 * 1024), 4),
        },
    }

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, indent=2)
    print("WROTE", OUT)
    print("FBX_SHA", files["fbx"].get("sha256"), files["fbx"].get("bytes"))
    print("BLEND_SHA", files["blend"].get("sha256"), files["blend"].get("bytes"))
    print("PREFAB_GUID", prefab_guid, "IN_SCENE", payload["scene_has_prefab_guid"])
    print("CATALOG", catalog)
    print("PINS", payload["source_pins"])
    print("COMPRESSED", payload["unique_compressed"])
    print("ANIMS", anim_entries)
    print("LODS", lod_hits)


if __name__ == "__main__":
    main()
