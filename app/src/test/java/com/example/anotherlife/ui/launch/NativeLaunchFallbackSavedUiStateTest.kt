package com.example.anotherlife.ui.launch

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class NativeLaunchFallbackSavedUiStateTest {
    @Test
    fun liveAttemptStatesAreNeverCaptured() {
        listOf(
            NativeLaunchFallbackState.NativeReady,
            NativeLaunchFallbackState.StartingUnity,
            NativeLaunchFallbackState.UnityActive,
            NativeLaunchFallbackState.StoppingUnity
        ).forEach { state ->
            val snapshot = snapshot(state = state, generation = 81L)
            val saved = NativeLaunchFallbackSavedUiStateCodec.capture(
                snapshot,
                NativeLaunchFallbackPresentationMapper.from(snapshot)
            )

            assertNull(saved)
        }
    }

    @Test
    fun encodedStateContainsOnlyFourBoundedCodeOwnedFields() {
        val snapshot = snapshot(
            state = NativeLaunchFallbackState.FallbackVisible,
            generation = 42L,
            reason = NativeLaunchFallbackReason.MediaFailed,
            retryAvailable = true
        )
        val saved = requireNotNull(
            NativeLaunchFallbackSavedUiStateCodec.capture(
                snapshot,
                NativeLaunchFallbackPresentationMapper.from(snapshot)
            )
        )

        val encoded = NativeLaunchFallbackSavedUiStateCodec.encode(saved)

        assertEquals(
            listOf(
                "anotherlife.launch.native-fallback",
                1,
                "recoverable",
                "media_failed"
            ),
            encoded
        )
        assertFalse(encoded.contains(42L))
        assertFalse(encoded.contains(true))

        val restored = NativeLaunchFallbackSavedUiStateCodec.restoredPresentation(
            NativeLaunchFallbackSavedUiStateCodec.decode(encoded),
            freshGeneration = 43L
        )
        assertEquals(NativeLaunchMessage.GenericRecovery, restored.descriptor?.message)
        assertFalse(restored.retryAvailable)
    }

    @Test
    fun staticFallbackRoundTripRestoresNoRuntimeOrRetryAuthority() {
        val snapshot = snapshot(
            state = NativeLaunchFallbackState.FallbackVisible,
            generation = 4L,
            reason = NativeLaunchFallbackReason.ReducedMotion
        )
        val captured = requireNotNull(
            NativeLaunchFallbackSavedUiStateCodec.capture(
                snapshot,
                NativeLaunchFallbackPresentationMapper.from(snapshot)
            )
        )
        val decoded = NativeLaunchFallbackSavedUiStateCodec.decode(
            NativeLaunchFallbackSavedUiStateCodec.encode(captured)
        )

        val presentation = NativeLaunchFallbackSavedUiStateCodec.restoredPresentation(
            decoded,
            freshGeneration = 9L
        )

        assertEquals(9L, presentation.generation)
        assertEquals(NativeLaunchMessage.StaticPresentation, presentation.descriptor?.message)
        assertFalse(presentation.showIndeterminateProgress)
        assertFalse(presentation.retryAvailable)
        assertTrue(presentation.exitAvailable)
    }

    @Test
    fun semanticIdentityMismatchFailsClosedToGenericRecovery() {
        val mismatched = listOf(
            listOf("unexpected.content", 1, "recoverable", "media_failed"),
            listOf(NATIVE_LAUNCH_CONTENT_ID, 2, "recoverable", "media_failed")
        )

        mismatched.forEach { fields ->
            val decoded = NativeLaunchFallbackSavedUiStateCodec.decode(fields)
            val presentation = NativeLaunchFallbackSavedUiStateCodec.restoredPresentation(
                decoded,
                freshGeneration = 10L
            )

            assertEquals(NativeLaunchSavedUiMode.GenericRecovery, decoded.mode)
            assertEquals(NativeLaunchMessage.GenericRecovery, presentation.descriptor?.message)
            assertFalse(presentation.retryAvailable)
            assertTrue(presentation.exitAvailable)
        }
    }

    @Test
    fun malformedOrUnknownFieldsCannotRestoreArbitraryContent() {
        val malformed = listOf<List<Any?>>(
            emptyList(),
            listOf(NATIVE_LAUNCH_CONTENT_ID, "1", "recoverable", "media_failed"),
            listOf(NATIVE_LAUNCH_CONTENT_ID, 1, "unexpected", "media_failed"),
            listOf(NATIVE_LAUNCH_CONTENT_ID, 1, "recoverable", "raw secret text")
        )

        malformed.forEach { fields ->
            val decoded = NativeLaunchFallbackSavedUiStateCodec.decode(fields)
            val reencoded = NativeLaunchFallbackSavedUiStateCodec.encode(decoded)

            if (fields.size != 4 || fields.getOrNull(2) == "unexpected") {
                assertEquals(NativeLaunchSavedUiMode.GenericRecovery, decoded.mode)
            }
            assertFalse(reencoded.contains("raw secret text"))
        }
    }

    @Test
    fun terminalRoundTripPreservesOnlyTypedReasonAndExitRecovery() {
        val snapshot = snapshot(
            state = NativeLaunchFallbackState.TerminalRecovery,
            generation = 7L,
            reason = NativeLaunchFallbackReason.GenerationExhausted
        )
        val captured = requireNotNull(
            NativeLaunchFallbackSavedUiStateCodec.capture(
                snapshot,
                NativeLaunchFallbackPresentationMapper.from(snapshot)
            )
        )
        val decoded = NativeLaunchFallbackSavedUiStateCodec.decode(
            NativeLaunchFallbackSavedUiStateCodec.encode(captured)
        )
        val restored = NativeLaunchFallbackSavedUiStateCodec.restoredPresentation(
            decoded,
            freshGeneration = 11L
        )

        assertEquals(NativeLaunchFallbackReason.GenerationExhausted, decoded.reason)
        assertEquals(NativeLaunchMessage.TerminalRecovery, restored.descriptor?.message)
        assertFalse(restored.retryAvailable)
        assertTrue(restored.exitAvailable)
    }

    @Test(expected = IllegalArgumentException::class)
    fun negativeFreshGenerationIsRejected() {
        NativeLaunchFallbackSavedUiStateCodec.restoredPresentation(
            NativeLaunchFallbackSavedUiState(
                contentId = NATIVE_LAUNCH_CONTENT_ID,
                semanticVersion = NATIVE_LAUNCH_SEMANTIC_VERSION,
                mode = NativeLaunchSavedUiMode.GenericRecovery,
                reason = null
            ),
            freshGeneration = -1L
        )
    }

    private fun snapshot(
        state: NativeLaunchFallbackState,
        generation: Long,
        reason: NativeLaunchFallbackReason? = null,
        retryAvailable: Boolean = false
    ): NativeLaunchFallbackSnapshot {
        return NativeLaunchFallbackSnapshot(
            state = state,
            generation = generation,
            fallbackReason = reason,
            retryAvailable = retryAvailable
        )
    }
}
