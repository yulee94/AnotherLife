# Android Unity Runtime Bridge

## Scope

Issue #135 brings the embedded Android Unity bridge into scope. The Android app must not claim that a text-only placeholder is an active Unity runtime. The bridge in `UnityView.kt` now hosts `com.unity3d.player.UnityPlayer` when the Unity Android export is packaged with the app, and shows a visible unavailable state when that runtime class is absent.

## Packaging Model

The Android project remains buildable without a checked-in Unity export. A production build that embeds Unity must add the exported Unity Android library to the Gradle project so `com.unity3d.player.UnityPlayer` is available at runtime.

Supported packaging options:

- Add a Unity-exported `unityLibrary` Gradle module and include it from `settings.gradle.kts`.
- Add a Unity-generated Android archive and native libraries through the app module.

The bridge intentionally uses reflection so regular Android CI can keep compiling before the Unity export is committed. Runtime availability is determined by loading `com.unity3d.player.UnityPlayer`.

## Lifecycle Ownership

`UnityView` owns the attached Unity player instance for the lifetime of the composable host.

- `ON_RESUME` forwards to `UnityPlayer.resume()`.
- `ON_PAUSE` forwards to `UnityPlayer.pause()`.
- `ON_DESTROY` and composable disposal forward to `UnityPlayer.destroy()` and detach child views.
- Focus is forwarded with `windowFocusChanged(true)` after attach.

The bridge should stay behind one Android view container. Do not let independent screens create competing Unity player instances.

## Route Contract

Android sends route context to Unity with:

- GameObject: `AndroidBridge`
- Method: `SetRouteContext`
- Payload: JSON

Payload version 1:

```json
{
  "contractVersion": 1,
  "routeTag": "Main"
}
```

Unknown routes must fail visibly in Unity and return a failure outcome.

## Outcome Contract

Unity reports a route outcome by calling the JVM static method:

```text
com.example.anotherlife.ui.unity.UnityBridgeCallbacks.reportOutcome(String rawJson)
```

Payload version 1:

```json
{
  "routeTag": "Main",
  "status": "success",
  "payload": "{}"
}
```

Supported `status` values are `success`, `failure`, and `cancelled`. Android ignores outcomes for inactive routes and ignores duplicate outcomes for the active route, preventing duplicate progression or reward application.

## Failure Behavior

If the Unity runtime class is missing, `UnityView` shows `Unity runtime unavailable` with the requested route. This is a visible integration failure, not a gameplay substitute.

If Unity reports malformed JSON, the Android callback path treats it as a failure route outcome once the payload parser can identify the route. Unity should prefer valid JSON and include a route tag in every outcome.

## Validation

Required checks for bridge changes:

- Android unit tests: `:app:testDebugUnitTest`
- Android debug build: `:app:assembleDebug`
- Device smoke test with a Unity export packaged into the app
- Route startup test from Android to Unity
- Outcome callback test from Unity to Android
- Back, home, pause, resume, and destroy lifecycle test

