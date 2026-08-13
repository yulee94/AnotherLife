namespace AL.Core
{
    public enum FirstUserRouteIntent
    {
        Invalid = 0,
        ResolveNext = 1,
        RequestIsolatedCharacterGameTest = 2,
        RequestGameplay = 3,
        RequestKingdom = 4
    }

    /// <summary>
    /// Identifies the ceiling of already-verified evidence without carrying any
    /// identity, receipt, provider, or persistence data into this pure planner.
    /// </summary>
    public enum FirstUserRouteEvidenceOrigin
    {
        Invalid = 0,
        ProductionAuthority = 1,
        DevelopmentEmulatorV1 = 2
    }

    public enum FirstUserJourneyStep
    {
        Invalid = 0,
        Realm = 1,
        OriginRace = 2,
        ClassSelection = 3,
        Customization = 4,
        Handle = 5,
        AuthoritativeReceipt = 6,
        LocalProjection = 7,
        Complete = 8
    }

    public enum FirstUserRouteDestination
    {
        None = 0,
        Realm = 1,
        OriginRace = 2,
        ClassSelection = 3,
        Customization = 4,
        Handle = 5,
        AuthoritativeReceipt = 6,
        LocalProjection = 7,
        HostReadiness = 8,
        WritableAuthority = 9,
        IsolatedCharacterGameTest = 10,
        Gameplay = 11
    }

    public enum FirstUserRouteCursorState
    {
        Invalid = 0,
        Missing = 1,
        Matching = 2,
        Stale = 3,
        Forward = 4,
        Malformed = 5,
        Conflict = 6
    }

    public enum FirstUserRoutePlanStatus
    {
        Invalid = 0,
        Rejected = 1,
        StepRequired = 2,
        AdmissionBlocked = 3,
        IsolatedCharacterGameTestEligible = 4,
        GameplayAdmitted = 5
    }

    public enum FirstUserRouteDiagnostic
    {
        Invalid = 0,
        None = 1,
        IntentInvalid = 2,
        EvidenceOutOfOrder = 3,
        CursorMalformed = 4,
        CursorMissing = 5,
        CursorStale = 6,
        CursorForward = 7,
        CursorConflict = 8,
        DirectGameplayDenied = 9,
        HostNotReady = 10,
        WritableUnavailable = 11,
        EvidenceOriginInvalid = 12,
        DevelopmentEvidenceCeiling = 13,
        DevelopmentEvidenceRequired = 14,
        KingdomAuthorityUnavailable = 15
    }

    /// <summary>
    /// Sanitized cursor evidence produced by an owning authority boundary. It is
    /// deliberately not the persisted onboarding cursor or its identity payload.
    /// </summary>
    public readonly struct FirstUserRouteCursorEvidence
    {
        public FirstUserRouteCursorEvidence(
            FirstUserRouteCursorState state,
            FirstUserJourneyStep step)
        {
            State = state;
            Step = step;
        }

        public FirstUserRouteCursorState State { get; }

        public FirstUserJourneyStep Step { get; }
    }

    /// <summary>
    /// Immutable, authority-neutral facts used only to resolve the next abstract
    /// first-user step. No field is a receipt, identity, save, or scene authority.
    /// </summary>
    public readonly struct FirstUserRouteSnapshot
    {
        public FirstUserRouteSnapshot(
            bool realmValidated,
            bool originRaceValidated,
            bool classSelectionValidated,
            bool customizationValidated,
            bool handleValidated,
            bool authoritativeReceiptVerified,
            bool localProjectionVerified,
            bool hostReady,
            bool writable,
            FirstUserRouteEvidenceOrigin evidenceOrigin,
            FirstUserRouteCursorEvidence cursor)
        {
            RealmValidated = realmValidated;
            OriginRaceValidated = originRaceValidated;
            ClassSelectionValidated = classSelectionValidated;
            CustomizationValidated = customizationValidated;
            HandleValidated = handleValidated;
            AuthoritativeReceiptVerified = authoritativeReceiptVerified;
            LocalProjectionVerified = localProjectionVerified;
            HostReady = hostReady;
            Writable = writable;
            EvidenceOrigin = evidenceOrigin;
            Cursor = cursor;
        }

        public bool RealmValidated { get; }

        public bool OriginRaceValidated { get; }

        public bool ClassSelectionValidated { get; }

        public bool CustomizationValidated { get; }

        public bool HandleValidated { get; }

        public bool AuthoritativeReceiptVerified { get; }

        public bool LocalProjectionVerified { get; }

        public bool HostReady { get; }

        public bool Writable { get; }

        public FirstUserRouteEvidenceOrigin EvidenceOrigin { get; }

        public FirstUserRouteCursorEvidence Cursor { get; }
    }

    public readonly struct FirstUserRoutePlan
    {
        internal FirstUserRoutePlan(
            FirstUserRoutePlanStatus status,
            FirstUserJourneyStep journeyStep,
            FirstUserRouteDestination destination,
            FirstUserRouteDiagnostic diagnostic)
        {
            Status = status;
            JourneyStep = journeyStep;
            Destination = destination;
            Diagnostic = diagnostic;
        }

        public FirstUserRoutePlanStatus Status { get; }

        public FirstUserJourneyStep JourneyStep { get; }

        public FirstUserRouteDestination Destination { get; }

        public FirstUserRouteDiagnostic Diagnostic { get; }

        public bool AllowsGameplay =>
            Status == FirstUserRoutePlanStatus.GameplayAdmitted &&
            JourneyStep == FirstUserJourneyStep.Complete &&
            Destination == FirstUserRouteDestination.Gameplay &&
            Diagnostic == FirstUserRouteDiagnostic.None;

        public bool AllowsIsolatedCharacterGameTest =>
            Status == FirstUserRoutePlanStatus.IsolatedCharacterGameTestEligible &&
            JourneyStep == FirstUserJourneyStep.Complete &&
            Destination == FirstUserRouteDestination.IsolatedCharacterGameTest &&
            Diagnostic == FirstUserRouteDiagnostic.None;
    }
}
