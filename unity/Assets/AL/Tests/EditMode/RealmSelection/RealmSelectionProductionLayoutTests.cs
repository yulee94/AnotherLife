using System.Collections.Generic;
using AL.Core;
using AL.Data.Definitions;
using AL.UI.Presentation;
using AL.UI.RealmSelection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class RealmSelectionProductionLayoutTests
    {
        private readonly List<Object> _owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null)
                {
                    Object.DestroyImmediate(_owned[i]);
                }
            }

            _owned.Clear();
            GameObject leftover = GameObject.Find("RealmSelectionCommitCanvas");
            if (leftover != null)
            {
                Object.DestroyImmediate(leftover);
            }
        }

        [Test]
        public void ProductionLayoutAuthorsFourHeraldicPlatesNotADebugGrid()
        {
            var realms = new[]
            {
                Definition(RealmId.Crownlands),
                Definition(RealmId.Stonehold),
                Definition(RealmId.Eldergrove),
                Definition(RealmId.Umbral)
            };

            RealmSelectionProductionScreen screen = RealmSelectionProductionLayout.Build(
                realms,
                _ => null,
                _ => { },
                PresentationChrome.ResolveFont());
            _owned.Add(screen.CanvasObject);

            Assert.That(screen.CanvasObject.name, Is.EqualTo(RealmSelectionProductionLayout.CanvasName));
            Assert.That(screen.RealmButtons, Has.Count.EqualTo(4));
            Assert.That(screen.Commit, Is.Not.Null);
            Assert.That(screen.Commit.IsVisible, Is.False);

            var frames = new HashSet<string>();
            var materialNouns = new HashSet<string>();
            foreach (Button button in screen.RealmButtons)
            {
                Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(0f));
                Assert.That(button.GetComponentInChildren<Text>(), Is.Not.Null);
                Transform frame = button.transform.Find("Frame_Outer");
                Assert.That(frame, Is.Not.Null, button.name);
                frames.Add(CollectFrameSignature(button.transform));
                RealmSelectionFormationEffect formation =
                    button.GetComponent<RealmSelectionFormationEffect>();
                Assert.That(formation, Is.Not.Null, button.name);
                Assert.That(button.transform.Find(RealmSelectionFormationEffect.RootName), Is.Not.Null);
                Assert.That(
                    button.transform.Find(
                        RealmSelectionFormationEffect.RootName + "/" +
                        RealmSelectionFormationEffect.FlowRootName).childCount,
                    Is.EqualTo(RealmSelectionFormationEffect.ParticleCount));
                materialNouns.Add(formation.MaterialNoun);
            }

            Assert.That(frames, Has.Count.EqualTo(4));
            Assert.That(materialNouns, Has.Count.EqualTo(4));
            Assert.That(screen.Grid, Is.Not.Null);
        }

        [Test]
        public void CommitOverlayRequiresExplicitBindAndBlocksAccidentalConfirm()
        {
            bool confirmed = false;
            bool withdrawn = false;
            var host = new GameObject("CommitHost");
            _owned.Add(host);
            RealmSelectionCommitOverlay overlay = RealmSelectionCommitOverlay.Create(
                host.transform,
                PresentationChrome.ResolveFont());
            overlay.Bind(() => confirmed = true, () => withdrawn = true);

            Assert.That(overlay.IsVisible, Is.False);
            Assert.That(overlay.TryConfirm(), Is.False);
            Assert.That(confirmed, Is.False);

            var identity = new RealmIdentityPresentation(
                RealmId.Umbral,
                "umbral",
                "Umbral",
                "Umbral Dark Elves",
                "Severed Eclipse",
                "offset eclipse and diagonal void",
                "obsidian, smoked metal",
                RealmStructuralFrameKind.SeveredEclipse);
            overlay.Present(identity, null);
            Assert.That(overlay.IsVisible, Is.True);
            Assert.That(overlay.PendingRealmId, Is.EqualTo(RealmId.Umbral));
            Assert.That(overlay.transform.Find("CommitVeil").GetComponent<Image>().raycastTarget, Is.True);

            Transform confirm = overlay.transform.Find("CommitPlate/" + RealmSelectionCommitOverlay.ConfirmButtonName);
            Transform withdraw = overlay.transform.Find("CommitPlate/" + RealmSelectionCommitOverlay.WithdrawButtonName);
            Assert.That(confirm, Is.Not.Null);
            Assert.That(withdraw, Is.Not.Null);
            Assert.That(confirm.GetComponent<RectTransform>().sizeDelta.y, Is.GreaterThanOrEqualTo(PresentationChrome.MinHit));
            Assert.That(withdraw.GetComponent<RectTransform>().sizeDelta.y, Is.GreaterThanOrEqualTo(PresentationChrome.MinHit));

            overlay.Withdraw();
            Assert.That(withdrawn, Is.True);
            Assert.That(overlay.IsVisible, Is.False);
            Assert.That(confirmed, Is.False);

            overlay.Present(identity, null);
            Assert.That(overlay.TryConfirm(), Is.True);
            Assert.That(confirmed, Is.True);
        }

        [Test]
        public void PresentingACandidateDoesNotPersistUntilConfirm()
        {
            var controllerHost = new GameObject("RealmSelectionController");
            _owned.Add(controllerHost);
            var controller = controllerHost.AddComponent<RealmSelectionController>();
            Assert.That(controller.IsCommitOverlayVisible, Is.False);
            controller.PresentCandidate(RealmId.Stonehold);
            Assert.That(controller.IsCommitOverlayVisible, Is.True);
            Assert.That(controller.PendingRealmId, Is.EqualTo(RealmId.Stonehold));
            controller.WithdrawPendingSelection();
            Assert.That(controller.IsCommitOverlayVisible, Is.False);
        }

        [Test]
        public void ProductionSceneWiresAuthoredScreenToCharacterCreation()
        {
            string scene = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "AL/Scenes/RealmSelection.unity"));
            string controller = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "AL/Scripts/UI/RealmSelection/RealmSelectionController.cs"));
            string screenPrefab = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "AL/Prefabs/UI/RealmSelection/AL_UI_RealmSelection_Screen.prefab"));

            Assert.That(scene, Does.Contain("_screenPrefab:"));
            Assert.That(scene, Does.Contain("_nextScene: CharacterCreation"));
            Assert.That(scene, Does.Not.Contain("_nextScene: Kingdom"));
            Assert.That(controller, Does.Contain("RealmSelectionProductionLayout"));
            Assert.That(controller, Does.Contain("ConfirmPendingSelection"));
            Assert.That(controller, Does.Not.Contain("LegacyRuntime"));
            Assert.That(screenPrefab, Does.Not.Contain("m_Script: {fileID: 0}"));
            Assert.That(screenPrefab, Does.Contain("m_CellSize: {x: 790, y: 280}"));
            Assert.That(screenPrefab, Does.Contain("m_ConstraintCount: 2"));
        }

        private RealmDefinition Definition(RealmId id)
        {
            var definition = ScriptableObject.CreateInstance<RealmDefinition>();
            definition.Id = id;
            definition.RealmName = id.ToString();
            _owned.Add(definition);
            return definition;
        }

        private static string CollectFrameSignature(Transform root)
        {
            var names = new List<string>();
            Collect(root, names);
            names.Sort(System.StringComparer.Ordinal);
            return string.Join("|", names);
        }

        private static void Collect(Transform root, List<string> names)
        {
            if (root.name.StartsWith("Frame_"))
            {
                names.Add(root.name);
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Collect(root.GetChild(i), names);
            }
        }
    }
}
