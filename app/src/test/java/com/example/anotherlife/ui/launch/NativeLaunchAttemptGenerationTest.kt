package com.example.anotherlife.ui.launch

import java.util.Collections
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.concurrent.thread
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class NativeLaunchAttemptGenerationTest {
    @Test
    fun oneSourceNeverReusesAGenerationAcrossRecreatedCoordinators() {
        val source = SequentialNativeLaunchAttemptGenerationSource()
        val firstHost = NativeLaunchFallbackCoordinator(generationSource = source)

        val first = firstHost.begin(NativeLaunchPresentationPreference.Cinematic)
        firstHost.fail(
            generation = first.snapshot.generation,
            failure = NativeLaunchFailure.RuntimeUnavailable,
            ownership = NativeLaunchRuntimeOwnership.NeverCreated
        )
        val retry = firstHost.retry(first.snapshot.generation)

        val recreatedHost = NativeLaunchFallbackCoordinator(generationSource = source)
        val recreated = recreatedHost.begin(NativeLaunchPresentationPreference.Cinematic)

        assertEquals(1L, first.snapshot.generation)
        assertEquals(2L, retry.snapshot.generation)
        assertEquals(3L, recreated.snapshot.generation)
    }

    @Test
    fun concurrentReservationsAreUniqueAndMonotonic() {
        val source = SequentialNativeLaunchAttemptGenerationSource()
        val ready = CountDownLatch(1)
        val done = CountDownLatch(32)
        val generations = Collections.synchronizedList(mutableListOf<Long>())

        repeat(32) {
            thread {
                ready.await()
                source.nextGeneration()?.let(generations::add)
                done.countDown()
            }
        }
        ready.countDown()

        assertTrue(done.await(5, TimeUnit.SECONDS))
        assertEquals((1L..32L).toList(), generations.sorted())
    }

    @Test
    fun exhaustedSourceReturnsNoGeneration() {
        val source = SequentialNativeLaunchAttemptGenerationSource(Long.MAX_VALUE)

        assertNull(source.nextGeneration())
        assertNull(source.nextGeneration())
    }
}
