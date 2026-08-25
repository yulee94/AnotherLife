using System;
using AL.ChampionMode.Control;
using AL.ChampionMode.Interaction;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Input;
using AL.Services.Local;
using AL.UI.QuestHud;
using AL.UI.SharedMenu;
using AL.UI.WorldMap;
using UnityEngine;

namespace AL.ChampionMode.Quests
{
    /// <summary>
    /// First-session 3D driver for OMEN_1 then MQ_C1_PROOF_OF_WORTH.
    /// The offer is manual by default and follows the Auto Quest preference.
    /// Guardian trial uses the catalog first fight.
    /// </summary>
    public sealed class ProofOfWorthDirector : MonoBehaviour
    {
        public const string OverlayRootName = "ProofOfWorthOverlay_TEMPORARY";
        public const string MarkerRootName = "ProofOfWorthMarkers_TEMPORARY";

        private static ProofOfWorthDirector _instance;
        private ProofOfWorthState _state;
        private ChampionArenaSceneController _arena;
        private Transform _player;
        private ChampionController _playerController;
        private WorldInteractionDirector _worldInteractionDirector;
        private GameObject _markerRoot;
        private bool _ready;
        private bool _autoFollowInputApplied;
        private bool _guardianStarted;
        private bool _persistAttempted;
        private string _lastPersistMessage = string.Empty;
        private ISaveGameService _saveGameService;
        private FirstWorldProgressSnapshot _progressSnapshot;
        private bool _durable;
        private bool _failedClosed;

        public static event Action LordshipGrantedObserved;

        public ProofOfWorthState State => _state;
        public QuestHudOverlay Hud { get; private set; }
        public string LastPersistMessage => _lastPersistMessage;
        public bool PersistAttempted => _persistAttempted;

        public static ProofOfWorthDirector AttachIfNeeded(
            Transform parent,
            ChampionArenaSceneController arena,
            Transform player,
            RealmId realm)
        {
            SaveGameData save = SharedMenuModeSwitchHost.ReadSave();
            if (!ShouldAttachForSave(save))
            {
                return null;
            }

            string message = string.Empty;
            FirstWorldProgressSnapshot progress = null;
            if (!ServiceLocator.TryGet(
                    out ISaveGameService saveGameService) ||
                !FirstWorldProgressSaveAuthority.CanCommit(saveGameService) ||
                !FirstWorldProgressSaveAuthority.TryRead(
                    saveGameService,
                    out progress,
                    out message) ||
                !progress.CanRunProof ||
                progress.Realm != realm ||
                progress.Proof.LordshipGranted)
            {
                Debug.LogError(
                    "[AL-PROOF-PROGRESS-FAILED-CLOSED] " +
                    (string.IsNullOrWhiteSpace(message)
                        ? "Durable tutorial handoff or Proof authority is unavailable."
                        : message));
                return null;
            }

            if (_instance != null)
            {
                _instance.EnsureReadyDurable(
                    arena,
                    player,
                    realm,
                    saveGameService,
                    progress);
                return _instance;
            }

            var host = new GameObject(OverlayRootName);
            if (parent != null)
            {
                host.transform.SetParent(parent, false);
            }

            ProofOfWorthDirector director = host.AddComponent<ProofOfWorthDirector>();
            director.EnsureReadyDurable(
                arena,
                player,
                realm,
                saveGameService,
                progress);
            return director;
        }

        public static bool ShouldAttachForSave(SaveGameData save)
        {
            MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(save);
            return FirstSessionChampionStart.IsFirstSessionLanding &&
                   (!snapshot.IdentityConfirmed ||
                    !string.Equals(
                        snapshot.LastResultId,
                        ProofOfWorthLordship.ResolveMarkId(snapshot.Realm),
                        StringComparison.Ordinal));
        }

        public static void ResetForTests()
        {
            if (_instance != null)
            {
                _instance.BindWorldInteractionDirector(null);
            }

            _instance = null;
            LordshipGrantedObserved = null;
            QuestHudAutoQuest.ResetForTests();
        }

        public void EnsureReady(ChampionArenaSceneController arena, Transform player, RealmId realm)
        {
            _instance = this;
            _arena = arena;
            _player = player;
            _playerController = player != null
                ? player.GetComponent<ChampionController>()
                : null;
            if (_ready)
            {
                return;
            }

            _state = ProofOfWorthPlanner.CreateOffered(realm);
            BuildOverlay();
            RefreshPresentation();
            _ready = true;
        }

        private void EnsureReadyDurable(
            ChampionArenaSceneController arena,
            Transform player,
            RealmId realm,
            ISaveGameService saveGameService,
            FirstWorldProgressSnapshot progress)
        {
            _instance = this;
            _arena = arena;
            _player = player;
            _playerController = player != null
                ? player.GetComponent<ChampionController>()
                : null;
            if (_ready)
            {
                if (!_durable ||
                    _progressSnapshot == null ||
                    _progressSnapshot.Realm != realm)
                {
                    FailClosed("AL-PROOF-INSTANCE-AUTHORITY-CONFLICT");
                }

                return;
            }

            if (!FirstWorldProgressSaveAuthority.CanCommit(saveGameService) ||
                progress == null ||
                !progress.CanRunProof ||
                progress.Realm != realm ||
                progress.Proof.LordshipGranted)
            {
                FailClosed("AL-PROOF-PROGRESS-UNAVAILABLE");
                return;
            }

            _saveGameService = saveGameService;
            _progressSnapshot = progress;
            _state = progress.Proof;
            _durable = true;
            BuildOverlay();
            RefreshPresentation();
            _ready = true;
        }

        public ProofOfWorthTransition ApplyForTests(ProofOfWorthCommand command)
        {
            return Apply(command);
        }

        public void BindWorldInteractionDirector(WorldInteractionDirector director)
        {
            if (_worldInteractionDirector == director)
            {
                return;
            }

            if (_worldInteractionDirector != null)
            {
                _worldInteractionDirector.Confirmed -=
                    HandleWorldInteractionConfirmed;
            }

            _worldInteractionDirector = director;
            if (_worldInteractionDirector != null)
            {
                _worldInteractionDirector.Confirmed +=
                    HandleWorldInteractionConfirmed;
            }
        }

        public bool TryApplyWorldInteraction(WorldInteractionResult result)
        {
            if (_failedClosed ||
                !result.Accepted ||
                _state == null ||
                !string.Equals(
                    result.CatalogId,
                    _state.ObjectiveId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            ProofOfWorthCommand command;
            switch (_state.Phase)
            {
                case ProofOfWorthPhase.C1MeetGuide
                    when result.Kind == WorldInteractionKind.Talk:
                    command = ProofOfWorthCommand.MeetRealmGuide;
                    break;
                case ProofOfWorthPhase.C1RestoreCovenant
                    when result.Kind == WorldInteractionKind.Use:
                    command = ProofOfWorthCommand.RestoreCovenant;
                    break;
                default:
                    return false;
            }

            return Apply(command).Changed;
        }

        private void OnDestroy()
        {
            BindWorldInteractionDirector(null);
            ReleaseAutoQuestFollow();
            if (_instance == this)
            {
                _instance = null;
            }

            MainQuestMapSession.Clear();
        }

        private void HandleWorldInteractionConfirmed(
            WorldInteractionResult result)
        {
            TryApplyWorldInteraction(result);
        }

        private void Update()
        {
            if (_failedClosed || _state == null || _state.LordshipGranted)
            {
                return;
            }

            if (_state.Phase == ProofOfWorthPhase.C1FaceGuardian)
            {
                ConsiderGuardian();
            }

            bool canAutoDrive = QuestHudAutoQuest.Enabled &&
                                QuestHudAutoQuest.CanDriveInCurrentContext();
            if (canAutoDrive)
            {
                Hud?.ConsiderAutoQuest();
            }

            DriveAutoQuestFollow(canAutoDrive);
            if (canAutoDrive && IsNearActiveMarker())
            {
                ConsiderSubmit();
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
            ProofOfWorthTransition transition =
                ProofOfWorthPlanner.Apply(_state, command);
            if (!transition.Changed)
            {
                return transition;
            }

            bool grantedLordship = transition.State.LordshipGranted;
            if (_durable)
            {
                if (_failedClosed ||
                    _saveGameService == null ||
                    _progressSnapshot == null)
                {
                    FailClosed("AL-PROOF-PROGRESS-UNAVAILABLE");
                    return RejectedCurrent();
                }

                if (grantedLordship && !TryPersistLordship())
                {
                    FailClosed(_lastPersistMessage);
                    return RejectedCurrent();
                }

                FirstWorldProgressCommitResult commit =
                    FirstWorldProgressSaveAuthority.TryAdvanceProof(
                        _saveGameService,
                        _progressSnapshot,
                        command);
                if (commit == null ||
                    !commit.Accepted ||
                    commit.Snapshot?.Proof == null)
                {
                    // A crash-safe lordship write is authoritative even if the
                    // extension write becomes uncertain. Its codec reconciles
                    // that older slot forward instead of replaying the quest.
                    if (grantedLordship &&
                        FirstWorldProgressSaveAuthority.TryRead(
                            _saveGameService,
                            out FirstWorldProgressSnapshot reconciled,
                            out _) &&
                        reconciled.CanRunProof &&
                        reconciled.Proof.LordshipGranted)
                    {
                        _progressSnapshot = reconciled;
                        _state = reconciled.Proof;
                        RefreshPresentation();
                        LordshipGrantedObserved?.Invoke();
                        return new ProofOfWorthTransition(
                            ProofOfWorthStatus.Applied,
                            _state);
                    }

                    FailClosed(
                        commit?.Message ??
                        "AL-PROOF-PROGRESS-COMMIT-FAILED");
                    return RejectedCurrent();
                }

                _progressSnapshot = commit.Snapshot;
                _state = commit.Snapshot.Proof;
            }
            else
            {
                _state = transition.State;
                if (grantedLordship)
                {
                    TryPersistLordship();
                }
            }

            RefreshPresentation();
            if (grantedLordship)
            {
                LordshipGrantedObserved?.Invoke();
            }

            return new ProofOfWorthTransition(
                ProofOfWorthStatus.Applied,
                _state);
        }

        private bool TryPersistLordship()
        {
            _persistAttempted = true;
            ISaveGameService save = _saveGameService;
            if (save == null)
            {
                ServiceLocator.TryGet(out save);
            }

            if (save == null)
            {
                _lastPersistMessage = "AL-C1-LORDSHIP-PROFILE-READ-ONLY";
                return false;
            }

            MvpLoopCommitResult result =
                ProofOfWorthLordship.TryPersist(save, _state.Realm);
            _lastPersistMessage = result?.Message ??
                                  "AL-C1-LORDSHIP-PERSIST-FAILED";
            return result != null && result.Accepted;
        }

        private ProofOfWorthTransition RejectedCurrent()
        {
            return new ProofOfWorthTransition(
                ProofOfWorthStatus.Rejected,
                _state);
        }

        private void FailClosed(string message)
        {
            _failedClosed = true;
            enabled = false;
            BindWorldInteractionDirector(null);
            ReleaseAutoQuestFollow();
            Debug.LogError(
                "[AL-PROOF-PROGRESS-FAILED-CLOSED] " +
                (string.IsNullOrWhiteSpace(message)
                    ? "Durable Proof authority is unavailable."
                    : message));
        }

        private bool IsNearActiveMarker()
        {
            if (_player == null || !TryGetActiveMarker(out Transform marker))
            {
                return false;
            }

            Vector3 delta = marker.position - _player.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= 9f;
        }

        private void DriveAutoQuestFollow(bool canAutoDrive)
        {
            if (_playerController == null)
            {
                return;
            }

            if (!canAutoDrive || !TryGetActiveMarker(out Transform marker))
            {
                ReleaseAutoQuestFollow();
                return;
            }

            Vector3 delta = marker.position - _player.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 9f)
            {
                ReleaseAutoQuestFollow();
                return;
            }

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera != null)
            {
                forward = camera.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.01f)
                {
                    forward.Normalize();
                    right = new Vector3(forward.z, 0f, -forward.x);
                }
            }

            Vector3 direction = delta.normalized;
            _playerController.SetExternalMoveInput(new Vector2(
                Vector3.Dot(direction, right),
                Vector3.Dot(direction, forward)));
            _autoFollowInputApplied = true;
        }

        private void ReleaseAutoQuestFollow()
        {
            if (!_autoFollowInputApplied || _playerController == null)
            {
                return;
            }

            _playerController.SetExternalMoveInput(Vector2.zero);
            _autoFollowInputApplied = false;
        }

        private bool TryGetActiveMarker(out Transform marker)
        {
            marker = null;
            if (_markerRoot == null)
            {
                return false;
            }

            string markerName = ActiveMarkerName();
            if (string.IsNullOrEmpty(markerName))
            {
                return false;
            }

            marker = _markerRoot.transform.Find(markerName);
            return marker != null;
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
            RebuildMarkers();
            BindQuestHud();
        }

        private void BindQuestHud()
        {
            if (Hud == null)
            {
                return;
            }

            QuestHudModel model =
                QuestHudPlanner.FromProofOfWorth(_state, QuestHudAutoQuest.Enabled);
            MainQuestMapSession.Publish(
                _state.ObjectiveId,
                _state.Realm,
                model.WhatToDo);
            Hud.Bind(
                model,
                ChoosePrimary,
                BindQuestHud);
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
            Hud = QuestHudOverlay.Mount(transform);
            BindQuestHud();
        }
    }
}
