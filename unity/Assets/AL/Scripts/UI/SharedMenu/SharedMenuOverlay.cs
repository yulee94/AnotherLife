using AL.UI.Presentation;
using UnityEngine;
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
        public Button KingdomButton { get; private set; }
        public Button ResumeButton { get; private set; }
        public Text HeaderLabel { get; private set; }
        public Text TitleLabel { get; private set; }
        public Text DetailLabel { get; private set; }

        public static SharedMenuOverlay Ensure(SharedMenuModuleState state)
        {
            SharedMenuOverlay existing = FindObjectOfType<SharedMenuOverlay>();
            if (existing != null)
            {
                existing.Build(state);
                existing.gameObject.SetActive(true);
                return existing;
            }

            var root = new GameObject(SharedMenuIds.OverlayRootName);
            SharedMenuOverlay overlay = root.AddComponent<SharedMenuOverlay>();
            overlay.Build(state);
            return overlay;
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
                new Vector2(720f, 420f));
            AddMetalEdge(plate.transform, new Vector2(720f, 420f));

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

            Button kingdom = PresentationChrome.CreateHit(
                plate.transform,
                SharedMenuIds.KingdomButtonName,
                PresentationChrome.StoneInset,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(PresentationChrome.SpaceMd, -108f),
                new Vector2(672f, 148f));
            KingdomButton = kingdom;
            KingdomButton.interactable = state.CanInvoke && state.Visible;
            AddMetalEdge(kingdom.transform, new Vector2(672f, 148f));

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
            gameObject.SetActive(false);
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
    }
}
