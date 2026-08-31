#!/usr/bin/env python
"""Validate the provider-neutral MMO contract baseline and adapter isolation."""

from __future__ import annotations

import re
import sys
from pathlib import Path


CONTRACTS = {
    "C-IDN-01": ("t_927d3dd9",),
    "C-PLC-01": ("t_a8960ec8",),
    "C-PER-01": ("t_22898962",),
    "C-SIM-01": ("t_7d1036e8",),
    "C-SOC-01": ("t_db8f937f",),
    "C-ECO-01": ("t_00f0f879",),
    "C-PLT-01": ("t_4f3f4535",),
    "C-CAP-01": ("t_d4d26ddf",),
    "C-DEP-01": ("t_28c37145",),
    "C-SEC-01": ("t_30eadba6",),
    "C-OPS-01": ("t_28c37145", "t_aa3849be"),
}

SHARED_CONSUMERS = ("t_1cfdd495", "t_c6e9368a")
SPIKE_CONSUMERS = ("t_ff702849", "t_27759e01")

ADR_FILES = (
    "0001-provider-neutral-authority-and-adapters.md",
    "0002-region-local-authority-and-global-minimization.md",
    "0003-managed-versus-custom-evidence-gate.md",
    "0004-modular-deployable-and-state-separation.md",
)

ADR_SECTIONS = (
    "Status:",
    "Decision owner:",
    "Review state:",
    "## Context",
    "## Decision",
    "## Consequences",
    "## Rejected alternatives",
    "## Failure and rollback",
    "## Unresolved owner decisions",
)

CORE_TRAITS = (
    "pub trait ExternalIdentityAdapter",
    "pub trait PlacementAdapter",
    "pub trait DeploymentAdapter",
    "pub trait OperationsAdapter",
    "pub trait PlatformAdapter",
)

DOMAIN_PORTS = (
    "pub trait PersistencePort",
    "pub trait SimulationPort",
    "pub trait SocialPort",
    "pub trait EconomyPort",
    "pub trait SecurityAbusePort",
    "pub trait ObservabilityPort",
)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def read(path: Path) -> str:
    require(path.is_file(), f"missing required file: {path}")
    return path.read_text(encoding="utf-8")


def enclosed(source: str, start: str, end: str) -> str:
    """Return one required executable declaration body."""
    require(start in source, f"missing executable declaration: {start}")
    remainder = source.split(start, 1)[1]
    require(end in remainder, f"unterminated executable declaration: {start}")
    return remainder.split(end, 1)[0]


def validate(root: Path) -> None:
    contract_path = (
        root
        / "unity"
        / "Docs"
        / "Architecture"
        / "MMO_Provider_Neutral_Contracts_v1.md"
    )
    contract = read(contract_path)

    require("MMO-CONTRACTS-v1.0.0" in contract, "missing contract version identity")
    require(
        "## 5. Contract register and named consumers" in contract,
        "missing named-consumer register",
    )
    require("## 9. Failure and rollback scenarios" in contract, "missing failure matrix")
    require("## 10. Unresolved owner decisions" in contract, "missing owner decisions")
    require("## 11. Acceptance traceability" in contract, "missing acceptance traceability")

    for contract_id, task_ids in CONTRACTS.items():
        require(contract_id in contract, f"missing contract {contract_id}")
        register_rows = [
            line
            for line in contract.splitlines()
            if line.startswith("|") and f"`{contract_id}`" in line
        ]
        require(len(register_rows) == 1, f"contract {contract_id} must have exactly one row")
        actual_task_ids = set(re.findall(r"`(t_[0-9a-f]+)`", register_rows[0]))
        require(
            actual_task_ids == set(task_ids),
            f"contract {contract_id} consumer mismatch: {sorted(actual_task_ids)}",
        )
        require(
            f"### 6." in contract and f"`{contract_id}`" in contract,
            f"missing detailed contract section {contract_id}",
        )

    shared_section = contract.split("Shared foundation consumers", 1)[1].split(
        "consume the entire", 1
    )[0]
    actual_shared = set(re.findall(r"`(t_[0-9a-f]+)`", shared_section))
    require(
        actual_shared == set(SHARED_CONSUMERS),
        f"shared consumer mismatch: {sorted(actual_shared)}",
    )
    spike_section = contract.split("The provider spikes", 1)[1].split("implement only", 1)[0]
    actual_spikes = set(re.findall(r"`(t_[0-9a-f]+)`", spike_section))
    require(
        actual_spikes == set(SPIKE_CONSUMERS),
        f"provider-spike consumer mismatch: {sorted(actual_spikes)}",
    )

    adr_dir = root / "unity" / "Docs" / "Architecture" / "ADRs"
    adr_texts: list[str] = []
    for filename in ADR_FILES:
        text = read(adr_dir / filename)
        adr_texts.append(text)
        for section in ADR_SECTIONS:
            require(section in text, f"{filename}: missing {section}")
        require(
            "Decision owner: game owner" in text,
            f"{filename}: material decisions are not reserved to the game owner",
        )
        unresolved = text.split("## Unresolved owner decisions", 1)[1].strip()
        require(len(unresolved) >= 40, f"{filename}: unresolved decisions are empty")

    reviewed_architecture = "\n".join([contract, *adr_texts])
    for provider_token in ("gamelift", "playfab"):
        require(
            provider_token not in reviewed_architecture.lower(),
            f"provider commitment leaked into approved architecture: {provider_token}",
        )
    for pattern, label in (
        (r"\$\s*\d", "currency amount"),
        (r"\b\d+(?:\.\d+)?\s*(?:usd|eur|krw)\b", "priced amount"),
        (r"\b\d+(?:\.\d+)?\s*(?:ms|milliseconds?)\b", "latency threshold"),
        (r"\b\d+(?:\.\d+)?\s*(?:fps|hz)\b", "device/performance tier"),
    ):
        require(
            re.search(pattern, reviewed_architecture, flags=re.IGNORECASE) is None,
            f"invented {label} found in architecture package",
        )

    core_manifest = read(root / "server" / "al_server_core" / "Cargo.toml")
    workspace_manifest = read(root / "server" / "Cargo.toml")
    adapter_manifest = read(root / "server" / "al_provider_adapter_stub" / "Cargo.toml")
    core_source = read(root / "server" / "al_server_core" / "src" / "provider_contracts.rs")
    domain_source = read(root / "server" / "al_server_core" / "src" / "domain_contracts.rs")
    core_lib = read(root / "server" / "al_server_core" / "src" / "lib.rs")
    adapter_source = read(root / "server" / "al_provider_adapter_stub" / "src" / "lib.rs")

    require(
        "al_provider_adapter_stub" not in core_manifest,
        "authoritative core must not depend on adapter crate",
    )
    core_dependency_lines = core_manifest.split("[dependencies]", 1)[1].strip()
    require(
        not core_dependency_lines,
        "authoritative core gained an external dependency or provider SDK",
    )
    require(
        "al_provider_adapter_stub" in workspace_manifest,
        "disposable adapter crate is not a workspace member",
    )
    require(
        'al_server_core = { path = "../al_server_core" }' in adapter_manifest,
        "adapter must depend on provider-neutral core contracts",
    )
    require(
        "pub mod provider_contracts;" in core_lib,
        "provider contract module is not exported",
    )
    require(
        "pub mod domain_contracts;" in core_lib,
        "domain contract module is not exported",
    )

    for trait_name in CORE_TRAITS:
        require(trait_name in core_source, f"missing core adapter seam: {trait_name}")
    for trait_name in DOMAIN_PORTS:
        require(trait_name in domain_source, f"missing domain port: {trait_name}")
    for forbidden_authority in (
        "fn decide_combat",
        "fn resolve_combat",
        "fn calculate_reward",
        "fn grant_reward",
        "fn choose_progression",
        "fn settle_economy",
    ):
        require(
            forbidden_authority not in domain_source,
            f"gameplay authority leaked into domain ports: {forbidden_authority}",
        )

    for field in (
        "contract_id",
        "actor_id",
        "service_id",
        "authorization_context_id",
        "policy_version",
        "region_id",
        "realm_id",
        "schema_version",
        "artifact_fingerprint",
        "compatibility_fingerprint",
    ):
        require(field in core_source, f"adapter context missing field: {field}")

    context_fields = enclosed(
        core_source,
        "pub struct AdapterRequestContext {",
        "}\n\nimpl AdapterRequestContext",
    )
    for declaration in (
        "contract_id: ContractId",
        "operation_id: OperationId",
        "correlation_id: CorrelationId",
        "actor_id: ActorId",
        "service_id: ServiceId",
        "authorization_context_id: AuthorizationContextId",
        "policy_version: PolicyVersion",
        "region_id: RegionId",
        "realm_id: Option<RealmId>",
        "schema_version: SchemaVersion",
        "artifact_fingerprint: ArtifactFingerprint",
        "compatibility_fingerprint: CompatibilityFingerprint",
        "attempt: u32",
    ):
        require(declaration in context_fields, f"context missing typed field: {declaration}")
    require("pub enum ContractId" in core_source, "typed contract ID is missing")
    require(
        "pub fn has_same_retry_invariants" in core_source,
        "retry-invariant context comparison is missing",
    )

    observation_fields = enclosed(
        core_source,
        "pub struct AdapterObservation {",
        "}\n\nimpl AdapterObservation",
    )
    for declaration in (
        "contract_id: ContractId",
        "operation_id: OperationId",
        "correlation_id: CorrelationId",
        "region_id: RegionId",
        "realm_id: Option<RealmId>",
        "schema_version: SchemaVersion",
        "artifact_fingerprint: ArtifactFingerprint",
        "boundary: AdapterBoundary",
        "kind: AdapterObservationKind",
    ):
        require(
            declaration in observation_fields,
            f"observation missing typed dimension: {declaration}",
        )
    for forbidden_label in (
        "account_id",
        "character_id",
        "item_id",
        "chat_id",
        "assertion",
        "endpoint",
    ):
        require(
            forbidden_label not in observation_fields,
            f"high-cardinality observation label present: {forbidden_label}",
        )
    observation_impl = enclosed(
        core_source,
        "impl AdapterObservation {",
        "\n}\n\n/// Provider boundaries",
    )
    for accessor in (
        "pub const fn contract_id",
        "pub const fn operation_id",
        "pub const fn correlation_id",
        "pub const fn region_id",
        "pub const fn realm_id",
        "pub const fn schema_version",
        "pub const fn artifact_fingerprint",
        "pub const fn boundary",
        "pub const fn kind",
        "pub const fn result_class",
    ):
        require(accessor in observation_impl, f"observation accessor missing: {accessor}")

    error_fields = enclosed(
        core_source,
        "pub struct AdapterError {",
        "}\n\nimpl AdapterError",
    )
    require(
        "diagnostic_code: SanitizedDiagnosticCode" in error_fields,
        "adapter error missing typed sanitized diagnostic code",
    )
    error_impl = enclosed(
        core_source,
        "impl AdapterError {",
        "\n}\n\nimpl fmt::Display for AdapterError",
    )
    require("pub struct SanitizedDiagnosticCode" in core_source, "diagnostic code type missing")
    require(
        "pub const fn diagnostic_code" in error_impl,
        "sanitized diagnostic code accessor is missing",
    )
    require(
        "AdapterError::new" not in core_source + adapter_source,
        "noncanonical AdapterError constructor remains",
    )
    require(
        "AdapterError::from_class" in core_source + adapter_source,
        "canonical AdapterError constructor missing",
    )
    require(
        "VerifiedIdentity" not in core_source + adapter_source,
        "overstated identity result name remains",
    )
    require(
        "PlatformEvidenceReceipt" not in core_source + adapter_source,
        "overstated evidence result name remains",
    )

    require(
        "impl PlacementAdapter for StubProviderAdapter" in adapter_source,
        "stub does not prove placement adapter compilation",
    )
    require(
        "duplicate_placement_returns_same_receipt" in adapter_source,
        "stub lacks duplicate-operation test",
    )
    require(
        "reused_operation_with_different_payload_fails_closed" in adapter_source,
        "stub lacks payload-drift conflict test",
    )
    for test_name in (
        "reused_operation_with_authorization_context_drift_fails_closed",
        "lifecycle_retry_invariant_drift_conflicts_and_emits_failed",
        "cancellation_retry_invariant_drift_conflicts_and_emits_failed",
        "placement_status_rejects_mismatched_allocation_and_emits_failed",
        "cancellation_operation_payload_drift_conflicts",
        "unknown_placement_and_lifecycle_receipts_emit_failed",
        "identity_conversion_failure_emits_failed",
        "capacity_counts_only_matching_pending_region_and_artifact",
        "cross_region_placement_fails_closed",
        "placement_status_and_cancel_reject_resource_scope_mismatch",
        "lifecycle_status_rejects_resource_scope_mismatch",
        "stateless_boundaries_reject_unconfigured_scope",
    ):
        require(test_name in adapter_source, f"stub lacks focused test: {test_name}")
    for record_name in ("PlacementRecord", "LifecycleRecord", "CancellationRecord"):
        record_fields = enclosed(
            adapter_source,
            f"struct {record_name} {{",
            "}\n\n",
        )
        require(
            "context: AdapterRequestContext" in record_fields,
            f"{record_name} must retain its original context",
        )
    require(
        adapter_source.count("has_same_retry_invariants(context)") == 3,
        "placement, lifecycle, and cancellation duplicates must compare retry invariants",
    )
    for scope_guard in (
        "placement_scope_matches(context, request)",
        "placement_record_scope_matches(context, entry)",
        "placement_record_scope_matches(context, self.placements[index])",
        "lifecycle_scope_matches(context, request)",
        "lifecycle_record_scope_matches(context, record)",
        "context.contract_id() != ContractId::Capacity",
        "configured_scope_matches(context)",
        "context.realm_id().is_none()",
    ):
        require(scope_guard in adapter_source, f"adapter scope guard missing: {scope_guard}")
    require(
        "pub struct StubProviderScope" in adapter_source,
        "stub must require explicit region/artifact/compatibility configuration",
    )
    for test_name in (
        "retry_context_preserves_every_field_except_attempt",
        "retry_invariant_comparison_covers_every_context_dimension",
        "observation_preserves_mandatory_low_cardinality_dimensions",
        "failure_classes_have_one_canonical_retry_disposition",
    ):
        require(test_name in core_source, f"core lacks focused test: {test_name}")

    adr_0002 = read(adr_dir / "0002-region-local-authority-and-global-minimization.md")
    require(
        "launch sequence remains Korea first, then North America" in adr_0002,
        "ADR 0002 contradicts REG-02 launch sequence",
    )

    candidate_tokens = ("game" + "lift", "play" + "fab")
    core_rs = "\n".join(
        read(path) for path in sorted((root / "server" / "al_server_core" / "src").glob("*.rs"))
    )
    normalized_core = core_rs.lower()
    for token in candidate_tokens:
        require(
            token not in normalized_core,
            f"provider-specific token leaked into authoritative core: {token}",
        )
    for sdk_token in ("aws_sdk", "playfab", "gamelift", "reqwest", "tokio"):
        require(
            sdk_token not in normalized_core,
            f"provider or network SDK leaked into authoritative core: {sdk_token}",
        )


def main() -> int:
    root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
    try:
        validate(root)
    except (AssertionError, OSError, UnicodeError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1

    print(
        "PASS: 11 consuming-epic contracts, 4 ADRs, provider-neutral adapter and "
        "domain ports, typed observations/errors, retry invariants, and isolated "
        "disposable adapter"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
