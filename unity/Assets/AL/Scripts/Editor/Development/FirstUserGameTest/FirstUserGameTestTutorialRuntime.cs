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
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.Editor.Development.FirstUserGameTest
{
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

        private const float MovementDistanceThreshold = 0.02f;

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
        private Action<string> _failClosed;
        private FirstUserGameTestTutorialState _state;
        private Button _titleAction;
        private Button _objectiveAction;
        private Text _titleLabel;
        private Text _objectiveLabel;
        private Text _detail;
        private bool _movementIntentPending;
        private Vector3 _movementOrigin;
        private bool _mouseAttackPending;
        private bool _followTargetAvailable = true;
        private bool _offeredFocusApplied;
        private bool _championInputSuppressed;
        private bool _failed;
        private FirstUserGameTestFollowResult _lastFollowResult;

        internal FirstUserGameTestTutorialState State => _state;
        internal Button TitleAction => _titleAction;
        internal Button ObjectiveAction => _objectiveAction;
        internal Text Detail => _detail;
        internal FirstUserGameTestFollowResult LastFollowResult => _lastFollowResult;
        internal bool ChampionInputSuppressed => _championInputSuppressed;
        internal bool MovementIntentPendingForTests => _movementIntentPending;

        internal static bool TryCreate(
            Transform parent,
            Font font,
            ChampionController controller,
            FirstUserGameTestTutorialSessionStore store,
            Action<string> failClosed,
            out FirstUserGameTestTutorialPresenter presenter,
            out string message)
        {
            presenter = null;
            message = string.Empty;
            if (parent == null || font == null || controller == null || store == null ||
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
            panelRect.sizeDelta = new Vector2(460f, 236f);
            panel.GetComponent<Image>().color = new Color(0.025f, 0.045f, 0.075f, 0.94f);

            presenter = panel.GetComponent<FirstUserGameTestTutorialPresenter>();
            presenter._controller = controller;
            presenter._store = store;
            presenter._failClosed = failClosed;
            presenter._state = state;

            presenter._titleAction = FirstUserGameTestRuntimeHost.CreateButton(
                panel.transform,
                TitleActionName,
                string.Empty,
                font,
                new Vector2(16f, -16f),
                new Vector2(428f, 54f),
                new Vector2(0f, 1f));
            presenter._titleAction.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            presenter._titleLabel = presenter._titleAction.GetComponentInChildren<Text>(true);

            presenter._objectiveAction = FirstUserGameTestRuntimeHost.CreateButton(
                panel.transform,
                ObjectiveActionName,
                string.Empty,
                font,
                new Vector2(16f, -82f),
                new Vector2(428f, 62f),
                new Vector2(0f, 1f));
            presenter._objectiveAction.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            presenter._objectiveLabel = presenter._objectiveAction.GetComponentInChildren<Text>(true);

            presenter._detail = FirstUserGameTestRuntimeHost.CreateText(
                panel.transform,
                DetailName,
                string.Empty,
                font,
                16,
                TextAnchor.MiddleLeft);
            RectTransform detailRect = presenter._detail.rectTransform;
            detailRect.anchorMin = new Vector2(0f, 1f);
            detailRect.anchorMax = new Vector2(0f, 1f);
            detailRect.pivot = new Vector2(0f, 1f);
            detailRect.anchoredPosition = new Vector2(18f, -158f);
            detailRect.sizeDelta = new Vector2(424f, 62f);
            presenter._detail.color = new Color(0.78f, 0.86f, 0.95f, 1f);

            presenter._titleAction.onClick.AddListener(presenter.FollowActiveObjective);
            presenter._objectiveAction.onClick.AddListener(presenter.FollowActiveObjective);
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

        internal void SetFollowTargetAvailableForTests(bool available)
        {
            _followTargetAvailable = available;
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

        private void FollowActiveObjective()
        {
            if (_failed || !TryRefreshState())
            {
                _lastFollowResult = new FirstUserGameTestFollowResult(
                    FirstUserGameTestFollowOutcome.Unavailable,
                    FirstUserGameTestTutorialContract.ActiveObjectiveUnavailableResultId);
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
            _lastFollowResult = FirstUserGameTestFollowPlanner.Plan(
                _state,
                FirstUserGameTestTutorialContract.FollowActiveObjectiveActionId,
                _followTargetAvailable && _detail != null);

            if (_lastFollowResult.Outcome == FirstUserGameTestFollowOutcome.Focused)
            {
                _detail.text =
                    "Development-only offer detail is focused. Nothing was accepted, moved, or rewarded.";
                EventSystem.current?.SetSelectedGameObject(_objectiveAction.gameObject);
            }
            else if (_lastFollowResult.Outcome == FirstUserGameTestFollowOutcome.NoTarget)
            {
                _detail.text = "No safe development detail target is available.";
            }

            if (!before.ValueEquals(_state) ||
                (_controller != null && _controller.transform.position != playerPosition))
            {
                FailClosed("Following the active objective attempted to mutate gameplay state.");
            }
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
                _titleLabel == null || _objectiveLabel == null || _detail == null)
            {
                FailClosed("The development tutorial presentation was incomplete.");
                return;
            }

            bool offered = _state != null && _state.IsOmenOffered;
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
            _titleAction.interactable = offeredReady;
            _objectiveAction.interactable = offeredReady;
            if (_state == null)
            {
                _titleLabel.text = "DEVELOPMENT TUTORIAL — UNAVAILABLE";
                _objectiveLabel.text = "Objective unavailable";
                _detail.text = "The isolated tutorial state is unavailable.";
                return;
            }

            switch (_state.Step)
            {
                case FirstUserGameTestTutorialStep.Move:
                    _titleLabel.text = "DEVELOPMENT TUTORIAL — NONPRODUCTION";
                    _objectiveLabel.text = "MOVE — move the character";
                    _detail.text = "Use player movement input. Elapsed time cannot complete this step.";
                    break;
                case FirstUserGameTestTutorialStep.BasicAttack:
                    _titleLabel.text = "DEVELOPMENT TUTORIAL — NONPRODUCTION";
                    _objectiveLabel.text = "BASIC ATTACK — submit one common attack";
                    _detail.text = "A hit, target, damage, kill, or reward is not required.";
                    break;
                case FirstUserGameTestTutorialStep.Complete:
                    _titleLabel.text = "DEVELOPMENT MAIN QUEST OFFER";
                    _objectiveLabel.text = "Review the offered objective";
                    _detail.text = offeredReady
                        ? "Offered only — not accepted, progressed, completed, or rewarded."
                        : "Finishing the accepted tutorial attack before enabling the offer controls.";
                    if (offeredReady && !_offeredFocusApplied && EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(_titleAction.gameObject);
                        _offeredFocusApplied = true;
                    }

                    break;
                default:
                    FailClosed("The development tutorial step was invalid.");
                    break;
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
            if (_titleAction != null)
            {
                _titleAction.onClick.RemoveListener(FollowActiveObjective);
            }

            if (_objectiveAction != null)
            {
                _objectiveAction.onClick.RemoveListener(FollowActiveObjective);
            }

            _controller = null;
            _store = null;
            _failClosed = null;
            _state = null;
        }
    }
}
