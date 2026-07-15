# Repository Quality Gate Gemini Addendum

**Status date:** 2026-07-15  
**Policy owner:** GPT  
**Tracking issues:** #155 and #194  
**Supersedes:** conflicting workstream-owner and terrestrial-design classifications in `Repository_Quality_Gate_Policy.md`

This addendum extends the merged repository quality-gate policy after the user assigned all terrestrial creature/fauna visual-design work to Gemini.

The original policy remains authoritative except where this addendum explicitly changes it. Codex must implement Phase A using both documents.

## 1. Primary workstream owners

The allowed primary workstream owners are now:

```text
GPT
Android Studio
Gemini
Codex
```

Exactly one primary owner remains required per pull request.

- A Gemini design PR is primarily Gemini-owned.
- A later Unity integration PR consuming the approved design is primarily Codex-owned and requires Gemini/user fidelity disposition.
- GPT coordination/specification and Android Studio narrative reviews do not change the primary PR owner.

## 2. Required ownership declaration

`policy / classify` must require these declarations:

```text
Narrative content changed: yes / no
Terrestrial visual design changed: yes / no
Runtime gameplay code changed: yes / no
Shared contracts or catalogs changed: yes / no
Save data or migration behavior changed: yes / no
Unrelated cleanup included: no
```

A missing terrestrial declaration fails classification.

## 3. Terrestrial-owned paths

The machine-readable policy must support a `terrestrial_design` ownership group. Initial paths are:

```text
unity/Docs/Terrestrials/**
unity/Assets/AL/Art/Terrestrials/**
unity/Docs/Gemini_Terrestrial_Design_Prompt.md
```

Issue-specific Gemini PRs may declare another focused design-source path. Such a path is treated as Gemini-owned after GPT records the decision in the issue and PR.

A path alone does not transfer narrative or runtime ownership:

- names, lore, descriptions, and story use remain Android Studio-owned;
- C# runtime, prefabs, spawning, animation hookup, LOD systems, pooling, and performance integration remain Codex-owned after design approval.

## 4. Classification failures

`policy / classify` must fail when:

- a Codex PR authors or substantially redesigns terrestrial concepts, anatomy, silhouettes, proportions, materials, color language, variants, or motion references without an approved Gemini package;
- a Gemini design PR changes Unity C# runtime, gameplay, save data, scenes, prefabs, Build Settings, technical catalogs, or Android narrative files;
- a terrestrial PR does not link #194 or an explicitly approved successor issue;
- a Codex integration PR does not identify the approved Gemini package, stable design IDs, source version, and intended fidelity boundary;
- a terrestrial design PR lacks source/provenance and licensing declarations;
- a PR mixes original terrestrial design and runtime integration as one primary completion without explicit user/GPT authorization;
- a PR treats closed #162 as approved design authority or cherry-picks it as the final terrestrial source;
- a PR uses `codex/<scope>` for original terrestrial design or `gemini/<scope>` for runtime implementation.

## 5. Required reports

`repository / hygiene` and `policy / classify` must report:

- all Gemini terrestrial-owned files changed;
- all terrestrial design-source, preview, manifest, and provenance files changed;
- whether external source/license records are present;
- whether a Codex integration references an approved Gemini source version;
- whether narrative-owned names/lore or runtime-owned files are mixed into the same diff.

These reports are review inputs. A report does not make an ownership violation acceptable.

## 6. Design artifact hygiene

The repository hygiene policy for terrestrial assets must reject:

- font files unless a separately reviewed repository policy explicitly permits and licenses them;
- unlicensed or unattributed external images/models/textures;
- opaque design archives with no source/provenance record;
- machine-local absolute paths embedded in manifests;
- temporary generation outputs, caches, thumbnails, or tool autosaves not intended as reviewed source;
- duplicate stable terrestrial profile or variant IDs;
- manifest paths that do not resolve to tracked files;
- source files whose license prohibits repository use or downstream game integration.

Large binary-source policy and Git LFS usage must be decided before committing files that exceed ordinary Git review limits. Do not silently commit oversized binaries merely because GitHub accepts them.

## 7. Manual review gates

### Gemini disposition required

Gemini fidelity review is required when:

- a PR adds or changes terrestrial concepts or design sources;
- a Codex PR imports, rigs, animates, shades, scales, simplifies, LODs, or otherwise presents an approved terrestrial design;
- technical constraints require an aesthetic deviation from the approved package.

The disposition must identify the design package/version and use one of:

```text
BLOCKED — DESIGN MISMATCH
READY FOR USER CREATIVE REVIEW
INTEGRATION FIDELITY ACCEPTED
```

### Android Studio disposition required

Android Studio review is required when a terrestrial PR adds or changes:

- species/creature names;
- lore or realm/cultural meaning;
- descriptions or localization-facing copy;
- narrative encounters or story behavior.

### GPT disposition required

GPT review is required for:

- design-manifest completeness and stable IDs;
- provenance and licensing completeness;
- technical handoff assumptions;
- mixed design/runtime/narrative scope;
- later runtime integration specifications and acceptance tests.

### User disposition required

User creative approval is required before the first Codex integration of a Gemini terrestrial package and after any material aesthetic deviation.

## 8. Same-identity limitation

The original review-count limitation now applies to GPT, Android Studio, Gemini, Codex, and the user when they act through the same GitHub identity.

Do not claim independent GitHub approval from self-authored comments or labels. Until separate trusted identities or Apps exist, retain explicit manual disposition comments tied to the current head SHA and require the available automated checks.

## 9. Validation matrix

| PR type | Required design checks | Required technical checks |
| --- | --- | --- |
| Gemini design-only documentation/source | manifest, IDs, path resolution, provenance/license, declared views/scale/motion/LOD/accessibility | repository hygiene; Android baseline under the universal policy; no Unity product validation claim |
| Gemini-generated preview only | same as design-only plus deterministic source/output mapping where applicable | repository hygiene; file-size/source policy |
| Codex terrestrial import/integration | approved design package/version, Gemini fidelity, stable ID mapping | Unity compile, EditMode, PlayMode after #127, Player/mobile after #150, performance and asset/import evidence |
| Android Studio terrestrial lore/naming | approved design IDs and Gemini visual reference | narrative validation, Android checks when Android source changes |
| Documentation-only ownership change | links and path consistency | universal repository/Android gate when Phase A is active |

A design-only PR must not cite a Unity compile as proof of design quality. A Codex integration PR must not cite visual screenshots alone as proof of runtime safety or performance.

## 10. Additional proof PRs for #155

Add these fixtures to the quality-gate proof matrix:

1. valid Gemini design-only PR classified correctly;
2. Codex PR modifying a Gemini-owned design path without an approved package fails;
3. Gemini PR modifying C# runtime or Build Settings fails;
4. terrestrial design PR missing provenance/license record fails;
5. Codex integration PR missing Gemini package/version reference fails;
6. terrestrial manifest with duplicate IDs or missing asset path fails.

Failure fixtures remain unmerged.

## 11. Implementation handoff

```text
Codex: when implementing Phase A of #155, read both Repository_Quality_Gate_Policy.md and Repository_Quality_Gate_Gemini_Addendum.md. Add Gemini as a primary workstream, require the terrestrial-design declaration, classify the approved terrestrial paths, and implement the ownership/provenance/manifest fixtures. Do not implement terrestrial artwork or runtime integration as part of the CI policy PR.
```

## 12. Relationship to active work

- Issue #194 is the active Gemini terrestrial-design lane.
- Closed PR #162 is reference-only and not approved authority.
- Closed issue #187 now covers non-terrestrial deferred visuals only.
- Current Phase 0/1 recovery, A1, G1, and save/runtime gates remain unchanged.
- Codex retains all non-terrestrial runtime/VFX work and later technical integration of approved Gemini assets.