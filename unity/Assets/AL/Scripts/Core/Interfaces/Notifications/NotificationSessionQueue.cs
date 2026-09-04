using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AL.Core.Interfaces.Notifications
{
    public sealed class NotificationSessionQueue
    {
        private const string DefinitionDiagnostic = "AL-NTF-DEFINITION";
        private const string ParameterDiagnostic = "AL-NTF-PARAMETER";
        private const string CorrelationConflictDiagnostic = "AL-NTF-CORRELATION-CONFLICT";
        private const string CapacityDiagnostic = "AL-NTF-CAPACITY";
        private const string PresenterDuplicateDiagnostic = "AL-NTF-PRESENTER-DUPLICATE";
        private const string PresenterFailedDiagnostic = "AL-NTF-PRESENTER-FAILED";
        private const string ObserverFailedDiagnostic = "AL-NTF-OBSERVER-FAILED";
        private const string ActionDiagnostic = "AL-NTF-ACTION";
        private const string ContentUnavailableDiagnostic = "AL-NTF-CONTENT-UNAVAILABLE";

        private readonly object _gate = new object();
        private readonly INotificationDefinitionResolver _definitionResolver;
        private readonly INotificationLocalizationReferenceAuthority _localizationReferenceAuthority;
        private readonly INotificationClock _clock;
        private readonly INotificationActionRegistry _actionRegistry;
        private readonly INotificationDiagnosticSink _diagnosticSink;
        private readonly INotificationDurableStore _durableStore;
        private readonly List<Record> _records = new List<Record>();
        private readonly Dictionary<NotificationChannel, Registration> _presenters =
            new Dictionary<NotificationChannel, Registration>();
        private readonly Dictionary<long, Registration> _registrations =
            new Dictionary<long, Registration>();
        private readonly Dictionary<long, ObserverRegistration> _observers =
            new Dictionary<long, ObserverRegistration>();
        private readonly HashSet<string> _emittedDiagnosticKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<NotificationChannel> _offersInProgress =
            new HashSet<NotificationChannel>();
        private long _nextSequence;
        private long _nextPresenterGeneration;
        private long _nextObserverGeneration;
        private long _revision;

        public NotificationSessionQueue(
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

        public NotificationSessionQueue(
            INotificationDefinitionResolver definitionResolver,
            INotificationLocalizationReferenceAuthority localizationReferenceAuthority,
            INotificationClock clock,
            INotificationActionRegistry actionRegistry,
            INotificationDiagnosticSink diagnosticSink,
            INotificationDurableStore durableStore)
        {
            _definitionResolver = definitionResolver;
            _localizationReferenceAuthority = localizationReferenceAuthority;
            _clock = clock;
            _actionRegistry = actionRegistry;
            _diagnosticSink = diagnosticSink;
            _durableStore = durableStore;
            HydrateDurableRecords();
        }

        public NotificationEnqueueResult Enqueue(NotificationRequest request)
        {
            lock (_gate)
            {
                long previousRevision = _revision;
                if (_definitionResolver == null || _clock == null)
                {
                    return Rejected(
                        NotificationEnqueueStatus.RejectedServiceUnavailable,
                        request,
                        DefinitionDiagnostic);
                }

                NotificationDefinitionResolution resolution;
                try
                {
                    resolution = _definitionResolver.Resolve(request?.DefinitionId);
                }
                catch
                {
                    return Rejected(
                        NotificationEnqueueStatus.RejectedServiceUnavailable,
                        request,
                        DefinitionDiagnostic);
                }

                if (resolution == null)
                {
                    return Rejected(
                        NotificationEnqueueStatus.RejectedDefinitionUnavailable,
                        request,
                        DefinitionDiagnostic);
                }

                if (resolution.Status != NotificationDefinitionResolutionStatus.Found)
                {
                    NotificationEnqueueStatus status =
                        resolution.Status == NotificationDefinitionResolutionStatus.UnsupportedVersion
                            ? NotificationEnqueueStatus.RejectedUnsupportedDefinitionVersion
                            : NotificationEnqueueStatus.RejectedDefinitionUnavailable;
                    return Rejected(
                        status,
                        request,
                        string.IsNullOrWhiteSpace(resolution.DiagnosticCode)
                            ? DefinitionDiagnostic
                            : resolution.DiagnosticCode);
                }

                NotificationDefinition definition = resolution.Definition;
                NotificationValidationResult definitionValidation =
                    NotificationValidation.ValidateDefinition(definition);
                if (!definitionValidation.IsValid)
                {
                    return Rejected(
                        NotificationEnqueueStatus.RejectedDefinitionUnavailable,
                        request,
                        definitionValidation.DiagnosticCode);
                }

                if (NotificationDurablePrivacy.IsDurable(definition.DurabilityPolicy))
                {
                    if (!DurableStoreAvailable())
                    {
                        return Rejected(
                            NotificationEnqueueStatus.RejectedDurabilityUnavailable,
                            request,
                            NotificationDurablePrivacy.PersistenceDiagnostic);
                    }

                    NotificationDurableRecord existingDurable = FindDurableCorrelation(
                        request.CorrelationId,
                        definition.DefinitionId);
                    if (existingDurable != null)
                    {
                        return new NotificationEnqueueResult(
                            NotificationEnqueueStatus.AcceptedAlreadyPresent,
                            definition.DefinitionId,
                            request.CorrelationId,
                            existingDurable.RecordId,
                            0L,
                            existingDurable.RecordId,
                            null,
                            false);
                    }
                }

                NotificationValidationResult requestValidation =
                    NotificationValidation.ValidateRequest(definition, request, _clock.UtcNow);
                if (!requestValidation.IsValid)
                {
                    NotificationEnqueueStatus status;
                    if (string.Equals(
                            requestValidation.DiagnosticCode,
                            "AL-NTF-CORRELATION-REQUIRED",
                            StringComparison.Ordinal))
                    {
                        status = NotificationEnqueueStatus.RejectedCorrelationRequired;
                    }
                    else
                    {
                        status = requestValidation.UnsafeParameter
                            ? NotificationEnqueueStatus.RejectedUnsafeParameter
                            : NotificationEnqueueStatus.RejectedInvalidRequest;
                    }

                    return Rejected(status, request, requestValidation.DiagnosticCode);
                }

                NotificationEnqueueResult localizationRejection =
                    ValidateLocalizationReferences(definition, request);
                if (localizationRejection != null)
                {
                    return localizationRejection;
                }

                NotificationChannel channel =
                    request.RequestedChannel ?? definition.DefaultChannel;
                Record correlationMatch = FindCorrelationMatch(
                    request.CorrelationId,
                    definition);
                string replacedInstanceId = null;
                if (correlationMatch != null &&
                    definition.DeduplicationPolicy != NotificationDeduplicationPolicy.None)
                {
                    if (string.Equals(
                            correlationMatch.Definition.DefinitionId,
                            definition.DefinitionId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            correlationMatch.Request.SourceSystemId,
                            request.SourceSystemId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            correlationMatch.CanonicalPayload,
                            requestValidation.CanonicalPayload,
                            StringComparison.Ordinal))
                    {
                        return new NotificationEnqueueResult(
                            NotificationEnqueueStatus.AcceptedAlreadyPresent,
                            definition.DefinitionId,
                            request.CorrelationId,
                            correlationMatch.InstanceId,
                            correlationMatch.Sequence,
                            correlationMatch.InstanceId,
                            null,
                            false);
                    }

                    if (CanReplace(correlationMatch, definition))
                    {
                        replacedInstanceId = correlationMatch.InstanceId;
                        Complete(
                            correlationMatch,
                            NotificationDeliveryState.Superseded,
                            null);
                    }
                    else
                    {
                        EmitOnce(
                            CorrelationConflictDiagnostic,
                            NotificationSeverity.RecoverableError,
                            definition.DefinitionId,
                            request.CorrelationId);
                        return Rejected(
                            NotificationEnqueueStatus.RejectedCorrelationConflict,
                            request,
                            CorrelationConflictDiagnostic);
                    }
                }

                RefreshCore();
                if (!EnsureCapacity())
                {
                    EmitOnce(
                        CapacityDiagnostic,
                        NotificationSeverity.BlockingError,
                        definition.DefinitionId,
                        request.CorrelationId);
                    NotifyObserversIfChanged(previousRevision);
                    return Rejected(
                        NotificationEnqueueStatus.RejectedCapacity,
                        request,
                        CapacityDiagnostic);
                }

                long sequence = checked(++_nextSequence);
                string instanceId = "al_notification_session_" +
                                    sequence.ToString("D20", CultureInfo.InvariantCulture);
                if (NotificationDurablePrivacy.IsDurable(definition.DurabilityPolicy))
                {
                    NotificationDurableRecord durable = NotificationDurablePrivacy.FromQueue(
                        null,
                        definition,
                        request,
                        NotificationDeliveryState.PendingPresenter,
                        null,
                        null,
                        0);
                    if (!TryCommitDurable(durable, out string persistDiagnostic))
                    {
                        return Rejected(
                            NotificationEnqueueStatus.RejectedDurabilityUnavailable,
                            request,
                            string.IsNullOrWhiteSpace(persistDiagnostic)
                                ? NotificationDurablePrivacy.PersistenceDiagnostic
                                : persistDiagnostic);
                    }

                    instanceId = durable.RecordId;
                }
                var record = new Record(
                    instanceId,
                    sequence,
                    definition,
                    request,
                    channel,
                    requestValidation.CanonicalPayload,
                    _clock.UtcNow,
                    _clock.RealtimeSeconds);
                _records.Add(record);
                _revision++;
                OfferNext(channel);
                NotifyObserversIfChanged(previousRevision);

                return new NotificationEnqueueResult(
                    replacedInstanceId == null
                        ? NotificationEnqueueStatus.AcceptedPending
                        : NotificationEnqueueStatus.AcceptedReplacedEarlier,
                    definition.DefinitionId,
                    request.CorrelationId,
                    instanceId,
                    sequence,
                    replacedInstanceId,
                    null,
                    true);
            }
        }

        private NotificationEnqueueResult ValidateLocalizationReferences(
            NotificationDefinition definition,
            NotificationRequest request)
        {
            if (!definition.ParameterSchema.Any(item =>
                    item.ValueKind == NotificationParameterValueKind.LocalizationReference))
            {
                return null;
            }

            bool available;
            try
            {
                available = _localizationReferenceAuthority != null &&
                            _localizationReferenceAuthority.IsAvailable;
            }
            catch
            {
                available = false;
            }

            if (!available)
            {
                return Rejected(
                    NotificationEnqueueStatus.RejectedDefinitionUnavailable,
                    request,
                    ContentUnavailableDiagnostic);
            }

            for (int index = 0; index < request.Parameters.Count; index++)
            {
                NotificationParameter parameter = request.Parameters[index];
                if (parameter.Value.Kind !=
                    NotificationParameterValueKind.LocalizationReference)
                {
                    continue;
                }

                bool contains;
                try
                {
                    contains = _localizationReferenceAuthority.Contains(
                        (string)parameter.Value.Value);
                }
                catch
                {
                    return Rejected(
                        NotificationEnqueueStatus.RejectedDefinitionUnavailable,
                        request,
                        ContentUnavailableDiagnostic);
                }

                if (!contains)
                {
                    return Rejected(
                        NotificationEnqueueStatus.RejectedUnsafeParameter,
                        request,
                        ParameterDiagnostic);
                }
            }

            return null;
        }

        public NotificationPresenterRegistrationResult RegisterPresenter(
            INotificationPresenter presenter,
            NotificationPresenterCapabilities capabilities)
        {
            lock (_gate)
            {
                if (presenter == null ||
                    capabilities == null ||
                    capabilities.Channels == null ||
                    capabilities.Channels.Count == 0 ||
                    capabilities.Channels.Count >
                    Enum.GetValues(typeof(NotificationChannel)).Length ||
                    capabilities.Channels.Any(channel =>
                        !Enum.IsDefined(typeof(NotificationChannel), channel)) ||
                    capabilities.Channels.Distinct().Count() != capabilities.Channels.Count ||
                    !IsSafePresenterId(presenter.PresenterId))
                {
                    return new NotificationPresenterRegistrationResult(
                        NotificationPresenterRegistrationStatus.RejectedInvalidPresenter,
                        null,
                        PresenterFailedDiagnostic);
                }

                if (capabilities.Channels.Any(channel => _presenters.ContainsKey(channel)))
                {
                    EmitOnce(
                        PresenterDuplicateDiagnostic,
                        NotificationSeverity.RecoverableError,
                        null,
                        presenter.PresenterId);
                    return new NotificationPresenterRegistrationResult(
                        NotificationPresenterRegistrationStatus.RejectedDuplicateCapability,
                        null,
                        PresenterDuplicateDiagnostic);
                }

                long generation = checked(++_nextPresenterGeneration);
                var token = new NotificationPresenterRegistrationToken(
                    generation,
                    presenter.PresenterId);
                var registration = new Registration(token, presenter, capabilities);
                _registrations.Add(generation, registration);
                for (int index = 0; index < capabilities.Channels.Count; index++)
                {
                    _presenters.Add(capabilities.Channels[index], registration);
                }

                _revision++;
                for (int index = 0; index < capabilities.Channels.Count; index++)
                {
                    OfferNext(capabilities.Channels[index]);
                }

                NotifyObservers();
                return new NotificationPresenterRegistrationResult(
                    NotificationPresenterRegistrationStatus.Registered,
                    token,
                    null);
            }
        }

        public NotificationPresenterUnregistrationStatus UnregisterPresenter(
            NotificationPresenterRegistrationToken token)
        {
            lock (_gate)
            {
                if (token == null ||
                    !_registrations.TryGetValue(token.Generation, out Registration registration) ||
                    !string.Equals(
                        registration.Token.PresenterId,
                        token.PresenterId,
                        StringComparison.Ordinal))
                {
                    return NotificationPresenterUnregistrationStatus.RejectedInvalidToken;
                }

                if (!registration.Active)
                {
                    return NotificationPresenterUnregistrationStatus.AlreadyUnregistered;
                }

                registration.Active = false;
                for (int index = 0; index < registration.Capabilities.Channels.Count; index++)
                {
                    NotificationChannel channel = registration.Capabilities.Channels[index];
                    if (_presenters.TryGetValue(channel, out Registration active) &&
                        ReferenceEquals(active, registration))
                    {
                        _presenters.Remove(channel);
                    }
                }

                DateTime now = _clock?.UtcNow ?? DateTime.UtcNow;
                for (int index = 0; index < _records.Count; index++)
                {
                    Record record = _records[index];
                    if (!IsComplete(record.State) &&
                        record.PresenterGeneration == token.Generation)
                    {
                        record.State = NotificationDeliveryState.DeliveryFailed;
                        record.PresenterId = null;
                        record.PresenterGeneration = null;
                        record.FailureCode = "AL-NTF-PRESENTER-DETACHED";
                        record.LastTransitionAtUtc = now;
                    }
                }

                _revision++;
                NotifyObservers();
                return NotificationPresenterUnregistrationStatus.Unregistered;
            }
        }

        public NotificationReceiptUpdateResult ConfirmPresented(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId)
        {
            lock (_gate)
            {
                if (!TryGetPresenterRecord(
                        token,
                        notificationInstanceId,
                        out Record record,
                        out NotificationReceiptUpdateResult rejected))
                {
                    return rejected;
                }

                if (record.State == NotificationDeliveryState.Presented)
                {
                    return Updated(NotificationReceiptUpdateStatus.NoChange, record, null);
                }

                if (record.State != NotificationDeliveryState.PendingPresentation)
                {
                    return Updated(
                        NotificationReceiptUpdateStatus.RejectedInvalidTransition,
                        record,
                        PresenterFailedDiagnostic);
                }

                record.State = NotificationDeliveryState.Presented;
                record.PresentedAtUtc = _clock.UtcNow;
                record.PresentedAtRealtime = _clock.RealtimeSeconds;
                record.LastTransitionAtUtc = _clock.UtcNow;
                record.FailureCode = null;
                _revision++;
                NotifyObservers();
                return Updated(NotificationReceiptUpdateStatus.Applied, record, null);
            }
        }

        public NotificationReceiptUpdateResult ReportDeliveryFailure(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId,
            string failureCode)
        {
            lock (_gate)
            {
                if (!TryGetPresenterRecord(
                        token,
                        notificationInstanceId,
                        out Record record,
                        out NotificationReceiptUpdateResult rejected))
                {
                    return rejected;
                }

                if (IsComplete(record.State))
                {
                    return Updated(
                        NotificationReceiptUpdateStatus.RejectedInvalidTransition,
                        record,
                        PresenterFailedDiagnostic);
                }

                record.State = NotificationDeliveryState.DeliveryFailed;
                record.FailureCode = IsSafeFailureCode(failureCode)
                    ? failureCode
                    : PresenterFailedDiagnostic;
                record.PresenterId = null;
                record.PresenterGeneration = null;
                record.LastTransitionAtUtc = _clock.UtcNow;
                _revision++;
                EmitOnce(
                    PresenterFailedDiagnostic,
                    NotificationSeverity.RecoverableError,
                    record.Definition.DefinitionId,
                    record.Request.CorrelationId);
                NotifyObservers();
                return Updated(NotificationReceiptUpdateStatus.Applied, record, null);
            }
        }

        public NotificationReceiptUpdateResult Acknowledge(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId)
        {
            lock (_gate)
            {
                if (!TryGetPresenterRecord(
                        token,
                        notificationInstanceId,
                        out Record record,
                        out NotificationReceiptUpdateResult rejected))
                {
                    return rejected;
                }

                if (record.State == NotificationDeliveryState.Acknowledged)
                {
                    return Updated(NotificationReceiptUpdateStatus.NoChange, record, null);
                }

                if (record.State != NotificationDeliveryState.Presented ||
                    record.Definition.AcknowledgementPolicy ==
                    NotificationAcknowledgementPolicy.None)
                {
                    return Updated(
                        NotificationReceiptUpdateStatus.RejectedPolicy,
                        record,
                        PresenterFailedDiagnostic);
                }

                if (!TryPersistDurableTransition(
                        record,
                        NotificationDeliveryState.Acknowledged,
                        _clock.UtcNow,
                        null))
                {
                    return Updated(
                        NotificationReceiptUpdateStatus.Failed,
                        record,
                        NotificationDurablePrivacy.PersistenceDiagnostic);
                }

                Complete(record, NotificationDeliveryState.Acknowledged, null);
                OfferNext(record.Channel);
                NotifyObservers();
                return Updated(NotificationReceiptUpdateStatus.Applied, record, null);
            }
        }

        public NotificationReceiptUpdateResult Dismiss(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId)
        {
            lock (_gate)
            {
                if (!TryGetPresenterRecord(
                        token,
                        notificationInstanceId,
                        out Record record,
                        out NotificationReceiptUpdateResult rejected))
                {
                    return rejected;
                }

                if (record.State == NotificationDeliveryState.Dismissed)
                {
                    return Updated(NotificationReceiptUpdateStatus.NoChange, record, null);
                }

                if (record.State != NotificationDeliveryState.Presented ||
                    record.Definition.AcknowledgementPolicy ==
                    NotificationAcknowledgementPolicy.Required)
                {
                    return Updated(
                        NotificationReceiptUpdateStatus.RejectedPolicy,
                        record,
                        PresenterFailedDiagnostic);
                }

                Complete(record, NotificationDeliveryState.Dismissed, null);
                OfferNext(record.Channel);
                NotifyObservers();
                return Updated(NotificationReceiptUpdateStatus.Applied, record, null);
            }
        }

        public NotificationActionResult InvokeAction(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId,
            NotificationActionInvocation invocation)
        {
            lock (_gate)
            {
                if (!TryGetPresenterRecord(
                        token,
                        notificationInstanceId,
                        out Record record,
                        out _))
                {
                    return new NotificationActionResult(
                        NotificationActionStatus.RejectedUnavailable,
                        ActionDiagnostic);
                }

                NotificationActionDefinition action = record.Definition.Actions.FirstOrDefault(
                    item => string.Equals(
                        item.ActionId,
                        invocation?.ActionId,
                        StringComparison.Ordinal));
                if (action == null)
                {
                    return new NotificationActionResult(
                        NotificationActionStatus.RejectedNotAllowed,
                        ActionDiagnostic);
                }

                if (!string.Equals(
                        record.Request.CorrelationId,
                        invocation.CorrelationId,
                        StringComparison.Ordinal))
                {
                    return new NotificationActionResult(
                        NotificationActionStatus.RejectedStaleCorrelation,
                        ActionDiagnostic);
                }

                NotificationValidationResult payload =
                    NotificationValidation.ValidateActionPayload(action, invocation);
                if (!payload.IsValid)
                {
                    return new NotificationActionResult(
                        NotificationActionStatus.RejectedInvalidPayload,
                        ActionDiagnostic);
                }

                string invocationKey = action.ActionId + "|" + payload.CanonicalPayload;
                if (record.AppliedActionKeys.Contains(invocationKey))
                {
                    return new NotificationActionResult(
                        NotificationActionStatus.NoChange,
                        null);
                }

                if (action.RequiresPresentedNotification &&
                    record.State != NotificationDeliveryState.Presented)
                {
                    return new NotificationActionResult(
                        NotificationActionStatus.RejectedNotPresented,
                        ActionDiagnostic);
                }

                NotificationActionResult result;
                try
                {
                    result = _actionRegistry?.Invoke(
                        Snapshot(record),
                        action,
                        invocation);
                }
                catch
                {
                    result = new NotificationActionResult(
                        NotificationActionStatus.Failed,
                        ActionDiagnostic);
                }

                if (result == null ||
                    !Enum.IsDefined(typeof(NotificationActionStatus), result.Status))
                {
                    result = new NotificationActionResult(
                        NotificationActionStatus.Failed,
                        ActionDiagnostic);
                }

                if (result.Status == NotificationActionStatus.Applied)
                {
                    record.AppliedActionKeys.Add(invocationKey);
                    if (action.AcknowledgesOnApplied &&
                        record.Definition.AcknowledgementPolicy !=
                        NotificationAcknowledgementPolicy.None)
                    {
                        Complete(record, NotificationDeliveryState.Acknowledged, null);
                        OfferNext(record.Channel);
                    }

                    _revision++;
                }
                else if (result.Status == NotificationActionStatus.Failed)
                {
                    EmitOnce(
                        ActionDiagnostic,
                        NotificationSeverity.RecoverableError,
                        record.Definition.DefinitionId,
                        record.Request.CorrelationId);
                }

                if (result.Status == NotificationActionStatus.Applied)
                {
                    NotifyObservers();
                }

                return result;
            }
        }

        public NotificationQueueObserverRegistrationResult RegisterObserver(
            INotificationQueueObserver observer)
        {
            lock (_gate)
            {
                if (observer == null)
                {
                    return new NotificationQueueObserverRegistrationResult(
                        NotificationQueueObserverRegistrationStatus.RejectedInvalidObserver,
                        null);
                }

                long generation = checked(++_nextObserverGeneration);
                var token = new NotificationQueueObserverRegistrationToken(generation);
                _observers.Add(
                    generation,
                    new ObserverRegistration(token, observer));
                return new NotificationQueueObserverRegistrationResult(
                    NotificationQueueObserverRegistrationStatus.Registered,
                    token);
            }
        }

        public NotificationQueueObserverUnregistrationStatus UnregisterObserver(
            NotificationQueueObserverRegistrationToken token)
        {
            lock (_gate)
            {
                if (token == null ||
                    !_observers.TryGetValue(
                        token.Generation,
                        out ObserverRegistration registration))
                {
                    return NotificationQueueObserverUnregistrationStatus.RejectedInvalidToken;
                }

                if (!registration.Active)
                {
                    return NotificationQueueObserverUnregistrationStatus.AlreadyUnregistered;
                }

                registration.Active = false;
                return NotificationQueueObserverUnregistrationStatus.Unregistered;
            }
        }

        public NotificationQueueSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                long previousRevision = _revision;
                RefreshCore();
                if (_revision != previousRevision)
                {
                    NotifyObservers();
                }

                return CreateSnapshot();
            }
        }

        public NotificationDeliveryReceipt GetReceipt(string notificationInstanceId)
        {
            lock (_gate)
            {
                Record record = _records.FirstOrDefault(item =>
                    string.Equals(
                        item.InstanceId,
                        notificationInstanceId,
                        StringComparison.Ordinal));
                return record == null ? null : Receipt(record);
            }
        }

        public void Refresh()
        {
            lock (_gate)
            {
                long previousRevision = _revision;
                RefreshCore();
                foreach (NotificationChannel channel in Enum.GetValues(typeof(NotificationChannel)))
                {
                    OfferNext(channel);
                }

                if (_revision != previousRevision)
                {
                    NotifyObservers();
                }
            }
        }

        private bool EnsureCapacity()
        {
            if (_records.Count < NotificationTechnicalLimits.SessionCapacity)
            {
                return true;
            }

            _records.RemoveAll(record => IsComplete(record.State));
            if (_records.Count < NotificationTechnicalLimits.SessionCapacity)
            {
                _revision++;
                return true;
            }

            Record evictable = _records
                .Where(record =>
                    record.Definition.DurabilityPolicy ==
                    NotificationDurabilityPolicy.SessionTransient &&
                    record.Definition.AllowCapacityEviction &&
                    record.Definition.AcknowledgementPolicy !=
                    NotificationAcknowledgementPolicy.Required &&
                    record.Definition.Severity != NotificationSeverity.BlockingError)
                .OrderBy(record => record.Definition.Priority)
                .ThenBy(record => record.Sequence)
                .FirstOrDefault();
            if (evictable == null)
            {
                return false;
            }

            _records.Remove(evictable);
            _revision++;
            return true;
        }

        private void RefreshCore()
        {
            if (_clock == null)
            {
                return;
            }

            bool changed = false;
            for (int index = 0; index < _records.Count; index++)
            {
                Record record = _records[index];
                if (IsComplete(record.State) ||
                    record.Definition.AcknowledgementPolicy ==
                    NotificationAcknowledgementPolicy.Required)
                {
                    continue;
                }

                NotificationExpiryPolicy expiry = record.Definition.ExpiryPolicy;
                bool expired = false;
                if (expiry.Mode == NotificationExpiryMode.AfterOccurrence &&
                    (expiry.ExpireWhilePresenterUnavailable ||
                     !IsAwaitingPresenter(record.State)))
                {
                    double elapsedAtEnqueue =
                        (record.CreatedAtUtc - record.Request.OccurredAtUtc).TotalSeconds;
                    double elapsedSinceEnqueue = Math.Max(
                        0d,
                        _clock.RealtimeSeconds - record.CreatedAtRealtime);
                    expired = elapsedAtEnqueue + elapsedSinceEnqueue >=
                              expiry.RealtimeDurationSeconds;
                }
                else if (expiry.Mode == NotificationExpiryMode.AfterPresentation &&
                         record.PresentedAtRealtime.HasValue)
                {
                    expired = _clock.RealtimeSeconds - record.PresentedAtRealtime.Value >=
                              expiry.RealtimeDurationSeconds;
                }

                if (expired)
                {
                    Complete(record, NotificationDeliveryState.Expired, null);
                    changed = true;
                }
            }

            if (changed)
            {
                _revision++;
            }
        }

        private void OfferNext(NotificationChannel channel)
        {
            if (!_presenters.TryGetValue(channel, out Registration registration) ||
                !registration.Active ||
                _records.Any(record =>
                    record.Channel == channel &&
                    !IsComplete(record.State) &&
                    (record.State == NotificationDeliveryState.PendingPresentation ||
                     record.State == NotificationDeliveryState.Presented) &&
                    record.PresenterGeneration == registration.Token.Generation) ||
                !_offersInProgress.Add(channel))
            {
                return;
            }

            try
            {
                Record next = OrderedRecords().FirstOrDefault(record =>
                    record.Channel == channel &&
                    !IsComplete(record.State) &&
                    IsAwaitingPresenter(record.State));
                if (next == null)
                {
                    return;
                }

                next.DeliveryAttempt++;
                NotificationPresenterOfferResult result;
                try
                {
                    result = registration.Presenter.Offer(
                        new NotificationPresentationOffer(Snapshot(next)));
                }
                catch
                {
                    result = new NotificationPresenterOfferResult(
                        NotificationPresenterOfferStatus.Failed,
                        PresenterFailedDiagnostic);
                }

                bool registrationStillOwnsChannel =
                    registration.Active &&
                    _presenters.TryGetValue(channel, out Registration active) &&
                    ReferenceEquals(active, registration);
                if (registrationStillOwnsChannel &&
                    result != null &&
                    result.Status ==
                    NotificationPresenterOfferStatus.AcceptedPendingPresentation)
                {
                    next.State = NotificationDeliveryState.PendingPresentation;
                    next.PresenterId = registration.Token.PresenterId;
                    next.PresenterGeneration = registration.Token.Generation;
                    next.FailureCode = null;
                }
                else
                {
                    next.State = NotificationDeliveryState.DeliveryFailed;
                    next.PresenterId = null;
                    next.PresenterGeneration = null;
                    next.FailureCode = result == null || !IsSafeFailureCode(result.FailureCode)
                        ? PresenterFailedDiagnostic
                        : result.FailureCode;
                    EmitOnce(
                        PresenterFailedDiagnostic,
                        NotificationSeverity.RecoverableError,
                        next.Definition.DefinitionId,
                        next.Request.CorrelationId);
                }

                next.LastTransitionAtUtc = _clock.UtcNow;
                _revision++;
            }
            finally
            {
                _offersInProgress.Remove(channel);
            }
        }

        private bool TryGetPresenterRecord(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId,
            out Record record,
            out NotificationReceiptUpdateResult rejected)
        {
            record = _records.FirstOrDefault(item =>
                string.Equals(
                    item.InstanceId,
                    notificationInstanceId,
                    StringComparison.Ordinal));
            if (record == null)
            {
                rejected = new NotificationReceiptUpdateResult(
                    NotificationReceiptUpdateStatus.RejectedNotFound,
                    null,
                    PresenterFailedDiagnostic);
                return false;
            }

            if (token == null ||
                !_registrations.TryGetValue(token.Generation, out Registration registration) ||
                !registration.Active ||
                !string.Equals(
                    registration.Token.PresenterId,
                    token.PresenterId,
                    StringComparison.Ordinal) ||
                record.PresenterGeneration != token.Generation ||
                !string.Equals(record.PresenterId, token.PresenterId, StringComparison.Ordinal))
            {
                rejected = Updated(
                    NotificationReceiptUpdateStatus.RejectedStaleRegistration,
                    record,
                    PresenterFailedDiagnostic);
                return false;
            }

            rejected = null;
            return true;
        }

        private bool DurableStoreAvailable()
        {
            try
            {
                return _durableStore != null && _durableStore.IsAvailable;
            }
            catch
            {
                return false;
            }
        }

        private NotificationDurableRecord FindDurableCorrelation(
            string correlationId,
            string definitionId)
        {
            if (!DurableStoreAvailable())
            {
                return null;
            }

            try
            {
                return _durableStore.FindByCorrelation(correlationId, definitionId);
            }
            catch
            {
                return null;
            }
        }

        private bool TryCommitDurable(NotificationDurableRecord record, out string diagnostic)
        {
            diagnostic = NotificationDurablePrivacy.PersistenceDiagnostic;
            if (!DurableStoreAvailable() || record == null)
            {
                return false;
            }

            try
            {
                return _durableStore.TryCommit(record, out diagnostic);
            }
            catch
            {
                diagnostic = NotificationDurablePrivacy.PersistenceDiagnostic;
                return false;
            }
        }

        private bool TryPersistDurableTransition(
            Record record,
            NotificationDeliveryState state,
            DateTime? acknowledgedAtUtc,
            DateTime? dismissedAtUtc)
        {
            if (record == null ||
                record.Definition == null ||
                !NotificationDurablePrivacy.IsDurable(record.Definition.DurabilityPolicy))
            {
                return true;
            }

            if (!DurableStoreAvailable())
            {
                return false;
            }

            NotificationDurableRecord durable = NotificationDurablePrivacy.FromQueue(
                record.InstanceId,
                record.Definition,
                record.Request,
                state,
                acknowledgedAtUtc,
                dismissedAtUtc,
                record.DeliveryAttempt);
            try
            {
                return _durableStore.TryUpdate(durable, out _);
            }
            catch
            {
                return false;
            }
        }

        private void HydrateDurableRecords()
        {
            if (!DurableStoreAvailable())
            {
                return;
            }

            IReadOnlyList<NotificationDurableRecord> records;
            try
            {
                records = _durableStore.LoadAll();
            }
            catch
            {
                return;
            }

            if (records == null)
            {
                return;
            }

            for (int index = 0; index < records.Count; index++)
            {
                NotificationDurableRecord durable = records[index];
                if (durable == null ||
                    NotificationDurableRecord.IsCompleted(durable.State) ||
                    string.IsNullOrWhiteSpace(durable.DefinitionId))
                {
                    continue;
                }

                NotificationDefinitionResolution resolution;
                try
                {
                    resolution = _definitionResolver == null
                        ? null
                        : _definitionResolver.Resolve(durable.DefinitionId);
                }
                catch
                {
                    continue;
                }

                if (resolution == null ||
                    resolution.Status != NotificationDefinitionResolutionStatus.Found ||
                    resolution.Definition == null)
                {
                    continue;
                }

                var parameters = new List<NotificationParameter>();
                if (durable.Parameters != null)
                {
                    for (int parameterIndex = 0;
                         parameterIndex < durable.Parameters.Count;
                         parameterIndex++)
                    {
                        NotificationDurableParameter parameter = durable.Parameters[parameterIndex];
                        if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
                        {
                            continue;
                        }

                        parameters.Add(
                            new NotificationParameter(
                                parameter.Name,
                                NotificationParameterValue.FromSafeDisplayText(
                                    parameter.TextValue ?? string.Empty)));
                    }
                }

                DateTime occurred = durable.OccurredAtUtcTicks <= 0
                    ? (_clock == null ? DateTime.UtcNow : _clock.UtcNow)
                    : new DateTime(durable.OccurredAtUtcTicks, DateTimeKind.Utc);
                var request = new NotificationRequest(
                    durable.DefinitionId,
                    durable.SourceSystemId,
                    durable.CorrelationId,
                    occurred,
                    parameters,
                    null,
                    null,
                    null);
                long sequence = checked(++_nextSequence);
                _records.Add(
                    new Record(
                        durable.RecordId,
                        sequence,
                        resolution.Definition,
                        request,
                        resolution.Definition.DefaultChannel,
                        string.Empty,
                        occurred,
                        _clock == null ? 0d : _clock.RealtimeSeconds));
            }
        }

        private Record FindCorrelationMatch(
            string correlationId,
            NotificationDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(correlationId) ||
                definition == null ||
                definition.DeduplicationPolicy == NotificationDeduplicationPolicy.None)
            {
                return null;
            }

            IEnumerable<Record> matches = _records
                .Where(record =>
                    record.State != NotificationDeliveryState.Superseded &&
                    string.Equals(
                        record.Request.CorrelationId,
                        correlationId,
                        StringComparison.Ordinal));
            if (definition.DeduplicationPolicy ==
                NotificationDeduplicationPolicy.ByCorrelationAndDefinition)
            {
                matches = matches.Where(record =>
                    string.Equals(
                        record.Definition.DefinitionId,
                        definition.DefinitionId,
                        StringComparison.Ordinal));
            }

            return matches
                .OrderByDescending(record => record.Sequence)
                .FirstOrDefault();
        }

        private static bool CanReplace(Record existing, NotificationDefinition replacement)
        {
            if (replacement.DeduplicationPolicy !=
                NotificationDeduplicationPolicy.ReplaceEarlierCorrelation ||
                !replacement.AllowedPredecessorDefinitionIds.Contains(
                    existing.Definition.DefinitionId,
                    StringComparer.Ordinal) ||
                !existing.Definition.AllowedSuccessorDefinitionIds.Contains(
                    replacement.DefinitionId,
                    StringComparer.Ordinal))
            {
                return false;
            }

            return existing.Definition.Severity != NotificationSeverity.BlockingError ||
                   replacement.Severity >= existing.Definition.Severity;
        }

        private IEnumerable<Record> OrderedRecords() =>
            _records
                .OrderByDescending(record =>
                    record.Definition.AcknowledgementPolicy ==
                    NotificationAcknowledgementPolicy.Required ||
                    record.Definition.Severity == NotificationSeverity.BlockingError)
                .ThenByDescending(record => record.Definition.Severity)
                .ThenByDescending(record => record.Definition.Priority)
                .ThenBy(record => record.Request.OccurredAtUtc)
                .ThenBy(record => record.Sequence);

        private void Complete(
            Record record,
            NotificationDeliveryState state,
            string failureCode)
        {
            record.State = state;
            record.CompletedAtUtc = _clock?.UtcNow ?? DateTime.UtcNow;
            record.LastTransitionAtUtc = record.CompletedAtUtc.Value;
            record.FailureCode = failureCode;
            _revision++;
        }

        private NotificationEnqueueResult Rejected(
            NotificationEnqueueStatus status,
            NotificationRequest request,
            string diagnosticCode)
        {
            EmitOnce(
                diagnosticCode,
                status == NotificationEnqueueStatus.RejectedCapacity
                    ? NotificationSeverity.BlockingError
                    : NotificationSeverity.RecoverableError,
                request?.DefinitionId,
                request?.CorrelationId);
            return new NotificationEnqueueResult(
                status,
                request?.DefinitionId,
                request?.CorrelationId,
                null,
                0,
                null,
                diagnosticCode,
                false);
        }

        private void EmitOnce(
            string code,
            NotificationSeverity severity,
            string definitionId,
            string correlationId)
        {
            if (_diagnosticSink == null || string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            string key = string.Join("|", code, definitionId ?? string.Empty, correlationId ?? string.Empty);
            if (!_emittedDiagnosticKeys.Add(key))
            {
                return;
            }

            try
            {
                _diagnosticSink.Record(
                    new NotificationDiagnostic(code, severity, definitionId, correlationId));
            }
            catch
            {
                // Diagnostic sinks cannot change queue semantics.
            }
        }

        private NotificationQueueSnapshot CreateSnapshot() =>
            new NotificationQueueSnapshot(
                _revision,
                OrderedRecords().Select(Snapshot));

        private void NotifyObserversIfChanged(long previousRevision)
        {
            if (_revision != previousRevision)
            {
                NotifyObservers();
            }
        }

        private void NotifyObservers()
        {
            if (_observers.Count == 0)
            {
                return;
            }

            NotificationQueueSnapshot snapshot = CreateSnapshot();
            ObserverRegistration[] registrations = _observers.Values
                .Where(registration => registration.Active)
                .ToArray();
            for (int index = 0; index < registrations.Length; index++)
            {
                ObserverRegistration registration = registrations[index];
                try
                {
                    registration.Observer.OnQueueChanged(snapshot);
                }
                catch
                {
                    EmitOnce(
                        ObserverFailedDiagnostic,
                        NotificationSeverity.RecoverableError,
                        null,
                        registration.Token.Generation.ToString(
                            CultureInfo.InvariantCulture));
                }
            }
        }

        private static NotificationQueueRecordSnapshot Snapshot(Record record) =>
            new NotificationQueueRecordSnapshot(
                record.InstanceId,
                record.Sequence,
                record.Definition,
                record.Request,
                record.Channel,
                Receipt(record));

        private static NotificationDeliveryReceipt Receipt(Record record) =>
            new NotificationDeliveryReceipt(
                record.InstanceId,
                record.State,
                record.PresenterId,
                record.PresenterId == null ? (NotificationChannel?)null : record.Channel,
                record.PresentedAtUtc,
                record.CompletedAtUtc,
                record.DeliveryAttempt,
                record.FailureCode);

        private static NotificationReceiptUpdateResult Updated(
            NotificationReceiptUpdateStatus status,
            Record record,
            string diagnosticCode) =>
            new NotificationReceiptUpdateResult(
                status,
                record == null ? null : Receipt(record),
                diagnosticCode);

        private static bool IsComplete(NotificationDeliveryState state) =>
            state == NotificationDeliveryState.Acknowledged ||
            state == NotificationDeliveryState.Dismissed ||
            state == NotificationDeliveryState.Expired ||
            state == NotificationDeliveryState.Superseded;

        private static bool IsAwaitingPresenter(NotificationDeliveryState state) =>
            state == NotificationDeliveryState.PendingPresenter ||
            state == NotificationDeliveryState.DeliveryFailed;

        private static bool IsSafePresenterId(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            Encoding.UTF8.GetByteCount(value) <=
            NotificationTechnicalLimits.MaximumDefinitionIdUtf8Bytes &&
            value.All(character => !char.IsControl(character));

        private static bool IsSafeFailureCode(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.StartsWith("AL-", StringComparison.Ordinal) &&
            value.Length <= NotificationTechnicalLimits.MaximumDiagnosticCodeUtf8Bytes &&
            value.All(character =>
                char.IsUpper(character) ||
                char.IsDigit(character) ||
                character == '-');

        private sealed class Registration
        {
            public Registration(
                NotificationPresenterRegistrationToken token,
                INotificationPresenter presenter,
                NotificationPresenterCapabilities capabilities)
            {
                Token = token;
                Presenter = presenter;
                Capabilities = capabilities;
                Active = true;
            }

            public NotificationPresenterRegistrationToken Token { get; }
            public INotificationPresenter Presenter { get; }
            public NotificationPresenterCapabilities Capabilities { get; }
            public bool Active { get; set; }
        }

        private sealed class ObserverRegistration
        {
            public ObserverRegistration(
                NotificationQueueObserverRegistrationToken token,
                INotificationQueueObserver observer)
            {
                Token = token;
                Observer = observer;
                Active = true;
            }

            public NotificationQueueObserverRegistrationToken Token { get; }
            public INotificationQueueObserver Observer { get; }
            public bool Active { get; set; }
        }

        private sealed class Record
        {
            public Record(
                string instanceId,
                long sequence,
                NotificationDefinition definition,
                NotificationRequest request,
                NotificationChannel channel,
                string canonicalPayload,
                DateTime createdAtUtc,
                double createdAtRealtime)
            {
                InstanceId = instanceId;
                Sequence = sequence;
                Definition = definition;
                Request = request;
                Channel = channel;
                CanonicalPayload = canonicalPayload;
                State = NotificationDeliveryState.PendingPresenter;
                CreatedAtUtc = createdAtUtc;
                CreatedAtRealtime = createdAtRealtime;
                LastTransitionAtUtc = createdAtUtc;
                AppliedActionKeys = new HashSet<string>(StringComparer.Ordinal);
            }

            public string InstanceId { get; }
            public long Sequence { get; }
            public NotificationDefinition Definition { get; }
            public NotificationRequest Request { get; }
            public NotificationChannel Channel { get; }
            public string CanonicalPayload { get; }
            public DateTime CreatedAtUtc { get; }
            public double CreatedAtRealtime { get; }
            public HashSet<string> AppliedActionKeys { get; }
            public NotificationDeliveryState State { get; set; }
            public string PresenterId { get; set; }
            public long? PresenterGeneration { get; set; }
            public DateTime? PresentedAtUtc { get; set; }
            public double? PresentedAtRealtime { get; set; }
            public DateTime? CompletedAtUtc { get; set; }
            public DateTime LastTransitionAtUtc { get; set; }
            public int DeliveryAttempt { get; set; }
            public string FailureCode { get; set; }
        }
    }

    public sealed class SystemNotificationClock : INotificationClock
    {
        private readonly long _startedAt = Stopwatch.GetTimestamp();

        public DateTime UtcNow => DateTime.UtcNow;

        public double RealtimeSeconds =>
            (Stopwatch.GetTimestamp() - _startedAt) / (double)Stopwatch.Frequency;
    }

    public sealed class UnavailableNotificationDefinitionResolver :
        INotificationDefinitionResolver,
        INotificationLocalizationReferenceAuthority
    {
        public bool IsAvailable => false;

        public NotificationDefinitionResolution Resolve(string definitionId) =>
            new NotificationDefinitionResolution(
                NotificationDefinitionResolutionStatus.CatalogUnavailable,
                null,
                "AL-NTF-DEFINITION");

        public bool Contains(string localizationReference) => false;
    }

    public sealed class UnavailableNotificationActionRegistry : INotificationActionRegistry
    {
        public NotificationActionResult Invoke(
            NotificationQueueRecordSnapshot record,
            NotificationActionDefinition action,
            NotificationActionInvocation invocation) =>
            new NotificationActionResult(
                NotificationActionStatus.RejectedUnavailable,
                "AL-NTF-ACTION");
    }
}
