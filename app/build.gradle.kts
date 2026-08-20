plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.jetbrains.kotlin.plugin.serialization)
}

val withUnity = providers.gradleProperty("withUnity")
    .map { it.toBooleanStrict() }
    .orElse(false)
val unityArtifactsRoot = rootProject.layout.projectDirectory.dir("unity/Builds/AndroidArtifacts")
val unityDebugAar = unityArtifactsRoot.file("debug/unityLibrary-debug.aar").asFile
val unityReleaseAar = unityArtifactsRoot.file("release/unityLibrary-release.aar").asFile
val packageVerifierPath = rootProject.layout.projectDirectory
    .file("tools/android_unity_package.py").asFile.absolutePath
val unityArtifactsPath = unityArtifactsRoot.asFile.absolutePath
val pythonCommand = providers.environmentVariable("PYTHON").getOrElse("python3")

val verifyUnityDebugPackageInput by tasks.registering(Exec::class) {
    group = "verification"
    description = "Verifies the opted-in Unity debug AAR and inventory."
    enabled = withUnity.get()
    commandLine(
        pythonCommand, packageVerifierPath, "--variant", "debug", "--verify-only",
        "--artifacts-dir", unityArtifactsPath,
    )
}

val verifyUnityReleasePackageInput by tasks.registering(Exec::class) {
    group = "verification"
    description = "Verifies the opted-in Unity release AAR and inventory."
    enabled = withUnity.get()
    commandLine(
        pythonCommand, packageVerifierPath, "--variant", "release", "--verify-only",
        "--artifacts-dir", unityArtifactsPath,
    )
}


android {
    namespace = "com.example.anotherlife"
    compileSdk {
        version = release(36) {
            minorApiLevel = 1
        }
    }

    defaultConfig {
        applicationId = "com.example.anotherlife"
        minSdk = 24
        targetSdk = 36
        versionCode = 1
        versionName = "1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        if (withUnity.get()) {
            ndk {
                abiFilters += "arm64-v8a"
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"))
        }
    }
    sourceSets {
        getByName("main") {
            assets.directories.add("../unity/Assets/AL/StreamingAssets/GameData")
        }
        getByName("debug") {
            assets.directories.add("../unity/Assets/StreamingAssets/AL/Narrative")
        }
    }
    androidResources {
        ignoreAssetsPatterns.add("!*.meta")
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
    buildFeatures {
        buildConfig = true
        compose = true
    }
}

dependencies {
    if (withUnity.get()) {
        debugImplementation(files(unityDebugAar))
        releaseImplementation(files(unityReleaseAar))
    }
    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.compose.material.icons.core)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.graphics)
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.navigation3.runtime)
    implementation(libs.androidx.navigation3.ui)
    implementation(libs.kotlinx.coroutines.core)
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.9.0")
    testImplementation(libs.junit)
    androidTestImplementation(libs.androidx.junit)
    androidTestImplementation(libs.androidx.runner)
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.compose.ui.test.junit4)
    debugImplementation(libs.androidx.compose.ui.test.manifest)
}

tasks.matching { it.name == "preDebugBuild" }.configureEach {
    dependsOn(verifyUnityDebugPackageInput)
}

tasks.matching { it.name == "preReleaseBuild" }.configureEach {
    dependsOn(verifyUnityReleasePackageInput)
}
