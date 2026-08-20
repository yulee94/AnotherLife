using System.Collections.Generic;
using AL.ChampionMode;
using AL.ChampionMode.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class WorldInteractionTests
    {
        [Test]
        public void FirstSessionIdsAreAuthoredC1Objectives()
        {
            Assert.AreEqual("OBJ_C1_MEET_REALM_GUIDE", FirstSessionWorldInteractables.GuideCatalogId);
            Assert.AreEqual("OBJ_C1_RESTORE_COVENANT", FirstSessionWorldInteractables.CovenantSiteCatalogId);
            Assert.That(FirstSessionWorldInteractables.GuideObjectName, Does.Contain("TEMPORARY"));
            Assert.That(FirstSessionWorldInteractables.CovenantSiteObjectName, Does.Contain("TEMPORARY"));
        }

        [Test]
        public void PromptCopyUsesTalkUseAndAuthoredSubjects()
        {
            Assert.AreEqual("Talk", WorldInteractionPromptCopy.Verb(WorldInteractionKind.Talk));
            Assert.AreEqual("Use", WorldInteractionPromptCopy.Verb(WorldInteractionKind.Use));
            Assert.AreEqual("Realm Guide", WorldInteractionPromptCopy.Subject(FirstSessionWorldInteractables.GuideCatalogId));
            Assert.AreEqual(
                "Covenant Site",
                WorldInteractionPromptCopy.Subject(FirstSessionWorldInteractables.CovenantSiteCatalogId));
            Assert.AreEqual(
                "Meet the realm guide who interprets the Celestial Tear's response.",
                WorldInteractionPromptCopy.ObjectiveText(FirstSessionWorldInteractables.GuideCatalogId));
            Assert.AreEqual(
                "Restore the damaged covenant site without sacrificing its keepers.",
                WorldInteractionPromptCopy.ObjectiveText(FirstSessionWorldInteractables.CovenantSiteCatalogId));
            Assert.AreEqual(string.Empty, WorldInteractionPromptCopy.Subject("npc_invented_guide"));
            StringAssert.Contains("[F]", WorldInteractionPromptCopy.Compose(
                "F",
                WorldInteractionKind.Talk,
                FirstSessionWorldInteractables.GuideCatalogId));
            StringAssert.Contains("TALK", WorldInteractionPromptCopy.Compose(
                "F",
                WorldInteractionKind.Talk,
                FirstSessionWorldInteractables.GuideCatalogId));
            StringAssert.Contains("USE", WorldInteractionPromptCopy.Compose(
                "F",
                WorldInteractionKind.Use,
                FirstSessionWorldInteractables.CovenantSiteCatalogId));
        }

        [Test]
        public void FocusSelectsLookedAtTargetInsideRange()
        {
            var candidates = new List<WorldInteractionCandidate>
            {
                new WorldInteractionCandidate(
                    FirstSessionWorldInteractables.GuideCatalogId,
                    new Vector3(-3f, 1f, 2f),
                    WorldInteractionKind.Talk,
                    4.6f,
                    48f),
                new WorldInteractionCandidate(
                    FirstSessionWorldInteractables.CovenantSiteCatalogId,
                    new Vector3(3f, 1f, 2f),
                    WorldInteractionKind.Use,
                    4.6f,
                    48f)
            };

            Assert.IsTrue(WorldInteractionFocus.TrySelect(
                Vector3.up,
                new Vector3(-3f, 0f, 2f),
                candidates,
                out int guideIndex));
            Assert.AreEqual(0, guideIndex);

            Assert.IsTrue(WorldInteractionFocus.TrySelect(
                Vector3.up,
                new Vector3(3f, 0f, 2f),
                candidates,
                out int siteIndex));
            Assert.AreEqual(1, siteIndex);
        }

        [Test]
        public void FocusRejectsLookAwayAndOutOfRange()
        {
            var candidates = new List<WorldInteractionCandidate>
            {
                new WorldInteractionCandidate(
                    FirstSessionWorldInteractables.GuideCatalogId,
                    new Vector3(0f, 1f, 2f),
                    WorldInteractionKind.Talk,
                    4.6f,
                    48f)
            };

            Assert.IsFalse(WorldInteractionFocus.TrySelect(
                Vector3.up,
                Vector3.back,
                candidates,
                out _));
            Assert.IsFalse(WorldInteractionFocus.TrySelect(
                new Vector3(0f, 1f, -20f),
                Vector3.forward,
                candidates,
                out _));
        }

        [Test]
        public void PolicyAllowsAvailableActorAndRejectsUnavailable()
        {
            Assert.IsTrue(WorldInteractionPolicy.CanConfirm(true));
            Assert.IsFalse(WorldInteractionPolicy.CanConfirm(false));
        }

        [Test]
        public void ConfirmTalkAndUseReturnAuthoredObjectiveText()
        {
            var host = new GameObject("InteractableHost");
            try
            {
                WorldInteractable talk = host.AddComponent<WorldInteractable>();
                talk.Configure(
                    FirstSessionWorldInteractables.GuideCatalogId,
                    WorldInteractionKind.Talk,
                    WorldInteractionPromptCopy.GuideSubject,
                    WorldInteractionPromptCopy.GuideObjectiveText);
                WorldInteractionResult talkResult = talk.Confirm(true);
                Assert.IsTrue(talkResult.Accepted);
                Assert.AreEqual(FirstSessionWorldInteractables.GuideCatalogId, talkResult.CatalogId);
                Assert.AreEqual(WorldInteractionKind.Talk, talkResult.Kind);
                Assert.AreEqual(WorldInteractionPromptCopy.GuideObjectiveText, talkResult.Feedback);
                Assert.AreEqual(1, talk.ConfirmCount);

                WorldInteractable use = host.AddComponent<WorldInteractable>();
                use.Configure(
                    FirstSessionWorldInteractables.CovenantSiteCatalogId,
                    WorldInteractionKind.Use,
                    WorldInteractionPromptCopy.CovenantSiteSubject,
                    WorldInteractionPromptCopy.CovenantObjectiveText);
                WorldInteractionResult useResult = use.Confirm(true);
                Assert.IsTrue(useResult.Accepted);
                Assert.AreEqual(FirstSessionWorldInteractables.CovenantSiteCatalogId, useResult.CatalogId);
                Assert.AreEqual(WorldInteractionKind.Use, useResult.Kind);
                Assert.AreEqual(WorldInteractionPromptCopy.CovenantObjectiveText, useResult.Feedback);

                Assert.IsFalse(talk.Confirm(false).Accepted);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void InstallPlacesGuideAndCovenantSiteOnFirstSessionPath()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            var player = new GameObject(FirstSessionChampionStart.PlayerObjectName);
            player.transform.position = new Vector3(0f, 1.1f, -7.4f);
            var cameraObject = new GameObject("Main Camera");
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.transform.position = new Vector3(0f, 7.2f, -13.4f);
            camera.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            try
            {
                WorldInteractionDirector director = FirstSessionWorldInteractables.Install(
                    player.transform,
                    camera);
                Assert.NotNull(director);
                WorldInteractable[] spawned = Object.FindObjectsOfType<WorldInteractable>();
                Assert.AreEqual(2, spawned.Length);

                WorldInteractable guide = Find(spawned, FirstSessionWorldInteractables.GuideCatalogId);
                WorldInteractable site = Find(spawned, FirstSessionWorldInteractables.CovenantSiteCatalogId);
                Assert.NotNull(guide);
                Assert.NotNull(site);
                Assert.AreEqual(WorldInteractionKind.Talk, guide.Kind);
                Assert.AreEqual(WorldInteractionKind.Use, site.Kind);
                Assert.AreEqual(FirstSessionWorldInteractables.GuideObjectName, guide.gameObject.name);
                Assert.AreEqual(FirstSessionWorldInteractables.CovenantSiteObjectName, site.gameObject.name);
                Assert.NotNull(GameObject.Find(WorldInteractionPromptView.CanvasName));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(cameraObject);
                GameObject root = GameObject.Find(FirstSessionWorldInteractables.RootName);
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static WorldInteractable Find(WorldInteractable[] spawned, string catalogId)
        {
            for (int i = 0; i < spawned.Length; i++)
            {
                if (spawned[i] != null && spawned[i].CatalogId == catalogId)
                {
                    return spawned[i];
                }
            }

            return null;
        }
    }
}
