using System;
using AL.ChampionMode.Camera;
using AL.ChampionMode.UI;
using AL.Input;
using AL.UI.QuestHud;
using AL.UI.SharedMenu;
using AL.UI.WorldMap;
using UnityEngine;
using UnityEngine.UI;

namespace AL.ChampionMode.Quests
{
    /// <summary>
    /// In-world NPC conversation presentation. The speaking authored model stays
    /// visible while subtitles occupy a bottom bar instead of the gameplay center.
    /// </summary>
    public sealed class NpcConversationView : MonoBehaviour
    {
        public const string RootName = "NpcConversationView";
        public const float DefaultAutoAdvanceSeconds = 4.5f;

        private Action _onCompleted;
        private Transform _player;
        private Transform _speaker;
        private UnityEngine.Camera _camera;
        private CameraFollow _cameraFollow;
        private bool _cameraFollowWasEnabled;
        private IDisposable _cameraFollowSuspension;
        private bool _cameraStateCaptured;
        private IDisposable _cursorOwnership;
        private bool _completionDelivered;
        private Vector3 _cameraPosition;
        private Quaternion _cameraRotation;
        private float _cameraFieldOfView;
        private GameObject _panel;
        private Text _speakerLabel;

        public NpcConversationSession Session { get; private set; }
        public RectTransform PanelRect { get; private set; }
        public Text SubtitleLabel { get; private set; }
        public bool IsVisible => _panel != null && _panel.activeSelf;

        public static NpcConversationView Mount(Transform parent = null)
        {
            NpcConversationView existing = FindObjectOfType<NpcConversationView>();
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject(RootName, typeof(RectTransform));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            return root.AddComponent<NpcConversationView>();
        }

        public void Show(
            string dialogueId,
            string speakerName,
            string body,
            Transform player,
            Transform speaker,
            UnityEngine.Camera camera,
            Action onCompleted,
            float autoAdvanceSeconds = DefaultAutoAdvanceSeconds)
        {
            RestoreCameraAndInput();
            EnsureBuilt();

            Session = new NpcConversationSession(dialogueId, body, autoAdvanceSeconds);
            _player = player;
            _speaker = speaker;
            _camera = camera;
            _onCompleted = onCompleted;
            _completionDelivered = false;
            _cameraStateCaptured = false;
            _speakerLabel.text = string.IsNullOrWhiteSpace(speakerName)
                ? ProofOfWorthCopy.SpeakerName
                : speakerName;
            SubtitleLabel.text = body;

            OpenPresentation();
        }

        public void Collapse()
        {
            if (Session == null || Session.IsCompleted)
            {
                return;
            }

            Session.Collapse();
            if (_panel != null)
            {
                _panel.SetActive(false);
            }

            RestoreCameraAndInput();
        }

        public void Reopen()
        {
            if (Session == null || Session.IsCompleted || !Session.IsCollapsed)
            {
                return;
            }

            Session.Reopen();
            _cameraStateCaptured = false;
            OpenPresentation();
        }

        public void Retire()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }

            RestoreCameraAndInput();
            _onCompleted = null;
            _player = null;
            _speaker = null;
            _camera = null;
            Session = null;
            _completionDelivered = true;
        }

        public bool SkipCurrentLine()
        {
            if (Session == null || ExternalModalOpen || !Session.SkipCurrentLine())
            {
                return false;
            }

            CompleteCurrentLine();
            return true;
        }

        private void Update()
        {
            if (Session == null || Session.IsCompleted || Session.IsCollapsed)
            {
                return;
            }

            if (ExternalModalOpen)
            {
                return;
            }

            if (GameInput.CancelPressed())
            {
                Collapse();
                return;
            }

            if (Session.Advance(Time.unscaledDeltaTime))
            {
                CompleteCurrentLine();
            }
        }

        private static bool ExternalModalOpen =>
            WorldMapSession.IsMapOpen ||
            SharedMenuModeSwitchHost.HasOpenMenu ||
            ChampionHudCameraGate.MenuOpen ||
            ChampionHudCameraGate.RecapOpen;

        private void OnDestroy()
        {
            RestoreCameraAndInput();
        }

        private void OnDisable()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }

            RestoreCameraAndInput();
        }

        private void OnEnable()
        {
            if (Session == null || Session.IsCompleted || Session.IsCollapsed || _panel == null)
            {
                return;
            }

            _cameraStateCaptured = false;
            OpenPresentation();
        }

        private void OpenPresentation()
        {
            if (_panel != null)
            {
                _panel.SetActive(true);
            }

            CaptureCamera();
            FrameSpeaker();
            _cursorOwnership =
                ChampionHudCameraGate.AcquireCursorOwnership("npc-conversation");
        }

        private void CompleteCurrentLine()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }

            RestoreCameraAndInput();
            if (_completionDelivered)
            {
                return;
            }

            _completionDelivered = true;
            Action callback = _onCompleted;
            _onCompleted = null;
            callback?.Invoke();
        }

        private void CaptureCamera()
        {
            if (_cameraStateCaptured || _camera == null)
            {
                return;
            }

            _cameraPosition = _camera.transform.position;
            _cameraRotation = _camera.transform.rotation;
            _cameraFieldOfView = _camera.fieldOfView;
            _cameraFollow = _camera.GetComponent<CameraFollow>();
            _cameraFollowWasEnabled = _cameraFollow != null && _cameraFollow.enabled;
            if (_cameraFollowWasEnabled)
            {
                _cameraFollowSuspension =
                    _cameraFollow.AcquirePresentationSuspension("npc-conversation");
            }

            _cameraStateCaptured = true;
        }

        private void FrameSpeaker()
        {
            if (_camera == null || _speaker == null)
            {
                return;
            }

            Vector3 towardPlayer = _player != null
                ? _player.position - _speaker.position
                : -_speaker.forward;
            towardPlayer.y = 0f;
            if (towardPlayer.sqrMagnitude < 0.01f)
            {
                towardPlayer = -_speaker.forward;
                towardPlayer.y = 0f;
            }

            towardPlayer.Normalize();
            Vector3 focus = _speaker.position + Vector3.up * 1.45f;
            _camera.transform.position =
                _speaker.position + towardPlayer * 2.8f + Vector3.up * 1.65f;
            _camera.transform.rotation = Quaternion.LookRotation(
                focus - _camera.transform.position,
                Vector3.up);
            _camera.fieldOfView = 38f;
        }

        private void RestoreCameraAndInput()
        {
            if (_cameraStateCaptured && _camera != null)
            {
                _camera.transform.position = _cameraPosition;
                _camera.transform.rotation = _cameraRotation;
                _camera.fieldOfView = _cameraFieldOfView;
            }

            _cameraStateCaptured = false;
            _cameraFollowSuspension?.Dispose();
            _cameraFollowSuspension = null;
            _cursorOwnership?.Dispose();
            _cursorOwnership = null;
        }

        private void EnsureBuilt()
        {
            if (_panel != null)
            {
                return;
            }

            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 94;
            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>() ??
                                  gameObject.AddComponent<CanvasScaler>();
            QuestHudChrome.ApplyScaler(scaler);
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            Font font = QuestHudChrome.ResolveFont(18);
            Image plate = QuestHudChrome.CreatePlate(
                transform,
                "NpcConversationBottomBar",
                new Color(0.035f, 0.045f, 0.065f, 0.96f),
                new Vector2(0.08f, 0f),
                new Vector2(0.92f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(0f, 152f));
            _panel = plate.gameObject;
            PanelRect = plate.rectTransform;
            Button skipButton = plate.gameObject.AddComponent<Button>();
            skipButton.onClick.AddListener(() => SkipCurrentLine());

            _speakerLabel = QuestHudChrome.CreateLabel(
                plate.transform,
                "Speaker",
                font,
                string.Empty,
                20,
                new Color(0.94f, 0.76f, 0.33f, 1f),
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -14f),
                new Vector2(-88f, 28f));

            SubtitleLabel = QuestHudChrome.CreateLabel(
                plate.transform,
                "Subtitle",
                font,
                string.Empty,
                18,
                new Color(0.94f, 0.95f, 0.98f, 1f),
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -48f),
                new Vector2(-48f, 66f));

            QuestHudChrome.CreateLabel(
                plate.transform,
                "Hint",
                font,
                "Click to skip this line  •  Esc to collapse",
                12,
                new Color(0.66f, 0.70f, 0.78f, 1f),
                TextAnchor.LowerRight,
                Vector2.zero,
                Vector2.one,
                new Vector2(1f, 0f),
                new Vector2(-22f, 12f),
                new Vector2(-44f, 22f));

            Button close = QuestHudChrome.CreateHit(
                plate.transform,
                "CollapseConversation",
                new Color(0.10f, 0.12f, 0.17f, 0.96f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-18f, -14f),
                new Vector2(44f, 34f));
            close.onClick.AddListener(Collapse);
            QuestHudChrome.CreateLabel(
                close.transform,
                "Label",
                font,
                "×",
                24,
                Color.white,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            _panel.SetActive(false);
        }
    }
}
