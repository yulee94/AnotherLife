# First-User Localization Collision Audit

## Authority and result

- Exact source baseline: main@6b79dcbbeb2f9917ae30b42548742b7fc70307b0.
- Approved comparison packet: a3-first-user-localization-semantics-2026-08-12-v001, now published separately as draft PR #466 at exact head 930f8f10cdc6282406d2139f22543837e6165874.
- Audit result: CHANGES REQUIRED across existing owning issues; no implementation, final replacement copy, or runtime disposition is approved here.
- Publication scope: this evidence document only. No runtime, catalog, save, schema, asset, workflow, shared-lock, A2/PR #369, Unity, Android, or Meshy change is authorized or included.
- The absent assigned A3 worktree was not recreated and the dirty canonical checkout was not touched.

## Grounded severity findings

P0-1 — stale Boot auto-transition contradicts the approved explicit Loading Complete gate.
- BootController immediately runs a fixed-duration fallback and routes to RealmSelection or Kingdom.
- No explicit Loading Complete interaction exists, and progress is time-based rather than readiness-authoritative.
- Owner: #284.

P0-2 — realm first tap is an irreversible local commit and direct Kingdom route.
- RealmSelectionController starts RealmSelectionCommitRoutine on the first card tap, creates a GUID request, calls TrySelectRealm, claims COMMAND ACCEPTED, waits 0.72 seconds, and loads Kingdom.
- This bypasses realm draft/review, origin/presentation/handle drafts, server genesis, local receipt projection, and 3D prologue.
- SELECT cannot alias first_user.realm.confirm because the old command commits while the approved key only confirms an uncommitted draft.
- Owner: #173.

P1-1 — local profile/save recovery and committed server-character projection recovery are semantically conflated by reusable generic wording.
- Existing profile-* tokens are local profile mutation/save authority only.
- They must never alias first_user.projection.*; CommittedProjectionPending/RecoveryRequired preserves an already-created server character, while current profile recovery may concern local save activation, migration, corruption, or deletion.
- Owner: #137.

P1-2 — player surfaces leak machine identifiers and protocol values.
- Customization title-cases raw option IDs.
- Android Unity host renders raw routeId and bridge wire error code.
- Missing localization must fail to an approved privacy-safe surface, never a machine-ID/raw-error fallback.
- Owners: #184 and #135.

P1-3 — realm fallback copy embeds stale people/race coupling, unapproved balance claims, and command-style language.
- Fallback descriptions/perks are fed directly into the realm cards.
- They must be retired from first-user onboarding; no alias to realm name or selection-line keys.
- Owner: #173.

P2 — Android shell and debug-preview copy are hard-coded and route directly to Kingdom, but do not form a production first-user localization namespace.
- Keep debug/shell notices separate; do not alias them to first_user.*.
- Production host/prologue status work belongs to #135.

## Exact path / blob / line / key / string ledger

### A. Boot and launch

1. unity/Assets/AL/Scripts/UI/BootController.cs
Blob: 282964fac49573c358624e752639bcc882248d4b

- 20: _buildLabel = "PRE-ALPHA RUNTIME"
- 36-60: Start -&gt; RunLaunchFallbackSequence -&gt; direct LoadScene("RealmSelection" or "Kingdom")
- 66: duration = max(0.8, _minSplashScreenTime)
- 79: FailToFallback("approved-media-unavailable") [machine-only diagnostic]
- 107: "ANOTHER LIFE"
- 113: "Preparing launch"
- 119: "Initializing required services"
- 293: "Initializing required services"
- 298: "Checking launch media availability"
- 303: "Using fallback launch path"
- 306: "Entering realm flow"

Disposition:
- RETAIN ANOTHER LIFE only as a brand referent; do not alias it to first_user.launch.logo.accessibility_label because decorative/meaningful role and asset are still user/visual blocked.
- RETIRE/SUPERSEDE the visible status strings as production first-user copy.
- RETIRE the direct transition behavior under #284.
- "approved-media-unavailable" remains machine-only and is never localized/rendered.

2. unity/Assets/AL/Scenes/Boot.unity
Blob: e8ef1ff3c90f336290a2d929c318ec2f7c841284

- 155: _minSplashScreenTime: 2
- 156: _realmSelectionScene: RealmSelection
- 157: _kingdomScene: Kingdom
- 159: _buildLabel: PRE-ALPHA RUNTIME
- 172: _autoLoadOnStart: 1
- 285: _sceneId: al_scene_boot
- 287: _role: production_entry

Disposition: scene IDs/role are machine-only; serialized auto-loading confirms #284 owns the state correction.

3. unity/Assets/AL/Scripts/UI/LaunchCinematicRuntime.cs
Blob: a93da17c525c99bad99df7425d4865808c975dd5

- AL-LAUNCH-* diagnostics and lifecycle states are machine-only.
- Current lifecycle completion/fallback invokes transition without a player gate.

Disposition: RETAIN diagnostics as machine-only; never alias/render them. #284 must add the semantic gate without turning diagnostics into copy.

### B. Realm selection and realm catalog

4. unity/Assets/AL/Scripts/UI/RealmSelection/RealmSelectionController.cs
Blob: 97544680c5d41f34034dba0c14dc5d2bf7dca509

Behavior:
- 102-110: first tap starts RealmSelectionCommitRoutine.
- 124-125: TrySelectRealm(new RealmSelectionRequest(Guid.NewGuid().ToString("N"), id)).
- 133-144: commit overlay, 0.72-second wait, direct next-scene load.

Strings:
- 188: "ANOTHER LIFE"
- 191: "Choose the realm that will define your command style."
- 252: realm.RealmName.ToUpperInvariant()
- 257: "SELECT"
- 262: raw realm.Description
- 316: "COMMAND ACCEPTED"
- 365: GetRealmDisplayName(id).ToUpperInvariant()
- 371: GetRealmCommandProfile(id) + " // KINGDOM LINK ESTABLISHING"
- 578: "FORTRESS ECONOMY"
- 579: "GROWTH ENGINE"
- 580: "ROYAL COMMAND"
- 581: "SHADOW WARFARE"
- 582: "COMMAND PROFILE"
- 588: id == None ? "Unclaimed Realm" : id.ToString()

Disposition:
- RETIRE/SUPERSEDE subtitle, SELECT, COMMAND ACCEPTED, command profiles, Kingdom-link string, enum-to-string fallback, and raw description rendering from production first-user flow.
- NO ALIAS SELECT -&gt; first_user.realm.confirm; semantics differ.
- NO ALIAS COMMAND ACCEPTED -&gt; any commit key; approved atomic commit has not occurred.
- ANOTHER LIFE remains only a brand referent, not final logo/accessibility copy.
- Owner #173 must also correct its legacy “per profile” wording to approved account/server receipt + separate local ProfileId projection semantics.

5. unity/Assets/AL/Scenes/RealmSelection.unity
Blob: a8cbd4b7f15c6e5343ab12dcac0272b5cb0b75c4

- 156: _nextScene: Kingdom
- 273: _autoLoadOnStart: 1
- 318: _sceneId: al_scene_realm_selection
- 320: _role: onboarding_selection

Disposition: route/scene IDs are machine-only; direct Kingdom target is stale under #173/#284 ordering.

6. unity/Assets/AL/Scripts/UI/RealmSelection/RealmSelectionCard.cs
Blob: 875aeb59912c077ab4f37cbeb489c7a04062657b

- 44: definition.RealmName.ToUpperInvariant() or "UNKNOWN REALM"
- 52: raw definition.Description

Disposition: RETIRE raw/uppercase/UNKNOWN fallback as localization behavior. Missing name/description must not expose raw machine data.

7. unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
Blob: 213a0f8e2573f45d3da331783501d9ba82e510ea

Exact fallback source:
- 28: "Stonehold Dwarves"; "Mountain kings and master smiths.\n\nPerks:\n+20% Stone\n+10% Def"; "Perks: Resilience"
- 29: "Eldergrove Elves"; "Forest guardians and peerless mages.\n\nPerks:\n+20% Wood\n+15% Magic"; "Perks: Harmony"
- 30: "Crownlands Humans"; "Adaptive leaders of the central plains.\n\nPerks:\n+15% Gold\n+10% All Atk"; "Perks: Ambition"
- 31: "Umbral Dark Elves"; "Masters of shadow and volcanic power.\n\nPerks:\n+20% Crit\n+15% Speed"; "Perks: Cunning"
- 147-148: name and combined desc/perks become RealmDefinition player copy.

Disposition: RETIRE from onboarding. These race coupling/balance claims are not approved first-user semantics and cannot become localization aliases.

8. unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json
Blob: 3de7ea95fb9cd49b30b129b0eb3b46ba156ac9f9
Version: 0.1.0

Raw source labels:
- crownlands: displayName "Crownlands"; peopleName "Crownlands Humans"
- stonehold: displayName "Stonehold"; peopleName "Stonehold Dwarves"
- eldergrove: displayName "Eldergrove"; peopleName "Eldergrove Elves"
- umbral: displayName "Umbral"; peopleName "Umbral Dark Elves"

Existing exact draft localization entries:
- realm.lock.warning = "This account will be bound to the chosen realm. Future characters on this account must belong to the same realm."
- realm.crownlands.selection.line = "Take the banner road, hold the oath in public, and let no border storm break Crownlands order."
- realm.stonehold.selection.line = "Keep forge and gate, remember the deep law, and make the mountain answer in iron."
- realm.eldergrove.selection.line = "Guard the living border, listen where roots remember, and choose mercy only when it can survive."
- realm.umbral.selection.line = "Claim the veil, keep power beneath ash, and let every oath cut before it is seen."

Disposition:
- RETAIN exact key IDs as EXISTING-DRAFT only; values remain copy-blocked.
- realm.lock.warning is required before irreversible submit.
- Selection lines are optional flavor and never substitute for the lock warning.
- Proposed realm.&lt;id&gt;.name keys do not exist at this baseline. Do not alias raw displayName as a missing-localization fallback.

9. unity/Assets/AL/Scripts/Services/Local/LocalRealmService.cs
Blob: 77c143c30f63dd8767d499d21c6c25f674f9e6b2

- AL-REALM-* technical codes and the legacy request GUID remain machine-only.

Disposition: no localization alias; no raw technical-code fallback.

### C. Character customization

10. unity/Assets/AL/Scripts/ChampionMode/Customization/ChampionCustomizationController.cs
Blob: 82394ff61332421b0532ea982a050015d2265c78

- 450: "Appearance unavailable"
- 455-457: appearance summary assembles GetProfileLabel + FormatId(raw IDs), raw compact flags "C:On|Off H:On|Off"
- 739/744/749/754/759/764/767: hardcoded "Dreadknight", "Oracle", "Duelist", "Arcanist", "Nightblade", "Vanguard", "Custom"
- 1674-1693: FormatId converts raw IDs to title-case; blank -&gt; "None"

Disposition:
- RETIRE raw ID title-casing and compact flags from player copy.
- Resolve through approved appearance localization keys or privacy-safe unavailable state under #184.
- Do not alias appearance build labels to origin or presentation.
- Appearance remains non-authoritative for server genesis.

11. unity/Assets/AL/StreamingAssets/GameData/al_character_customization_content_catalog.json
Blob: 7684f1143e05751a85915a8f0a67899c3d524e13
Version: 0.1.0
Policy: keyPrefix customization; missingKeyBehavior technical_unavailable_status; internalIdExposure debug_only; release copy user-gated.

Exact 63 EXISTING-DRAFT entries, compact prefix-preserving ledger:

- customization.family:
  body_presets.name="Build"; hair_styles.name="Hair"; armor_styles.name="Armor"; face_marks.name="Marks"; weapon_styles.name="Main Hand"; offhand_styles.name="Off Hand"
- customization.body:
  average="Average"; slim="Slim"; broad="Broad"; tall="Tall"; stout="Stout"; duelist="Duelist"; statuesque="Statuesque"; massive="Massive"; compact="Compact" (all .name)
- customization.hair:
  short="Short"; long="Long"; braid="Braid"; mohawk="Mohawk"; topknot="Topknot" (all .name)
- customization.armor:
  realm_basic="Realm Basic"; light_scout="Light Scout"; heavy_plate="Heavy Plate"; warmaster_plate="Warmaster Plate"; arcane_robes="Arcane Robes"; assassin_leathers="Assassin Leathers" (all .name)
- customization.face:
  none="None"; scar="Scar"; warpaint="Warpaint"; realm_mark="Realm Mark"; rune="Rune"; tattoo="Tattoo"; beard="Beard"; duelist_scar="Duelist Scar"; ash_mask="Ash Mask" (all .name)
- customization.weapon:
  sword="Sword"; axe="Axe"; staff="Staff"; bow="Bow"; hammer="Hammer" (all .name)
- customization.offhand:
  shield="Shield"; orb="Orb"; dagger="Dagger"; tome="Tome"; none="None" (all .name)
- customization.preset.vanguard.name="Vanguard"
  summary="Front-line plate identity with shield discipline and a heavy command silhouette."
- customization.preset.arcanist.name="Arcanist"
  summary="Tall arcane profile with robe volume, staff focus, and cool emissive accents."
- customization.preset.nightblade.name="Nightblade"
  summary="Lean predatory profile with assassin leathers, bow pressure, and dagger offhand."
- customization.preset.dreadknight.name="Dreadknight"
  summary="Massive warmaster plate identity with hammer pressure, ash mask, shield wall posture, and blood ember accents."
- customization.preset.oracle.name="Oracle"
  summary="Statuesque ritual profile with braided hair, staff and orb focus, pale cloth, and luminous green accents."
- customization.preset.duelist.name="Duelist"
  summary="Lean precision frame with light scout armor, sword and dagger, copper hair, scar detail, and burnished gold accents."
- customization.preset.inquisitor.name="Inquisitor"
  summary="Controlled adult command profile with heavy plate, sword and tome, stark battlecloth, and severe gold-lit facial detail."
- customization.preset.warden.name="Warden"
  summary="Grounded guardian profile with broad armor, braid detail, shield discipline, moss-dark cloth, and controlled green accents."
- customization.preset.spellblade.name="Spellblade"
  summary="Elegant hybrid profile with duelist frame, arcane robes, sword and orb focus, silver hair, and blue-violet arcane accents."

Disposition:
- RETAIN customization.* as appearance-only EXISTING-DRAFT namespace.
- NO ALIAS customization.body.* -&gt; first_user.presentation.*.
- NO ALIAS any customization.* -&gt; origin.*.
- Values remain non-final and do not establish appearance/voice/pronoun/genesis authority.

12. unity/Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json
Blob: b03a9378583e4b4a5823de49a65eee2498976de4
Version: 0.5.0

Raw displayName arrays match the draft keys above (nine bodies, five hair, six armor, nine face, five weapon, five offhand, nine presets). Disposition: raw displayName is source metadata, not fallback localization authority.

### D. Local profile/save recovery

13. unity/Assets/AL/Scripts/UI/ProfileMutationPresentationPolicy.cs
Blob: a6fdf6b97ebf6698f2b3d48025b240a7c11e98c0

Exact display | reason | meta token:
- 79-81: "COMMAND DECK WRITABLE — PROFILE AUTHORITY VERIFIED" | "PROFILE AUTHORITY VERIFIED" | profile-writes-authorized
- 85-87: "COMMAND DECK READ-ONLY — PROFILE WRITES NOT ACTIVATED" | "PROFILE WRITES NOT ACTIVATED" | profile-writes-not-activated
- 93-95: "COMMAND DECK READ-ONLY — PROFILE MISSING" | "PROFILE MISSING" | profile-missing
- 101-103: "COMMAND DECK READ-ONLY — PROFILE MIGRATION REQUIRED" | "PROFILE MIGRATION REQUIRED" | profile-migration-required
- 109-111: "COMMAND DECK READ-ONLY — NEWER PROFILE VERSION" | "NEWER PROFILE VERSION" | profile-forward-schema
- 117-119: "COMMAND DECK READ-ONLY — PROFILE DATA DEGRADED" | "PROFILE DATA DEGRADED" | profile-data-degraded
- 125-127: "COMMAND DECK READ-ONLY — PROFILE RECOVERY REQUIRED" | "PROFILE RECOVERY REQUIRED" | profile-recovery-required
- 133-135: "COMMAND DECK READ-ONLY — SAVE COMMIT UNRESOLVED" | "SAVE COMMIT UNRESOLVED" | profile-commit-unresolved
- 141-143: "COMMAND DECK READ-ONLY — PROFILE DELETED" | "PROFILE DELETED" | profile-deleted
- 150-152: "COMMAND DECK READ-ONLY — PROFILE AUTHORITY UNAVAILABLE" | "PROFILE AUTHORITY UNAVAILABLE" | profile-authority-unavailable

Disposition:
- RETIRE hardcoded uppercase display/reason strings from player surfaces.
- RETAIN meta tokens as machine-only status identifiers, not localization keys.
- Future local-save presentation requires a separate profile.authority.* namespace under #137.
- NEVER alias profile-recovery-required/profile-commit-unresolved to first_user.projection.recovery_required.

14. unity/Assets/AL/Scripts/UI/Kingdom/KingdomSceneController.cs
Blob: 9f36be48f55bf6a14180d773f8c71fa009d956f3

- 263-264: renders ProfileMutationPresentation.DisplayText.
- 427: header + "\n\nTEMPORARILY UNAVAILABLE\nLive data pending domain contract."
- 1491-1494: renders display text or "COMMAND DECK READ-ONLY — PROFILE AUTHORITY UNAVAILABLE"
- 1515: "CONSTRUCTION UNAVAILABLE: no authoritative result."
- 1941-1943: reset-classification surface "SYSTEM NOTICE" / "RESET" / "BOOT FLOW"

Disposition:
- Profile authority and reset copy remain #137 user/irreversible-policy scope.
- No reset wording is approved here.
- Generic unavailable/reset strings must not be reused for committed projection recovery.

15. unity/Assets/AL/Scripts/Services/Local/LocalSaveGameService.cs
Blob: 2a36ac0e01bb6a8495df78b137dd0c5137c04898

Relevant exact machine diagnostics:
- 2066: "AL-SAVE-LOAD-PRIMARY: A semantically valid primary was loaded without disk mutation or offline progression."
- 2067: "AL-SAVE-LOAD-PRIMARY-COMPATIBLE: A round-trippable primary using only approved compatibility handling was loaded without disk mutation or offline progression."
- 2088: "AL-SAVE-RECOVERY-REQUIRED: A bounded candidate view is available, but activation awaits an explicit recovery decision."
- 2187: "AL-SAVE-DELETE-FAILED: Local save reset could not remove every profile artifact. Failed={count}; Remaining={count}."
- 2204: "AL-SAVE-DELETED: Local save data deleted."

Disposition:
- Every AL-SAVE-* and SAVE_SELECT_* value, LastLoadMessage, LastSaveMessage, counts, paths, exception details, and “offline progression” phrase remains machine-only.
- “Offline” is an engineering capability/state term here, not a player localization semantic.
- No generic offline alias to first_user.commit.*, first_user.projection.*, or first_user.prologue.*.

16. unity/Assets/AL/Scripts/Core/Bootloader.cs
Blob: 3cd8bc0e3906616b2e8deb257649f77ef547bbf8

Representative exact machine-only strings:
- 267: "Unsupported offline stack marker version {version}."
- 289: "Offline service stack marker has no expected-instance inventory."
- 333: "Offline service stack marker no longer matches registered services."
- 586: "[BOOT_STACK_REUSED] Reused offline service stack {registrationId}."
- 595: "[BOOT_STACK_CREATED] Created offline service stack {registrationId}."
- 652-678: BOOT_STACK_* construction/publication/load/in-progress diagnostics.

Disposition: machine-only; never localize or render raw. There is no approved generic “offline” first-user key at this baseline.

### E. Android host/shell/resources

17. app/src/main/java/com/example/anotherlife/ui/unity/UnityView.kt
Blob: 25e7775d68239b5f164139bf498bedcd7684fefc

Hardcoded player status strings:
- 413: "Unity runtime unavailable\nLifecycle failure"
- 429/768: "Unity runtime unavailable\nHost handoff pending"
- 434: "Unity runtime unavailable\nHost handoff capacity reached"
- 506/528/544/554/620/630/642/654: "Unity runtime unavailable\nHost activation failed"
- 524: "Unity runtime unavailable"
- 567/578/591: "Unity runtime unavailable\nLifecycle callback registration failed"
- 770: "Unity runtime unavailable\nRoute: $routeId"
- 807: "Unity bridge unavailable\nCode: ${error.code.wireValue}"

Disposition:
- RETIRE generic catch-all player copy and raw route/error interpolation from production first-user host surfaces.
- Host handoff, bridge protocol, committed local projection, and prologue scene handoff are distinct meanings and must not share one generic alias.
- Raw routeId and wireValue are machine-only.
- Owner #135.

18. app/src/main/java/com/example/anotherlife/ui/shell/AdaptiveShell.kt
Blob: 383be3a246d345b7574e78283080fda31274d90f

- 79: narrativeLog.add("The kingdom awakens to a new era.")
- 85: initial route Route.Kingdom

Disposition: RETIRE/restrict as simulation shell source for production first-user entry; do not alias to first_user launch/prologue keys. Owner #135.

19. app/src/main/java/com/example/anotherlife/ui/shell/ShellRoutePolicy.kt
Blob: 89328cfd55a88f9aec5de4d800048d68dc3df306

- 7: "Developer preview is unavailable in this build. Returned to a safe screen."
- 9: "Quest preview is unavailable in this build. Returned to Kingdom."

Disposition: retain as separate debug/shell semantics pending normal Android resource localization; no alias to first_user.*.

20. app/src/main/res/values/strings.xml
Blob: dc5fa03093a04aa6284e09880b311106d79b78e9

- app_name="Another Life"
- quest_preview_open="Open quest source"

Disposition: app_name remains brand resource; quest_preview_open remains separate preview scope. No production first-user namespace exists here.

## Namespace disposition matrix

- first_user.* — approved proposed semantic namespace; absent from audited current main. ADD only through later reviewed implementation; no final values in this audit.
- origin.* — approved proposed label namespace for exact ten origin IDs; absent. ADD later; never fallback to raw IDs.
- realm.lock.warning — RETAIN exact key; EXISTING-DRAFT value; required pre-submit.
- realm.&lt;id&gt;.selection.line — RETAIN exact keys; EXISTING-DRAFT optional flavor; never lock-warning substitute.
- realm.&lt;id&gt;.name — proposed/absent; ADD later; no raw displayName fallback.
- customization.* — RETAIN appearance-only EXISTING-DRAFT namespace; no alias to origin.* or first_user.presentation.*.
- profile-* — RETAIN as machine-only status/meta tokens; RETIRE as player-facing copy source; later separate profile.authority.* presentation namespace.
- AL-LAUNCH-*, AL-REALM-*, AL-SAVE-*, SAVE_SELECT_*, BOOT_STACK_*, route IDs, wire error codes — RETAIN machine-only; never localize/render.
- app_name / quest_preview_* — retain in their brand/debug-preview scopes; no first_user alias.

## Exact issue routing

- #284 Launch cinematic runtime, patch/loading fallback, and packaging gate:
  stale auto-transition, time-based/fallback copy, Loading Complete gate.
- #173 Enforce one durable realm per profile:
  first-tap commit, direct Kingdom route, draft/review semantics, stale per-profile wording, fallback realm/balance copy.
- #184 Champion customization integrity:
  raw FormatId leakage, hardcoded profile labels, customization namespace consumption; no origin/presentation alias.
- #137 Crash-safe local save persistence:
  local profile authority/recovery/reset namespace, generic offline classification, no projection-recovery conflation.
- #135 Packaged Android↔Unity bridge:
  generic host failure copy, raw route/error leakage, stale direct-Kingdom Android shell entry.

## Validation and approval limits

- Source baseline is exact `main@6b79dcbbeb2f9917ae30b42548742b7fc70307b0`.
- Comparison packet is PR #466 at exact head `930f8f10cdc6282406d2139f22543837e6165874`.
- Every listed path, blob SHA, quoted string, and line anchor is evidence from that exact baseline.
- Existing realm draft localization key count is exactly 5.
- Existing customization draft localization entry count is exactly 63.
- Namespace decisions introduce no alias from a machine ID, raw enum, route ID, wire value, diagnostic, or raw display name to player-facing localization.
- This audit contains no class roster assumption. Issue #467 remains `SOURCE-RECONCILIATION-PENDING`; no class roster, grouping, stable ID, alias, spelling, eligibility, localization key, default, or implementation is admitted here.
- No issue comment is authorized or made by this publication.
- Documentation-only validation applies. Unity and Android builds are not run or claimed.
- Meshy and paid-operation usage is zero.

This audit approves evidence classification and RETAIN/ALIAS/RETIRE/SUPERSEDE recommendations only. It does not approve final copy or localized values, translation or locale policy, typography or layout, accessibility wording, identity or handle policy, profile-reset policy, appearance/voice/pronoun behavior, runtime, backend, save schema, routes, scenes, visual design, player acceptance, milestone completion, issue closure, merge, or release. PR #466 remains the separate comparison packet and is not superseded by this audit.
