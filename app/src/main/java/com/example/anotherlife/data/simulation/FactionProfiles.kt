package com.example.anotherlife.data.simulation

/**
 * Authoritative Faction Data for Another Life.
 */
object FactionProfiles {
    
    val humanCouncil = Faction(
        id = "faction_crownlands_radiant_council",
        name = "The Radiant Council",
        description = "Governors of the Crownlands, focused on order and expansion.",
        reputation = 50,
        rivalFactions = listOf("faction_umbral_cabal")
    )

    val dwarvenForge = Faction(
        id = "faction_stonehold_assembly",
        name = "Stonehold Assembly",
        description = "Masters of the mountain deep, value resilience and tradition.",
        reputation = 50,
        alliedFactions = listOf("faction_crownlands_radiant_council")
    )

    val elvenGlade = Faction(
        id = "faction_eldergrove_wardens",
        name = "Eldergrove Wardens",
        description = "Keepers of the World Tree, prioritize harmony and arcane mystery.",
        reputation = 50,
        rivalFactions = listOf("faction_umbral_cabal")
    )

    val darkElfRift = Faction(
        id = "faction_umbral_cabal",
        name = "The Umbral Cabal",
        description = "Practitioners of shadow magic, driven by survival and cunning.",
        reputation = 30,
        rivalFactions = listOf("faction_crownlands_radiant_council", "faction_eldergrove_wardens")
    )

    val allFactions = listOf(humanCouncil, dwarvenForge, elvenGlade, darkElfRift)
}
