package com.example.anotherlife.ui.shell

import android.util.Log
import com.example.anotherlife.BuildConfig
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.AccountBox
import androidx.compose.material.icons.rounded.Build
import androidx.compose.material.icons.rounded.Info
import androidx.compose.material.icons.automirrored.rounded.List
import androidx.compose.material.icons.rounded.LocationOn
import androidx.compose.material.icons.rounded.Star
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.navigation3.runtime.NavEntry
import androidx.navigation3.ui.NavDisplay
import com.example.anotherlife.ui.navigation.Route
import com.example.anotherlife.ui.simulation.AcademyScreen
import com.example.anotherlife.ui.simulation.BattleSimulatorScreen
import com.example.anotherlife.ui.simulation.DossierScreen
import com.example.anotherlife.ui.simulation.KingdomDashboard
import com.example.anotherlife.ui.simulation.NarrativeDebugScreen
import com.example.anotherlife.ui.simulation.WarzoneMapScreen
import com.example.anotherlife.ui.simulation.QuestPreviewRoute
import com.example.anotherlife.ui.simulation.StoryDialogueScreen
import com.example.anotherlife.data.simulation.DialogueNode
import com.example.anotherlife.data.simulation.KingdomState
import com.example.anotherlife.data.simulation.NarrativeState
import com.example.anotherlife.data.contracts.AndroidSharedCatalogLoader

/**
 * The core adaptive shell of "Another Life".
 *
 * Implements Jetpack Navigation 3 with a snapshot-state backed backstack.
 * The layout automatically adapts between Phone (Bottom Bar) and Tablet (Rail)
 * using NavigationSuiteScaffold.
 */
@Composable
fun AnotherLifeShell() {
    val debugToolsEnabled = BuildConfig.DEBUG
    val appContext = LocalContext.current.applicationContext
    val sharedCatalogLoader = remember(appContext) {
        AndroidSharedCatalogLoader.shared(appContext)
    }
    LaunchedEffect(sharedCatalogLoader) {
        runCatching { sharedCatalogLoader.load() }
            .onFailure { error ->
                Log.e("AnotherLifeShell", "Shared catalog validation failed.", error)
            }
    }

    // Shared state for the simulation
    val kingdomState = remember { KingdomState() }
    val narrativeState = remember { 
        NarrativeState().apply {
            advisors.addAll(com.example.anotherlife.data.simulation.AdvisorPersonas.allAdvisors)
            factions.addAll(com.example.anotherlife.data.simulation.FactionProfiles.allFactions)
            narrativeLog.add("The kingdom awakens to a new era.")
        }
    }

    // Navigation 3 Backstack: snapshot-state backed list of keys
    val backStack = remember { mutableStateListOf<Any>(Route.Kingdom) }
    val routeNotice = remember { mutableStateOf<String?>(null) }
    LaunchedEffect(debugToolsEnabled, backStack.toList()) {
        val sanitized = ShellRoutePolicy.sanitizeBackStack(backStack, debugToolsEnabled)
        if (sanitized.routes != backStack) {
            backStack.clear()
            backStack.addAll(sanitized.routes)
        }
        routeNotice.value = sanitized.rejectedTopRoute?.message
    }
    val currentKey = backStack.lastOrNull() ?: Route.Kingdom
    val currentRoute = ShellRoutePolicy.resolveRoute(currentKey, debugToolsEnabled).route

    Scaffold(
        bottomBar = {
            NavigationBar {
                ShellRoutePolicy.bottomNavigationRoutes(debugToolsEnabled).forEach { route ->
                    NavigationBarItem(
                        selected = currentRoute == route,
                        onClick = {
                            if (route == Route.Kingdom) {
                                backStack.clear()
                                backStack.add(Route.Kingdom)
                            } else if (currentKey != route) {
                                backStack.add(route)
                            }
                        },
                        icon = { Icon(iconForRoute(route), contentDescription = labelForRoute(route)) },
                        label = { Text(labelForRoute(route)) }
                    )
                }
            }
        }
    ) { contentPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(contentPadding)
        ) {
            routeNotice.value?.let { message ->
                Text(
                    text = message,
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodyMedium,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp, vertical = 8.dp)
                )
            }

            // NavDisplay observes the backstack and reflects state changes in the UI
            NavDisplay(
                backStack = backStack,
                onBack = { if (backStack.size > 1) backStack.removeAt(backStack.size - 1) },
                modifier = Modifier
                    .fillMaxSize(),
                entryProvider = { key ->
                    val resolvedRoute = ShellRoutePolicy.resolveRoute(key, debugToolsEnabled).route
                    when (resolvedRoute) {
                        Route.Kingdom -> NavEntry(resolvedRoute) {
                            KingdomDashboard(state = kingdomState)
                        }
                        Route.Dossier -> NavEntry(resolvedRoute) {
                            DossierScreen(state = kingdomState, narrative = narrativeState)
                        }
                        Route.Quest -> NavEntry(resolvedRoute) {
                            QuestPreviewRoute()
                        }
                        Route.Champion -> NavEntry(resolvedRoute) { AcademyScreen(state = kingdomState) }
                        Route.Battle -> NavEntry(resolvedRoute) { BattleSimulatorScreen(state = kingdomState) }
                        Route.Warzone -> NavEntry(resolvedRoute) {
                            WarzoneMapScreen(state = kingdomState, onAttack = { territory ->
                                // Navigate to Battle screen for the selected territory
                                backStack.add(Route.Battle)
                            })
                        }
                        Route.NarrativeDebug -> NavEntry(resolvedRoute) {
                            NarrativeDebugScreen(
                                state = narrativeState,
                                onOpenQuestPreview = {
                                    if (backStack.lastOrNull() != Route.Quest) {
                                        backStack.add(Route.Quest)
                                    }
                                }
                            )
                        }
                    }
                }
            )
        }
    }

    // Narrative Overlay
    if (narrativeState.currentDialogue.value != null) {
        StoryDialogueScreen(
            state = narrativeState,
            onChoiceSelected = { nodeId ->
                if (nodeId == "end") {
                    narrativeState.currentDialogue.value = null
                } else {
                    // Logic to load next node from packets (Simulation)
                    val nextNode = findDialogueNode(nodeId)
                    if (nextNode != null) {
                        narrativeState.currentDialogue.value = nextNode
                    } else {
                        narrativeState.currentDialogue.value = null
                    }
                }
            }
        )
    }
}

/**
 * Helper to find dialogue nodes across authored packets.
 */
private fun findDialogueNode(id: String): DialogueNode? {
    val allNodes = com.example.anotherlife.data.simulation.NVS_01_Packet.storyNodes
    return allNodes.find { it.id == id }
}

private fun labelForRoute(route: Route): String {
    return when (route) {
        Route.Kingdom -> "Kingdom"
        Route.Dossier -> "Dossier"
        Route.Champion -> "Academy"
        Route.Warzone -> "Warzone"
        Route.NarrativeDebug -> "Debug"
        Route.Battle -> "Battle"
        Route.Quest -> "Quest"
    }
}

private fun iconForRoute(route: Route) = when (route) {
    Route.Kingdom -> Icons.Rounded.Build
    Route.Dossier -> Icons.AutoMirrored.Rounded.List
    Route.Champion -> Icons.Rounded.AccountBox
    Route.Warzone -> Icons.Rounded.LocationOn
    Route.NarrativeDebug -> Icons.Rounded.Info
    Route.Battle -> Icons.Rounded.Star
    Route.Quest -> Icons.Rounded.Star
}
