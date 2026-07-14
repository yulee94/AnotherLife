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

    val eldergroveNodes = listOf(
        DialogueNode(
            id = "DLG_EG_C1_START",
            characterName = "Molly",
            text = "The World Tree's song is discordant, My Lord. A sickly grey rot creeps from the border. We must purge the blight or lose the wood's blessing.",
            choices = listOf(
                DialogueChoice("Initiate the Purge.", "DLG_EG_C1_PURGE"),
                DialogueChoice("Search for the source first.", "DLG_EG_C1_SOURCE")
            )
        ),
        DialogueNode(
            id = "DLG_EG_C1_PURGE",
            characterName = "Molly",
            text = "I've prepared the purification rites. We'll need a vast amount of pure Mana Stones to fuel the cleansing.",
            choices = listOf(
                DialogueChoice("Begin the rites.", "end")
            )
        ),
        DialogueNode(
            id = "DLG_EG_C1_SOURCE",
            characterName = "Molly",
            text = "Searching for answers while the wood burns? Courageous, or perhaps foolhardy. We will send wardens to the 'Obsidian Peaks' border.",
            choices = listOf(
                DialogueChoice("Protect our borders.", "end")
            )
        )
    )

    val umbralNodes = listOf(
        DialogueNode(
            id = "DLG_UM_C1_START",
            characterName = "Xerath",
            text = "The Void is restless. The magma vents flare with an unnatural indigo light. If we do not stabilize the rift, the very earth will shatter.",
            choices = listOf(
                DialogueChoice("Stabilize the vents.", "DLG_UM_C1_STABILIZE"),
                DialogueChoice("Harness the energy instead.", "DLG_UM_C1_HARNESS")
            )
        ),
        DialogueNode(
            id = "DLG_UM_C1_STABILIZE",
            characterName = "Xerath",
            text = "A prudent decision. We shall use Dark Crystals to anchor the energies. It will cost much, but buy us safety.",
            choices = listOf(
                DialogueChoice("Proceed with caution.", "end")
            )
        ),
        DialogueNode(
            id = "DLG_UM_C1_HARNESS",
            characterName = "Xerath",
            text = "Dangerous, yet potentially lucrative. The power is immense. I will begin the extraction, but be warned: the Radiant Council will not be pleased.",
            choices = listOf(
                DialogueChoice("Power justifies the risk.", "end")
            )
        ),
        DialogueNode(
            id = "DLG_C1_RECOVERY_RESOURCE",
            characterName = "Advisor",
            text = "We lack the necessary materials to proceed, My Lord. I suggest focusing on our resource production or perhaps a tactical raid to replenish our stores.",
            choices = listOf(
                DialogueChoice("I will oversee the production.", "end")
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
        ),
        "DLG_EG_C1_PURGE" to listOf(
            "SET_QUEST_STATE:EG_BLIGHT_1:ACTIVE",
            "SET_REPUTATION:FACT_ELVEN_GLADE:+15",
            "SET_STRATEGIC_BIAS:ARCANE"
        ),
        "DLG_UM_C1_STABILIZE" to listOf(
            "SET_QUEST_STATE:UM_RIFT_1:ACTIVE",
            "SET_REPUTATION:FACT_DARK_ELF_RIFT:+15",
            "SET_STRATEGIC_BIAS:ECONOMIC"
        ),
        "DLG_UM_C1_HARNESS" to listOf(
            "SET_REPUTATION:FACT_HUMAN_COUNCIL:-25",
            "ADD_RESOURCE_MODIFIER:MANA_PRODUCTION:+20%",
            "SET_STRATEGIC_BIAS:SHADOW"
        )
    )
}
