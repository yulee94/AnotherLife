#if !UNITY_EDITOR
#error The isolated first-user core gameplay planner is Editor-only.
#endif

using System;

namespace AL.Editor.Development.FirstUserGameTest
{
    internal static class FirstUserCoreGameplayPlanner
    {
        internal const float MovementDistanceThreshold = 0.02f;
        internal const float MaximumMovementEvidenceDistance = 2f;
        internal const string OmenOfferedStateId = "OFFERED";
        internal const string SelectValeriusActionId = "SELECT_VALERIUS";

        internal static FirstUserRuntimeFailureDisposition ClassifyRuntimeFailure(
            bool visibleRecoveryExplicitlyAllowed,
            bool hostInitialized,
            bool editorPlaying,
            bool exactProductionTickPolicyVerified)
        {
            return visibleRecoveryExplicitlyAllowed &&
                   hostInitialized && editorPlaying &&
                   exactProductionTickPolicyVerified
                ? FirstUserRuntimeFailureDisposition.RetainBlockedPanel
                : FirstUserRuntimeFailureDisposition.HardStop;
        }

        internal static bool RequiresHardStopForSceneLoad(
            bool terminalFailure,
            bool sceneValid,
            bool sceneLoaded)
        {
            return terminalFailure && sceneValid && sceneLoaded;
        }

        internal static bool TryCreateFocusSnapshot(
            string sessionId,
            int generation,
            out FirstUserFocusSnapshot snapshot)
        {
            if (!IsCanonicalSessionId(sessionId) || generation <= 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = new FirstUserFocusSnapshot(
                sessionId,
                generation,
                FirstUserFocusState.Active,
                epoch: 0);
            return true;
        }

        internal static FirstUserFocusTransition SuspendForFocusLoss(
            FirstUserFocusSnapshot current)
        {
            if (!current.IsCanonical || current.State == FirstUserFocusState.FailClosed)
            {
                return FocusFailure(current, "focus_snapshot_invalid");
            }

            if (current.State == FirstUserFocusState.Suspended)
            {
                return new FirstUserFocusTransition(
                    FirstUserCoreTransitionStatus.DuplicateIgnored,
                    current,
                    string.Empty);
            }

            if (current.State != FirstUserFocusState.Active &&
                current.State != FirstUserFocusState.ResumePending &&
                current.State != FirstUserFocusState.AwaitingNeutralInput)
            {
                return FocusFailure(current, "focus_loss_out_of_order");
            }

            if (current.Epoch == int.MaxValue)
            {
                return FocusFailure(current, "focus_epoch_exhausted");
            }

            return new FirstUserFocusTransition(
                FirstUserCoreTransitionStatus.Applied,
                new FirstUserFocusSnapshot(
                    current.SessionId,
                    current.Generation,
                    FirstUserFocusState.Suspended,
                    current.Epoch + 1),
                string.Empty);
        }

        internal static FirstUserFocusTransition MarkFocusReturned(
            FirstUserFocusSnapshot current)
        {
            if (!current.IsCanonical || current.State == FirstUserFocusState.FailClosed)
            {
                return FocusFailure(current, "focus_snapshot_invalid");
            }

            if (current.State == FirstUserFocusState.ResumePending ||
                current.State == FirstUserFocusState.AwaitingNeutralInput)
            {
                return new FirstUserFocusTransition(
                    FirstUserCoreTransitionStatus.DuplicateIgnored,
                    current,
                    string.Empty);
            }

            if (current.State != FirstUserFocusState.Suspended)
            {
                return new FirstUserFocusTransition(
                    FirstUserCoreTransitionStatus.Rejected,
                    current,
                    "focus_return_without_suspension");
            }

            return new FirstUserFocusTransition(
                FirstUserCoreTransitionStatus.Applied,
                new FirstUserFocusSnapshot(
                    current.SessionId,
                    current.Generation,
                    FirstUserFocusState.ResumePending,
                    current.Epoch),
                string.Empty);
        }

        internal static FirstUserFocusTransition BeginResumeRevalidation(
            FirstUserFocusSnapshot current,
            string sessionId,
            int generation,
            int epoch,
            FirstUserResumeEvidence evidence)
        {
            if (!current.IsCanonical ||
                current.State != FirstUserFocusState.ResumePending ||
                !string.Equals(current.SessionId, sessionId, StringComparison.Ordinal) ||
                current.Generation != generation ||
                current.Epoch != epoch ||
                !evidence.IsExact)
            {
                return FocusFailure(current, "focus_resume_revalidation_failed");
            }

            return new FirstUserFocusTransition(
                FirstUserCoreTransitionStatus.Applied,
                new FirstUserFocusSnapshot(
                    current.SessionId,
                    current.Generation,
                    FirstUserFocusState.AwaitingNeutralInput,
                    current.Epoch),
                string.Empty);
        }

        internal static FirstUserFocusTransition CompleteResumeAfterNeutralInput(
            FirstUserFocusSnapshot current,
            bool allGameplayInputNeutral)
        {
            if (!current.IsCanonical || current.State == FirstUserFocusState.FailClosed)
            {
                return FocusFailure(current, "focus_snapshot_invalid");
            }

            if (current.State != FirstUserFocusState.AwaitingNeutralInput)
            {
                return new FirstUserFocusTransition(
                    FirstUserCoreTransitionStatus.Rejected,
                    current,
                    "focus_neutral_check_out_of_order");
            }

            if (!allGameplayInputNeutral)
            {
                return new FirstUserFocusTransition(
                    FirstUserCoreTransitionStatus.Waiting,
                    current,
                    string.Empty);
            }

            return new FirstUserFocusTransition(
                FirstUserCoreTransitionStatus.Applied,
                new FirstUserFocusSnapshot(
                    current.SessionId,
                    current.Generation,
                    FirstUserFocusState.Active,
                    current.Epoch),
                string.Empty);
        }

        internal static FirstUserFocusTransition FailClosedForNonResumableBoundary(
            FirstUserFocusSnapshot current,
            string diagnostic)
        {
            return FocusFailure(
                current,
                string.IsNullOrEmpty(diagnostic)
                    ? "non_resumable_lifecycle_boundary"
                    : diagnostic);
        }

        internal static FirstUserMovementTransition BeginMovementProof(
            string sessionId,
            int generation,
            int focusEpoch,
            int attackGeneration,
            float originX,
            float originZ,
            float directionX,
            float directionZ)
        {
            if (!IsCanonicalSessionId(sessionId) ||
                generation <= 0 ||
                focusEpoch < 0 ||
                attackGeneration < 0 ||
                !AreFinite(originX, originZ, directionX, directionZ))
            {
                return MovementFailure("movement_request_invalid");
            }

            float lengthSquared = directionX * directionX + directionZ * directionZ;
            if (lengthSquared < 0.0001f)
            {
                return MovementFailure("movement_direction_required");
            }

            float inverseLength = 1f / (float)Math.Sqrt(lengthSquared);
            var proof = new FirstUserMovementProof(
                FirstUserMovementProofState.Pending,
                sessionId,
                generation,
                focusEpoch,
                attackGeneration,
                originX,
                originZ,
                directionX * inverseLength,
                directionZ * inverseLength);
            return new FirstUserMovementTransition(
                FirstUserCoreTransitionStatus.Applied,
                proof,
                string.Empty);
        }

        internal static FirstUserMovementTransition ObserveMovement(
            FirstUserMovementProof current,
            string sessionId,
            int generation,
            int focusEpoch,
            int attackGeneration,
            bool attackActive,
            float currentX,
            float currentZ)
        {
            if (!current.IsPending ||
                !string.Equals(current.SessionId, sessionId, StringComparison.Ordinal) ||
                current.Generation != generation ||
                current.FocusEpoch != focusEpoch ||
                !AreFinite(currentX, currentZ))
            {
                return MovementFailure("movement_observation_mismatch");
            }

            if (attackActive || current.AttackGeneration != attackGeneration)
            {
                return new FirstUserMovementTransition(
                    FirstUserCoreTransitionStatus.Rejected,
                    CopyMovement(current, FirstUserMovementProofState.Contaminated),
                    "movement_contaminated_by_attack");
            }

            float deltaX = currentX - current.OriginX;
            float deltaZ = currentZ - current.OriginZ;
            float distanceSquared = deltaX * deltaX + deltaZ * deltaZ;
            if (distanceSquared > MaximumMovementEvidenceDistance * MaximumMovementEvidenceDistance)
            {
                return new FirstUserMovementTransition(
                    FirstUserCoreTransitionStatus.Rejected,
                    CopyMovement(current, FirstUserMovementProofState.Contaminated),
                    "movement_displacement_not_continuous");
            }

            float projected = deltaX * current.DirectionX + deltaZ * current.DirectionZ;
            if (projected < MovementDistanceThreshold)
            {
                return new FirstUserMovementTransition(
                    FirstUserCoreTransitionStatus.Waiting,
                    current,
                    string.Empty);
            }

            return new FirstUserMovementTransition(
                FirstUserCoreTransitionStatus.Applied,
                CopyMovement(current, FirstUserMovementProofState.Confirmed),
                string.Empty);
        }

        internal static FirstUserMovementTransition AbortMovementForFocusLoss(
            FirstUserMovementProof current)
        {
            if (!current.IsPending)
            {
                return new FirstUserMovementTransition(
                    FirstUserCoreTransitionStatus.DuplicateIgnored,
                    current,
                    string.Empty);
            }

            return new FirstUserMovementTransition(
                FirstUserCoreTransitionStatus.Applied,
                CopyMovement(current, FirstUserMovementProofState.Contaminated),
                "movement_aborted_for_focus_suspension");
        }

        internal static FirstUserAttackTransition BeginAttackProof(
            string sessionId,
            int generation,
            int focusEpoch,
            int attackSequence,
            int frame,
            bool requestAccepted)
        {
            if (!requestAccepted ||
                !IsCanonicalSessionId(sessionId) ||
                generation <= 0 ||
                focusEpoch < 0 ||
                attackSequence <= 0 ||
                frame < 0)
            {
                return AttackFailure("attack_request_not_accepted");
            }

            return new FirstUserAttackTransition(
                FirstUserCoreTransitionStatus.Applied,
                new FirstUserAttackProof(
                    FirstUserAttackProofState.AcceptedStart,
                    sessionId,
                    generation,
                    focusEpoch,
                    attackSequence,
                    frame,
                    frame,
                    mechanicsResultObserved: false),
                string.Empty);
        }

        internal static FirstUserAttackTransition ObserveAttack(
            FirstUserAttackProof current,
            string sessionId,
            int generation,
            int focusEpoch,
            int attackSequence,
            int frame,
            bool attackActive,
            bool mechanicsResultObserved)
        {
            if (!current.IsPending ||
                !string.Equals(current.SessionId, sessionId, StringComparison.Ordinal) ||
                current.Generation != generation ||
                current.FocusEpoch != focusEpoch ||
                current.AttackSequence != attackSequence ||
                frame <= current.LastObservedFrame)
            {
                return new FirstUserAttackTransition(
                    FirstUserCoreTransitionStatus.Rejected,
                    CopyAttack(
                        current,
                        FirstUserAttackProofState.Contaminated,
                        frame,
                        mechanicsResultObserved),
                    "attack_observation_mismatch");
            }

            if (current.State == FirstUserAttackProofState.AcceptedStart)
            {
                if (!attackActive)
                {
                    return new FirstUserAttackTransition(
                        FirstUserCoreTransitionStatus.Rejected,
                        CopyAttack(
                            current,
                            FirstUserAttackProofState.Contaminated,
                            frame,
                            mechanicsResultObserved),
                        "attack_never_became_active");
                }

                return new FirstUserAttackTransition(
                    FirstUserCoreTransitionStatus.Applied,
                    CopyAttack(
                        current,
                        FirstUserAttackProofState.ActiveObserved,
                        frame,
                        mechanicsResultObserved),
                    string.Empty);
            }

            if (attackActive)
            {
                return new FirstUserAttackTransition(
                    FirstUserCoreTransitionStatus.Waiting,
                    CopyAttack(
                        current,
                        FirstUserAttackProofState.ActiveObserved,
                        frame,
                        mechanicsResultObserved),
                    string.Empty);
            }

            if (!current.MechanicsResultObserved && !mechanicsResultObserved)
            {
                return new FirstUserAttackTransition(
                    FirstUserCoreTransitionStatus.Rejected,
                    CopyAttack(
                        current,
                        FirstUserAttackProofState.Contaminated,
                        frame,
                        mechanicsResultObserved),
                    "attack_mechanics_result_missing");
            }

            return new FirstUserAttackTransition(
                FirstUserCoreTransitionStatus.Applied,
                CopyAttack(
                    current,
                    FirstUserAttackProofState.Settled,
                    frame,
                    mechanicsResultObserved),
                string.Empty);
        }

        internal static FirstUserAttackTransition AbortAttackForFocusLoss(
            FirstUserAttackProof current)
        {
            if (!current.IsPending)
            {
                return new FirstUserAttackTransition(
                    FirstUserCoreTransitionStatus.DuplicateIgnored,
                    current,
                    string.Empty);
            }

            return new FirstUserAttackTransition(
                FirstUserCoreTransitionStatus.Applied,
                CopyAttack(
                    current,
                    FirstUserAttackProofState.Contaminated,
                    current.LastObservedFrame,
                    mechanicsResultObserved: false),
                "attack_aborted_for_focus_suspension");
        }

        internal static FirstUserExitTransition RequestExit(FirstUserExitState current)
        {
            if (current == FirstUserExitState.Inactive)
            {
                return new FirstUserExitTransition(
                    FirstUserCoreTransitionStatus.Applied,
                    FirstUserExitState.Prompted,
                    string.Empty);
            }

            if (current == FirstUserExitState.Prompted ||
                current == FirstUserExitState.Committed)
            {
                return new FirstUserExitTransition(
                    FirstUserCoreTransitionStatus.DuplicateIgnored,
                    current,
                    string.Empty);
            }

            return new FirstUserExitTransition(
                FirstUserCoreTransitionStatus.Rejected,
                current,
                "exit_state_invalid");
        }

        internal static FirstUserExitTransition CancelExit(FirstUserExitState current)
        {
            if (current == FirstUserExitState.Prompted)
            {
                return new FirstUserExitTransition(
                    FirstUserCoreTransitionStatus.Applied,
                    FirstUserExitState.Inactive,
                    string.Empty);
            }

            if (current == FirstUserExitState.Inactive)
            {
                return new FirstUserExitTransition(
                    FirstUserCoreTransitionStatus.DuplicateIgnored,
                    current,
                    string.Empty);
            }

            return new FirstUserExitTransition(
                FirstUserCoreTransitionStatus.Rejected,
                current,
                "committed_exit_cannot_be_canceled");
        }

        internal static FirstUserExitTransition ConfirmExit(FirstUserExitState current)
        {
            if (current == FirstUserExitState.Prompted)
            {
                return new FirstUserExitTransition(
                    FirstUserCoreTransitionStatus.Applied,
                    FirstUserExitState.Committed,
                    string.Empty);
            }

            if (current == FirstUserExitState.Committed)
            {
                return new FirstUserExitTransition(
                    FirstUserCoreTransitionStatus.DuplicateIgnored,
                    current,
                    string.Empty);
            }

            return new FirstUserExitTransition(
                FirstUserCoreTransitionStatus.Rejected,
                current,
                "exit_confirmation_not_prompted");
        }

        internal static bool IsPassiveOmenOffer(FirstUserOmenProjection projection)
        {
            return projection.Revision == 1 &&
                   string.Equals(
                       projection.StateId,
                       OmenOfferedStateId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       projection.PendingActionId,
                       SelectValeriusActionId,
                       StringComparison.Ordinal) &&
                   projection.PendingChoice &&
                   projection.AcceptedCount == 0 &&
                   projection.ProgressedCount == 0 &&
                   projection.CompletedCount == 0 &&
                   projection.RewardCount == 0;
        }

        internal static bool IsCanonicalSessionId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
            {
                return false;
            }

            bool anyNonZero = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool digit = character >= '0' && character <= '9';
                bool lowerHex = character >= 'a' && character <= 'f';
                if (!digit && !lowerHex)
                {
                    return false;
                }

                anyNonZero |= character != '0';
            }

            return anyNonZero;
        }

        private static FirstUserFocusTransition FocusFailure(
            FirstUserFocusSnapshot current,
            string diagnostic)
        {
            var failed = new FirstUserFocusSnapshot(
                current.SessionId,
                current.Generation,
                FirstUserFocusState.FailClosed,
                Math.Max(0, current.Epoch));
            return new FirstUserFocusTransition(
                FirstUserCoreTransitionStatus.FailClosed,
                failed,
                diagnostic);
        }

        private static FirstUserMovementTransition MovementFailure(string diagnostic)
        {
            return new FirstUserMovementTransition(
                FirstUserCoreTransitionStatus.Rejected,
                default,
                diagnostic);
        }

        private static FirstUserAttackTransition AttackFailure(string diagnostic)
        {
            return new FirstUserAttackTransition(
                FirstUserCoreTransitionStatus.Rejected,
                default,
                diagnostic);
        }

        private static FirstUserMovementProof CopyMovement(
            FirstUserMovementProof current,
            FirstUserMovementProofState state)
        {
            return new FirstUserMovementProof(
                state,
                current.SessionId,
                current.Generation,
                current.FocusEpoch,
                current.AttackGeneration,
                current.OriginX,
                current.OriginZ,
                current.DirectionX,
                current.DirectionZ);
        }

        private static FirstUserAttackProof CopyAttack(
            FirstUserAttackProof current,
            FirstUserAttackProofState state,
            int frame,
            bool mechanicsResultObserved)
        {
            return new FirstUserAttackProof(
                state,
                current.SessionId,
                current.Generation,
                current.FocusEpoch,
                current.AttackSequence,
                current.AcceptedFrame,
                frame,
                current.MechanicsResultObserved || mechanicsResultObserved);
        }

        private static bool AreFinite(float first, float second)
        {
            return IsFinite(first) && IsFinite(second);
        }

        private static bool AreFinite(
            float first,
            float second,
            float third,
            float fourth)
        {
            return IsFinite(first) && IsFinite(second) &&
                   IsFinite(third) && IsFinite(fourth);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
