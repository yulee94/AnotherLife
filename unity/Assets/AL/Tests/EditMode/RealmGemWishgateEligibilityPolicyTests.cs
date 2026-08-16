using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.RealmGems;
using AL.Services.Local;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public sealed class RealmGemWishgateEligibilityPolicyTests
    {
        private const string GemId = "gem_crownlands_sun";
        private const string ActorId = "profile.actor";
        private const string RealmZone = "zone.crownlands.frontier";
        private const string WishgateZone = "zone_accordant_isle";

        [Test]
        public void CustodyPolicyAllowsAuthoritativeActorRealmAndNonNeutralZone()
        {
            RealmGemWishgateCatalogSnapshot catalog = LoadValidRuntimeCatalog();
            var authority = new FixedAuthorityProvider(Authority(
                actorRealm: RealmId.Crownlands,
                controllingRealm: RealmId.Crownlands,
                zoneId: RealmZone,
                isNeutralZone: false));

            RealmGemWishgatePolicyResult result = RealmGemWishgateEligibilityPolicy.EvaluateRealmGem(
                catalog,
                authority,
                new RealmGemMutationRequest(GemId, ActorId, RealmZone));

            Assert.That(result.Outcome, Is.EqualTo(RealmGemWishgatePolicyOutcome.Allowed));
            Assert.That(result.Authority.ActorId, Is.EqualTo(ActorId));
        }

        [Test]
        public void TypedCustodyMutationsApplyOnlyAfterFreshPolicyAuthorization()
        {
            RealmGemWishgateCatalogSnapshot catalog = LoadValidRuntimeCatalog();
            var save = NewSave();
            save.RealmGems.Add(HomeGem());
            var saves = new FakeSaveService(save);
            var service = new LocalRealmGemService(
                saves,
                () => catalog,
                new FixedAuthorityProvider(Authority()),
                () => save);

            RealmGemMutationResult pickup = service.PickUpGem(
                new RealmGemMutationRequest(GemId, ActorId, RealmZone));
            RealmGemMutationResult drop = service.DropGem(
                new RealmGemMutationRequest(GemId, ActorId, RealmZone));
            RealmGemMutationResult returned = service.ReturnGemHome(
                new RealmGemMutationRequest(GemId, ActorId, RealmZone));

            Assert.That(pickup.Outcome, Is.EqualTo(RealmGemMutationOutcome.Allowed));
            Assert.That(drop.Outcome, Is.EqualTo(RealmGemMutationOutcome.Allowed));
            Assert.That(returned.Outcome, Is.EqualTo(RealmGemMutationOutcome.Allowed));
            Assert.That(save.RealmGems[0].IsAtHome, Is.True);
            Assert.That(save.RealmGems[0].CarrierId, Is.Null);
            Assert.That(saves.SaveCount, Is.EqualTo(3));
        }

        [Test]
        public void DropRejectsActorWhoDoesNotOwnCurrentCustody()
        {
            RealmGemWishgateCatalogSnapshot catalog = LoadValidRuntimeCatalog();
            var save = NewSave();
            RealmGemState gem = HomeGem();
            gem.IsAtHome = false;
            gem.CarrierId = "profile.other";
            save.RealmGems.Add(gem);
            var saves = new FakeSaveService(save);
            var service = new LocalRealmGemService(
                saves,
                () => catalog,
                new FixedAuthorityProvider(Authority()),
                () => save);

            RealmGemMutationResult result = service.DropGem(
                new RealmGemMutationRequest(GemId, ActorId, RealmZone));

            Assert.That(result.Outcome, Is.EqualTo(RealmGemMutationOutcome.InvalidState));
            Assert.That(gem.CarrierId, Is.EqualTo("profile.other"));
            Assert.That(gem.IsDropped, Is.False);
            Assert.That(saves.SaveCount, Is.Zero);
        }

        [Test]
        public void ReturnHomeRejectsForeignControllerAndForeignCarrier()
        {
            RealmGemWishgateCatalogSnapshot catalog = LoadValidRuntimeCatalog();
            var save = NewSave();
            RealmGemState gem = HomeGem();
            gem.IsAtHome = false;
            gem.CarrierId = "profile.carrier";
            save.RealmGems.Add(gem);
            var saves = new FakeSaveService(save);
            var service = new LocalRealmGemService(
                saves,
                () => catalog,
                new FixedAuthorityProvider(Authority(
                    actorId: "profile.attacker",
                    actorRealm: RealmId.Umbral,
                    controllingRealm: RealmId.Umbral,
                    zoneId: "zone.umbral.frontier")),
                () => save);

            RealmGemMutationResult result = service.ReturnGemHome(
                new RealmGemMutationRequest(
                    GemId,
                    "profile.attacker",
                    "zone.umbral.frontier"));

            Assert.That(result.Outcome, Is.EqualTo(RealmGemMutationOutcome.InvalidState));
            Assert.That(gem.CarrierId, Is.EqualTo("profile.carrier"));
            Assert.That(gem.IsAtHome, Is.False);
            Assert.That(saves.SaveCount, Is.Zero);
        }

        [TestCase(RealmGemMutationOutcome.MissingContext, "", RealmZone, false, true, true)]
        [TestCase(RealmGemMutationOutcome.IneligibleActor, ActorId, RealmZone, false, false, true)]
        [TestCase(RealmGemMutationOutcome.UnauthorizedRealm, ActorId, RealmZone, false, true, false)]
        [TestCase(RealmGemMutationOutcome.DisallowedZone, ActorId, RealmZone, true, true, true)]
        public void CustodyMutationDenialsFailClosedBeforeMutation(
            RealmGemMutationOutcome expected,
            string actorId,
            string zoneId,
            bool neutral,
            bool actorEligible,
            bool authorityMatches)
        {
            RealmGemWishgateCatalogSnapshot catalog = LoadValidRuntimeCatalog();
            var save = NewSave();
            save.RealmGems.Add(HomeGem());
            var saves = new FakeSaveService(save);
            RealmId controllingRealm = authorityMatches ? RealmId.Crownlands : RealmId.Stonehold;
            var service = new LocalRealmGemService(
                saves,
                () => catalog,
                new FixedAuthorityProvider(Authority(
                    actorId: string.IsNullOrEmpty(actorId) ? ActorId : actorId,
                    actorEligible: actorEligible,
                    actorRealm: RealmId.Crownlands,
                    controllingRealm: controllingRealm,
                    zoneId: zoneId,
                    isNeutralZone: neutral)));

            RealmGemMutationResult result = service.PickUpGem(
                new RealmGemMutationRequest(GemId, actorId, zoneId));

            Assert.That(result.Outcome, Is.EqualTo(expected));
            Assert.That(save.RealmGems[0].IsAtHome, Is.True);
            Assert.That(save.RealmGems[0].CarrierId, Is.Null);
            Assert.That(saves.SaveCount, Is.Zero);
        }

        [Test]
        public void MissingMalformedOrThrowingAuthorityFailsClosed()
        {
            RealmGemWishgateCatalogSnapshot catalog = LoadValidRuntimeCatalog();
            foreach (IRealmGemWishgateAuthorityProvider provider in new IRealmGemWishgateAuthorityProvider[]
            {
                null,
                new FixedAuthorityProvider(null),
                new ThrowingAuthorityProvider(),
                new FixedAuthorityProvider(Authority(authorityId: "forged.authority"))
            })
            {
                var save = NewSave();
                save.RealmGems.Add(HomeGem());
                var saves = new FakeSaveService(save);
                var service = new LocalRealmGemService(saves, () => catalog, provider);

                RealmGemMutationResult result = service.PickUpGem(
                    new RealmGemMutationRequest(GemId, ActorId, RealmZone));

                Assert.That(result.Outcome, Is.EqualTo(RealmGemMutationOutcome.UnverifiableAuthority));
                Assert.That(save.RealmGems[0].IsAtHome, Is.True);
                Assert.That(saves.SaveCount, Is.Zero);
            }
        }

        [Test]
        public void LegacyMutationEntryPointCannotBypassAuthoritativePolicy()
        {
            RealmGemWishgateCatalogSnapshot catalog = LoadValidRuntimeCatalog();
            var save = NewSave();
            save.RealmGems.Add(HomeGem());
            var saves = new FakeSaveService(save);
            var service = new LocalRealmGemService(saves, () => catalog, null);

            Assert.That(service.PickUpGem(GemId, ActorId), Is.False);
            service.DropGem(GemId);
            service.ReturnGemHome(GemId);

            Assert.That(save.RealmGems[0].IsAtHome, Is.True);
            Assert.That(saves.SaveCount, Is.Zero);
        }

        [Test]
        public void WishgateRequiresExactCatalogZoneNeutralAuthorityAndEntitlement()
        {
            RealmGemWishgateCatalogSnapshot catalog = EligibilityCatalog();
            var authority = new FixedAuthorityProvider(Authority(
                actorRealm: RealmId.Crownlands,
                controllingRealm: RealmId.None,
                zoneId: WishgateZone,
                isNeutralZone: true,
                entitlementEligible: true,
                requiredGemCount: catalog.Wishgate.RequiredGemCount));

            RealmGemWishgatePolicyResult result = RealmGemWishgateEligibilityPolicy.EvaluateWishgate(
                catalog,
                authority,
                new WishgateUseRequest(ActorId, WishgateZone));

            Assert.That(result.Outcome, Is.EqualTo(RealmGemWishgatePolicyOutcome.Allowed));
        }

        [Test]
        public void ProductionCatalogKeepsWishgateEligibilityUnavailable()
        {
            RealmGemWishgateCatalogSnapshot catalog = LoadValidRuntimeCatalog();

            RealmGemWishgatePolicyResult result = RealmGemWishgateEligibilityPolicy.EvaluateWishgate(
                catalog,
                new FixedAuthorityProvider(Authority(
                    zoneId: WishgateZone,
                    isNeutralZone: true,
                    entitlementEligible: true,
                    requiredGemCount: catalog.Wishgate.RequiredGemCount)),
                new WishgateUseRequest(ActorId, WishgateZone));

            Assert.That(result.Outcome, Is.EqualTo(RealmGemWishgatePolicyOutcome.CatalogUnavailable));
            Assert.That(result.TechnicalCode, Is.EqualTo("AL-RGW-POLICY-CATALOG"));
        }

        [TestCase(RealmGemWishgatePolicyOutcome.DisallowedZone, "zone.other", true, true, 8)]
        [TestCase(RealmGemWishgatePolicyOutcome.DisallowedZone, WishgateZone, false, true, 8)]
        [TestCase(RealmGemWishgatePolicyOutcome.EntitlementMissing, WishgateZone, true, false, 8)]
        [TestCase(RealmGemWishgatePolicyOutcome.EntitlementMissing, WishgateZone, true, true, 7)]
        public void WishgateDenialsAreExplicitAndFailClosed(
            RealmGemWishgatePolicyOutcome expected,
            string zoneId,
            bool neutral,
            bool entitled,
            int gemCount)
        {
            RealmGemWishgateCatalogSnapshot catalog = EligibilityCatalog();
            var provider = new FixedAuthorityProvider(Authority(
                actorRealm: RealmId.Crownlands,
                controllingRealm: RealmId.None,
                zoneId: zoneId,
                isNeutralZone: neutral,
                entitlementEligible: entitled,
                requiredGemCount: gemCount));

            RealmGemWishgatePolicyResult result = RealmGemWishgateEligibilityPolicy.EvaluateWishgate(
                catalog,
                provider,
                new WishgateUseRequest(ActorId, zoneId));

            Assert.That(result.Outcome, Is.EqualTo(expected));
        }

        private static RealmGemWishgateAuthoritySnapshot Authority(
            string actorId = ActorId,
            string authorityId = "another-life.realm-gem-wishgate.production",
            bool actorEligible = true,
            RealmId actorRealm = RealmId.Crownlands,
            RealmId controllingRealm = RealmId.Crownlands,
            string zoneId = RealmZone,
            bool isNeutralZone = false,
            bool entitlementEligible = false,
            int requiredGemCount = 0)
        {
            return new RealmGemWishgateAuthoritySnapshot(
                authorityId,
                1,
                actorId,
                actorEligible,
                actorRealm,
                zoneId,
                controllingRealm,
                isNeutralZone,
                entitlementEligible,
                requiredGemCount);
        }

        private static RealmGemState HomeGem() => new RealmGemState
        {
            GemId = GemId,
            HomeRealm = RealmId.Crownlands,
            GemIndex = 1,
            IsAtHome = true
        };

        private static RealmGemWishgateCatalogSnapshot LoadValidRuntimeCatalog()
        {
            string path = System.IO.Path.Combine(
                UnityEngine.Application.dataPath,
                "StreamingAssets",
                RealmGemWishgateRuntimeCatalog.RelativePath);
            RealmGemWishgateCatalogLoadResult result =
                RealmGemWishgateRuntimeCatalog.Parse(System.IO.File.ReadAllText(path));
            Assert.That(result.IsSuccess, Is.True, result.TechnicalCode);
            return result.Snapshot;
        }

        private static RealmGemWishgateCatalogSnapshot EligibilityCatalog()
        {
            RealmGemWishgateCatalogSnapshot production = LoadValidRuntimeCatalog();
            return new RealmGemWishgateCatalogSnapshot(
                production.CatalogId,
                production.ContentVersion,
                production.AuthorityId,
                production.AuthorityVersion,
                production.SourceCatalogId,
                production.SourcePacketId,
                production.SourceSha256,
                production.CustodyAuthorityAvailable,
                production.RealmGems,
                new WishgateCatalogEntry(
                    production.Wishgate.Id,
                    production.Wishgate.EntryZoneId,
                    production.Wishgate.RequiredGemCount,
                    true,
                    false,
                    Array.Empty<string>()));
        }

        private static SaveGameData NewSave() => new SaveGameData
        {
            SaveFormatId = SaveGameData.CurrentSaveFormatId,
            SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion,
            ProfileInitializationVersion = SaveGameData.CurrentProfileInitializationVersion,
            SelectedRealm = RealmId.Crownlands,
            RealmGems = new List<RealmGemState>(),
            Wishgate = new WishgateState()
        };

        private sealed class FixedAuthorityProvider : IRealmGemWishgateAuthorityProvider
        {
            private readonly RealmGemWishgateAuthoritySnapshot _snapshot;
            public FixedAuthorityProvider(RealmGemWishgateAuthoritySnapshot snapshot) { _snapshot = snapshot; }
            public RealmGemWishgateAuthoritySnapshot Resolve(string actorId, string zoneId) => _snapshot;
        }

        private sealed class ThrowingAuthorityProvider : IRealmGemWishgateAuthorityProvider
        {
            public RealmGemWishgateAuthoritySnapshot Resolve(string actorId, string zoneId) =>
                throw new InvalidOperationException("authority unavailable");
        }

        private sealed class FakeSaveService : AL.Core.Interfaces.ISaveGameService
        {
            public FakeSaveService(SaveGameData save) { CurrentSave = save; }
            public SaveGameData CurrentSave { get; private set; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus { get; private set; }
            public string LastSaveMessage => string.Empty;
            public int SaveCount { get; private set; }
            public void Save() { SaveCount++; LastSaveStatus = SaveOperationStatus.SavedPrimary; }
            public void Load() { }
            public bool HasSave() => CurrentSave != null;
            public void CreateNewSave(RealmId realmId) { }
            public void DeleteSave() { CurrentSave = null; }
        }
    }
}
