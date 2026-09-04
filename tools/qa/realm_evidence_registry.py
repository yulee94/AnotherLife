#!/usr/bin/env python3
"""Validate and persist append-only realm-slice qualification evidence."""

from __future__ import annotations

import argparse
import copy
import hashlib
import hmac
import importlib.util
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


SCRIPT_PATH = Path(__file__).resolve()
DEFAULT_POLICY = SCRIPT_PATH.with_name("realm_evidence_registry_policy.v1.json")
REALM_ORDER = ["Stonehold", "Eldergrove", "Crownlands", "Umbral"]
MODE_NAMES = {"Adventure3D", "Kingdom2_5D"}
MODE_IDENTITIES = {
    "Adventure3D": ("3d", "3D"),
    "Kingdom2_5D": ("2_5d", "2_5D"),
}
REQUIRED_CHECK_SUFFIXES = (
    "REN",
    "CAM",
    "NAV",
    "CMB",
    "UI",
    "PERF",
    "SAVE",
    "NAR",
    "INP",
    "ACC",
    "LOC-EN",
    "LOC-KO",
)
HEX_DIGITS = set("0123456789abcdef")
PACK_FIELDS = {
    "schemaVersion",
    "protocolId",
    "packetId",
    "candidateId",
    "realm",
    "realmOrdinal",
    "mode",
    "modeNamespace",
    "evidenceOwner",
    "evidenceOwnerKeyId",
    "independentReviewer",
    "signatureMethod",
    "signedUtc",
    "validUntilUtc",
    "rowManifests",
    "supersedes",
    "manifestSha256",
    "evidenceOwnerSignature",
}
DEFAULT_HARNESS_POLICY = SCRIPT_PATH.with_name("realm_slice_evidence_policy.v1.json")
ROW_COVERAGE_FIELDS = ("checkId", "locale", "inputClass", "accessibilityPreset")
_VERIFIED_ROW_SIGNATURES: set[tuple[str, str]] = set()
DECISION_FIELDS = {
    "schemaVersion",
    "protocolId",
    "decisionId",
    "realm",
    "kind",
    "mode",
    "action",
    "owner",
    "ownerKeyId",
    "authorityTaskId",
    "authorityEventId",
    "packetRefs",
    "baselineId",
    "limitations",
    "signedUtc",
    "signatureMethod",
    "supersedes",
    "decisionSha256",
    "decisionSignature",
}
TRANSITION_AUTH_FIELDS = {
    "authorizedBy",
    "ownerKeyId",
    "signatureMethod",
    "signedUtc",
    "transitionSha256",
    "transitionSignature",
}


class RealmEvidenceError(RuntimeError):
    """A realm evidence or gate transition failed closed."""


def canonical_json(payload: Any) -> bytes:
    return (
        json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
        + "\n"
    ).encode("utf-8")


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(Path(path).read_bytes())


def policy_sha256(policy: dict[str, Any]) -> str:
    return sha256_bytes(canonical_json(policy))


def _is_hex(value: Any, length: int) -> bool:
    return isinstance(value, str) and len(value) == length and all(
        character in HEX_DIGITS for character in value
    )


def _load_harness_module():
    path = SCRIPT_PATH.with_name("run_realm_slice_evidence.py")
    spec = importlib.util.spec_from_file_location("run_realm_slice_evidence", path)
    if spec is None or spec.loader is None:
        raise RealmEvidenceError(f"realm-slice evidence harness is unavailable: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def load_harness_policy(
    path: Path = DEFAULT_HARNESS_POLICY,
    allowed_signers: Path | None = None,
) -> tuple[Any, dict[str, Any]]:
    harness = _load_harness_module()
    try:
        policy = harness.load_policy(path)
    except harness.RealmSliceEvidenceError as error:
        raise RealmEvidenceError(f"harness policy is invalid: {error}") from error
    if allowed_signers is not None:
        policy["_reviewAllowedSignersPath"] = str(Path(allowed_signers).resolve())
    return harness, policy


def _row_coverage_key(row: dict[str, Any]) -> tuple[Any, ...]:
    return tuple(row.get(field) for field in ROW_COVERAGE_FIELDS)


def _packet_qa(packet: dict[str, Any]) -> tuple[str, str]:
    rows = packet.get("rowManifests")
    if not isinstance(rows, list) or not rows or not isinstance(rows[0], dict):
        raise RealmEvidenceError("evidence pack row manifests are incomplete")
    run_id = rows[0].get("qa", {}).get("runId") if isinstance(rows[0].get("qa"), dict) else None
    if not isinstance(run_id, str) or not run_id:
        raise RealmEvidenceError("evidence pack integrated QA is incomplete")
    completed = max(str(row.get("timing", {}).get("completedUtc") or "") for row in rows)
    return run_id, completed


def _packet_save_fixture(packet: dict[str, Any]) -> tuple[str, str]:
    rows = packet.get("rowManifests")
    if not isinstance(rows, list) or not rows or not isinstance(rows[0], dict):
        raise RealmEvidenceError("evidence pack row manifests are incomplete")
    save = rows[0].get("saveFixture")
    if not isinstance(save, dict):
        raise RealmEvidenceError("evidence pack save fixture is not bound to a retained artifact")
    fixture_id = save.get("id")
    digest = save.get("sha256")
    if not isinstance(fixture_id, str) or not fixture_id or not _is_hex(digest, 64):
        raise RealmEvidenceError("evidence pack save fixture is not bound to a retained artifact")
    return fixture_id, digest


def _utc(value: Any, field: str) -> datetime:
    if not isinstance(value, str) or not value.endswith("Z"):
        raise RealmEvidenceError(f"{field} must be ISO 8601 UTC")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as error:
        raise RealmEvidenceError(f"{field} must be ISO 8601 UTC") from error
    return parsed.astimezone(timezone.utc)


def _pack_signing_material(packet: dict[str, Any]) -> dict[str, Any]:
    material = copy.deepcopy(packet)
    material.pop("manifestSha256", None)
    material.pop("evidenceOwnerSignature", None)
    return material


def sign_evidence_pack(packet: dict[str, Any], signing_key: bytes) -> dict[str, Any]:
    if not isinstance(signing_key, bytes) or not signing_key:
        raise RealmEvidenceError("a non-empty byte signing key is required")
    signed = copy.deepcopy(packet)
    manifest_sha256 = sha256_bytes(canonical_json(_pack_signing_material(signed)))
    signed["manifestSha256"] = manifest_sha256
    signed["evidenceOwnerSignature"] = hmac.new(
        signing_key,
        manifest_sha256.encode("ascii"),
        hashlib.sha256,
    ).hexdigest()
    return signed


def _decision_signing_material(decision: dict[str, Any]) -> dict[str, Any]:
    material = copy.deepcopy(decision)
    material.pop("decisionSha256", None)
    material.pop("decisionSignature", None)
    return material


def sign_owner_decision(decision: dict[str, Any], signing_key: bytes) -> dict[str, Any]:
    if not isinstance(signing_key, bytes) or not signing_key:
        raise RealmEvidenceError("a non-empty byte signing key is required")
    signed = copy.deepcopy(decision)
    digest = sha256_bytes(canonical_json(_decision_signing_material(signed)))
    signed["decisionSha256"] = digest
    signed["decisionSignature"] = hmac.new(
        signing_key,
        digest.encode("ascii"),
        hashlib.sha256,
    ).hexdigest()
    return signed


def _transition_signing_material(record: dict[str, Any]) -> dict[str, Any]:
    material = copy.deepcopy(record)
    material.pop("transitionSha256", None)
    material.pop("transitionSignature", None)
    material.pop("targets", None)
    material.pop("completeImpactedPackRerunRequired", None)
    return material


def sign_transition_record(record: dict[str, Any], signing_key: bytes) -> dict[str, Any]:
    if not isinstance(signing_key, bytes) or not signing_key:
        raise RealmEvidenceError("a non-empty byte signing key is required")
    signed = copy.deepcopy(record)
    digest = sha256_bytes(canonical_json(_transition_signing_material(signed)))
    signed["transitionSha256"] = digest
    signed["transitionSignature"] = hmac.new(
        signing_key,
        digest.encode("ascii"),
        hashlib.sha256,
    ).hexdigest()
    return signed


def load_policy(path: Path = DEFAULT_POLICY) -> dict[str, Any]:
    try:
        policy = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise RealmEvidenceError(f"registry policy is invalid: {path}: {error}") from error
    if not isinstance(policy, dict):
        raise RealmEvidenceError("registry policy must be a JSON object")
    if (
        policy.get("schemaVersion") != 1
        or policy.get("registryId") != "anotherlife-realm-slice-evidence-registry"
        or policy.get("protocolId") != "RSQ-PROTOCOL-v1.0.0"
        or policy.get("realmOrder") != REALM_ORDER
        or set(policy.get("modes", {})) != MODE_NAMES
    ):
        raise RealmEvidenceError("registry policy identity, realm order, or modes changed")
    for mode, definition in policy["modes"].items():
        namespace, check_prefix = MODE_IDENTITIES[mode]
        expected_checks = [
            f"RSQ-{check_prefix}-{suffix}-001" for suffix in REQUIRED_CHECK_SUFFIXES
        ]
        if (
            definition.get("namespace") != namespace
            or definition.get("requiredChecks") != expected_checks
        ):
            raise RealmEvidenceError(f"registry policy checks or namespace changed for {mode}")
    if (
        policy.get("requiredLocales") != ["en-US", "ko-KR"]
        or policy.get("requiredInputClasses") != ["keyboard_mouse", "controller"]
        or policy.get("requiredAccessibilityPresets") != [
            "default",
            "text-200",
            "reduced-motion",
            "reduced-flash",
            "reduced-vfx",
            "audio-off-captions",
            "non-color",
        ]
        or policy.get("requiredPlatforms") != ["WindowsPlayer"]
        or policy.get("signatureMethod") != "hmac-sha256-v1"
        or policy.get("advancementActions") != {
            "Stonehold": "ADVANCE_TO_ELDERGROVE",
            "Eldergrove": "ADVANCE_TO_CROWNLANDS",
            "Crownlands": "ADVANCE_TO_UMBRAL",
            "Umbral": "COMPLETE_REALM_SEQUENCE",
        }
        or policy.get("reopenTriggers") != [
            "renderer",
            "camera",
            "realm_art",
            "combat",
            "narrative",
            "platform",
            "accessibility",
            "save",
            "evidence_control",
            "artifact_failure",
            "owner_reopen",
        ]
    ):
        raise RealmEvidenceError("registry policy run coverage or signature controls changed")
    trusted_signers = policy.get("trustedSigners")
    if trusted_signers != {
        "anotherlife-evidence-owner-v1": {
            "role": "EVIDENCE_OWNER",
            "subject": "evidence-owner",
        },
        "anotherlife-game-owner-v1": {
            "role": "GAME_OWNER",
            "subject": "game-owner",
        },
    }:
        raise RealmEvidenceError("registry policy trusted signer identities changed")
    return policy


def create_registry(policy: dict[str, Any], created_utc: str) -> dict[str, Any]:
    realms: dict[str, Any] = {}
    for ordinal, realm in enumerate(policy["realmOrder"], start=1):
        realms[realm] = {
            "entryGate": "OPEN" if ordinal == 1 else "CLOSED",
            "modes": {
                mode: {
                    "qualification": "EMPTY",
                    "currentPacket": None,
                    "ownerApproval": "PENDING",
                    "ownerDecision": None,
                    "lastOwnerApprovedBaseline": None,
                    "contentPath": "DISABLED" if ordinal != 1 else "ENABLED_UNAPPROVED",
                    "rerunRequired": False,
                    "reopenedUtc": None,
                }
                for mode in policy["modes"]
            },
            "creativeVisual": {"status": "PENDING", "record": None},
            "ownerAuthorization": {"status": "PENDING", "record": None},
        }
    registry = {
        "schemaVersion": 1,
        "registryId": policy["registryId"],
        "protocolId": policy["protocolId"],
        "policySha256": policy_sha256(policy),
        "realmOrder": list(policy["realmOrder"]),
        "createdUtc": created_utc,
        "updatedUtc": created_utc,
        "activeRealm": policy["realmOrder"][0],
        "realms": realms,
        "evidencePackets": {},
        "ownerDecisions": {},
        "reopens": {},
        "rollbacks": {},
        "events": [],
    }
    registry["registrySha256"] = sha256_bytes(canonical_json(registry))
    return registry


def _registry_state_sha256(registry: dict[str, Any]) -> str:
    material = copy.deepcopy(registry)
    material.pop("registrySha256", None)
    material.pop("events", None)
    return sha256_bytes(canonical_json(material))


def _event_signing_material(event: dict[str, Any]) -> dict[str, Any]:
    material = copy.deepcopy(event)
    material.pop("eventSha256", None)
    material.pop("eventSignature", None)
    return material


def _verify_registry_digest(
    registry: dict[str, Any],
    trusted_signers: dict[str, bytes] | None = None,
) -> None:
    recorded = registry.get("registrySha256")
    unsigned = copy.deepcopy(registry)
    unsigned.pop("registrySha256", None)
    if not _is_hex(recorded, 64) or recorded != sha256_bytes(canonical_json(unsigned)):
        raise RealmEvidenceError("registry digest is missing or invalid")
    previous = "0" * 64
    for expected_sequence, event in enumerate(registry.get("events", []), start=1):
        if not isinstance(event, dict):
            raise RealmEvidenceError("registry event history is invalid")
        hashed_event = copy.deepcopy(event)
        event_hash = hashed_event.pop("eventSha256", None)
        signer_key_id = event.get("signerKeyId")
        signing_key = (trusted_signers or {}).get(signer_key_id)
        signing_digest = sha256_bytes(canonical_json(_event_signing_material(event)))
        expected_signature = (
            hmac.new(signing_key, signing_digest.encode("ascii"), hashlib.sha256).hexdigest()
            if isinstance(signing_key, bytes) and signing_key
            else None
        )
        if (
            hashed_event.get("sequence") != expected_sequence
            or hashed_event.get("previousEventSha256") != previous
            or not _is_hex(event_hash, 64)
            or event_hash != sha256_bytes(canonical_json(hashed_event))
            or event.get("signatureMethod") != "hmac-sha256-v1"
            or not _is_hex(event.get("eventSignature"), 64)
            or expected_signature is None
            or not hmac.compare_digest(event["eventSignature"], expected_signature)
        ):
            raise RealmEvidenceError("registry event history hash chain or signature is invalid")
        previous = event_hash
    if registry.get("events"):
        expected_state = registry["events"][-1].get("resultStateSha256")
        if expected_state != _registry_state_sha256(registry):
            raise RealmEvidenceError("registry authenticated state does not match event history")


def _seal_registry(registry: dict[str, Any]) -> dict[str, Any]:
    sealed = copy.deepcopy(registry)
    sealed.pop("registrySha256", None)
    sealed["registrySha256"] = sha256_bytes(canonical_json(sealed))
    return sealed


def _append_event(
    registry: dict[str, Any],
    occurred_utc: str,
    kind: str,
    realm: str,
    mode: str | None,
    data: dict[str, Any],
    signer_key_id: str,
    signing_key: bytes,
) -> dict[str, Any]:
    updated = copy.deepcopy(registry)
    updated["updatedUtc"] = occurred_utc
    previous = updated["events"][-1]["eventSha256"] if updated["events"] else "0" * 64
    event = {
        "sequence": len(updated["events"]) + 1,
        "eventId": f"RSQ-REG-{len(updated['events']) + 1:06d}",
        "occurredUtc": occurred_utc,
        "kind": kind,
        "realm": realm,
        "mode": mode,
        "data": copy.deepcopy(data),
        "previousEventSha256": previous,
        "resultStateSha256": _registry_state_sha256(updated),
        "signerKeyId": signer_key_id,
        "signatureMethod": "hmac-sha256-v1",
    }
    signing_digest = sha256_bytes(canonical_json(_event_signing_material(event)))
    event["eventSignature"] = hmac.new(
        signing_key,
        signing_digest.encode("ascii"),
        hashlib.sha256,
    ).hexdigest()
    event["eventSha256"] = sha256_bytes(canonical_json(event))
    updated["events"].append(event)
    return _seal_registry(updated)


def _verify_evidence_signature(
    policy: dict[str, Any],
    packet: dict[str, Any],
    trusted_signers: dict[str, bytes],
) -> None:
    key_id = packet.get("evidenceOwnerKeyId")
    signer_definition = policy["trustedSigners"].get(key_id)
    if signer_definition != {
        "role": "EVIDENCE_OWNER",
        "subject": packet.get("evidenceOwner"),
    }:
        raise RealmEvidenceError("evidence pack signer identity is not policy-trusted")
    if packet.get("signatureMethod") != policy["signatureMethod"]:
        raise RealmEvidenceError("evidence pack signature method is invalid")
    signing_key = trusted_signers.get(key_id)
    recorded_digest = packet.get("manifestSha256")
    actual_digest = sha256_bytes(canonical_json(_pack_signing_material(packet)))
    expected_signature = (
        hmac.new(signing_key, actual_digest.encode("ascii"), hashlib.sha256).hexdigest()
        if isinstance(signing_key, bytes) and signing_key
        else None
    )
    if (
        not _is_hex(recorded_digest, 64)
        or recorded_digest != actual_digest
        or not _is_hex(packet.get("evidenceOwnerSignature"), 64)
        or expected_signature is None
        or not hmac.compare_digest(packet["evidenceOwnerSignature"], expected_signature)
    ):
        raise RealmEvidenceError("evidence pack is unsigned or signature verification failed")


def _verify_row_manifests(
    policy: dict[str, Any],
    packet: dict[str, Any],
    harness: Any,
    harness_policy: dict[str, Any],
    artifact_root: Path | None,
    verify_artifacts: bool,
) -> None:
    rows = packet.get("rowManifests")
    realm = packet["realm"]
    mode = packet["mode"]
    namespace = policy["modes"][mode]["namespace"]
    if not isinstance(rows, list) or not rows:
        raise RealmEvidenceError("evidence pack is incomplete or merged across modes")
    expected_specs = [
        _row_coverage_key(spec) for spec in harness.expand_run_specs(harness_policy, realm, mode)
    ]
    observed_specs = [_row_coverage_key(row) for row in rows if isinstance(row, dict)]
    if observed_specs != expected_specs:
        raise RealmEvidenceError("evidence pack is incomplete or merged across modes")
    expected_checks = set(policy["modes"][mode]["requiredChecks"])
    if {row.get("checkId") for row in rows} != expected_checks:
        raise RealmEvidenceError("evidence pack is incomplete or merged across modes")
    first = rows[0]
    shared_build = first.get("build")
    shared_catalogs = first.get("catalogs")
    shared_save = first.get("saveFixture")
    shared_qa = first.get("qa")
    if (
        not isinstance(shared_qa, dict)
        or shared_qa.get("profile") != "full"
        or shared_qa.get("status") != "passed"
        or not isinstance(shared_qa.get("runId"), str)
        or not shared_qa["runId"]
        or not _is_hex(shared_qa.get("reportSha256"), 64)
    ):
        raise RealmEvidenceError("evidence pack integrated QA is incomplete")
    signed_at = _utc(packet.get("signedUtc"), "signedUtc")
    for row in rows:
        if not isinstance(row, dict):
            raise RealmEvidenceError("evidence pack is incomplete or merged across modes")
        if (
            row.get("protocolId") != policy["protocolId"]
            or row.get("evidencePacketId") != packet["packetId"]
            or row.get("candidateId") != packet["candidateId"]
            or row.get("realm") != realm
            or row.get("realmOrdinal") != packet["realmOrdinal"]
            or row.get("mode") != mode
            or row.get("modeNamespace") != namespace
            or row.get("build") != shared_build
            or row.get("catalogs") != shared_catalogs
            or row.get("saveFixture") != shared_save
            or row.get("qa") != shared_qa
            or row.get("executionState") != "COMPLETE"
            or row.get("technicalResult") != "PASS"
            or row.get("reviewerDisposition") != "PASS"
            or row.get("reviewer") != packet["independentReviewer"]
            or row.get("independentReviewer") != packet["independentReviewer"]
            or row.get("signatureMethod") != "ssh-keygen-y"
        ):
            raise RealmEvidenceError(
                f"evidence pack check is incomplete or failed: {row.get('checkId')}"
            )
        if row.get("modeNamespace") != namespace:
            raise RealmEvidenceError("evidence pack contains merged or mismatched mode artifacts")
        for artifact in row.get("artifacts") or []:
            if not isinstance(artifact, dict):
                raise RealmEvidenceError("evidence pack contains merged or mismatched mode artifacts")
            path = str(artifact.get("path") or "")
            if f"/{namespace}/" not in f"/{path}/":
                raise RealmEvidenceError("evidence pack contains merged or mismatched mode artifacts")
        try:
            reviewed_at = _utc(row.get("reviewedUtc"), "reviewedUtc")
        except RealmEvidenceError as error:
            raise RealmEvidenceError(
                f"evidence pack check is incomplete or failed: {row.get('checkId')}"
            ) from error
        if reviewed_at > signed_at:
            raise RealmEvidenceError(
                f"evidence pack check review occurs after signing: {row.get('checkId')}"
            )
        signature_key = (str(row.get("manifestSha256") or ""), str(row.get("reviewerSignature") or ""))
        try:
            if verify_artifacts:
                if artifact_root is None:
                    raise RealmEvidenceError("evidence pack artifact inventory is incomplete")
                harness.verify_manifest(artifact_root, row, harness_policy)
                _VERIFIED_ROW_SIGNATURES.add(signature_key)
            elif signature_key not in _VERIFIED_ROW_SIGNATURES:
                harness.verify_review_signature(harness_policy, row)
                _VERIFIED_ROW_SIGNATURES.add(signature_key)
        except harness.RealmSliceEvidenceError as error:
            message = str(error)
            if "RSQ_REVIEW_SIGNATURE" in message or "detached" in message:
                raise RealmEvidenceError(
                    "evidence pack is unsigned or signature verification failed"
                ) from error
            if "outside" in message or "PATH" in message:
                raise RealmEvidenceError(
                    f"evidence pack artifact resolves outside artifact root: {error}"
                ) from error
            raise RealmEvidenceError(f"harness row manifest failed verification: {error}") from error


def _verify_evidence_pack(
    policy: dict[str, Any],
    packet: dict[str, Any],
    trusted_signers: dict[str, bytes],
    now_utc: str,
    artifact_root: Path | None,
    harness: Any,
    harness_policy: dict[str, Any],
    allow_stale: bool = False,
    verify_artifacts: bool = True,
) -> None:
    if not isinstance(packet, dict) or set(packet) != PACK_FIELDS:
        raise RealmEvidenceError("evidence pack is incomplete or has unknown fields")
    realm = packet.get("realm")
    mode = packet.get("mode")
    if (
        packet.get("schemaVersion") != 1
        or packet.get("protocolId") != policy["protocolId"]
        or realm not in policy["realmOrder"]
        or mode not in policy["modes"]
        or packet.get("realmOrdinal") != policy["realmOrder"].index(realm) + 1
    ):
        raise RealmEvidenceError("evidence pack identity is invalid")
    namespace = policy["modes"][mode]["namespace"]
    if packet.get("modeNamespace") != namespace:
        raise RealmEvidenceError("evidence pack mode and namespace mismatch")
    if (
        not isinstance(packet.get("packetId"), str)
        or not packet["packetId"].startswith(f"RSQ-EV-{realm}-{namespace}-")
        or not packet["packetId"].rsplit("-", 1)[-1].isdigit()
        or not isinstance(packet.get("candidateId"), str)
        or not packet["candidateId"].startswith(f"RSQ-{realm}-{namespace}-")
        or not packet["candidateId"].rsplit("-", 1)[-1].isdigit()
    ):
        raise RealmEvidenceError("evidence pack mode identity is mismatched")
    if packet.get("evidenceOwner") == packet.get("independentReviewer"):
        raise RealmEvidenceError("evidence owner cannot independently review the same pack")
    signed_at = _utc(packet.get("signedUtc"), "signedUtc")
    valid_until = _utc(packet.get("validUntilUtc"), "validUntilUtc")
    now = _utc(now_utc, "nowUtc")
    qa_run_id, qa_completed = _packet_qa(packet)
    qa_completed_at = _utc(qa_completed, "integratedQa.completedUtc")
    if qa_completed_at > signed_at or signed_at > now or (not allow_stale and valid_until < now):
        raise RealmEvidenceError("evidence pack is stale")
    if not qa_run_id:
        raise RealmEvidenceError("evidence pack integrated QA is incomplete")
    _verify_evidence_signature(policy, packet, trusted_signers)
    _verify_row_manifests(
        policy,
        packet,
        harness,
        harness_policy,
        artifact_root,
        verify_artifacts,
    )
    _packet_save_fixture(packet)


def ingest_evidence_pack(
    registry: dict[str, Any],
    policy: dict[str, Any],
    packet: dict[str, Any],
    trusted_signers: dict[str, bytes],
    now_utc: str,
    artifact_root: Path,
    harness: Any,
    harness_policy: dict[str, Any],
) -> dict[str, Any]:
    verify_registry(registry, policy, trusted_signers, harness=harness, harness_policy=harness_policy)
    realm = packet.get("realm")
    if realm not in registry.get("realms", {}) or registry["realms"][realm]["entryGate"] != "OPEN":
        raise RealmEvidenceError(f"realm gate is closed: {realm}")
    _verify_evidence_pack(
        policy,
        packet,
        trusted_signers,
        now_utc,
        artifact_root,
        harness,
        harness_policy,
    )
    packet_id = packet["packetId"]
    if packet_id in registry["evidencePackets"]:
        raise RealmEvidenceError(f"evidence packet already exists: {packet_id}")
    existing_state = registry["realms"][realm]["modes"][packet["mode"]]
    existing = existing_state["currentPacket"]
    if existing is None:
        if packet.get("supersedes") is not None:
            raise RealmEvidenceError("first evidence packet cannot supersede another packet")
    else:
        if not existing_state["rerunRequired"] or existing_state.get("reopenedUtc") is None:
            raise RealmEvidenceError("replacement evidence requires an explicit scoped reopen")
        if packet.get("supersedes") != existing["packetId"]:
            raise RealmEvidenceError(
                f"replacement evidence must supersede current packet: {existing['packetId']}"
            )
        previous_packet = registry["evidencePackets"][existing["packetId"]]
        new_qa_run, new_qa_completed = _packet_qa(packet)
        previous_qa_run, _previous_qa_completed = _packet_qa(previous_packet)
        if new_qa_run == previous_qa_run:
            raise RealmEvidenceError("replacement evidence requires a distinct complete QA rerun")
        if _utc(new_qa_completed, "integratedQa.completedUtc") <= _utc(
            existing_state["reopenedUtc"], "reopenedUtc"
        ):
            raise RealmEvidenceError("replacement evidence QA must complete after the scoped reopen")
    updated = copy.deepcopy(registry)
    updated["evidencePackets"][packet_id] = copy.deepcopy(packet)
    mode_state = updated["realms"][realm]["modes"][packet["mode"]]
    mode_state["qualification"] = "QUALIFIED"
    mode_state["currentPacket"] = {
        "packetId": packet_id,
        "manifestSha256": packet["manifestSha256"],
        "candidateId": packet["candidateId"],
    }
    mode_state["ownerApproval"] = "PENDING"
    mode_state["rerunRequired"] = existing is not None
    if existing is None:
        mode_state["contentPath"] = "ENABLED_UNAPPROVED"
    else:
        mode_state["contentPath"] = "DISABLED_PENDING_REAPPROVAL"
    return _append_event(
        _seal_registry(updated),
        now_utc,
        "EVIDENCE_INGESTED",
        realm,
        packet["mode"],
        {
            "packetId": packet_id,
            "candidateId": packet["candidateId"],
            "manifestSha256": packet["manifestSha256"],
        },
        packet["evidenceOwnerKeyId"],
        trusted_signers[packet["evidenceOwnerKeyId"]],
    )


def _verify_owner_decision(
    policy: dict[str, Any],
    decision: dict[str, Any],
    trusted_owner_keys: dict[str, bytes],
    now_utc: str,
) -> None:
    if not isinstance(decision, dict) or set(decision) != DECISION_FIELDS:
        raise RealmEvidenceError("owner decision is incomplete or has unknown fields")
    if (
        decision.get("schemaVersion") != 1
        or decision.get("protocolId") != policy["protocolId"]
        or decision.get("realm") not in policy["realmOrder"]
        or decision.get("kind") not in {"MODE", "CREATIVE_VISUAL", "AUTHORIZATION"}
        or not all(
            isinstance(decision.get(field), str) and decision[field]
            for field in ("decisionId", "owner", "ownerKeyId", "authorityTaskId", "authorityEventId", "baselineId")
        )
        or not isinstance(decision.get("packetRefs"), list)
        or not isinstance(decision.get("limitations"), list)
        or not decision["limitations"]
    ):
        raise RealmEvidenceError("owner decision identity or authority is invalid")
    signed_at = _utc(decision.get("signedUtc"), "signedUtc")
    if signed_at > _utc(now_utc, "nowUtc"):
        raise RealmEvidenceError("owner decision is future-dated")
    if decision.get("signatureMethod") != policy["signatureMethod"]:
        raise RealmEvidenceError("owner decision signature method is invalid")
    signer_definition = policy["trustedSigners"].get(decision.get("ownerKeyId"))
    if signer_definition != {
        "role": "GAME_OWNER",
        "subject": decision.get("owner"),
    }:
        raise RealmEvidenceError("owner decision signer identity is not policy-trusted")
    digest = sha256_bytes(canonical_json(_decision_signing_material(decision)))
    signing_key = trusted_owner_keys.get(decision["ownerKeyId"])
    expected_signature = (
        hmac.new(signing_key, digest.encode("ascii"), hashlib.sha256).hexdigest()
        if isinstance(signing_key, bytes) and signing_key
        else None
    )
    if (
        decision.get("decisionSha256") != digest
        or not _is_hex(decision.get("decisionSignature"), 64)
        or expected_signature is None
        or not hmac.compare_digest(decision["decisionSignature"], expected_signature)
    ):
        raise RealmEvidenceError("owner decision is unsigned or signature verification failed")


def _current_packet_refs(realm_state: dict[str, Any], modes: list[str]) -> list[dict[str, str]]:
    refs: list[dict[str, str]] = []
    for mode in modes:
        current = realm_state["modes"][mode]["currentPacket"]
        if current is None:
            raise RealmEvidenceError(f"owner decision requires current qualified evidence: {mode}")
        refs.append(copy.deepcopy(current))
    return refs


def record_owner_decision(
    registry: dict[str, Any],
    policy: dict[str, Any],
    decision: dict[str, Any],
    trusted_owner_keys: dict[str, bytes],
    now_utc: str,
    harness: Any = None,
    harness_policy: dict[str, Any] | None = None,
) -> dict[str, Any]:
    verify_registry(
        registry,
        policy,
        trusted_owner_keys,
        harness=harness,
        harness_policy=harness_policy,
    )
    _verify_owner_decision(policy, decision, trusted_owner_keys, now_utc)
    realm = decision["realm"]
    if registry.get("activeRealm") != realm or registry["realms"][realm]["entryGate"] != "OPEN":
        raise RealmEvidenceError(f"owner decision attempted out of realm order: {realm}")
    if decision["decisionId"] in registry["ownerDecisions"]:
        raise RealmEvidenceError(f"owner decision already exists: {decision['decisionId']}")

    realm_state = registry["realms"][realm]
    kind = decision["kind"]
    mode = decision["mode"]
    action = decision["action"]
    if kind == "MODE":
        if mode not in policy["modes"] or action not in {"APPROVE", "REVISE", "REJECT"}:
            raise RealmEvidenceError("mode owner decision is invalid")
        mode_state = realm_state["modes"][mode]
        if mode_state["qualification"] != "QUALIFIED":
            raise RealmEvidenceError("mode owner approval requires current qualified evidence")
        expected_refs = _current_packet_refs(realm_state, [mode])
        superseded_decision = mode_state["ownerDecision"]
    elif kind == "CREATIVE_VISUAL":
        if mode is not None or action not in {"APPROVE", "REVISE", "REJECT"}:
            raise RealmEvidenceError("creative owner decision is invalid")
        if any(
            realm_state["modes"][name]["ownerApproval"] != "APPROVED"
            for name in policy["modes"]
        ):
            raise RealmEvidenceError("creative approval requires both mode owner approvals")
        expected_refs = _current_packet_refs(realm_state, list(policy["modes"]))
        superseded_decision = realm_state["creativeVisual"]["record"]
    else:
        if mode is not None or action != policy["advancementActions"][realm]:
            raise RealmEvidenceError("advancement authorization action is invalid")
        if (
            any(
                realm_state["modes"][name]["ownerApproval"] != "APPROVED"
                for name in policy["modes"]
            )
            or realm_state["creativeVisual"]["status"] != "APPROVED"
        ):
            raise RealmEvidenceError("advancement authorization requires all prior owner approvals")
        expected_refs = _current_packet_refs(realm_state, list(policy["modes"]))
        superseded_decision = realm_state["ownerAuthorization"]["record"]
    if decision["packetRefs"] != expected_refs:
        raise RealmEvidenceError("owner decision packet identity does not match current evidence")
    if decision["supersedes"] != superseded_decision:
        raise RealmEvidenceError("owner decision must explicitly supersede the current decision")
    now = _utc(now_utc, "nowUtc")
    for packet_ref in expected_refs:
        stored_packet = registry["evidencePackets"].get(packet_ref["packetId"])
        if stored_packet is None or _utc(stored_packet["validUntilUtc"], "validUntilUtc") < now:
            raise RealmEvidenceError("owner decision cannot approve stale evidence")

    updated = copy.deepcopy(registry)
    updated["ownerDecisions"][decision["decisionId"]] = copy.deepcopy(decision)
    target = updated["realms"][realm]
    status = "APPROVED" if action == "APPROVE" or kind == "AUTHORIZATION" else action
    if kind == "MODE":
        target_mode = target["modes"][mode]
        target_mode["ownerApproval"] = status
        target_mode["ownerDecision"] = decision["decisionId"]
        if status == "APPROVED":
            target_mode["lastOwnerApprovedBaseline"] = {
                "baselineId": decision["baselineId"],
                "packet": copy.deepcopy(target_mode["currentPacket"]),
                "decisionId": decision["decisionId"],
            }
            target_mode["contentPath"] = "ENABLED_APPROVED"
            target_mode["rerunRequired"] = False
            target_mode["reopenedUtc"] = None
        else:
            target_mode["qualification"] = "REOPENED"
            target_mode["contentPath"] = "DISABLED_PENDING_RERUN"
            target_mode["rerunRequired"] = True
            target_mode["reopenedUtc"] = now_utc
            target["creativeVisual"]["status"] = "REOPENED"
            target["ownerAuthorization"]["status"] = "SUSPENDED"
    elif kind == "CREATIVE_VISUAL":
        target["creativeVisual"] = {"status": status, "record": decision["decisionId"]}
    else:
        target["ownerAuthorization"] = {"status": "APPROVED", "record": decision["decisionId"]}
        target["entryGate"] = "APPROVED"
        ordinal = policy["realmOrder"].index(realm)
        if ordinal + 1 < len(policy["realmOrder"]):
            next_realm = policy["realmOrder"][ordinal + 1]
            updated["realms"][next_realm]["entryGate"] = "OPEN"
            updated["activeRealm"] = next_realm
        else:
            updated["activeRealm"] = None
    return _append_event(
        _seal_registry(updated),
        now_utc,
        "OWNER_DECISION_RECORDED",
        realm,
        mode,
        {
            "decisionId": decision["decisionId"],
            "kind": kind,
            "action": action,
            "decisionSha256": decision["decisionSha256"],
        },
        decision["ownerKeyId"],
        trusted_owner_keys[decision["ownerKeyId"]],
    )


def _verify_realm_state(
    registry: dict[str, Any],
    policy: dict[str, Any],
    realm: str,
) -> None:
    realm_state = registry["realms"][realm]
    if (
        set(realm_state) != {"entryGate", "modes", "creativeVisual", "ownerAuthorization"}
        or realm_state["entryGate"] not in {"CLOSED", "OPEN", "APPROVED", "SUSPENDED"}
    ):
        raise RealmEvidenceError(f"registry realm state is invalid: {realm}")
    if set(realm_state.get("modes", {})) != set(policy["modes"]):
        raise RealmEvidenceError(f"registry realm mode structure is invalid: {realm}")
    for mode, state in realm_state["modes"].items():
        if set(state) != {
            "qualification",
            "currentPacket",
            "ownerApproval",
            "ownerDecision",
            "lastOwnerApprovedBaseline",
            "contentPath",
            "rerunRequired",
            "reopenedUtc",
        }:
            raise RealmEvidenceError(f"registry mode state is incomplete: {realm}/{mode}")
        packet_ref = state["currentPacket"]
        if state["qualification"] not in {"EMPTY", "QUALIFIED", "REOPENED"}:
            raise RealmEvidenceError(f"registry qualification state is invalid: {realm}/{mode}")
        if state["ownerApproval"] not in {
            "PENDING", "APPROVED", "REVISE", "REJECT", "REOPENED",
        }:
            raise RealmEvidenceError(f"registry mode state values are invalid: {realm}/{mode}")
        if state["contentPath"] not in {
            "DISABLED",
            "ENABLED_UNAPPROVED",
            "ENABLED_APPROVED",
            "DISABLED_PENDING_RERUN",
            "DISABLED_PENDING_REAPPROVAL",
            "ROLLED_BACK_TO_APPROVED_BASELINE",
        }:
            raise RealmEvidenceError(f"registry mode state values are invalid: {realm}/{mode}")
        if state["qualification"] == "EMPTY":
            if (
                packet_ref is not None
                or state["ownerApproval"] != "PENDING"
                or state["ownerDecision"] is not None
                or state["lastOwnerApprovedBaseline"] is not None
                or state["rerunRequired"]
                or state["reopenedUtc"] is not None
                or state["contentPath"] not in {"DISABLED", "ENABLED_UNAPPROVED"}
            ):
                raise RealmEvidenceError(f"registry empty mode state is impossible: {realm}/{mode}")
            continue
        if not isinstance(packet_ref, dict):
            raise RealmEvidenceError(f"registry qualified mode lacks packet: {realm}/{mode}")
        packet = registry["evidencePackets"].get(packet_ref.get("packetId"))
        expected_ref = (
            {
                "packetId": packet["packetId"],
                "manifestSha256": packet["manifestSha256"],
                "candidateId": packet["candidateId"],
            }
            if isinstance(packet, dict)
            else None
        )
        if (
            not isinstance(packet, dict)
            or packet_ref != expected_ref
            or packet.get("realm") != realm
            or packet.get("mode") != mode
        ):
            raise RealmEvidenceError(f"registry current packet reference is invalid: {realm}/{mode}")
        owner_decision = state["ownerDecision"]
        if owner_decision is not None and owner_decision not in registry["ownerDecisions"]:
            raise RealmEvidenceError(f"registry mode owner decision is missing: {realm}/{mode}")
        baseline = state["lastOwnerApprovedBaseline"]
        if baseline is not None and (
            baseline.get("packet", {}).get("packetId") not in registry["evidencePackets"]
            or baseline.get("decisionId") not in registry["ownerDecisions"]
        ):
            raise RealmEvidenceError(f"registry approved baseline is invalid: {realm}/{mode}")
        if state["ownerApproval"] == "APPROVED":
            if (
                baseline is None
                or state["contentPath"] != "ENABLED_APPROVED"
                or state["rerunRequired"]
                or state["reopenedUtc"] is not None
            ):
                raise RealmEvidenceError(f"registry approved mode state is impossible: {realm}/{mode}")
        elif state["rerunRequired"]:
            if state["contentPath"] not in {
                "DISABLED_PENDING_RERUN",
                "DISABLED_PENDING_REAPPROVAL",
                "ROLLED_BACK_TO_APPROVED_BASELINE",
            } or state["reopenedUtc"] is None:
                raise RealmEvidenceError(f"registry rerun state is impossible: {realm}/{mode}")
        elif state["ownerApproval"] != "PENDING":
            raise RealmEvidenceError(f"registry mode approval state is impossible: {realm}/{mode}")
        elif state["contentPath"] != "ENABLED_UNAPPROVED":
            raise RealmEvidenceError(f"registry pending mode state is impossible: {realm}/{mode}")
    for decision_name in ("creativeVisual", "ownerAuthorization"):
        decision_state = realm_state.get(decision_name)
        if not isinstance(decision_state, dict) or set(decision_state) != {"status", "record"}:
            raise RealmEvidenceError(f"registry decision state is invalid: {realm}/{decision_name}")
        record = decision_state["record"]
        if record is not None and record not in registry["ownerDecisions"]:
            raise RealmEvidenceError(f"registry decision record is missing: {realm}/{decision_name}")
    if realm_state["creativeVisual"]["status"] not in {
        "PENDING", "APPROVED", "REVISE", "REJECT", "REOPENED",
    }:
        raise RealmEvidenceError(f"registry decision state is invalid: {realm}/creativeVisual")
    if realm_state["ownerAuthorization"]["status"] not in {
        "PENDING", "APPROVED", "SUSPENDED",
    }:
        raise RealmEvidenceError(f"registry decision state is invalid: {realm}/ownerAuthorization")
    if realm_state["creativeVisual"]["status"] == "APPROVED" and any(
        state["ownerApproval"] != "APPROVED" for state in realm_state["modes"].values()
    ):
        raise RealmEvidenceError(f"registry creative approval is premature: {realm}")
    if realm_state["ownerAuthorization"]["status"] == "APPROVED" and (
        realm_state["creativeVisual"]["status"] != "APPROVED"
        or any(state["ownerApproval"] != "APPROVED" for state in realm_state["modes"].values())
        or realm_state["entryGate"] != "APPROVED"
    ):
        raise RealmEvidenceError(f"registry advancement authorization is impossible: {realm}")
    if (
        realm_state["entryGate"] == "APPROVED"
        and realm_state["ownerAuthorization"]["status"] != "APPROVED"
    ):
        raise RealmEvidenceError(f"registry approved realm lacks authorization: {realm}")


def verify_registry(
    registry: dict[str, Any],
    policy: dict[str, Any],
    trusted_signers: dict[str, bytes] | None = None,
    harness: Any = None,
    harness_policy: dict[str, Any] | None = None,
) -> bool:
    _verify_registry_digest(registry, trusted_signers)
    if (
        set(registry) != {
            "schemaVersion",
            "registryId",
            "protocolId",
            "policySha256",
            "realmOrder",
            "createdUtc",
            "updatedUtc",
            "activeRealm",
            "realms",
            "evidencePackets",
            "ownerDecisions",
            "reopens",
            "rollbacks",
            "events",
            "registrySha256",
        }
        or registry.get("schemaVersion") != 1
        or registry.get("registryId") != policy["registryId"]
        or registry.get("protocolId") != policy["protocolId"]
        or registry.get("policySha256") != policy_sha256(policy)
        or registry.get("realmOrder") != policy["realmOrder"]
        or set(registry.get("realms", {})) != set(policy["realmOrder"])
        or not all(
            isinstance(registry.get(field), dict)
            for field in ("evidencePackets", "ownerDecisions", "reopens", "rollbacks")
        )
    ):
        raise RealmEvidenceError("registry structure or policy identity is invalid")
    open_realms = [
        realm
        for realm in policy["realmOrder"]
        if registry["realms"][realm]["entryGate"] == "OPEN"
    ]
    if registry.get("activeRealm") is None:
        if open_realms:
            raise RealmEvidenceError("completed registry still has an open realm")
    elif open_realms != [registry["activeRealm"]]:
        raise RealmEvidenceError("registry must have exactly one active open realm")
    created_at = _utc(registry.get("createdUtc"), "createdUtc")
    updated_at = _utc(registry.get("updatedUtc"), "updatedUtc")
    previous_event_at = created_at
    for event in registry.get("events", []):
        occurred_at = _utc(event.get("occurredUtc"), "event.occurredUtc")
        if occurred_at < previous_event_at:
            raise RealmEvidenceError("registry event times are not monotonic")
        previous_event_at = occurred_at
    if updated_at != previous_event_at:
        raise RealmEvidenceError("registry updatedUtc does not match its event history")
    for realm in policy["realmOrder"]:
        _verify_realm_state(registry, policy, realm)
    if registry["activeRealm"] is None:
        if any(registry["realms"][realm]["entryGate"] != "APPROVED" for realm in policy["realmOrder"]):
            raise RealmEvidenceError("completed registry has an impossible realm gate state")
    else:
        active_ordinal = policy["realmOrder"].index(registry["activeRealm"])
        if any(
            registry["realms"][realm]["entryGate"] == "APPROVED"
            for realm in policy["realmOrder"][active_ordinal + 1 :]
        ):
            raise RealmEvidenceError("later realm is approved ahead of the active realm")
    seen_records = {
        "evidencePackets": set(),
        "ownerDecisions": set(),
        "reopens": set(),
        "rollbacks": set(),
    }
    for event in registry.get("events", []):
        kind = event["kind"]
        data = event["data"]
        if kind == "EVIDENCE_INGESTED":
            record_id = data.get("packetId")
            collection = "evidencePackets"
            record = registry[collection].get(record_id)
            expected_key_id = record.get("evidenceOwnerKeyId") if isinstance(record, dict) else None
            expected_digest = record.get("manifestSha256") if isinstance(record, dict) else None
            recorded_digest = data.get("manifestSha256")
            if isinstance(record, dict):
                if harness is None or harness_policy is None:
                    raise RealmEvidenceError(
                        "non-empty evidence history requires harness row-manifest verification"
                    )
                _verify_evidence_pack(
                    policy,
                    record,
                    trusted_signers or {},
                    registry["updatedUtc"],
                    None,
                    harness,
                    harness_policy,
                    allow_stale=True,
                    verify_artifacts=False,
                )
        elif kind == "OWNER_DECISION_RECORDED":
            record_id = data.get("decisionId")
            collection = "ownerDecisions"
            record = registry[collection].get(record_id)
            expected_key_id = record.get("ownerKeyId") if isinstance(record, dict) else None
            expected_digest = record.get("decisionSha256") if isinstance(record, dict) else None
            recorded_digest = data.get("decisionSha256")
            if isinstance(record, dict):
                _verify_owner_decision(
                    policy,
                    record,
                    trusted_signers or {},
                    registry["updatedUtc"],
                )
        elif kind == "SCOPE_REOPENED":
            record_id = data.get("reopenId")
            collection = "reopens"
            record = registry[collection].get(record_id)
            expected_key_id = record.get("ownerKeyId") if isinstance(record, dict) else None
            expected_digest = record.get("transitionSha256") if isinstance(record, dict) else None
            recorded_digest = data.get("transitionSha256")
            if isinstance(record, dict):
                _verify_transition_record(
                    policy,
                    record,
                    trusted_signers or {},
                    registry["updatedUtc"],
                )
        elif kind == "ROLLBACK_RECORDED":
            record_id = data.get("rollbackId")
            collection = "rollbacks"
            record = registry[collection].get(record_id)
            expected_key_id = record.get("ownerKeyId") if isinstance(record, dict) else None
            expected_digest = record.get("transitionSha256") if isinstance(record, dict) else None
            recorded_digest = data.get("transitionSha256")
            if isinstance(record, dict):
                _verify_transition_record(
                    policy,
                    record,
                    trusted_signers or {},
                    registry["updatedUtc"],
                )
        else:
            raise RealmEvidenceError(f"registry event kind is invalid: {kind}")
        if record_id in seen_records[collection]:
            raise RealmEvidenceError("registry record is referenced by duplicate events")
        seen_records[collection].add(record_id)
        if (
            record is None
            or event.get("signerKeyId") != expected_key_id
            or recorded_digest != expected_digest
        ):
            raise RealmEvidenceError("registry event does not authenticate its append-only record")
    for collection, seen_ids in seen_records.items():
        if seen_ids != set(registry[collection]):
            raise RealmEvidenceError(f"registry append-only records lack events: {collection}")
    return True


def verify_append_only(base: dict[str, Any], current: dict[str, Any]) -> bool:
    base_events = base.get("events")
    current_events = current.get("events")
    if (
        not isinstance(base_events, list)
        or not isinstance(current_events, list)
        or current_events[: len(base_events)] != base_events
    ):
        raise RealmEvidenceError("registry append-only event history was removed or rewritten")
    if len(current_events) == len(base_events) and current != base:
        raise RealmEvidenceError("registry state changed without an appended event")
    for collection in ("evidencePackets", "ownerDecisions", "reopens", "rollbacks"):
        base_records = base.get(collection)
        current_records = current.get(collection)
        if not isinstance(base_records, dict) or not isinstance(current_records, dict):
            raise RealmEvidenceError("registry append-only collections are invalid")
        for record_id, record in base_records.items():
            if current_records.get(record_id) != record:
                raise RealmEvidenceError(
                    f"registry append-only record was removed or rewritten: {collection}/{record_id}"
                )
    return True


def _verify_transition_record(
    policy: dict[str, Any],
    record: dict[str, Any],
    trusted_owner_keys: dict[str, bytes],
    now_utc: str,
) -> None:
    signer_definition = policy["trustedSigners"].get(record.get("ownerKeyId"))
    if signer_definition != {
        "role": "GAME_OWNER",
        "subject": record.get("authorizedBy"),
    }:
        raise RealmEvidenceError("transition signer identity is not policy-trusted")
    occurred_at = _utc(record.get("occurredUtc"), "occurredUtc")
    signed_at = _utc(record.get("signedUtc"), "signedUtc")
    now = _utc(now_utc, "nowUtc")
    if (
        record.get("signatureMethod") != policy["signatureMethod"]
        or occurred_at > signed_at
        or signed_at > now
    ):
        raise RealmEvidenceError("transition signature metadata is invalid")
    digest = sha256_bytes(canonical_json(_transition_signing_material(record)))
    signing_key = trusted_owner_keys.get(record.get("ownerKeyId"))
    expected_signature = (
        hmac.new(signing_key, digest.encode("ascii"), hashlib.sha256).hexdigest()
        if isinstance(signing_key, bytes) and signing_key
        else None
    )
    if (
        record.get("transitionSha256") != digest
        or not _is_hex(record.get("transitionSignature"), 64)
        or expected_signature is None
        or not hmac.compare_digest(record["transitionSignature"], expected_signature)
    ):
        raise RealmEvidenceError("transition is unsigned or signature verification failed")


def reopen_scope(
    registry: dict[str, Any],
    policy: dict[str, Any],
    reopen_record: dict[str, Any],
    trusted_owner_keys: dict[str, bytes],
    now_utc: str,
    harness: Any = None,
    harness_policy: dict[str, Any] | None = None,
) -> dict[str, Any]:
    verify_registry(
        registry,
        policy,
        trusted_owner_keys,
        harness=harness,
        harness_policy=harness_policy,
    )
    required = {
        "reopenId",
        "trigger",
        "realm",
        "affectedModes",
        "dependentRealms",
        "impactReason",
        "authorityTaskId",
        "authorityEventId",
        "occurredUtc",
    } | TRANSITION_AUTH_FIELDS
    if not isinstance(reopen_record, dict) or set(reopen_record) != required:
        raise RealmEvidenceError("reopen record is incomplete or has unknown fields")
    realm = reopen_record["realm"]
    modes = reopen_record["affectedModes"]
    dependent_realms = reopen_record["dependentRealms"]
    if (
        realm not in policy["realmOrder"]
        or reopen_record["trigger"] not in policy["reopenTriggers"]
        or not isinstance(modes, list)
        or not modes
        or len(modes) != len(set(modes))
        or any(mode not in policy["modes"] for mode in modes)
        or not isinstance(dependent_realms, list)
        or len(dependent_realms) != len(set(dependent_realms))
        or not all(
            isinstance(reopen_record[field], str) and reopen_record[field]
            for field in (
                "reopenId",
                "impactReason",
                "authorityTaskId",
                "authorityEventId",
                "occurredUtc",
            )
        )
    ):
        raise RealmEvidenceError("reopen scope or authority is invalid")
    _utc(reopen_record["occurredUtc"], "occurredUtc")
    _verify_transition_record(policy, reopen_record, trusted_owner_keys, now_utc)
    ordinal = policy["realmOrder"].index(realm)
    later_realms = policy["realmOrder"][ordinal + 1 :]
    if any(item not in later_realms for item in dependent_realms):
        raise RealmEvidenceError("reopen dependent realms must follow the affected realm")
    if reopen_record["reopenId"] in registry["reopens"]:
        raise RealmEvidenceError(f"reopen record already exists: {reopen_record['reopenId']}")
    if any(registry["realms"][realm]["modes"][mode]["qualification"] == "EMPTY" for mode in modes):
        raise RealmEvidenceError("cannot reopen a mode without prior qualification evidence")

    updated = copy.deepcopy(registry)
    updated["reopens"][reopen_record["reopenId"]] = copy.deepcopy(reopen_record)
    affected = updated["realms"][realm]
    affected["entryGate"] = "OPEN"
    for mode in modes:
        state = affected["modes"][mode]
        state["qualification"] = "REOPENED"
        state["ownerApproval"] = "REOPENED"
        state["contentPath"] = "DISABLED_PENDING_RERUN"
        state["rerunRequired"] = True
        state["reopenedUtc"] = reopen_record["occurredUtc"]
    affected["creativeVisual"]["status"] = "REOPENED"
    affected["ownerAuthorization"]["status"] = "SUSPENDED"
    for later in later_realms:
        if updated["realms"][later]["entryGate"] != "CLOSED":
            updated["realms"][later]["entryGate"] = "SUSPENDED"
    for dependent in dependent_realms:
        dependent_state = updated["realms"][dependent]
        for dependent_mode in modes:
            state = dependent_state["modes"][dependent_mode]
            if state["qualification"] != "EMPTY":
                state["qualification"] = "REOPENED"
                state["ownerApproval"] = "REOPENED"
                state["contentPath"] = "DISABLED_PENDING_RERUN"
                state["rerunRequired"] = True
                state["reopenedUtc"] = reopen_record["occurredUtc"]
        dependent_state["creativeVisual"]["status"] = "REOPENED"
        dependent_state["ownerAuthorization"]["status"] = "SUSPENDED"
        dependent_state["entryGate"] = "SUSPENDED"
    updated["activeRealm"] = realm
    return _append_event(
        _seal_registry(updated),
        reopen_record["occurredUtc"],
        "SCOPE_REOPENED",
        realm,
        modes[0] if len(modes) == 1 else None,
        {
            "reopenId": reopen_record["reopenId"],
            "trigger": reopen_record["trigger"],
            "affectedModes": list(modes),
            "dependentRealms": list(dependent_realms),
            "impactReason": reopen_record["impactReason"],
            "transitionSha256": reopen_record["transitionSha256"],
        },
        reopen_record["ownerKeyId"],
        trusted_owner_keys[reopen_record["ownerKeyId"]],
    )


def record_rollback(
    registry: dict[str, Any],
    policy: dict[str, Any],
    rollback_record: dict[str, Any],
    trusted_owner_keys: dict[str, bytes],
    now_utc: str,
    artifact_root: Path,
    harness: Any = None,
    harness_policy: dict[str, Any] | None = None,
) -> dict[str, Any]:
    verify_registry(
        registry,
        policy,
        trusted_owner_keys,
        harness=harness,
        harness_policy=harness_policy,
    )
    required = {
        "rollbackId",
        "realm",
        "affectedModes",
        "reason",
        "executedBy",
        "preserveEvidence",
        "preserveSaves",
        "disableOnlyAffectedPaths",
        "authorityTaskId",
        "authorityEventId",
        "baselineRefs",
        "saveSnapshots",
        "occurredUtc",
    } | TRANSITION_AUTH_FIELDS
    if not isinstance(rollback_record, dict) or set(rollback_record) != required:
        raise RealmEvidenceError("rollback record is incomplete or has unknown fields")
    realm = rollback_record["realm"]
    modes = rollback_record["affectedModes"]
    if (
        realm not in policy["realmOrder"]
        or not isinstance(modes, list)
        or not modes
        or len(modes) != len(set(modes))
        or any(mode not in policy["modes"] for mode in modes)
        or rollback_record.get("preserveEvidence") is not True
        or rollback_record.get("preserveSaves") is not True
        or rollback_record.get("disableOnlyAffectedPaths") is not True
        or not all(
            isinstance(rollback_record[field], str) and rollback_record[field]
            for field in (
                "rollbackId",
                "reason",
                "executedBy",
                "authorityTaskId",
                "authorityEventId",
                "occurredUtc",
            )
        )
    ):
        raise RealmEvidenceError("rollback scope violates preservation controls")
    _utc(rollback_record["occurredUtc"], "occurredUtc")
    _verify_transition_record(policy, rollback_record, trusted_owner_keys, now_utc)
    if rollback_record["rollbackId"] in registry["rollbacks"]:
        raise RealmEvidenceError(f"rollback record already exists: {rollback_record['rollbackId']}")
    targets = []
    expected_save_snapshots = []
    for mode in modes:
        state = registry["realms"][realm]["modes"][mode]
        baseline = state["lastOwnerApprovedBaseline"]
        if not state["rerunRequired"] or baseline is None:
            raise RealmEvidenceError(f"rollback requires a reopened mode and approved baseline: {mode}")
        packet_ref = baseline["packet"]
        packet = registry["evidencePackets"].get(packet_ref["packetId"])
        owner_decision = registry["ownerDecisions"].get(baseline["decisionId"])
        if not isinstance(packet, dict) or not isinstance(owner_decision, dict):
            raise RealmEvidenceError(f"rollback baseline evidence history is missing: {mode}")
        _verify_evidence_pack(
            policy,
            packet,
            trusted_owner_keys,
            now_utc,
            artifact_root,
            harness,
            harness_policy,
            allow_stale=True,
        )
        _verify_owner_decision(policy, owner_decision, trusted_owner_keys, now_utc)
        if (
            owner_decision.get("kind") != "MODE"
            or owner_decision.get("realm") != realm
            or owner_decision.get("mode") != mode
            or owner_decision.get("action") != "APPROVE"
            or owner_decision.get("baselineId") != baseline["baselineId"]
            or owner_decision.get("packetRefs") != [packet_ref]
        ):
            raise RealmEvidenceError(
                f"rollback baseline approval is not bound to the target realm and mode: {mode}"
            )
        target = {
            "mode": mode,
            "baselineId": baseline["baselineId"],
            "packetId": packet_ref["packetId"],
            "manifestSha256": packet_ref["manifestSha256"],
            "ownerDecisionId": baseline["decisionId"],
        }
        targets.append(target)
        save_id, save_digest = _packet_save_fixture(packet)
        expected_save_snapshots.append({
            "mode": mode,
            "saveFixtureId": save_id,
            "saveFixtureSha256": save_digest,
        })
    if rollback_record["baselineRefs"] != targets:
        raise RealmEvidenceError("rollback signed baseline targets do not match approved history")
    if rollback_record["saveSnapshots"] != expected_save_snapshots:
        raise RealmEvidenceError("rollback save-preservation evidence does not match baseline fixtures")
    retained_packet_count = len(registry["evidencePackets"])
    retained_decision_count = len(registry["ownerDecisions"])
    updated = copy.deepcopy(registry)
    stored_record = copy.deepcopy(rollback_record)
    stored_record["targets"] = targets
    stored_record["completeImpactedPackRerunRequired"] = True
    updated["rollbacks"][rollback_record["rollbackId"]] = stored_record
    for mode in modes:
        updated["realms"][realm]["modes"][mode]["contentPath"] = (
            "ROLLED_BACK_TO_APPROVED_BASELINE"
        )
    if (
        len(updated["evidencePackets"]) != retained_packet_count
        or len(updated["ownerDecisions"]) != retained_decision_count
    ):
        raise RealmEvidenceError("rollback attempted to remove evidence or approval history")
    return _append_event(
        _seal_registry(updated),
        rollback_record["occurredUtc"],
        "ROLLBACK_RECORDED",
        realm,
        modes[0] if len(modes) == 1 else None,
        {
            "rollbackId": rollback_record["rollbackId"],
            "targets": targets,
            "preserveEvidence": True,
            "preserveSaves": True,
            "completeImpactedPackRerunRequired": True,
            "transitionSha256": rollback_record["transitionSha256"],
        },
        rollback_record["ownerKeyId"],
        trusted_owner_keys[rollback_record["ownerKeyId"]],
    )


def _load_json(path: Path, label: str) -> dict[str, Any]:
    try:
        payload = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise RealmEvidenceError(f"{label} JSON is invalid: {path}: {error}") from error
    if not isinstance(payload, dict):
        raise RealmEvidenceError(f"{label} must be a JSON object: {path}")
    return payload


def _write_registry_file(path: Path, registry: dict[str, Any]) -> None:
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_bytes(canonical_json(registry))
    os.replace(temporary, path)


def initialize_registry_file(
    path: Path,
    policy: dict[str, Any],
    created_utc: str,
) -> dict[str, Any]:
    path = Path(path)
    if path.exists():
        raise RealmEvidenceError(f"registry already exists: {path}")
    _utc(created_utc, "createdUtc")
    registry = create_registry(policy, created_utc)
    verify_registry(registry, policy)
    _write_registry_file(path, registry)
    return registry


def _load_registry_file(
    path: Path,
    policy: dict[str, Any],
    trusted_signers: dict[str, bytes] | None = None,
    harness: Any = None,
    harness_policy: dict[str, Any] | None = None,
) -> dict[str, Any]:
    registry = _load_json(path, "registry")
    verify_registry(
        registry,
        policy,
        trusted_signers,
        harness=harness,
        harness_policy=harness_policy,
    )
    return registry


def _load_keyring(path: Path) -> dict[str, bytes]:
    payload = _load_json(path, "keyring")
    rows = payload.get("keys")
    if payload.get("schemaVersion") != 1 or not isinstance(rows, list) or not rows:
        raise RealmEvidenceError("keyring must contain a non-empty version 1 key list")
    result: dict[str, bytes] = {}
    for row in rows:
        if not isinstance(row, dict) or set(row) != {"keyId", "secretEnv"}:
            raise RealmEvidenceError("keyring entries require only keyId and secretEnv")
        key_id = row["keyId"]
        environment_name = row["secretEnv"]
        if (
            not isinstance(key_id, str)
            or not key_id
            or key_id in result
            or not isinstance(environment_name, str)
            or not environment_name
        ):
            raise RealmEvidenceError("keyring entry identity is invalid")
        secret = os.environ.get(environment_name)
        if not secret:
            raise RealmEvidenceError(
                f"keyring secret environment variable is unavailable: {environment_name}"
            )
        result[key_id] = secret.encode("utf-8")
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    subparsers = parser.add_subparsers(dest="command", required=True)

    initialize = subparsers.add_parser("init", help="create a new fail-closed registry")
    initialize.add_argument("--registry", type=Path, required=True)
    initialize.add_argument("--created-utc", required=True)

    verify = subparsers.add_parser("verify", help="verify registry digest and audit chain")
    verify.add_argument("--registry", type=Path, required=True)
    verify.add_argument("--keyring", type=Path)
    verify.add_argument("--allowed-signers", type=Path)

    append_only = subparsers.add_parser(
        "verify-append-only",
        help="prove a candidate registry only appends to a trusted base",
    )
    append_only.add_argument("--base", type=Path, required=True)
    append_only.add_argument("--registry", type=Path, required=True)
    append_only.add_argument("--keyring", type=Path)
    append_only.add_argument("--allowed-signers", type=Path)

    ingest = subparsers.add_parser("ingest", help="verify and ingest one signed mode pack")
    ingest.add_argument("--registry", type=Path, required=True)
    ingest.add_argument("--pack", type=Path, required=True)
    ingest.add_argument("--artifact-root", type=Path, required=True)
    ingest.add_argument("--keyring", type=Path, required=True)
    ingest.add_argument("--allowed-signers", type=Path, required=True)
    ingest.add_argument("--now-utc", required=True)

    approve = subparsers.add_parser("approve", help="record one signed owner decision")
    approve.add_argument("--registry", type=Path, required=True)
    approve.add_argument("--decision", type=Path, required=True)
    approve.add_argument("--keyring", type=Path, required=True)
    approve.add_argument("--allowed-signers", type=Path, required=True)
    approve.add_argument("--now-utc", required=True)

    reopen = subparsers.add_parser("reopen", help="reopen only the impact-analyzed scope")
    reopen.add_argument("--registry", type=Path, required=True)
    reopen.add_argument("--record", type=Path, required=True)
    reopen.add_argument("--keyring", type=Path, required=True)
    reopen.add_argument("--allowed-signers", type=Path, required=True)
    reopen.add_argument("--now-utc", required=True)

    rollback = subparsers.add_parser("rollback", help="record scoped rollback containment")
    rollback.add_argument("--registry", type=Path, required=True)
    rollback.add_argument("--record", type=Path, required=True)
    rollback.add_argument("--artifact-root", type=Path, required=True)
    rollback.add_argument("--keyring", type=Path, required=True)
    rollback.add_argument("--allowed-signers", type=Path, required=True)
    rollback.add_argument("--now-utc", required=True)

    args = parser.parse_args(argv)
    try:
        policy = load_policy(args.policy)
        if args.command == "init":
            registry = initialize_registry_file(args.registry, policy, args.created_utc)
        else:
            trusted_signers = (
                _load_keyring(args.keyring)
                if getattr(args, "keyring", None) is not None
                else None
            )
            allowed_signers = getattr(args, "allowed_signers", None)
            peek = _load_json(args.registry, "registry")
            harness = None
            harness_policy = None
            if peek.get("evidencePackets") or args.command == "ingest":
                if allowed_signers is None:
                    raise RealmEvidenceError(
                        "non-empty evidence history requires --allowed-signers"
                    )
                harness, harness_policy = load_harness_policy(allowed_signers=allowed_signers)
            registry = (
                peek
                if args.command == "verify-append-only" and trusted_signers is None
                else _load_registry_file(
                    args.registry,
                    policy,
                    trusted_signers,
                    harness=harness,
                    harness_policy=harness_policy,
                )
            )
            if args.command == "ingest":
                registry = ingest_evidence_pack(
                    registry,
                    policy,
                    _load_json(args.pack, "evidence pack"),
                    trusted_signers,
                    args.now_utc,
                    args.artifact_root,
                    harness,
                    harness_policy,
                )
            elif args.command == "approve":
                registry = record_owner_decision(
                    registry,
                    policy,
                    _load_json(args.decision, "owner decision"),
                    trusted_signers,
                    args.now_utc,
                    harness=harness,
                    harness_policy=harness_policy,
                )
            elif args.command == "reopen":
                registry = reopen_scope(
                    registry,
                    policy,
                    _load_json(args.record, "reopen record"),
                    trusted_signers,
                    args.now_utc,
                    harness=harness,
                    harness_policy=harness_policy,
                )
            elif args.command == "rollback":
                registry = record_rollback(
                    registry,
                    policy,
                    _load_json(args.record, "rollback record"),
                    trusted_signers,
                    args.now_utc,
                    args.artifact_root,
                    harness=harness,
                    harness_policy=harness_policy,
                )
            elif args.command == "verify-append-only":
                base = (
                    _load_json(args.base, "base registry")
                    if trusted_signers is None
                    else _load_registry_file(
                        args.base,
                        policy,
                        trusted_signers,
                        harness=harness,
                        harness_policy=harness_policy,
                    )
                )
                if trusted_signers is None and (
                    base.get("events") or registry.get("events")
                ):
                    raise RealmEvidenceError(
                        "append-only verification of non-empty history requires a keyring"
                    )
                verify_append_only(base, registry)
            if args.command not in {"verify", "verify-append-only"}:
                verify_registry(
                    registry,
                    policy,
                    trusted_signers,
                    harness=harness,
                    harness_policy=harness_policy,
                )
                _write_registry_file(args.registry, registry)
    except (RealmEvidenceError, OSError) as error:
        print(f"realm-evidence-registry: {error}", file=sys.stderr)
        return 2
    print(
        f"REALM_EVIDENCE_REGISTRY_VERIFIED active={registry['activeRealm']} "
        f"events={len(registry['events'])} sha256={registry['registrySha256']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
