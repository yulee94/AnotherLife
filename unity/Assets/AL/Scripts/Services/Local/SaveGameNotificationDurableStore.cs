using System;
using System.Collections.Generic;
using AL.Core.Interfaces.Notifications;
using AL.Data.Runtime;

namespace AL.Services.Local
{
    public sealed class SaveGameNotificationDurableStore : INotificationDurableStore
    {
        private readonly Func<SaveGameData> _save;

        public SaveGameNotificationDurableStore(Func<SaveGameData> save)
        {
            _save = save;
        }

        public bool IsAvailable
        {
            get
            {
                try
                {
                    return _save != null && _save() != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool TryCommit(NotificationDurableRecord record, out string diagnostic)
        {
            InMemoryNotificationDurableStore memory = Snapshot();
            if (memory == null)
            {
                diagnostic = NotificationDurablePrivacy.PersistenceDiagnostic;
                return false;
            }

            if (!memory.TryCommit(record, out diagnostic))
            {
                return false;
            }

            Write(memory);
            return true;
        }

        public bool TryUpdate(NotificationDurableRecord record, out string diagnostic)
        {
            InMemoryNotificationDurableStore memory = Snapshot();
            if (memory == null)
            {
                diagnostic = NotificationDurablePrivacy.PersistenceDiagnostic;
                return false;
            }

            if (!memory.TryUpdate(record, out diagnostic))
            {
                return false;
            }

            Write(memory);
            return true;
        }

        public IReadOnlyList<NotificationDurableRecord> LoadAll()
        {
            InMemoryNotificationDurableStore memory = Snapshot();
            return memory == null
                ? Array.Empty<NotificationDurableRecord>()
                : memory.LoadAll();
        }

        public NotificationDurableRecord FindByCorrelation(string correlationId, string definitionId)
        {
            InMemoryNotificationDurableStore memory = Snapshot();
            return memory == null
                ? null
                : memory.FindByCorrelation(correlationId, definitionId);
        }

        public void Clear()
        {
            SaveGameData save = LoadSave();
            if (save == null)
            {
                return;
            }

            save.NotificationHistory = null;
        }

        private SaveGameData LoadSave()
        {
            try
            {
                return _save == null ? null : _save();
            }
            catch
            {
                return null;
            }
        }

        private InMemoryNotificationDurableStore Snapshot()
        {
            SaveGameData save = LoadSave();
            if (save == null)
            {
                return null;
            }

            var memory = new InMemoryNotificationDurableStore();
            NotificationHistoryPersistentState history = save.NotificationHistory;
            if (history == null || history.Version == 0)
            {
                return memory;
            }

            Append(memory, history.Outbox);
            Append(memory, history.Records);
            return memory;
        }

        private static void Append(
            InMemoryNotificationDurableStore memory,
            List<NotificationHistoryRecord> rows)
        {
            if (rows == null)
            {
                return;
            }

            for (int index = 0; index < rows.Count; index++)
            {
                NotificationDurableRecord record = FromRow(rows[index]);
                if (record == null)
                {
                    continue;
                }

                memory.TryCommit(record, out _);
            }
        }

        private void Write(InMemoryNotificationDurableStore memory)
        {
            SaveGameData save = LoadSave();
            if (save == null)
            {
                return;
            }

            var history = new NotificationHistoryPersistentState
            {
                Version = NotificationHistoryPersistentState.CurrentVersion,
                Records = new List<NotificationHistoryRecord>(),
                Outbox = new List<NotificationHistoryRecord>()
            };
            IReadOnlyList<NotificationDurableRecord> records = memory.LoadAll();
            for (int index = 0; index < records.Count; index++)
            {
                NotificationDurableRecord record = records[index];
                NotificationHistoryRecord row = ToRow(record);
                if (NotificationDurableRecord.IsCompleted(record.State))
                {
                    history.Records.Add(row);
                }
                else
                {
                    history.Outbox.Add(row);
                }
            }

            save.NotificationHistory = history;
        }

        private static NotificationDurableRecord FromRow(NotificationHistoryRecord row)
        {
            if (row == null)
            {
                return null;
            }

            var parameters = new List<NotificationDurableParameter>();
            if (row.Parameters != null)
            {
                for (int index = 0; index < row.Parameters.Count; index++)
                {
                    NotificationHistoryParameterRecord parameter = row.Parameters[index];
                    if (parameter == null)
                    {
                        continue;
                    }

                    parameters.Add(
                        new NotificationDurableParameter
                        {
                            Name = parameter.Name ?? string.Empty,
                            Kind = (NotificationParameterValueKind)parameter.Kind,
                            TextValue = parameter.TextValue ?? string.Empty
                        });
                }
            }

            return new NotificationDurableRecord
            {
                RecordId = row.RecordId ?? string.Empty,
                NotificationSchemaVersion = row.NotificationSchemaVersion,
                DefinitionId = row.DefinitionId ?? string.Empty,
                DefinitionVersion = row.DefinitionVersion,
                SourceSystemId = row.SourceSystemId ?? string.Empty,
                CorrelationId = row.CorrelationId ?? string.Empty,
                OccurredAtUtcTicks = row.OccurredAtUtcTicks,
                Parameters = parameters,
                State = (NotificationDeliveryState)row.State,
                AcknowledgedAtUtcTicks = row.AcknowledgedAtUtcTicks,
                DismissedAtUtcTicks = row.DismissedAtUtcTicks,
                ExpiresAtUtcTicks = row.ExpiresAtUtcTicks,
                LastDeliveryAttemptUtcTicks = row.LastDeliveryAttemptUtcTicks,
                DeliveryAttemptCount = row.DeliveryAttemptCount,
                SupersededByRecordId = row.SupersededByRecordId ?? string.Empty,
                DurabilityPolicy = (NotificationDurabilityPolicy)row.DurabilityPolicy,
                PrivacyClass = (NotificationPrivacyClass)row.PrivacyClass,
                RequiresAcknowledgement = row.RequiresAcknowledgement
            };
        }

        private static NotificationHistoryRecord ToRow(NotificationDurableRecord record)
        {
            var parameters = new List<NotificationHistoryParameterRecord>();
            if (record.Parameters != null)
            {
                for (int index = 0; index < record.Parameters.Count; index++)
                {
                    NotificationDurableParameter parameter = record.Parameters[index];
                    if (parameter == null)
                    {
                        continue;
                    }

                    parameters.Add(
                        new NotificationHistoryParameterRecord
                        {
                            Name = parameter.Name ?? string.Empty,
                            Kind = (int)parameter.Kind,
                            TextValue = parameter.TextValue ?? string.Empty
                        });
                }
            }

            return new NotificationHistoryRecord
            {
                RecordId = record.RecordId ?? string.Empty,
                NotificationSchemaVersion = record.NotificationSchemaVersion,
                DefinitionId = record.DefinitionId ?? string.Empty,
                DefinitionVersion = record.DefinitionVersion,
                SourceSystemId = record.SourceSystemId ?? string.Empty,
                CorrelationId = record.CorrelationId ?? string.Empty,
                OccurredAtUtcTicks = record.OccurredAtUtcTicks,
                Parameters = parameters,
                State = (int)record.State,
                AcknowledgedAtUtcTicks = record.AcknowledgedAtUtcTicks,
                DismissedAtUtcTicks = record.DismissedAtUtcTicks,
                ExpiresAtUtcTicks = record.ExpiresAtUtcTicks,
                LastDeliveryAttemptUtcTicks = record.LastDeliveryAttemptUtcTicks,
                DeliveryAttemptCount = record.DeliveryAttemptCount,
                SupersededByRecordId = record.SupersededByRecordId ?? string.Empty,
                DurabilityPolicy = (int)record.DurabilityPolicy,
                PrivacyClass = (int)record.PrivacyClass,
                RequiresAcknowledgement = record.RequiresAcknowledgement
            };
        }
    }
}
