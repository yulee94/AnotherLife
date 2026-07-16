package com.example.anotherlife.ui.simulation

import com.example.anotherlife.data.simulation.NVS_01_Packet
import com.example.anotherlife.data.simulation.NarrativeState

object NarrativeDebugTriggers {
    fun triggerPreviewNode(
        state: NarrativeState,
        nodeId: String,
        previewNodes: List<com.example.anotherlife.data.simulation.DialogueNode> = NVS_01_Packet.storyNodes
    ): Boolean {
        val node = previewNodes.firstOrNull { it.id == nodeId } ?: return false
        state.currentDialogue.value = node
        return true
    }

    fun missingNodeMessage(nodeId: String): String {
        return "Preview node not found: $nodeId"
    }
}
