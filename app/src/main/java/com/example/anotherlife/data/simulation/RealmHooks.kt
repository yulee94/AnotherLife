package com.example.anotherlife.data.simulation

/**
 * Narrative Hooks for Realm Selection.
 * 
 * Defines how choice of Realm shapes the initial narrative state and strategic identity.
 */
object RealmHooks {
    
    data class RealmNarrativeIdentity(
        val realmId: String,
        val theme: String,
        val initialAdvisorId: String,
        val startingFactionRep: Map<String, Int>,
        val traitDescription: String
    )

    val realmIdentities = listOf(
        RealmNarrativeIdentity(
            "Crownlands",
            "Ambition & Order",
            "npc_valerius",
            mapOf("faction_crownlands_radiant_council" to 50, "faction_eldergrove_wardens" to 0),
            "Enhanced trade networks (+15% Gold) and balanced military doctrine."
        ),
        RealmNarrativeIdentity(
            "Stonehold",
            "Resilience & Tradition",
            "npc_gruff",
            mapOf("faction_stonehold_assembly" to 50, "faction_umbral_cabal" to -10),
            "Mastery over earth (+20% Stone) and heavy defensive fortifications (+10% Def)."
        ),
        RealmNarrativeIdentity(
            "Eldergrove",
            "Harmony & Mystery",
            "npc_molly",
            mapOf("faction_eldergrove_wardens" to 50, "faction_crownlands_radiant_council" to 10),
            "Forest wisdom (+20% Wood) and arcane resonance (+15% Magic Power)."
        ),
        RealmNarrativeIdentity(
            "Umbral",
            "Cunning & Survival",
            "npc_xerath",
            mapOf("faction_umbral_cabal" to 50, "faction_eldergrove_wardens" to -20),
            "Shadow mastery (+20% Crit) and volcanic energy tapping (+15% Speed)."
        )
    )

    // Semantic Handoff: Triggered when realm is selected
    const val EVENT_REALM_SELECTED = "EVENT_SET_NARRATIVE_IDENTITY_BY_REALM"
}
