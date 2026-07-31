using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AL.Core.Interfaces.Notifications
{
    public sealed class NotificationValidationResult
    {
        public NotificationValidationResult(
            bool isValid,
            bool unsafeParameter,
            string diagnosticCode,
            string canonicalPayload)
        {
            IsValid = isValid;
            UnsafeParameter = unsafeParameter;
            DiagnosticCode = diagnosticCode;
            CanonicalPayload = canonicalPayload;
        }

        public bool IsValid { get; }
        public bool UnsafeParameter { get; }
        public string DiagnosticCode { get; }
        public string CanonicalPayload { get; }
    }

    public static class NotificationValidation
    {
        private static readonly Regex DefinitionIdPattern = new Regex(
            "^al_notify_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex SourceSystemIdPattern = new Regex(
            "^al_source_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex TechnicalNamePattern = new Regex(
            "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex ActionIdPattern = new Regex(
            "^al_notify_action_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex LocalizationReferencePattern = new Regex(
            "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*(?:\\.[a-z][a-z0-9]*(?:_[a-z0-9]+)*)+$",
            RegexOptions.CultureInvariant);

        private static readonly Regex DecimalPattern = new Regex(
            "^-?(?:0|[1-9][0-9]*)(?:\\.[0-9]+)?$",
            RegexOptions.CultureInvariant);

        private static readonly Regex DiagnosticPattern = new Regex(
            "^AL-[A-Z0-9]+(?:-[A-Z0-9]+)*$",
            RegexOptions.CultureInvariant);

        public static NotificationValidationResult ValidateDefinition(NotificationDefinition definition)
        {
            if (definition == null ||
                !IsDefinitionId(definition.DefinitionId) ||
                definition.SchemaVersion != NotificationTechnicalLimits.CurrentDefinitionSchemaVersion ||
                definition.ContentVersion <= 0 ||
                !Enum.IsDefined(typeof(NotificationSeverity), definition.Severity) ||
                !Enum.IsDefined(typeof(NotificationCategory), definition.Category) ||
                !Enum.IsDefined(typeof(NotificationChannel), definition.DefaultChannel) ||
                !Enum.IsDefined(
                    typeof(NotificationAcknowledgementPolicy),
                    definition.AcknowledgementPolicy) ||
                !Enum.IsDefined(typeof(NotificationDurabilityPolicy), definition.DurabilityPolicy) ||
                !Enum.IsDefined(
                    typeof(NotificationDeduplicationPolicy),
                    definition.DeduplicationPolicy) ||
                !Enum.IsDefined(typeof(NotificationPrivacyClass), definition.PrivacyClass))
            {
                return Invalid("AL-NTF-DEFINITION");
            }

            if (definition.AllowedChannels == null ||
                definition.AllowedChannels.Count == 0 ||
                definition.AllowedChannels.Count >
                Enum.GetValues(typeof(NotificationChannel)).Length ||
                definition.AllowedChannels.Any(channel =>
                    !Enum.IsDefined(typeof(NotificationChannel), channel)) ||
                definition.AllowedChannels.Distinct().Count() != definition.AllowedChannels.Count ||
                !definition.AllowedChannels.Contains(definition.DefaultChannel))
            {
                return Invalid("AL-NTF-DEFINITION");
            }

            if (definition.Severity == NotificationSeverity.BlockingError &&
                definition.AcknowledgementPolicy != NotificationAcknowledgementPolicy.Required)
            {
                return Invalid("AL-NTF-DEFINITION");
            }

            if (definition.AcknowledgementPolicy == NotificationAcknowledgementPolicy.Required &&
                (definition.DefaultChannel != NotificationChannel.Acknowledgement ||
                 definition.AllowedChannels.Count != 1 ||
                 definition.AllowedChannels[0] != NotificationChannel.Acknowledgement))
            {
                return Invalid("AL-NTF-DEFINITION");
            }

            if (!ValidateExpiry(definition) ||
                definition.Priority < 0 ||
                definition.Priority > 100 ||
                (definition.AllowCapacityEviction &&
                 (definition.DurabilityPolicy != NotificationDurabilityPolicy.SessionTransient ||
                  definition.AcknowledgementPolicy == NotificationAcknowledgementPolicy.Required ||
                  definition.Severity == NotificationSeverity.BlockingError)))
            {
                return Invalid("AL-NTF-DEFINITION");
            }

            if ((definition.DurabilityPolicy ==
                 NotificationDurabilityPolicy.SessionUntilAcknowledged ||
                 definition.DurabilityPolicy ==
                 NotificationDurabilityPolicy.DurableUntilAcknowledged) &&
                (definition.AcknowledgementPolicy == NotificationAcknowledgementPolicy.None ||
                 definition.ExpiryPolicy.Mode != NotificationExpiryMode.None))
            {
                return Invalid("AL-NTF-DEFINITION");
            }

            if (definition.DeduplicationPolicy != NotificationDeduplicationPolicy.None &&
                !definition.RequiresCorrelation)
            {
                return Invalid("AL-NTF-DEFINITION");
            }

            if (!ValidateSourceIds(definition.AllowedSourceSystemIds) ||
                !ValidateParameterSchema(definition.ParameterSchema) ||
                !ValidateActions(definition.Actions) ||
                !ValidateReplacementGraph(definition))
            {
                return Invalid("AL-NTF-DEFINITION");
            }

            return Valid(null);
        }

        public static NotificationValidationResult ValidateRequest(
            NotificationDefinition definition,
            NotificationRequest request,
            DateTime utcNow)
        {
            if (definition == null ||
                request == null ||
                !string.Equals(
                    definition.DefinitionId,
                    request.DefinitionId,
                    StringComparison.Ordinal) ||
                !IsSourceSystemId(request.SourceSystemId) ||
                request.OccurredAtUtc.Kind != DateTimeKind.Utc ||
                request.OccurredAtUtc < utcNow.AddDays(-365d) ||
                request.OccurredAtUtc > utcNow.AddMinutes(5d))
            {
                return Invalid("AL-NTF-PARAMETER");
            }

            if (definition.AllowedSourceSystemIds.Count > 0 &&
                !definition.AllowedSourceSystemIds.Contains(
                    request.SourceSystemId,
                    StringComparer.Ordinal))
            {
                return Invalid("AL-NTF-PARAMETER");
            }

            if (request.RequestedChannel.HasValue &&
                (!Enum.IsDefined(typeof(NotificationChannel), request.RequestedChannel.Value) ||
                 !definition.AllowedChannels.Contains(request.RequestedChannel.Value)))
            {
                return Invalid("AL-NTF-PARAMETER");
            }

            if ((definition.RequiresCorrelation ||
                 definition.DeduplicationPolicy != NotificationDeduplicationPolicy.None) &&
                string.IsNullOrWhiteSpace(request.CorrelationId))
            {
                return Invalid("AL-NTF-CORRELATION-REQUIRED");
            }

            if (!IsSafeOpaqueId(
                    request.CorrelationId,
                    NotificationTechnicalLimits.MaximumCorrelationIdUtf8Bytes,
                    allowBlank: !definition.RequiresCorrelation) ||
                !IsSafeOpaqueId(
                    request.SubjectReference,
                    NotificationTechnicalLimits.MaximumSubjectReferenceUtf8Bytes,
                    allowBlank: true) ||
                !IsDiagnosticCode(request.OriginDiagnosticCode, allowBlank: true))
            {
                return Unsafe("AL-NTF-PARAMETER");
            }

            NotificationValidationResult parameters = ValidateParameters(
                definition.ParameterSchema,
                request.Parameters);
            if (!parameters.IsValid)
            {
                return parameters;
            }

            NotificationChannel channel = request.RequestedChannel ?? definition.DefaultChannel;
            string canonicalPayload = string.Join(
                "|",
                definition.DefinitionId,
                definition.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                definition.ContentVersion.ToString(CultureInfo.InvariantCulture),
                request.SourceSystemId,
                ((int)channel).ToString(CultureInfo.InvariantCulture),
                request.OccurredAtUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                Encode(request.SubjectReference),
                Encode(request.OriginDiagnosticCode),
                parameters.CanonicalPayload);
            return Valid(canonicalPayload);
        }

        public static NotificationValidationResult ValidateActionPayload(
            NotificationActionDefinition action,
            NotificationActionInvocation invocation)
        {
            if (action == null ||
                invocation == null ||
                !string.Equals(action.ActionId, invocation.ActionId, StringComparison.Ordinal))
            {
                return Invalid("AL-NTF-ACTION");
            }

            return ValidateParameters(action.PayloadSchema, invocation.Payload);
        }

        public static bool IsDefinitionId(string value) =>
            IsBoundedPattern(
                value,
                DefinitionIdPattern,
                NotificationTechnicalLimits.MaximumDefinitionIdUtf8Bytes);

        public static bool IsSourceSystemId(string value) =>
            IsBoundedPattern(
                value,
                SourceSystemIdPattern,
                NotificationTechnicalLimits.MaximumSourceSystemIdUtf8Bytes);

        public static bool IsActionId(string value) =>
            IsBoundedPattern(
                value,
                ActionIdPattern,
                NotificationTechnicalLimits.MaximumDefinitionIdUtf8Bytes);

        public static bool IsLocalizationReference(string value) =>
            IsBoundedPattern(
                value,
                LocalizationReferencePattern,
                NotificationTechnicalLimits.MaximumStableValueUtf8Bytes);

        private static NotificationValidationResult ValidateParameters(
            IReadOnlyList<NotificationParameterDefinition> schema,
            IReadOnlyList<NotificationParameter> parameters)
        {
            if (schema == null ||
                parameters == null ||
                schema.Count > NotificationTechnicalLimits.MaximumParameterCount ||
                parameters.Count > NotificationTechnicalLimits.MaximumParameterCount)
            {
                return Invalid("AL-NTF-PARAMETER");
            }

            var definitions = new Dictionary<string, NotificationParameterDefinition>(
                StringComparer.Ordinal);
            for (int index = 0; index < schema.Count; index++)
            {
                NotificationParameterDefinition item = schema[index];
                if (item == null || !definitions.TryAdd(item.Name, item))
                {
                    return Invalid("AL-NTF-DEFINITION");
                }
            }

            var values = new Dictionary<string, NotificationParameterValue>(StringComparer.Ordinal);
            for (int index = 0; index < parameters.Count; index++)
            {
                NotificationParameter parameter = parameters[index];
                if (parameter == null ||
                    parameter.Value == null ||
                    !definitions.TryGetValue(parameter.Name ?? string.Empty, out NotificationParameterDefinition item) ||
                    !values.TryAdd(parameter.Name, parameter.Value))
                {
                    return Invalid("AL-NTF-PARAMETER");
                }

                NotificationValidationResult valueResult = ValidateValue(item, parameter.Value);
                if (!valueResult.IsValid)
                {
                    return valueResult;
                }
            }

            if (schema.Any(item => item.Required && !values.ContainsKey(item.Name)))
            {
                return Invalid("AL-NTF-PARAMETER");
            }

            string canonical = string.Join(
                ";",
                values
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => string.Concat(
                        Encode(pair.Key),
                        ":",
                        ((int)pair.Value.Kind).ToString(CultureInfo.InvariantCulture),
                        ":",
                        pair.Value.CanonicalValue ?? "<null>")));
            return Valid(canonical);
        }

        private static NotificationValidationResult ValidateValue(
            NotificationParameterDefinition definition,
            NotificationParameterValue value)
        {
            if (definition.ValueKind != value.Kind || value.Value == null)
            {
                return Invalid("AL-NTF-PARAMETER");
            }

            switch (value.Kind)
            {
                case NotificationParameterValueKind.Int64:
                case NotificationParameterValueKind.DurationSeconds:
                {
                    long number = (long)value.Value;
                    if ((definition.MinimumInt64.HasValue &&
                         number < definition.MinimumInt64.Value) ||
                        (definition.MaximumInt64.HasValue &&
                         number > definition.MaximumInt64.Value))
                    {
                        return Invalid("AL-NTF-PARAMETER");
                    }

                    return Valid(null);
                }
                case NotificationParameterValueKind.UInt64:
                {
                    ulong number = (ulong)value.Value;
                    if ((definition.MinimumUInt64.HasValue &&
                         number < definition.MinimumUInt64.Value) ||
                        (definition.MaximumUInt64.HasValue &&
                         number > definition.MaximumUInt64.Value))
                    {
                        return Invalid("AL-NTF-PARAMETER");
                    }

                    return Valid(null);
                }
                case NotificationParameterValueKind.DecimalString:
                {
                    string text = (string)value.Value;
                    if (!DecimalPattern.IsMatch(text) ||
                        !decimal.TryParse(
                            text,
                            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture,
                            out _))
                    {
                        return Invalid("AL-NTF-PARAMETER");
                    }

                    return ValidateString(definition, text, rejectMarkup: false);
                }
                case NotificationParameterValueKind.Boolean:
                    return value.Value is bool ? Valid(null) : Invalid("AL-NTF-PARAMETER");
                case NotificationParameterValueKind.ResourceType:
                    return value.Value is ResourceType &&
                           Enum.IsDefined(typeof(ResourceType), value.Value)
                        ? Valid(null)
                        : Invalid("AL-NTF-PARAMETER");
                case NotificationParameterValueKind.TimestampUtc:
                    return value.Value is DateTime timestamp &&
                           timestamp.Kind == DateTimeKind.Utc
                        ? Valid(null)
                        : Invalid("AL-NTF-PARAMETER");
                case NotificationParameterValueKind.SafeDisplayText:
                    if (definition.Persistable ||
                        definition.PrivacyClass == NotificationPrivacyClass.SensitiveTechnical)
                    {
                        return Unsafe("AL-NTF-PARAMETER");
                    }

                    return ValidateString(definition, (string)value.Value, rejectMarkup: true);
                case NotificationParameterValueKind.StableId:
                case NotificationParameterValueKind.RealmId:
                {
                    string text = (string)value.Value;
                    NotificationValidationResult bounded =
                        ValidateString(definition, text, rejectMarkup: true);
                    int maximum = definition.MaximumUtf8Bytes > 0
                        ? definition.MaximumUtf8Bytes
                        : NotificationTechnicalLimits.MaximumStableValueUtf8Bytes;
                    return bounded.IsValid &&
                           IsSafeOpaqueId(text, maximum, allowBlank: false)
                        ? Valid(null)
                        : Unsafe("AL-NTF-PARAMETER");
                }
                case NotificationParameterValueKind.LocalizationReference:
                {
                    string text = (string)value.Value;
                    NotificationValidationResult bounded =
                        ValidateString(definition, text, rejectMarkup: true);
                    return bounded.IsValid && IsLocalizationReference(text)
                        ? Valid(null)
                        : Unsafe("AL-NTF-PARAMETER");
                }
                default:
                    return Invalid("AL-NTF-PARAMETER");
            }
        }

        private static NotificationValidationResult ValidateString(
            NotificationParameterDefinition definition,
            string value,
            bool rejectMarkup)
        {
            int maximum = definition.MaximumUtf8Bytes > 0
                ? definition.MaximumUtf8Bytes
                : value == null
                    ? 0
                    : NotificationTechnicalLimits.MaximumStableValueUtf8Bytes;
            if (string.IsNullOrEmpty(value) ||
                Encoding.UTF8.GetByteCount(value) > maximum ||
                ContainsControl(value) ||
                (rejectMarkup && (value.IndexOf('<') >= 0 || value.IndexOf('>') >= 0)))
            {
                return Unsafe("AL-NTF-PARAMETER");
            }

            return Valid(null);
        }

        private static bool ValidateExpiry(NotificationDefinition definition)
        {
            NotificationExpiryPolicy expiry = definition.ExpiryPolicy;
            if (expiry == null ||
                !Enum.IsDefined(typeof(NotificationExpiryMode), expiry.Mode) ||
                double.IsNaN(expiry.RealtimeDurationSeconds) ||
                double.IsInfinity(expiry.RealtimeDurationSeconds) ||
                expiry.RealtimeDurationSeconds < 0d ||
                expiry.RealtimeDurationSeconds > NotificationTechnicalLimits.MaximumExpirySeconds)
            {
                return false;
            }

            if (expiry.Mode == NotificationExpiryMode.None &&
                expiry.RealtimeDurationSeconds != 0d)
            {
                return false;
            }

            return definition.AcknowledgementPolicy != NotificationAcknowledgementPolicy.Required ||
                   expiry.Mode == NotificationExpiryMode.None;
        }

        private static bool ValidateSourceIds(IReadOnlyList<string> sourceIds) =>
            sourceIds != null &&
            sourceIds.Count > 0 &&
            sourceIds.Count <= NotificationTechnicalLimits.MaximumParameterCount &&
            sourceIds.All(IsSourceSystemId) &&
            sourceIds.Distinct(StringComparer.Ordinal).Count() == sourceIds.Count;

        private static bool ValidateParameterSchema(
            IReadOnlyList<NotificationParameterDefinition> schema)
        {
            if (schema == null || schema.Count > NotificationTechnicalLimits.MaximumParameterCount)
            {
                return false;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < schema.Count; index++)
            {
                NotificationParameterDefinition item = schema[index];
                if (item == null ||
                    !IsTechnicalName(item.Name) ||
                    !names.Add(item.Name) ||
                    !Enum.IsDefined(typeof(NotificationParameterValueKind), item.ValueKind) ||
                    !Enum.IsDefined(typeof(NotificationPrivacyClass), item.PrivacyClass) ||
                    item.MaximumUtf8Bytes < 0 ||
                    item.MaximumUtf8Bytes > NotificationTechnicalLimits.MaximumSafeDisplayTextUtf8Bytes ||
                    (item.MinimumInt64.HasValue &&
                     item.MaximumInt64.HasValue &&
                     item.MinimumInt64.Value > item.MaximumInt64.Value) ||
                    (item.MinimumUInt64.HasValue &&
                     item.MaximumUInt64.HasValue &&
                     item.MinimumUInt64.Value > item.MaximumUInt64.Value) ||
                    (item.Persistable &&
                     (item.PrivacyClass == NotificationPrivacyClass.SensitiveTechnical ||
                      item.ValueKind == NotificationParameterValueKind.SafeDisplayText)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateActions(IReadOnlyList<NotificationActionDefinition> actions)
        {
            if (actions == null || actions.Count > NotificationTechnicalLimits.MaximumActionCount)
            {
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < actions.Count; index++)
            {
                NotificationActionDefinition action = actions[index];
                if (action == null ||
                    !IsActionId(action.ActionId) ||
                    !ids.Add(action.ActionId) ||
                    !Enum.IsDefined(typeof(NotificationActionKind), action.Kind) ||
                    !ValidateParameterSchema(action.PayloadSchema))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateReplacementGraph(NotificationDefinition definition)
        {
            if (definition.AllowedPredecessorDefinitionIds == null ||
                definition.AllowedSuccessorDefinitionIds == null ||
                definition.AllowedPredecessorDefinitionIds.Any(id => !IsDefinitionId(id)) ||
                definition.AllowedSuccessorDefinitionIds.Any(id => !IsDefinitionId(id)) ||
                definition.AllowedPredecessorDefinitionIds
                    .Distinct(StringComparer.Ordinal)
                    .Count() != definition.AllowedPredecessorDefinitionIds.Count ||
                definition.AllowedSuccessorDefinitionIds
                    .Distinct(StringComparer.Ordinal)
                    .Count() != definition.AllowedSuccessorDefinitionIds.Count)
            {
                return false;
            }

            return definition.DeduplicationPolicy ==
                   NotificationDeduplicationPolicy.ReplaceEarlierCorrelation
                ? definition.AllowedPredecessorDefinitionIds.Count > 0
                : definition.AllowedPredecessorDefinitionIds.Count == 0;
        }

        private static bool IsTechnicalName(string value) =>
            IsBoundedPattern(
                value,
                TechnicalNamePattern,
                NotificationTechnicalLimits.MaximumDefinitionIdUtf8Bytes);

        private static bool IsDiagnosticCode(string value, bool allowBlank)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return allowBlank;
            }

            return Encoding.UTF8.GetByteCount(value) <=
                   NotificationTechnicalLimits.MaximumDiagnosticCodeUtf8Bytes &&
                   DiagnosticPattern.IsMatch(value);
        }

        private static bool IsSafeOpaqueId(string value, int maximumUtf8Bytes, bool allowBlank)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return allowBlank;
            }

            return Encoding.UTF8.GetByteCount(value) <= maximumUtf8Bytes &&
                   !ContainsControl(value) &&
                   value.IndexOf('@') < 0 &&
                   value.IndexOf('/') < 0 &&
                   value.IndexOf('\\') < 0 &&
                   value.IndexOf('<') < 0 &&
                   value.IndexOf('>') < 0 &&
                   value.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        private static bool IsBoundedPattern(string value, Regex pattern, int maximumUtf8Bytes) =>
            !string.IsNullOrWhiteSpace(value) &&
            Encoding.UTF8.GetByteCount(value) <= maximumUtf8Bytes &&
            pattern.IsMatch(value);

        private static bool ContainsControl(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Encode(string value) =>
            value == null ? "<null>" : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        private static NotificationValidationResult Valid(string canonicalPayload) =>
            new NotificationValidationResult(true, false, null, canonicalPayload);

        private static NotificationValidationResult Invalid(string code) =>
            new NotificationValidationResult(false, false, code, null);

        private static NotificationValidationResult Unsafe(string code) =>
            new NotificationValidationResult(false, true, code, null);
    }
}
