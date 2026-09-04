using System;
using System.Collections.Generic;
using System.Linq;
using AL.Alliances;
using AL.Guilds;
using AL.Pvp;
using NUnit.Framework;

namespace AL.Tests.EditMode.Pvp
{
    public sealed class PvpHarmfulEffectRuntimeAuthorityTests
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
        private const string ZoneOpen = "zone_warzone_stonehold_gate";
        private const string ZoneCity = "zone_protected_stonehold_city";
        private const string ZoneBeginner = "zone_protected_stonehold_beginner";
        private const string ZoneTown = "zone_protected_stonehold_town";
        private const string ZoneAccordant = "zone_accordant_isle";
        private const string ZoneUnknown = "CityScene";
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
        public void EveryEffectKindAppliesOnlyThroughPlannerAndMayMutateHealth()
        {
            PvpHarmfulEffectRuntimeAuthority authority = Authority();
            var health = new RecordingHealthLedger();
            foreach (PvpHarmfulEffectKind kind in AllEffectKinds)
            {
                PvpHarmfulEffectApplicationReceipt receipt = authority.Apply(
                    Request(kind, sourceToggle: true, targetToggle: true),
                    health);
                Assert.That(receipt.Applied, Is.True, kind.ToString());
                Assert.That(receipt.MayMutateHealth, Is.True, kind.ToString());
                Assert.That(receipt.Gate.Eligible, Is.True, kind.ToString());
                Assert.That(receipt.Gate.EffectKind, Is.EqualTo(kind));
                Assert.That(receipt.Presentation.Kind, Is.EqualTo(PvpPresentationKind.Hostile));
                Assert.That(receipt.Presentation.ShowRedNameplate, Is.True, kind.ToString());
                Assert.That(receipt.Presentation.ShowWarIcon, Is.False, kind.ToString());
                Assert.That(receipt.Presentation.AccessibleLabel, Is.Not.Empty);
                Assert.That(receipt.Presentation.IsAuthoritative, Is.False, kind.ToString());
                Assert.That(receipt.Gate.PresentationIsAuthoritative, Is.False);
                Assert.That(receipt.Gate.MutatesHealth, Is.False, kind.ToString());
            }

            Assert.That(health.AppliedKinds, Is.EqualTo(AllEffectKinds));
            Assert.That(health.RejectedCount, Is.EqualTo(0));
        }

        [Test]
        public void SameGuildAndSameAllianceRejectEveryKindWithZeroMutation()
        {
            PvpHarmfulEffectRuntimeAuthority authority = Authority();
            var health = new RecordingHealthLedger();
            foreach (PvpHarmfulEffectKind kind in AllEffectKinds)
            {
                PvpHarmfulEffectApplicationReceipt guild = authority.Apply(
                    Request(kind, sourceToggle: true, targetToggle: true, sameGuild: true),
                    health);
                Assert.That(guild.Applied, Is.False, kind.ToString());
                Assert.That(guild.MayMutateHealth, Is.False, kind.ToString());
                Assert.That(guild.Gate.Reason, Is.EqualTo(PvpGateRejectReason.SameGuild), kind.ToString());
                Assert.That(guild.Presentation.Kind, Is.EqualTo(PvpPresentationKind.Protected));
                Assert.That(guild.Presentation.IsAuthoritative, Is.False);

                PvpHarmfulEffectApplicationReceipt alliance = authority.Apply(
                    Request(kind, sourceToggle: true, targetToggle: true, sameAlliance: true),
                    health);
                Assert.That(alliance.Applied, Is.False, kind.ToString());
                Assert.That(alliance.MayMutateHealth, Is.False, kind.ToString());
                Assert.That(alliance.Gate.Reason, Is.EqualTo(PvpGateRejectReason.SameAlliance), kind.ToString());
            }

            Assert.That(health.AppliedKinds, Is.Empty);
            Assert.That(health.RejectedCount, Is.EqualTo(AllEffectKinds.Length * 2));
        }

        [Test]
        public void Canonical181SafeZoneIdsRejectEvenDuringActiveWar()
        {
            PvpHarmfulEffectRuntimeAuthority authority = Authority();
            var health = new RecordingHealthLedger();
            string[] safeIds = { ZoneCity, ZoneBeginner, ZoneTown, ZoneAccordant };
            foreach (string zoneId in safeIds)
            {
                PvpHarmfulEffectApplicationReceipt receipt = authority.Apply(
                    Request(
                        PvpHarmfulEffectKind.Projectile,
                        sourceToggle: false,
                        targetToggle: false,
                        withOpposingWar: true,
                        clock: ClockZero + (24 * Hour),
                        targetZoneId: zoneId),
                    health);
                Assert.That(receipt.Applied, Is.False, zoneId);
                Assert.That(receipt.MayMutateHealth, Is.False, zoneId);
                Assert.That(receipt.Gate.Reason, Is.EqualTo(PvpGateRejectReason.ForcedSafeZone), zoneId);
                Assert.That(receipt.Presentation.Kind, Is.EqualTo(PvpPresentationKind.Protected), zoneId);
                Assert.That(receipt.Presentation.ShowRedNameplate, Is.False, zoneId);
                Assert.That(receipt.Presentation.ShowWarIcon, Is.False, zoneId);
                Assert.That(receipt.Presentation.IsAuthoritative, Is.False, zoneId);
            }

            Assert.That(health.AppliedKinds, Is.Empty);
        }

        [Test]
        public void UnknownOrHardcodedZoneIdsFailClosed()
        {
            PvpHarmfulEffectRuntimeAuthority authority = Authority();
            var health = new RecordingHealthLedger();
            PvpHarmfulEffectApplicationReceipt unknown = authority.Apply(
                Request(
                    PvpHarmfulEffectKind.DirectHit,
                    sourceToggle: true,
                    targetToggle: true,
                    targetZoneId: ZoneUnknown),
                health);
            Assert.That(unknown.Applied, Is.False);
            Assert.That(unknown.MayMutateHealth, Is.False);
            Assert.That(unknown.Gate.Status, Is.EqualTo(PvpGateStatus.Indeterminate));
            Assert.That(unknown.Gate.Reason, Is.EqualTo(PvpGateRejectReason.UnknownAuthority));
            Assert.That(health.AppliedKinds, Is.Empty);
        }

        [Test]
        public void StaleToggleRevisionFailsClosedWithoutMutation()
        {
            PvpHarmfulEffectRuntimeAuthority authority = Authority();
            var health = new RecordingHealthLedger();
            PvpHarmfulEffectApplicationRequest request = Request(
                PvpHarmfulEffectKind.DamageOverTimeTick,
                sourceToggle: true,
                targetToggle: true);
            var stale = new PvpHarmfulEffectApplicationRequest(
                request.Source,
                request.Target,
                request.Provenance,
                request.Guilds,
                request.Alliances,
                request.ExpectedGuildAuthorityRevision,
                request.ExpectedAllianceAuthorityRevision,
                request.ExpectedSourceToggleRevision + 1,
                request.ExpectedTargetToggleRevision,
                request.ExpectedSourceZoneRevision,
                request.ExpectedTargetZoneRevision,
                request.ExpectedSourceActorRevision,
                request.ExpectedTargetActorRevision,
                request.ExpectedCatalogBinding,
                request.ClockUnixSeconds);
            PvpHarmfulEffectApplicationReceipt receipt = authority.Apply(stale, health);
            Assert.That(receipt.Applied, Is.False);
            Assert.That(receipt.MayMutateHealth, Is.False);
            Assert.That(receipt.Gate.Reason, Is.EqualTo(PvpGateRejectReason.StaleRevision));
            Assert.That(health.AppliedKinds, Is.Empty);
        }

        [Test]
        public void DerivedPresentationCannotAuthorizeHarm()
        {
            PvpHarmfulEffectRuntimeAuthority authority = Authority();
            var health = new RecordingHealthLedger();
            PvpHarmfulEffectApplicationReceipt rejected = authority.Apply(
                Request(PvpHarmfulEffectKind.Splash, sourceToggle: true, targetToggle: false),
                health);
            Assert.That(rejected.Applied, Is.False);
            Assert.That(rejected.Gate.Reason, Is.EqualTo(PvpGateRejectReason.ToggleOff));
            Assert.That(rejected.Presentation.Kind, Is.EqualTo(PvpPresentationKind.Neutral));
            Assert.That(rejected.Presentation.ShowRedNameplate, Is.False);
            Assert.That(rejected.Presentation.IsAuthoritative, Is.False);

            PvpHostileTargetAuthorization targeting = authority.AuthorizeHostileTarget(
                Request(PvpHarmfulEffectKind.DirectHit, sourceToggle: true, targetToggle: false));
            Assert.That(targeting.Allowed, Is.False);
            Assert.That(targeting.Presentation.IsAuthoritative, Is.False);
            Assert.That(targeting.Presentation.Kind, Is.Not.EqualTo(PvpPresentationKind.Hostile));
            Assert.That(health.AppliedKinds, Is.Empty);
        }

        [Test]
        public void MixedAreaCandidatesFilterProtectedWithoutCancelingValidHits()
        {
            PvpHarmfulEffectRuntimeAuthority authority = Authority();
            var health = new RecordingHealthLedger();
            IReadOnlyList<PvpHarmfulEffectApplicationReceipt> results = authority.ApplyEach(
                new[]
                {
                    Request(PvpHarmfulEffectKind.AreaOfEffect, sourceToggle: true, targetToggle: true),
                    Request(
                        PvpHarmfulEffectKind.AreaOfEffect,
                        sourceToggle: true,
                        targetToggle: true,
                        sameGuild: true)
                },
                health);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].Applied, Is.True);
            Assert.That(results[0].MayMutateHealth, Is.True);
            Assert.That(results[1].Applied, Is.False);
            Assert.That(results[1].Gate.Reason, Is.EqualTo(PvpGateRejectReason.SameGuild));
            Assert.That(health.AppliedKinds, Is.EqualTo(new[] { PvpHarmfulEffectKind.AreaOfEffect }));
            Assert.That(health.RejectedCount, Is.EqualTo(1));
        }

        [Test]
        public void ActiveWarPresentsWarIconButDoesNotOverrideSafeOrSameOrg()
        {
            PvpHarmfulEffectRuntimeAuthority authority = Authority();
            var health = new RecordingHealthLedger();
            PvpHarmfulEffectApplicationReceipt war = authority.Apply(
                Request(
                    PvpHarmfulEffectKind.Chain,
                    sourceToggle: false,
                    targetToggle: false,
                    withOpposingWar: true,
                    clock: ClockZero + (24 * Hour)),
                health);
            Assert.That(war.Applied, Is.True);
            Assert.That(war.Gate.ForcedByActiveWar, Is.True);
            Assert.That(war.Presentation.Kind, Is.EqualTo(PvpPresentationKind.WarHostile));
            Assert.That(war.Presentation.ShowRedNameplate, Is.True);
            Assert.That(war.Presentation.ShowWarIcon, Is.True);
            Assert.That(war.Presentation.IsAuthoritative, Is.False);

            PvpHarmfulEffectApplicationReceipt sameGuildWar = authority.Apply(
                Request(
                    PvpHarmfulEffectKind.Chain,
                    sourceToggle: false,
                    targetToggle: false,
                    withOpposingWar: true,
                    sameGuild: true,
                    clock: ClockZero + (24 * Hour)),
                health);
            Assert.That(sameGuildWar.Applied, Is.False);
            Assert.That(sameGuildWar.Gate.Reason, Is.EqualTo(PvpGateRejectReason.SameGuild));
            Assert.That(health.AppliedKinds, Is.EqualTo(new[] { PvpHarmfulEffectKind.Chain }));
        }

        [Test]
        public void UnconfiguredRuntimeGateFailsClosedForPlayerHarm()
        {
            PvpHarmfulEffectRuntimeGate.Reset();
            var health = new RecordingHealthLedger();
            PvpHarmfulEffectApplicationReceipt receipt = PvpHarmfulEffectRuntimeGate.Apply(
                Request(PvpHarmfulEffectKind.DirectHit, sourceToggle: true, targetToggle: true),
                health);
            Assert.That(receipt.Applied, Is.False);
            Assert.That(receipt.MayMutateHealth, Is.False);
            Assert.That(receipt.Gate.Status, Is.EqualTo(PvpGateStatus.Indeterminate));
            Assert.That(receipt.Gate.Reason, Is.EqualTo(PvpGateRejectReason.UnknownAuthority));
            Assert.That(health.AppliedKinds, Is.Empty);

            PvpHarmfulEffectApplicationReceipt overlap = PvpHarmfulEffectRuntimeGate.ApplyOverlap(
                PvpHarmfulEffectKind.Projectile,
                health);
            Assert.That(overlap.Applied, Is.False);
            Assert.That(overlap.Gate.Reason, Is.EqualTo(PvpGateRejectReason.UnknownAuthority));

            PvpHarmfulEffectRuntimeGate.Bind(Authority());
            PvpHarmfulEffectApplicationReceipt bound = PvpHarmfulEffectRuntimeGate.Apply(
                Request(PvpHarmfulEffectKind.DirectHit, sourceToggle: true, targetToggle: true),
                health);
            Assert.That(bound.Applied, Is.True);
            PvpHarmfulEffectRuntimeGate.Reset();
        }

        [Test]
        public void ZoneCatalogBinds181IdsAndDoesNotHardcodeKindsFromSceneNames()
        {
            PvpZonePresenceCatalog catalog = CanonicalZones();
            Assert.That(catalog.TryResolve(ZoneCity, out PvpZonePolicyKind city), Is.True);
            Assert.That(city, Is.EqualTo(PvpZonePolicyKind.City));
            Assert.That(catalog.TryResolve(ZoneBeginner, out PvpZonePolicyKind beginner), Is.True);
            Assert.That(beginner, Is.EqualTo(PvpZonePolicyKind.Beginner));
            Assert.That(catalog.TryResolve(ZoneTown, out PvpZonePolicyKind town), Is.True);
            Assert.That(town, Is.EqualTo(PvpZonePolicyKind.ForcedSafe));
            Assert.That(catalog.TryResolve(ZoneAccordant, out PvpZonePolicyKind accordant), Is.True);
            Assert.That(accordant, Is.EqualTo(PvpZonePolicyKind.Accordant));
            Assert.That(catalog.TryResolve(ZoneOpen, out PvpZonePolicyKind open), Is.True);
            Assert.That(open, Is.EqualTo(PvpZonePolicyKind.Open));
            Assert.That(catalog.TryResolve("City", out _), Is.False);
            Assert.That(catalog.TryResolve("BeginnerArea", out _), Is.False);
            Assert.That(catalog.TryResolve(ZoneUnknown, out _), Is.False);
        }

        private static PvpHarmfulEffectRuntimeAuthority Authority()
        {
            return new PvpHarmfulEffectRuntimeAuthority(
                new PvpHarmfulEffectGatePlanner(Policy()),
                CanonicalZones());
        }

        private static PvpZonePresenceCatalog CanonicalZones()
        {
            return PvpZonePresenceCatalog.FromRecords(
                new[]
                {
                    new PvpZonePresenceRecord(ZoneCity, "city", "forced_non_pvp"),
                    new PvpZonePresenceRecord(ZoneBeginner, "beginner", "forced_non_pvp"),
                    new PvpZonePresenceRecord(ZoneTown, "town", "forced_non_pvp"),
                    new PvpZonePresenceRecord(ZoneAccordant, "accordant", "forced_non_pvp"),
                    new PvpZonePresenceRecord(ZoneOpen, "open", "open_world_pvp")
                });
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

        private static PvpHarmfulEffectApplicationRequest Request(
            PvpHarmfulEffectKind kind,
            bool sourceToggle,
            bool targetToggle,
            bool withOpposingWar = false,
            bool sameGuild = false,
            bool sameAlliance = false,
            string targetZoneId = ZoneOpen,
            long clock = ClockZero)
        {
            PvpHarmfulEffectGatePolicySnapshot policy = Policy();
            PvpActorSnapshot source = Actor(
                AccountAlpha, CharacterAlpha, RealmStonehold, ZoneOpen, sourceToggle);
            PvpActorSnapshot target = Actor(
                AccountBeta, CharacterBeta, RealmStonehold, targetZoneId, targetToggle);
            GuildAuthoritySnapshot guilds = Guilds(sameGuild, sameAlliance, withOpposingWar);
            AllianceAuthoritySnapshot alliances = Alliances(sameAlliance, withOpposingWar);
            return new PvpHarmfulEffectApplicationRequest(
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
            string zoneId,
            bool toggle)
        {
            return new PvpActorSnapshot(
                accountId,
                characterId,
                1,
                realm,
                PvpActorLifeState.Alive,
                PvpZonePolicyKind.Unknown,
                zoneId,
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

        private sealed class RecordingHealthLedger : IPvpHarmfulHealthMutator
        {
            private readonly List<PvpHarmfulEffectKind> applied = new List<PvpHarmfulEffectKind>();

            public IReadOnlyList<PvpHarmfulEffectKind> AppliedKinds => applied;

            public int RejectedCount { get; private set; }

            public bool TryMutate(PvpHarmfulEffectApplicationReceipt receipt)
            {
                if (receipt == null || !receipt.Applied || !receipt.MayMutateHealth)
                {
                    RejectedCount++;
                    return false;
                }

                applied.Add(receipt.Gate.EffectKind);
                return true;
            }
        }
    }
}
