using System;
using AL.Core.Interfaces;
using AL.Services.Local;
using AL.UI.QuestHud;
using UnityEngine;

namespace AL.UI.Kingdom
{
    public static class KingdomTeachingInteraction
    {
        public static event Action<string> InteractionRequested;
        public static event Action<string> InteractionObserved;

        public static void Request(string interaction)
        {
            if (!string.IsNullOrWhiteSpace(interaction))
            {
                InteractionRequested?.Invoke(interaction);
            }
        }

        public static void Observe(string interaction)
        {
            if (!string.IsNullOrWhiteSpace(interaction))
            {
                InteractionObserved?.Invoke(interaction);
            }
        }

        public static void ResetForTests()
        {
            InteractionRequested = null;
            InteractionObserved = null;
        }
    }

    /// <summary>
    /// Binds the current catalog-driven private-kingdom teaching step to the
    /// shared Quest HUD. Manual primary taps and Auto Quest use the same typed
    /// interaction/completion path; construction cannot advance until the live
    /// Town Hall build is present in the save candidate.
    /// </summary>
    public sealed class KingdomTeachingDirector : MonoBehaviour
    {
        private ISaveGameService _saveGameService;
        private KingdomTeachingCatalog _catalog;

        public KingdomTeachingState State { get; private set; }
        public QuestHudOverlay Hud { get; private set; }

        private void OnEnable()
        {
            KingdomTeachingInteraction.InteractionObserved += HandleInteractionObserved;
        }

        private void OnDisable()
        {
            KingdomTeachingInteraction.InteractionObserved -= HandleInteractionObserved;
        }

        public void EnsureReady(
            ISaveGameService saveGameService,
            QuestHudOverlay hud,
            KingdomTeachingCatalog catalog = null)
        {
            KingdomTeachingInteraction.InteractionObserved -= HandleInteractionObserved;
            KingdomTeachingInteraction.InteractionObserved += HandleInteractionObserved;
            _saveGameService = saveGameService;
            Hud = hud;
            _catalog = catalog ?? KingdomTeachingCatalog.LoadCanonical();
            Refresh();
        }

        public void Refresh()
        {
            State = KingdomTeachingQuestline.Evaluate(
                _saveGameService?.CurrentSave,
                _catalog);
            if (Hud == null)
            {
                return;
            }

            bool visible = State != null &&
                           State.IsAvailable &&
                           !State.IsComplete &&
                           State.CurrentStep != null;
            Hud.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            Hud.Bind(
                QuestHudPlanner.FromKingdomTeaching(
                    State.CurrentStep,
                    QuestHudAutoQuest.Enabled),
                ChoosePrimary,
                Refresh);
        }

        public void ChoosePrimary()
        {
            KingdomTeachingStep step = State?.CurrentStep;
            if (step == null)
            {
                return;
            }

            if (string.Equals(
                    step.Interaction,
                    "acknowledge",
                    StringComparison.Ordinal) ||
                string.Equals(
                    step.Interaction,
                    "inspect_research_troops",
                    StringComparison.Ordinal))
            {
                Advance(step.CompletionEvent);
                return;
            }

            KingdomTeachingInteraction.Request(step.Interaction);
        }

        private void HandleInteractionObserved(string interaction)
        {
            KingdomTeachingStep step = State?.CurrentStep;
            if (step != null &&
                string.Equals(
                    step.Interaction,
                    interaction,
                    StringComparison.Ordinal))
            {
                Advance(step.CompletionEvent);
            }
        }

        private void Advance(string completionEvent)
        {
            if (_saveGameService == null || _catalog == null)
            {
                return;
            }

            KingdomTeachingCommitResult result =
                KingdomTeachingSaveAuthority.TryAdvance(
                    _saveGameService,
                    _catalog,
                    completionEvent);
            if (result.Accepted)
            {
                Refresh();
            }
        }
    }
}
