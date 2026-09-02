package com.example.anotherlife.ui.simulation

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Info
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.example.anotherlife.data.simulation.KingdomState
import com.example.anotherlife.data.simulation.Research
import com.example.anotherlife.ui.layout.usesLargeTextLayout

@Composable
fun AcademyScreen(state: KingdomState) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Text(
            text = "Royal Academy",
            style = MaterialTheme.typography.headlineMedium,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(bottom = 16.dp)
        )

        Text(
            text = "Research technologies to empower your realm and increase production.",
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(bottom = 24.dp)
        )

        LazyColumn(
            verticalArrangement = Arrangement.spacedBy(12.dp),
            modifier = Modifier.weight(1f)
        ) {
            items(state.researches) { tech ->
                ResearchCard(tech = tech, onResearch = {
                    // Simulation logic
                })
            }
        }
    }
}

@Composable
fun ResearchCard(tech: Research, onResearch: () -> Unit) {
    OutlinedCard(
        modifier = Modifier.fillMaxWidth()
    ) {
        BoxWithConstraints(modifier = Modifier.fillMaxWidth()) {
            val useStackedLayout = usesLargeTextLayout() || maxWidth < 320.dp

            Column(
                modifier = Modifier.padding(16.dp)
            ) {
                if (useStackedLayout) {
                    Column(
                        modifier = Modifier.fillMaxWidth(),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        ResearchDetails(tech)
                        ResearchLevel(tech)
                    }
                } else {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(modifier = Modifier.weight(1f)) {
                            ResearchDetails(tech)
                        }
                        ResearchLevel(tech)
                    }
                }

                if (tech.isResearching) {
                    Spacer(modifier = Modifier.height(16.dp))
                    LinearProgressIndicator(
                        modifier = Modifier.fillMaxWidth(),
                        color = MaterialTheme.colorScheme.primary
                    )
                    Text(
                        text = "Researching...",
                        style = MaterialTheme.typography.labelSmall,
                        modifier = Modifier.padding(top = 4.dp)
                    )
                } else {
                    Spacer(modifier = Modifier.height(16.dp))
                    Button(
                        onClick = onResearch,
                        modifier = if (useStackedLayout) {
                            Modifier
                                .fillMaxWidth()
                                .heightIn(min = 48.dp)
                        } else {
                            Modifier
                                .align(Alignment.End)
                                .heightIn(min = 48.dp)
                        }
                    ) {
                        Icon(Icons.Default.Info, contentDescription = null)
                        Spacer(Modifier.width(8.dp))
                        Text(
                            text = "Research (${(tech.level + 1) * 200} Gold)",
                            textAlign = TextAlign.Center
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun ResearchDetails(tech: Research) {
    Column {
        Text(text = tech.name, style = MaterialTheme.typography.titleLarge)
        Text(text = tech.description, style = MaterialTheme.typography.bodySmall)
    }
}

@Composable
private fun ResearchLevel(tech: Research) {
    Surface(
        color = MaterialTheme.colorScheme.secondaryContainer,
        shape = MaterialTheme.shapes.small
    ) {
        Text(
            text = "Lv. ${tech.level}",
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp),
            style = MaterialTheme.typography.labelLarge
        )
    }
}
