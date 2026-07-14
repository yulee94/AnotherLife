package com.example.anotherlife.ui.simulation

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Info
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.anotherlife.data.simulation.DossierNarrative
import com.example.anotherlife.data.simulation.KingdomState

@Composable
fun DossierScreen(state: KingdomState) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Text(
            text = "Command Dossier",
            style = MaterialTheme.typography.headlineMedium,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(bottom = 16.dp)
        )

        Text(
            text = "Strategic Overview and Narrative Intelligence",
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(bottom = 24.dp)
        )

        LazyColumn(
            verticalArrangement = Arrangement.spacedBy(12.dp),
            modifier = Modifier.weight(1f)
        ) {
            items(DossierNarrative.initialEntries) { entry ->
                DossierCard(entry)
            }
            
            item {
                Spacer(modifier = Modifier.height(24.dp))
                Text(
                    text = DossierNarrative.LOG_TITLE,
                    style = MaterialTheme.typography.titleLarge,
                    modifier = Modifier.padding(bottom = 8.dp)
                )
            }
            
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f))
                ) {
                    Text(
                        text = DossierNarrative.EMPTY_LOG_MESSAGE,
                        modifier = Modifier.padding(16.dp),
                        style = MaterialTheme.typography.bodyMedium,
                        color = Color.Gray
                    )
                }
            }
        }
    }
}

@Composable
fun DossierCard(entry: DossierNarrative.DossierEntry) {
    val categoryColor = when (entry.category) {
        DossierNarrative.Category.CHAPTER_PROGRESS -> MaterialTheme.colorScheme.primary
        DossierNarrative.Category.ADVISOR_STATUS -> MaterialTheme.colorScheme.secondary
        DossierNarrative.Category.REPUTATION -> MaterialTheme.colorScheme.tertiary
        DossierNarrative.Category.WORLD_EVENTS -> MaterialTheme.colorScheme.error
    }

    OutlinedCard(
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier
                .padding(16.dp)
                .fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                Icons.Rounded.Info,
                contentDescription = null,
                tint = categoryColor,
                modifier = Modifier.size(32.dp)
            )
            
            Spacer(modifier = Modifier.width(16.dp))
            
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = entry.category.name.replace("_", " "),
                    style = MaterialTheme.typography.labelSmall,
                    color = categoryColor
                )
                Text(
                    text = entry.title,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = entry.value,
                    style = MaterialTheme.typography.bodyLarge
                )
                if (entry.trend != null) {
                    Text(
                        text = entry.trend,
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.secondary
                    )
                }
            }
        }
    }
}
