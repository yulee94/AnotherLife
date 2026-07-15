package com.example.anotherlife.data.simulation

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class QuestModelTest {
    @Test
    fun metadataDefaultsKeepExistingQuestConstructionCompatible() {
        val quest = Quest(
            id = "Q_TEST",
            title = "Test Quest",
            description = "Verifies metadata defaults.",
            target = 1
        )

        assertEquals(QuestMode.Kingdom, quest.mode)
        assertNull(quest.mapMarkerId)
    }

    @Test
    fun metadataSupportsArenaModeAndMapMarker() {
        val quest = Quest(
            id = "Q_ARENA",
            title = "Arena Quest",
            description = "Verifies explicit metadata.",
            target = 1,
            mode = QuestMode.Arena3D,
            mapMarkerId = "arena_gate"
        )

        assertEquals(QuestMode.Arena3D, quest.mode)
        assertEquals("arena_gate", quest.mapMarkerId)
    }

    @Test
    fun legacyBooleanSlotsRemainBeforeQuestMetadata() {
        val quest = Quest("Q_COMPAT", "Title", "Description", 0, 1, true, true)

        assertEquals(true, quest.isCompleted)
        assertEquals(true, quest.isClaimed)
        assertEquals(QuestMode.Kingdom, quest.mode)
        assertNull(quest.mapMarkerId)
    }
}
