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
        val handoff: String? = null,
        val consequences: Map<String, List<String>> = emptyMap()
    )

    enum class QuestType {
        MAIN, SIDE, HIDDEN
    }
}
