package com.example.anotherlife.ui.theme

import java.io.File
import javax.xml.parsers.DocumentBuilderFactory
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test
import org.w3c.dom.Element

class LaunchThemeResourceTest {
    @Test
    fun launchPaletteMatchesTheApprovedLightAndDarkFoundations() {
        assertEquals(
            "#F8F1DF",
            colorValue("app/src/main/res/values/launch_colors.xml", "launch_background")
        )
        assertEquals(
            "#10130F",
            colorValue("app/src/main/res/values-night/launch_colors.xml", "launch_background")
        )
    }

    @Test
    fun launchThemesSelectLegibleDayNightParentsAndReuseTheLauncherIcon() {
        val lightPlatformBase = style(
            "app/src/main/res/values/themes.xml",
            "Theme.AnotherLife.PlatformBase"
        )
        val darkPlatformBase = style(
            "app/src/main/res/values-night/themes.xml",
            "Theme.AnotherLife.PlatformBase"
        )
        val lightApi27Base = style(
            "app/src/main/res/values-v27/themes.xml",
            "Theme.AnotherLife.Base"
        )
        val darkApi27Base = style(
            "app/src/main/res/values-night-v27/themes.xml",
            "Theme.AnotherLife.Base"
        )
        val lightTheme = style(
            "app/src/main/res/values/themes.xml",
            "Theme.AnotherLife"
        )
        val platformSplash = style(
            "app/src/main/res/values-v31/themes.xml",
            "Theme.AnotherLife"
        )

        assertEquals(
            "android:style/Theme.Material.Light.NoActionBar",
            lightPlatformBase.parent
        )
        assertEquals(
            "android:style/Theme.Material.NoActionBar",
            darkPlatformBase.parent
        )
        assertLaunchWindowUsesSemanticBackground(lightPlatformBase)
        assertLaunchWindowUsesSemanticBackground(darkPlatformBase)
        assertEquals("true", lightPlatformBase.items["android:windowLightStatusBar"])
        assertEquals("false", darkPlatformBase.items["android:windowLightStatusBar"])
        assertEquals("Theme.AnotherLife.PlatformBase", lightApi27Base.parent)
        assertEquals("Theme.AnotherLife.PlatformBase", darkApi27Base.parent)
        assertEquals("true", lightApi27Base.items["android:windowLightNavigationBar"])
        assertEquals("false", darkApi27Base.items["android:windowLightNavigationBar"])
        assertEquals("Theme.AnotherLife.Base", lightTheme.parent)
        assertEquals("Theme.AnotherLife.Base", platformSplash.parent)
        assertEquals(
            "@color/launch_background",
            platformSplash.items["android:windowSplashScreenBackground"]
        )
        assertFalse(platformSplash.items.containsKey("android:windowSplashScreenAnimatedIcon"))
    }

    private fun assertLaunchWindowUsesSemanticBackground(style: StyleSpec) {
        listOf(
            "android:windowBackground",
            "android:statusBarColor",
            "android:navigationBarColor"
        ).forEach { itemName ->
            assertEquals("@color/launch_background", style.items[itemName])
        }
    }

    private fun colorValue(path: String, colorName: String): String {
        val colors = document(path).getElementsByTagName("color")
        for (index in 0 until colors.length) {
            val color = colors.item(index) as Element
            if (color.getAttribute("name") == colorName) {
                return color.textContent.trim().uppercase()
            }
        }
        error("Missing color $colorName in $path")
    }

    private fun style(path: String, styleName: String): StyleSpec {
        val styles = document(path).getElementsByTagName("style")
        for (index in 0 until styles.length) {
            val style = styles.item(index) as Element
            if (style.getAttribute("name") != styleName) {
                continue
            }

            val items = buildMap {
                val itemNodes = style.getElementsByTagName("item")
                for (itemIndex in 0 until itemNodes.length) {
                    val item = itemNodes.item(itemIndex) as Element
                    put(item.getAttribute("name"), item.textContent.trim())
                }
            }
            return StyleSpec(
                parent = style.getAttribute("parent"),
                items = items
            )
        }
        error("Missing style $styleName in $path")
    }

    private fun document(path: String) = DocumentBuilderFactory.newInstance()
        .newDocumentBuilder()
        .parse(repositoryFile(path))

    private fun repositoryFile(path: String): File {
        var current = File(requireNotNull(System.getProperty("user.dir"))).canonicalFile
        while (true) {
            if (File(current, "settings.gradle.kts").isFile) {
                return File(current, path)
            }
            current = current.parentFile ?: error("Repository root not found from user.dir")
        }
    }

    private data class StyleSpec(
        val parent: String,
        val items: Map<String, String>
    )
}
