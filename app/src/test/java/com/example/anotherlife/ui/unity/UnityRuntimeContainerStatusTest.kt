package com.example.anotherlife.ui.unity

import org.junit.Assert.assertEquals
import org.junit.Test

class UnityRuntimeContainerStatusTest {
    @Test
    fun observerReceivesCurrentSnapshotBeforeLaterChanges() {
        val initial = snapshot(UnityRuntimeContainerPhase.RequestingOwnership)
        val publisher = UnityRuntimeContainerStatusPublisher(initial)
        val received = mutableListOf<UnityRuntimeContainerSnapshot>()

        publisher.publish(snapshot(UnityRuntimeContainerPhase.Activating))
        publisher.observe(UnityRuntimeContainerObserver(received::add))
        val active = snapshot(
            phase = UnityRuntimeContainerPhase.Active,
            ownership = UnityRuntimeContainerOwnership.Active
        )
        publisher.publish(active)

        assertEquals(
            listOf(snapshot(UnityRuntimeContainerPhase.Activating), active),
            received
        )
    }

    @Test
    fun observerFailureCannotChangePublishedEvidence() {
        val publisher = UnityRuntimeContainerStatusPublisher(
            snapshot(UnityRuntimeContainerPhase.RequestingOwnership)
        )
        publisher.observe(UnityRuntimeContainerObserver { error("synthetic observer failure") })
        val failed = snapshot(
            phase = UnityRuntimeContainerPhase.Failed,
            ownership = UnityRuntimeContainerOwnership.Uncertain,
            teardown = UnityRuntimeContainerTeardownEvidence.Uncertain,
            failure = UnityRuntimeContainerFailure.ConstructionFailed
        )

        publisher.publish(failed)

        assertEquals(failed, publisher.snapshot())
    }

    @Test
    fun destroyedEvidenceCannotBeOverwrittenByLateRuntimeSignals() {
        val publisher = UnityRuntimeContainerStatusPublisher(
            snapshot(UnityRuntimeContainerPhase.Active, UnityRuntimeContainerOwnership.Active)
        )
        val destroyed = snapshot(
            phase = UnityRuntimeContainerPhase.Destroyed,
            ownership = UnityRuntimeContainerOwnership.NeverCreated,
            teardown = UnityRuntimeContainerTeardownEvidence.Confirmed
        )
        publisher.publish(destroyed)

        publisher.publish(
            snapshot(UnityRuntimeContainerPhase.Active, UnityRuntimeContainerOwnership.Active)
        )

        assertEquals(destroyed, publisher.snapshot())
        assertEquals(
            UnityRuntimeContainerTeardownResult.Confirmed,
            publisher.teardownResult()
        )
    }

    @Test
    fun teardownEvidenceMapsFailClosed() {
        val publisher = UnityRuntimeContainerStatusPublisher(
            snapshot(UnityRuntimeContainerPhase.Destroying).copy(
                teardown = UnityRuntimeContainerTeardownEvidence.InProgress
            )
        )

        assertEquals(
            UnityRuntimeContainerTeardownResult.AwaitingCleanup,
            publisher.teardownResult()
        )

        publisher.publish(
            snapshot(
                phase = UnityRuntimeContainerPhase.Destroyed,
                ownership = UnityRuntimeContainerOwnership.Uncertain,
                teardown = UnityRuntimeContainerTeardownEvidence.Uncertain
            )
        )

        assertEquals(
            UnityRuntimeContainerTeardownResult.Uncertain,
            publisher.teardownResult()
        )
    }

    private fun snapshot(
        phase: UnityRuntimeContainerPhase,
        ownership: UnityRuntimeContainerOwnership =
            UnityRuntimeContainerOwnership.NeverCreated,
        teardown: UnityRuntimeContainerTeardownEvidence =
            UnityRuntimeContainerTeardownEvidence.NotStarted,
        failure: UnityRuntimeContainerFailure? = null
    ) = UnityRuntimeContainerSnapshot(
        phase = phase,
        ownership = ownership,
        teardown = teardown,
        failure = failure
    )
}
