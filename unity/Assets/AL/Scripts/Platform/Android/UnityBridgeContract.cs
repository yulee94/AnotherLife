using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace AL.Platform.Android
{
    public enum UnityRouteIntent
    {
        Preview = 0,
        Authoritative = 1
    }

    public enum UnityRouteOutcomeStatus
    {
        Success = 0,
        Failure = 1,
        Cancelled = 2,
        Unavailable = 3
    }

    public enum UnityBridgeProtocolErrorCode
    {
        NullMessage = 0,
        EmptyMessage = 1,
        MessageTooLarge = 2,
        MalformedJson = 3,
        DuplicateField = 4,
        UnexpectedField = 5,
        MissingField = 6,
        InvalidContractVersion = 7,
        InvalidRequestId = 8,
        InvalidRouteId = 9,
        InvalidIntent = 10,
        TooManyCapabilities = 11,
        InvalidCapability = 12,
        DuplicateCapability = 13,
        InvalidStatus = 14,
        InvalidDiagnosticCode = 15,
        InvalidResultId = 16,
        MissingResultId = 17,
        PayloadTooLarge = 18,
        MissingDiagnosticCode = 19,
        NoActiveRequest = 20,
        RequestMismatch = 21,
        RouteMismatch = 22,
        DuplicateOutcome = 23,
        SessionClosed = 24,
        SendUnavailable = 25
    }

    public sealed class UnityBridgeProtocolError
    {
        public UnityBridgeProtocolError(
            UnityBridgeProtocolErrorCode code,
            string field = null)
        {
            Code = code;
            Field = field;
        }

        public UnityBridgeProtocolErrorCode Code { get; }
        public string WireCode => UnityBridgeContract.GetProtocolErrorWireValue(Code);
        public string Field { get; }
    }

    public sealed class UnityRouteRequest
    {
        private readonly IReadOnlyList<string> requestedCapabilities;

        public UnityRouteRequest(
            int contractVersion,
            string requestId,
            string routeId,
            UnityRouteIntent intent,
            IReadOnlyList<string> requestedCapabilities)
        {
            ContractVersion = contractVersion;
            RequestId = requestId;
            RouteId = routeId;
            Intent = intent;

            if (requestedCapabilities == null)
            {
                this.requestedCapabilities = null;
                return;
            }

            var retainedCount = Math.Min(
                requestedCapabilities.Count,
                UnityBridgeContract.MaximumCapabilities + 1);
            var copy = retainedCount == 0
                ? Array.Empty<string>()
                : new string[retainedCount];
            for (var index = 0; index < retainedCount; index++)
            {
                copy[index] = requestedCapabilities[index];
            }
            this.requestedCapabilities =
                new ReadOnlyCollection<string>(copy);
        }

        public int ContractVersion { get; }
        public string RequestId { get; }
        public string RouteId { get; }
        public UnityRouteIntent Intent { get; }
        public IReadOnlyList<string> RequestedCapabilities =>
            requestedCapabilities;
    }

    public sealed class UnityRouteOutcome
    {
        public UnityRouteOutcome(
            int contractVersion,
            string requestId,
            string routeId,
            UnityRouteOutcomeStatus status,
            string diagnosticCode = null,
            string resultId = null,
            string payload = null)
        {
            ContractVersion = contractVersion;
            RequestId = requestId;
            RouteId = routeId;
            Status = status;
            DiagnosticCode = diagnosticCode;
            ResultId = resultId;
            Payload = payload;
        }

        public int ContractVersion { get; }
        public string RequestId { get; }
        public string RouteId { get; }
        public UnityRouteOutcomeStatus Status { get; }
        public string DiagnosticCode { get; }
        public string ResultId { get; }
        public string Payload { get; }
    }

    public sealed class UnityBridgeRequestResult
    {
        private UnityBridgeRequestResult(
            UnityRouteRequest request,
            UnityBridgeProtocolError error)
        {
            Request = request;
            Error = error;
        }

        public bool IsAccepted => Request != null && Error == null;
        public UnityRouteRequest Request { get; }
        public UnityBridgeProtocolError Error { get; }

        internal static UnityBridgeRequestResult Accepted(
            UnityRouteRequest request)
        {
            return new UnityBridgeRequestResult(
                request ?? throw new ArgumentNullException(nameof(request)),
                null);
        }

        internal static UnityBridgeRequestResult Rejected(
            UnityBridgeProtocolErrorCode code,
            string field = null)
        {
            return new UnityBridgeRequestResult(
                null,
                new UnityBridgeProtocolError(code, field));
        }
    }

    public sealed class UnityBridgeOutcomeValidationResult
    {
        private UnityBridgeOutcomeValidationResult(
            UnityRouteOutcome outcome,
            UnityBridgeProtocolError error)
        {
            Outcome = outcome;
            Error = error;
        }

        public bool IsAccepted => Outcome != null && Error == null;
        public UnityRouteOutcome Outcome { get; }
        public UnityBridgeProtocolError Error { get; }

        internal static UnityBridgeOutcomeValidationResult Accepted(
            UnityRouteOutcome outcome)
        {
            return new UnityBridgeOutcomeValidationResult(
                outcome ?? throw new ArgumentNullException(nameof(outcome)),
                null);
        }

        internal static UnityBridgeOutcomeValidationResult Rejected(
            UnityBridgeProtocolErrorCode code,
            string field = null)
        {
            return new UnityBridgeOutcomeValidationResult(
                null,
                new UnityBridgeProtocolError(code, field));
        }
    }

    public static class UnityBridgeContract
    {
        public const int ContractVersion = 2;
        public const int MaximumMessageBytes = 32 * 1024;
        public const int MaximumPayloadBytes = 16 * 1024;
        public const int MaximumRouteIdLength = 64;
        public const int MaximumRequestIdLength = 128;
        public const int MaximumResultIdLength = 128;
        public const int MaximumDiagnosticCodeLength = 64;
        public const int MaximumCapabilities = 16;

        public const string RouteNotAvailableDiagnostic =
            "route.not_available";

        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);

        public static UnityBridgeRequestResult ParseRequest(string rawJson)
        {
            if (rawJson == null)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.NullMessage);
            }

            if (rawJson.Length > MaximumMessageBytes)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MessageTooLarge);
            }

            int byteCount;
            try
            {
                byteCount = StrictUtf8.GetByteCount(rawJson);
            }
            catch (EncoderFallbackException)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MalformedJson);
            }

            if (byteCount > MaximumMessageBytes)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MessageTooLarge);
            }

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.EmptyMessage);
            }

            ParsedRequest parsed;
            try
            {
                parsed = new RequestJsonParser(rawJson).Parse();
            }
            catch (DuplicateMemberException duplicate)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.DuplicateField,
                    duplicate.MemberName);
            }
            catch (MalformedJsonException)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MalformedJson);
            }

            if (parsed.UnexpectedField != null)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.UnexpectedField,
                    parsed.UnexpectedField);
            }

            return ValidateParsedRequest(parsed);
        }

        public static UnityBridgeRequestResult ValidateRequest(
            UnityRouteRequest request)
        {
            if (request == null)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.NullMessage);
            }

            if (request.ContractVersion != ContractVersion)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidContractVersion,
                    "contractVersion");
            }

            if (!IsCorrelationId(
                    request.RequestId,
                    MaximumRequestIdLength))
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidRequestId,
                    "requestId");
            }

            if (!IsRouteId(request.RouteId, MaximumRouteIdLength))
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidRouteId,
                    "routeId");
            }

            if (!Enum.IsDefined(typeof(UnityRouteIntent), request.Intent))
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidIntent,
                    "intent");
            }

            var capabilities = request.RequestedCapabilities;
            if (capabilities == null)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingField,
                    "requestedCapabilities");
            }

            if (capabilities.Count > MaximumCapabilities)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.TooManyCapabilities,
                    "requestedCapabilities");
            }

            var uniqueCapabilities =
                new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < capabilities.Count; index++)
            {
                var capability = capabilities[index];
                if (string.IsNullOrEmpty(capability) ||
                    (capability.Length <= MaximumRouteIdLength &&
                     string.IsNullOrWhiteSpace(capability)))
                {
                    return UnityBridgeRequestResult.Rejected(
                        UnityBridgeProtocolErrorCode.InvalidCapability,
                        "requestedCapabilities[" + index + "]");
                }
            }

            for (var index = 0; index < capabilities.Count; index++)
            {
                var capability = capabilities[index];
                if (!IsRouteId(capability, MaximumRouteIdLength))
                {
                    return UnityBridgeRequestResult.Rejected(
                        UnityBridgeProtocolErrorCode.InvalidCapability,
                        "requestedCapabilities[" + index + "]");
                }
            }

            for (var index = 0; index < capabilities.Count; index++)
            {
                var capability = capabilities[index];
                if (!uniqueCapabilities.Add(capability))
                {
                    return UnityBridgeRequestResult.Rejected(
                        UnityBridgeProtocolErrorCode.DuplicateCapability,
                        "requestedCapabilities");
                }
            }

            return UnityBridgeRequestResult.Accepted(
                new UnityRouteRequest(
                    request.ContractVersion,
                    request.RequestId,
                    request.RouteId,
                    request.Intent,
                    capabilities));
        }

        public static UnityBridgeOutcomeValidationResult ValidateOutcome(
            UnityRouteOutcome outcome)
        {
            if (outcome == null)
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.NullMessage);
            }

            if (ExceedsEncodedOutcomeMessageLimit(outcome))
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.MessageTooLarge);
            }

            if (outcome.ContractVersion != ContractVersion)
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidContractVersion,
                    "contractVersion");
            }

            if (!IsCorrelationId(
                    outcome.RequestId,
                    MaximumRequestIdLength))
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidRequestId,
                    "requestId");
            }

            if (!IsRouteId(outcome.RouteId, MaximumRouteIdLength))
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidRouteId,
                    "routeId");
            }

            if (!Enum.IsDefined(
                    typeof(UnityRouteOutcomeStatus),
                    outcome.Status))
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidStatus,
                    "status");
            }

            if (outcome.DiagnosticCode != null &&
                string.IsNullOrWhiteSpace(outcome.DiagnosticCode))
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingField,
                    "diagnosticCode");
            }

            if (outcome.DiagnosticCode != null &&
                !IsDiagnosticCode(outcome.DiagnosticCode))
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidDiagnosticCode,
                    "diagnosticCode");
            }

            if (outcome.ResultId != null &&
                string.IsNullOrWhiteSpace(outcome.ResultId))
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingField,
                    "resultId");
            }

            if (outcome.ResultId != null &&
                !IsCorrelationId(
                    outcome.ResultId,
                    MaximumResultIdLength))
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidResultId,
                    "resultId");
            }

            if (outcome.Payload != null)
            {
                if (string.IsNullOrWhiteSpace(outcome.Payload))
                {
                    return UnityBridgeOutcomeValidationResult.Rejected(
                        UnityBridgeProtocolErrorCode.MissingField,
                        "payload");
                }

                int payloadBytes;
                try
                {
                    payloadBytes = StrictUtf8.GetByteCount(outcome.Payload);
                }
                catch (EncoderFallbackException)
                {
                    return UnityBridgeOutcomeValidationResult.Rejected(
                        UnityBridgeProtocolErrorCode.MalformedJson,
                        "payload");
                }

                if (payloadBytes > MaximumPayloadBytes)
                {
                    return UnityBridgeOutcomeValidationResult.Rejected(
                        UnityBridgeProtocolErrorCode.PayloadTooLarge,
                        "payload");
                }
            }

            if ((outcome.Status == UnityRouteOutcomeStatus.Failure ||
                 outcome.Status == UnityRouteOutcomeStatus.Unavailable) &&
                outcome.DiagnosticCode == null)
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingDiagnosticCode,
                    "diagnosticCode");
            }

            return UnityBridgeOutcomeValidationResult.Accepted(
                new UnityRouteOutcome(
                    outcome.ContractVersion,
                    outcome.RequestId,
                    outcome.RouteId,
                    outcome.Status,
                    outcome.DiagnosticCode,
                    outcome.ResultId,
                    outcome.Payload));
        }

        public static UnityBridgeOutcomeValidationResult
            ValidateOutcomeForRequest(
                UnityRouteOutcome outcome,
                UnityRouteRequest request)
        {
            if (request == null)
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.NoActiveRequest);
            }

            var requestValidation = ValidateRequest(request);
            if (!requestValidation.IsAccepted)
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    requestValidation.Error.Code,
                    requestValidation.Error.Field);
            }

            var outcomeValidation = ValidateOutcome(outcome);
            if (!outcomeValidation.IsAccepted)
            {
                return outcomeValidation;
            }

            var validRequest = requestValidation.Request;
            var validOutcome = outcomeValidation.Outcome;
            if (!string.Equals(
                    validOutcome.RequestId,
                    validRequest.RequestId,
                    StringComparison.Ordinal))
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.RequestMismatch,
                    "requestId");
            }

            if (!string.Equals(
                    validOutcome.RouteId,
                    validRequest.RouteId,
                    StringComparison.Ordinal))
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.RouteMismatch,
                    "routeId");
            }

            if (validRequest.Intent == UnityRouteIntent.Authoritative &&
                validOutcome.Status == UnityRouteOutcomeStatus.Success &&
                validOutcome.ResultId == null)
            {
                return UnityBridgeOutcomeValidationResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingResultId,
                    "resultId");
            }

            return outcomeValidation;
        }

        public static string GetIntentWireValue(UnityRouteIntent intent)
        {
            switch (intent)
            {
                case UnityRouteIntent.Preview:
                    return "preview";
                case UnityRouteIntent.Authoritative:
                    return "authoritative";
                default:
                    return string.Empty;
            }
        }

        public static string GetOutcomeStatusWireValue(
            UnityRouteOutcomeStatus status)
        {
            switch (status)
            {
                case UnityRouteOutcomeStatus.Success:
                    return "success";
                case UnityRouteOutcomeStatus.Failure:
                    return "failure";
                case UnityRouteOutcomeStatus.Cancelled:
                    return "cancelled";
                case UnityRouteOutcomeStatus.Unavailable:
                    return "unavailable";
                default:
                    return string.Empty;
            }
        }

        public static string GetProtocolErrorWireValue(
            UnityBridgeProtocolErrorCode code)
        {
            switch (code)
            {
                case UnityBridgeProtocolErrorCode.NullMessage:
                    return "bridge.null_message";
                case UnityBridgeProtocolErrorCode.EmptyMessage:
                    return "bridge.empty_message";
                case UnityBridgeProtocolErrorCode.MessageTooLarge:
                    return "bridge.message_too_large";
                case UnityBridgeProtocolErrorCode.MalformedJson:
                    return "bridge.malformed_json";
                case UnityBridgeProtocolErrorCode.DuplicateField:
                    return "bridge.duplicate_field";
                case UnityBridgeProtocolErrorCode.UnexpectedField:
                    return "bridge.unexpected_field";
                case UnityBridgeProtocolErrorCode.MissingField:
                    return "bridge.missing_field";
                case UnityBridgeProtocolErrorCode.InvalidContractVersion:
                    return "bridge.invalid_contract_version";
                case UnityBridgeProtocolErrorCode.InvalidRequestId:
                    return "bridge.invalid_request_id";
                case UnityBridgeProtocolErrorCode.InvalidRouteId:
                    return "bridge.invalid_route_id";
                case UnityBridgeProtocolErrorCode.InvalidIntent:
                    return "bridge.invalid_intent";
                case UnityBridgeProtocolErrorCode.TooManyCapabilities:
                    return "bridge.too_many_capabilities";
                case UnityBridgeProtocolErrorCode.InvalidCapability:
                    return "bridge.invalid_capability";
                case UnityBridgeProtocolErrorCode.DuplicateCapability:
                    return "bridge.duplicate_capability";
                case UnityBridgeProtocolErrorCode.InvalidStatus:
                    return "bridge.invalid_status";
                case UnityBridgeProtocolErrorCode.InvalidDiagnosticCode:
                    return "bridge.invalid_diagnostic_code";
                case UnityBridgeProtocolErrorCode.InvalidResultId:
                    return "bridge.invalid_result_id";
                case UnityBridgeProtocolErrorCode.MissingResultId:
                    return "bridge.missing_result_id";
                case UnityBridgeProtocolErrorCode.PayloadTooLarge:
                    return "bridge.payload_too_large";
                case UnityBridgeProtocolErrorCode.MissingDiagnosticCode:
                    return "bridge.missing_diagnostic_code";
                case UnityBridgeProtocolErrorCode.NoActiveRequest:
                    return "bridge.no_active_request";
                case UnityBridgeProtocolErrorCode.RequestMismatch:
                    return "bridge.request_mismatch";
                case UnityBridgeProtocolErrorCode.RouteMismatch:
                    return "bridge.route_mismatch";
                case UnityBridgeProtocolErrorCode.DuplicateOutcome:
                    return "bridge.duplicate_outcome";
                case UnityBridgeProtocolErrorCode.SessionClosed:
                    return "bridge.session_closed";
                case UnityBridgeProtocolErrorCode.SendUnavailable:
                    return "bridge.send_unavailable";
                default:
                    return string.Empty;
            }
        }

        private static UnityBridgeRequestResult ValidateParsedRequest(
            ParsedRequest parsed)
        {
            if (!parsed.ContractVersion.IsPresent)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingField,
                    "contractVersion");
            }

            if (parsed.ContractVersion.Kind != JsonValueKind.Number ||
                !string.Equals(
                    parsed.ContractVersion.NumberToken,
                    "2",
                    StringComparison.Ordinal))
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidContractVersion,
                    "contractVersion");
            }

            if (!parsed.RequestId.IsPresent)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingField,
                    "requestId");
            }

            if (parsed.RequestId.Kind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(parsed.RequestId.StringValue))
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingField,
                    "requestId");
            }

            if (!IsCorrelationId(
                    parsed.RequestId.StringValue,
                    MaximumRequestIdLength))
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidRequestId,
                    "requestId");
            }

            if (!parsed.RouteId.IsPresent)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingField,
                    "routeId");
            }

            if (parsed.RouteId.Kind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(parsed.RouteId.StringValue))
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingField,
                    "routeId");
            }

            if (!IsRouteId(
                    parsed.RouteId.StringValue,
                    MaximumRouteIdLength))
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidRouteId,
                    "routeId");
            }

            if (!parsed.Intent.IsPresent)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingField,
                    "intent");
            }

            if (parsed.Intent.Kind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(parsed.Intent.StringValue))
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidIntent,
                    "intent");
            }

            UnityRouteIntent intent;
            if (string.Equals(
                    parsed.Intent.StringValue,
                    "preview",
                    StringComparison.Ordinal))
            {
                intent = UnityRouteIntent.Preview;
            }
            else if (string.Equals(
                         parsed.Intent.StringValue,
                         "authoritative",
                         StringComparison.Ordinal))
            {
                intent = UnityRouteIntent.Authoritative;
            }
            else
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidIntent,
                    "intent");
            }

            if (!parsed.Capabilities.IsPresent)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.MissingField,
                    "requestedCapabilities");
            }

            if (!parsed.Capabilities.IsArray)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.InvalidCapability,
                    "requestedCapabilities");
            }

            if (parsed.Capabilities.Count > MaximumCapabilities)
            {
                return UnityBridgeRequestResult.Rejected(
                    UnityBridgeProtocolErrorCode.TooManyCapabilities,
                    "requestedCapabilities");
            }

            var capabilities = new string[parsed.Capabilities.Count];
            var uniqueCapabilities =
                new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 index < parsed.Capabilities.Count;
                 index++)
            {
                var value = parsed.Capabilities.Values[index];
                var field = "requestedCapabilities[" + index + "]";
                if (value.Kind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.StringValue))
                {
                    return UnityBridgeRequestResult.Rejected(
                        UnityBridgeProtocolErrorCode.InvalidCapability,
                        field);
                }

                capabilities[index] = value.StringValue;
            }

            for (var index = 0;
                 index < capabilities.Length;
                 index++)
            {
                if (!IsRouteId(
                        capabilities[index],
                        MaximumRouteIdLength))
                {
                    return UnityBridgeRequestResult.Rejected(
                        UnityBridgeProtocolErrorCode.InvalidCapability,
                        "requestedCapabilities[" + index + "]");
                }
            }

            for (var index = 0;
                 index < capabilities.Length;
                 index++)
            {
                if (!uniqueCapabilities.Add(capabilities[index]))
                {
                    return UnityBridgeRequestResult.Rejected(
                        UnityBridgeProtocolErrorCode.DuplicateCapability,
                        "requestedCapabilities");
                }
            }

            return UnityBridgeRequestResult.Accepted(
                new UnityRouteRequest(
                    ContractVersion,
                    parsed.RequestId.StringValue,
                    parsed.RouteId.StringValue,
                    intent,
                    capabilities));
        }

        private static bool IsCorrelationId(string value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length > maximumLength ||
                !IsAsciiLetterOrDigit(value[0]))
            {
                return false;
            }

            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                if (!IsAsciiLetterOrDigit(character) &&
                    character != '.' &&
                    character != '_' &&
                    character != ':' &&
                    character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsRouteId(string value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length > maximumLength ||
                !IsAsciiLetter(value[0]))
            {
                return false;
            }

            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                if (!IsAsciiLetterOrDigit(character) &&
                    character != '.' &&
                    character != '_' &&
                    character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsDiagnosticCode(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length > MaximumDiagnosticCodeLength ||
                !IsLowerAsciiLetter(value[0]))
            {
                return false;
            }

            var separatorPending = false;
            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                if (IsLowerAsciiLetter(character) ||
                    (character >= '0' && character <= '9'))
                {
                    separatorPending = false;
                    continue;
                }

                if ((character == '.' ||
                     character == '_' ||
                     character == '-') &&
                    !separatorPending)
                {
                    separatorPending = true;
                    continue;
                }

                return false;
            }

            return !separatorPending;
        }

        private static bool IsAsciiLetter(char value)
        {
            return (value >= 'A' && value <= 'Z') ||
                   (value >= 'a' && value <= 'z');
        }

        private static bool IsLowerAsciiLetter(char value)
        {
            return value >= 'a' && value <= 'z';
        }

        private static bool IsAsciiLetterOrDigit(char value)
        {
            return IsAsciiLetter(value) ||
                   (value >= '0' && value <= '9');
        }

        private static bool ExceedsEncodedOutcomeMessageLimit(
            UnityRouteOutcome outcome)
        {
            long minimumCount = 1;
            minimumCount += "\"contractVersion\":2".Length;
            minimumCount += ",\"requestId\":\"".Length;
            minimumCount += outcome.RequestId?.Length ?? 0;
            minimumCount += 1;
            minimumCount += ",\"routeId\":\"".Length;
            minimumCount += outcome.RouteId?.Length ?? 0;
            minimumCount += 1;
            minimumCount += ",\"status\":\"".Length;
            minimumCount += GetOutcomeStatusWireValue(
                outcome.Status).Length;
            minimumCount += 1;

            if (outcome.DiagnosticCode != null)
            {
                minimumCount += ",\"diagnosticCode\":\"".Length;
                minimumCount += outcome.DiagnosticCode.Length;
                minimumCount += 1;
            }

            if (outcome.ResultId != null)
            {
                minimumCount += ",\"resultId\":\"".Length;
                minimumCount += outcome.ResultId.Length;
                minimumCount += 1;
            }

            if (outcome.Payload != null)
            {
                minimumCount += ",\"payload\":\"".Length;
                minimumCount += outcome.Payload.Length;
                minimumCount += 1;
            }

            minimumCount += 1;
            if (minimumCount > MaximumMessageBytes)
            {
                return true;
            }

            return GetEncodedOutcomeByteCount(outcome) >
                   MaximumMessageBytes;
        }

        private static int GetEncodedOutcomeByteCount(
            UnityRouteOutcome outcome)
        {
            var count = 1;
            count += "\"contractVersion\":2".Length;
            count += ",\"requestId\":\"".Length;
            count += GetEscapedStringByteCount(outcome.RequestId);
            count += 1;
            count += ",\"routeId\":\"".Length;
            count += GetEscapedStringByteCount(outcome.RouteId);
            count += 1;
            count += ",\"status\":\"".Length;
            count += GetOutcomeStatusWireValue(outcome.Status).Length;
            count += 1;

            if (outcome.DiagnosticCode != null)
            {
                count += ",\"diagnosticCode\":\"".Length;
                count += GetEscapedStringByteCount(
                    outcome.DiagnosticCode);
                count += 1;
            }

            if (outcome.ResultId != null)
            {
                count += ",\"resultId\":\"".Length;
                count += GetEscapedStringByteCount(outcome.ResultId);
                count += 1;
            }

            if (outcome.Payload != null)
            {
                count += ",\"payload\":\"".Length;
                count += GetEscapedStringByteCount(outcome.Payload);
                count += 1;
            }

            return count + 1;
        }

        private static int GetEscapedStringByteCount(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character == '"' || character == '\\')
                {
                    count += 2;
                }
                else if (character == '\b' ||
                         character == '\f' ||
                         character == '\n' ||
                         character == '\r' ||
                         character == '\t')
                {
                    count += 2;
                }
                else if (character < 0x20)
                {
                    count += 6;
                }
                else if (character <= 0x7f)
                {
                    count += 1;
                }
                else if (character <= 0x7ff)
                {
                    count += 2;
                }
                else if (char.IsHighSurrogate(character) &&
                         index + 1 < value.Length &&
                         char.IsLowSurrogate(value[index + 1]))
                {
                    count += 4;
                    index++;
                }
                else
                {
                    count += 3;
                }
            }

            return count;
        }

        private enum RequestMember
        {
            Unknown = -1,
            ContractVersion = 0,
            RequestId = 1,
            RouteId = 2,
            Intent = 3,
            RequestedCapabilities = 4
        }

        private enum JsonValueKind
        {
            Missing = 0,
            String = 1,
            Number = 2,
            Object = 3,
            Array = 4,
            Boolean = 5,
            Null = 6
        }

        private sealed class ParsedValue
        {
            internal bool IsPresent { get; set; }
            internal JsonValueKind Kind { get; set; }
            internal string StringValue { get; set; }
            internal string NumberToken { get; set; }
        }

        private sealed class ParsedCapabilities
        {
            internal bool IsPresent { get; set; }
            internal bool IsArray { get; set; }
            internal int Count { get; set; }
            internal List<ParsedValue> Values { get; } =
                new List<ParsedValue>(MaximumCapabilities);
        }

        private sealed class ParsedRequest
        {
            internal ParsedValue ContractVersion { get; } =
                new ParsedValue();
            internal ParsedValue RequestId { get; } =
                new ParsedValue();
            internal ParsedValue RouteId { get; } =
                new ParsedValue();
            internal ParsedValue Intent { get; } =
                new ParsedValue();
            internal ParsedCapabilities Capabilities { get; } =
                new ParsedCapabilities();
            internal string UnexpectedField { get; set; }
        }

        private sealed class RequestJsonParser
        {
            private const int MaximumJsonDepth = 32;

            private readonly string source;
            private readonly ParsedRequest parsed = new ParsedRequest();
            private int index;
            private int seenMembers;
            private bool duplicateGuardEnabled = true;

            internal RequestJsonParser(string source)
            {
                this.source =
                    source ?? throw new ArgumentNullException(nameof(source));
            }

            internal ParsedRequest Parse()
            {
                SkipWhitespace();
                if (!Consume('{'))
                {
                    throw new MalformedJsonException();
                }

                SkipWhitespace();
                if (Consume('}'))
                {
                    FinishDocument();
                    return parsed;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (!Peek('"'))
                    {
                        throw new MalformedJsonException();
                    }

                    var memberName = ReadString();
                    var member = GetRequestMember(memberName);
                    var memberBit = 0;
                    var duplicateMember = false;
                    if (member != RequestMember.Unknown)
                    {
                        memberBit = 1 << (int)member;
                        duplicateMember =
                            duplicateGuardEnabled &&
                            (seenMembers & memberBit) != 0;
                    }

                    SkipWhitespace();
                    Require(':');
                    SkipWhitespace();

                    if (duplicateMember)
                    {
                        throw new DuplicateMemberException(
                            GetRequestMemberName(member));
                    }

                    if (member == RequestMember.Unknown)
                    {
                        if (parsed.UnexpectedField == null)
                        {
                            parsed.UnexpectedField = memberName;
                        }

                        duplicateGuardEnabled = false;
                    }
                    else
                    {
                        seenMembers |= memberBit;
                    }

                    ReadMemberValue(member);
                    SkipWhitespace();
                    if (Consume('}'))
                    {
                        FinishDocument();
                        return parsed;
                    }

                    Require(',');
                }
            }

            private void ReadMemberValue(RequestMember member)
            {
                switch (member)
                {
                    case RequestMember.ContractVersion:
                        ReadCapturedValue(parsed.ContractVersion, 1);
                        break;
                    case RequestMember.RequestId:
                        ReadCapturedValue(parsed.RequestId, 1);
                        break;
                    case RequestMember.RouteId:
                        ReadCapturedValue(parsed.RouteId, 1);
                        break;
                    case RequestMember.Intent:
                        ReadCapturedValue(parsed.Intent, 1);
                        break;
                    case RequestMember.RequestedCapabilities:
                        ReadCapabilities(1);
                        break;
                    default:
                        SkipValue(1);
                        break;
                }
            }

            private void ReadCapturedValue(
                ParsedValue destination,
                int depth)
            {
                if (destination.IsPresent)
                {
                    SkipValue(depth);
                    return;
                }

                destination.IsPresent = true;
                if (Peek('"'))
                {
                    destination.Kind = JsonValueKind.String;
                    destination.StringValue = ReadString();
                    return;
                }

                if (PeekNumberStart())
                {
                    destination.Kind = JsonValueKind.Number;
                    destination.NumberToken = ReadNumber();
                    return;
                }

                if (Peek('{'))
                {
                    destination.Kind = JsonValueKind.Object;
                    SkipObject(depth);
                    return;
                }

                if (Peek('['))
                {
                    destination.Kind = JsonValueKind.Array;
                    SkipArray(depth);
                    return;
                }

                if (ConsumeLiteral("true") ||
                    ConsumeLiteral("false"))
                {
                    destination.Kind = JsonValueKind.Boolean;
                    return;
                }

                if (ConsumeLiteral("null"))
                {
                    destination.Kind = JsonValueKind.Null;
                    return;
                }

                throw new MalformedJsonException();
            }

            private void ReadCapabilities(int depth)
            {
                if (parsed.Capabilities.IsPresent)
                {
                    SkipValue(depth);
                    return;
                }

                parsed.Capabilities.IsPresent = true;
                if (!Peek('['))
                {
                    parsed.Capabilities.IsArray = false;
                    SkipValue(depth);
                    return;
                }

                parsed.Capabilities.IsArray = true;
                Require('[');
                SkipWhitespace();
                if (Consume(']'))
                {
                    return;
                }

                while (true)
                {
                    if (parsed.Capabilities.Count <
                        MaximumCapabilities)
                    {
                        var value = new ParsedValue();
                        ReadCapturedValue(value, depth + 1);
                        parsed.Capabilities.Values.Add(value);
                    }
                    else
                    {
                        SkipValue(depth + 1);
                    }

                    parsed.Capabilities.Count++;
                    SkipWhitespace();
                    if (Consume(']'))
                    {
                        return;
                    }

                    Require(',');
                    SkipWhitespace();
                }
            }

            private void SkipValue(int depth)
            {
                if (depth > MaximumJsonDepth)
                {
                    throw new MalformedJsonException();
                }

                if (Peek('"'))
                {
                    ReadString();
                    return;
                }

                if (PeekNumberStart())
                {
                    ReadNumber();
                    return;
                }

                if (Peek('{'))
                {
                    SkipObject(depth);
                    return;
                }

                if (Peek('['))
                {
                    SkipArray(depth);
                    return;
                }

                if (ConsumeLiteral("true") ||
                    ConsumeLiteral("false") ||
                    ConsumeLiteral("null"))
                {
                    return;
                }

                throw new MalformedJsonException();
            }

            private void SkipObject(int depth)
            {
                if (depth > MaximumJsonDepth)
                {
                    throw new MalformedJsonException();
                }

                Require('{');
                SkipWhitespace();
                if (Consume('}'))
                {
                    return;
                }

                while (true)
                {
                    if (!Peek('"'))
                    {
                        throw new MalformedJsonException();
                    }

                    ReadString();
                    SkipWhitespace();
                    Require(':');
                    SkipWhitespace();
                    SkipValue(depth + 1);
                    SkipWhitespace();
                    if (Consume('}'))
                    {
                        return;
                    }

                    Require(',');
                    SkipWhitespace();
                }
            }

            private void SkipArray(int depth)
            {
                if (depth > MaximumJsonDepth)
                {
                    throw new MalformedJsonException();
                }

                Require('[');
                SkipWhitespace();
                if (Consume(']'))
                {
                    return;
                }

                while (true)
                {
                    SkipValue(depth + 1);
                    SkipWhitespace();
                    if (Consume(']'))
                    {
                        return;
                    }

                    Require(',');
                    SkipWhitespace();
                }
            }

            private string ReadString()
            {
                Require('"');
                var segmentStart = index;
                StringBuilder builder = null;

                while (index < source.Length)
                {
                    var character = source[index++];
                    if (character == '"')
                    {
                        if (builder == null)
                        {
                            return source.Substring(
                                segmentStart,
                                index - segmentStart - 1);
                        }

                        builder.Append(
                            source,
                            segmentStart,
                            index - segmentStart - 1);
                        return builder.ToString();
                    }

                    if (character < 0x20)
                    {
                        throw new MalformedJsonException();
                    }

                    if (character == '\\')
                    {
                        if (builder == null)
                        {
                            builder = new StringBuilder();
                        }

                        builder.Append(
                            source,
                            segmentStart,
                            index - segmentStart - 1);
                        AppendEscapedCharacter(builder);
                        segmentStart = index;
                        continue;
                    }

                    if (char.IsHighSurrogate(character))
                    {
                        if (index >= source.Length ||
                            !char.IsLowSurrogate(source[index]))
                        {
                            throw new MalformedJsonException();
                        }

                        index++;
                    }
                    else if (char.IsLowSurrogate(character))
                    {
                        throw new MalformedJsonException();
                    }
                }

                throw new MalformedJsonException();
            }

            private void AppendEscapedCharacter(StringBuilder builder)
            {
                if (index >= source.Length)
                {
                    throw new MalformedJsonException();
                }

                var escaped = source[index++];
                switch (escaped)
                {
                    case '"':
                        builder.Append('"');
                        return;
                    case '\\':
                        builder.Append('\\');
                        return;
                    case '/':
                        builder.Append('/');
                        return;
                    case 'b':
                        builder.Append('\b');
                        return;
                    case 'f':
                        builder.Append('\f');
                        return;
                    case 'n':
                        builder.Append('\n');
                        return;
                    case 'r':
                        builder.Append('\r');
                        return;
                    case 't':
                        builder.Append('\t');
                        return;
                    case 'u':
                        var value = ReadUnicodeEscape();
                        if (char.IsHighSurrogate(value))
                        {
                            if (index + 2 > source.Length ||
                                source[index] != '\\' ||
                                source[index + 1] != 'u')
                            {
                                throw new MalformedJsonException();
                            }

                            index += 2;
                            var low = ReadUnicodeEscape();
                            if (!char.IsLowSurrogate(low))
                            {
                                throw new MalformedJsonException();
                            }

                            builder.Append(value);
                            builder.Append(low);
                            return;
                        }

                        if (char.IsLowSurrogate(value))
                        {
                            throw new MalformedJsonException();
                        }

                        builder.Append(value);
                        return;
                    default:
                        throw new MalformedJsonException();
                }
            }

            private char ReadUnicodeEscape()
            {
                if (index + 4 > source.Length)
                {
                    throw new MalformedJsonException();
                }

                var value = 0;
                for (var offset = 0; offset < 4; offset++)
                {
                    var digit = HexValue(source[index++]);
                    if (digit < 0)
                    {
                        throw new MalformedJsonException();
                    }

                    value = (value << 4) | digit;
                }

                return (char)value;
            }

            private string ReadNumber()
            {
                var start = index;
                Consume('-');
                if (index >= source.Length)
                {
                    throw new MalformedJsonException();
                }

                if (Consume('0'))
                {
                    if (index < source.Length &&
                        IsDigit(source[index]))
                    {
                        throw new MalformedJsonException();
                    }
                }
                else
                {
                    if (index >= source.Length ||
                        source[index] < '1' ||
                        source[index] > '9')
                    {
                        throw new MalformedJsonException();
                    }

                    while (index < source.Length &&
                           IsDigit(source[index]))
                    {
                        index++;
                    }
                }

                if (Consume('.'))
                {
                    if (index >= source.Length ||
                        !IsDigit(source[index]))
                    {
                        throw new MalformedJsonException();
                    }

                    while (index < source.Length &&
                           IsDigit(source[index]))
                    {
                        index++;
                    }
                }

                if (index < source.Length &&
                    (source[index] == 'e' ||
                     source[index] == 'E'))
                {
                    index++;
                    if (index < source.Length &&
                        (source[index] == '+' ||
                         source[index] == '-'))
                    {
                        index++;
                    }

                    if (index >= source.Length ||
                        !IsDigit(source[index]))
                    {
                        throw new MalformedJsonException();
                    }

                    while (index < source.Length &&
                           IsDigit(source[index]))
                    {
                        index++;
                    }
                }

                return source.Substring(start, index - start);
            }

            private void FinishDocument()
            {
                SkipWhitespace();
                if (index != source.Length)
                {
                    throw new MalformedJsonException();
                }
            }

            private void SkipWhitespace()
            {
                while (index < source.Length)
                {
                    var value = source[index];
                    if (value != ' ' &&
                        value != '\t' &&
                        value != '\r' &&
                        value != '\n')
                    {
                        return;
                    }

                    index++;
                }
            }

            private bool Consume(char expected)
            {
                if (index >= source.Length ||
                    source[index] != expected)
                {
                    return false;
                }

                index++;
                return true;
            }

            private bool ConsumeLiteral(string literal)
            {
                if (index + literal.Length > source.Length)
                {
                    return false;
                }

                for (var offset = 0; offset < literal.Length; offset++)
                {
                    if (source[index + offset] != literal[offset])
                    {
                        return false;
                    }
                }

                index += literal.Length;
                return true;
            }

            private void Require(char expected)
            {
                if (!Consume(expected))
                {
                    throw new MalformedJsonException();
                }
            }

            private bool Peek(char expected)
            {
                return index < source.Length &&
                       source[index] == expected;
            }

            private bool PeekNumberStart()
            {
                return index < source.Length &&
                       (source[index] == '-' ||
                        IsDigit(source[index]));
            }

            private static bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }

            private static int HexValue(char value)
            {
                if (value >= '0' && value <= '9')
                {
                    return value - '0';
                }

                if (value >= 'a' && value <= 'f')
                {
                    return value - 'a' + 10;
                }

                if (value >= 'A' && value <= 'F')
                {
                    return value - 'A' + 10;
                }

                return -1;
            }

            private static RequestMember GetRequestMember(string name)
            {
                if (string.Equals(
                        name,
                        "contractVersion",
                        StringComparison.Ordinal))
                {
                    return RequestMember.ContractVersion;
                }

                if (string.Equals(
                        name,
                        "requestId",
                        StringComparison.Ordinal))
                {
                    return RequestMember.RequestId;
                }

                if (string.Equals(
                        name,
                        "routeId",
                        StringComparison.Ordinal))
                {
                    return RequestMember.RouteId;
                }

                if (string.Equals(
                        name,
                        "intent",
                        StringComparison.Ordinal))
                {
                    return RequestMember.Intent;
                }

                if (string.Equals(
                        name,
                        "requestedCapabilities",
                        StringComparison.Ordinal))
                {
                    return RequestMember.RequestedCapabilities;
                }

                return RequestMember.Unknown;
            }

            private static string GetRequestMemberName(
                RequestMember member)
            {
                switch (member)
                {
                    case RequestMember.ContractVersion:
                        return "contractVersion";
                    case RequestMember.RequestId:
                        return "requestId";
                    case RequestMember.RouteId:
                        return "routeId";
                    case RequestMember.Intent:
                        return "intent";
                    case RequestMember.RequestedCapabilities:
                        return "requestedCapabilities";
                    default:
                        return string.Empty;
                }
            }
        }

        private sealed class DuplicateMemberException : Exception
        {
            internal DuplicateMemberException(string memberName)
                : base(string.Empty)
            {
                MemberName = memberName;
            }

            internal string MemberName { get; }
        }

        private sealed class MalformedJsonException : Exception
        {
            internal MalformedJsonException()
                : base(string.Empty)
            {
            }
        }
    }
}
