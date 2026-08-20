using UnityEngine;
using UnityEngine.UI;

namespace AL.UI.SharedMenu
{
    /// <summary>
    /// Visible Shared Menu row for Kingdom Management. LockedNarrative shows real copy;
    /// the button is never omitted.
    /// </summary>
    public sealed class SharedMenuOverlay : MonoBehaviour
    {
        public SharedMenuModuleState State { get; private set; }
        public Button KingdomButton { get; private set; }
        public Text TitleLabel { get; private set; }
        public Text DetailLabel { get; private set; }

        public static SharedMenuOverlay Ensure(SharedMenuModuleState state)
        {
            var root = new GameObject(SharedMenuIds.OverlayRootName);
            SharedMenuOverlay overlay = root.AddComponent<SharedMenuOverlay>();
            overlay.Build(state);
            return overlay;
        }

        public void Build(SharedMenuModuleState state)
        {
            State = state;
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            Transform existing = transform.Find(SharedMenuIds.KingdomButtonName);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            var buttonGo = new GameObject(SharedMenuIds.KingdomButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(transform, false);
            KingdomButton = buttonGo.GetComponent<Button>();
            KingdomButton.interactable = state.CanInvoke;

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(buttonGo.transform, false);
            TitleLabel = titleGo.GetComponent<Text>();
            TitleLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            TitleLabel.text = state.Title;
            TitleLabel.fontSize = 18;
            TitleLabel.alignment = TextAnchor.MiddleLeft;

            var detailGo = new GameObject("Detail", typeof(RectTransform), typeof(Text));
            detailGo.transform.SetParent(buttonGo.transform, false);
            DetailLabel = detailGo.GetComponent<Text>();
            DetailLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            DetailLabel.text = state.Detail;
            DetailLabel.fontSize = 14;
            DetailLabel.alignment = TextAnchor.UpperLeft;
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
    }
}
