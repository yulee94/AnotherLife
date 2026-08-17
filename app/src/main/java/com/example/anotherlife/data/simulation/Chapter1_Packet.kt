package com.example.anotherlife.data.simulation

/**
 * Authoritative Narrative Packet for Chapter 1: The Proof of Worth
 * 
 * Purpose: Transition from Prologue to Realm-specific content.
 */
object Chapter1_Packet {
    const val MILESTONE_ID = "CH1-SPINE"
    
    val realmChapters = mapOf(
        "Crownlands" to ChapterData(
            id = "C1_CL",
            title = "The King's Decree",
            lore = "Rebuilding the capital and seeking the blessing of Aurelius the Gold Dragon.",
            initialNpc = "npc_valerius",
            mainQuestChain = listOf("OMEN_1", "CL_REBUILD_1", "CL_DRAGON_TRIAL")
        ),
        "Stonehold" to ChapterData(
            id = "C1_SH",
            title = "The Echoes of Iron",
            lore = "Re-opening the ancestral Deep Forge and defeating Ferrum the Iron Dragon.",
            initialNpc = "npc_gruff",
            mainQuestChain = listOf("OMEN_1", "SH_FORGE_1", "SH_DRAGON_TRIAL")
        ),
        "Eldergrove" to ChapterData(
            id = "C1_EG",
            title = "Whispers of the Sapling",
            lore = "Investigating a blight on the World Tree and purging Virens the Blighted Dragon.",
            initialNpc = "npc_molly",
            mainQuestChain = listOf("OMEN_1", "EG_BLIGHT_1", "EG_DRAGON_TRIAL")
        ),
        "Umbral" to ChapterData(
            id = "C1_UM",
            title = "Shadows of the Void",
            lore = "Rituals to stabilize the volcanic rifts and taming Nox the Void Dragon.",
            initialNpc = "npc_xerath",
            mainQuestChain = listOf("OMEN_1", "UM_RIFT_1", "UM_DRAGON_TRIAL")
        )
    )

    data class ChapterData(
        val id: String,
        val title: String,
        val lore: String,
        val initialNpc: String,
        val mainQuestChain: List<String>
    )

    // Strategic Narrative Goals for Chapter 1
    val strategicGoals = mapOf(
        "npc_valerius" to "Consolidate defense of the capital (+10% Wall Durability)",
        "npc_gruff" to "Re-ignite the ancestral forge (+15% Ore Production)",
        "npc_molly" to "Purge the blight from the World Tree (+20% Wood Production)",
        "npc_xerath" to "Stabilize the volcanic rifts (+10% Mana Stone yield)"
    )

    // NPC Arcs & Consequences
    data class NpcArc(
        val advisorId: String,
        val loyaltyMilestones: Map<Int, String>, // Loyalty Level -> Consequence
        val conflictTriggers: List<String>
    )

    val npcArcs = listOf(
        NpcArc(
            NPC_VALERIUS,
            mapOf(50 to "UNLOCK_ELITE_PALADINS", 100 to "GRANT_GOLDEN_AEGIS"),
            listOf("NEGLECT_WALL_REPAIRS", "ALLIANCE_WITH_UMBRAL")
        ),
        NpcArc(
            NPC_GRUFF,
            mapOf(50 to "UNLOCK_HEAVY_SIEGE", 100 to "GRANT_FIRST_KINGS_ANVIL"),
            listOf("EXPORT_DEEP_ORE", "PEACE_TREATY_WITH_ELVES")
        )
    )

    // Chapter Close Conditions
    const val CLOSE_CONDITION_QUEST_CHAIN = "ALL_MAIN_QUESTS_COMPLETED"
    const val CLOSE_CONDITION_BOSS = "BOSS_DRAGON_TRIAL_DEFEATED"
    const val NEXT_CHAPTER_ID = "CH2_THE_TREASURE_HUNT"

    // Key NPC IDs for Chapter 1
    const val NPC_VALERIUS = "npc_valerius"
    const val NPC_GRUFF = "npc_gruff"
    const val NPC_MOLLY = "npc_molly"
    const val NPC_XERATH = "npc_xerath"
}
