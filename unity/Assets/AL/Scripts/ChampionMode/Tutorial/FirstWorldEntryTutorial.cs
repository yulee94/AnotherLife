using System;
using AL.ChampionMode.Control;
using AL.Narrative.Nvs01.Contracts;

namespace AL.ChampionMode.Tutorial
{
    /// <summary>
    /// Production TUTORIAL_FIRST_WORLD_ENTRY contract. Spine IDs only; not a quest.
    /// Camera look and one interact are teaching beats, not new objective IDs.
    /// Completion foregrounds OMEN_1 in OFFERED and never accepts it.
    /// </summary>
    public static class FirstWorldEntryTutorialIds
    {
        public const string TutorialId = "TUTORIAL_FIRST_WORLD_ENTRY";
        public const string MoveObjectiveId = "OBJ_TUTORIAL_FIRST_WORLD_ENTRY_MOVE";
        public const string BasicAttackObjectiveId = "OBJ_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK";
        public const string MovementConfirmedEventId = "EVENT_TUTORIAL_FIRST_WORLD_ENTRY_MOVEMENT_CONFIRMED";
        public const string BasicAttackConfirmedEventId = "EVENT_TUTORIAL_FIRST_WORLD_ENTRY_BASIC_ATTACK_CONFIRMED";
        public const string CompletedEventId = "EVENT_TUTORIAL_FIRST_WORLD_ENTRY_COMPLETED";
        public const string FollowActionId = "ACTION_FOLLOW_ACTIVE_OBJECTIVE";
        public const string FollowFocusedResultId = "RESULT_ACTIVE_OBJECTIVE_FOCUSED";
        public const string FollowNoTargetResultId = "RESULT_ACTIVE_OBJECTIVE_NO_TARGET";
        public const string FollowUnavailableResultId = "RESULT_ACTIVE_OBJECTIVE_UNAVAILABLE";
        public const string OmenQuestId = Nvs01CatalogContract.QuestId;
        public const string OmenOfferedState = "OFFERED";
        public const string OmenTalkObjectiveId = "OBJ_OMEN_1_TALK";
        public const string OmenOfferAction = "SELECT_VALERIUS";
        public const string OmenTitleKey = "quest.omen1.title";
        public const string OmenTalkKey = "objective.omen1.talk";
        public const string OmenOfferKey = "dialogue.omen1.offer";
        public const string OmenSpeakerKey = "npc.valerius.name";
        public const string TitleKey = "tutorial.first_world_entry.title";
        public const string MoveKey = "objective.tutorial.first_world_entry.move";
        public const string AttackKey = "objective.tutorial.first_world_entry.basic_attack";
    }

    public enum FirstWorldEntryTutorialStep
    {
        Invalid = 0,
        Move = 1,
        BasicAttack = 2,
        Complete = 3
    }

    public enum FirstWorldEntryTeachingBeat
    {
        Invalid = 0,
        CameraLook = 1,
        Move = 2,
        Interact = 3,
        BasicAttack = 4,
        OmenOffered = 5
    }

    public enum FirstWorldEntryEvidenceKind
    {
        Invalid = 0,
        MovementConfirmed = 1,
        BasicAttackConfirmed = 2
    }

    public enum FirstWorldEntryTransitionStatus
    {
        Invalid = 0,
        Applied = 1,
        DuplicateIgnored = 2,
        Rejected = 3
    }

    public sealed class FirstWorldEntryTutorialState
    {
        public FirstWorldEntryTutorialState(
            FirstWorldEntryTutorialStep step,
            FirstWorldEntryTeachingBeat teachingBeat,
            int movementConfirmationCount,
            int basicAttackConfirmationCount,
            int completionEventCount,
            int omenOfferCount,
            bool omenAccepted,
            bool blockTaught)
        {
            Step = step;
            TeachingBeat = teachingBeat;
            MovementConfirmationCount = movementConfirmationCount;
            BasicAttackConfirmationCount = basicAttackConfirmationCount;
            CompletionEventCount = completionEventCount;
            OmenOfferCount = omenOfferCount;
            OmenAccepted = omenAccepted;
            BlockTaught = blockTaught;
        }

        public FirstWorldEntryTutorialStep Step { get; }
        public FirstWorldEntryTeachingBeat TeachingBeat { get; }
        public int MovementConfirmationCount { get; }
        public int BasicAttackConfirmationCount { get; }
        public int CompletionEventCount { get; }
        public int OmenOfferCount { get; }
        public bool OmenAccepted { get; }
        public bool BlockTaught { get; }
        public bool IsComplete => Step == FirstWorldEntryTutorialStep.Complete;
        public bool IsOmenOffered => IsComplete && OmenOfferCount == 1 && !OmenAccepted;

        public string ActiveTutorialObjectiveId
        {
            get
            {
                if (Step == FirstWorldEntryTutorialStep.Move)
                {
                    return FirstWorldEntryTutorialIds.MoveObjectiveId;
                }

                if (Step == FirstWorldEntryTutorialStep.BasicAttack)
                {
                    return FirstWorldEntryTutorialIds.BasicAttackObjectiveId;
                }

                return string.Empty;
            }
        }

        public string ForegroundQuestId =>
            IsOmenOffered ? FirstWorldEntryTutorialIds.OmenQuestId : string.Empty;

        public string ForegroundQuestState =>
            IsOmenOffered ? FirstWorldEntryTutorialIds.OmenOfferedState : string.Empty;

        public string ForegroundObjectiveId =>
            IsOmenOffered ? FirstWorldEntryTutorialIds.OmenTalkObjectiveId : string.Empty;
    }

    public sealed class FirstWorldEntryTutorialTransition
    {
        public FirstWorldEntryTutorialTransition(
            FirstWorldEntryTransitionStatus status,
            FirstWorldEntryTutorialState state,
            string confirmedEventId,
            string completionEventId)
        {
            Status = status;
            State = state;
            ConfirmedEventId = confirmedEventId ?? string.Empty;
            CompletionEventId = completionEventId ?? string.Empty;
        }

        public FirstWorldEntryTransitionStatus Status { get; }
        public FirstWorldEntryTutorialState State { get; }
        public string ConfirmedEventId { get; }
        public string CompletionEventId { get; }
        public bool Changed => Status == FirstWorldEntryTransitionStatus.Applied;
    }

    public static class FirstWorldEntryTutorialPlanner
    {
        public static FirstWorldEntryTutorialState CreateInitial()
        {
            return new FirstWorldEntryTutorialState(
                FirstWorldEntryTutorialStep.Move,
                FirstWorldEntryTeachingBeat.CameraLook,
                movementConfirmationCount: 0,
                basicAttackConfirmationCount: 0,
                completionEventCount: 0,
                omenOfferCount: 0,
                omenAccepted: false,
                blockTaught: false);
        }

        public static bool IsValid(FirstWorldEntryTutorialState state)
        {
            if (state == null || state.OmenAccepted)
            {
                return false;
            }

            switch (state.Step)
            {
                case FirstWorldEntryTutorialStep.Move:
                    return state.MovementConfirmationCount == 0 &&
                           state.BasicAttackConfirmationCount == 0 &&
                           state.CompletionEventCount == 0 &&
                           state.OmenOfferCount == 0 &&
                           (state.TeachingBeat == FirstWorldEntryTeachingBeat.CameraLook ||
                            state.TeachingBeat == FirstWorldEntryTeachingBeat.Move);
                case FirstWorldEntryTutorialStep.BasicAttack:
                    return state.MovementConfirmationCount == 1 &&
                           state.BasicAttackConfirmationCount == 0 &&
                           state.CompletionEventCount == 0 &&
                           state.OmenOfferCount == 0 &&
                           (state.TeachingBeat == FirstWorldEntryTeachingBeat.Interact ||
                            state.TeachingBeat == FirstWorldEntryTeachingBeat.BasicAttack);
                case FirstWorldEntryTutorialStep.Complete:
                    return state.MovementConfirmationCount == 1 &&
                           state.BasicAttackConfirmationCount == 1 &&
                           state.CompletionEventCount == 1 &&
                           state.OmenOfferCount == 1 &&
                           state.TeachingBeat == FirstWorldEntryTeachingBeat.OmenOffered;
                default:
                    return false;
            }
        }

        public static FirstWorldEntryTutorialTransition Apply(
            FirstWorldEntryTutorialState current,
            FirstWorldEntryEvidenceKind kind)
        {
            if (!IsValid(current))
            {
                return Reject(current);
            }

            if (kind == FirstWorldEntryEvidenceKind.MovementConfirmed)
            {
                if (current.Step == FirstWorldEntryTutorialStep.Move &&
                    current.TeachingBeat == FirstWorldEntryTeachingBeat.Move)
                {
                    var next = new FirstWorldEntryTutorialState(
                        FirstWorldEntryTutorialStep.BasicAttack,
                        FirstWorldEntryTeachingBeat.Interact,
                        movementConfirmationCount: 1,
                        basicAttackConfirmationCount: 0,
                        completionEventCount: 0,
                        omenOfferCount: 0,
                        omenAccepted: false,
                        blockTaught: current.BlockTaught);
                    return new FirstWorldEntryTutorialTransition(
                        FirstWorldEntryTransitionStatus.Applied,
                        next,
                        FirstWorldEntryTutorialIds.MovementConfirmedEventId,
                        string.Empty);
                }

                return Duplicate(current);
            }

            if (kind == FirstWorldEntryEvidenceKind.BasicAttackConfirmed)
            {
                if (current.Step == FirstWorldEntryTutorialStep.Move)
                {
                    return Reject(current);
                }

                if (current.Step == FirstWorldEntryTutorialStep.BasicAttack &&
                    current.TeachingBeat == FirstWorldEntryTeachingBeat.BasicAttack)
                {
                    var next = new FirstWorldEntryTutorialState(
                        FirstWorldEntryTutorialStep.Complete,
                        FirstWorldEntryTeachingBeat.OmenOffered,
                        movementConfirmationCount: 1,
                        basicAttackConfirmationCount: 1,
                        completionEventCount: 1,
                        omenOfferCount: 1,
                        omenAccepted: false,
                        blockTaught: current.BlockTaught);
                    return new FirstWorldEntryTutorialTransition(
                        FirstWorldEntryTransitionStatus.Applied,
                        next,
                        FirstWorldEntryTutorialIds.BasicAttackConfirmedEventId,
                        FirstWorldEntryTutorialIds.CompletedEventId);
                }

                return Duplicate(current);
            }

            return Reject(current);
        }

        public static FirstWorldEntryTutorialState AdvanceTeaching(
            FirstWorldEntryTutorialState current,
            FirstWorldEntryTeachingBeat nextBeat,
            bool blockTaught)
        {
            if (!IsValid(current))
            {
                return current;
            }

            return new FirstWorldEntryTutorialState(
                current.Step,
                nextBeat,
                current.MovementConfirmationCount,
                current.BasicAttackConfirmationCount,
                current.CompletionEventCount,
                current.OmenOfferCount,
                omenAccepted: false,
                blockTaught: current.BlockTaught || blockTaught);
        }

        public static string Follow(FirstWorldEntryTutorialState state, bool targetAvailable)
        {
            if (!IsValid(state) || !state.IsOmenOffered)
            {
                return FirstWorldEntryTutorialIds.FollowUnavailableResultId;
            }

            return targetAvailable
                ? FirstWorldEntryTutorialIds.FollowFocusedResultId
                : FirstWorldEntryTutorialIds.FollowNoTargetResultId;
        }

        public static FirstWorldEntryTutorialState RejectAccept(FirstWorldEntryTutorialState current)
        {
            if (!IsValid(current) || !current.IsOmenOffered)
            {
                return current;
            }

            return new FirstWorldEntryTutorialState(
                current.Step,
                current.TeachingBeat,
                current.MovementConfirmationCount,
                current.BasicAttackConfirmationCount,
                current.CompletionEventCount,
                current.OmenOfferCount,
                omenAccepted: false,
                blockTaught: current.BlockTaught);
        }

        private static FirstWorldEntryTutorialTransition Duplicate(FirstWorldEntryTutorialState state)
        {
            return new FirstWorldEntryTutorialTransition(
                FirstWorldEntryTransitionStatus.DuplicateIgnored,
                state,
                string.Empty,
                string.Empty);
        }

        private static FirstWorldEntryTutorialTransition Reject(FirstWorldEntryTutorialState state)
        {
            return new FirstWorldEntryTutorialTransition(
                FirstWorldEntryTransitionStatus.Rejected,
                state,
                string.Empty,
                string.Empty);
        }
    }

    public static class FirstWorldEntryTutorialEvidence
    {
        public const float LookThreshold = 12f;
        public const float MoveThreshold = 0.35f;
        public const float HorizontalDisplacementThreshold = 0.01f;

        public static bool IsLookAccepted(float lookMagnitude)
        {
            return lookMagnitude >= LookThreshold;
        }

        public static bool IsMoveAccepted(ChampionMovementReceipt receipt)
        {
            return receipt.RequestedInput.magnitude >= MoveThreshold &&
                   receipt.WasGrounded &&
                   receipt.IsGrounded &&
                   receipt.HorizontalDisplacement >= HorizontalDisplacementThreshold;
        }
    }

    public static class FirstWorldEntryTutorialCopy
    {
        public const string TemporaryBadge = "TEMPORARY";

        public const string Title = "Champion's First Steps";
        public const string CameraPrompt = "Look around with the mouse or right stick.";
        public const string MovePrompt = "Walk with WASD.";
        public const string InteractPrompt =
            "Approach Captain Valerius and press [F] to speak.";
        public const string AttackPrompt = "Strike once with Left Mouse.";
        public const string OmenOfferTitle = "The First Signal";
        public const string OmenTalk = "Speak with Captain Valerius.";
        public const string OmenOffer =
            "The Veil Watch has detected a strange resonance above the Sky Castle. Will you hear my report?";
        public const string OmenSpeaker = "Captain Valerius";
        public const string OmenOfferedHint =
            "Speak with Captain Valerius at the gold beacon when you are ready.";
        public const string FollowLabel = "Mark the signal";
        public const string DeferLabel = "Not yet.";

        public static string ForBeat(FirstWorldEntryTeachingBeat beat)
        {
            switch (beat)
            {
                case FirstWorldEntryTeachingBeat.CameraLook:
                    return CameraPrompt;
                case FirstWorldEntryTeachingBeat.Move:
                    return MovePrompt;
                case FirstWorldEntryTeachingBeat.Interact:
                    return InteractPrompt;
                case FirstWorldEntryTeachingBeat.BasicAttack:
                    return AttackPrompt;
                case FirstWorldEntryTeachingBeat.OmenOffered:
                    return OmenOfferedHint;
                default:
                    return Title;
            }
        }

        public static bool IsTemporary(string copy)
        {
            return !string.IsNullOrEmpty(copy) &&
                   copy.IndexOf(TemporaryBadge, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
