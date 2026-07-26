using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.ChampionMode.C1
{
    public enum CombatDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum CombatDiagnosticDomain
    {
        Contract = 0,
        ChampionProfile = 1,
        CombatantState = 2,
        CombatAction = 3,
        SkillCatalog = 4,
        SkillLoadout = 5,
        Targeting = 6,
        BossProfile = 7,
        BossState = 8,
        EncounterRequest = 9,
        EncounterState = 10,
        EncounterResult = 11,
        EncounterPresentation = 12
    }

    [Flags]
    public enum CombatBlockScope
    {
        None = 0,
        Construction = 1 << 0,
        Action = 1 << 1,
        Encounter = 1 << 2,
        Result = 1 << 3,
        Presentation = 1 << 4
    }

    /// <summary>
    /// Immutable, bounded technical diagnostic. Values are safe developer context,
    /// never raw exceptions, filesystem paths, or player-authored text.
    /// </summary>
    public sealed class CombatDiagnostic : IComparable<CombatDiagnostic>
    {
        private static readonly string[] AllowedCodePrefixes =
        {
            "AL-CHAMPION-PROFILE-",
            "AL-COMBATANT-STATE-",
            "AL-COMBAT-ACTION-",
            "AL-SKILL-CATALOG-",
            "AL-SKILL-LOADOUT-",
            "AL-TARGETING-",
            "AL-BOSS-PROFILE-",
            "AL-BOSS-STATE-",
            "AL-ENCOUNTER-REQUEST-",
            "AL-ENCOUNTER-STATE-",
            "AL-ENCOUNTER-RESULT-",
            "AL-ENCOUNTER-PRESENTATION-"
        };

        public const int MaximumCodeCharacters = 96;
        public const int MaximumContextCharacters = 256;
        public const int MaximumMessageCharacters = 512;

        public CombatDiagnostic(
            string code,
            CombatDiagnosticSeverity severity,
            CombatDiagnosticDomain domain,
            string fieldPath,
            string message,
            CombatBlockScope blockScope,
            string sourceDefinitionId = "",
            string encounterSessionId = "",
            string encounterAttemptId = "",
            string actionId = "",
            string participantId = "",
            string schemaVersion = "",
            string contentVersion = "",
            string policyVersion = "")
        {
            if (!Enum.IsDefined(typeof(CombatDiagnosticSeverity), severity))
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown diagnostic severity.");
            if (!Enum.IsDefined(typeof(CombatDiagnosticDomain), domain))
                throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unknown diagnostic domain.");
            const CombatBlockScope knownScopes =
                CombatBlockScope.Construction |
                CombatBlockScope.Action |
                CombatBlockScope.Encounter |
                CombatBlockScope.Result |
                CombatBlockScope.Presentation;
            if ((blockScope & ~knownScopes) != 0)
                throw new ArgumentOutOfRangeException(nameof(blockScope), blockScope, "Unknown block scope.");

            Code = ValidateCode(code);
            Severity = severity;
            Domain = domain;
            FieldPath = SanitizeAndBound(fieldPath, MaximumContextCharacters);
            Message = SanitizeAndBound(message, MaximumMessageCharacters);
            BlockScope = blockScope;
            SourceDefinitionId = SanitizeAndBound(sourceDefinitionId, MaximumContextCharacters);
            EncounterSessionId = SanitizeAndBound(encounterSessionId, MaximumContextCharacters);
            EncounterAttemptId = SanitizeAndBound(encounterAttemptId, MaximumContextCharacters);
            ActionId = SanitizeAndBound(actionId, MaximumContextCharacters);
            ParticipantId = SanitizeAndBound(participantId, MaximumContextCharacters);
            SchemaVersion = SanitizeAndBound(schemaVersion, MaximumContextCharacters);
            ContentVersion = SanitizeAndBound(contentVersion, MaximumContextCharacters);
            PolicyVersion = SanitizeAndBound(policyVersion, MaximumContextCharacters);
        }

        public string Code { get; }
        public CombatDiagnosticSeverity Severity { get; }
        public CombatDiagnosticDomain Domain { get; }
        public string SourceDefinitionId { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string ActionId { get; }
        public string ParticipantId { get; }
        public string FieldPath { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public string PolicyVersion { get; }
        public CombatBlockScope BlockScope { get; }
        public string Message { get; }

        public bool BlocksConstruction => (BlockScope & CombatBlockScope.Construction) != 0;
        public bool BlocksAction => (BlockScope & CombatBlockScope.Action) != 0;
        public bool BlocksEncounter => (BlockScope & CombatBlockScope.Encounter) != 0;
        public bool BlocksResult => (BlockScope & CombatBlockScope.Result) != 0;
        public bool BlocksPresentation => (BlockScope & CombatBlockScope.Presentation) != 0;

        public int CompareTo(CombatDiagnostic other)
        {
            if (ReferenceEquals(other, null))
            {
                return -1;
            }

            int comparison = StringComparer.Ordinal.Compare(Code, other.Code);
            if (comparison != 0) return comparison;
            comparison = Domain.CompareTo(other.Domain);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(SourceDefinitionId, other.SourceDefinitionId);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(FieldPath, other.FieldPath);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(EncounterSessionId, other.EncounterSessionId);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(EncounterAttemptId, other.EncounterAttemptId);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(ActionId, other.ActionId);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(ParticipantId, other.ParticipantId);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(SchemaVersion, other.SchemaVersion);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(ContentVersion, other.ContentVersion);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(PolicyVersion, other.PolicyVersion);
            if (comparison != 0) return comparison;
            comparison = Severity.CompareTo(other.Severity);
            if (comparison != 0) return comparison;
            comparison = BlockScope.CompareTo(other.BlockScope);
            if (comparison != 0) return comparison;
            return StringComparer.Ordinal.Compare(Message, other.Message);
        }

        private static string ValidateCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A diagnostic code is required.", nameof(value));
            }

            if (value.Length > MaximumCodeCharacters)
            {
                throw new ArgumentException("The diagnostic code is too long.", nameof(value));
            }

            bool recognizedFamily = false;
            for (int index = 0; index < AllowedCodePrefixes.Length; index++)
            {
                if (value.StartsWith(AllowedCodePrefixes[index], StringComparison.Ordinal))
                {
                    recognizedFamily = true;
                    break;
                }
            }

            if (!recognizedFamily)
            {
                throw new ArgumentException(
                    "The diagnostic code is outside the approved Champion combat code families.",
                    nameof(value));
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool allowed =
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9') ||
                    character == '-';
                if (!allowed)
                {
                    throw new ArgumentException(
                        "Diagnostic codes must use upper-case ASCII, digits, and hyphens.",
                        nameof(value));
                }
            }

            return value;
        }

        private static string SanitizeAndBound(string value, int maximum)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            int limit = Math.Min(value.Length, maximum);
            var characters = new char[limit];
            int output = 0;
            for (int index = 0; index < value.Length && output < maximum; index++)
            {
                char character = value[index];
                if (char.IsControl(character))
                {
                    characters[output++] = ' ';
                    continue;
                }

                if (!char.IsSurrogate(character))
                {
                    characters[output++] = character;
                    continue;
                }

                if (char.IsHighSurrogate(character) &&
                    index + 1 < value.Length &&
                    char.IsLowSurrogate(value[index + 1]) &&
                    output + 1 < maximum)
                {
                    characters[output++] = character;
                    characters[output++] = value[++index];
                    continue;
                }

                characters[output++] = '?';
            }

            return new string(characters, 0, output);
        }
    }

    public static class CombatDiagnosticOrdering
    {
        private static readonly IReadOnlyList<CombatDiagnostic> Empty =
            Array.AsReadOnly(new CombatDiagnostic[0]);

        public static IReadOnlyList<CombatDiagnostic> Order(IEnumerable<CombatDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                return Empty;
            }

            var bounded = new List<CombatDiagnostic>();
            foreach (CombatDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic == null)
                    throw new ArgumentException(
                        "Diagnostic collection contains a null record.",
                        nameof(diagnostics));
                if (bounded.Count >= CombatTechnicalLimits.MaximumDiagnostics)
                    throw new ArgumentException(
                        "Diagnostic collection exceeds the technical entry ceiling.",
                        nameof(diagnostics));
                bounded.Add(diagnostic);
            }

            CombatDiagnostic[] copy = bounded.ToArray();
            Array.Sort(copy);
            return copy.Length == 0 ? Empty : Array.AsReadOnly(copy);
        }
    }

    public sealed class CombatValidationResult
    {
        public CombatValidationResult(IEnumerable<CombatDiagnostic> diagnostics)
        {
            Diagnostics = CombatDiagnosticOrdering.Order(diagnostics);
        }

        public IReadOnlyList<CombatDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.All(diagnostic =>
            diagnostic.Severity != CombatDiagnosticSeverity.Error);
        public bool IsBlocked => Diagnostics.Any(diagnostic =>
            diagnostic.BlockScope != CombatBlockScope.None);
    }

    public static class CombatImmutable
    {
        public static IReadOnlyList<T> Freeze<T>(IList<T> source, string parameterName)
        {
            return Freeze(source, parameterName, CombatTechnicalLimits.MaximumReferenceEntries);
        }

        public static IReadOnlyList<T> Freeze<T>(
            IList<T> source,
            string parameterName,
            int maximumCount)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (maximumCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCount));
            }
            if (source.Count > maximumCount)
            {
                throw new ArgumentException(
                    "Collection exceeds the technical entry ceiling of " + maximumCount + ".",
                    parameterName);
            }

            var copy = new T[source.Count];
            source.CopyTo(copy, 0);
            return Array.AsReadOnly(copy);
        }

        public static IReadOnlyList<T> FreezeNonNull<T>(IList<T> source, string parameterName)
            where T : class
        {
            return FreezeNonNull(
                source,
                parameterName,
                CombatTechnicalLimits.MaximumReferenceEntries);
        }

        public static IReadOnlyList<T> FreezeNonNull<T>(
            IList<T> source,
            string parameterName,
            int maximumCount)
            where T : class
        {
            IReadOnlyList<T> frozen = Freeze(source, parameterName, maximumCount);
            for (int index = 0; index < frozen.Count; index++)
            {
                if (frozen[index] == null)
                {
                    throw new ArgumentException(
                        "Collection contains a null record at index " + index + ".",
                        parameterName);
                }
            }

            return frozen;
        }
    }
}
