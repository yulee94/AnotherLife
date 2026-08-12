package com.example.anotherlife.ui.launch

internal fun interface NativeLaunchAttemptGenerationSource {
    fun nextGeneration(): Long?
}

internal class SequentialNativeLaunchAttemptGenerationSource(
    initialGeneration: Long = 0L
) : NativeLaunchAttemptGenerationSource {
    private var currentGeneration = initialGeneration

    init {
        require(initialGeneration >= 0L)
    }

    @Synchronized
    override fun nextGeneration(): Long? {
        if (currentGeneration == Long.MAX_VALUE) return null
        currentGeneration += 1L
        return currentGeneration
    }
}

/**
 * Prevents callback generations from being reused across recreated Android hosts in one process.
 * A process restart creates a new source, but callbacks from the terminated process cannot survive.
 */
internal object NativeLaunchProcessGenerationSource : NativeLaunchAttemptGenerationSource {
    private val source = SequentialNativeLaunchAttemptGenerationSource()

    override fun nextGeneration(): Long? = source.nextGeneration()
}
