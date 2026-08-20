using AL.ChampionMode;
using AL.ChampionMode.Control;
using AL.ChampionMode.Death;
using AL.Core;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class InnerRealmDeathRespawnPlannerTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void PrefersUnnamedCapitalOverNearerArea()
        {
            var death = new InnerRealmVec3(40f, 1.1f, 40f);
            InnerRealmDeathRespawnPlan plan = InnerRealmDeathRespawnPlanner.Plan(
                new InnerRealmDeathRespawnRequest(
                    RealmId.Crownlands,
                    death,
                    InnerRealmDeathZoneKind.Inner,
                    new[]
                    {
                        Area(RealmId.Crownlands, "inner_area_a", new InnerRealmVec3(41f, 1.1f, 41f)),
                        InnerRealmSafeSite.UnnamedCapital(
                            RealmId.Crownlands,
                            new InnerRealmVec3(0f, 1.1f, -7.4f))
                    }));

            Assert.That(plan.IsApplied, Is.True);
            Assert.That(plan.Site.SiteId, Is.EqualTo("inner_capital"));
            Assert.That(plan.Site.Kind, Is.EqualTo(InnerRealmSafeSiteKind.Capital));
            Assert.That(plan.Site.ZoneId, Is.EqualTo("zone_inner_crownlands"));
            Assert.That(plan.Site.Position.X, Is.EqualTo(0f));
            Assert.That(plan.Site.Position.Z, Is.EqualTo(-7.4f));
            AssertPresentationIsStandUp(plan, InnerRealmDeathRespawnPlanner.CapitalStandUpDetail);
        }

        [Test]
        public void FallsBackToNearestInnerAreaWhenCapitalMissing()
        {
            var death = new InnerRealmVec3(10f, 1.1f, 0f);
            InnerRealmDeathRespawnPlan plan = InnerRealmDeathRespawnPlanner.Plan(
                new InnerRealmDeathRespawnRequest(
                    RealmId.Stonehold,
                    death,
                    InnerRealmDeathZoneKind.Inner,
                    new[]
                    {
                        Area(RealmId.Stonehold, "inner_area_far", new InnerRealmVec3(80f, 1.1f, 0f)),
                        Area(RealmId.Stonehold, "inner_area_near", new InnerRealmVec3(12f, 1.1f, 0f)),
                        Area(RealmId.Umbral, "inner_area_other", new InnerRealmVec3(10.5f, 1.1f, 0f))
                    }));

            Assert.That(plan.IsApplied, Is.True);
            Assert.That(plan.Site.SiteId, Is.EqualTo("inner_area_near"));
            Assert.That(plan.Site.Kind, Is.EqualTo(InnerRealmSafeSiteKind.Area));
            Assert.That(plan.Site.ZoneId, Is.EqualTo("zone_inner_stonehold"));
            AssertPresentationIsStandUp(plan, InnerRealmDeathRespawnPlanner.AreaStandUpDetail);
        }

        [Test]
        public void NeverSelectsWarzonePillarEvenWhenCloserOrOnlyOption()
        {
            var death = new InnerRealmVec3(0f, 1.1f, 0f);
            var pillar = new InnerRealmSafeSite(
                "warzone_save_pillar",
                RealmId.Eldergrove,
                InnerRealmSafeSiteKind.WarzonePillar,
                "zone_warzone_eldergrove_gate",
                new InnerRealmVec3(1f, 1.1f, 0f),
                isWarzone: true);

            InnerRealmDeathRespawnPlan closerPillar = InnerRealmDeathRespawnPlanner.Plan(
                new InnerRealmDeathRespawnRequest(
                    RealmId.Eldergrove,
                    death,
                    InnerRealmDeathZoneKind.Inner,
                    new[]
                    {
                        pillar,
                        InnerRealmSafeSite.UnnamedCapital(
                            RealmId.Eldergrove,
                            new InnerRealmVec3(50f, 1.1f, 0f))
                    }));

            Assert.That(closerPillar.Site.Kind, Is.EqualTo(InnerRealmSafeSiteKind.Capital));
            Assert.That(closerPillar.Site.SiteId, Is.EqualTo("inner_capital"));

            InnerRealmDeathRespawnPlan onlyPillar = InnerRealmDeathRespawnPlanner.Plan(
                new InnerRealmDeathRespawnRequest(
                    RealmId.Eldergrove,
                    death,
                    InnerRealmDeathZoneKind.Inner,
                    new[] { pillar }));

            Assert.That(onlyPillar.Status, Is.EqualTo(InnerRealmDeathRespawnStatus.RejectedNoInnerSite));
            Assert.That(onlyPillar.DiagnosticCode, Is.EqualTo(InnerRealmDeathRespawnPlanner.NoInnerSiteCode));
            Assert.That(onlyPillar.Site, Is.Null);
        }

        [Test]
        public void RejectsWarzoneDeathWithoutOwningPillarCamping()
        {
            InnerRealmDeathRespawnPlan plan = InnerRealmDeathRespawnPlanner.Plan(
                new InnerRealmDeathRespawnRequest(
                    RealmId.Umbral,
                    new InnerRealmVec3(0f, 1.1f, 0f),
                    InnerRealmDeathZoneKind.Warzone,
                    new[]
                    {
                        InnerRealmSafeSite.UnnamedCapital(
                            RealmId.Umbral,
                            new InnerRealmVec3(0f, 1.1f, -7.4f))
                    }));

            Assert.That(plan.Status, Is.EqualTo(InnerRealmDeathRespawnStatus.RejectedWarzoneNotOwned));
            Assert.That(plan.DiagnosticCode, Is.EqualTo(InnerRealmDeathRespawnPlanner.WarzoneNotOwnedCode));
            Assert.That(plan.IsApplied, Is.False);
        }

        [Test]
        public void RejectsInvalidRealmAndNullRequest()
        {
            Assert.That(
                InnerRealmDeathRespawnPlanner.Plan(null).Status,
                Is.EqualTo(InnerRealmDeathRespawnStatus.RejectedInvalidRequest));
            Assert.That(
                InnerRealmDeathRespawnPlanner.Plan(
                    new InnerRealmDeathRespawnRequest(
                        RealmId.None,
                        new InnerRealmVec3(0f, 0f, 0f),
                        InnerRealmDeathZoneKind.Inner,
                        new[]
                        {
                            InnerRealmSafeSite.UnnamedCapital(
                                RealmId.Crownlands,
                                new InnerRealmVec3(0f, 1.1f, -7.4f))
                        })).Status,
                Is.EqualTo(InnerRealmDeathRespawnStatus.RejectedInvalidRealm));
        }

        [TestCase(RealmId.Crownlands, "zone_inner_crownlands")]
        [TestCase(RealmId.Stonehold, "zone_inner_stonehold")]
        [TestCase(RealmId.Eldergrove, "zone_inner_eldergrove")]
        [TestCase(RealmId.Umbral, "zone_inner_umbral")]
        public void MapsCommittedRealmToWorldAtlasInnerZone(RealmId realmId, string zoneId)
        {
            string resolved;
            Assert.That(InnerRealmDeathRespawnPlanner.TryInnerZoneId(realmId, out resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(zoneId));

            InnerRealmDeathRespawnPlan plan = InnerRealmDeathRespawnPlanner.Plan(
                new InnerRealmDeathRespawnRequest(
                    realmId,
                    new InnerRealmVec3(3f, 1.1f, 4f),
                    InnerRealmDeathZoneKind.Inner,
                    new[]
                    {
                        InnerRealmSafeSite.UnnamedCapital(realmId, new InnerRealmVec3(0f, 1.1f, -7.4f))
                    }));

            Assert.That(plan.IsApplied, Is.True);
            Assert.That(plan.Site.ZoneId, Is.EqualTo(zoneId));
        }

        [Test]
        public void ReviveRestoresDeadChampionAndHealDoesNot()
        {
            _root = new GameObject("InnerDeathCombatHost");
            ChampionCombat combat = _root.AddComponent<ChampionCombat>();
            combat.TakeDamage(combat.MaxHealth);
            Assert.That(combat.IsDead, Is.True);
            Assert.That(combat.CurrentHealth, Is.EqualTo(0f));

            combat.Heal(combat.MaxHealth);
            Assert.That(combat.IsDead, Is.True);
            Assert.That(combat.CurrentHealth, Is.EqualTo(0f));

            Assert.That(combat.TryRevive(1f), Is.True);
            Assert.That(combat.IsDead, Is.False);
            Assert.That(combat.CurrentHealth, Is.EqualTo(combat.MaxHealth));
            Assert.That(combat.TryRevive(1f), Is.False);
        }

        [Test]
        public void ApplierStandsChampionAtCapitalWithoutReload()
        {
            _root = new GameObject("InnerDeathApplyHost");
            _root.transform.position = new Vector3(18f, 1.1f, 22f);
            ChampionCombat combat = _root.AddComponent<ChampionCombat>();
            ChampionController controller = _root.AddComponent<ChampionController>();
            combat.TakeDamage(combat.MaxHealth);

            InnerRealmDeathRespawnPlan plan = InnerRealmDeathRespawnPlanner.Plan(
                new InnerRealmDeathRespawnRequest(
                    RealmId.Crownlands,
                    new InnerRealmVec3(18f, 1.1f, 22f),
                    InnerRealmDeathZoneKind.Inner,
                    new[]
                    {
                        InnerRealmSafeSite.UnnamedCapital(
                            RealmId.Crownlands,
                            new InnerRealmVec3(0f, 1.1f, -7.4f))
                    }));

            Assert.That(InnerRealmDeathRespawnApplier.TryApply(plan, combat, controller), Is.True);
            Assert.That(combat.IsDead, Is.False);
            Assert.That(_root.transform.position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_root.transform.position.z, Is.EqualTo(-7.4f).Within(0.001f));
            Assert.That(plan.Presentation.ReloadsScene, Is.False);
            Assert.That(plan.Presentation.PersistsSave, Is.False);
        }

        private static InnerRealmSafeSite Area(RealmId realmId, string siteId, InnerRealmVec3 position)
        {
            string zoneId;
            InnerRealmDeathRespawnPlanner.TryInnerZoneId(realmId, out zoneId);
            return new InnerRealmSafeSite(
                siteId,
                realmId,
                InnerRealmSafeSiteKind.Area,
                zoneId,
                position,
                isWarzone: false);
        }

        private static void AssertPresentationIsStandUp(InnerRealmDeathRespawnPlan plan, string detail)
        {
            Assert.That(plan.Presentation, Is.Not.Null);
            Assert.That(
                plan.Presentation.Kind,
                Is.EqualTo(InnerRealmDeathPresentationKind.DefeatThenStandUp));
            Assert.That(plan.Presentation.Title, Is.EqualTo(InnerRealmDeathRespawnPlanner.FallenTitle));
            Assert.That(plan.Presentation.Detail, Is.EqualTo(detail));
            Assert.That(plan.Presentation.ReloadsScene, Is.False);
            Assert.That(plan.Presentation.PersistsSave, Is.False);
            Assert.That(plan.Presentation.AllowsMenuSetRespawn, Is.False);
            Assert.That(plan.Presentation.AllowsPillarBind, Is.False);
        }
    }
}
