package com.example.anotherlife.ui.shell

import com.example.anotherlife.ui.navigation.Route
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ShellRoutePolicyTest {
    @Test
    fun debugBuildShowsNarrativeDebugEntry() {
        val routes = ShellRoutePolicy.bottomNavigationRoutes(debugToolsEnabled = true)

        assertTrue(routes.contains(Route.NarrativeDebug))
        assertFalse(routes.contains(Route.Quest))
        assertEquals(
            listOf(Route.Kingdom, Route.Dossier, Route.Champion, Route.Warzone, Route.NarrativeDebug),
            routes
        )
    }

    @Test
    fun releaseBuildHidesNarrativeDebugEntry() {
        val routes = ShellRoutePolicy.bottomNavigationRoutes(debugToolsEnabled = false)

        assertFalse(routes.contains(Route.NarrativeDebug))
        assertFalse(routes.contains(Route.Quest))
        assertEquals(listOf(Route.Kingdom, Route.Dossier, Route.Champion, Route.Warzone), routes)
    }

    @Test
    fun releaseRouteResolutionReturnsVisibleFallbackFromNarrativeDebug() {
        val resolution = ShellRoutePolicy.resolveRoute(Route.NarrativeDebug, debugToolsEnabled = false)

        assertTrue(resolution is RouteResolution.Rejected)
        assertEquals(Route.Kingdom, resolution.route)
        assertEquals(ShellRoutePolicy.DEBUG_ROUTE_UNAVAILABLE_MESSAGE, (resolution as RouteResolution.Rejected).message)
    }

    @Test
    fun debugRouteResolutionKeepsNarrativeDebug() {
        assertEquals(Route.NarrativeDebug, ShellRoutePolicy.resolveRoute(Route.NarrativeDebug, debugToolsEnabled = true).route)
    }

    @Test
    fun questPreviewIsAllowedOnlyInDebugBuilds() {
        val debugResolution = ShellRoutePolicy.resolveRoute(Route.Quest, debugToolsEnabled = true)
        val releaseResolution = ShellRoutePolicy.resolveRoute(Route.Quest, debugToolsEnabled = false)

        assertEquals(Route.Quest, debugResolution.route)
        assertTrue(releaseResolution is RouteResolution.Rejected)
        assertEquals(Route.Kingdom, releaseResolution.route)
        assertEquals(
            ShellRoutePolicy.QUEST_ROUTE_UNAVAILABLE_MESSAGE,
            (releaseResolution as RouteResolution.Rejected).message
        )
    }

    @Test
    fun questPreviewKeepsNarrativeDebugSelectedInBottomNavigation() {
        assertEquals(
            Route.NarrativeDebug,
            ShellRoutePolicy.navigationSelection(Route.Quest)
        )
        assertEquals(
            Route.NarrativeDebug,
            ShellRoutePolicy.navigationSelection(Route.UnityBridgeSmoke)
        )
        assertEquals(
            Route.Kingdom,
            ShellRoutePolicy.navigationSelection(Route.Kingdom)
        )
    }

    @Test
    fun unityBridgeSmokeIsDebugOnlyAndFallsBackSafelyInRelease() {
        val debugResolution = ShellRoutePolicy.resolveRoute(
            Route.UnityBridgeSmoke,
            debugToolsEnabled = true
        )
        val releaseResolution = ShellRoutePolicy.resolveRoute(
            Route.UnityBridgeSmoke,
            debugToolsEnabled = false
        )

        assertEquals(Route.UnityBridgeSmoke, debugResolution.route)
        assertTrue(releaseResolution is RouteResolution.Rejected)
        assertEquals(Route.Kingdom, releaseResolution.route)
        assertEquals(
            ShellRoutePolicy.UNITY_BRIDGE_SMOKE_UNAVAILABLE_MESSAGE,
            (releaseResolution as RouteResolution.Rejected).message
        )
    }

    @Test
    fun routePersistenceKeysRoundTripAndRejectUnknownValues() {
        val routes = listOf(
            Route.Kingdom,
            Route.Dossier,
            Route.Champion,
            Route.Battle,
            Route.Warzone,
            Route.Quest,
            Route.NarrativeDebug,
            Route.UnityBridgeSmoke
        )

        routes.forEach { route ->
            assertEquals(
                route,
                ShellRoutePolicy.routeFromPersistenceKey(
                    ShellRoutePolicy.persistenceKey(route)
                )
            )
        }
        assertEquals(null, ShellRoutePolicy.routeFromPersistenceKey("unknown"))
    }

    @Test
    fun releaseStateRestorationRemovesHistoricalNarrativeDebug() {
        val restored = listOf(
            Route.Kingdom,
            Route.NarrativeDebug,
            Route.Quest,
            Route.Dossier
        )

        val sanitized = ShellRoutePolicy.sanitizeBackStack(restored, debugToolsEnabled = false)

        assertEquals(listOf(Route.Kingdom, Route.Dossier), sanitized.routes)
        assertEquals(null, sanitized.rejectedTopRoute)
    }

    @Test
    fun releaseStateRestorationFallsBackOnceWhenCurrentRouteIsNarrativeDebug() {
        val restored = listOf(Route.Kingdom, Route.Dossier, Route.NarrativeDebug)

        val sanitized = ShellRoutePolicy.sanitizeBackStack(restored, debugToolsEnabled = false)

        assertEquals(
            listOf(Route.Kingdom, Route.Dossier, Route.Kingdom),
            sanitized.routes
        )
        assertEquals(Route.NarrativeDebug, sanitized.rejectedTopRoute?.requestedRoute)
        assertEquals(ShellRoutePolicy.DEBUG_ROUTE_UNAVAILABLE_MESSAGE, sanitized.rejectedTopRoute?.message)
    }

    @Test
    fun releaseStateRestorationFallsBackOnceWhenCurrentRouteIsQuestPreview() {
        val restored = listOf(Route.Kingdom, Route.Dossier, Route.Quest)

        val sanitized = ShellRoutePolicy.sanitizeBackStack(restored, debugToolsEnabled = false)

        assertEquals(
            listOf(Route.Kingdom, Route.Dossier, Route.Kingdom),
            sanitized.routes
        )
        assertEquals(Route.Quest, sanitized.rejectedTopRoute?.requestedRoute)
        assertEquals(
            ShellRoutePolicy.QUEST_ROUTE_UNAVAILABLE_MESSAGE,
            sanitized.rejectedTopRoute?.message
        )
    }

    @Test
    fun releaseStateRestorationAvoidsAdjacentFallbackDuplicates() {
        val restored = listOf(Route.Kingdom, Route.NarrativeDebug)

        val sanitized = ShellRoutePolicy.sanitizeBackStack(restored, debugToolsEnabled = false)

        assertEquals(listOf(Route.Kingdom), sanitized.routes)
        assertEquals(Route.NarrativeDebug, sanitized.rejectedTopRoute?.requestedRoute)
    }

    @Test
    fun normalNavigationRoutesRemainStableInRelease() {
        val restored = listOf(Route.Kingdom, Route.Dossier, Route.Champion, Route.Warzone)

        assertEquals(restored, ShellRoutePolicy.sanitizeBackStack(restored, debugToolsEnabled = false).routes)
    }
}
