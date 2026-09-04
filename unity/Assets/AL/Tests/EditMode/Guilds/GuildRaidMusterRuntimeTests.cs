using System;
using System.Linq;
using AL.Alliances;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Guilds;
using NUnit.Framework;

namespace AL.Tests.EditMode.Guilds
{
    public sealed class GuildRaidMusterRuntimeTests
    {
        private const string AccountMaster = "account_master_001";
        private const string AccountMemberA = "account_member_a_001";
        private const string AccountMemberB = "account_member_b_001";
        private const string GuildAlpha = "guild_alpha_001";
        private const string RealmStonehold = "stonehold";
        private const string CallAlpha = "raid_call_alpha_001";
        private const string ClosedInstance = "closed_raid_stonehold_001";
        private const string ClosedTopology = "closed_guild_raid_dungeon_v1";
        private const string BossIron = "raid_boss_iron_warden";
        private const string BossAsh = "raid_boss_ash_tyrant";
        private const string BossThorn = "raid_boss_thorn_colossus";
        private const string BossVeil = "raid_boss_veil_harbinger";
        private const string EnvelopeIn = "instance_envelope_alpha_001";
        private const string EnvelopeReturn = "safe_return_envelope_alpha_001";
        private const long WeekId = 30;
        private const long SeasonEpoch = 1;
        private const long ClockStart = 1700000000;
        private static readonly string CatalogHash = new string('a', 64);

        [Test]
        public void MissingLegacySaveMigratesToEmptyAndRoundTripPreservesRaidAndInstanceSnapshots()
        {
            var legacy = new SaveGameData();
            Assert.That(legacy.GuildRaidMuster, Is.Null);

            RaidAuthoritySnapshot empty = GuildRaidMusterSaveCodec.Read(legacy.GuildRaidMuster);
            Assert.That(empty.Status, Is.EqualTo(GuildAuthorityStatus.Available));
            Assert.That(empty.Revision, Is.EqualTo(0));
            Assert.That(empty.Calls, Is.Empty);
            Assert.That(empty.Receipts, Is.Empty);

            GuildRaidMusterPersistentState written = GuildRaidMusterSaveCodec.Write(
                SnapshotWithActiveParticipant(),
                ClockStart + 90);
            RaidAuthoritySnapshot restored = GuildRaidMusterSaveCodec.Read(written);
            RaidCallSnapshot call = restored.Calls.Single();
            RaidParticipantSnapshot participant = call.Participants.Single(value => value.AccountId == AccountMemberA);

            Assert.That(written.Version, Is.EqualTo(GuildRaidMusterPersistentState.CurrentVersion));
            Assert.That(written.LastTrustedClockUnixSeconds, Is.EqualTo(ClockStart + 90));
            Assert.That(call.Instance.ClosedInstanceEnvelopeId, Is.EqualTo(EnvelopeIn));
            Assert.That(call.Instance.ClosedDungeonTopologyId, Is.EqualTo(ClosedTopology));
            Assert.That(participant.Transfer, Is.EqualTo(RaidTransferState.InInstance));
            Assert.That(participant.SafeReturnEnvelopeId, Is.EqualTo(EnvelopeReturn));
        }

        [Test]
        public void TrustedAuthoritativeNetworkClockIsRequiredAndMustMatchCommand()
        {
            GuildRaidMusterRuntime runtime = Runtime();
            GuildRaidMusterTransitionRequest request = Announce("operation_announce_clock", 0, ClockStart);

            GuildRaidMusterRuntimeResult client = runtime.Apply(
                Envelope(request, GuildRaidClockKind.ClientUntrusted, "device_clock"),
                Membership(),
                EmptyAlliance(),
                null);
            Assert.That(client.Status, Is.EqualTo(GuildPlanningStatus.Unauthorized));
            Assert.That(client.DiagnosticCode, Is.EqualTo(GuildRaidMusterRuntime.ClockUntrustedCode));
            Assert.That(client.Mutated, Is.False);

            GuildRaidMusterRuntimeResult mismatch = runtime.Apply(
                Envelope(request, trustedClock: ClockStart + 1),
                Membership(),
                EmptyAlliance(),
                null);
            Assert.That(mismatch.Status, Is.EqualTo(GuildPlanningStatus.InvalidRequest));
            Assert.That(mismatch.DiagnosticCode, Is.EqualTo(GuildRaidMusterRuntime.ClockMismatchCode));
            Assert.That(mismatch.Mutated, Is.False);

            GuildRaidMusterRuntimeResult spoofedSource = runtime.Apply(
                Envelope(request, sourceId: "client_raid_service"),
                Membership(),
                EmptyAlliance(),
                null);
            Assert.That(spoofedSource.Status, Is.EqualTo(GuildPlanningStatus.Unauthorized));
            Assert.That(spoofedSource.Mutated, Is.False);

            GuildRaidMusterPersistentState current = Apply(
                runtime,
                null,
                Announce("operation_announce_clock_regression", 0, ClockStart));
            GuildRaidMusterRuntimeResult regressed = runtime.Apply(
                Envelope(Respond(
                    RaidOperation.Join,
                    AccountMemberA,
                    "operation_join_clock_regression",
                    current.Revision,
                    ClockStart - 1)),
                Membership(),
                EmptyAlliance(),
                current);
            Assert.That(regressed.Status, Is.EqualTo(GuildPlanningStatus.StaleAuthority));
            Assert.That(regressed.DiagnosticCode, Is.EqualTo(GuildRaidMusterRuntime.ClockRegressionCode));
            Assert.That(regressed.Mutated, Is.False);
        }

        [Test]
        public void RuntimeConsumesPlannerPersistsToSaveAndReplaysWithoutMutation()
        {
            GuildRaidMusterRuntime runtime = Runtime();
            var save = new SaveGameData();
            GuildRaidNetworkCommandEnvelope command = Envelope(
                Announce("operation_announce_runtime", 0, ClockStart));

            GuildRaidMusterRuntimeResult applied = runtime.ApplyToSave(
                command,
                Membership(),
                EmptyAlliance(),
                save);
            Assert.That(applied.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(applied.Mutated, Is.True);
            Assert.That(save.GuildRaidMuster, Is.SameAs(applied.Persisted));
            Assert.That(save.GuildRaidMuster.Calls.Single().State, Is.EqualTo((int)RaidCallState.Accepting));
            Assert.That(save.GuildRaidMuster.Calls.Single().WindowEndUnixSeconds -
                        save.GuildRaidMuster.Calls.Single().WindowStartUnixSeconds,
                Is.EqualTo(30 * 60));

            GuildRaidMusterRuntimeResult replay = runtime.ApplyToSave(
                command,
                Membership(),
                EmptyAlliance(),
                save);
            Assert.That(replay.Status, Is.EqualTo(GuildPlanningStatus.AlreadyCommitted));
            Assert.That(replay.Mutated, Is.False);
            Assert.That(save.GuildRaidMuster.Revision, Is.EqualTo(applied.Persisted.Revision));
        }

        [Test]
        public void SaveServiceCommitsPreparedSnapshotAndRollsBackWhenDurabilityFails()
        {
            GuildRaidMusterRuntime runtime = Runtime();
            var durable = new RecordingSaveGameService(SaveOperationStatus.SavedPrimary);
            GuildRaidMusterRuntimeResult committed = runtime.ApplyToSaveService(
                Envelope(Announce("operation_durable_announce", 0, ClockStart)),
                Membership(),
                EmptyAlliance(),
                durable);
            Assert.That(committed.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(committed.Mutated, Is.True);
            Assert.That(durable.SaveCalls, Is.EqualTo(1));
            Assert.That(durable.CurrentSave.GuildRaidMuster.Calls.Single().CallId, Is.EqualTo(CallAlpha));

            var failed = new RecordingSaveGameService(SaveOperationStatus.SaveFailedPreviousPreserved);
            GuildRaidMusterRuntimeResult rejected = runtime.ApplyToSaveService(
                Envelope(Announce("operation_failed_announce", 0, ClockStart)),
                Membership(),
                EmptyAlliance(),
                failed);
            Assert.That(rejected.Status, Is.EqualTo(GuildPlanningStatus.Unavailable));
            Assert.That(rejected.DiagnosticCode, Is.EqualTo(GuildRaidMusterRuntime.SaveCommitFailedCode));
            Assert.That(rejected.Mutated, Is.False);
            Assert.That(failed.SaveCalls, Is.EqualTo(1));
            Assert.That(failed.CurrentSave.GuildRaidMuster, Is.Null);
        }

        [Test]
        public void SaveFailureDoesNotLoadRaidInstanceBeforeCommit()
        {
            GuildRaidMusterRuntime runtime = Runtime();
            var save = new RecordingSaveGameService(SaveOperationStatus.SaveFailedPreviousPreserved);
            save.CurrentSave.GuildRaidMuster = GuildRaidMusterSaveCodec.Write(
                SnapshotWithActiveParticipant(),
                ClockStart);
            var loader = new RecordingLoader(true);
            GuildRaidMusterTransitionRequest request = Request(
                RaidOperation.TransferOut,
                "operation_transfer_out_save_failure",
                AccountMemberA,
                save.CurrentSave.GuildRaidMuster.Revision,
                ClockStart + 100,
                AccountMemberA,
                EnvelopeIn,
                EnvelopeReturn);

            GuildRaidMusterRuntimeResult result = runtime.ApplyToSaveService(
                Envelope(request),
                Membership(),
                EmptyAlliance(),
                save,
                loader);

            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Unavailable));
            Assert.That(result.DiagnosticCode, Is.EqualTo(GuildRaidMusterRuntime.SaveCommitFailedCode));
            Assert.That(save.SaveCalls, Is.EqualTo(1));
            Assert.That(loader.LastCommand, Is.Null);
        }

        [Test]
        public void TransferUsesOnlyExplicitInstanceEnvelopeAndPersistsAfterLoaderAcceptance()
        {
            GuildRaidMusterRuntime runtime = Runtime();
            GuildRaidMusterPersistentState countdown = BuildCountdown(runtime);
            var loader = new RecordingLoader(true);
            var save = new RecordingSaveGameService(SaveOperationStatus.SavedPrimary);
            save.CurrentSave.GuildRaidMuster = countdown;
            GuildRaidMusterTransitionRequest request = TransferIn(
                "operation_transfer_in_runtime",
                countdown.Revision,
                ClockStart + 90);

            GuildRaidMusterRuntimeResult result = runtime.ApplyToSaveService(
                Envelope(request),
                Membership(),
                EmptyAlliance(),
                save,
                loader);

            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(result.Mutated, Is.True);
            Assert.That(loader.LastCommand, Is.Not.Null);
            Assert.That(loader.LastCommand.Direction, Is.EqualTo(RaidTransferDirection.Enter));
            Assert.That(loader.LastCommand.InstanceEnvelopeId, Is.EqualTo(EnvelopeIn));
            Assert.That(loader.LastCommand.ClosedDungeonTopologyId, Is.EqualTo(ClosedTopology));
            Assert.That(loader.LastCommand.TargetAccountId, Is.EqualTo(AccountMemberA));
            Assert.That(loader.LastCommand.GetType().GetProperty("SceneName"), Is.Null);
            Assert.That(loader.LastCommand.GetType().GetProperty("Coordinates"), Is.Null);
            Assert.That(result.Persisted.Calls.Single().Participants
                .Single(value => value.AccountId == AccountMemberA).Transfer,
                Is.EqualTo((int)RaidTransferState.InInstance));
        }

        [Test]
        public void TransferLoaderFailureCanRetryFromDurableAuthority()
        {
            GuildRaidMusterRuntime runtime = Runtime();
            GuildRaidMusterPersistentState countdown = BuildCountdown(runtime);
            var save = new RecordingSaveGameService(SaveOperationStatus.SavedPrimary);
            save.CurrentSave.GuildRaidMuster = countdown;
            var loader = new RecordingLoader(false, "AL-RAID-INSTANCE-LOAD-FAILED");
            GuildRaidNetworkCommandEnvelope command = Envelope(
                TransferIn("operation_transfer_load_fail", countdown.Revision, ClockStart + 90));

            GuildRaidMusterRuntimeResult result = runtime.ApplyToSaveService(
                command,
                Membership(),
                EmptyAlliance(),
                save,
                loader);

            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Unavailable));
            Assert.That(result.DiagnosticCode, Is.EqualTo("AL-RAID-INSTANCE-LOAD-FAILED"));
            Assert.That(result.Mutated, Is.True);
            Assert.That(save.SaveCalls, Is.EqualTo(1));
            Assert.That(result.Persisted.Calls.Single().Participants
                .Single(value => value.AccountId == AccountMemberA).Transfer,
                Is.EqualTo((int)RaidTransferState.InInstance));

            var retryLoader = new RecordingLoader(true);
            GuildRaidMusterRuntimeResult retry = runtime.ApplyToSaveService(
                command,
                Membership(),
                EmptyAlliance(),
                save,
                retryLoader);
            Assert.That(retry.Status, Is.EqualTo(GuildPlanningStatus.AlreadyCommitted));
            Assert.That(retry.Mutated, Is.False);
            Assert.That(save.SaveCalls, Is.EqualTo(1));
            Assert.That(retryLoader.LastCommand.CommandId, Is.EqualTo("operation_transfer_load_fail"));
        }

        [Test]
        public void InstanceEnvelopeLoaderResolvesOpaqueDestinationBeforeLoading()
        {
            var resolver = new RecordingDestinationResolver();
            var backend = new RecordingInstanceBackend();
            var loader = new RaidInstanceEnvelopeLoader(resolver, backend);
            var command = new RaidInstanceCommandEnvelope(
                GuildRaidMusterRuntime.ContractVersion,
                "operation_load_opaque",
                CallAlpha,
                GuildAlpha,
                AccountMemberA,
                RaidTransferDirection.Enter,
                EnvelopeIn,
                ClosedTopology);

            Assert.That(loader.TryLoad(command, out string diagnostic), Is.True);
            Assert.That(diagnostic, Is.EqualTo(string.Empty));
            Assert.That(resolver.LastCommand, Is.SameAs(command));
            Assert.That(backend.LastDestination.DestinationToken, Is.EqualTo("destination_token_alpha"));
            Assert.That(backend.LastDestination.GetType().GetProperty("SceneName"), Is.Null);
            Assert.That(backend.LastDestination.GetType().GetProperty("Coordinates"), Is.Null);
        }

        [Test]
        public void UiPresentsAnnounceJoinDeclineLaunchAndTransferWithoutMutatingAuthority()
        {
            GuildRaidMusterRuntime runtime = Runtime();
            GuildRaidMusterUiPresentation empty = runtime.Present(
                null,
                Membership(),
                AccountMaster,
                GuildAlpha,
                ClockStart);
            Assert.That(empty.Actions.Single(value => value.Operation == RaidOperation.AnnounceCall).Enabled, Is.True);

            GuildRaidMusterPersistentState accepting = Apply(runtime, null,
                Announce("operation_announce_ui", 0, ClockStart));
            GuildRaidMusterUiPresentation member = runtime.Present(
                accepting,
                Membership(),
                AccountMemberA,
                GuildAlpha,
                ClockStart + 1);
            Assert.That(member.Actions.Single(value => value.Operation == RaidOperation.Join).Enabled, Is.True);
            Assert.That(member.Actions.Single(value => value.Operation == RaidOperation.Decline).Enabled, Is.True);
            Assert.That(member.Actions.Single(value => value.Operation == RaidOperation.Launch).Enabled, Is.False);

            GuildRaidMusterUiPresentation officer = runtime.Present(
                accepting,
                Membership(),
                AccountMaster,
                GuildAlpha,
                ClockStart + 1);
            Assert.That(officer.Actions.Single(value => value.Operation == RaidOperation.Launch).Enabled, Is.True);

            GuildRaidMusterPersistentState countdown = BuildCountdown(runtime);
            GuildRaidMusterUiPresentation transfer = runtime.Present(
                countdown,
                Membership(),
                AccountMemberA,
                GuildAlpha,
                ClockStart + 90);
            Assert.That(transfer.Actions.Single(value => value.Operation == RaidOperation.TransferIn).Enabled, Is.True);

            GuildRaidMusterPersistentState active = runtime.Apply(
                Envelope(TransferIn("operation_transfer_ui", countdown.Revision, ClockStart + 90)),
                Membership(),
                EmptyAlliance(),
                countdown).Persisted;
            GuildRaidMusterUiPresentation returnUi = runtime.Present(
                active,
                Membership(),
                AccountMemberA,
                GuildAlpha,
                ClockStart + 100);
            Assert.That(returnUi.Actions.Single(value => value.Operation == RaidOperation.TransferOut).Enabled, Is.True);
        }

        [Test]
        public void UiKeepsInInstanceCallVisibleWhenNewerCallExists()
        {
            GuildRaidMusterRuntime runtime = Runtime();
            RaidAuthoritySnapshot active = SnapshotWithActiveParticipant();
            var newer = new RaidCallSnapshot(
                "call_bravo_002",
                GuildAlpha,
                AccountMaster,
                RaidCallState.Accepting,
                WeekId + 1,
                SeasonEpoch,
                BossVeil,
                "closed_instance_bravo_002",
                ClosedTopology,
                ClockStart + 60,
                ClockStart + 600,
                new[]
                {
                    new RaidParticipantSnapshot(
                        AccountMemberB,
                        RaidParticipantResponse.NoResponse,
                        RaidTransferState.NotTransferred,
                        string.Empty,
                        string.Empty,
                        false,
                        false)
                },
                new RaidInstanceSnapshot(RaidInstanceState.NotLaunched, string.Empty, ClosedTopology),
                RaidOutcomeKind.None,
                false,
                false);
            var snapshot = new RaidAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                active.Revision,
                Binding(),
                new[] { active.Calls[0], newer },
                Array.Empty<RaidOperationReceipt>(),
                true);

            GuildRaidMusterUiPresentation ui = runtime.Present(
                GuildRaidMusterSaveCodec.Write(snapshot, ClockStart + 100),
                Membership(),
                AccountMemberA,
                GuildAlpha,
                ClockStart + 100);

            Assert.That(ui.CallId, Is.EqualTo(CallAlpha));
            Assert.That(ui.Actions.Single(value => value.Operation == RaidOperation.TransferOut).Enabled, Is.True);
        }

        [Test]
        public void MissingPlannerOrFuturePersistentVersionFailsClosed()
        {
            var unavailable = new GuildRaidMusterRuntime(null);
            GuildRaidMusterRuntimeResult noPlanner = unavailable.Apply(
                Envelope(Announce("operation_no_planner", 0, ClockStart)),
                Membership(),
                EmptyAlliance(),
                null);
            Assert.That(noPlanner.Status, Is.EqualTo(GuildPlanningStatus.Unavailable));
            Assert.That(noPlanner.Mutated, Is.False);
            Assert.That(unavailable.Present(null, Membership(), AccountMaster, GuildAlpha, ClockStart).Actions,
                Is.Empty);

            var future = new GuildRaidMusterPersistentState
            {
                Version = GuildRaidMusterPersistentState.CurrentVersion + 1
            };
            GuildRaidMusterRuntimeResult futureResult = Runtime().Apply(
                Envelope(Announce("operation_future_save", 0, ClockStart)),
                Membership(),
                EmptyAlliance(),
                future);
            Assert.That(futureResult.Status, Is.EqualTo(GuildPlanningStatus.Unavailable));
            Assert.That(futureResult.DiagnosticCode, Is.EqualTo(GuildRaidMusterRuntime.SaveVersionUnsupportedCode));
            Assert.That(futureResult.Mutated, Is.False);
        }

        [Test]
        public void MalformedPersistentSnapshotFailsClosedBeforePlannerOrLoader()
        {
            var malformed = new GuildRaidMusterPersistentState
            {
                Version = GuildRaidMusterPersistentState.CurrentVersion,
                Revision = 1,
                CatalogSchemaVersion = 1,
                ContentVersion = "1.0.0",
                SourceRevision = "guild_raid_muster_policy_v1",
                CatalogHash = CatalogHash,
                Calls = null,
                Receipts = null
            };

            GuildRaidMusterRuntimeResult result = Runtime().Apply(
                Envelope(Announce("operation_malformed_save", 1, ClockStart)),
                Membership(),
                EmptyAlliance(),
                malformed);
            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Unavailable));
            Assert.That(result.DiagnosticCode, Is.EqualTo(GuildRaidMusterRuntime.SaveMalformedCode));
            Assert.That(result.Mutated, Is.False);
        }

        private static GuildRaidMusterPersistentState BuildCountdown(GuildRaidMusterRuntime runtime)
        {
            GuildRaidMusterPersistentState state = Apply(runtime, null,
                Announce("operation_announce_countdown_runtime", 0, ClockStart));
            state = Apply(runtime, state,
                Respond(RaidOperation.Join, AccountMemberA, "operation_join_a_runtime", state.Revision, ClockStart + 5));
            state = Apply(runtime, state,
                Respond(RaidOperation.Join, AccountMemberB, "operation_join_b_runtime", state.Revision, ClockStart + 6));
            return Apply(runtime, state,
                Launch("operation_launch_countdown_runtime", state.Revision, ClockStart + 60));
        }

        private static GuildRaidMusterPersistentState Apply(
            GuildRaidMusterRuntime runtime,
            GuildRaidMusterPersistentState state,
            GuildRaidMusterTransitionRequest request)
        {
            GuildRaidMusterRuntimeResult result = runtime.Apply(
                Envelope(request),
                Membership(),
                EmptyAlliance(),
                state);
            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Prepared), result.DiagnosticCode);
            return result.Persisted;
        }

        private static GuildRaidNetworkCommandEnvelope Envelope(
            GuildRaidMusterTransitionRequest request,
            GuildRaidClockKind kind = GuildRaidClockKind.TrustedServer,
            string clockSourceId = GuildRaidMusterRuntime.TrustedClockSourceId,
            long? trustedClock = null,
            string sourceId = GuildRaidMusterRuntime.AuthoritativeCommandSourceId)
        {
            return new GuildRaidNetworkCommandEnvelope(
                GuildRaidMusterRuntime.ContractVersion,
                sourceId,
                kind,
                clockSourceId,
                trustedClock ?? request.TrustedClockUnixSeconds,
                request);
        }

        private static GuildRaidMusterTransitionRequest Announce(string operationId, long revision, long clock)
        {
            return Request(RaidOperation.AnnounceCall, operationId, AccountMaster, revision, clock);
        }

        private static GuildRaidMusterTransitionRequest Respond(
            RaidOperation operation,
            string actor,
            string operationId,
            long revision,
            long clock)
        {
            return Request(operation, operationId, actor, revision, clock);
        }

        private static GuildRaidMusterTransitionRequest Launch(string operationId, long revision, long clock)
        {
            return Request(RaidOperation.Launch, operationId, AccountMaster, revision, clock);
        }

        private static GuildRaidMusterTransitionRequest TransferIn(string operationId, long revision, long clock)
        {
            return Request(
                RaidOperation.TransferIn,
                operationId,
                AccountMemberA,
                revision,
                clock,
                AccountMemberA,
                EnvelopeIn,
                EnvelopeReturn);
        }

        private static GuildRaidMusterTransitionRequest Request(
            RaidOperation operation,
            string operationId,
            string actor,
            long revision,
            long clock,
            string target = "",
            string instanceEnvelope = "",
            string returnEnvelope = "")
        {
            return new GuildRaidMusterTransitionRequest(
                operation,
                operationId,
                actor,
                GuildAlpha,
                CallAlpha,
                string.IsNullOrEmpty(target) ? actor : target,
                WeekId,
                SeasonEpoch,
                BossVeil,
                ClosedInstance,
                instanceEnvelope,
                returnEnvelope,
                string.Empty,
                clock,
                revision,
                1,
                true,
                true,
                true,
                true,
                RaidReconcileReason.Duplicate,
                Binding());
        }

        private static GuildRaidMusterRuntime Runtime()
        {
            return new GuildRaidMusterRuntime(Policy());
        }

        private static GuildCatalogBinding Binding()
        {
            return new GuildCatalogBinding(1, "1.0.0", "guild_raid_muster_policy_v1", CatalogHash);
        }

        private static GuildRaidMusterPolicySnapshot Policy()
        {
            return new GuildRaidMusterPolicySnapshot(
                GuildCatalogStatus.Ready,
                Binding(),
                30,
                1,
                1,
                2,
                new[]
                {
                    new RaidBossSlotDefinition(0, BossIron),
                    new RaidBossSlotDefinition(1, BossAsh),
                    new RaidBossSlotDefinition(2, BossThorn),
                    new RaidBossSlotDefinition(3, BossVeil)
                },
                ClosedTopology,
                new[] { "public_realm_dungeon_entrance", "public_realm_dungeon_reward" },
                true,
                true);
        }

        private static GuildAuthoritySnapshot Membership()
        {
            return new GuildAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                1,
                Binding(),
                new[]
                {
                    new GuildSnapshot(
                        GuildAlpha,
                        RealmStonehold,
                        1,
                        GuildStatus.Active,
                        new[]
                        {
                            new GuildMemberSnapshot(AccountMaster, RealmStonehold, GuildRole.Master, GuildMembershipState.Active),
                            new GuildMemberSnapshot(AccountMemberA, RealmStonehold, GuildRole.Member, GuildMembershipState.Active),
                            new GuildMemberSnapshot(AccountMemberB, RealmStonehold, GuildRole.Member, GuildMembershipState.Active)
                        })
                },
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);
        }

        private static AllianceAuthoritySnapshot EmptyAlliance()
        {
            return new AllianceAuthoritySnapshot(
                AllianceAuthorityStatus.Available,
                1,
                Binding(),
                Array.Empty<AllianceSnapshot>(),
                Array.Empty<AlliancePendingRequest>(),
                Array.Empty<AllianceWarSnapshot>(),
                Array.Empty<AllianceOperationReceipt>(),
                true);
        }

        private static RaidAuthoritySnapshot SnapshotWithActiveParticipant()
        {
            var participant = new RaidParticipantSnapshot(
                AccountMemberA,
                RaidParticipantResponse.Join,
                RaidTransferState.InInstance,
                EnvelopeIn,
                EnvelopeReturn,
                false,
                false);
            var call = new RaidCallSnapshot(
                CallAlpha,
                GuildAlpha,
                AccountMaster,
                RaidCallState.Active,
                WeekId,
                SeasonEpoch,
                BossVeil,
                ClosedInstance,
                ClosedTopology,
                ClockStart,
                ClockStart + 1800,
                new[] { participant },
                new RaidInstanceSnapshot(RaidInstanceState.Active, EnvelopeIn, ClosedTopology),
                RaidOutcomeKind.None,
                false,
                false);
            return new RaidAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                5,
                Binding(),
                new[] { call },
                Array.Empty<RaidOperationReceipt>(),
                true);
        }

        private sealed class RecordingLoader : IRaidInstanceEnvelopeLoader
        {
            private readonly bool succeeds;
            private readonly string diagnosticCode;

            public RecordingLoader(bool succeeds, string diagnosticCode = "")
            {
                this.succeeds = succeeds;
                this.diagnosticCode = diagnosticCode;
            }

            public RaidInstanceCommandEnvelope LastCommand { get; private set; }

            public bool TryLoad(RaidInstanceCommandEnvelope command, out string diagnostic)
            {
                LastCommand = command;
                diagnostic = diagnosticCode;
                return succeeds;
            }
        }

        private sealed class RecordingDestinationResolver : IRaidInstanceDestinationResolver
        {
            public RaidInstanceCommandEnvelope LastCommand { get; private set; }

            public bool TryResolve(
                RaidInstanceCommandEnvelope command,
                out RaidInstanceLoadDestination destination,
                out string diagnosticCode)
            {
                LastCommand = command;
                destination = new RaidInstanceLoadDestination(
                    "destination_token_alpha",
                    command.InstanceEnvelopeId,
                    command.ClosedDungeonTopologyId,
                    command.Direction);
                diagnosticCode = string.Empty;
                return true;
            }
        }

        private sealed class RecordingInstanceBackend : IRaidInstanceLoadBackend
        {
            public RaidInstanceLoadDestination LastDestination { get; private set; }

            public bool TryLoad(
                string commandId,
                RaidInstanceLoadDestination destination,
                out string diagnosticCode)
            {
                LastDestination = destination;
                diagnosticCode = string.Empty;
                return true;
            }
        }

        private sealed class RecordingSaveGameService : ISaveGameService
        {
            private readonly SaveOperationStatus saveStatus;

            public RecordingSaveGameService(SaveOperationStatus saveStatus)
            {
                this.saveStatus = saveStatus;
                CurrentSave = new SaveGameData();
            }

            public SaveGameData CurrentSave { get; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus { get; private set; }
            public string LastSaveMessage => string.Empty;
            public int SaveCalls { get; private set; }

            public void Save()
            {
                SaveCalls++;
                LastSaveStatus = saveStatus;
            }

            public void Load() { }
            public bool HasSave() => true;
            public void CreateNewSave(RealmId realmId) { }
            public void DeleteSave() { }
        }
    }
}
