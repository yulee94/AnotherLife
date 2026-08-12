# First-User Flow Figma Board Construction Blueprint

## Status and authority

| Field | Value |
|---|---|
| Classification | CONCEPT ONLY · PLANNING APPROVED · VISUAL NOT APPROVED · NOT MERGED |
| Primary delivery mode | Coordination/review |
| Source baseline | main@6b79dcbbeb2f9917ae30b42548742b7fc70307b0 |
| Cross-lane status | Draft coordination ledger #463 |
| Destination Figma URL |  |
| Destination Figma file key |  |
| Destination Figma node IDs |  |
| Figma artifact | Not created |
| Runtime implementation | Not created |
| User visual approval | Not granted |

This document is the exact construction plan for one future Figma design file. It does not authorize a Figma write, select a destination file, approve a visual direction, or claim that the planned player flow exists in Unity.

A6 executable schemas remain PENDING/PARTIAL. This blueprint references only A1-confirmed abstract commit/reconciliation states and the confirmed binding vector. It must not be treated as an executable request, receipt, persistence, or server schema.

Permanent banner on every future Figma page:

CONCEPT ONLY · PLANNING APPROVED · VISUAL NOT APPROVED · NOT MERGED

Subtle watermark on every future product screen:

CONCEPT / NO VISUAL APPROVAL

## Scope

The future board may document:

- Current Boot and Realm Selection vocabulary with explicit CURRENT provenance.
- Planned Loading Complete, realm draft, character identity draft, handle, atomic commit, recovery, and 3D-handoff states.
- Exact source-resolved origin, eligibility, percentage, presentation, and Dark-Elf-tail rules.
- Mobile viewport planning at 360dp, 390dp, and 411dp with 100%, 150%, and 200% text.
- TalkBack intent, keyboard/controller focus order, IME/inset behavior, non-color cues, and reduced-motion equivalence.
- Traceability to the A3, A4, A6, and Context7 source packets.

The future board may not:

- Invent final copy, narrative, palette, typography, art, animation, audio, haptics, cinematic content, or production 3D assets.
- Claim current character creation, online handle validation, atomic profile commit, TalkBack integration, cinematic playback, or 3D handoff.
- Pick a final one- versus two-column layout at 390dp or 411dp.
- Define PC layouts without a separate source handoff.
- Convert A6 abstract states into executable schemas.
- Use technical IDs, hashes, or diagnostic details as player-facing copy.

## File and page structure

Future Figma working title:

AnotherLife_First_User_Flow_Concept

### 00_Cover_Scope

- S00.1_Status
  - Board_Status
- S00.2_In_Scope
  - Scope_In
- S00.3_Not_Approved
  - Scope_Out
- S00.4_How_To_Read
  - Board_Legend

Board_Status must repeat the permanent banner, source baseline, blank destination fields, and the absence of runtime and visual approval.

### 01_Source_Traceability

- S01.1_Status_Legend
  - Classification_Badges
- S01.2_Source_Register
  - Source_Register_A3_A4_A6_Context7
- S01.3_Current_Vocabulary
  - Current_Copy_And_Runtime_Behavior
- S01.4_Traceability_Matrix
  - Requirement_To_Node
- S01.5_Placeholder_And_Blocker_Register
  - Open_Gates

### 02_Fixtures_Data

- S02.1_Realm_Fixtures
  - DATA.Realm
- S02.2_Origin_Fixtures
  - DATA.Origin
- S02.3_Realm_Origin_Eligibility_4x10
  - DATA.Eligibility
- S02.4_Percent_Fixtures
  - DATA.CanonicalFirstParentPercent
- S02.5_Presentation_Fixtures
  - DATA.Presentation
- S02.6_Dark_Elf_Tail_Invariant
  - DATA.Tail
- S02.7_Handle_Fixtures
  - DATA.Handle
- S02.8_Operation_Commit_Recovery
  - DATA.Operation
- S02.9_Copy_Register
  - DATA.Copy

### 03_Semantic_Foundations

- S03.1_Color_Roles
  - TOK.Color
- S03.2_Type_Roles_And_Text_Scale
  - TOK.Type
- S03.3_Spacing_Targets_Insets
  - TOK.Layout
- S03.4_Status_Focus_NonColor
  - TOK.State
- S03.5_Motion_Reduced_Motion
  - TOK.Motion
- S03.6_Art_And_3D_Placeholders
  - TOK.Asset

### 04_Components

- S04.1_Flow_Chrome
  - AL.Chrome.FlowShell
- S04.2_Actions
  - AL.Action.Button
- S04.3_Feedback
  - AL.Feedback.Status
  - AL.Feedback.Progress
- S04.4_Realm
  - AL.Selection.RealmCard
- S04.5_Origin_Heritage
  - AL.Selection.OriginCard
  - AL.Input.HeritagePercent
- S04.6_Presentation
  - AL.Selection.PresentationGroup
- S04.7_Handle
  - AL.Input.HandleField
- S04.8_Commit_Recovery
  - AL.Profile.CommitPanel
- S04.9_3D_Handoff
  - AL.Preview.Champion3DPlaceholder
  - AL.Handoff.HandoffPanel
- S04.10_Annotation_Helpers
  - DOC.Annotation
  - DOC.TraceBadge

### 05_Primary_Flow

All canonical primary-flow frames use W390, T100, C1, Touch, Standard unless a frame says otherwise.

- S05.1_Current_Boot
  - Boot.Resolving
  - Boot.MediaFallback
- S05.2_Loading_Complete
  - LoadingComplete.Ready
- S05.3_Realm_Draft
  - Realm.CatalogLoading
  - Realm.Empty
  - Realm.PreviewSelected
  - Realm.AllegianceReviewOpen
  - Realm.DraftConfirmed
- S05.4_Pure_Origin_Path
  - Origin.PureEligible
  - Origin.PureSelected
  - Origin.PureReview_PercentOmitted
- S05.5_Half_Origin_Path
  - Origin.HalfBeforeExplicitPair
  - Origin.HalfEligiblePairSelected
  - Origin.HalfPercent30
  - Origin.HalfPercent50
  - Origin.HalfPercent70
- S05.6_Presentation_Appearance
  - Presentation.NoneSelected
  - Presentation.MaleSelected
  - Presentation.FemaleSelected
  - Appearance.DirtyPreview
- S05.7_Handle
  - Handle.Empty
  - Handle.ReadyToCheck
  - Handle.Checking
  - Handle.Available
- S05.8_Atomic_Commit
  - Review.Ready
  - Commit.Preflight
  - Commit.ReservingHandle
  - Commit.PreparingCandidate
  - Commit.Persisting
  - Commit.VerifyingReceipt
  - Commit.PublishingCommittedProfile
  - Commit.Completed
- S05.9_3D_Handoff
  - Handoff.CommittedAwaitingRuntime
  - Handoff.ScenePreparing
  - Handoff.Entering
  - Handoff.ActiveConcept

LoadingComplete.Ready requires an explicit Continue action and never auto-navigates. Handoff frames are conceptual placeholders and may not imply a current scene, video, model, or adapter.

### 06_Responsive_Stress

- S06.1_Realm_Grid_Matrix
- S06.2_Origin_Grid_Matrix
- S06.3_One_Vs_Two_Column_Comparisons
- S06.4_Long_Copy_And_Pseudolocalization
- S06.5_IME_And_Safe_Area
- S06.6_Preview_Height_Bands

Realm and Origin each contain the same 11-frame matrix:

| Width | 100% text | 150% text | 200% text |
|---|---|---|---|
| 360dp | C1 | C1 | C1 |
| 390dp | C1 and C2-CANDIDATE | C1 | C1 |
| 411dp | C1 and C2-CANDIDATE | C1 | C1 |

This produces 11 Realm frames plus 11 Origin frames: 22 responsive selection-grid frames total.

No other C2 frame is permitted. A C2-CANDIDATE passes only if complete copy, a non-color state cue, a visible focus cue, deterministic reading/navigation order, and every 48dp interaction target fit without truncation or collision. Failure means C1. C1 is mandatory at 150% and 200%.

Additional frames:

- Longest-copy stress: W411, T200, C1.
- Handle with visible IME: W360, T200, C1.
- Safe-area annotation: W390, T100, C1.
- One-versus-two-column focus comparison: W390, T100.
- Preview height annotations:
  - 100% text: 240–320dp.
  - 150% text: 192–248dp.
  - 200% text: 144–192dp.
- A 48dp View character action may open a full-screen preview; its release copy is PLACEHOLDER.
- At 200%, the primary action belongs inside the scroll flow. Do not use a sticky footer.

### 07_Failure_Recovery

Every full-screen critical fixture uses W360, T200, C1 unless a comparison explicitly says otherwise.

- S07.1_Loading_Host
  - Boot.RecoverableError
  - Boot.ProfileBlocked
  - Boot.LocalReadyOfflineDraftOnly
  - Host.Waiting
  - Host.RecoverableFailure
  - Host.TerminalFailure
- S07.2_Realm
  - Realm.CatalogUnavailable
  - Realm.ProfileUnavailable
  - Realm.InvalidRequested
  - Realm.RejectedDifferent
  - Realm.AlreadyCommittedSame
  - Realm.SaveFailedPreviousPreserved
  - Realm.InvalidTransaction
- S07.3_Appearance
  - Appearance.CatalogUnavailable
  - Appearance.InvalidDraft
  - Appearance.RollbackSucceeded
  - Appearance.DivergedBlocked
  - Appearance.PreservedUnknown
  - Appearance.StaleDraft
- S07.4_Handle
  - Handle.Duplicate
  - Handle.Invalid
  - Handle.ReservedConflict
  - Handle.RateLimited
  - Handle.ServiceUnavailableRetry
  - Handle.OfflineUnavailable
  - Handle.StaleAvailability
- S07.5_Profile_Commit
  - Profile.MigrationRequired
  - Profile.ReadOnly
  - Profile.RecoveryRequired
  - Commit.RejectedNoMutation
  - Commit.RetryableNoSideEffect
  - Commit.FailedPreviousPreserved
  - Commit.Uncertain
  - Commit.CommittedPresentationPending
  - Profile.MigrationSuccess
- S07.6_Reconciliation
  - Reconcile.Committed
  - Reconcile.TerminalRejected
  - Reconcile.CommittedThenSuppressed
  - Reconcile.InProgressOrUnknown
  - Reconcile.NotFoundAfterBarrier
- S07.7_Handoff
  - Handoff.HostRecoverable
  - Handoff.SceneRetryable
  - Handoff.CommittedButPresentationFailed
  - Handoff.OwnershipUncertain
- S07.8_Native_Recovery_Overlay
  - Recovery.RetryPermitted
  - Recovery.NoBlindRetry
  - Recovery.Terminal

Commit.Uncertain offers Check status and support guidance only. It never offers blind Retry, duplicate submission, success, or navigation. Realm.SaveFailedPreviousPreserved stays distinct from Commit.Uncertain. Launch-media fallback stays distinct from offline. Offline may preserve a draft but may not promise reservation, profile commit, progression, or 3D handoff.

The five A6 reconciliation labels above are abstract source states only. Executable transition guards, request/receipt fields, persistence behavior, and network protocol remain pending/partial and are not defined here.

### 08_Accessibility_Input

- S08.1_TalkBack_Reading_Order
  - Loading
  - Realm
  - Origin
  - Handle
  - Commit
  - Recovery
- S08.2_Controller_Focus_C1
  - Realm.FocusMap__W390__T100__C1__Controller__Standard
- S08.3_Controller_Focus_C2_Candidate
  - Realm.FocusMap__W390__T100__C2-CANDIDATE__Controller__Standard
- S08.4_Adjustable_Heritage_Semantics
- S08.5_Presentation_Semantics
- S08.6_IME_Composition_And_Insets
- S08.7_Live_Status_Announcements
- S08.8_Reduced_Motion_Equivalence
- S08.9_Accessibility_Bridge_Gate

TalkBack order:

1. Back, when present.
2. Title and step.
3. Blocking status.
4. Controls in visual order.
5. Field-specific help or error.
6. Primary action.
7. Secondary action.

A Realm or Origin card is one semantic focus target. C2 controller navigation is deterministic and row-major. Heritage is one adjustable control that announces both parents and both percentages and changes exactly one point per increment. Presentation is a two-item radio group and never announces voice or pronoun consequences. Tail is not focusable because it is not an input.

Availability updates use a polite live region. A single blocking commit error may be assertive. Do not announce every animation frame or percent tick. Modal focus begins at the heading and returns to the invoking control. The IME fixture preserves composition and selection, rejects stale-generation validation, and keeps the field, error, and applicable action visible. Exactly one surface owns bottom/IME insets.

Reduced motion removes pulse, scale, camera sweep, and motion-only meaning while preserving state order and actions.

BLOCKED: the project has no proven production Unity TalkBack bridge. These annotations are design intent, not runtime evidence.

### 09_Handoff_QA

- S09.1_Construction_Checklist
- S09.2_Responsive_QA
- S09.3_State_And_Recovery_QA
- S09.4_Accessibility_QA
- S09.5_Trace_Coverage
- S09.6_Open_Gates
- S09.7_Future_Implementation_Notes
- S09.8_Approval_Record_Placeholders

## Frame and layer naming

Screen name pattern:

Flow.State__Wwidth__Tscale__Ccolumns__InputMode__MotionMode

Examples:

- Realm.Ready__W390__T100__C1__Touch__Standard
- Realm.Ready__W390__T100__C2-CANDIDATE__Controller__Standard
- Commit.Uncertain__W360__T200__C1__TalkBack__Reduced
- Handle.Editing-IME__W360__T200__C1__Touch__Reduced

Every screen uses this layer hierarchy:

- Viewport
  - SafeArea
    - FlowShell
      - Header
      - StatusRegion
      - ScrollRegion
        - StepContent
      - ActionRegion
- Annotations

Annotations is a sibling outside the clipped Viewport. Width, text scale, column count, input method, and motion mode are frame context, not component variant axes.

## Component sets and properties

### AL/Chrome/FlowShell

- Back: Hidden, Enabled, Disabled.
- Footer: None, Primary, PrimarySecondary.
- Status: None, Inline, Banner.
- IMEInset: False, True.
- Text properties: StepLabel, Title, SupportingText.

### AL/Action/Button

- Hierarchy: Primary, Secondary, Tertiary.
- State: Rest, Focus, Pressed, Disabled, Busy.
- Optional icon property.
- Visible text label required.

### AL/Feedback/Status

- Kind: Info, Success, Warning, Error, Offline.
- Placement: Inline, Banner, Modal.
- Action: None, Retry, Support, CheckStatus.
- Icon, heading, and body are required for non-color communication.

### AL/Feedback/Progress

- Mode: Indeterminate, Determinate, Stage.
- State: Active, Paused, Complete, Error.
- Determinate data is permitted only when a source exposes truthful completed, total, and unit values.
- Do not invent save or migration percentages.

### AL/Selection/RealmCard

- State: Rest, Focus, Selected, Disabled, Locked, Unavailable.
- Text properties: RealmName, CommandProfile, Description.
- EmblemPlaceholder property.
- Whole card is one semantic target.

### AL/Selection/OriginCard

- Kind: Pure, Half.
- State: Rest, Focus, Selected, Disabled, Ineligible.
- DisplayLabel remains placeholder.
- StableId is annotation/data only and never player-facing copy.

### AL/Input/HeritagePercent

- State: Ready, Focus, Disabled, Error.
- Value is a numeric property, not 41 component variants.
- Range is 30 through 70 inclusive.
- Step is one integer point.
- Component is absent for pure origins.
- Value is absent before an eligible half pair is explicitly selected.
- The UI draft becomes 50 only after that explicit eligible selection.
- Localization may reorder parent wording but may not change canonical stored orientation.

### AL/Selection/PresentationGroup

- Selection: None, male, female.
- State: Rest, Focus, Disabled, Error.
- Stable values are exact and case-sensitive.
- No voice or pronoun property exists.

### AL/Input/HandleField

- State: Empty, Editing, ReadyToCheck, Checking, Available, Duplicate, Invalid, ReservedConflict, RateLimited, ServiceUnavailable, Offline, StaleAvailability, ReadOnly.
- Trailing: None, Spinner, Success, Error.
- The visible handle-versus-username term and service policy copy remain placeholders.

### AL/Profile/CommitPanel

- State: Confirm, Preflight, Submitting, Success, RejectedNoMutation, RetryableNoSideEffect, FailedPreviousPreserved, CommitUncertain, CommittedPresentationPending, ReadOnly, MigrationRequired, RecoveryRequired, MigrationSuccess, Offline.
- Action: None, Retry, CheckStatus, Support.
- CommitUncertain must never pair with Retry.

### AL/Preview/Champion3DPlaceholder

- Origin: Pure, Half.
- Tail: None, Required.
- State: Placeholder, Loading, Unavailable.
- Motion: Standard, Reduced.
- No Tail toggle exists.
- No production model, rig, adapter, or scene is implied.

### AL/Handoff/HandoffPanel

- State: Ready, Progress, Complete, RetryableUnavailable, OwnershipUncertain.
- Motion: Standard, Reduced.

### DOC/TraceBadge

- Class: CURRENT, SOURCE-DRAFT, PLANNED, PLANNED-SOURCE-RESOLVED-NOT-MERGED, BLOCKED, PLACEHOLDER.
- Badges use text, icon shape, and outline. Color alone is insufficient.

## Exact fixture tables

### Realms

| Fixture | Stable ID | Display name | Order |
|---|---|---|---:|
| R01 | crownlands | Crownlands | 1 |
| R02 | stonehold | Stonehold | 2 |
| R03 | eldergrove | Eldergrove | 3 |
| R04 | umbral | Umbral | 4 |

peopleName, current command-profile text, and final release copy retain separate provenance. They do not silently become a selectable race label.

### Origins

| Fixture | Exact stable ID | Kind |
|---|---|---|
| O01 | race_human | Pure |
| O02 | race_dwarf | Pure |
| O03 | race_elf | Pure |
| O04 | race_dark_elf | Pure |
| O05 | heritage_half_human_dwarf | Half |
| O06 | heritage_half_human_elf | Half |
| O07 | heritage_half_human_dark_elf | Half |
| O08 | heritage_half_dwarf_elf | Half |
| O09 | heritage_half_dwarf_dark_elf | Half |
| O10 | heritage_half_elf_dark_elf | Half |

No alias, reverse ID, or generic multi-segment origin is permitted.

### Literal realm-origin eligibility matrix

A check is valid. A dash is invalid and must remain noninteractive with a non-color cue.

| Realm | O01 | O02 | O03 | O04 | O05 | O06 | O07 | O08 | O09 | O10 | Valid |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| crownlands | ✓ | — | — | — | ✓ | ✓ | ✓ | — | — | — | 4 |
| stonehold | — | ✓ | — | — | ✓ | — | — | ✓ | ✓ | — | 4 |
| eldergrove | — | — | ✓ | — | — | ✓ | — | ✓ | — | ✓ | 4 |
| umbral | — | — | — | ✓ | — | — | ✓ | — | ✓ | ✓ | 4 |

Checksum: 4 realms × 10 origins = 40 cells; exactly 16 valid and 24 invalid.

### Percentage fixtures

| Fixture | Input | Expected |
|---|---|---|
| PCT-PURE | Pure origin | Field omitted; control absent |
| PCT-UNSELECTED-HALF | No explicit eligible half selection | Value absent; control inactive |
| PCT-30 | 30 | Accepted |
| PCT-31 | 31 | Accepted |
| PCT-50 | 50 | Accepted only after explicit eligible half selection |
| PCT-69 | 69 | Accepted |
| PCT-70 | 70 | Accepted |
| PCT-29 | 29 | Rejected; do not clamp |
| PCT-71 | 71 | Rejected; do not clamp |

The stored field is canonicalFirstParentPercent. Second-parent share is 100 minus that value. Realm-native share is derived from canonical orientation and is never separately stored.

### Presentation fixtures

| Fixture | Exact value | Expected |
|---|---|---|
| PRESENTATION-NONE | absent | Selection required by planned flow |
| PRESENTATION-MALE | male | Accepted |
| PRESENTATION-FEMALE | female | Accepted |

No alias, case folding, voice choice, or pronoun choice is implied.

### Dark-Elf tail invariant

| Origin fixtures | Tail | Rule |
|---|---|---|
| O04 | Exactly one | Pure Dark Elf appearance |
| O07, O09, O10 | Exactly one | Prominence derives continuously from Dark-Elf share |
| O01, O02, O03, O05, O06, O08 | None | No Dark-Elf share |

Tail is appearance-only and never an input. Exact asset, curve, rigging, animation, collision, and final visual treatment remain BLOCKED.

### Confirmed A6 binding vector

Annotation-only values:

- Binding ID: realm-identity-binding/1
- Digest: sha256:70389ca2e64ac41de43ef1d07a81733457e54c4a23191714dcec755065e13a57

These values must never appear as release copy.

## Current copy register

Only exact current strings may carry CURRENT provenance. Examples from the audited baseline include:

- ANOTHER LIFE
- Preparing launch
- PRE-ALPHA RUNTIME
- Initializing required services
- Checking launch media availability
- Using fallback launch path
- Entering realm flow
- Choose the realm that will define your command style.
- SELECT
- COMMAND ACCEPTED
- KINGDOM LINK ESTABLISHING
- PROFILE MIGRATION REQUIRED
- PROFILE RECOVERY REQUIRED
- SAVE COMMIT UNRESOLVED

Loading Complete, Continue, offline guidance, validation, error, retry, recovery, support, migration, origin, handle, and 3D-handoff release copy are not approved merely because a planned state exists.

## Semantic token names

Functional names may be defined before visual values:

- color.surface.canvas
- color.surface.raised
- color.text.primary
- color.text.secondary
- color.action.primary
- color.focus.ring
- color.status.info
- color.status.success
- color.status.warning
- color.status.error
- color.status.offline
- color.border.default
- color.border.strong
- type.display
- type.title
- type.heading
- type.body
- type.label
- type.support
- type.technical
- space.xs
- space.sm
- space.md
- space.lg
- space.xl
- radius.sm
- radius.md
- radius.lg
- elevation.raised
- elevation.modal
- motion.enter
- motion.exit
- motion.feedback
- motion.cinematic
- target.interactive.min

All visual token values remain PLACEHOLDER except target.interactive.min = 48dp. Placeholder color values cannot receive a contrast PASS.

## Annotation convention

Every specimen must carry:

- Classification badge.
- Fixture ID.
- One or more trace IDs.
- Behavior and effect boundary.
- TalkBack output/order.
- Keyboard/controller action.
- Responsive rule.
- Reduced-motion equivalent.
- Placeholder or blocker.
- Do not infer note.
- CONCEPT / NO VISUAL APPROVAL watermark.

Technical IDs, hashes, fingerprints, receipts, profile identifiers, handles, tokens, file paths, exceptions, and stack traces are never release copy. A support fixture may expose only a bounded sanitized support code placeholder.

## Traceability matrix

| Trace ID | Requirement | Class | Board nodes | Open gate |
|---|---|---|---|---|
| TR-A3-001 | Loading Complete is an explicit interaction gate after readiness and media terminals | SOURCE-DRAFT | S05.2 | Final copy and runtime |
| TR-A3-002 | Realm remains draft/preview/review before commit | SOURCE-DRAFT | S05.3 | Runtime integration |
| TR-A3-003 | Exact ten origin IDs; no aliases | PLANNED-SOURCE-RESOLVED-NOT-MERGED | S02.2 | Merge/runtime |
| TR-A3-004 | Literal 4x10 eligibility and 16/24 checksum | PLANNED-SOURCE-RESOLVED-NOT-MERGED | S02.3 | Merge/runtime |
| TR-A3-005 | Pure omission; half integer 30..70 step 1; conditional draft 50 | PLANNED-SOURCE-RESOLVED-NOT-MERGED | S02.4, S04.5 | Merge/runtime |
| TR-A3-006 | Canonical orientation and derived shares | PLANNED-SOURCE-RESOLVED-NOT-MERGED | S02.4 | Localization/runtime |
| TR-A3-007 | Exact male or female; no voice/pronoun inference | PLANNED-SOURCE-RESOLVED-NOT-MERGED | S02.5, S04.6 | Merge/runtime |
| TR-A3-008 | Dark-Elf tail invariant and derived prominence | PLANNED-SOURCE-RESOLVED-NOT-MERGED | S02.6 | Curve, asset, runtime |
| TR-A3-009 | Realm change revalidates origin and clears incompatible downstream draft | SOURCE-DRAFT | S07.2, S07.3 | Executable state contract |
| TR-A3-010 | Cancel/reconnect preserves authority boundaries; no partial success | SOURCE-DRAFT | S07.4–S07.7 | Executable state contract |
| TR-A3-011 | Machine IDs never become release copy | SOURCE-DRAFT | S01.3, S02.9 | Localization approval |
| TR-A4-001 | Current-versus-planned loading and explicit Continue | PLANNED | S05.1, S05.2 | Runtime |
| TR-A4-002 | Loading, realm, appearance, handle, commit, and handoff state families | PLANNED | S05, S07 | Runtime |
| TR-A4-003 | 360/390/411 at 100/150/200 | PLANNED | S06.1, S06.2 | Render/device QA |
| TR-A4-004 | C2 only as measured 390/411 at 100% candidate | PLANNED | S06.3 | Visual decision |
| TR-A4-005 | 48dp, full copy, no truncation, C1 at 150/200 | PLANNED | S03.3, S06 | Render/device QA |
| TR-A4-006 | Preview height bands and full-screen preview action | PLANNED | S06.6 | Final copy/3D |
| TR-A4-007 | TalkBack order, non-color cues, focus restoration | PLANNED | S08.1, S08.7 | Runtime bridge/device QA |
| TR-A4-008 | Deterministic controller focus for C1/C2 | PLANNED | S08.2, S08.3 | Runtime/device QA |
| TR-A4-009 | IME composition, visibility, and one inset owner | PLANNED | S08.6 | Runtime/device QA |
| TR-A4-010 | Reduced-motion semantic equivalence | PLANNED | S08.8 | Runtime/device QA |
| TR-A4-011 | Offline is draft-only planning, not final copy | PLANNED | S07.1, S07.4 | Copy/backend |
| TR-A4-012 | No onboarding bottom navigation | PLANNED | S04.1, S05 | Visual/runtime |
| TR-A6-001 | Confirmed binding ID and digest | PLANNED-SOURCE-RESOLVED-NOT-MERGED | S02.8 | Executable schema pending/partial |
| TR-A6-002 | Abstract commit stages and verified-receipt gate | PLANNED | S05.8 | Executable schema pending/partial |
| TR-A6-003 | COMMITTED reconciliation disposition | PLANNED | S07.6 | Executable schema pending/partial |
| TR-A6-004 | TERMINAL_REJECTED reconciliation disposition | PLANNED | S07.6 | Executable schema pending/partial |
| TR-A6-005 | COMMITTED_THEN_SUPPRESSED disposition | PLANNED | S07.6 | Executable schema pending/partial |
| TR-A6-006 | IN_PROGRESS_OR_UNKNOWN uses same-operation reconciliation | PLANNED | S07.6 | Executable schema pending/partial |
| TR-A6-007 | NOT_FOUND_AFTER_BARRIER is constrained and never a casual retry | PLANNED | S07.6 | Executable schema pending/partial |
| TR-A6-008 | Abstract receipt echoes origin/presentation and conditionally half percentage | PLANNED | S02.8 | Executable schema pending/partial |
| TR-A6-009 | Commit uncertainty never blind-retries | PLANNED | S07.5, S07.6 | Executable schema pending/partial |
| TR-A6-010 | Support output remains privacy-safe | PLANNED | S07.8 | Final support policy |
| TR-C7-001 | Durable operation precedes external mutation; UI state is not authority | PLANNED | S02.8, S07.6 | Platform implementation |
| TR-C7-002 | Readiness is authoritative identity plus terminal media plus idle transition | PLANNED | S05.1, S05.2 | Runtime |
| TR-C7-003 | Media has finite complete, explicit skip, or accessible fallback terminal | PLANNED | S05.1, S08.8 | Media implementation |
| TR-C7-004 | IME selection/composition and generation-bound validation | PLANNED | S08.6 | Platform implementation |
| TR-C7-005 | Exactly one inset owner across surfaces | PLANNED | S06.5, S08.6 | Platform implementation |
| TR-C7-006 | Reservation/commit are online-authoritative; timeout is unknown | PLANNED | S07.4–S07.6 | Backend/client implementation |
| TR-C7-007 | Back changes UI draft only | PLANNED | S04.1, S05 | Platform implementation |
| TR-C7-008 | Local cursor is bounded/non-authoritative and recovery fails closed | PLANNED | S02.8, S07.6 | Platform implementation |
| TR-C7-009 | Unity accessibility bridge is unresolved | BLOCKED | S08.9 | Plugin/upgrade decision |
| TR-C7-010 | Device performance/package evidence remains required | BLOCKED | S09.6 | Implementation/device QA |
| TR-C7-011 | Recreation, process death, restore, accessibility, media, and profiling evidence stay distinct | PLANNED | S09.2–S09.4 | Validation |

## Placeholder and blocker register

PLACEHOLDER:

- Final palette and contrast values.
- Font family and exact typography metrics.
- Spacing values beyond the functional 48dp target.
- Radii, shadows, gradients, and textures.
- Realm art, emblems, and environments.
- Character art and production 3D models, pose, lighting, camera, and scene.
- Tail asset and prominence curve.
- Display labels where only stable IDs are resolved.
- Visible handle-versus-username term and service policy copy.
- Loading Complete, validation, error, retry, recovery, support, migration, offline, origin, and handoff release copy.
- Support destination and sanitized code format.
- Motion duration/easing, audio, haptics, and cinematic treatment.
- Pseudolocalized and final localized strings.

BLOCKED OR UNRESOLVED:

- Destination Figma URL, file key, and node IDs.
- Final C2 decision at 390dp or 411dp.
- PC layout.
- Actual Unity TalkBack bridge, plugin, or upgrade path.
- Production character creation, online handle, atomic commit, and reconciliation implementation.
- A6 executable schemas, which remain pending/partial.
- Production 3D adapter and scene handoff.
- Cinematic/video integration.
- Device, accessibility, performance, memory, build-size, install-size, and integrated-playtest evidence.
- Final user visual/product/milestone/release approval.

## Future construction validation

Before a future Figma artifact can receive a review disposition:

1. Confirm all ten exact, case-sensitive origin IDs and reject aliases or reversed IDs.
2. Recompute the 4x10 table: exactly 16 valid and 24 invalid.
3. Confirm pure origins contain no percentage control or stored percentage.
4. Confirm half value is absent before explicit eligible selection, becomes 50 afterward, and is integer 30 through 70 with step one.
5. Confirm male and female are the only exact presentation IDs and have no voice/pronoun semantics.
6. Confirm the tail is derived and noninteractive only where Dark-Elf share exists.
7. Confirm Realm and Origin each contain 11 responsive frames: 22 total.
8. Confirm every 150% and 200% frame is C1 and C2-CANDIDATE appears only at 390dp/411dp and 100%.
9. Confirm no truncation, overlap, horizontal primary-flow scrolling, or IME-obscured field/error/action.
10. Confirm every interactive target is at least 48dp.
11. Confirm focus, selection, warning, error, and availability never rely on color alone.
12. Confirm TalkBack and controller orders match visual order.
13. Confirm reduced motion preserves meaning and actions.
14. Confirm media fallback is not labeled offline.
15. Confirm Commit.Uncertain has no Retry or navigation.
16. Confirm success and handoff appear only after authoritative verified commit.
17. Confirm technical identifiers and support annotations are privacy-safe and not player copy.
18. Confirm every trace row resolves to a board node, fixture, future QA evidence, and open gate.
19. Confirm placeholder color tokens are UNTESTED, not contrast-passed.
20. Confirm no screen claims an existing cinematic, 3D path, accessibility bridge, or executable A6 schema.

## Approval limits

Approval of this document approves only the repository-tracked construction specification.

It does not approve:

- A destination Figma file, URL, key, node, component, prototype, or design.
- Palette, typography, spacing, art, copy, animation, audio, haptics, or cinematic direction.
- A two-column mobile layout.
- A PC layout.
- Runtime, save, schema, service, catalog, scene, Android, iOS, or PC implementation.
- TalkBack integration.
- Character creation, handle availability, commit, reconciliation, or 3D-handoff availability.
- A6 executable schemas.
- Visual fidelity, integrated playtest, milestone, store, release, or user acceptance.

A1 must separately authorize a destination and exact write scope before any Figma action. The user retains final product, creative, visual-design, integrated-playtest, milestone, and release approval.

## Repository publication record

- Proposed path: unity/Docs/First_User_Flow_Figma_Board_Blueprint.md
- Branch: codex/coordination-first-user-figma-blueprint
- Draft PR title: docs: specify the first-user Figma board construction blueprint
- Related ledger: https://github.com/yulee94/AnotherLife/pull/463
- Reviewer requested after draft creation: @rslee94
- Shared-file locks: none
- Runtime/assets/build impact: none
