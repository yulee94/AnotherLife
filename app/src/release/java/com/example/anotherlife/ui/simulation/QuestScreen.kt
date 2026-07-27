package com.example.anotherlife.ui.simulation

import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier

@Composable
fun QuestPreviewRoute(
    onBack: () -> Unit,
    modifier: Modifier = Modifier
) {
    @Suppress("UNUSED_VARIABLE")
    val ignoredBackHandler = onBack
    // ShellRoutePolicy redirects this debug-only destination before composition.
}
