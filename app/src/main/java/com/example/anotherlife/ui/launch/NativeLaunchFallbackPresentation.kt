package com.example.anotherlife.ui.launch

internal const val NATIVE_LAUNCH_CONTENT_ID = "anotherlife.launch.native-fallback"
internal const val NATIVE_LAUNCH_SEMANTIC_VERSION = 1

internal enum class NativeLaunchSurfaceOwner {
    Native,
    Unity
}

internal enum class NativeLaunchMessage {
    Preparing,
    StaticPresentation,
    Stopping,
    FallbackAvailable,
    TerminalRecovery,
    GenericRecovery
}

internal data class NativeLaunchSemanticDescriptor(
    val contentId: String = NATIVE_LAUNCH_CONTENT_ID,
    val semanticVersion: Int = NATIVE_LAUNCH_SEMANTIC_VERSION,
    val message: NativeLaunchMessage,
    val decorativeVisualId: String? = null
)

internal data class NativeLaunchFallbackPresentation(
    val generation: Long,
    val owner: NativeLaunchSurfaceOwner,
    val descriptor: NativeLaunchSemanticDescriptor?,
    val showIndeterminateProgress: Boolean,
    val retryAvailable: Boolean,
    val exitAvailable: Boolean
) {
    val isVisible: Boolean
        get() = owner == NativeLaunchSurfaceOwner.Native && descriptor != null
}

internal object NativeLaunchFallbackPresentationMapper {
    fun from(snapshot: NativeLaunchFallbackSnapshot): NativeLaunchFallbackPresentation {
        return when (snapshot.state) {
            NativeLaunchFallbackState.NativeReady -> nativePresentation(
                snapshot = snapshot,
                message = NativeLaunchMessage.Preparing,
                showProgress = true
            )

            NativeLaunchFallbackState.StartingUnity -> nativePresentation(
                snapshot = snapshot,
                message = NativeLaunchMessage.Preparing,
                showProgress = true
            )

            NativeLaunchFallbackState.UnityActive -> NativeLaunchFallbackPresentation(
                generation = snapshot.generation,
                owner = NativeLaunchSurfaceOwner.Unity,
                descriptor = null,
                showIndeterminateProgress = false,
                retryAvailable = false,
                exitAvailable = false
            )

            NativeLaunchFallbackState.StoppingUnity -> nativePresentation(
                snapshot = snapshot,
                message = NativeLaunchMessage.Stopping,
                showProgress = true,
                exitAvailable = false
            )

            NativeLaunchFallbackState.FallbackVisible -> nativePresentation(
                snapshot = snapshot,
                message = if (
                    snapshot.fallbackReason == NativeLaunchFallbackReason.ReducedMotion
                ) {
                    NativeLaunchMessage.StaticPresentation
                } else {
                    NativeLaunchMessage.FallbackAvailable
                },
                showProgress = false
            )

            NativeLaunchFallbackState.TerminalRecovery -> nativePresentation(
                snapshot = snapshot,
                message = NativeLaunchMessage.TerminalRecovery,
                showProgress = false
            )
        }
    }

    fun sanitizedForDisplay(
        presentation: NativeLaunchFallbackPresentation
    ): NativeLaunchFallbackPresentation {
        if (presentation.owner == NativeLaunchSurfaceOwner.Unity) {
            return presentation.copy(
                descriptor = null,
                showIndeterminateProgress = false,
                retryAvailable = false,
                exitAvailable = false
            )
        }

        val descriptor = presentation.descriptor
        val descriptorIsCurrent = descriptor?.contentId == NATIVE_LAUNCH_CONTENT_ID &&
            descriptor.semanticVersion == NATIVE_LAUNCH_SEMANTIC_VERSION

        if (descriptorIsCurrent) return presentation

        return presentation.copy(
            descriptor = NativeLaunchSemanticDescriptor(
                message = NativeLaunchMessage.GenericRecovery
            ),
            showIndeterminateProgress = false,
            retryAvailable = false,
            exitAvailable = true
        )
    }

    private fun nativePresentation(
        snapshot: NativeLaunchFallbackSnapshot,
        message: NativeLaunchMessage,
        showProgress: Boolean,
        exitAvailable: Boolean = true
    ): NativeLaunchFallbackPresentation {
        return NativeLaunchFallbackPresentation(
            generation = snapshot.generation,
            owner = NativeLaunchSurfaceOwner.Native,
            descriptor = NativeLaunchSemanticDescriptor(message = message),
            showIndeterminateProgress = showProgress,
            retryAvailable = snapshot.retryAvailable,
            exitAvailable = exitAvailable
        )
    }
}
