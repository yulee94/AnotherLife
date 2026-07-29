using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using AL.Core;

namespace AL.Core.Interfaces.Notifications
{
    public static class NotificationTechnicalLimits
    {
        public const int SessionCapacity = 64;
        public const int MaximumDefinitionIdUtf8Bytes = 96;
        public const int MaximumSourceSystemIdUtf8Bytes = 96;
        public const int MaximumCorrelationIdUtf8Bytes = 128;
        public const int MaximumSubjectReferenceUtf8Bytes = 128;
        public const int MaximumDiagnosticCodeUtf8Bytes = 96;
        public const int MaximumParameterCount = 32;
        public const int MaximumActionCount = 8;
        public const int MaximumSafeDisplayTextUtf8Bytes = 512;
        public const int MaximumStableValueUtf8Bytes = 256;
        public const double MaximumExpirySeconds = 604800d;
        public const int CurrentDefinitionSchemaVersion = 1;
    }

    public enum NotificationSeverity
    {
        Information = 0,
        Success = 1,
        Warning = 2,
        RecoverableError = 3,
        BlockingError = 4
    }

    public enum NotificationCategory
    {
        System = 0,
        SaveRecovery = 1,
        ContentAvailability = 2,
        Economy = 3,
        Progression = 4,
        Reward = 5,
        WorldState = 6,
        Integration = 7,
        Connectivity = 8
    }

    public enum NotificationChannel
    {
        Toast = 0,
        Banner = 1,
        Acknowledgement = 2,
        HistoryOnly = 3
    }

    public enum NotificationAcknowledgementPolicy
    {
        None = 0,
        Dismissible = 1,
        Required = 2
    }

    public enum NotificationDurabilityPolicy
    {
        SessionTransient = 0,
        SessionUntilAcknowledged = 1,
        DurableUntilAcknowledged = 2,
        DurableHistory = 3
    }

    public enum NotificationExpiryMode
    {
        None = 0,
        AfterPresentation = 1,
        AfterOccurrence = 2
    }

    public enum NotificationDeduplicationPolicy
    {
        None = 0,
        ByCorrelation = 1,
        ByCorrelationAndDefinition = 2,
        ReplaceEarlierCorrelation = 3
    }

    public enum NotificationPrivacyClass
    {
        PublicGameplay = 0,
        ProfilePrivate = 1,
        SensitiveTechnical = 2
    }

    public enum NotificationParameterValueKind
    {
        Int64 = 0,
        UInt64 = 1,
        DecimalString = 2,
        Boolean = 3,
        StableId = 4,
        LocalizationReference = 5,
        ResourceType = 6,
        RealmId = 7,
        TimestampUtc = 8,
        DurationSeconds = 9,
        SafeDisplayText = 10
    }

    public enum NotificationDefinitionResolutionStatus
    {
        Found = 0,
        UnknownId = 1,
        CatalogPending = 2,
        CatalogUnavailable = 3,
        InvalidDefinition = 4,
        UnsupportedVersion = 5
    }

    public enum NotificationEnqueueStatus
    {
        AcceptedPending = 0,
        AcceptedAlreadyPresent = 1,
        AcceptedReplacedEarlier = 2,
        RejectedServiceUnavailable = 3,
        RejectedDefinitionUnavailable = 4,
        RejectedUnsupportedDefinitionVersion = 5,
        RejectedInvalidRequest = 6,
        RejectedUnsafeParameter = 7,
        RejectedCorrelationRequired = 8,
        RejectedCorrelationConflict = 9,
        RejectedDurabilityUnavailable = 10,
        RejectedCapacity = 11,
        RejectedPresenterPolicy = 12
    }

    public enum NotificationDeliveryState
    {
        PendingPresenter = 0,
        PendingPresentation = 1,
        Presented = 2,
        Acknowledged = 3,
        Dismissed = 4,
        Expired = 5,
        Superseded = 6,
        DeliveryFailed = 7,
        PersistencePending = 8,
        PersistenceFailed = 9
    }

    public enum NotificationPresenterOfferStatus
    {
        AcceptedPendingPresentation = 0,
        RejectedUnavailable = 1,
        RejectedUnsupported = 2,
        Failed = 3
    }

    public enum NotificationPresenterRegistrationStatus
    {
        Registered = 0,
        RejectedDuplicateCapability = 1,
        RejectedInvalidPresenter = 2,
        RejectedServiceUnavailable = 3
    }

    public enum NotificationPresenterUnregistrationStatus
    {
        Unregistered = 0,
        AlreadyUnregistered = 1,
        RejectedInvalidToken = 2
    }

    public enum NotificationQueueObserverRegistrationStatus
    {
        Registered = 0,
        RejectedInvalidObserver = 1
    }

    public enum NotificationQueueObserverUnregistrationStatus
    {
        Unregistered = 0,
        AlreadyUnregistered = 1,
        RejectedInvalidToken = 2
    }

    public enum NotificationReceiptUpdateStatus
    {
        Applied = 0,
        NoChange = 1,
        RejectedNotFound = 2,
        RejectedStaleRegistration = 3,
        RejectedInvalidTransition = 4,
        RejectedPolicy = 5,
        Failed = 6
    }

    public enum NotificationActionKind
    {
        Acknowledge = 0,
        RetryOperation = 1,
        OpenApprovedRoute = 2,
        OpenRecoveryDetails = 3,
        Dismiss = 4
    }

    public enum NotificationActionStatus
    {
        Applied = 0,
        NoChange = 1,
        RejectedUnavailable = 2,
        RejectedInvalidPayload = 3,
        RejectedStaleCorrelation = 4,
        RejectedNotPresented = 5,
        RejectedNotAllowed = 6,
        Failed = 7
    }

    public sealed class NotificationExpiryPolicy
    {
        public NotificationExpiryPolicy(
            NotificationExpiryMode mode,
            double realtimeDurationSeconds,
            bool expireWhilePresenterUnavailable)
        {
            Mode = mode;
            RealtimeDurationSeconds = realtimeDurationSeconds;
            ExpireWhilePresenterUnavailable = expireWhilePresenterUnavailable;
        }

        public NotificationExpiryMode Mode { get; }
        public double RealtimeDurationSeconds { get; }
        public bool ExpireWhilePresenterUnavailable { get; }
    }

    public sealed class NotificationParameterDefinition
    {
        public NotificationParameterDefinition(
            string name,
            NotificationParameterValueKind valueKind,
            bool required,
            int maximumUtf8Bytes,
            long? minimumInt64,
            long? maximumInt64,
            ulong? minimumUInt64,
            ulong? maximumUInt64,
            bool persistable,
            NotificationPrivacyClass privacyClass)
        {
            Name = name;
            ValueKind = valueKind;
            Required = required;
            MaximumUtf8Bytes = maximumUtf8Bytes;
            MinimumInt64 = minimumInt64;
            MaximumInt64 = maximumInt64;
            MinimumUInt64 = minimumUInt64;
            MaximumUInt64 = maximumUInt64;
            Persistable = persistable;
            PrivacyClass = privacyClass;
        }

        public string Name { get; }
        public NotificationParameterValueKind ValueKind { get; }
        public bool Required { get; }
        public int MaximumUtf8Bytes { get; }
        public long? MinimumInt64 { get; }
        public long? MaximumInt64 { get; }
        public ulong? MinimumUInt64 { get; }
        public ulong? MaximumUInt64 { get; }
        public bool Persistable { get; }
        public NotificationPrivacyClass PrivacyClass { get; }
    }

    public sealed class NotificationActionDefinition
    {
        public NotificationActionDefinition(
            string actionId,
            NotificationActionKind kind,
            IEnumerable<NotificationParameterDefinition> payloadSchema,
            bool requiresPresentedNotification,
            bool acknowledgesOnApplied)
        {
            ActionId = actionId;
            Kind = kind;
            PayloadSchema = NotificationImmutable.Freeze(
                payloadSchema,
                NotificationTechnicalLimits.MaximumParameterCount);
            RequiresPresentedNotification = requiresPresentedNotification;
            AcknowledgesOnApplied = acknowledgesOnApplied;
        }

        public string ActionId { get; }
        public NotificationActionKind Kind { get; }
        public IReadOnlyList<NotificationParameterDefinition> PayloadSchema { get; }
        public bool RequiresPresentedNotification { get; }
        public bool AcknowledgesOnApplied { get; }
    }

    public sealed class NotificationDefinition
    {
        public NotificationDefinition(
            string definitionId,
            int schemaVersion,
            int contentVersion,
            NotificationSeverity severity,
            NotificationCategory category,
            NotificationChannel defaultChannel,
            IEnumerable<NotificationChannel> allowedChannels,
            NotificationAcknowledgementPolicy acknowledgementPolicy,
            NotificationDurabilityPolicy durabilityPolicy,
            NotificationExpiryPolicy expiryPolicy,
            int priority,
            NotificationDeduplicationPolicy deduplicationPolicy,
            NotificationPrivacyClass privacyClass,
            bool requiresCorrelation,
            bool allowCapacityEviction,
            IEnumerable<string> allowedSourceSystemIds,
            IEnumerable<NotificationParameterDefinition> parameterSchema,
            IEnumerable<NotificationActionDefinition> actions,
            IEnumerable<string> allowedPredecessorDefinitionIds,
            IEnumerable<string> allowedSuccessorDefinitionIds)
        {
            DefinitionId = definitionId;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Severity = severity;
            Category = category;
            DefaultChannel = defaultChannel;
            AllowedChannels = NotificationImmutable.Freeze(
                allowedChannels,
                Enum.GetValues(typeof(NotificationChannel)).Length);
            AcknowledgementPolicy = acknowledgementPolicy;
            DurabilityPolicy = durabilityPolicy;
            ExpiryPolicy = expiryPolicy;
            Priority = priority;
            DeduplicationPolicy = deduplicationPolicy;
            PrivacyClass = privacyClass;
            RequiresCorrelation = requiresCorrelation;
            AllowCapacityEviction = allowCapacityEviction;
            AllowedSourceSystemIds = NotificationImmutable.Freeze(
                allowedSourceSystemIds,
                NotificationTechnicalLimits.MaximumParameterCount);
            ParameterSchema = NotificationImmutable.Freeze(
                parameterSchema,
                NotificationTechnicalLimits.MaximumParameterCount);
            Actions = NotificationImmutable.Freeze(
                actions,
                NotificationTechnicalLimits.MaximumActionCount);
            AllowedPredecessorDefinitionIds = NotificationImmutable.Freeze(
                allowedPredecessorDefinitionIds,
                NotificationTechnicalLimits.MaximumParameterCount);
            AllowedSuccessorDefinitionIds = NotificationImmutable.Freeze(
                allowedSuccessorDefinitionIds,
                NotificationTechnicalLimits.MaximumParameterCount);
        }

        public string DefinitionId { get; }
        public int SchemaVersion { get; }
        public int ContentVersion { get; }
        public NotificationSeverity Severity { get; }
        public NotificationCategory Category { get; }
        public NotificationChannel DefaultChannel { get; }
        public IReadOnlyList<NotificationChannel> AllowedChannels { get; }
        public NotificationAcknowledgementPolicy AcknowledgementPolicy { get; }
        public NotificationDurabilityPolicy DurabilityPolicy { get; }
        public NotificationExpiryPolicy ExpiryPolicy { get; }
        public int Priority { get; }
        public NotificationDeduplicationPolicy DeduplicationPolicy { get; }
        public NotificationPrivacyClass PrivacyClass { get; }
        public bool RequiresCorrelation { get; }
        public bool AllowCapacityEviction { get; }
        public IReadOnlyList<string> AllowedSourceSystemIds { get; }
        public IReadOnlyList<NotificationParameterDefinition> ParameterSchema { get; }
        public IReadOnlyList<NotificationActionDefinition> Actions { get; }
        public IReadOnlyList<string> AllowedPredecessorDefinitionIds { get; }
        public IReadOnlyList<string> AllowedSuccessorDefinitionIds { get; }
    }

    public sealed class NotificationParameterValue
    {
        private NotificationParameterValue(
            NotificationParameterValueKind kind,
            object value,
            string canonicalValue)
        {
            Kind = kind;
            Value = value;
            CanonicalValue = canonicalValue;
        }

        public NotificationParameterValueKind Kind { get; }
        public object Value { get; }
        public string CanonicalValue { get; }

        public static NotificationParameterValue FromInt64(long value) =>
            new NotificationParameterValue(
                NotificationParameterValueKind.Int64,
                value,
                value.ToString(CultureInfo.InvariantCulture));

        public static NotificationParameterValue FromUInt64(ulong value) =>
            new NotificationParameterValue(
                NotificationParameterValueKind.UInt64,
                value,
                value.ToString(CultureInfo.InvariantCulture));

        public static NotificationParameterValue FromDecimalString(string value) =>
            FromString(NotificationParameterValueKind.DecimalString, value);

        public static NotificationParameterValue FromBoolean(bool value) =>
            new NotificationParameterValue(
                NotificationParameterValueKind.Boolean,
                value,
                value ? "true" : "false");

        public static NotificationParameterValue FromStableId(string value) =>
            FromString(NotificationParameterValueKind.StableId, value);

        public static NotificationParameterValue FromLocalizationReference(string value) =>
            FromString(NotificationParameterValueKind.LocalizationReference, value);

        public static NotificationParameterValue FromResourceType(ResourceType value) =>
            new NotificationParameterValue(
                NotificationParameterValueKind.ResourceType,
                value,
                Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));

        public static NotificationParameterValue FromRealmId(string value) =>
            FromString(NotificationParameterValueKind.RealmId, value);

        public static NotificationParameterValue FromTimestampUtc(DateTime value) =>
            new NotificationParameterValue(
                NotificationParameterValueKind.TimestampUtc,
                value,
                value.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));

        public static NotificationParameterValue FromDurationSeconds(long value) =>
            new NotificationParameterValue(
                NotificationParameterValueKind.DurationSeconds,
                value,
                value.ToString(CultureInfo.InvariantCulture));

        public static NotificationParameterValue FromSafeDisplayText(string value) =>
            FromString(NotificationParameterValueKind.SafeDisplayText, value);

        private static NotificationParameterValue FromString(
            NotificationParameterValueKind kind,
            string value)
        {
            string normalized = value == null ? null : value.Normalize(NormalizationForm.FormC);
            string canonical = normalized == null
                ? null
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(normalized));
            return new NotificationParameterValue(kind, normalized, canonical);
        }
    }

    public sealed class NotificationParameter
    {
        public NotificationParameter(string name, NotificationParameterValue value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public NotificationParameterValue Value { get; }
    }

    public sealed class NotificationRequest
    {
        public NotificationRequest(
            string definitionId,
            string sourceSystemId,
            string correlationId,
            DateTime occurredAtUtc,
            IEnumerable<NotificationParameter> parameters,
            NotificationChannel? requestedChannel,
            string subjectReference,
            string originDiagnosticCode)
        {
            DefinitionId = definitionId;
            SourceSystemId = sourceSystemId;
            CorrelationId = correlationId;
            OccurredAtUtc = occurredAtUtc;
            Parameters = NotificationImmutable.Freeze(
                parameters,
                NotificationTechnicalLimits.MaximumParameterCount);
            RequestedChannel = requestedChannel;
            SubjectReference = subjectReference;
            OriginDiagnosticCode = originDiagnosticCode;
        }

        public string DefinitionId { get; }
        public string SourceSystemId { get; }
        public string CorrelationId { get; }
        public DateTime OccurredAtUtc { get; }
        public IReadOnlyList<NotificationParameter> Parameters { get; }
        public NotificationChannel? RequestedChannel { get; }
        public string SubjectReference { get; }
        public string OriginDiagnosticCode { get; }
    }

    public sealed class NotificationEnqueueResult
    {
        public NotificationEnqueueResult(
            NotificationEnqueueStatus status,
            string definitionId,
            string correlationId,
            string notificationInstanceId,
            long sessionSequence,
            string existingInstanceId,
            string diagnosticCode,
            bool queueChanged)
        {
            Status = status;
            DefinitionId = definitionId;
            CorrelationId = correlationId;
            NotificationInstanceId = notificationInstanceId;
            SessionSequence = sessionSequence;
            ExistingInstanceId = existingInstanceId;
            DiagnosticCode = diagnosticCode;
            QueueChanged = queueChanged;
        }

        public NotificationEnqueueStatus Status { get; }
        public string DefinitionId { get; }
        public string CorrelationId { get; }
        public string NotificationInstanceId { get; }
        public long SessionSequence { get; }
        public string ExistingInstanceId { get; }
        public string DiagnosticCode { get; }
        public bool QueueChanged { get; }
    }

    public sealed class NotificationDeliveryReceipt
    {
        public NotificationDeliveryReceipt(
            string notificationInstanceId,
            NotificationDeliveryState state,
            string presenterId,
            NotificationChannel? channel,
            DateTime? presentedAtUtc,
            DateTime? completedAtUtc,
            int deliveryAttempt,
            string failureCode)
        {
            NotificationInstanceId = notificationInstanceId;
            State = state;
            PresenterId = presenterId;
            Channel = channel;
            PresentedAtUtc = presentedAtUtc;
            CompletedAtUtc = completedAtUtc;
            DeliveryAttempt = deliveryAttempt;
            FailureCode = failureCode;
        }

        public string NotificationInstanceId { get; }
        public NotificationDeliveryState State { get; }
        public string PresenterId { get; }
        public NotificationChannel? Channel { get; }
        public DateTime? PresentedAtUtc { get; }
        public DateTime? CompletedAtUtc { get; }
        public int DeliveryAttempt { get; }
        public string FailureCode { get; }
    }

    public sealed class NotificationQueueRecordSnapshot
    {
        public NotificationQueueRecordSnapshot(
            string notificationInstanceId,
            long sessionSequence,
            NotificationDefinition definition,
            NotificationRequest request,
            NotificationChannel channel,
            NotificationDeliveryReceipt receipt)
        {
            NotificationInstanceId = notificationInstanceId;
            SessionSequence = sessionSequence;
            Definition = definition;
            Request = request;
            Channel = channel;
            Receipt = receipt;
        }

        public string NotificationInstanceId { get; }
        public long SessionSequence { get; }
        public NotificationDefinition Definition { get; }
        public NotificationRequest Request { get; }
        public NotificationChannel Channel { get; }
        public NotificationDeliveryReceipt Receipt { get; }
    }

    public sealed class NotificationQueueSnapshot
    {
        public NotificationQueueSnapshot(
            long revision,
            IEnumerable<NotificationQueueRecordSnapshot> records)
        {
            Revision = revision;
            Records = NotificationImmutable.Freeze(records, NotificationTechnicalLimits.SessionCapacity);
        }

        public long Revision { get; }
        public IReadOnlyList<NotificationQueueRecordSnapshot> Records { get; }
    }

    public interface INotificationQueueObserver
    {
        void OnQueueChanged(NotificationQueueSnapshot snapshot);
    }

    public sealed class NotificationQueueObserverRegistrationToken
    {
        public NotificationQueueObserverRegistrationToken(long generation)
        {
            Generation = generation;
        }

        public long Generation { get; }
    }

    public sealed class NotificationQueueObserverRegistrationResult
    {
        public NotificationQueueObserverRegistrationResult(
            NotificationQueueObserverRegistrationStatus status,
            NotificationQueueObserverRegistrationToken token)
        {
            Status = status;
            Token = token;
        }

        public NotificationQueueObserverRegistrationStatus Status { get; }
        public NotificationQueueObserverRegistrationToken Token { get; }
    }

    public sealed class NotificationDefinitionResolution
    {
        public NotificationDefinitionResolution(
            NotificationDefinitionResolutionStatus status,
            NotificationDefinition definition,
            string diagnosticCode)
        {
            Status = status;
            Definition = definition;
            DiagnosticCode = diagnosticCode;
        }

        public NotificationDefinitionResolutionStatus Status { get; }
        public NotificationDefinition Definition { get; }
        public string DiagnosticCode { get; }
    }

    public interface INotificationDefinitionResolver
    {
        NotificationDefinitionResolution Resolve(string definitionId);
    }

    public interface INotificationClock
    {
        DateTime UtcNow { get; }
        double RealtimeSeconds { get; }
    }

    public sealed class NotificationDiagnostic
    {
        public NotificationDiagnostic(
            string code,
            NotificationSeverity severity,
            string definitionId,
            string correlationId)
        {
            Code = code;
            Severity = severity;
            DefinitionId = definitionId;
            CorrelationId = correlationId;
        }

        public string Code { get; }
        public NotificationSeverity Severity { get; }
        public string DefinitionId { get; }
        public string CorrelationId { get; }
    }

    public interface INotificationDiagnosticSink
    {
        void Record(NotificationDiagnostic diagnostic);
        void RecordLegacyRaw(string escapedTechnicalText, bool isError);
    }

    public sealed class NotificationPresenterCapabilities
    {
        public NotificationPresenterCapabilities(IEnumerable<NotificationChannel> channels)
        {
            Channels = NotificationImmutable.Freeze(
                channels,
                Enum.GetValues(typeof(NotificationChannel)).Length);
        }

        public IReadOnlyList<NotificationChannel> Channels { get; }
    }

    public sealed class NotificationPresentationOffer
    {
        public NotificationPresentationOffer(NotificationQueueRecordSnapshot record)
        {
            Record = record;
        }

        public NotificationQueueRecordSnapshot Record { get; }
    }

    public sealed class NotificationPresenterOfferResult
    {
        public NotificationPresenterOfferResult(
            NotificationPresenterOfferStatus status,
            string failureCode)
        {
            Status = status;
            FailureCode = failureCode;
        }

        public NotificationPresenterOfferStatus Status { get; }
        public string FailureCode { get; }
    }

    public interface INotificationPresenter
    {
        string PresenterId { get; }
        NotificationPresenterOfferResult Offer(NotificationPresentationOffer offer);
    }

    public sealed class NotificationPresenterRegistrationToken
    {
        public NotificationPresenterRegistrationToken(long generation, string presenterId)
        {
            Generation = generation;
            PresenterId = presenterId;
        }

        public long Generation { get; }
        public string PresenterId { get; }
    }

    public sealed class NotificationPresenterRegistrationResult
    {
        public NotificationPresenterRegistrationResult(
            NotificationPresenterRegistrationStatus status,
            NotificationPresenterRegistrationToken token,
            string diagnosticCode)
        {
            Status = status;
            Token = token;
            DiagnosticCode = diagnosticCode;
        }

        public NotificationPresenterRegistrationStatus Status { get; }
        public NotificationPresenterRegistrationToken Token { get; }
        public string DiagnosticCode { get; }
    }

    public sealed class NotificationReceiptUpdateResult
    {
        public NotificationReceiptUpdateResult(
            NotificationReceiptUpdateStatus status,
            NotificationDeliveryReceipt receipt,
            string diagnosticCode)
        {
            Status = status;
            Receipt = receipt;
            DiagnosticCode = diagnosticCode;
        }

        public NotificationReceiptUpdateStatus Status { get; }
        public NotificationDeliveryReceipt Receipt { get; }
        public string DiagnosticCode { get; }
    }

    public sealed class NotificationActionInvocation
    {
        public NotificationActionInvocation(
            string actionId,
            string correlationId,
            IEnumerable<NotificationParameter> payload)
        {
            ActionId = actionId;
            CorrelationId = correlationId;
            Payload = NotificationImmutable.Freeze(
                payload,
                NotificationTechnicalLimits.MaximumParameterCount);
        }

        public string ActionId { get; }
        public string CorrelationId { get; }
        public IReadOnlyList<NotificationParameter> Payload { get; }
    }

    public sealed class NotificationActionResult
    {
        public NotificationActionResult(NotificationActionStatus status, string diagnosticCode)
        {
            Status = status;
            DiagnosticCode = diagnosticCode;
        }

        public NotificationActionStatus Status { get; }
        public string DiagnosticCode { get; }
    }

    public interface INotificationActionRegistry
    {
        NotificationActionResult Invoke(
            NotificationQueueRecordSnapshot record,
            NotificationActionDefinition action,
            NotificationActionInvocation invocation);
    }

    internal static class NotificationImmutable
    {
        public static IReadOnlyList<T> Freeze<T>(IEnumerable<T> source, int maximumCount)
        {
            if (source == null)
            {
                return new ReadOnlyCollection<T>(new T[0]);
            }

            T[] values = source.Take(maximumCount + 1).ToArray();
            return new ReadOnlyCollection<T>(values);
        }
    }
}
