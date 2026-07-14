package com.example.anotherlife.data.simulation

/**
 * Narrative Metadata for the Command Dossier UI.
 * 
 * Defines how narrative state is visualized for the player's strategic overview.
 */
object DossierNarrative {
    
    data class DossierEntry(
        val category: Category,
        val title: String,
        val value: String,
        val trend: String? = null,
        val localizationKey: String
    )

    enum class Category {
        CHAPTER_PROGRESS,
        ADVISOR_STATUS,
        REPUTATION,
        WORLD_EVENTS
    }

    // Default Metadata for Chapter 0
    val initialEntries = listOf(
        DossierEntry(
            Category.CHAPTER_PROGRESS,
            "Current Chapter",
            "Prologue: The Awakening",
            null,
            "dossier.chapter.name"
        ),
        DossierEntry(
            Category.ADVISOR_STATUS,
            "Military Advisor",
            "Captain Valerius (Loyal)",
            "Affinity: 60",
            "dossier.advisor.valerius"
        ),
        DossierEntry(
            Category.WORLD_EVENTS,
            "Active Anomaly",
            "Sky Castle Vibrations",
            "Status: Unstable",
            "dossier.event.sky_castle"
        )
    )

    val strategicLogs = listOf(
        "Day 1: The kingdom awakens to the sound of celestial vibrations.",
        "Day 2: Captain Valerius has established the forward command post.",
        "Day 3: Master Gruff reports critical shortage of Deep Ore for forge ignition.",
        "Day 5: Observers report a widening of the rift above the Obsidian Citadel."
    )

    const val LOG_TITLE = "Strategic Narrative Log"
    const val EMPTY_LOG_MESSAGE = "No significant events recorded."
}
