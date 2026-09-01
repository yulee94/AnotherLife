# Android Unity Runtime Bridge

## Scope

Issue #135 owns the embedded Android Unity bridge. The Android app must not claim that a text-only placeholder or a reflection host without a packaged export is an active Unity runtime.

The current Android foundation:

- reflection-hosts `com.unity3d.player.UnityPlayer` when that class is packaged;
- shows a visible unavailable state when the runtime is absent;
- sends a bounded, versioned route request;
- validates and correlates one terminal outcome to one active request;
- posts UI-facing callbacks through the Android view's main-thread queue;
- owns callback registration and disposal by host token;
- exposes a debug-only `bridge.smoke.unavailable` route from the developer tools,
  consumes terminal outcomes without granting gameplay authority, returns to the
  safe Android shell on unavailable or cancelled, and keeps failed or unexpected
  success outcomes visibly contained for investigation.

The current Unity foundation:

- defines the matching contract-v2 request and outcome DTOs, diagnostics, and
  bounded validators;
- parses the five-field route request without `JsonUtility` or an added JSON
  dependency;
- exposes an isolated `AndroidBridge.SetRouteContext(string)` component
  boundary backed by a pure receiver and an injected outcome sink;
- canonically encodes validated contract-v2 outcomes and exposes an isolated
  Unity-to-JVM sender with a guarded, injected Android platform adapter;
- keeps every route unavailable (the runtime host wires the boundaries, but no
  gameplay route is enabled).

The receiver and sender are now production-wired through `AndroidBridgeRuntimeHost`
(see "Runtime host" under "Unity Receiver Boundary"). What remains future work is a
Unity Android export packaged into the app, a production route that returns a
non-`unavailable` outcome, and a physical-device round trip.

## Packaging Model

The selected intermediate packaging shape is Unity's generated `unityLibrary`
Gradle module. An AAR is not the current source of truth. The export is a
disposable build product and is never committed. The Android project remains
buildable without it, and its existing `settings.gradle.kts`, app module, and
dependency graph are not changed by the exporter.

`AL.EditorTools.AndroidUnityLibraryExporter.ExportDevelopmentArm64Il2Cpp`
defines the reproducible export boundary. Run it only from exact Unity
`2022.3.62f3` with Android Build Support and the Android target already active:

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" `
  -batchmode -nographics -quit `
  -projectPath "<AnotherLife-worktree>\unity" `
  -buildTarget Android `
  -executeMethod AL.EditorTools.AndroidUnityLibraryExporter.ExportDevelopmentArm64Il2Cpp `
  -logFile "<AnotherLife-worktree>\unity\Logs\AndroidUnityLibraryExport.log"
```

The command is fail-closed before mutation unless the production shell scene
validator passes and both generated destinations are the exact ignored,
untracked, non-reparse paths below:

- export: `unity/Builds/AndroidExport`;
- deterministic result summary:
  `unity/Logs/AndroidUnityLibraryExportSummary.json`.

The tool snapshots Android Player/build settings, temporarily selects IL2CPP,
ARM64 only, minimum API 24, Gradle-project export, development build, and the
three ordered ShellFoundation scenes, then restores every captured setting in
a `finally` path. Restoration is not accepted until all five settings are
recaptured and match the original snapshot exactly. Cleanup is restricted to
the exact export directory and currently requires Windows no-follow handle
attestation. It boundedly opens and retains at most 8,192 regular-file and 8,192
regular-directory DELETE-capable handles with share-delete denied, rejects
reparse, duplicate case-normalized path, and duplicate filesystem identities,
then marks files through those exact handles and marks directories bottom-up
with the root last. Handles are closed only after disposition; a new descendant
makes its parent nonempty and fails closed. There is no dispose-then-path-delete
window, recursive traversal primitive, or path-based deletion fallback. After
creation, the output directory is re-attested by no-follow identity; that exact
identity is checked again and held under a mutation lease across build and
inspection. A successful result additionally
requires a successful exact-target `BuildReport`, no build errors, no ABI other
than `arm64-v8a`, and the expected Gradle root, `unityLibrary` manifest,
`unity-classes.jar`, ProGuard rules, player data, Unity native libraries, and
the staged IL2CPP source/toolchain under `Il2CppOutputProject`. Required
artifacts must be nonempty; the JAR, exported ELF libraries, manifest, module
inclusion, library plugin, generated minimum API 24 shape, and Gradle's explicit
deferred `libil2cpp.so` generation receive bounded structural checks. The
current-host IL2CPP tool is exact: Windows requires a credible PE `il2cpp.exe`;
Linux requires executable extensionless ELF; macOS requires executable
extensionless Mach-O/fat format. A wrong-host alternative or corrupt header
fails closed. The Linux/macOS format rules are retained as pure future-host
policy, but export and artifact inspection currently fail before any mutation
or summary write on those hosts. Unity's pathname-only `BuildPipeline` cannot
be bound to a Unix directory descriptor, and an open descriptor cannot prevent
a swap-write-restore rename race. Windows Editor is therefore the only current
execution host, using no-follow handles and share-deny-delete directory leases.
Inventory traversal rejects every reparse entry before descent
and is bounded to 8,192 files, 8,192 directories, and 2 GiB. Each file is opened
once with the strongest available no-follow/share guard; its length, cumulative
byte accounting, SHA-256, and structural prefix all come from that same stable
handle/stream, with an extra-byte EOF probe and identity/length drift rejection.

Summary writing repeats the exact ignored/untracked/non-reparse guard after
creating its parent and immediately before temporary-file creation and atomic
commit. The parent no-follow identity is retained across the write; both the
temporary entry and any existing destination must be regular entries in that
same directory.

Unity's Gradle-project export does not itself place `libil2cpp.so` under
`jniLibs`. It stages generated C++ and the IL2CPP toolchain, while the exported
`unityLibrary/build.gradle` compiles that native library during a later Gradle
assemble/package task. The exporter therefore proves the staged IL2CPP inputs
and generation declaration, not a packaged native library or AAR. Native Gradle
assembly must separately prove the resulting `libil2cpp.so` before integration.

The generated launcher is not an application authority and must not be copied
into source. The bridge continues to use reflection so ordinary Android CI
compiles without generated artifacts; runtime availability remains determined
by loading `com.unity3d.player.UnityPlayer`.

### Production AAR assembly and host integration

The production boundary is two isolated Gradle builds. Unity's generated Gradle
7.5.1/AGP 7.4.2/JDK 11 project performs the final IL2CPP link and emits an AAR;
the tracked Android Gradle 9.4.1/AGP 9.2.1/JDK 21 project consumes only the
verified AAR. Never include the generated `unityLibrary` project in
`settings.gradle.kts`, copy the generated launcher, commit generated output, or
use `pickFirst` to hide duplicate native libraries.

On the authorized Windows runner, generate and package each distinct profile:

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"

# Debug: Development + IL2CPP Debug + Minimal managed stripping, ARM64/API 24.
& $Unity -batchmode -nographics -quit -projectPath "<repo>\unity" `
  -buildTarget Android `
  -executeMethod AL.EditorTools.AndroidUnityLibraryExporter.ExportDevelopmentArm64Il2Cpp `
  -logFile "<repo>\unity\Logs\AndroidUnityLibraryExport-debug.log"
py -3 tools\android_unity_package.py --variant debug --repo-root .

# Release: non-Development + IL2CPP Release + Medium managed stripping,
# ARM64/API 24. This is a separate export; never relabel the debug AAR.
& $Unity -batchmode -nographics -quit -projectPath "<repo>\unity" `
  -buildTarget Android `
  -executeMethod AL.EditorTools.AndroidUnityLibraryExporter.ExportReleaseArm64Il2Cpp `
  -logFile "<repo>\unity\Logs\AndroidUnityLibraryExport-release.log"
py -3 tools\android_unity_package.py --variant release --repo-root .
```

`android_unity_package.py` runs the generated wrapper's
`:unityLibrary:clean :unityLibrary:assemble<Variant>`, requires exactly the
ARM64 native family (`libmain.so`, `libunity.so`, and the finally linked
`libil2cpp.so`), verifies AArch64 ELF headers, Unity player data,
`UnityPlayer.class`, manifest, and ProGuard rules, then atomically stages:

- `unity/Builds/AndroidArtifacts/debug/unityLibrary-debug.aar`;
- `unity/Builds/AndroidArtifacts/release/unityLibrary-release.aar`;
- one deterministic `inventory.json` beside each AAR.

The inventory binds the AAR and required entries by size/SHA-256 to the exact
repository commit, Unity version, ABI, API, scripting backend, and variant
optimization profile. Host verification rejects missing, stale, wrong-profile,
wrong-ABI, duplicate-entry, malformed, or modified artifacts.

Build the opted-in host from the repository root with JDK 21 and the Android
SDK used by the host:

```powershell
.\gradlew.bat clean :app:testDebugUnitTest :app:verifyUnityDebugApk `
  :app:assembleDebugAndroidTest :app:lintDebug -PwithUnity=true --rerun-tasks
.\gradlew.bat clean :app:testDebugUnitTest :app:verifyUnityReleaseApk `
  -PwithUnity=true --rerun-tasks
```

`-PwithUnity=true` is mandatory for a Unity-enabled package. It selects the
matching AAR per Android variant, filters the final package to `arm64-v8a`, and
makes the corresponding pre-build task verify its inventory. Without the flag,
the ordinary visible-unavailable Android shell remains intentionally buildable;
it must not be reported as a packaged Unity result.

The variant-specific verification tasks build the matching host APK and then
run `android_unity_package.py --verify-apk`. Verification revalidates the staged
AAR against the exact repository commit and Unity version, rejects any APK ABI
other than ARM64, validates the packaged `libmain.so`, `libunity.so`, and
`libil2cpp.so` ELF targets, binds those native libraries and
`globalgamemanagers` byte-for-byte back to the staged AAR inventory, and parses
every `classes*.dex` class-definition table to prove that
`com.unity3d.player.UnityPlayer` survived DEX conversion and release shrinking.
The verifier prints both the final APK and source AAR SHA-256 identities.

Before accepting either generated package, inspect it rather than trusting the
build result alone. The automated verifier covers package presence, identity,
ABI, ELF machine, player data, and the retained Unity player class; retain the
following manual/native inspection for dynamic-link and signing evidence:

```powershell
# AAR/APK contents: require Unity assets/classes and only arm64-v8a Unity ELFs.
tar -tf unity\Builds\AndroidArtifacts\debug\unityLibrary-debug.aar
tar -tf app\build\outputs\apk\debug\app-debug.apk

# Use Unity NDK 23.1.7779620's llvm-readelf on each extracted native library.
$ReadElf = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\PlaybackEngines\AndroidPlayer\NDK\toolchains\llvm\prebuilt\windows-x86_64\bin\llvm-readelf.exe"
& $ReadElf -h -d -Ws <extracted>\libmain.so
& $ReadElf -h -d -Ws <extracted>\libunity.so
& $ReadElf -h -d -Ws <extracted>\libil2cpp.so
```

Require `Machine: AArch64`, no text relocations, no unexpected unresolved
non-platform dependency, and a closed `DT_NEEDED` set against packaged Unity
libraries plus Android platform libraries. Verify the final APK/AAB contains
all three libraries and `assets/bin/Data/**`; verify the AAR's `classes.jar`
contains `com/unity3d/player/UnityPlayer.class`. Install only the signed debug
APK (or a properly signed release artifact) on an API 24+ ARM64 target. The
packaged round trip, lifecycle/recovery stress, and representative-device
performance evidence are separate dependent #135 gates.

## Lifecycle Ownership

`UnityView` owns the attached Unity player instance for the lifetime of the composable host.

- A process-wide, reference-identity lease allows only one Android `UnityView` host to own a
  player and callback registration at a time. Up to four overlapping incoming hosts wait in a
  bounded FIFO handoff queue. Release atomically transfers a new identity lease to the oldest
  live waiter, which then creates the runtime and dispatches its already-validated pending route;
  a cancelled or stale waiter cannot acquire or release a replacement owner. Capacity exhaustion
  is visible and does not create another player.
- A transferred lease is retained before any main-thread post. Disposal of an unattached waiter
  recovers and releases that exact lease, and its late deferred runnable cannot claim it. Claim to
  activation-permit publication is synchronized with destruction. Close marks destruction
  immediately, but it cannot take or release a lease while activation is in progress. The permit
  holder checks close before and after each opaque construction, registration, view, lifecycle,
  and route-dispatch step; when close wins, that holder performs the only partial cleanup and
  returns the exact lease only after cleanup is proven. Grant callbacks drain through a
  non-recursive bounded runner; enqueue during an opaque grant callback is rejected, so a
  self-requeue-and-throw callback cannot grow the stack or handoff indefinitely.
- The Compose host is keyed by the actual `LifecycleOwner`, and Unity destruction is bound to the
  corresponding Android view's `onRelease`. An owner swap therefore releases the old view and
  deterministically creates or waits for one replacement instead of destroying an unkeyed view
  that Compose may retain.
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
- Application component-callback registration is a required part of runtime activation. A thrown
  or rejected registration destroys that player, blocks route dispatch, displays a lifecycle
  registration failure, and releases the host lease only when teardown is proven complete.
- Lease grant, callback registration, player-view attachment, retained-state synchronization, and
  pending-route dispatch form one fail-closed activation transaction. A failure after registration
  clears the exact callback token, closes callback admission and the route session, attempts to
  unregister application callbacks, destroys and detaches the player, and releases the lease only
  after every cleanup step is proven. A replacement can then acquire without inheriting callbacks
  or state. The player's exact view is observed immediately after successful construction and
  before registrar work, so a clean registrar rejection can prove detachment and recover; a
  throwing view lookup remains uncertain and deliberately retains the lease. Destruction during
  construction, registration, pre-attachment, or post-attachment activation cannot let a
  replacement acquire until the permit holder has completed cleanup.
- Teardown is ordered `focus false -> pause -> destroy`, idempotent, and rejects later lifecycle,
  configuration, and memory signals. Reflection invocation failures are contained at the Android
  host boundary. A failed `destroy()` invocation or uncertain application-callback unregistration
  deliberately retains the process-wide lease so a second native player cannot start over an
  incomplete first teardown; process restart is the recovery boundary for that failure.
- A replacement host gets a new callback token; disposal of the prior host cannot clear the replacement.
- AndroidX `@Keep` preserves the exact `UnityBridgeCallbacks.reportReady(String)` and
  `UnityBridgeCallbacks.reportOutcome(String)` JVM entry points in minified release builds while
  unused host and contract code can still be removed. Their Java reference parameters are nullable
  at the external boundary; a null JNI/Unity argument is delivered as typed
  `bridge.null_message` failure instead of throwing across JNI.

The reflection host loads the named class without initialization and accepts it only when it can
prove a compatible public one-argument constructor on a `View`, exact instance `void` `resume`,
`pause`, `destroy`, `windowFocusChanged(boolean)`, `lowMemory`, and
`configurationChanged(Configuration)` methods, plus exact static `void`
`UnitySendMessage(String,String,String)`. Missing classes and incompatible signatures remain
visibly unavailable without construction. Class initialization or constructor failure propagates
as uncertain activation and deliberately retains the lease instead of treating a potentially
partial native runtime as safely absent.

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
The wired Unity receiver returns a correlated `unavailable` outcome for every
unknown but syntactically valid route, and the registered sender delivers that
outcome back to the JVM in an Android player build. The Android debug shell now
mounts that exact safe-unavailable smoke route and returns to Debug only after an
unavailable or cancelled outcome. Failure and unapproved success remain visible
and apply no result. Focused host tests prove off-main callback admission,
correlation, typed unavailable consumption, and duplicate rejection; shell tests
prove the route is inaccessible in release and cannot navigate into gameplay.
These deterministic tests do not replace an installed ARM64 package run.
End-to-end physical-device visibility and a route that returns an approved
non-`unavailable` outcome remain future slices. No gameplay route is enabled by
this contract alone.

## Unity Receiver Boundary

The Unity boundary lives under
`Assets/AL/Scripts/Platform/Android/`. It is part of `AL.Runtime`; it adds no
assembly definition, package, committed scene object, prefab, service
registration, or protected shared-file edit. The `AndroidBridge` GameObject is
instantiated at runtime by `AndroidBridgeRuntimeHost`, not serialized into a scene.

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

### Runtime host

`AndroidBridgeRuntimeHost` is the production owner that turns the dormant receiver and
sender into one live boundary. It is a `[DisallowMultipleComponent]` MonoBehaviour that
runs on a GameObject named `AndroidBridge` (the exact name the JVM host targets via
`UnitySendMessage`). On the Unity main thread it constructs the
`AndroidUnityBridgeOutcomePlatformAdapter`, a logging `IUnityBridgeOutcomeDispatchResultSink`,
and the `UnityBridgeOutcomeSender`, then registers the sender as the receiver's outcome sink
via `AndroidBridge.ConfigureOutcomeSink`. It logs dispatch results and
foreground/background transitions, and tears down receiver-first, sender-second.

A `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` hook creates the host (with a
`DontDestroyOnLoad` GameObject) before the first scene loads, so the bridge is present
regardless of which ShellFoundation scene the JVM host activates against, while a
`FindObjectOfType` guard prevents a duplicate when a scene also carries its own host.

On non-Android builds the adapter reports `Unavailable` without touching JNI, so the slice
stays live-but-unavailable in the Editor and only becomes live in an Android player build.
The discarding sink remains the receiver's fallback only for standalone use without the host.

The component still defaults to a discarding sink when used without the host, so the
receiver and sender remain testable without implying a production route.

## Ready Acknowledgement Contract

Sending a validated route request is not proof that Unity can present or own that route. The
Android host keeps its full-screen native starting surface in front of the attached player, owns
input and accessibility semantics, and waits for Unity to call:

```text
com.example.anotherlife.ui.unity.UnityBridgeCallbacks.reportReady(String rawJson)
```

Payload version 2 contains only the active request correlation:

```json
{
  "contractVersion": 2,
  "requestId": "76f35664-447f-49e1-9f05-e2fa6af47aac",
  "routeId": "bridge.smoke"
}
```

`requestId` is the attempt-generation fence: every route launch and recreated host creates a new
request identity. Android accepts exactly one ready acknowledgement whose version, request, and
route match the current incomplete session. Only that acknowledgement removes the native starting
surface and invokes `onReady`. A prior-generation request ID, duplicate, or post-outcome ready
callback is inert. A current request carrying the wrong route, or a malformed, oversized, or
structurally invalid ready message, remains a typed protocol failure and does not transfer
presentation ownership.

The JVM boundary and Android-side parser/session/host transition are implemented. Unity also owns a
canonical `UnityRouteReady` validator/encoder, an exact `reportReady(String)` Android adapter, and a
bounded, main-thread `UnityBridgeReadySender`. A route reports readiness explicitly through
`AndroidBridgeRuntimeHost.TryReportReady(validatedRequest)`; bridge initialization and
`SetRouteContext` never call that API. An unavailable platform callback is retryable only for the
same pinned request envelope, while an invoked or ambiguously failed callback becomes terminal and
cannot be replayed by the sender.

No production Unity route currently calls this sender, because no gameplay route is enabled. A
future route must call it only after that route has completed its own presentation/readiness checks;
it must not treat receipt of `SetRouteContext` as readiness.

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

`UnityBridgeOutcomeSender` implements `IUnityBridgeOutcomeSink` and is
production-registered by `AndroidBridgeRuntimeHost`. Its constructor captures the current
thread as the declared Unity main thread and takes exclusive ownership of one
`IUnityBridgeOutcomePlatformAdapter`. It also requires an
`IUnityBridgeOutcomeDispatchResultSink`; there is no constructor that silently
discards dispatch results. The host constructs the adapter, result sink, and sender on the
Unity main thread, keeps the sender alive until the receiver's bounded graceful drain
completes, and disposes the receiver first and the sender afterward.

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

`onRouteDispatched` is invoked only after a validated request is successfully sent through the reflected Unity message method. It is not a Unity-loaded or route-ready acknowledgement, does not remove the native starting surface, and is not invoked for a missing runtime, invalid route request, or unavailable send method.

Malformed, stale, mismatched, unsupported, and duplicate outcomes surface a stable `bridge.*` protocol diagnostic through `onProtocolError`. They do not fabricate a route result or consume the active request. Callback delivery is posted to the host view before invoking UI/navigation callbacks, and a disposed host's registration token cannot clear a newer host.

Before posting any JVM callback to the main thread, Android performs a non-allocating UTF-8 scan
against the inclusive 32 KiB message limit. Null and oversized values enqueue only their typed
diagnostic and never retain the raw value in a `Runnable`. At most 32 regular callbacks plus one
payload-free overflow sentinel can wait for main-thread delivery. Overflow closes admission and
the bridge session fail-closed with `bridge.session_closed`; later burst items are dropped. Host
disposal closes admission immediately and revokes every posted delivery that has not reached its
identity-permit start transition. A delivery whose start transition already won may finish, while
no later UI callback can begin after close returns. Close never waits for callback completion, and
a callback may close its own dispatcher without deadlock. Admission and `View.post` share one
serialized producer boundary, so the overflow sentinel cannot overtake callbacks admitted before
it, including under concurrent JNI producers.
The count bound can retain up to 32 payload objects, runnables/lambdas, and raw strings of at most
32 KiB UTF-8 each until the main thread drains them: roughly 1–2 MiB of Java/Kotlin string storage
at the declared maximum, plus object, queue, and later parse overhead. It is not an aggregate-byte
budget.

Unity sender rejection, unavailability, busy/thread failure, retention
exhaustion, and invocation failure remain local typed dispatch results. Direct
callers receive them synchronously; each admitted receiver-facing `Publish`
forwards the original report plus exactly one result through the required
result sink. They are not sent through the same unavailable callback, do not
fabricate another route outcome, and do not activate fallback gameplay.

## Validation

Required checks for bridge changes:

- Android unit tests: `:app:testDebugUnitTest`
- Android instrumentation: off-main callback delivery, disposed-host rejection,
  real-shell safe return, back-navigation disposal, and Activity recreation with
  stale-request rejection
- Android debug/release assembly and lint
- Focused Unity EditMode exporter contract tests, including every preflight
  rejection, exact-path cleanup, settings restoration, artifact drift, and
  deterministic summary behavior
- One authorized batch export with the exact command/profile above; retain the
  ignored summary and log as local evidence and do not stage generated output
- The export-stage check proves nonempty bounded IL2CPP generated source and
  toolchain inputs plus the Gradle declaration that later produces
  `libil2cpp.so`; it does not claim Gradle native compilation or AAR packaging
- Device smoke test with a Unity export packaged into the app
- Route startup test from Android to Unity
- Outcome callback test from Unity to Android
- Back, home, pause, resume, and destroy lifecycle test

### Retained export evidence — 2026-08-06

The guarded command above completed on Unity `2022.3.62f3` with status
`Succeeded`, Android target, IL2CPP, ARM64 only, minimum API 24, and the ordered
Boot, RealmSelection, and Kingdom scenes. The exact ignored export contained
2,699 files / 436,882,793 bytes with deterministically computed inventory SHA-256
`075141b8fb4a2f4fb459e8d717b5765f6e1b701bdf410142b1ab0f36ca1abd89`;
the corrected contract's `BuildReport` recorded zero errors and zero warnings
in `00:00:39.0483549`. The run began with the prior retained 2,699-file,
225-descendant-directory export at the same total byte count, so successful
recreation also exercised the bounded retained-handle cleanup against a
representative generated tree before rebuild and inspection. The bounded staged
`Il2CppOutputProject` contained 2,327 files / 386,572,461 bytes, one nonempty
host-valid PE `il2cpp.exe` tool, generated registration/API source, and the Gradle
declaration that later writes `jniLibs/arm64-v8a/libil2cpp.so`. Exported native
ABI directories were exactly `arm64-v8a`; nonempty `libmain.so` and
`libunity.so` passed ELF-signature checks.

This evidence proves the export stage only. No exported Gradle assemble task,
final `libil2cpp.so`, AAR, Android-app inclusion, installed-size delta, or
device execution was produced or claimed.

Current Android contract coverage includes valid/invalid request and outcome JSON, unsupported versions and exact enum values, malformed and oversized input, duplicate request/outcome members, nullable Java/JNI boundary rejection, bounded payloads, same-route retries, stale and duplicate outcomes, malformed-then-valid recovery, callback replacement, off-main UI dispatch, and host disposal.

Compiled Android instrumentation also mounts the debug smoke route through the real
`MainActivity` shell. It verifies correlated unavailable safe return, visible containment of an
unapproved success, back-navigation disposal followed by a dropped late outcome, and Activity
recreation with a new request identity. A late outcome for the pre-recreation request must not
complete or replace the new session; the current correlated unavailable outcome can still return
safely to Debug. These tests do not claim physical-device execution or a generated Unity-enabled
APK on this host.

Current Android host-lifecycle coverage additionally includes deferred focus until resume,
duplicate resume/pause/stop suppression, focus restoration after a real resume, ordered and
idempotent focused teardown, post-destroy signal rejection, exact configuration/trim callback
ordering, individually reached resume/focus-gain/direct-pause/low-memory/configuration failure
paths, callback-exception containment, bounded non-recursive single-owner FIFO handoff,
cancel-versus-dequeue races, self-requeue rejection, off-main unattached-waiter disposal,
registration-failure teardown, post-registration activation rollback and clean replacement,
strict pre-construction reflection signatures and constructor-failure containment,
`LifecycleOwner`-keyed Android-view replacement, pre-post UTF-8 rejection, concurrent producer
post ordering, exactly-one bounded burst overflow, close-versus-delivery-start linearization,
re-entrant dispatcher close, disposal admission closure, stale-release protection, and
replacement-host callback delivery. JVM state-machine tests prove close-before-publication,
close-during-activation, concurrent close, successful ownership transfer, and unproven-cleanup
retention. Compiled Android instrumentation fixtures additionally exercise destruction from a
constructor, concurrent constructor blocking, external registration before attachment, and
retained-state resume after attachment, plus clean registrar recovery and throwing-view
uncertainty; current device execution remains separately disclosed below.
These are controller/JVM and Android host tests; they do not substitute for a packaged Unity
runtime or physical-device lifecycle proof.

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
- The Android lifecycle slice adds one host controller, one active lease object, at most four
  lightweight FIFO waiter records, one application component-callback registration, and a
  bounded activation permit. Callback admission is capped at 32 regular posts plus one
  payload-free overflow sentinel; each posted item adds one lightweight identity delivery permit
  and one once-only completion flag until delivery or disposal.
  Those regular posts can retain roughly 1–2 MiB of bounded string storage at maximum payloads,
  plus object/queue/parse overhead; the implementation bounds count rather than aggregate bytes.
  It adds no timer, polling loop, per-frame work, content asset, native library, or dependency.
  UTF-8 admission is one bounded O(n) scan and does not allocate a byte array. Exact optimized APK
  and installed-size deltas remain build-dependent measurements.
- No external binary, asset, AAR, package, assembly definition, or dependency
  is added.
- The Editor-only exporter and EditMode fixtures add no Player runtime code,
  per-frame work, or install payload. A real ignored export temporarily consumes
  bounded local disk and build CPU/RAM; its exact duration, peak memory, and
  generated bytes must be measured by the authorized batch run.
- Player build size, installed size, runtime-memory/startup allocation, final
  Gradle IL2CPP native compilation/linker behavior, Android package assembly,
  AAR output, profiler measurements,
  physical-device startup/frame pacing/thermal behavior, and low-end device
  compatibility remain unperformed.

Still blocked for #135 completion:

- regeneration of exact-profile debug and release Unity AARs on the authorized Windows host,
  followed by Unity-enabled APK verification against those exact artifacts;
- physical-device execution of the correlated unknown-route unavailable round trip;
- packaged back/home/app-switch, rotation/recreation, multi-window, process-death/runtime-loss,
  audio/input/keyboard/controller, focus, low-memory, and repeated-launch lifecycle proof;
- packaged representative-device performance, memory, thermal, and size evidence;
- any approved production gameplay route and its durable consequence authority.
