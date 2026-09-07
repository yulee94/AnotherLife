# Oathmark wallet runtime v1

Issue #477. This is a local save-authority infrastructure slice, not a Marketplace,
authenticated account service, earning source, UI, repair shop, or consumable shop.

## Authority and compatibility

- `al_oathmark_marketplace_policy.json` supplies the canonical `oathmark` currency
  identity and forbidden currency domains. `al_oathmark_wallet_policy.json` supplies
  zero installation balance, schema binding, and receipt capacity. Neither resource
  labels nor legacy Gold, Kingdom, guild, realm, or Warzone balances are inputs.
- `SaveGameData.OathmarkWallet` is an optional schema-2 extension. Missing/default
  state is uninstalled, not a balance conversion. Existing schema-1 migration first
  establishes schema-2 profile authority; explicit installation then writes zero.
- Account identity is exactly the existing local profile identity. Wallet identity
  is `profileId:currencyId`. This is not authenticated multi-account or remote
  transaction authority. Restoring/importing a save remains governed by the existing
  profile recovery protocol; a wallet cannot make a local file tamper-proof.
- Policy identity hashes both complete source files. Their Git attributes pin LF
  bytes across checkouts. Changing either shipped file intentionally invalidates
  old wallet write policy; future policy changes require an explicit compatible
  migration, not clearing/replacing the wallet or its receipts. Do not change a
  shipped policy casually for presentation text or unrelated Marketplace tuning.
- An older binary that does not recognize this extension must preserve its raw
  save through the existing unknown-field/read-only behavior. Never roll back by
  deleting the wallet field. Recovery and policy migrations need their own tests.

## Transaction seam

`LocalSaveGameService.TryCommitOathmarkWallet` is internal. Requests bind account,
profile authority epoch/generation, wallet, currency, policy hash, operation ID,
correlation ID, operation, whole signed Int64 amount, and expected wallet revision.
Inspect/install accept only zero; credit/debit accept only positive amounts.

All operations use the existing profile-bound candidate transaction and disk
verification. Exact historical requests replay their original receipt without a
second write, including after restart. Reusing either operation or correlation ID
with a changed request is a conflict. New requests require current generation and
revision. Results are detached copies. Inspect verifies authority without writing
or creating a durable mutation receipt.

The ledger starts at zero, checks every arithmetic step, validates its full receipt
chain, and never evicts operation identities. Capacity exhaustion rejects new
mutations but permits exact replay and inspection. Corruption, uncertain writes,
stale/colliding authority, wrong profiles/currencies, and reentrant/off-owner-thread
calls cannot authorize a mutation. Uncertain writes require explicit existing save
recovery; they must not be interpreted as a successful payment.

Future sinks must compose wallet mutation and the purchased effect in ONE save
candidate/receipt transaction. Do not debit via this method and revive, repair, or
grant goods in a later save. Existing successor `t_1a94fa72` owns max-level revival;
its approved price remains a separate authority gate. Marketplace settlement still
requires its own escrow and backend authority slice. There are no production credit
callers or 2.5D minting hooks in this slice.

## Verification

Unity EditMode fixture: `AL.Tests.EditMode.ProfileBoundOathmarkWalletTests` (three
partial files). It covers install/inspect/credit/debit/restart replay, conflicts,
whole-unit overflow/negative input, forbidden wallets, stale authority, malformed
save fields and receipts, legacy migration, previous-preserved/uncertain I/O,
receipt exhaustion, and off-thread/reentrant calls. The targeted debit regression
was also run with debit disabled and failed before restoring the implementation.
Python contract tests: `unity/SharedContracts/Tests/test_oathmark_wallet_contract.py`.
