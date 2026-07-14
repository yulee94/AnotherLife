package com.example.anotherlife.data.simulation

/**
 * Authoritative Persona Data for Chapter 1 Advisors.
 */
object AdvisorPersonas {
    
    val valerius = Persona(
        id = "ADVISOR_VALERIUS",
        name = "Captain Valerius",
        role = "Military Commander",
        strategicBias = StrategicBias.MILITARY,
        emotionalTone = "Stoic & Disciplined",
        affinity = 60
    )

    val gruff = Persona(
        id = "ADVISOR_GRUFF",
        name = "Master Gruff",
        role = "Chief Architect & Smith",
        strategicBias = StrategicBias.ECONOMIC,
        emotionalTone = "Gruff & Pragmatic",
        affinity = 50
    )

    val molly = Persona(
        id = "ADVISOR_MOLLY",
        name = "Molly",
        role = "Royal Archivist & Innkeeper",
        strategicBias = StrategicBias.DIPLOMATIC,
        emotionalTone = "Warm & Observant",
        affinity = 55
    )

    val xerath = Persona(
        id = "ADVISOR_XERATH",
        name = "Xerath",
        role = "Void Seer",
        strategicBias = StrategicBias.SHADOW,
        emotionalTone = "Enigmatic & Calculating",
        affinity = 40
    )

    val allAdvisors = listOf(valerius, gruff, molly, xerath)
}
