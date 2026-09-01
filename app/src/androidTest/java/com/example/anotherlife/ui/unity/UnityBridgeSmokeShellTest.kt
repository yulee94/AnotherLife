package com.example.anotherlife.ui.unity

import android.view.View
import android.view.ViewGroup
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assert
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.example.anotherlife.MainActivity
import java.util.concurrent.atomic.AtomicReference
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
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
    fun safeReturnNoticeSurvivesRecreationWithoutRestoringTheUnityHost() {
        val request = openSmokeAndReadRequest()

        reportOutcomeOffMain(
            outcomeJson(
                request = request,
                status = UnityRouteOutcomeStatus.Unavailable,
                diagnosticCode = "route.not_available"
            )
        )

        val safeReturnMessage =
            "Unity bridge smoke route is unavailable as expected. Returned safely to Debug."
        waitForText(safeReturnMessage)
        assertPoliteLiveRegion(safeReturnMessage)

        composeRule.activityRule.scenario.recreate()

        waitForText(safeReturnMessage)
        assertPoliteLiveRegion(safeReturnMessage)
        composeRule.onNodeWithText("Narrative Director Debug").assertIsDisplayed()
        composeRule.onNodeWithText("Unity bridge smoke").assertDoesNotExist()
        assertFalse(UnityBridgeCallbacks.hasActiveRegistrationForTesting())
        composeRule.runOnUiThread {
            assertNull(findUnityRuntimeContainer(composeRule.activity.window.decorView))
        }

        composeRule.onNodeWithText("Kingdom").performClick()

        composeRule.onNodeWithText(safeReturnMessage).assertDoesNotExist()
        composeRule.onNodeWithText("Kingdom Management").assertIsDisplayed()
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

    @Test
    fun backNavigationDisposesSmokeHostAndDropsLateOutcome() {
        val disposedRequest = openSmokeAndReadRequest()

        composeRule.onNodeWithText("Back to developer tools").performClick()
        waitForText("Narrative Director Debug")
        InstrumentationRegistry.getInstrumentation().waitForIdleSync()

        assertFalse(UnityBridgeCallbacks.hasActiveRegistrationForTesting())
        composeRule.runOnUiThread {
            assertNull(findUnityRuntimeContainer(composeRule.activity.window.decorView))
        }

        reportOutcomeOffMain(
            outcomeJson(
                request = disposedRequest,
                status = UnityRouteOutcomeStatus.Unavailable,
                diagnosticCode = "route.not_available"
            )
        )

        assertFalse(UnityBridgeCallbacks.hasActiveRegistrationForTesting())
        composeRule.onNodeWithText("Narrative Director Debug").assertIsDisplayed()
        composeRule.onNodeWithText("Unity bridge smoke").assertDoesNotExist()
        composeRule.onNodeWithText(
            "Unity bridge smoke route is unavailable as expected. Returned safely to Debug."
        ).assertDoesNotExist()
        composeRule.onNodeWithText("Kingdom Management").assertDoesNotExist()
    }

    @Test
    fun recreationCreatesNewRequestAndRejectsLatePriorOutcome() {
        val priorRequest = openSmokeAndReadRequest()

        composeRule.activityRule.scenario.recreate()
        waitForText("Non-authoritative transport check")
        val currentRequest = readActiveRequest()
        assertNotEquals(priorRequest.requestId, currentRequest.requestId)
        assertEquals(priorRequest.routeId, currentRequest.routeId)

        reportOutcomeOffMain(
            outcomeJson(
                request = priorRequest,
                status = UnityRouteOutcomeStatus.Unavailable,
                diagnosticCode = "route.not_available"
            )
        )
        InstrumentationRegistry.getInstrumentation().waitForIdleSync()

        assertEquals(
            "Unity bridge unavailable\nCode: bridge.request_mismatch",
            readHostStatus()
        )
        composeRule.onNodeWithText("Unity bridge smoke").assertIsDisplayed()
        composeRule.onNodeWithText("Narrative Director Debug").assertDoesNotExist()
        assertEquals(currentRequest, readActiveRequest())

        reportOutcomeOffMain(
            outcomeJson(
                request = currentRequest,
                status = UnityRouteOutcomeStatus.Unavailable,
                diagnosticCode = "route.not_available"
            )
        )

        waitForText(
            "Unity bridge smoke route is unavailable as expected. Returned safely to Debug."
        )
        composeRule.onNodeWithText("Narrative Director Debug").assertIsDisplayed()
        composeRule.onNodeWithText("Unity bridge smoke").assertDoesNotExist()
        composeRule.onNodeWithText("Kingdom Management").assertDoesNotExist()
    }

    private fun openSmokeAndReadRequest(): UnityRouteRequest {
        composeRule.onNodeWithText("Debug").performClick()
        composeRule.onNodeWithText("Open Unity bridge smoke").performClick()
        waitForText("Non-authoritative transport check")

        return readActiveRequest().also {
            check(it.routeId == UnityBridgeSmokePolicy.ROUTE_ID)
        }
    }

    private fun readActiveRequest(): UnityRouteRequest {
        val request = AtomicReference<UnityRouteRequest?>()
        composeRule.runOnUiThread {
            val host = findUnityRuntimeContainer(composeRule.activity.window.decorView)
            request.set(host?.activeRequestForTesting())
        }
        return requireNotNull(request.get())
    }

    private fun readHostStatus(): String {
        val status = AtomicReference<String?>()
        composeRule.runOnUiThread {
            val host = findUnityRuntimeContainer(composeRule.activity.window.decorView)
            status.set(host?.statusTextForTesting())
        }
        return requireNotNull(status.get())
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

    private fun assertPoliteLiveRegion(text: String) {
        composeRule.onNodeWithText(text).assert(
            SemanticsMatcher.expectValue(
                SemanticsProperties.LiveRegion,
                LiveRegionMode.Polite
            )
        )
    }
}
