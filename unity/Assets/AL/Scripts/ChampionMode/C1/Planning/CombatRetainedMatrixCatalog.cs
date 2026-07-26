using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AL.ChampionMode.C1
{
    public enum CombatRetainedMatrixCategory
    {
        Finite = 0,
        Resource = 1,
        Action = 2,
        Encounter = 3,
        Boss = 4,
        Diagnostic = 5,
        Replay = 6
    }

    public sealed class CombatRetainedMatrixRow
    {
        internal CombatRetainedMatrixRow(
            string rowId,
            CombatRetainedMatrixCategory category,
            string schemaVersion,
            string policyVersion,
            string inputState,
            string operation,
            string expectedStatus,
            long expectedRevisionDelta,
            params string[] expectedEvents)
        {
            RowId = rowId ?? string.Empty;
            Category = category;
            SchemaVersion = schemaVersion ?? string.Empty;
            PolicyVersion = policyVersion ?? string.Empty;
            InputState = inputState ?? string.Empty;
            Operation = operation ?? string.Empty;
            ExpectedStatus = expectedStatus ?? string.Empty;
            ExpectedRevisionDelta = expectedRevisionDelta;
            ExpectedEvents = Array.AsReadOnly(
                (expectedEvents ?? new string[0]).ToArray());
        }

        public string RowId { get; }
        public CombatRetainedMatrixCategory Category { get; }
        public string SchemaVersion { get; }
        public string PolicyVersion { get; }
        public string InputState { get; }
        public string Operation { get; }
        public string ExpectedStatus { get; }
        public long ExpectedRevisionDelta { get; }
        public IReadOnlyList<string> ExpectedEvents { get; }
    }

    /// <summary>
    /// Pure retained outcome/replay example. Counts describe effects owned by a
    /// future result transaction; this C1 artifact performs no persistence or
    /// domain mutation.
    /// </summary>
    public sealed class CombatRetainedResultReplayExample
    {
        internal CombatRetainedResultReplayExample(
            string exampleId,
            string schemaVersion,
            string policyVersion,
            string encounterResultId,
            string originalOutcomeHash,
            string replayOutcomeHash,
            string expectedStatus,
            int expectedResultApplications,
            int expectedRewardApplications,
            int expectedPresentationEvents)
        {
            ExampleId = exampleId ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            PolicyVersion = policyVersion ?? string.Empty;
            EncounterResultId = encounterResultId ?? string.Empty;
            OriginalOutcomeHash = originalOutcomeHash ?? string.Empty;
            ReplayOutcomeHash = replayOutcomeHash ?? string.Empty;
            ExpectedStatus = expectedStatus ?? string.Empty;
            ExpectedResultApplications = expectedResultApplications;
            ExpectedRewardApplications = expectedRewardApplications;
            ExpectedPresentationEvents = expectedPresentationEvents;
        }

        public string ExampleId { get; }
        public string SchemaVersion { get; }
        public string PolicyVersion { get; }
        public string EncounterResultId { get; }
        public string OriginalOutcomeHash { get; }
        public string ReplayOutcomeHash { get; }
        public string ExpectedStatus { get; }
        public int ExpectedResultApplications { get; }
        public int ExpectedRewardApplications { get; }
        public int ExpectedPresentationEvents { get; }
    }

    /// <summary>
    /// Machine-readable C1 state/vector fixtures retained in production source
    /// so a later adapter cannot silently change expected statuses or events.
    /// Rows are immutable, bounded, versioned, and contain no player content.
    /// </summary>
    public static class CombatRetainedMatrixCatalog
    {
        public const string SchemaVersion =
            CombatTechnicalLimits.SupportedSchemaVersion;
        public const string PolicyVersion =
            "combat.retained-matrix.c1.v1";

        private static readonly IReadOnlyList<CombatRetainedMatrixRow>
            RetainedRows = Array.AsReadOnly(new[]
            {
                Row(
                    "finite.zero.allowed",
                    CombatRetainedMatrixCategory.Finite,
                    "finite=0 requirePositive=false",
                    "TryCreate",
                    "Accepted",
                    0),
                Row(
                    "finite.zero.positive-required",
                    CombatRetainedMatrixCategory.Finite,
                    "finite=0 requirePositive=true",
                    "TryCreate",
                    "RejectedNonPositive",
                    0),
                Row(
                    "finite.nan",
                    CombatRetainedMatrixCategory.Finite,
                    "value=NaN",
                    "TryCreate",
                    "RejectedNonFinite",
                    0),
                Row(
                    "finite.positive-infinity",
                    CombatRetainedMatrixCategory.Finite,
                    "value=PositiveInfinity",
                    "TryCreate",
                    "RejectedNonFinite",
                    0),
                Row(
                    "finite.maximum",
                    CombatRetainedMatrixCategory.Finite,
                    "value=technicalMaximum",
                    "TryCreate",
                    "Accepted",
                    0),
                Row(
                    "finite.over-maximum",
                    CombatRetainedMatrixCategory.Finite,
                    "value=technicalMaximum+epsilon",
                    "TryCreate",
                    "RejectedOutOfRange",
                    0),

                Row(
                    "resource.reserve",
                    CombatRetainedMatrixCategory.Resource,
                    "Alive mana=10 reservation=none",
                    "ReserveMana amount=4",
                    CombatantResourcePlanStatus.Applied.ToString(),
                    1,
                    "ResourcesChanged"),
                Row(
                    "resource.reserve.insufficient",
                    CombatRetainedMatrixCategory.Resource,
                    "Alive availableMana=3",
                    "ReserveMana amount=4",
                    CombatantResourcePlanStatus.InsufficientMana.ToString(),
                    0),
                Row(
                    "resource.commit",
                    CombatRetainedMatrixCategory.Resource,
                    "reservation=Reserved",
                    "CommitManaReservation",
                    CombatantResourcePlanStatus.Applied.ToString(),
                    1,
                    "ResourcesChanged"),
                Row(
                    "resource.release",
                    CombatRetainedMatrixCategory.Resource,
                    "reservation=Reserved",
                    "ReleaseManaReservation",
                    CombatantResourcePlanStatus.Applied.ToString(),
                    1,
                    "ResourcesChanged"),
                Row(
                    "resource.release.duplicate",
                    CombatRetainedMatrixCategory.Resource,
                    "reservation=Released",
                    "ReleaseManaReservation exact operation",
                    CombatantResourcePlanStatus.DuplicateExact.ToString(),
                    0),
                Row(
                    "resource.damage.defeat",
                    CombatRetainedMatrixCategory.Resource,
                    "Alive health=4",
                    "Damage amount=4",
                    CombatantResourcePlanStatus
                        .AppliedAndDefeated.ToString(),
                    1,
                    "ResourcesChanged",
                    "CombatantDefeated"),

                Row(
                    "action.request.validate",
                    CombatRetainedMatrixCategory.Action,
                    CombatActionState.Requested.ToString(),
                    "validate",
                    CombatActionPlanStatus.Applied.ToString(),
                    1,
                    "ActionStateChanged"),
                Row(
                    "action.windup.commit",
                    CombatRetainedMatrixCategory.Action,
                    CombatActionState.Windup.ToString(),
                    "commit",
                    CombatActionPlanStatus.Applied.ToString(),
                    1,
                    "ActionStateChanged",
                    "ManaCommitted"),
                Row(
                    "action.resolve.complete",
                    CombatRetainedMatrixCategory.Action,
                    CombatActionState.Resolving.ToString(),
                    "complete",
                    CombatActionPlanStatus.Applied.ToString(),
                    1,
                    "EffectApplied",
                    "CooldownStarted",
                    "Terminal"),
                Row(
                    "action.terminal.resolve",
                    CombatRetainedMatrixCategory.Action,
                    CombatActionState.Completed.ToString(),
                    "resolve again",
                    CombatActionPlanStatus.TerminalState.ToString(),
                    0),
                Row(
                    "action.replay.exact",
                    CombatRetainedMatrixCategory.Action,
                    "receipt exists",
                    "same action ID + payload",
                    CombatActionPlanStatus.DuplicateExact.ToString(),
                    0),
                Row(
                    "action.replay.conflict",
                    CombatRetainedMatrixCategory.Action,
                    "receipt exists",
                    "same action ID + changed payload",
                    CombatActionPlanStatus.CorrelationConflict.ToString(),
                    0),

                Row(
                    "encounter.created.validating",
                    CombatRetainedMatrixCategory.Encounter,
                    CombatEncounterState.Created.ToString(),
                    CombatEncounterState.Validating.ToString(),
                    ChampionEncounterTransitionStatus.Applied.ToString(),
                    1,
                    "EncounterStateChanged"),
                Row(
                    "encounter.validating.ready",
                    CombatRetainedMatrixCategory.Encounter,
                    CombatEncounterState.Validating.ToString(),
                    CombatEncounterState.Ready.ToString(),
                    ChampionEncounterTransitionStatus.Applied.ToString(),
                    1,
                    "EncounterStateChanged"),
                Row(
                    "encounter.practice.resolve",
                    CombatRetainedMatrixCategory.Encounter,
                    "Practice/Resolving",
                    CombatEncounterState.Completed.ToString(),
                    ChampionEncounterTransitionStatus.Applied.ToString(),
                    1,
                    "EncounterStateChanged",
                    "EncounterTerminal:ChampionVictory"),
                Row(
                    "encounter.authoritative.pending",
                    CombatRetainedMatrixCategory.Encounter,
                    "AuthoritativeBoss/Resolving",
                    CombatEncounterState
                        .CompletionPendingCommit.ToString(),
                    ChampionEncounterTransitionStatus.Applied.ToString(),
                    1,
                    "EncounterStateChanged"),
                Row(
                    "encounter.commit.uncertain",
                    CombatRetainedMatrixCategory.Encounter,
                    CombatEncounterState
                        .CompletionPendingCommit.ToString(),
                    CombatEncounterState.RecoveryRequired.ToString(),
                    ChampionEncounterTransitionStatus.Applied.ToString(),
                    1,
                    "EncounterStateChanged",
                    "EncounterTerminal:RecoveryRequired"),
                Row(
                    "encounter.terminal.late-complete",
                    CombatRetainedMatrixCategory.Encounter,
                    CombatEncounterState.Failed.ToString(),
                    CombatEncounterState.Completed.ToString(),
                    ChampionEncounterTransitionStatus
                        .NoChangeTerminal.ToString(),
                    0),
                Row(
                    "encounter.terminal.dispose",
                    CombatRetainedMatrixCategory.Encounter,
                    CombatEncounterState.Completed.ToString(),
                    CombatEncounterState.Disposed.ToString(),
                    ChampionEncounterTransitionStatus.Applied.ToString(),
                    1,
                    "EncounterStateChanged",
                    "EncounterDisposed"),

                Row(
                    "boss.damage.phase",
                    CombatRetainedMatrixCategory.Boss,
                    "Alive/phase0/health=100",
                    "Damage amount=40",
                    BossStateTransitionStatus.Applied.ToString(),
                    1,
                    "BossHealthChanged",
                    "BossPhaseChanged:phase1"),
                Row(
                    "boss.guard.break",
                    CombatRetainedMatrixCategory.Boss,
                    "Stable/guard=10",
                    "GuardDamage amount=10",
                    BossStateTransitionStatus.Applied.ToString(),
                    1,
                    "BossGuardChanged",
                    "BossBreakChanged:Broken"),
                Row(
                    "boss.break.duplicate",
                    CombatRetainedMatrixCategory.Boss,
                    BossGuardState.Broken.ToString(),
                    "GuardDamage",
                    BossStateTransitionStatus
                        .NoChangeAlreadyBroken.ToString(),
                    0),
                Row(
                    "boss.enrage.health",
                    CombatRetainedMatrixCategory.Boss,
                    BossEnrageState.Dormant.ToString(),
                    "Damage crosses health threshold",
                    BossStateTransitionStatus.Applied.ToString(),
                    1,
                    "BossHealthChanged",
                    "BossEnrageChanged:TriggeredByHealth"),
                Row(
                    "boss.defeat",
                    CombatRetainedMatrixCategory.Boss,
                    "Alive/health=1",
                    "Damage amount=1",
                    BossStateTransitionStatus
                        .AppliedAndDefeated.ToString(),
                    1,
                    "BossHealthChanged",
                    "BossDefeated"),
                Row(
                    "boss.defeated.late-damage",
                    CombatRetainedMatrixCategory.Boss,
                    CombatantLifeState.Defeated.ToString(),
                    "Damage",
                    BossStateTransitionStatus
                        .NoChangeTerminal.ToString(),
                    0),

                Row(
                    "diagnostic.code-order",
                    CombatRetainedMatrixCategory.Diagnostic,
                    "codes=B,A",
                    "CombatDiagnosticOrdering.Order",
                    "Ordered:A,B",
                    0),
                Row(
                    "diagnostic.null-elision",
                    CombatRetainedMatrixCategory.Diagnostic,
                    "diagnostics=A,null",
                    "CombatDiagnosticOrdering.Order",
                    "RejectedNullDiagnostic",
                    0),
                Row(
                    "diagnostic.block-scope",
                    CombatRetainedMatrixCategory.Diagnostic,
                    "scope=Encounter|Result",
                    "CombatValidationResult",
                    "Blocked",
                    0),

                Row(
                    "replay.result.exact",
                    CombatRetainedMatrixCategory.Replay,
                    "resultId=same outcomeHash=same",
                    "replay",
                    "DuplicateExact",
                    0),
                Row(
                    "replay.result.conflict",
                    CombatRetainedMatrixCategory.Replay,
                    "resultId=same outcomeHash=changed",
                    "replay",
                    "CorrelationConflict",
                    0),
                Row(
                    "replay.result.new",
                    CombatRetainedMatrixCategory.Replay,
                    "resultId=new",
                    "apply",
                    "Planned",
                    1,
                    "EncounterResultCommitted"),
                Row(
                    "replay.result.recovery",
                    CombatRetainedMatrixCategory.Replay,
                    "commit=uncertain",
                    "recover",
                    "RecoveryRequired",
                    1,
                    "EncounterStateChanged")
            });

        private static readonly IReadOnlyList
            <CombatRetainedResultReplayExample> RetainedReplayExamples =
                Array.AsReadOnly(new[]
                {
                    Replay(
                        "result.authoritative.first",
                        "encounter-result-1",
                        Hash('a'),
                        Hash('a'),
                        "Planned",
                        1,
                        1,
                        1),
                    Replay(
                        "result.authoritative.exact-replay",
                        "encounter-result-1",
                        Hash('a'),
                        Hash('a'),
                        "DuplicateExact",
                        0,
                        0,
                        0),
                    Replay(
                        "result.authoritative.conflict",
                        "encounter-result-1",
                        Hash('a'),
                        Hash('b'),
                        "CorrelationConflict",
                        0,
                        0,
                        0),
                    Replay(
                        "result.practice.session-only",
                        "encounter-result-practice-1",
                        Hash('c'),
                        Hash('c'),
                        "SessionOnly",
                        0,
                        0,
                        1),
                    Replay(
                        "result.commit-uncertain",
                        "encounter-result-2",
                        Hash('d'),
                        Hash('d'),
                        "RecoveryRequired",
                        0,
                        0,
                        0)
                });

        public static IReadOnlyList<CombatRetainedMatrixRow> Rows =>
            RetainedRows;

        public static IReadOnlyList<CombatRetainedResultReplayExample>
            ResultReplayExamples => RetainedReplayExamples;

        public static IReadOnlyList<CombatRetainedMatrixRow> ForCategory(
            CombatRetainedMatrixCategory category)
        {
            return Array.AsReadOnly(
                RetainedRows
                    .Where(row => row.Category == category)
                    .ToArray());
        }

        private static CombatRetainedMatrixRow Row(
            string id,
            CombatRetainedMatrixCategory category,
            string inputState,
            string operation,
            string expectedStatus,
            long revisionDelta,
            params string[] events)
        {
            return new CombatRetainedMatrixRow(
                id,
                category,
                SchemaVersion,
                PolicyVersion,
                inputState,
                operation,
                expectedStatus,
                revisionDelta,
                events);
        }

        private static CombatRetainedResultReplayExample Replay(
            string id,
            string resultId,
            string originalHash,
            string replayHash,
            string status,
            int resultApplications,
            int rewardApplications,
            int presentationEvents)
        {
            return new CombatRetainedResultReplayExample(
                id,
                SchemaVersion,
                PolicyVersion,
                resultId,
                originalHash,
                replayHash,
                status,
                resultApplications,
                rewardApplications,
                presentationEvents);
        }

        private static string Hash(char value)
        {
            return new string(value, CombatTechnicalLimits.Sha256HexCharacters);
        }
    }

    /// <summary>
    /// Collision-free, culture-invariant canonical text used only as a compact
    /// replay comparison key. Every field is length-prefixed, so stable IDs may
    /// safely contain delimiter characters.
    /// </summary>
    internal static class CombatC1CanonicalFingerprint
    {
        public const int MaximumCharacters = 8192;

        public static string Build(params string[] fields)
        {
            var builder = new StringBuilder();
            foreach (string raw in fields ?? new string[0])
            {
                string value = raw ?? string.Empty;
                builder
                    .Append(value.Length.ToString(
                        CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(value);
            }

            if (builder.Length > MaximumCharacters)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fields),
                    "Canonical fingerprint exceeds its technical maximum.");
            }

            return builder.ToString();
        }

        public static string Long(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static string Integer(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static bool IsCanonical(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length > MaximumCharacters)
            {
                return false;
            }

            int cursor = 0;
            while (cursor < value.Length)
            {
                int colon = value.IndexOf(':', cursor);
                if (colon <= cursor)
                {
                    return false;
                }

                string lengthText =
                    value.Substring(cursor, colon - cursor);
                if (lengthText.Length > 1 &&
                    lengthText[0] == '0')
                {
                    return false;
                }

                if (!int.TryParse(
                        lengthText,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int fieldLength) ||
                    fieldLength < 0)
                {
                    return false;
                }

                cursor = colon + 1;
                if (fieldLength > value.Length - cursor)
                {
                    return false;
                }

                cursor += fieldLength;
            }

            return cursor == value.Length;
        }
    }
}
