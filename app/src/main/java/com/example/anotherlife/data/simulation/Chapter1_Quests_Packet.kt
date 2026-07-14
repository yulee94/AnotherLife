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
        ),
        QuestTemplate(
            id = "EG_BLIGHT_1",
            titleKey = "Purging the Blight",
            descKey = "Investigate the fungal spread at the World Tree border.",
            type = QuestType.MAIN,
            handoff = "HOOK_KINGDOM_BLIGHT_PURGE",
            objectives = listOf(
                QuestObjective("Gather 300 Mana Stones", 300),
                QuestObjective("Heal 5 Blighted Treants", 5)
            ),
            consequences = mapOf(
                "COMPLETION" to listOf("REPUTATION:FACT_ELVEN_GLADE:+20", "WOOD_PRODUCTION:+20%")
            )
        ),
        QuestTemplate(
            id = "UM_RIFT_1",
            titleKey = "Echoes of the Void",
            descKey = "Stabilize the unstable magma vents in the Umbral peaks.",
            type = QuestType.MAIN,
            handoff = "HOOK_KINGDOM_RIFT_STABILIZATION",
            objectives = listOf(
                QuestObjective("Gather 400 Dark Crystals", 400),
                QuestObjective("Seal 2 Void Leaks", 2)
            ),
            consequences = mapOf(
                "COMPLETION" to listOf("REPUTATION:FACT_DARK_ELF_RIFT:+20", "MANA_PRODUCTION:+10%")
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
