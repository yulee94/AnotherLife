from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True

try:
    from PIL import Image
except ImportError:  # pragma: no cover
    Image = None  # type: ignore

ROOT = Path(__file__).resolve().parent
FAMILIES_PATH = ROOT / "families_v001.json"
MANIFEST_PATH = ROOT / "family_sheet_manifest_v001.json"
REPORT_PATH = ROOT / "validation_report_v001.json"
EXPECTED_FAMILY_COUNT = 54
REQUIRED_CATEGORIES = {
    "terrain_geology",
    "slagfall",
    "vegetation",
    "water_shore",
    "ore_crystal",
    "vfx_weather",
    "dressing",
}
TEXT_SUFFIXES = {".json", ".md", ".py"}
FORBIDDEN_DIR_NAMES = {"__pycache__", ".pytest_cache", ".mypy_cache", "tmp", "temp"}
FORBIDDEN_FILE_SUFFIXES = {".pyc", ".pyo", ".tmp", ".temp", ".bak", ".swp"}
FORBIDDEN_FILE_NAMES = {".ds_store", "thumbs.db"}
ALLOWED_UNMANIFESTED = {".gitattributes"}
GENERATED_UNMANIFESTED = {
    "family_sheet_manifest_v001.json",
    "validation_report_v001.json",
    "generation_log_v001.json",
}
GENERATION_LOG_PATH = ROOT / "generation_log_v001.json"
REQUIRED_GITATTR_LINES = (
    ".gitattributes text eol=lf",
    "*.md text eol=lf",
    "*.json text eol=lf",
    "*.py text eol=lf",
    "*.png filter=lfs diff=lfs merge=lfs -text",
)
REQUIRED_TEXT = {
    "families_v001.json",
    "generate_sheets.py",
    "validate_packet.py",
    "test_validate_packet.py",
    "README.md",
}


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def check_gitattributes() -> list[str]:
    errors: list[str] = []
    path = ROOT / ".gitattributes"
    if not path.is_file():
        return ["missing packet-local .gitattributes"]
    raw = path.read_bytes()
    if b"\r\n" in raw or b"\r" in raw:
        errors.append("non-LF line ending: .gitattributes")
    text = raw.decode("utf-8")
    normalized = {
        line.strip()
        for line in text.split("\n")
        if line.strip() and not line.strip().startswith("#")
    }
    if "*.png filter=lfs diff=lfs merge=lfs -text" not in normalized:
        errors.append("packet .gitattributes does not pin *.png to Git LFS")
    missing_lf = [line for line in REQUIRED_GITATTR_LINES if "eol=lf" in line and line not in normalized]
    if missing_lf:
        errors.append("packet .gitattributes missing text eol=lf rules")
    return errors


def check_forbidden_files() -> list[str]:
    errors: list[str] = []
    for path in ROOT.rglob("*"):
        rel = path.relative_to(ROOT).as_posix()
        parts = set(path.relative_to(ROOT).parts)
        if parts & FORBIDDEN_DIR_NAMES:
            errors.append(f"forbidden cache/temp path: {rel}")
            continue
        if path.is_file() and (
            path.suffix.lower() in FORBIDDEN_FILE_SUFFIXES or path.name.lower() in FORBIDDEN_FILE_NAMES
        ):
            errors.append(f"forbidden cache/temp path: {rel}")
    return errors


def check_lf_text(path: Path) -> list[str]:
    if path.suffix.lower() not in TEXT_SUFFIXES:
        return []
    raw = path.read_bytes()
    if b"\r\n" in raw or b"\r" in raw:
        return [f"non-LF line ending: {path.relative_to(ROOT).as_posix()}"]
    return []


def check_exact_file_set(manifest: dict) -> list[str]:
    errors: list[str] = []
    allowed = set(ALLOWED_UNMANIFESTED)
    allowed.update(GENERATED_UNMANIFESTED)
    for artifact in manifest.get("artifacts", []):
        rel = artifact.get("path")
        if rel:
            allowed.add(rel.replace("\\", "/"))
    present: set[str] = set()
    for path in ROOT.rglob("*"):
        if not path.is_file():
            continue
        rel = path.relative_to(ROOT).as_posix()
        present.add(rel)
        if rel not in allowed:
            errors.append(f"unmanifested extra file: {rel}")
    required = set(REQUIRED_TEXT)
    for family in json.loads(FAMILIES_PATH.read_text(encoding="utf-8"))["families"]:
        required.add(f"Sheets/{family['familyId']}_family_sheet_v001.png")
    for rel in sorted(required):
        if rel not in present:
            errors.append(f"missing required packet file: {rel}")
    return errors


def check_families(manifest: dict) -> list[str]:
    errors: list[str] = []
    catalog = json.loads(FAMILIES_PATH.read_text(encoding="utf-8"))
    catalog_ids = [row["familyId"] for row in catalog["families"]]
    if len(catalog_ids) != EXPECTED_FAMILY_COUNT:
        errors.append(f"families catalog count {len(catalog_ids)} != {EXPECTED_FAMILY_COUNT}")
    if len(set(catalog_ids)) != len(catalog_ids):
        errors.append("duplicate familyId in catalog")
    sheet_ids = [row["familyId"] for row in manifest.get("families", [])]
    if sheet_ids != catalog_ids:
        errors.append("manifest family order/IDs do not match families_v001.json")
    declared = manifest.get("declaredTotals", {})
    if declared.get("familyCount") != EXPECTED_FAMILY_COUNT:
        errors.append("declaredTotals.familyCount mismatch")
    if declared.get("meshyAuthorized") not in (0, False):
        errors.append("meshyAuthorized must remain 0/false")
    categories = {row["category"] for row in catalog["families"]}
    missing_cat = REQUIRED_CATEGORIES - categories
    if missing_cat:
        errors.append(f"missing required categories: {sorted(missing_cat)}")
    for row in catalog["families"]:
        rel = f"Sheets/{row['familyId']}_family_sheet_v001.png"
        path = ROOT / rel
        if not path.is_file() or path.stat().st_size <= 1000:
            errors.append(f"missing or empty family sheet: {rel}")
            continue
        if Image is not None:
            with Image.open(path) as im:
                if im.size[0] < 640 or im.size[1] < 360:
                    errors.append(f"sheet too small: {rel} {im.size}")
    return errors


def check_generation_log(manifest: dict) -> list[str]:
    errors: list[str] = []
    log_path = ROOT / "generation_log_v001.json"
    if not log_path.is_file():
        return ["missing generation_log_v001.json"]
    errors.extend(check_lf_text(log_path))
    try:
        log = json.loads(log_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return ["generation_log_v001.json is not valid JSON"]
    catalog = json.loads(FAMILIES_PATH.read_text(encoding="utf-8"))
    catalog_ids = [row["familyId"] for row in catalog["families"]]
    manifest_ids = [row["familyId"] for row in manifest.get("families", [])]
    log_ids = [row.get("familyId") for row in log.get("results", [])]
    if log_ids != catalog_ids:
        errors.append("generation-log family order/IDs do not match families_v001.json")
    if log_ids != manifest_ids:
        errors.append("generation-log family order/IDs do not match manifest families")
    if len(log_ids) != EXPECTED_FAMILY_COUNT:
        errors.append(f"generation-log count {len(log_ids)} != {EXPECTED_FAMILY_COUNT}")
    if len(set(log_ids)) != len(log_ids):
        errors.append("duplicate familyId in generation log")
    if log.get("provider") != "Grok" or log.get("model") != "4.6 High":
        errors.append("generation-log provider/model must remain Grok 4.6 High")
    if log.get("imageModel") != "grok-imagine-image-2.0":
        errors.append("generation-log imageModel must remain grok-imagine-image-2.0")
    if log.get("errors"):
        errors.append("generation-log still records errors")
    for row in log.get("results", []):
        fid = row.get("familyId")
        rel = str(row.get("path") or "").replace("\\", "/")
        expected = f"Sheets/{fid}_family_sheet_v001.png"
        if rel != expected:
            errors.append(f"generation-log path must be packet-relative {expected}")
        if not row.get("ok"):
            errors.append(f"generation-log not ok: {fid}")
        if row.get("fallback") not in (None, "none"):
            errors.append(f"generation-log fallback must be none: {fid}")
        sheet = ROOT / expected
        if sheet.is_file() and row.get("bytes") not in (None, sheet.stat().st_size):
            errors.append(f"generation-log byte length mismatch: {fid}")
    return errors


def check_hashes(manifest: dict) -> list[str]:
    errors: list[str] = []
    for artifact in manifest.get("artifacts", []):
        rel = artifact.get("path")
        path = ROOT / rel
        if not path.is_file():
            errors.append(f"artifact missing on disk: {rel}")
            continue
        digest = sha256_file(path)
        if digest != artifact.get("sha256"):
            errors.append(f"sha256 mismatch: {rel}")
        if path.stat().st_size != artifact.get("bytes"):
            errors.append(f"byte length mismatch: {rel}")
        errors.extend(check_lf_text(path))
    return errors


def check_review_integrity(manifest: dict) -> list[str]:
    """Illegal claims, not the PENDING visual gate."""
    errors: list[str] = []
    approval = manifest.get("approval", {})
    if approval.get("runtimeAuthority") is True or approval.get("releaseAuthority") is True:
        errors.append("review gate illegally grants runtime or release authority")
    if approval.get("meshyAuthorized") is True:
        errors.append("review gate illegally authorizes Meshy")
    if approval.get("decision") == "APPROVE":
        if approval.get("independentReviewVerdict") != "PASS":
            errors.append("APPROVE without independent PASS")
        if not approval.get("independentReviewId") or approval.get("independentReviewId") == "pending":
            errors.append("APPROVE without independent review id")
    state = manifest.get("readinessBoundary", {}).get("state")
    if state == "approved_2d_source_only" and approval.get("decision") != "APPROVE":
        errors.append("readiness approved_2d_source_only without APPROVE")
    return errors


def check_review_closed(manifest: dict) -> list[str]:
    errors: list[str] = []
    approval = manifest.get("approval", {})
    if approval.get("decision") != "APPROVE" or approval.get("independentReviewVerdict") != "PASS":
        errors.append("approval/review gate is not closed with APPROVE and PASS")
    if not approval.get("independentReviewId") or approval.get("independentReviewId") == "pending":
        errors.append("independent review ID is absent or pending")
    if not approval.get("summary") or approval.get("summary") == "Independent visual/source review pending.":
        errors.append("independent review summary is absent or pending")
    if manifest.get("readinessBoundary", {}).get("state") != "approved_2d_source_only":
        errors.append("readinessState is not approved_2d_source_only")
    return errors


def validate(require_review: bool = False) -> dict:
    errors: list[str] = []
    if not MANIFEST_PATH.is_file():
        report = {
            "ok": False,
            "integrityOk": False,
            "reviewClosed": False,
            "errors": ["missing family_sheet_manifest_v001.json"],
        }
        REPORT_PATH.write_bytes((json.dumps(report, indent=2) + "\n").encode("utf-8"))
        return report
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    errors.extend(check_gitattributes())
    errors.extend(check_forbidden_files())
    errors.extend(check_exact_file_set(manifest))
    errors.extend(check_families(manifest))
    errors.extend(check_generation_log(manifest))
    errors.extend(check_hashes(manifest))
    errors.extend(check_review_integrity(manifest))
    review_errors = check_review_closed(manifest)
    integrity_ok = not errors
    review_closed = not review_errors
    if require_review:
        errors.extend(review_errors)
    report = {
        "ok": not errors,
        "integrityOk": integrity_ok,
        "reviewClosed": review_closed,
        "familyCount": EXPECTED_FAMILY_COUNT,
        "requireReview": require_review,
        "errors": errors,
        "reviewErrors": review_errors,
    }
    REPORT_PATH.write_bytes((json.dumps(report, indent=2) + "\n").encode("utf-8"))
    return report


def main() -> int:
    require_review = "--require-review" in sys.argv
    report = validate(require_review=require_review)
    print(json.dumps(report, indent=2))
    if not report["integrityOk"]:
        return 1
    if require_review and not report["reviewClosed"]:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
