namespace AL.ChampionMode.Quests
{
    /// <summary>
    /// Authored OMEN_1 / C1 copy only. Overlay chrome is labelled TEMPORARY.
    /// </summary>
    public static class ProofOfWorthCopy
    {
        public const string TemporaryBadge = "TEMPORARY";

        public const string OmenTitle = "The First Signal";
        public const string OmenDescription =
            "Investigate a celestial disturbance at the Sky Castle and report what you recover.";
        public const string SpeakerName = "Captain Valerius";
        public const string SpeakerRole = "Veil Watch military liaison";
        public const string OfferBody =
            "The Veil Watch has detected a strange resonance above the Sky Castle. Will you hear my report?";
        public const string StartBody =
            "The air itself is trembling. Whatever waits there may threaten every realm, but we still have time to understand it.";
        public const string LoreBody =
            "Our oldest records call it an opening of the Veil—not an ending, but a warning that the world is changing.";
        public const string GoBody =
            "The Sky Castle marker is ready. When you are prepared, deploy your Champion and investigate.";
        public const string ArenaStartBody =
            "Go carefully. Return with whatever truth you can recover.";
        public const string FailureBody =
            "You made it back—that matters more than one failed attempt. Regroup, and return when you are ready.";
        public const string ReportBody =
            "You carry the same light our observers saw. Let me examine it.";
        public const string ReportConclusionBody =
            "A Celestial Tear... and it answers to you. Keep it. The Veil Watch will prepare your realm for what comes next.";

        public const string ChoiceAccept = "Tell me what happened.";
        public const string ChoiceDecline = "Not yet.";
        public const string ChoiceInvestigate = "I will investigate personally.";
        public const string ChoiceAskMore = "What do the old records say?";
        public const string ChoiceDepart = "Then I will go.";
        public const string ChoiceDeploy = "Deploy Champion.";
        public const string ChoiceRetry = "I will try again.";
        public const string ChoicePresentTear = "Present the Celestial Tear.";
        public const string ChoiceContinue = "Prepare the realm.";

        public const string OmenTalkObjective = "Speak with Captain Valerius.";
        public const string OmenArenaObjective =
            "Deploy your Champion and investigate the Sky Castle anomaly.";
        public const string OmenReportObjective = "Present the Celestial Tear to Valerius.";

        public const string C1Title = "Proof of Worth";
        public const string C1MeetGuide =
            "Meet the realm guide who interprets the Celestial Tear's response.";
        public const string C1RestoreCovenant =
            "Restore the damaged covenant site without sacrificing its keepers.";
        public const string C1FaceGuardian = "Complete the realm guardian trial.";
        public const string C1AcceptMark = "Accept the covenant mark and its duty.";

        public const string GuideSubject = "Realm Guide";
        public const string CovenantSiteSubject = "Covenant Site";
        public const string SkyCastleSubject = "Sky Castle marker";
        public const string OverlayChrome = "TEMPORARY — first-session quest overlay";

        public static string DialogueBody(string dialogueId)
        {
            switch (dialogueId)
            {
                case ProofOfWorthIds.OfferDialogueId:
                    return OfferBody;
                case ProofOfWorthIds.StartDialogueId:
                    return StartBody;
                case ProofOfWorthIds.LoreDialogueId:
                    return LoreBody;
                case ProofOfWorthIds.GoDialogueId:
                    return GoBody;
                case ProofOfWorthIds.ArenaStartDialogueId:
                    return ArenaStartBody;
                case ProofOfWorthIds.FailureDialogueId:
                    return FailureBody;
                case ProofOfWorthIds.ReportDialogueId:
                    return ReportBody;
                case ProofOfWorthIds.ReportConclusionDialogueId:
                    return ReportConclusionBody;
                default:
                    return string.Empty;
            }
        }

        public static string ObjectiveText(ProofOfWorthState state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            switch (state.Phase)
            {
                case ProofOfWorthPhase.OmenOffered:
                case ProofOfWorthPhase.OmenTalk:
                    return OmenTalkObjective;
                case ProofOfWorthPhase.OmenArena:
                case ProofOfWorthPhase.OmenFailed:
                    return OmenArenaObjective;
                case ProofOfWorthPhase.OmenReport:
                    return OmenReportObjective;
                case ProofOfWorthPhase.C1MeetGuide:
                    return C1MeetGuide;
                case ProofOfWorthPhase.C1RestoreCovenant:
                    return C1RestoreCovenant;
                case ProofOfWorthPhase.C1FaceGuardian:
                    return C1FaceGuardian;
                case ProofOfWorthPhase.C1AcceptMark:
                case ProofOfWorthPhase.LordshipGranted:
                    return C1AcceptMark;
                default:
                    return string.Empty;
            }
        }
    }
}
