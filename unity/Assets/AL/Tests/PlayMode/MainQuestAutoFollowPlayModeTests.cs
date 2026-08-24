using System;
using System.Collections;
using System.IO;
using System.Reflection;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.Kingdom;
using AL.UI.QuestHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AL.Tests.PlayMode
{
    public sealed class MainQuestAutoFollowPlayModeTests
    {
        private GameObject _root;
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            ProofOfWorthDirector.ResetForTests();
            QuestHudAutoQuest.ResetForTests();
            KingdomTeachingInteraction.ResetForTests();
            _root = new GameObject("MainQuestAutoFollowPlayModeRoot");
            _saveRoot = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-MainQuestTeachingPlayModeTests",
                Guid.NewGuid().ToString("N"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            QuestHudAutoQuest.ResetForTests();
            ProofOfWorthDirector.ResetForTests();
            KingdomTeachingInteraction.ResetForTests();
            if (_root != null)
            {
                Object.Destroy(_root);
            }

            ProofOfWorthDirector[] directors = Object.FindObjectsOfType<ProofOfWorthDirector>();
            for (int i = 0; i < directors.Length; i++)
            {
                Object.Destroy(directors[i].gameObject);
            }

            GameObject markerRoot = GameObject.Find(ProofOfWorthDirector.MarkerRootName);
            if (markerRoot != null)
            {
                Object.Destroy(markerRoot);
            }

            yield return null;

            if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot))
            {
                Directory.Delete(_saveRoot, true);
            }
        }

        [UnityTest]
        public IEnumerator AutoQuestOnWalksToAndCompletesOneArrivalWithoutWorldClicks()
        {
            QuestHudAutoQuest.SetEnabled(true);
            var champion = new GameObject("AutoQuestChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            champion.AddComponent<ChampionController>();
            Vector3 start = champion.transform.position;

            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Stonehold);
            Assert.AreEqual(ProofOfWorthPhase.OmenArena, director.State.Phase);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (director.State.Phase == ProofOfWorthPhase.OmenArena &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.Greater((int)director.State.Phase, (int)ProofOfWorthPhase.OmenArena);
            Assert.Greater(HorizontalDistance(start, champion.transform.position), 0.1f);
        }

        [UnityTest]
        public IEnumerator AutoQuestOnStopsFollowWhileCombatIsActive()
        {
            QuestHudAutoQuest.SetEnabled(true);
            var champion = new GameObject("AutoQuestChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            champion.AddComponent<ChampionController>();
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Eldergrove);
            Assert.AreEqual(ProofOfWorthPhase.OmenArena, director.State.Phase);

            var guardian = new GameObject("AutoQuestGuardian");
            guardian.transform.SetParent(_root.transform, false);
            guardian.AddComponent<BossDummyAI>().ConfigureRealmContext(RealmId.Eldergrove);
            Vector3 pausedAt = champion.transform.position;

            yield return new WaitForSeconds(0.25f);

            Assert.AreEqual(ProofOfWorthPhase.OmenArena, director.State.Phase);
            Assert.Less(HorizontalDistance(pausedAt, champion.transform.position), 0.01f);
        }

        [UnityTest]
        public IEnumerator AutoQuestOffLeavesOfferAndChampionUnderManualControl()
        {
            var champion = new GameObject("ManualChampion");
            champion.transform.SetParent(_root.transform, false);
            champion.AddComponent<CharacterController>();
            ChampionController controller = champion.AddComponent<ChampionController>();
            Vector3 start = champion.transform.position;

            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, champion.transform, RealmId.Crownlands);
            controller.SetExternalMoveInput(Vector2.up);

            yield return null;
            Vector3 afterFirstFrame = champion.transform.position;

            yield return new WaitForSeconds(0.25f);

            Assert.IsTrue(director.State.IsOmenOffered);
            Assert.Greater(HorizontalDistance(start, champion.transform.position), 0.1f);
            Assert.Greater(HorizontalDistance(afterFirstFrame, champion.transform.position), 0.1f);
        }

        [UnityTest]
        public IEnumerator AutoQuestOnAdvancesOnePostLordshipTwoPointFiveDTeachingStep()
        {
            Directory.CreateDirectory(_saveRoot);
            ISaveGameService save = CreateSaveService(_saveRoot);
            save.CreateNewSave(RealmId.Crownlands);
            Assert.That(
                MvpLoopSaveAuthority.TryCommit(
                    save,
                    new MvpLoopCommitRequest(
                        Guid.NewGuid().ToString("N"),
                        RealmId.Crownlands,
                        ClassFamily.Mage,
                        true,
                        ProofOfWorthIds.CrownlandsVariantId,
                        string.Empty,
                        0)).Persisted,
                Is.True);

            KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
            QuestHudOverlay hud = QuestHudOverlay.Mount(_root.transform);
            KingdomTeachingDirector director =
                _root.AddComponent<KingdomTeachingDirector>();
            string requestedInteraction = string.Empty;
            KingdomTeachingInteraction.InteractionRequested +=
                interaction => requestedInteraction = interaction;
            QuestHudAutoQuest.SetEnabled(false);
            director.EnsureReady(save, hud, catalog);
            Assert.That(director.State.IsAvailable, Is.True);
            Assert.That(director.State.ProgressValue, Is.Zero);
            Assert.That(director.State.CurrentStep, Is.SameAs(catalog.Steps[0]));
            Assert.That(requestedInteraction, Is.Empty);

            QuestHudAutoQuest.SetEnabled(true);
            director.Refresh();
            yield return null;

            Assert.That(director.State.ProgressValue, Is.EqualTo(1));
            Assert.That(director.State.CurrentStep, Is.SameAs(catalog.Steps[1]));
            Assert.That(director.Hud.Model.Surface, Is.EqualTo(QuestHudSurface.Kingdom25D));
            Assert.That(director.Hud.Model.StepId, Is.EqualTo(catalog.Steps[1].Id));
            Assert.That(requestedInteraction, Is.EqualTo(catalog.Steps[1].Interaction));
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static ISaveGameService CreateSaveService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(constructor, Is.Not.Null);
            return (ISaveGameService)constructor.Invoke(new object[] { root });
        }
    }
}
