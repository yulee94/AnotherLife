package com.example.anotherlife.ui.theme

import androidx.compose.material3.ColorScheme
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.unit.sp
import kotlin.math.max
import kotlin.math.min
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class AnotherLifeThemeTokensTest {
    @Test
    fun dynamicWallpaperColorIsNotTheDefaultBrandSource() {
        assertFalse(AnotherLifeDynamicColorEnabledByDefault)
    }

    @Test
    fun darkAndLightBodyTextMeetsContrastRequirement() {
        assertContrastAtLeast(AnotherLifeDarkColorScheme.onBackground, AnotherLifeDarkColorScheme.background, 4.5)
        assertContrastAtLeast(AnotherLifeDarkColorScheme.onSurface, AnotherLifeDarkColorScheme.surface, 4.5)
        assertContrastAtLeast(AnotherLifeDarkColorScheme.onSurfaceVariant, AnotherLifeDarkColorScheme.surfaceVariant, 4.5)

        assertContrastAtLeast(AnotherLifeLightColorScheme.onBackground, AnotherLifeLightColorScheme.background, 4.5)
        assertContrastAtLeast(AnotherLifeLightColorScheme.onSurface, AnotherLifeLightColorScheme.surface, 4.5)
        assertContrastAtLeast(AnotherLifeLightColorScheme.onSurfaceVariant, AnotherLifeLightColorScheme.surfaceVariant, 4.5)
    }

    @Test
    fun componentAndErrorRolesMeetAccessibleContrast() {
        listOf(AnotherLifeDarkColorScheme, AnotherLifeLightColorScheme).forEach { scheme ->
            assertContrastAtLeast(scheme.onPrimary, scheme.primary, 4.5)
            assertContrastAtLeast(scheme.onPrimaryContainer, scheme.primaryContainer, 4.5)
            assertContrastAtLeast(scheme.onSecondary, scheme.secondary, 4.5)
            assertContrastAtLeast(scheme.onSecondaryContainer, scheme.secondaryContainer, 4.5)
            assertContrastAtLeast(scheme.onTertiary, scheme.tertiary, 4.5)
            assertContrastAtLeast(scheme.onError, scheme.error, 4.5)
            assertContrastAtLeast(scheme.onErrorContainer, scheme.errorContainer, 4.5)
        }
    }

    @Test
    fun importantMaterialRolesRemainVisuallyDistinct() {
        assertDistinctRoles(AnotherLifeDarkColorScheme)
        assertDistinctRoles(AnotherLifeLightColorScheme)
    }

    @Test
    fun typographyUsesPlatformFontsWithZeroLetterSpacing() {
        val styles = listOf(
            Typography.displayLarge,
            Typography.displayMedium,
            Typography.displaySmall,
            Typography.headlineLarge,
            Typography.headlineMedium,
            Typography.headlineSmall,
            Typography.titleLarge,
            Typography.titleMedium,
            Typography.titleSmall,
            Typography.bodyLarge,
            Typography.bodyMedium,
            Typography.bodySmall,
            Typography.labelLarge,
            Typography.labelMedium,
            Typography.labelSmall
        )

        styles.forEach { style ->
            assertEquals(0.sp, style.letterSpacing)
            assertTrue(style.fontSize.value > 0f)
            assertTrue(style.lineHeight.value >= style.fontSize.value)
        }
    }

    private fun assertDistinctRoles(scheme: ColorScheme) {
        assertNotEquals(scheme.primary.toArgb(), scheme.secondary.toArgb())
        assertNotEquals(scheme.primary.toArgb(), scheme.tertiary.toArgb())
        assertNotEquals(scheme.secondary.toArgb(), scheme.tertiary.toArgb())
        assertNotEquals(scheme.error.toArgb(), scheme.primary.toArgb())
    }

    private fun assertContrastAtLeast(foreground: Color, background: Color, minimum: Double) {
        val ratio = contrastRatio(foreground, background)
        assertTrue("Expected contrast >= $minimum but was $ratio", ratio >= minimum)
    }

    private fun contrastRatio(first: Color, second: Color): Double {
        val firstLuminance = relativeLuminance(first)
        val secondLuminance = relativeLuminance(second)
        return (max(firstLuminance, secondLuminance) + 0.05) /
            (min(firstLuminance, secondLuminance) + 0.05)
    }

    private fun relativeLuminance(color: Color): Double {
        val argb = color.toArgb()
        val red = linearize((argb shr 16 and 0xFF) / 255.0)
        val green = linearize((argb shr 8 and 0xFF) / 255.0)
        val blue = linearize((argb and 0xFF) / 255.0)
        return 0.2126 * red + 0.7152 * green + 0.0722 * blue
    }

    private fun linearize(channel: Double): Double =
        if (channel <= 0.03928) channel / 12.92 else Math.pow((channel + 0.055) / 1.055, 2.4)
}
