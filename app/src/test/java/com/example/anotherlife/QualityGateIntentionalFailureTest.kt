package com.example.anotherlife

import org.junit.Assert.fail
import org.junit.Test

/**
 * Disposable hosted-gate proof for issue #155.
 *
 * This test is intentionally red. Its branch and pull request must never merge.
 */
class QualityGateIntentionalFailureTest {
    @Test
    fun intentionalFailureProvesHostedAndroidGateBlocksMerge() {
        fail("AL-QG-LIVE-PROOF-ANDROID-FAILURE: intentional negative fixture; never merge")
    }
}
