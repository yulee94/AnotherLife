#!/usr/bin/env python3
"""Audit a blocked building evidence packet, without production writes or activation."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_six_family_production_authority as authority  # noqa: E402

DEFAULT_PACKET_PATH = "unity/Docs/GameDataCatalog/building-production-evidence.v1.json"
AUDITED_REVISION = "a958885b1c99c3bfaa1df191099b49761f2ff425"
GAME_DATA = "unity/Assets/AL/StreamingAssets/GameData"
SOURCE_PATHS = {
    "authority_ledger": authority.DEFAULT_LEDGER_PATH,
    "technical_source": "unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source-v003.json",
    "building_art": GAME_DATA + "/al_building_catalog.json",
    "wire_buildings": GAME_DATA + "/buildings.json",
    "profile_loader": "unity/Assets/AL/Scripts/Services/Local/KingdomProductionProfileCatalog.cs",
    "contribution_provider": "unity/Assets/AL/Scripts/Services/Local/KingdomProductionContributionProvider.cs",
    "live_consumer": "unity/Assets/AL/Scripts/Services/Local/KingdomProductionConsumer.cs",
    "provider_test_fixtures": "unity/Assets/AL/Tests/EditMode/KingdomProductionContributionProviderTests.cs",
}
MISSING_FIELDS = ["outputs", "rates", "caps", "resourceBindings", "failurePolicy"]
BLOCKERS = ["buildings.production_profiles", "buildings.asset_refs"]

ValidationError = authority.ValidationError


def fail(message: str) -> None:
    raise ValidationError("AL-BUILDING-EVIDENCE-INVALID: " + message)


def read_source(repo_root: Path, relative_path: str) -> bytes:
    """Read a fixed revision; compare checkout text after CRLF normalization only."""
    result = subprocess.run(
        ["git", "show", f"{AUDITED_REVISION}:{relative_path}"],
        cwd=repo_root, capture_output=True, check=False,
    )
    if result.returncode:
        fail(f"pinned Git source missing: {relative_path}")
    try:
        current = (repo_root / relative_path).read_bytes()
    except OSError as error:
        fail(f"current source unavailable: {relative_path}: {error}")
    # Every source in this packet is UTF-8 JSON, C#, YAML prefab or meta text.
    if current.replace(b"\r\n", b"\n") != result.stdout.replace(b"\r\n", b"\n"):
        fail(f"current source drift: {relative_path}; version and review new evidence")
    return result.stdout


def expected_packet(repo_root: Path) -> dict:
    """Derive observations only from the pinned audit; this is not a generator."""
    blobs = {role: read_source(repo_root, path) for role, path in SOURCE_PATHS.items()}
    technical = json.loads(blobs["technical_source"])
    building = next(row for row in technical["families"] if row["family"] == "buildings")
    art = json.loads(blobs["building_art"])["buildings"]
    wire = json.loads(blobs["wire_buildings"])["records"]
    bindings = building["progressionBindings"]
    ids = [row["canonicalId"] for row in bindings]
    if [row["id"] for row in art] != ids or [row["id"] for row in wire] != ids:
        fail("source building identity/order mismatch")
    rows = []
    for progression, models, runtime in zip(bindings, art, wire):
        rows.append({
            "canonicalId": progression["canonicalId"],
            "progressionEvidence": progression,
            "productionStatus": "unavailable",
            "productionProfile": None,
            "missingProductionFields": list(MISSING_FIELDS),
            "rejectedWireProfileIds": runtime["production_profile_ids"],
            "rejectedWireAssetRef": runtime["asset_ref"],
            "assetDimension": "realm" if models["models"] else "unavailable",
            "modelEvidence": models["models"],
            "modelEvidenceStatus": "partial_family_evidence" if models["models"] else "unavailable",
        })
    return {
        "schemaVersion": 1,
        "packetId": "al_building_production_evidence_v1",
        "auditedRevision": AUDITED_REVISION,
        "evidenceKind": "blocked_source_audit_not_production_authority",
        "sources": [
            {"role": role, "path": path, "sourceRevision": AUDITED_REVISION,
             "rawSha256": authority.sha256(blobs[role])}
            for role, path in SOURCE_PATHS.items()
        ],
        "productionEligible": False,
        "resolvedBlockerIds": [],
        "blockers": [
            {"id": BLOCKERS[0], "type": "behavior", "status": "open",
             "evidenceStatus": "provider_implemented_source_missing"},
            {"id": BLOCKERS[1], "type": "asset", "status": "open",
             "evidenceStatus": "partial_realm_dimensional"},
        ],
        "productionSourceObservation": {
            "providerImplemented": True,
            "consumerRegistered": True,
            "profileRecords": "absent_in_canonical_gamedata",
            "testFixtures": "not_balance_authority",
            "nonProducingPolicy": "recommend_explicit_profiles_not_inferred_zero_rates",
            "runtimeDefaults": "not_production_authority",
            "pr417": "unaccepted_not_source_authority",
        },
        "buildings": rows,
        "unavailableAnchors": ["ManaShrine", "Mine"],
        "generationGate": {"status": "blocked", "outputPaths": [], "activationTargets": []},
    }


def verify_model_sources(packet: dict, repo_root: Path) -> None:
    for building in packet["buildings"]:
        for model in building["modelEvidence"]:
            asset = model["asset_ref"]
            path = "unity/" + asset["path"]
            raw = read_source(repo_root, path)
            if authority.sha256(raw) != asset["sha256"]:
                fail(f"model SHA-256 mismatch: {path}")
            meta = read_source(repo_root, path + ".meta").decode("utf-8")
            guids = re.findall(r"^guid: ([0-9a-f]{32})\r?$", meta, re.MULTILINE)
            if guids != [asset["guid"]]:
                fail(f"model GUID mismatch: {path}")


def verify_profile_absence(repo_root: Path) -> None:
    """A newly authored canonical profile requires re-audit, never auto-promotion."""
    directory = repo_root / GAME_DATA
    if not directory.is_dir():
        fail("canonical GameData directory missing")
    for path in sorted(directory.rglob("*.json")):
        try:
            raw = path.read_text(encoding="utf-8")
        except (OSError, UnicodeError) as error:
            fail(f"cannot inspect canonical profile source: {path}: {error}")
        if "kingdom_production_profile_v1" in raw or "ratePerLevelPerSecond" in raw:
            fail(f"new production profile evidence requires versioned review: {path.relative_to(repo_root)}")


def validate_packet(packet: dict, repo_root: Path) -> dict:
    expected = expected_packet(repo_root)
    # Serialized equality also rejects bool-for-int, reordered/extra keys and 1.0-for-1.
    if authority.canonical_json(packet) != authority.canonical_json(expected):
        fail("packet differs from exact pinned evidence; no implicit resolution or approval")
    verify_model_sources(packet, repo_root)
    verify_profile_absence(repo_root)
    gate = authority.validate_ledger_file(repo_root / authority.DEFAULT_LEDGER_PATH, repo_root)
    if gate.production_eligible or any(item not in gate.blocking_ids for item in BLOCKERS):
        fail("upstream gate no longer retains building blockers; version the packet")
    rows = packet["buildings"]
    return {
        "diagnosticCode": "AL-BUILDING-EVIDENCE-BLOCKED",
        "buildingCount": len(rows),
        "modelTupleCount": sum(len(row["modelEvidence"]) for row in rows),
        "missingProductionCount": sum(row["productionProfile"] is None for row in rows),
        "missingModelCount": sum(not row["modelEvidence"] for row in rows),
        "productionEligible": False,
        "blockingIds": gate.blocking_ids,
        "outputPaths": [],
        "activationTargets": [],
    }


def validate_file(path: Path, repo_root: Path) -> dict:
    return validate_packet(authority.load_ledger_file(path), repo_root)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packet", default=DEFAULT_PACKET_PATH)
    parser.add_argument("--require-production-eligible", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    try:
        result = validate_file(root / args.packet, root)
    except (ValidationError, OSError, UnicodeError, ValueError) as error:
        print(error, file=sys.stderr)
        return 1
    print(json.dumps(result, sort_keys=True, separators=(",", ":")))
    return 2 if args.require_production_eligible else 0


if __name__ == "__main__":
    raise SystemExit(main())
