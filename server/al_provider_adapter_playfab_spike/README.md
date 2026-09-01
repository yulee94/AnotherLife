# Disposable PlayFab adapter spike

Status: non-production, synthetic-only, and not connected to a PlayFab title.

This crate maps the provider-neutral placement, deployment, capacity, error, and
observation contracts to a minimal Microsoft PlayFab Multiplayer Servers boundary.
Provider request/session/build identifiers stay private; callers receive only
neutral receipts and stable failure classes. A transport is injected through
`PlayFabApi`, which makes duplicate, ambiguous-completion reconciliation, retained
journal adapter-instance recreation, drift cleanup, forbidden-region, lifecycle
polling, capacity, and provider-error behavior deterministic under contract tests.
Construction also requires an opaque authorization produced only when exact
title/build/region bindings and the explicit sandbox environment gate match;
disabling blocks new work while preserving cleanup for journaled placements. This
spike intentionally does not define a durable production journal format.

The crate deliberately has no concrete credential or network transport. It cannot
be enabled by the Android/Unity runtime and is not a production adapter. That hard
boundary prevents accidental title mutation while the bake-off is blocked on an
approved synthetic PlayFab title, MPS build, two approved regions, quotas, and a
credential reference. A future reviewed live executor must implement `PlayFabApi`
without exposing credentials in command lines, logs, observations, receipts, or
evidence.

From the repository root:

```sh
cargo test --manifest-path server/Cargo.toml -p al_provider_adapter_playfab_spike
python tools/architecture/run_playfab_spike.py --packet evidence/microsoft_playfab/<run-id>
python tools/architecture/validate_mmo_bakeoff_plan.py . --record evidence/microsoft_playfab/<run-id>/run-record.json
```

The packet generator fails closed and rejects uncommitted adapter/driver sources.
Without every required secure environment reference and explicit live authorization,
it creates no provider resource and
records all 16 common scenarios as blocked. It still captures the public status
signal, dated vendor sources, exact unknown-measurement inventories, a packet
secret scan, and an exercised synthetic disable/cleanup plus neutral-core rollback
proof (while the provider rollback remains honestly blocked). Public
status and vendor documentation are never promoted to measured sandbox facts.
