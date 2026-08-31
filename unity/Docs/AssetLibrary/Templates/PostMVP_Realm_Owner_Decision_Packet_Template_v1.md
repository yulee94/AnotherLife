# Post-MVP Realm Owner Decision Packet Template v1

**Packet ID:** `rct_<scope>_decision_<slug>_v001`

**Catalog ID:** `rct_<realm>_catalog_<slug>_v001`

**Owner status:** `PENDING`

**Final decision authority:** Project owner

**Generation state:** `HELD`

**Activation state:** `HELD`

Use this packet for unresolved morphology, culture, silhouette, anatomy, clothing, armor, animation personality, or magical grammar. Do not create a default answer. Keep generation and downstream production held until the owner records `APPROVE`, `REVISE`, or `REJECT`.

## 1. Decision identity

| Field | Required value |
| --- | --- |
| Packet stable ID | `rct_<scope>_decision_<slug>_vNNN` |
| Realm | `stonehold`, `eldergrove`, `crownlands`, or `umbral` |
| Subject stable IDs | Every catalog record controlled by this decision |
| Decision dimensions | One or more of the eight protected dimensions |
| Accountable implementers | Concept, modeling, rigging, animation, VFX, gameplay, audio, accessibility, performance, runtime, and/or QA |
| Date opened (UTC) | ISO 8601 |
| Requested-by reference | Task, issue, source packet, or catalog path |

## 2. Decision question

Write one neutral question the owner can answer without inferring implementation details.

**Question:** `OPEN`

**Why a decision is required:** `OPEN`

**What is already approved and cannot change in this packet:**

- `OPEN`

**What remains explicitly undecided:**

- `OPEN`

## 3. Source and provenance

List every source used to frame the alternatives. Benchmarks are directional evidence only and never approval authority.

| Provenance ID | Source kind | Path / URL / catalog ID | Creator | Tool and version | Date | Rights/license state | Prompt/brief reference | SHA-256 | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `rct_<scope>_provenance_<slug>_v001` | `repo_document` / `runtime_catalog` / `human_authored` / `ai_assisted` / `ai_generated` / `external_reference` | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `project_internal` / `cleared` / `restricted` / `unknown` / `rejected` | `OPEN` or `null` | `OPEN` or `null` | `OPEN` |

Required checks:

- [ ] Creator and authoring route are known.
- [ ] Tool/model/version is recorded for AI-assisted/generated work.
- [ ] Prompt or brief is retained where applicable.
- [ ] Rights and license state are explicit.
- [ ] Suspicious or copied work is excluded rather than repaired.
- [ ] Sources do not silently establish gameplay, lore, statistics, or release authority.

## 4. Alternatives

Provide at least two meaningfully different alternatives. Do not use a fake alternative whose only purpose is to make another option look preferable. An option to retain the hold is valid when evidence is insufficient.

### Alternative `<alternative_id_a>`

**Summary:** `OPEN`

**Approved facts preserved:**

- `OPEN`

**Proposed choices:**

- `OPEN`

**Evidence references:**

- `OPEN`

**Production implications:**

- Modeling: `OPEN`
- Rigging: `OPEN`
- Animation: `OPEN`
- VFX: `OPEN`
- Gameplay/readability: `OPEN`
- Accessibility: `OPEN`
- Mobile performance/memory: `OPEN`

**Risks and reversibility:**

- `OPEN`

### Alternative `<alternative_id_b>`

**Summary:** `OPEN`

**Approved facts preserved:**

- `OPEN`

**Proposed choices:**

- `OPEN`

**Evidence references:**

- `OPEN`

**Production implications:**

- Modeling: `OPEN`
- Rigging: `OPEN`
- Animation: `OPEN`
- VFX: `OPEN`
- Gameplay/readability: `OPEN`
- Accessibility: `OPEN`
- Mobile performance/memory: `OPEN`

**Risks and reversibility:**

- `OPEN`

### Alternative `retain_hold`

**Summary:** Keep the affected records at `owner_decision_required`; do not generate or activate content.

**Use when:** evidence, provenance, budget measurement, or creative direction is insufficient.

**Downstream consequence:** affected work remains blocked without implying rejection of the category.

## 5. Cross-discipline impact matrix

| Discipline | Affected IDs/files | Impact if A | Impact if B | Impact if hold | Owner-sensitive follow-up |
| --- | --- | --- | --- | --- | --- |
| Concept | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `OPEN` |
| Modeling | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `OPEN` |
| Rigging | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `OPEN` |
| Animation | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `OPEN` |
| VFX | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `OPEN` |
| Gameplay | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `OPEN` |
| Audio | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `OPEN` |
| Accessibility | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `OPEN` |
| Performance | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `OPEN` |
| Runtime | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `OPEN` |
| QA | `OPEN` | `OPEN` | `OPEN` | `OPEN` | `OPEN` |

## 6. Budget and platform impact

Do not enter a proposed number as an approved limit.

| Platform profile ID | Budget profile ID | Metric groups affected | Existing source value/state | Proposed change | Measurement needed | Admission impact |
| --- | --- | --- | --- | --- | --- | --- |
| `OPEN` | `OPEN` | geometry/materials/textures/bones/physics/animation/VFX/colliders/hitboxes | `OPEN` | `OPEN` | `OPEN` | `OPEN` |

Protected cues that must survive every option and quality tier:

- timing and committed result;
- target, danger, objective, ownership, and interaction state;
- face/focal region, weapon/attack origin, silhouette, realm cue, and threat cue;
- gameplay collider/hitbox authority;
- reduced-motion, non-color, and VFX-off-state cues.

## 7. Motion, skill, and VFX impact

| Subject/skill ID | Required motion phases affected | Effect categories affected | Timing/result authority reference | Traceability rows to update | Validation impact |
| --- | --- | --- | --- | --- | --- |
| `OPEN` | anticipation/cast/channel/release/recovery | telegraph/cast/channel/release/trail/projectile/impact/area/buff/debuff/status/environmental/result/cleanup | `OPEN` | `OPEN` | `OPEN` |

The decision may choose presentation; it may not redefine authoritative gameplay timing or results.

## 8. Recommendation

The implementer may recommend one alternative, but must state the reason and uncertainty. The owner is not required to accept it.

**Recommended alternative:** `OPEN` or `NONE`

**Reason:** `OPEN`

**Uncertainty / missing evidence:** `OPEN`

## 9. Owner ruling

Select exactly one ruling.

### `APPROVE`

- Approved alternative ID: `OPEN`
- Owner response: `OPEN`
- Approved exceptions: `NONE` or `OPEN`
- Decision time (UTC): `OPEN`
- Approval evidence reference: `OPEN`

Effect: the selected facts may be promoted to `approved_fact` only after the catalog is updated, schema/semantic validation passes, and any required measurements/evidence are attached. Approval of this packet does not automatically authorize runtime release.

### `REVISE`

- Required revision: `OPEN`
- Alternatives/evidence to add or remove: `OPEN`
- Decision time (UTC): `OPEN`
- Owner response reference: `OPEN`

Effect: keep record status `proposal` or `owner_decision_required`, owner status `REVISE`, generation held, and activation held.

### `REJECT`

- Rejected subject/alternative IDs: `OPEN`
- Reason: `OPEN`
- Decision time (UTC): `OPEN`
- Owner response reference: `OPEN`

Effect: mark the rejected record/alternative `rejected`, preserve provenance and decision history, and do not reuse its ID for a different meaning.

### `PENDING`

Initial state only. `approvedAlternativeId`, owner response, and decision time remain null. Generation and activation remain held.

## 10. Post-decision update checklist

- [ ] Owner ruling is copied verbatim into the catalog decision packet.
- [ ] Approved alternative ID resolves and exists in this packet.
- [ ] Every controlled record references this packet.
- [ ] Record authority states match the ruling.
- [ ] Provenance and approval evidence resolve.
- [ ] Platform and budget profiles are updated without inventing values.
- [ ] Motion templates and subject motion coverage still pass.
- [ ] Every affected skill trace row still covers all five phases and fourteen effect categories.
- [ ] Reduced-motion, low-quality, non-color, and off-state cues remain explicit.
- [ ] `realm_character_taxonomy.py` passes.
- [ ] Physical-device/performance evidence is attached when the ruling changes a budget.
- [ ] Generation remains held until all generation gates pass.
- [ ] Runtime activation remains held until the separate release gate passes.
