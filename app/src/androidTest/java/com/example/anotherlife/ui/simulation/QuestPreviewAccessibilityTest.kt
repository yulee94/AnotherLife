package com.example.anotherlife.ui.simulation

import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.hasScrollToIndexAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollToIndex
import androidx.compose.ui.test.performScrollToNode
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.example.anotherlife.MainActivity
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class QuestPreviewAccessibilityTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<MainActivity>()

    @Test
    fun previewUsesApprovedReadOnlySourceAndProvidesAccessibleBackNavigation() {
        openQuestPreview()

        composeRule.onNodeWithText("Quest source").assertIsDisplayed()
        composeRule.onNodeWithText("The First Signal").assertIsDisplayed()
        composeRule.onNodeWithText("Sky Castle Anomaly").assertIsDisplayed()
        composeRule.onNodeWithText("Start Story").assertDoesNotExist()
        composeRule.onNodeWithText("CLAIM REWARD: 500 GOLD").assertDoesNotExist()
        composeRule.onNodeWithText("SKY_CASTLE").assertDoesNotExist()
        val questList = composeRule.onNode(hasScrollToIndexAction())
        questList.performScrollToIndex(5)
        composeRule.onNodeWithContentDescription(
            "Objective 1 of 3: Speak with Captain Valerius."
        )
            .assertIsDisplayed()

        questList.performScrollToNode(hasText("Runtime hook requested"))
        composeRule.onNodeWithText("Runtime hook requested")
            .assertIsDisplayed()

        questList.performScrollToIndex(0)
        composeRule.onNodeWithContentDescription("Back to narrative debug")
            .assertHasClickAction()
            .performClick()
        composeRule.onNodeWithText("Narrative Director Debug").assertIsDisplayed()
    }

    @Test
    fun activityRecreationPreservesNestedPreviewAndParentNavigationSelection() {
        openQuestPreview()

        composeRule.activityRule.scenario.recreate()
        waitForText("Quest source")

        composeRule.onNodeWithText("Quest source").assertIsDisplayed()
        composeRule.onNodeWithText("Debug").assertIsSelected()
    }

    private fun openQuestPreview() {
        composeRule.onNodeWithText("Debug").performClick()
        composeRule.onNodeWithText("Open quest source").performClick()
        waitForText("The First Signal")
    }

    private fun waitForText(text: String) {
        composeRule.waitUntil(timeoutMillis = 10_000) {
            composeRule.onAllNodes(hasText(text))
                .fetchSemanticsNodes()
                .isNotEmpty()
        }
    }
}
