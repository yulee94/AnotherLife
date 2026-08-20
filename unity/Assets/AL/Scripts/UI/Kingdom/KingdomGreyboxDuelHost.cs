using System;
using AL.Data.Runtime;
using AL.VerticalSlice;
using AL.VerticalSlice.Combat;
using UnityEngine;

namespace AL.UI.Kingdom
{
    /// <summary>
    /// Hosts the existing greybox champion duel inside Kingdom. Does not load ChampionArena
    /// and does not invent combat stats — it binds the session/default champion onto
    /// <see cref="AL.VerticalSlice.SliceRunState"/> and starts <see cref="GreyboxCombatEncounter"/>.
    /// </summary>
    public sealed class KingdomGreyboxDuelHost : MonoBehaviour
    {
        private GreyboxCombatEncounter _encounter;
        private Action<string> _setMessage;

        public bool IsRunning => _encounter != null && _encounter.IsRunning;

        public void Bind(Action<string> setMessage)
        {
            _setMessage = setMessage;
        }

        public void StartDuel()
        {
            AL.VerticalSlice.SliceRunState.SelectedChampion = ResolveSelectedOrDefaultChampion();

            if (_encounter == null)
            {
                var encounterObject = new GameObject("KingdomGreyboxCombatEncounter");
                encounterObject.transform.SetParent(transform, false);
                _encounter = encounterObject.AddComponent<GreyboxCombatEncounter>();
                _encounter.Completed += OnChampionDuelCompleted;
                _encounter.ReturnRequested += OnChampionDuelReturned;
            }

            SliceChampionProfile champion = AL.VerticalSlice.SliceRunState.SelectedChampion;
            _setMessage?.Invoke(
                "CHAMPION DUEL STARTED — " +
                champion.DisplayName +
                " enters the greybox arena.");
            _encounter.BeginEncounter();
        }

        /// <summary>
        /// Selected champion already in the session, else the confirmed slice champion, else the
        /// existing greybox default. Combat numbers come from those sources; this method does not
        /// invent a new stat block.
        /// </summary>
        public static SliceChampionProfile ResolveSelectedOrDefaultChampion()
        {
            if (AL.VerticalSlice.SliceRunState.SelectedChampion != null)
            {
                return AL.VerticalSlice.SliceRunState.SelectedChampion;
            }

            if (AL.Data.Runtime.SliceRunState.HasConfirmedChampion)
            {
                return FromConfirmedChampion(AL.Data.Runtime.SliceRunState.Champion);
            }

            return SliceChampionProfile.CreateDefault();
        }

        internal static SliceChampionProfile FromConfirmedChampion(ChampionState state)
        {
            if (state == null || !state.HasIdentity)
            {
                return SliceChampionProfile.CreateDefault();
            }

            SliceChampionProfile catalogProfile;
            string diagnosticCode;
            if (AL.Services.Local.SixFamilyRuntimeCatalog.TryGetDefaultChampion(
                    out catalogProfile,
                    out diagnosticCode))
            {
                SliceChampionProfile named;
                if (AL.Services.Local.SixFamilyRuntimeCatalog.TryLoad(out var snapshot, out _) &&
                    snapshot.TryCreateSliceProfile(state.Id, out named, out _))
                {
                    catalogProfile = named;
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "AL-GDC-CHAMPION-MISSING: confirmed champion cannot resolve catalog stats (" +
                    diagnosticCode +
                    ").");
            }

            return new SliceChampionProfile(
                state.Id,
                state.DisplayName,
                state.Family.ToString(),
                state.MaxHealth > 0 ? state.MaxHealth : catalogProfile.MaxHealth,
                state.MaxMana > 0 ? state.MaxMana : catalogProfile.MaxMana,
                state.Attack > 0 ? state.Attack : catalogProfile.AttackPower,
                catalogProfile.SpecialPower,
                catalogProfile.DefendMitigation);
        }

        private void OnChampionDuelCompleted(SliceCombatResult result)
        {
            if (result == null)
            {
                return;
            }

            string outcome = result.Won
                ? "VICTORY"
                : result.Lost
                    ? "DEFEAT"
                    : result.Outcome.ToString().ToUpperInvariant();
            _setMessage?.Invoke(
                "CHAMPION DUEL " + outcome + " — " +
                result.ChampionDisplayName + " vs " + result.OpponentDisplayName +
                " in " + result.TurnsTaken + " turn(s).");
        }

        private void OnChampionDuelReturned()
        {
            SliceCombatResult result = AL.VerticalSlice.SliceRunState.LastCombatResult;
            string outcome = result == null
                ? "NONE"
                : result.Outcome.ToString().ToUpperInvariant();
            _setMessage?.Invoke(
                "CHAMPION DUEL CONCLUDED — returned to Kingdom. Last result: " + outcome + ".");
        }

        private void OnDestroy()
        {
            if (_encounter == null)
            {
                return;
            }

            _encounter.Completed -= OnChampionDuelCompleted;
            _encounter.ReturnRequested -= OnChampionDuelReturned;
        }
    }
}
