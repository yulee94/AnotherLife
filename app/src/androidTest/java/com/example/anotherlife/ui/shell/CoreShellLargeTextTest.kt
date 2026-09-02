package com.example.anotherlife.ui.shell

import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertTextEquals
import androidx.compose.ui.test.hasScrollToIndexAction
import androidx.compose.ui.test.hasTestTag
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollToNode
import androidx.compose.ui.unit.Density
import com.example.anotherlife.data.simulation.KingdomState
import com.example.anotherlife.ui.simulation.WarzoneMapScreen
import com.example.anotherlife.ui.theme.AnotherLifeTheme
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class CoreShellLargeTextTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun warzoneUsesSingleColumnAndKeepsStatusAndActionsInsideCards() {
        setLargeTextContent {
            WarzoneMapScreen(
                state = KingdomState(),
                onAttack = {}
            )
        }

        val ironPeaks = composeRule.onNodeWithTag("territory_Iron Peaks")
            .fetchSemanticsNode()
            .boundsInRoot
        val silverWoods = composeRule.onNodeWithTag("territory_Silver Woods")
            .fetchSemanticsNode()
            .boundsInRoot
        assertTrue(
            "Large-text territory cards must use one column.",
            silverWoods.top >= ironPeaks.bottom
        )

        val grid = composeRule.onNode(hasScrollToIndexAction())
        grid.performScrollToNode(hasTestTag("territory_Golden Plains"))
        composeRule.onNodeWithTag("territory_status_Golden Plains")
            .assertTextEquals("SAFE / DEFENDED")
            .assertIsDisplayed()
        assertContained(
            parentTag = "territory_Golden Plains",
            childTag = "territory_status_Golden Plains"
        )

        grid.performScrollToNode(hasTestTag("territory_Neutral Borderlands"))
        composeRule.onNodeWithTag("territory_action_Neutral Borderlands")
            .assertIsDisplayed()
        assertContained(
            parentTag = "territory_Neutral Borderlands",
            childTag = "territory_action_Neutral Borderlands"
        )
    }

    @Test
    fun largeTextNavigationKeepsVisibleSelectionAndAccessibleDestinations() {
        setLargeTextContent {
            AnotherLifeShell()
        }

        composeRule.onNodeWithTag("selected_navigation_label")
            .assertTextEquals("Kingdom")
            .assertIsDisplayed()

        listOf("Kingdom", "Dossier", "Academy", "Warzone", "Debug").forEach { label ->
            composeRule.onNodeWithContentDescription(label).assertIsDisplayed()
        }

        composeRule.onNodeWithContentDescription("Warzone").performClick()
        composeRule.onNodeWithTag("selected_navigation_label")
            .assertTextEquals("Warzone")
            .assertIsDisplayed()
    }

    private fun setLargeTextContent(content: @Composable () -> Unit) {
        composeRule.setContent {
            AnotherLifeTheme {
                val deviceDensity = LocalDensity.current
                CompositionLocalProvider(
                    LocalDensity provides Density(
                        density = deviceDensity.density,
                        fontScale = 1.5f
                    )
                ) {
                    content()
                }
            }
        }
    }

    private fun assertContained(parentTag: String, childTag: String) {
        val parent = composeRule.onNodeWithTag(parentTag)
            .fetchSemanticsNode()
            .boundsInRoot
        val child = composeRule.onNodeWithTag(childTag)
            .fetchSemanticsNode()
            .boundsInRoot

        assertTrue("$childTag starts above $parentTag.", child.top >= parent.top)
        assertTrue("$childTag ends below $parentTag.", child.bottom <= parent.bottom)
        assertTrue("$childTag starts left of $parentTag.", child.left >= parent.left)
        assertTrue("$childTag ends right of $parentTag.", child.right <= parent.right)
    }
}
