package com.example.anotherlife.ui.simulation

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.anotherlife.data.simulation.KingdomState
import com.example.anotherlife.data.simulation.Territory

@Composable
fun WarzoneMapScreen(state: KingdomState, onAttack: (Territory) -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Text(
            text = "Warzone: World Map",
            style = MaterialTheme.typography.headlineMedium,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(bottom = 16.dp)
        )

        LazyVerticalGrid(
            columns = GridCells.Fixed(2),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
            modifier = Modifier.weight(1f)
        ) {
            items(state.territories) { territory ->
                TerritoryCard(territory = territory, onAttack = { onAttack(territory) })
            }
        }
    }
}

@Composable
fun TerritoryCard(territory: Territory, onAttack: () -> Unit) {
    val ownerColor = when (territory.owner) {
        "Stonehold" -> Color(0xFF795548) // Brown
        "Eldergrove" -> Color(0xFF4CAF50) // Green
        "Crownlands" -> Color(0xFFFFC107) // Gold
        "Umbral" -> Color(0xFF673AB7) // Purple
        else -> Color.Gray
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .height(150.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
    ) {
        Column(
            modifier = Modifier
                .padding(12.dp)
                .fillMaxSize(),
            verticalArrangement = Arrangement.SpaceBetween
        ) {
            Column {
                Text(text = territory.name, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                Box(
                    modifier = Modifier
                        .size(16.dp)
                        .background(ownerColor, shape = MaterialTheme.shapes.extraSmall)
                )
                Text(text = "Owned by: ${territory.owner}", style = MaterialTheme.typography.bodySmall)
            }

            if (territory.owner != "Crownlands") { // Assuming player is Crownlands for simulation
                Button(
                    onClick = onAttack,
                    modifier = Modifier.fillMaxWidth(),
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
                ) {
                    Text("Attack")
                }
            } else {
                Text(
                    text = "SAFE / DEFENDED",
                    color = Color(0xFF388E3C),
                    style = MaterialTheme.typography.labelLarge,
                    modifier = Modifier.align(Alignment.CenterHorizontally)
                )
            }
        }
    }
}
