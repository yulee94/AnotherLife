package com.example.anotherlife.ui.simulation

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.anotherlife.data.simulation.Building
import com.example.anotherlife.data.simulation.KingdomState
import com.example.anotherlife.data.simulation.ResourceType

@Composable
fun KingdomDashboard(state: KingdomState) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Text(
            text = "Kingdom Management",
            style = MaterialTheme.typography.headlineMedium,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(bottom = 16.dp)
        )

        ResourceBar(state)

        Spacer(modifier = Modifier.height(24.dp))

        Text(
            text = "Buildings",
            style = MaterialTheme.typography.titleLarge,
            modifier = Modifier.padding(bottom = 8.dp)
        )

        LazyColumn(
            verticalArrangement = Arrangement.spacedBy(8.dp),
            modifier = Modifier.weight(1f)
        ) {
            items(state.buildings) { building ->
                BuildingItem(building = building, onUpgrade = {
                    // In a real mock, this would update the state
                })
            }
        }
    }
}

@Composable
fun ResourceBar(state: KingdomState) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
    ) {
        Row(
            modifier = Modifier
                .padding(12.dp)
                .fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceEvenly
        ) {
            state.resources.forEach { (type, amount) ->
                ResourceInfo(type = type.name, amount = amount)
            }
        }
    }
}

@Composable
fun ResourceInfo(type: String, amount: Long) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(text = type, style = MaterialTheme.typography.labelSmall)
        Text(text = amount.toString(), fontWeight = FontWeight.Bold)
    }
}

@Composable
fun BuildingItem(building: Building, onUpgrade: () -> Unit) {
    OutlinedCard(
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier
                .padding(16.dp)
                .fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column {
                Text(text = building.name, style = MaterialTheme.typography.titleMedium)
                Text(text = "Level ${building.level}", style = MaterialTheme.typography.bodySmall)
            }

            Button(onClick = onUpgrade) {
                Icon(Icons.Default.KeyboardArrowUp, contentDescription = null)
                Spacer(Modifier.width(4.dp))
                Text("Upgrade")
            }
        }
    }
}
