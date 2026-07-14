package com.example.anotherlife.ui.simulation

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.anotherlife.data.simulation.DialogueNode
import com.example.anotherlife.data.simulation.NVS_01_Packet
import com.example.anotherlife.data.simulation.NarrativeState

@Composable
fun NarrativeDebugScreen(state: NarrativeState) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Text(
            text = "Narrative Director Debug",
            style = MaterialTheme.typography.headlineMedium,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(bottom = 16.dp)
        )

        Text(
            text = "Trigger Dialogue Nodes for NVS-01 Validation",
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(bottom = 24.dp)
        )

        LazyColumn(
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            items(NVS_01_Packet.storyNodes) { node ->
                NodeDebugCard(node) {
                    state.currentDialogue.value = it
                }
            }
        }
    }
}

@Composable
fun NodeDebugCard(node: DialogueNode, onTrigger: (DialogueNode) -> Unit) {
    OutlinedCard(
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(text = node.id, style = MaterialTheme.typography.labelSmall)
                Text(text = node.characterName, style = MaterialTheme.typography.titleMedium)
                Text(text = node.text, style = MaterialTheme.typography.bodySmall, maxLines = 1)
            }
            
            IconButton(onClick = { onTrigger(node) }) {
                Icon(Icons.Default.PlayArrow, contentDescription = "Trigger")
            }
        }
    }
}
