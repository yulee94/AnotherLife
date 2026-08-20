using AL.ChampionMode.Tutorial;
using AL.Input;
using UnityEngine;
using UnityEngine.UI;

namespace AL.ChampionMode
{
    /// <summary>
    /// First-session onboarding overlay. Observes GameInput; does not patch ChampionController.
    /// </summary>
    public sealed class FirstWorldEntryTutorialDirector : MonoBehaviour
    {
        public const string OverlayRootName = "TUTORIAL_FIRST_WORLD_ENTRY_Overlay_TEMPORARY";
        public const string PromptName = "TutorialPrompt_TEMPORARY";
        public const string OfferPlateName = "OmenOfferPlate_TEMPORARY";

        private static FirstWorldEntryTutorialDirector _instance;
        private FirstWorldEntryTutorialState _state;
        private float _lookAccumulated;
        private Text _promptText;
        private Text _titleText;
        private GameObject _offerPlate;
        private Text _offerBody;
        private string _lastFollowResult = string.Empty;
        private int _acceptAttempts;
        private bool _ready;

        public FirstWorldEntryTutorialState State => _state;
        public string LastFollowResult => _lastFollowResult;
        public int AcceptAttempts => _acceptAttempts;
        public bool OpenedKingdom => false;

        public static FirstWorldEntryTutorialDirector AttachIfNeeded(Transform parent)
        {
            if (!FirstSessionChampionStart.ShouldRunFirstWorldEntryTutorial)
            {
                return null;
            }

            if (_instance != null)
            {
                _instance.EnsureReady();
                return _instance;
            }

            var host = new GameObject(OverlayRootName);
            if (parent != null)
            {
                host.transform.SetParent(parent, false);
            }

            FirstWorldEntryTutorialDirector director =
                host.AddComponent<FirstWorldEntryTutorialDirector>();
            director.EnsureReady();
            return director;
        }

        public void ApplyLookForTests(float magnitude)
        {
            ConsiderLook(magnitude);
        }

        public void ApplyMoveForTests(float magnitude, bool sprintHeld)
        {
            ConsiderMove(magnitude, sprintHeld);
        }

        public void ApplyInteractForTests()
        {
            ConsiderInteract();
        }

        public void ApplyAttackForTests()
        {
            ConsiderAttack();
        }

        public string FollowActiveObjective(bool targetAvailable)
        {
            _lastFollowResult = FirstWorldEntryTutorialPlanner.Follow(_state, targetAvailable);
            return _lastFollowResult;
        }

        public void AttemptAcceptOmen()
        {
            _acceptAttempts++;
            _state = FirstWorldEntryTutorialPlanner.RejectAccept(_state);
            RefreshPresentation();
        }

        private void Awake()
        {
            EnsureReady();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public static void ResetForTests()
        {
            _instance = null;
        }

        public void EnsureReady()
        {
            _instance = this;
            if (_ready)
            {
                return;
            }

            _state = FirstWorldEntryTutorialPlanner.CreateInitial();
            BuildOverlay();
            RefreshPresentation();
            _ready = true;
        }

        private void Update()
        {
            if (_state == null || _state.IsComplete)
            {
                return;
            }

            Vector2 look = GameInput.ReadLook();
            ConsiderLook(look.magnitude);
            Vector2 move = GameInput.ReadMove();
            ConsiderMove(move.magnitude, GameInput.BlockHeld());
            if (GameInput.SubmitPressed())
            {
                ConsiderInteract();
            }

            if (GameInput.AttackPressed())
            {
                ConsiderAttack();
            }
        }

        private void ConsiderLook(float magnitude)
        {
            if (_state.TeachingBeat != FirstWorldEntryTeachingBeat.CameraLook)
            {
                return;
            }

            _lookAccumulated += Mathf.Max(0f, magnitude);
            if (!FirstWorldEntryTutorialEvidence.IsLookAccepted(_lookAccumulated))
            {
                return;
            }

            _state = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                _state,
                FirstWorldEntryTeachingBeat.Move,
                sprintTaught: false);
            RefreshPresentation();
        }

        private void ConsiderMove(float magnitude, bool sprintHeld)
        {
            if (_state.TeachingBeat != FirstWorldEntryTeachingBeat.Move)
            {
                return;
            }

            if (sprintHeld && FirstWorldEntryTutorialEvidence.IsMoveAccepted(magnitude))
            {
                _state = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                    _state,
                    FirstWorldEntryTeachingBeat.Move,
                    sprintTaught: true);
            }

            if (!FirstWorldEntryTutorialEvidence.IsMoveAccepted(magnitude))
            {
                return;
            }

            FirstWorldEntryTutorialTransition transition = FirstWorldEntryTutorialPlanner.Apply(
                _state,
                FirstWorldEntryEvidenceKind.MovementConfirmed);
            if (transition.Changed)
            {
                _state = transition.State;
                RefreshPresentation();
            }
        }

        private void ConsiderInteract()
        {
            if (_state.TeachingBeat != FirstWorldEntryTeachingBeat.Interact)
            {
                return;
            }

            _state = FirstWorldEntryTutorialPlanner.AdvanceTeaching(
                _state,
                FirstWorldEntryTeachingBeat.BasicAttack,
                sprintTaught: false);
            RefreshPresentation();
        }

        private void ConsiderAttack()
        {
            if (_state.TeachingBeat != FirstWorldEntryTeachingBeat.BasicAttack)
            {
                return;
            }

            FirstWorldEntryTutorialTransition transition = FirstWorldEntryTutorialPlanner.Apply(
                _state,
                FirstWorldEntryEvidenceKind.BasicAttackConfirmed);
            if (transition.Changed)
            {
                _state = transition.State;
                RefreshPresentation();
            }
        }

        private void BuildOverlay()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("TutorialCanvas_TEMPORARY");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            Image plate = CreatePlate(
                canvasObject.transform,
                PromptName,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 118f),
                new Vector2(760f, 92f),
                new Color(0.012f, 0.016f, 0.022f, 0.88f));
            CreateAccent(plate.transform, new Color(0.86f, 0.68f, 0.32f, 0.92f));
            _titleText = CreateLabel(
                plate.transform,
                font,
                "TutorialTitle",
                FirstWorldEntryTutorialCopy.Title,
                15,
                new Vector2(22f, -10f),
                new Vector2(716f, 22f),
                new Color(0.92f, 0.78f, 0.48f));
            _promptText = CreateLabel(
                plate.transform,
                font,
                "TutorialBody",
                FirstWorldEntryTutorialCopy.CameraPrompt,
                17,
                new Vector2(22f, -34f),
                new Vector2(716f, 48f),
                new Color(0.88f, 0.90f, 0.93f));

            _offerPlate = CreatePlate(
                canvasObject.transform,
                OfferPlateName,
                new Vector2(0.5f, 0.58f),
                Vector2.zero,
                new Vector2(640f, 248f),
                new Color(0.014f, 0.018f, 0.026f, 0.94f)).gameObject;
            CreateAccent(_offerPlate.transform, new Color(0.78f, 0.62f, 0.28f, 0.95f));
            CreateLabel(
                _offerPlate.transform,
                font,
                "OmenTitle",
                FirstWorldEntryTutorialCopy.OmenOfferTitle,
                22,
                new Vector2(24f, -16f),
                new Vector2(592f, 32f),
                new Color(0.96f, 0.84f, 0.52f));
            CreateLabel(
                _offerPlate.transform,
                font,
                "OmenSpeaker",
                FirstWorldEntryTutorialCopy.OmenSpeaker,
                14,
                new Vector2(24f, -50f),
                new Vector2(592f, 20f),
                new Color(0.72f, 0.78f, 0.86f));
            _offerBody = CreateLabel(
                _offerPlate.transform,
                font,
                "OmenBody",
                FirstWorldEntryTutorialCopy.OmenOffer,
                16,
                new Vector2(24f, -76f),
                new Vector2(592f, 88f),
                new Color(0.86f, 0.88f, 0.90f));
            CreateLabel(
                _offerPlate.transform,
                font,
                "OmenHint",
                FirstWorldEntryTutorialCopy.OmenOfferedHint,
                13,
                new Vector2(24f, -170f),
                new Vector2(592f, 36f),
                new Color(0.78f, 0.70f, 0.48f));
            CreateLabel(
                _offerPlate.transform,
                font,
                "OmenTalk",
                FirstWorldEntryTutorialCopy.OmenTalk,
                14,
                new Vector2(24f, -210f),
                new Vector2(592f, 22f),
                new Color(0.70f, 0.82f, 0.92f));
            _offerPlate.SetActive(false);
        }

        private void RefreshPresentation()
        {
            if (_promptText != null)
            {
                _promptText.text = FirstWorldEntryTutorialCopy.ForBeat(_state.TeachingBeat);
            }

            if (_titleText != null)
            {
                _titleText.text = _state.IsOmenOffered
                    ? FirstWorldEntryTutorialCopy.OmenOfferTitle
                    : FirstWorldEntryTutorialCopy.Title;
            }

            if (_offerPlate != null)
            {
                _offerPlate.SetActive(_state.IsOmenOffered);
            }

            if (_offerBody != null)
            {
                _offerBody.text = FirstWorldEntryTutorialCopy.OmenOffer;
            }
        }

        private static Image CreatePlate(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return image;
        }

        private static void CreateAccent(Transform parent, Color color)
        {
            var go = new GameObject("GoldEdge_TEMPORARY");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 4f);
        }

        private static Text CreateLabel(
            Transform parent,
            Font font,
            string name,
            string copy,
            int size,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = copy;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return text;
        }
    }
}
