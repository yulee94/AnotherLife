using System;
using System.Globalization;
using System.Linq;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Notifications;
using UnityEngine;

namespace AL.Services.Local
{
    public sealed class LocalNotificationService : INotificationService
    {
        private const string LegacyDiagnostic = "AL-NTF-LEGACY-RAW";
        private readonly NotificationSessionQueue _queue;
        private readonly INotificationDiagnosticSink _diagnosticSink;

        public LocalNotificationService()
            : this(new UnavailableNotificationDefinitionResolver())
        {
        }

        private LocalNotificationService(
            UnavailableNotificationDefinitionResolver unavailableResolver)
            : this(
                unavailableResolver,
                unavailableResolver,
                new SystemNotificationClock(),
                new UnavailableNotificationActionRegistry(),
                new UnityNotificationDiagnosticSink())
        {
        }

        public LocalNotificationService(
            INotificationDefinitionResolver definitionResolver,
            INotificationLocalizationReferenceAuthority localizationReferenceAuthority,
            INotificationClock clock,
            INotificationActionRegistry actionRegistry,
            INotificationDiagnosticSink diagnosticSink)
            : this(
                definitionResolver,
                localizationReferenceAuthority,
                clock,
                actionRegistry,
                diagnosticSink,
                null)
        {
        }

        public LocalNotificationService(
            INotificationDefinitionResolver definitionResolver,
            INotificationLocalizationReferenceAuthority localizationReferenceAuthority,
            INotificationClock clock,
            INotificationActionRegistry actionRegistry,
            INotificationDiagnosticSink diagnosticSink,
            INotificationDurableStore durableStore)
        {
            _diagnosticSink = diagnosticSink ?? new UnityNotificationDiagnosticSink();
            _queue = new NotificationSessionQueue(
                definitionResolver,
                localizationReferenceAuthority,
                clock,
                actionRegistry,
                _diagnosticSink,
                durableStore);
        }

        public NotificationEnqueueResult Enqueue(NotificationRequest request) =>
            _queue.Enqueue(request);

        public NotificationPresenterRegistrationResult RegisterPresenter(
            INotificationPresenter presenter,
            NotificationPresenterCapabilities capabilities) =>
            _queue.RegisterPresenter(presenter, capabilities);

        public NotificationPresenterUnregistrationStatus UnregisterPresenter(
            NotificationPresenterRegistrationToken token) =>
            _queue.UnregisterPresenter(token);

        public NotificationReceiptUpdateResult ConfirmPresented(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId) =>
            _queue.ConfirmPresented(token, notificationInstanceId);

        public NotificationReceiptUpdateResult ReportDeliveryFailure(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId,
            string failureCode) =>
            _queue.ReportDeliveryFailure(token, notificationInstanceId, failureCode);

        public NotificationReceiptUpdateResult Acknowledge(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId) =>
            _queue.Acknowledge(token, notificationInstanceId);

        public NotificationReceiptUpdateResult Dismiss(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId) =>
            _queue.Dismiss(token, notificationInstanceId);

        public NotificationActionResult InvokeAction(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId,
            NotificationActionInvocation invocation) =>
            _queue.InvokeAction(token, notificationInstanceId, invocation);

        public NotificationQueueObserverRegistrationResult RegisterObserver(
            INotificationQueueObserver observer) =>
            _queue.RegisterObserver(observer);

        public NotificationQueueObserverUnregistrationStatus UnregisterObserver(
            NotificationQueueObserverRegistrationToken token) =>
            _queue.UnregisterObserver(token);

        public NotificationQueueSnapshot GetSnapshot() => _queue.GetSnapshot();

        public NotificationDeliveryReceipt GetReceipt(string notificationInstanceId) =>
            _queue.GetReceipt(notificationInstanceId);

        public void Refresh() => _queue.Refresh();

        [Obsolete("Compatibility-only raw notification wrapper. Use Enqueue(NotificationRequest).")]
        public void ShowMessage(string message)
        {
            RecordLegacy(message, false);
        }

        [Obsolete("Compatibility-only raw notification wrapper. Use Enqueue(NotificationRequest).")]
        public void ShowError(string error)
        {
            RecordLegacy(error, true);
        }

        [Obsolete("Compatibility-only raw notification wrapper. Use Enqueue(NotificationRequest).")]
        public void ShowResourceGain(ResourceType type, long amount)
        {
            RecordLegacy(
                string.Concat(
                    "+",
                    amount.ToString(CultureInfo.InvariantCulture),
                    " ",
                    type.ToString()),
                false);
        }

        private void RecordLegacy(string value, bool isError)
        {
            try
            {
                _diagnosticSink.Record(
                    new NotificationDiagnostic(
                        LegacyDiagnostic,
                        isError
                            ? NotificationSeverity.RecoverableError
                            : NotificationSeverity.Information,
                        null,
                        null));
            }
            catch
            {
                // Compatibility diagnostics cannot change legacy caller behavior.
            }

            try
            {
                _diagnosticSink.RecordLegacyRaw(EscapeTechnicalText(value), isError);
            }
            catch
            {
                // Compatibility logging is non-authoritative.
            }
        }

        private static string EscapeTechnicalText(string value)
        {
            string source = value ?? string.Empty;
            var output = new StringBuilder(Math.Min(source.Length, 512));
            for (int index = 0; index < source.Length && output.Length < 512; index++)
            {
                char character = source[index];
                if (char.IsControl(character))
                {
                    output.Append(' ');
                }
                else if (character == '<')
                {
                    output.Append("&lt;");
                }
                else if (character == '>')
                {
                    output.Append("&gt;");
                }
                else if (character == '&')
                {
                    output.Append("&amp;");
                }
                else
                {
                    output.Append(character);
                }
            }

            return output.ToString();
        }

        private sealed class UnityNotificationDiagnosticSink : INotificationDiagnosticSink
        {
            public void Record(NotificationDiagnostic diagnostic)
            {
                if (diagnostic == null)
                {
                    return;
                }

                string context = string.Join(
                    " ",
                    new[] { diagnostic.DefinitionId, diagnostic.CorrelationId }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                string message = string.IsNullOrEmpty(context)
                    ? $"[{diagnostic.Code}] Notification diagnostic."
                    : $"[{diagnostic.Code}] Notification diagnostic: {context}.";
                if (diagnostic.Severity >= NotificationSeverity.RecoverableError)
                {
                    Debug.LogWarning(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }

            public void RecordLegacyRaw(string escapedTechnicalText, bool isError)
            {
                string message =
                    $"[{LegacyDiagnostic}] Console-only compatibility fallback: {escapedTechnicalText}";
                if (isError)
                {
                    Debug.LogWarning(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }
    }
}
