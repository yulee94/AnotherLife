package com.example.anotherlife.ui.simulation

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.AccountBox
import androidx.compose.material.icons.rounded.Info
import androidx.compose.material.icons.rounded.Star
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.anotherlife.data.simulation.*
import com.example.anotherlife.ui.layout.usesLargeTextLayout

@Composable
fun DossierScreen(state: KingdomState, narrative: NarrativeState) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Text(
            text = "Command Dossier",
            style = MaterialTheme.typography.headlineMedium,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(bottom = 8.dp)
        )
        
        Text(
            text = "Chapter: ${narrative.currentChapterId.value}",
            style = MaterialTheme.typography.labelLarge,
            color = MaterialTheme.colorScheme.secondary
        )

        Spacer(modifier = Modifier.height(24.dp))

        LazyColumn(
            verticalArrangement = Arrangement.spacedBy(16.dp),
            modifier = Modifier.weight(1f)
        ) {
            // Advisor Section
            item { SectionHeader("Royal Advisors", Icons.Rounded.AccountBox) }
            items(narrative.advisors) { advisor ->
                AdvisorCard(advisor)
            }

            // Faction Section
            item { SectionHeader("Faction Intelligence", Icons.Rounded.Star) }
            items(narrative.factions) { faction ->
                FactionCard(faction)
            }
            
            // Strategic Log
            item { SectionHeader("Strategic Narrative Log", Icons.Rounded.Info) }
            if (narrative.narrativeLog.isEmpty()) {
                item {
                    Text(
                        text = "No recent events recorded.",
                        modifier = Modifier.padding(16.dp),
                        style = MaterialTheme.typography.bodyMedium,
                        color = Color.Gray
                    )
                }
            } else {
                items(narrative.narrativeLog.reversed()) { log ->
                    LogEntry(log)
                }
            }
        }
    }
}

@Composable
fun SectionHeader(title: String, icon: ImageVector) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
        Spacer(Modifier.width(8.dp))
        Text(text = title, style = MaterialTheme.typography.titleLarge)
    }
}

@Composable
fun AdvisorCard(advisor: Persona) {
    OutlinedCard(modifier = Modifier.fillMaxWidth()) {
        BoxWithConstraints(modifier = Modifier.fillMaxWidth()) {
            val useStackedLayout = usesLargeTextLayout() || maxWidth < 320.dp

            Column(modifier = Modifier.padding(16.dp)) {
                Text(text = advisor.name, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                Text(text = advisor.role, style = MaterialTheme.typography.labelSmall)
                Spacer(Modifier.height(8.dp))
                if (useStackedLayout) {
                    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        AdvisorAffinity(advisor)
                        AdvisorBias(advisor)
                    }
                } else {
                    Row(
                        horizontalArrangement = Arrangement.SpaceBetween,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        AdvisorAffinity(advisor)
                        AdvisorBias(advisor)
                    }
                }
                LinearProgressIndicator(
                    progress = { advisor.affinity / 100f },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 4.dp),
                    color = MaterialTheme.colorScheme.secondary
                )
            }
        }
    }
}

@Composable
private fun AdvisorAffinity(advisor: Persona) {
    Text(text = "Affinity: ${advisor.affinity}", style = MaterialTheme.typography.bodyMedium)
}

@Composable
private fun AdvisorBias(advisor: Persona) {
    Text(
        text = advisor.strategicBias.name,
        style = MaterialTheme.typography.labelMedium,
        color = MaterialTheme.colorScheme.tertiary
    )
}

@Composable
fun FactionCard(faction: Faction) {
    Card(modifier = Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.3f))) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(text = faction.name, style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
            Text(text = faction.description, style = MaterialTheme.typography.bodySmall)
            Spacer(Modifier.height(8.dp))
            Text(text = "Reputation: ${faction.reputation}", style = MaterialTheme.typography.labelLarge)
        }
    }
}

@Composable
fun LogEntry(log: String) {
    Text(
        text = "> $log",
        style = MaterialTheme.typography.bodySmall,
        modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp),
        color = MaterialTheme.colorScheme.outline
    )
}
