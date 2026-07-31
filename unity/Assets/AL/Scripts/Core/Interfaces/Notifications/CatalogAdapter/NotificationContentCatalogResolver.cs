using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AL.Data.Catalogs;

namespace AL.Core.Interfaces.Notifications
{
    public sealed class NotificationContentCatalogResolver :
        INotificationDefinitionResolver,
        INotificationLocalizationReferenceAuthority
    {
        public const int ExpectedSourceByteLength = 11526;
        public const string ExpectedSourceSha256 =
            "3c32ba4faa8293897fa8c6ecf3518993aa17778c5848ea47bc48ce697ae1c1c3";
        public const string ExpectedSourcePacketId =
            "al_narrative_notification_content_source_v001";
        public const string ExpectedCatalogId = "al_notification_content_catalog";
        public const string ExpectedCatalogVersion = "0.1.0";
        public const int ExpectedSourceCount = 6;
        public const int ExpectedActionCount = 3;
        public const int ExpectedDefinitionCount = 11;
        public const int ExpectedDraftLocalizationCount = 31;
        public const int MaximumSourceBytes = 16384;

        private const string DefinitionDiagnostic = "AL-NTF-DEFINITION";
        private const string UnsupportedDiagnostic = "AL-NTF-UNSUPPORTED";

        private static readonly Regex PlaceholderPattern = new Regex(
            "\\{([a-z][a-z0-9]*(?:_[a-z0-9]+)*)\\}",
            RegexOptions.CultureInvariant);

        private static readonly string[] ExpectedActionIds =
        {
            "al_notify_action_acknowledge",
            "al_notify_action_retry_operation",
            "al_notify_action_open_recovery_details"
        };

        private static readonly string[] ExpectedDefinitionIds =
        {
            "al_notify_save_recovered_backup",
            "al_notify_save_profile_degraded",
            "al_notify_save_unrecoverable",
            "al_notify_operation_unavailable",
            "al_notify_reward_committed",
            "al_notify_reward_failed",
            "al_notify_world_event_started",
            "al_notify_world_event_ended",
            "al_notify_bridge_unavailable",
            "al_notify_catalog_unavailable",
            "al_notify_content_unavailable"
        };

        private readonly INotificationLocalizationReferenceAuthority localizationReferenceAuthority;
        private readonly IReadOnlyDictionary<string, NotificationDefinition> definitionsById;
        private readonly IReadOnlyList<string> definitionIds;
        private readonly NotificationDefinitionResolutionStatus catalogStatus;
        private readonly string diagnosticCode;

        public NotificationContentCatalogResolver()
        {
            localizationReferenceAuthority = null;
            definitionsById = EmptyDefinitions();
            definitionIds = EmptyStrings();
            catalogStatus = NotificationDefinitionResolutionStatus.CatalogPending;
            diagnosticCode = DefinitionDiagnostic;
        }

        public NotificationContentCatalogResolver(
            byte[] sourceBytes,
            INotificationLocalizationReferenceAuthority localizationReferenceAuthority)
        {
            this.localizationReferenceAuthority = localizationReferenceAuthority;
            if (sourceBytes == null)
            {
                definitionsById = EmptyDefinitions();
                definitionIds = EmptyStrings();
                catalogStatus = NotificationDefinitionResolutionStatus.CatalogUnavailable;
                diagnosticCode = DefinitionDiagnostic;
                return;
            }

            CatalogBuildResult result = TryBuild(sourceBytes);
            definitionsById = result.DefinitionsById;
            definitionIds = result.DefinitionIds;
            catalogStatus = result.Status;
            diagnosticCode = result.DiagnosticCode;
        }

        public IReadOnlyList<string> DefinitionIds => definitionIds;

        public bool IsAvailable
        {
            get
            {
                if (catalogStatus != NotificationDefinitionResolutionStatus.Found ||
                    localizationReferenceAuthority == null)
                {
                    return false;
                }

                try
                {
                    return localizationReferenceAuthority.IsAvailable;
                }
                catch
                {
                    return false;
                }
            }
        }

        public NotificationDefinitionResolution Resolve(string definitionId)
        {
            if (catalogStatus != NotificationDefinitionResolutionStatus.Found)
            {
                return new NotificationDefinitionResolution(
                    catalogStatus,
                    null,
                    diagnosticCode);
            }

            NotificationDefinition definition;
            if (definitionId != null && definitionsById.TryGetValue(definitionId, out definition))
            {
                return new NotificationDefinitionResolution(
                    NotificationDefinitionResolutionStatus.Found,
                    definition,
                    null);
            }

            return new NotificationDefinitionResolution(
                NotificationDefinitionResolutionStatus.UnknownId,
                null,
                DefinitionDiagnostic);
        }

        public bool Contains(string localizationReference)
        {
            if (!IsAvailable || string.IsNullOrEmpty(localizationReference))
            {
                return false;
            }

            try
            {
                return localizationReferenceAuthority.Contains(localizationReference);
            }
            catch
            {
                return false;
            }
        }

        private static CatalogBuildResult TryBuild(byte[] sourceBytes)
        {
            StrictJsonObject root;
            try
            {
                root = StrictJsonDocument.Parse(sourceBytes, MaximumSourceBytes) as StrictJsonObject;
            }
            catch (StrictJsonException)
            {
                return CatalogBuildResult.Invalid();
            }

            if (root == null)
            {
                return CatalogBuildResult.Invalid();
            }

            string version;
            string catalogId;
            string packetId;
            if (!TryReadRequiredString(root, "version", out version) ||
                !TryReadRequiredString(root, "catalogId", out catalogId) ||
                !TryReadRequiredString(root, "sourcePacketId", out packetId))
            {
                return CatalogBuildResult.Invalid();
            }

            if (!string.Equals(version, ExpectedCatalogVersion, StringComparison.Ordinal) ||
                !string.Equals(catalogId, ExpectedCatalogId, StringComparison.Ordinal) ||
                !string.Equals(packetId, ExpectedSourcePacketId, StringComparison.Ordinal))
            {
                return CatalogBuildResult.Unsupported();
            }

            if (!HasExactRootShape(root))
            {
                return CatalogBuildResult.Invalid();
            }

            StrictJsonArray sources;
            StrictJsonArray actions;
            StrictJsonArray definitions;
            StrictJsonArray localization;
            if (!TryReadRequiredArray(root, "sources", out sources) ||
                !TryReadRequiredArray(root, "actions", out actions) ||
                !TryReadRequiredArray(root, "definitions", out definitions) ||
                !TryReadRequiredArray(root, "draftLocalization", out localization) ||
                sources.Items.Count != ExpectedSourceCount ||
                actions.Items.Count != ExpectedActionCount ||
                definitions.Items.Count != ExpectedDefinitionCount ||
                localization.Items.Count != ExpectedDraftLocalizationCount)
            {
                return CatalogBuildResult.Invalid();
            }

            if (sourceBytes.Length != ExpectedSourceByteLength ||
                !string.Equals(ComputeSha256(sourceBytes), ExpectedSourceSha256, StringComparison.Ordinal))
            {
                return CatalogBuildResult.Invalid();
            }

            Dictionary<string, string> localizationByKey;
            HashSet<string> sourceIds;
            var referencedLocalizationKeys = new HashSet<string>(StringComparer.Ordinal);
            if (!TryReadLocalization(localization, out localizationByKey) ||
                !TryReadSources(sources, localizationByKey, referencedLocalizationKeys, out sourceIds) ||
                !ValidateActions(actions, localizationByKey, referencedLocalizationKeys))
            {
                return CatalogBuildResult.Invalid();
            }

            var built = new Dictionary<string, NotificationDefinition>(
                ExpectedDefinitionCount,
                StringComparer.Ordinal);
            var orderedIds = new string[ExpectedDefinitionCount];
            var usedSourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < ExpectedDefinitionIds.Length; index++)
            {
                var row = definitions.Items[index] as StrictJsonObject;
                if (row == null)
                {
                    return CatalogBuildResult.Invalid();
                }

                NotificationDefinition definition;
                if (!TryBuildDefinition(
                        row,
                        ExpectedDefinitionIds[index],
                        sourceIds,
                        localizationByKey,
                        usedSourceIds,
                        referencedLocalizationKeys,
                        out definition) ||
                    built.ContainsKey(definition.DefinitionId))
                {
                    return CatalogBuildResult.Invalid();
                }

                built.Add(definition.DefinitionId, definition);
                orderedIds[index] = definition.DefinitionId;
            }

            if (!usedSourceIds.SetEquals(sourceIds) ||
                !referencedLocalizationKeys.SetEquals(localizationByKey.Keys))
            {
                return CatalogBuildResult.Invalid();
            }

            return CatalogBuildResult.Found(
                new ReadOnlyDictionary<string, NotificationDefinition>(built),
                Array.AsReadOnly(orderedIds));
        }

        private static bool HasExactRootShape(StrictJsonObject root)
        {
            return HasExactProperties(
                root,
                "version",
                "catalogId",
                "game",
                "sourcePacketId",
                "idFormat",
                "sourceAuthorities",
                "contentPolicy",
                "sources",
                "actions",
                "definitions",
                "draftLocalization",
                "engineeringHandoff");
        }

        private static bool TryReadSources(
            StrictJsonArray sources,
            IReadOnlyDictionary<string, string> localizationByKey,
            ISet<string> referencedLocalizationKeys,
            out HashSet<string> sourceIds)
        {
            sourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < sources.Items.Count; index++)
            {
                var source = sources.Items[index] as StrictJsonObject;
                string id;
                string displayNameKey;
                if (source == null ||
                    !HasExactProperties(source, "id", "displayNameKey") ||
                    !TryReadRequiredString(source, "id", out id) ||
                    !TryReadRequiredString(source, "displayNameKey", out displayNameKey) ||
                    !NotificationValidation.IsSourceSystemId(id) ||
                    !NotificationValidation.IsLocalizationReference(displayNameKey) ||
                    !localizationByKey.ContainsKey(displayNameKey) ||
                    sourceIds.Contains(id))
                {
                    return false;
                }

                sourceIds.Add(id);
                referencedLocalizationKeys.Add(displayNameKey);
            }

            return true;
        }

        private static bool ValidateActions(
            StrictJsonArray actions,
            IReadOnlyDictionary<string, string> localizationByKey,
            ISet<string> referencedLocalizationKeys)
        {
            for (var index = 0; index < ExpectedActionIds.Length; index++)
            {
                var action = actions.Items[index] as StrictJsonObject;
                string id;
                string labelKey;
                string kind;
                if (action == null ||
                    !HasExactProperties(action, "id", "labelKey", "kind") ||
                    !TryReadRequiredString(action, "id", out id) ||
                    !TryReadRequiredString(action, "labelKey", out labelKey) ||
                    !TryReadRequiredString(action, "kind", out kind) ||
                    !string.Equals(id, ExpectedActionIds[index], StringComparison.Ordinal) ||
                    !NotificationValidation.IsActionId(id) ||
                    !NotificationValidation.IsLocalizationReference(labelKey) ||
                    !ActionKindMatchesId(id, kind) ||
                    !localizationByKey.ContainsKey(labelKey))
                {
                    return false;
                }

                referencedLocalizationKeys.Add(labelKey);
            }

            return true;
        }

        private static bool TryReadLocalization(
            StrictJsonArray localization,
            out Dictionary<string, string> localizationByKey)
        {
            localizationByKey = new Dictionary<string, string>(
                ExpectedDraftLocalizationCount,
                StringComparer.Ordinal);
            for (var index = 0; index < localization.Items.Count; index++)
            {
                var row = localization.Items[index] as StrictJsonObject;
                string key;
                string text;
                if (row == null ||
                    !HasExactProperties(row, "key", "text") ||
                    !TryReadRequiredString(row, "key", out key) ||
                    !TryReadRequiredString(row, "text", out text) ||
                    !NotificationValidation.IsLocalizationReference(key) ||
                    string.IsNullOrEmpty(text) ||
                    localizationByKey.ContainsKey(key))
                {
                    return false;
                }

                localizationByKey.Add(key, text);
            }

            return true;
        }

        private static bool TryBuildDefinition(
            StrictJsonObject row,
            string expectedDefinitionId,
            IReadOnlyCollection<string> sourceIds,
            IReadOnlyDictionary<string, string> localizationByKey,
            ISet<string> usedSourceIds,
            ISet<string> referencedLocalizationKeys,
            out NotificationDefinition definition)
        {
            definition = null;
            if (!HasExactProperties(
                    row,
                    "id",
                    "sourceId",
                    "severity",
                    "category",
                    "channel",
                    "titleKey",
                    "bodyKey",
                    "parameterNames",
                    "requiresCorrelation",
                    "requiresAcknowledgement",
                    "durability"))
            {
                return false;
            }

            string id;
            string sourceId;
            string severity;
            string category;
            string channel;
            string titleKey;
            string bodyKey;
            string durability;
            bool requiresCorrelation;
            bool requiresAcknowledgement;
            StrictJsonArray parameterNames;
            if (!TryReadRequiredString(row, "id", out id) ||
                !TryReadRequiredString(row, "sourceId", out sourceId) ||
                !TryReadRequiredString(row, "severity", out severity) ||
                !TryReadRequiredString(row, "category", out category) ||
                !TryReadRequiredString(row, "channel", out channel) ||
                !TryReadRequiredString(row, "titleKey", out titleKey) ||
                !TryReadRequiredString(row, "bodyKey", out bodyKey) ||
                !TryReadRequiredArray(row, "parameterNames", out parameterNames) ||
                !TryReadRequiredBoolean(row, "requiresCorrelation", out requiresCorrelation) ||
                !TryReadRequiredBoolean(row, "requiresAcknowledgement", out requiresAcknowledgement) ||
                !TryReadRequiredString(row, "durability", out durability))
            {
                return false;
            }

            NotificationSeverity runtimeSeverity;
            int priority;
            NotificationCategory runtimeCategory;
            NotificationChannel runtimeChannel;
            NotificationDurabilityPolicy runtimeDurability;
            string parameterName;
            if (!string.Equals(id, expectedDefinitionId, StringComparison.Ordinal) ||
                !NotificationValidation.IsDefinitionId(id) ||
                !sourceIds.Contains(sourceId) ||
                !TryMapSeverity(severity, out runtimeSeverity, out priority) ||
                !TryMapCategory(category, out runtimeCategory) ||
                !TryMapChannel(channel, out runtimeChannel) ||
                !TryMapDurability(durability, out runtimeDurability) ||
                !requiresCorrelation ||
                !TryReadSingleParameter(parameterNames, out parameterName) ||
                !NotificationValidation.IsLocalizationReference(titleKey) ||
                !NotificationValidation.IsLocalizationReference(bodyKey) ||
                !localizationByKey.ContainsKey(titleKey) ||
                !localizationByKey.ContainsKey(bodyKey) ||
                !BodyPlaceholdersMatch(localizationByKey[bodyKey], parameterName) ||
                !ValidateDefinitionContradictions(
                    runtimeSeverity,
                    runtimeChannel,
                    runtimeDurability,
                    requiresAcknowledgement))
            {
                return false;
            }

            usedSourceIds.Add(sourceId);
            referencedLocalizationKeys.Add(titleKey);
            referencedLocalizationKeys.Add(bodyKey);

            NotificationAcknowledgementPolicy acknowledgementPolicy =
                requiresAcknowledgement
                    ? NotificationAcknowledgementPolicy.Required
                    : NotificationAcknowledgementPolicy.None;
            NotificationPrivacyClass privacyClass = DefinitionPrivacy(sourceId);
            NotificationPrivacyClass parameterPrivacy =
                string.Equals(parameterName, "profile_label", StringComparison.Ordinal)
                    ? NotificationPrivacyClass.ProfilePrivate
                    : NotificationPrivacyClass.PublicGameplay;
            bool allowCapacityEviction =
                runtimeDurability == NotificationDurabilityPolicy.SessionTransient &&
                acknowledgementPolicy == NotificationAcknowledgementPolicy.None &&
                runtimeSeverity != NotificationSeverity.BlockingError;

            definition = new NotificationDefinition(
                id,
                NotificationTechnicalLimits.CurrentDefinitionSchemaVersion,
                1,
                runtimeSeverity,
                runtimeCategory,
                runtimeChannel,
                new[] { runtimeChannel },
                acknowledgementPolicy,
                runtimeDurability,
                new NotificationExpiryPolicy(NotificationExpiryMode.None, 0d, false),
                priority,
                NotificationDeduplicationPolicy.ByCorrelationAndDefinition,
                privacyClass,
                true,
                allowCapacityEviction,
                new[] { sourceId },
                new[]
                {
                    new NotificationParameterDefinition(
                        parameterName,
                        NotificationParameterValueKind.LocalizationReference,
                        true,
                        256,
                        null,
                        null,
                        null,
                        null,
                        false,
                        parameterPrivacy)
                },
                Array.Empty<NotificationActionDefinition>(),
                Array.Empty<string>(),
                Array.Empty<string>());

            return NotificationValidation.ValidateDefinition(definition).IsValid;
        }

        private static bool TryReadSingleParameter(
            StrictJsonArray parameterNames,
            out string parameterName)
        {
            parameterName = null;
            if (parameterNames == null || parameterNames.Items.Count != 1)
            {
                return false;
            }

            var parameter = parameterNames.Items[0] as StrictJsonString;
            if (parameter == null ||
                !Regex.IsMatch(
                    parameter.Value,
                    "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$",
                    RegexOptions.CultureInvariant))
            {
                return false;
            }

            parameterName = parameter.Value;
            return true;
        }

        private static bool BodyPlaceholdersMatch(string bodyText, string expectedParameterName)
        {
            MatchCollection matches = PlaceholderPattern.Matches(bodyText ?? string.Empty);
            return matches.Count == 1 &&
                   string.Equals(
                       matches[0].Groups[1].Value,
                       expectedParameterName,
                       StringComparison.Ordinal);
        }

        private static bool ActionKindMatchesId(string actionId, string kind)
        {
            if (string.IsNullOrEmpty(actionId) || string.IsNullOrEmpty(kind))
            {
                return false;
            }

            const string prefix = "al_notify_action_";
            return actionId.StartsWith(prefix, StringComparison.Ordinal) &&
                   string.Equals(actionId.Substring(prefix.Length), kind, StringComparison.Ordinal);
        }

        private static bool TryMapSeverity(
            string source,
            out NotificationSeverity severity,
            out int priority)
        {
            switch (source)
            {
                case "info":
                    severity = NotificationSeverity.Information;
                    priority = 30;
                    return true;
                case "success":
                    severity = NotificationSeverity.Success;
                    priority = 40;
                    return true;
                case "warning":
                    severity = NotificationSeverity.Warning;
                    priority = 60;
                    return true;
                case "recoverable_error":
                    severity = NotificationSeverity.RecoverableError;
                    priority = 80;
                    return true;
                case "blocking_error":
                    severity = NotificationSeverity.BlockingError;
                    priority = 100;
                    return true;
                default:
                    severity = default(NotificationSeverity);
                    priority = 0;
                    return false;
            }
        }

        private static bool TryMapCategory(string source, out NotificationCategory category)
        {
            switch (source)
            {
                case "save_recovery":
                    category = NotificationCategory.SaveRecovery;
                    return true;
                case "operation_availability":
                case "catalog":
                case "content_resolution":
                    category = NotificationCategory.ContentAvailability;
                    return true;
                case "reward_result":
                    category = NotificationCategory.Reward;
                    return true;
                case "world_state":
                    category = NotificationCategory.WorldState;
                    return true;
                case "bridge":
                    category = NotificationCategory.Integration;
                    return true;
                default:
                    category = default(NotificationCategory);
                    return false;
            }
        }

        private static bool TryMapChannel(string source, out NotificationChannel channel)
        {
            switch (source)
            {
                case "toast":
                    channel = NotificationChannel.Toast;
                    return true;
                case "acknowledgement":
                    channel = NotificationChannel.Acknowledgement;
                    return true;
                default:
                    channel = default(NotificationChannel);
                    return false;
            }
        }

        private static bool TryMapDurability(
            string source,
            out NotificationDurabilityPolicy durability)
        {
            switch (source)
            {
                case "session_only":
                    durability = NotificationDurabilityPolicy.SessionTransient;
                    return true;
                case "future_durable_outbox":
                    durability = NotificationDurabilityPolicy.DurableUntilAcknowledged;
                    return true;
                default:
                    durability = default(NotificationDurabilityPolicy);
                    return false;
            }
        }

        private static bool ValidateDefinitionContradictions(
            NotificationSeverity severity,
            NotificationChannel channel,
            NotificationDurabilityPolicy durability,
            bool requiresAcknowledgement)
        {
            if (severity == NotificationSeverity.BlockingError && !requiresAcknowledgement)
            {
                return false;
            }

            if (requiresAcknowledgement)
            {
                return channel == NotificationChannel.Acknowledgement &&
                       durability == NotificationDurabilityPolicy.DurableUntilAcknowledged;
            }

            return channel == NotificationChannel.Toast &&
                   durability == NotificationDurabilityPolicy.SessionTransient;
        }

        private static NotificationPrivacyClass DefinitionPrivacy(string sourceId)
        {
            return string.Equals(sourceId, "al_source_save", StringComparison.Ordinal)
                ? NotificationPrivacyClass.ProfilePrivate
                : NotificationPrivacyClass.PublicGameplay;
        }

        private static bool HasExactProperties(StrictJsonObject value, params string[] expected)
        {
            if (value == null || value.Properties.Count != expected.Length)
            {
                return false;
            }

            for (var index = 0; index < expected.Length; index++)
            {
                if (!string.Equals(value.Properties[index].Name, expected[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadRequiredString(
            StrictJsonObject parent,
            string name,
            out string text)
        {
            text = null;
            StrictJsonValue value;
            var stringValue = parent.TryGet(name, out value) ? value as StrictJsonString : null;
            if (stringValue == null || string.IsNullOrEmpty(stringValue.Value))
            {
                return false;
            }

            text = stringValue.Value;
            return true;
        }

        private static bool TryReadRequiredBoolean(
            StrictJsonObject parent,
            string name,
            out bool result)
        {
            result = false;
            StrictJsonValue value;
            var booleanValue = parent.TryGet(name, out value) ? value as StrictJsonBoolean : null;
            if (booleanValue == null)
            {
                return false;
            }

            result = booleanValue.Value;
            return true;
        }

        private static bool TryReadRequiredArray(
            StrictJsonObject parent,
            string name,
            out StrictJsonArray result)
        {
            result = null;
            StrictJsonValue value;
            result = parent.TryGet(name, out value) ? value as StrictJsonArray : null;
            return result != null;
        }

        private static string ComputeSha256(byte[] sourceBytes)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(sourceBytes);
                return string.Concat(hash.Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static IReadOnlyDictionary<string, NotificationDefinition> EmptyDefinitions()
        {
            return new ReadOnlyDictionary<string, NotificationDefinition>(
                new Dictionary<string, NotificationDefinition>(0, StringComparer.Ordinal));
        }

        private static IReadOnlyList<string> EmptyStrings()
        {
            return Array.AsReadOnly(new string[0]);
        }

        private sealed class CatalogBuildResult
        {
            private CatalogBuildResult(
                NotificationDefinitionResolutionStatus status,
                string diagnosticCode,
                IReadOnlyDictionary<string, NotificationDefinition> definitionsById,
                IReadOnlyList<string> definitionIds)
            {
                Status = status;
                DiagnosticCode = diagnosticCode;
                DefinitionsById = definitionsById;
                DefinitionIds = definitionIds;
            }

            internal NotificationDefinitionResolutionStatus Status { get; }
            internal string DiagnosticCode { get; }
            internal IReadOnlyDictionary<string, NotificationDefinition> DefinitionsById { get; }
            internal IReadOnlyList<string> DefinitionIds { get; }

            internal static CatalogBuildResult Found(
                IReadOnlyDictionary<string, NotificationDefinition> definitionsById,
                IReadOnlyList<string> definitionIds)
            {
                return new CatalogBuildResult(
                    NotificationDefinitionResolutionStatus.Found,
                    null,
                    definitionsById,
                    definitionIds);
            }

            internal static CatalogBuildResult Invalid()
            {
                return new CatalogBuildResult(
                    NotificationDefinitionResolutionStatus.InvalidDefinition,
                    DefinitionDiagnostic,
                    EmptyDefinitions(),
                    EmptyStrings());
            }

            internal static CatalogBuildResult Unsupported()
            {
                return new CatalogBuildResult(
                    NotificationDefinitionResolutionStatus.UnsupportedVersion,
                    UnsupportedDiagnostic,
                    EmptyDefinitions(),
                    EmptyStrings());
            }
        }

    }
}
