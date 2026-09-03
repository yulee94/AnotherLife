using System;
using System.Collections.Generic;
using AL.Core.Interfaces.Notifications;
using AL.Services.Local;
using AL.UI.DesignSystem;
using AL.UI.Notifications;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.Tests.EditMode.Notifications
{
    public sealed class NotificationPresenterTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            UiAccessibilityPreferences.Reset();
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(objects[index]);
            }

            objects.Clear();
        }

        [Test]
        public void RequiredAcknowledgementPlansModalActionAndNonColorSeverity()
        {
            NotificationQueueRecordSnapshot record = Record(
                Definition(
                    acknowledgement: NotificationAcknowledgementPolicy.Required,
                    durability: NotificationDurabilityPolicy.SessionUntilAcknowledged,
                    channel: NotificationChannel.Acknowledgement,
                    severity: NotificationSeverity.BlockingError),
                NotificationChannel.Acknowledgement);

            bool planned = NotificationPresentationPlanner.TryCreate(
                new NotificationPresentationOffer(record),
                new FakeContentResolver(),
                out NotificationPresentationPlan plan,
                out string failureCode);

            Assert.IsTrue(planned, failureCode);
            Assert.AreEqual(NotificationPresentationAction.Acknowledge, plan.Action);
            Assert.AreEqual("Acknowledge", plan.ActionLabel);
            Assert.AreEqual("[!!]", plan.SeverityMarker);
            Assert.IsTrue(plan.BlocksBackground);
            Assert.IsTrue(plan.MovesFocus);
        }

        [Test]
        public void NonDismissibleToastWithoutExpiryFailsClosed()
        {
            NotificationQueueRecordSnapshot record = Record(
                Definition(),
                NotificationChannel.Toast);

            bool planned = NotificationPresentationPlanner.TryCreate(
                new NotificationPresentationOffer(record),
                new FakeContentResolver(),
                out NotificationPresentationPlan plan,
                out string failureCode);

            Assert.IsFalse(planned);
            Assert.IsNull(plan);
            Assert.AreEqual(NotificationPresentationPlanner.LifetimeDiagnostic, failureCode);
        }

        [Test]
        public void UnsafeRichTextContentFailsClosedBeforeRendering()
        {
            NotificationQueueRecordSnapshot record = Record(
                Definition(expiry: Expiring()),
                NotificationChannel.Toast);

            bool planned = NotificationPresentationPlanner.TryCreate(
                new NotificationPresentationOffer(record),
                new FakeContentResolver(body: "<b>unsafe</b>"),
                out NotificationPresentationPlan plan,
                out string failureCode);

            Assert.IsFalse(planned);
            Assert.IsNull(plan);
            Assert.AreEqual(NotificationPresentationPlanner.ContentDiagnostic, failureCode);
        }

        [Test]
        public void HostRendersConfirmsAnnouncesAndAcknowledgesRequiredNotification()
        {
            NotificationDefinition definition = Definition(
                acknowledgement: NotificationAcknowledgementPolicy.Required,
                durability: NotificationDurabilityPolicy.SessionUntilAcknowledged,
                channel: NotificationChannel.Acknowledgement,
                severity: NotificationSeverity.BlockingError);
            var resolver = new FakeDefinitionResolver(definition);
            var service = new LocalNotificationService(
                resolver,
                resolver,
                new FakeClock(),
                new UnavailableNotificationActionRegistry(),
                new FakeDiagnosticSink());
            var announcer = new FakeAnnouncer();
            NotificationPresenterHost host = NewHost();
            host.Bind(service, new FakeContentResolver(), announcer);

            NotificationEnqueueResult enqueue = service.Enqueue(Request(definition));
            host.TickForTests();

            Assert.AreEqual(NotificationEnqueueStatus.AcceptedPending, enqueue.Status);
            Assert.AreEqual(
                NotificationDeliveryState.Presented,
                service.GetReceipt(enqueue.NotificationInstanceId).State);
            Assert.IsTrue(host.Overlay.IsShowing);
            Assert.IsTrue(host.Overlay.BlocksBackground);
            Assert.AreEqual("Profile Protected", host.Overlay.TitleLabel.text);
            Assert.AreEqual("Acknowledge", host.Overlay.ActionLabel.text);
            Assert.AreEqual(1, announcer.CallCount);

            host.Overlay.ActionButton.onClick.Invoke();

            Assert.AreEqual(
                NotificationDeliveryState.Acknowledged,
                service.GetReceipt(enqueue.NotificationInstanceId).State);
            Assert.IsFalse(host.Overlay.IsShowing);
        }

        [Test]
        public void SecondHostCannotClaimTheSamePresentationChannels()
        {
            NotificationDefinition definition = Definition(expiry: Expiring());
            var resolver = new FakeDefinitionResolver(definition);
            var service = new LocalNotificationService(
                resolver,
                resolver,
                new FakeClock(),
                new UnavailableNotificationActionRegistry(),
                new FakeDiagnosticSink());
            NotificationPresenterHost first = NewHost();
            NotificationPresenterHost second = NewHost();

            first.Bind(service, new FakeContentResolver());
            second.Bind(service, new FakeContentResolver());

            Assert.IsTrue(first.IsRegistered);
            Assert.IsFalse(second.IsRegistered);
        }

        [Test]
        public void ToastDoesNotMoveFocusAndActionMeetsLargeTextTouchTarget()
        {
            GameObject eventSystemObject = Track(new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule)));
            GameObject priorSelection = Track(new GameObject(
                "PriorSelection",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)));
            EventSystem.current.SetSelectedGameObject(priorSelection);
            UiAccessibilityPreferences.Configure(1.5f, true, true, true);

            NotificationQueueRecordSnapshot toastRecord = Record(
                Definition(expiry: Expiring()),
                NotificationChannel.Toast);
            Assert.IsTrue(NotificationPresentationPlanner.TryCreate(
                new NotificationPresentationOffer(toastRecord),
                new FakeContentResolver(),
                out NotificationPresentationPlan toastPlan,
                out _));
            NotificationPresenterOverlay overlay = NewOverlay();
            overlay.Show(toastPlan, null);

            Assert.AreSame(priorSelection, EventSystem.current.currentSelectedGameObject);

            NotificationQueueRecordSnapshot requiredRecord = Record(
                Definition(
                    acknowledgement: NotificationAcknowledgementPolicy.Required,
                    durability: NotificationDurabilityPolicy.SessionUntilAcknowledged,
                    channel: NotificationChannel.Acknowledgement),
                NotificationChannel.Acknowledgement);
            Assert.IsTrue(NotificationPresentationPlanner.TryCreate(
                new NotificationPresentationOffer(requiredRecord),
                new FakeContentResolver(),
                out NotificationPresentationPlan requiredPlan,
                out _));
            overlay.Show(requiredPlan, () => { });

            RectTransform actionRect = overlay.ActionButton.transform as RectTransform;
            Assert.GreaterOrEqual(actionRect.sizeDelta.x, UiAccessibilityRuntime.MinimumTouchTarget);
            Assert.GreaterOrEqual(actionRect.sizeDelta.y, UiAccessibilityRuntime.MinimumTouchTarget);
            Assert.GreaterOrEqual(overlay.TitleLabel.fontSize, 39);
            Assert.AreSame(
                overlay.ActionButton.gameObject,
                EventSystem.current.currentSelectedGameObject);
        }

        [Test]
        public void SafeAreaProjectionClampsToViewport()
        {
            GameObject targetObject = Track(new GameObject("SafeArea", typeof(RectTransform)));
            RectTransform target = targetObject.GetComponent<RectTransform>();

            NotificationPresenterOverlay.ApplySafeArea(
                target,
                new Rect(0f, 0f, 1000f, 500f),
                new Rect(-50f, 20f, 900f, 600f));

            Assert.AreEqual(new Vector2(0f, 0.04f), target.anchorMin);
            Assert.AreEqual(new Vector2(0.85f, 1f), target.anchorMax);
            Assert.AreEqual(Vector2.zero, target.offsetMin);
            Assert.AreEqual(Vector2.zero, target.offsetMax);
        }

        private NotificationPresenterHost NewHost()
        {
            GameObject root = Track(new GameObject(NotificationPresenterHost.RuntimeHostName));
            return root.AddComponent<NotificationPresenterHost>();
        }

        private NotificationPresenterOverlay NewOverlay()
        {
            GameObject root = Track(new GameObject("OverlayHost"));
            return NotificationPresenterOverlay.Mount(root.transform);
        }

        private GameObject Track(GameObject value)
        {
            objects.Add(value);
            return value;
        }

        private static NotificationDefinition Definition(
            NotificationAcknowledgementPolicy acknowledgement =
                NotificationAcknowledgementPolicy.None,
            NotificationDurabilityPolicy durability =
                NotificationDurabilityPolicy.SessionTransient,
            NotificationChannel channel = NotificationChannel.Toast,
            NotificationSeverity severity = NotificationSeverity.Information,
            NotificationExpiryPolicy expiry = null)
        {
            return new NotificationDefinition(
                "al_notify_presenter_test",
                NotificationTechnicalLimits.CurrentDefinitionSchemaVersion,
                1,
                severity,
                NotificationCategory.System,
                channel,
                new[] { channel },
                acknowledgement,
                durability,
                expiry ?? new NotificationExpiryPolicy(NotificationExpiryMode.None, 0d, false),
                80,
                NotificationDeduplicationPolicy.ByCorrelationAndDefinition,
                NotificationPrivacyClass.PublicGameplay,
                true,
                acknowledgement == NotificationAcknowledgementPolicy.None,
                new[] { "al_source_presenter_test" },
                Array.Empty<NotificationParameterDefinition>(),
                Array.Empty<NotificationActionDefinition>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static NotificationExpiryPolicy Expiring()
        {
            return new NotificationExpiryPolicy(
                NotificationExpiryMode.AfterPresentation,
                5d,
                false);
        }

        private static NotificationRequest Request(NotificationDefinition definition)
        {
            return new NotificationRequest(
                definition.DefinitionId,
                "al_source_presenter_test",
                "notification:presenter:test",
                new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc),
                Array.Empty<NotificationParameter>(),
                null,
                null,
                null);
        }

        private static NotificationQueueRecordSnapshot Record(
            NotificationDefinition definition,
            NotificationChannel channel)
        {
            var receipt = new NotificationDeliveryReceipt(
                "al_notification_session_00000000000000000001",
                NotificationDeliveryState.PendingPresenter,
                null,
                null,
                null,
                null,
                0,
                null);
            return new NotificationQueueRecordSnapshot(
                receipt.NotificationInstanceId,
                1,
                definition,
                Request(definition),
                channel,
                receipt);
        }

        private sealed class FakeContentResolver : INotificationPresentationContentResolver
        {
            private readonly string body;

            internal FakeContentResolver(string body = "Your progress is protected.")
            {
                this.body = body;
            }

            public NotificationPresentationContentResolution ResolveContent(
                NotificationQueueRecordSnapshot record)
            {
                return new NotificationPresentationContentResolution(
                    NotificationPresentationContentStatus.Resolved,
                    new NotificationPresentationContent(
                        "Profile Protected",
                        body,
                        "Acknowledge",
                        "Dismiss",
                        "Profile Protected. " + body),
                    null);
            }
        }

        private sealed class FakeDefinitionResolver :
            INotificationDefinitionResolver,
            INotificationLocalizationReferenceAuthority
        {
            private readonly NotificationDefinition definition;

            internal FakeDefinitionResolver(NotificationDefinition definition)
            {
                this.definition = definition;
            }

            public bool IsAvailable => true;

            public bool Contains(string localizationReference)
            {
                return false;
            }

            public NotificationDefinitionResolution Resolve(string definitionId)
            {
                return string.Equals(definitionId, definition.DefinitionId, StringComparison.Ordinal)
                    ? new NotificationDefinitionResolution(
                        NotificationDefinitionResolutionStatus.Found,
                        definition,
                        null)
                    : new NotificationDefinitionResolution(
                        NotificationDefinitionResolutionStatus.UnknownId,
                        null,
                        "AL-NTF-DEFINITION");
            }
        }

        private sealed class FakeClock : INotificationClock
        {
            public DateTime UtcNow { get; } =
                new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

            public double RealtimeSeconds => 0d;
        }

        private sealed class FakeDiagnosticSink : INotificationDiagnosticSink
        {
            public void Record(NotificationDiagnostic diagnostic)
            {
            }

            public void RecordLegacyRaw(string escapedTechnicalText, bool isError)
            {
            }
        }

        private sealed class FakeAnnouncer : INotificationAccessibilityAnnouncer
        {
            public int CallCount { get; private set; }

            public NotificationAccessibilityAnnouncementStatus Announce(string announcement)
            {
                CallCount++;
                return NotificationAccessibilityAnnouncementStatus.Announced;
            }
        }
    }
}
