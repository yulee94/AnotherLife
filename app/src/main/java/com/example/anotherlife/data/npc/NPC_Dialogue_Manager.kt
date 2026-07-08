package com.example.anotherlife.data.npc

import androidx.compose.runtime.mutableStateOf
import com.example.anotherlife.data.simulation.DialogueNode
import com.example.anotherlife.data.simulation.DialogueChoice

/**
 * Manages non-story NPC interactions and rumors within the kingdom.
 */
class NPC_Dialogue_Manager {
    var activeNpcDialogue = mutableStateOf<DialogueNode?>(null)

    fun startInteraction(npcId: String) {
        activeNpcDialogue.value = when(npcId) {
            "Blacksmith" -> DialogueNode(
                id = "npc_smith",
                characterName = "Master Gruff",
                text = "Looking to sharpen your blade, Lord? Or maybe you're here about the lack of coal?",
                choices = listOf(
                    DialogueChoice("Tell me about the coal.", "quest_coal"),
                    DialogueChoice("Just browsing.", "end")
                )
            )
            "Innkeeper" -> DialogueNode(
                id = "npc_inn",
                characterName = "Molly",
                text = "Welcome! The travelers speak of strange lights in the eastern woods. Interested?",
                choices = listOf(
                    DialogueChoice("Strange lights?", "quest_lights"),
                    DialogueChoice("Not today.", "end")
                )
            )
            else -> null
        }
    }

    fun closeInteraction() {
        activeNpcDialogue.value = null
    }
}
