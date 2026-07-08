package com.example.anotherlife.ui.simulation

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.anotherlife.data.simulation.KingdomState

@Composable
fun BattleSimulatorScreen(state: KingdomState) {
    var battleResult by remember { mutableStateOf<String?>(null) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = "Deterministic Battle Simulator",
            style = MaterialTheme.typography.headlineMedium,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(bottom = 16.dp)
        )

        Card(
            modifier = Modifier.fillMaxWidth().padding(bottom = 16.dp)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(text = "Your Army", style = MaterialTheme.typography.titleLarge)
                state.troops.forEach { troop ->
                    Text(text = "${troop.type}: ${troop.count}")
                }
            }
        }

        Text(text = "vs", style = MaterialTheme.typography.headlineSmall, modifier = Modifier.padding(vertical = 8.dp))

        Card(
            modifier = Modifier.fillMaxWidth().padding(bottom = 24.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(text = "Enemy Forces (Dark Elves)", style = MaterialTheme.typography.titleLarge)
                Text(text = "Infantry: 120")
                Text(text = "Cavalry: 40")
                Text(text = "Ranged: 100")
            }
        }

        Button(
            onClick = { battleResult = "Victory! Losses: 15 Infantry, 5 Cavalry." },
            modifier = Modifier.fillMaxWidth().height(56.dp)
        ) {
            Icon(Icons.Default.PlayArrow, contentDescription = null)
            Spacer(Modifier.width(8.dp))
            Text("Run Simulation")
        }

        battleResult?.let {
            Spacer(modifier = Modifier.height(24.dp))
            Surface(
                color = MaterialTheme.colorScheme.secondaryContainer,
                shape = MaterialTheme.shapes.medium,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(
                    text = it,
                    modifier = Modifier.padding(16.dp),
                    style = MaterialTheme.typography.bodyLarge
                )
            }
        }
    }
}
