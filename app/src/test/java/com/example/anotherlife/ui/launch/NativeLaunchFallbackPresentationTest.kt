package com.example.anotherlife.ui.launch

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class NativeLaunchFallbackPresentationTest {
    @Test
    fun descriptorUsesOneStableVersionedSemanticIdentity() {
        NativeLaunchMessage.values().forEach { message ->
            val descriptor = NativeLaunchSemanticDescriptor(message = message)

            assertEquals("anotherlife.launch.native-fallback", descriptor.contentId)
            assertEquals(1, descriptor.semanticVersion)
            assertNull(descriptor.decorativeVisualId)
        }
    }

    @Test
    fun nativeReadyAndStartupExposeTruthfulIndeterminatePreparation() {
        listOf(
            NativeLaunchFallbackState.NativeReady,
            NativeLaunchFallbackState.StartingUnity
        ).forEach { state ->
            val presentation = presentation(state)

            assertTrue(presentation.isVisible)
            assertEquals(NativeLaunchSurfaceOwner.Native, presentation.owner)
            assertEquals(NativeLaunchMessage.Preparing, presentation.descriptor?.message)
            assertTrue(presentation.showIndeterminateProgress)
            assertFalse(presentation.retryAvailable)
            assertTrue(presentation.exitAvailable)
        }
    }

    @Test
    fun activeUnityOwnsTheSurfaceAndNativeSemanticsDisappear() {
        val presentation = presentation(NativeLaunchFallbackState.UnityActive)

        assertFalse(presentation.isVisible)
        assertEquals(NativeLaunchSurfaceOwner.Unity, presentation.owner)
        assertNull(presentation.descriptor)
        assertFalse(presentation.showIndeterminateProgress)
        assertFalse(presentation.retryAvailable)
        assertFalse(presentation.exitAvailable)
    }

    @Test
    fun stoppingShowsStatusButFencesEveryAction() {
        val presentation = presentation(NativeLaunchFallbackState.StoppingUnity)

        assertTrue(presentation.isVisible)
        assertEquals(NativeLaunchMessage.Stopping, presentation.descriptor?.message)
        assertTrue(presentation.showIndeterminateProgress)
        assertFalse(presentation.retryAvailable)
        assertFalse(presentation.exitAvailable)
    }

    @Test
    fun reducedMotionMapsToStaticPresentationWithoutRetry() {
        val presentation = NativeLaunchFallbackPresentationMapper.from(
            snapshot(
                state = NativeLaunchFallbackState.FallbackVisible,
                reason = NativeLaunchFallbackReason.ReducedMotion,
                retryAvailable = false
            )
        )

        assertTrue(presentation.isVisible)
        assertEquals(
            NativeLaunchMessage.StaticPresentation,
            presentation.descriptor?.message
        )
        assertFalse(presentation.showIndeterminateProgress)
        assertFalse(presentation.retryAvailable)
        assertTrue(presentation.exitAvailable)
    }

    @Test
    fun recoverableFailuresUseOneGenericPrivacySafePresentation() {
        val reasons = NativeLaunchFallbackReason.values().filterNot {
            it == NativeLaunchFallbackReason.ReducedMotion
        }

        reasons.forEach { reason ->
            val presentation = NativeLaunchFallbackPresentationMapper.from(
                snapshot(
                    state = NativeLaunchFallbackState.FallbackVisible,
                    reason = reason,
                    retryAvailable = true
                )
            )

            assertEquals(
                NativeLaunchMessage.FallbackAvailable,
                presentation.descriptor?.message
            )
            assertTrue(presentation.retryAvailable)
            assertFalse(presentation.showIndeterminateProgress)
        }
    }

    @Test
    fun terminalRecoveryOffersNoRetryOrProgress() {
        val presentation = NativeLaunchFallbackPresentationMapper.from(
            snapshot(
                state = NativeLaunchFallbackState.TerminalRecovery,
                reason = NativeLaunchFallbackReason.CleanupUncertain
            )
        )

        assertTrue(presentation.isVisible)
        assertEquals(
            NativeLaunchMessage.TerminalRecovery,
            presentation.descriptor?.message
        )
        assertFalse(presentation.showIndeterminateProgress)
        assertFalse(presentation.retryAvailable)
        assertTrue(presentation.exitAvailable)
    }

    @Test
    fun generationPassesThroughOnlyAsEphemeralUiCorrelation() {
        val presentation = NativeLaunchFallbackPresentationMapper.from(
            NativeLaunchFallbackSnapshot(
                state = NativeLaunchFallbackState.StartingUnity,
                generation = 42L
            )
        )

        assertEquals(42L, presentation.generation)
    }

    @Test
    fun missingOrMismatchedDescriptorsFailClosedToGenericRecovery() {
        val unsafePresentations = listOf(
            nativePresentation(descriptor = null),
            nativePresentation(
                descriptor = NativeLaunchSemanticDescriptor(
                    contentId = "unexpected.content",
                    message = NativeLaunchMessage.Preparing
                )
            ),
            nativePresentation(
                descriptor = NativeLaunchSemanticDescriptor(
                    semanticVersion = NATIVE_LAUNCH_SEMANTIC_VERSION + 1,
                    message = NativeLaunchMessage.Preparing
                )
            )
        )

        unsafePresentations.forEach { presentation ->
            val safePresentation = NativeLaunchFallbackPresentationMapper
                .sanitizedForDisplay(presentation)

            assertTrue(safePresentation.isVisible)
            assertEquals(
                NativeLaunchMessage.GenericRecovery,
                safePresentation.descriptor?.message
            )
            assertFalse(safePresentation.showIndeterminateProgress)
            assertFalse(safePresentation.retryAvailable)
            assertTrue(safePresentation.exitAvailable)
        }
    }

    @Test
    fun unityOwnershipSuppressesEvenMalformedNativePresentationData() {
        val safePresentation = NativeLaunchFallbackPresentationMapper
            .sanitizedForDisplay(
                NativeLaunchFallbackPresentation(
                    generation = 8L,
                    owner = NativeLaunchSurfaceOwner.Unity,
                    descriptor = NativeLaunchSemanticDescriptor(
                        message = NativeLaunchMessage.FallbackAvailable
                    ),
                    showIndeterminateProgress = true,
                    retryAvailable = true,
                    exitAvailable = true
                )
            )

        assertFalse(safePresentation.isVisible)
        assertNull(safePresentation.descriptor)
        assertFalse(safePresentation.showIndeterminateProgress)
        assertFalse(safePresentation.retryAvailable)
        assertFalse(safePresentation.exitAvailable)
    }

    private fun nativePresentation(
        descriptor: NativeLaunchSemanticDescriptor?
    ): NativeLaunchFallbackPresentation {
        return NativeLaunchFallbackPresentation(
            generation = 5L,
            owner = NativeLaunchSurfaceOwner.Native,
            descriptor = descriptor,
            showIndeterminateProgress = true,
            retryAvailable = true,
            exitAvailable = false
        )
    }

    private fun presentation(
        state: NativeLaunchFallbackState
    ): NativeLaunchFallbackPresentation {
        return NativeLaunchFallbackPresentationMapper.from(snapshot(state))
    }

    private fun snapshot(
        state: NativeLaunchFallbackState,
        reason: NativeLaunchFallbackReason? = null,
        retryAvailable: Boolean = false
    ): NativeLaunchFallbackSnapshot {
        return NativeLaunchFallbackSnapshot(
            state = state,
            generation = 1L,
            fallbackReason = reason,
            retryAvailable = retryAvailable
        )
    }
}
