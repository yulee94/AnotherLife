using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AL.Warmaster.Planning;
using NUnit.Framework;

namespace AL.Tests.EditMode.Warmaster.Planning
{
    public sealed class WarmasterTransactionPlannerTests
    {
        private const string ProfileId = "profile_fixture_001";
        private const string ActorId = "actor_fixture_001";
        private const string SetId = "fixture_set_alpha";
        private const string FirstPieceId = "fixture_piece_alpha";
        private const string SecondPieceId = "fixture_piece_beta";
        private const string CurrencyId = "fixture_credit";
        private static readonly string CatalogHash = new string('a', 64);
        private static readonly string GenerationHash = new string('f', 64);

        private WarmasterCatalogSnapshot catalog;
        private FakeAuthority authority;
        private WarmasterTransactionPlanner planner;

        [SetUp]
        public void SetUp()
        {
            catalog = Catalog(
                WarmasterUnlockPolicy.ManualAfterCompletion,
                WarmasterEquipPolicy.ManualOnly);
            authority = new FakeAuthority();
            planner = new WarmasterTransactionPlanner(catalog, authority);
        }

        [Test]
        public void PurchaseUsesDefinitionOwnedPriceAndOnlyVerifiedCommitMintsReceipt()
        {
            WarmasterStateSnapshot initial = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterTransactionRequest request = PurchaseRequest(
                initial,
                wallet,
                FirstPieceId,
                "purchase_fixture_alpha",
                "event_fixture_alpha");

            WarmasterPlanningResult result = planner.Plan(request, initial, wallet);

            Assert.That(result.IsPrepared, Is.True);
            Assert.That(result.Plan.RequiresEconomyDebit, Is.True);
            Assert.That(result.Plan.EconomyDebit.CurrencyId, Is.EqualTo(CurrencyId));
            Assert.That(result.Plan.EconomyDebit.Amount, Is.EqualTo(7));
            Assert.That(result.Plan.EconomyDebit.CandidateBalance, Is.EqualTo(93));
            Assert.That(result.Plan.EconomyDebit.CandidateRevision, Is.EqualTo(1));
            Assert.That(result.Plan.CandidateState.Level, Is.EqualTo(1));
            Assert.That(result.Plan.CandidateState.Experience, Is.EqualTo(3));
            Assert.That(
                result.Plan.CandidateState.PurchasedPieces.Select(row => row.PieceId),
                Is.EqualTo(new[] { FirstPieceId }));
            Assert.That(initial.PurchasedPieces, Is.Empty);
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(result.ExistingReceipt, Is.Null);
            Assert.That(result.Plan.PostCommitNotificationCorrelationId, Has.Length.EqualTo(64));

            WarmasterStateSnapshot committed = Verify(
                result,
                out WarmasterWalletSnapshot committedWallet,
                out WarmasterVerifiedReceipt receipt);

            Assert.That(committedWallet.Balance, Is.EqualTo(93));
            Assert.That(receipt.ReceiptHash, Has.Length.EqualTo(64));
            Assert.That(receipt.VerifiedStateRevision, Is.EqualTo(1));
            Assert.That(receipt.VerifiedEconomyRevision, Is.EqualTo(1));
            Assert.That(
                receipt.PostCommitNotificationCorrelationId,
                Is.EqualTo(result.Plan.PostCommitNotificationCorrelationId));

            WarmasterPlanningResult ledgerReplay = planner.Plan(request, committed, committedWallet);
            WarmasterPlanningResult receiptReplay = planner.Plan(
                WithReceipt(request, receipt),
                null,
                null);

            AssertDuplicate(ledgerReplay, hasReceipt: false);
            AssertDuplicate(receiptReplay, hasReceipt: true);
        }

        [Test]
        public void ManualFixtureRequiresExactCompletionThenUnlockAndEquip()
        {
            WarmasterStateSnapshot state = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            state = PurchaseAndVerify(
                planner,
                state,
                wallet,
                FirstPieceId,
                "purchase_manual_alpha",
                "event_manual_alpha",
                out wallet);

            WarmasterPlanningResult earlyUnlock = planner.Plan(
                SetRequest(
                    WarmasterOperation.UnlockSet,
                    state,
                    "unlock_manual_early",
                    "event_manual_early"),
                state);
            Assert.That(earlyUnlock.Status, Is.EqualTo(WarmasterPlanStatus.Ineligible));

            state = PurchaseAndVerify(
                planner,
                state,
                wallet,
                SecondPieceId,
                "purchase_manual_beta",
                "event_manual_beta",
                out wallet);

            Assert.That(state.UnlockedSets, Is.Empty);
            Assert.That(state.EquippedSetId, Is.Empty);

            WarmasterPlanningResult unlock = planner.Plan(
                SetRequest(
                    WarmasterOperation.UnlockSet,
                    state,
                    "unlock_manual_alpha",
                    "event_unlock_manual_alpha"),
                state);
            state = Verify(unlock, out WarmasterWalletSnapshot noWallet, out _);

            Assert.That(noWallet, Is.Null);
            Assert.That(state.UnlockedSets.Single().SetId, Is.EqualTo(SetId));
            Assert.That(state.EquippedSetId, Is.Empty);

            WarmasterPlanningResult equip = planner.Plan(
                SetRequest(
                    WarmasterOperation.EquipSet,
                    state,
                    "equip_manual_alpha",
                    "event_equip_manual_alpha"),
                state);
            state = Verify(equip, out noWallet, out _);

            Assert.That(noWallet, Is.Null);
            Assert.That(state.EquippedSetId, Is.EqualTo(SetId));
            Assert.That(
                planner.Plan(
                    SetRequest(
                        WarmasterOperation.EquipSet,
                        state,
                        "equip_manual_repeat",
                        "event_equip_manual_repeat"),
                    state).Status,
                Is.EqualTo(WarmasterPlanStatus.NoChange));
        }

        [Test]
        public void AutomaticFixtureUnlocksAndEquipsOnlyAfterLastExactMember()
        {
            WarmasterCatalogSnapshot automaticCatalog = Catalog(
                WarmasterUnlockPolicy.AutomaticOnCompletion,
                WarmasterEquipPolicy.AutomaticOnUnlock);
            var automaticPlanner = new WarmasterTransactionPlanner(
                automaticCatalog,
                authority);
            WarmasterStateSnapshot state = InitialState(automaticCatalog.Binding);
            WarmasterWalletSnapshot wallet = Wallet();
            state = PurchaseAndVerify(
                automaticPlanner,
                state,
                wallet,
                FirstPieceId,
                "purchase_auto_alpha",
                "event_auto_alpha",
                out wallet);

            Assert.That(state.UnlockedSets, Is.Empty);
            Assert.That(state.EquippedSetId, Is.Empty);

            state = PurchaseAndVerify(
                automaticPlanner,
                state,
                wallet,
                SecondPieceId,
                "purchase_auto_beta",
                "event_auto_beta",
                out wallet);

            Assert.That(state.UnlockedSets.Single().SetId, Is.EqualTo(SetId));
            Assert.That(state.EquippedSetId, Is.EqualTo(SetId));
            Assert.That(state.Level, Is.EqualTo(1));
            Assert.That(state.Experience, Is.EqualTo(8));
        }

        [Test]
        public void WalletAuthorityFailsClosedWithoutMutatingInput()
        {
            WarmasterStateSnapshot state = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterTransactionRequest request = PurchaseRequest(
                state,
                wallet,
                FirstPieceId,
                "purchase_wallet_gate",
                "event_wallet_gate");

            Assert.That(planner.Plan(request, state, null).Status,
                Is.EqualTo(WarmasterPlanStatus.Unavailable));
            Assert.That(
                planner.Plan(
                    request,
                    state,
                    new WarmasterWalletSnapshot(
                        WarmasterWalletStatus.CommitUncertain,
                        CurrencyId,
                        100,
                        0,
                        true)).Status,
                Is.EqualTo(WarmasterPlanStatus.CommitUncertain));
            Assert.That(
                planner.Plan(
                    request,
                    state,
                    new WarmasterWalletSnapshot(
                        WarmasterWalletStatus.Malformed,
                        CurrencyId,
                        100,
                        0,
                        true)).Status,
                Is.EqualTo(WarmasterPlanStatus.Malformed));
            Assert.That(
                planner.Plan(
                    request,
                    state,
                    new WarmasterWalletSnapshot(
                        WarmasterWalletStatus.Available,
                        CurrencyId,
                        6,
                        0,
                        true)).Status,
                Is.EqualTo(WarmasterPlanStatus.InsufficientFunds));
            Assert.That(
                planner.Plan(
                    request,
                    state,
                    new WarmasterWalletSnapshot(
                        WarmasterWalletStatus.Available,
                        CurrencyId,
                        100,
                        1,
                        true)).Status,
                Is.EqualTo(WarmasterPlanStatus.StaleEconomy));
            Assert.That(
                planner.Plan(
                    request,
                    state,
                    new WarmasterWalletSnapshot(
                        WarmasterWalletStatus.Available,
                        "wrong_credit",
                        100,
                        0,
                        true)).Status,
                Is.EqualTo(WarmasterPlanStatus.Malformed));
            Assert.That(state.Revision, Is.Zero);
            Assert.That(state.PurchasedPieces, Is.Empty);
        }

        [Test]
        public void CatalogAndApprovalFailuresRejectBeforeAnyDebit()
        {
            WarmasterStateSnapshot state = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterTransactionRequest request = PurchaseRequest(
                state,
                wallet,
                FirstPieceId,
                "purchase_catalog_gate",
                "event_catalog_gate");

            AssertCatalogStatus(request, state, wallet,
                WarmasterCatalogStatus.Unavailable, WarmasterPlanStatus.Unavailable);
            AssertCatalogStatus(request, state, wallet,
                WarmasterCatalogStatus.UnsupportedVersion, WarmasterPlanStatus.Unsupported);
            AssertCatalogStatus(request, state, wallet,
                WarmasterCatalogStatus.Incomplete, WarmasterPlanStatus.Unavailable);
            AssertCatalogStatus(request, state, wallet,
                WarmasterCatalogStatus.ApprovalMissing, WarmasterPlanStatus.ApprovalMissing);
            AssertCatalogStatus(request, state, wallet,
                WarmasterCatalogStatus.Malformed, WarmasterPlanStatus.Malformed);

            WarmasterCatalogSnapshot unapproved = new WarmasterCatalogSnapshot(
                WarmasterCatalogStatus.Ready,
                catalog.Binding,
                catalog.Sets,
                new[]
                {
                    Piece(FirstPieceId, 7, 1, 3, isApproved: false),
                    Piece(SecondPieceId, 11, 0, 5)
                },
                true);
            Assert.That(
                new WarmasterTransactionPlanner(unapproved, authority)
                    .Plan(request, state, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.ApprovalMissing));

            WarmasterCatalogSnapshot duplicateMembership = new WarmasterCatalogSnapshot(
                WarmasterCatalogStatus.Ready,
                catalog.Binding,
                new[]
                {
                    Set(SetId, new[] { FirstPieceId, SecondPieceId }),
                    Set("fixture_set_beta", new[] { FirstPieceId })
                },
                catalog.Pieces,
                true);
            Assert.That(
                new WarmasterTransactionPlanner(duplicateMembership, authority)
                    .Plan(request, state, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Malformed));

            WarmasterCatalogBinding staleBinding = Binding("fixture_content_v2");
            WarmasterTransactionRequest stale = CopyRequest(
                request,
                expectedCatalogBinding: staleBinding);
            Assert.That(planner.Plan(stale, state, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.StaleCatalog));
            Assert.That(wallet.Balance, Is.EqualTo(100));
        }

        [Test]
        public void StateAuthorityMigrationAndAuthorizationFailuresAreExplicit()
        {
            WarmasterStateSnapshot state = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterTransactionRequest request = PurchaseRequest(
                state,
                wallet,
                FirstPieceId,
                "purchase_state_gate",
                "event_state_gate");

            AssertStateStatus(request, wallet,
                WarmasterStateStatus.Unavailable, WarmasterPlanStatus.Unavailable);
            AssertStateStatus(request, wallet,
                WarmasterStateStatus.MigrationRequired, WarmasterPlanStatus.MigrationRequired);
            AssertStateStatus(request, wallet,
                WarmasterStateStatus.UnsupportedReadOnly, WarmasterPlanStatus.Unsupported);
            AssertStateStatus(request, wallet,
                WarmasterStateStatus.CommitUncertain, WarmasterPlanStatus.CommitUncertain);
            AssertStateStatus(request, wallet,
                WarmasterStateStatus.Malformed, WarmasterPlanStatus.Malformed);

            WarmasterStateSnapshot legacy = CopyState(
                state,
                legacyTrueWarmasterFlag: true);
            Assert.That(planner.Plan(request, legacy, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.MigrationRequired));

            authority.Status = WarmasterAuthorizationStatus.Denied;
            Assert.That(planner.Plan(request, state, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Unauthorized));
            authority.Status = WarmasterAuthorizationStatus.Unavailable;
            Assert.That(planner.Plan(request, state, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Unavailable));

            authority.Status = WarmasterAuthorizationStatus.Allowed;
            Assert.That(
                planner.Plan(
                    CopyRequest(request, expectedStateRevision: 1),
                    state,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.StaleState));
            Assert.That(
                planner.Plan(
                    CopyRequest(request, profileId: "another_profile"),
                    state,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Conflict));
        }

        [Test]
        public void UnknownDefinitionsOwnershipAndSetEligibilityRejectSafely()
        {
            WarmasterStateSnapshot state = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();

            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        state,
                        wallet,
                        "fixture_piece_missing",
                        "purchase_missing",
                        "event_missing"),
                    state,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.UnknownDefinition));
            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        state,
                        wallet,
                        FirstPieceId,
                        "purchase_wrong_set",
                        "event_wrong_set",
                        setId: "fixture_set_missing"),
                    state,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.UnknownDefinition));
            Assert.That(
                planner.Plan(
                    SetRequest(
                        WarmasterOperation.EquipSet,
                        state,
                        "equip_locked",
                        "event_equip_locked"),
                    state).Status,
                Is.EqualTo(WarmasterPlanStatus.Ineligible));

            state = PurchaseAndVerify(
                planner,
                state,
                wallet,
                FirstPieceId,
                "purchase_owned_alpha",
                "event_owned_alpha",
                out wallet);
            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        state,
                        wallet,
                        FirstPieceId,
                        "purchase_owned_repeat",
                        "event_owned_repeat"),
                    state,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.AlreadyOwned));

            WarmasterCatalogSnapshot unavailablePieceCatalog = new WarmasterCatalogSnapshot(
                WarmasterCatalogStatus.Ready,
                catalog.Binding,
                catalog.Sets,
                new[]
                {
                    new WarmasterPieceDefinition(
                        FirstPieceId,
                        SetId,
                        7,
                        WarmasterPieceAvailability.Unavailable,
                        new WarmasterProgressionRule(
                            WarmasterProgressionMode.AddDeltas,
                            1,
                            3,
                            true),
                        true),
                    Piece(SecondPieceId, 11, 0, 5)
                },
                true);
            var unavailablePlanner = new WarmasterTransactionPlanner(
                unavailablePieceCatalog,
                authority);
            Assert.That(
                unavailablePlanner.Plan(
                    PurchaseRequest(
                        InitialState(),
                        Wallet(),
                        FirstPieceId,
                        "purchase_unavailable",
                        "event_unavailable"),
                    InitialState(),
                    Wallet()).Status,
                Is.EqualTo(WarmasterPlanStatus.Ineligible));
        }

        [Test]
        public void OperationAndEventIdentitiesAreIdempotentAndCollisionSafe()
        {
            WarmasterStateSnapshot initial = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterTransactionRequest request = PurchaseRequest(
                initial,
                wallet,
                FirstPieceId,
                "purchase_idempotent_alpha",
                "event_idempotent_alpha");
            WarmasterPlanningResult first = planner.Plan(request, initial, wallet);
            WarmasterStateSnapshot committed = Verify(first, out wallet, out _);

            AssertDuplicate(planner.Plan(request, committed, wallet), hasReceipt: false);
            Assert.That(
                planner.Plan(
                    CopyRequest(request, pieceId: SecondPieceId),
                    committed,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Conflict));
            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        committed,
                        wallet,
                        SecondPieceId,
                        "purchase_event_collision",
                        request.EventId),
                    committed,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Conflict));

            WarmasterStateSnapshot later = PurchaseAndVerify(
                planner,
                committed,
                wallet,
                SecondPieceId,
                "purchase_later_beta",
                "event_later_beta",
                out wallet);
            AssertDuplicate(planner.Plan(request, later, wallet), hasReceipt: false);
        }

        [Test]
        public void UnknownFutureRowsArePreservedButTheirCollisionsExcludeMutation()
        {
            WarmasterTransactionRecord future = FutureRecord(
                "future_operation_001",
                "future_event_001",
                99);
            WarmasterStateSnapshot state = new WarmasterStateSnapshot(
                WarmasterStateStatus.Valid,
                ProfileId,
                0,
                catalog.Binding,
                new[]
                {
                    new WarmasterOwnedPieceRecord("future_piece_gamma", false)
                },
                new[]
                {
                    new WarmasterUnlockedSetRecord("future_set_gamma", false)
                },
                string.Empty,
                false,
                0,
                0,
                new[] { future },
                true);
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterPlanningResult result = planner.Plan(
                PurchaseRequest(
                    state,
                    wallet,
                    FirstPieceId,
                    "purchase_with_future",
                    "event_with_future"),
                state,
                wallet);

            Assert.That(result.IsPrepared, Is.True);
            Assert.That(
                result.Plan.CandidateState.PurchasedPieces.Any(row =>
                    row.PieceId == "future_piece_gamma" && !row.IsSupported),
                Is.True);
            Assert.That(
                result.Plan.CandidateState.TransactionRecords.Any(row =>
                    ReferenceEquals(row, future)),
                Is.True);

            WarmasterTransactionRequest operationCollision = PurchaseRequest(
                state,
                wallet,
                FirstPieceId,
                future.OperationId,
                "different_event");
            Assert.That(
                planner.Plan(operationCollision, state, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Unsupported));

            WarmasterStateSnapshot revisionCollision = CopyState(
                state,
                transactionRecords: new[]
                {
                    FutureRecord("future_operation_002", "future_event_002", 1)
                });
            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        revisionCollision,
                        wallet,
                        FirstPieceId,
                        "purchase_revision_collision",
                        "event_revision_collision"),
                    revisionCollision,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Unsupported));
        }

        [Test]
        public void ReceiptVerificationRejectsTamperedStateWalletAndGeneration()
        {
            Assert.That(
                typeof(WarmasterTransactionPlan).GetConstructors(
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Empty);
            Assert.That(
                typeof(WarmasterVerifiedReceipt).GetConstructors(
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Empty);
            Assert.That(
                typeof(WarmasterEconomyDebitIntent).GetConstructors(
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Empty);

            WarmasterStateSnapshot initial = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterPlanningResult result = planner.Plan(
                PurchaseRequest(
                    initial,
                    wallet,
                    FirstPieceId,
                    "purchase_verify_alpha",
                    "event_verify_alpha"),
                initial,
                wallet);
            WarmasterTransactionPlan plan = result.Plan;

            WarmasterStateSnapshot wrongState = CopyState(
                plan.CandidateState,
                level: plan.CandidateState.Level + 1);
            Assert.That(
                WarmasterTransactionPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    wrongState,
                    CandidateWallet(plan),
                    GenerationHash,
                    out _),
                Is.False);
            Assert.That(
                WarmasterTransactionPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    plan.CandidateState,
                    new WarmasterWalletSnapshot(
                        WarmasterWalletStatus.Available,
                        CurrencyId,
                        plan.EconomyDebit.CandidateBalance + 1,
                        plan.EconomyDebit.CandidateRevision,
                        true),
                    GenerationHash,
                    out _),
                Is.False);
            Assert.That(
                WarmasterTransactionPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    plan.CandidateState,
                    CandidateWallet(plan),
                    "not_a_generation_hash",
                    out _),
                Is.False);

            WarmasterStateSnapshot committed = Verify(
                result,
                out WarmasterWalletSnapshot committedWallet,
                out WarmasterVerifiedReceipt receipt);
            var tamperedReceipt = new WarmasterVerifiedReceipt(
                receipt.TransactionRecord,
                receipt.VerifiedGenerationFingerprint,
                receipt.VerifiedStateRevision,
                receipt.VerifiedEconomyRevision,
                new string('0', 64));
            WarmasterTransactionRequest original = PurchaseRequest(
                initial,
                Wallet(),
                FirstPieceId,
                "purchase_verify_alpha",
                "event_verify_alpha");

            Assert.That(
                planner.Plan(
                    WithReceipt(original, tamperedReceipt),
                    committed,
                    committedWallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Conflict));
        }

        [Test]
        public void EquivalentInputsAreDeterministicAndLengthDelimited()
        {
            WarmasterStateSnapshot state = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterTransactionRequest firstRequest = PurchaseRequest(
                state,
                wallet,
                FirstPieceId,
                "alpha|beta",
                "gamma");
            WarmasterTransactionRequest secondRequest = PurchaseRequest(
                state,
                wallet,
                FirstPieceId,
                "alpha",
                "beta|gamma");

            WarmasterPlanningResult first = planner.Plan(firstRequest, state, wallet);
            WarmasterPlanningResult equivalent = planner.Plan(firstRequest, state, wallet);
            WarmasterPlanningResult second = planner.Plan(secondRequest, state, wallet);

            Assert.That(first.IsPrepared, Is.True);
            Assert.That(equivalent.Plan.RequestFingerprint,
                Is.EqualTo(first.Plan.RequestFingerprint));
            Assert.That(equivalent.Plan.PlanHash,
                Is.EqualTo(first.Plan.PlanHash));
            Assert.That(second.Plan.RequestFingerprint,
                Is.Not.EqualTo(first.Plan.RequestFingerprint));
            Assert.That(second.Plan.PlanHash, Is.Not.EqualTo(first.Plan.PlanHash));
        }

        [Test]
        public void ConstructorsCopyCallerCollectionsAndProgressionOverflowRejects()
        {
            var members = new List<string> { FirstPieceId, SecondPieceId };
            WarmasterSetDefinition set = Set(SetId, members);
            members.Clear();
            Assert.That(set.MemberPieceIds,
                Is.EqualTo(new[] { FirstPieceId, SecondPieceId }));

            var owned = new List<WarmasterOwnedPieceRecord>();
            WarmasterStateSnapshot state = new WarmasterStateSnapshot(
                WarmasterStateStatus.Valid,
                ProfileId,
                0,
                catalog.Binding,
                owned,
                Array.Empty<WarmasterUnlockedSetRecord>(),
                string.Empty,
                false,
                int.MaxValue,
                0,
                Array.Empty<WarmasterTransactionRecord>(),
                true);
            owned.Add(new WarmasterOwnedPieceRecord(FirstPieceId, true));
            Assert.That(state.PurchasedPieces, Is.Empty);

            WarmasterWalletSnapshot wallet = Wallet();
            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        state,
                        wallet,
                        FirstPieceId,
                        "purchase_overflow",
                        "event_overflow"),
                    state,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Overflow));

            WarmasterWalletSnapshot maxWallet = new WarmasterWalletSnapshot(
                WarmasterWalletStatus.Available,
                CurrencyId,
                100,
                long.MaxValue,
                true);
            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        InitialState(),
                        maxWallet,
                        FirstPieceId,
                        "purchase_wallet_overflow",
                        "event_wallet_overflow"),
                    InitialState(),
                    maxWallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Overflow));
        }

        [Test]
        public void CatalogAndStateBoundsAcceptTheLimitAndRejectGrowthPastIt()
        {
            WarmasterCatalogSnapshot boundedCatalog = CatalogAtBounds();
            var boundedPlanner = new WarmasterTransactionPlanner(
                boundedCatalog,
                authority);
            WarmasterStateSnapshot boundedState = InitialState(boundedCatalog.Binding);
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterPlanningResult exactCatalog = boundedPlanner.Plan(
                PurchaseRequest(
                    boundedState,
                    wallet,
                    "fixture_piece_0000",
                    "purchase_catalog_boundary",
                    "event_catalog_boundary",
                    "fixture_set_00"),
                boundedState,
                wallet);
            Assert.That(exactCatalog.IsPrepared, Is.True);

            WarmasterPieceDefinition extraPiece = new WarmasterPieceDefinition(
                "fixture_piece_1024",
                "fixture_set_63",
                1,
                WarmasterPieceAvailability.Available,
                new WarmasterProgressionRule(
                    WarmasterProgressionMode.NoChange,
                    0,
                    0,
                    true),
                true);
            var tooManyPieces = new WarmasterCatalogSnapshot(
                WarmasterCatalogStatus.Ready,
                boundedCatalog.Binding,
                boundedCatalog.Sets,
                boundedCatalog.Pieces.Concat(new[] { extraPiece }),
                true);
            Assert.That(
                new WarmasterTransactionPlanner(tooManyPieces, authority)
                    .Plan(
                        PurchaseRequest(
                            boundedState,
                            wallet,
                            "fixture_piece_0000",
                            "purchase_catalog_overflow",
                            "event_catalog_overflow",
                            "fixture_set_00"),
                        boundedState,
                        wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Malformed));

            WarmasterOwnedPieceRecord[] fullOwned = Enumerable.Range(0, 1024)
                .Select(index => new WarmasterOwnedPieceRecord(
                    "future_piece_" + index.ToString("D4"),
                    false))
                .ToArray();
            WarmasterStateSnapshot fullOwnedState = new WarmasterStateSnapshot(
                WarmasterStateStatus.Valid,
                ProfileId,
                0,
                catalog.Binding,
                fullOwned,
                Array.Empty<WarmasterUnlockedSetRecord>(),
                string.Empty,
                false,
                0,
                0,
                Array.Empty<WarmasterTransactionRecord>(),
                true);
            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        fullOwnedState,
                        wallet,
                        FirstPieceId,
                        "purchase_owned_capacity",
                        "event_owned_capacity"),
                    fullOwnedState,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Malformed));

            WarmasterTransactionRecord[] fullLedger = Enumerable.Range(1, 256)
                .Select(index => FutureRecord(
                    "future_operation_" + index.ToString("D3"),
                    "future_event_" + index.ToString("D3"),
                    index))
                .ToArray();
            WarmasterStateSnapshot fullLedgerState = new WarmasterStateSnapshot(
                WarmasterStateStatus.Valid,
                ProfileId,
                0,
                catalog.Binding,
                Array.Empty<WarmasterOwnedPieceRecord>(),
                Array.Empty<WarmasterUnlockedSetRecord>(),
                string.Empty,
                false,
                0,
                0,
                fullLedger,
                true);
            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        fullLedgerState,
                        wallet,
                        FirstPieceId,
                        "purchase_ledger_capacity",
                        "event_ledger_capacity"),
                    fullLedgerState,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Malformed));
        }

        [Test]
        public void TamperedDefinitionLedgerAndUnorderedHistoryAreMalformed()
        {
            WarmasterStateSnapshot initial = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterPlanningResult purchase = planner.Plan(
                PurchaseRequest(
                    initial,
                    wallet,
                    FirstPieceId,
                    "purchase_ledger_alpha",
                    "event_ledger_alpha"),
                initial,
                wallet);
            WarmasterStateSnapshot committed = Verify(purchase, out wallet, out _);
            WarmasterTransactionRecord source = committed.TransactionRecords.Single();
            WarmasterTransactionRecord tampered = CloneRecord(
                source,
                debitAmount: source.DebitAmount - 1);
            WarmasterStateSnapshot tamperedState = CopyState(
                committed,
                transactionRecords: new[] { tampered });
            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        tamperedState,
                        wallet,
                        SecondPieceId,
                        "purchase_after_tamper",
                        "event_after_tamper"),
                    tamperedState,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Malformed));

            WarmasterStateSnapshot unordered = new WarmasterStateSnapshot(
                WarmasterStateStatus.Valid,
                ProfileId,
                0,
                catalog.Binding,
                Array.Empty<WarmasterOwnedPieceRecord>(),
                Array.Empty<WarmasterUnlockedSetRecord>(),
                string.Empty,
                false,
                0,
                0,
                new[]
                {
                    FutureRecord("future_operation_002", "future_event_002", 2),
                    FutureRecord("future_operation_001", "future_event_001", 1)
                },
                true);
            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        unordered,
                        Wallet(),
                        FirstPieceId,
                        "purchase_unordered",
                        "event_unordered"),
                    unordered,
                    Wallet()).Status,
                Is.EqualTo(WarmasterPlanStatus.Malformed));
        }

        [Test]
        public void UnknownFutureDefinitionCollisionIsUnsupported()
        {
            WarmasterTransactionRecord collision = FutureRecord(
                "future_operation_collision",
                "future_event_collision",
                7,
                SetId,
                FirstPieceId);
            WarmasterStateSnapshot state = new WarmasterStateSnapshot(
                WarmasterStateStatus.Valid,
                ProfileId,
                0,
                catalog.Binding,
                Array.Empty<WarmasterOwnedPieceRecord>(),
                Array.Empty<WarmasterUnlockedSetRecord>(),
                string.Empty,
                false,
                0,
                0,
                new[] { collision },
                true);
            WarmasterWalletSnapshot wallet = Wallet();

            Assert.That(
                planner.Plan(
                    PurchaseRequest(
                        state,
                        wallet,
                        FirstPieceId,
                        "purchase_future_collision",
                        "event_purchase_future_collision"),
                    state,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Unsupported));
        }

        [Test]
        public void InvalidRequestIdentitiesAndOperationFieldsFailClosed()
        {
            WarmasterStateSnapshot state = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterTransactionRequest request = PurchaseRequest(
                state,
                wallet,
                FirstPieceId,
                "purchase_identity",
                "event_identity");

            Assert.That(planner.Plan(null, state, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.InvalidRequest));
            foreach (string invalid in new[]
            {
                string.Empty,
                " ",
                "operation\ncontrol",
                "operation with space",
                new string('x', 129)
            })
            {
                Assert.That(
                    planner.Plan(
                        CopyRequest(request, operationId: invalid),
                        state,
                        wallet).Status,
                    Is.EqualTo(WarmasterPlanStatus.InvalidRequest));
            }

            foreach (string invalid in new[]
            {
                string.Empty,
                "Fixture_piece_alpha",
                "fixture__piece_alpha",
                "fixture_piece_alpha_",
                "fixture-piece-alpha",
                new string('p', 129)
            })
            {
                Assert.That(
                    planner.Plan(
                        CopyRequest(request, pieceId: invalid),
                        state,
                        wallet).Status,
                    Is.EqualTo(WarmasterPlanStatus.InvalidRequest));
            }

            Assert.That(
                planner.Plan(
                    CopyRequest(request, operation: (WarmasterOperation)99),
                    state,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.InvalidRequest));
            Assert.That(
                planner.Plan(
                    CopyRequest(request, expectedEconomyRevision: -1),
                    state,
                    wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.InvalidRequest));
        }

        [Test]
        public void MissingRulesDuplicateDefinitionsAndMembershipDriftFailClosed()
        {
            WarmasterStateSnapshot state = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterTransactionRequest request = PurchaseRequest(
                state,
                wallet,
                FirstPieceId,
                "purchase_malformed_catalog",
                "event_malformed_catalog");
            WarmasterCatalogSnapshot[] malformed =
            {
                new WarmasterCatalogSnapshot(
                    WarmasterCatalogStatus.Ready, catalog.Binding,
                    catalog.Sets.Concat(catalog.Sets), catalog.Pieces, true),
                new WarmasterCatalogSnapshot(
                    WarmasterCatalogStatus.Ready, catalog.Binding,
                    catalog.Sets, catalog.Pieces.Concat(catalog.Pieces), true),
                new WarmasterCatalogSnapshot(
                    WarmasterCatalogStatus.Ready, catalog.Binding,
                    new[] { Set(SetId, new[] { FirstPieceId, FirstPieceId }) },
                    catalog.Pieces, true),
                new WarmasterCatalogSnapshot(
                    WarmasterCatalogStatus.Ready, catalog.Binding,
                    new[] { Set(SetId, new[] { FirstPieceId, "fixture_piece_missing" }) },
                    catalog.Pieces, true),
                new WarmasterCatalogSnapshot(
                    WarmasterCatalogStatus.Ready, catalog.Binding,
                    catalog.Sets,
                    new[] { Piece(FirstPieceId, 0, 1, 3), Piece(SecondPieceId, 11, 0, 5) },
                    true),
                new WarmasterCatalogSnapshot(
                    WarmasterCatalogStatus.Ready, catalog.Binding,
                    new[]
                    {
                        Set(SetId, new[] { FirstPieceId, SecondPieceId },
                            (WarmasterUnlockPolicy)99, WarmasterEquipPolicy.ManualOnly)
                    },
                    catalog.Pieces, true)
            };
            foreach (WarmasterCatalogSnapshot source in malformed)
            {
                Assert.That(
                    new WarmasterTransactionPlanner(source, authority)
                        .Plan(request, state, wallet).Status,
                    Is.EqualTo(WarmasterPlanStatus.Malformed));
            }

            var missingProgression = new WarmasterCatalogSnapshot(
                WarmasterCatalogStatus.Ready,
                catalog.Binding,
                catalog.Sets,
                new[]
                {
                    new WarmasterPieceDefinition(
                        FirstPieceId, SetId, 7,
                        WarmasterPieceAvailability.Available, null, true),
                    Piece(SecondPieceId, 11, 0, 5)
                },
                true);
            Assert.That(
                new WarmasterTransactionPlanner(missingProgression, authority)
                    .Plan(request, state, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.ApprovalMissing));
        }

        [Test]
        public void CrossProfileAndStateLedgerContradictionsCannotReplay()
        {
            WarmasterStateSnapshot initial = InitialState();
            WarmasterWalletSnapshot wallet = Wallet();
            WarmasterTransactionRequest request = PurchaseRequest(
                initial,
                wallet,
                FirstPieceId,
                "purchase_profile_ledger",
                "event_profile_ledger");
            WarmasterStateSnapshot committed = Verify(
                planner.Plan(request, initial, wallet),
                out wallet,
                out _);
            WarmasterTransactionRecord source = committed.TransactionRecords.Single();
            WarmasterStateSnapshot wrongProfileLedger = CopyState(
                committed,
                transactionRecords: new[]
                {
                    CloneRecord(source, profileId: "another_profile")
                });
            Assert.That(
                planner.Plan(request, wrongProfileLedger, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Malformed));

            WarmasterStateSnapshot wrongState = CopyState(
                committed,
                level: committed.Level + 1);
            Assert.That(
                planner.Plan(request, wrongState, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Malformed));
            WarmasterStateSnapshot duplicateLedger = CopyState(
                committed,
                transactionRecords: new[] { source, source });
            Assert.That(
                planner.Plan(request, duplicateLedger, wallet).Status,
                Is.EqualTo(WarmasterPlanStatus.Malformed));
        }

        private void AssertCatalogStatus(
            WarmasterTransactionRequest request,
            WarmasterStateSnapshot state,
            WarmasterWalletSnapshot wallet,
            WarmasterCatalogStatus status,
            WarmasterPlanStatus expected)
        {
            var source = new WarmasterCatalogSnapshot(
                status,
                catalog.Binding,
                catalog.Sets,
                catalog.Pieces,
                status != WarmasterCatalogStatus.Incomplete);
            WarmasterPlanningResult result = new WarmasterTransactionPlanner(source, authority)
                .Plan(request, state, wallet);
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(result.Plan, Is.Null);
        }

        private void AssertStateStatus(
            WarmasterTransactionRequest request,
            WarmasterWalletSnapshot wallet,
            WarmasterStateStatus status,
            WarmasterPlanStatus expected)
        {
            WarmasterStateSnapshot state = CopyState(InitialState(), status: status);
            Assert.That(planner.Plan(request, state, wallet).Status, Is.EqualTo(expected));
        }

        private static WarmasterCatalogSnapshot Catalog(
            WarmasterUnlockPolicy unlockPolicy,
            WarmasterEquipPolicy equipPolicy)
        {
            WarmasterCatalogBinding binding = Binding("fixture_content_v1");
            return new WarmasterCatalogSnapshot(
                WarmasterCatalogStatus.Ready,
                binding,
                new[]
                {
                    Set(
                        SetId,
                        new[] { FirstPieceId, SecondPieceId },
                        unlockPolicy,
                        equipPolicy)
                },
                new[]
                {
                    Piece(FirstPieceId, 7, 1, 3),
                    Piece(SecondPieceId, 11, 0, 5)
                },
                true);
        }

        private static WarmasterCatalogSnapshot CatalogAtBounds()
        {
            WarmasterCatalogBinding binding = Binding("fixture_content_bounds_v1");
            var sets = new List<WarmasterSetDefinition>(64);
            var pieces = new List<WarmasterPieceDefinition>(1024);
            for (var setIndex = 0; setIndex < 64; setIndex++)
            {
                string setId = "fixture_set_" + setIndex.ToString("D2");
                string[] members = Enumerable.Range(setIndex * 16, 16)
                    .Select(index => "fixture_piece_" + index.ToString("D4"))
                    .ToArray();
                sets.Add(Set(setId, members));
                foreach (string pieceId in members)
                {
                    pieces.Add(new WarmasterPieceDefinition(
                        pieceId,
                        setId,
                        1,
                        WarmasterPieceAvailability.Available,
                        new WarmasterProgressionRule(
                            WarmasterProgressionMode.NoChange,
                            0,
                            0,
                            true),
                        true));
                }
            }

            return new WarmasterCatalogSnapshot(
                WarmasterCatalogStatus.Ready,
                binding,
                sets,
                pieces,
                true);
        }

        private static WarmasterCatalogBinding Binding(string contentVersion)
        {
            return new WarmasterCatalogBinding(
                1,
                contentVersion,
                "fixture_source_revision",
                CatalogHash,
                "fixture_approval_revision",
                CurrencyId);
        }

        private static WarmasterSetDefinition Set(
            string setId,
            IEnumerable<string> members,
            WarmasterUnlockPolicy unlockPolicy = WarmasterUnlockPolicy.ManualAfterCompletion,
            WarmasterEquipPolicy equipPolicy = WarmasterEquipPolicy.ManualOnly)
        {
            return new WarmasterSetDefinition(
                setId,
                members,
                WarmasterCompletionPolicy.AllMembers,
                unlockPolicy,
                equipPolicy,
                true);
        }

        private static WarmasterPieceDefinition Piece(
            string pieceId,
            long price,
            int levelDelta,
            int experienceDelta,
            bool isApproved = true)
        {
            WarmasterProgressionMode mode = levelDelta == 0 && experienceDelta == 0
                ? WarmasterProgressionMode.NoChange
                : WarmasterProgressionMode.AddDeltas;
            return new WarmasterPieceDefinition(
                pieceId,
                SetId,
                price,
                WarmasterPieceAvailability.Available,
                new WarmasterProgressionRule(
                    mode,
                    levelDelta,
                    experienceDelta,
                    true),
                isApproved);
        }

        private WarmasterStateSnapshot InitialState(
            WarmasterCatalogBinding binding = null)
        {
            return new WarmasterStateSnapshot(
                WarmasterStateStatus.Valid,
                ProfileId,
                0,
                binding ?? catalog.Binding,
                Array.Empty<WarmasterOwnedPieceRecord>(),
                Array.Empty<WarmasterUnlockedSetRecord>(),
                string.Empty,
                false,
                0,
                0,
                Array.Empty<WarmasterTransactionRecord>(),
                true);
        }

        private static WarmasterWalletSnapshot Wallet(long balance = 100, long revision = 0)
        {
            return new WarmasterWalletSnapshot(
                WarmasterWalletStatus.Available,
                CurrencyId,
                balance,
                revision,
                true);
        }

        private static WarmasterTransactionRequest PurchaseRequest(
            WarmasterStateSnapshot state,
            WarmasterWalletSnapshot wallet,
            string pieceId,
            string operationId,
            string eventId,
            string setId = SetId)
        {
            return new WarmasterTransactionRequest(
                WarmasterOperation.PurchasePiece,
                ProfileId,
                ActorId,
                operationId,
                eventId,
                "correlation_" + operationId,
                setId,
                pieceId,
                state.Revision,
                wallet.Revision,
                state.CatalogBinding);
        }

        private static WarmasterTransactionRequest SetRequest(
            WarmasterOperation operation,
            WarmasterStateSnapshot state,
            string operationId,
            string eventId)
        {
            return new WarmasterTransactionRequest(
                operation,
                ProfileId,
                ActorId,
                operationId,
                eventId,
                "correlation_" + operationId,
                SetId,
                string.Empty,
                state.Revision,
                -1,
                state.CatalogBinding);
        }

        private static WarmasterTransactionRequest CopyRequest(
            WarmasterTransactionRequest source,
            string profileId = null,
            string pieceId = null,
            long? expectedStateRevision = null,
            WarmasterCatalogBinding expectedCatalogBinding = null,
            string operationId = null,
            WarmasterOperation? operation = null,
            long? expectedEconomyRevision = null)
        {
            return new WarmasterTransactionRequest(
                operation ?? source.Operation,
                profileId ?? source.ProfileId,
                source.ActorId,
                operationId ?? source.OperationId,
                source.EventId,
                source.CorrelationId,
                source.SetId,
                pieceId ?? source.PieceId,
                expectedStateRevision ?? source.ExpectedStateRevision,
                expectedEconomyRevision ?? source.ExpectedEconomyRevision,
                expectedCatalogBinding ?? source.ExpectedCatalogBinding,
                source.PriorReceipt);
        }

        private static WarmasterTransactionRequest WithReceipt(
            WarmasterTransactionRequest source,
            WarmasterVerifiedReceipt receipt)
        {
            return new WarmasterTransactionRequest(
                source.Operation,
                source.ProfileId,
                source.ActorId,
                source.OperationId,
                source.EventId,
                source.CorrelationId,
                source.SetId,
                source.PieceId,
                source.ExpectedStateRevision,
                source.ExpectedEconomyRevision,
                source.ExpectedCatalogBinding,
                receipt);
        }

        private static WarmasterStateSnapshot PurchaseAndVerify(
            WarmasterTransactionPlanner targetPlanner,
            WarmasterStateSnapshot state,
            WarmasterWalletSnapshot wallet,
            string pieceId,
            string operationId,
            string eventId,
            out WarmasterWalletSnapshot candidateWallet)
        {
            WarmasterPlanningResult result = targetPlanner.Plan(
                PurchaseRequest(state, wallet, pieceId, operationId, eventId),
                state,
                wallet);
            return Verify(result, out candidateWallet, out _);
        }

        private static WarmasterStateSnapshot Verify(
            WarmasterPlanningResult result,
            out WarmasterWalletSnapshot candidateWallet,
            out WarmasterVerifiedReceipt receipt)
        {
            Assert.That(result.IsPrepared, Is.True);
            WarmasterTransactionPlan plan = result.Plan;
            candidateWallet = CandidateWallet(plan);
            Assert.That(
                WarmasterTransactionPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    plan.CandidateState,
                    candidateWallet,
                    GenerationHash,
                    out receipt),
                Is.True);
            return plan.CandidateState;
        }

        private static WarmasterWalletSnapshot CandidateWallet(
            WarmasterTransactionPlan plan)
        {
            if (!plan.RequiresEconomyDebit)
            {
                return null;
            }

            return new WarmasterWalletSnapshot(
                WarmasterWalletStatus.Available,
                plan.EconomyDebit.CurrencyId,
                plan.EconomyDebit.CandidateBalance,
                plan.EconomyDebit.CandidateRevision,
                true);
        }

        private static WarmasterStateSnapshot CopyState(
            WarmasterStateSnapshot source,
            WarmasterStateStatus? status = null,
            bool? legacyTrueWarmasterFlag = null,
            int? level = null,
            IEnumerable<WarmasterTransactionRecord> transactionRecords = null)
        {
            return new WarmasterStateSnapshot(
                status ?? source.Status,
                source.ProfileId,
                source.Revision,
                source.CatalogBinding,
                source.PurchasedPieces,
                source.UnlockedSets,
                source.EquippedSetId,
                legacyTrueWarmasterFlag ?? source.LegacyTrueWarmasterFlag,
                level ?? source.Level,
                source.Experience,
                transactionRecords ?? source.TransactionRecords,
                source.IsComplete);
        }

        private static WarmasterTransactionRecord FutureRecord(
            string operationId,
            string eventId,
            long resultingRevision,
            string setId = "future_set_gamma",
            string pieceId = "")
        {
            string hash = new string('b', 64);
            return new WarmasterTransactionRecord(
                operationId,
                eventId,
                "future_correlation_" + operationId,
                ProfileId,
                (WarmasterOperation)99,
                hash,
                Binding("fixture_content_v1"),
                setId,
                pieceId,
                string.Empty,
                0,
                hash,
                resultingRevision,
                -1,
                hash,
                hash,
                hash,
                false);
        }

        private static WarmasterTransactionRecord CloneRecord(
            WarmasterTransactionRecord source,
            long? debitAmount = null,
            string resultingStateHash = null,
            long? resultingStateRevision = null,
            string profileId = null)
        {
            return new WarmasterTransactionRecord(
                source.OperationId,
                source.EventId,
                source.CorrelationId,
                profileId ?? source.ProfileId,
                source.Operation,
                source.RequestFingerprint,
                source.CatalogBinding,
                source.SetId,
                source.PieceId,
                source.CurrencyId,
                debitAmount ?? source.DebitAmount,
                source.DefinitionFingerprint,
                resultingStateRevision ?? source.ResultingStateRevision,
                source.ResultingEconomyRevision,
                resultingStateHash ?? source.ResultingStateHash,
                source.PlanHash,
                source.PostCommitNotificationCorrelationId,
                source.IsSupported);
        }

        private static void AssertDuplicate(
            WarmasterPlanningResult result,
            bool hasReceipt)
        {
            Assert.That(result.Status, Is.EqualTo(WarmasterPlanStatus.AlreadyCommitted));
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.ExistingRecord, Is.Not.Null);
            Assert.That(result.ExistingReceipt != null, Is.EqualTo(hasReceipt));
        }

        private sealed class FakeAuthority : IWarmasterTransactionAuthority
        {
            public WarmasterAuthorizationStatus Status { get; set; } =
                WarmasterAuthorizationStatus.Allowed;

            public WarmasterAuthorizationStatus Authorize(
                WarmasterTransactionRequest request,
                WarmasterStateSnapshot currentState)
            {
                return Status;
            }
        }
    }
}
