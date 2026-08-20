using System;
using System.IO;
using System.Reflection;
using AL.ChampionMode.Control;
using AL.Core;
using AL.Editor.Development.FirstUserGameTest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace AL.Tests.EditMode.FirstUserGameTest
{
    public sealed class FirstUserCoreGameplayPlannerTests
    {
        private const string SessionId = "1234567890abcdef1234567890abcdef";

        [Test]
        public void OnlyExplicitlySafeVerifiedActiveRuntimeMayRetainBlockedFailurePanel()
        {
            Assert.That(
                FirstUserCoreGameplayPlanner.ClassifyRuntimeFailure(
                    visibleRecoveryExplicitlyAllowed: true,
                    hostInitialized: true,
                    editorPlaying: true,
                    exactProductionTickPolicyVerified: true),
                Is.EqualTo(
                    FirstUserRuntimeFailureDisposition.RetainBlockedPanel));

            for (int mismatch = 0; mismatch < 4; mismatch++)
            {
                bool[] evidence = { true, true, true, true };
                evidence[mismatch] = false;
                Assert.That(
                    FirstUserCoreGameplayPlanner.ClassifyRuntimeFailure(
                        evidence[0],
                        evidence[1],
                        evidence[2],
                        evidence[3]),
                    Is.EqualTo(FirstUserRuntimeFailureDisposition.HardStop));
            }
        }

        [Test]
        public void AnyLoadedSceneAfterRetainedFailureRequiresHardStop()
        {
            Assert.That(
                FirstUserCoreGameplayPlanner.RequiresHardStopForSceneLoad(
                    terminalFailure: true,
                    sceneValid: true,
                    sceneLoaded: true),
                Is.True);
            Assert.That(
                FirstUserCoreGameplayPlanner.RequiresHardStopForSceneLoad(
                    terminalFailure: false,
                    sceneValid: true,
                    sceneLoaded: true),
                Is.False);
        }

        [Test]
        public void FocusLifecycleStartsActiveWithZeroEpoch()
        {
            Assert.That(FirstUserCoreGameplayPlanner.TryCreateFocusSnapshot(
                SessionId,
                generation: 7,
                out FirstUserFocusSnapshot snapshot), Is.True);
            Assert.That(snapshot.State, Is.EqualTo(FirstUserFocusState.Active));
            Assert.That(snapshot.Epoch, Is.Zero);
            Assert.That(snapshot.IsCanonical, Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("00000000000000000000000000000000")]
        [TestCase("1234567890ABCDEF1234567890ABCDEF")]
        [TestCase("1234")]
        public void FocusLifecycleRejectsNonCanonicalSession(string sessionId)
        {
            Assert.That(FirstUserCoreGameplayPlanner.TryCreateFocusSnapshot(
                sessionId,
                generation: 1,
                out FirstUserFocusSnapshot snapshot), Is.False);
            Assert.That(snapshot.IsCanonical, Is.False);
        }

        [Test]
        public void RepeatedFocusLossIsIdempotentAndDoesNotAdvanceEpochTwice()
        {
            FirstUserFocusSnapshot active = CreateFocus();
            FirstUserFocusTransition first =
                FirstUserCoreGameplayPlanner.SuspendForFocusLoss(active);
            FirstUserFocusTransition duplicate =
                FirstUserCoreGameplayPlanner.SuspendForFocusLoss(first.Snapshot);

            Assert.That(first.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Applied));
            Assert.That(first.Snapshot.State, Is.EqualTo(FirstUserFocusState.Suspended));
            Assert.That(first.Snapshot.Epoch, Is.EqualTo(1));
            Assert.That(duplicate.Status, Is.EqualTo(
                FirstUserCoreTransitionStatus.DuplicateIgnored));
            Assert.That(duplicate.Snapshot.Epoch, Is.EqualTo(1));
        }

        [Test]
        public void FocusReturnDoesNotResumeBeforeExactRevalidation()
        {
            FirstUserFocusSnapshot suspended = FirstUserCoreGameplayPlanner
                .SuspendForFocusLoss(CreateFocus()).Snapshot;

            FirstUserFocusTransition returned =
                FirstUserCoreGameplayPlanner.MarkFocusReturned(suspended);

            Assert.That(returned.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Applied));
            Assert.That(returned.Snapshot.State, Is.EqualTo(
                FirstUserFocusState.ResumePending));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        public void EveryResumeEvidenceMismatchFailsClosed(int mismatchIndex)
        {
            FirstUserFocusSnapshot pending = ResumePending();
            bool[] values =
            {
                true, true, true, true, true, true, true,
                true, true, true, true, true, true
            };
            values[mismatchIndex] = false;
            var evidence = new FirstUserResumeEvidence(
                values[0], values[1], values[2], values[3], values[4], values[5],
                values[6], values[7], values[8], values[9], values[10], values[11],
                values[12]);

            FirstUserFocusTransition result =
                FirstUserCoreGameplayPlanner.BeginResumeRevalidation(
                    pending,
                    SessionId,
                    generation: 7,
                    pending.Epoch,
                    evidence);

            Assert.That(result.Status, Is.EqualTo(
                FirstUserCoreTransitionStatus.FailClosed));
            Assert.That(result.Snapshot.State, Is.EqualTo(
                FirstUserFocusState.FailClosed));
        }

        [Test]
        public void ExactResumeWaitsForNeutralInputBeforeReactivation()
        {
            FirstUserFocusSnapshot pending = ResumePending();
            FirstUserFocusTransition verified =
                FirstUserCoreGameplayPlanner.BeginResumeRevalidation(
                    pending,
                    SessionId,
                    generation: 7,
                    pending.Epoch,
                    FirstUserResumeEvidence.Exact);
            FirstUserFocusTransition held =
                FirstUserCoreGameplayPlanner.CompleteResumeAfterNeutralInput(
                    verified.Snapshot,
                    allGameplayInputNeutral: false);
            FirstUserFocusTransition resumed =
                FirstUserCoreGameplayPlanner.CompleteResumeAfterNeutralInput(
                    held.Snapshot,
                    allGameplayInputNeutral: true);

            Assert.That(verified.Snapshot.State, Is.EqualTo(
                FirstUserFocusState.AwaitingNeutralInput));
            Assert.That(held.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Waiting));
            Assert.That(held.Snapshot.State, Is.EqualTo(
                FirstUserFocusState.AwaitingNeutralInput));
            Assert.That(resumed.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Applied));
            Assert.That(resumed.Snapshot.State, Is.EqualTo(FirstUserFocusState.Active));
            Assert.That(resumed.Snapshot.Epoch, Is.EqualTo(pending.Epoch));
        }

        [Test]
        public void EditorCoordinatorIsTheOnlyFocusLifecycleOwner()
        {
            string bootstrapSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/Development/EditorGameTestModeBootstrap.cs"));
            string coordinatorSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/Editor/GameTestModeWindow.cs"));
            string hostSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/Editor/Development/FirstUserGameTest/" +
                "FirstUserGameTestRuntimeHost.cs"));
            string tutorialSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/Editor/Development/FirstUserGameTest/" +
                "FirstUserGameTestTutorialRuntime.cs"));

            Assert.That(bootstrapSource, Does.Not.Contain("OnApplicationFocus("));
            Assert.That(bootstrapSource, Does.Not.Contain("OnApplicationPause("));
            Assert.That(CountOccurrences(
                coordinatorSource,
                "EditorApplication.focusChanged += HandleEditorFocusChanged"),
                Is.EqualTo(1));
            Assert.That(CountOccurrences(
                coordinatorSource,
                "TryNotifyEditorFocusChanged("),
                Is.EqualTo(1));
            Assert.That(CountOccurrences(
                coordinatorSource,
                "FirstUserGameTestRuntimeHost.TrySynchronizeFocusSuspension("),
                Is.EqualTo(1));

            string focusHandler = SliceBetween(
                coordinatorSource,
                "private static void HandleEditorFocusChanged(bool hasFocus)",
                "private static void HandleEditorPauseStateChanged(PauseState state)");
            Assert.That(focusHandler.IndexOf(
                "TryNotifyEditorFocusChanged(",
                StringComparison.Ordinal), Is.LessThan(focusHandler.IndexOf(
                "TrySynchronizeFocusSuspension(",
                StringComparison.Ordinal)));

            string suspension = SliceBetween(
                hostSource,
                "private bool TrySynchronizeFocusSuspension(int epoch, out string message)",
                "private bool BeginFocusResume(int epoch)");
            Assert.That(suspension.IndexOf(
                "eventSystem.enabled = false",
                StringComparison.Ordinal), Is.LessThan(suspension.IndexOf(
                "TryCaptureFocusResumeState(",
                StringComparison.Ordinal)));
            Assert.That(suspension.IndexOf(
                "inputModule.enabled = false",
                StringComparison.Ordinal), Is.LessThan(suspension.IndexOf(
                "TryCaptureFocusResumeState(",
                StringComparison.Ordinal)));
            Assert.That(suspension, Does.Contain("SetFocusSuspended(true)"));
            Assert.That(hostSource, Does.Contain("Input.touchCount == 0"),
                "Touch must participate in the neutral-input quarantine before resume.");
            string hardInputGate = SliceBetween(
                hostSource,
                "private bool TryApplyOwnedFocusInputSuspension(out string message)",
                "private bool BeginFocusResume(int epoch)");
            Assert.That(hardInputGate.IndexOf(
                "SetSelectedGameObject(null)",
                StringComparison.Ordinal), Is.LessThan(hardInputGate.IndexOf(
                "_focusOwnedEventSystem.enabled = false",
                StringComparison.Ordinal)),
                "A pre-activation EventSystem has no current module to clear selection, so the owned selection must be cleared explicitly.");
            Assert.That(hostSource, Does.Contain("CaptureAuthorityState()"));
            Assert.That(hostSource, Does.Contain("CaptureProjectionState()"));
            Assert.That(hostSource, Does.Contain("TryCaptureRetainedState("));
            Assert.That(tutorialSource, Does.Contain("TryLoadExisting("));
            Assert.That(CountOccurrences(
                hostSource,
                "TryValidateFocusResumeBoundary("),
                Is.EqualTo(5),
                "The exact boundary must be defined once and consumed before validation, restoration, the one-frame module-activation wait, and final activation.");

            string completeResume = SliceBetween(
                hostSource,
                "private bool CompleteFocusResume(int epoch)",
                "private bool TryValidateFocusResumeBoundary(");
            Assert.That(completeResume, Does.Contain("_focusInputRestorePending"));
            Assert.That(completeResume, Does.Contain(
                "_focusInputRestoreActivationWaitFrames == 0"));
            Assert.That(completeResume, Does.Contain(
                "allowInputModuleActivationPending: true"));
            Assert.That(completeResume, Does.Contain(
                "did not activate within its single safe restoration frame"));
            Assert.That(completeResume.IndexOf(
                "TryValidateFocusResumeBoundary(",
                StringComparison.Ordinal), Is.LessThan(completeResume.IndexOf(
                "TryBeginFocusInputRestoration(",
                StringComparison.Ordinal)));
            Assert.That(completeResume.LastIndexOf(
                "TryValidateFocusResumeBoundary(",
                StringComparison.Ordinal), Is.LessThan(completeResume.IndexOf(
                "SetFocusSuspended(false)",
                StringComparison.Ordinal)));

            string beginResume = SliceBetween(
                hostSource,
                "private bool BeginFocusResume(int epoch)",
                "private bool CompleteFocusResume(int epoch)");
            Assert.That(beginResume, Does.Not.Contain("SuspendForFocusLoss("));
        }

        [Test]
        public void IsolatedRuntimePolicySuppressesExactProductionTickWithoutGrantingWriteAuthority()
        {
            string policySource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/Editor/Development/FirstUserGameTest/" +
                "FirstUserIsolatedRuntimePolicy.cs"));
            string hostSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/Editor/Development/FirstUserGameTest/" +
                "FirstUserGameTestRuntimeHost.cs"));
            string bootstrapSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/Development/EditorGameTestModeBootstrap.cs"));

            Assert.That(policySource, Does.Contain("bootloader.enabled = false"));
            Assert.That(policySource, Does.Contain("\"_runtimeActive\""));
            Assert.That(policySource, Does.Contain("\"_standbyForOwnership\""));
            Assert.That(policySource, Does.Contain(
                "ProfileWriteAuthorityProviderGuard.IsCurrentWritable"));
            Assert.That(policySource, Does.Not.Contain("TickProduction("));
            Assert.That(policySource, Does.Not.Contain("Save()"));
            Assert.That(policySource, Does.Contain(
                "RuntimeInitializeLoadType.AfterSceneLoad"));
            Assert.That(CountOccurrences(
                policySource,
                "SceneManager.sceneLoaded += HandleSceneLoadedBeforeFirstUpdate"),
                Is.EqualTo(1));
            Assert.That(hostSource, Does.Contain(
                "FirstUserIsolatedRuntimePolicy.TrySecureScene("));
            Assert.That(hostSource, Does.Contain(
                "FirstUserIsolatedRuntimePolicy.TryAdvanceAndVerify("));
            Assert.That(hostSource, Does.Contain(
                "FirstUserIsolatedRuntimePolicy.TryAdvanceTickBoundary("));
            Assert.That(bootstrapSource, Does.Contain(
                "[DefaultExecutionOrder(-31990)]"));
            Assert.That(hostSource.IndexOf(
                "FirstUserIsolatedRuntimePolicy.TryAdvanceTickBoundary(",
                StringComparison.Ordinal), Is.LessThan(hostSource.IndexOf(
                "HandleFocusLifecycle()",
                StringComparison.Ordinal)));

            string tickBoundary = SliceBetween(
                policySource,
                "private bool TryAdvanceMemoryOnlyTickBoundary(",
                "private bool Matches(");
            Assert.That(tickBoundary, Does.Not.Contain("Path.GetFullPath"));
            Assert.That(tickBoundary, Does.Not.Contain("Directory."));
            Assert.That(tickBoundary, Does.Not.Contain("FieldInfo"));
            Assert.That(tickBoundary, Does.Not.Contain("GetValue("));
            Assert.That(tickBoundary, Does.Not.Contain("TryVerifyActiveRuntime"));
        }

        [TestCase("editor_pause")]
        [TestCase("domain_reload")]
        public void NonResumableLifecycleBoundariesRemainFailClosed(string diagnostic)
        {
            FirstUserFocusTransition result =
                FirstUserCoreGameplayPlanner.FailClosedForNonResumableBoundary(
                    FirstUserCoreGameplayPlanner.SuspendForFocusLoss(CreateFocus()).Snapshot,
                    diagnostic);

            Assert.That(result.Status, Is.EqualTo(
                FirstUserCoreTransitionStatus.FailClosed));
            Assert.That(result.Snapshot.State, Is.EqualTo(
                FirstUserFocusState.FailClosed));
            Assert.That(result.Diagnostic, Is.EqualTo(diagnostic));
        }

        [Test]
        public void FocusLossDuringResumeCreatesNewEpochAndInvalidatesStaleReturn()
        {
            FirstUserFocusSnapshot pending = ResumePending();
            FirstUserFocusTransition lostAgain =
                FirstUserCoreGameplayPlanner.SuspendForFocusLoss(pending);
            FirstUserFocusTransition stale =
                FirstUserCoreGameplayPlanner.BeginResumeRevalidation(
                    FirstUserCoreGameplayPlanner.MarkFocusReturned(lostAgain.Snapshot).Snapshot,
                    SessionId,
                    generation: 7,
                    pending.Epoch,
                    FirstUserResumeEvidence.Exact);

            Assert.That(lostAgain.Snapshot.Epoch, Is.EqualTo(pending.Epoch + 1));
            Assert.That(stale.Status, Is.EqualTo(
                FirstUserCoreTransitionStatus.FailClosed));
        }

        [Test]
        public void MovementProofRequiresIntentionalPositiveXZDisplacement()
        {
            FirstUserMovementTransition started = BeginMovement(1f, 0f);
            FirstUserMovementTransition waiting = ObserveMovement(
                started.Proof,
                x: FirstUserCoreGameplayPlanner.MovementDistanceThreshold - 0.001f,
                z: 0f);
            FirstUserMovementTransition confirmed = ObserveMovement(
                waiting.Proof,
                x: FirstUserCoreGameplayPlanner.MovementDistanceThreshold,
                z: 0f);

            Assert.That(waiting.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Waiting));
            Assert.That(confirmed.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Applied));
            Assert.That(confirmed.Proof.IsConfirmed, Is.True);
        }

        [TestCase(0f, 0f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(0f, float.PositiveInfinity)]
        public void MovementProofRejectsInvalidIntent(float x, float z)
        {
            FirstUserMovementTransition result =
                FirstUserCoreGameplayPlanner.BeginMovementProof(
                    SessionId, 7, 2, 4, 0f, 0f, x, z);

            Assert.That(result.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Rejected));
            Assert.That(result.Proof.IsPending, Is.False);
        }

        [Test]
        public void OppositeMovementNeverConfirms()
        {
            FirstUserMovementTransition result = ObserveMovement(
                BeginMovement(1f, 0f).Proof,
                x: -1f,
                z: 0f);

            Assert.That(result.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Waiting));
            Assert.That(result.Proof.IsConfirmed, Is.False);
        }

        [TestCase(true, 4)]
        [TestCase(false, 5)]
        public void AttackActivityOrGenerationChangeContaminatesMovement(
            bool attackActive,
            int attackGeneration)
        {
            FirstUserMovementTransition result =
                FirstUserCoreGameplayPlanner.ObserveMovement(
                    BeginMovement(1f, 0f).Proof,
                    SessionId,
                    generation: 7,
                    focusEpoch: 2,
                    attackGeneration,
                    attackActive,
                    currentX: 0.5f,
                    currentZ: 0f);

            Assert.That(result.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Rejected));
            Assert.That(result.Proof.State, Is.EqualTo(
                FirstUserMovementProofState.Contaminated));
        }

        [Test]
        public void TeleportLikeDisplacementDoesNotConfirmMovement()
        {
            FirstUserMovementTransition result = ObserveMovement(
                BeginMovement(1f, 0f).Proof,
                x: FirstUserCoreGameplayPlanner.MaximumMovementEvidenceDistance + 0.1f,
                z: 0f);

            Assert.That(result.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Rejected));
            Assert.That(result.Proof.State, Is.EqualTo(
                FirstUserMovementProofState.Contaminated));
        }

        [Test]
        public void FocusSuspensionAbortsMovementWithoutConfirmation()
        {
            FirstUserMovementTransition result =
                FirstUserCoreGameplayPlanner.AbortMovementForFocusLoss(
                    BeginMovement(0f, 1f).Proof);

            Assert.That(result.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Applied));
            Assert.That(result.Proof.State, Is.EqualTo(
                FirstUserMovementProofState.Contaminated));
        }

        [Test]
        public void AttackProofDoesNotCompleteAtAcceptedStart()
        {
            FirstUserAttackTransition started = BeginAttack();

            Assert.That(started.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Applied));
            Assert.That(started.Proof.State, Is.EqualTo(
                FirstUserAttackProofState.AcceptedStart));
            Assert.That(started.Proof.IsSettled, Is.False);
        }

        [Test]
        public void AttackProofRequiresLaterActiveThenLaterSettledObservation()
        {
            FirstUserAttackTransition active = ObserveAttack(
                BeginAttack().Proof,
                frame: 11,
                active: true);
            FirstUserAttackTransition stillActive = ObserveAttack(
                active.Proof,
                frame: 12,
                active: true);
            FirstUserAttackTransition settled = ObserveAttack(
                stillActive.Proof,
                frame: 13,
                active: false,
                mechanicsResultObserved: true);

            Assert.That(active.Proof.State, Is.EqualTo(
                FirstUserAttackProofState.ActiveObserved));
            Assert.That(stillActive.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Waiting));
            Assert.That(settled.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Applied));
            Assert.That(settled.Proof.IsSettled, Is.True);
        }

        [Test]
        public void AttackAnimationSettlingWithoutEnemyMechanicsResultIsRejected()
        {
            FirstUserAttackProof active = ObserveAttack(
                BeginAttack().Proof,
                frame: 11,
                active: true).Proof;

            FirstUserAttackTransition result = ObserveAttack(
                active,
                frame: 12,
                active: false,
                mechanicsResultObserved: false);

            Assert.That(result.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Rejected));
            Assert.That(result.Proof.State, Is.EqualTo(
                FirstUserAttackProofState.Contaminated));
            Assert.That(result.Diagnostic, Is.EqualTo("attack_mechanics_result_missing"));
        }

        [Test]
        public void EnemyMechanicsReceiptMustBindExactAttackTargetAndHealthTransition()
        {
            var request = new FirstUserOnboardingAttackRequest(
                SessionId,
                generation: 7,
                attackSequence: 9,
                frame: 11,
                enemyAssetId: "common_enemy_v001",
                attackCenter: Vector3.one,
                attackRadius: 2f);
            var exact = new FirstUserOnboardingAttackReceipt(
                SessionId,
                generation: 7,
                attackSequence: 9,
                enemyAssetId: "common_enemy_v001",
                result: FirstUserOnboardingEncounterResult.HitConfirmed,
                hitPointsBefore: 2,
                hitPointsAfter: 1,
                resetSequence: 0);
            var wrongSequence = new FirstUserOnboardingAttackReceipt(
                SessionId,
                generation: 7,
                attackSequence: 10,
                enemyAssetId: "common_enemy_v001",
                result: FirstUserOnboardingEncounterResult.HitConfirmed,
                hitPointsBefore: 2,
                hitPointsAfter: 1,
                resetSequence: 0);
            var falseDefeat = new FirstUserOnboardingAttackReceipt(
                SessionId,
                generation: 7,
                attackSequence: 9,
                enemyAssetId: "common_enemy_v001",
                result: FirstUserOnboardingEncounterResult.Defeated,
                hitPointsBefore: 2,
                hitPointsAfter: 1,
                resetSequence: 0);
            var overflowingReset = new FirstUserOnboardingAttackReceipt(
                SessionId,
                generation: 7,
                attackSequence: 9,
                enemyAssetId: "common_enemy_v001",
                result: FirstUserOnboardingEncounterResult.HitConfirmed,
                hitPointsBefore: 2,
                hitPointsAfter: 1,
                resetSequence: int.MaxValue);

            Assert.That(FirstUserOnboardingEncounterContract.IsValidRequest(request), Is.True);
            Assert.That(
                FirstUserOnboardingEncounterContract.IsValidReceipt(request, exact),
                Is.True);
            Assert.That(
                FirstUserOnboardingEncounterContract.IsValidReceipt(request, wrongSequence),
                Is.False);
            Assert.That(
                FirstUserOnboardingEncounterContract.IsValidReceipt(request, falseDefeat),
                Is.False);
            Assert.That(
                FirstUserOnboardingEncounterContract.IsValidReceipt(
                    request,
                    overflowingReset),
                Is.False);
        }

        [Test]
        public void EnemyResolverAppliesOneExactHitAndPerformsOneBoundedReset()
        {
            GameObject championRoot = null;
            GameObject enemyRoot = null;
            try
            {
                championRoot = new GameObject(
                    "ResolverChampion",
                    typeof(CharacterController),
                    typeof(ChampionController));
                ChampionController champion = championRoot.GetComponent<ChampionController>();
                enemyRoot = new GameObject("ResolverEnemy");
                BoxCollider enemyCollider = enemyRoot.AddComponent<BoxCollider>();
                var encounter = new TestEnemyEncounter(enemyRoot, initialHitPoints: 2);

                Assert.That(
                    FirstUserGameTestEnemyAttackResolver.TryCreate(
                        champion,
                        encounter,
                        new PassThroughMutationBoundary(),
                        out FirstUserGameTestEnemyAttackResolver resolver,
                        out string diagnostic),
                    Is.True,
                    diagnostic);
                var context = new ChampionBasicAttackContext(
                    champion,
                    attackSequence: 1,
                    hitCenter: enemyCollider.bounds.center,
                    hitRadius: 2f,
                    hitColliders: new Collider[] { enemyCollider },
                    realmId: RealmId.Crownlands);

                Assert.That(
                    resolver.TryResolve(
                        context,
                        out ChampionBasicAttackResolution resolution),
                    Is.True,
                    resolver.FailureDiagnostic);
                Assert.That(resolution.Kind, Is.EqualTo(ChampionBasicAttackResolutionKind.Hit));
                Assert.That(resolution.CombatText, Is.EqualTo("HIT"));
                Assert.That(encounter.ApplyCallCount, Is.EqualTo(1));
                Assert.That(resolver.TryGetConfirmedReceipt(1, out _), Is.True);
                Assert.That(
                    resolver.TryResetConfirmedResult(1, out diagnostic),
                    Is.True,
                    diagnostic);
                Assert.That(encounter.ResetCallCount, Is.EqualTo(1));
                Assert.That(encounter.ResetSequence, Is.EqualTo(1));
                Assert.That(resolver.ExpectedEncounterResetSequence, Is.EqualTo(1));
                Assert.That(encounter.CurrentHitPoints, Is.EqualTo(2));
                Assert.That(encounter.PresentationState, Is.EqualTo(
                    FirstUserOnboardingEncounterPresentationState.Idle));

                Assert.That(resolver.TryResolve(context, out _), Is.False);
                Assert.That(
                    encounter.ApplyCallCount,
                    Is.EqualTo(1),
                    "One Champion attack sequence must never apply provider mechanics twice.");
            }
            finally
            {
                if (championRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(championRoot);
                }

                if (enemyRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(enemyRoot);
                }
            }
        }

        [Test]
        public void EnemyResolverMissDoesNotInvokeProviderMechanics()
        {
            GameObject championRoot = null;
            GameObject enemyRoot = null;
            try
            {
                championRoot = new GameObject(
                    "MissResolverChampion",
                    typeof(CharacterController),
                    typeof(ChampionController));
                ChampionController champion = championRoot.GetComponent<ChampionController>();
                enemyRoot = new GameObject("MissResolverEnemy");
                enemyRoot.AddComponent<BoxCollider>();
                var encounter = new TestEnemyEncounter(enemyRoot, initialHitPoints: 2);
                Assert.That(
                    FirstUserGameTestEnemyAttackResolver.TryCreate(
                        champion,
                        encounter,
                        new PassThroughMutationBoundary(),
                        out FirstUserGameTestEnemyAttackResolver resolver,
                        out string diagnostic),
                    Is.True,
                    diagnostic);

                Assert.That(
                    resolver.TryResolve(
                        new ChampionBasicAttackContext(
                            champion,
                            attackSequence: 1,
                            hitCenter: Vector3.zero,
                            hitRadius: 2f,
                            hitColliders: Array.Empty<Collider>(),
                            realmId: RealmId.Crownlands),
                        out ChampionBasicAttackResolution resolution),
                    Is.True);
                Assert.That(resolution.Kind, Is.EqualTo(ChampionBasicAttackResolutionKind.Miss));
                Assert.That(encounter.ApplyCallCount, Is.Zero);
                Assert.That(resolver.TryGetConfirmedReceipt(1, out _), Is.False);
            }
            finally
            {
                if (championRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(championRoot);
                }

                if (enemyRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(enemyRoot);
                }
            }
        }

        [Test]
        public void EnemyResolverFailsClosedWhenMutationBoundaryRejectsCallback()
        {
            GameObject championRoot = null;
            GameObject enemyRoot = null;
            try
            {
                championRoot = new GameObject(
                    "BoundaryResolverChampion",
                    typeof(CharacterController),
                    typeof(ChampionController));
                ChampionController champion = championRoot.GetComponent<ChampionController>();
                enemyRoot = new GameObject("BoundaryResolverEnemy");
                BoxCollider enemyCollider = enemyRoot.AddComponent<BoxCollider>();
                var encounter = new TestEnemyEncounter(enemyRoot, initialHitPoints: 2);
                var boundary = new RejectingMutationBoundary(
                    rejectOnValidation: 3);
                Assert.That(
                    FirstUserGameTestEnemyAttackResolver.TryCreate(
                        champion,
                        encounter,
                        boundary,
                        out FirstUserGameTestEnemyAttackResolver resolver,
                        out string diagnostic),
                    Is.True,
                    diagnostic);

                Assert.That(
                    resolver.TryResolve(
                        new ChampionBasicAttackContext(
                            champion,
                            attackSequence: 1,
                            hitCenter: enemyCollider.bounds.center,
                            hitRadius: 2f,
                            hitColliders: new Collider[] { enemyCollider },
                            realmId: RealmId.Crownlands),
                        out _),
                    Is.False);
                Assert.That(resolver.HasFailure, Is.True);
                Assert.That(boundary.CaptureCount, Is.EqualTo(3));
                Assert.That(boundary.ValidateCount, Is.EqualTo(3));
            }
            finally
            {
                if (championRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(championRoot);
                }

                if (enemyRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(enemyRoot);
                }
            }
        }

        [Test]
        public void EnemyResolverFailsClosedWhenGetterBoundaryRejectsBeforeCallback()
        {
            GameObject championRoot = null;
            GameObject enemyRoot = null;
            try
            {
                championRoot = new GameObject(
                    "GetterBoundaryResolverChampion",
                    typeof(CharacterController),
                    typeof(ChampionController));
                ChampionController champion = championRoot.GetComponent<ChampionController>();
                enemyRoot = new GameObject("GetterBoundaryResolverEnemy");
                BoxCollider enemyCollider = enemyRoot.AddComponent<BoxCollider>();
                var encounter = new TestEnemyEncounter(enemyRoot, initialHitPoints: 2);
                var boundary = new RejectingMutationBoundary(
                    rejectOnValidation: 2);
                Assert.That(
                    FirstUserGameTestEnemyAttackResolver.TryCreate(
                        champion,
                        encounter,
                        boundary,
                        out FirstUserGameTestEnemyAttackResolver resolver,
                        out string diagnostic),
                    Is.True,
                    diagnostic);

                Assert.That(
                    resolver.TryResolve(
                        new ChampionBasicAttackContext(
                            champion,
                            attackSequence: 1,
                            hitCenter: enemyCollider.bounds.center,
                            hitRadius: 2f,
                            hitColliders: new Collider[] { enemyCollider },
                            realmId: RealmId.Crownlands),
                        out _),
                    Is.False);
                Assert.That(resolver.HasFailure, Is.True);
                Assert.That(encounter.ApplyCallCount, Is.Zero);
                Assert.That(boundary.CaptureCount, Is.EqualTo(2));
                Assert.That(boundary.ValidateCount, Is.EqualTo(2));
            }
            finally
            {
                if (championRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(championRoot);
                }

                if (enemyRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(enemyRoot);
                }
            }
        }

        [TestCase(TestEnemyBehavior.DishonestHitPointsBefore)]
        [TestCase(TestEnemyBehavior.DishonestResetAdvance)]
        public void EnemyResolverRejectsProviderStateOrReceiptDrift(
            TestEnemyBehavior behavior)
        {
            GameObject championRoot = null;
            GameObject enemyRoot = null;
            try
            {
                championRoot = new GameObject(
                    "DriftResolverChampion",
                    typeof(CharacterController),
                    typeof(ChampionController));
                ChampionController champion = championRoot.GetComponent<ChampionController>();
                enemyRoot = new GameObject("DriftResolverEnemy");
                BoxCollider enemyCollider = enemyRoot.AddComponent<BoxCollider>();
                var encounter = new TestEnemyEncounter(
                    enemyRoot,
                    initialHitPoints: 2,
                    behavior);
                Assert.That(
                    FirstUserGameTestEnemyAttackResolver.TryCreate(
                        champion,
                        encounter,
                        new PassThroughMutationBoundary(),
                        out FirstUserGameTestEnemyAttackResolver resolver,
                        out string diagnostic),
                    Is.True,
                    diagnostic);

                Assert.That(
                    resolver.TryResolve(
                        new ChampionBasicAttackContext(
                            champion,
                            attackSequence: 1,
                            hitCenter: enemyCollider.bounds.center,
                            hitRadius: 2f,
                            hitColliders: new Collider[] { enemyCollider },
                            realmId: RealmId.Crownlands),
                        out _),
                    Is.False);
                Assert.That(resolver.HasFailure, Is.True);
                Assert.That(encounter.ApplyCallCount, Is.EqualTo(1));
            }
            finally
            {
                if (championRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(championRoot);
                }

                if (enemyRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(enemyRoot);
                }
            }
        }

        [Test]
        public void AttackThatNeverBecomesActiveIsContaminated()
        {
            FirstUserAttackTransition result = ObserveAttack(
                BeginAttack().Proof,
                frame: 11,
                active: false);

            Assert.That(result.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Rejected));
            Assert.That(result.Proof.State, Is.EqualTo(
                FirstUserAttackProofState.Contaminated));
        }

        [Test]
        public void SequenceDriftCannotSettleAttackProof()
        {
            FirstUserAttackProof active = ObserveAttack(
                BeginAttack().Proof,
                frame: 11,
                active: true).Proof;
            FirstUserAttackTransition result = FirstUserCoreGameplayPlanner.ObserveAttack(
                active,
                SessionId,
                generation: 7,
                focusEpoch: 2,
                attackSequence: 10,
                frame: 12,
                attackActive: false,
                mechanicsResultObserved: true);

            Assert.That(result.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Rejected));
            Assert.That(result.Proof.State, Is.EqualTo(
                FirstUserAttackProofState.Contaminated));
        }

        [Test]
        public void FocusSuspensionAbortsPendingAttack()
        {
            FirstUserAttackTransition result =
                FirstUserCoreGameplayPlanner.AbortAttackForFocusLoss(BeginAttack().Proof);

            Assert.That(result.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Applied));
            Assert.That(result.Proof.State, Is.EqualTo(
                FirstUserAttackProofState.Contaminated));
        }

        [Test]
        public void ExitIsPromptedCancelableAndCommittedExactlyOnce()
        {
            FirstUserExitTransition prompted =
                FirstUserCoreGameplayPlanner.RequestExit(FirstUserExitState.Inactive);
            FirstUserExitTransition canceled =
                FirstUserCoreGameplayPlanner.CancelExit(prompted.State);
            FirstUserExitTransition promptedAgain =
                FirstUserCoreGameplayPlanner.RequestExit(canceled.State);
            FirstUserExitTransition committed =
                FirstUserCoreGameplayPlanner.ConfirmExit(promptedAgain.State);
            FirstUserExitTransition duplicate =
                FirstUserCoreGameplayPlanner.ConfirmExit(committed.State);

            Assert.That(prompted.State, Is.EqualTo(FirstUserExitState.Prompted));
            Assert.That(canceled.State, Is.EqualTo(FirstUserExitState.Inactive));
            Assert.That(committed.State, Is.EqualTo(FirstUserExitState.Committed));
            Assert.That(duplicate.Status, Is.EqualTo(
                FirstUserCoreTransitionStatus.DuplicateIgnored));
        }

        [Test]
        public void CommittedExitCannotBeCanceled()
        {
            FirstUserExitTransition result = FirstUserCoreGameplayPlanner.CancelExit(
                FirstUserExitState.Committed);

            Assert.That(result.Status, Is.EqualTo(FirstUserCoreTransitionStatus.Rejected));
            Assert.That(result.State, Is.EqualTo(FirstUserExitState.Committed));
        }

        [Test]
        public void ExactOmenProjectionRemainsOfferedAndNonAuthoritative()
        {
            var projection = new FirstUserOmenProjection(
                revision: 1,
                FirstUserCoreGameplayPlanner.OmenOfferedStateId,
                FirstUserCoreGameplayPlanner.SelectValeriusActionId,
                pendingChoice: true,
                acceptedCount: 0,
                progressedCount: 0,
                completedCount: 0,
                rewardCount: 0);

            Assert.That(FirstUserCoreGameplayPlanner.IsPassiveOmenOffer(projection), Is.True);
        }

        [TestCase(2, "OFFERED", "SELECT_VALERIUS", true, 0, 0, 0, 0)]
        [TestCase(1, "ACCEPTED", "SELECT_VALERIUS", true, 0, 0, 0, 0)]
        [TestCase(1, "OFFERED", "choice.omen1.accept", true, 0, 0, 0, 0)]
        [TestCase(1, "OFFERED", "SELECT_VALERIUS", false, 0, 0, 0, 0)]
        [TestCase(1, "OFFERED", "SELECT_VALERIUS", true, 1, 0, 0, 0)]
        [TestCase(1, "OFFERED", "SELECT_VALERIUS", true, 0, 1, 0, 0)]
        [TestCase(1, "OFFERED", "SELECT_VALERIUS", true, 0, 0, 1, 0)]
        [TestCase(1, "OFFERED", "SELECT_VALERIUS", true, 0, 0, 0, 1)]
        public void AnyOmenAcceptanceProgressOrRewardDriftFailsClosed(
            int revision,
            string state,
            string action,
            bool pending,
            int accepted,
            int progressed,
            int completed,
            int rewarded)
        {
            Assert.That(FirstUserCoreGameplayPlanner.IsPassiveOmenOffer(
                new FirstUserOmenProjection(
                    revision,
                    state,
                    action,
                    pending,
                    accepted,
                    progressed,
                    completed,
                    rewarded)), Is.False);
        }

        private static FirstUserFocusSnapshot CreateFocus()
        {
            Assert.That(FirstUserCoreGameplayPlanner.TryCreateFocusSnapshot(
                SessionId,
                generation: 7,
                out FirstUserFocusSnapshot snapshot), Is.True);
            return snapshot;
        }

        private static FirstUserFocusSnapshot ResumePending()
        {
            return FirstUserCoreGameplayPlanner.MarkFocusReturned(
                FirstUserCoreGameplayPlanner.SuspendForFocusLoss(CreateFocus()).Snapshot)
                .Snapshot;
        }

        private static FirstUserMovementTransition BeginMovement(float x, float z)
        {
            return FirstUserCoreGameplayPlanner.BeginMovementProof(
                SessionId,
                generation: 7,
                focusEpoch: 2,
                attackGeneration: 4,
                originX: 0f,
                originZ: 0f,
                directionX: x,
                directionZ: z);
        }

        private static FirstUserMovementTransition ObserveMovement(
            FirstUserMovementProof proof,
            float x,
            float z)
        {
            return FirstUserCoreGameplayPlanner.ObserveMovement(
                proof,
                SessionId,
                generation: 7,
                focusEpoch: 2,
                attackGeneration: 4,
                attackActive: false,
                currentX: x,
                currentZ: z);
        }

        private static FirstUserAttackTransition BeginAttack()
        {
            return FirstUserCoreGameplayPlanner.BeginAttackProof(
                SessionId,
                generation: 7,
                focusEpoch: 2,
                attackSequence: 9,
                frame: 10,
                requestAccepted: true);
        }

        private static FirstUserAttackTransition ObserveAttack(
            FirstUserAttackProof proof,
            int frame,
            bool active,
            bool mechanicsResultObserved = false)
        {
            return FirstUserCoreGameplayPlanner.ObserveAttack(
                proof,
                SessionId,
                generation: 7,
                focusEpoch: 2,
                attackSequence: 9,
                frame,
                active,
                mechanicsResultObserved);
        }

        private static int CountOccurrences(string value, string token)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += token.Length;
            }

            return count;
        }

        public enum TestEnemyBehavior
        {
            Valid = 0,
            DishonestHitPointsBefore = 1,
            DishonestResetAdvance = 2
        }

        private sealed class PassThroughMutationBoundary :
            IFirstUserGameTestMutationBoundary
        {
            public bool TryCapture(out object boundary, out string diagnostic)
            {
                boundary = this;
                diagnostic = string.Empty;
                return true;
            }

            public bool TryValidate(object boundary, out string diagnostic)
            {
                diagnostic = string.Empty;
                return ReferenceEquals(boundary, this);
            }
        }

        private sealed class RejectingMutationBoundary :
            IFirstUserGameTestMutationBoundary
        {
            private readonly int _rejectOnValidation;

            internal RejectingMutationBoundary(int rejectOnValidation)
            {
                _rejectOnValidation = rejectOnValidation;
            }

            internal int CaptureCount { get; private set; }
            internal int ValidateCount { get; private set; }

            public bool TryCapture(out object boundary, out string diagnostic)
            {
                CaptureCount++;
                boundary = this;
                diagnostic = string.Empty;
                return true;
            }

            public bool TryValidate(object boundary, out string diagnostic)
            {
                ValidateCount++;
                bool rejected = ValidateCount == _rejectOnValidation;
                diagnostic = rejected ? "mutation rejected" : string.Empty;
                return !rejected && ReferenceEquals(boundary, this);
            }
        }

        private sealed class TestEnemyEncounter : IFirstUserOnboardingEnemyEncounter
        {
            private readonly TestEnemyBehavior _behavior;

            internal TestEnemyEncounter(
                GameObject enemyRoot,
                int initialHitPoints,
                TestEnemyBehavior behavior = TestEnemyBehavior.Valid)
            {
                EnemyRoot = enemyRoot;
                InitialHitPoints = initialHitPoints;
                CurrentHitPoints = initialHitPoints;
                _behavior = behavior;
                IsReady = true;
                PresentationState =
                    FirstUserOnboardingEncounterPresentationState.Idle;
            }

            public string SessionId =>
                FirstUserCoreGameplayPlannerTests.SessionId;
            public int Generation => 7;
            public string EnemyAssetId => "common_enemy_v001";
            public GameObject EnemyRoot { get; }
            public int InitialHitPoints { get; }
            public int CurrentHitPoints { get; private set; }
            public int ResetSequence { get; private set; }
            public bool IsReady { get; private set; }
            public FirstUserOnboardingEncounterPresentationState PresentationState
            {
                get;
                private set;
            }

            internal int ApplyCallCount { get; private set; }
            internal int ResetCallCount { get; private set; }

            public bool TryApplyBasicAttack(
                FirstUserOnboardingAttackRequest request,
                out FirstUserOnboardingAttackReceipt receipt,
                out string diagnostic)
            {
                ApplyCallCount++;
                int before = CurrentHitPoints;
                CurrentHitPoints = Math.Max(0, CurrentHitPoints - 1);
                if (_behavior == TestEnemyBehavior.DishonestResetAdvance)
                {
                    ResetSequence++;
                }

                bool defeated = CurrentHitPoints == 0;
                IsReady = !defeated;
                PresentationState = defeated
                    ? FirstUserOnboardingEncounterPresentationState.Defeated
                    : FirstUserOnboardingEncounterPresentationState.HitReaction;
                receipt = new FirstUserOnboardingAttackReceipt(
                    SessionId,
                    Generation,
                    request.AttackSequence,
                    EnemyAssetId,
                    defeated
                        ? FirstUserOnboardingEncounterResult.Defeated
                        : FirstUserOnboardingEncounterResult.HitConfirmed,
                    _behavior == TestEnemyBehavior.DishonestHitPointsBefore
                        ? before + 1
                        : before,
                    CurrentHitPoints,
                    ResetSequence);
                diagnostic = string.Empty;
                return true;
            }

            public bool TryReset(
                string sessionId,
                int generation,
                int expectedNextResetSequence,
                out int appliedResetSequence,
                out string diagnostic)
            {
                ResetCallCount++;
                bool valid = string.Equals(
                                 sessionId,
                                 SessionId,
                                 StringComparison.Ordinal) &&
                             generation == Generation &&
                             expectedNextResetSequence == ResetSequence + 1 &&
                             expectedNextResetSequence <=
                                 FirstUserOnboardingEnvironmentBudget
                                     .MaximumEncounterResetSequence;
                if (valid)
                {
                    ResetSequence = expectedNextResetSequence;
                    CurrentHitPoints = InitialHitPoints;
                    IsReady = true;
                    PresentationState =
                        FirstUserOnboardingEncounterPresentationState.Idle;
                }

                appliedResetSequence = ResetSequence;
                diagnostic = valid ? string.Empty : "test_reset_rejected";
                return valid;
            }
        }

        private static string SliceBetween(
            string value,
            string startToken,
            string endToken)
        {
            int start = value.IndexOf(startToken, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startToken);
            int end = value.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endToken);
            return value.Substring(start, end - start);
        }
    }

    public sealed class FirstUserOnboardingEnvironmentTests
    {
        private const string SessionId = "1234567890abcdef1234567890abcdef";
        private TestEnvironmentLease _lease;
        private Scene _previewScene;

        [TearDown]
        public void TearDown()
        {
            _lease?.Dispose();
            _lease = null;
            if (_previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(_previewScene);
                _previewScene = default;
            }
        }

        [Test]
        public void ExactEightByTwelveTestDoublePassesBoundedValidation()
        {
            _lease = TestEnvironmentLease.Create();
            FirstUserOnboardingEnvironmentValidation result = Validate(_lease);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Failure, Is.EqualTo(
                FirstUserOnboardingEnvironmentFailure.None));
            Assert.That(result.RendererCount, Is.Zero);
            Assert.That(result.SharedMaterialCount, Is.Zero);
            Assert.That(result.VisibleTriangles, Is.Zero);
        }

        [Test]
        public void RealUserRunRejectsUnitTestDoubleBeforeAssetFallback()
        {
            _lease = TestEnvironmentLease.Create();
            FirstUserOnboardingEnvironmentValidation result =
                FirstUserOnboardingEnvironmentValidator.Validate(
                    new FirstUserOnboardingEnvironmentRequest(
                        SessionId,
                        generation: 3,
                        _lease.OriginalScene,
                        allowUnitTestDouble: false),
                    _lease);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Failure, Is.EqualTo(
                FirstUserOnboardingEnvironmentFailure.UnitTestDoubleNotAllowed));
        }

        [Test]
        public void RealHostContainsNoPrimitiveFallbackAndRequiresAuthoredRequest()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL/Scripts/Editor/Development/FirstUserGameTest/" +
                "FirstUserGameTestRuntimeHost.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("GameObject.CreatePrimitive"));
            Assert.That(source, Does.Contain("allowUnitTestDouble: false"));
            Assert.That(source, Does.Contain(
                "Primitive fallback is forbidden for a user playtest"));
        }

        [Test]
        public void RealRegistrationRemainsClosedUntilRegistryOwnedManifestExists()
        {
            object owner = new object();
            var factory = new RejectingEnvironmentFactory();
            Assert.That(FirstUserOnboardingFixedAssetManifestGate.TryAuthorizeRegistration(
                owner,
                factory,
                out IFirstUserOnboardingAssetInventoryVerifier verifier), Is.False);
            Assert.That(verifier, Is.Null);
            Assert.That(FirstUserOnboardingEnvironmentRegistry.TryRegister(
                owner,
                factory), Is.False);
        }

        [Test]
        public void EnvironmentFactoryBoundaryRejectsReparentedPreexistingSceneObject()
        {
            var existing = new GameObject("PreexistingFactoryBoundaryObject");
            existing.AddComponent<BoxCollider>();
            var providerRoot = new GameObject("ProviderOwnedRoot");
            try
            {
                Assert.That(
                    FirstUserGameTestRuntimeHost.TryVerifyEnvironmentFactoryMutationForTests(
                        () => existing.transform.SetParent(providerRoot.transform, false),
                        out string diagnostic),
                    Is.False);
                Assert.That(diagnostic, Does.Contain("pre-existing scene component"));
            }
            finally
            {
                existing.transform.SetParent(null, false);
                UnityEngine.Object.DestroyImmediate(existing);
                UnityEngine.Object.DestroyImmediate(providerRoot);
            }
        }

        [Test]
        public void EnvironmentFactoryBoundaryRejectsPreexistingComponentStateMutation()
        {
            var existing = new GameObject("PreexistingFactoryStateObject");
            BoxCollider collider = existing.AddComponent<BoxCollider>();
            try
            {
                Assert.That(
                    FirstUserGameTestRuntimeHost.TryVerifyEnvironmentFactoryMutationForTests(
                        () => collider.size = new Vector3(2f, 3f, 4f),
                        out string diagnostic),
                    Is.False);
                Assert.That(diagnostic, Does.Contain("pre-existing scene component"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        [Test]
        public void AssetInventoryVerifierCallbackCannotMutatePreexistingSceneState()
        {
            var existing = new GameObject("AdversarialAssetVerifierTarget");
            BoxCollider collider = existing.AddComponent<BoxCollider>();
            try
            {
                Assert.That(
                    FirstUserGameTestRuntimeHost
                        .TryVerifyAssetInventoryCallbackMutationForTests(
                            () => collider.center = new Vector3(4f, 5f, 6f),
                            out string diagnostic),
                    Is.False);
                Assert.That(diagnostic, Does.Contain("pre-existing scene component"));

                string hostSource = File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "AL/Scripts/Editor/Development/FirstUserGameTest/" +
                    "FirstUserGameTestRuntimeHost.cs"));
                Assert.That(hostSource, Does.Contain(
                    "TryValidateAuthoredEnvironmentProviderBoundary("));
                Assert.That(hostSource, Does.Contain(
                    "TryValidateNonAuthoritativeEncounterBoundary("));
                Assert.That(hostSource, Does.Contain(
                    "selectedBeforeProviderVerification"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        [Test]
        public void AuthoredModuleRequiresEveryBoundChampionGearEnemyStructureAndPbrSlot()
        {
            _lease = TestEnvironmentLease.Create();
            _lease.SourceKindValue =
                FirstUserOnboardingEnvironmentSourceKind.AuthoredModule;

            Assert.That(Validate(_lease).Failure, Is.EqualTo(
                FirstUserOnboardingEnvironmentFailure.AssetCompletenessMissing));
        }

        [Test]
        public void EnvironmentIdentityMustMatchExactSessionAndGeneration()
        {
            _lease = TestEnvironmentLease.Create();
            _lease.SessionIdValue = "abcdef1234567890abcdef1234567890";

            Assert.That(Validate(_lease).Failure, Is.EqualTo(
                FirstUserOnboardingEnvironmentFailure.IdentityMismatch));
        }

        [Test]
        public void EnvironmentRootMustRemainInExactRequestedScene()
        {
            _lease = TestEnvironmentLease.Create();
            _previewScene = EditorSceneManager.NewPreviewScene();
            SceneManager.MoveGameObjectToScene(_lease.RootValue, _previewScene);

            Assert.That(Validate(_lease).Failure, Is.EqualTo(
                FirstUserOnboardingEnvironmentFailure.SceneMismatch));
        }

        [TestCase(7f, 12f)]
        [TestCase(8f, 11f)]
        public void EnvironmentRequiresExactEightByTwelveWalkableBounds(
            float width,
            float length)
        {
            _lease = TestEnvironmentLease.Create();
            _lease.WalkableBoundsValue = new Bounds(
                new Vector3(0f, 1.5f, 0f),
                new Vector3(width, 3f, length));

            Assert.That(Validate(_lease).Failure, Is.EqualTo(
                FirstUserOnboardingEnvironmentFailure.WalkableBoundsInvalid));
        }

        [Test]
        public void EnvironmentRequiresIntentionalMovementPathInsideBounds()
        {
            _lease = TestEnvironmentLease.Create();
            _lease.MovementEndValue = _lease.MovementStartValue + Vector3.right * 0.5f;

            Assert.That(Validate(_lease).Failure, Is.EqualTo(
                FirstUserOnboardingEnvironmentFailure.MovementPathInvalid));
        }

        [Test]
        public void AuthoredInventoryContractRequiresCharacterControllerSafeTraversalProof()
        {
            Assert.That(
                typeof(IFirstUserOnboardingAssetInventoryVerifier).GetMethod(
                    "TryVerifyCharacterControllerSafeTraversal"),
                Is.Not.Null,
                "The sealed authored inventory gate must prove capsule clearance, support, and " +
                "an unobstructed XZ proof route before it can be opened for a user run.");
        }

        [Test]
        public void AuthoredInventoryContractRequiresExactGameplaySlotProofs()
        {
            Type verifier = typeof(IFirstUserOnboardingAssetInventoryVerifier);
            Assert.That(verifier.GetMethod("TryVerifyChampionRigAndLoadout"), Is.Not.Null);
            Assert.That(verifier.GetMethod("TryVerifyMechanicsEncounterSlot"), Is.Not.Null);
            Assert.That(
                verifier.GetMethod("TryVerifyLockedKingdomStructureSlot"),
                Is.Not.Null);
            Assert.That(
                verifier.GetMethod("TryVerifyRuntimeComponentInventory"),
                Is.Not.Null,
                "The sealed manifest gate must approve every runtime component in the authored bundle.");
            Assert.That(
                FirstUserOnboardingKingdomStructureMode.LockedPreviewOnly,
                Is.Not.EqualTo(FirstUserOnboardingKingdomStructureMode.Invalid));
        }

        [Test]
        public void FactoryBoundaryAndResumeIdentityCoverEveryRuntimeAuthoritySurface()
        {
            string hostSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/Editor/Development/FirstUserGameTest/" +
                "FirstUserGameTestRuntimeHost.cs"));
            string environmentSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/Editor/Development/FirstUserGameTest/" +
                "FirstUserOnboardingEnvironment.cs"));
            string tutorialSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/Editor/Development/FirstUserGameTest/" +
                "FirstUserGameTestTutorialRuntime.cs"));

            Assert.That(hostSource, Does.Contain(
                "Resources.FindObjectsOfTypeAll<Component>()"));
            Assert.That(hostSource, Does.Contain(
                "typeof(ServiceLocator).GetField("));
            Assert.That(hostSource, Does.Contain(
                "ServiceRegistrationsMatch(before.Services, servicesAfter)"));
            Assert.That(environmentSource, Does.Contain(
                "TryVerifyRuntimeComponentInventory"));
            Assert.That(hostSource, Does.Contain(
                "TryCaptureProviderDisposalBoundary("));
            Assert.That(hostSource, Does.Contain(
                "TryValidateProviderDisposalBoundary("));
            Assert.That(hostSource, Does.Contain(
                "TryReadEnvironmentLeaseDisposalState("));
            Assert.That(tutorialSource, Does.Contain(
                "_mutationBoundary.TryCapture("));
            Assert.That(tutorialSource, Does.Contain(
                "_mutationBoundary.TryValidate("));
            Assert.That(tutorialSource, Does.Contain(
                "TryReadEncounterSnapshot("));
            Assert.That(hostSource, Does.Contain(
                "TryValidateAuthoredEnvironmentProviderBoundary("));
            Assert.That(hostSource, Does.Contain(
                "selectedBeforeProviderVerification"));

            string policySource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL/Scripts/Editor/Development/FirstUserGameTest/" +
                "FirstUserIsolatedRuntimePolicy.cs"));
            Assert.That(policySource, Does.Contain(
                "RuntimeInitializeLoadType.SubsystemRegistration"));
            Assert.That(policySource, Does.Contain("HandleOwnedSceneUnloaded"));

            int identityStart = hostSource.IndexOf(
                "private sealed class EnvironmentLeaseIdentity",
                StringComparison.Ordinal);
            int identityEnd = hostSource.IndexOf(
                "private bool TryCaptureEnvironmentIdentity(",
                identityStart,
                StringComparison.Ordinal);
            Assert.That(identityStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(identityEnd, Is.GreaterThan(identityStart));
            string identitySource = hostSource.Substring(
                identityStart,
                identityEnd - identityStart);
            PropertyInfo[] properties =
                typeof(IFirstUserOnboardingEnvironmentLease).GetProperties();
            for (int index = 0; index < properties.Length; index++)
            {
                if (string.Equals(
                        properties[index].Name,
                        "IsDisposed",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.That(
                    identitySource,
                    Does.Contain("lease." + properties[index].Name),
                    properties[index].Name);
            }
        }

        [Test]
        public void EnvironmentRequiresCharacterControllerSafePlayer()
        {
            _lease = TestEnvironmentLease.Create();
            _lease.PlayerControllerValue.enabled = false;

            Assert.That(Validate(_lease).Failure, Is.EqualTo(
                FirstUserOnboardingEnvironmentFailure.PlayerControllerInvalid));
        }

        [Test]
        public void EnvironmentRequiresCameraOmenLightingAndPresentationHooks()
        {
            _lease = TestEnvironmentLease.Create();
            _lease.PresentationHookValue = null;

            Assert.That(Validate(_lease).Failure, Is.EqualTo(
                FirstUserOnboardingEnvironmentFailure.PresentationHookInvalid));
        }

        [Test]
        public void EnvironmentCannotCreateASecondEventSystemOrInputOwner()
        {
            _lease = TestEnvironmentLease.Create();
            var inputRoot = new GameObject(
                "ForbiddenEnvironmentEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            inputRoot.transform.SetParent(_lease.RootValue.transform, false);

            Assert.That(Validate(_lease).Failure, Is.EqualTo(
                FirstUserOnboardingEnvironmentFailure.ForbiddenAuthorityPresent));
        }

        [Test]
        public void EnvironmentTexelDensityIsRestrictedToAuthoringOrLowTier()
        {
            _lease = TestEnvironmentLease.Create();
            _lease.EffectiveTexelsPerMeterValue = 64;

            Assert.That(Validate(_lease).Failure, Is.EqualTo(
                FirstUserOnboardingEnvironmentFailure.TexelDensityInvalid));
        }

        [Test]
        public void EnvironmentLeaseDisposalDestroysOnlyItsExactOwnedRoot()
        {
            _lease = TestEnvironmentLease.Create();
            GameObject owned = _lease.OwnedRoot;
            var unrelated = new GameObject("UnrelatedRoot");

            _lease.Dispose();

            Assert.That(_lease.IsDisposed, Is.True);
            Assert.That(owned == null, Is.True);
            Assert.That(unrelated, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(unrelated);
            _lease = null;
        }

        private static FirstUserOnboardingEnvironmentValidation Validate(
            TestEnvironmentLease lease)
        {
            return FirstUserOnboardingEnvironmentValidator.Validate(
                new FirstUserOnboardingEnvironmentRequest(
                    SessionId,
                    generation: 3,
                    lease.OriginalScene,
                    allowUnitTestDouble: true),
                lease);
        }

        private sealed class TestEnvironmentLease : IFirstUserOnboardingEnvironmentLease
        {
            private readonly GameObject _originalRoot;

            private TestEnvironmentLease(GameObject root)
            {
                _originalRoot = root;
                RootValue = root;
                OriginalScene = root.scene;
                SessionIdValue = FirstUserOnboardingEnvironmentTests.SessionId;
                GenerationValue = 3;
                ModuleIdValue = "onboarding_neutral_test_double";
                ContentFingerprintValue = new string('a', 64);
                SourceKindValue = FirstUserOnboardingEnvironmentSourceKind.UnitTestDouble;
                SceneAnchorValue = Child(root, "SceneAnchor", Vector3.zero);
                SpawnAnchorValue = Child(root, "SpawnAnchor", Vector3.zero);
                MovementStartValue = Vector3.zero;
                MovementEndValue = Vector3.forward * 2f;
                WalkableBoundsValue = new Bounds(
                    new Vector3(0f, 1.5f, 0f),
                    new Vector3(8f, 3f, 12f));
                AttackSafeBoundsValue = new Bounds(
                    new Vector3(0f, 1f, 2f),
                    new Vector3(4f, 2f, 4f));
                var player = new GameObject("PlayerController");
                player.transform.SetParent(root.transform, false);
                PlayerControllerValue = player.AddComponent<CharacterController>();
                PlayerChampionValue = player.AddComponent<ChampionController>();
                var cameraObject = new GameObject("PrimaryCamera");
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.tag = "MainCamera";
                PrimaryCameraValue = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                CameraAnchorValue = Child(root, "CameraAnchor", new Vector3(0f, 2f, -3f));
                CameraTargetValue = Child(root, "CameraTarget", Vector3.up);
                OmenAnchorValue = Child(root, "OmenAnchor", Vector3.forward * 3f);
                LightingHookValue = Child(root, "LightingHook", Vector3.zero);
                PresentationHookValue = Child(root, "PresentationHook", Vector3.zero);
                EffectiveTexelsPerMeterValue = 128;
            }

            internal static TestEnvironmentLease Create()
            {
                return new TestEnvironmentLease(new GameObject("OnboardingEnvironmentTestDouble"));
            }

            internal UnityEngine.SceneManagement.Scene OriginalScene { get; }
            internal string SessionIdValue { get; set; }
            internal int GenerationValue { get; set; }
            internal string ModuleIdValue { get; set; }
            internal string ContentFingerprintValue { get; set; }
            internal FirstUserOnboardingEnvironmentSourceKind SourceKindValue { get; set; }
            internal GameObject RootValue { get; set; }
            internal Transform SceneAnchorValue { get; set; }
            internal Transform SpawnAnchorValue { get; set; }
            internal Bounds WalkableBoundsValue { get; set; }
            internal Vector3 MovementStartValue { get; set; }
            internal Vector3 MovementEndValue { get; set; }
            internal Bounds AttackSafeBoundsValue { get; set; }
            internal CharacterController PlayerControllerValue { get; set; }
            internal ChampionController PlayerChampionValue { get; set; }
            internal Camera PrimaryCameraValue { get; set; }
            internal Transform CameraAnchorValue { get; set; }
            internal Transform CameraTargetValue { get; set; }
            internal Transform OmenAnchorValue { get; set; }
            internal Transform LightingHookValue { get; set; }
            internal Transform PresentationHookValue { get; set; }
            internal int EffectiveTexelsPerMeterValue { get; set; }

            public string SessionId => SessionIdValue;
            public int Generation => GenerationValue;
            public string ModuleId => ModuleIdValue;
            public string ContentFingerprint => ContentFingerprintValue;
            public string AssetInventoryFingerprint => string.Empty;
            public FirstUserOnboardingEnvironmentSourceKind SourceKind => SourceKindValue;
            public GameObject OwnedRoot => RootValue;
            public UnityEngine.Object EnvironmentModuleSourceAsset => null;
            public string EnvironmentModuleAssetId => string.Empty;
            public GameObject NeutralEnvironmentRoot => RootValue;
            public Transform SceneAnchor => SceneAnchorValue;
            public Transform SpawnAnchor => SpawnAnchorValue;
            public Bounds WalkableBounds => WalkableBoundsValue;
            public Vector3 MovementProofStart => MovementStartValue;
            public Vector3 MovementProofEnd => MovementEndValue;
            public Bounds AttackSafeBounds => AttackSafeBoundsValue;
            public CharacterController PlayerController => PlayerControllerValue;
            public ChampionController PlayerChampion => PlayerChampionValue;
            public Camera PrimaryCamera => PrimaryCameraValue;
            public Transform PrimaryCameraAnchor => CameraAnchorValue;
            public Transform PrimaryCameraTarget => CameraTargetValue;
            public Transform OmenAnchor => OmenAnchorValue;
            public Transform LightingHook => LightingHookValue;
            public Transform PresentationHook => PresentationHookValue;
            public GameObject ModularChampionRoot => null;
            public string ChampionAssetId => string.Empty;
            public UnityEngine.Object ChampionSourceAsset => null;
            public GameObject SelectedArmorRoot => null;
            public string ArmorAssetId => string.Empty;
            public UnityEngine.Object ArmorSourceAsset => null;
            public GameObject SelectedWeaponRoot => null;
            public string WeaponAssetId => string.Empty;
            public UnityEngine.Object WeaponSourceAsset => null;
            public GameObject EnemyRoot => null;
            public string EnemyAssetId => string.Empty;
            public UnityEngine.Object EnemySourceAsset => null;
            public FirstUserOnboardingEnemyCandidateKind EnemyCandidateKind =>
                FirstUserOnboardingEnemyCandidateKind.Invalid;
            public FirstUserOnboardingEncounterMode EncounterMode =>
                FirstUserOnboardingEncounterMode.Invalid;
            public IFirstUserOnboardingEnemyEncounter EnemyEncounter => null;
            public Transform EnemySpawnAnchor => null;
            public GameObject KingdomStructureRoot => null;
            public string KingdomStructureAssetId => string.Empty;
            public UnityEngine.Object KingdomStructureSourceAsset => null;
            public FirstUserOnboardingKingdomStructureMode KingdomStructureMode =>
                FirstUserOnboardingKingdomStructureMode.Invalid;
            public Material FloorMaterial => null;
            public string FloorMaterialAssetId => string.Empty;
            public Material WallMaterial => null;
            public string WallMaterialAssetId => string.Empty;
            public Material TrimMaterial => null;
            public string TrimMaterialAssetId => string.Empty;
            public Transform PropsRoot => null;
            public GameObject FloorModuleRoot => null;
            public GameObject WallModuleRoot => null;
            public GameObject InnerCornerModuleRoot => null;
            public GameObject OuterCornerModuleRoot => null;
            public GameObject DoorwayModuleRoot => null;
            public GameObject CeilingBeamModuleRoot => null;
            public GameObject TrimModuleRoot => null;
            public GameObject BrazierPropRoot => null;
            public GameObject BannerStandPropRoot => null;
            public GameObject CrateBarrelPropRoot => null;
            public int EffectiveTexelsPerMeter => EffectiveTexelsPerMeterValue;
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                if (_originalRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(_originalRoot);
                }
            }

            private static Transform Child(GameObject root, string name, Vector3 position)
            {
                var child = new GameObject(name);
                child.transform.SetParent(root.transform, false);
                child.transform.position = position;
                return child.transform;
            }
        }

        private sealed class RejectingEnvironmentFactory :
            IFirstUserOnboardingEnvironmentFactory
        {
            public bool TryCreate(
                FirstUserOnboardingEnvironmentRequest request,
                out IFirstUserOnboardingEnvironmentLease lease,
                out string diagnostic)
            {
                lease = null;
                diagnostic = "not_used";
                return false;
            }
        }

    }
}
