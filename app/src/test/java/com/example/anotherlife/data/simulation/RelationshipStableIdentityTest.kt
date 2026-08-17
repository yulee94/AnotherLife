package com.example.anotherlife.data.simulation

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class RelationshipStableIdentityTest {
    @Test
    fun advisorAndFactionFixturesUseCatalogIdsWhileLabelsStayUnchanged() {
        assertEquals("npc_valerius", AdvisorPersonas.valerius.id)
        assertEquals("Captain Valerius", AdvisorPersonas.valerius.name)
        assertEquals("npc_gruff", AdvisorPersonas.gruff.id)
        assertEquals("Master Gruff", AdvisorPersonas.gruff.name)

        assertEquals("faction_crownlands_radiant_council", FactionProfiles.humanCouncil.id)
        assertEquals("The Radiant Council", FactionProfiles.humanCouncil.name)
        assertEquals("faction_umbral_cabal", FactionProfiles.darkElfRift.id)
        assertEquals("The Umbral Cabal", FactionProfiles.darkElfRift.name)
    }

    @Test
    fun relationshipReferencesAcrossPreviewContentUseCatalogIds() {
        assertEquals("npc_valerius", NVS_01_Packet.ADVISOR_ID)
        assertTrue(
            NVS_01_Packet.consequences.values.flatten()
                .contains("SET_AFFINITY:npc_valerius:+5")
        )
        assertEquals("npc_valerius", Chapter1_Packet.NPC_VALERIUS)
        assertTrue(Chapter1_Packet.strategicGoals.containsKey("npc_valerius"))
        assertFalse(Chapter1_Packet.strategicGoals.containsKey("ADVISOR_VALERIUS"))

        val crownlands = RealmHooks.realmIdentities.first { it.realmId == "Crownlands" }
        assertEquals("npc_valerius", crownlands.initialAdvisorId)
        assertTrue(crownlands.startingFactionRep.containsKey("faction_crownlands_radiant_council"))
        assertFalse(crownlands.startingFactionRep.containsKey("HUMAN_COUNCIL"))

        assertTrue(
            Chapter1_Quests_Packet.rebuildQuests.first().consequences
                .getValue("COMPLETION")
                .contains("REPUTATION:faction_crownlands_radiant_council:+20")
        )
    }
}
