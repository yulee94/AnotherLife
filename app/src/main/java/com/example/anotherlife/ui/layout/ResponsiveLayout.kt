package com.example.anotherlife.ui.layout

import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.testTag
import com.example.anotherlife.BuildConfig

internal const val LargeTextFontScale = 1.3f

internal fun usesLargeTextLayout(fontScale: Float): Boolean =
    fontScale >= LargeTextFontScale

@Composable
internal fun usesLargeTextLayout(): Boolean =
    usesLargeTextLayout(LocalDensity.current.fontScale)

internal fun Modifier.debugTestTag(tag: String): Modifier =
    if (BuildConfig.DEBUG) testTag(tag) else this
