from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

sys.dont_write_bytecode = True

from PIL import Image

ROOT = Path(__file__).resolve().parent
MANIFEST = ROOT / "npc_2d_source_manifest_v001.json"
REPORT = ROOT / "validation_report_v001.json"
EXPECTED_IDS = {
    "rct_stonehold_npc_service_v001",
    "rct_eldergrove_npc_caretaker_v001",
    "rct_crownlands_npc_service_v001",
    "rct_umbral_npc_archivist_v001",
}
EXPECTED_VIEWS = {"front", "back", "left", "right"}
EXPECTED_DIMENSIONS = {
    "concept": [1024, 1024],
    "turnaround_view": [1024, 1024],
    "handoff_sheet": [3840, 2160],
    "lineup": [3840, 2160],
}
TEXT_SUFFIXES = {".json", ".md", ".py"}
UNRESOLVED = re.compile(r"\b(?:TODO|TBD|REPLACE_ME)\b|<[^>\n]+>")
FORBIDDEN_DIR_NAMES = {"__pycache__", ".pytest_cache", ".mypy_cache", "tmp", "temp"}
FORBIDDEN_FILE_SUFFIXES = {".pyc", ".pyo", ".tmp", ".temp", ".bak", ".swp"}
FORBIDDEN_FILE_NAMES = {".ds_store", "thumbs.db"}
ALLOWED_UNMANIFESTED = {".gitattributes"}
GENERATED_UNMANIFESTED = {"npc_2d_source_manifest_v001.json", "validation_report_v001.json"}
REQUIRED_GITATTR_LINES = (
    ".gitattributes text eol=lf",
    "*.md text eol=lf",
    "*.json text eol=lf",
    "*.py text eol=lf",
    "*.png filter=lfs diff=lfs merge=lfs -text",
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def check_gitattributes(root: Path) -> list[str]:
    errors: list[str] = []
    path = root / ".gitattributes"
    if not path.is_file():
        return ["missing packet-local .gitattributes"]
    raw = path.read_bytes()
    if b"\r\n" in raw or b"\r" in raw:
        errors.append("non-LF line ending: .gitattributes")
    text = raw.decode("utf-8")
    normalized = {line.strip() for line in text.split("\n") if line.strip() and not line.strip().startswith("#")}
    if "*.png filter=lfs diff=lfs merge=lfs -text" not in normalized:
        errors.append("packet .gitattributes does not pin *.png to Git LFS")
    missing_lf = [line for line in REQUIRED_GITATTR_LINES if "eol=lf" in line and line not in normalized]
    if missing_lf:
        errors.append("packet .gitattributes missing text eol=lf rules")
    return errors


def check_forbidden_files(root: Path) -> list[str]:
    errors: list[str] = []
    for path in root.rglob("*"):
        rel = path.relative_to(root).as_posix()
        parts = set(path.relative_to(root).parts)
        if parts & FORBIDDEN_DIR_NAMES:
            errors.append(f"forbidden cache/temp path: {rel}")
            continue
        if path.is_file() and (
            path.suffix.lower() in FORBIDDEN_FILE_SUFFIXES or path.name.lower() in FORBIDDEN_FILE_NAMES
        ):
            errors.append(f"forbidden cache/temp path: {rel}")
    return errors


def check_exact_file_set(root: Path, artifacts: list[dict]) -> list[str]:
    errors: list[str] = []
    allowed = set(ALLOWED_UNMANIFESTED)
    allowed.update(GENERATED_UNMANIFESTED)
    for artifact in artifacts:
        rel = artifact.get("path")
        if rel:
            allowed.add(rel.replace("\\", "/"))
    present: set[str] = set()
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        rel = path.relative_to(root).as_posix()
        present.add(rel)
        if rel not in allowed:
            errors.append(f"unmanifested extra file: {rel}")
    for rel in sorted(allowed - ALLOWED_UNMANIFESTED - GENERATED_UNMANIFESTED):
        if rel not in present:
            errors.append(f"missing required packet file: {rel}")
    return errors


def check_review_gates(manifest: dict) -> list[str]:
    errors: list[str] = []
    review = manifest.get("approval", {})
    if review.get("decision") != "APPROVE" or review.get("independentReviewVerdict") != "PASS":
        errors.append("approval/review gate is not closed with APPROVE and PASS")
    if not review.get("independentReviewId") or review.get("independentReviewId") == "pending":
        errors.append("independent review ID is absent or pending")
    if not review.get("summary") or review.get("summary") == "Independent visual/source review pending.":
        errors.append("independent review summary is absent or pending")
    if review.get("runtimeAuthority") is True or review.get("releaseAuthority") is True:
        errors.append("review gate illegally grants runtime or release authority")
    for npc in manifest.get("npcs", []):
        roster_id = npc.get("rosterId", "missing")
        if npc.get("downstream3DReady") is True and review.get("decision") != "APPROVE":
            errors.append(f"{roster_id}: downstream3DReady is true before independent review")
        if npc.get("readinessState") != "approved_2d_source_only":
            errors.append(f"{roster_id}: readiness is not approved 2D-only")
    return errors


def main() -> int:
    errors: list[str] = []
    checks: list[str] = []

    if not MANIFEST.is_file():
        errors.append("missing manifest")
        manifest = {}
    else:
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        checks.append("manifest parses as UTF-8 JSON")

    npcs = manifest.get("npcs", [])
    ids = [entry.get("rosterId") for entry in npcs]
    if len(npcs) != 4 or set(ids) != EXPECTED_IDS or len(ids) != len(set(ids)):
        errors.append(f"roster mismatch: {ids}")
    else:
        checks.append("exact four canonical roster IDs are bound once")

    for npc in npcs:
        roster_id = npc.get("rosterId", "missing")
        if not npc.get("sourceAuthority"):
            errors.append(f"{roster_id}: missing source authority")
        if not npc.get("roleActionKeys"):
            errors.append(f"{roster_id}: missing role action key")
        profiles = npc.get("intendedProfiles", {})
        required_profiles = {
            "body", "equipment", "rig", "face", "lod", "collider", "platform"
        }
        if set(profiles) != required_profiles:
            errors.append(f"{roster_id}: intended profile fields mismatch")
        for key, value in profiles.items():
            if not isinstance(value, dict) or "intent" not in value or "catalogIds" not in value:
                errors.append(f"{roster_id}: malformed {key} profile binding")
        provenance = npc.get("generationProvenance", {})
        required_generation = {"concept", "frontView", "backView", "leftView", "rightView"}
        if set(provenance) != required_generation:
            errors.append(f"{roster_id}: incomplete generation provenance")
        for key, record in provenance.items():
            if (
                not record.get("provider")
                or not record.get("tool")
                or not record.get("taskId")
                or not record.get("model")
                or not record.get("prompt")
            ):
                errors.append(f"{roster_id}: incomplete provenance for {key}")
            if "seed" not in record or not record.get("seedStatus"):
                errors.append(f"{roster_id}: absent seed provenance for {key}")

    review_errors = check_review_gates(manifest)
    errors.extend(review_errors)
    if not review_errors:
        checks.append("delegated owner approval follows a clean independent review")

    artifacts = manifest.get("artifacts", [])
    bound_paths: list[str] = []
    hashes: dict[str, str] = {}
    view_coverage = {roster_id: set() for roster_id in EXPECTED_IDS}
    concept_coverage: set[str] = set()
    handoff_coverage: set[str] = set()
    lineup_count = 0

    for artifact in artifacts:
        rel = artifact.get("path")
        if not rel or rel in bound_paths:
            errors.append(f"duplicate or absent artifact path: {rel}")
            continue
        bound_paths.append(rel)
        path = ROOT / rel
        if not path.is_file():
            errors.append(f"missing artifact: {rel}")
            continue
        actual_hash = sha256(path)
        declared_hash = artifact.get("sha256")
        if not isinstance(declared_hash, str) or not re.fullmatch(r"[0-9a-f]{64}", declared_hash):
            errors.append(f"absent or malformed SHA-256: {rel}")
        if declared_hash != actual_hash:
            errors.append(f"stale hash: {rel}")
        if actual_hash in hashes:
            errors.append(f"duplicate artifact bytes: {rel} and {hashes[actual_hash]}")
        else:
            hashes[actual_hash] = rel
        if not artifact.get("provenance"):
            errors.append(f"absent provenance: {rel}")

        if path.suffix.lower() == ".png":
            with Image.open(path) as image:
                dimensions = [image.width, image.height]
            if artifact.get("dimensions") != dimensions:
                errors.append(f"wrong dimensions: {rel} expected {artifact.get('dimensions')} got {dimensions}")

        role = artifact.get("role")
        roster_id = artifact.get("rosterId")
        expected_dimensions = EXPECTED_DIMENSIONS.get(role)
        if expected_dimensions is not None and artifact.get("dimensions") != expected_dimensions:
            errors.append(
                f"role dimension violation: {rel} expected {expected_dimensions} "
                f"got {artifact.get('dimensions')}"
            )
        if role == "concept":
            concept_coverage.add(roster_id)
        elif role == "handoff_sheet":
            handoff_coverage.add(roster_id)
            callouts = set(artifact.get("callouts", []))
            required_callouts = {"face", "modules", "materials", "scale", "rig", "lod", "collider"}
            if not required_callouts.issubset(callouts):
                errors.append(f"missing handoff callouts: {rel}")
        elif role == "turnaround_view":
            view_coverage.setdefault(roster_id, set()).add(artifact.get("view"))
        elif role == "lineup":
            lineup_count += 1
            if not all(artifact.get(key) is True for key in ("commonScale", "commonCamera", "commonLighting")):
                errors.append(f"lineup treatment flags incomplete: {rel}")

    if concept_coverage != EXPECTED_IDS:
        errors.append(f"concept coverage mismatch: {sorted(concept_coverage)}")
    if handoff_coverage != EXPECTED_IDS:
        errors.append(f"handoff coverage mismatch: {sorted(handoff_coverage)}")
    for roster_id, views in view_coverage.items():
        if views != EXPECTED_VIEWS:
            errors.append(f"{roster_id}: view coverage mismatch {sorted(views)}")
    if lineup_count != 1:
        errors.append(f"lineup count is {lineup_count}, expected 1")

    packet_pngs = {
        path.relative_to(ROOT).as_posix()
        for path in ROOT.rglob("*.png")
    }
    bound_pngs = {
        artifact["path"]
        for artifact in artifacts
        if artifact.get("path", "").lower().endswith(".png")
    }
    if packet_pngs != bound_pngs:
        errors.append(
            f"unbound/missing PNGs: disk_only={sorted(packet_pngs - bound_pngs)} "
            f"manifest_only={sorted(bound_pngs - packet_pngs)}"
        )
    else:
        checks.append(f"all {len(packet_pngs)} PNGs are uniquely manifest-bound")

    gitattributes_errors = check_gitattributes(ROOT)
    errors.extend(gitattributes_errors)
    if not gitattributes_errors:
        checks.append("packet-local .gitattributes pins PNG LFS and text LF")

    forbidden_errors = check_forbidden_files(ROOT)
    errors.extend(forbidden_errors)
    if not forbidden_errors:
        checks.append("packet contains no cache/temp files")

    fileset_errors = check_exact_file_set(ROOT, artifacts)
    errors.extend(fileset_errors)
    if not fileset_errors:
        checks.append("packet file set matches artifacts plus allowed .gitattributes")

    text_paths = [
        ROOT / "README.md",
        ROOT / "validate_packet.py",
        ROOT / "build_packet.py",
        ROOT / "test_validate_packet.py",
        MANIFEST,
        ROOT / ".gitattributes",
    ]
    for path in text_paths:
        if not path.is_file():
            errors.append(f"missing text artifact: {path.name}")
            continue
        raw = path.read_bytes()
        if b"\r\n" in raw or b"\r" in raw:
            errors.append(f"non-LF line ending: {path.name}")
        text = raw.decode("utf-8")
        if path.name in {"README.md", MANIFEST.name} and UNRESOLVED.search(text):
            errors.append(f"unresolved placeholder token: {path.name}")
    if not any(error.startswith("non-LF") for error in errors):
        checks.append("tracked packet text uses LF line endings")

    result = "PASS" if not errors else "FAIL"
    report = {
        "packetId": manifest.get("packetId"),
        "result": result,
        "rosterCount": len(npcs),
        "artifactCount": len(artifacts),
        "imageCount": len(packet_pngs),
        "checks": checks,
        "errors": errors,
        "manifestSha256": sha256(MANIFEST) if MANIFEST.is_file() else None,
    }
    REPORT.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return 0 if result == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
