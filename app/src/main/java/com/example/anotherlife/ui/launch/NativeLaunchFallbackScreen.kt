package com.example.anotherlife.ui.launch

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.paneTitle
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.example.anotherlife.R

@Composable
internal fun NativeLaunchFallbackScreen(
    presentation: NativeLaunchFallbackPresentation,
    onRetry: (generation: Long) -> Unit,
    onExit: (generation: Long) -> Unit,
    modifier: Modifier = Modifier
) {
    val safePresentation = NativeLaunchFallbackPresentationMapper
        .sanitizedForDisplay(presentation)
    if (!safePresentation.isVisible) return

    val descriptor = requireNotNull(safePresentation.descriptor)
    val title = stringResource(R.string.launch_fallback_title)
    val message = stringResource(descriptor.message.stringResource())

    Surface(
        modifier = modifier
            .fillMaxSize()
            .semantics { paneTitle = title },
        color = MaterialTheme.colorScheme.background,
        contentColor = MaterialTheme.colorScheme.onBackground
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 32.dp, vertical = 48.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.headlineLarge,
                textAlign = TextAlign.Center,
                modifier = Modifier.semantics { heading() }
            )
            Spacer(modifier = Modifier.height(24.dp))

            if (safePresentation.showIndeterminateProgress) {
                CircularProgressIndicator(
                    modifier = Modifier
                        .size(48.dp)
                        .semantics { contentDescription = message }
                )
                Spacer(modifier = Modifier.height(24.dp))
            }

            Text(
                text = message,
                style = MaterialTheme.typography.bodyLarge,
                textAlign = TextAlign.Center,
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite }
            )

            if (safePresentation.retryAvailable || safePresentation.exitAvailable) {
                Spacer(modifier = Modifier.height(32.dp))
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(
                        12.dp,
                        Alignment.CenterHorizontally
                    ),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    if (safePresentation.retryAvailable) {
                        Button(onClick = { onRetry(safePresentation.generation) }) {
                            Text(stringResource(R.string.launch_fallback_retry))
                        }
                    }
                    if (safePresentation.exitAvailable) {
                        TextButton(onClick = { onExit(safePresentation.generation) }) {
                            Text(stringResource(R.string.launch_fallback_exit))
                        }
                    }
                }
            }
        }
    }
}

private fun NativeLaunchMessage.stringResource(): Int {
    return when (this) {
        NativeLaunchMessage.Preparing -> R.string.launch_fallback_preparing
        NativeLaunchMessage.StaticPresentation -> R.string.launch_fallback_static
        NativeLaunchMessage.Stopping -> R.string.launch_fallback_stopping
        NativeLaunchMessage.FallbackAvailable -> R.string.launch_fallback_available
        NativeLaunchMessage.TerminalRecovery -> R.string.launch_fallback_terminal
        NativeLaunchMessage.GenericRecovery -> R.string.launch_fallback_generic
    }
}
