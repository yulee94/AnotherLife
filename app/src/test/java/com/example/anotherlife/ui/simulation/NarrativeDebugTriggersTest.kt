package com.example.anotherlife.ui.simulation

import com.example.anotherlife.data.simulation.NarrativeState
import com.example.anotherlife.data.simulation.DialogueNode
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class NarrativeDebugTriggersTest {
    @Test
    fun triggerPreviewNodeShowsDialogueWithoutChangingAuthoritativeState() {
        val state = NarrativeState()
        val node = DialogueNode(
            id = "TEST_PREVIEW_NODE",
            characterName = "Preview",
            text = "Preview-only node.",
            choices = emptyList()
        )

        val triggered = NarrativeDebugTriggers.triggerPreviewNode(
            state = state,
            nodeId = node.id,
            previewNodes = listOf(node)
        )

        assertTrue(triggered)
        assertEquals(node.id, state.currentDialogue.value?.id)
        assertTrue(state.narrativeLog.isEmpty())
    }

    @Test
    fun missingPreviewNodeIsVisibleAndDoesNotMutateDialogue() {
        val state = NarrativeState()

        val triggered = NarrativeDebugTriggers.triggerPreviewNode(
            state = state,
            nodeId = "MISSING_NODE",
            previewNodes = emptyList()
        )

        assertFalse(triggered)
        assertNull(state.currentDialogue.value)
        assertEquals("Preview node not found: MISSING_NODE", NarrativeDebugTriggers.missingNodeMessage("MISSING_NODE"))
    }
}
