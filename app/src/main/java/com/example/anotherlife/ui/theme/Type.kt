package com.example.anotherlife.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp

val Typography = Typography(
    displayLarge = BrandTextStyle(FontWeight.SemiBold, 48, 56),
    displayMedium = BrandTextStyle(FontWeight.SemiBold, 40, 48),
    displaySmall = BrandTextStyle(FontWeight.SemiBold, 34, 42),
    headlineLarge = BrandTextStyle(FontWeight.SemiBold, 30, 38),
    headlineMedium = BrandTextStyle(FontWeight.SemiBold, 26, 34),
    headlineSmall = BrandTextStyle(FontWeight.SemiBold, 22, 30),
    titleLarge = BrandTextStyle(FontWeight.SemiBold, 20, 28),
    titleMedium = BrandTextStyle(FontWeight.Medium, 16, 24),
    titleSmall = BrandTextStyle(FontWeight.Medium, 14, 20),
    bodyLarge = BrandTextStyle(FontWeight.Normal, 16, 24),
    bodyMedium = BrandTextStyle(FontWeight.Normal, 14, 21),
    bodySmall = BrandTextStyle(FontWeight.Normal, 12, 18),
    labelLarge = BrandTextStyle(FontWeight.Medium, 14, 20),
    labelMedium = BrandTextStyle(FontWeight.Medium, 12, 16),
    labelSmall = BrandTextStyle(FontWeight.Medium, 11, 16)
)

private fun BrandTextStyle(
    weight: FontWeight,
    sizeSp: Int,
    lineHeightSp: Int
) = TextStyle(
    fontFamily = FontFamily.Default,
    fontWeight = weight,
    fontSize = sizeSp.sp,
    lineHeight = lineHeightSp.sp,
    letterSpacing = 0.sp
)
