using System.Collections;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.UI.QuestHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public sealed class MainQuestAutoFollowPlayModeTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            ProofOfWorthDirector.ResetForTests();
            QuestHudAutoQuest.ResetForTests();
            _root = new GameObject("MainQuestAutoFollowPlayModeRoot");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            QuestHudAutoQuest.ResetForTests();
            ProofOfWorthDirector.ResetForTests();
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

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
