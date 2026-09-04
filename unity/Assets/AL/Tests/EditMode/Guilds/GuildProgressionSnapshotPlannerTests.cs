using System;
using System.Linq;
using AL.Guilds;
using NUnit.Framework;

namespace AL.Tests.EditMode.Guilds
{
    public sealed class GuildProgressionSnapshotPlannerTests
    {
        private const string AccountMaster = "account_master_001";
        private const string AccountMember = "account_member_001";
        private const string GuildAlpha = "guild_alpha_001";
        private const string RealmStonehold = "stonehold";
        private const string LevelOne = "guild_level_structural_1";
        private const string ResearchOne = "guild_research_structural_1";
        private const string RuleOne = "guild_research_structural_1_rule";
        private const string BreakdownToken = "guild.research.structural_1";
        private const string PerkSourceHash =
            "3f6c9a1b2d4e5f708192a3b4c5d6e7f8091a2b3c4d5e6f708192a3b4c5d6e7f8";
        private static readonly string CatalogHash = new string('a', 64);

        [Test]
        public void ResolveGrantsTypedProvenanceWithoutMutatingInputs()
        {
            GuildProgressionPolicySnapshot policy = Policy();
            var planner = new GuildProgressionSnapshotPlanner(policy);
            GuildAuthoritySnapshot membership = ActiveGuildWithMasterAndMember();
            GuildProgressionStateSnapshot initial = ProgressedState(LevelOne, ResearchOne);

            GuildProgressionPlanningResult result = planner.ResolveMemberPerkProvenance(
                initial,
                membership);

            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(result.Plan, Is.Not.Null);
            Assert.That(result.Plan.EffectDomains, Is.Empty);
            Assert.That(result.Plan.MemberPerkProvenance, Has.Count.EqualTo(2));
            foreach (string accountId in new[] { AccountMaster, AccountMember })
            {
                GuildMemberPerkProvenance row = result.Plan.MemberPerkProvenance.Single(
                    value => value.AccountId == accountId);
                Assert.That(row.SourceId, Is.EqualTo("guild_progression"));
                Assert.That(row.ProfileId, Is.EqualTo("guild_member_character_stats"));
                Assert.That(row.RuleId, Is.EqualTo(RuleOne));
                Assert.That(row.RequiredLevelId, Is.EqualTo(LevelOne));
                Assert.That(row.RequiredResearchId, Is.EqualTo(ResearchOne));
                Assert.That(row.Scope, Is.EqualTo(GuildPerkScope.MemberCharacterStats));
                Assert.That(row.CapKind, Is.EqualTo("unselected"));
                Assert.That(row.StackingGroup, Is.EqualTo("guild_research_structural"));
                Assert.That(row.StackingOrder, Is.Zero);
                Assert.That(row.StackingRule, Is.EqualTo("explicit_visible_only"));
                Assert.That(row.SourceVersion, Is.EqualTo("1.0.0"));
                Assert.That(row.SourceHash, Is.EqualTo(PerkSourceHash));
                Assert.That(row.StatBreakdownToken, Is.EqualTo(BreakdownToken));
                Assert.That(row.ProductionEligible, Is.False);
            }

            Assert.That(initial.Revision, Is.EqualTo(2));
            Assert.That(initial.CompletedResearchIds, Is.EqualTo(new[] { ResearchOne }));
            Assert.That(membership.Revision, Is.EqualTo(1));
            Assert.That(membership.Guilds.Single().Members.Count, Is.EqualTo(2));
        }

        [Test]
        public void MissingLevelOrResearchYieldsNoMemberPerks()
        {
            GuildProgressionPolicySnapshot policy = Policy();
            var planner = new GuildProgressionSnapshotPlanner(policy);
            GuildAuthoritySnapshot membership = ActiveGuildWithMasterAndMember();

            Assert.That(
                planner.ResolveMemberPerkProvenance(EmptyProgression(), membership)
                    .Plan.MemberPerkProvenance,
                Is.Empty);
            Assert.That(
                planner.ResolveMemberPerkProvenance(ProgressedState(LevelOne), membership)
                    .Plan.MemberPerkProvenance,
                Is.Empty);
        }

        [Test]
        public void InactiveMemberAndDisbandedGuildYieldNoProvenance()
        {
            GuildProgressionPolicySnapshot policy = Policy();
            var planner = new GuildProgressionSnapshotPlanner(policy);
            GuildProgressionStateSnapshot progressed = ProgressedState(LevelOne, ResearchOne);

            GuildAuthoritySnapshot withInactive = new GuildAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                1,
                policy.Binding,
                new[]
                {
                    new GuildSnapshot(
                        GuildAlpha,
                        RealmStonehold,
                        1,
                        GuildStatus.Active,
                        new[]
                        {
                            new GuildMemberSnapshot(
                                AccountMaster, RealmStonehold, GuildRole.Master,
                                GuildMembershipState.Active),
                            new GuildMemberSnapshot(
                                AccountMember, RealmStonehold, GuildRole.Member,
                                GuildMembershipState.Inactive)
                        })
                },
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);

            GuildMemberPerkProvenance[] activeOnly = planner
                .ResolveMemberPerkProvenance(progressed, withInactive)
                .Plan.MemberPerkProvenance.ToArray();
            Assert.That(activeOnly, Has.Length.EqualTo(1));
            Assert.That(activeOnly[0].AccountId, Is.EqualTo(AccountMaster));

            GuildAuthoritySnapshot disbanded = new GuildAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                1,
                policy.Binding,
                new[]
                {
                    new GuildSnapshot(
                        GuildAlpha,
                        RealmStonehold,
                        1,
                        GuildStatus.Disbanded,
                        new[]
                        {
                            new GuildMemberSnapshot(
                                AccountMaster, RealmStonehold, GuildRole.Master,
                                GuildMembershipState.Inactive)
                        })
                },
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);
            Assert.That(
                planner.ResolveMemberPerkProvenance(progressed, disbanded)
                    .Plan.MemberPerkProvenance,
                Is.Empty);
        }

        [Test]
        public void HiddenGlobalMultiplierPolicyFailsClosed()
        {
            GuildProgressionPolicySnapshot policy = Policy(hiddenGlobalMultipliersForbidden: false);
            var planner = new GuildProgressionSnapshotPlanner(policy);

            GuildProgressionPlanningResult result = planner.ResolveMemberPerkProvenance(
                ProgressedState(LevelOne, ResearchOne),
                ActiveGuildWithMasterAndMember());

            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Malformed));
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("AL-GUILD-PERK-HIDDEN-MULTIPLIER"));
        }

        [Test]
        public void ProductionEligibleNumericTuningFailsClosed()
        {
            GuildProgressionPolicySnapshot policy = Policy(numericPerkTuningProductionEligible: true);
            var planner = new GuildProgressionSnapshotPlanner(policy);

            GuildProgressionPlanningResult result = planner.ResolveMemberPerkProvenance(
                ProgressedState(LevelOne, ResearchOne),
                ActiveGuildWithMasterAndMember());

            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Unsupported));
            Assert.That(result.Plan, Is.Null);
            Assert.That(
                result.Diagnostics.Single().Code,
                Is.EqualTo("AL-GUILD-PERK-TUNING-PRODUCTION-INELIGIBLE"));
        }

        [Test]
        public void MasterAdvanceAndResearchAreDeterministicAndDoNotMutateInput()
        {
            GuildProgressionPolicySnapshot policy = Policy();
            var planner = new GuildProgressionSnapshotPlanner(policy);
            GuildAuthoritySnapshot membership = ActiveGuildWithMasterAndMember();
            GuildProgressionStateSnapshot initial = EmptyProgression();

            GuildProgressionPlanningResult level = planner.Plan(
                Request(GuildProgressionOperation.AdvanceLevel, "operation_advance_1", AccountMaster,
                    targetLevelId: LevelOne, expectedRevision: 0),
                initial,
                membership);
            Assert.That(level.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(level.Plan.CandidateSnapshot.CurrentLevelId, Is.EqualTo(LevelOne));
            Assert.That(level.Plan.CandidateSnapshot.Revision, Is.EqualTo(1));
            Assert.That(level.Plan.EffectDomains, Is.Empty);
            Assert.That(initial.Revision, Is.Zero);
            Assert.That(initial.CurrentLevelId, Is.Empty);

            GuildProgressionPlanningResult research = planner.Plan(
                Request(GuildProgressionOperation.CompleteResearch, "operation_research_1", AccountMaster,
                    targetResearchId: ResearchOne, expectedRevision: 1),
                level.Plan.CandidateSnapshot,
                membership);
            Assert.That(research.Status, Is.EqualTo(GuildPlanningStatus.Prepared));
            Assert.That(research.Plan.CandidateSnapshot.CompletedResearchIds, Is.EqualTo(new[] { ResearchOne }));
            Assert.That(research.Plan.PlanHash, Has.Length.EqualTo(64));
            Assert.That(research.Plan.RequestFingerprint, Has.Length.EqualTo(64));
            Assert.That(level.Plan.CandidateSnapshot.CompletedResearchIds, Is.Empty);

            GuildMemberPerkProvenance[] provenance = planner
                .ResolveMemberPerkProvenance(research.Plan.CandidateSnapshot, membership)
                .Plan.MemberPerkProvenance.ToArray();
            Assert.That(provenance.Select(row => row.StatBreakdownToken).Distinct().ToArray(),
                Is.EqualTo(new[] { BreakdownToken }));
        }

        [Test]
        public void OfficerCannotAdvanceAndResearchRequiresLevel()
        {
            GuildProgressionPolicySnapshot policy = Policy();
            var planner = new GuildProgressionSnapshotPlanner(policy);
            GuildAuthoritySnapshot membership = ActiveGuildWithMasterAndMember();

            Assert.That(
                planner.Plan(
                    Request(GuildProgressionOperation.AdvanceLevel, "operation_officer_level", AccountMember,
                        targetLevelId: LevelOne, expectedRevision: 0),
                    EmptyProgression(),
                    membership).Status,
                Is.EqualTo(GuildPlanningStatus.Unauthorized));

            Assert.That(
                planner.Plan(
                    Request(GuildProgressionOperation.CompleteResearch, "operation_research_early", AccountMaster,
                        targetResearchId: ResearchOne, expectedRevision: 0),
                    EmptyProgression(),
                    membership).Status,
                Is.EqualTo(GuildPlanningStatus.Ineligible));
        }

        [Test]
        public void ReplaySameFingerprintIsCommittedAndDifferentFingerprintConflicts()
        {
            GuildProgressionPolicySnapshot policy = Policy();
            var planner = new GuildProgressionSnapshotPlanner(policy);
            GuildAuthoritySnapshot membership = ActiveGuildWithMasterAndMember();
            GuildProgressionStateSnapshot committed = Apply(
                planner.Plan(
                    Request(GuildProgressionOperation.AdvanceLevel, "operation_advance_replay", AccountMaster,
                        targetLevelId: LevelOne, expectedRevision: 0),
                    EmptyProgression(),
                    membership));

            GuildProgressionPlanningResult replay = planner.Plan(
                Request(GuildProgressionOperation.AdvanceLevel, "operation_advance_replay", AccountMaster,
                    targetLevelId: LevelOne, expectedRevision: 0),
                committed,
                membership);
            Assert.That(replay.Status, Is.EqualTo(GuildPlanningStatus.AlreadyCommitted));
            Assert.That(replay.ExistingReceipt, Is.Not.Null);

            GuildProgressionPlanningResult conflict = planner.Plan(
                Request(GuildProgressionOperation.AdvanceLevel, "operation_advance_replay", AccountMaster,
                    targetLevelId: LevelOne, expectedRevision: 1),
                committed,
                membership);
            Assert.That(conflict.Status, Is.EqualTo(GuildPlanningStatus.Conflict));
        }

        [Test]
        public void UnavailableCatalogAndAuthorityFailClosed()
        {
            GuildProgressionPolicySnapshot unavailable = Policy(status: GuildCatalogStatus.Unavailable);
            var planner = new GuildProgressionSnapshotPlanner(unavailable);
            Assert.That(
                planner.ResolveMemberPerkProvenance(
                    ProgressedState(LevelOne, ResearchOne),
                    ActiveGuildWithMasterAndMember()).Status,
                Is.EqualTo(GuildPlanningStatus.Unavailable));

            GuildProgressionPolicySnapshot ready = Policy();
            var readyPlanner = new GuildProgressionSnapshotPlanner(ready);
            GuildAuthoritySnapshot missingMembership = new GuildAuthoritySnapshot(
                GuildAuthorityStatus.Unavailable,
                0,
                ready.Binding,
                Array.Empty<GuildSnapshot>(),
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                false);
            Assert.That(
                readyPlanner.ResolveMemberPerkProvenance(EmptyProgression(), missingMembership).Status,
                Is.EqualTo(GuildPlanningStatus.Unavailable));
        }

        [Test]
        public void CombatMutationPerkNeverResolvesAndStaleRevisionIsRejected()
        {
            GuildPerkDefinition combatPerk = Perk(appliesCombatMutation: true);
            GuildProgressionPolicySnapshot policy = Policy(perks: new[] { combatPerk });
            var planner = new GuildProgressionSnapshotPlanner(policy);
            GuildProgressionPlanningResult combat = planner.ResolveMemberPerkProvenance(
                ProgressedState(LevelOne, ResearchOne),
                ActiveGuildWithMasterAndMember());
            Assert.That(combat.Status, Is.EqualTo(GuildPlanningStatus.Malformed));
            Assert.That(combat.Diagnostics.Single().Code, Is.EqualTo("AL-GUILD-PERK-COMBAT-MUTATION"));

            GuildProgressionPolicySnapshot ready = Policy();
            var readyPlanner = new GuildProgressionSnapshotPlanner(ready);
            Assert.That(
                readyPlanner.Plan(
                    Request(GuildProgressionOperation.AdvanceLevel, "operation_stale", AccountMaster,
                        targetLevelId: LevelOne, expectedRevision: 4),
                    EmptyProgression(),
                    ActiveGuildWithMasterAndMember()).Status,
                Is.EqualTo(GuildPlanningStatus.StaleAuthority));
        }

        private static GuildProgressionStateSnapshot Apply(GuildProgressionPlanningResult result)
        {
            Assert.That(result.Status, Is.EqualTo(GuildPlanningStatus.Prepared),
                result.Diagnostics.FirstOrDefault()?.Code);
            return result.Plan.CandidateSnapshot;
        }

        private static GuildProgressionTransitionRequest Request(
            GuildProgressionOperation operation,
            string operationId,
            string actorAccountId,
            string targetLevelId = "",
            string targetResearchId = "",
            long expectedRevision = 0)
        {
            return new GuildProgressionTransitionRequest(
                operation,
                operationId,
                actorAccountId,
                GuildAlpha,
                targetLevelId,
                targetResearchId,
                expectedRevision,
                1,
                Binding());
        }

        private static GuildCatalogBinding Binding()
        {
            return new GuildCatalogBinding(1, "1.0.0", "guild_progression_policy_v1", CatalogHash);
        }

        private static GuildProgressionPolicySnapshot Policy(
            GuildCatalogStatus status = GuildCatalogStatus.Ready,
            bool hiddenGlobalMultipliersForbidden = true,
            bool numericPerkTuningProductionEligible = false,
            GuildPerkDefinition[] perks = null)
        {
            return new GuildProgressionPolicySnapshot(
                status,
                Binding(),
                false,
                false,
                false,
                numericPerkTuningProductionEligible,
                true,
                new[] { new GuildProgressionLevelDefinition(LevelOne, 1, false) },
                new[]
                {
                    new GuildResearchDefinition(
                        ResearchOne, LevelOne, Array.Empty<string>(), false)
                },
                perks ?? new[] { Perk() },
                hiddenGlobalMultipliersForbidden,
                status == GuildCatalogStatus.Ready);
        }

        private static GuildPerkDefinition Perk(bool appliesCombatMutation = false)
        {
            return new GuildPerkDefinition(
                "guild_progression",
                "guild_member_character_stats",
                RuleOne,
                LevelOne,
                ResearchOne,
                GuildPerkScope.MemberCharacterStats,
                new GuildPerkCap("unselected", false),
                new GuildPerkStacking("guild_research_structural", 0, "explicit_visible_only"),
                "1.0.0",
                PerkSourceHash,
                BreakdownToken,
                false,
                false,
                appliesCombatMutation);
        }

        private static GuildProgressionStateSnapshot EmptyProgression()
        {
            return new GuildProgressionStateSnapshot(
                GuildAuthorityStatus.Available,
                GuildAlpha,
                0,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<GuildProgressionReceipt>(),
                true);
        }

        private static GuildProgressionStateSnapshot ProgressedState(
            string levelId,
            string researchId = null)
        {
            string[] research = string.IsNullOrEmpty(researchId)
                ? Array.Empty<string>()
                : new[] { researchId };
            return new GuildProgressionStateSnapshot(
                GuildAuthorityStatus.Available,
                GuildAlpha,
                research.Length == 0 ? 1 : 2,
                levelId,
                research,
                Array.Empty<GuildProgressionReceipt>(),
                true);
        }

        private static GuildAuthoritySnapshot ActiveGuildWithMasterAndMember()
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
                            new GuildMemberSnapshot(
                                AccountMaster, RealmStonehold, GuildRole.Master,
                                GuildMembershipState.Active),
                            new GuildMemberSnapshot(
                                AccountMember, RealmStonehold, GuildRole.Member,
                                GuildMembershipState.Active)
                        })
                },
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);
        }
    }
}
