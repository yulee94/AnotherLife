package com.example.anotherlife.data.simulation

import androidx.compose.runtime.mutableStateOf

data class DialogueChoice(
    val text: String,
    val nextNodeId: String
)

data class DialogueNode(
    val id: String,
    val characterName: String,
    val text: String,
    val choices: List<DialogueChoice>
)

class NarrativeState {
    var currentDialogue = mutableStateOf<DialogueNode?>(null)
}
