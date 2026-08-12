package com.example.anotherlife.ui.launch

/**
 * Native launch ownership states. The fallback remains independent of Unity and a media decoder.
 */
internal enum class NativeLaunchFallbackState {
    NativeReady,
    StartingUnity,
    UnityActive,
    StoppingUnity,
    FallbackVisible,
    TerminalRecovery
}

internal enum class NativeLaunchPresentationPreference {
    Cinematic,
    StaticFallback
}

internal enum class NativeLaunchFailure {
    RuntimeUnavailable,
    ConstructionFailed,
    RouteAdmissionFailed,
    ReadyTimeout,
    MediaUnavailable,
    MediaFailed,
    Unknown
}

/**
 * What Android can prove about the Unity owner when a failure is received.
 */
internal enum class NativeLaunchRuntimeOwnership {
    NeverCreated,
    Active,
    Uncertain
}

internal enum class NativeLaunchFallbackReason {
    ReducedMotion,
    RuntimeUnavailable,
    StartupFailed,
    RouteUnavailable,
    ReadyTimeout,
    MediaUnavailable,
    MediaFailed,
    CleanupUncertain,
    GenerationExhausted,
    UnknownFailure
}

internal data class NativeLaunchFallbackSnapshot(
    val state: NativeLaunchFallbackState,
    val generation: Long,
    val fallbackReason: NativeLaunchFallbackReason? = null,
    val retryAvailable: Boolean = false
)

internal sealed interface NativeLaunchFallbackEffect {
    data class StartUnity(val generation: Long) : NativeLaunchFallbackEffect

    data class StopUnity(val generation: Long) : NativeLaunchFallbackEffect
}

internal data class NativeLaunchFallbackTransition(
    val snapshot: NativeLaunchFallbackSnapshot,
    val effect: NativeLaunchFallbackEffect? = null
)
