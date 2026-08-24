using AL.ChampionMode;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.UI.QuestHud;
using AL.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.QuestHud
{
    public sealed class QuestHudPresentationTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            ProofOfWorthDirector.ResetForTests();
            QuestHudAutoQuest.ResetForTests();
            _root = new GameObject("QuestHudTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            QuestHudAutoQuest.ResetForTests();
            ProofOfWorthDirector.ResetForTests();
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            QuestHudOverlay[] overlays = Object.FindObjectsOfType<QuestHudOverlay>();
            for (int i = 0; i < overlays.Length; i++)
            {
                Object.DestroyImmediate(overlays[i].gameObject);
            }

            ProofOfWorthDirector[] directors = Object.FindObjectsOfType<ProofOfWorthDirector>();
            for (int i = 0; i < directors.Length; i++)
            {
                Object.DestroyImmediate(directors[i].gameObject);
            }

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void ThreeDOfferShowsTitleWhatWhereAndAccept()
        {
            ProofOfWorthState offered = ProofOfWorthPlanner.CreateOffered(RealmId.Stonehold);
            QuestHudModel model = QuestHudPlanner.FromProofOfWorth(offered, autoQuestOn: false);

            Assert.AreEqual(ProofOfWorthCopy.OmenTitle, model.Title);
            Assert.AreEqual(ProofOfWorthCopy.OmenTalkObjective, model.WhatToDo);
            Assert.AreEqual(QuestHudCopy.Capital, model.LocationName);
            Assert.AreEqual(QuestHudAction.Accept, model.Action);
            Assert.AreEqual(QuestHudCopy.Accept, model.ActionLabel);
            Assert.IsFalse(QuestHudPlanner.CopyLooksLikeId(model.Title));
            Assert.IsFalse(QuestHudPlanner.CopyLooksLikeId(model.WhatToDo));
            Assert.IsFalse(QuestHudPlanner.CopyLooksLikeId(model.LocationName));
            Assert.IsFalse(model.CanAutoFire);
        }

        [Test]
        public void ArenaStepUsesSkyCastleNotAWarzoneId()
        {
            ProofOfWorthState state = WalkTo(ProofOfWorthPhase.OmenArena, RealmId.Crownlands);
            QuestHudModel model = QuestHudPlanner.FromProofOfWorth(state, autoQuestOn: false);

            Assert.AreEqual(QuestHudCopy.SkyCastle, model.LocationName);
            Assert.AreEqual(QuestHudAction.Continue, model.Action);
            Assert.IsFalse(model.LocationName.ToLowerInvariant().Contains("warzone"));
            Assert.AreNotEqual(FirstSessionInnerRealmSpawn.WarzoneCenterId, model.LocationName);
            Assert.IsFalse(QuestHudPlanner.CopyLooksLikeId(model.LocationName));
        }

        [Test]
        public void TeachingStepIsCastleNotOuterRealm()
        {
            QuestHudModel model = QuestHudPlanner.TeachingStores(autoQuestOn: false);
            Assert.AreEqual(QuestHudCopy.TeachStoresTitle, model.Title);
            Assert.AreEqual(QuestHudCopy.TeachStoresWhat, model.WhatToDo);
            Assert.AreEqual(QuestHudCopy.Castle, model.LocationName);
            Assert.AreEqual(QuestHudAction.Continue, model.Action);
            Assert.AreEqual(QuestHudSurface.Kingdom25D, model.Surface);
            Assert.IsFalse(QuestHudPlanner.CopyLooksLikeId(model.LocationName));
            Assert.IsFalse(QuestHudPlanner.IsForbiddenLocationId(model.LocationName));
        }

        [Test]
        public void WarzoneGateNeverAutoFiresEvenWhenAutoQuestOn()
        {
            QuestHudModel model = QuestHudPlanner.WarzoneGate(autoQuestOn: true);
            Assert.IsTrue(model.IsWarzoneGate);
            Assert.IsFalse(model.CanAutoFire);
            Assert.AreEqual(QuestHudAction.None, model.Action);
            Assert.AreEqual(QuestHudCopy.WarzoneGate, model.LocationName);
            Assert.IsFalse(QuestHudAutoQuest.ShouldFire(model));
            Assert.IsFalse(QuestHudPlanner.CopyLooksLikeId(model.LocationName));
        }

        [Test]
        public void OverlayIsNotLegacyRuntimeAndMountsIntoSlot()
        {
            var slot = new GameObject(QuestHudCopy.SlotName, typeof(RectTransform));
            slot.transform.SetParent(_root.transform, false);

            QuestHudOverlay overlay = QuestHudOverlay.Mount(_root.transform);
            overlay.Bind(
                QuestHudPlanner.FromProofOfWorth(
                    ProofOfWorthPlanner.CreateOffered(RealmId.Eldergrove),
                    autoQuestOn: false),
                () => { });

            Assert.AreSame(slot, overlay.gameObject);
            Assert.AreEqual(ProofOfWorthCopy.OmenTitle, overlay.TitleLabel.text);
            Assert.AreEqual(ProofOfWorthCopy.OmenTalkObjective, overlay.WhatLabel.text);
            Assert.AreEqual(QuestHudCopy.Capital, overlay.WhereLabel.text);
            Assert.IsTrue(overlay.AcceptButton.gameObject.activeSelf);
            Assert.IsFalse(overlay.ContinueButton.gameObject.activeSelf);
            Assert.IsFalse(overlay.CompleteButton.gameObject.activeSelf);
            Assert.IsFalse(overlay.UsesLegacyRuntimeFont());
            Assert.IsNull(overlay.transform.Find("Chrome"));
            Assert.That(overlay.TitleLabel.text, Does.Not.Contain("TEMPORARY"));
        }

        [Test]
        public void FallbackOverlayStaysBoundedAndCannotCoverTheKingdomHud()
        {
            QuestHudOverlay overlay = QuestHudOverlay.Mount();
            overlay.Bind(
                QuestHudPlanner.TeachingStores(autoQuestOn: false),
                () => { });

            RectTransform plate = overlay.transform
                .Find(QuestHudCopy.RootName)
                .GetComponent<RectTransform>();
            Assert.That(plate.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(plate.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(
                plate.sizeDelta,
                Is.EqualTo(new Vector2(
                    QuestHudChrome.PlateWidth,
                    QuestHudChrome.PlateHeight)));
            Assert.That(plate.offsetMin, Is.Not.EqualTo(Vector2.zero));
            Assert.That(plate.offsetMax, Is.Not.EqualTo(Vector2.zero));
        }

        [Test]
        public void AutoQuestOffDoesNotAcceptOffer()
        {
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, _root.transform, RealmId.Umbral);

            Assert.IsFalse(QuestHudAutoQuest.Enabled);
            Assert.IsTrue(director.State.IsOmenOffered);
            Assert.AreEqual(QuestHudCopy.Accept, director.Hud.Model.ActionLabel);
            Assert.IsFalse(director.Hud.Model.CanAutoFire);
        }

        [Test]
        public void AutoQuestOnAcceptsOfferFromHud()
        {
            QuestHudAutoQuest.SetEnabled(true);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, _root.transform, RealmId.Stonehold);

            Assert.IsFalse(director.State.IsOmenOffered);
            Assert.IsTrue(director.State.OmenAccepted);
            Assert.Greater((int)director.State.Phase, (int)ProofOfWorthPhase.OmenOffered);
        }

        [Test]
        public void ManualTapAdvancesWhenAutoQuestOff()
        {
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, _root.transform, RealmId.Crownlands);
            Assert.IsTrue(director.State.IsOmenOffered);

            director.Hud.FirePrimary();
            Assert.IsTrue(director.State.OmenAccepted);
            Assert.AreEqual(QuestHudAction.Continue, director.Hud.Model.Action);
            Assert.IsTrue(director.Hud.ContinueButton.gameObject.activeSelf);
        }

        [Test]
        public void ForbiddenIdsAreStrippedFromWhereLine()
        {
            Assert.AreEqual(string.Empty, QuestHudPlanner.SanitizeLocation("warzone_center_unplayable"));
            Assert.AreEqual(string.Empty, QuestHudPlanner.SanitizeLocation("zone_outer_crownlands"));
            Assert.AreEqual(string.Empty, QuestHudPlanner.SanitizeLocation("poi_zone_inner_stonehold_capital"));
            Assert.AreEqual(QuestHudCopy.Castle, QuestHudPlanner.SanitizeLocation(QuestHudCopy.Castle));
            Assert.AreEqual(QuestHudCopy.Areas, QuestHudPlanner.SanitizeLocation(QuestHudCopy.Areas));
            Assert.AreEqual(QuestHudCopy.WarzoneGate, QuestHudPlanner.SanitizeLocation(QuestHudCopy.WarzoneGate));
        }

        private static ProofOfWorthState WalkTo(ProofOfWorthPhase phase, RealmId realm)
        {
            ProofOfWorthState state = ProofOfWorthPlanner.CreateOffered(realm);
            ProofOfWorthCommand[] commands =
            {
                ProofOfWorthCommand.AcceptOffer,
                ProofOfWorthCommand.Investigate,
                ProofOfWorthCommand.DeployChampion,
                ProofOfWorthCommand.ArenaSuccess,
                ProofOfWorthCommand.SelectValerius,
                ProofOfWorthCommand.PresentTear,
                ProofOfWorthCommand.ConcludeReport,
                ProofOfWorthCommand.MeetRealmGuide,
                ProofOfWorthCommand.RestoreCovenant,
                ProofOfWorthCommand.GuardianDefeated
            };

            for (int i = 0; i < commands.Length && state.Phase != phase; i++)
            {
                ProofOfWorthTransition transition = ProofOfWorthPlanner.Apply(state, commands[i]);
                Assert.AreEqual(ProofOfWorthStatus.Applied, transition.Status, commands[i].ToString());
                state = transition.State;
            }

            Assert.AreEqual(phase, state.Phase);
            return state;
        }
    }
}
