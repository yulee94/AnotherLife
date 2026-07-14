package com.example.anotherlife.data.simulation

/**
 * Narrative Hooks for the World Atlas and Conflict Hints.
 * 
 * Defines how narrative state produces contextual hints on the world map.
 */
object WorldAtlasHooks {
    
    data class ConflictHint(
        val territoryId: String,
        val activeChapterId: String,
        val hintText: String,
        val severity: Severity = Severity.LOW
    )

    enum class Severity {
        LOW, MEDIUM, HIGH, CRITICAL
    }

    val activeHints = listOf(
        ConflictHint(
            "SKY_CASTLE",
            "CH0_PROLOGUE",
            "Faint celestial hum reported by observers.",
            Severity.LOW
        ),
        ConflictHint(
            "SKY_CASTLE",
            "C1_CL", // Crownlands Chapter 1
            "Vibrations intensifying; structural damage to the dais detected.",
            Severity.HIGH
        ),
        ConflictHint(
            "SILVER_WOODS",
            "C1_EG", // Eldergrove Chapter 1
            "Leaves at the border are turning obsidian black.",
            Severity.MEDIUM
        )
    )

    // Semantic Handoff: Control returns to narrative when a marker is clicked
    const val EVENT_MARKER_INSPECTED = "EVENT_SHOW_NARRATIVE_HINT_FOR_TERRITORY"
}
