package com.example.anotherlife.data.simulation

/**
 * Reusable Templates for Narrative Authoring.
 * 
 * Enforces the strict data requirements defined in AGENTS.md.
 */
object NarrativeTemplates {

    data class QuestTemplate(
        val id: String,
        val titleKey: String,
        val descKey: String,
        val type: QuestType,
        val prerequisites: List<String> = emptyList(),
        val handoff: String? = null,
        val returnEvent: String? = null,
        val consequences: Map<String, List<String>> = emptyMap()
    )

    enum class QuestType {
        MAIN, SIDE, HIDDEN, WORLD
    }

    data class DialogueTemplate(
        val id: String,
        val speakerId: String,
        val textKey: String,
        val choices: List<ChoiceTemplate>
    )

    data class ChoiceTemplate(
        val textKey: String,
        val nextNodeId: String,
        val consequenceTriggers: List<String> = emptyList()
    )
}
