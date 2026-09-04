using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AL.Core.Interfaces.Notifications
{
    public interface INotificationDurableStore
    {
        bool IsAvailable { get; }

        bool TryCommit(NotificationDurableRecord record, out string diagnostic);

        bool TryUpdate(NotificationDurableRecord record, out string diagnostic);

        IReadOnlyList<NotificationDurableRecord> LoadAll();

        NotificationDurableRecord FindByCorrelation(string correlationId, string definitionId);

        void Clear();
    }

    public sealed class NotificationDurableRecord
    {
        public const int CurrentSchemaVersion = 1;
        public const int CompletedHistoryLimit = 100;

        public string RecordId { get; set; }
        public int NotificationSchemaVersion { get; set; }
        public string DefinitionId { get; set; }
        public int DefinitionVersion { get; set; }
        public string SourceSystemId { get; set; }
        public string CorrelationId { get; set; }
        public long OccurredAtUtcTicks { get; set; }
        public List<NotificationDurableParameter> Parameters { get; set; }
        public NotificationDeliveryState State { get; set; }
        public long AcknowledgedAtUtcTicks { get; set; }
        public long DismissedAtUtcTicks { get; set; }
        public long ExpiresAtUtcTicks { get; set; }
        public long LastDeliveryAttemptUtcTicks { get; set; }
        public int DeliveryAttemptCount { get; set; }
        public string SupersededByRecordId { get; set; }
        public NotificationDurabilityPolicy DurabilityPolicy { get; set; }
        public NotificationPrivacyClass PrivacyClass { get; set; }
        public bool RequiresAcknowledgement { get; set; }

        public NotificationDurableRecord Clone()
        {
            return new NotificationDurableRecord
            {
                RecordId = RecordId ?? string.Empty,
                NotificationSchemaVersion = NotificationSchemaVersion,
                DefinitionId = DefinitionId ?? string.Empty,
                DefinitionVersion = DefinitionVersion,
                SourceSystemId = SourceSystemId ?? string.Empty,
                CorrelationId = CorrelationId ?? string.Empty,
                OccurredAtUtcTicks = OccurredAtUtcTicks,
                Parameters = (Parameters ?? new List<NotificationDurableParameter>())
                    .Select(parameter => parameter == null ? null : parameter.Clone())
                    .Where(parameter => parameter != null)
                    .ToList(),
                State = State,
                AcknowledgedAtUtcTicks = AcknowledgedAtUtcTicks,
                DismissedAtUtcTicks = DismissedAtUtcTicks,
                ExpiresAtUtcTicks = ExpiresAtUtcTicks,
                LastDeliveryAttemptUtcTicks = LastDeliveryAttemptUtcTicks,
                DeliveryAttemptCount = DeliveryAttemptCount,
                SupersededByRecordId = SupersededByRecordId ?? string.Empty,
                DurabilityPolicy = DurabilityPolicy,
                PrivacyClass = PrivacyClass,
                RequiresAcknowledgement = RequiresAcknowledgement
            };
        }

        public static bool IsCompleted(NotificationDeliveryState state)
        {
            return state == NotificationDeliveryState.Acknowledged ||
                   state == NotificationDeliveryState.Dismissed ||
                   state == NotificationDeliveryState.Expired ||
                   state == NotificationDeliveryState.Superseded;
        }

        public bool CanPrune()
        {
            if (!IsCompleted(State))
            {
                return false;
            }

            if (RequiresAcknowledgement && State != NotificationDeliveryState.Acknowledged)
            {
                return false;
            }

            return true;
        }
    }

    public sealed class NotificationDurableParameter
    {
        public string Name { get; set; }
        public NotificationParameterValueKind Kind { get; set; }
        public string TextValue { get; set; }

        public NotificationDurableParameter Clone()
        {
            return new NotificationDurableParameter
            {
                Name = Name ?? string.Empty,
                Kind = Kind,
                TextValue = TextValue ?? string.Empty
            };
        }
    }

    public static class NotificationDurablePrivacy
    {
        public const string PersistenceDiagnostic = "AL-NTF-PERSISTENCE";
        public const string PrivacyDiagnostic = "AL-NTF-PRIVACY";

        public static bool IsDurable(NotificationDurabilityPolicy policy)
        {
            return policy == NotificationDurabilityPolicy.DurableUntilAcknowledged ||
                   policy == NotificationDurabilityPolicy.DurableHistory;
        }

        public static List<NotificationDurableParameter> CapturePersistableParameters(
            NotificationDefinition definition,
            NotificationRequest request)
        {
            var captured = new List<NotificationDurableParameter>();
            if (definition == null || request == null || request.Parameters == null)
            {
                return captured;
            }

            for (int index = 0; index < request.Parameters.Count; index++)
            {
                NotificationParameter parameter = request.Parameters[index];
                if (parameter == null || parameter.Value == null)
                {
                    continue;
                }

                NotificationParameterDefinition schema = definition.ParameterSchema
                    .FirstOrDefault(item =>
                        string.Equals(item.Name, parameter.Name, StringComparison.Ordinal));
                if (schema == null ||
                    !schema.Persistable ||
                    schema.PrivacyClass == NotificationPrivacyClass.SensitiveTechnical)
                {
                    continue;
                }

                captured.Add(
                    new NotificationDurableParameter
                    {
                        Name = parameter.Name,
                        Kind = parameter.Value.Kind,
                        TextValue = parameter.Value.Value as string ??
                                    parameter.Value.CanonicalValue ??
                                    string.Empty
                    });
            }

            return captured;
        }

        public static NotificationDurableRecord FromQueue(
            string recordId,
            NotificationDefinition definition,
            NotificationRequest request,
            NotificationDeliveryState state,
            DateTime? acknowledgedAtUtc,
            DateTime? dismissedAtUtc,
            int deliveryAttemptCount)
        {
            DateTime occurred = request == null ? DateTime.UtcNow : request.OccurredAtUtc;
            return new NotificationDurableRecord
            {
                RecordId = recordId ?? string.Empty,
                NotificationSchemaVersion = NotificationDurableRecord.CurrentSchemaVersion,
                DefinitionId = definition == null ? string.Empty : definition.DefinitionId,
                DefinitionVersion = definition == null ? 0 : definition.ContentVersion,
                SourceSystemId = request == null ? string.Empty : request.SourceSystemId,
                CorrelationId = request == null ? string.Empty : request.CorrelationId,
                OccurredAtUtcTicks = occurred.ToUniversalTime().Ticks,
                Parameters = CapturePersistableParameters(definition, request),
                State = state,
                AcknowledgedAtUtcTicks = acknowledgedAtUtc == null
                    ? 0L
                    : acknowledgedAtUtc.Value.ToUniversalTime().Ticks,
                DismissedAtUtcTicks = dismissedAtUtc == null
                    ? 0L
                    : dismissedAtUtc.Value.ToUniversalTime().Ticks,
                ExpiresAtUtcTicks = 0L,
                LastDeliveryAttemptUtcTicks = 0L,
                DeliveryAttemptCount = deliveryAttemptCount,
                SupersededByRecordId = string.Empty,
                DurabilityPolicy = definition == null
                    ? NotificationDurabilityPolicy.SessionTransient
                    : definition.DurabilityPolicy,
                PrivacyClass = definition == null
                    ? NotificationPrivacyClass.PublicGameplay
                    : definition.PrivacyClass,
                RequiresAcknowledgement = definition != null &&
                                          definition.AcknowledgementPolicy ==
                                          NotificationAcknowledgementPolicy.Required
            };
        }
    }

    public sealed class InMemoryNotificationDurableStore : INotificationDurableStore
    {
        private readonly List<NotificationDurableRecord> _records =
            new List<NotificationDurableRecord>();
        private int _nextId;

        public bool IsAvailable { get; set; } = true;

        public bool FailNextCommit { get; set; }

        public bool FailNextUpdate { get; set; }

        public IReadOnlyList<NotificationDurableRecord> Records =>
            _records.Select(record => record.Clone()).ToList();

        public bool TryCommit(NotificationDurableRecord record, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!IsAvailable)
            {
                diagnostic = NotificationDurablePrivacy.PersistenceDiagnostic;
                return false;
            }

            if (FailNextCommit)
            {
                FailNextCommit = false;
                diagnostic = NotificationDurablePrivacy.PersistenceDiagnostic;
                return false;
            }

            if (!TrySanitize(record, out NotificationDurableRecord stored, out diagnostic))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(stored.RecordId))
            {
                stored.RecordId = "al_notification_durable_" +
                                  (++_nextId).ToString("D20", CultureInfo.InvariantCulture);
            }

            _records.Add(stored);
            Prune();
            record.RecordId = stored.RecordId;
            return true;
        }

        public bool TryUpdate(NotificationDurableRecord record, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!IsAvailable)
            {
                diagnostic = NotificationDurablePrivacy.PersistenceDiagnostic;
                return false;
            }

            if (FailNextUpdate)
            {
                FailNextUpdate = false;
                diagnostic = NotificationDurablePrivacy.PersistenceDiagnostic;
                return false;
            }

            if (!TrySanitize(record, out NotificationDurableRecord stored, out diagnostic))
            {
                return false;
            }

            int index = _records.FindIndex(item =>
                string.Equals(item.RecordId, stored.RecordId, StringComparison.Ordinal));
            if (index < 0)
            {
                diagnostic = NotificationDurablePrivacy.PersistenceDiagnostic;
                return false;
            }

            _records[index] = stored;
            Prune();
            return true;
        }

        public IReadOnlyList<NotificationDurableRecord> LoadAll()
        {
            return _records.Select(record => record.Clone()).ToList();
        }

        public NotificationDurableRecord FindByCorrelation(string correlationId, string definitionId)
        {
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                return null;
            }

            NotificationDurableRecord match = _records
                .Where(record =>
                    record.State != NotificationDeliveryState.Superseded &&
                    string.Equals(record.CorrelationId, correlationId, StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(definitionId) ||
                     string.Equals(record.DefinitionId, definitionId, StringComparison.Ordinal)))
                .OrderByDescending(record => record.OccurredAtUtcTicks)
                .FirstOrDefault();
            return match == null ? null : match.Clone();
        }

        public void Clear()
        {
            _records.Clear();
        }

        private static bool TrySanitize(
            NotificationDurableRecord record,
            out NotificationDurableRecord stored,
            out string diagnostic)
        {
            stored = null;
            diagnostic = string.Empty;
            if (record == null)
            {
                diagnostic = NotificationDurablePrivacy.PersistenceDiagnostic;
                return false;
            }

            if (record.PrivacyClass == NotificationPrivacyClass.SensitiveTechnical)
            {
                diagnostic = NotificationDurablePrivacy.PrivacyDiagnostic;
                return false;
            }

            stored = record.Clone();
            stored.Parameters = (stored.Parameters ?? new List<NotificationDurableParameter>())
                .Where(parameter =>
                    parameter != null &&
                    !string.IsNullOrWhiteSpace(parameter.Name))
                .ToList();
            return true;
        }

        private void Prune()
        {
            List<NotificationDurableRecord> prunable = _records
                .Where(record => record.CanPrune())
                .OrderBy(record => record.OccurredAtUtcTicks)
                .ThenBy(record => record.RecordId, StringComparer.Ordinal)
                .ToList();
            int overflow = prunable.Count - NotificationDurableRecord.CompletedHistoryLimit;
            for (int index = 0; index < overflow; index++)
            {
                _records.Remove(prunable[index]);
            }
        }
    }
}
