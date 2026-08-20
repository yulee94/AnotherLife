using AL.Core;
using AL.Core.Interfaces;
using AL.Input;
using UnityEngine;
using UnityEngine.UI;

namespace AL.ChampionMode.Quests
{
    /// <summary>
    /// First-session 3D driver for OMEN_1 then MQ_C1_PROOF_OF_WORTH.
    /// Offer is never auto-accepted. Guardian trial uses the catalog first fight.
    /// </summary>
    public sealed class ProofOfWorthDirector : MonoBehaviour
    {
        public const string OverlayRootName = "ProofOfWorthOverlay_TEMPORARY";
        public const string MarkerRootName = "ProofOfWorthMarkers_TEMPORARY";

        private static ProofOfWorthDirector _instance;
        private ProofOfWorthState _state;
        private ChampionArenaSceneController _arena;
        private Transform _player;
        private GameObject _markerRoot;
        private Text _titleText;
        private Text _speakerText;
        private Text _bodyText;
        private Text _objectiveText;
        private Text _primaryLabel;
        private Text _secondaryLabel;
        private bool _ready;
        private bool _guardianStarted;
        private bool _persistAttempted;
        private string _lastPersistMessage = string.Empty;

        public ProofOfWorthState State => _state;
        public string LastPersistMessage => _lastPersistMessage;
        public bool PersistAttempted => _persistAttempted;

        public static ProofOfWorthDirector AttachIfNeeded(
            Transform parent,
            ChampionArenaSceneController arena,
            Transform player,
            RealmId realm)
        {
            if (!FirstSessionChampionStart.IsFirstSessionLanding)
            {
                return null;
            }

            if (_instance != null)
            {
                _instance.EnsureReady(arena, player, realm);
                return _instance;
            }

            var host = new GameObject(OverlayRootName);
            if (parent != null)
            {
                host.transform.SetParent(parent, false);
            }

            ProofOfWorthDirector director = host.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(arena, player, realm);
            return director;
        }

        public static void ResetForTests()
        {
            _instance = null;
        }

        public void EnsureReady(ChampionArenaSceneController arena, Transform player, RealmId realm)
        {
            _instance = this;
            _arena = arena;
            _player = player;
            if (_ready)
            {
                return;
            }

            _state = ProofOfWorthPlanner.CreateOffered(realm);
            BuildOverlay();
            RefreshPresentation();
            _ready = true;
        }

        public ProofOfWorthTransition ApplyForTests(ProofOfWorthCommand command)
        {
            return Apply(command);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (_state == null || _state.LordshipGranted)
            {
                return;
            }

            if (_state.Phase == ProofOfWorthPhase.C1FaceGuardian)
            {
                ConsiderGuardian();
            }

            if (!GameInput.SubmitPressed())
            {
                return;
            }

            ConsiderSubmit();
        }

        private void ConsiderGuardian()
        {
            if (!_guardianStarted && _arena != null)
            {
                _guardianStarted = _arena.TryStartGuardianTrial();
            }

            if (_arena != null && _arena.GuardianTrialCleared)
            {
                Apply(ProofOfWorthCommand.GuardianDefeated);
            }
        }

        private void ConsiderSubmit()
        {
            switch (_state.Phase)
            {
                case ProofOfWorthPhase.OmenOffered:
                    Apply(ProofOfWorthCommand.SelectValerius);
                    break;
                case ProofOfWorthPhase.OmenTalk:
                    if (_state.DialogueId == ProofOfWorthIds.GoDialogueId)
                    {
                        Apply(ProofOfWorthCommand.DeployChampion);
                    }
                    else if (_state.DialogueId == ProofOfWorthIds.LoreDialogueId)
                    {
                        Apply(ProofOfWorthCommand.Depart);
                    }
                    else
                    {
                        Apply(ProofOfWorthCommand.Investigate);
                    }

                    break;
                case ProofOfWorthPhase.OmenArena:
                    if (IsNearActiveMarker())
                    {
                        Apply(ProofOfWorthCommand.ArenaSuccess);
                    }

                    break;
                case ProofOfWorthPhase.OmenFailed:
                    Apply(ProofOfWorthCommand.RetryArena);
                    break;
                case ProofOfWorthPhase.OmenReport:
                    if (_state.DialogueId == ProofOfWorthIds.ReportConclusionDialogueId)
                    {
                        Apply(ProofOfWorthCommand.ConcludeReport);
                    }
                    else if (_state.DialogueId == ProofOfWorthIds.ReportDialogueId)
                    {
                        Apply(ProofOfWorthCommand.PresentTear);
                    }
                    else
                    {
                        Apply(ProofOfWorthCommand.SelectValerius);
                    }

                    break;
                case ProofOfWorthPhase.C1MeetGuide:
                    if (IsNearActiveMarker())
                    {
                        Apply(ProofOfWorthCommand.MeetRealmGuide);
                    }

                    break;
                case ProofOfWorthPhase.C1RestoreCovenant:
                    if (IsNearActiveMarker())
                    {
                        Apply(ProofOfWorthCommand.RestoreCovenant);
                    }

                    break;
                case ProofOfWorthPhase.C1AcceptMark:
                    if (IsNearActiveMarker())
                    {
                        Apply(ProofOfWorthCommand.AcceptMark);
                    }

                    break;
            }
        }

        public void ChoosePrimary()
        {
            switch (_state.Phase)
            {
                case ProofOfWorthPhase.OmenOffered:
                    Apply(ProofOfWorthCommand.AcceptOffer);
                    break;
                case ProofOfWorthPhase.OmenTalk:
                    if (_state.DialogueId == ProofOfWorthIds.GoDialogueId)
                    {
                        Apply(ProofOfWorthCommand.DeployChampion);
                    }
                    else if (_state.DialogueId == ProofOfWorthIds.LoreDialogueId)
                    {
                        Apply(ProofOfWorthCommand.Depart);
                    }
                    else
                    {
                        Apply(ProofOfWorthCommand.Investigate);
                    }

                    break;
                case ProofOfWorthPhase.OmenFailed:
                    Apply(ProofOfWorthCommand.RetryArena);
                    break;
                case ProofOfWorthPhase.OmenReport:
                    if (_state.DialogueId == ProofOfWorthIds.ReportConclusionDialogueId)
                    {
                        Apply(ProofOfWorthCommand.ConcludeReport);
                    }
                    else if (_state.DialogueId == ProofOfWorthIds.ReportDialogueId)
                    {
                        Apply(ProofOfWorthCommand.PresentTear);
                    }
                    else
                    {
                        Apply(ProofOfWorthCommand.SelectValerius);
                    }

                    break;
                default:
                    ConsiderSubmit();
                    break;
            }
        }

        public void ChooseSecondary()
        {
            if (_state.Phase == ProofOfWorthPhase.OmenOffered)
            {
                Apply(ProofOfWorthCommand.DeclineOffer);
                return;
            }

            if (_state.Phase == ProofOfWorthPhase.OmenTalk &&
                _state.DialogueId == ProofOfWorthIds.StartDialogueId)
            {
                Apply(ProofOfWorthCommand.AskMore);
            }
        }

        private ProofOfWorthTransition Apply(ProofOfWorthCommand command)
        {
            ProofOfWorthTransition transition = ProofOfWorthPlanner.Apply(_state, command);
            if (transition.Changed)
            {
                _state = transition.State;
                if (_state.LordshipGranted)
                {
                    PersistLordship();
                }

                RefreshPresentation();
            }

            return transition;
        }

        private void PersistLordship()
        {
            _persistAttempted = true;
            if (ServiceLocator.TryGet(out ISaveGameService save) && save != null)
            {
                AL.Services.Local.MvpLoopCommitResult result =
                    ProofOfWorthLordship.TryPersist(save, _state.Realm);
                _lastPersistMessage = result.Message;
                if (result.Accepted)
                {
                    return;
                }
            }

            if (ServiceLocator.TryGet(out ISaveGameService fallback) &&
                fallback != null &&
                fallback.CurrentSave != null)
            {
                ProofOfWorthLordship.TryWriteMark(
                    fallback.CurrentSave,
                    _state.ChapterVariantId);
            }
        }

        private bool IsNearActiveMarker()
        {
            if (_player == null || _markerRoot == null)
            {
                return true;
            }

            Transform marker = _markerRoot.transform.Find(ActiveMarkerName());
            if (marker == null)
            {
                return true;
            }

            Vector3 delta = marker.position - _player.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= 9f;
        }

        private string ActiveMarkerName()
        {
            switch (_state.Phase)
            {
                case ProofOfWorthPhase.OmenArena:
                    return ProofOfWorthIds.SkyCastleMarkerId + "_TEMPORARY";
                case ProofOfWorthPhase.C1MeetGuide:
                    return ProofOfWorthIds.MeetGuideObjectiveId + "_TEMPORARY";
                case ProofOfWorthPhase.C1RestoreCovenant:
                    return ProofOfWorthIds.RestoreCovenantObjectiveId + "_TEMPORARY";
                case ProofOfWorthPhase.C1AcceptMark:
                    return ProofOfWorthIds.AcceptMarkObjectiveId + "_TEMPORARY";
                default:
                    return string.Empty;
            }
        }

        private void RefreshPresentation()
        {
            if (_titleText != null)
            {
                _titleText.text = _state.QuestId == ProofOfWorthIds.MainQuestId
                    ? ProofOfWorthCopy.C1Title
                    : ProofOfWorthCopy.OmenTitle;
            }

            if (_speakerText != null)
            {
                _speakerText.text = ProofOfWorthCopy.SpeakerName;
            }

            if (_bodyText != null)
            {
                string body = ProofOfWorthCopy.DialogueBody(_state.DialogueId);
                _bodyText.text = string.IsNullOrEmpty(body)
                    ? ProofOfWorthCopy.ObjectiveText(_state)
                    : body;
            }

            if (_objectiveText != null)
            {
                _objectiveText.text = ProofOfWorthCopy.ObjectiveText(_state);
            }

            if (_primaryLabel != null)
            {
                _primaryLabel.text = PrimaryChoiceLabel();
            }

            if (_secondaryLabel != null)
            {
                string secondary = SecondaryChoiceLabel();
                _secondaryLabel.text = secondary;
                _secondaryLabel.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(secondary));
            }

            RebuildMarkers();
        }

        private string PrimaryChoiceLabel()
        {
            switch (_state.Phase)
            {
                case ProofOfWorthPhase.OmenOffered:
                    return ProofOfWorthCopy.ChoiceAccept;
                case ProofOfWorthPhase.OmenTalk:
                    if (_state.DialogueId == ProofOfWorthIds.GoDialogueId)
                    {
                        return ProofOfWorthCopy.ChoiceDeploy;
                    }

                    return _state.DialogueId == ProofOfWorthIds.LoreDialogueId
                        ? ProofOfWorthCopy.ChoiceDepart
                        : ProofOfWorthCopy.ChoiceInvestigate;
                case ProofOfWorthPhase.OmenFailed:
                    return ProofOfWorthCopy.ChoiceRetry;
                case ProofOfWorthPhase.OmenReport:
                    if (_state.DialogueId == ProofOfWorthIds.ReportConclusionDialogueId)
                    {
                        return ProofOfWorthCopy.ChoiceContinue;
                    }

                    return _state.DialogueId == ProofOfWorthIds.ReportDialogueId
                        ? ProofOfWorthCopy.ChoicePresentTear
                        : ProofOfWorthCopy.SpeakerName;
                case ProofOfWorthPhase.C1AcceptMark:
                    return ProofOfWorthCopy.C1AcceptMark;
                default:
                    return ProofOfWorthCopy.ObjectiveText(_state);
            }
        }

        private string SecondaryChoiceLabel()
        {
            if (_state.Phase == ProofOfWorthPhase.OmenOffered)
            {
                return ProofOfWorthCopy.ChoiceDecline;
            }

            if (_state.Phase == ProofOfWorthPhase.OmenTalk &&
                _state.DialogueId == ProofOfWorthIds.StartDialogueId)
            {
                return ProofOfWorthCopy.ChoiceAskMore;
            }

            return string.Empty;
        }

        private void RebuildMarkers()
        {
            if (_markerRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_markerRoot);
                }
                else
                {
                    DestroyImmediate(_markerRoot);
                }

                _markerRoot = null;
            }

            string name = ActiveMarkerName();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            Vector3 origin = _player != null ? _player.position : Vector3.zero;
            origin.y = 0f;
            _markerRoot = new GameObject(MarkerRootName);
            Vector3 offset = _state.Phase == ProofOfWorthPhase.OmenArena
                ? new Vector3(0f, 0f, 4.2f)
                : _state.Phase == ProofOfWorthPhase.C1MeetGuide
                    ? new Vector3(-3.15f, 0f, 2.35f)
                    : _state.Phase == ProofOfWorthPhase.C1RestoreCovenant
                        ? new Vector3(3.25f, 0f, 2.55f)
                        : new Vector3(0f, 0f, 3.1f);
            CreateMarker(_markerRoot.transform, name, origin + offset);
        }

        private static void CreateMarker(Transform parent, string name, Vector3 position)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(parent, true);
            marker.transform.position = position + Vector3.up * 0.2f;
            marker.transform.localScale = new Vector3(1.1f, 0.2f, 1.1f);
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                var material = new Material(renderer.sharedMaterial);
                material.color = new Color(0.78f, 0.64f, 0.30f);
                renderer.sharedMaterial = material;
            }
        }

        private void BuildOverlay()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("ProofOfWorthCanvas_TEMPORARY");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            Image plate = CreatePlate(
                canvasObject.transform,
                "QuestPlate_TEMPORARY",
                new Vector2(0.5f, 0.62f),
                Vector2.zero,
                new Vector2(680f, 280f),
                new Color(0.014f, 0.018f, 0.026f, 0.94f));
            CreateAccent(plate.transform, new Color(0.78f, 0.62f, 0.28f, 0.95f));
            _titleText = CreateLabel(
                plate.transform, font, "Title", ProofOfWorthCopy.OmenTitle, 22,
                new Vector2(24f, -16f), new Vector2(632f, 30f), new Color(0.96f, 0.84f, 0.52f));
            _speakerText = CreateLabel(
                plate.transform, font, "Speaker", ProofOfWorthCopy.SpeakerName, 14,
                new Vector2(24f, -48f), new Vector2(632f, 20f), new Color(0.72f, 0.78f, 0.86f));
            _bodyText = CreateLabel(
                plate.transform, font, "Body", ProofOfWorthCopy.OfferBody, 16,
                new Vector2(24f, -74f), new Vector2(632f, 96f), new Color(0.86f, 0.88f, 0.90f));
            _objectiveText = CreateLabel(
                plate.transform, font, "Objective", ProofOfWorthCopy.OmenTalkObjective, 14,
                new Vector2(24f, -176f), new Vector2(632f, 36f), new Color(0.70f, 0.82f, 0.92f));
            CreateLabel(
                plate.transform, font, "Chrome", ProofOfWorthCopy.OverlayChrome, 12,
                new Vector2(24f, -246f), new Vector2(632f, 20f), new Color(0.62f, 0.56f, 0.40f));

            _primaryLabel = CreateChoiceButton(
                canvasObject.transform, font, "PrimaryChoice", ProofOfWorthCopy.ChoiceAccept,
                new Vector2(-150f, -210f), ChoosePrimary);
            _secondaryLabel = CreateChoiceButton(
                canvasObject.transform, font, "SecondaryChoice", ProofOfWorthCopy.ChoiceDecline,
                new Vector2(150f, -210f), ChooseSecondary);
        }

        private Text CreateChoiceButton(
            Transform parent,
            Font font,
            string name,
            string copy,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction onClick)
        {
            Image plate = CreatePlate(
                parent,
                name + "_TEMPORARY",
                new Vector2(0.5f, 0.62f),
                anchoredPosition,
                new Vector2(280f, 44f),
                new Color(0.08f, 0.09f, 0.11f, 0.95f));
            var button = plate.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            return CreateLabel(
                plate.transform,
                font,
                name + "Label",
                copy,
                15,
                new Vector2(12f, -10f),
                new Vector2(256f, 24f),
                new Color(0.92f, 0.86f, 0.70f));
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
