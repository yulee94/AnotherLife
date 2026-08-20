using AL.Core;

namespace AL.ChampionMode.Quests
{
    public enum ProofOfWorthPhase
    {
        Invalid = 0,
        OmenOffered = 1,
        OmenTalk = 2,
        OmenArena = 3,
        OmenFailed = 4,
        OmenReport = 5,
        C1MeetGuide = 6,
        C1RestoreCovenant = 7,
        C1FaceGuardian = 8,
        C1AcceptMark = 9,
        LordshipGranted = 10
    }

    public enum ProofOfWorthCommand
    {
        Invalid = 0,
        SelectValerius = 1,
        AcceptOffer = 2,
        DeclineOffer = 3,
        Investigate = 4,
        AskMore = 5,
        Depart = 6,
        DeployChampion = 7,
        ArenaSuccess = 8,
        ArenaFailure = 9,
        RetryArena = 10,
        PresentTear = 11,
        ConcludeReport = 12,
        MeetRealmGuide = 13,
        RestoreCovenant = 14,
        GuardianDefeated = 15,
        AcceptMark = 16
    }

    public enum ProofOfWorthStatus
    {
        Invalid = 0,
        Applied = 1,
        DuplicateIgnored = 2,
        Rejected = 3
    }

    public sealed class ProofOfWorthState
    {
        public ProofOfWorthState(
            ProofOfWorthPhase phase,
            string questId,
            string questStateId,
            string objectiveId,
            string dialogueId,
            string lastEventId,
            RealmId realm,
            string chapterVariantId,
            bool omenAccepted,
            bool autoAccept)
        {
            Phase = phase;
            QuestId = questId ?? string.Empty;
            QuestStateId = questStateId ?? string.Empty;
            ObjectiveId = objectiveId ?? string.Empty;
            DialogueId = dialogueId ?? string.Empty;
            LastEventId = lastEventId ?? string.Empty;
            Realm = realm;
            ChapterVariantId = chapterVariantId ?? string.Empty;
            OmenAccepted = omenAccepted;
            AutoAccept = autoAccept;
        }

        public ProofOfWorthPhase Phase { get; }
        public string QuestId { get; }
        public string QuestStateId { get; }
        public string ObjectiveId { get; }
        public string DialogueId { get; }
        public string LastEventId { get; }
        public RealmId Realm { get; }
        public string ChapterVariantId { get; }
        public bool OmenAccepted { get; }
        public bool AutoAccept { get; }
        public bool LordshipGranted => Phase == ProofOfWorthPhase.LordshipGranted;
        public bool IsOmenOffered =>
            Phase == ProofOfWorthPhase.OmenOffered && !OmenAccepted && !AutoAccept;
    }

    public sealed class ProofOfWorthTransition
    {
        public ProofOfWorthTransition(ProofOfWorthStatus status, ProofOfWorthState state)
        {
            Status = status;
            State = state;
        }

        public ProofOfWorthStatus Status { get; }
        public ProofOfWorthState State { get; }
        public bool Changed => Status == ProofOfWorthStatus.Applied;
    }

    public static class ProofOfWorthPlanner
    {
        public static ProofOfWorthState CreateOffered(RealmId realm)
        {
            return new ProofOfWorthState(
                ProofOfWorthPhase.OmenOffered,
                ProofOfWorthIds.OmenQuestId,
                ProofOfWorthIds.OmenOfferedState,
                ProofOfWorthIds.OmenTalkObjectiveId,
                ProofOfWorthIds.OfferDialogueId,
                string.Empty,
                realm,
                string.Empty,
                omenAccepted: false,
                autoAccept: ProofOfWorthIds.AutoAccept);
        }

        public static bool IsValid(ProofOfWorthState state)
        {
            if (state == null || state.AutoAccept)
            {
                return false;
            }

            switch (state.Phase)
            {
                case ProofOfWorthPhase.OmenOffered:
                    return !state.OmenAccepted &&
                           state.QuestId == ProofOfWorthIds.OmenQuestId &&
                           state.QuestStateId == ProofOfWorthIds.OmenOfferedState &&
                           state.ObjectiveId == ProofOfWorthIds.OmenTalkObjectiveId;
                case ProofOfWorthPhase.OmenTalk:
                    return state.OmenAccepted &&
                           state.QuestStateId == ProofOfWorthIds.OmenTalkState;
                case ProofOfWorthPhase.OmenArena:
                    return state.OmenAccepted &&
                           state.QuestStateId == ProofOfWorthIds.OmenArenaState &&
                           state.ObjectiveId == ProofOfWorthIds.OmenArenaObjectiveId;
                case ProofOfWorthPhase.OmenFailed:
                    return state.OmenAccepted &&
                           state.QuestStateId == ProofOfWorthIds.OmenFailedState;
                case ProofOfWorthPhase.OmenReport:
                    return state.OmenAccepted &&
                           state.QuestStateId == ProofOfWorthIds.OmenReportState &&
                           state.ObjectiveId == ProofOfWorthIds.OmenReportObjectiveId;
                case ProofOfWorthPhase.C1MeetGuide:
                    return state.QuestId == ProofOfWorthIds.MainQuestId &&
                           state.ObjectiveId == ProofOfWorthIds.MeetGuideObjectiveId;
                case ProofOfWorthPhase.C1RestoreCovenant:
                    return state.QuestId == ProofOfWorthIds.MainQuestId &&
                           state.ObjectiveId == ProofOfWorthIds.RestoreCovenantObjectiveId;
                case ProofOfWorthPhase.C1FaceGuardian:
                    return state.QuestId == ProofOfWorthIds.MainQuestId &&
                           state.ObjectiveId == ProofOfWorthIds.FaceGuardianObjectiveId;
                case ProofOfWorthPhase.C1AcceptMark:
                    return state.QuestId == ProofOfWorthIds.MainQuestId &&
                           state.ObjectiveId == ProofOfWorthIds.AcceptMarkObjectiveId;
                case ProofOfWorthPhase.LordshipGranted:
                    return ProofOfWorthIds.IsRealmVariantId(state.ChapterVariantId);
                default:
                    return false;
            }
        }

        public static ProofOfWorthTransition Apply(ProofOfWorthState current, ProofOfWorthCommand command)
        {
            if (!IsValid(current) || command == ProofOfWorthCommand.Invalid)
            {
                return Reject(current);
            }

            switch (command)
            {
                case ProofOfWorthCommand.SelectValerius:
                    return ApplySelectValerius(current);
                case ProofOfWorthCommand.AcceptOffer:
                    return ApplyAcceptOffer(current);
                case ProofOfWorthCommand.DeclineOffer:
                    return current.Phase == ProofOfWorthPhase.OmenOffered
                        ? Duplicate(current)
                        : Reject(current);
                case ProofOfWorthCommand.Investigate:
                    return ApplyTalkChoice(current, ProofOfWorthIds.GoDialogueId, ProofOfWorthIds.ChoiceInvestigate);
                case ProofOfWorthCommand.AskMore:
                    return ApplyTalkChoice(current, ProofOfWorthIds.LoreDialogueId, ProofOfWorthIds.ChoiceAskMore);
                case ProofOfWorthCommand.Depart:
                    return ApplyTalkChoice(current, ProofOfWorthIds.GoDialogueId, ProofOfWorthIds.ChoiceDepart);
                case ProofOfWorthCommand.DeployChampion:
                    return ApplyDeploy(current);
                case ProofOfWorthCommand.ArenaSuccess:
                    return ApplyArenaSuccess(current);
                case ProofOfWorthCommand.ArenaFailure:
                    return ApplyArenaFailure(current);
                case ProofOfWorthCommand.RetryArena:
                    return ApplyRetry(current);
                case ProofOfWorthCommand.PresentTear:
                    return ApplyPresentTear(current);
                case ProofOfWorthCommand.ConcludeReport:
                    return ApplyConcludeReport(current);
                case ProofOfWorthCommand.MeetRealmGuide:
                    return AdvanceC1(
                        current,
                        ProofOfWorthPhase.C1MeetGuide,
                        ProofOfWorthPhase.C1RestoreCovenant,
                        ProofOfWorthIds.RestoreCovenantObjectiveId,
                        ProofOfWorthIds.MeetGuideObjectiveId);
                case ProofOfWorthCommand.RestoreCovenant:
                    return AdvanceC1(
                        current,
                        ProofOfWorthPhase.C1RestoreCovenant,
                        ProofOfWorthPhase.C1FaceGuardian,
                        ProofOfWorthIds.FaceGuardianObjectiveId,
                        ProofOfWorthIds.RestoreCovenantObjectiveId);
                case ProofOfWorthCommand.GuardianDefeated:
                    return AdvanceC1(
                        current,
                        ProofOfWorthPhase.C1FaceGuardian,
                        ProofOfWorthPhase.C1AcceptMark,
                        ProofOfWorthIds.AcceptMarkObjectiveId,
                        ProofOfWorthIds.GuardianTrialHook);
                case ProofOfWorthCommand.AcceptMark:
                    return ApplyAcceptMark(current);
                default:
                    return Reject(current);
            }
        }

        private static ProofOfWorthTransition ApplySelectValerius(ProofOfWorthState current)
        {
            if (current.Phase == ProofOfWorthPhase.OmenOffered)
            {
                if (current.DialogueId == ProofOfWorthIds.OfferDialogueId)
                {
                    return Duplicate(current);
                }

                return Applied(WithDialogue(current, ProofOfWorthIds.OfferDialogueId, ProofOfWorthIds.OfferAction));
            }

            if (current.Phase == ProofOfWorthPhase.OmenReport)
            {
                if (current.DialogueId == ProofOfWorthIds.ReportDialogueId ||
                    current.DialogueId == ProofOfWorthIds.ReportConclusionDialogueId)
                {
                    return Duplicate(current);
                }

                return Applied(WithDialogue(current, ProofOfWorthIds.ReportDialogueId, ProofOfWorthIds.OfferAction));
            }

            return Reject(current);
        }

        private static ProofOfWorthTransition ApplyAcceptOffer(ProofOfWorthState current)
        {
            if (current.Phase != ProofOfWorthPhase.OmenOffered)
            {
                return current.OmenAccepted ? Duplicate(current) : Reject(current);
            }

            return Applied(new ProofOfWorthState(
                ProofOfWorthPhase.OmenTalk,
                ProofOfWorthIds.OmenQuestId,
                ProofOfWorthIds.OmenTalkState,
                ProofOfWorthIds.OmenTalkObjectiveId,
                ProofOfWorthIds.StartDialogueId,
                ProofOfWorthIds.QuestAcceptedEvent,
                current.Realm,
                string.Empty,
                omenAccepted: true,
                autoAccept: false));
        }

        private static ProofOfWorthTransition ApplyTalkChoice(
            ProofOfWorthState current,
            string nextDialogueId,
            string choiceKey)
        {
            if (current.Phase != ProofOfWorthPhase.OmenTalk)
            {
                return Reject(current);
            }

            bool fromStart = current.DialogueId == ProofOfWorthIds.StartDialogueId &&
                             (choiceKey == ProofOfWorthIds.ChoiceInvestigate ||
                              choiceKey == ProofOfWorthIds.ChoiceAskMore);
            bool fromLore = current.DialogueId == ProofOfWorthIds.LoreDialogueId &&
                            choiceKey == ProofOfWorthIds.ChoiceDepart;
            if (!fromStart && !fromLore)
            {
                return current.DialogueId == nextDialogueId ? Duplicate(current) : Reject(current);
            }

            return Applied(WithDialogue(current, nextDialogueId, choiceKey));
        }

        private static ProofOfWorthTransition ApplyDeploy(ProofOfWorthState current)
        {
            if (current.Phase != ProofOfWorthPhase.OmenTalk ||
                current.DialogueId != ProofOfWorthIds.GoDialogueId)
            {
                return current.Phase == ProofOfWorthPhase.OmenArena ? Duplicate(current) : Reject(current);
            }

            return Applied(new ProofOfWorthState(
                ProofOfWorthPhase.OmenArena,
                ProofOfWorthIds.OmenQuestId,
                ProofOfWorthIds.OmenArenaState,
                ProofOfWorthIds.OmenArenaObjectiveId,
                ProofOfWorthIds.ArenaStartDialogueId,
                ProofOfWorthIds.RequestArenaEvent,
                current.Realm,
                string.Empty,
                omenAccepted: true,
                autoAccept: false));
        }

        private static ProofOfWorthTransition ApplyArenaSuccess(ProofOfWorthState current)
        {
            if (current.Phase != ProofOfWorthPhase.OmenArena)
            {
                return current.Phase == ProofOfWorthPhase.OmenReport ? Duplicate(current) : Reject(current);
            }

            return Applied(new ProofOfWorthState(
                ProofOfWorthPhase.OmenReport,
                ProofOfWorthIds.OmenQuestId,
                ProofOfWorthIds.OmenReportState,
                ProofOfWorthIds.OmenReportObjectiveId,
                string.Empty,
                ProofOfWorthIds.ArenaSuccessEvent,
                current.Realm,
                string.Empty,
                omenAccepted: true,
                autoAccept: false));
        }

        private static ProofOfWorthTransition ApplyArenaFailure(ProofOfWorthState current)
        {
            if (current.Phase != ProofOfWorthPhase.OmenArena)
            {
                return current.Phase == ProofOfWorthPhase.OmenFailed ? Duplicate(current) : Reject(current);
            }

            return Applied(new ProofOfWorthState(
                ProofOfWorthPhase.OmenFailed,
                ProofOfWorthIds.OmenQuestId,
                ProofOfWorthIds.OmenFailedState,
                ProofOfWorthIds.OmenArenaObjectiveId,
                ProofOfWorthIds.FailureDialogueId,
                ProofOfWorthIds.ArenaFailureEvent,
                current.Realm,
                string.Empty,
                omenAccepted: true,
                autoAccept: false));
        }

        private static ProofOfWorthTransition ApplyRetry(ProofOfWorthState current)
        {
            if (current.Phase != ProofOfWorthPhase.OmenFailed)
            {
                return current.Phase == ProofOfWorthPhase.OmenArena ? Duplicate(current) : Reject(current);
            }

            return Applied(new ProofOfWorthState(
                ProofOfWorthPhase.OmenArena,
                ProofOfWorthIds.OmenQuestId,
                ProofOfWorthIds.OmenArenaState,
                ProofOfWorthIds.OmenArenaObjectiveId,
                ProofOfWorthIds.ArenaStartDialogueId,
                ProofOfWorthIds.RetryArenaEvent,
                current.Realm,
                string.Empty,
                omenAccepted: true,
                autoAccept: false));
        }

        private static ProofOfWorthTransition ApplyPresentTear(ProofOfWorthState current)
        {
            if (current.Phase != ProofOfWorthPhase.OmenReport ||
                current.DialogueId != ProofOfWorthIds.ReportDialogueId)
            {
                return Reject(current);
            }

            return Applied(WithDialogue(
                current,
                ProofOfWorthIds.ReportConclusionDialogueId,
                ProofOfWorthIds.ChoicePresentTear));
        }

        private static ProofOfWorthTransition ApplyConcludeReport(ProofOfWorthState current)
        {
            if (current.Phase != ProofOfWorthPhase.OmenReport ||
                current.DialogueId != ProofOfWorthIds.ReportConclusionDialogueId)
            {
                return current.Phase == ProofOfWorthPhase.C1MeetGuide ? Duplicate(current) : Reject(current);
            }

            return Applied(new ProofOfWorthState(
                ProofOfWorthPhase.C1MeetGuide,
                ProofOfWorthIds.MainQuestId,
                ProofOfWorthIds.OmenCompletedState,
                ProofOfWorthIds.MeetGuideObjectiveId,
                string.Empty,
                ProofOfWorthIds.ReportConclusionEvent,
                current.Realm,
                string.Empty,
                omenAccepted: true,
                autoAccept: false));
        }

        private static ProofOfWorthTransition AdvanceC1(
            ProofOfWorthState current,
            ProofOfWorthPhase expected,
            ProofOfWorthPhase next,
            string nextObjectiveId,
            string eventId)
        {
            if (current.Phase != expected)
            {
                return current.Phase == next ? Duplicate(current) : Reject(current);
            }

            return Applied(new ProofOfWorthState(
                next,
                ProofOfWorthIds.MainQuestId,
                ProofOfWorthIds.OmenCompletedState,
                nextObjectiveId,
                string.Empty,
                eventId,
                current.Realm,
                string.Empty,
                omenAccepted: true,
                autoAccept: false));
        }

        private static ProofOfWorthTransition ApplyAcceptMark(ProofOfWorthState current)
        {
            if (current.Phase != ProofOfWorthPhase.C1AcceptMark)
            {
                return current.LordshipGranted ? Duplicate(current) : Reject(current);
            }

            string variantId = ProofOfWorthIds.ResolveRealmVariantId(current.Realm);
            if (!ProofOfWorthIds.IsRealmVariantId(variantId))
            {
                return Reject(current);
            }

            return Applied(new ProofOfWorthState(
                ProofOfWorthPhase.LordshipGranted,
                ProofOfWorthIds.MainQuestId,
                ProofOfWorthIds.OmenCompletedState,
                ProofOfWorthIds.AcceptMarkObjectiveId,
                string.Empty,
                ProofOfWorthIds.AcceptMarkObjectiveId,
                current.Realm,
                variantId,
                omenAccepted: true,
                autoAccept: false));
        }

        private static ProofOfWorthState WithDialogue(
            ProofOfWorthState current,
            string dialogueId,
            string eventId)
        {
            return new ProofOfWorthState(
                current.Phase,
                current.QuestId,
                current.QuestStateId,
                current.ObjectiveId,
                dialogueId,
                eventId,
                current.Realm,
                current.ChapterVariantId,
                current.OmenAccepted,
                autoAccept: false);
        }

        private static ProofOfWorthTransition Applied(ProofOfWorthState state)
        {
            return new ProofOfWorthTransition(ProofOfWorthStatus.Applied, state);
        }

        private static ProofOfWorthTransition Duplicate(ProofOfWorthState state)
        {
            return new ProofOfWorthTransition(ProofOfWorthStatus.DuplicateIgnored, state);
        }

        private static ProofOfWorthTransition Reject(ProofOfWorthState state)
        {
            return new ProofOfWorthTransition(ProofOfWorthStatus.Rejected, state);
        }
    }
}
