package com.example.anotherlife.ui.theme

import androidx.compose.material3.ColorScheme
import androidx.compose.material3.Typography
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.unit.sp
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class AnotherLifeThemeContractTest {
    @Test
    fun sampleComposePaletteValuesAreNotBrandTokens() {
        val sampleValues = setOf(
            0xFF673AB7.toInt(),
            0xFFE91E63.toInt(),
            0xFFFFC107.toInt(),
            0xFFD0BCFF.toInt(),
            0xFFFF80AB.toInt(),
            0xFFFFD700.toInt()
        )

        val brandValues = listOf(
            AnotherLifeLightColorScheme.primary,
            AnotherLifeLightColorScheme.secondary,
            AnotherLifeLightColorScheme.tertiary,
            AnotherLifeLightColorScheme.background,
            AnotherLifeDarkColorScheme.primary,
            AnotherLifeDarkColorScheme.secondary,
            AnotherLifeDarkColorScheme.tertiary,
            AnotherLifeDarkColorScheme.background
        ).map { it.toArgb() }.toSet()

        assertTrue(sampleValues.intersect(brandValues).isEmpty())
    }

    @Test
    fun importantMaterialRolesRemainVisuallyDistinct() {
        assertDistinctRoles(AnotherLifeLightColorScheme)
        assertDistinctRoles(AnotherLifeDarkColorScheme)
    }

    @Test
    fun materialRoleContrastMeetsReadableThresholds() {
        assertReadablePairs(AnotherLifeLightColorScheme)
        assertReadablePairs(AnotherLifeDarkColorScheme)
    }

    @Test
    fun typographyUsesPlatformFontsWithZeroLetterSpacing() {
        val styles = Typography.allStyles()
        assertEquals(15, styles.size)
        styles.forEach { style ->
            assertEquals(0.sp, style.letterSpacing)
            assertTrue(style.fontSize.value > 0f)
            assertTrue(style.lineHeight.value >= style.fontSize.value)
        }
    }

    private fun assertReadablePairs(scheme: ColorScheme) {
        assertContrastAtLeast(scheme.onPrimary, scheme.primary, 4.5f, "onPrimary")
        assertContrastAtLeast(scheme.onPrimaryContainer, scheme.primaryContainer, 4.5f, "onPrimaryContainer")
        assertContrastAtLeast(scheme.onSecondary, scheme.secondary, 4.5f, "onSecondary")
        assertContrastAtLeast(scheme.onSecondaryContainer, scheme.secondaryContainer, 4.5f, "onSecondaryContainer")
        assertContrastAtLeast(scheme.onTertiary, scheme.tertiary, 4.5f, "onTertiary")
        assertContrastAtLeast(scheme.onTertiaryContainer, scheme.tertiaryContainer, 4.5f, "onTertiaryContainer")
        assertContrastAtLeast(scheme.onError, scheme.error, 4.5f, "onError")
        assertContrastAtLeast(scheme.onErrorContainer, scheme.errorContainer, 4.5f, "onErrorContainer")
        assertContrastAtLeast(scheme.onBackground, scheme.background, 4.5f, "onBackground")
        assertContrastAtLeast(scheme.onSurface, scheme.surface, 4.5f, "onSurface")
        assertContrastAtLeast(scheme.onSurfaceVariant, scheme.surfaceVariant, 4.5f, "onSurfaceVariant")
        assertContrastAtLeast(scheme.inverseOnSurface, scheme.inverseSurface, 4.5f, "inverseOnSurface")
    }

    private fun assertDistinctRoles(scheme: ColorScheme) {
        assertNotEquals(scheme.primary.toArgb(), scheme.secondary.toArgb())
        assertNotEquals(scheme.primary.toArgb(), scheme.tertiary.toArgb())
        assertNotEquals(scheme.secondary.toArgb(), scheme.tertiary.toArgb())
        assertNotEquals(scheme.error.toArgb(), scheme.primary.toArgb())
    }

    private fun assertContrastAtLeast(
        foreground: Color,
        background: Color,
        minimum: Float,
        label: String
    ) {
        val lighter = maxOf(foreground.luminance(), background.luminance())
        val darker = minOf(foreground.luminance(), background.luminance())
        val ratio = (lighter + 0.05f) / (darker + 0.05f)
        assertTrue("$label contrast $ratio below $minimum", ratio >= minimum)
    }

    private fun Typography.allStyles(): List<TextStyle> = listOf(
        displayLarge,
        displayMedium,
        displaySmall,
        headlineLarge,
        headlineMedium,
        headlineSmall,
        titleLarge,
        titleMedium,
        titleSmall,
        bodyLarge,
        bodyMedium,
        bodySmall,
        labelLarge,
        labelMedium,
        labelSmall
    )
}
