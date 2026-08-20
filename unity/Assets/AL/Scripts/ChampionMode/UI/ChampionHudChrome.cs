using AL.UI.Presentation;
using AL.UI.SharedMenu;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AL.ChampionMode.UI
{
    /// <summary>
    /// Applies PresentationChrome to the existing ChampionArena HUD pieces
    /// and mounts Shared Menu / quest-slot chrome. Does not rebuild the 3D world.
    /// </summary>
    public static class ChampionHudChrome
    {
        public static readonly string[] CombatOnlyRoots =
        {
            "CombatGoals",
            "BossFrame",
            "CombatPressureFrame"
        };

        public static void ApplyScalerAndFont(CanvasScaler scaler, Transform root)
        {
            PresentationChrome.ApplyCanvasScaler(scaler);
            PresentationChrome.BindFonts(root, PresentationChrome.ResolveFont());
        }

        public static void RestyleExistingPlates(Transform hudRoot)
        {
            if (hudRoot == null)
            {
                return;
            }

            Image[] images = hudRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null)
                {
                    continue;
                }

                string name = image.gameObject.name;
                if (name == "PlayerFrame" ||
                    name == "CombatHotbar" ||
                    name == "CombatActions" ||
                    name == "NavigationPad" ||
                    name == "CombatFeed" ||
                    name == "CombatGoals" ||
                    name == "BossFrame" ||
                    name == "DefeatRetryPanel" ||
                    name == "EncounterClearPanel" ||
                    name == "EncounterIntroPanel")
                {
                    image.color = PresentationChrome.StonePlate;
                    EnsureMetalCap(image.transform);
                }
            }
        }

        public static Button MountSharedMenuButton(Transform hudRoot, UnityAction onOpen)
        {
            Transform existing = hudRoot.Find(ChampionHudCopy.SharedMenuButtonName);
            if (existing != null)
            {
                Button reuse = existing.GetComponent<Button>();
                if (reuse != null && onOpen != null)
                {
                    reuse.onClick.RemoveAllListeners();
                    reuse.onClick.AddListener(onOpen);
                }

                return reuse;
            }

            Button button = PresentationChrome.CreateHit(
                hudRoot,
                ChampionHudCopy.SharedMenuButtonName,
                PresentationChrome.StonePlate,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-PresentationChrome.SpaceMd, -PresentationChrome.SpaceMd),
                new Vector2(148f, PresentationChrome.MinHit));
            if (onOpen != null)
            {
                button.onClick.AddListener(onOpen);
            }

            PresentationChrome.CreateLabel(
                button.transform,
                "Label",
                PresentationChrome.ResolveFont(),
                ChampionHudCopy.SharedMenuButtonLabel,
                PresentationChrome.ActionSize,
                PresentationChrome.Ink,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            EnsureMetalCap(button.transform);
            return button;
        }

        public static RectTransform MountQuestSlot(Transform hudRoot)
        {
            Transform existing = hudRoot.Find(ChampionHudCopy.QuestSlotName);
            if (existing != null)
            {
                return existing as RectTransform;
            }

            Image slot = PresentationChrome.CreatePlate(
                hudRoot,
                ChampionHudCopy.QuestSlotName,
                new Color(
                    PresentationChrome.StoneInset.r,
                    PresentationChrome.StoneInset.g,
                    PresentationChrome.StoneInset.b,
                    0f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-PresentationChrome.SpaceMd, -92f),
                new Vector2(360f, 120f));
            slot.raycastTarget = false;
            return slot.rectTransform;
        }

        public static Button AttachRecapSharedMenu(Transform recapRoot, UnityAction onOpen)
        {
            if (recapRoot == null)
            {
                return null;
            }

            Transform debugKingdom = recapRoot.Find(FirstSessionChampionStart.DebugKingdomButtonName);
            if (debugKingdom != null && !FirstSessionChampionStart.AllowDebugKingdomLoad)
            {
                debugKingdom.gameObject.SetActive(false);
            }

            Transform existing = recapRoot.Find(ChampionHudCopy.RecapSharedMenuButtonName);
            if (existing != null)
            {
                Button reuse = existing.GetComponent<Button>();
                if (reuse != null && onOpen != null)
                {
                    reuse.onClick.RemoveAllListeners();
                    reuse.onClick.AddListener(onOpen);
                }

                return reuse;
            }

            Button button = PresentationChrome.CreateHit(
                recapRoot,
                ChampionHudCopy.RecapSharedMenuButtonName,
                PresentationChrome.StoneInset,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(450f, -232f),
                new Vector2(160f, PresentationChrome.MinHit));
            if (onOpen != null)
            {
                button.onClick.AddListener(onOpen);
            }

            PresentationChrome.CreateLabel(
                button.transform,
                "Label",
                PresentationChrome.ResolveFont(),
                ChampionHudCopy.SharedMenuButtonLabel,
                PresentationChrome.ActionSize,
                PresentationChrome.Ink,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            return button;
        }

        public static void SetExplorationChrome(Transform hudRoot, bool exploration)
        {
            if (hudRoot == null)
            {
                return;
            }

            for (int i = 0; i < CombatOnlyRoots.Length; i++)
            {
                Transform child = hudRoot.Find(CombatOnlyRoots[i]);
                if (child != null)
                {
                    child.gameObject.SetActive(!exploration);
                }
            }
        }

        public static bool UsesPresentationFont(Transform root)
        {
            if (root == null)
            {
                return false;
            }

            Text[] labels = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null &&
                    labels[i].font != null &&
                    labels[i].font.name.IndexOf("LegacyRuntime", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            return labels.Length > 0;
        }

        private static void EnsureMetalCap(Transform parent)
        {
            if (parent.Find("ChromeCap") != null)
            {
                return;
            }

            PresentationChrome.CreatePlate(
                parent,
                "ChromeCap",
                PresentationChrome.MetalEdge,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 2f));
        }
    }
}
