#if !UNITY_EDITOR
#error The isolated first-user tutorial handoff is Editor-only.
#endif

using System;
using System.Globalization;
using AL.Narrative.Nvs01.Contracts;

namespace AL.Editor.Development.FirstUserGameTest
{
    internal static class FirstUserGameTestTutorialContract
    {
        internal const string ContractVersion = "al.editor.first-user-tutorial.v1";
        internal const string TutorialId = "TUTORIAL_FIRST_WORLD_ENTRY";
        internal const string MoveStepId = "MOVE";
        internal const string BasicAttackStepId = "BASIC_ATTACK";
        internal const string MoveObjectiveId = "OBJ_TUTORIAL_FIRST_WORLD_ENTRY_MOVE";
        internal const string BasicAttackObjectiveId =
            "OBJ_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK";
        internal const string MovementConfirmedEventId =
            "EVENT_TUTORIAL_FIRST_WORLD_ENTRY_MOVEMENT_CONFIRMED";
        internal const string BasicAttackConfirmedEventId =
            "EVENT_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK_CONFIRMED";
        internal const string TutorialCompletedEventId =
            "EVENT_TUTORIAL_FIRST_WORLD_ENTRY_COMPLETED";
        internal const string FollowActiveObjectiveActionId =
            "ACTION_FOLLOW_ACTIVE_OBJECTIVE";
        internal const string ActiveObjectiveFocusedResultId =
            "RESULT_ACTIVE_OBJECTIVE_FOCUSED";
        internal const string ActiveObjectiveNoTargetResultId =
            "RESULT_ACTIVE_OBJECTIVE_NO_TARGET";
        internal const string ActiveObjectiveUnavailableResultId =
            "RESULT_ACTIVE_OBJECTIVE_UNAVAILABLE";
        internal const string OmenQuestId = Nvs01CatalogContract.QuestId;
        internal const string OmenOfferedState = "OFFERED";
        internal const string OmenOfferedObjectiveId = "OBJ_OMEN_1_TALK";
        internal const int SessionIdLength = 32;
        internal const int GenerationLength = 64;
        internal const int MaximumRetainedEnvelopeCharacters = 256;

        internal static bool IsCanonicalSessionId(string value)
        {
            return IsLowerHex(value, SessionIdLength);
        }

        internal static bool IsCanonicalGeneration(string value)
        {
            return IsLowerHex(value, GenerationLength);
        }

        private static bool IsLowerHex(string value, int exactLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length != exactLength)
            {
                return false;
            }

            int aggregate = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool valid = (character >= '0' && character <= '9') ||
                             (character >= 'a' && character <= 'f');
                aggregate |= valid ? 0 : 1;
            }

            return aggregate == 0;
        }
    }

    internal enum FirstUserGameTestTutorialStep
    {
        Invalid = 0,
        Move = 1,
        BasicAttack = 2,
        Complete = 3
    }

    internal enum FirstUserGameTestTutorialEvidenceKind
    {
        Invalid = 0,
        MovementConfirmed = 1,
        BasicAttackConfirmed = 2
    }

    internal enum FirstUserGameTestTutorialTransitionStatus
    {
        Invalid = 0,
        Applied = 1,
        DuplicateIgnored = 2,
        Rejected = 3
    }

    internal enum FirstUserGameTestTutorialDiagnostic
    {
        None = 0,
        StateInvalid = 1,
        EvidenceInvalid = 2,
        SessionMismatch = 3,
        GenerationMismatch = 4,
        OutOfOrder = 5,
        RetainedStateUnavailable = 6,
        RetainedStateConflict = 7
    }

    internal sealed class FirstUserGameTestTutorialState
    {
        internal FirstUserGameTestTutorialState(
            string sessionId,
            string generation,
            FirstUserGameTestTutorialStep step,
            int movementConfirmationCount,
            int basicAttackConfirmationCount,
            int completionEventCount,
            int omenOfferCount)
        {
            SessionId = sessionId ?? string.Empty;
            Generation = generation ?? string.Empty;
            Step = step;
            MovementConfirmationCount = movementConfirmationCount;
            BasicAttackConfirmationCount = basicAttackConfirmationCount;
            CompletionEventCount = completionEventCount;
            OmenOfferCount = omenOfferCount;
        }

        internal string SessionId { get; }
        internal string Generation { get; }
        internal FirstUserGameTestTutorialStep Step { get; }
        internal int MovementConfirmationCount { get; }
        internal int BasicAttackConfirmationCount { get; }
        internal int CompletionEventCount { get; }
        internal int OmenOfferCount { get; }
        internal bool IsComplete => Step == FirstUserGameTestTutorialStep.Complete;
        internal bool IsOmenOffered => IsComplete && OmenOfferCount == 1;
        internal string ActiveTutorialObjectiveId =>
            Step == FirstUserGameTestTutorialStep.Move
                ? FirstUserGameTestTutorialContract.MoveObjectiveId
                : Step == FirstUserGameTestTutorialStep.BasicAttack
                    ? FirstUserGameTestTutorialContract.BasicAttackObjectiveId
                    : string.Empty;
        internal string CompletionEventId =>
            CompletionEventCount == 1
                ? FirstUserGameTestTutorialContract.TutorialCompletedEventId
                : string.Empty;
        internal string ForegroundQuestId =>
            IsOmenOffered ? FirstUserGameTestTutorialContract.OmenQuestId : string.Empty;
        internal string ForegroundQuestState =>
            IsOmenOffered ? FirstUserGameTestTutorialContract.OmenOfferedState : string.Empty;
        internal string ForegroundObjectiveId =>
            IsOmenOffered
                ? FirstUserGameTestTutorialContract.OmenOfferedObjectiveId
                : string.Empty;

        internal bool ValueEquals(FirstUserGameTestTutorialState other)
        {
            return other != null &&
                   string.Equals(SessionId, other.SessionId, StringComparison.Ordinal) &&
                   string.Equals(Generation, other.Generation, StringComparison.Ordinal) &&
                   Step == other.Step &&
                   MovementConfirmationCount == other.MovementConfirmationCount &&
                   BasicAttackConfirmationCount == other.BasicAttackConfirmationCount &&
                   CompletionEventCount == other.CompletionEventCount &&
                   OmenOfferCount == other.OmenOfferCount;
        }
    }

    internal readonly struct FirstUserGameTestTutorialEvidence
    {
        internal FirstUserGameTestTutorialEvidence(
            string sessionId,
            string generation,
            FirstUserGameTestTutorialEvidenceKind kind)
        {
            SessionId = sessionId ?? string.Empty;
            Generation = generation ?? string.Empty;
            Kind = kind;
        }

        internal string SessionId { get; }
        internal string Generation { get; }
        internal FirstUserGameTestTutorialEvidenceKind Kind { get; }
    }

    internal sealed class FirstUserGameTestTutorialTransition
    {
        internal FirstUserGameTestTutorialTransition(
            FirstUserGameTestTutorialTransitionStatus status,
            FirstUserGameTestTutorialDiagnostic diagnostic,
            FirstUserGameTestTutorialState state,
            string confirmedEventId,
            string completionEventId,
            string foregroundQuestId,
            string foregroundQuestState)
        {
            Status = status;
            Diagnostic = diagnostic;
            State = state;
            ConfirmedEventId = confirmedEventId ?? string.Empty;
            CompletionEventId = completionEventId ?? string.Empty;
            ForegroundQuestId = foregroundQuestId ?? string.Empty;
            ForegroundQuestState = foregroundQuestState ?? string.Empty;
        }

        internal FirstUserGameTestTutorialTransitionStatus Status { get; }
        internal FirstUserGameTestTutorialDiagnostic Diagnostic { get; }
        internal FirstUserGameTestTutorialState State { get; }
        internal string ConfirmedEventId { get; }
        internal string CompletionEventId { get; }
        internal string ForegroundQuestId { get; }
        internal string ForegroundQuestState { get; }
        internal bool Changed => Status == FirstUserGameTestTutorialTransitionStatus.Applied;
    }

    internal static class FirstUserGameTestTutorialPlanner
    {
        internal static bool TryCreateInitial(
            string sessionId,
            string generation,
            out FirstUserGameTestTutorialState state)
        {
            state = new FirstUserGameTestTutorialState(
                sessionId,
                generation,
                FirstUserGameTestTutorialStep.Move,
                movementConfirmationCount: 0,
                basicAttackConfirmationCount: 0,
                completionEventCount: 0,
                omenOfferCount: 0);
            if (IsValidState(state))
            {
                return true;
            }

            state = null;
            return false;
        }

        internal static FirstUserGameTestTutorialTransition Apply(
            FirstUserGameTestTutorialState current,
            FirstUserGameTestTutorialEvidence evidence)
        {
            if (!IsValidState(current))
            {
                return Reject(current, FirstUserGameTestTutorialDiagnostic.StateInvalid);
            }

            if (!FirstUserGameTestTutorialContract.IsCanonicalSessionId(evidence.SessionId) ||
                !FirstUserGameTestTutorialContract.IsCanonicalGeneration(evidence.Generation) ||
                (evidence.Kind != FirstUserGameTestTutorialEvidenceKind.MovementConfirmed &&
                 evidence.Kind != FirstUserGameTestTutorialEvidenceKind.BasicAttackConfirmed))
            {
                return Reject(current, FirstUserGameTestTutorialDiagnostic.EvidenceInvalid);
            }

            if (!string.Equals(current.SessionId, evidence.SessionId, StringComparison.Ordinal))
            {
                return Reject(current, FirstUserGameTestTutorialDiagnostic.SessionMismatch);
            }

            if (!string.Equals(current.Generation, evidence.Generation, StringComparison.Ordinal))
            {
                return Reject(current, FirstUserGameTestTutorialDiagnostic.GenerationMismatch);
            }

            if (evidence.Kind == FirstUserGameTestTutorialEvidenceKind.MovementConfirmed)
            {
                if (current.Step == FirstUserGameTestTutorialStep.Move)
                {
                    var next = new FirstUserGameTestTutorialState(
                        current.SessionId,
                        current.Generation,
                        FirstUserGameTestTutorialStep.BasicAttack,
                        movementConfirmationCount: 1,
                        basicAttackConfirmationCount: 0,
                        completionEventCount: 0,
                        omenOfferCount: 0);
                    return Applied(
                        next,
                        FirstUserGameTestTutorialContract.MovementConfirmedEventId,
                        string.Empty,
                        string.Empty,
                        string.Empty);
                }

                return Duplicate(current);
            }

            if (current.Step == FirstUserGameTestTutorialStep.Move)
            {
                return Reject(current, FirstUserGameTestTutorialDiagnostic.OutOfOrder);
            }

            if (current.Step == FirstUserGameTestTutorialStep.BasicAttack)
            {
                var next = new FirstUserGameTestTutorialState(
                    current.SessionId,
                    current.Generation,
                    FirstUserGameTestTutorialStep.Complete,
                    movementConfirmationCount: 1,
                    basicAttackConfirmationCount: 1,
                    completionEventCount: 1,
                    omenOfferCount: 1);
                return Applied(
                    next,
                    FirstUserGameTestTutorialContract.BasicAttackConfirmedEventId,
                    FirstUserGameTestTutorialContract.TutorialCompletedEventId,
                    FirstUserGameTestTutorialContract.OmenQuestId,
                    FirstUserGameTestTutorialContract.OmenOfferedState);
            }

            return Duplicate(current);
        }

        internal static bool IsValidState(FirstUserGameTestTutorialState state)
        {
            if (state == null ||
                !FirstUserGameTestTutorialContract.IsCanonicalSessionId(state.SessionId) ||
                !FirstUserGameTestTutorialContract.IsCanonicalGeneration(state.Generation))
            {
                return false;
            }

            switch (state.Step)
            {
                case FirstUserGameTestTutorialStep.Move:
                    return state.MovementConfirmationCount == 0 &&
                           state.BasicAttackConfirmationCount == 0 &&
                           state.CompletionEventCount == 0 &&
                           state.OmenOfferCount == 0;
                case FirstUserGameTestTutorialStep.BasicAttack:
                    return state.MovementConfirmationCount == 1 &&
                           state.BasicAttackConfirmationCount == 0 &&
                           state.CompletionEventCount == 0 &&
                           state.OmenOfferCount == 0;
                case FirstUserGameTestTutorialStep.Complete:
                    return state.MovementConfirmationCount == 1 &&
                           state.BasicAttackConfirmationCount == 1 &&
                           state.CompletionEventCount == 1 &&
                           state.OmenOfferCount == 1;
                default:
                    return false;
            }
        }

        private static FirstUserGameTestTutorialTransition Applied(
            FirstUserGameTestTutorialState state,
            string confirmedEventId,
            string completionEventId,
            string foregroundQuestId,
            string foregroundQuestState)
        {
            return new FirstUserGameTestTutorialTransition(
                FirstUserGameTestTutorialTransitionStatus.Applied,
                FirstUserGameTestTutorialDiagnostic.None,
                state,
                confirmedEventId,
                completionEventId,
                foregroundQuestId,
                foregroundQuestState);
        }

        private static FirstUserGameTestTutorialTransition Duplicate(
            FirstUserGameTestTutorialState state)
        {
            return new FirstUserGameTestTutorialTransition(
                FirstUserGameTestTutorialTransitionStatus.DuplicateIgnored,
                FirstUserGameTestTutorialDiagnostic.None,
                state,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static FirstUserGameTestTutorialTransition Reject(
            FirstUserGameTestTutorialState state,
            FirstUserGameTestTutorialDiagnostic diagnostic)
        {
            return new FirstUserGameTestTutorialTransition(
                FirstUserGameTestTutorialTransitionStatus.Rejected,
                diagnostic,
                state,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }
    }

    internal enum FirstUserGameTestFollowOutcome
    {
        Invalid = 0,
        Focused = 1,
        NoTarget = 2,
        Unavailable = 3
    }

    internal readonly struct FirstUserGameTestFollowResult
    {
        internal FirstUserGameTestFollowResult(
            FirstUserGameTestFollowOutcome outcome,
            string resultId)
        {
            Outcome = outcome;
            ResultId = resultId ?? string.Empty;
        }

        internal FirstUserGameTestFollowOutcome Outcome { get; }
        internal string ResultId { get; }
    }

    internal static class FirstUserGameTestFollowPlanner
    {
        internal static FirstUserGameTestFollowResult Plan(
            FirstUserGameTestTutorialState state,
            string actionId,
            bool targetAvailable)
        {
            if (!FirstUserGameTestTutorialPlanner.IsValidState(state) ||
                !state.IsOmenOffered ||
                !string.Equals(
                    actionId,
                    FirstUserGameTestTutorialContract.FollowActiveObjectiveActionId,
                    StringComparison.Ordinal))
            {
                return new FirstUserGameTestFollowResult(
                    FirstUserGameTestFollowOutcome.Unavailable,
                    FirstUserGameTestTutorialContract.ActiveObjectiveUnavailableResultId);
            }

            return targetAvailable
                ? new FirstUserGameTestFollowResult(
                    FirstUserGameTestFollowOutcome.Focused,
                    FirstUserGameTestTutorialContract.ActiveObjectiveFocusedResultId)
                : new FirstUserGameTestFollowResult(
                    FirstUserGameTestFollowOutcome.NoTarget,
                    FirstUserGameTestTutorialContract.ActiveObjectiveNoTargetResultId);
        }
    }

    internal static class FirstUserGameTestTutorialStateCodec
    {
        private const char Separator = '\n';

        internal static bool TryEncode(
            FirstUserGameTestTutorialState state,
            out string payload)
        {
            payload = string.Empty;
            if (!FirstUserGameTestTutorialPlanner.IsValidState(state))
            {
                return false;
            }

            payload = string.Join(
                Separator.ToString(),
                FirstUserGameTestTutorialContract.ContractVersion,
                state.SessionId,
                state.Generation,
                ((int)state.Step).ToString(CultureInfo.InvariantCulture),
                state.MovementConfirmationCount.ToString(CultureInfo.InvariantCulture),
                state.BasicAttackConfirmationCount.ToString(CultureInfo.InvariantCulture),
                state.CompletionEventCount.ToString(CultureInfo.InvariantCulture),
                state.OmenOfferCount.ToString(CultureInfo.InvariantCulture));
            return payload.Length <=
                   FirstUserGameTestTutorialContract.MaximumRetainedEnvelopeCharacters;
        }

        internal static bool TryDecode(
            string payload,
            out FirstUserGameTestTutorialState state)
        {
            state = null;
            if (string.IsNullOrEmpty(payload) ||
                payload.Length > FirstUserGameTestTutorialContract.MaximumRetainedEnvelopeCharacters ||
                payload.IndexOf('\r') >= 0)
            {
                return false;
            }

            string[] fields = payload.Split(Separator);
            if (fields.Length != 8 ||
                !string.Equals(
                    fields[0],
                    FirstUserGameTestTutorialContract.ContractVersion,
                    StringComparison.Ordinal) ||
                !TryCanonicalInteger(fields[3], out int step) ||
                !TryCanonicalInteger(fields[4], out int movementCount) ||
                !TryCanonicalInteger(fields[5], out int attackCount) ||
                !TryCanonicalInteger(fields[6], out int completionCount) ||
                !TryCanonicalInteger(fields[7], out int offerCount))
            {
                return false;
            }

            var candidate = new FirstUserGameTestTutorialState(
                fields[1],
                fields[2],
                (FirstUserGameTestTutorialStep)step,
                movementCount,
                attackCount,
                completionCount,
                offerCount);
            if (!FirstUserGameTestTutorialPlanner.IsValidState(candidate) ||
                !TryEncode(candidate, out string canonical) ||
                !string.Equals(payload, canonical, StringComparison.Ordinal))
            {
                return false;
            }

            state = candidate;
            return true;
        }

        private static bool TryCanonicalInteger(string value, out int parsed)
        {
            parsed = 0;
            return !string.IsNullOrEmpty(value) &&
                   int.TryParse(
                       value,
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out parsed) &&
                   string.Equals(
                       value,
                       parsed.ToString(CultureInfo.InvariantCulture),
                       StringComparison.Ordinal);
        }
    }
}
