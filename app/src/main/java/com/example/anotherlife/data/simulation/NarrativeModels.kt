package com.example.anotherlife.data.simulation

import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateMapOf
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

data class Persona(
    val id: String,
    val name: String,
    val role: String,
    val strategicBias: StrategicBias,
    val emotionalTone: String,
    var affinity: Int = 50,
    var loyaltyLevel: Int = 0
)

enum class StrategicBias {
    MILITARY, ECONOMIC, DIPLOMATIC, ARCANE, SHADOW
}

data class Faction(
    val id: String,
    val name: String,
    val description: String,
    var reputation: Int = 0,
    val alliedFactions: List<String> = emptyList(),
    val rivalFactions: List<String> = emptyList()
)

class NarrativeState {
    var currentDialogue = mutableStateOf<DialogueNode?>(null)
    
    val advisors = mutableStateListOf<Persona>()
    val factions = mutableStateListOf<Faction>()
    
    val narrativeLog = mutableStateListOf<String>()
    
    var currentChapterId = mutableStateOf("CH0_PROLOGUE")
}
