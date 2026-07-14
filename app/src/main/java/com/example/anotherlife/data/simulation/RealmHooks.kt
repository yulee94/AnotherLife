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
            "ADVISOR_VALERIUS",
            mapOf("HUMAN_COUNCIL" to 50, "ELVEN_GLADE" to 0),
            "Enhanced trade networks (+15% Gold) and balanced military doctrine."
        ),
        RealmNarrativeIdentity(
            "Stonehold",
            "Resilience & Tradition",
            "ADVISOR_GRUFF",
            mapOf("DWARVEN_FORGE" to 50, "DARK_ELF_RIFT" to -10),
            "Mastery over earth (+20% Stone) and heavy defensive fortifications (+10% Def)."
        ),
        RealmNarrativeIdentity(
            "Eldergrove",
            "Harmony & Mystery",
            "ADVISOR_MOLLY",
            mapOf("ELVEN_GLADE" to 50, "HUMAN_COUNCIL" to 10),
            "Forest wisdom (+20% Wood) and arcane resonance (+15% Magic Power)."
        ),
        RealmNarrativeIdentity(
            "Umbral",
            "Cunning & Survival",
            "ADVISOR_XERATH",
            mapOf("DARK_ELF_RIFT" to 50, "ELVEN_GLADE" to -20),
            "Shadow mastery (+20% Crit) and volcanic energy tapping (+15% Speed)."
        )
    )

    // Semantic Handoff: Triggered when realm is selected
    const val EVENT_REALM_SELECTED = "EVENT_SET_NARRATIVE_IDENTITY_BY_REALM"
}
