package com.example.anotherlife.ui.unity

import android.view.View
import android.view.ViewGroup
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.example.anotherlife.MainActivity
import java.util.concurrent.atomic.AtomicReference
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class UnityBridgeSmokeShellTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<MainActivity>()

    @Test
    fun unavailableOutcomeReturnsToDebugWithoutEnteringGameplay() {
        val request = openSmokeAndReadRequest()
        val unavailable = outcomeJson(
            request = request,
            status = UnityRouteOutcomeStatus.Unavailable,
            diagnosticCode = "route.not_available"
        )

        reportOutcomeOffMain(unavailable)

        waitForText(
            "Unity bridge smoke route is unavailable as expected. Returned safely to Debug."
        )
        composeRule.onNodeWithText("Narrative Director Debug").assertIsDisplayed()
        composeRule.onNodeWithText("Unity bridge smoke").assertDoesNotExist()
        composeRule.onNodeWithText("Kingdom Management").assertDoesNotExist()
    }

    @Test
    fun unapprovedSuccessStaysVisibleAndAppliesNothing() {
        val request = openSmokeAndReadRequest()

        reportOutcomeOffMain(outcomeJson(request, UnityRouteOutcomeStatus.Success))

        waitForText("Unity bridge smoke returned an unapproved success. No result was applied.")
        composeRule.onNodeWithText("Unity bridge smoke").assertIsDisplayed()
        composeRule.onNodeWithText("Back to developer tools").assertHasClickAction()
        composeRule.onNodeWithText("Narrative Director Debug").assertDoesNotExist()
        composeRule.onNodeWithText("Kingdom Management").assertDoesNotExist()
    }

    private fun openSmokeAndReadRequest(): UnityRouteRequest {
        composeRule.onNodeWithText("Debug").performClick()
        composeRule.onNodeWithText("Open Unity bridge smoke").performClick()
        waitForText("Non-authoritative transport check")

        val request = AtomicReference<UnityRouteRequest?>()
        composeRule.runOnUiThread {
            val host = findUnityRuntimeContainer(composeRule.activity.window.decorView)
            request.set(host?.activeRequestForTesting())
        }
        return requireNotNull(request.get()).also {
            check(it.routeId == UnityBridgeSmokePolicy.ROUTE_ID)
        }
    }

    private fun reportOutcomeOffMain(outcome: String) {
        Thread { UnityBridgeCallbacks.reportOutcome(outcome) }.apply {
            start()
            join()
        }
    }

    private fun outcomeJson(
        request: UnityRouteRequest,
        status: UnityRouteOutcomeStatus,
        diagnosticCode: String? = null
    ): String {
        return buildJsonObject {
            put("contractVersion", UNITY_BRIDGE_CONTRACT_VERSION)
            put("requestId", request.requestId)
            put("routeId", request.routeId)
            put("status", status.wireValue)
            diagnosticCode?.let { put("diagnosticCode", it) }
        }.toString()
    }

    private fun findUnityRuntimeContainer(view: View): UnityRuntimeContainer? {
        if (view is UnityRuntimeContainer) return view
        if (view !is ViewGroup) return null
        for (index in 0 until view.childCount) {
            findUnityRuntimeContainer(view.getChildAt(index))?.let { return it }
        }
        return null
    }

    private fun waitForText(text: String) {
        composeRule.waitUntil(timeoutMillis = 10_000) {
            composeRule.onAllNodes(hasText(text))
                .fetchSemanticsNodes()
                .isNotEmpty()
        }
    }
}
