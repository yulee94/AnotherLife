using System;
using System.Collections;
using System.Collections.Generic;
using AL.Core.Interfaces.Notifications;
using AL.Services.Local;
using AL.UI.DesignSystem;
using AL.UI.Notifications;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AL.Tests.PlayMode
{
    public sealed class NotificationPresenterPlayModeTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UiAccessibilityPreferences.Reset();
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null)
                {
                    UnityEngine.Object.Destroy(objects[index]);
                }
            }

            objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RequiredAcknowledgementRendersAndCompletesThroughRuntimeFrames()
        {
            GameObject eventSystemObject = Track(new GameObject(
                "NotificationEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule)));
            EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();
            GameObject priorSelection = Track(new GameObject(
                "PriorSelection",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)));
            eventSystem.SetSelectedGameObject(priorSelection);
            UiAccessibilityPreferences.Configure(1.5f, true, true, true);

            NotificationDefinition definition = Definition();
            var resolver = new FakeDefinitionResolver(definition);
            var service = new LocalNotificationService(
                resolver,
                resolver,
                new FakeClock(),
                new UnavailableNotificationActionRegistry(),
                new FakeDiagnosticSink());
            GameObject hostObject = Track(new GameObject(NotificationPresenterHost.RuntimeHostName));
            NotificationPresenterHost host = hostObject.AddComponent<NotificationPresenterHost>();
            host.Bind(service, new FakeContentResolver());

            NotificationEnqueueResult enqueue = service.Enqueue(Request(definition));
            Assert.That(enqueue.Status, Is.EqualTo(NotificationEnqueueStatus.AcceptedPending));

            yield return null;

            Assert.That(host.Overlay.IsShowing, Is.True);
            Assert.That(host.Overlay.BlocksBackground, Is.True);
            Assert.That(host.Overlay.SeverityLabel.text, Is.EqualTo("[!!]"));
            Assert.That(host.Overlay.ActionButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(host.Overlay.ActionButton.gameObject));
            Assert.That(host.GetComponentInChildren<Animator>(), Is.Null,
                "Reduced-motion presentation must not depend on an animated cue.");
            Assert.That(
                service.GetReceipt(enqueue.NotificationInstanceId).State,
                Is.EqualTo(NotificationDeliveryState.Presented));

            host.Overlay.ActionButton.onClick.Invoke();
            yield return null;

            Assert.That(host.Overlay.IsShowing, Is.False);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(priorSelection));
            Assert.That(
                service.GetReceipt(enqueue.NotificationInstanceId).State,
                Is.EqualTo(NotificationDeliveryState.Acknowledged));
        }

        private GameObject Track(GameObject value)
        {
            objects.Add(value);
            return value;
        }

        private static NotificationDefinition Definition()
        {
            return new NotificationDefinition(
                "al_notify_presenter_playmode_test",
                NotificationTechnicalLimits.CurrentDefinitionSchemaVersion,
                1,
                NotificationSeverity.BlockingError,
                NotificationCategory.System,
                NotificationChannel.Acknowledgement,
                new[] { NotificationChannel.Acknowledgement },
                NotificationAcknowledgementPolicy.Required,
                NotificationDurabilityPolicy.SessionUntilAcknowledged,
                new NotificationExpiryPolicy(NotificationExpiryMode.None, 0d, false),
                100,
                NotificationDeduplicationPolicy.ByCorrelationAndDefinition,
                NotificationPrivacyClass.PublicGameplay,
                true,
                false,
                new[] { "al_source_presenter_playmode_test" },
                Array.Empty<NotificationParameterDefinition>(),
                Array.Empty<NotificationActionDefinition>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static NotificationRequest Request(NotificationDefinition definition)
        {
            return new NotificationRequest(
                definition.DefinitionId,
                "al_source_presenter_playmode_test",
                "notification:presenter:playmode",
                new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc),
                Array.Empty<NotificationParameter>(),
                null,
                null,
                null);
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

        private sealed class FakeContentResolver : INotificationPresentationContentResolver
        {
            public NotificationPresentationContentResolution ResolveContent(
                NotificationQueueRecordSnapshot record)
            {
                return new NotificationPresentationContentResolution(
                    NotificationPresentationContentStatus.Resolved,
                    new NotificationPresentationContent(
                        "Profile Protected",
                        "Your progress is protected.",
                        "Acknowledge",
                        string.Empty,
                        "Profile Protected. Your progress is protected."),
                    null);
            }
        }

        private sealed class FakeClock : INotificationClock
        {
            public DateTime UtcNow { get; } =
                new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc);

            public double RealtimeSeconds => Time.realtimeSinceStartupAsDouble;
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
    }
}
