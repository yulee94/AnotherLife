using System;
using System.Collections.Generic;
using AL.ChampionMode.Control;
using AL.Input;
using UnityEngine;

namespace AL.ChampionMode.Interaction
{
    public sealed class WorldInteractionDirector : MonoBehaviour
    {
        public const float ConfirmationFeedbackSeconds = 2.5f;

        private readonly List<WorldInteractable> _targets = new List<WorldInteractable>(8);
        private readonly List<WorldInteractionCandidate> _candidates = new List<WorldInteractionCandidate>(8);
        private Transform _actor;
        private ChampionCombat _combat;
        private UnityEngine.Camera _camera;
        private WorldInteractionPromptView _prompt;
        private WorldInteractable _focused;
        private float _feedbackVisibleUntil;

        public WorldInteractable Focused => _focused;
        public string LastFeedback { get; private set; } = string.Empty;
        public event Action<WorldInteractionResult> Confirmed;

        public void Configure(Transform actor, UnityEngine.Camera camera, WorldInteractionPromptView prompt)
        {
            _actor = actor;
            _combat = actor != null ? actor.GetComponent<ChampionCombat>() : null;
            _camera = camera;
            _prompt = prompt;
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
            if (_focused == null)
            {
                return false;
            }

            bool actorAvailable = _combat == null || !_combat.IsDead;
            WorldInteractionResult result = _focused.Confirm(actorAvailable);
            if (!result.Accepted)
            {
                return false;
            }

            LastFeedback = result.Feedback ?? string.Empty;
            if (!string.IsNullOrEmpty(LastFeedback))
            {
                _feedbackVisibleUntil = Time.unscaledTime +
                                        ConfirmationFeedbackSeconds;
                _prompt?.ShowFeedback(LastFeedback);
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
            if (_actor == null)
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
                if (target == null)
                {
                    continue;
                }

                _candidates.Add(target.ToCandidate());
            }

            if (!WorldInteractionFocus.TrySelect(origin, forward, _candidates, out int index))
            {
                _prompt?.Hide();
                return;
            }

            string catalogId = _candidates[index].CatalogId;
            for (int i = 0; i < _targets.Count; i++)
            {
                WorldInteractable target = _targets[i];
                if (target != null && target.CatalogId == catalogId)
                {
                    _focused = target;
                    break;
                }
            }

            if (_focused == null)
            {
                _prompt?.Hide();
                return;
            }

            _prompt?.Show(
                WorldInteractionPromptCopy.Compose(
                    WorldInteractionPromptCopy.InteractGlyph,
                    _focused.Kind,
                    _focused.CatalogId));
        }
    }
}
