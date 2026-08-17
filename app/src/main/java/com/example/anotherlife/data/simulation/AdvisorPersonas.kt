package com.example.anotherlife.data.simulation

/**
 * Authoritative Persona Data for Chapter 1 Advisors.
 */
object AdvisorPersonas {
    
    val valerius = Persona(
        id = "npc_valerius",
        name = "Captain Valerius",
        role = "Military Commander",
        strategicBias = StrategicBias.MILITARY,
        emotionalTone = "Stoic & Disciplined",
        affinity = 60
    )

    val gruff = Persona(
        id = "npc_gruff",
        name = "Master Gruff",
        role = "Chief Architect & Smith",
        strategicBias = StrategicBias.ECONOMIC,
        emotionalTone = "Gruff & Pragmatic",
        affinity = 50
    )

    val molly = Persona(
        id = "npc_molly",
        name = "Molly",
        role = "Royal Archivist & Innkeeper",
        strategicBias = StrategicBias.DIPLOMATIC,
        emotionalTone = "Warm & Observant",
        affinity = 55
    )

    val xerath = Persona(
        id = "npc_xerath",
        name = "Xerath",
        role = "Void Seer",
        strategicBias = StrategicBias.SHADOW,
        emotionalTone = "Enigmatic & Calculating",
        affinity = 40
    )

    val allAdvisors = listOf(valerius, gruff, molly, xerath)
}
