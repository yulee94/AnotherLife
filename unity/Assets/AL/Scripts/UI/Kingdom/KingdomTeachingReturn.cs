using System;
using AL.ChampionMode;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Data.Catalogs.WorldAtlas;
using AL.Data.Runtime;
using AL.UI.QuestHud;
using AL.UI.SharedMenu;
using AL.World;
using UnityEngine;

namespace AL.UI.Kingdom
{
    public sealed class KingdomTeachingReturnPlan
    {
        internal KingdomTeachingReturnPlan(
            RealmId realm,
            string innerAtlasZoneId,
            string mainGateId,
            string transitionZoneId,
            Vector3 position,
            Vector3 forward)
        {
            Realm = realm;
            InnerAtlasZoneId = innerAtlasZoneId ?? string.Empty;
            MainGateId = mainGateId ?? string.Empty;
            TransitionZoneId = transitionZoneId ?? string.Empty;
            Position = position;
            Forward = forward;
        }

        public RealmId Realm { get; }
        public string InnerAtlasZoneId { get; }
        public string MainGateId { get; }
        public string TransitionZoneId { get; }
        public Vector3 Position { get; }
        public Vector3 Forward { get; }
        public string DestinationScene => SharedMenuIds.AdventureScene;
        public bool ShouldEnterWarzone => false;
    }

    /// <summary>
    /// Resolves the post-teaching 3D landing from the canonical teaching and
    /// World Atlas catalogs. The champion remains inside the protected inner
    /// safe zone and faces the controlled main gate; no Warzone load is planned.
    /// </summary>
    public static class KingdomTeachingReturnPlanner
    {
        private const float InnerGateInset = 8f;
        private const float ChampionHeight = 1.1f;

        public static bool TryPlan(
            SaveGameData save,
            KingdomTeachingCatalog catalog,
            WorldAtlasSnapshot snapshot,
            out KingdomTeachingReturnPlan plan)
        {
            plan = null;
            if (!CrossModeSceneSwitch.IsIdentityCommitted(save))
            {
                return false;
            }

            MvpLoopSnapshot identity = MvpLoopSaveCodec.Read(save);
            if (!string.Equals(
                    identity.LastResultId,
                    ProofOfWorthLordship.ResolveMarkId(identity.Realm),
                    StringComparison.Ordinal))
            {
                return false;
            }

            KingdomTeachingState teaching =
                KingdomTeachingQuestline.Evaluate(save, catalog);
            if (teaching == null || !teaching.IsAvailable || !teaching.IsComplete ||
                snapshot == null)
            {
                return false;
            }

            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(snapshot);
            string realmId = InnerRealmWorldLayout.RealmCatalogId(save.SelectedRealm);
            if (!layout.TryGetInner(realmId, out InnerRealmSlotLayout inner))
            {
                return false;
            }

            Vector3 inward = inner.InnerSafe.Center - inner.GatePosition;
            inward.y = 0f;
            if (inward.sqrMagnitude < 0.01f)
            {
                return false;
            }

            inward.Normalize();
            Vector3 position = inner.GatePosition + inward * InnerGateInset;
            position.y = ChampionHeight;
            if (!inner.InnerSafe.Contains(position))
            {
                return false;
            }

            Vector3 forward = -inward;
            plan = new KingdomTeachingReturnPlan(
                save.SelectedRealm,
                inner.InnerAtlasZoneId,
                inner.MainGateId,
                inner.TransitionZoneId,
                position,
                forward);
            return true;
        }
    }

    /// <summary>
    /// Applies the catalog-planned inner-gate landing after ChampionArena has
    /// rebuilt its champion, then leaves a non-actionable gate prompt visible.
    /// It never requests a scene transition or supplies movement input.
    /// </summary>
    public sealed class KingdomTeachingReturnDirector : MonoBehaviour
    {
        public KingdomTeachingReturnPlan Plan { get; private set; }
        public bool IsApplied { get; private set; }

        public bool EnsureReady(
            SaveGameData save,
            QuestHudOverlay hud,
            KingdomTeachingCatalog catalog = null,
            WorldAtlasSnapshot snapshot = null)
        {
            if (!CrossModeSession.HasPendingTeachingReturn)
            {
                return false;
            }

            catalog ??= KingdomTeachingCatalog.LoadCanonical();
            snapshot ??= FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            if (!KingdomTeachingReturnPlanner.TryPlan(
                    save,
                    catalog,
                    snapshot,
                    out KingdomTeachingReturnPlan plan))
            {
                Plan = null;
                IsApplied = false;
                return false;
            }

            bool sameLanding = Plan != null &&
                               Plan.Realm == plan.Realm &&
                               Plan.Position == plan.Position;
            Plan = plan;
            if (!sameLanding)
            {
                IsApplied = false;
            }

            hud?.Bind(
                QuestHudPlanner.WarzoneGate(QuestHudAutoQuest.Enabled),
                null);
            TryApplyFoundChampion();
            return true;
        }

        public bool TryApply(Transform champion)
        {
            if (IsApplied ||
                !CrossModeSession.HasPendingTeachingReturn ||
                Plan == null ||
                champion == null)
            {
                return false;
            }

            CharacterController controller = champion.GetComponent<CharacterController>();
            bool restoreController = controller != null && controller.enabled;
            if (restoreController)
            {
                controller.enabled = false;
            }

            champion.position = Plan.Position;
            if (Plan.Forward.sqrMagnitude > 0.01f)
            {
                champion.rotation = Quaternion.LookRotation(Plan.Forward, Vector3.up);
            }

            if (restoreController)
            {
                controller.enabled = true;
            }

            IsApplied = true;
            CrossModeSession.TryConsumeTeachingReturn();
            return true;
        }

        private void Update()
        {
            TryApplyFoundChampion();
        }

        private void TryApplyFoundChampion()
        {
            if (IsApplied || Plan == null)
            {
                return;
            }

            GameObject champion = GameObject.Find(FirstSessionChampionStart.PlayerObjectName);
            if (champion != null)
            {
                TryApply(champion.transform);
            }
        }
    }
}
