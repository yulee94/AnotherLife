package com.example.anotherlife.data.simulation

/**
 * Authoritative Narrative Packet for Milestone NVS-01
 * 
 * Purpose: First end-to-end quest loop bridging Kingdom and Arena.
 * Quest: OMEN_1 "The First Signal"
 * 
 * CONTINUITY NOTES:
 * This quest serves as the player's first introduction to the Sky Castle and the Celestial vibrations.
 * It establishes Captain Valerius as the primary military advisor.
 */
object NVS_01_Packet {
    const val MILESTONE_ID = "NVS-01"
    const val CHAPTER_ID = "CH0_PROLOGUE"
    const val QUEST_ID = "OMEN_1"
    
    // Stable IDs for handoff
    const val ADVISOR_ID = "ADVISOR_VALERIUS"
    const val ARENA_HOOK_ID = "HOOK_SKY_CASTLE_ARENA"
    const val REWARD_ARTIFACT_ID = "REW_OMEN_1_TEAR"
    
    // Semantic Gameplay Handoff
    const val GAMEPLAY_HANDOFF_REQUEST = "EXECUTE_ARENA_ENCOUNTER:HOOK_SKY_CASTLE_ARENA"
    const val GAMEPLAY_RETURN_EVENT = "EVENT_ARENA_ENCOUNTER_SUCCESS:HOOK_SKY_CASTLE_ARENA"

    // Prerequisites & Entry Conditions
    val prerequisites = listOf("NONE") // Starting quest
    val entryConditions = "CHAPTER_ID == CH0_PROLOGUE"
    val unlockRules = "AUTOMATIC_ON_START"

    // Quest States
    enum class State {
        INACTIVE,
        TALK_TO_VALERIUS,
        INVESTIGATE_SKY_CASTLE, // Arena Handoff State
        REPORT_TO_VALERIUS,
        COMPLETED,
        FAILED
    }

    // Objective Definitions
    val objectives = listOf(
        Objective("OBJ_OMEN_1_TALK", "Speak with Captain Valerius in the Command Center."),
        Objective("OBJ_OMEN_1_ARENA", "Investigate the celestial anomaly at the Sky Castle."),
        Objective("OBJ_OMEN_1_REPORT", "Deliver the Celestial Tear to Valerius.")
    )

    data class Objective(val id: String, val description: String)

    val storyNodes = listOf(
        DialogueNode(
            id = "DLG_OMEN_1_START",
            characterName = "Captain Valerius",
            text = "My Lord, the observers at the Sky Castle report strange vibrations. The very air seems to hum. Will you investigate?",
            choices = listOf(
                DialogueChoice("I will investigate personally.", "DLG_OMEN_1_GO"),
                DialogueChoice("Tell me more first.", "DLG_OMEN_1_LORE")
            )
        ),
        DialogueNode(
            id = "DLG_OMEN_1_LORE",
            characterName = "Captain Valerius",
            text = "The vibrations match the ancient texts regarding the 'Opening of the Veil'. It is a sign we cannot ignore.",
            choices = listOf(
                DialogueChoice("Then I must go.", "DLG_OMEN_1_GO")
            )
        ),
        DialogueNode(
            id = "DLG_OMEN_1_GO",
            characterName = "Captain Valerius",
            text = "As you command. The Sky Castle awaits your arrival. Be prepared for anything.",
            choices = listOf(
                DialogueChoice("[Transition to Arena]", "DLG_OMEN_1_ARENA_START")
            )
        ),
        DialogueNode(
            id = "DLG_OMEN_1_SUCCESS",
            characterName = "Captain Valerius",
            text = "You returned! And with a fragment of the anomaly. This 'Celestial Tear' will be vital for our research.",
            choices = listOf(
                DialogueChoice("The kingdom is safe for now.", "end")
            )
        ),
        DialogueNode(
            id = "DLG_OMEN_1_FAILURE",
            characterName = "Captain Valerius",
            text = "The vibrations were too strong, My Lord. Your safety is paramount. We must regroup and try again when you are ready.",
            choices = listOf(
                DialogueChoice("I will return soon.", "end")
            )
        )
    )

    // Consequences & Transitions
    // Format: "DIALOGUE_ID" -> List of "COMMAND:PAYLOAD"
    val consequences = mapOf(
        "DLG_OMEN_1_GO" to listOf(
            "SET_AFFINITY:ADVISOR_VALERIUS:+5",
            "TRANSITION_STATE:INVESTIGATE_SKY_CASTLE",
            "REQUEST_HANDOFF:HOOK_SKY_CASTLE_ARENA"
        ),
        "EVENT_ARENA_ENCOUNTER_SUCCESS:HOOK_SKY_CASTLE_ARENA" to listOf(
            "TRANSITION_STATE:REPORT_TO_VALERIUS",
            "TRIGGER_DIALOGUE:DLG_OMEN_1_SUCCESS"
        ),
        "EVENT_ARENA_ENCOUNTER_FAILURE:HOOK_SKY_CASTLE_ARENA" to listOf(
            "TRANSITION_STATE:TALK_TO_VALERIUS", // Recovery point
            "TRIGGER_DIALOGUE:DLG_OMEN_1_FAILURE"
        ),
        "DLG_OMEN_1_SUCCESS" to listOf(
            "ADD_RESOURCE:GOLD:500",
            "ADD_ARTIFACT:REW_OMEN_1_TEAR",
            "TRANSITION_STATE:COMPLETED"
        )
    )

    // Recovery & Retry Behavior
    const val RETRY_BEHAVIOR = "ON_ARENA_FAILURE:RESET_TO_STATE:INVESTIGATE_SKY_CASTLE"
    const val CANCELLATION_BEHAVIOR = "NOT_ALLOWED"
    const val RECOVERY_BEHAVIOR = "ON_LOAD:RESUME_LATEST_STATE"

    // Localization Keys (Placeholder)
    val locKeys = mapOf(
        "DLG_OMEN_1_START_TEXT" to "omen1.dialogue.start",
        "OBJ_OMEN_1_TALK_DESC" to "omen1.objective.talk"
    )

    // Validation Confirmation
    const val RUNTIME_SYSTEMS_RETOUCHED = false
}
