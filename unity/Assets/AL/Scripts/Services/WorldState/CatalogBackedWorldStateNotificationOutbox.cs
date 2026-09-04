using System;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Notifications;
using AL.Core.Interfaces.WorldState;

namespace AL.Services.WorldState
{
    public sealed class CatalogBackedWorldStateNotificationOutbox : IWorldStateNotificationOutbox
    {
        public const string SourceSystemId = "al_source_world_state";
        public const string EventNameParameter = "event_name";
        public const string DefaultEventNameReference = "world.event.veil_omen";

        private readonly INotificationService _notifications;
        private readonly INotificationClock _clock;
        private readonly string _eventNameReference;

        public CatalogBackedWorldStateNotificationOutbox(
            INotificationService notifications,
            INotificationClock clock,
            string eventNameReference)
        {
            _notifications = notifications;
            _clock = clock;
            _eventNameReference = string.IsNullOrWhiteSpace(eventNameReference)
                ? DefaultEventNameReference
                : eventNameReference;
        }

        public bool TryEnqueue(WorldStateNotificationIntent intent, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (intent == null)
            {
                diagnostic = "AL-WST-NOTIFY-INTENT";
                return false;
            }

            if (!string.Equals(
                    intent.DefinitionId,
                    WorldStateAuthoredCatalog.StartNotificationId,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    intent.DefinitionId,
                    WorldStateAuthoredCatalog.EndNotificationId,
                    StringComparison.Ordinal))
            {
                diagnostic = "AL-WST-NOTIFY-DEFINITION";
                return false;
            }

            if (_notifications == null)
            {
                diagnostic = "AL-WST-NOTIFY-SERVICE";
                return false;
            }

            DateTime occurred = _clock == null ? DateTime.UtcNow : _clock.UtcNow;
            NotificationEnqueueResult result = _notifications.Enqueue(
                new NotificationRequest(
                    intent.DefinitionId,
                    SourceSystemId,
                    intent.CorrelationId,
                    occurred,
                    new[]
                    {
                        new NotificationParameter(
                            EventNameParameter,
                            NotificationParameterValue.FromLocalizationReference(_eventNameReference))
                    },
                    null,
                    null,
                    null));
            if (result != null &&
                (result.Status == NotificationEnqueueStatus.AcceptedPending ||
                 result.Status == NotificationEnqueueStatus.AcceptedAlreadyPresent ||
                 result.Status == NotificationEnqueueStatus.AcceptedReplacedEarlier))
            {
                return true;
            }

            diagnostic = result == null || string.IsNullOrWhiteSpace(result.DiagnosticCode)
                ? "AL-WST-NOTIFY"
                : result.DiagnosticCode;
            return false;
        }
    }
}
