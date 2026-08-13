namespace AL.Core
{
    /// <summary>
    /// Pure, allocation-free first-user route resolver. It does not load, persist,
    /// mutate, navigate, or authenticate any of the evidence supplied to it.
    /// </summary>
    public static class FirstUserRouteAdmissionPlanner
    {
        public static FirstUserRoutePlan Plan(
            FirstUserRouteIntent intent,
            FirstUserRouteSnapshot snapshot)
        {
            // The planner has no Lord-appointment or Kingdom-grant predicate.
            // Consequently this intent is denied before inspecting any supplied
            // evidence; callers cannot reinterpret onboarding as Kingdom authority.
            if (intent == FirstUserRouteIntent.RequestKingdom)
            {
                return Reject(
                    FirstUserJourneyStep.Invalid,
                    FirstUserRouteDiagnostic.KingdomAuthorityUnavailable);
            }

            if (intent != FirstUserRouteIntent.ResolveNext &&
                intent != FirstUserRouteIntent.RequestIsolatedCharacterGameTest &&
                intent != FirstUserRouteIntent.RequestGameplay)
            {
                return Reject(
                    FirstUserJourneyStep.Invalid,
                    FirstUserRouteDiagnostic.IntentInvalid);
            }

            FirstUserJourneyStep journeyStep = ResolveJourneyStep(snapshot);
            if (HasOutOfOrderEvidence(snapshot, journeyStep))
            {
                return Reject(
                    journeyStep,
                    FirstUserRouteDiagnostic.EvidenceOutOfOrder);
            }

            FirstUserRouteDiagnostic cursorDiagnostic = ValidateCursor(
                snapshot.Cursor,
                journeyStep);
            if (cursorDiagnostic != FirstUserRouteDiagnostic.None)
            {
                return Reject(journeyStep, cursorDiagnostic);
            }

            if (journeyStep != FirstUserJourneyStep.Complete)
            {
                return new FirstUserRoutePlan(
                    FirstUserRoutePlanStatus.StepRequired,
                    journeyStep,
                    ToDestination(journeyStep),
                    intent == FirstUserRouteIntent.ResolveNext
                        ? FirstUserRouteDiagnostic.None
                        : FirstUserRouteDiagnostic.DirectGameplayDenied);
            }

            // Host readiness and writable authority are independent runtime gates.
            // They neither complete nor invalidate a persisted journey step.
            if (!snapshot.HostReady)
            {
                return new FirstUserRoutePlan(
                    FirstUserRoutePlanStatus.AdmissionBlocked,
                    FirstUserJourneyStep.Complete,
                    FirstUserRouteDestination.HostReadiness,
                    FirstUserRouteDiagnostic.HostNotReady);
            }

            if (!snapshot.Writable)
            {
                return new FirstUserRoutePlan(
                    FirstUserRoutePlanStatus.AdmissionBlocked,
                    FirstUserJourneyStep.Complete,
                    FirstUserRouteDestination.WritableAuthority,
                    FirstUserRouteDiagnostic.WritableUnavailable);
            }

            switch (snapshot.EvidenceOrigin)
            {
                case FirstUserRouteEvidenceOrigin.ProductionAuthority:
                    if (intent == FirstUserRouteIntent.RequestIsolatedCharacterGameTest)
                    {
                        return Reject(
                            FirstUserJourneyStep.Complete,
                            FirstUserRouteDiagnostic.DevelopmentEvidenceRequired);
                    }

                    return new FirstUserRoutePlan(
                        FirstUserRoutePlanStatus.GameplayAdmitted,
                        FirstUserJourneyStep.Complete,
                        FirstUserRouteDestination.Gameplay,
                        FirstUserRouteDiagnostic.None);

                case FirstUserRouteEvidenceOrigin.DevelopmentEmulatorV1:
                    if (intent == FirstUserRouteIntent.RequestGameplay)
                    {
                        return Reject(
                            FirstUserJourneyStep.Complete,
                            FirstUserRouteDiagnostic.DevelopmentEvidenceCeiling);
                    }

                    return new FirstUserRoutePlan(
                        FirstUserRoutePlanStatus.IsolatedCharacterGameTestEligible,
                        FirstUserJourneyStep.Complete,
                        FirstUserRouteDestination.IsolatedCharacterGameTest,
                        FirstUserRouteDiagnostic.None);

                default:
                    return Reject(
                        FirstUserJourneyStep.Complete,
                        FirstUserRouteDiagnostic.EvidenceOriginInvalid);
            }
        }

        private static FirstUserJourneyStep ResolveJourneyStep(FirstUserRouteSnapshot snapshot)
        {
            if (!snapshot.RealmValidated)
            {
                return FirstUserJourneyStep.Realm;
            }

            if (!snapshot.OriginRaceValidated)
            {
                return FirstUserJourneyStep.OriginRace;
            }

            if (!snapshot.ClassSelectionValidated)
            {
                return FirstUserJourneyStep.ClassSelection;
            }

            if (!snapshot.CustomizationValidated)
            {
                return FirstUserJourneyStep.Customization;
            }

            if (!snapshot.HandleValidated)
            {
                return FirstUserJourneyStep.Handle;
            }

            if (!snapshot.AuthoritativeReceiptVerified)
            {
                return FirstUserJourneyStep.AuthoritativeReceipt;
            }

            if (!snapshot.LocalProjectionVerified)
            {
                return FirstUserJourneyStep.LocalProjection;
            }

            return FirstUserJourneyStep.Complete;
        }

        private static bool HasOutOfOrderEvidence(
            FirstUserRouteSnapshot snapshot,
            FirstUserJourneyStep journeyStep)
        {
            switch (journeyStep)
            {
                case FirstUserJourneyStep.Realm:
                    return snapshot.OriginRaceValidated ||
                           snapshot.ClassSelectionValidated ||
                           snapshot.CustomizationValidated ||
                           snapshot.HandleValidated ||
                           snapshot.AuthoritativeReceiptVerified ||
                           snapshot.LocalProjectionVerified;
                case FirstUserJourneyStep.OriginRace:
                    return snapshot.ClassSelectionValidated ||
                           snapshot.CustomizationValidated ||
                           snapshot.HandleValidated ||
                           snapshot.AuthoritativeReceiptVerified ||
                           snapshot.LocalProjectionVerified;
                case FirstUserJourneyStep.ClassSelection:
                    return snapshot.CustomizationValidated ||
                           snapshot.HandleValidated ||
                           snapshot.AuthoritativeReceiptVerified ||
                           snapshot.LocalProjectionVerified;
                case FirstUserJourneyStep.Customization:
                    return snapshot.HandleValidated ||
                           snapshot.AuthoritativeReceiptVerified ||
                           snapshot.LocalProjectionVerified;
                case FirstUserJourneyStep.Handle:
                    return snapshot.AuthoritativeReceiptVerified ||
                           snapshot.LocalProjectionVerified;
                case FirstUserJourneyStep.AuthoritativeReceipt:
                    return snapshot.LocalProjectionVerified;
                case FirstUserJourneyStep.LocalProjection:
                case FirstUserJourneyStep.Complete:
                    return false;
                default:
                    return true;
            }
        }

        private static FirstUserRouteDiagnostic ValidateCursor(
            FirstUserRouteCursorEvidence cursor,
            FirstUserJourneyStep expectedStep)
        {
            switch (cursor.State)
            {
                case FirstUserRouteCursorState.Missing:
                    if (cursor.Step != FirstUserJourneyStep.Invalid)
                    {
                        return FirstUserRouteDiagnostic.CursorMalformed;
                    }

                    return expectedStep == FirstUserJourneyStep.Realm
                        ? FirstUserRouteDiagnostic.None
                        : FirstUserRouteDiagnostic.CursorMissing;

                case FirstUserRouteCursorState.Matching:
                    if (!IsPersistableJourneyStep(cursor.Step))
                    {
                        return FirstUserRouteDiagnostic.CursorMalformed;
                    }

                    return cursor.Step == expectedStep
                        ? FirstUserRouteDiagnostic.None
                        : FirstUserRouteDiagnostic.CursorConflict;

                case FirstUserRouteCursorState.Stale:
                    return IsPersistableJourneyStep(cursor.Step)
                        ? FirstUserRouteDiagnostic.CursorStale
                        : FirstUserRouteDiagnostic.CursorMalformed;

                case FirstUserRouteCursorState.Forward:
                    return IsPersistableJourneyStep(cursor.Step)
                        ? FirstUserRouteDiagnostic.CursorForward
                        : FirstUserRouteDiagnostic.CursorMalformed;

                case FirstUserRouteCursorState.Malformed:
                    return FirstUserRouteDiagnostic.CursorMalformed;

                case FirstUserRouteCursorState.Conflict:
                    return IsPersistableJourneyStep(cursor.Step)
                        ? FirstUserRouteDiagnostic.CursorConflict
                        : FirstUserRouteDiagnostic.CursorMalformed;

                default:
                    return FirstUserRouteDiagnostic.CursorMalformed;
            }
        }

        private static bool IsPersistableJourneyStep(FirstUserJourneyStep step)
        {
            switch (step)
            {
                case FirstUserJourneyStep.Realm:
                case FirstUserJourneyStep.OriginRace:
                case FirstUserJourneyStep.ClassSelection:
                case FirstUserJourneyStep.Customization:
                case FirstUserJourneyStep.Handle:
                case FirstUserJourneyStep.AuthoritativeReceipt:
                case FirstUserJourneyStep.LocalProjection:
                case FirstUserJourneyStep.Complete:
                    return true;
                default:
                    return false;
            }
        }

        private static FirstUserRouteDestination ToDestination(FirstUserJourneyStep step)
        {
            switch (step)
            {
                case FirstUserJourneyStep.Realm:
                    return FirstUserRouteDestination.Realm;
                case FirstUserJourneyStep.OriginRace:
                    return FirstUserRouteDestination.OriginRace;
                case FirstUserJourneyStep.ClassSelection:
                    return FirstUserRouteDestination.ClassSelection;
                case FirstUserJourneyStep.Customization:
                    return FirstUserRouteDestination.Customization;
                case FirstUserJourneyStep.Handle:
                    return FirstUserRouteDestination.Handle;
                case FirstUserJourneyStep.AuthoritativeReceipt:
                    return FirstUserRouteDestination.AuthoritativeReceipt;
                case FirstUserJourneyStep.LocalProjection:
                    return FirstUserRouteDestination.LocalProjection;
                default:
                    return FirstUserRouteDestination.None;
            }
        }

        private static FirstUserRoutePlan Reject(
            FirstUserJourneyStep journeyStep,
            FirstUserRouteDiagnostic diagnostic)
        {
            return new FirstUserRoutePlan(
                FirstUserRoutePlanStatus.Rejected,
                journeyStep,
                FirstUserRouteDestination.None,
                diagnostic);
        }
    }
}
