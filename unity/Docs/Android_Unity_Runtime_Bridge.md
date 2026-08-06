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

The current Unity foundation:

- defines the matching contract-v2 request and outcome DTOs, diagnostics, and
  bounded validators;
- parses the five-field route request without `JsonUtility` or an added JSON
  dependency;
- exposes an isolated `AndroidBridge.SetRouteContext(string)` component
  boundary backed by a pure receiver and an injected outcome sink;
- canonically encodes validated contract-v2 outcomes and exposes an isolated
  Unity-to-JVM sender with a guarded, injected Android platform adapter;
- keeps the component unregistered and every route unavailable.

This is not a completed embedded runtime. The Unity receiver has no production
scene registration, its default sink still discards reports, and the sender is
not registered. No Unity Android export, production route, or device round trip
is present.

## Packaging Model

The Android project remains buildable without a checked-in Unity export. A production build that embeds Unity must add the exported Unity Android library to the Gradle project so `com.unity3d.player.UnityPlayer` is available at runtime.

Supported packaging options:

- Add a Unity-exported `unityLibrary` Gradle module and include it from `settings.gradle.kts`.
- Add a Unity-generated Android archive and native libraries through the app module.

The bridge intentionally uses reflection so regular Android CI can keep compiling before the Unity export is committed. Runtime availability is determined by loading `com.unity3d.player.UnityPlayer`.

## Lifecycle Ownership

`UnityView` owns the attached Unity player instance for the lifetime of the composable host.

- A process-wide, reference-identity lease allows only one Android `UnityView` host to own a
  player and callback registration at a time. A second overlapping host stays visibly
  unavailable, and a stale lease cannot release a replacement owner.
- A newly mounted host synchronizes immediately to the current Android lifecycle state; it does
  not wait for a future lifecycle transition before resuming an already-resumed Activity.
- `ON_RESUME` forwards to `UnityPlayer.resume()`.
- `ON_PAUSE` forwards to `UnityPlayer.pause()`.
- `ON_STOP` also enforces the paused state without issuing a duplicate pause.
- `ON_DESTROY` and composable disposal close the route session, clear that host's callback token, forward to `UnityPlayer.destroy()`, and detach child views.
- Actual attach, detach, and window-focus changes are forwarded. Focus gained before resume is
  retained and forwarded only after resume; pause and destroy clear forwarded focus first.
- Application configuration callbacks forward the exact `Configuration` instance. Explicit low
  memory and trim levels at or above Android's running-low boundary forward to
  `UnityPlayer.lowMemory()`.
- Teardown is ordered `focus false -> pause -> destroy`, idempotent, and rejects later lifecycle,
  configuration, and memory signals. Reflection invocation failures are contained at the Android
  host boundary. A failed `destroy()` invocation or uncertain application-callback unregistration
  deliberately retains the process-wide lease so a second native player cannot start over an
  incomplete first teardown; process restart is the recovery boundary for that failure.
- A replacement host gets a new callback token; disposal of the prior host cannot clear the replacement.
- AndroidX `@Keep` preserves the exact `UnityBridgeCallbacks.reportOutcome(String)` JVM entry point in minified release builds while unused host and contract code can still be removed. The Java reference parameter is nullable at this external boundary; a null JNI/Unity argument is delivered as typed `bridge.null_message` failure instead of throwing across JNI.

The reflection host now accepts a Unity player only when it can prove a compatible one-argument
constructor plus the exact `resume`, `pause`, `destroy`, `windowFocusChanged(boolean)`,
`lowMemory`, `configurationChanged(Configuration)`, and static
`UnitySendMessage(String,String,String)` methods. A partial or incompatible export remains visibly
unavailable instead of silently dropping lifecycle calls.

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
- Duplicate root object members are rejected by a streaming parser guard before the request is materialized as a JSON tree.
- Realm, profile, encounter, content, catalog, and receipt fields remain absent until an approved route-specific contract defines their authority and validation.

`UnityView.routeLaunchSequence` is the Android launch identity. Changing it creates a new request ID even when `routeId` is unchanged, so a retry cannot inherit the prior launch's duplicate guard. Callers must supply a route ID explicitly; there is no implicit gameplay route.

An invalid Android request is shown as a bridge protocol error and is not sent.
The current isolated Unity receiver returns a correlated `unavailable` outcome
for every unknown but syntactically valid route. JVM delivery and end-to-end
visibility remain future slices. No route is enabled by this contract alone.

## Unity Receiver Boundary

The unregistered Unity boundary lives under
`Assets/AL/Scripts/Platform/Android/`. It is part of `AL.Runtime`; it adds no
assembly definition, package, scene object, prefab, service registration, or
protected shared-file edit.

`UnityBridgeContract.ParseRequest` performs:

1. a strict UTF-8 preflight with the inclusive 32 KiB limit;
2. exact JSON object parsing with decoded member-name comparison;
3. duplicate rejection for every allowed root member, including escaped-key
   equivalents such as `request\u0049d`;
4. exact required/allowed field, version, intent, stable-ID, capability-count,
   capability-shape, and ordinal-uniqueness validation.

The specialized parser is bounded to the bridge schema. It retains at most 16
capability values, limits skipped JSON nesting to 32 levels, and does not
materialize a generic JSON tree. Malformed UTF-16, comments, trailing content,
non-object roots, and unsupported JSON shapes fail closed.

`AndroidBridge.SetRouteContext(string)` delegates to a pure
`UnityBridgeRouteReceiver`. Every receive accepted before a close, terminal
capacity failure, or disposal request attempts exactly one immutable sink
report. Reports are queued for serialized delivery outside the state lock, so a
finite sink re-entry or a concurrent receive cannot deadlock or recurse through
the sink:

- a fully valid, previously unseen request produces one sendable
  `UnityRouteOutcome` with the exact request/route correlation, status
  `unavailable`, diagnostic `route.not_available`, and no result or payload;
- malformed or invalid input produces one non-sendable local
  `ProtocolFailure` carrying the exact `bridge.*` diagnostic and no fabricated
  fallback IDs;
- an exact latest replay produces local `bridge.duplicate_outcome`;
- reuse of an older request ID, or a current ID with changed intent or ordered
  capabilities, produces local `bridge.request_mismatch`;
- reuse of the current request ID with a changed route produces local
  `bridge.route_mismatch`;
- a new request ID for the same route is a new launch and receives its own
  correlated unavailable outcome.

Only correlated reports are eligible for the Unity-to-JVM sender. Protocol
failures are deliberately not serialized into invalid wire outcomes.
The receiver retains at most 256 exact request IDs without eviction. Reaching
that bound permanently closes the receiver with `bridge.session_closed` rather
than forgetting a stale identity. Serialized dispatch retains at most 64
regular pending reports plus one reserved terminal report. Queue-capacity or
per-drain dispatch-budget exhaustion emits exactly one terminal
`bridge.session_closed`, drains already accepted reports in FIFO order, and
tears down rather than permitting an unbounded recursive queue. `Close` is
idempotent and reports `bridge.session_closed` on later receives. `Dispose`,
component disable, and component destruction are nonblocking graceful-close
requests: they reject new receives, allow reports already accepted by the
receiver to drain in FIFO order, then clear the sink and ledger. This avoids
same-thread and coordinated cross-thread disposal deadlocks; after the bounded
drain completes, no later sink invocation can start. Sink exceptions and direct
re-entry are contained.

The component currently defaults to a discarding sink and is absent from every
scene and prefab. The sender is also absent from every scene, prefab, service,
and component configuration. This keeps both boundaries testable without
implying production registration or a production route.

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

### Unity sender boundary

`UnityBridgeContract.EncodeOutcome` first reruns outcome validation, then emits
the exact contract-v2 fields in canonical order. Null optional fields are
omitted rather than serialized as JSON `null`. Quotes, reverse slashes, and
control characters are escaped explicitly; strict UTF-8 is checked again after
encoding, and the complete output remains bounded to 32 KiB.

`UnityBridgeOutcomeSender` implements `IUnityBridgeOutcomeSink` but remains
unregistered. Its constructor captures the current thread as the declared Unity
main thread and takes exclusive ownership of one
`IUnityBridgeOutcomePlatformAdapter`. It also requires an
`IUnityBridgeOutcomeDispatchResultSink`; there is no constructor that silently
discards dispatch results. A future owner must construct the adapter, result
sink, and sender on the Unity main thread, keep the sender alive until the
receiver's bounded graceful drain completes, dispose the receiver first, and
dispose the sender afterward.

Before any platform call, the sender:

1. accepts only `UnityBridgeReceiverReport.IsSendable` reports;
2. reruns exact request/outcome validation and correlation;
3. canonically encodes the validated outcome;
4. rejects an off-owner-thread or re-entrant call before it can take request
   ownership or touch Unity/JNI state;
5. reserves the request identity before entering the adapter.

Dispatch depth is one; there is no recursive queue. A busy or wrong-thread call
is typed and retryable because no platform invocation began. A platform
`Unavailable` result is contractually restricted to cases where no external
callback invocation was attempted. It retains a SHA-256 fingerprint of the
full validated request envelope, including intent and ordered capabilities,
plus the exact canonical outcome. Only that same correlated report can retry
the request ID; an altered envelope is rejected as
`bridge.request_mismatch`.

A completed callback invocation and an adapter exception or invalid adapter
result are terminal. Exceptions are delivery-uncertain, so automatic retry is
prohibited to avoid a second JVM call after a possibly completed first call.
Terminal and retryable identities share one ordinal, no-eviction limit of 256;
capacity fails closed as `bridge.session_closed`. Disposal is idempotent,
nonblocking during an in-flight invocation, disposes the exclusively owned
adapter once after that invocation returns, and prevents later dispatch.

Direct `TryDispatch` callers receive the typed dispatch result. When the sender
is consumed through the receiver-facing, void `IUnityBridgeOutcomeSink.Publish`
boundary, every admitted, non-re-entrant, pre-disposal call forwards the
original report and exactly one typed result to the required result sink after
dispatch. That owner can retain a retryable report and schedule a later exact
retry after `Publish` returns; the sender does not schedule retries itself.
Result-sink exceptions are contained, and an immediate result-sink
`TryDispatch` re-entry receives typed `Busy` without another platform call.
Nested/concurrent `Publish` is forbidden by the serialized main-thread
ownership contract and is suppressed to prevent recursive result
notifications. A result-sink-triggered disposal is deferred until result
publication returns so the adapter and observer remain valid for the complete
bounded call.

`AndroidUnityBridgeOutcomePlatformAdapter` preserves these exact constants:

```text
class:      com.example.anotherlife.ui.unity.UnityBridgeCallbacks
method:     reportOutcome
descriptor: (Ljava/lang/String;)V
```

It captures the same main-thread assumption, compiles the JNI path only for
`UNITY_ANDROID && !UNITY_EDITOR`, checks `RuntimePlatform.Android`, and uses a
short-lived `AndroidJavaClass` wrapper for each invocation. Other builds return
typed `Unavailable` without touching JNI. Missing classes/methods and JNI
exceptions become typed `InvocationFailed`; none unwind through the receiver.

The JVM method returns `void` and Android deliberately no-ops when no host
callback is registered. Sender status `CallbackInvoked` therefore proves only
that the JVM call returned. It does not prove host receipt, Android session
consumption, route completion, gameplay consequence, persistence, or durable
exactly-once authority.

Android rejects:

- null, malformed, blank, oversized, or structurally unexpected JSON;
- duplicate root object members before last-value-wins tree materialization;
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

Unity sender rejection, unavailability, busy/thread failure, retention
exhaustion, and invocation failure remain local typed dispatch results. Direct
callers receive them synchronously; each admitted receiver-facing `Publish`
forwards the original report plus exactly one result through the required
result sink. They are not sent through the same unavailable callback, do not
fabricate another route outcome, and do not activate fallback gameplay.

## Validation

Required checks for bridge changes:

- Android unit tests: `:app:testDebugUnitTest`
- Android instrumentation: off-main callback delivery and disposed-host rejection
- Android debug/release assembly and lint
- Device smoke test with a Unity export packaged into the app
- Route startup test from Android to Unity
- Outcome callback test from Unity to Android
- Back, home, pause, resume, and destroy lifecycle test

Current Android contract coverage includes valid/invalid request and outcome JSON, unsupported versions and exact enum values, malformed and oversized input, duplicate request/outcome members, nullable Java/JNI boundary rejection, bounded payloads, same-route retries, stale and duplicate outcomes, malformed-then-valid recovery, callback replacement, off-main UI dispatch, and host disposal.

Current Android host-lifecycle coverage additionally includes deferred focus until resume,
duplicate resume/pause/stop suppression, focus restoration after a real resume, ordered and
idempotent focused teardown, post-destroy signal rejection, exact configuration/trim callback
ordering, callback-exception containment, single-owner lease denial, stale-release protection, and
replacement-host callback delivery. These are controller/JVM and Android host tests; they do not
substitute for a packaged Unity runtime or physical-device lifecycle proof.

Current Unity contract coverage includes exact constants and wire values,
valid/invalid route requests, decoded escaped-key duplicates, strict
allowed/required fields, malformed UTF-16/JSON, exact UTF-8 size boundaries,
stable-ID limits, exact intent values, capability count/type/uniqueness,
outcome diagnostics and exact payload/message bounds, request/outcome correlation,
unknown routes, malformed-then-valid recovery, same-route relaunch, exact
replay, stale and altered envelopes, bounded identity exhaustion, throwing and
re-entrant sinks, serialized queue/dispatch-budget exhaustion, accepted-report
drain, and nonblocking same-thread/cross-thread disposal. Focused sender
coverage additionally exercises canonical encoding/escaping, the exact JVM
class/method/descriptor constants, Editor platform unavailability,
non-sendable and forged-correlation rejection, adapter-reported callback
invocation and duplicate suppression, exact unavailable retry binding,
ambiguous exception containment, invalid adapter results, wrong-thread retry,
depth-one re-entry, terminal/retryable/shared 256-ID limits, sink exception
containment, typed receiver-facing result publication, retained exact retry,
result-sink re-entry rejection, and re-entrant/cross-thread disposal. The guarded
`AndroidJavaClass.CallStatic` path and actual JVM callback remain part of the
unperformed packaged/device round trip.

## Unity Bridge Optimization Impact

- Runtime work is one-shot per `SetRouteContext` call and O(n) for a message
  bounded to 32 KiB. There is no `Update`, polling, per-frame allocation, route
  loading, or gameplay mutation.
- Receiver-call managed allocations include bounded decoded strings/parser
  state, at most 16 capability values, validation uniqueness storage, DTO
  copy/read-only wrappers, one result/report, and a `StringBuilder` only when
  decoded JSON string content requires escape processing.
- Session retention is bounded to 256 request IDs of at most 128 ASCII
  characters each; capacity closes fail-closed rather than evicting history.
- Re-entrant/concurrent sink delivery is serialized through at most 64 regular
  pending reports plus one reserved terminal report, with fail-closed teardown
  on queue or per-drain budget exhaustion.
- Sender work is one-shot and depth-one. Every accepted dispatch allocates the
  outcome encoder's `StringBuilder` and canonical string of at most 32 KiB, an
  additional bounded length-framed request-plus-outcome `StringBuilder` and
  string (about 34 Ki UTF-16 code units at declared maxima), strict-UTF-8 hash
  input bytes, one SHA-256 instance, and a 32-byte fingerprint. At most 256
  request identities are retained across terminal IDs and retryable
  fingerprints; there is no eviction, unbounded retry queue, or per-frame work.
- The Android adapter creates and disposes one short-lived JNI class wrapper per
  attempted callback. It retains no Java object or Android host registration.
- The three runtime C# files add expected nonzero, linker/stripping-dependent
  managed Player assembly, build, and installed-size growth.
- The Android lifecycle slice adds one host controller, one active lease object, one application
  component-callback registration, and bounded callback-only state. It adds no timer, queue,
  polling loop, per-frame work, asset, native library, or dependency. Exact optimized APK and
  installed-size deltas remain build-dependent measurements.
- No external binary, asset, AAR, package, assembly definition, or dependency
  is added.
- Player build size, installed size, runtime-memory/startup allocation,
  IL2CPP/linker behavior, Android package export, profiler measurements,
  physical-device startup/frame pacing/thermal behavior, and low-end device
  compatibility remain unperformed.

Still blocked for #135 completion:

- production registration/wiring of the receiver and sender;
- reproducible `unityLibrary`/AAR packaging;
- unknown-route end-to-end unavailable behavior;
- packaged back/home/app-switch, rotation/recreation, multi-window, process-death/runtime-loss,
  audio/input/keyboard/controller, focus, low-memory, and repeated-launch lifecycle proof;
- packaged representative-device performance, memory, thermal, and size evidence;
- any approved production gameplay route and its durable consequence authority.
