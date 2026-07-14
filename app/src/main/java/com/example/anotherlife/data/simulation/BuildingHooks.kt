package com.example.anotherlife.data.simulation

/**
 * Narrative Hooks for Kingdom Buildings and Research.
 * 
 * Defines how structural progression unlocks new story chapters and choices.
 */
object BuildingHooks {
    
    data class ProgressionHook(
        val sourceId: String,
        val requiredLevel: Int,
        val unlockDialogueId: String,
        val unlockQuestId: String? = null
    )

    val buildingHooks = listOf(
        ProgressionHook("barracks", 5, "DLG_VALERIUS_ELITE_GUARD", "QUEST_ELITE_TRIAL"),
        ProgressionHook("academy", 5, "DLG_MOLLY_ARCANE_BREACH", "QUEST_PORTAL_STABILIZATION"),
        ProgressionHook("forge", 5, "DLG_GRUFF_MASTERWORK", "QUEST_LEGENDARY_HAMMER"),
        ProgressionHook("gold_mine", 10, "DLG_KING_TREASURY_FULL")
    )

    val researchHooks = listOf(
        ProgressionHook("steel_forging", 2, "DLG_VALERIUS_BETTER_BLADES"),
        ProgressionHook("arcane_study", 3, "DLG_XERATH_VOID_INSIGHT")
    )

    // Semantic Return: Triggered when a building/research reaches a target level
    const val EVENT_PROGRESSION_REACHED = "EVENT_TRIGGER_NARRATIVE_HOOK_BY_LEVEL"
}
