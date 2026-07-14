package com.example.anotherlife.data.simulation

/**
 * Narrative Arcs and Loyalty Milestones for Advisors.
 */
object AdvisorArcs {
    
    data class LoyaltyMilestone(
        val affinityThreshold: Int,
        val rewardDescription: String,
        val unlockDialogueId: String
    )

    val valeriusArc = listOf(
        LoyaltyMilestone(70, "Unlocks 'Elite Paladin' training.", "DLG_VALERIUS_LOYALTY_1"),
        LoyaltyMilestone(90, "Grants the 'Golden Aegis' artifact.", "DLG_VALERIUS_LOYALTY_2")
    )

    val gruffArc = listOf(
        LoyaltyMilestone(70, "Unlocks 'Heavy Siege Engine' blueprints.", "DLG_GRUFF_LOYALTY_1"),
        LoyaltyMilestone(90, "Grants the 'First King's Anvil' relic.", "DLG_GRUFF_LOYALTY_2")
    )

    val mollyArc = listOf(
        LoyaltyMilestone(70, "Unlocks 'World Sap' harvesting efficiency.", "DLG_MOLLY_LOYALTY_1"),
        LoyaltyMilestone(90, "Grants the 'Archivist's monocle'.", "DLG_MOLLY_LOYALTY_2")
    )

    val xerathArc = listOf(
        LoyaltyMilestone(70, "Unlocks 'Void Walk' tactical movement.", "DLG_XERATH_LOYALTY_1"),
        LoyaltyMilestone(90, "Grants the 'Void Eye' crystal.", "DLG_XERATH_LOYALTY_2")
    )
}
