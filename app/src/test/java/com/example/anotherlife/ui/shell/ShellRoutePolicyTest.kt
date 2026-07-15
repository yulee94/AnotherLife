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
        assertEquals(
            listOf(Route.Kingdom, Route.Dossier, Route.Champion, Route.Warzone, Route.NarrativeDebug),
            routes
        )
    }

    @Test
    fun releaseBuildHidesNarrativeDebugEntry() {
        val routes = ShellRoutePolicy.bottomNavigationRoutes(debugToolsEnabled = false)

        assertFalse(routes.contains(Route.NarrativeDebug))
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
    fun releaseStateRestorationRemovesHistoricalNarrativeDebug() {
        val restored = listOf(Route.Kingdom, Route.NarrativeDebug, Route.Dossier)

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
