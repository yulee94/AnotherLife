# Android Unity Runtime Bridge

## Scope

Issue #135 owns the embedded Android Unity bridge. The Android app must not claim that a text-only placeholder or a reflection host without a packaged export is an active Unity runtime.

The current Android foundation:

- reflection-hosts `com.unity3d.player.UnityPlayer` when that class is packaged;
- shows a visible unavailable state when the runtime is absent;
- sends a bounded, versioned route request;
- validates and correlates one terminal outcome to one active request;
- posts UI-facing callbacks through the Android view's main-thread queue;
- owns callback registration and disposal by host token.

This is not a completed embedded runtime. No Unity receiver/sender, Unity Android export, production route, or device round trip is present.

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
- `ON_DESTROY` and composable disposal close the route session, clear that host's callback token, forward to `UnityPlayer.destroy()`, and detach child views.
- Focus is forwarded with `windowFocusChanged(true)` after attach.
- A replacement host gets a new callback token; disposal of the prior host cannot clear the replacement.
- AndroidX `@Keep` preserves the exact `UnityBridgeCallbacks.reportOutcome(String)` JVM entry point in minified release builds while unused host and contract code can still be removed.

The bridge must stay behind one Android view container. Do not let independent screens create competing Unity player instances.

## Route Contract

Android sends route context to Unity with:

- GameObject: `AndroidBridge`
- Method: `SetRouteContext`
- Payload: JSON

Payload version 2:

```json
{
  "contractVersion": 2,
  "requestId": "76f35664-447f-49e1-9f05-e2fa6af47aac",
  "routeId": "bridge.smoke",
  "intent": "preview",
  "requestedCapabilities": [
    "route.acknowledge",
    "route.cancel"
  ]
}
```

Contract rules:

- `contractVersion` must equal `2`; version 1 has no request correlation and is unsupported.
- `requestId`, `routeId`, and capability IDs are bounded ASCII stable IDs and are compared ordinally without trimming, case folding, aliasing, or normalization.
- `intent` is exactly `preview` or `authoritative`. It describes transport intent; it does not grant save, reward, progression, or gameplay authority.
- Capability IDs are explicit, unique, and bounded to 16 entries.
- The complete UTF-8 message is bounded to 32 KiB.
- Realm, profile, encounter, content, catalog, and receipt fields remain absent until an approved route-specific contract defines their authority and validation.

`UnityView.routeLaunchSequence` is the Android launch identity. Changing it creates a new request ID even when `routeId` is unchanged, so a retry cannot inherit the prior launch's duplicate guard. Callers must supply a route ID explicitly; there is no implicit gameplay route.

An invalid Android request is shown as a bridge protocol error and is not sent. An unknown but syntactically valid route must fail visibly in the future Unity receiver and return a correlated `unavailable` or `failure` outcome. No route is enabled by this contract alone.

## Outcome Contract

Unity reports a route outcome by calling the JVM static method:

```text
com.example.anotherlife.ui.unity.UnityBridgeCallbacks.reportOutcome(String rawJson)
```

Payload version 2:

```json
{
  "contractVersion": 2,
  "requestId": "76f35664-447f-49e1-9f05-e2fa6af47aac",
  "routeId": "bridge.smoke",
  "status": "unavailable",
  "diagnosticCode": "route.not_available",
  "resultId": "result-0001",
  "payload": "{}"
}
```

Supported `status` values are exactly `success`, `failure`, `cancelled`, and `unavailable`. `failure` and `unavailable` require a bounded lowercase diagnostic code. A successful outcome for an `authoritative` request requires a stable `resultId`; other outcomes may omit it. `payload` is optional, opaque, bounded to 16 KiB, and is not gameplay authority.

Android rejects:

- malformed, blank, oversized, or structurally unexpected JSON;
- unsupported versions or statuses;
- missing or invalid stable IDs;
- request or route mismatches;
- late results from a prior same-route launch;
- duplicate terminal outcomes for the active request;
- callbacks after session disposal.

A rejected malformed or mismatched outcome does not complete the active request, so a later valid correlated result can still be delivered. A delivered callback is only view-lifetime deduplication; durable reward, progression, receipt, and save idempotency remain with their owning services.

## Failure Behavior

If the Unity runtime class is missing, `UnityView` shows `Unity runtime unavailable` with the requested route. This is a visible integration failure, not a gameplay substitute.

`onRouteDispatched` is invoked only after a validated request is successfully sent through the reflected Unity message method. It is not a Unity-loaded or route-ready acknowledgement, and it is not invoked for a missing runtime, invalid route request, or unavailable send method.

Malformed, stale, mismatched, unsupported, and duplicate outcomes surface a stable `bridge.*` protocol diagnostic through `onProtocolError`. They do not fabricate a route result or consume the active request. Callback delivery is posted to the host view before invoking UI/navigation callbacks, and a disposed host's registration token cannot clear a newer host.

## Validation

Required checks for bridge changes:

- Android unit tests: `:app:testDebugUnitTest`
- Android instrumentation: off-main callback delivery and disposed-host rejection
- Android debug/release assembly and lint
- Device smoke test with a Unity export packaged into the app
- Route startup test from Android to Unity
- Outcome callback test from Unity to Android
- Back, home, pause, resume, and destroy lifecycle test

Current Android contract coverage includes valid/invalid request and outcome JSON, unsupported versions and exact enum values, malformed and oversized input, bounded payloads, same-route retries, stale and duplicate outcomes, malformed-then-valid recovery, callback replacement, off-main UI dispatch, and host disposal.

Still blocked for #135 completion:

- matching Unity DTO/validator and `AndroidBridge.SetRouteContext` receiver;
- Unity-to-JVM outcome sender;
- reproducible `unityLibrary`/AAR packaging;
- unknown-route end-to-end unavailable behavior;
- back/home/configuration/process/audio/input/focus/low-memory lifecycle proof;
- packaged representative-device performance, memory, thermal, and size evidence;
- any approved production gameplay route and its durable consequence authority.
