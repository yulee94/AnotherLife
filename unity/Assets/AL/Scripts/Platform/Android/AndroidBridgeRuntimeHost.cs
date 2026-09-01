using System;
using UnityEngine;

namespace AL.Platform.Android
{
    /// <summary>
    /// Production wiring for the Android &lt;-&gt; Unity narrative bridge vertical slice.
    ///
    /// The contract (<see cref="UnityBridgeContract"/>), the receiver
    /// (<see cref="AndroidBridge"/>), and the sender (<see cref="UnityBridgeOutcomeSender"/>)
    /// are each dormant until one owner connects them. This component is that owner. On the Unity
    /// main thread it:
    ///
    ///   1. lives on a GameObject named <c>AndroidBridge</c> — the exact name the JVM host targets via
    ///      <c>UnityPlayer.UnitySendMessage("AndroidBridge", "SetRouteContext", json)</c>;
    ///   2. constructs the JNI-to-JVM <see cref="AndroidUnityBridgeOutcomePlatformAdapter"/> and the
    ///      <see cref="UnityBridgeOutcomeSender"/> and explicit
    ///      <see cref="UnityBridgeReadySender"/>, then registers the outcome sender as the receiver's
    ///      sink, so a correlated route outcome flows back to Kotlin through
    ///      <c>com.example.anotherlife.ui.unity.UnityBridgeCallbacks.reportOutcome</c>;
    ///   3. exposes <see cref="TryReportReady"/> for a route to call only after its own presentation
    ///      checks, without treating route receipt as readiness;
    ///   4. logs dispatch results and app foreground/background transitions;
    ///   5. tears down receiver-first, senders-second, matching the receiver's bounded graceful-drain
    ///      contract.
    ///
    /// Construction happens on the main thread (Awake) because the sender and adapter each capture the
    /// constructing thread as their exclusive owner. On non-Android builds the adapter reports
    /// <see cref="UnityBridgePlatformCallbackStatus.Unavailable"/> without touching JNI, so the slice
    /// stays live-but-unavailable in the Editor and only becomes live in an Android player build.
    ///
    /// Lifecycle: the receiver and sender are thread-safe and main-thread-bound, and Unity queues
    /// <c>UnitySendMessage</c> calls while the main loop is paused, so backgrounding/orientation
    /// changes cannot deadlock them. The pause/resume/focus callbacks here are observability only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AndroidBridgeRuntimeHost : MonoBehaviour
    {
        /// <summary>Exact GameObject name the JVM host sends to via UnitySendMessage.</summary>
        public const string BridgeGameObjectName = "AndroidBridge";

        private AndroidBridge _bridge;
        private UnityBridgeOutcomeSender _outcomeSender;
        private UnityBridgeReadySender _readySender;
        private AndroidUnityBridgeOutcomePlatformAdapter
            _outcomePlatformAdapter;
        private AndroidUnityBridgeReadyPlatformAdapter
            _readyPlatformAdapter;
        private DispatchLogSink _dispatchSink;
        private UnityBridgeReadyDispatchStatus? _lastReadyDispatchStatus;
        private bool _initialized;
        private bool _tearingDown;

        /// <summary>
        /// Most recent outcome-dispatch status recorded by this host, or null before any dispatch.
        /// Exposed so tests and diagnostics can confirm the receiver→sender→adapter path is wired.
        /// </summary>
        public UnityBridgeOutcomeDispatchStatus? LastDispatchStatus =>
            _dispatchSink?.LastStatus;

        /// <summary>
        /// Most recent explicit ready-dispatch status, or null until route code
        /// calls <see cref="TryReportReady"/>.
        /// </summary>
        public UnityBridgeReadyDispatchStatus? LastReadyDispatchStatus =>
            _lastReadyDispatchStatus;

        private void Awake()
        {
            InitializeBridge();
        }

        /// <summary>
        /// Wires the receiver, sender, and platform adapter on the current (main) thread. Idempotent:
        /// later calls are no-ops. Safe to call directly from an EditMode test where Awake does not fire.
        /// </summary>
        public void InitializeBridge()
        {
            if (_initialized || _tearingDown)
            {
                return;
            }

            _bridge = GetComponent<AndroidBridge>();
            if (_bridge == null)
            {
                _bridge = gameObject.AddComponent<AndroidBridge>();
            }

            _dispatchSink = new DispatchLogSink();
            _outcomePlatformAdapter =
                new AndroidUnityBridgeOutcomePlatformAdapter();
            _outcomeSender = new UnityBridgeOutcomeSender(
                _outcomePlatformAdapter,
                _dispatchSink);
            _readyPlatformAdapter =
                new AndroidUnityBridgeReadyPlatformAdapter();
            _readySender = new UnityBridgeReadySender(
                _readyPlatformAdapter);
            _lastReadyDispatchStatus = null;

            // The receiver is created lazily on first SetRouteContext, so registering the sink now
            // (before any request) is required and cannot hit the "receiver already active" guard.
            _bridge.ConfigureOutcomeSink(_outcomeSender);
            _initialized = true;

            Debug.Log(
                "[AL-BRIDGE-RUNTIME] initialized '" + BridgeGameObjectName + "' " +
                "(androidPlayerBuild=" +
                AndroidUnityBridgeOutcomePlatformAdapter.IsAndroidPlayerBuild + ").");
        }

        /// <summary>
        /// Reports readiness for one validated route request. Route code must
        /// call this only after it can visibly present the route and own input;
        /// bridge initialization and SetRouteContext never call it.
        /// </summary>
        public UnityBridgeReadyDispatchResult TryReportReady(
            UnityRouteRequest request)
        {
            UnityBridgeReadyDispatchResult result;
            if (!_initialized || _tearingDown || _readySender == null)
            {
                result = UnityBridgeReadyDispatchResult.Failed(
                    UnityBridgeReadyDispatchStatus.Disposed,
                    false,
                    UnityBridgeProtocolErrorCode.SessionClosed);
            }
            else
            {
                result = _readySender.TryDispatch(request);
            }

            _lastReadyDispatchStatus = result.Status;
            if (result.CallbackInvoked)
            {
                Debug.Log(
                    "[AL-BRIDGE-READY] ready acknowledgement sent.");
            }
            else
            {
                var diagnostic = result.Error?.WireCode ??
                    UnityBridgeContract.GetProtocolErrorWireValue(
                        UnityBridgeProtocolErrorCode.SendUnavailable);
                Debug.LogWarning(
                    "[AL-BRIDGE-READY] dispatch " + result.Status +
                    ": " + diagnostic + ".");
            }

            return result;
        }

        private void OnApplicationPause(bool paused)
        {
            Debug.Log(
                "[AL-BRIDGE-RUNTIME] application " +
                (paused ? "paused" : "resumed") + ".");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Debug.Log(
                "[AL-BRIDGE-RUNTIME] application " +
                (hasFocus ? "focused" : "unfocused") + ".");
        }

        private void OnDisable()
        {
            Teardown();
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void Teardown()
        {
            if (_tearingDown)
            {
                return;
            }

            _tearingDown = true;

            // Receiver first: reject new receives and clear the sink before the sender disappears.
            if (_bridge != null)
            {
                _bridge.Dispose();
                _bridge = null;
            }

            // Senders second: each disposes its exclusively owned platform adapter.
            if (_readySender != null)
            {
                _readySender.Dispose();
                _readySender = null;
            }

            if (_outcomeSender != null)
            {
                _outcomeSender.Dispose();
                _outcomeSender = null;
            }

            // The senders own and dispose their adapters; these references are only dropped.
            _readyPlatformAdapter = null;
            _outcomePlatformAdapter = null;
            _dispatchSink = null;
            _initialized = false;
        }

        /// <summary>
        /// Ensures exactly one wired bridge exists for the process lifetime, before the first scene
        /// loads, regardless of which scene the JVM host activates against. A scene may also carry its
        /// own host; the FindObjectOfType guard keeps this from creating a second instance.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureBridgeRuntime()
        {
            if (FindObjectOfType<AndroidBridgeRuntimeHost>() != null)
            {
                return;
            }

            var bridgeObject = new GameObject(BridgeGameObjectName);
            bridgeObject.AddComponent<AndroidBridge>();
            bridgeObject.AddComponent<AndroidBridgeRuntimeHost>();
            DontDestroyOnLoad(bridgeObject);
        }

        private sealed class DispatchLogSink : IUnityBridgeOutcomeDispatchResultSink
        {
            public UnityBridgeOutcomeDispatchStatus? LastStatus { get; private set; }

            public void Publish(
                UnityBridgeReceiverReport report,
                UnityBridgeOutcomeDispatchResult result)
            {
                LastStatus = result.Status;

                var requestId = result.RequestId ??
                    report?.Request?.RequestId ??
                    "<none>";

                if (result.CallbackInvoked)
                {
                    var outcomeStatus = report?.Outcome == null
                        ? string.Empty
                        : UnityBridgeContract.GetOutcomeStatusWireValue(
                            report.Outcome.Status);
                    Debug.Log(
                        "[AL-BRIDGE-OUTCOME] dispatched request " + requestId +
                        " (status=" + outcomeStatus + ").");
                    return;
                }

                var diagnostic = result.Error?.WireCode ??
                    UnityBridgeContract.GetProtocolErrorWireValue(
                        UnityBridgeProtocolErrorCode.SendUnavailable);
                Debug.LogWarning(
                    "[AL-BRIDGE-OUTCOME] dispatch " + result.Status +
                    " for request " + requestId + ": " + diagnostic + ".");
            }
        }
    }
}
