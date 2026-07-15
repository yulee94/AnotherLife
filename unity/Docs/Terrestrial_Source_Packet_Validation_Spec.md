# Terrestrial Design Source-Packet Validation and Handoff Specification

**Status date:** 2026-07-15  
**Tracking issue:** #194  
**Active source PR:** #217  
**Technical specification/review owner:** GPT  
**Source-design owner:** Codex terrestrial-design mode  
**Narrative naming/lore owner:** Codex narrative/content mode  
**Engineering owner after approval:** Codex engineering mode  
**Final creative approval:** user  
**Audited baseline:** `91202c7b05ccf2897646fe7cdfafba2a1a1ddf96`  
**Reviewed source head:** `f8ab5f6bc60ada117a384c4263c09a520f821644`  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`  
**Runtime authority contract:** `unity/Docs/Game_Data_Catalog_Authority_Spec.md`

## 1. Goal

Define the exact technical completeness, source identity, provenance, transport, review-surface, import, and handoff requirements for an original terrestrial-design packet before it is shown to the user for creative approval or consumed by engineering.

This specification does not judge whether the creatures are attractive, appropriate, or creatively approved. It gives the user and later engineering reviewers confidence that:

- the pixels they review are the exact pixels recorded by the source packet;
- every declared profile and variant has an honest source-completeness state;
- media can be retrieved from a clean checkout rather than existing only on one workstation;
- source labels and biome tags are not mistaken for player-facing or runtime identifiers;
- Unity import, when used, is deliberate and reproducible;
- engineering can trace every derivative back to one immutable source version;
- no runtime, gameplay, AI, spawning, combat, reward, save, narrative, or balance authority is inferred from a concept packet;
- user creative approval is recorded separately from technical review and separately from runtime integration.

## 2. Binding decisions

1. **Technical completeness is not creative approval.** GPT may disposition a packet `TECHNICAL HANDOFF COMPLETE`; only the user may approve its visual direction.
2. **Creative approval is not runtime approval.** A user-approved source packet remains non-runtime until a separate Codex engineering PR passes its own dependencies and technical review.
3. **The source PR must not close #194.** Use `Refs #194`; the issue remains open through user approval and engineering consumption without silent redesign.
4. **Every reviewed media file has immutable identity in the manifest.** PR prose alone is insufficient.
5. **Every Git LFS object must be retrievable in a clean environment.** Upload confirmation is not retrieval proof.
6. **A reviewer must see the actual rendered pixels.** File names, hashes, prompts, LFS pointers, and metadata do not substitute for direct visual presentation.
7. **Variants cannot be called approved when no visual source exists.** Text-only ideas use a proposed/pending state.
8. **The manifest has a retained schema and semantic validator.** JSON parse success and path existence are not enough.
9. **Working labels are review labels only.** They are neither localization keys nor player-facing names.
10. **Biome/realm eligibility entries are source-design intent tags only.** They are not current #183 catalog IDs, spawn rules, encounter logic, or save state.
11. **Source-design IDs are stable source identities.** Runtime/profile/spawn IDs may later reuse them only through an explicit #183 mapping; reuse is not automatic.
12. **Source images under `unity/Assets` are imported Unity assets.** Keeping them there requires deliberate importer settings and canonical Unity import evidence.
13. **Pure review media may instead live under `unity/Docs`.** Moving there removes Unity importer/runtime implications but still requires LFS/media validation where applicable.
14. **No external input is represented as absent merely because no file was copied.** Prompts, references, mood boards, model checkpoints, source sketches, edits, and generators are all provenance inputs and must be declared.
15. **Generated-original provenance is versioned.** The manifest records the generator/product, date, source prompt record, editing steps, and external-input list.
16. **Licensing statements are evidence records, not legal conclusions.** They identify source and intended project use without making unsupported ownership guarantees.
17. **Hashes are over the retrieved binary bytes.** The Git LFS pointer hash and the file SHA-256 are recorded distinctly when they differ in representation.
18. **Pixel dimensions and media type are validated from the file header, not trusted from prose or an extension.**
19. **Source-version changes are immutable.** Changed pixels, dimensions, prompt lineage, profile intent, or variant source require a new source version and updated hashes.
20. **A source version never silently reuses an ID for a different silhouette or design meaning.** Material/palette-only variants may share a profile; silhouette/anatomy redesign requires a new profile or explicit superseding migration.
21. **Engineering derives nothing beyond the approved source.** Missing variant art, hidden views, materials, rig behavior, or scale cannot be improvised as if approved.
22. **Reduced-motion and non-color readability are source requirements.** Runtime implementation must preserve them or return to design review.
23. **No source packet creates gameplay authority.** AI, navigation, spawn tables, faction/realm binding, interaction, hostility, rewards, combat roles, collection, persistence, and rarity remain unresolved unless separately approved.
24. **No source packet creates narrative authority.** Final names, descriptions, lore, story meaning, and localization remain Codex narrative/content plus user approval.
25. **The first engineering integration is separate from the source PR.** Mixed source/engineering scope requires an explicit exceptional rationale and review; it is not the default.
26. **Runtime integration remains gated by #156, #183, the owning world/creature issue, #150 packaging, applicable #127/#178/#180 controls, and user approval.**
27. **Source preview images are not final production art by default.** The packet states whether each asset is concept, turnaround, material callout, paintover, or production-ready source.
28. **Editable source availability is explicit.** `not available` is acceptable when truthful; it must not be implied to exist.
29. **A missing LFS object, unrenderable media, hash mismatch, malformed manifest, or unresolved provenance blocks technical completion.**
30. **Unity licensing/import failure is blocked evidence, not a pass.**

## 3. Verified current PR #217 state

### 3.1 Delivered written source

The current packet contains:

```text
unity/Docs/Terrestrials/README.md
unity/Docs/Terrestrials/Terrestrial_Design_Brief.md
unity/Docs/Terrestrials/Terrestrial_Engineering_Handoff.md
unity/Docs/Terrestrials/Source_Prompts_And_Provenance.md
unity/Docs/Terrestrials/terrestrial_profiles_manifest.json
```

The brief records:

- visual tone;
- non-humanoid silhouette/anatomy rules;
- approximate Champion-relative scale;
- material/palette direction;
- motion intent;
- LOD/accessibility intent;
- explicit non-authority;
- unresolved user decisions.

The engineering handoff correctly prohibits silent derivation of narrative, gameplay, AI, spawn, reward, save, or redesigned visual meaning.

### 3.2 Delivered media paths

```text
unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_basalt_grazer_concept_sheet_v001.png
unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_grove_strider_concept_sheet_v001.png
unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_mire_lumenback_concept_sheet_v001.png
```

Each is tracked through Git LFS and has a Unity `.meta` file.

The PR reports:

```text
1536 x 1024
```

for each image and reports these file SHA-256 values:

```text
tdf_basalt_grazer: 2e14484df86f685f16b0cf00db9de85bb132651b0f83354dabea0b451bfdc354
tdf_grove_strider: 4e7864cc02c571357ad3faf8c77631bd9fa1c08944d18e89ad36a6b9dac89920
tdf_mire_lumenback: 39154a3ea94394efabac558e704a54a630e005371123b32ec9dd3803ec2235b0
```

Those values currently live in PR prose rather than the manifest.

### 3.3 Current profile inventory

```text
tdf_basalt_grazer
tdf_grove_strider
tdf_mire_lumenback
```

Each profile has one delivered base concept sheet.

### 3.4 Current variant inventory

The manifest declares nine variants:

```text
tdf_basalt_grazer_standard
tdf_basalt_grazer_ashen
tdf_basalt_grazer_mineral

tdf_grove_strider_standard
tdf_grove_strider_late_autumn
tdf_grove_strider_mist

tdf_mire_lumenback_standard
tdf_mire_lumenback_clay
tdf_mire_lumenback_night
```

Only one concept sheet per profile is currently delivered. The non-standard variants have written intent but no independent sheet, swatch, paintover, or exact pixel-region reference. They cannot be represented as visually approved or engineering-ready.

### 3.5 Current transport/review limitation

The repository metadata exposes the Git LFS pointer, object ID, and declared object size. The current review transport does not expose the binary pixels directly through the repository file response. The PR also does not embed or link rendered full-resolution sheets in its description.

Therefore GPT cannot honestly perform silhouette, anatomy, material, scale-chart, or visual-fidelity review from the actual pixels at the current head. That limitation remains a blocker until direct rendered media and clean LFS retrieval evidence are provided.

### 3.6 Current Unity import boundary

The images live under `unity/Assets`, so Unity imports them as normal textures. Current metadata uses ordinary texture settings, mipmaps, compression, and a 2048 maximum size. The PR has no canonical Unity import evidence for those assets.

If the intended use is documentation/review only, keeping them as imported Unity assets is unnecessary overhead unless a later editor workflow intentionally consumes them.

## 4. Source-packet readiness model

The packet exposes four independent states.

### 4.1 Technical packet state

```text
Draft
SourceIncomplete
ValidationFailed
TechnicalReviewReady
TechnicalHandoffComplete
Superseded
```

`TechnicalHandoffComplete` means:

- manifest/schema/semantic validation passes;
- every delivered asset is retrievable and hash-verified;
- the review surface renders every required source;
- provenance and license records are complete;
- declared variants are honestly classified;
- Unity import evidence passes or media has moved out of `Assets`;
- no runtime/narrative authority is implied.

It does not mean the design is approved.

### 4.2 User creative state

```text
NotRequested
ReadyForReview
ChangesRequested
ApprovedSourceVersion
Rejected
Superseded
```

Only the user changes this state to `ApprovedSourceVersion` or `Rejected`.

Approval identifies the exact source version and immutable asset hashes. It is not a general approval of future edits.

### 4.3 Runtime integration state

```text
Blocked
NotPlanned
Planned
EngineeringInProgress
TechnicalValidationFailed
ReadyForIntegratedReview
IntegratedAccepted
Superseded
```

PR #217 remains `Blocked` regardless of technical/creative source review.

### 4.4 Narrative naming state

```text
WorkingLabelsOnly
NarrativeSourcePending
UserNameApprovalPending
ApprovedLocalizationSource
```

The current packet remains `WorkingLabelsOnly`.

## 5. Manifest contract

A retained schema must validate equivalent fields. Names may vary only when meaning remains exact.

### 5.1 Top-level fields

```text
schemaVersion
packetId
sourceVersion
createdAtUtc
authority
readiness
profiles
sourceAssets
generationRecords
externalInputs
supersedesSourceVersion
```

Recommended example:

```json
{
  "schemaVersion": 2,
  "packetId": "anotherlife-terrestrial-foundation",
  "sourceVersion": "tdf-2026-07-15-v001",
  "createdAtUtc": "2026-07-15T00:00:00Z",
  "authority": {
    "sourceOwnerMode": "Codex terrestrial-design",
    "technicalReviewOwner": "GPT",
    "narrativeOwnerMode": "Codex narrative/content",
    "finalCreativeApprover": "user",
    "runtimeAuthority": false,
    "narrativeAuthority": false
  },
  "readiness": {
    "technicalPacketState": "TechnicalReviewReady",
    "userCreativeState": "NotRequested",
    "runtimeIntegrationState": "Blocked",
    "narrativeNamingState": "WorkingLabelsOnly"
  },
  "profiles": [],
  "sourceAssets": [],
  "generationRecords": [],
  "externalInputs": [],
  "supersedesSourceVersion": null
}
```

### 5.2 Top-level validation

- `schemaVersion` is a positive supported integer;
- `packetId` is nonblank and stable;
- `sourceVersion` is nonblank and matches every profile/variant/asset/generation record;
- `createdAtUtc` is a valid UTC instant;
- authority values match `Ownership_Decision_Record.md`;
- runtime and narrative authority are false;
- readiness enum values are recognized;
- `TechnicalHandoffComplete` is invalid while user state claims `ApprovedSourceVersion` without a separate recorded user decision;
- runtime state cannot advance beyond `Blocked` in the source PR;
- `supersedesSourceVersion` cannot equal the current version;
- ordering is deterministic.

## 6. Profile contract

Each profile includes equivalent:

```text
terrestrialProfileId
sourceVersion
workingReviewLabel
workingLabelStatus
designIntentTags
approximateWorldScale
silhouetteClass
anatomyIntent
materialSlotIntent
motionIntent
rigOrSkeletonIntent
requiredAnimationIntent
lodIntent
colliderIntent
vfxAnchorIntent
accessibilityNotes
explicitExclusions
unresolvedDecisions
primarySourceAssetIds
variants
```

### 6.1 Profile ID rules

```text
^tdf_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$
```

- unique, nonblank, ordinal case-sensitive;
- never generated from the working label;
- cannot be reused for a materially different silhouette/anatomy;
- source ID is not automatically a runtime catalog/spawn ID;
- reuse by engineering requires an explicit #183 mapping.

### 6.2 Working label rules

Use fields equivalent to:

```text
workingReviewLabel
workingLabelStatus = "nonlocalized_review_only"
```

The label:

- may be readable English for review;
- is not a localization key;
- is not final player-facing copy;
- cannot appear in production UI merely because it exists in the packet;
- may be changed by Codex narrative/content plus user approval without changing source identity when the design meaning is unchanged.

### 6.3 Design-intent tag rules

Replace ambiguous runtime-sounding `realmOrBiomeEligibility` with a clearly non-authoritative shape such as:

```text
designIntentTags: ["biome:volcanic_lowlands", "biome:rocky_steppe"]
designIntentStatus: "source_design_only"
```

Tags:

- are not validated against current #183 catalogs;
- do not create spawn tables;
- do not bind a realm/faction;
- do not imply rarity, hostility, rewards, or progression;
- may inform later source/content discussions only.

### 6.4 Scale rules

Scale records include:

```text
referenceBasis
measurements
units
measurementStatus
```

- numeric values are finite and strictly positive;
- allowed fields are enumerated by schema;
- use one consistent Champion-relative unit definition;
- diagrams/sheets must visibly support claimed scale where scale is required for user approval;
- runtime meters remain an engineering mapping, not a hidden source assumption.

### 6.5 Required design-intent text

The following are nonblank for a technically complete profile:

- silhouette class;
- anatomy/proportion intent;
- at least two material/surface families;
- motion/temperament intent;
- rig/skeleton intent;
- required animation intent;
- LOD silhouette retention;
- collider intent;
- accessibility/non-color readability;
- reduced-motion behavior;
- explicit exclusions;
- unresolved decisions or explicit `none`.

## 7. Source asset contract

Every file in `assetPaths` becomes a normalized source-asset record.

Required equivalent fields:

```text
assetId
sourceVersion
profileIds
variantIds
role
path
repositoryStorage
mediaType
pixelWidth
pixelHeight
byteLength
sha256
gitLfsOid
gitLfsSize
unityAssetGuid
editableSourceAvailable
editableSourcePath
generationRecordId
promptSectionReference
licenseRecordId
reviewSurfaceUrlOrPrAnchor
status
```

### 7.1 Asset ID

Example:

```text
tdf_asset_basalt_grazer_concept_v001
```

- unique, stable, nonblank;
- changes only when the logical source asset changes;
- one asset may cover multiple views/variants only when exact coverage is declared.

### 7.2 Roles

Allowed roles include:

```text
concept_turnaround
silhouette_sheet
scale_chart
material_callout
variant_sheet
pose_motion_sheet
paintover
editable_source
export_preview
```

One file may have multiple roles, but the manifest records them explicitly.

### 7.3 Media identity

- `mediaType` is verified from file bytes;
- width/height are positive integers read from the image header;
- `byteLength` matches retrieved bytes;
- `sha256` is lower-case 64-character hex over retrieved file bytes;
- `gitLfsOid` is recorded without the `sha256:` prefix or in one normalized documented format;
- `gitLfsSize` equals the LFS pointer size and retrieved byte length;
- Git LFS OID and file SHA-256 are expected to represent the same binary hash for standard Git LFS SHA-256 storage and must match when so configured;
- `unityAssetGuid` is required only when the file remains under `unity/Assets`;
- any mismatch blocks technical completion.

### 7.4 Prompt/generation linkage

Every generated image links to:

```text
generationRecordId
promptSectionReference
```

A free-form prompt stored only in PR discussion is insufficient.

### 7.5 Editable source

Use explicit fields:

```text
editableSourceAvailable: true|false
editableSourcePath: string|null
editableSourceReason: string|null
```

Do not claim editable source availability merely because a PNG exists.

### 7.6 Review surface

Every required asset must be directly visible in the PR body or a stable PR comment through:

- an inline rendered image; and
- a full-resolution link or attachment that resolves to the retrieved binary.

The PR identifies profile, asset role, source version, dimensions, and SHA-256 next to the rendered sheet.

A repository `blob` page showing only an LFS pointer is not a sufficient creative-review surface.

## 8. Variant contract

Each variant includes equivalent:

```text
variantId
profileId
sourceVersion
status
variantKind
intent
sourceAssetIds
changedDesignDimensions
requiresSeparateEngineeringSource
userCreativeDecision
```

### 8.1 Variant ID rules

```text
^tdf_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$
```

- globally unique across the packet;
- belongs to exactly one profile;
- cannot duplicate a profile ID;
- cannot move to another profile in a later version without explicit supersession.

### 8.2 Variant status

```text
DeliveredReference
ProposedTextOnly
ReadyForUserReview
ChangesRequested
UserApproved
Rejected
Superseded
```

Current non-standard PR #217 variants must be `ProposedTextOnly` unless visual source is added.

### 8.3 Variant visual-source requirement

`ReadyForUserReview` requires at least one of:

- dedicated variant sheet;
- dedicated paintover;
- exact labeled swatch/material callout tied to a base sheet;
- exact region/overlay reference showing the change.

`UserApproved` additionally requires the user decision to identify the exact source version and asset hashes.

### 8.4 Engineering consumption

Engineering may consume only:

- a base profile source explicitly approved by the user; and
- variants with `UserApproved` and complete source assets.

Text-only variant intent cannot be converted into production textures/materials without returning to terrestrial-design mode and user review.

## 9. Generation and provenance contract

### 9.1 Generation record

Each generation record includes equivalent:

```text
generationRecordId
generatorProduct
generatorVersionOrModelWhenAvailable
generatedAtUtc
promptTextOrPromptPath
negativeConstraints
inputAssetIds
externalInputIds
editingSteps
outputAssetIds
operatorMode
```

Rules:

- prompt text is retained exactly or as a referenced immutable file section;
- “built-in image generation” is recorded as product/tool context;
- model/version is included when available and `unavailable` when not exposed;
- all post-generation edits, crops, compositing, paintover, resizing, and metadata stripping are listed;
- output links are complete;
- no hidden external input is omitted.

### 9.2 External input record

Every external input includes:

```text
externalInputId
kind
sourceUrlOrRepositoryPath
creatorOrPublisher
license
licenseEvidence
retrievedAtUtc
sha256
usage
```

An empty external-input list is valid only when generation and editing truly used no external visual/font/logo/source reference.

### 9.3 License/provenance wording

Acceptable technical statement:

```text
No third-party source images, fonts, logos, or named-IP references were supplied as generation/editing inputs for this source version. The generation tool and prompts are recorded. Final project-use approval remains with the user/project owner.
```

Do not claim:

- universal copyright ownership;
- freedom from all latent model-training concerns;
- legal clearance beyond the recorded facts;
- trademark clearance for final names that are not yet approved.

## 10. Git LFS transport proof

### 10.1 Repository tracking

Verify:

```text
git check-attr filter diff merge text -- <each media path>
git lfs ls-files --name-only
```

Each expected media path must be listed exactly once.

### 10.2 Local integrity

Run:

```text
git lfs fsck
```

Result must pass for every source asset.

### 10.3 Clean retrieval

Use one disposable clean clone or fresh worktree with no pre-existing LFS object cache assumption.

Equivalent sequence:

```text
git clone --no-local <repository> <temp-path>
cd <temp-path>
git checkout <exact-head-sha>
git lfs pull --include="unity/Assets/AL/Art/Terrestrials/**"
git lfs fsck
```

If credentials/private access require another reviewed method, document it. The proof must demonstrate downloaded binary bytes, not merely pointers.

### 10.4 Retrieved-file verification

For every retrieved source asset, recompute:

```text
byte length
SHA-256
pixel width
pixel height
media type
```

Compare to the manifest. Mismatch blocks the PR.

### 10.5 CI/review transport

When #155 supports source-asset checks, add a path-aware informational/required job that:

- fetches LFS;
- validates pointers/objects;
- runs the terrestrial manifest validator;
- emits a small review artifact or contact sheet when safe;
- never treats unavailable LFS as a pass.

Until then, retain exact local clean-retrieval evidence in the PR.

## 11. Manifest schema and semantic validator

### 11.1 Retained schema

Expected source path:

```text
unity/Docs/Terrestrials/terrestrial_source_packet.schema.json
```

or another explicitly declared source-contract path.

The schema covers:

- required top-level fields;
- enum values;
- ID patterns;
- nonempty arrays/strings;
- finite positive numeric scale values where JSON Schema support permits;
- source asset shape;
- variant state;
- readiness state;
- provenance/license shape.

### 11.2 Semantic validator

JSON Schema alone is insufficient. Add a deterministic validator that checks:

- unique profile IDs;
- globally unique variant IDs;
- unique asset/generation/external-input IDs;
- source-version consistency;
- every profile source asset reference resolves;
- every variant profile/asset reference resolves;
- every generation output resolves;
- every asset generation/license reference resolves;
- every manifest path exists and is unique;
- media header/hash/dimension/size match;
- LFS pointer/object match;
- variant status is compatible with source asset coverage;
- `TechnicalHandoffComplete` is impossible with missing required source or proposed-only required variants;
- runtime/narrative authority remains false;
- working labels and intent tags carry explicit non-authority status;
- no IDs are reused across retained source-version history without a migration/supersession record;
- no absolute/traversal path;
- deterministic diagnostic ordering.

### 11.3 Diagnostic contract

Equivalent fields:

```text
code
severity
sourceVersion
profileId
variantId
assetId
fieldPath
message
blocksTechnicalHandoff
```

Suggested stable codes:

```text
AL-TDF-MANIFEST-SCHEMA
AL-TDF-DUPLICATE-ID
AL-TDF-SOURCE-VERSION-MISMATCH
AL-TDF-MISSING-ASSET
AL-TDF-LFS-POINTER-ONLY
AL-TDF-LFS-OBJECT-MISSING
AL-TDF-HASH-MISMATCH
AL-TDF-DIMENSION-MISMATCH
AL-TDF-MEDIA-TYPE
AL-TDF-PROVENANCE-INCOMPLETE
AL-TDF-VARIANT-SOURCE-MISSING
AL-TDF-REVIEW-SURFACE-MISSING
AL-TDF-UNITY-IMPORT
AL-TDF-AUTHORITY-LEAK
AL-TDF-UNRESOLVED-DECISION
```

## 12. Unity asset-import boundary

Choose exactly one path for PR #217.

### 12.1 Path A — documentation/review media outside `Assets`

Move review-only media to equivalent:

```text
unity/Docs/Terrestrials/ConceptSheets/**
```

Requirements:

- remove Unity `.meta` files for moved media;
- update manifest paths/hashes/LFS records;
- retain direct PR render/full-resolution links;
- prove no Unity/runtime reference expected them under `Assets`;
- validate docs/LFS media normally.

Benefits:

- no Unity import overhead;
- no accidental runtime/package reference;
- no importer-settings authority;
- source packet remains clearly documentation/design source.

### 12.2 Path B — intentional Unity-imported source references

Keep media under:

```text
unity/Assets/AL/Art/Terrestrials/ConceptSheets/**
```

Requirements:

- document why Editor/Unity import is needed;
- define reviewed importer settings for source concepts;
- preserve stable `.meta` GUIDs;
- use loss-conscious source settings appropriate to concept reference;
- prove no scene, prefab, material, runtime catalog, Addressables group, Resources path, AssetBundle, or Player code references them;
- canonical Unity batch import succeeds after a clean LFS pull;
- no texture/import warnings or missing LFS pointer import;
- imported dimensions and GUIDs match expected records;
- a Player build under #150 does not package them unless a later approved reason exists.

### 12.3 Current metadata review

Current ordinary texture settings are not automatically accepted simply because Unity generated them. The PR must state whether mipmaps, compression, 2048 maximum size, readability, texture type, and platform overrides are intended for concept reference.

Do not change visual pixels through importer resize/compression and then use the imported preview as source authority. Source identity remains the retrieved original binary.

## 13. Direct user-review package

When technical corrections pass, PR #217 adds a section equivalent to:

```text
## User creative review — exact source version
Source version: tdf-2026-07-15-v001
Technical handoff disposition: COMPLETE
Runtime integration: BLOCKED
Narrative names: WORKING LABELS ONLY
```

For each profile, show:

- inline concept sheet;
- full-resolution source link;
- profile ID;
- working non-player-facing label;
- source asset ID;
- SHA-256;
- dimensions;
- silhouette/scale/material summary;
- accessibility/reduced-motion summary;
- variant status list;
- unresolved creative questions.

The user decision records one of:

```text
Approve exact source version
Approve selected profiles/variants only
Changes requested
Reject
Defer
```

Approval must identify profile/variant IDs and source version. Silence, PR merge, source presence, or a GPT review is not user approval.

## 14. Engineering handoff contract

After user approval, engineering receives an immutable approved-source snapshot:

```text
sourceVersion
approved profile IDs
approved variant IDs
asset IDs and SHA-256
working-label status
scale/silhouette/material/motion/LOD/accessibility intent
explicit exclusions
unresolved product/runtime decisions
```

### 14.1 Engineering may derive

Only through a separately scoped issue/PR:

- greybox/model briefs preserving approved silhouette and scale;
- mesh/rig/material task definitions;
- collider and VFX anchor transforms matching source intent;
- animation tasks matching approved motion intent;
- source-to-runtime mapping artifacts under #183;
- technical LOD/performance implementation within approved target budgets.

### 14.2 Engineering may not derive silently

- unapproved variants;
- different silhouette/anatomy/scale/material family;
- player-facing names/lore;
- biome/spawn/realm binding;
- AI, hostility, combat role, loot, rewards, interaction, collection, persistence, or rarity;
- mandatory glow/color-only state;
- performance targets not approved by the user/project;
- production assets from missing or text-only source.

### 14.3 Source-to-runtime mapping

Every runtime derivative records:

```text
runtimeAssetId
sourceVersion
sourceProfileId
sourceVariantId
sourceAssetIds
sourceAssetSha256
engineeringRevision
fidelityExceptions
```

A fidelity exception requires Codex terrestrial-design review and, when creatively material, user approval.

## 15. Relationship to #183

The merged game-data authority specification establishes that source-design IDs and tags do not become runtime catalog authority automatically.

A later terrestrial catalog entry must:

- use an explicit mapping from source profile/variant IDs;
- carry source version/hash/provenance;
- distinguish technical runtime ID from source-design identity when needed;
- retain user approval record;
- validate cross-references and packaging;
- remain immutable/pure at runtime;
- avoid player-facing working labels;
- avoid source-design biome tags as implicit spawn data.

PR #217 cannot add the runtime catalog itself.

## 16. Required file boundary for PR #217 correction

Expected source-mode corrections:

```text
PR body/comments with direct rendered review surface
unity/Docs/Terrestrials/README.md
unity/Docs/Terrestrials/Terrestrial_Design_Brief.md
unity/Docs/Terrestrials/Terrestrial_Engineering_Handoff.md
unity/Docs/Terrestrials/Source_Prompts_And_Provenance.md
unity/Docs/Terrestrials/terrestrial_profiles_manifest.json
unity/Docs/Terrestrials/terrestrial_source_packet.schema.json
small deterministic source-manifest validator/tests or script
concept-sheet media/meta paths according to chosen import boundary
variant visual sources when delivered
```

Permitted technical validation tooling must not become gameplay/runtime source.

Prohibited:

```text
runtime C# gameplay/AI/spawning/combat
scenes/prefabs/shaders/material production assets
save fields/services
Build Settings
Android runtime
player-facing narrative/localization source
balance/reward data
#183 runtime catalog
Bootloader/shared integration files
```

## 17. Required tests and evidence

### 17.1 Manifest schema

- valid current packet;
- missing required top-level field;
- unsupported schema version;
- duplicate profile ID;
- duplicate variant ID across profiles;
- duplicate asset/generation ID;
- invalid ID pattern;
- inconsistent source version;
- invalid readiness enum;
- runtime/narrative authority accidentally true;
- invalid path traversal/absolute path;
- deterministic error ordering.

### 17.2 References and readiness

- profile source asset missing;
- variant profile missing;
- variant asset missing;
- generation output missing;
- asset generation/license reference missing;
- proposed text-only variant accepted only as proposed;
- proposed variant cannot be `UserApproved`;
- handoff cannot complete with missing required turnaround/silhouette/scale/material source;
- unresolved product decisions keep runtime blocked;
- working labels/tags remain non-authoritative.

### 17.3 Media validation

For each delivered media file:

- file exists after clean LFS retrieval;
- bytes are not an LFS pointer text file;
- media signature is expected PNG or declared type;
- exact byte length;
- exact SHA-256;
- exact dimensions;
- no duplicate asset path;
- no duplicate incompatible hash record;
- source prompt/generation record resolves.

Failure fixtures:

- pointer without object;
- truncated PNG;
- wrong extension/media type;
- changed bytes with old hash;
- changed dimensions;
- wrong LFS OID/size.

### 17.4 LFS

- attributes match expected LFS macro;
- all expected files listed by `git lfs ls-files`;
- `git lfs fsck` passes;
- clean clone/worktree retrieves each object;
- recomputed bytes/hash/dimensions match manifest;
- no unreferenced source LFS objects are represented as delivered packet assets.

### 17.5 Review surface

- every required source asset appears inline in PR body/comment;
- full-resolution link resolves to binary image;
- source version/hash/dimensions shown;
- no sheet is substituted by a screenshot with unknown hash;
- all approved/proposed variant states shown honestly;
- GPT technical review statement explicitly disclaims creative approval.

### 17.6 Unity boundary

If Path A:

- no concept files remain under `Assets`;
- no related `.meta` files remain;
- no runtime references are broken;
- docs/LFS validation passes.

If Path B:

- clean LFS pull before Unity import;
- canonical Unity 2022.3.62f3 batch import passes;
- stable `.meta` GUIDs;
- exact importer settings validated;
- no missing/import-error log;
- no scene/prefab/runtime/Addressables/Resources/AssetBundle reference;
- Player packaging excludes source concepts unless later approved.

### 17.7 Scope

- no runtime C#/Kotlin/scene/prefab/save/build-setting change;
- no gameplay catalog;
- no player-facing narrative/lore;
- no shared-file lock;
- `git diff --check origin/main...HEAD` passes;
- final branch contains only declared source/validation files.

## 18. Canonical validation commands

From repository root:

```powershell
# JSON syntax
Get-Content -Raw 'unity/Docs/Terrestrials/terrestrial_profiles_manifest.json' | ConvertFrom-Json | Out-Null
Get-Content -Raw 'unity/Docs/Terrestrials/terrestrial_source_packet.schema.json' | ConvertFrom-Json | Out-Null

# Retained validator; exact script path may vary
./tools/ci/Invoke-TerrestrialSourceValidation.ps1 `
  -Manifest 'unity/Docs/Terrestrials/terrestrial_profiles_manifest.json' `
  -Schema 'unity/Docs/Terrestrials/terrestrial_source_packet.schema.json'

# LFS
& git lfs ls-files
& git lfs fsck

# Repository diff
& git diff --check origin/main...HEAD
& git status --short --branch
```

Clean retrieval must use a separate temporary path and the exact head SHA.

If assets remain under Unity `Assets`:

```powershell
$repo = "D:\260711\MY\AndroidStudioProjects\AnotherLife"
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"

& $unity -batchmode -quit -nographics `
  -projectPath "$repo\unity" `
  -logFile "$repo\unity\Logs\TerrestrialSourceImport.log"
```

Report Unity exit code, final markers, import/missing-asset scan, GUID inventory, and no-runtime-reference evidence.

## 19. Technical handoff disposition

GPT may use:

```text
BLOCKED
TECHNICAL CHANGES REQUIRED
TECHNICAL HANDOFF COMPLETE / READY FOR USER CREATIVE REVIEW
```

A complete comment identifies:

- exact head SHA;
- source version;
- profile/variant/asset counts by state;
- manifest/schema/semantic validation result;
- LFS clean-retrieval result;
- image hashes/dimensions;
- Unity import disposition;
- direct rendered review surface;
- remaining unresolved creative/product decisions;
- explicit statement that creative approval is not granted by GPT.

## 20. Acceptance criteria

- [ ] PR #217 uses `Refs #194` and keeps #194 open.
- [ ] The branch is rebased onto current `main` with exact final evidence.
- [ ] A retained schema and semantic validator cover all manifest invariants.
- [ ] Every profile, variant, asset, generation record, and source version has unique deterministic identity.
- [ ] Every media asset records media type, dimensions, byte length, SHA-256, LFS OID/size, source version, generation record, and review link.
- [ ] Clean LFS retrieval and `git lfs fsck` pass.
- [ ] Actual rendered pixels are directly available for user review.
- [ ] Working labels and biome/realm tags are explicitly non-player-facing and non-runtime.
- [ ] Text-only variants are proposed/pending, not represented as approved or engineering-ready.
- [ ] Every user-review-ready variant has visual source.
- [ ] Provenance, generation steps, external inputs, and license evidence are complete and truthful.
- [ ] The packet exposes separate technical, user-creative, narrative-naming, and runtime-integration states.
- [ ] Review-only media is either moved outside `Assets` or intentionally imported with canonical Unity evidence and no runtime/package references.
- [ ] GPT technical review does not claim creative approval.
- [ ] User approval identifies the exact source version and approved profile/variant IDs.
- [ ] Engineering consumption remains a later separate PR with immutable source-to-runtime mapping and fidelity review.
- [ ] No gameplay, AI, spawn, combat, reward, save, narrative, balance, Android, scene, prefab, shader, runtime catalog, or shared-file change is included.

## 21. Codex handoff

```text
Codex terrestrial-design: correct PR #217 from current main using unity/Docs/Terrestrial_Source_Packet_Validation_Spec.md. Keep all creative authorship in terrestrial-design mode. Change `Fixes #194` to `Refs #194`; add the retained manifest schema/validator; record immutable media/LFS/provenance identity; prove clean LFS retrieval; embed direct full-resolution user-review images; classify undelivered variants as proposed or add exact visual source; clarify working labels and biome tags as non-authoritative; and either move review media outside Unity Assets or supply canonical Unity import/no-runtime-reference evidence. Do not add runtime, gameplay, AI, spawn, combat, reward, save, narrative, balance, Android, scene, prefab, shader, or #183 catalog work. Return the exact corrected head for GPT technical review; only after `TECHNICAL HANDOFF COMPLETE` should the user be asked for creative approval.
```
