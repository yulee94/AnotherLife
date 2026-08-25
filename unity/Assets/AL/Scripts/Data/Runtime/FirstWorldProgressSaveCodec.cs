using System;
using System.Globalization;
using AL.ChampionMode.Quests;
using AL.ChampionMode.Tutorial;
using AL.Core;

namespace AL.Data.Runtime
{
    public enum FirstWorldProgressReadDisposition
    {
        Invalid = 0,
        LegacyDefault = 1,
        Durable = 2,
        ReconciledFromLordship = 3
    }

    public enum FirstWorldTutorialProgressCommand
    {
        Invalid = 0,
        CameraLookAccepted = 1,
        MovementAccepted = 2,
        GuideInteractionAccepted = 3,
        BasicAttackAccepted = 4
    }

    public enum FirstWorldProgressPrepareDisposition
    {
        Prepared = 0,
        Duplicate = 1,
        Rejected = 2
    }

    public sealed class FirstWorldProgressSnapshot
    {
        internal FirstWorldProgressSnapshot(
            RealmId realm,
            long revision,
            FirstWorldEntryTutorialState tutorial,
            bool handoffCommitted,
            ProofOfWorthState proof,
            string lastOperationId,
            FirstWorldProgressReadDisposition readDisposition)
        {
            Realm = realm;
            Revision = revision;
            Tutorial = tutorial;
            HandoffCommitted = handoffCommitted;
            Proof = proof;
            LastOperationId = lastOperationId ?? string.Empty;
            ReadDisposition = readDisposition;
        }

        public RealmId Realm { get; }
        public long Revision { get; }
        public FirstWorldEntryTutorialState Tutorial { get; }
        public bool HandoffCommitted { get; }
        public ProofOfWorthState Proof { get; }
        public string LastOperationId { get; }
        public FirstWorldProgressReadDisposition ReadDisposition { get; }
        public bool HasDurableState =>
            ReadDisposition == FirstWorldProgressReadDisposition.Durable ||
            ReadDisposition == FirstWorldProgressReadDisposition.ReconciledFromLordship;
        public bool IsTutorialComplete => Tutorial != null && Tutorial.IsComplete;
        public bool CanRunProof =>
            IsTutorialComplete &&
            HandoffCommitted &&
            ProofOfWorthPlanner.IsValid(Proof) &&
            Proof.Realm == Realm;
    }

    public readonly struct FirstWorldProgressCommitRequest
    {
        internal FirstWorldProgressCommitRequest(
            string transactionId,
            string operationId,
            FirstWorldProgressSnapshot expected,
            FirstWorldTutorialProgressCommand tutorialCommand,
            bool blockTaught,
            ProofOfWorthCommand proofCommand)
        {
            TransactionId = transactionId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            Expected = expected;
            TutorialCommand = tutorialCommand;
            BlockTaught = blockTaught;
            ProofCommand = proofCommand;
        }

        public string TransactionId { get; }
        public string OperationId { get; }
        public FirstWorldProgressSnapshot Expected { get; }
        public FirstWorldTutorialProgressCommand TutorialCommand { get; }
        public bool BlockTaught { get; }
        public ProofOfWorthCommand ProofCommand { get; }
    }

    /// <summary>
    /// Versioned schema-v1 extension for the first-world tutorial and its
    /// single durable handoff into the existing Proof-of-Worth state machine.
    /// Every write is reconstructed from one admitted typed command; callers
    /// cannot publish an arbitrary state snapshot.
    /// </summary>
    public static class FirstWorldProgressSaveCodec
    {
        public const string PersistenceSlot = "SaveGameData.FirstWorldProgress";
        public const string PersistenceSlotPath = "$.FirstWorldProgress";
        public const int MaximumOperationIdLength = 96;

        public static bool TryRead(
            SaveGameData save,
            out FirstWorldProgressSnapshot snapshot,
            out string message)
        {
            snapshot = null;
            message = string.Empty;
            if (save == null)
            {
                message = "AL-FIRST-WORLD-SAVE-MISSING";
                return false;
            }

            FirstWorldProgressData data = save.FirstWorldProgress;
            if (data == null || data.Version == 0 && IsNeutralLegacyData(data))
            {
                snapshot = CreateLegacySnapshot(save);
                return true;
            }

            if (data.Version != FirstWorldProgressData.CurrentVersion ||
                data.Revision <= 0 ||
                string.IsNullOrWhiteSpace(data.LastOperationId) ||
                data.LastOperationId.Length > MaximumOperationIdLength)
            {
                message = "AL-FIRST-WORLD-STORED-VERSION-INVALID";
                return false;
            }

            var tutorial = new FirstWorldEntryTutorialState(
                (FirstWorldEntryTutorialStep)data.TutorialStep,
                (FirstWorldEntryTeachingBeat)data.TeachingBeat,
                data.MovementConfirmationCount,
                data.BasicAttackConfirmationCount,
                data.CompletionEventCount,
                data.OmenOfferCount,
                omenAccepted: false,
                blockTaught: data.BlockTaught);
            if (!FirstWorldEntryTutorialPlanner.IsValid(tutorial))
            {
                message = "AL-FIRST-WORLD-TUTORIAL-TOPOLOGY-INVALID";
                return false;
            }

            ProofOfWorthState proof = null;
            if (data.HandoffCommitted)
            {
                proof = DecodeProof(data, save.SelectedRealm);
                if (!tutorial.IsComplete ||
                    !ProofOfWorthPlanner.IsValid(proof) ||
                    proof.Realm != save.SelectedRealm)
                {
                    message = "AL-FIRST-WORLD-HANDOFF-TOPOLOGY-INVALID";
                    return false;
                }
            }
            else if (tutorial.IsComplete || !HasNeutralProof(data))
            {
                message = "AL-FIRST-WORLD-HANDOFF-MISSING";
                return false;
            }

            bool lordshipGranted = ProofOfWorthLordship.IsGranted(save);
            if (proof != null && proof.LordshipGranted && !lordshipGranted)
            {
                message = "AL-FIRST-WORLD-LORDSHIP-EVIDENCE-MISSING";
                return false;
            }

            if (lordshipGranted && (proof == null || !proof.LordshipGranted))
            {
                snapshot = new FirstWorldProgressSnapshot(
                    save.SelectedRealm,
                    data.Revision,
                    CreateCompletedTutorial(data.BlockTaught),
                    true,
                    CreateGrantedProof(save.SelectedRealm),
                    data.LastOperationId,
                    FirstWorldProgressReadDisposition.ReconciledFromLordship);
                return true;
            }

            snapshot = new FirstWorldProgressSnapshot(
                save.SelectedRealm,
                data.Revision,
                tutorial,
                data.HandoffCommitted,
                proof,
                data.LastOperationId,
                FirstWorldProgressReadDisposition.Durable);
            return true;
        }

        public static bool TryValidateStoredData(
            SaveGameData save,
            out string message)
        {
            if (save == null)
            {
                message = "AL-FIRST-WORLD-SAVE-MISSING";
                return false;
            }

            if (save.FirstWorldProgress == null)
            {
                message = string.Empty;
                return true;
            }

            return TryRead(save, out _, out message);
        }

        internal static string BuildOperationId(
            FirstWorldProgressSnapshot expected,
            FirstWorldTutorialProgressCommand tutorialCommand,
            bool blockTaught,
            ProofOfWorthCommand proofCommand)
        {
            if (expected == null)
            {
                return string.Empty;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "fw1_v1_{0}_{1}_{2}_{3}_{4}",
                (int)expected.Realm,
                expected.Revision,
                (int)tutorialCommand,
                blockTaught ? 1 : 0,
                (int)proofCommand);
        }

        internal static FirstWorldProgressPrepareDisposition PrepareCandidate(
            SaveGameData candidate,
            FirstWorldProgressCommitRequest request,
            out FirstWorldProgressSnapshot prepared,
            out string message)
        {
            prepared = null;
            message = string.Empty;
            if (candidate == null ||
                request.Expected == null ||
                string.IsNullOrWhiteSpace(request.TransactionId) ||
                request.Expected.Realm == RealmId.None ||
                candidate.SelectedRealm != request.Expected.Realm)
            {
                message = "AL-FIRST-WORLD-REQUEST-INVALID";
                return FirstWorldProgressPrepareDisposition.Rejected;
            }

            string expectedOperationId = BuildOperationId(
                request.Expected,
                request.TutorialCommand,
                request.BlockTaught,
                request.ProofCommand);
            if (!string.Equals(
                    request.OperationId,
                    expectedOperationId,
                    StringComparison.Ordinal) ||
                request.OperationId.Length > MaximumOperationIdLength)
            {
                message = "AL-FIRST-WORLD-OPERATION-INVALID";
                return FirstWorldProgressPrepareDisposition.Rejected;
            }

            if (!TryRead(candidate, out FirstWorldProgressSnapshot current, out message))
            {
                return FirstWorldProgressPrepareDisposition.Rejected;
            }

            if (!TryApply(
                    request.Expected,
                    request.TutorialCommand,
                    request.BlockTaught,
                    request.ProofCommand,
                    request.OperationId,
                    out FirstWorldProgressSnapshot expectedNext,
                    out string applyMessage))
            {
                message = applyMessage;
                return FirstWorldProgressPrepareDisposition.Rejected;
            }

            FirstWorldProgressData stored = candidate.FirstWorldProgress;
            if (stored != null &&
                string.Equals(
                    stored.LastOperationId,
                    request.OperationId,
                    StringComparison.Ordinal))
            {
                if (Equivalent(current, expectedNext))
                {
                    prepared = current;
                    return FirstWorldProgressPrepareDisposition.Duplicate;
                }

                message = "AL-FIRST-WORLD-OPERATION-CONFLICT";
                return FirstWorldProgressPrepareDisposition.Rejected;
            }

            bool completingReconciledLordship =
                request.TutorialCommand ==
                    FirstWorldTutorialProgressCommand.Invalid &&
                request.ProofCommand == ProofOfWorthCommand.AcceptMark &&
                request.Expected.Proof?.Phase ==
                    ProofOfWorthPhase.C1AcceptMark &&
                ProofOfWorthLordship.IsGranted(candidate) &&
                current.ReadDisposition ==
                    FirstWorldProgressReadDisposition.ReconciledFromLordship &&
                StoredEquivalent(stored, Encode(request.Expected));
            if (!Equivalent(current, request.Expected) &&
                !completingReconciledLordship)
            {
                message = "AL-FIRST-WORLD-REVISION-CONFLICT";
                return FirstWorldProgressPrepareDisposition.Rejected;
            }

            if (expectedNext.Proof != null &&
                expectedNext.Proof.LordshipGranted &&
                !ProofOfWorthLordship.IsGranted(candidate))
            {
                message = "AL-FIRST-WORLD-LORDSHIP-COMMIT-REQUIRED";
                return FirstWorldProgressPrepareDisposition.Rejected;
            }

            candidate.FirstWorldProgress = Encode(expectedNext);
            if (!TryRead(candidate, out prepared, out message) ||
                !Equivalent(prepared, expectedNext))
            {
                prepared = null;
                message = string.IsNullOrWhiteSpace(message)
                    ? "AL-FIRST-WORLD-CANDIDATE-INVALID"
                    : message;
                return FirstWorldProgressPrepareDisposition.Rejected;
            }

            return FirstWorldProgressPrepareDisposition.Prepared;
        }

        internal static bool TryApply(
            FirstWorldProgressSnapshot current,
            FirstWorldTutorialProgressCommand tutorialCommand,
            bool blockTaught,
            ProofOfWorthCommand proofCommand,
            string operationId,
            out FirstWorldProgressSnapshot next,
            out string message)
        {
            next = null;
            message = string.Empty;
            if (current == null ||
                current.Revision == long.MaxValue ||
                string.IsNullOrWhiteSpace(operationId))
            {
                message = "AL-FIRST-WORLD-EXPECTED-STATE-INVALID";
                return false;
            }

            bool tutorialOperation =
                tutorialCommand != FirstWorldTutorialProgressCommand.Invalid;
            bool proofOperation = proofCommand != ProofOfWorthCommand.Invalid;
            if (tutorialOperation == proofOperation)
            {
                message = "AL-FIRST-WORLD-COMMAND-INVALID";
                return false;
            }

            FirstWorldEntryTutorialState tutorial = current.Tutorial;
            ProofOfWorthState proof = current.Proof;
            bool handoff = current.HandoffCommitted;
            if (tutorialOperation)
            {
                if (handoff || proof != null || tutorial == null || tutorial.IsComplete)
                {
                    message = "AL-FIRST-WORLD-TUTORIAL-ALREADY-COMPLETE";
                    return false;
                }

                switch (tutorialCommand)
                {
                    case FirstWorldTutorialProgressCommand.CameraLookAccepted:
                        if (tutorial.TeachingBeat != FirstWorldEntryTeachingBeat.CameraLook)
                        {
                            message = "AL-FIRST-WORLD-TUTORIAL-ORDER-CONFLICT";
                            return false;
                        }

                        tutorial = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                            tutorial,
                            FirstWorldEntryTeachingBeat.Move,
                            blockTaught: false);
                        break;
                    case FirstWorldTutorialProgressCommand.MovementAccepted:
                        if (tutorial.TeachingBeat != FirstWorldEntryTeachingBeat.Move)
                        {
                            message = "AL-FIRST-WORLD-TUTORIAL-ORDER-CONFLICT";
                            return false;
                        }

                        tutorial = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                            tutorial,
                            FirstWorldEntryTeachingBeat.Move,
                            blockTaught);
                        FirstWorldEntryTutorialTransition movement =
                            FirstWorldEntryTutorialPlanner.Apply(
                                tutorial,
                                FirstWorldEntryEvidenceKind.MovementConfirmed);
                        if (!movement.Changed)
                        {
                            message = "AL-FIRST-WORLD-MOVEMENT-REJECTED";
                            return false;
                        }

                        tutorial = movement.State;
                        break;
                    case FirstWorldTutorialProgressCommand.GuideInteractionAccepted:
                        if (tutorial.TeachingBeat != FirstWorldEntryTeachingBeat.Interact)
                        {
                            message = "AL-FIRST-WORLD-TUTORIAL-ORDER-CONFLICT";
                            return false;
                        }

                        tutorial = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                            tutorial,
                            FirstWorldEntryTeachingBeat.BasicAttack,
                            blockTaught: false);
                        break;
                    case FirstWorldTutorialProgressCommand.BasicAttackAccepted:
                        if (tutorial.TeachingBeat != FirstWorldEntryTeachingBeat.BasicAttack)
                        {
                            message = "AL-FIRST-WORLD-TUTORIAL-ORDER-CONFLICT";
                            return false;
                        }

                        FirstWorldEntryTutorialTransition attack =
                            FirstWorldEntryTutorialPlanner.Apply(
                                tutorial,
                                FirstWorldEntryEvidenceKind.BasicAttackConfirmed);
                        if (!attack.Changed ||
                            !string.Equals(
                                attack.CompletionEventId,
                                FirstWorldEntryTutorialIds.CompletedEventId,
                                StringComparison.Ordinal))
                        {
                            message = "AL-FIRST-WORLD-ATTACK-REJECTED";
                            return false;
                        }

                        tutorial = attack.State;
                        handoff = true;
                        proof = ProofOfWorthPlanner.CreateOffered(current.Realm);
                        break;
                    default:
                        message = "AL-FIRST-WORLD-COMMAND-INVALID";
                        return false;
                }
            }
            else
            {
                if (!current.CanRunProof || proof == null || proof.LordshipGranted)
                {
                    message = "AL-FIRST-WORLD-PROOF-UNAVAILABLE";
                    return false;
                }

                ProofOfWorthTransition transition =
                    ProofOfWorthPlanner.Apply(proof, proofCommand);
                if (!transition.Changed)
                {
                    message = transition.Status == ProofOfWorthStatus.DuplicateIgnored
                        ? "AL-FIRST-WORLD-PROOF-DUPLICATE"
                        : "AL-FIRST-WORLD-PROOF-REJECTED";
                    return false;
                }

                proof = transition.State;
            }

            if (!FirstWorldEntryTutorialPlanner.IsValid(tutorial) ||
                handoff != tutorial.IsComplete ||
                handoff && !ProofOfWorthPlanner.IsValid(proof) ||
                !handoff && proof != null)
            {
                message = "AL-FIRST-WORLD-NEXT-STATE-INVALID";
                return false;
            }

            next = new FirstWorldProgressSnapshot(
                current.Realm,
                current.Revision + 1,
                tutorial,
                handoff,
                proof,
                operationId,
                FirstWorldProgressReadDisposition.Durable);
            return true;
        }

        internal static bool Equivalent(
            FirstWorldProgressSnapshot left,
            FirstWorldProgressSnapshot right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null ||
                left.Realm != right.Realm ||
                left.Revision != right.Revision ||
                left.HandoffCommitted != right.HandoffCommitted ||
                !string.Equals(
                    left.LastOperationId,
                    right.LastOperationId,
                    StringComparison.Ordinal) ||
                !Equivalent(left.Tutorial, right.Tutorial))
            {
                return false;
            }

            return Equivalent(left.Proof, right.Proof);
        }

        private static bool Equivalent(
            FirstWorldEntryTutorialState left,
            FirstWorldEntryTutorialState right)
        {
            return left != null &&
                   right != null &&
                   left.Step == right.Step &&
                   left.TeachingBeat == right.TeachingBeat &&
                   left.MovementConfirmationCount == right.MovementConfirmationCount &&
                   left.BasicAttackConfirmationCount == right.BasicAttackConfirmationCount &&
                   left.CompletionEventCount == right.CompletionEventCount &&
                   left.OmenOfferCount == right.OmenOfferCount &&
                   left.OmenAccepted == right.OmenAccepted &&
                   left.BlockTaught == right.BlockTaught;
        }

        private static bool Equivalent(
            ProofOfWorthState left,
            ProofOfWorthState right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            return left.Phase == right.Phase &&
                   string.Equals(left.QuestId, right.QuestId, StringComparison.Ordinal) &&
                   string.Equals(left.QuestStateId, right.QuestStateId, StringComparison.Ordinal) &&
                   string.Equals(left.ObjectiveId, right.ObjectiveId, StringComparison.Ordinal) &&
                   string.Equals(left.DialogueId, right.DialogueId, StringComparison.Ordinal) &&
                   string.Equals(left.LastEventId, right.LastEventId, StringComparison.Ordinal) &&
                   left.Realm == right.Realm &&
                   string.Equals(
                       left.ChapterVariantId,
                       right.ChapterVariantId,
                       StringComparison.Ordinal) &&
                   left.OmenAccepted == right.OmenAccepted &&
                   left.AutoAccept == right.AutoAccept;
        }

        private static FirstWorldProgressSnapshot CreateLegacySnapshot(
            SaveGameData save)
        {
            if (ProofOfWorthLordship.IsGranted(save))
            {
                return new FirstWorldProgressSnapshot(
                    save.SelectedRealm,
                    0,
                    CreateCompletedTutorial(blockTaught: false),
                    true,
                    CreateGrantedProof(save.SelectedRealm),
                    string.Empty,
                    FirstWorldProgressReadDisposition.ReconciledFromLordship);
            }

            return new FirstWorldProgressSnapshot(
                save.SelectedRealm,
                0,
                FirstWorldEntryTutorialPlanner.CreateInitial(),
                false,
                null,
                string.Empty,
                FirstWorldProgressReadDisposition.LegacyDefault);
        }

        private static FirstWorldProgressData Encode(
            FirstWorldProgressSnapshot snapshot)
        {
            FirstWorldEntryTutorialState tutorial = snapshot.Tutorial;
            ProofOfWorthState proof = snapshot.Proof;
            return new FirstWorldProgressData
            {
                Version = FirstWorldProgressData.CurrentVersion,
                Revision = snapshot.Revision,
                TutorialStep = (int)tutorial.Step,
                TeachingBeat = (int)tutorial.TeachingBeat,
                MovementConfirmationCount = tutorial.MovementConfirmationCount,
                BasicAttackConfirmationCount = tutorial.BasicAttackConfirmationCount,
                CompletionEventCount = tutorial.CompletionEventCount,
                OmenOfferCount = tutorial.OmenOfferCount,
                BlockTaught = tutorial.BlockTaught,
                HandoffCommitted = snapshot.HandoffCommitted,
                ProofPhase = proof == null ? 0 : (int)proof.Phase,
                ProofQuestId = proof?.QuestId ?? string.Empty,
                ProofQuestStateId = proof?.QuestStateId ?? string.Empty,
                ProofObjectiveId = proof?.ObjectiveId ?? string.Empty,
                ProofDialogueId = proof?.DialogueId ?? string.Empty,
                ProofLastEventId = proof?.LastEventId ?? string.Empty,
                ProofChapterVariantId = proof?.ChapterVariantId ?? string.Empty,
                ProofOmenAccepted = proof != null && proof.OmenAccepted,
                ProofAutoAccept = proof != null && proof.AutoAccept,
                LastOperationId = snapshot.LastOperationId
            };
        }

        private static bool StoredEquivalent(
            FirstWorldProgressData left,
            FirstWorldProgressData right)
        {
            return left != null &&
                   right != null &&
                   left.Version == right.Version &&
                   left.Revision == right.Revision &&
                   left.TutorialStep == right.TutorialStep &&
                   left.TeachingBeat == right.TeachingBeat &&
                   left.MovementConfirmationCount ==
                       right.MovementConfirmationCount &&
                   left.BasicAttackConfirmationCount ==
                       right.BasicAttackConfirmationCount &&
                   left.CompletionEventCount == right.CompletionEventCount &&
                   left.OmenOfferCount == right.OmenOfferCount &&
                   left.BlockTaught == right.BlockTaught &&
                   left.HandoffCommitted == right.HandoffCommitted &&
                   left.ProofPhase == right.ProofPhase &&
                   string.Equals(
                       left.ProofQuestId,
                       right.ProofQuestId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.ProofQuestStateId,
                       right.ProofQuestStateId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.ProofObjectiveId,
                       right.ProofObjectiveId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.ProofDialogueId,
                       right.ProofDialogueId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.ProofLastEventId,
                       right.ProofLastEventId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.ProofChapterVariantId,
                       right.ProofChapterVariantId,
                       StringComparison.Ordinal) &&
                   left.ProofOmenAccepted == right.ProofOmenAccepted &&
                   left.ProofAutoAccept == right.ProofAutoAccept &&
                   string.Equals(
                       left.LastOperationId,
                       right.LastOperationId,
                       StringComparison.Ordinal);
        }

        private static ProofOfWorthState DecodeProof(
            FirstWorldProgressData data,
            RealmId realm)
        {
            return new ProofOfWorthState(
                (ProofOfWorthPhase)data.ProofPhase,
                data.ProofQuestId,
                data.ProofQuestStateId,
                data.ProofObjectiveId,
                data.ProofDialogueId,
                data.ProofLastEventId,
                realm,
                data.ProofChapterVariantId,
                data.ProofOmenAccepted,
                data.ProofAutoAccept);
        }

        private static FirstWorldEntryTutorialState CreateCompletedTutorial(
            bool blockTaught)
        {
            return new FirstWorldEntryTutorialState(
                FirstWorldEntryTutorialStep.Complete,
                FirstWorldEntryTeachingBeat.OmenOffered,
                movementConfirmationCount: 1,
                basicAttackConfirmationCount: 1,
                completionEventCount: 1,
                omenOfferCount: 1,
                omenAccepted: false,
                blockTaught: blockTaught);
        }

        private static ProofOfWorthState CreateGrantedProof(RealmId realm)
        {
            return new ProofOfWorthState(
                ProofOfWorthPhase.LordshipGranted,
                ProofOfWorthIds.MainQuestId,
                ProofOfWorthIds.OmenCompletedState,
                ProofOfWorthIds.AcceptMarkObjectiveId,
                string.Empty,
                ProofOfWorthIds.AcceptMarkObjectiveId,
                realm,
                ProofOfWorthIds.ResolveRealmVariantId(realm),
                omenAccepted: true,
                autoAccept: false);
        }

        private static bool HasNeutralProof(FirstWorldProgressData data)
        {
            return data.ProofPhase == 0 &&
                   string.IsNullOrEmpty(data.ProofQuestId) &&
                   string.IsNullOrEmpty(data.ProofQuestStateId) &&
                   string.IsNullOrEmpty(data.ProofObjectiveId) &&
                   string.IsNullOrEmpty(data.ProofDialogueId) &&
                   string.IsNullOrEmpty(data.ProofLastEventId) &&
                   string.IsNullOrEmpty(data.ProofChapterVariantId) &&
                   !data.ProofOmenAccepted &&
                   !data.ProofAutoAccept;
        }

        private static bool IsNeutralLegacyData(FirstWorldProgressData data)
        {
            return data != null &&
                   data.Revision == 0 &&
                   data.TutorialStep == 0 &&
                   data.TeachingBeat == 0 &&
                   data.MovementConfirmationCount == 0 &&
                   data.BasicAttackConfirmationCount == 0 &&
                   data.CompletionEventCount == 0 &&
                   data.OmenOfferCount == 0 &&
                   !data.BlockTaught &&
                   !data.HandoffCommitted &&
                   HasNeutralProof(data) &&
                   string.IsNullOrEmpty(data.LastOperationId);
        }
    }
}
