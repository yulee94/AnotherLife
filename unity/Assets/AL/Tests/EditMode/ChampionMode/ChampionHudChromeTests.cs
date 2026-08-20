using AL.ChampionMode;
using AL.ChampionMode.UI;
using AL.Data.Runtime;
using AL.UI.Presentation;
using AL.UI.SharedMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ChampionHudChromeTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            ChampionHudCameraGate.Reset();
            Time.timeScale = 1f;
            _root = new GameObject("ChampionMode_HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        }

        [TearDown]
        public void TearDown()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            ChampionHudCameraGate.Reset();
            Time.timeScale = 1f;
            SharedMenuOverlay[] leftovers = Object.FindObjectsOfType<SharedMenuOverlay>();
            for (int i = 0; i < leftovers.Length; i++)
            {
                Object.DestroyImmediate(leftovers[i].gameObject);
            }

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void FirstSessionHudMountsSharedMenuAndQuestSlotWithoutDebugKingdom()
        {
            CreateNamedPlate("PlayerFrame");
            CreateNamedPlate("CombatHotbar");
            CreateNamedPlate("BossFrame");
            CreateNamedPlate(FirstSessionChampionStart.LosePanelName);
            CreateNamedPlate(FirstSessionChampionStart.WinPanelName);

            ChampionHudSession session = ChampionHudSession.Attach(_root.transform);

            Assert.IsNotNull(_root.transform.Find(FirstSessionChampionStart.SharedMenuButtonName));
            Assert.IsNotNull(_root.transform.Find(FirstSessionChampionStart.QuestHudSlotName));
            Assert.IsNull(_root.transform.Find(FirstSessionChampionStart.DebugKingdomButtonName));
            Assert.IsFalse(
                _root.transform.Find("BossFrame").gameObject.activeSelf,
                "Exploration must not cover the world with a boss frame.");
            Assert.IsNotNull(
                FindDeep(_root.transform, FirstSessionChampionStart.LosePanelName)
                    .Find(ChampionHudCopy.RecapSharedMenuButtonName));
            Assert.IsNotNull(
                FindDeep(_root.transform, FirstSessionChampionStart.WinPanelName)
                    .Find(ChampionHudCopy.RecapSharedMenuButtonName));
            Assert.IsFalse(session.IsOpen);
        }

        [Test]
        public void SharedMenuOpensLockedNarrativeAndBlocksCameraLook()
        {
            ChampionHudSession session = ChampionHudSession.Attach(_root.transform);
            session.OpenMenu();

            Assert.IsTrue(session.IsOpen);
            Assert.IsTrue(ChampionHudCameraGate.ShouldIgnoreLook());
            Assert.IsFalse(session.Overlay.KingdomButton.interactable);
            Assert.AreEqual(SharedMenuCopy.Title, session.Overlay.TitleLabel.text);
            Assert.That(session.Overlay.DetailLabel.text, Does.Contain("Proof of Worth"));
            Assert.That(session.Overlay.HeaderLabel.text, Is.EqualTo(SharedMenuCopy.MenuHeader));
            Assert.That(session.Overlay.KingdomButton.name, Is.EqualTo(SharedMenuIds.KingdomButtonName));
            Assert.That(ChampionHudChrome.UsesPresentationFont(session.Overlay.transform), Is.True);

            session.CloseMenu();
            Assert.IsFalse(session.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void PresentationTokensMatchRealmSelectAndStayTouchable()
        {
            Assert.That(PresentationChrome.MinHit, Is.GreaterThanOrEqualTo(48f));
            Assert.That(PresentationChrome.TitleSize, Is.EqualTo(26));
            Assert.That(PresentationChrome.ActionSize, Is.EqualTo(16));
            Assert.That(PresentationChrome.StoneVoid.grayscale, Is.LessThan(0.12f));

            Button menu = ChampionHudChrome.MountSharedMenuButton(_root.transform, null);
            Assert.That(menu.GetComponent<RectTransform>().sizeDelta.y, Is.GreaterThanOrEqualTo(PresentationChrome.MinHit));
            Font font = PresentationChrome.ResolveFont();
            Assert.That(font.name, Does.Not.Contain("LegacyRuntime"));
        }

        [Test]
        public void RecapCopyNeverSendsThePlayerToADebugKingdomButton()
        {
            Assert.That(ChampionHudCopy.RecapNext, Does.Contain("Shared Menu"));
            Assert.That(ChampionHudCopy.RecapNext, Does.Not.Contain("Kingdom"));
            Assert.That(ChampionHudCopy.DefeatFeed, Does.Not.Contain("return to Kingdom"));
            Assert.That(ChampionHudCopy.ClearFeed, Does.Not.Contain("return to Kingdom"));
        }

        [Test]
        public void CameraGateBlocksLookWhileMenuOrRecapIsOpen()
        {
            Assert.IsFalse(ChampionHudCameraGate.BlocksLook);
            ChampionHudCameraGate.MenuOpen = true;
            Assert.IsTrue(ChampionHudCameraGate.ShouldIgnoreLook());
            ChampionHudCameraGate.MenuOpen = false;
            ChampionHudCameraGate.RecapOpen = true;
            Assert.IsTrue(ChampionHudCameraGate.ShouldIgnoreLook());
        }

        [Test]
        public void FreshSaveCannotSwitchThroughAnythingButSharedMenu()
        {
            var save = new SaveGameData();
            SharedMenuModuleState state = KingdomManagementUnlock.EvaluateKingdomManagement(save);
            Assert.AreEqual(SharedMenuAvailability.LockedNarrative, state.Availability);
            Assert.IsFalse(
                KingdomManagementUnlock.RequestSwitch(
                    new ModeSwitchRequest(
                        SharedMenuIds.Adventure3D,
                        SharedMenuIds.Kingdom2_5D,
                        save,
                        false,
                        false,
                        SharedMenuIds.InputBoot)).Succeeded);
        }

        private void CreateNamedPlate(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root.transform, false);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), name);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }
    }
}
