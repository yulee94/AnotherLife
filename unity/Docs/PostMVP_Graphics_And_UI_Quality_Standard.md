# AnotherLife Post-MVP Graphics and UI Quality Standard

**Status:** Active owner-approved post-MVP quality supplement

**Decision date:** 2026-08-25

**Canonical style authority:** [`DESIGN.md`](../../DESIGN.md)

**Measurement companion:** [`Benchmarks/PostMVP_Graphics_Benchmark_Spec_2026-08-25.md`](Benchmarks/PostMVP_Graphics_Benchmark_Spec_2026-08-25.md)

## 1. Authority and scope

This document records the project owner's post-MVP graphics, animation, UI/HUD,
accessibility, performance, and approval decisions. It refines how the canonical
`DESIGN.md` style is judged after MVP; it does not replace its realm identities,
asset-packet rules, provenance requirements, or approval precedence.

When sources disagree, use this order:

1. The project owner's latest explicit decision.
2. An approved asset-specific packet or source sheet.
3. `DESIGN.md`.
4. This post-MVP quality supplement.
5. The benchmark specification and test evidence.
6. MVP assets, gap lists, prototypes, and exploratory references.

This supplement supersedes MVP gap lists only as a statement of the final target.
MVP assets remain evidence that systems work; they are not a permanent quality floor.
Proven systems, catalogs, save contracts, and data authority must be preserved when
visual presentation is replaced.

## 2. Product-level visual promise

AnotherLife targets **premium AA dark high-fantasy stylized realism**, with selected
near-AAA hero moments. The canonical style remains **mystical medieval naturalism**:
believable anatomy and construction, grounded physical materials, structural realm
identity, controlled magical phenomena, and selective silhouette exaggeration for
readability.

The first hour should leave three visual memories:

- a serious and unmistakable fantasy world;
- beautiful landscapes and composed long views;
- grand fortresses and cities that imply history, power, and inhabitable scale.

The product must not look cartoonish, toy-like, generic, procedurally scattered,
visibly low-budget, or dependent on glow and particle count for quality.

## 3. Audience and mature presentation

The visual language is for a broad adult audience, approximately ages 20–60.
Sensuality, body diversity, mature themes, danger, damage, and adult-fantasy
presentation are permitted when they support character, culture, or story.
Presentation must not become excessively sexualized, sexually exploitative,
gratuitously gory, or dependent on shock value.

Armor, clothing, physique, exposed skin, aging, scarring, and body variation must be
intentional character choices. They must preserve believable function, motion,
cultural identity, and equal production finish.

## 4. Quality allocation

### 4.1 Near-AAA hero surfaces

The limited near-AAA effort is reserved for:

1. character creator and class reveal;
2. first capital arrival;
3. realm transitions;
4. major bosses and milestone cinematics.

These surfaces require close-camera material fidelity, authored lighting, final
animation, deliberate composition, polished transitions, final UI motion, and a
representative device capture before owner review.

### 4.2 Premium-AA continuous surfaces

All other shipped player-facing content targets cohesive premium-AA finish. It may use
more modular production, shared rigs, trim sheets, material families, VFX grammars,
and procedural placement, but reuse must read as cultural coherence rather than
obvious repetition.

When production capacity is insufficient:

- reduce breadth before finish;
- reuse modular content before creating visibly cheaper content;
- simplify hidden implementation before simplifying the player-facing read;
- delay a surface from the review package rather than presenting unfinished work as
  final quality.

No weighted average may allow excellent hero content to conceal an unfinished ordinary
surface.

## 5. Characters and creator

The creator targets BDO-inspired fidelity and meaningful customization depth translated
into AnotherLife's original stylized realism. It must include:

- a live, correctly lit 3D preview with orbit, zoom, reset, and safe framing;
- clear class and realm identity before fine editing;
- skin, hair, eye, facial, body, age, and presentation controls sufficient for broad
  adult identity and body diversity;
- richer sliders or direct manipulation where they add meaningful visible range;
- undo/redo, reset, randomize, preview poses, and reliable save/load feedback;
- preview lighting or backgrounds that reveal material and color choices rather than
  hiding them;
- subtle background motion that supports the subject without competing with it;
- realm-glyph transitions in which rocks, leaves, dark matter, gold powder, or other
  realm-authentic material flow inward and form the elemental surface, rather than a
  generic glistening overlay.

Characters must hold up in inspection, dialogue, gameplay, and icon views. Skin, hair,
eyes, cloth, leather, metal, and magic require materially distinct response. Sensuality
must never substitute for anatomy, clothing construction, or class identity.

## 6. World, architecture, and landscapes

World production uses modular, upgrade-friendly settlement kits with authored
landmarks, deliberate street and approach composition, and carefully staged traversal
views.

Every city, fortress, and settlement must provide:

- a dominant silhouette visible from its intended approach;
- one or more authored arrival compositions;
- readable navigation landmarks at gameplay distance;
- believable structural hierarchy, defenses, services, and circulation;
- material and construction logic belonging to its realm;
- population, movement, banners, weather, sound direction, and environmental response
  sufficient to avoid a static set-piece read;
- graceful streaming and LOD transitions that do not erase identity.

Private kingdoms remain organized and upgrade-friendly. Duplicate building types may
appear where center level and gameplay permit, but structures must not be randomly
scattered. Upgrade states must preserve building identity while visibly communicating
progress.

Beautiful vistas and grand scale do not excuse empty traversal. Composition, density,
interaction, streaming, and destination rhythm must support the intended journey.

## 7. Lighting and materials

Use grounded PBR materials plus art-directed color, selective magical exaggeration,
and luminous realm phenomena.

Required rules:

- material response remains physically distinguishable before emissive effects;
- lighting establishes form, navigation, threat, and focal hierarchy before mood;
- magical light has a source, path, and consequence;
- realm identity is structural and material, never a palette swap;
- emission remains selective enough to preserve dark adaptation, silhouettes, skin,
  fabric, and surface detail;
- every critical scene is reviewed in its supported time, weather, and accessibility
  states;
- mobile quality tiers may reduce secondary lighting, shadow reach, reflections,
  particles, and distant detail, but not gameplay truth or realm identity.

## 8. Animation, camera, and VFX

### 8.1 Animation

Locomotion and combat target responsive AA quality throughout. Class reveals, major
bosses, realm transitions, and milestone cinematics receive near-AAA animation effort.

Animation approval requires believable weight, contact, anticipation, recovery,
interrupt behavior, camera agreement, and transition quality. Static beauty renders do
not approve a rigged runtime subject.

### 8.2 Camera

Exploration framing protects vistas, stable navigation, and character visibility.
Combat framing protects the target, hostile telegraphs, objectives, and the local threat
field. Camera shake, correction, motion, and recentering must be reducible or disabled.

### 8.3 VFX

Gameplay clarity always wins over spectacle. Effects must preserve:

1. the player silhouette and legal action state;
2. the current target and actionable cast state;
3. hostile telegraphs and damage direction;
4. critical party/support fields;
5. objective ownership, progress, and route information.

High-tier VFX add authored shape, timing, material interaction, and environmental
response—not merely more particles, opacity, bloom, or screen coverage. No quality mode
may remove an actionable threat without a truthful fallback.

## 9. UI design language

UI and HUD use **restrained dark-fantasy luxury**:

- materially rich dark foundations;
- subtle precious-metal accents;
- tactile stone, metal, leather, parchment, glass, or magical surfaces where functional;
- luminous realm glyphs and controlled realm-color accents;
- an original commissioned display/text family with localization coverage;
- restrained ornament concentrated around hierarchy and state;
- motion that communicates transition, causality, and focus.

Avoid flat debug plates, generic mobile gradients, fantasy ornament on every edge,
constant glow, tiny text, icon-only ambiguity, red-dot clutter, and monetization-first
visual hierarchy.

## 10. HUD and PvP information contract

AnotherLife uses a **fixed designer-authored HUD with minimal general customization**.
The default must be good enough that players do not need to repair it.

Mandatory accessibility and platform settings are not optional customization and must
remain available.

During PvP, the HUD must inform without hiding play. Protect the central combat scan
path and keep necessary information at predictable edges. Required layers are:

- player vitals, resources, control state, and immediate actions;
- target, cast, defense, break, and actionable status;
- hostile telegraphs, damage direction, and crowd-control state;
- party/squad health, revive, and role-critical support state;
- objective owner, contest state, progress, timer, and route;
- realm/alliance identity and commander markers;
- secondary rewards, logs, ambient notices, and decorative feedback.

Lower layers collapse or aggregate before higher layers. Damage numbers, cosmetic VFX,
ambient notifications, and decorative chrome must never obscure hostile telegraphs,
target state, or objectives.

## 11. Phone, tablet, and PC composition

Phone, tablet, and PC use purpose-built layouts sharing one component library, token
system, icon grammar, and state model. They are not one layout merely scaled up or down.

- **Phone:** thumb reach, safe areas, large touch targets, central-world visibility,
  condensed secondary information, and landscape aspect-ratio extremes.
- **Tablet:** expanded map, party, and management space without desktop-sized text or
  excessive empty panels.
- **PC:** keyboard/mouse and controller navigation, expanded information where it aids
  decisions, stable focus, and supported ultrawide behavior.

Input method must never change the authoritative information shown or create a
competitive information advantage.

## 12. Minimap and world map

Maps target BDO-inspired information richness through original AnotherLife visual
language. Use progressive disclosure rather than permanent clutter.

Required capabilities include:

- readable topology, elevation/route cues, realm boundaries, and major landmarks;
- progressive filters and zoom levels;
- clear self, party, squad, objective, threat, route, and selected-marker hierarchy;
- color-independent allegiance and contest states;
- predictable pan, zoom, recenter, filter, and selection behavior;
- PvP states that remain understandable without opening unrelated panels;
- world-map and minimap agreement on identity, route, and authoritative state.

## 13. Kingdom-management presentation

Infinity Kingdom is a directional reference for management density and visible city
progress, not for art style, layout, monetization, heroes, or branded interaction.
AnotherLife's kingdom surface remains subordinate to the authoritative
[private-kingdom state architecture](Private_Kingdom_Save_And_State_Synchronization_Architecture.md)
and [cross-mode navigation contract](Cross_Mode_Menu_Kingdom_Navigation_Contract.md).
It requires:

- an attractive city view with at least 60% of the safe-area viewport unobstructed
  during normal navigation and placement;
- compact but readable resource, queue, state, and module surfaces;
- the canonical bottom-center construction dock, right-side selected-building
  inspector, actual bounded queue capacity, and private-grid-only minimap;
- contextual building actions and truthful upgrade consequences;
- accepted server receipts as the source for scaffolding, progress, completion, and
  rollback presentation;
- organized upgrade-friendly placement and landmark hierarchy;
- progressive icon and notification density;
- visible city life and construction response;
- collapsed combat HUD after the cross-mode transition settles, retaining only
  essential shared status;
- equivalent clarity on phone, tablet, and PC.

## 14. Accessibility release gate

A visual surface cannot pass final review without:

- scalable UI and text;
- safe-area and aspect-ratio support;
- readable contrast and non-color-only signals;
- reduced motion, shake, flashes, and nonessential VFX;
- captions/subtitles and audio-off semantic parity;
- remappable controls and touch/controller/keyboard navigation;
- focus order and restoration;
- no loss of gameplay truth in any accessibility mode.

Accessibility changes may alter presentation but must preserve the intended hierarchy,
identity, and premium finish.

## 15. Approval model

Final quality uses two sequential gates:

1. **Objective evidence gate:** performance, frame pacing, memory, thermal behavior,
   streaming, LOD transitions, readability, accessibility, provenance, and originality
   all pass the benchmark specification.
2. **Owner creative gate:** the project owner issues `APPROVE`, `REVISE`, or `REJECT`.

3D and 2.5D are approved separately. A technical pass, comparator score, blinded test,
or approval in one mode cannot override an owner revision or approve the other mode.

## 16. Initial production sequence

Before broad post-MVP asset production:

1. maintain this supplement and the canonical `DESIGN.md` relationship;
2. establish the benchmark capture and evidence pipeline;
3. establish the representative device matrix;
4. build the five golden benchmark scenes;
5. validate objective gates;
6. obtain separate owner approval for 3D and 2.5D;
7. scale production one realm slice at a time: Stonehold, Eldergrove, Crownlands, Umbral.
