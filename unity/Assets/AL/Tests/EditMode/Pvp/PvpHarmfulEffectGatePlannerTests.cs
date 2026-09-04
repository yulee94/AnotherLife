using System;
using System.Collections.Generic;
using System.Linq;
using AL.Alliances;
using AL.Guilds;
using AL.Pvp;
using NUnit.Framework;

namespace AL.Tests.EditMode.Pvp
{
    public sealed class PvpHarmfulEffectGatePlannerTests
    {
        private const string AccountAlpha = "account_alpha_001";
        private const string AccountBeta = "account_beta_001";
        private const string AccountGamma = "account_gamma_001";
        private const string CharacterAlpha = "character_alpha_001";
        private const string CharacterBeta = "character_beta_001";
        private const string CharacterGamma = "character_gamma_001";
        private const string GuildAlpha = "guild_alpha_001";
        private const string GuildBeta = "guild_beta_001";
        private const string GuildGamma = "guild_gamma_001";
        private const string AllianceStone = "alliance_stone_001";
        private const string AllianceForge = "alliance_forge_001";
        private const string RealmStonehold = "stonehold";
        private const string RealmCrownlands = "crownlands";
        private const string ZoneOpen = "zone_open_field_01";
        private const string ZoneCity = "zone_city_anvildeep";
        private const long ClockZero = 1_700_000_000;
        private const long Hour = 3600;
        private static readonly string CatalogHash = new string('a', 64);

        private static readonly PvpHarmfulEffectKind[] AllEffectKinds =
        {
            PvpHarmfulEffectKind.DirectHit,
            PvpHarmfulEffectKind.Projectile,
            PvpHarmfulEffectKind.AreaOfEffect,
            PvpHarmfulEffectKind.DamageOverTimeTick,
            PvpHarmfulEffectKind.Chain,
            PvpHarmfulEffectKind.Trap,
            PvpHarmfulEffectKind.PetSummon,
            PvpHarmfulEffectKind.Reflect,
            PvpHarmfulEffectKind.Splash,
            PvpHarmfulEffectKind.Environmental,
            PvpHarmfulEffectKind.CrowdControl
        };

        [Test]
        public void MutualTogglesAllowEveryHarmfulEffectKindOutsideSafeZones()
        {
            PvpHarmfulEffectGatePlanner planner = Planner();
            foreach (PvpHarmfulEffectKind kind in AllEffectKinds)
            {
                PvpHarmfulEffectGateDecision decision = planner.Evaluate(
                    Query(kind, sourceToggle: true, targetToggle: true));
                Assert.That(decision.Eligible, Is.True, kind.ToString());
                Assert.That(decision.Status, Is.EqualTo(PvpGateStatus.Eligible), kind.ToString());
                Assert.That(decision.Reason, Is.EqualTo(PvpGateRejectReason.None), kind.ToString());
                Assert.That(decision.EffectKind, Is.EqualTo(kind));
                Assert.That(decision.ForcedByActiveWar, Is.False);
                Assert.That(decision.Presentation, Is.EqualTo(PvpPresentationKind.Hostile));
                Assert.That(decision.PresentationIsAuthoritative, Is.False);
                Assert.That(decision.MutatesHealth, Is.False);
            }
        }

        [Test]
        public void OneToggleOffRejectsUnlessOpposingActiveWarExists()
        {
            PvpHarmfulEffectGatePlanner planner = Planner();
            foreach (PvpHarmfulEffectKind kind in AllEffectKinds)
            {
                PvpHarmfulEffectGateDecision off = planner.Evaluate(
                    Query(kind, sourceToggle: true, targetToggle: false));
                Assert.That(off.Eligible, Is.False, kind.ToString());
                Assert.That(off.Reason, Is.EqualTo(PvpGateRejectReason.ToggleOff), kind.ToString());
                Assert.That(off.Presentation, Is.EqualTo(PvpPresentationKind.Neutral));
            }

            PvpHarmfulEffectGateDecision war = planner.Evaluate(
                Query(
                    PvpHarmfulEffectKind.DirectHit,
                    sourceToggle: false,
                    targetToggle: false,
                    withOpposingWar: true,
                    clock: ClockZero + (24 * Hour)));
            Assert.That(war.Eligible, Is.True);
            Assert.That(war.ForcedByActiveWar, Is.True);
            Assert.That(war.Presentation, Is.EqualTo(PvpPresentationKind.WarHostile));
            Assert.That(war.PresentationIsAuthoritative, Is.False);
        }

        [Test]
        public void DeclaredWarDoesNotOverrideTogglesAndCoolingNeverForces()
        {
            PvpHarmfulEffectGatePlanner planner = Planner();
            PvpHarmfulEffectGateDecision declared = planner.Evaluate(
                Query(
                    PvpHarmfulEffectKind.Projectile,
                    sourceToggle: false,
                    targetToggle: false,
                    withOpposingWar: true,
                    clock: ClockZero));
            Assert.That(declared.Eligible, Is.False);
            Assert.That(declared.Reason, Is.EqualTo(PvpGateRejectReason.ToggleOff));
            Assert.That(declared.ForcedByActiveWar, Is.False);

            PvpHarmfulEffectGateDecision cooling = planner.Evaluate(
                Query(
                    PvpHarmfulEffectKind.Projectile,
                    sourceToggle: false,
                    targetToggle: false,
                    withOpposingWar: true,
                    clock: ClockZero + (24 * Hour) + (168 * Hour)));
            Assert.That(cooling.Eligible, Is.False);
            Assert.That(cooling.Reason, Is.EqualTo(PvpGateRejectReason.ToggleOff));
        }

        [Test]
        public void ForcedSafeZonesRejectEvenDuringActiveWar()
        {
            PvpHarmfulEffectGatePlanner planner = Planner();
            foreach (PvpZonePolicyKind zone in new[]
            {
                PvpZonePolicyKind.City,
                PvpZonePolicyKind.Beginner,
                PvpZonePolicyKind.Accordant,
                PvpZonePolicyKind.ForcedSafe
            })
            {
                PvpHarmfulEffectGateDecision sourceSafe = planner.Evaluate(
                    Query(
                        PvpHarmfulEffectKind.AreaOfEffect,
                        sourceToggle: true,
                        targetToggle: true,
                        withOpposingWar: true,
                        clock: ClockZero + (24 * Hour),
                        sourceZone: zone));
                Assert.That(sourceSafe.Eligible, Is.False, zone.ToString());
                Assert.That(sourceSafe.Reason, Is.EqualTo(PvpGateRejectReason.ForcedSafeZone), zone.ToString());
                Assert.That(sourceSafe.ForcedByActiveWar, Is.False, zone.ToString());
                Assert.That(sourceSafe.Presentation, Is.EqualTo(PvpPresentationKind.Protected));

                PvpHarmfulEffectGateDecision targetSafe = planner.Evaluate(
                    Query(
                        PvpHarmfulEffectKind.DamageOverTimeTick,
                        sourceToggle: true,
                        targetToggle: true,
                        withOpposingWar: true,
                        clock: ClockZero + (24 * Hour),
                        targetZone: zone));
                Assert.That(targetSafe.Eligible, Is.False, zone + "-target");
                Assert.That(targetSafe.Reason, Is.EqualTo(PvpGateRejectReason.ForcedSafeZone));
            }
        }

        [Test]
        public void SameGuildAndSameAllianceNeverReceiveHarmfulEffects()
        {
            PvpHarmfulEffectGatePlanner planner = Planner();
            foreach (PvpHarmfulEffectKind kind in AllEffectKinds)
            {
                PvpHarmfulEffectGateDecision guild = planner.Evaluate(
                    Query(
                        kind,
                        sourceToggle: true,
                        targetToggle: true,
                        withOpposingWar: true,
                        clock: ClockZero + (24 * Hour),
                        sameGuild: true));
                Assert.That(guild.Eligible, Is.False, kind + "-guild");
                Assert.That(guild.Reason, Is.EqualTo(PvpGateRejectReason.SameGuild), kind + "-guild");
                Assert.That(guild.Presentation, Is.EqualTo(PvpPresentationKind.Protected));

                PvpHarmfulEffectGateDecision alliance = planner.Evaluate(
                    Query(
                        kind,
                        sourceToggle: true,
                        targetToggle: true,
                        withOpposingWar: true,
                        clock: ClockZero + (24 * Hour),
                        sameAlliance: true));
                Assert.That(alliance.Eligible, Is.False, kind + "-alliance");
                Assert.That(alliance.Reason, Is.EqualTo(PvpGateRejectReason.SameAlliance), kind + "-alliance");
            }
        }

        [Test]
        public void InvalidStaleUnknownAndCrossRealmFailClosed()
        {
            PvpHarmfulEffectGatePlanner planner = Planner();
            Assert.That(planner.Evaluate(null).Status, Is.EqualTo(PvpGateStatus.Indeterminate));

            PvpHarmfulEffectGateDecision cross = planner.Evaluate(
                Query(PvpHarmfulEffectKind.DirectHit, sourceToggle: true, targetToggle: true,
                    targetRealm: RealmCrownlands));
            Assert.That(cross.Eligible, Is.False);
            Assert.That(cross.Reason, Is.EqualTo(PvpGateRejectReason.CrossRealm));

            PvpHarmfulEffectGateDecision same = planner.Evaluate(
                Query(PvpHarmfulEffectKind.DirectHit, sourceToggle: true, targetToggle: true, sameActor: true));
            Assert.That(same.Eligible, Is.False);
            Assert.That(same.Reason, Is.EqualTo(PvpGateRejectReason.SameActor));

            foreach (PvpActorLifeState life in new[]
            {
                PvpActorLifeState.Dead,
                PvpActorLifeState.Loading,
                PvpActorLifeState.Disconnected,
                PvpActorLifeState.Unknown
            })
            {
                PvpHarmfulEffectGateDecision notLive = planner.Evaluate(
                    Query(PvpHarmfulEffectKind.Chain, sourceToggle: true, targetToggle: true, targetLife: life));
                Assert.That(notLive.Eligible, Is.False, life.ToString());
                Assert.That(notLive.Reason, Is.EqualTo(PvpGateRejectReason.ActorNotLive), life.ToString());
            }

            PvpHarmfulEffectQuery fresh = Query(
                PvpHarmfulEffectKind.Trap, sourceToggle: true, targetToggle: true);
            PvpHarmfulEffectGateDecision staleToggle = planner.Evaluate(
                new PvpHarmfulEffectQuery(
                    fresh.Source,
                    fresh.Target,
                    fresh.Provenance,
                    fresh.Guilds,
                    fresh.Alliances,
                    fresh.ExpectedGuildAuthorityRevision,
                    fresh.ExpectedAllianceAuthorityRevision,
                    fresh.ExpectedSourceToggleRevision + 1,
                    fresh.ExpectedTargetToggleRevision,
                    fresh.ExpectedSourceZoneRevision,
                    fresh.ExpectedTargetZoneRevision,
                    fresh.ExpectedSourceActorRevision,
                    fresh.ExpectedTargetActorRevision,
                    fresh.ExpectedCatalogBinding,
                    fresh.ClockUnixSeconds));
            Assert.That(staleToggle.Eligible, Is.False);
            Assert.That(staleToggle.Reason, Is.EqualTo(PvpGateRejectReason.StaleRevision));

            PvpHarmfulEffectGateDecision unknownZone = planner.Evaluate(
                Query(
                    PvpHarmfulEffectKind.Splash,
                    sourceToggle: true,
                    targetToggle: true,
                    targetZone: PvpZonePolicyKind.Unknown));
            Assert.That(unknownZone.Status, Is.EqualTo(PvpGateStatus.Indeterminate));
            Assert.That(unknownZone.Reason, Is.EqualTo(PvpGateRejectReason.UnknownAuthority));
        }

        [Test]
        public void ProvenanceMismatchAndCatalogFaultsFailClosed()
        {
            PvpHarmfulEffectGatePlanner planner = Planner();
            PvpHarmfulEffectQuery query = Query(
                PvpHarmfulEffectKind.PetSummon, sourceToggle: true, targetToggle: true);
            PvpHarmfulEffectGateDecision stolen = planner.Evaluate(
                new PvpHarmfulEffectQuery(
                    query.Source,
                    query.Target,
                    new PvpEffectProvenance(
                        AccountGamma,
                        CharacterGamma,
                        "action_pet_001",
                        "hit_pet_001",
                        PvpHarmfulEffectKind.PetSummon),
                    query.Guilds,
                    query.Alliances,
                    query.ExpectedGuildAuthorityRevision,
                    query.ExpectedAllianceAuthorityRevision,
                    query.ExpectedSourceToggleRevision,
                    query.ExpectedTargetToggleRevision,
                    query.ExpectedSourceZoneRevision,
                    query.ExpectedTargetZoneRevision,
                    query.ExpectedSourceActorRevision,
                    query.ExpectedTargetActorRevision,
                    query.ExpectedCatalogBinding,
                    query.ClockUnixSeconds));
            Assert.That(stolen.Eligible, Is.False);
            Assert.That(stolen.Reason, Is.EqualTo(PvpGateRejectReason.ProvenanceMismatch));

            var clientPolicy = new PvpHarmfulEffectGatePolicySnapshot(
                PvpCatalogStatus.Ready,
                Policy().Binding,
                AllEffectKinds,
                Policy().ForcedSafeZones,
                AllianceWarState.Active,
                24,
                168,
                false,
                true,
                false,
                true);
            Assert.That(new PvpHarmfulEffectGatePlanner(clientPolicy).Evaluate(query).Status,
                Is.EqualTo(PvpGateStatus.Indeterminate));

            var mutating = new PvpHarmfulEffectGatePolicySnapshot(
                PvpCatalogStatus.Ready,
                Policy().Binding,
                AllEffectKinds,
                Policy().ForcedSafeZones,
                AllianceWarState.Active,
                24,
                168,
                false,
                false,
                true,
                true);
            Assert.That(new PvpHarmfulEffectGatePlanner(mutating).Evaluate(query).Status,
                Is.EqualTo(PvpGateStatus.Indeterminate));
        }

        [Test]
        public void AreaCandidatesAreFilteredIndividuallyWithoutCancelingValidHits()
        {
            PvpHarmfulEffectGatePlanner planner = Planner();
            PvpHarmfulEffectQuery hostile = Query(
                PvpHarmfulEffectKind.AreaOfEffect, sourceToggle: true, targetToggle: true);
            PvpHarmfulEffectQuery protectedMate = Query(
                PvpHarmfulEffectKind.AreaOfEffect,
                sourceToggle: true,
                targetToggle: true,
                sameGuild: true);
            IReadOnlyList<PvpHarmfulEffectGateDecision> results = planner.EvaluateEach(
                new[] { hostile, protectedMate });
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].Eligible, Is.True);
            Assert.That(results[1].Eligible, Is.False);
            Assert.That(results[1].Reason, Is.EqualTo(PvpGateRejectReason.SameGuild));
            Assert.That(results[0].MutatesHealth, Is.False);
        }

        [Test]
        public void DelayedTickRechecksZoneAndDoesNotTrustNameplatePresentation()
        {
            PvpHarmfulEffectGatePlanner planner = Planner();
            PvpHarmfulEffectGateDecision impact = planner.Evaluate(
                Query(PvpHarmfulEffectKind.Projectile, sourceToggle: true, targetToggle: true));
            Assert.That(impact.Eligible, Is.True);
            Assert.That(impact.Presentation, Is.EqualTo(PvpPresentationKind.Hostile));

            PvpHarmfulEffectGateDecision laterTick = planner.Evaluate(
                Query(
                    PvpHarmfulEffectKind.DamageOverTimeTick,
                    sourceToggle: true,
                    targetToggle: true,
                    targetZone: PvpZonePolicyKind.City));
            Assert.That(laterTick.Eligible, Is.False);
            Assert.That(laterTick.Reason, Is.EqualTo(PvpGateRejectReason.ForcedSafeZone));
            Assert.That(laterTick.Presentation, Is.EqualTo(PvpPresentationKind.Protected));
            Assert.That(laterTick.PresentationIsAuthoritative, Is.False);
        }

        private static PvpHarmfulEffectGatePlanner Planner()
        {
            return new PvpHarmfulEffectGatePlanner(Policy());
        }

        private static PvpHarmfulEffectGatePolicySnapshot Policy()
        {
            var binding = new GuildCatalogBinding(
                1, "1.0.0", "pvp_harmful_effect_gate_policy_v1", CatalogHash);
            return new PvpHarmfulEffectGatePolicySnapshot(
                PvpCatalogStatus.Ready,
                binding,
                AllEffectKinds,
                new[]
                {
                    PvpZonePolicyKind.City,
                    PvpZonePolicyKind.Beginner,
                    PvpZonePolicyKind.Accordant,
                    PvpZonePolicyKind.ForcedSafe
                },
                AllianceWarState.Active,
                24,
                168,
                false,
                false,
                false,
                true);
        }

        private static PvpHarmfulEffectQuery Query(
            PvpHarmfulEffectKind kind,
            bool sourceToggle,
            bool targetToggle,
            bool withOpposingWar = false,
            bool sameGuild = false,
            bool sameAlliance = false,
            bool sameActor = false,
            string targetRealm = RealmStonehold,
            PvpZonePolicyKind sourceZone = PvpZonePolicyKind.Open,
            PvpZonePolicyKind targetZone = PvpZonePolicyKind.Open,
            PvpActorLifeState targetLife = PvpActorLifeState.Alive,
            long clock = ClockZero)
        {
            PvpHarmfulEffectGatePolicySnapshot policy = Policy();
            PvpActorSnapshot source = Actor(
                AccountAlpha, CharacterAlpha, RealmStonehold, sourceZone, sourceToggle);
            PvpActorSnapshot target = sameActor
                ? source
                : Actor(
                    AccountBeta,
                    CharacterBeta,
                    targetRealm,
                    targetZone,
                    targetToggle,
                    life: targetLife);
            GuildAuthoritySnapshot guilds = Guilds(sameGuild, sameAlliance, withOpposingWar);
            AllianceAuthoritySnapshot alliances = Alliances(sameAlliance, withOpposingWar);
            return new PvpHarmfulEffectQuery(
                source,
                target,
                new PvpEffectProvenance(
                    AccountAlpha,
                    CharacterAlpha,
                    "action_" + kind.ToString().ToLowerInvariant(),
                    "hit_" + kind.ToString().ToLowerInvariant(),
                    kind),
                guilds,
                alliances,
                guilds.Revision,
                alliances.Revision,
                source.PvpToggleRevision,
                target.PvpToggleRevision,
                source.ZonePolicyRevision,
                target.ZonePolicyRevision,
                source.ActorRevision,
                target.ActorRevision,
                policy.Binding,
                clock);
        }

        private static PvpActorSnapshot Actor(
            string accountId,
            string characterId,
            string realm,
            PvpZonePolicyKind zone,
            bool toggle,
            PvpActorLifeState life = PvpActorLifeState.Alive)
        {
            return new PvpActorSnapshot(
                accountId,
                characterId,
                1,
                realm,
                life,
                zone,
                zone == PvpZonePolicyKind.City ? ZoneCity : ZoneOpen,
                1,
                toggle,
                1,
                1);
        }

        private static GuildAuthoritySnapshot Guilds(
            bool sameGuild,
            bool sameAlliance,
            bool withOpposingWar)
        {
            var alpha = new GuildSnapshot(
                GuildAlpha,
                RealmStonehold,
                1,
                GuildStatus.Active,
                new[]
                {
                    new GuildMemberSnapshot(
                        AccountAlpha, RealmStonehold, GuildRole.Master, GuildMembershipState.Active)
                });
            var beta = new GuildSnapshot(
                sameGuild ? GuildAlpha : GuildBeta,
                RealmStonehold,
                1,
                GuildStatus.Active,
                new[]
                {
                    new GuildMemberSnapshot(
                        AccountBeta, RealmStonehold, GuildRole.Member, GuildMembershipState.Active)
                });
            var gamma = new GuildSnapshot(
                GuildGamma,
                RealmStonehold,
                1,
                GuildStatus.Active,
                new[]
                {
                    new GuildMemberSnapshot(
                        AccountGamma, RealmStonehold, GuildRole.Master, GuildMembershipState.Active)
                });
            GuildSnapshot[] rows = sameGuild
                ? new[] { MergeGuild(alpha, beta), gamma }
                : new[] { alpha, beta, gamma };
            return new GuildAuthoritySnapshot(
                GuildAuthorityStatus.Available,
                1,
                Policy().Binding,
                rows.OrderBy(row => row.GuildId, StringComparer.Ordinal),
                Array.Empty<GuildPendingRequest>(),
                Array.Empty<GuildOperationReceipt>(),
                true);
        }

        private static GuildSnapshot MergeGuild(GuildSnapshot left, GuildSnapshot right)
        {
            return new GuildSnapshot(
                left.GuildId,
                left.ImmutableRealmId,
                left.Revision,
                GuildStatus.Active,
                left.Members.Concat(right.Members));
        }

        private static AllianceAuthoritySnapshot Alliances(bool sameAlliance, bool withOpposingWar)
        {
            if (!sameAlliance && !withOpposingWar)
            {
                return new AllianceAuthoritySnapshot(
                    AllianceAuthorityStatus.Available,
                    1,
                    Policy().Binding,
                    Array.Empty<AllianceSnapshot>(),
                    Array.Empty<AlliancePendingRequest>(),
                    Array.Empty<AllianceWarSnapshot>(),
                    Array.Empty<AllianceOperationReceipt>(),
                    true);
            }

            var stone = new AllianceSnapshot(
                AllianceStone,
                RealmStonehold,
                "hash_stone",
                1,
                AllianceRelationState.Active,
                GuildAlpha,
                sameAlliance
                    ? new[]
                    {
                        new AllianceMemberGuildSnapshot(GuildAlpha, 1),
                        new AllianceMemberGuildSnapshot(GuildBeta, 1)
                    }
                    : new[] { new AllianceMemberGuildSnapshot(GuildAlpha, 1) });
            var forge = new AllianceSnapshot(
                AllianceForge,
                RealmStonehold,
                "hash_forge",
                1,
                AllianceRelationState.Active,
                GuildGamma,
                sameAlliance
                    ? new[] { new AllianceMemberGuildSnapshot(GuildGamma, 1) }
                    : new[]
                    {
                        new AllianceMemberGuildSnapshot(GuildBeta, 1),
                        new AllianceMemberGuildSnapshot(GuildGamma, 1)
                    });
            AllianceWarSnapshot[] wars = withOpposingWar && !sameAlliance
                ? new[]
                {
                    new AllianceWarSnapshot(
                        "war_stone_forge_001",
                        AllianceStone,
                        AllianceForge,
                        AllianceWarState.Declared,
                        ClockZero,
                        0,
                        1,
                        1)
                }
                : Array.Empty<AllianceWarSnapshot>();
            return new AllianceAuthoritySnapshot(
                AllianceAuthorityStatus.Available,
                1,
                Policy().Binding,
                new[] { stone, forge }.OrderBy(row => row.AllianceId, StringComparer.Ordinal),
                Array.Empty<AlliancePendingRequest>(),
                wars,
                Array.Empty<AllianceOperationReceipt>(),
                true);
        }
    }
}
