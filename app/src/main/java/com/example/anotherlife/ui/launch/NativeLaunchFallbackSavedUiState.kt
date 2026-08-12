package com.example.anotherlife.ui.launch

internal enum class NativeLaunchSavedUiMode(val persistenceKey: String) {
    StaticFallback("static"),
    RecoverableFallback("recoverable"),
    TerminalRecovery("terminal"),
    GenericRecovery("generic")
}

internal data class NativeLaunchFallbackSavedUiState(
    val contentId: String,
    val semanticVersion: Int,
    val mode: NativeLaunchSavedUiMode,
    val reason: NativeLaunchFallbackReason?
)

/**
 * Saves only code-owned presentation context. Attempt generations, retry authority, callbacks,
 * runtime objects, receipts, identifiers, raw errors, completion, and navigation are excluded.
 */
internal object NativeLaunchFallbackSavedUiStateCodec {
    private const val FIELD_COUNT = 4
    private const val NO_REASON = "none"

    fun capture(
        snapshot: NativeLaunchFallbackSnapshot,
        presentation: NativeLaunchFallbackPresentation
    ): NativeLaunchFallbackSavedUiState? {
        if (
            snapshot.state != NativeLaunchFallbackState.FallbackVisible &&
            snapshot.state != NativeLaunchFallbackState.TerminalRecovery
        ) {
            return null
        }

        val safePresentation = NativeLaunchFallbackPresentationMapper
            .sanitizedForDisplay(presentation)
        val descriptor = safePresentation.descriptor ?: return genericState()
        val mode = when (descriptor.message) {
            NativeLaunchMessage.StaticPresentation -> NativeLaunchSavedUiMode.StaticFallback
            NativeLaunchMessage.FallbackAvailable -> NativeLaunchSavedUiMode.RecoverableFallback
            NativeLaunchMessage.TerminalRecovery -> NativeLaunchSavedUiMode.TerminalRecovery
            NativeLaunchMessage.GenericRecovery -> NativeLaunchSavedUiMode.GenericRecovery
            NativeLaunchMessage.Preparing,
            NativeLaunchMessage.Stopping -> NativeLaunchSavedUiMode.GenericRecovery
        }

        return sanitize(
            NativeLaunchFallbackSavedUiState(
                contentId = descriptor.contentId,
                semanticVersion = descriptor.semanticVersion,
                mode = mode,
                reason = snapshot.fallbackReason
            )
        )
    }

    fun encode(state: NativeLaunchFallbackSavedUiState): List<Any> {
        val safeState = sanitize(state)
        return listOf(
            safeState.contentId,
            safeState.semanticVersion,
            safeState.mode.persistenceKey,
            safeState.reason.persistenceKey()
        )
    }

    fun decode(fields: List<Any?>): NativeLaunchFallbackSavedUiState {
        if (fields.size != FIELD_COUNT) return genericState()

        val contentId = fields[0] as? String ?: return genericState()
        val semanticVersion = fields[1] as? Int ?: return genericState()
        val modeKey = fields[2] as? String ?: return genericState()
        val reasonKey = fields[3] as? String ?: return genericState()
        val mode = NativeLaunchSavedUiMode.entries.firstOrNull {
            it.persistenceKey == modeKey
        } ?: return genericState()

        return sanitize(
            NativeLaunchFallbackSavedUiState(
                contentId = contentId,
                semanticVersion = semanticVersion,
                mode = mode,
                reason = reasonFromPersistenceKey(reasonKey)
            )
        )
    }

    fun restoredPresentation(
        state: NativeLaunchFallbackSavedUiState,
        freshGeneration: Long
    ): NativeLaunchFallbackPresentation {
        require(freshGeneration >= 0L)
        val safeState = sanitize(state)
        val message = when (safeState.mode) {
            NativeLaunchSavedUiMode.StaticFallback -> NativeLaunchMessage.StaticPresentation
            NativeLaunchSavedUiMode.RecoverableFallback ->
                NativeLaunchMessage.GenericRecovery
            NativeLaunchSavedUiMode.TerminalRecovery -> NativeLaunchMessage.TerminalRecovery
            NativeLaunchSavedUiMode.GenericRecovery -> NativeLaunchMessage.GenericRecovery
        }

        return NativeLaunchFallbackPresentation(
            generation = freshGeneration,
            owner = NativeLaunchSurfaceOwner.Native,
            descriptor = NativeLaunchSemanticDescriptor(message = message),
            showIndeterminateProgress = false,
            retryAvailable = false,
            exitAvailable = true
        )
    }

    private fun sanitize(
        state: NativeLaunchFallbackSavedUiState
    ): NativeLaunchFallbackSavedUiState {
        if (
            state.contentId != NATIVE_LAUNCH_CONTENT_ID ||
            state.semanticVersion != NATIVE_LAUNCH_SEMANTIC_VERSION
        ) {
            return genericState()
        }

        return when (state.mode) {
            NativeLaunchSavedUiMode.StaticFallback -> state.copy(
                reason = NativeLaunchFallbackReason.ReducedMotion
            )
            NativeLaunchSavedUiMode.RecoverableFallback -> state.copy(
                reason = state.reason.recoverableReason()
            )
            NativeLaunchSavedUiMode.TerminalRecovery -> state.copy(
                reason = state.reason.terminalReason()
            )
            NativeLaunchSavedUiMode.GenericRecovery -> genericState()
        }
    }

    private fun genericState(): NativeLaunchFallbackSavedUiState {
        return NativeLaunchFallbackSavedUiState(
            contentId = NATIVE_LAUNCH_CONTENT_ID,
            semanticVersion = NATIVE_LAUNCH_SEMANTIC_VERSION,
            mode = NativeLaunchSavedUiMode.GenericRecovery,
            reason = null
        )
    }

    private fun NativeLaunchFallbackReason?.recoverableReason():
        NativeLaunchFallbackReason {
        return when (this) {
            null,
            NativeLaunchFallbackReason.ReducedMotion,
            NativeLaunchFallbackReason.CleanupUncertain,
            NativeLaunchFallbackReason.GenerationExhausted ->
                NativeLaunchFallbackReason.UnknownFailure
            else -> this
        }
    }

    private fun NativeLaunchFallbackReason?.terminalReason():
        NativeLaunchFallbackReason {
        return when (this) {
            NativeLaunchFallbackReason.GenerationExhausted -> this
            else -> NativeLaunchFallbackReason.CleanupUncertain
        }
    }

    private fun NativeLaunchFallbackReason?.persistenceKey(): String {
        return when (this) {
            null -> NO_REASON
            NativeLaunchFallbackReason.ReducedMotion -> "reduced_motion"
            NativeLaunchFallbackReason.RuntimeUnavailable -> "runtime_unavailable"
            NativeLaunchFallbackReason.StartupFailed -> "startup_failed"
            NativeLaunchFallbackReason.RouteUnavailable -> "route_unavailable"
            NativeLaunchFallbackReason.ReadyTimeout -> "ready_timeout"
            NativeLaunchFallbackReason.MediaUnavailable -> "media_unavailable"
            NativeLaunchFallbackReason.MediaFailed -> "media_failed"
            NativeLaunchFallbackReason.CleanupUncertain -> "cleanup_uncertain"
            NativeLaunchFallbackReason.GenerationExhausted -> "generation_exhausted"
            NativeLaunchFallbackReason.UnknownFailure -> "unknown_failure"
        }
    }

    private fun reasonFromPersistenceKey(key: String): NativeLaunchFallbackReason? {
        return when (key) {
            NO_REASON -> null
            "reduced_motion" -> NativeLaunchFallbackReason.ReducedMotion
            "runtime_unavailable" -> NativeLaunchFallbackReason.RuntimeUnavailable
            "startup_failed" -> NativeLaunchFallbackReason.StartupFailed
            "route_unavailable" -> NativeLaunchFallbackReason.RouteUnavailable
            "ready_timeout" -> NativeLaunchFallbackReason.ReadyTimeout
            "media_unavailable" -> NativeLaunchFallbackReason.MediaUnavailable
            "media_failed" -> NativeLaunchFallbackReason.MediaFailed
            "cleanup_uncertain" -> NativeLaunchFallbackReason.CleanupUncertain
            "generation_exhausted" -> NativeLaunchFallbackReason.GenerationExhausted
            "unknown_failure" -> NativeLaunchFallbackReason.UnknownFailure
            else -> null
        }
    }
}
