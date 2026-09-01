package com.example.anotherlife.ui.unity

import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier

@Suppress("UNUSED_PARAMETER")
@Composable
fun UnityBridgeSmokeRoute(
    onBack: () -> Unit,
    onSafeReturn: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    // ShellRoutePolicy redirects this debug-only destination before composition.
}
