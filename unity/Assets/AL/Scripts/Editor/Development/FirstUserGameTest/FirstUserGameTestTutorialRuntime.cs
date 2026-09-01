#if !UNITY_EDITOR
#error The isolated first-user tutorial runtime is Editor-only.
#endif

using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AL.ChampionMode.Control;
using AL.Editor.Development.OnboardingAuthority;
using AL.Narrative.Nvs01.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.Editor.Development.FirstUserGameTest
{
    internal enum FirstUserGameTestTutorialFocusTarget
    {
        None = 0,
        Move = 1,
        Attack = 2,
        Report = 3,
        Response = 4,
        Exit = 5
    }

    internal enum FirstUserGameTestOmenUiState
    {
        Preparing = 0,
        ReadyToOpen = 1,
        AwaitingResponse = 2,
        Complete = 3
    }

    internal readonly struct FirstUserGameTestTutorialInteractionPlan
    {
        private FirstUserGameTestTutorialInteractionPlan(
            bool movementEnabled,
            bool movementEmphasized,
            bool attackEnabled,
            bool objectiveActionable,
            bool responseActionable,
            FirstUserGameTestButtonRole objectiveRole,
            FirstUserGameTestTutorialFocusTarget focusTarget)
        {
            MovementEnabled = movementEnabled;
            MovementEmphasized = movementEmphasized;
            AttackEnabled = attackEnabled;
            ObjectiveActionable = objectiveActionable;
            ResponseActionable = responseActionable;
            ObjectiveRole = objectiveRole;
            FocusTarget = focusTarget;
        }

        internal bool MovementEnabled { get; }
        internal bool MovementEmphasized { get; }
        internal bool AttackEnabled { get; }
        internal bool ObjectiveActionable { get; }
        internal bool ResponseActionable { get; }
        internal FirstUserGameTestButtonRole ObjectiveRole { get; }
        internal FirstUserGameTestTutorialFocusTarget FocusTarget { get; }

        internal static bool TryCreate(
            FirstUserGameTestTutorialState state,
            FirstUserGameTestOmenUiState omenUiState,
            out FirstUserGameTestTutorialInteractionPlan plan)
        {
            plan = default(FirstUserGameTestTutorialInteractionPlan);
            if (!FirstUserGameTestTutorialPlanner.IsValidState(state) ||
                !Enum.IsDefined(typeof(FirstUserGameTestOmenUiState), omenUiState) ||
                state.Step != FirstUserGameTestTutorialStep.Complete &&
                omenUiState != FirstUserGameTestOmenUiState.Preparing)
            {
                return false;
            }

            switch (state.Step)
            {
                case FirstUserGameTestTutorialStep.Move:
                    plan = new FirstUserGameTestTutorialInteractionPlan(
                        movementEnabled: true,
                        movementEmphasized: true,
                        attackEnabled: false,
                        objectiveActionable: false,
                        responseActionable: false,
                        objectiveRole: FirstUserGameTestButtonRole.Status,
                        focusTarget: FirstUserGameTestTutorialFocusTarget.Move);
                    return true;
                case FirstUserGameTestTutorialStep.BasicAttack:
                    plan = new FirstUserGameTestTutorialInteractionPlan(
                        movementEnabled: false,
                        movementEmphasized: false,
                        attackEnabled: true,
                        objectiveActionable: false,
                        responseActionable: false,
                        objectiveRole: FirstUserGameTestButtonRole.Status,
                        focusTarget: FirstUserGameTestTutorialFocusTarget.Attack);
                    return true;
                case FirstUserGameTestTutorialStep.Complete:
                    bool objectiveActionable =
                        omenUiState == FirstUserGameTestOmenUiState.ReadyToOpen;
                    bool responseActionable =
                        omenUiState == FirstUserGameTestOmenUiState.AwaitingResponse ||
                        omenUiState == FirstUserGameTestOmenUiState.Complete;
                    plan = new FirstUserGameTestTutorialInteractionPlan(
                        movementEnabled: false,
                        movementEmphasized: false,
                        attackEnabled: false,
                        objectiveActionable: objectiveActionable,
                        responseActionable: responseActionable,
                        objectiveRole:
                            omenUiState == FirstUserGameTestOmenUiState.Complete
                                ? FirstUserGameTestButtonRole.Completed
                                : objectiveActionable
                                ? FirstUserGameTestButtonRole.ActiveTask
                                : FirstUserGameTestButtonRole.Status,
                        focusTarget:
                            objectiveActionable
                                ? FirstUserGameTestTutorialFocusTarget.Report
                                : responseActionable
                                    ? FirstUserGameTestTutorialFocusTarget.Response
                                    : FirstUserGameTestTutorialFocusTarget.None);
                    return true;
                default:
                    return false;
            }
        }
    }

    internal static class FirstUserGameTestIsolatedInputGate
    {
        internal static bool AllowsChampionControllerProcessing(
            FirstUserGameTestTutorialState state,
            bool followUiActive)
        {
            return FirstUserGameTestTutorialPlanner.IsValidState(state) &&
                   !followUiActive &&
                   !state.IsOmenOffered;
        }
    }

    internal static class FirstUserGameTestTutorialGeneration
    {
        private const string FrameTag = "al.editor.first-user-tutorial-generation.v1";
        private const string HexAlphabet = "0123456789abcdef";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static bool TryCreate(
            string sessionId,
            VerifiedDevelopmentReceipt receipt,
            VerifiedDevelopmentProjection projection,
            out string generation)
        {
            generation = string.Empty;
            if (!FirstUserGameTestTutorialContract.IsCanonicalSessionId(sessionId) ||
                receipt == null || !receipt.IsValid || receipt.Receipt == null ||
                !receipt.Handle.IsValid ||
                projection == null || !projection.IsValid || projection.Marker == null)
            {
                return false;
            }

            DevelopmentReceiptHandle receiptHandle = receipt.Handle;
            DevelopmentProjectionHandle projectionHandle = projection.Handle;
            if (string.IsNullOrEmpty(projectionHandle.ProjectionInstanceId) ||
                string.IsNullOrEmpty(projectionHandle.ContractVersion) ||
                string.IsNullOrEmpty(projectionHandle.MarkerId) ||
                !projectionHandle.MarkerDigest.IsValid || projectionHandle.MarkerDigest.IsZero)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(512))
                using (var writer = new BinaryWriter(stream, StrictUtf8, leaveOpen: true))
                {
                    WriteField(writer, FrameTag);
                    WriteField(writer, sessionId);
                    WriteField(writer, receiptHandle.AuthorityInstanceId);
                    WriteField(writer, receiptHandle.ContractVersion);
                    WriteField(writer, receiptHandle.ReceiptId);
                    WriteField(writer, receiptHandle.BodyDigest.ToHex());
                    WriteField(writer, receipt.Receipt.CommittedGeneration.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));
                    WriteField(writer, projectionHandle.ProjectionInstanceId);
                    WriteField(writer, projectionHandle.ContractVersion);
                    WriteField(writer, projectionHandle.MarkerId);
                    WriteField(writer, projectionHandle.MarkerDigest.ToHex());
                    writer.Flush();

                    using (SHA256 sha = SHA256.Create())
                    {
                        byte[] digest = sha.ComputeHash(stream.ToArray());
                        var characters = new char[digest.Length * 2];
                        for (int index = 0; index < digest.Length; index++)
                        {
                            characters[index * 2] = HexAlphabet[digest[index] >> 4];
                            characters[(index * 2) + 1] = HexAlphabet[digest[index] & 0x0f];
                        }

                        generation = new string(characters);
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is EncoderFallbackException ||
                exception is CryptographicException ||
                exception is ObjectDisposedException)
            {
                generation = string.Empty;
                return false;
            }

            return FirstUserGameTestTutorialContract.IsCanonicalGeneration(generation);
        }

        private static void WriteField(BinaryWriter writer, string value)
        {
            byte[] bytes = StrictUtf8.GetBytes(value ?? string.Empty);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }

    internal sealed class FirstUserGameTestTutorialSessionStore
    {
        private const string KeyPrefix = "AL.FirstUserGameTest.Tutorial.v1.";

        private readonly string _sessionId;
        private readonly string _generation;
        private readonly string _key;

        internal FirstUserGameTestTutorialSessionStore(
            string sessionId,
            string generation)
        {
            if (!FirstUserGameTestTutorialContract.IsCanonicalSessionId(sessionId) ||
                !FirstUserGameTestTutorialContract.IsCanonicalGeneration(generation))
            {
                throw new ArgumentException(
                    "Tutorial storage requires an exact session and verified generation.");
            }

            _sessionId = sessionId;
            _generation = generation;
            _key = KeyPrefix + sessionId;
        }

        internal bool TryLoadOrCreate(
            out FirstUserGameTestTutorialState state,
            out string message)
        {
            state = null;
            message = string.Empty;
            string payload = SessionState.GetString(_key, string.Empty);
            if (string.IsNullOrEmpty(payload))
            {
                if (!FirstUserGameTestTutorialPlanner.TryCreateInitial(
                        _sessionId,
                        _generation,
                        out state) ||
                    !TryPersist(state, out message))
                {
                    state = null;
                    if (string.IsNullOrEmpty(message))
                    {
                        message = "The initial tutorial state could not be retained.";
                    }

                    return false;
                }

                message = string.Empty;
                return true;
            }

            if (!FirstUserGameTestTutorialStateCodec.TryDecode(payload, out state) ||
                !string.Equals(state.SessionId, _sessionId, StringComparison.Ordinal) ||
                !string.Equals(state.Generation, _generation, StringComparison.Ordinal))
            {
                state = null;
                message =
                    "The retained tutorial state did not match the exact Game Test session generation.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        internal bool TryApply(
            FirstUserGameTestTutorialEvidenceKind kind,
            out FirstUserGameTestTutorialTransition transition,
            out string message)
        {
            transition = null;
            if (!TryLoadOrCreate(out FirstUserGameTestTutorialState current, out message))
            {
                return false;
            }

            transition = FirstUserGameTestTutorialPlanner.Apply(
                current,
                new FirstUserGameTestTutorialEvidence(_sessionId, _generation, kind));
            if (transition.Status == FirstUserGameTestTutorialTransitionStatus.Rejected)
            {
                message = "Tutorial evidence was rejected: " + transition.Diagnostic + ".";
                return false;
            }

            if (!transition.Changed)
            {
                message = string.Empty;
                return true;
            }

            if (!TryPersist(transition.State, out message))
            {
                transition = new FirstUserGameTestTutorialTransition(
                    FirstUserGameTestTutorialTransitionStatus.Rejected,
                    FirstUserGameTestTutorialDiagnostic.RetainedStateConflict,
                    current,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty);
                return false;
            }

            return true;
        }

        internal static void EraseForTests(string sessionId)
        {
            EraseSession(sessionId);
        }

        internal static void EraseSession(string sessionId)
        {
            if (FirstUserGameTestTutorialContract.IsCanonicalSessionId(sessionId))
            {
                SessionState.EraseString(KeyPrefix + sessionId);
            }
        }

        internal static void SetRawForTests(string sessionId, string payload)
        {
            if (FirstUserGameTestTutorialContract.IsCanonicalSessionId(sessionId))
            {
                SessionState.SetString(KeyPrefix + sessionId, payload ?? string.Empty);
            }
        }

        private bool TryPersist(
            FirstUserGameTestTutorialState state,
            out string message)
        {
            message = string.Empty;
            if (!FirstUserGameTestTutorialStateCodec.TryEncode(state, out string payload))
            {
                message = "The tutorial state was not canonical.";
                return false;
            }

            SessionState.SetString(_key, payload);
            string retained = SessionState.GetString(_key, string.Empty);
            if (!string.Equals(payload, retained, StringComparison.Ordinal) ||
                !FirstUserGameTestTutorialStateCodec.TryDecode(
                    retained,
                    out FirstUserGameTestTutorialState verified) ||
                !state.ValueEquals(verified))
            {
                message = "The tutorial state could not be verified after retention.";
                return false;
            }

            return true;
        }
    }

    internal sealed class FirstUserGameTestTutorialPresenter : MonoBehaviour
    {
        internal const string PanelName = "FirstUserGameTestTutorialPanel";
        internal const string TitleActionName = "FirstUserGameTestActiveTitleAction";
        internal const string ObjectiveActionName = "FirstUserGameTestActiveObjectiveAction";
        internal const string DetailName = "FirstUserGameTestActiveObjectiveDetail";
        internal const string PrimaryResponseActionName = "FirstUserGameTestPrimaryResponseAction";
        internal const string SecondaryResponseActionName = "FirstUserGameTestSecondaryResponseAction";

        private const float MovementDistanceThreshold = 0.02f;
        private const float TutorialPanelHeight = 304f;
        private const float OmenPanelHeight = 432f;

        private static readonly FieldInfo IsAttackingField = typeof(ChampionController).GetField(
            "_isAttacking",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ControlsLockedField = typeof(ChampionController).GetField(
            "_controlsLocked",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo RealmField = typeof(ChampionController).GetField(
            "_realmId",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private ChampionController _controller;
        private FirstUserGameTestTutorialSessionStore _store;
        private FirstUserGameTestOmenOfferSession _omenOfferSession;
        private Action<string> _failClosed;
        private FirstUserGameTestTutorialState _state;
        private Button _titleAction;
        private Button _objectiveAction;
        private Text _titleLabel;
        private Text _objectiveLabel;
        private Text _speakerLabel;
        private Text _detail;
        private Image _signalEdge;
        private Button _moveAction;
        private Button[] _moveActions = Array.Empty<Button>();
        private Button _attackAction;
        private Button _exitAction;
        private Button _primaryResponseAction;
        private Button _secondaryResponseAction;
        private string _primaryResponseChoiceKey = string.Empty;
        private string _secondaryResponseChoiceKey = string.Empty;
        private bool _movementIntentPending;
        private Vector3 _movementOrigin;
        private bool _mouseAttackPending;
        private bool _moveFocusApplied;
        private bool _attackFocusApplied;
        private long _focusedObjectiveRevision = -1;
        private long _focusedResponseRevision = -1;
        private FirstUserGameTestOmenOfferStage _focusedResponseStage =
            FirstUserGameTestOmenOfferStage.Closed;
        private long _focusedCompletionRevision = -1;
        private bool _championInputSuppressed;
        private bool _failed;

        internal FirstUserGameTestTutorialState State => _state;
        internal Button TitleAction => _titleAction;
        internal Button ObjectiveAction => _objectiveAction;
        internal Button PrimaryResponseAction => _primaryResponseAction;
        internal Button SecondaryResponseAction => _secondaryResponseAction;
        internal Text Detail => _detail;
        internal Text SpeakerLabel => _speakerLabel;
        internal FirstUserGameTestOmenOfferView OmenOfferView => _omenOfferSession?.View;
        internal FirstUserGameTestPlaytestPhase PlaytestPhase
        {
            get
            {
                if (_state == null || !_state.IsOmenOffered || _omenOfferSession == null)
                {
                    return FirstUserGameTestPlaytestPhase.WorldTutorial;
                }

                FirstUserGameTestOmenOfferStage stage = _omenOfferSession.View.Stage;
                if (stage == FirstUserGameTestOmenOfferStage.RealmReady)
                {
                    return FirstUserGameTestPlaytestPhase.RealmReady;
                }

                string stateId = _omenOfferSession.Snapshot.StateId;
                if (string.Equals(
                        stateId,
                        FirstUserGameTestOmenOfferContract.ReportState,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        stateId,
                        FirstUserGameTestOmenOfferContract.CompletedState,
                        StringComparison.Ordinal))
                {
                    return FirstUserGameTestPlaytestPhase.ValeriusReturn;
                }

                if (stage == FirstUserGameTestOmenOfferStage.DeploymentPrepared ||
                    stage == FirstUserGameTestOmenOfferStage.EncounterActive ||
                    stage == FirstUserGameTestOmenOfferStage.RecoveryReady ||
                    string.Equals(
                        stateId,
                        FirstUserGameTestOmenOfferContract.InvestigateState,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        stateId,
                        FirstUserGameTestOmenOfferContract.FailedState,
                        StringComparison.Ordinal))
                {
                    return FirstUserGameTestPlaytestPhase.SkyCastle;
                }

                return FirstUserGameTestPlaytestPhase.Omen;
            }
        }
        internal bool ChampionInputSuppressed => _championInputSuppressed;
        internal bool MovementIntentPendingForTests => _movementIntentPending;

        internal void BindNavigationActions(
            Button moveLeftAction,
            Button moveRightAction,
            Button moveForwardAction,
            Button moveBackAction,
            Button attackAction,
            Button exitAction)
        {
            _moveActions = new[]
            {
                moveLeftAction,
                moveRightAction,
                moveForwardAction,
                moveBackAction
            };
            _moveAction = moveForwardAction;
            _attackAction = attackAction;
            _exitAction = exitAction;
            if (moveLeftAction == null || moveRightAction == null ||
                moveForwardAction == null || moveBackAction == null ||
                _attackAction == null || _exitAction == null)
            {
                FailClosed("The isolated tutorial navigation boundary was incomplete.");
                return;
            }

            RefreshPresentation();
        }

        internal static bool TryCreate(
            Transform parent,
            Font font,
            ChampionController controller,
            FirstUserGameTestTutorialSessionStore store,
            FirstUserGameTestOmenOfferSession omenOfferSession,
            Action<string> failClosed,
            out FirstUserGameTestTutorialPresenter presenter,
            out string message)
        {
            presenter = null;
            message = string.Empty;
            if (parent == null || font == null || controller == null || store == null ||
                omenOfferSession == null ||
                failClosed == null ||
                !store.TryLoadOrCreate(out FirstUserGameTestTutorialState state, out message))
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "The development tutorial presentation boundary was unavailable.";
                }

                return false;
            }

            var panel = new GameObject(
                PanelName,
                typeof(RectTransform),
                typeof(Image),
                typeof(FirstUserGameTestTutorialPresenter));
            panel.transform.SetParent(parent, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(18f, -102f);
            panelRect.sizeDelta = new Vector2(500f, 432f);
            panel.GetComponent<Image>().color = new Color(0.035f, 0.052f, 0.071f, 0.97f);

            var signalEdge = new GameObject(
                "FirstUserGameTestOmenSignalEdge",
                typeof(RectTransform),
                typeof(Image));
            signalEdge.transform.SetParent(panel.transform, false);
            RectTransform signalRect = signalEdge.GetComponent<RectTransform>();
            signalRect.anchorMin = new Vector2(0f, 0f);
            signalRect.anchorMax = new Vector2(0f, 1f);
            signalRect.pivot = new Vector2(0f, 0.5f);
            signalRect.anchoredPosition = Vector2.zero;
            signalRect.sizeDelta = new Vector2(5f, 0f);
            presenter = panel.GetComponent<FirstUserGameTestTutorialPresenter>();
            presenter._signalEdge = signalEdge.GetComponent<Image>();
            presenter._signalEdge.color = new Color(0.88f, 0.67f, 0.24f, 1f);

            presenter._controller = controller;
            presenter._store = store;
            presenter._omenOfferSession = omenOfferSession;
            presenter._failClosed = failClosed;
            presenter._state = state;

            presenter._titleAction = FirstUserGameTestRuntimeHost.CreateButton(
                panel.transform,
                TitleActionName,
                string.Empty,
                font,
                new Vector2(20f, -18f),
                new Vector2(460f, 48f),
                new Vector2(0f, 1f),
                FirstUserGameTestButtonRole.Status);
            presenter._titleAction.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            presenter._titleAction.transition = Selectable.Transition.None;
            presenter._titleAction.targetGraphic.color = Color.clear;
            presenter._titleAction.targetGraphic.raycastTarget = false;
            Outline titleOutline = presenter._titleAction.GetComponent<Outline>();
            if (titleOutline != null)
            {
                titleOutline.enabled = false;
            }
            Navigation titleNavigation = presenter._titleAction.navigation;
            titleNavigation.mode = Navigation.Mode.None;
            presenter._titleAction.navigation = titleNavigation;
            presenter._titleLabel = presenter._titleAction.GetComponentInChildren<Text>(true);
            presenter._titleLabel.alignment = TextAnchor.MiddleLeft;
            presenter._titleLabel.fontSize = 23;
            presenter._titleLabel.fontStyle = FontStyle.Bold;
            presenter._titleLabel.color = new Color(0.94f, 0.96f, 0.98f, 1f);
            presenter._titleLabel.raycastTarget = false;

            presenter._objectiveAction = FirstUserGameTestRuntimeHost.CreateButton(
                panel.transform,
                ObjectiveActionName,
                string.Empty,
                font,
                new Vector2(20f, -78f),
                new Vector2(460f, 58f),
                new Vector2(0f, 1f),
                FirstUserGameTestButtonRole.Status);
            presenter._objectiveAction.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            presenter._objectiveLabel = presenter._objectiveAction.GetComponentInChildren<Text>(true);
            presenter._objectiveLabel.alignment = TextAnchor.MiddleLeft;
            RectTransform objectiveLabelRect = presenter._objectiveLabel.rectTransform;
            objectiveLabelRect.anchoredPosition = new Vector2(14f, 0f);
            objectiveLabelRect.sizeDelta = new Vector2(-28f, 0f);
            presenter._speakerLabel = FirstUserGameTestRuntimeHost.CreateText(
                panel.transform,
                "FirstUserGameTestOmenSpeaker",
                string.Empty,
                font,
                17,
                TextAnchor.MiddleLeft);
            RectTransform speakerRect = presenter._speakerLabel.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(0f, 1f);
            speakerRect.pivot = new Vector2(0f, 1f);
            speakerRect.anchoredPosition = new Vector2(24f, -150f);
            speakerRect.sizeDelta = new Vector2(452f, 30f);
            presenter._speakerLabel.color = new Color(0.90f, 0.72f, 0.34f, 1f);

            presenter._detail = FirstUserGameTestRuntimeHost.CreateText(
                panel.transform,
                DetailName,
                string.Empty,
                font,
                17,
                TextAnchor.UpperLeft);
            RectTransform detailRect = presenter._detail.rectTransform;
            detailRect.anchorMin = new Vector2(0f, 1f);
            detailRect.anchorMax = new Vector2(0f, 1f);
            detailRect.pivot = new Vector2(0f, 1f);
            detailRect.anchoredPosition = new Vector2(24f, -188f);
            detailRect.sizeDelta = new Vector2(452f, 104f);
            presenter._detail.color = new Color(0.83f, 0.88f, 0.93f, 1f);

            presenter._primaryResponseAction = FirstUserGameTestRuntimeHost.CreateButton(
                panel.transform,
                PrimaryResponseActionName,
                string.Empty,
                font,
                new Vector2(24f, -308f),
                new Vector2(452f, 50f),
                new Vector2(0f, 1f),
                FirstUserGameTestButtonRole.Primary);
            presenter._primaryResponseAction.GetComponent<RectTransform>().pivot =
                new Vector2(0f, 1f);
            presenter._secondaryResponseAction = FirstUserGameTestRuntimeHost.CreateButton(
                panel.transform,
                SecondaryResponseActionName,
                string.Empty,
                font,
                new Vector2(24f, -370f),
                new Vector2(452f, 50f),
                new Vector2(0f, 1f),
                FirstUserGameTestButtonRole.Choice);
            presenter._secondaryResponseAction.GetComponent<RectTransform>().pivot =
                new Vector2(0f, 1f);
            presenter._primaryResponseAction.onClick.AddListener(
                presenter.SelectPrimaryOmenResponse);
            presenter._secondaryResponseAction.onClick.AddListener(
                presenter.SelectSecondaryOmenResponse);

            presenter._objectiveAction.onClick.AddListener(presenter.OpenValeriusReport);
            presenter.RefreshPresentation();
            return true;
        }

        internal void Tick()
        {
            if (_failed)
            {
                return;
            }

            if (!TryRefreshState())
            {
                return;
            }

            if (!ApplyChampionControllerInputPolicy(_state.IsOmenOffered))
            {
                return;
            }

            if (_state.Step == FirstUserGameTestTutorialStep.Move)
            {
                if (!_movementIntentPending)
                {
                    Vector2 playerInput = new Vector2(
                        Input.GetAxisRaw("Horizontal"),
                        Input.GetAxisRaw("Vertical"));
                    RecordPlayerMovementIntent(playerInput);
                }

                if (_movementIntentPending && _controller != null)
                {
                    Vector3 delta = _controller.transform.position - _movementOrigin;
                    delta.y = 0f;
                    if (delta.magnitude >= MovementDistanceThreshold)
                    {
                        _movementIntentPending = false;
                        ApplyEvidence(FirstUserGameTestTutorialEvidenceKind.MovementConfirmed);
                    }
                }

                return;
            }

            _movementIntentPending = false;
            if (_state.Step == FirstUserGameTestTutorialStep.Complete)
            {
                _mouseAttackPending = false;
                RefreshPresentation();
                return;
            }

            if (_state.Step != FirstUserGameTestTutorialStep.BasicAttack)
            {
                _mouseAttackPending = false;
                return;
            }

            if (_mouseAttackPending)
            {
                _mouseAttackPending = false;
                if (TryReadChampionState(out bool locked, out bool attacking, out _) &&
                    !locked && attacking)
                {
                    ApplyEvidence(FirstUserGameTestTutorialEvidenceKind.BasicAttackConfirmed);
                }

                return;
            }

            if (Input.GetMouseButtonDown(0) &&
                TryReadChampionState(out bool controlsLocked, out bool isAttacking, out int realm) &&
                !controlsLocked && !isAttacking && realm != 0)
            {
                _mouseAttackPending = true;
            }
        }

        internal bool RecordPlayerMovementIntent(Vector2 direction)
        {
            if (_failed || !TryRefreshState() ||
                _state.Step != FirstUserGameTestTutorialStep.Move ||
                _controller == null ||
                direction.sqrMagnitude < 0.01f ||
                !TryReadChampionState(out bool locked, out bool attacking, out int realm) ||
                locked || attacking || realm == 0)
            {
                return false;
            }

            if (!_movementIntentPending)
            {
                _movementOrigin = _controller.transform.position;
                _movementIntentPending = true;
            }

            return true;
        }

        internal bool RequestPlayerBasicAttack()
        {
            if (_failed || !TryRefreshState() ||
                _state.Step != FirstUserGameTestTutorialStep.BasicAttack ||
                _controller == null ||
                !TryReadChampionState(out bool locked, out bool attackingBefore, out int realm) ||
                locked || attackingBefore || realm == 0)
            {
                return false;
            }

            _controller.RequestBasicAttack();
            if (!TryReadChampionState(out locked, out bool attackingAfter, out realm) ||
                locked || !attackingAfter || realm == 0)
            {
                return false;
            }

            return ApplyEvidence(FirstUserGameTestTutorialEvidenceKind.BasicAttackConfirmed);
        }

        internal bool EvaluateChampionControllerInputForTests(bool followUiActive)
        {
            return FirstUserGameTestIsolatedInputGate.AllowsChampionControllerProcessing(
                _state,
                followUiActive);
        }

        internal bool TryInspectChampionInputForTests(
            out bool controlsLocked,
            out bool isAttacking)
        {
            return TryReadChampionState(out controlsLocked, out isAttacking, out _);
        }

        private void OpenValeriusReport()
        {
            if (_failed || _objectiveAction == null ||
                !_objectiveAction.interactable || !TryRefreshState())
            {
                return;
            }

            if (!ApplyChampionControllerInputPolicy(followUiActive: true) ||
                !_championInputSuppressed || _controller.enabled ||
                !TryReadChampionState(out _, out bool isAttacking, out _) || isAttacking)
            {
                FailClosed("The offered objective could not suppress isolated gameplay input.");
                return;
            }

            Vector3 playerPosition = _controller == null
                ? Vector3.zero
                : _controller.transform.position;
            FirstUserGameTestTutorialState before = _state;
            string message = string.Empty;
            if (_omenOfferSession == null ||
                !_omenOfferSession.TryOpenReport(out _, out message))
            {
                FailClosed(string.IsNullOrEmpty(message)
                    ? "Valerius's report could not be opened."
                    : message);
                return;
            }

            if (!before.ValueEquals(_state) ||
                (_controller != null && _controller.transform.position != playerPosition))
            {
                FailClosed("Opening Valerius's report attempted to mutate the tutorial or player.");
                return;
            }

            RefreshPresentation();
        }

        private void SelectPrimaryOmenResponse()
        {
            if (_failed || _primaryResponseAction == null ||
                !_primaryResponseAction.interactable || _omenOfferSession == null)
            {
                return;
            }

            FirstUserGameTestOmenOfferView offer = _omenOfferSession.View;
            string message;
            bool applied;
            switch (offer.Stage)
            {
                case FirstUserGameTestOmenOfferStage.DeploymentReady:
                case FirstUserGameTestOmenOfferStage.RecoveryReady:
                    applied = _omenOfferSession.TryPrepareDeployment(out _, out message);
                    break;
                case FirstUserGameTestOmenOfferStage.DeploymentPrepared:
                    applied = _omenOfferSession.TryEnterEncounter(out _, out message);
                    break;
                case FirstUserGameTestOmenOfferStage.EncounterActive:
                    applied = _omenOfferSession.TryResolveEncounter(
                        NvsEncounterOutcome.Success,
                        out _,
                        out message);
                    break;
                case FirstUserGameTestOmenOfferStage.ReportReady:
                    applied = _omenOfferSession.TryOpenReport(out _, out message);
                    break;
                case FirstUserGameTestOmenOfferStage.RealmReady:
                    if (_exitAction == null || !_exitAction.interactable)
                    {
                        applied = false;
                        message = "The completed journey could not close safely.";
                    }
                    else
                    {
                        _exitAction.onClick.Invoke();
                        return;
                    }
                    break;
                default:
                    applied = _omenOfferSession.TrySelectChoice(
                        _primaryResponseChoiceKey,
                        out _,
                        out message);
                    break;
            }
            if (!applied)
            {
                FailClosed(string.IsNullOrEmpty(message)
                    ? "Valerius's response could not be applied."
                    : message);
                return;
            }

            RefreshPresentation();
        }

        private void SelectSecondaryOmenResponse()
        {
            if (_failed || _secondaryResponseAction == null ||
                !_secondaryResponseAction.interactable || _omenOfferSession == null)
            {
                return;
            }

            string message;
            bool applied = _omenOfferSession.View.CanResolveEncounter
                ? _omenOfferSession.TryResolveEncounter(
                    NvsEncounterOutcome.Cancelled,
                    out _,
                    out message)
                : _omenOfferSession.TrySelectChoice(
                    _secondaryResponseChoiceKey,
                    out _,
                    out message);
            if (!applied)
            {
                FailClosed(string.IsNullOrEmpty(message)
                    ? "Valerius's response could not be applied."
                    : message);
                return;
            }

            RefreshPresentation();
        }

        internal bool TryResolveEncounterForTests(NvsEncounterOutcome outcome)
        {
            string message = string.Empty;
            if (_failed || _omenOfferSession == null ||
                !_omenOfferSession.TryResolveEncounter(
                    outcome,
                    out _,
                    out message))
            {
                if (!string.IsNullOrEmpty(message))
                {
                    FailClosed(message);
                }

                return false;
            }

            RefreshPresentation();
            return true;
        }

        private bool ApplyEvidence(FirstUserGameTestTutorialEvidenceKind kind)
        {
            if (!_store.TryApply(
                    kind,
                    out FirstUserGameTestTutorialTransition transition,
                    out string message))
            {
                FailClosed(message);
                return false;
            }

            _state = transition.State;
            RefreshPresentation();
            return transition.Status == FirstUserGameTestTutorialTransitionStatus.Applied;
        }

        private bool TryRefreshState()
        {
            string message = string.Empty;
            if (_store != null && _store.TryLoadOrCreate(out _state, out message))
            {
                return true;
            }

            FailClosed(string.IsNullOrEmpty(message)
                ? "The retained tutorial state was unavailable."
                : message);
            return false;
        }

        private bool TryReadChampionState(
            out bool controlsLocked,
            out bool isAttacking,
            out int realm)
        {
            controlsLocked = true;
            isAttacking = false;
            realm = 0;
            if (_controller == null || IsAttackingField == null ||
                ControlsLockedField == null || RealmField == null ||
                IsAttackingField.FieldType != typeof(bool) ||
                ControlsLockedField.FieldType != typeof(bool) ||
                !RealmField.FieldType.IsEnum)
            {
                return false;
            }

            try
            {
                controlsLocked = (bool)ControlsLockedField.GetValue(_controller);
                isAttacking = (bool)IsAttackingField.GetValue(_controller);
                realm = Convert.ToInt32(
                    RealmField.GetValue(_controller),
                    System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidCastException ||
                exception is TargetException ||
                exception is TargetInvocationException)
            {
                return false;
            }
        }

        private bool ApplyChampionControllerInputPolicy(bool followUiActive)
        {
            if (_controller == null)
            {
                FailClosed("The isolated Champion input boundary was unavailable.");
                return false;
            }

            bool allowsProcessing =
                FirstUserGameTestIsolatedInputGate.AllowsChampionControllerProcessing(
                    _state,
                    followUiActive);
            if (allowsProcessing)
            {
                if (_championInputSuppressed || !_controller.enabled)
                {
                    FailClosed("The isolated Champion input boundary changed unexpectedly.");
                    return false;
                }

                return true;
            }

            if (!FirstUserGameTestTutorialPlanner.IsValidState(_state) ||
                !_state.IsOmenOffered || !followUiActive)
            {
                FailClosed("The isolated Champion input boundary rejected an invalid state.");
                return false;
            }

            if (!_championInputSuppressed)
            {
                _controller.enabled = false;
                _championInputSuppressed = true;
            }

            if (_controller.enabled)
            {
                FailClosed("The isolated Champion input boundary could not be verified.");
                return false;
            }

            return true;
        }

        private void RefreshPresentation()
        {
            if (_titleAction == null || _objectiveAction == null ||
                _titleLabel == null || _objectiveLabel == null ||
                _speakerLabel == null || _detail == null || _omenOfferSession == null)
            {
                FailClosed("The development tutorial presentation was incomplete.");
                return;
            }

            bool offered = _state != null && _state.IsOmenOffered;
            _signalEdge.gameObject.SetActive(offered);
            if (!ApplyChampionControllerInputPolicy(offered))
            {
                return;
            }

            bool offeredReady = offered &&
                                _championInputSuppressed &&
                                _controller != null &&
                                !_controller.enabled &&
                                TryReadChampionState(out _, out bool isAttacking, out _) &&
                                !isAttacking;

            FirstUserGameTestOmenOfferView offer = _omenOfferSession.View;
            _signalEdge.color = offer.IsJourneyComplete
                ? new Color(0.36f, 0.78f, 0.52f, 1f)
                : offer.Stage == FirstUserGameTestOmenOfferStage.DeploymentPrepared ||
                  offer.Stage == FirstUserGameTestOmenOfferStage.EncounterActive ||
                  offer.Stage == FirstUserGameTestOmenOfferStage.RecoveryReady
                    ? new Color(0.28f, 0.74f, 0.92f, 1f)
                    : new Color(0.88f, 0.67f, 0.24f, 1f);
            FirstUserGameTestOmenUiState omenUiState =
                ResolveOmenUiState(offeredReady, offer);

            if (!FirstUserGameTestTutorialInteractionPlan.TryCreate(
                    _state,
                    omenUiState,
                    out FirstUserGameTestTutorialInteractionPlan interaction))
            {
                FailClosed("The development tutorial interaction plan was invalid.");
                return;
            }

            RectTransform panelRect = transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.sizeDelta = new Vector2(
                    panelRect.sizeDelta.x,
                    _state.Step == FirstUserGameTestTutorialStep.Complete
                        ? OmenPanelHeight
                        : TutorialPanelHeight);
            }

            foreach (Button moveAction in _moveActions)
            {
                moveAction.gameObject.SetActive(interaction.MovementEnabled);
                moveAction.interactable = interaction.MovementEnabled;
                FirstUserGameTestRuntimeHost.ApplyButtonRole(
                    moveAction,
                    interaction.MovementEmphasized
                        ? FirstUserGameTestButtonRole.ActiveTask
                        : FirstUserGameTestButtonRole.Secondary);
            }
            if (_attackAction != null)
            {
                _attackAction.gameObject.SetActive(interaction.AttackEnabled);
                _attackAction.interactable = interaction.AttackEnabled;
                FirstUserGameTestRuntimeHost.ApplyButtonRole(
                    _attackAction,
                    interaction.AttackEnabled
                        ? FirstUserGameTestButtonRole.ActiveTask
                        : FirstUserGameTestButtonRole.Secondary);
            }
            _titleAction.interactable = false;
            _objectiveAction.interactable = interaction.ObjectiveActionable;
            _objectiveAction.targetGraphic.raycastTarget = interaction.ObjectiveActionable;
            FirstUserGameTestRuntimeHost.ApplyButtonRole(
                _objectiveAction,
                interaction.ObjectiveRole);
            if (!interaction.ObjectiveActionable &&
                interaction.ObjectiveRole == FirstUserGameTestButtonRole.Status)
            {
                _objectiveAction.transition = Selectable.Transition.None;
                _objectiveAction.targetGraphic.color =
                    new Color(0.08f, 0.12f, 0.15f, 0.88f);
            }
            RefreshResponseActions(interaction, offer);
            RefreshNavigation(interaction);
            if (_state == null)
            {
                _titleLabel.text = "First Steps";
                _objectiveLabel.text = "Tutorial unavailable";
                _speakerLabel.text = string.Empty;
                _detail.text = "Exit the isolated playtest and review the Console.";
                return;
            }

            switch (_state.Step)
            {
                case FirstUserGameTestTutorialStep.Move:
                    _titleLabel.text = FirstUserGameTestPlaytestCopy.MoveTitle;
                    _objectiveLabel.text = FirstUserGameTestPlaytestCopy.MoveObjective;
                    _speakerLabel.text = string.Empty;
                    _detail.text = FirstUserGameTestPlaytestCopy.MoveDetail;
                    TryFocusCurrentStep(interaction);
                    break;
                case FirstUserGameTestTutorialStep.BasicAttack:
                    _titleLabel.text = FirstUserGameTestPlaytestCopy.AttackTitle;
                    _objectiveLabel.text = FirstUserGameTestPlaytestCopy.AttackObjective;
                    _speakerLabel.text = string.Empty;
                    _detail.text = FirstUserGameTestPlaytestCopy.AttackDetail;
                    TryFocusCurrentStep(interaction);
                    break;
                case FirstUserGameTestTutorialStep.Complete:
                    _titleLabel.text = offer.IsOpened || offer.CanReopen || offer.IsJourneyComplete
                        ? offer.Title
                        : FirstUserGameTestPlaytestCopy.OmenTitle;
                    _objectiveLabel.text = ResolveOmenObjectiveCopy(offer);
                    _speakerLabel.text = ResolveOmenSpeakerCopy(offer);
                    _detail.text = !offeredReady
                        ? "Preparing Valerius's report…"
                        : ResolveOmenDetailCopy(offer);
                    TryFocusCurrentStep(interaction);
                    break;
                default:
                    FailClosed("The development tutorial step was invalid.");
                    break;
            }
        }

        private static FirstUserGameTestOmenUiState ResolveOmenUiState(
            bool offeredReady,
            FirstUserGameTestOmenOfferView offer)
        {
            if (!offeredReady || offer == null)
            {
                return FirstUserGameTestOmenUiState.Preparing;
            }

            if (offer.IsJourneyComplete)
            {
                return FirstUserGameTestOmenUiState.Complete;
            }

            if (!offer.IsOpened || offer.CanReopen)
            {
                return FirstUserGameTestOmenUiState.ReadyToOpen;
            }

            return FirstUserGameTestOmenUiState.AwaitingResponse;
        }

        private static string ResolveOmenObjectiveCopy(
            FirstUserGameTestOmenOfferView offer)
        {
            if (offer == null || offer.Stage == FirstUserGameTestOmenOfferStage.Closed)
            {
                return FirstUserGameTestPlaytestCopy.OmenObjective;
            }

            if (offer.CanReopen)
            {
                return FirstUserGameTestPlaytestCopy.OmenReopenAction;
            }

            if (offer.IsJourneyComplete)
            {
                return FirstUserGameTestPlaytestCopy.RealmReadyStatus;
            }

            switch (offer.Stage)
            {
                case FirstUserGameTestOmenOfferStage.DeploymentReady:
                    return FirstUserGameTestPlaytestCopy.OmenDeploymentReadyStatus;
                case FirstUserGameTestOmenOfferStage.DeploymentPrepared:
                    return FirstUserGameTestPlaytestCopy.OmenDeploymentStatus;
                case FirstUserGameTestOmenOfferStage.EncounterActive:
                    return FirstUserGameTestPlaytestCopy.EncounterStatus;
                case FirstUserGameTestOmenOfferStage.RecoveryReady:
                    return FirstUserGameTestPlaytestCopy.RecoveryStatus;
                case FirstUserGameTestOmenOfferStage.ReportReady:
                    return FirstUserGameTestPlaytestCopy.ReportReadyStatus;
                default:
                    return FirstUserGameTestPlaytestCopy.OmenOpenedStatus;
            }
        }

        private static string ResolveOmenDetailCopy(
            FirstUserGameTestOmenOfferView offer)
        {
            if (offer == null || offer.Stage == FirstUserGameTestOmenOfferStage.Closed)
            {
                return FirstUserGameTestPlaytestCopy.OmenDetail;
            }

            if (offer.CanReopen)
            {
                return FirstUserGameTestPlaytestCopy.OmenDeclinedDetail;
            }

            if (offer.IsJourneyComplete)
            {
                return FirstUserGameTestPlaytestCopy.RealmReadyDetail;
            }

            switch (offer.Stage)
            {
                case FirstUserGameTestOmenOfferStage.DeploymentPrepared:
                    return FirstUserGameTestPlaytestCopy.OmenDeploymentDetail;
                case FirstUserGameTestOmenOfferStage.EncounterActive:
                    return FirstUserGameTestPlaytestCopy.EncounterDetail;
                case FirstUserGameTestOmenOfferStage.RecoveryReady:
                    return FirstUserGameTestPlaytestCopy.RecoveryDetail;
                case FirstUserGameTestOmenOfferStage.ReportReady:
                    return FirstUserGameTestPlaytestCopy.ReportReadyDetail;
                default:
                    return offer.Dialogue;
            }
        }

        private static string ResolveOmenSpeakerCopy(
            FirstUserGameTestOmenOfferView offer)
        {
            if (offer == null)
            {
                return "Veil Watch dispatch";
            }

            switch (offer.Stage)
            {
                case FirstUserGameTestOmenOfferStage.DeploymentPrepared:
                case FirstUserGameTestOmenOfferStage.EncounterActive:
                case FirstUserGameTestOmenOfferStage.RecoveryReady:
                    return "Sky Castle  •  Journey checkpoint";
                case FirstUserGameTestOmenOfferStage.RealmReady:
                    return "Veil Watch  •  Realm secured";
                default:
                    return string.IsNullOrWhiteSpace(offer.SpeakerLine)
                        ? "Veil Watch dispatch"
                        : offer.SpeakerLine;
            }
        }

        private void RefreshResponseActions(
            FirstUserGameTestTutorialInteractionPlan interaction,
            FirstUserGameTestOmenOfferView offer)
        {
            if (_primaryResponseAction == null || _secondaryResponseAction == null)
            {
                return;
            }

            _primaryResponseChoiceKey = string.Empty;
            _secondaryResponseChoiceKey = string.Empty;
            bool showPrimary = interaction.ResponseActionable &&
                               offer != null &&
                               offer.HasPrimaryAction;
            bool showSecondary = showPrimary && offer.HasSecondaryAction;

            _primaryResponseAction.gameObject.SetActive(showPrimary);
            _secondaryResponseAction.gameObject.SetActive(showSecondary);
            _primaryResponseAction.interactable = showPrimary;
            _secondaryResponseAction.interactable = showSecondary;
            if (!showPrimary)
            {
                DisableNavigation(_primaryResponseAction);
                DisableNavigation(_secondaryResponseAction);
                return;
            }

            Text primaryLabel = _primaryResponseAction.GetComponentInChildren<Text>(true);
            if (offer.Choices.Count == 0)
            {
                primaryLabel.text = offer.PrimaryActionLabel;
            }
            else
            {
                _primaryResponseChoiceKey = offer.Choices[0].Key;
                primaryLabel.text = offer.Choices[0].Label;
            }

            FirstUserGameTestRuntimeHost.ApplyButtonRole(
                _primaryResponseAction,
                offer.Choices.Count <= 1
                    ? FirstUserGameTestButtonRole.Primary
                    : FirstUserGameTestButtonRole.Choice);

            if (showSecondary)
            {
                Text secondaryLabel =
                    _secondaryResponseAction.GetComponentInChildren<Text>(true);
                if (offer.Choices.Count > 1)
                {
                    _secondaryResponseChoiceKey = offer.Choices[1].Key;
                    secondaryLabel.text = offer.Choices[1].Label;
                }
                else
                {
                    secondaryLabel.text = offer.SecondaryActionLabel;
                }
                FirstUserGameTestRuntimeHost.ApplyButtonRole(
                    _secondaryResponseAction,
                    FirstUserGameTestButtonRole.Choice);
            }
        }

        private void RefreshNavigation(FirstUserGameTestTutorialInteractionPlan interaction)
        {
            Navigation noNavigation = _titleAction.navigation;
            noNavigation.mode = Navigation.Mode.None;
            _titleAction.navigation = noNavigation;

            if (_moveActions.Length != 4 || _exitAction == null)
            {
                return;
            }

            Button moveLeft = _moveActions[0];
            Button moveRight = _moveActions[1];
            Button moveForward = _moveActions[2];
            Button moveBack = _moveActions[3];

            if (interaction.ObjectiveActionable)
            {
                DisableNavigation(moveLeft);
                DisableNavigation(moveRight);
                DisableNavigation(moveForward);
                DisableNavigation(moveBack);
                DisableNavigation(_attackAction);
                FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                    _objectiveAction,
                    _exitAction,
                    _exitAction,
                    _exitAction,
                    _exitAction);
                FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                    _exitAction,
                    _objectiveAction,
                    _objectiveAction,
                    _objectiveAction,
                    _objectiveAction);
                return;
            }

            DisableNavigation(_objectiveAction);
            if (interaction.ResponseActionable &&
                _primaryResponseAction != null &&
                _primaryResponseAction.gameObject.activeSelf)
            {
                DisableNavigation(moveLeft);
                DisableNavigation(moveRight);
                DisableNavigation(moveForward);
                DisableNavigation(moveBack);
                DisableNavigation(_attackAction);
                Selectable secondOrExit =
                    _secondaryResponseAction != null &&
                    _secondaryResponseAction.gameObject.activeSelf
                        ? _secondaryResponseAction
                        : _exitAction;
                FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                    _primaryResponseAction,
                    _exitAction,
                    secondOrExit,
                    _exitAction,
                    secondOrExit);
                if (secondOrExit == _secondaryResponseAction)
                {
                    FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                        _secondaryResponseAction,
                        _primaryResponseAction,
                        _exitAction,
                        _primaryResponseAction,
                        _exitAction);
                }
                FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                    _exitAction,
                    secondOrExit,
                    _primaryResponseAction,
                    secondOrExit,
                    _primaryResponseAction);
                return;
            }

            if (!interaction.MovementEnabled)
            {
                DisableNavigation(moveLeft);
                DisableNavigation(moveRight);
                DisableNavigation(moveForward);
                DisableNavigation(moveBack);
                DisableNavigation(_attackAction);
                FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                    _exitAction,
                    _exitAction,
                    _exitAction,
                    _exitAction,
                    _exitAction);
                return;
            }

            Selectable attackOrExit = interaction.AttackEnabled
                ? _attackAction
                : _exitAction;
            FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                moveLeft,
                _exitAction,
                moveForward,
                moveForward,
                moveBack);
            FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                moveForward,
                moveLeft,
                moveRight,
                _exitAction,
                moveBack);
            FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                moveRight,
                moveForward,
                attackOrExit,
                _exitAction,
                moveBack);
            FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                moveBack,
                moveLeft,
                moveRight,
                moveForward,
                attackOrExit);
            if (interaction.AttackEnabled)
            {
                FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                    _attackAction,
                    moveBack,
                    _exitAction,
                    moveRight,
                    _exitAction);
            }
            else
            {
                DisableNavigation(_attackAction);
            }

            FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                _exitAction,
                attackOrExit,
                moveLeft,
                moveForward,
                moveForward);
        }

        private static void DisableNavigation(Selectable selectable)
        {
            if (selectable == null)
            {
                return;
            }

            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            navigation.selectOnUp = null;
            navigation.selectOnDown = null;
            selectable.navigation = navigation;
        }

        private void TryFocusCurrentStep(
            FirstUserGameTestTutorialInteractionPlan interaction)
        {
            if (_state == null || EventSystem.current == null)
            {
                return;
            }

            if (interaction.FocusTarget == FirstUserGameTestTutorialFocusTarget.Move &&
                !_moveFocusApplied && _moveAction != null && _moveAction.interactable)
            {
                EventSystem.current.SetSelectedGameObject(_moveAction.gameObject);
                _moveFocusApplied = true;
                return;
            }

            if (interaction.FocusTarget == FirstUserGameTestTutorialFocusTarget.Attack &&
                !_attackFocusApplied && _attackAction != null && _attackAction.interactable)
            {
                EventSystem.current.SetSelectedGameObject(_attackAction.gameObject);
                _attackFocusApplied = true;
                return;
            }

            if (interaction.FocusTarget == FirstUserGameTestTutorialFocusTarget.Report &&
                _objectiveAction.interactable &&
                _omenOfferSession != null &&
                _focusedObjectiveRevision != _omenOfferSession.Snapshot.Revision)
            {
                EventSystem.current.SetSelectedGameObject(_objectiveAction.gameObject);
                _focusedObjectiveRevision = _omenOfferSession.Snapshot.Revision;
                return;
            }

            if (interaction.FocusTarget == FirstUserGameTestTutorialFocusTarget.Response &&
                _primaryResponseAction != null &&
                _primaryResponseAction.gameObject.activeSelf &&
                _primaryResponseAction.interactable &&
                _omenOfferSession != null &&
                (_focusedResponseRevision != _omenOfferSession.Snapshot.Revision ||
                 _focusedResponseStage != _omenOfferSession.View.Stage))
            {
                EventSystem.current.SetSelectedGameObject(
                    _primaryResponseAction.gameObject);
                _focusedResponseRevision = _omenOfferSession.Snapshot.Revision;
                _focusedResponseStage = _omenOfferSession.View.Stage;
                return;
            }

            if (interaction.FocusTarget == FirstUserGameTestTutorialFocusTarget.Exit &&
                _exitAction != null &&
                _omenOfferSession != null &&
                _focusedCompletionRevision != _omenOfferSession.Snapshot.Revision)
            {
                EventSystem.current.SetSelectedGameObject(_exitAction.gameObject);
                _focusedCompletionRevision = _omenOfferSession.Snapshot.Revision;
            }
        }

        private void FailClosed(string message)
        {
            if (_failed)
            {
                return;
            }

            _failed = true;
            _controller?.SetControlLocked(true);
            _failClosed?.Invoke(string.IsNullOrEmpty(message)
                ? "The development tutorial failed closed."
                : message);
        }

        private void OnDestroy()
        {
            if (_objectiveAction != null)
            {
                _objectiveAction.onClick.RemoveListener(OpenValeriusReport);
            }
            if (_primaryResponseAction != null)
            {
                _primaryResponseAction.onClick.RemoveListener(SelectPrimaryOmenResponse);
            }
            if (_secondaryResponseAction != null)
            {
                _secondaryResponseAction.onClick.RemoveListener(SelectSecondaryOmenResponse);
            }

            _controller = null;
            _store = null;
            _omenOfferSession = null;
            _failClosed = null;
            _state = null;
            _signalEdge = null;
            _moveAction = null;
            _moveActions = Array.Empty<Button>();
            _attackAction = null;
            _exitAction = null;
            _primaryResponseAction = null;
            _secondaryResponseAction = null;
            _primaryResponseChoiceKey = string.Empty;
            _secondaryResponseChoiceKey = string.Empty;
        }
    }
}
