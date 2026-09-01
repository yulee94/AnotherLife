# Another Life — Visual and Model Style Guide

**Status:** Active design contract
**Version:** 1.29
**Last updated:** 2026-08-13
**Primary owner:** Project owner / creative director
**Applies to:** Human artists, designers, engineers, contractors, and AI-assisted tools producing visual work for Another Life

## Source of truth

This document is the canonical cross-project visual and model-production contract for Another Life. It defines the shared style, the minimum information every visual proposal must contain, and the checks required before an asset can be treated as production-ready.

Use this precedence order when sources disagree:

1. The project owner's latest explicit decision.
2. An approved, asset-specific design packet or source sheet.
3. This global style guide.
4. Prototype assets, implementation details, benchmarks, and exploratory images.

An approved asset-specific packet may refine this guide for one asset, but it must identify every intentional exception. Prototype code and generated blockouts demonstrate mechanics or composition only; they do not silently establish final art direction.

AI-generated or externally sourced work is always a proposal until a human approves it. It must not invent or lock gameplay rules, statistics, narrative canon, names, monetization tiers, or realm identity. Keep the prompt, tool/model name, generation date, source references, license or provenance, and human review decision with the asset.

### Core style lock

Another Life is:

- **Mystical medieval naturalism:** adult, realistic high fantasy with dark foundations, luminous wonder, and a premium illustrated finish.
- **Medieval in craft and worldview**, with materials, construction, heraldry, ornament, and ritual that imply age, labor, inheritance, and history.
- **Grounded in believable anatomy, construction, weight, and physical materials.**
- **Mystical through controlled phenomena**, such as biologically integrated luminescence, purposeful runes, celestial energy, mineral seams, sacred geometry, or environmental transformation.
- **Art-directed for gameplay**, using selective silhouette and proportion exaggeration where it improves recognition.
- **Premium, artistic, and serious**, without becoming visually muddy, relentlessly bleak, or dependent on gore.

Use **mystical medieval naturalism** as the canonical shorthand for the project style. The approved terrestrial creatures establish the standard for believable bodies, ecology, silhouette, and integrated magic. The approved app icon establishes the standard for medieval mysticism, material richness, focal hierarchy, and presentation finish. New work should sit within the shared range of those references rather than copying one reference literally.

Another Life is not:

- Cartoon, chibi, toy-like, cute-by-default, or mobile low-poly as a final presentation.
- Photorealism pursued at the cost of readable silhouettes, broad device support, or art direction.
- Generic AI-fantasy collage, random ornament, or borrowed visual language from a recognizable franchise.
- A palette-swap system in which color is the only meaningful difference between realms, ranks, enemies, or states.

Changing the Core style lock, realm identities, or approval precedence is a critical design-direction change and requires explicit project-owner approval.

### Evidence and supporting direction

This guide consolidates the active project direction in:

- [Product Direction](unity/Docs/Product_Direction.md)
- [Champion Mode Visual Direction](unity/Docs/ChampionMode_VisualDirection.md)
- [Competitive Experience Benchmark](unity/Docs/AnotherLife_Competitive_Experience_Benchmark.md)
- [Terrestrial Design Brief](unity/Docs/Terrestrials/Terrestrial_Design_Brief.md)
- [Terrestrial Engineering Handoff](unity/Docs/Terrestrials/Terrestrial_Engineering_Handoff.md)
- [Customization Design](unity/Assets/AL/Art/Designs/ModularChampionCustomization.md)
- [Four-Realm Champion Anchor](unity/Assets/AL/Art/Designs/FourRealmChampionAnchor.md)
- [Four-Realm Heraldry — Arcane Axis](unity/Assets/AL/Art/Designs/FourRealmHeraldry.md)
- [Four-Realm Architecture](unity/Assets/AL/Art/Designs/FourRealmArchitecture.md)
- [Stonehold Architecture Animation Contract](unity/Docs/Architecture/Stonehold_Architecture_Animation_Contract.md)
- [Stonehold Animation Prototype Handoff](unity/Docs/Architecture/Stonehold_Animation_Prototype_Handoff.md)
- [Stonehold Workshop Level Progression](unity/Assets/AL/Art/Designs/StoneholdWorkshopLevelProgression.md)
- [Stonehold Workshop Final Model and Runtime Binding](unity/Docs/Architecture/Stonehold_Workshop_Final_Model_And_Runtime_Binding.md)
- [Eldergrove Architecture Animation Contract](unity/Docs/Architecture/Eldergrove_Architecture_Animation_Contract.md)
- [Eldergrove Animation Prototype Handoff](unity/Docs/Architecture/Eldergrove_Animation_Prototype_Handoff.md)
- [Crownlands Architecture Animation Contract](unity/Docs/Architecture/Crownlands_Architecture_Animation_Contract.md)
- [Umbral Architecture Animation Contract](unity/Docs/Architecture/Umbral_Architecture_Animation_Contract.md)
- [Crownlands Animation Prototype Handoff](unity/Docs/Architecture/Crownlands_Animation_Prototype_Handoff.md)
- [Crownlands Workshop Level Progression](unity/Assets/AL/Art/Designs/CrownlandsWorkshopLevelProgression.md)
- [Crownlands Workshop Final Model and Runtime Binding](unity/Docs/Architecture/Crownlands_Workshop_Final_Model_And_Runtime_Binding.md)
- [Umbral Animation Prototype Handoff](unity/Docs/Architecture/Umbral_Animation_Prototype_Handoff.md)
- [Umbral Workshop Level Progression](unity/Assets/AL/Art/Designs/UmbralWorkshopLevelProgression.md)
- [Umbral Workshop Final Model and Runtime Binding](unity/Docs/Architecture/Umbral_Workshop_Final_Model_And_Runtime_Binding.md)
- [Reusable Architecture Construction-State System](unity/Docs/Architecture/Reusable_Architecture_Construction_State_System.md)
- [Kingdom Building Level, Placement, and Presentation Design](unity/Docs/Architecture/Kingdom_Building_Level_And_Placement_Design.md)
- [Live Kingdom Construction UX Design](unity/Docs/Architecture/Live_Kingdom_Construction_UX_Design.md)
- [Eldergrove Workshop Level Progression — approved production source](unity/Assets/AL/Art/Designs/EldergroveWorkshopLevelProgression.md)
- [Eldergrove Workshop Level Blockout Handoff](unity/Docs/Architecture/Eldergrove_Workshop_Level_Blockout_Handoff.md)
- [Eldergrove Workshop Final Model and Runtime Binding](unity/Docs/Architecture/Eldergrove_Workshop_Final_Model_And_Runtime_Binding.md)
- [Architecture Android and iOS Compatibility Handoff](unity/Docs/Architecture/Architecture_Mobile_Compatibility_Handoff.md)
- [Four-Realm Town Hall Production Contract](unity/Docs/Architecture/FourRealm_TownHall_Production_Contract.md)
- [Stonehold Town Hall Level Blockout Handoff](unity/Docs/Architecture/Stonehold_TownHall_Level_Blockout_Handoff.md)
- [Stonehold Town Hall Final Model and Runtime Binding](unity/Docs/Architecture/Stonehold_TownHall_Final_Model_And_Runtime_Binding.md)
- [Eldergrove Town Hall Level Blockout Handoff](unity/Docs/Architecture/Eldergrove_TownHall_Level_Blockout_Handoff.md)
- [Eldergrove Town Hall Final Model and Runtime Binding](unity/Docs/Architecture/Eldergrove_TownHall_Final_Model_And_Runtime_Binding.md)
- [Crownlands Town Hall Level Blockout Handoff](unity/Docs/Architecture/Crownlands_TownHall_Level_Blockout_Handoff.md)
- [Crownlands Town Hall Final Model and Runtime Binding](unity/Docs/Architecture/Crownlands_TownHall_Final_Model_And_Runtime_Binding.md)
- [Umbral Town Hall Level Blockout Handoff](unity/Docs/Architecture/Umbral_TownHall_Level_Blockout_Handoff.md)
- [Umbral Town Hall Final Model and Runtime Binding](unity/Docs/Architecture/Umbral_TownHall_Final_Model_And_Runtime_Binding.md)
- [Approved Arcane Axis Vector Masters](unity/Assets/AL/Art/Heraldry/VectorMasters/README.md)
- [Android and Windows Design Handoff](unity/Docs/Cross_Platform_Design_Handoff.md)
- [Android Adaptive Icon Packet](unity/Docs/Branding/AndroidAdaptive/README.md)
- [Skill Effects and Weather Design](unity/Assets/AL/Art/Designs/SkillEffectsAndWeather.md)
- GitHub issue `#259`, which establishes the active realistic, high-end dark-fantasy direction for realm bosses and elites.
- The approved mystical medieval [`AL` application icon](unity/Assets/AL/Art/App_Icon_Mystic_Medieval_AL.png), shared on `main` for iOS, Android, and Windows derivatives.
- The approved terrestrial source concepts introduced by commit `8893306`: [Basalt Grazer](unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_basalt_grazer_concept_sheet_v001.png), [Grove Strider](unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_grove_strider_concept_sheet_v001.png), and [Mire Lumenback](unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_mire_lumenback_concept_sheet_v001.png).
- The owner-approved [Arcane Axis four-realm heraldry](unity/Assets/AL/Art/Designs/FourRealmHeraldry.md), which establishes the protected abstract marks for Stonehold, Eldergrove, Crownlands, and Umbral.

External games and images are benchmarks for quality, readability, or experience only. Never reproduce their characters, symbols, costumes, architecture, compositions, or proprietary visual signatures.

### Approved visual north stars

These five approved source groups define the current artistic range. Contributors and AI tools must study them together. No single source should be treated as a universal template.

| Approved source | What it establishes | Carry forward | Do not copy literally |
| --- | --- | --- | --- |
| **Mystical medieval `AL` app icon** | Brand mood and finish | Dark stone and midnight-indigo foundations; engraved aged metal; gothic and sacred framing; restrained gold/silver contrast; celestial violet energy; one dominant heraldic read supported by intricate craft | The `AL` monogram, exact arch, exact filigree, centered symmetry, or violet-and-gold palette on every asset |
| **Arcane Axis four-realm heraldry** | Realm identity and symbol construction | Original abstract geometry; one protected center or void; broad negative spaces; rendered, flat, inverse, and micro hierarchy; distinct realm marks held in one family | Raster artifacts, exact incidental bevels, ceremonial detail at micro size, literal mascots, or the marks as gameplay authority |
| **Basalt Grazer** | Believable mass and geological adaptation | Broad weight-bearing anatomy; protective silhouette; layered stone-like plates; warm hide against dark mineral surfaces; tiny magical mineral accents | Its quadruped anatomy, plate layout, or basalt treatment as the default solution for all heavy creatures |
| **Grove Strider** | Elegant ecological mysticism | Tall browsing silhouette; credible locomotion; bark, moss, lichen, and living growth integrated into the body; quiet magical dignity rather than decorative spectacle | Its long-neck body plan, antler/branch language, or forest materials on unrelated species |
| **Mire Lumenback** | Biological magic and compact readability | Squat amphibian mass; wet material response; throat and breathing behavior; cyan ring markings integrated like natural signaling; strong profile recognition at small scale | Its amphibian anatomy, ring pattern, or cyan emission as a generic magic treatment |

Together they establish this relationship:

1. **Nature and function determine the base form.**
2. **Medieval craft, ritual, and age determine how cultures frame, equip, build around, or represent that form.**
3. **Magic emerges from a specific biological, material, celestial, cultural, or environmental source.**
4. **A premium illustrated finish unifies the result through deliberate composition, tactile surfaces, controlled contrast, and a clear focal read.**

The app icon is the strongest reference for branding, interface framing, relics, ceremonial architecture, title presentation, and marketing finish. The Arcane Axis packet and its owner-approved flat and micro vector masters are the strongest references for realm marks, banners, allegiance UI, equipment stamps, and realm-controlled architecture. The terrestrial sheets are the strongest references for living anatomy, ecological credibility, material integration, and creature-sheet presentation. Character, creature, architecture, UI, and VFX work should share their artistic seriousness and material richness without forcing the same shapes or colors onto every category.

### Approved-range test

Before presenting a new concept, verify:

- The subject remains understandable with its glow, particles, ornament, and surface noise removed.
- Its anatomy or construction could support its stated movement, habitat, use, or load.
- Its medieval influence comes through believable craft, ritual, heraldry, repair, or material history—not costume clichés.
- Its magic has a named source and changes material, behavior, silhouette, or atmosphere purposefully.
- Its primary shape remains readable at the intended gameplay or icon size.
- Its detail is concentrated around focal and functional areas rather than spread uniformly.
- It could be presented beside the approved icon and terrestrial sheets without appearing cartoonish, generic, toy-like, sterile, or borrowed from another franchise.

## Brand

### Brand promise

Another Life should feel like entering a beautiful, dangerous world with centuries of conflict behind it. Every visual should suggest that its people, creatures, buildings, and relics belong to a functioning realm rather than a temporary level or decorative backdrop.

### Brand attributes

Use these attributes to judge every visual:

- **Mythic:** The world contains forces larger than the individual.
- **Tactile:** Stone, metal, leather, bark, skin, cloth, and magic have distinct physical behavior.
- **Ancient:** Wear, repair, patina, inheritance, and ritual create history.
- **Regal:** Even brutal or ruined subjects retain intentional craft and hierarchy.
- **Dangerous:** Threat is communicated through shape, motion, mass, and context.
- **Readable:** Players can understand identity and threat at gameplay distance.

### Signature motif

The approved app icon establishes the brand's ceremonial signature: engraved precious metal, gothic and sacred framing, dark stone, indigo-violet magic, and a centered heraldic focus. Apply the relationship—not the literal composition—across the product:

- Dark, materially rich foundations.
- Controlled precious-metal highlights.
- Magic used as a focal accent rather than a full-surface coating.
- Fine detail supported by a strong, readable primary shape.
- Symmetry for authority and ritual; asymmetry for corruption, nature, damage, and danger.
- A sense that symbols, equipment, buildings, and interfaces were crafted or inherited inside the world rather than applied as modern decoration.

Indigo-violet celestial magic and gold/silver metal are signature brand accents, not mandatory colors for every in-world subject. Realm and ecological palettes remain distinct. Use the icon's hierarchy, richness, contrast discipline, and atmosphere more broadly than its exact colors.

### Content boundaries

- Mature and serious does not mean sexually exploitative, gratuitously gory, or visually hostile.
- Armor and clothing must support the character's function, culture, climate, and movement.
- Exposed skin is a character choice, not a default substitute for design.
- Damage and decay may be present, but form, value, and material readability come first.

## Product goals

### Experience goals

The visual system must support three connected experiences:

1. **Champion and character experience:** A close, aspirational 3D view where anatomy, face, gear, material quality, and customization matter.
2. **Kingdom experience:** A polished 2.5D strategic view where structures, terrain, resources, and states remain legible on compact screens.
3. **Outer-warzone experience:** A 3D conflict space where realm identity, threat level, objectives, and combat effects read quickly.

### Visual success criteria

A successful asset:

- Belongs unmistakably to Another Life before a logo or label appears.
- Communicates category, realm, function, and approximate threat through more than color.
- Holds up at its closest intended view and remains identifiable at normal gameplay distance.
- Uses detail to reinforce structure rather than obscure it.
- Has a documented mobile and PC presentation plan.
- Can enter production without requiring another contributor to guess scale, materials, views, or intended use.

### Production goals

- Build reusable material families, trims, modular parts, rigs, and VFX grammars before multiplying unique assets.
- Preserve a high-end impression through composition, lighting, material response, and selective detail—not unrestricted texture size or polygon count.
- Support broad device reach, small downloads, quality tiers, and graceful degradation.
- Keep source art, runtime derivatives, prompts, provenance, and approvals traceable.

### Non-goals

- Locking final gameplay statistics or narrative canon through visual documents.
- Treating concept art as a final runtime asset without topology, rig, material, and performance review.
- Building a different visual language for every feature.
- Using technical expense as a proxy for visual quality.

## Personas and jobs

### Player

The player needs to:

- Recognize their Champion, realm, allies, enemies, objectives, and threats quickly.
- Understand equipment quality and threat without relying on one color scale.
- Enjoy close inspection without losing clarity during combat or strategy play.
- Use reduced-motion and non-color cues without losing important information.

### Creative director / project owner

The owner needs to:

- Compare proposals against one shared standard.
- Approve important identity choices before they spread across many assets.
- See source evidence, prompt history, exceptions, and production implications.
- Reject inconsistent work without rewriting the entire brief.

### Artist or modeler

The artist needs to:

- Know the subject's role, realm, scale, silhouette, materials, views, and runtime budget before modeling.
- Know which elements are locked and which are open to interpretation.
- Receive measurable review notes rather than general requests to “make it more fantasy.”

### Engineer or technical artist

The implementer needs to:

- Receive predictable files, pivots, scales, materials, rigs, LODs, colliders, and naming.
- Understand what can degrade across quality tiers without changing identity or gameplay meaning.
- Detect production risk before import.

### AI-assisted contributor

An AI-assisted contributor needs to:

- Use a complete prompt contract.
- Distinguish approved facts from creative suggestions.
- Preserve provenance and avoid copyrighted imitation.
- Return reviewable model sheets and alternatives instead of an unexplained final image.

## Information architecture

### Primary product surfaces

- Launch, authentication, and realm selection.
- Character creation, Champion presentation, equipment, and customization.
- 2.5D kingdom map and management surfaces.
- 3D Champion combat and boss encounters.
- 3D outer warzone, objectives, armies, and realm conflict.
- Collection, progression, rewards, settings, accessibility, and recovery states.

### Visual hierarchy

Use this order when composing a screen, scene, or model:

1. **Primary read:** subject, objective, or action.
2. **Identity read:** realm, faction, role, or category.
3. **State read:** friendly/hostile, available/disabled, normal/elite/boss, selected/unselected.
4. **Material and craft read:** what it is made from and how it was built.
5. **Story detail:** wear, repair, inscriptions, trophies, vegetation, and secondary ornament.

If story detail weakens the primary read, remove or simplify it.

### First-user journey interaction contract

The first playable journey must remain understandable without prior game knowledge,
mouse hover, or developer explanation. Apply these rules from realm selection through
the first authored quest handoff:

- Show one visually dominant primary action at a time. Choice controls may share a
  group, but the action that advances the journey must remain distinct from choices,
  Back, and Exit.
- Name commands for the result the player will receive: "Continue to class," "Enter
  the world," and "Hear Valerius's report." Avoid implementation terms such as draft,
  verification, destination, or tutorial in action labels.
- A selected choice must communicate selection through at least two channels, using
  visible text plus framing or material treatment. Color alone is insufficient.
- A control that cannot currently produce its named result must be visibly unavailable.
  Nearby status copy must state the missing prerequisite; do not leave enabled no-op
  buttons in the journey.
- Guidance, headings, and completed status are not buttons. If implementation requires
  a button-shaped compatibility surface, remove it from navigation, pointer ownership,
  and action styling until it becomes actionable.
- When an action completes, replace command wording with a completed or pending state.
  Never leave a disabled command that looks broken or invites repeated input.
- Hide task-specific controls after their task ends. Movement, combat, and dialogue
  controls must not remain as inert visual competition for the next primary action.
- Authored dialogue choices replace the previous choice set after selection. Declining
  must expose an explicit way to reopen the conversation, and accepting must advance to
  a named next action rather than an unlabelled or automatic transition.
- When the next game scene is outside the current test boundary, end on a clear prepared
  handoff state that names what is ready; do not imply that the encounter already ran.
- An isolated journey checkpoint may continue an authored encounter without inventing
  combat. It must label the checkpoint, expose success and safe retreat as distinct
  actions, and keep all results in memory rather than claiming production progression.
- Failure, retreat, and unavailable encounter outcomes must always lead to a visible
  retry action. A retry replaces the resolved encounter request and must never reuse a
  prior result or strand the player on a status-only surface.
- Encounter success must lead visibly back to the authored quest giver, then through the
  authored report choices to a stable realm-ready state. Show the completion result and
  one final completion action; do not make Exit the only explanation of what happened.
- Keyboard and controller focus must enter on the current task, move through controls in
  their visual order, and move to the next meaningful action after completion. Hidden,
  inactive, and presentation-only elements must not appear in the focus graph.
- Back changes the previous choice, Exit leaves the isolated experience, and the primary
  action advances. These meanings and their relative visual weight must remain stable on
  every screen.
- Keep the current task and its control instruction together. Global progress, identity
  context, and development disclosure remain secondary and must not compete with the
  immediate action.

### Camera-aware design

Every asset packet must identify its intended cameras:

- **Inspection:** close Champion, equipment, boss, or reward view.
- **Gameplay:** normal combat or traversal distance.
- **Strategic:** elevated 2.5D kingdom view.
- **Icon:** compact portrait, card, marker, or inventory presentation.

Do not approve an asset from a beauty render alone. Review it at the smallest and farthest intended presentation.

## Design principles

### 1. Silhouette before surface

The outer contour and the largest interior negative spaces must establish identity before texture, particles, or color. Use a solid-black silhouette test at gameplay scale.

### 2. Believable weight, selective exaggeration

Anatomy, balance, joints, construction, and material thickness should feel plausible. Exaggerate mass, length, posture, horns, shoulders, weapons, or architectural rhythm only to communicate role and improve readability.

### 3. Material truth

Metal reflects differently from stone; worn leather bends differently from plate; wet amphibian skin does not read like polished plastic. Ornament follows how the object was made. Edges, seams, fasteners, grain, and wear must reinforce the material.

### 4. History in layers

Use restrained dirt, oxidation, scratches, repairs, faded dye, chipped edges, moss, ash, or magical scarring to imply use and age. Wear should follow contact, weather, and movement rather than appear as uniform noise.

### 5. Magic is a controlled accent

Magic should reveal a source, path, and consequence. Favor focused emissive seams, runes with purpose, particles with directional motion, or material transformation. Avoid covering every edge with the same glow.

### 6. Realm identity is structural

Communicate realm through silhouette, construction logic, anatomy, materials, ornament, movement, sound direction, and VFX behavior. Palette is reinforcement, never the only cue.

### 7. Rank has separate channels

Do not collapse equipment rarity, boss threat, skill progression, and presentation intensity into one color ladder. Use category-specific cues such as silhouette, scale, material sophistication, motion, framing, sound, or effect complexity.

### 8. Detail follows distance

Spend geometry, texture detail, shader cost, and animation where the camera and interaction can reveal it. Large forms and value grouping must survive every LOD.

### 9. Reuse creates coherence

Share material families, trim sheets, construction rules, motifs, rigs, and VFX motion grammar within a realm. Reuse must feel cultural, not repetitive.

### 10. Accessibility is part of the art

Color-blind, reduced-motion, low-quality, and compact-screen presentations must preserve meaning. Accessibility is an approval requirement, not a late overlay.

## Visual language

### Global form language

- Favor strong primary masses and a limited number of intentional secondary forms.
- Use fine tertiary detail only where it supports hierarchy, craftsmanship, or close inspection.
- Mix durable geometry with aged, organic interruption: chipped stone, repaired plate, roots through masonry, worn cloth over rigid armor.
- Avoid uniform spikes, random filigree, repeated skulls, featureless smooth armor, and detail evenly distributed over every surface.
- Preserve readable faces, hands, weapons, and interaction points.

### Proportion and anatomy

#### Humanoids

- Start from realistic adult anatomy and joint placement.
- A heroic Champion may use approximately 7.5–8 head proportions, but body variety must remain credible.
- Hands, feet, armor thickness, and weapon grips must support animation and believable use.
- Do not use extreme shoulder, waist, chest, hip, or weapon proportions as a default shortcut for power.
- Faces should retain natural planes, age cues, and asymmetry; avoid doll-like skin and generic “perfect” AI faces.

#### Creatures

- Define habitat, diet, locomotion, defensive strategy, and ecological role before surface decoration.
- Maintain a believable center of mass and joint range.
- Give each species a profile-level silhouette and at least one non-color recognition cue.
- Magical features should feel integrated into biology or environmental adaptation rather than glued onto an ordinary animal.

#### Buildings and props

- Construction must imply load-bearing logic, assembly, repair, climate, and available materials.
- Slightly exaggerate roofline, tower, gate, resource node, and interaction shapes for the strategic camera.
- Preserve close-view material credibility even when primary shapes are enlarged for 2.5D readability.

### Shape hierarchy

Use an approximate visual-detail ratio:

- **70% primary forms:** anatomy, mass, major armor, roofline, body plan.
- **20% secondary forms:** plates, limbs, structural braces, drapery, major ornament.
- **10% tertiary detail:** engraving, stitching, chips, scars, runes, small vegetation.

This is a composition check, not a polygon allocation formula.

### Value and color

- Establish readable light/dark grouping before hue.
- Keep most surfaces within controlled, naturalistic saturation.
- Reserve the brightest values, strongest hue contrast, and emissive color for focus and state.
- Favor the approved overall range: midnight blue, indigo, charcoal, aged stone, tarnished metals, natural hides, bark, moss, wet earth, restrained precious-metal warmth, and small luminous magical accents.
- Let habitat, realm, and material determine the local palette; the app icon's violet, silver, and gold are a brand anchor rather than a universal recoloring rule.
- Ensure important identities survive grayscale and common color-vision simulations.
- Avoid crushing Umbral assets into featureless black or lifting magical elements until they lose a physical base.

The following realm colors are directional anchors, not permanent hex-value locks. Final palette tokens require project-owner approval after representative character, creature, building, and VFX tests.

### Realm identity matrix

| Realm | Structural identity | Material family | Palette direction | Magic and motion | Avoid |
| --- | --- | --- | --- | --- | --- |
| **Stonehold** | Defensive mass, compression, buttresses, layered plates, practical forge construction | Basalt, dark iron, aged steel, soot, heavy leather, mineral inclusions | Charcoal, iron brown, ash, restrained forge amber; small mineral accents | Pressure, sparks, heat distortion, impact, short forceful motion | “Dwarf” pastiche, orange on every edge, identical blocky silhouettes |
| **Eldergrove** | Grown interlock, branching rhythm, vertical organic forms, living repair | Bark, lichen, moss, woven fiber, weathered bronze, bone used sparingly | Deep green, bark umber, muted leaf gold, warm living accents | Germination, spirals, drifting seed, elastic recovery, flowing arcs | Cute forest sprites by default, uncontrolled foliage noise, bright green as the only cue |
| **Crownlands** | Disciplined heraldry, balanced geometry, tall authority, engineered refinement | Aged silver, polished steel, blue textile, pale stone, controlled gold/brass | Royal blue, steel, parchment, restrained gold; clear sky/celestial accents | Ordered arcs, radiant lines, banners, measured precision | Pristine plastic “paladin” surfaces, excessive gold, borrowed real-world heraldry |
| **Umbral** | Negative space, fractured symmetry, concealment, narrow predatory forms | Obsidian, smoked metal, blackened leather, ash cloth, glassy corruption | Aubergine, indigo, charcoal, cold violet; enough value separation to retain form | Absorption, delayed trails, smoke, folding space, quiet directional motion | Featureless black, purple glow on everything, horror gore as the main identity |

### Materials

Each production asset must declare:

- Base material and fabrication method.
- Surface age and environmental exposure.
- Roughness range and variation logic.
- Edge and cavity treatment.
- Damage and repair logic.
- Whether any emission is physical, magical, or UI-only.

Prefer physically plausible values and controlled variation. Avoid:

- Uniform roughness.
- Edge wear on untouched recesses.
- High-frequency grunge across the full asset.
- Deep black albedo used to fake shadow.
- Emissive values that erase the underlying material.
- Unique materials where a shared library or trim sheet can preserve quality.

### Ornament and symbols

- Every symbol must have a known cultural, institutional, functional, or magical reason.
- Reuse approved realm motifs consistently.
- Keep large motifs readable before adding nested engraving.
- Do not generate unreadable pseudo-text as finished ornament.
- Do not use living religious, cultural, heraldic, or military symbols without deliberate review.
- Never imitate the distinctive emblem or signature pattern of another franchise.

### Lighting

- Use high-contrast, cinematic lighting to reveal form, not hide unfinished surfaces.
- Maintain readable midtones and contact shadows on compact screens.
- Use a neutral studio-lighting render for approval in addition to beauty lighting.
- Realm lighting can influence mood, but it must not replace the asset's own realm cues.
- Check skin tones, dark materials, and emissive accents in both bright and dim environments.

### VFX

- Define the source, direction, timing, area, and end state of every effect.
- Use shape and motion to communicate function before color.
- Create reduced-motion and low-quality variants.
- Keep particles away from faces, interaction points, and gameplay telegraphs unless they are the telegraph.
- Limit layered transparency and full-screen effects, especially on mobile.
- Boss spectacle may increase intensity, but the underlying silhouette and attack read must remain visible.

### Motion

- Motion must express anatomy, construction, material, function, and realm identity rather than decorate every visible asset.
- A structure's load-bearing mass remains stable during ordinary idle states. Put ambient life in functional elements such as doors, shutters, machinery, cloth, foliage, smoke, light, water, workers, and activity props.
- Construction and repair motion must follow the approved modular hierarchy, pivots, sockets, and load paths. Do not scale a complete building from zero, stretch rigid masonry, or use unexplained floating assembly.
- Use state-driven construction that can resume at a credible persistent stage after streaming, reconnecting, or returning from offline progress.
- Use one shared six-state architecture lifecycle across realms. Realm profiles define construction motion character and optional bounded activity components define stable-state behavior; do not create independent realm state machines.
- Treat the four realm prototypes as construction-motion grammars, not as final models or one-to-one building definitions. Building function and level own the modules being changed; realm identity owns how that change moves.
- Derive construction presentation from the authoritative gameplay level and active order. Never persist a separate visual stage that can disagree with gameplay.
- A `0 → 1` order may use the complete construction grammar. Later upgrades keep the existing building settled and animate only the approved module delta for the target level.
- Separate a major state transition from its stable loop: the transition may use a short readable action, while the finished state must settle into a long quiet hold.
- Apply realm motion grammar before VFX. Stonehold uses pressure, leverage, impact, and short forceful actions. Eldergrove uses guided growth, flowing arcs, biological circulation, and one damped recovery into stable structure. Crownlands uses synchronized placement, ordered arcs, measured calibration, controlled radiant lines, and long precise holds. Magic may support or confirm a functional state but does not replace physical construction.
- Reduce loop count, character activity, particles, secondary motion, and update frequency with camera distance. Far proxies remain static.
- Give every major transition and ambient loop a reduced-motion version. State meaning must survive without camera shake, repeated impacts, flashing, or continuous particles.

### Reference quality bar

The approved Basalt Grazer, Grove Strider, and Mire Lumenback concept sheets are the current creature-design quality anchors. Match their relationship between form, ecology, material, and restrained fantasy:

- Realistic, naturalistic anatomy.
- Distinct profile silhouettes.
- Materials and magical features integrated into ecology.
- Restrained accent color.
- Enough exaggeration for gameplay recognition without becoming cartoon-like.
- Tactile surface differentiation that remains subordinate to the large body masses.
- Multiple consistent views suitable for later modeling decisions.
- A serious, artistic presentation compatible with the app icon's medieval-mystical world.

They are approved source evidence, not universal anatomy templates or finished runtime models. New creatures must occupy the same artistic range without reusing their anatomy, markings, surface motifs, or palette as a shortcut.

For non-creature work, translate rather than imitate: preserve the sheets' believable function, silhouette discipline, restrained fantasy, tactile material separation, and production-readable presentation. Add the app icon's sense of ancient craft, ritual, atmosphere, and focal richness where appropriate to the category.

## Model categories

### Champions and major characters

- Use realistic adult anatomy with an aspirational, heroic primary silhouette.
- Preserve face, hair, hands, weapon grip, and customization seams at inspection distance.
- Build modular equipment around stable body, rig, and attachment standards.
- Hide or intentionally design module boundaries; avoid visible gaps and double surfaces.
- Communicate class or combat role through posture, equipment distribution, major shapes, and animation—not only weapon color.
- Keep ornament subordinate to face, hands, and action.

### NPCs and soldiers

- Share the world's anatomy, material, and construction standards.
- Communicate profession and hierarchy through tailoring, maintenance, material access, posture, and equipment organization.
- Use controlled modular variation rather than fully unique assets for every unit.
- Do not make low-rank characters toy-like or anatomically simplified.

### Equipment, armor, and weapons

- Define how an item is worn, fastened, drawn, carried, repaired, and stored.
- Maintain plausible thickness, edge treatment, grip size, and center of mass.
- Scale ornament and material complexity with cultural importance, not one global rarity formula.
- High-tier equipment may use stronger silhouette and controlled magical response, but must retain physical construction.
- Never use glow alone to communicate rarity or functionality.

### Ambient and terrestrial creatures

- Begin with an ecological design brief.
- Establish scale against the Champion and a familiar environmental reference.
- Provide locomotion, idle, alert, flee/defend, hit, and death or despawn intent as applicable.
- Preserve the species' key profile cue through all LODs and animation extremes.
- Give herd or flock species variation that does not weaken recognition.

### Elites

- Read as an elevated version of a realm's ecosystem or culture without merely enlarging a common unit.
- Require at least three distinguishing channels among anatomy, silhouette, material, equipment, locomotion, VFX behavior, and encounter framing.
- Keep the realm relationship visible underneath corruption, armor, or magic.

### Bosses

- Establish a recognizable full-body silhouette at encounter-entry distance.
- Use at least three threat channels: for example mass, posture, anatomy, motion cadence, environmental response, equipment, sound direction, or effect behavior.
- Design attack origins and telegraph zones into the anatomy and silhouette.
- Preserve target points, face or focal region, and major attack shapes under VFX.
- A boss is not a common creature scaled up, recolored, and covered in particles.

### Buildings and environmental props

- Author for both strategic readability and close-enough material credibility.
- Make function visible through access points, production areas, storage, defenses, smoke, banners, traffic, or maintenance.
- Use realm construction logic consistently across modular kits.
- Use stable building-slot identity for placement. Save-list or enumeration order must never determine a building's grid position, footprint, rotation, or entrance orientation.
- Use the approved `Level 0` through `Level 10` visual progression in the kingdom-building level design. `Level 0` is an unbuilt reserved plot, `Level 1` is the first complete operational building and current baseline, and later levels add cumulative modular changes without replacing the building's function.
- Production models bind from stable `RealmId + BuildingId` identity and derive their cumulative visual level directly from confirmed gameplay state. Never persist a parallel visual stage that can disagree with gameplay.
- Live production construction motion is confirmation-driven and session-only: an active upgrade keeps the confirmed model settled with localized worksite feedback, a newly confirmed adjacent level may animate only its new delta, and first load or offline reconciliation never replays motion.
- Live construction is gameplay-authoritative. Building definitions own the exact Level `1`–`10` resource recipes and UTC durations; the building service owns quote, validation, resource spend, active-order state, completion, rollback, and persistence; the runtime owner reconciles completed work before presentation consumes the save.
- A missing building row is Level `0` and remains query-safe; an existing Level `1` row remains the current built baseline. Queries never create building rows. Only a committed `0 → 1` construction order may create the first row.
- One active order is allowed per building. Costs are paid in full when the order is accepted. A known save failure restores both wallet and building state; an uncertain commit preserves the candidate and blocks further construction until save reconciliation.
- The first live command-deck construction slice exposes approved runtime definitions for Town Hall, Farm, Lumber Mill, Quarry, Gold Mine, and Barracks. Reserved Mana Shrine and Mine slots remain visible but inert until matching game-data definitions are approved.
- Recommended next interaction: construction discovery and construction commitment are separate. Selecting a world building or BUILD entry opens the same local inspector and authoritative quote; only the inspector's explicit `Construct` or `Upgrade` action may spend resources. Implementation remains held for owner review; do not treat the current direct-spend buttons as the finished UX.
- The construction inspector shows only confirmed current level, exact next level, authoritative cost, treasury sufficiency, duration, active completion time, and supported status. It must not invent yields, unlocks, production bonuses, lore, or target-stage visuals that do not exist in approved data.
- Cancellation, refunds, build queues, cross-building prerequisites, premium speedups, server order identity, demolition, and relocation are on explicit product hold. Do not surface them as disabled teasers, placeholder buttons, or implied future promises. Reopening any of them requires a separate owner decision.
- The active production building family is the four-realm `TownHall`, using the existing stable center slot as a hero-scale civic anchor rather than replacing the castle keep or landmark layer. Stonehold, Eldergrove, Crownlands, and Umbral production sources, Level `1`/`6`/`10` grayboxes, final Level `1`–`10` production models, and direct live bindings are approved. Working Level `10` labels guide visual production but do not establish narrative canon, powers, institutions, or gameplay.
- Town Hall Level `10` capstones remain grounded, static extensions of approved civic structure: Stonehold uses the **Oathstone Crown**, Eldergrove the **Open Crown Arbor**, Crownlands the **Concord Meridian**, and Umbral the **Veiled Accord Yoke**. They do not reuse the Workshop seed lantern, forge chimney, storm calibration device, or bound-eclipse apparatus. Replacing them with unsupported, continuously active, function-changing, or Workshop-specific spectacle is a major design-direction decision.
- Provide clean state, active state, damaged state, disabled state, and selected/outlined behavior where required.
- Avoid miniature-diorama cuteness as the default 2.5D solution.

### Icons, portraits, and marketing crops

- Preserve one dominant silhouette or monogram and a clean focal hierarchy.
- Test at the actual smallest display size.
- Avoid critical detail near crop-safe edges.
- Do not place small text, pseudo-text, or thin unlit ornament where it disappears at icon size.
- Marketing lighting may be dramatic, but it must not misrepresent the runtime design.

## Components and tokens

### Shared visual components

Build and reuse:

- Realm material libraries.
- Realm trim sheets and approved ornament sheets.
- Shared human rigs, body standards, attachment points, and modular armor interfaces.
- Creature rig families where anatomy genuinely supports reuse.
- VFX motion families for impact, healing, corruption, authority, weather, and environmental magic.
- Selection, hostility, objective, disabled, and interactable presentation treatments.
- Studio-lighting, gameplay-lighting, silhouette, grayscale, and icon-preview review scenes.

### Token categories

Store final approved values in implementation-friendly tokens or profiles:

- Realm base, secondary, metal, textile, magic, and warning colors.
- Material roughness and metal-response ranges.
- Emissive intensity ranges per quality tier.
- VFX density, lifetime, and transparency tiers.
- Outline or selection treatment.
- LOD thresholds by asset category.
- Texture-resolution and compression overrides by platform.

Do not hard-code new visual constants across unrelated scripts when a shared profile is appropriate. Profile work must preserve non-color identity and accessibility.

## AI and contributor handoff contract

### Required brief fields

No model, concept, icon, VFX, or environment request is ready for production until it includes:

| Field | Required content |
| --- | --- |
| Asset ID | Stable, unique identifier |
| Category | Champion, NPC, elite, boss, terrestrial, building, prop, equipment, icon, VFX, or environment |
| Purpose | Gameplay and presentation job |
| Realm / affiliation | Approved identity or `neutral/unassigned` |
| Approval state | Exploration, selected concept, production source, runtime candidate, or approved |
| Scale | Unity meters plus Champion/environment comparison |
| Camera use | Inspection, gameplay, strategic, icon, or cinematic |
| Primary silhouette | One-sentence shape description and key negative spaces |
| Anatomy / construction | Body plan or fabrication logic |
| Materials | Primary/secondary materials, age, roughness, damage, and repair |
| Palette | Realm relationship and accent limits; do not use color as the sole cue |
| Magic / VFX | Source, behavior, intensity, and reduced-motion intent |
| Required views | Model-sheet and gameplay-review views |
| Animation needs | Rig family, locomotion, interaction, and deformation requirements |
| Runtime tier | Mobile/PC and LOD expectations |
| Accessibility | Non-color cues, motion, flash, and compact-screen checks |
| Exclusions | Project-wide negatives plus asset-specific prohibited directions |
| Evidence | Approved docs, issue, source packet, and reference paths |
| Provenance | Human/AI author, tool/model/version, date, source/license, and prompt |
| Open decisions | Choices that still require owner approval |

If a required field is unknown, label it `OPEN` rather than inventing an answer.

### Required concept/model sheet

Unless an asset-specific packet says otherwise, submit:

- Front, side, back, and three-quarter views with consistent proportions.
- Neutral orthographic or long-lens presentation with minimal perspective distortion.
- Neutral studio lighting and a separate mood or gameplay-lighting view.
- Solid-black silhouette at normal gameplay size.
- Scale comparison against the Champion or a standard environment measure.
- Material swatches with names and response notes.
- Close-ups of face, hands, joints, fasteners, interaction areas, and signature details.
- Color and grayscale presentations.
- Intended LOD silhouette progression.
- T-pose or A-pose for rigged humanoids; appropriate neutral bind pose for creatures.

Keep text labels outside the subject. Do not allow watermarks, pseudo-text, or generated annotations to become part of the design.

### Baseline generation prompt

Use this structure when requesting visual exploration:

> Create an original production concept for **[ASSET ID / CATEGORY]** in Another Life using the project's **mystical medieval naturalism**. Match the approved visual range: the ecological credibility, silhouette discipline, tactile material separation, and restrained integrated magic of the Basalt Grazer, Grove Strider, and Mire Lumenback concept sheets; and the ancient craftsmanship, dark material richness, controlled precious-metal contrast, celestial atmosphere, and clear focal hierarchy of the approved `AL` app icon. Translate those qualities to this asset—do not copy their anatomy, markings, monogram, arch, filigree, composition, or exact palette. The asset serves **[PURPOSE]**, belongs to **[REALM/AFFILIATION]**, measures **[SCALE]**, and must read from **[CAMERAS]**. Its primary silhouette is **[SILHOUETTE]**. Use believable **[ANATOMY/CONSTRUCTION]**, physically plausible **[MATERIALS]**, layered age and repair, restrained **[PALETTE]**, and controlled magic originating from **[SOURCE]**. Preserve non-color recognition through **[SHAPE/MOTION/MATERIAL CUES]**. Provide **[REQUIRED VIEWS]** under neutral studio lighting plus one cinematic medieval-mystical presentation and one gameplay-lighting view, with scale reference, material swatches, grayscale check, and LOD silhouette intent. Keep the result original and production-readable. Do not invent lore, statistics, names, or mechanics; label unresolved decisions OPEN.

Add asset-specific exclusions after the baseline negative direction:

> No cartoon, chibi, toy-like, cute-by-default, low-poly final look, plastic materials, doll face, generic AI-fantasy collage, random runes, uniform grunge, excessive spikes, glow on every edge, palette-swap identity, illegible black surfaces, copied franchise motifs, logos, watermark, pseudo-text, or unsupported gameplay/narrative claims.

### AI review rules

- Request a small number of meaningfully different silhouette directions before surface polish.
- Reject outputs with view-to-view anatomy or costume inconsistency.
- Do not use an attractive beauty image as topology, material, or rig proof.
- Do not “repair” copied or suspicious work by adding more generation passes; remove it from consideration.
- A human must select the direction and record the decision before production modeling.
- Any AI-assisted production texture or mesh requires source, license, cleanup, artifact, and similarity review.

### Naming and versioning

Use lowercase source-document IDs and stable runtime names:

- Source packet: `<category>_<realm-or-neutral>_<asset>_v###`
- Concept image: `<asset-id>_concept_<view>_v###`
- Model source: `<asset-id>_source_v###`
- Runtime prefab: `AL_<Category>_<AssetName>`
- Materials: `MAT_<RealmOrNeutral>_<MaterialName>`
- Textures: `T_<AssetName>_<Map>_<Size>`
- LOD meshes: `<AssetName>_LOD0` through `<AssetName>_LOD#`

Never overwrite an approved source version. Create a new version and preserve the approval trail.

## Implementation constraints

### Current technical baseline

- Unity `2022.3.62f3`.
- Built-in Render Pipeline at the time of this guide.
- Target experiences include mobile and PC.
- iOS `15.0` is the approved minimum deployment target; compatibility claims still distinguish build-level validation from an actual iOS 15 runtime or device pass.
- Current generated prefabs and materials are blockouts/reference implementations, not a final quality bar.
- Production models must use Unity's meter scale: `1 Unity unit = 1 meter`.
- Shared design handoffs must remain usable from Android and Windows checkouts: preserve committed Unity metadata, portable paths, and Git LFS assets; do not require a macOS-only path or an iOS-only branch to recover approved source.
- Concept sheets and review images are production reference, not runtime textures. A Player-ready use requires an explicit derivative, import contract, and measured budget.
- The approved Arcane Axis runtime derivatives are white-alpha PNG Sprites with exact Android and Standalone overrides; consuming UI owns tint while final realm colors remain open.
- Android adaptive launcher derivatives must keep their primary mark inside the centered `66/108` safe-zone ratio, use an independent transparent foreground and opaque full-bleed background, remain legible under circle/squircle/rounded-square masks, and preserve the approved full square application icon as immutable brand authority.

A render-pipeline migration, a change to the target device floor, or a material-system replacement is a broad technical/design decision and requires an explicit migration plan.

### Provisional runtime authoring ceilings

These are starting ceilings for planning, not automatic targets or permanent guarantees. Use less where the asset can preserve its read. Profile representative scenes on the actual lowest-supported device before promoting an asset or changing a ceiling.

| Runtime category | LOD0 triangle ceiling | Typical material slots at LOD0 | Runtime texture guidance |
| --- | ---: | ---: | --- |
| Champion / major character | 60k | 3 | Up to 2K primary sets; share and pack where practical |
| Major boss | 100k | 4 | Up to 2K sets per major material family; justify every additional set |
| Elite / important NPC | 45k | 3 | 1K–2K according to closest camera |
| Ambient terrestrial / common unit | 25k | 2 | Usually 1K; use shared families |
| Hero kingdom building | 40k | 3 | 1K–2K with trims/atlases |
| Common building / large prop | 20k | 2 | Usually 1K with trims/atlases |
| Small prop | 5k | 1 | 512–1K; atlas when repeated |

Additional expectations:

- Mobile normal gameplay should generally use an appropriate reduced LOD; reserve LOD0 for inspection, marketing capture, or camera distance that can reveal it.
- Start LOD1 near 50–60% of LOD0 triangles, LOD2 near 20–30%, and a far LOD near 5–10%, then adjust by silhouette and measured cost.
- Preserve face/focal region, weapon/attack origin, negative spaces, realm cue, and threat cue before secondary detail.
- Screen-relative LOD thresholds must be tuned in representative cameras. Cross-fade can temporarily render two LODs, so include that overlap in performance review.
- Standard skinned assets should use no more than four bone influences per vertex; use fewer where deformation allows.
- Keep deformation bones focused on visible motion. A Champion should begin under 90 deformation bones and a boss under 120 unless profiling and animation needs justify an exception.
- Merge material slots where this does not damage material truth or customization. Lower LODs should normally reduce slots.
- Prefer opaque materials. Use alpha clipping selectively for hair, foliage, or necessary fine structure; limit blended transparency.
- Use mipmaps for distance-varying textures and platform-specific compression overrides.
- For modern supported iOS devices, evaluate ASTC and choose block size by measured quality and memory rather than one project-wide setting.
- Disable mesh and texture read/write access unless runtime code demonstrably requires CPU access.
- Mesh compression is asset-specific: inspect deformation, normal, seam, and silhouette artifacts before choosing a level.
- Pack compatible grayscale masks into channels when it improves memory and sampling without obscuring ownership.
- Remove unused blendshapes, animation curves, UV channels, bones, and imported data.
- Keep colliders separate from visual topology and as simple as gameplay permits.

Do not create a unique 4K runtime texture for a small or medium asset. A 4K source may be retained for baking, cinematic output, or a separately approved high tier, but it needs an explicit runtime derivative.

### Required LOD and quality-tier submission

Every production 3D asset packet must state:

- LOD count and screen-relative intent.
- What identity cues are protected at every LOD.
- Material-slot changes.
- Texture changes or streaming assumptions.
- Shadow behavior.
- Collider strategy.
- Animation or VFX reduction.
- Mobile-low, mobile-high, and PC-high presentation differences.
- Measured scene, camera, device, and profiler evidence when available.

### Import and prefab requirements

- Apply transforms and verify meter scale before import.
- Use a predictable forward/up-axis conversion and document exceptions.
- Place pivots for animation, placement, snapping, and destruction—not for modeling convenience.
- Use consistent mesh, rig, material, texture, animation, collider, and socket naming.
- Keep source files and runtime exports version-linked.
- Ensure rigged models return cleanly to bind pose and do not depend on hidden helper geometry.
- Create intentional shadow, bounds, culling, and LOD settings.
- Validate dark surfaces and emissive accents under project lighting rather than only in the DCC viewport.

### Performance validation

Unity's official guidance emphasizes profiling instead of universal asset counts. Validate:

- Visible renderer and material count.
- Draw calls and render-state changes.
- Triangle and vertex count, including UV seams and hard-edge splits.
- Skinned-mesh and bone cost.
- Overdraw and transparent layers.
- Real-time light and shadow cost.
- Texture memory, import format, mip use, and loading behavior.
- LOD transition quality and cross-fade overlap.
- Build-size effect.

Official references:

- [Unity 2022.3 graphics performance fundamentals](https://docs.unity3d.com/2022.3/Documentation/Manual/OptimizingGraphicsPerformance.html)
- [Unity 2022.3 LOD Group](https://docs.unity3d.com/2022.3/Documentation/Manual/class-LODGroup.html)
- [Unity 2022.3 mesh compression](https://docs.unity3d.com/2022.3/Documentation/Manual/mesh-compression.html)
- [Unity 2022.3 model importing](https://docs.unity3d.com/2022.3/Documentation/Manual/ImportingModelFiles.html)
- [Unity 2022.3 platform texture overrides](https://docs.unity3d.com/2022.3/Documentation/Manual/class-TextureImporterOverride.html)
- [Unity 2022.3 draw-call optimization](https://docs.unity3d.com/2022.3/Documentation/Manual/optimizing-draw-calls.html)

## Accessibility

### Non-color communication

Every important identity or state needs at least two channels among:

- Silhouette or icon shape.
- Pattern or material.
- Position or framing.
- Animation or motion direction.
- Sound or haptic behavior.
- Label or symbol.
- Color.

Color may be one channel, never the only one.

### Required checks

- Grayscale check at gameplay and icon size.
- Common color-vision simulation for realm, hostility, rarity, objective, and warning cues.
- Compact-screen check on the smallest supported phone class.
- Reduced-motion version for major VFX, camera impulses, looping ambience, and UI animation.
- Flash-frequency and full-screen luminance review for intense magic or weather.
- Low-quality check ensuring identity and telegraphs survive reduced particles, shadows, texture detail, and post-processing.

### Character and creature clarity

- Keep faces and key anatomy readable across a representative range of skin tones and lighting.
- Do not rely on subtle red/green shifts to distinguish damage, poison, healing, or allegiance.
- Ensure attack telegraphs retain shape, origin, and boundary without bloom.
- Avoid camera, cape, foliage, and particle behavior that repeatedly obscures the player or target.

## Responsive behavior

### Device and camera adaptation

- Compose assets and scenes for iPhone, iPad/tablet, Android, and PC-class aspect ratios.
- Keep focal content inside safe crop zones for icons, cards, and store art.
- Test portrait-safe crops even when runtime gameplay is landscape.
- On compact screens, simplify tertiary detail and effect density before weakening primary silhouette or state.
- On larger screens, add material and environmental depth without changing gameplay meaning.

### Input adaptation

- Touch targets and selection treatments must not depend on hover.
- PC hover may add detail, but all required state must exist for touch and controller.
- Inspection rotation, zoom, and equipment selection must tolerate finger occlusion and safe-area insets.

### 2.5D kingdom behavior

- Preserve building function through roofline, footprint, entrance, activity, and icon treatment.
- Use depth, value, and spacing to separate interactable structures from scenery.
- Avoid fine façade detail as the only indicator of upgrade or damage.
- Keep important effects contained enough that neighboring structures remain selectable.
- Let players continuously pinch-zoom and drag-pan across the bounded inner kingdom rather than exposing a small set of visible zoom stages.
- Rendering may use hidden LOD, culling, label-density, and effect thresholds, but transitions must preserve the feeling of one continuous explorable place.
- A tap selects a building, plot, gate, activity node, or approved character anchor and reveals a compact local action surface; persistent action bubbles must not cover neighboring targets.
- Dragging empty ground pans the camera. Moving, rotating, demolishing, or otherwise transforming a selected element requires an explicit interaction mode so ordinary navigation cannot trigger a destructive or positional action.
- Selected and occluded targets need readable ground bounds, outlines, or temporary obstruction treatment that does not depend on realm color or heavy glow.
- Keep camera tilt and orbit behavior controlled until an asset-completeness and occlusion review proves that unrestricted rotation is supportable.

## Interaction states

Every reusable visual component or interactive asset must define applicable states:

- Default.
- Hover/focus where supported.
- Selected.
- Pressed/activated.
- Available/interactable.
- Disabled/unavailable.
- Friendly, neutral, and hostile.
- Normal, elite, and boss threat.
- Loading or streaming.
- Placeholder or missing asset.
- Error or failed load.
- Offline or stale data.
- Success, reward, or completed.
- Damaged, destroyed, repairing, or recovering.

Use hierarchy, shape, material, framing, motion, and labels before adding more glow. Loading and placeholder visuals must be intentional and must not be mistaken for approved production art.

## Content voice

### Visual-document voice

Write briefs and review notes in direct, observable language:

- Prefer “the shoulder silhouette merges with the weapon at gameplay distance.”
- Avoid “it lacks soul” or “make it cooler.”
- Identify the camera, scale, asset version, violated principle, and desired outcome.
- Separate required corrections from optional exploration.

### In-world tone

- Mature, mythic, restrained, and specific.
- Favor short names and language that suggest a real culture and function.
- Avoid jokey modern phrasing unless a specific character or surface calls for it.
- Avoid empty superlatives such as “ultimate,” “epic,” or “legendary” as substitutes for meaning.
- Do not generate lore or names solely to decorate a concept sheet.

### Review vocabulary

Use these terms consistently:

- **Primary read:** first recognized mass or action.
- **Secondary read:** realm, role, material, or state.
- **Gameplay read:** what survives at normal play distance.
- **Inspection read:** what rewards close viewing.
- **Identity cue:** a feature required for recognition.
- **Threat cue:** a feature communicating danger or attack behavior.
- **Protected cue:** a feature that must survive LOD and quality reductions.
- **Production source:** approved art sufficient to begin implementation.
- **Runtime candidate:** imported asset awaiting performance and integration approval.

## Review and approval

### Stage gates

1. **Brief complete:** All handoff fields are present; unknowns are marked OPEN.
2. **Silhouette selected:** Owner approves primary shape and scale before detail multiplication.
3. **Concept selected:** Views, anatomy/construction, materials, realm cues, and exclusions are consistent.
4. **Production source approved:** Modeler/artist can work without inventing critical direction.
5. **Runtime candidate reviewed:** Topology, rig, materials, LODs, naming, colliders, and import are valid.
6. **Performance validated:** Representative mobile and PC checks meet the current target.
7. **Creative final approved:** Owner confirms the runtime asset preserves the selected direction.

A stage approval does not imply approval of later stages.

### Review scorecard

Score each category `Pass`, `Revise`, or `Not applicable`:

- Core style lock.
- Originality and provenance.
- Primary silhouette.
- Realm and role identity.
- Anatomy or construction.
- Material truth.
- Gameplay and icon readability.
- Non-color and reduced-motion accessibility.
- Cross-view consistency.
- Animation and interaction readiness.
- LOD and quality-tier preservation.
- Runtime budget and profiler evidence.
- Naming, source linkage, and approval record.

Any `Revise` in Core style lock, originality/provenance, silhouette, accessibility, or runtime readiness blocks final approval.

### Critical direction flags

Stop production multiplication and request project-owner direction when a proposal would:

- Change the Core style lock.
- Redefine a realm's structural or material identity.
- Establish a new universal rarity, threat, or magic-visualization system.
- Replace realistic anatomy with a cartoon, toy, or deliberately low-poly final style.
- Change the target platform floor, render pipeline, lighting architecture, or runtime material system.
- Exceed the provisional budget enough to require a different scene or platform strategy.
- Use a licensed or AI-derived source with unclear rights or suspicious similarity.
- Introduce sexualization, gore, cultural symbolism, or other sensitive content beyond the established boundary.
- Change customization body, face, armor, or attachment assumptions across multiple assets.

Do not stop for ordinary reversible interpretation inside the approved brief. Record the assumption and continue to a reviewable proposal.

## Open questions

These questions do not block concept exploration, but they must be resolved before the affected production gate:

- Which minimum iPhone and iPad hardware models within the approved iOS 15 software floor, and which Android and PC hardware targets, define the lowest production tier?
- What frame-rate, memory, download-size, and scene-complexity budgets define each quality tier?
- Will the project remain on the Built-in Render Pipeline through first production art, or adopt a planned rendering migration?
- Which exact realm palette values and shared material tokens pass representative character, creature, architecture, UI, and VFX tests?
- What are the approved ranges for body type, age, facial variation, cultural influence, and character sexualization?
- What is the explicit gore and body-horror boundary for bosses, damage states, and death presentation?
- Which DCC source formats, texture-authoring tools, and large-file/version-control workflow are required for production?
- Which assets require facial blendshapes, cloth, hair simulation, destructibility, or other features that materially change budget?
- After representative on-device profiling, which provisional triangle, bone, material, texture, and VFX ceilings should become category-specific production limits?
- Should the recommended construction interaction—select building or BUILD entry, inspect authoritative quote, then explicitly `Construct` or `Upgrade`—replace the current direct-spend BUILD buttons in the next implementation pass?

Record answers here or in an approved subject-specific decision record, then link that record from this section.
