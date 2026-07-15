# Android Dependency Reproducibility Record

**Status date:** 2026-07-15
**Owner:** Codex
**Issue:** #159
**Original validation baseline:** `ecb8c43ebd8860e47840ac259e682431f00ece6d`
**Current PR base:** `e2bfecb031e5bb5eb185d354db96a2b7d5c8f319`

## Dynamic Alias Classification

`libs.material` is consumed by `app/build.gradle.kts` and resolved from `com.google.android.material:material:1.4.+` to `1.4.0` on `debugCompileClasspath`.

The following dynamic catalog aliases were unused by build scripts and source references at the time of this change and were removed instead of pinned:

- `androidx-compose-adaptive`
- `androidx-compose-adaptive-layout`
- `androidx-compose-adaptive-navigation3`
- `androidx-lifecycle-viewmodel-navigation3`

## Selected Pin

`material = "1.4.0"` was selected because Gradle dependency insight resolved the existing dynamic request `1.4.+` to `1.4.0`. This change preserves the validated artifact instead of upgrading to a newer Material release.

## Baseline Evidence

`./gradlew.bat --version` reported:

```text
Gradle 9.4.1
Kotlin 2.3.0
Launcher JVM 21.0.10 JetBrains
Daemon JVM Compatible with Java 21
```

`dependencyInsight` for `com.google.android.material:material` on `debugCompileClasspath` reported:

```text
com.google.android.material:material:1.4.+ -> 1.4.0
```

Repository configuration already uses `RepositoriesMode.FAIL_ON_PROJECT_REPOS`, with Google and Maven Central declared in `settings.gradle.kts`.

## Post-Change Validation

After pinning `material = "1.4.0"` and removing unused dynamic aliases:

```text
./gradlew.bat :app:dependencyInsight --configuration debugCompileClasspath --dependency com.google.android.material:material
```

reported:

```text
com.google.android.material:material:1.4.0
\--- debugCompileClasspath
```

The same focused dependency insight passed with `--refresh-dependencies --no-daemon`.

The full Android validation matrix passed:

```text
./gradlew.bat :app:testDebugUnitTest :app:assembleDebug --no-daemon
BUILD SUCCESSFUL in 3m 40s
44 actionable tasks: 44 executed
```

The repeat run reused the configuration cache:

```text
./gradlew.bat :app:testDebugUnitTest :app:assembleDebug --no-daemon
Reusing configuration cache.
BUILD SUCCESSFUL in 35s
44 actionable tasks: 44 up-to-date
Configuration cache entry reused.
```

KSP tasks remained successful:

- `:app:kspDebugKotlin`
- `:app:kspDebugUnitTestKotlin`

No KSP/AWT diagnostic appeared in the validation output. Android packaging retained the existing non-fatal native-library strip warning for several dependency libraries.

After rebasing onto the original compatibility baseline `ecb8c43ebd8860e47840ac259e682431f00ece6d`, the full validation matrix passed again:

```text
./gradlew.bat :app:testDebugUnitTest :app:assembleDebug --no-daemon
BUILD SUCCESSFUL in 57s
45 actionable tasks: 45 executed
```

Release assembly also passed under the merged quality-gate expectation for dependency-resolution changes:

```text
./gradlew.bat :app:assembleRelease --no-daemon
BUILD SUCCESSFUL in 2m 1s
49 actionable tasks: 49 executed
```

Focused dependency insight after the rebase still resolved the pinned Material artifact:

```text
./gradlew.bat :app:dependencyInsight --configuration debugCompileClasspath --dependency com.google.android.material:material
com.google.android.material:material:1.4.0
\--- debugCompileClasspath
```

The dynamic-version scan returned no matches:

```text
rg '= "[^"]*\+"' gradle/libs.versions.toml
```

## Current-Base Refresh

The focused change was finally rebased onto `main` at `e2bfecb031e5bb5eb185d354db96a2b7d5c8f319`. The compare remains one focused commit and exactly two files:

```text
gradle/libs.versions.toml
unity/Docs/Android_Dependency_Reproducibility_Record.md
```

No executable Android, Unity, source, asset, or save file changed during the final documentation-only base refresh. The previously recorded Android unit, debug assembly, release assembly, dependency insight, KSP, and dynamic-version evidence therefore remains applicable. Final merge review must still confirm the current head, changed-file set, and repository diff status.

## Locking And Verification

Dependency locking and verification metadata are deferred to #155 so CI can introduce and maintain those generated files with a single repository quality gate. This PR removes current dynamic Android catalog resolution without adding broad generated metadata.
