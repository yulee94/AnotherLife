package com.example.anotherlife.ui.simulation

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material3.*
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
fun QuestScreen(state: KingdomState, onLocate: (String) -> Unit) {
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
            items(state.quests) { quest ->
                QuestCard(quest = quest, onClaim = {
                    // Simulation logic
                }, onLocate = { onLocate(quest.mapMarkerId ?: "") })
            }
        }
    }
}

@Composable
fun QuestCard(quest: Quest, onClaim: () -> Unit, onLocate: () -> Unit) {
    val progressPercent = quest.progress.toFloat() / quest.target.toFloat()

    OutlinedCard(
        modifier = Modifier.fillMaxWidth(),
        colors = if (quest.isCompleted && !quest.isClaimed) 
            CardDefaults.outlinedCardColors(containerColor = MaterialTheme.colorScheme.secondaryContainer.copy(alpha = 0.2f))
            else CardDefaults.outlinedCardColors()
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
                    val modeColor = if (quest.mode == QuestMode.Arena3D) MaterialTheme.colorScheme.tertiary else MaterialTheme.colorScheme.secondary
                    Surface(
                        color = modeColor,
                        shape = RoundedCornerShape(4.dp)
                    ) {
                        Text(
                            text = if (quest.mode == QuestMode.Arena3D) "3D ARENA" else "KINGDOM",
                            modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp),
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSecondary
                        )
                    }

                    if (quest.isCompleted) {
                        Icon(
                            Icons.Default.CheckCircle,
                            contentDescription = "Completed",
                            tint = Color(0xFF4CAF50),
                            modifier = Modifier.size(32.dp).padding(top = 4.dp)
                        )
                    }
                }
            }

            if (quest.mapMarkerId != null) {
                Spacer(modifier = Modifier.height(8.dp))
                AssistChip(
                    onClick = onLocate,
                    label = { Text("Locate: ${quest.mapMarkerId}") },
                    leadingIcon = { Icon(Icons.Default.LocationOn, contentDescription = null, modifier = Modifier.size(16.dp)) }
                )
            }

            Spacer(modifier = Modifier.height(16.dp))

            Row(verticalAlignment = Alignment.CenterVertically) {
                LinearProgressIndicator(
                    progress = { progressPercent },
                    modifier = Modifier.weight(1f).height(8.dp),
                    color = if (quest.isCompleted) Color(0xFF4CAF50) else MaterialTheme.colorScheme.primary,
                    trackColor = MaterialTheme.colorScheme.surfaceVariant
                )
                Spacer(modifier = Modifier.width(12.dp))
                Text(
                    text = "${quest.progress} / ${quest.target}",
                    style = MaterialTheme.typography.labelLarge
                )
            }

            if (quest.isCompleted && !quest.isClaimed) {
                Spacer(modifier = Modifier.height(16.dp))
                Button(
                    onClick = onClaim,
                    modifier = Modifier.fillMaxWidth(),
                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFFFC107), contentColor = Color.Black)
                ) {
                    Text("CLAIM REWARD: 500 GOLD", fontWeight = FontWeight.Bold)
                }
            } else if (quest.isClaimed) {
                Spacer(modifier = Modifier.height(16.dp))
                Text(
                    text = "REWARD CLAIMED",
                    style = MaterialTheme.typography.labelMedium,
                    color = Color.Gray,
                    modifier = Modifier.align(Alignment.CenterHorizontally)
                )
            }
        }
    }
}
