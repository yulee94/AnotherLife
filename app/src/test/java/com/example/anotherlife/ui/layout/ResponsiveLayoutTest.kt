package com.example.anotherlife.ui.layout

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ResponsiveLayoutTest {
    @Test
    fun largeTextLayoutStartsAtDeclaredThreshold() {
        assertFalse(usesLargeTextLayout(LargeTextFontScale - 0.01f))
        assertTrue(usesLargeTextLayout(LargeTextFontScale))
        assertTrue(usesLargeTextLayout(LargeTextFontScale + 0.5f))
    }
}
