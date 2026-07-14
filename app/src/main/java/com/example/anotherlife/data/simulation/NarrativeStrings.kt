package com.example.anotherlife.data.simulation

/**
 * Authoritative Localization Mapping for Narrative Content.
 * 
 * Provides a central registry for all player-facing text, mapped to stable IDs.
 */
object NarrativeStrings {
    
    val strings = mapOf(
        // General UI
        "ui.dossier.title" to "Command Dossier",
        "ui.dossier.subtitle" to "Strategic Overview and Narrative Intelligence",
        "ui.quest.title" to "Royal Quests & Milestones",
        
        // Advisor Roles
        "advisor.VALERIUS.role" to "Military Commander",
        "advisor.GRUFF.role" to "Chief Architect & Smith",
        "advisor.MOLLY.role" to "Royal Archivist & Innkeeper",
        "advisor.XERATH.role" to "Void Seer",
        
        // Chapter 1 Dialogue
        "dialogue.DLG_CL_C1_START.text" to "The anomaly at the Sky Castle was just the beginning. Our capital, the Crownlands, is in disarray. We must rebuild the walls before the Umbral Cabal senses our weakness.",
        "dialogue.DLG_SH_C1_START.text" to "Bah! The magma pumps are clogged with ash. Without them, the Deep Forge is just a cold cave. We dwarves can't work in the cold, Lord.",
        "dialogue.DLG_EG_C1_START.text" to "The World Tree's song is discordant, My Lord. A sickly grey rot creeps from the border. We must purge the blight or lose the wood's blessing.",
        "dialogue.DLG_UM_C1_START.text" to "The Void is restless. The magma vents flare with an unnatural indigo light. If we do not stabilize the rift, the very earth will shatter.",
        
        // Quest Titles
        "quest.CL_REBUILD_1.title" to "Restoring the Crown",
        "quest.SH_FORGE_1.title" to "Igniting the Deep",
        "quest.EG_BLIGHT_1.title" to "Purging the Blight",
        "quest.UM_RIFT_1.title" to "Echoes of the Void",
        
        // Faction Names
        "faction.FACT_HUMAN_COUNCIL.name" to "The Radiant Council",
        "faction.FACT_DWARVEN_FORGE.name" to "Stonehold Assembly",
        "faction.FACT_ELVEN_GLADE.name" to "Eldergrove Wardens",
        "faction.FACT_DARK_ELF_RIFT.name" to "The Umbral Cabal"
    )

    fun get(key: String): String = strings[key] ?: "[MISSING_STRING: $key]"
}
