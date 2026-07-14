package com.example.anotherlife.ui.shell

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.AccountBox
import androidx.compose.material.icons.rounded.Build
import androidx.compose.material.icons.rounded.CheckCircle
import androidx.compose.material.icons.rounded.LocationOn
import androidx.compose.material.icons.rounded.Star
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.material3.adaptive.navigationsuite.NavigationSuiteScaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.navigation3.runtime.NavEntry
import androidx.navigation3.ui.NavDisplay
import com.example.anotherlife.ui.navigation.Route
import com.example.anotherlife.ui.simulation.AcademyScreen
import com.example.anotherlife.ui.simulation.BattleSimulatorScreen
import com.example.anotherlife.ui.simulation.KingdomDashboard
import com.example.anotherlife.ui.simulation.QuestScreen
import com.example.anotherlife.ui.simulation.StoryDialogueScreen
import com.example.anotherlife.ui.simulation.WarzoneMapScreen
import com.example.anotherlife.data.simulation.KingdomState
import com.example.anotherlife.data.simulation.NarrativeState
import com.example.anotherlife.data.simulation.DialogueNode
import com.example.anotherlife.data.simulation.DialogueChoice

/**
 * The core adaptive shell of "Another Life".
 *
 * Implements Jetpack Navigation 3 with a snapshot-state backed backstack.
 * The layout automatically adapts between Phone (Bottom Bar) and Tablet (Rail)
 * using NavigationSuiteScaffold.
 */
@Composable
fun AnotherLifeShell() {
    // Shared state for the simulation
    val kingdomState = remember { KingdomState() }
    val narrativeState = remember { NarrativeState() }

    // Initial Dialogue trigger logic (Demo)
    LaunchedEffect(Unit) {
        if (narrativeState.currentDialogue.value == null) {
            narrativeState.currentDialogue.value = DialogueNode(
                id = "intro",
                characterName = "Captain Valerius",
                text = "The walls are rebuilt, but the spirit of the people is still fragile. Your decree will shape our future.",
                choices = listOf(
                    DialogueChoice("A new era begins today.", "end"),
                    DialogueChoice("We must remain vigilant.", "end")
                )
            )
        }
    }

    // Navigation 3 Backstack: snapshot-state backed list of keys
    val backStack = remember { mutableStateListOf<Any>(Route.Kingdom) }
    val currentKey = backStack.lastOrNull() ?: Route.Kingdom

    NavigationSuiteScaffold(
        navigationSuiteItems = {
            item(
                selected = currentKey == Route.Kingdom,
                onClick = { 
                    backStack.clear()
                    backStack.add(Route.Kingdom) 
                },
                icon = { Icon(Icons.Rounded.Build, contentDescription = "Kingdom") },
                label = { Text("Kingdom") }
            )
            item(
                selected = currentKey == Route.Quests,
                onClick = {
                    if (currentKey != Route.Quests) {
                        backStack.add(Route.Quests)
                    }
                },
                icon = { Icon(Icons.Rounded.CheckCircle, contentDescription = "Quests") },
                label = { Text("Quests") }
            )
            item(
                selected = currentKey == Route.Champion,
                onClick = {
                    if (currentKey != Route.Champion) {
                        backStack.add(Route.Champion)
                    }
                },
                icon = { Icon(Icons.Rounded.AccountBox, contentDescription = "Academy") },
                label = { Text("Academy") }
            )
            item(
                selected = currentKey == Route.Battle,
                onClick = {
                    if (currentKey != Route.Battle) {
                        backStack.add(Route.Battle)
                    }
                },
                icon = { Icon(Icons.Rounded.Star, contentDescription = "Battle") },
                label = { Text("Battle") }
            )
            item(
                selected = currentKey == Route.Warzone,
                onClick = {
                    if (currentKey != Route.Warzone) {
                        backStack.add(Route.Warzone)
                    }
                },
                icon = { Icon(Icons.Rounded.LocationOn, contentDescription = "Warzone") },
                label = { Text("Warzone") }
            )
        }
    ) {
        // NavDisplay observes the backstack and reflects state changes in the UI
        NavDisplay(
            backStack = backStack,
            onBack = { if (backStack.size > 1) backStack.removeAt(backStack.size - 1) },
            modifier = Modifier.fillMaxSize(),
            entryProvider = { key ->
                when (key) {
                    is Route.Kingdom -> NavEntry(key) { KingdomDashboard(state = kingdomState) }
                    is Route.Quests -> NavEntry(key) {
                        QuestScreen(
                            state = kingdomState,
                            onLocate = { markerId ->
                                if (markerId.isNotBlank()) {
                                    backStack.add(Route.Warzone)
                                }
                            }
                        )
                    }
                    is Route.Champion -> NavEntry(key) { AcademyScreen(state = kingdomState) }
                    is Route.Battle -> NavEntry(key) { BattleSimulatorScreen(state = kingdomState) }
                    is Route.Warzone -> NavEntry(key) { 
                        WarzoneMapScreen(state = kingdomState, onAttack = { _ ->
                            // Navigate to Battle screen for the selected territory
                            backStack.add(Route.Battle)
                        }) 
                    }
                    else -> NavEntry(Unit) { Text("Unknown Route") }
                }
            }
        )
    }

    // Narrative Overlay
    if (narrativeState.currentDialogue.value != null) {
        StoryDialogueScreen(
            state = narrativeState,
            onChoiceSelected = { nodeId ->
                if (nodeId == "end") {
                    narrativeState.currentDialogue.value = null
                } else {
                    // Logic to load next node
                }
            }
        )
    }
}
