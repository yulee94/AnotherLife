using AL.Core;

namespace AL.ChampionMode.Quests
{
    /// <summary>
    /// Catalog IDs for the playable OMEN_1 → MQ_C1_PROOF_OF_WORTH beat.
    /// Do not invent quest, NPC, or chapter IDs here.
    /// </summary>
    public static class ProofOfWorthIds
    {
        public const string OmenQuestId = "OMEN_1";
        public const string OmenOfferedState = "OFFERED";
        public const string OmenTalkState = "TALK_TO_VALERIUS";
        public const string OmenArenaState = "INVESTIGATE_SKY_CASTLE";
        public const string OmenFailedState = "FAILED";
        public const string OmenReportState = "REPORT_TO_VALERIUS";
        public const string OmenCompletedState = "COMPLETED";

        public const string OmenTalkObjectiveId = "OBJ_OMEN_1_TALK";
        public const string OmenArenaObjectiveId = "OBJ_OMEN_1_ARENA";
        public const string OmenReportObjectiveId = "OBJ_OMEN_1_REPORT";

        public const string OfferAction = "SELECT_VALERIUS";
        public const string QuestAcceptedEvent = "QUEST_ACCEPTED";
        public const string RequestArenaEvent = "REQUEST_SKY_CASTLE_ARENA";
        public const string RetryArenaEvent = "RETRY_SKY_CASTLE_ARENA";
        public const string ArenaSuccessEvent = "EVENT_SKY_CASTLE_ARENA_SUCCESS";
        public const string ArenaFailureEvent = "EVENT_SKY_CASTLE_ARENA_FAILURE";
        public const string ReportConclusionEvent = "DLG_OMEN_1_REPORT_CONCLUSION";

        public const string OfferDialogueId = "DLG_OMEN_1_OFFER";
        public const string StartDialogueId = "DLG_OMEN_1_START";
        public const string LoreDialogueId = "DLG_OMEN_1_LORE";
        public const string GoDialogueId = "DLG_OMEN_1_GO";
        public const string ArenaStartDialogueId = "DLG_OMEN_1_ARENA_START";
        public const string FailureDialogueId = "DLG_OMEN_1_FAILURE";
        public const string ReportDialogueId = "DLG_OMEN_1_REPORT";
        public const string ReportConclusionDialogueId = "DLG_OMEN_1_REPORT_CONCLUSION";

        public const string ChoiceAccept = "choice.omen1.accept";
        public const string ChoiceDecline = "choice.omen1.decline";
        public const string ChoiceInvestigate = "choice.omen1.investigate";
        public const string ChoiceAskMore = "choice.omen1.ask_more";
        public const string ChoiceDepart = "choice.omen1.depart";
        public const string ChoiceDeploy = "choice.omen1.deploy";
        public const string ChoiceRetry = "choice.omen1.retry";
        public const string ChoicePresentTear = "choice.omen1.present_tear";
        public const string ChoiceContinue = "choice.omen1.continue";

        public const string SpeakerId = "NPC_VALERIUS";
        public const string SkyCastleMarkerId = "LOCATION_SKY_CASTLE_MARKER";
        public const string ChapterUnlockId = "CH1_REALM_INTRO";

        public const string MainQuestId = "MQ_C1_PROOF_OF_WORTH";
        public const string ChapterId = "ch01_proof_of_worth";
        public const string StoneholdVariantId = "ch01_stonehold";
        public const string EldergroveVariantId = "ch01_eldergrove";
        public const string CrownlandsVariantId = "ch01_crownlands";
        public const string UmbralVariantId = "ch01_umbral";

        public const string MeetGuideObjectiveId = "OBJ_C1_MEET_REALM_GUIDE";
        public const string RestoreCovenantObjectiveId = "OBJ_C1_RESTORE_COVENANT";
        public const string FaceGuardianObjectiveId = "OBJ_C1_FACE_GUARDIAN";
        public const string AcceptMarkObjectiveId = "OBJ_C1_ACCEPT_MARK";
        public const string GuardianTrialHook = "HOOK_REALM_GUARDIAN_TRIAL";

        public static readonly string[] RealmVariantIds =
        {
            StoneholdVariantId,
            EldergroveVariantId,
            CrownlandsVariantId,
            UmbralVariantId
        };

        public static bool AutoAccept => false;

        public static string ResolveRealmVariantId(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold:
                    return StoneholdVariantId;
                case RealmId.Eldergrove:
                    return EldergroveVariantId;
                case RealmId.Crownlands:
                    return CrownlandsVariantId;
                case RealmId.Umbral:
                    return UmbralVariantId;
                default:
                    return string.Empty;
            }
        }

        public static bool IsRealmVariantId(string id)
        {
            return string.Equals(id, StoneholdVariantId, System.StringComparison.Ordinal) ||
                   string.Equals(id, EldergroveVariantId, System.StringComparison.Ordinal) ||
                   string.Equals(id, CrownlandsVariantId, System.StringComparison.Ordinal) ||
                   string.Equals(id, UmbralVariantId, System.StringComparison.Ordinal);
        }
    }
}
