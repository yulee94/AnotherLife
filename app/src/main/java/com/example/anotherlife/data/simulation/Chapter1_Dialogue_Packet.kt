package com.example.anotherlife.data.simulation

/**
 * Authoritative Dialogue Nodes for Chapter 1: The Proof of Worth.
 */
object Chapter1_Dialogue_Packet {
    
    val crownlandsNodes = listOf(
        DialogueNode(
            id = "DLG_CL_C1_START",
            characterName = "Captain Valerius",
            text = "The anomaly at the Sky Castle was just the beginning. Our capital, the Crownlands, is in disarray. We must rebuild the walls before the Umbral Cabal senses our weakness.",
            choices = listOf(
                DialogueChoice("Prioritize the wall repairs.", "DLG_CL_C1_WALLS"),
                DialogueChoice("Focus on scouting the border.", "DLG_CL_C1_SCOUT")
            )
        ),
        DialogueNode(
            id = "DLG_CL_C1_WALLS",
            characterName = "Captain Valerius",
            text = "A wise choice. Security first. I have drafted the blueprints. We will need significant Stone and Gold.",
            choices = listOf(
                DialogueChoice("Start the reconstruction.", "end")
            )
        ),
        DialogueNode(
            id = "DLG_CL_C1_SCOUT",
            characterName = "Captain Valerius",
            text = "Offense is a form of defense, I suppose. But the people will feel exposed. We will scout the 'Shadow Vale' border.",
            choices = listOf(
                DialogueChoice("Keep me informed.", "end")
            )
        )
    )

    val stoneholdNodes = listOf(
        DialogueNode(
            id = "DLG_SH_C1_START",
            characterName = "Master Gruff",
            text = "Bah! The magma pumps are clogged with ash. Without them, the Deep Forge is just a cold cave. We dwarves can't work in the cold, Lord.",
            choices = listOf(
                DialogueChoice("Let's clear those pumps.", "DLG_SH_C1_PUMPS"),
                DialogueChoice("Can't we use alternative fuel?", "DLG_SH_C1_FUEL")
            )
        ),
        DialogueNode(
            id = "DLG_SH_C1_PUMPS",
            characterName = "Master Gruff",
            text = "That's the spirit! It'll be dangerous work. The steam pressure alone could melt a beard off.",
            choices = listOf(
                DialogueChoice("Get it done.", "end")
            )
        ),
        DialogueNode(
            id = "DLG_SH_C1_FUEL",
            characterName = "Master Gruff",
            text = "Alternative? In Stonehold? You've been spending too much time with the Elves, Lord. Magma is the only way.",
            choices = listOf(
                DialogueChoice("Clear the pumps then.", "DLG_SH_C1_PUMPS")
            )
        )
    )

    // Consequences for Chapter 1 Choices
    val consequences = mapOf(
        "DLG_CL_C1_WALLS" to listOf(
            "SET_QUEST_STATE:CL_REBUILD_1:ACTIVE",
            "SET_REPUTATION:FACT_HUMAN_COUNCIL:+10",
            "SET_STRATEGIC_BIAS:MILITARY"
        ),
        "DLG_CL_C1_SCOUT" to listOf(
            "SET_REPUTATION:FACT_DARK_ELF_RIFT:-20",
            "SET_STRATEGIC_BIAS:DIPLOMATIC",
            "UNLOCK_WORLD_MAP_MARKER:SHADOW_VALE_BORDER"
        ),
        "DLG_SH_C1_PUMPS" to listOf(
            "SET_QUEST_STATE:SH_FORGE_1:ACTIVE",
            "SET_REPUTATION:FACT_DWARVEN_FORGE:+15",
            "ADD_RESOURCE_MODIFIER:ORE_PRODUCTION:+10%"
        )
    )
}
