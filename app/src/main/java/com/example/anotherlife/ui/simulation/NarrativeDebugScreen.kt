package com.example.anotherlife.ui.simulation

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.List
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import com.example.anotherlife.R
import com.example.anotherlife.data.simulation.DialogueNode
import com.example.anotherlife.data.simulation.NVS_01_Packet
import com.example.anotherlife.data.simulation.NarrativeState

@Composable
fun NarrativeDebugScreen(
    state: NarrativeState,
    onOpenQuestPreview: () -> Unit = {},
    onOpenUnityBridgeSmoke: () -> Unit = {}
) {
    val errorMessage = remember { mutableStateOf<String?>(null) }

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
            text = "Developer-only non-authoritative preview. This screen is unavailable in release builds.",
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(bottom = 12.dp)
        )

        OutlinedButton(
            onClick = onOpenQuestPreview,
            modifier = Modifier.padding(bottom = 12.dp)
        ) {
            Icon(
                imageVector = Icons.AutoMirrored.Rounded.List,
                contentDescription = null
            )
            Spacer(modifier = Modifier.width(8.dp))
            Text(text = stringResource(R.string.quest_preview_open))
        }

        OutlinedButton(
            onClick = onOpenUnityBridgeSmoke,
            modifier = Modifier.padding(bottom = 12.dp)
        ) {
            Icon(
                imageVector = Icons.Default.PlayArrow,
                contentDescription = null
            )
            Spacer(modifier = Modifier.width(8.dp))
            Text(text = stringResource(R.string.unity_bridge_smoke_open))
        }

        errorMessage.value?.let { message ->
            Text(
                text = message,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(bottom = 12.dp)
            )
        }

        LazyColumn(
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            items(NVS_01_Packet.storyNodes) { node ->
                NodeDebugCard(node) {
                    val triggered = NarrativeDebugTriggers.triggerPreviewNode(state, it.id)
                    errorMessage.value = if (triggered) {
                        null
                    } else {
                        NarrativeDebugTriggers.missingNodeMessage(it.id)
                    }
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
