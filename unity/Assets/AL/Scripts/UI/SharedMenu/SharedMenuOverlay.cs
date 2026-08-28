using System;
using AL.UI.Presentation;
using AL.UI.WorldMap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.UI.SharedMenu
{
    /// <summary>
    /// Authored Shared Menu chrome. Kingdom Management stays visible when locked.
    /// PresentationChrome tokens match realm-select and character-create.
    /// </summary>
    public sealed class SharedMenuOverlay : MonoBehaviour
    {
        public SharedMenuModuleState State { get; private set; }
        public Button WorldMapButton { get; private set; }
        public Button KingdomButton { get; private set; }
        public Button ResumeButton { get; private set; }
        public Text HeaderLabel { get; private set; }
        public Text TitleLabel { get; private set; }
        public Text DetailLabel { get; private set; }
        private object _owner;
        private Action _onDisplaced;

        public static SharedMenuOverlay Ensure(SharedMenuModuleState state) =>
            Ensure(SceneManager.GetActiveScene(), state, null, null);

        public static SharedMenuOverlay Ensure(
            Scene ownerScene,
            SharedMenuModuleState state,
            object owner,
            Action onDisplaced)
        {
            if (!ownerScene.IsValid() || !ownerScene.isLoaded)
            {
                throw new InvalidOperationException(
                    "Shared Menu requires an exact loaded owner scene.");
            }

            SharedMenuOverlay existing = null;
            SharedMenuOverlay[] overlays =
                Resources.FindObjectsOfTypeAll<SharedMenuOverlay>();
            for (int i = 0; i < overlays.Length; i++)
            {
                SharedMenuOverlay candidate = overlays[i];
                if (candidate != null && candidate.gameObject.scene == ownerScene)
                {
                    if (existing == null || Prefer(candidate, existing))
                    {
                        existing = candidate;
                    }
                }
            }

            for (int i = 0; i < overlays.Length; i++)
            {
                SharedMenuOverlay duplicate = overlays[i];
                if (duplicate != null && duplicate != existing &&
                    duplicate.gameObject.scene == ownerScene)
                {
                    duplicate.RetireDuplicate();
                }
            }

            if (existing != null)
            {
                existing.Claim(owner, onDisplaced);
                existing.Build(state);
                existing.gameObject.SetActive(true);
                return existing;
            }

            var root = new GameObject(SharedMenuIds.OverlayRootName);
            SceneManager.MoveGameObjectToScene(root, ownerScene);
            SharedMenuOverlay overlay = root.AddComponent<SharedMenuOverlay>();
            overlay.Claim(owner, onDisplaced);
            overlay.Build(state);
            return overlay;
        }

        public bool IsOwnedBy(object owner) =>
            owner != null && ReferenceEquals(_owner, owner);

        public void Release(object owner, bool hide)
        {
            if (!IsOwnedBy(owner))
            {
                return;
            }

            _owner = null;
            _onDisplaced = null;
            if (hide)
            {
                gameObject.SetActive(false);
            }
        }

        public void Build(SharedMenuModuleState state)
        {
            State = state;
            EnsureCanvas();
            ClearChildren();

            Font font = PresentationChrome.ResolveFont();
            Image veil = PresentationChrome.CreatePlate(
                transform,
                "SharedMenuVeil",
                PresentationChrome.Veil,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                raycastTarget: true);

            Image plate = PresentationChrome.CreatePlate(
                transform,
                "SharedMenuPlate",
                PresentationChrome.StonePlate,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(720f, 500f));
            AddMetalEdge(plate.transform, new Vector2(720f, 500f));

            HeaderLabel = PresentationChrome.CreateLabel(
                plate.transform,
                "SharedMenuHeader",
                font,
                SharedMenuCopy.MenuHeader,
                PresentationChrome.TitleSize,
                PresentationChrome.Ink,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(PresentationChrome.SpaceMd, -PresentationChrome.SpaceMd),
                new Vector2(520f, 36f));

            PresentationChrome.CreateLabel(
                plate.transform,
                "SharedMenuCaption",
                font,
                SharedMenuCopy.MenuCaption,
                PresentationChrome.CaptionSize,
                PresentationChrome.InkMuted,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(PresentationChrome.SpaceMd, -64f),
                new Vector2(672f, 28f));

            WorldMapButton = PresentationChrome.CreateHit(
                plate.transform,
                WorldMapIds.MenuModuleWorldMap,
                PresentationChrome.StoneInset,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(PresentationChrome.SpaceMd, -108f),
                new Vector2(672f, 72f));
            AddMetalEdge(WorldMapButton.transform, new Vector2(672f, 72f));
            PresentationChrome.CreateLabel(
                WorldMapButton.transform,
                "WorldMapLabel",
                font,
                WorldMapIds.SharedMenuWorldMapLabel,
                PresentationChrome.ActionSize,
                PresentationChrome.Ink,
                TextAnchor.MiddleLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                new Vector2(PresentationChrome.SpaceSm, 0f),
                new Vector2(-PresentationChrome.SpaceSm * 2f, 0f));

            Button kingdom = PresentationChrome.CreateHit(
                plate.transform,
                SharedMenuIds.KingdomButtonName,
                PresentationChrome.StoneInset,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(PresentationChrome.SpaceMd, -196f),
                new Vector2(672f, 132f));
            KingdomButton = kingdom;
            KingdomButton.interactable = state.CanInvoke && state.Visible;
            AddMetalEdge(kingdom.transform, new Vector2(672f, 132f));

            TitleLabel = PresentationChrome.CreateLabel(
                kingdom.transform,
                "Title",
                font,
                state.Title,
                PresentationChrome.ActionSize,
                PresentationChrome.Ink,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(PresentationChrome.SpaceSm, -PresentationChrome.SpaceSm),
                new Vector2(640f, 28f));

            DetailLabel = PresentationChrome.CreateLabel(
                kingdom.transform,
                "Detail",
                font,
                state.Detail,
                PresentationChrome.BodySize,
                state.CanInvoke ? PresentationChrome.InkMuted : PresentationChrome.InkFaint,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(PresentationChrome.SpaceSm, -48f),
                new Vector2(640f, 84f));

            ResumeButton = PresentationChrome.CreateHit(
                plate.transform,
                SharedMenuCopy.ResumeButtonName,
                PresentationChrome.StoneInset,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, PresentationChrome.SpaceMd),
                new Vector2(240f, PresentationChrome.MinHit));
            AddMetalEdge(ResumeButton.transform, new Vector2(240f, PresentationChrome.MinHit));
            PresentationChrome.CreateLabel(
                ResumeButton.transform,
                "ResumeLabel",
                font,
                SharedMenuCopy.Resume,
                PresentationChrome.ActionSize,
                PresentationChrome.Ink,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            veil.gameObject.SetActive(true);
        }

        public void Close()
        {
            _owner = null;
            _onDisplaced = null;
            gameObject.SetActive(false);
        }

        private void Claim(object owner, Action onDisplaced)
        {
            if (owner == null)
            {
                return;
            }
            if (ReferenceEquals(_owner, owner))
            {
                _onDisplaced = onDisplaced;
                return;
            }

            Action displaced = _onDisplaced;
            _owner = null;
            _onDisplaced = null;
            displaced?.Invoke();
            _owner = owner;
            _onDisplaced = onDisplaced;
        }

        private static bool Prefer(
            SharedMenuOverlay candidate,
            SharedMenuOverlay current)
        {
            bool candidateOwned = candidate._owner != null;
            bool currentOwned = current._owner != null;
            if (candidateOwned != currentOwned)
            {
                return candidateOwned;
            }

            bool candidateActive = candidate.gameObject.activeSelf;
            bool currentActive = current.gameObject.activeSelf;
            return candidateActive != currentActive
                ? candidateActive
                : candidate.GetInstanceID() < current.GetInstanceID();
        }

        private void RetireDuplicate()
        {
            NotifyUnexpectedSurfaceLoss();
            gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void OnDisable()
        {
            NotifyUnexpectedSurfaceLoss();
        }

        private void OnDestroy()
        {
            NotifyUnexpectedSurfaceLoss();
        }

        private void NotifyUnexpectedSurfaceLoss()
        {
            Action displaced = _onDisplaced;
            _owner = null;
            _onDisplaced = null;
            displaced?.Invoke();
        }

        private void EnsureCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 80;
                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                PresentationChrome.ApplyCanvasScaler(scaler);
                gameObject.AddComponent<GraphicRaycaster>();
            }
            else
            {
                PresentationChrome.ApplyCanvasScaler(GetComponent<CanvasScaler>());
            }
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        private static void AddMetalEdge(Transform parent, Vector2 size)
        {
            PresentationChrome.CreatePlate(
                parent,
                "MetalEdge",
                PresentationChrome.MetalEdge,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 2f));
            PresentationChrome.CreatePlate(
                parent,
                "MetalEdgeLeft",
                PresentationChrome.MetalDim,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(2f, 0f));
        }

        public void BindInvoke(UnityEngine.Events.UnityAction action)
        {
            if (KingdomButton == null)
            {
                return;
            }

            KingdomButton.onClick.RemoveAllListeners();
            if (action != null && State.CanInvoke)
            {
                KingdomButton.onClick.AddListener(action);
            }
        }

        public void BindWorldMap(UnityEngine.Events.UnityAction action)
        {
            if (WorldMapButton == null)
            {
                return;
            }

            WorldMapButton.onClick.RemoveAllListeners();
            if (action != null)
            {
                WorldMapButton.onClick.AddListener(action);
            }
        }
    }
}
