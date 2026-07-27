package com.example.anotherlife.ui.simulation

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material3.AssistChip
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedCard
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.anotherlife.data.simulation.KingdomState
import com.example.anotherlife.data.simulation.Quest
import com.example.anotherlife.data.simulation.QuestMode

@Composable
fun QuestScreen(
    state: KingdomState,
    onLocate: (String) -> Unit,
    onStartQuest: (String) -> Unit = {}
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Text(
            text = "Royal Quests & Milestones",
            style = MaterialTheme.typography.headlineMedium,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(bottom = 16.dp)
        )

        LazyColumn(
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            itemsIndexed(state.quests, key = { index, quest -> "${quest.id}:$index" }) { _, quest ->
                QuestCard(
                    quest = quest,
                    onLocate = { markerId -> onLocate(markerId) },
                    onStart = { questId -> onStartQuest(questId) }
                )
            }
        }
    }
}

@Composable
fun QuestCard(
    quest: Quest,
    onLocate: (String) -> Unit,
    onStart: (String) -> Unit
) {
    val preview = QuestPreviewState.from(quest)

    OutlinedCard(
        modifier = Modifier.fillMaxWidth(),
        colors = if (preview.isCompleted && !preview.isInvalid) {
            CardDefaults.outlinedCardColors(
                containerColor = MaterialTheme.colorScheme.secondaryContainer.copy(alpha = 0.2f)
            )
        } else {
            CardDefaults.outlinedCardColors()
        }
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(text = quest.title, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                    Text(text = quest.description, style = MaterialTheme.typography.bodyMedium)
                }

                Column(horizontalAlignment = Alignment.End) {
                    val modeColor = if (quest.mode == QuestMode.Arena3D) {
                        MaterialTheme.colorScheme.tertiary
                    } else {
                        MaterialTheme.colorScheme.secondary
                    }
                    val modeContentColor = if (quest.mode == QuestMode.Arena3D) {
                        MaterialTheme.colorScheme.onTertiary
                    } else {
                        MaterialTheme.colorScheme.onSecondary
                    }
                    Surface(
                        color = modeColor,
                        shape = RoundedCornerShape(4.dp)
                    ) {
                        Text(
                            text = if (quest.mode == QuestMode.Arena3D) "3D ARENA" else "KINGDOM",
                            modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp),
                            style = MaterialTheme.typography.labelSmall,
                            color = modeContentColor
                        )
                    }

                    if (preview.isCompleted && !preview.isInvalid) {
                        Icon(
                            Icons.Default.CheckCircle,
                            contentDescription = "Completed",
                            tint = Color(0xFF4CAF50),
                            modifier = Modifier.size(32.dp).padding(top = 4.dp)
                        )
                    }
                }
            }

            preview.markerId?.let { markerId ->
                Spacer(modifier = Modifier.height(8.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    AssistChip(
                        onClick = { onLocate(markerId) },
                        label = { Text("Location available") },
                        leadingIcon = {
                            Icon(
                                Icons.Default.LocationOn,
                                contentDescription = null,
                                modifier = Modifier.size(16.dp)
                            )
                        },
                        enabled = !preview.isInvalid
                    )
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            Row(verticalAlignment = Alignment.CenterVertically) {
                if (preview.progressRatio != null) {
                    LinearProgressIndicator(
                        progress = { preview.progressRatio },
                        modifier = Modifier.weight(1f).height(8.dp),
                        color = if (preview.isCompleted) Color(0xFF4CAF50) else MaterialTheme.colorScheme.primary,
                        trackColor = MaterialTheme.colorScheme.surfaceVariant
                    )
                } else {
                    Text(
                        text = "Progress unavailable",
                        style = MaterialTheme.typography.labelLarge,
                        color = MaterialTheme.colorScheme.error,
                        modifier = Modifier.weight(1f)
                    )
                }
                Spacer(modifier = Modifier.width(12.dp))
                Text(
                    text = preview.progressText,
                    style = MaterialTheme.typography.labelLarge,
                    color = if (preview.isInvalid) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurface
                )
            }

            Spacer(modifier = Modifier.height(12.dp))
            Text(
                text = preview.statusText,
                style = MaterialTheme.typography.labelMedium,
                color = if (preview.isInvalid) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.align(Alignment.CenterHorizontally)
            )
        }
    }
}

private data class QuestPreviewState(
    val progressRatio: Float?,
    val progressText: String,
    val statusText: String,
    val markerId: String?,
    val isCompleted: Boolean,
    val isInvalid: Boolean
) {
    companion object {
        fun from(quest: Quest): QuestPreviewState {
            val trimmedMarker = quest.mapMarkerId?.trim()?.takeIf { it.isNotEmpty() }
            val invalidReason = when {
                quest.id.isBlank() -> "Invalid quest identity"
                quest.target <= 0 -> "Invalid objective target"
                quest.progress < 0 -> "Invalid objective progress"
                quest.progress > quest.target -> "Objective progress exceeds target"
                quest.isClaimed && !quest.isCompleted -> "Reward state conflicts with completion"
                else -> null
            }

            if (invalidReason != null) {
                return QuestPreviewState(
                    progressRatio = null,
                    progressText = "${quest.progress} / ${quest.target}",
                    statusText = invalidReason,
                    markerId = trimmedMarker,
                    isCompleted = false,
                    isInvalid = true
                )
            }

            val ratio = quest.progress.toFloat() / quest.target.toFloat()
            val status = when {
                quest.isClaimed -> "Reward already committed"
                quest.isCompleted -> "Completion awaiting authoritative runtime result"
                quest.mode == QuestMode.Arena3D -> "Story launch unavailable in preview"
                else -> "Preview only"
            }

            return QuestPreviewState(
                progressRatio = ratio,
                progressText = "${quest.progress} / ${quest.target}",
                statusText = status,
                markerId = trimmedMarker,
                isCompleted = quest.isCompleted,
                isInvalid = false
            )
        }
    }
}
