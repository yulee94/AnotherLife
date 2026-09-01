using System;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.ChampionMode.Interaction;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Input;
using AL.Services.Local;
using AL.UI.QuestHud;
using AL.UI.SharedMenu;
using AL.UI.WorldMap;
using AL.World;
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
        private AutoCombatController _questCombat;
        private NpcConversationView _conversation;
        private FirstSessionAuthoredRealmRoute _authoredRoute;
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

#if UNITY_EDITOR
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
#endif

        public void EnsureReady(ChampionArenaSceneController arena, Transform player, RealmId realm)
        {
            _instance = this;
            _arena = arena;
            _player = player;
            _playerController = player != null
                ? player.GetComponent<ChampionController>()
                : null;
            _questCombat = player != null
                ? player.GetComponent<AutoCombatController>()
                : null;
            _authoredRoute = FindFirstObjectByType<FirstSessionAuthoredRealmRoute>();
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
            _questCombat = player != null
                ? player.GetComponent<AutoCombatController>()
                : null;
            _authoredRoute = FindFirstObjectByType<FirstSessionAuthoredRealmRoute>();
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

#if UNITY_EDITOR
        public ProofOfWorthTransition ApplyForTests(ProofOfWorthCommand command)
        {
            if (!OwnerAllowsProgression)
            {
                return new ProofOfWorthTransition(
                    ProofOfWorthStatus.Rejected,
                    _state);
            }

            return Apply(command);
        }
#endif

#if UNITY_EDITOR
        public bool ApplyWorldInteractionForTests(string catalogId)
        {
            if (_state == null || !OwnerAllowsProgression)
            {
                return false;
            }

            if (_conversation != null &&
                _conversation.Session != null &&
                _conversation.Session.IsCollapsed &&
                !string.IsNullOrWhiteSpace(ProofOfWorthCopy.DialogueBody(_state.DialogueId)) &&
                string.Equals(
                    _conversation.Session.DialogueId,
                    _state.DialogueId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    catalogId,
                    FirstSessionWorldInteractables.GuideCatalogId,
                    StringComparison.Ordinal))
            {
                _conversation.Reopen();
                return true;
            }

            if (_state.Phase == ProofOfWorthPhase.OmenOffered &&
                string.Equals(
                    catalogId,
                    FirstSessionWorldInteractables.GuideCatalogId,
                    StringComparison.Ordinal))
            {
                return Apply(ProofOfWorthCommand.AcceptOffer).Changed;
            }

            if (_state.Phase == ProofOfWorthPhase.OmenTalk &&
                string.Equals(
                    catalogId,
                    FirstSessionWorldInteractables.GuideCatalogId,
                    StringComparison.Ordinal))
            {
                ProofOfWorthCommand command = _state.DialogueId == ProofOfWorthIds.GoDialogueId
                    ? ProofOfWorthCommand.DeployChampion
                    : _state.DialogueId == ProofOfWorthIds.LoreDialogueId
                        ? ProofOfWorthCommand.Depart
                        : ProofOfWorthCommand.Investigate;
                return Apply(command).Changed;
            }

            if (_state.Phase == ProofOfWorthPhase.C1MeetGuide &&
                string.Equals(
                    catalogId,
                    FirstSessionWorldInteractables.GuideCatalogId,
                    StringComparison.Ordinal))
            {
                return Apply(ProofOfWorthCommand.MeetRealmGuide).Changed;
            }

            if (_state.Phase == ProofOfWorthPhase.C1RestoreCovenant &&
                string.Equals(
                    catalogId,
                    FirstSessionWorldInteractables.CovenantSiteCatalogId,
                    StringComparison.Ordinal))
            {
                return Apply(ProofOfWorthCommand.RestoreCovenant).Changed;
            }

            return false;
        }
#endif

        public static bool TryResolveRouteTarget(
            ProofOfWorthPhase phase,
            out FirstSessionRouteTarget target)
        {
            switch (phase)
            {
                case ProofOfWorthPhase.OmenOffered:
                case ProofOfWorthPhase.OmenTalk:
                case ProofOfWorthPhase.OmenReport:
                case ProofOfWorthPhase.C1MeetGuide:
                    target = FirstSessionRouteTarget.CaptainValerius;
                    return true;
                case ProofOfWorthPhase.OmenArena:
                case ProofOfWorthPhase.OmenFailed:
                case ProofOfWorthPhase.C1FaceGuardian:
                    target = FirstSessionRouteTarget.GuardianTrial;
                    return true;
                case ProofOfWorthPhase.C1RestoreCovenant:
                    target = FirstSessionRouteTarget.CovenantSite;
                    return true;
                case ProofOfWorthPhase.C1AcceptMark:
                    target = FirstSessionRouteTarget.LordshipDestination;
                    return true;
                default:
                    target = default;
                    return false;
            }
        }

        public static bool TryBindQuestCombat(
            ProofOfWorthPhase phase,
            ChampionArenaSceneController arena,
            AutoCombatController combat,
            Transform questTarget)
        {
            return phase == ProofOfWorthPhase.C1FaceGuardian &&
                   arena != null &&
                   ReferenceEquals(arena.GuardianTrialTarget, questTarget) &&
                   combat != null &&
                   combat.TryAssignQuestTarget(arena, questTarget);
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

#if UNITY_EDITOR
        public bool ApplyWorldInteractionForTests(WorldInteractionResult result)
        {
            return TryApplyAcceptedWorldInteraction(result);
        }
#endif

        private bool TryApplyAcceptedWorldInteraction(WorldInteractionResult result)
        {
            if (_failedClosed ||
                !OwnerAllowsProgression ||
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

        private void OnDisable()
        {
            ReleaseAutoQuestFollow();
            _conversation?.Retire();
        }

        private void OnEnable()
        {
            if (_ready && _state != null)
            {
                RefreshPresentation();
            }
        }

        private void HandleWorldInteractionConfirmed(
            WorldInteractionResult result)
        {
            if (_worldInteractionDirector == null ||
                _playerController == null ||
                !ReferenceEquals(
                    _worldInteractionDirector.Actor,
                    _playerController.transform))
            {
                return;
            }

            TryApplyAcceptedWorldInteraction(result);
        }

        private void Update()
        {
            if (_failedClosed || _state == null || _state.LordshipGranted)
            {
                return;
            }

            if (!OwnerIsActive)
            {
                ReleaseAutoQuestFollow();
                return;
            }

            if (_state.Phase == ProofOfWorthPhase.C1FaceGuardian)
            {
                ConsiderGuardian();
            }

            if (!OwnerAllowsProgression)
            {
                ReleaseAutoQuestFollow();
                return;
            }

            bool canAutoDrive = QuestHudAutoQuest.Enabled &&
                                (_conversation == null || !_conversation.IsVisible) &&
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
            if (!_guardianStarted)
            {
                if (_arena == null || !OwnerAllowsProgression)
                {
                    return;
                }

                _guardianStarted = _arena.TryStartGuardianTrial();
                if (_guardianStarted)
                {
                    TryBindQuestCombat(
                        _state.Phase,
                        _arena,
                        _questCombat,
                        _arena.GuardianTrialTarget);
                }
            }

            if (_guardianStarted &&
                OwnerIsActive &&
                _arena != null &&
                _arena.GuardianTrialCleared)
            {
                _questCombat?.ClearQuestTarget();
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
            if (!OwnerAllowsConversationCommand)
            {
                return;
            }

            if (_conversation != null &&
                _conversation.Session != null &&
                _conversation.Session.IsCollapsed &&
                !string.IsNullOrWhiteSpace(
                    ProofOfWorthCopy.DialogueBody(_state.DialogueId)) &&
                string.Equals(
                    _conversation.Session.DialogueId,
                    _state.DialogueId,
                    StringComparison.Ordinal))
            {
                _conversation.Reopen();
                return;
            }

            if (_conversation != null &&
                _conversation.Session != null &&
                _conversation.Session.IsCollapsed)
            {
                _conversation.Retire();
            }

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
            if (!OwnerAllowsConversationCommand)
            {
                return;
            }

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

        private bool OwnerIsActive =>
            isActiveAndEnabled &&
            _playerController != null &&
            _playerController.isActiveAndEnabled;

        private bool OwnerAllowsProgression =>
            OwnerIsActive &&
            !_playerController.BlocksGameplayEntry;

        private bool OwnerAllowsConversationCommand =>
            OwnerAllowsProgression ||
            (OwnerIsActive &&
             _playerController.AllowsOwnedModalCommand &&
             ChampionHudCameraGate.HasExclusiveOwnedCursorGate &&
             _conversation != null &&
             _conversation.IsVisible &&
             _conversation.Session != null &&
             !_conversation.Session.IsCollapsed &&
             !string.IsNullOrWhiteSpace(
                 ProofOfWorthCopy.DialogueBody(_state.DialogueId)) &&
             string.Equals(
                 _conversation.Session.DialogueId,
                 _state.DialogueId,
                 StringComparison.Ordinal));

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

            Vector3 objectiveDelta = marker.position - _player.position;
            objectiveDelta.y = 0f;
            if (objectiveDelta.sqrMagnitude <= 9f)
            {
                ReleaseAutoQuestFollow();
                return;
            }

            Transform steeringTarget = marker;
            if (_authoredRoute != null &&
                _authoredRoute.TryGetNextWaypoint(
                    _player.position,
                    marker,
                    out Transform waypoint))
            {
                steeringTarget = waypoint;
            }

            Vector3 delta = steeringTarget.position - _player.position;
            delta.y = 0f;

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
                case ProofOfWorthPhase.OmenOffered:
                case ProofOfWorthPhase.OmenTalk:
                case ProofOfWorthPhase.OmenReport:
                case ProofOfWorthPhase.C1MeetGuide:
                    return ProofOfWorthIds.MeetGuideObjectiveId + "_TEMPORARY";
                case ProofOfWorthPhase.OmenArena:
                case ProofOfWorthPhase.OmenFailed:
                    return ProofOfWorthIds.SkyCastleMarkerId + "_TEMPORARY";
                case ProofOfWorthPhase.C1RestoreCovenant:
                    return ProofOfWorthIds.RestoreCovenantObjectiveId + "_TEMPORARY";
                case ProofOfWorthPhase.C1FaceGuardian:
                    return ProofOfWorthIds.FaceGuardianObjectiveId + "_TEMPORARY";
                case ProofOfWorthPhase.C1AcceptMark:
                    return ProofOfWorthIds.AcceptMarkObjectiveId + "_TEMPORARY";
                default:
                    return string.Empty;
            }
        }

        private void RefreshPresentation()
        {
            RebuildMarkers();
            RefreshConversation();
            BindQuestHud();
        }

        private void RefreshConversation()
        {
            string body = ProofOfWorthCopy.DialogueBody(_state.DialogueId);
            if (string.IsNullOrEmpty(body))
            {
                _conversation?.Retire();
                return;
            }

            if (_conversation != null &&
                _conversation.Session != null &&
                !_conversation.Session.IsCompleted &&
                string.Equals(
                    _conversation.Session.DialogueId,
                    _state.DialogueId,
                    StringComparison.Ordinal))
            {
                return;
            }

            GameObject speakerObject = GameObject.Find(
                FirstSessionWorldInteractables.GuideObjectName);
            Transform speaker = speakerObject != null
                ? speakerObject.transform
                : _authoredRoute != null
                    ? _authoredRoute.CaptainValerius
                    : null;
            _conversation ??= NpcConversationView.Mount(transform);
            _conversation.Show(
                _state.DialogueId,
                ProofOfWorthCopy.SpeakerName,
                body,
                _player,
                speaker,
                UnityEngine.Camera.main,
                ChoosePrimary);
        }

        private void BindQuestHud()
        {
            if (Hud == null)
            {
                return;
            }

            QuestHudModel model =
                QuestHudPlanner.FromProofOfWorth(_state, QuestHudAutoQuest.Enabled);
            bool hasDialogue = !string.IsNullOrEmpty(
                ProofOfWorthCopy.DialogueBody(_state.DialogueId));
            MainQuestMapSession.Publish(
                _state.ObjectiveId,
                _state.Realm,
                model.WhatToDo);
            Hud.Bind(
                model,
                ChoosePrimary,
                BindQuestHud,
                allowImmediateAutoFire: !hasDialogue);
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

            if (!TryResolveRouteTarget(_state.Phase, out FirstSessionRouteTarget target))
            {
                return;
            }

            Vector3 position;
            if (_authoredRoute != null &&
                _authoredRoute.TryGetAnchor(target, out Transform routeAnchor))
            {
                position = routeAnchor.position;
            }
            else
            {
                position = FallbackMarkerPosition(target);
            }

            _markerRoot = new GameObject(MarkerRootName);
            CreateMarker(_markerRoot.transform, name, position);
        }

        private Vector3 FallbackMarkerPosition(FirstSessionRouteTarget target)
        {
            Vector3 origin = _player != null ? _player.position : Vector3.zero;
            origin.y = 0f;
            switch (target)
            {
                case FirstSessionRouteTarget.CaptainValerius:
                    return origin + new Vector3(-1.8f, 0f, 3.5f);
                case FirstSessionRouteTarget.GuardianTrial:
                    return origin + new Vector3(0f, 0f, 4.2f);
                case FirstSessionRouteTarget.CovenantSite:
                    return origin + new Vector3(3.25f, 0f, 2.55f);
                case FirstSessionRouteTarget.LordshipDestination:
                    return origin + new Vector3(0f, 0f, 3.1f);
                default:
                    return origin;
            }
        }

        private static void CreateMarker(Transform parent, string name, Vector3 position)
        {
            var marker = new GameObject(name);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.position = position + Vector3.up * 0.12f;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            var material = new Material(shader);
            var color = new Color(1f, 0.74f, 0.22f, 0.92f);
            material.color = color;

            var ringObject = new GameObject("ObjectiveRing");
            ringObject.transform.SetParent(marker.transform, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = material;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 32;
            ring.widthMultiplier = 0.12f;
            ring.alignment = LineAlignment.View;
            for (int index = 0; index < ring.positionCount; index++)
            {
                float angle = Mathf.PI * 2f * index / ring.positionCount;
                ring.SetPosition(index, new Vector3(
                    Mathf.Cos(angle) * 1.25f,
                    0f,
                    Mathf.Sin(angle) * 1.25f));
            }

            var beamObject = new GameObject("ObjectiveBeam");
            beamObject.transform.SetParent(marker.transform, false);
            LineRenderer beam = beamObject.AddComponent<LineRenderer>();
            beam.sharedMaterial = material;
            beam.useWorldSpace = false;
            beam.positionCount = 2;
            beam.widthMultiplier = 0.08f;
            beam.alignment = LineAlignment.View;
            beam.SetPosition(0, Vector3.up * 2.6f);
            beam.SetPosition(1, Vector3.up * 6.8f);

            Light light = marker.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 7f;
            light.intensity = 1.4f;
        }

        private void BuildOverlay()
        {
            Hud = QuestHudOverlay.Mount(transform);
            BindQuestHud();
        }
    }
}
