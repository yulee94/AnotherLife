package com.example.anotherlife.ui.unity

import androidx.compose.foundation.layout.*
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

/**
 * Debug-only transport smoke route. Outcomes never carry gameplay authority or mutate profile
 * state. Only the expected unavailable/cancelled statuses return automatically to the shell.
 */
@Composable
fun UnityBridgeSmokeRoute(
    onBack: () -> Unit,
    onSafeReturn: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    UnityBridgeSmokeRouteContent(
        onBack = onBack,
        onSafeReturn = onSafeReturn,
        modifier = modifier
    ) { hostModifier, onOutcome ->
        UnityView(
            routeId = UnityBridgeSmokePolicy.ROUTE_ID,
            routeIntent = UnityRouteIntent.Preview,
            requestedCapabilities = emptyList(),
            onOutcome = onOutcome,
            modifier = hostModifier
        )
    }
}

@Composable
internal fun UnityBridgeSmokeRouteContent(
    onBack: () -> Unit,
    onSafeReturn: (String) -> Unit,
    modifier: Modifier = Modifier,
    unityHost: @Composable (Modifier, (UnityRouteOutcome) -> Unit) -> Unit
) {
    val blockingNotice = remember { mutableStateOf<String?>(null) }

    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Text(
            text = "Unity bridge smoke",
            style = MaterialTheme.typography.headlineMedium,
            color = MaterialTheme.colorScheme.primary
        )
        Text(
            text = "Non-authoritative transport check",
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(top = 4.dp, bottom = 12.dp)
        )
        OutlinedButton(
            onClick = onBack,
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 12.dp)
        ) {
            Text("Back to developer tools")
        }
        blockingNotice.value?.let { notice ->
            Text(
                text = notice,
                color = MaterialTheme.colorScheme.error,
                style = MaterialTheme.typography.bodyMedium,
                modifier = Modifier.padding(bottom = 12.dp)
            )
        }
        unityHost(Modifier.weight(1f)) { outcome ->
            when (val decision = UnityBridgeSmokePolicy.decide(outcome)) {
                is UnityBridgeSmokeDecision.SafeReturn -> onSafeReturn(decision.notice)
                is UnityBridgeSmokeDecision.StayVisible -> {
                    blockingNotice.value = decision.notice
                }
            }
        }
    }
}
