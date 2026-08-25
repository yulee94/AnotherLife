using System.Collections;
using AL.ChampionMode.Interaction;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AL.Tests.PlayMode
{
    public sealed class ProofOfWorthWorldInteractionPlayModeTests
    {
        private GameObject _root;
        private bool _previousGameplaySuppressed;

        [SetUp]
        public void SetUp()
        {
            ProofOfWorthDirector.ResetForTests();
            _previousGameplaySuppressed = GameInput.GameplaySuppressed;
            GameInput.SetGameplaySuppressed(true);
            _root = new GameObject("ProofOfWorthWorldInteractionTests");
        }

        [TearDown]
        public void TearDown()
        {
            ProofOfWorthDirector.ResetForTests();
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            Transform[] transforms = Object.FindObjectsOfType<Transform>();
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate != null &&
                    candidate.parent == null &&
                    candidate.name == ProofOfWorthDirector.MarkerRootName)
                {
                    Object.DestroyImmediate(candidate.gameObject);
                }
            }

            ProofOfWorthDirector.ResetForTests();
            GameInput.SetGameplaySuppressed(_previousGameplaySuppressed);
        }

        [Test]
        public void AcceptedMatchingReceiptsAdvanceGuideThenCovenant()
        {
            ProofOfWorthDirector proof = CreateAtGuide();

            Assert.That(
                proof.TryApplyWorldInteraction(Receipt(
                    accepted: true,
                    FirstSessionWorldInteractables.GuideCatalogId,
                    WorldInteractionKind.Talk)),
                Is.True);
            Assert.That(
                proof.State.Phase,
                Is.EqualTo(ProofOfWorthPhase.C1RestoreCovenant));
            Assert.That(
                proof.State.ObjectiveId,
                Is.EqualTo(ProofOfWorthIds.RestoreCovenantObjectiveId));

            Assert.That(
                proof.TryApplyWorldInteraction(Receipt(
                    accepted: true,
                    FirstSessionWorldInteractables.CovenantSiteCatalogId,
                    WorldInteractionKind.Use)),
                Is.True);
            Assert.That(
                proof.State.Phase,
                Is.EqualTo(ProofOfWorthPhase.C1FaceGuardian));
            Assert.That(
                proof.State.ObjectiveId,
                Is.EqualTo(ProofOfWorthIds.FaceGuardianObjectiveId));
        }

        [Test]
        public void WrongOutOfOrderAndRejectedReceiptsFailClosed()
        {
            ProofOfWorthDirector proof = CreateProof();
            AssertReceiptRejectedWithoutStateChange(
                proof,
                Receipt(
                    accepted: true,
                    FirstSessionWorldInteractables.GuideCatalogId,
                    WorldInteractionKind.Talk),
                ProofOfWorthPhase.OmenOffered);
            AdvanceToGuide(proof);

            AssertReceiptRejectedWithoutStateChange(
                proof,
                Receipt(
                    accepted: true,
                    FirstSessionWorldInteractables.CovenantSiteCatalogId,
                    WorldInteractionKind.Use),
                ProofOfWorthPhase.C1MeetGuide);
            AssertReceiptRejectedWithoutStateChange(
                proof,
                Receipt(
                    accepted: false,
                    FirstSessionWorldInteractables.GuideCatalogId,
                    WorldInteractionKind.Talk),
                ProofOfWorthPhase.C1MeetGuide);
            AssertReceiptRejectedWithoutStateChange(
                proof,
                Receipt(
                    accepted: true,
                    FirstSessionWorldInteractables.GuideCatalogId,
                    WorldInteractionKind.Use),
                ProofOfWorthPhase.C1MeetGuide);
            AssertReceiptRejectedWithoutStateChange(
                proof,
                Receipt(
                    accepted: true,
                    FirstSessionWorldInteractables.GuideCatalogId.ToLowerInvariant(),
                    WorldInteractionKind.Talk),
                ProofOfWorthPhase.C1MeetGuide);

            Assert.That(
                proof.TryApplyWorldInteraction(Receipt(
                    accepted: true,
                    FirstSessionWorldInteractables.GuideCatalogId,
                    WorldInteractionKind.Talk)),
                Is.True);
            AssertReceiptRejectedWithoutStateChange(
                proof,
                Receipt(
                    accepted: true,
                    FirstSessionWorldInteractables.GuideCatalogId,
                    WorldInteractionKind.Talk),
                ProofOfWorthPhase.C1RestoreCovenant);
            AssertReceiptRejectedWithoutStateChange(
                proof,
                Receipt(
                    accepted: false,
                    FirstSessionWorldInteractables.CovenantSiteCatalogId,
                    WorldInteractionKind.Use),
                ProofOfWorthPhase.C1RestoreCovenant);
        }

        [UnityTest]
        public IEnumerator BindingReplacesAndUnbindsWorldInteractionEventSource()
        {
            var actor = new GameObject("WorldInteractionActor");
            actor.transform.SetParent(_root.transform, false);
            var cameraObject = new GameObject("WorldInteractionCamera");
            cameraObject.transform.SetParent(_root.transform, false);
            UnityEngine.Camera camera =
                cameraObject.AddComponent<UnityEngine.Camera>();

            ProofOfWorthDirector proof = CreateAtGuide(actor.transform);
            WorldInteractionDirector first = CreateEventSource(
                "FirstWorldInteractionSource",
                actor.transform,
                camera);
            WorldInteractionDirector second = CreateEventSource(
                "SecondWorldInteractionSource",
                actor.transform,
                camera);

            proof.BindWorldInteractionDirector(first);
            proof.BindWorldInteractionDirector(null);
            yield return null;

            Assert.That(first.Focused, Is.Not.Null);
            Assert.That(second.Focused, Is.Not.Null);
            Assert.That(first.TryConfirmFocused(), Is.True);
            Assert.That(
                proof.State.Phase,
                Is.EqualTo(ProofOfWorthPhase.C1MeetGuide),
                "An explicitly unbound source must not retain quest authority.");

            proof.BindWorldInteractionDirector(first);
            proof.BindWorldInteractionDirector(second);
            Assert.That(first.TryConfirmFocused(), Is.True);
            Assert.That(
                proof.State.Phase,
                Is.EqualTo(ProofOfWorthPhase.C1MeetGuide),
                "Rebinding must remove the previous source subscription.");

            Assert.That(second.TryConfirmFocused(), Is.True);
            Assert.That(
                proof.State.Phase,
                Is.EqualTo(ProofOfWorthPhase.C1RestoreCovenant),
                "The currently bound source must deliver its accepted receipt once.");
        }

        private ProofOfWorthDirector CreateAtGuide(Transform player = null)
        {
            ProofOfWorthDirector proof = CreateProof(player);
            AdvanceToGuide(proof);
            return proof;
        }

        private ProofOfWorthDirector CreateProof(Transform player = null)
        {
            ProofOfWorthDirector proof =
                _root.AddComponent<ProofOfWorthDirector>();
            proof.EnsureReady(
                null,
                player != null ? player : _root.transform,
                RealmId.Crownlands);
            return proof;
        }

        private static void AdvanceToGuide(ProofOfWorthDirector proof)
        {
            ProofOfWorthCommand[] commands =
            {
                ProofOfWorthCommand.AcceptOffer,
                ProofOfWorthCommand.Investigate,
                ProofOfWorthCommand.DeployChampion,
                ProofOfWorthCommand.ArenaSuccess,
                ProofOfWorthCommand.SelectValerius,
                ProofOfWorthCommand.PresentTear,
                ProofOfWorthCommand.ConcludeReport
            };

            for (int index = 0; index < commands.Length; index++)
            {
                Assert.That(
                    proof.ApplyForTests(commands[index]).Changed,
                    Is.True,
                    commands[index].ToString());
            }

            Assert.That(
                proof.State.Phase,
                Is.EqualTo(ProofOfWorthPhase.C1MeetGuide));
        }

        private WorldInteractionDirector CreateEventSource(
            string name,
            Transform actor,
            UnityEngine.Camera camera)
        {
            var sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(_root.transform, false);
            WorldInteractionDirector source =
                sourceObject.AddComponent<WorldInteractionDirector>();
            source.Configure(actor, camera, null);

            var targetObject = new GameObject(name + "Guide");
            targetObject.transform.SetParent(_root.transform, false);
            targetObject.transform.position = new Vector3(0f, 1.15f, 2f);
            WorldInteractable target =
                targetObject.AddComponent<WorldInteractable>();
            target.Configure(
                FirstSessionWorldInteractables.GuideCatalogId,
                WorldInteractionKind.Talk,
                string.Empty,
                string.Empty);
            source.Register(target);
            return source;
        }

        private static WorldInteractionResult Receipt(
            bool accepted,
            string catalogId,
            WorldInteractionKind kind)
        {
            return new WorldInteractionResult(
                accepted,
                catalogId,
                kind,
                string.Empty);
        }

        private static void AssertReceiptRejectedWithoutStateChange(
            ProofOfWorthDirector proof,
            WorldInteractionResult receipt,
            ProofOfWorthPhase expectedPhase)
        {
            ProofOfWorthState before = proof.State;
            Assert.That(proof.TryApplyWorldInteraction(receipt), Is.False);
            Assert.That(proof.State, Is.SameAs(before));
            Assert.That(proof.State.Phase, Is.EqualTo(expectedPhase));
        }
    }
}
