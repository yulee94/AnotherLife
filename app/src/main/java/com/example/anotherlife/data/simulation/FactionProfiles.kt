package com.example.anotherlife.data.simulation

/**
 * Authoritative Faction Data for Another Life.
 */
object FactionProfiles {
    
    val humanCouncil = Faction(
        id = "FACT_HUMAN_COUNCIL",
        name = "The Radiant Council",
        description = "Governors of the Crownlands, focused on order and expansion.",
        reputation = 50,
        rivalFactions = listOf("FACT_DARK_ELF_RIFT")
    )

    val dwarvenForge = Faction(
        id = "FACT_DWARVEN_FORGE",
        name = "Stonehold Assembly",
        description = "Masters of the mountain deep, value resilience and tradition.",
        reputation = 50,
        alliedFactions = listOf("FACT_HUMAN_COUNCIL")
    )

    val elvenGlade = Faction(
        id = "FACT_ELVEN_GLADE",
        name = "Eldergrove Wardens",
        description = "Keepers of the World Tree, prioritize harmony and arcane mystery.",
        reputation = 50,
        rivalFactions = listOf("FACT_DARK_ELF_RIFT")
    )

    val darkElfRift = Faction(
        id = "FACT_DARK_ELF_RIFT",
        name = "The Umbral Cabal",
        description = "Practitioners of shadow magic, driven by survival and cunning.",
        reputation = 30,
        rivalFactions = listOf("FACT_HUMAN_COUNCIL", "FACT_ELVEN_GLADE")
    )

    val allFactions = listOf(humanCouncil, dwarvenForge, elvenGlade, darkElfRift)
}
