"""Fail-closed checks for the Stonehold Master Gruff 3D foundation packet."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
PACKET = ROOT / "unity" / "Docs" / "AssetLibrary" / "StoneholdMasterGruffNpc3DSourceV001"
ART = ROOT / "unity" / "ArtSource" / "NPCs" / "rct_stonehold_npc_service_v001"
MANIFEST = PACKET / "npc_3d_foundation_manifest_v001.json"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    errors: list[str] = []
    if not MANIFEST.exists():
        print("FAIL missing manifest")
        return 1
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    for artifact in manifest.get("artifacts", []):
        rel = artifact["path"].replace("/", os.sep)
        path = ROOT / rel if not Path(rel).is_absolute() else Path(rel)
        if not path.exists():
            errors.append(f"missing {artifact['path']}")
            continue
        actual = sha256(path)
        if actual != artifact["sha256"]:
            errors.append(f"hash mismatch {artifact['path']}")
    report = manifest.get("buildReport", {})
    if report.get("lod0Tris", 0) > 50000:
        errors.append(f"LOD0 {report.get('lod0Tris')} exceeds foundation candidate ceiling 50k")
    if not (report.get("lod2Tris", 0) < report.get("lod1Tris", 0) < report.get("lod0Tris", 1)):
        errors.append("LOD triangle counts are not strictly decreasing")
    if report.get("humanoidBones") != 22:
        errors.append("expected 22 Humanoid bones")
    if report.get("heightMeters", 0) < 1.3 or report.get("heightMeters", 0) > 1.55:
        errors.append(f"height {report.get('heightMeters')} outside 1.43m dwarf envelope")
    blend = ART / "rct_stonehold_npc_service_humanoid_v001.blend"
    fbx = ART / "Exports" / "rct_stonehold_npc_service_humanoid_v001.fbx"
    if not blend.exists():
        errors.append("missing blend")
    if not fbx.exists():
        errors.append("missing fbx")
    if errors:
        print("FAIL")
        for item in errors:
            print(" -", item)
        return 1
    print("PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
