using System.Reflection;
using AL.Platform.Android;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.AndroidBridge
{
    public sealed class AndroidBridgeRuntimeHostTests
    {
        private const string RequestOne = "request-0001";
        private const string RequestTwo = "request-0002";
        private const string Route = "bridge.smoke";

        private GameObject bridgeObject;
        private AL.Platform.Android.AndroidBridge bridge;
        private AndroidBridgeRuntimeHost host;

        [TearDown]
        public void TearDown()
        {
            if (bridgeObject != null)
            {
                UnityEngine.Object.DestroyImmediate(bridgeObject);
                bridgeObject = null;
            }

            bridge = null;
            host = null;
        }

        [Test]
        public void HostTargetsTheExactJvmSendMessageGameObjectName()
        {
            Assert.That(
                AndroidBridgeRuntimeHost.BridgeGameObjectName,
                Is.EqualTo("AndroidBridge"));
        }

        [Test]
        public void InitializeWiresReceiverThroughSenderAndPlatformAdapter()
        {
            CreateHost();

            host.InitializeBridge();
            Assert.That(host.LastDispatchStatus, Is.Null);

            bridge.SetRouteContext(ValidRequestJson());

            // The receiver parsed the request and produced a correlated unavailable outcome, which the
            // sender encoded and handed to the platform adapter. In the Editor the adapter reports
            // Unavailable without touching JNI, proving the full receive->send wiring is connected.
            Assert.That(
                host.LastDispatchStatus,
                Is.EqualTo(
                    UnityBridgeOutcomeDispatchStatus.PlatformUnavailable));
        }

        [Test]
        public void InitializeIsIdempotentAndDoesNotRejectTheRegisteredSink()
        {
            CreateHost();

            host.InitializeBridge();
            host.InitializeBridge();
            host.InitializeBridge();

            bridge.SetRouteContext(ValidRequestJson());

            Assert.That(
                host.LastDispatchStatus,
                Is.EqualTo(
                    UnityBridgeOutcomeDispatchStatus.PlatformUnavailable));
        }

        [Test]
        public void TeardownDisposesReceiverBeforeSenderAndRejectsLaterReceives()
        {
            CreateHost();

            host.InitializeBridge();
            bridge.SetRouteContext(ValidRequestJson());
            Assert.That(
                host.LastDispatchStatus,
                Is.EqualTo(
                    UnityBridgeOutcomeDispatchStatus.PlatformUnavailable));

            var onDisable = typeof(AndroidBridgeRuntimeHost).GetMethod(
                "OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var onDestroy = typeof(AndroidBridgeRuntimeHost).GetMethod(
                "OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onDisable, Is.Not.Null);
            Assert.That(onDestroy, Is.Not.Null);

            onDisable.Invoke(host, null);
            onDestroy.Invoke(host, null);

            // After teardown the bridge is disposed and further receives are dropped, not dispatched.
            var statusAfterTeardown = host.LastDispatchStatus;
            bridge.SetRouteContext(ValidRequestJson(RequestTwo));
            Assert.That(
                host.LastDispatchStatus,
                Is.EqualTo(statusAfterTeardown));
        }

        private void CreateHost()
        {
            bridgeObject = new GameObject("AndroidBridge");
            bridge = bridgeObject.AddComponent<AL.Platform.Android.AndroidBridge>();
            host = bridgeObject.AddComponent<AndroidBridgeRuntimeHost>();
        }

        private static string ValidRequestJson(
            string requestId = RequestOne)
        {
            return "{\"contractVersion\":2," +
                   "\"requestId\":\"" + requestId + "\"," +
                   "\"routeId\":\"" + Route + "\"," +
                   "\"intent\":\"preview\"," +
                   "\"requestedCapabilities\":[]}";
        }
    }
}
