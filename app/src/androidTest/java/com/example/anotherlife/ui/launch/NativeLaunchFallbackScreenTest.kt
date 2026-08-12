package com.example.anotherlife.ui.launch

import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.example.anotherlife.ui.theme.AnotherLifeTheme
import java.util.concurrent.atomic.AtomicLong
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class NativeLaunchFallbackScreenTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun recoverableFallbackExposesOneSemanticOwnerAndGenerationBoundActions() {
        val retryGeneration = AtomicLong(-1L)
        val exitGeneration = AtomicLong(-1L)
        val presentation = NativeLaunchFallbackPresentationMapper.from(
            NativeLaunchFallbackSnapshot(
                state = NativeLaunchFallbackState.FallbackVisible,
                generation = 7L,
                fallbackReason = NativeLaunchFallbackReason.MediaFailed,
                retryAvailable = true
            )
        )

        composeRule.setContent {
            AnotherLifeTheme(darkTheme = true) {
                NativeLaunchFallbackScreen(
                    presentation = presentation,
                    onRetry = retryGeneration::set,
                    onExit = exitGeneration::set
                )
            }
        }

        composeRule.onNodeWithText("Another Life").assertIsDisplayed()
        composeRule.onNodeWithText(
            "The 3D experience is unavailable. You can retry or exit."
        ).assertIsDisplayed()
        composeRule.onNodeWithText("Retry")
            .assertHasClickAction()
            .performClick()
        composeRule.onNodeWithText("Exit")
            .assertHasClickAction()
            .performClick()

        assertEquals(7L, retryGeneration.get())
        assertEquals(7L, exitGeneration.get())
        composeRule.onNodeWithText("Start Game").assertDoesNotExist()
    }

    @Test
    fun preparingStateProvidesIndeterminateStatusWithoutFakePercentage() {
        val presentation = NativeLaunchFallbackPresentationMapper.from(
            NativeLaunchFallbackSnapshot(
                state = NativeLaunchFallbackState.StartingUnity,
                generation = 3L
            )
        )

        composeRule.setContent {
            AnotherLifeTheme(darkTheme = false) {
                NativeLaunchFallbackScreen(
                    presentation = presentation,
                    onRetry = {},
                    onExit = {}
                )
            }
        }

        composeRule.onNodeWithText("Preparing the 3D experience.").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Preparing the 3D experience.")
            .assertIsDisplayed()
        composeRule.onNodeWithText("Retry").assertDoesNotExist()
        composeRule.onNodeWithText("0%").assertDoesNotExist()
        composeRule.onNodeWithText("100%").assertDoesNotExist()
    }

    @Test
    fun unityOwnedStateHasNoNativeSemanticTree() {
        val presentation = NativeLaunchFallbackPresentationMapper.from(
            NativeLaunchFallbackSnapshot(
                state = NativeLaunchFallbackState.UnityActive,
                generation = 4L
            )
        )

        composeRule.setContent {
            AnotherLifeTheme {
                NativeLaunchFallbackScreen(
                    presentation = presentation,
                    onRetry = {},
                    onExit = {}
                )
            }
        }

        composeRule.onNodeWithText("Another Life").assertDoesNotExist()
        composeRule.onNodeWithText("Retry").assertDoesNotExist()
        composeRule.onNodeWithText("Exit").assertDoesNotExist()
    }

    @Test
    fun mismatchedSemanticVersionShowsOnlyGenericRecovery() {
        val presentation = NativeLaunchFallbackPresentation(
            generation = 9L,
            owner = NativeLaunchSurfaceOwner.Native,
            descriptor = NativeLaunchSemanticDescriptor(
                semanticVersion = NATIVE_LAUNCH_SEMANTIC_VERSION + 1,
                message = NativeLaunchMessage.Preparing
            ),
            showIndeterminateProgress = true,
            retryAvailable = true,
            exitAvailable = true
        )

        composeRule.setContent {
            AnotherLifeTheme {
                NativeLaunchFallbackScreen(
                    presentation = presentation,
                    onRetry = {},
                    onExit = {}
                )
            }
        }

        composeRule.onNodeWithText(
            "Another Life cannot continue from this screen. Exit and reopen the app."
        ).assertIsDisplayed()
        composeRule.onNodeWithText("Preparing the 3D experience.").assertDoesNotExist()
        composeRule.onNodeWithText("Retry").assertDoesNotExist()
        composeRule.onNodeWithText("Exit").assertIsDisplayed()
    }

    @Test
    fun teardownStatusExposesNoActionUntilCleanupIsConfirmed() {
        val presentation = NativeLaunchFallbackPresentationMapper.from(
            NativeLaunchFallbackSnapshot(
                state = NativeLaunchFallbackState.StoppingUnity,
                generation = 12L,
                fallbackReason = NativeLaunchFallbackReason.MediaFailed
            )
        )

        composeRule.setContent {
            AnotherLifeTheme {
                NativeLaunchFallbackScreen(
                    presentation = presentation,
                    onRetry = {},
                    onExit = {}
                )
            }
        }

        composeRule.onNodeWithText("Closing the 3D experience safely.")
            .assertIsDisplayed()
        composeRule.onNodeWithText("Retry").assertDoesNotExist()
        composeRule.onNodeWithText("Exit").assertDoesNotExist()
    }
}
