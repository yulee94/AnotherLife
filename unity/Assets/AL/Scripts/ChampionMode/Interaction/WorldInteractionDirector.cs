using System;
using System.Collections.Generic;
using AL.ChampionMode.Control;
using AL.ChampionMode.UI;
using AL.Input;
using UnityEngine;

namespace AL.ChampionMode.Interaction
{
    public sealed class WorldInteractionDirector : MonoBehaviour
    {
        public const float ConfirmationFeedbackSeconds = 2.5f;

        private readonly List<WorldInteractable> _targets = new List<WorldInteractable>(8);
        private readonly List<WorldInteractionCandidate> _candidates = new List<WorldInteractionCandidate>(8);
        private readonly List<WorldInteractable> _candidateTargets = new List<WorldInteractable>(8);
        private Transform _actor;
        private ChampionCombat _combat;
        private ChampionController _controller;
        private UnityEngine.Camera _camera;
        private WorldInteractionPromptView _prompt;
        private WorldInteractable _focused;
        private float _feedbackVisibleUntil;

        public WorldInteractable Focused => _focused;
        public Transform Actor => _actor;
        public string LastFeedback { get; private set; } = string.Empty;
        public event Action<WorldInteractionResult> Confirmed;

        public void Configure(Transform actor, UnityEngine.Camera camera, WorldInteractionPromptView prompt)
        {
            _prompt?.Hide();
            _focused = null;
            LastFeedback = string.Empty;
            _feedbackVisibleUntil = 0f;
            _actor = actor;
            _combat = actor != null ? actor.GetComponent<ChampionCombat>() : null;
            _controller = actor != null ? actor.GetComponent<ChampionController>() : null;
            _camera = camera;
            _prompt = prompt;
            _prompt?.Hide();
        }

        public void Register(WorldInteractable target)
        {
            if (target != null && !_targets.Contains(target))
            {
                _targets.Add(target);
            }
        }

        public bool TryConfirmFocused()
        {
            WorldInteractable offeredTarget = _focused;
            RefreshFocus();
            // UI callbacks may arrive after movement, modal changes, or disablement.
            // A stale tap must not silently confirm a different newly focused object.
            if (offeredTarget == null || offeredTarget != _focused)
            {
                return false;
            }

            WorldInteractionResult result = offeredTarget.Confirm(actorAvailable: true);
            if (!result.Accepted)
            {
                return false;
            }

            _focused = null;
            LastFeedback = result.Feedback ?? string.Empty;
            if (!string.IsNullOrEmpty(LastFeedback))
            {
                _feedbackVisibleUntil = Time.unscaledTime +
                                        ConfirmationFeedbackSeconds;
                _prompt?.ShowFeedback(LastFeedback);
            }
            else
            {
                _prompt?.Hide();
            }
            Confirmed?.Invoke(result);
            return true;
        }

        private void Update()
        {
            RefreshFocus();
            if (GameInput.InteractPressed())
            {
                TryConfirmFocused();
            }
        }

        private void RefreshFocus()
        {
            _focused = null;
            _candidates.Clear();
            _candidateTargets.Clear();
            if (!isActiveAndEnabled ||
                _actor == null || !_actor.gameObject.activeInHierarchy ||
                GameInput.GameplaySuppressed ||
                ChampionHudCameraGate.BlocksGameplay ||
                ChampionHudCameraGate.MenuOpen || ChampionHudCameraGate.RecapOpen ||
                (_controller != null && _controller.BlocksGameplayEntry) ||
                (_combat != null && (!_combat.isActiveAndEnabled || _combat.IsDead)))
            {
                _prompt?.Hide();
                return;
            }

            if (!string.IsNullOrEmpty(LastFeedback) &&
                Time.unscaledTime < _feedbackVisibleUntil)
            {
                _prompt?.ShowFeedback(LastFeedback);
                return;
            }

            Vector3 origin = _actor.position + Vector3.up * 1.15f;
            Vector3 forward = _camera != null ? _camera.transform.forward : _actor.forward;
            for (int i = 0; i < _targets.Count; i++)
            {
                WorldInteractable target = _targets[i];
                if (target == null || !target.isActiveAndEnabled)
                {
                    continue;
                }

                _candidates.Add(target.ToCandidate());
                _candidateTargets.Add(target);
            }

            if (!WorldInteractionFocus.TrySelect(origin, forward, _candidates, out int index))
            {
                _prompt?.Hide();
                return;
            }

            _focused = _candidateTargets[index];

            _prompt?.Show(
                WorldInteractionPromptCopy.Compose(
                    WorldInteractionPromptCopy.InteractGlyph,
                    _focused.Kind,
                    _focused.CatalogId));
        }

        private void OnDisable()
        {
            _focused = null;
            _prompt?.Hide();
        }
    }
}
