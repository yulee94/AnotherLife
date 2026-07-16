package com.example.anotherlife.data.simulation

/**
 * Authoritative Narrative Packet for Milestone NVS-01
 * 
 * Purpose: First end-to-end quest loop bridging Kingdom and Arena.
 * Quest: OMEN_1 "The First Signal"
 * 
 * FIDELITY STATUS: Clean A1 (Complies with D1-D16 #138 and Audit 2026-07-14)
 */
object NVS_01_Packet {
    const val MILESTONE_ID = "NVS-01"
    const val CHAPTER_ID = "CH1_PROLOGUE"
    const val QUEST_ID = "OMEN_1"
    
    // Stable IDs for handoff
    const val ADVISOR_ID = "ADVISOR_VALERIUS"
    const val ARENA_HOOK_ID = "HOOK_SKY_CASTLE_ARENA"
    const val REWARD_ARTIFACT_ID = "REW_OMEN_1_TEAR"
    
    // Semantic Gameplay Handoff (D1, D8)
    const val GAMEPLAY_HANDOFF_REQUEST = "EXECUTE_ARENA_ENCOUNTER:HOOK_SKY_CASTLE_ARENA"
    const val GAMEPLAY_RETURN_SUCCESS = "EVENT_ARENA_ENCOUNTER_SUCCESS:HOOK_SKY_CASTLE_ARENA"
    const val GAMEPLAY_RETURN_FAILURE = "EVENT_ARENA_ENCOUNTER_FAILURE:HOOK_SKY_CASTLE_ARENA"

    // Entry, Placement, and Unlock (D10, D12, D15)
    val eligibleRealms = listOf("Crownlands")
    val prerequisites = listOf("NONE") // Starting quest for Crownlands
    val entryConditions = "CHAPTER_ID == CH1_PROLOGUE && SELECTED_REALM == Crownlands"
    val unlockRules = "MANUAL_INTERACTION:ADVISOR_VALERIUS"

    // Quest States (D3, D16)
    enum class State {
        INACTIVE,
        TALK_TO_VALERIUS,
        INVESTIGATE_SKY_CASTLE, // Mid-arena / Handoff State
        REPORT_TO_VALERIUS,     // Manual report required after Arena (D14)
        COMPLETED,
        FAILED                  // Transient failure state (D3)
    }

    // Objective Progression (D2, D6, D13, D14)
    val objectives = listOf(
        Objective("OBJ_OMEN_1_TALK", "Speak with Captain Valerius in the Command Center."),
        Objective("OBJ_OMEN_1_ARENA", "Investigate the celestial anomaly at the Sky Castle."),
        Objective("OBJ_OMEN_1_REPORT", "Report findings back to Captain Valerius.")
    )

    data class Objective(val id: String, val description: String)

    val storyNodes = listOf(
        DialogueNode(
            id = "DLG_OMEN_1_START",
            characterName = "Captain Valerius",
            text = "My Lord, the observers at the Sky Castle report strange vibrations. Will you investigate?",
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
                DialogueChoice("[Transition to Arena]", "end") // Handoff via consequence
            )
        ),
        DialogueNode(
            id = "DLG_OMEN_1_SUCCESS",
            characterName = "Captain Valerius",
            text = "You returned! This 'Celestial Tear' will be vital for our research. Well done.",
            choices = listOf(
                DialogueChoice("The kingdom is safe for now.", "end")
            )
        ),
        DialogueNode(
            id = "DLG_OMEN_1_FAILURE",
            characterName = "Captain Valerius",
            text = "The vibrations were too strong, My Lord. We must regroup and try again when you are ready.",
            choices = listOf(
                DialogueChoice("I will return soon.", "end")
            )
        )
    )

    // Consequence Intent and Ordering (D4, D5)
    // 1. Success -> Acquire Tear -> Report State
    // 2. Report Interaction -> Gain Gold -> Gain Affinity -> Completion
    val consequences = mapOf(
        "DLG_OMEN_1_GO" to listOf(
            "TRANSITION_STATE:INVESTIGATE_SKY_CASTLE",
            "REQUEST_HANDOFF:HOOK_SKY_CASTLE_ARENA"
        ),
        "EVENT_ARENA_ENCOUNTER_SUCCESS:HOOK_SKY_CASTLE_ARENA" to listOf(
            "ADD_ARTIFACT:REW_OMEN_1_TEAR", // Acquire Tear immediately (D5, D13)
            "TRANSITION_STATE:REPORT_TO_VALERIUS",
            "UPDATE_OBJECTIVE:OBJ_OMEN_1_REPORT:ACTIVE"
        ),
        "EVENT_ARENA_ENCOUNTER_FAILURE:HOOK_SKY_CASTLE_ARENA" to listOf(
            "TRANSITION_STATE:TALK_TO_VALERIUS", // Recovery point (D2)
            "TRIGGER_DIALOGUE:DLG_OMEN_1_FAILURE"
        ),
        "DLG_OMEN_1_SUCCESS" to listOf(
            "SET_AFFINITY:ADVISOR_VALERIUS:+5", // Affinity at Report (D4)
            "ADD_RESOURCE:GOLD:500",           // Gold at Report (D5)
            "TRANSITION_STATE:COMPLETED"        // Final Terminal State (D6)
        )
    )

    // Persistence and Recovery (D16)
    const val RESUME_SEMANTICS = "ON_LOAD:START_OF_CURRENT_STATE"
    const val CANCELLATION_ALLOWED = true // (D9)
    const val RETRY_PLAYER_ACTION = "TALK_TO_VALERIUS_AFTER_FAILURE"

    // Validation Confirmation
    const val RUNTIME_SYSTEMS_RETOUCHED = false
}
