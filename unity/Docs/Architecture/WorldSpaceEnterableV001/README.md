# World-Space Enterable Architecture 2D Authority v001

## Approval and status

The owner approved every artifact shown from the civic-hall and fort-gatehouse packets on 2026-09-01. The manifests and this document supersede the pre-approval `candidate` footer embedded in some rendered review sheets.

This package is approved **2D spatial, modular-construction, and visual-reference authority** for downstream 3D modeling. It does not contain or authorize silent 3D generation, runtime prefabs, gameplay binding, save changes, balance changes, or release configuration.

After this scoped package merges with required CI green, production pauses. Realm-specific fort/castle exteriors, castle-keep interiors, additional structures, and downstream 3D implementation remain triage work.

## Approved packets

| Packet | Manifest | Approved authority |
| --- | --- | --- |
| Civic hall | `CivicHall/civic_hall_2d_manifest_v001.json` | Shared two-floor room layout, section, furniture zones, reusable shell/roof modules, layered exterior/interior finish tiles, and two realm exterior massing/material references |
| Fort gatehouse | `FortGatehouse/fort_gatehouse_2d_manifest_v001.json` | Shared enterable side-wing and upper-gallery layout, impassable central gate slot, section, and fort/castle additive modules |

Each manifest records repository-relative locators, byte lengths, SHA-256 hashes, and PNG dimensions. The two Meshy-generated civic-hall exterior references also record provider task IDs.

## Construction authority

- Author on a `0.5 m` snap grid using reusable structural panels rather than brick/block GameObjects.
- Keep foundations, floors, ceilings, solid/opening wall bays, corners, pillars, lintels, window frames, roof slopes/ridges/hips/gables/fascia, stairs, and rails as reusable source modules.
- Building shells contain **open apertures only**. Door leaves, frames, hinges, interaction, and door colliders are excluded and deferred to a separate family.
- Treat exterior skins as larger `1.5–3 m` realm-specific cladding and silhouette modules.
- Treat interior finishes as finer `0.5–1 m` wall, wainscot, floor, ceiling, corner, reveal, pillar-wrap, wear, and grime tiles.
- Preserve source modules for authoring, then combine static opaque geometry per room or exterior visibility cell and material atlas. Runtime must not retain one renderer, script, or collider per tile.
- Group glass/shutters separately and minimize transparent renderer count.
- Use simple room-shell, wall, opening, stair, and gate colliders; decorative tiles have no colliders.

## Civic-hall boundary

- The measured `9.5 m × 8.5 m × 6.8 m` two-floor layout is the spatial authority.
- Public hall, records, stores, steward office, service stair, council workroom, upper gallery, archives, staff landing, and upper council spaces must remain connected through the documented apertures.
- The Eldergrove and Crownlands images are **massing/material references only**. Visible door leaves and apparent monolithic walls in those images are not topology authority; the modular sheets and layout control construction.
- Realm-specific Stonehold and Umbral exterior references remain outside this approved package.

## Fort-gatehouse boundary

- Side guard/inspection wings, stairs, barracks, wallwalk access, and the upper control gallery are enterable spaces.
- Left and right stair bays align exactly with their upper landings.
- The central gate slot is physically impassable in the intact state. Teleport and enemy-breach behavior remain separate gameplay-owned systems.
- Building modules must never create a physical route through the intact gate by themselves.
- Fort-specific embrasure, parapet, merlon, portal frame, control beam, murder-hole ceiling, buttress, and breach-end-cap modules extend the shared civic structural kit.

## Validation and rollback

- Validate every manifest artifact by locator, byte length, SHA-256, and declared PNG dimensions.
- Validate unique artifact IDs and locators, approved status, gate policy, stair alignment, aperture count, and absence of new 3D jobs.
- This package is additive documentation/source authority. Rollback is deletion or one squash revert; no save migration or runtime compatibility action is required.
