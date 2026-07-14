package com.example.anotherlife.data.simulation

/**
 * Detailed Quest Data for Chapter 1 Spine.
 */
object Chapter1_Quests_Packet {
    
    val rebuildQuests = listOf(
        QuestTemplate(
            id = "CL_REBUILD_1",
            titleKey = "Restoring the Crown",
            descKey = "Repair the city walls and restore public order.",
            type = QuestType.MAIN,
            handoff = "HOOK_KINGDOM_WALL_REPAIR",
            objectives = listOf(
                QuestObjective("Gather 500 Stone", 500),
                QuestObjective("Assign 50 Troops to guard the gates", 50)
            ),
            consequences = mapOf(
                "COMPLETION" to listOf("REPUTATION:FACT_HUMAN_COUNCIL:+20", "UNSTABLE_CELESTIAL_SIGNAL")
            )
        ),
        QuestTemplate(
            id = "SH_FORGE_1",
            titleKey = "Igniting the Deep",
            descKey = "Re-activate the ancestral magma pumps of Stonehold.",
            type = QuestType.MAIN,
            handoff = "HOOK_KINGDOM_FORGE_PUMP",
            objectives = listOf(
                QuestObjective("Acquire 100 Deep Ore", 100),
                QuestObjective("Defeat 3 Rogue Ash Golems", 3)
            ),
            consequences = mapOf(
                "COMPLETION" to listOf("REPUTATION:FACT_DWARVEN_FORGE:+20", "ORE_PRODUCTION:+15%")
            )
        )
    )

    data class QuestTemplate(
        val id: String,
        val titleKey: String,
        val descKey: String,
        val type: QuestType,
        val objectives: List<QuestObjective> = emptyList(),
        val handoff: String? = null,
        val consequences: Map<String, List<String>> = emptyMap()
    )

    data class QuestObjective(val description: String, val targetValue: Int)

    enum class QuestType {
        MAIN, SIDE, HIDDEN
    }
}
