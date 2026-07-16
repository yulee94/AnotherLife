package com.example.anotherlife.data.simulation

import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Validates the integrity of authored narrative packets.
 * 
 * Enforces:
 * 1. Global ID uniqueness.
 * 2. Dialogue branch resolution (all NEXT_NODE_IDs must exist or be 'end').
 */
class NarrativeIntegrityTest {

    @Test
    fun dialogueBranchesResolveCorrectly() {
        val allPackets = listOf(
            NVS_01_Packet.storyNodes
        ).flatten()

        val allNodeIds = allPackets.map { it.id }.toSet()

        for (node in allPackets) {
            for (choice in node.choices) {
                val targetId = choice.nextNodeId
                if (targetId != "end") {
                    assertTrue(
                        "Dialogue Node [${node.id}] references non-existent node [$targetId]",
                        allNodeIds.contains(targetId)
                    )
                }
            }
        }
    }

    @Test
    fun questIdsAreUnique() {
        val allQuestIds = mutableListOf<String>()
        allQuestIds.add(NVS_01_Packet.QUEST_ID)

        val duplicates = allQuestIds.groupBy { it }.filter { it.value.size > 1 }
        
        assertTrue(
            "Duplicate Quest IDs found: ${duplicates.keys}",
            duplicates.isEmpty()
        )
    }

    @Test
    fun advisorIdsAreConsistent() {
        val advisorIds = AdvisorPersonas.allAdvisors.map { it.id }.toSet()
        assertTrue(advisorIds.contains(NVS_01_Packet.ADVISOR_ID))
    }
}
