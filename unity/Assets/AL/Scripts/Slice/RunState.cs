using System;
using System.Collections.Generic;
using AL.Core;
using UnityEngine;

namespace AL.Slice
{
    /// <summary>
    /// Greybox vertical-slice run snapshot. This is the LOCAL slice save contract only: a flat,
    /// JsonUtility-friendly data object that records the four stages of the find-the-fun loop
    /// (realm selection -> character creation -> combat -> kingdom build) plus the phase the run
    /// reached, so a reload can continue or replay the loop.
    ///
    /// It is intentionally NOT part of the catalog/save/determinism authority (LocalSaveGameService /
    /// SaveAuthority / OfflineServiceStack). It never references those systems and is not registered in
    /// the ServiceLocator, so the authority stack cannot observe or validate it. Realm/character/combat/
    /// kingdom values are stored as stable strings and primitive lists so the snapshot survives enum
    /// reordering and remains human-readable.
    /// </summary>
    [Serializable]
    public sealed class RunState
    {
        public const string CurrentSchemaVersion = "1";

        public string schemaVersion;
        public string phase;
        public long savedAtUnixSeconds;

        public RealmSelectionState realm;
        public CharacterState character;
        public CombatResultState combat;
        public KingdomBuildState kingdom;

        public static RunState CreateDefault()
        {
            return new RunState
            {
                schemaVersion = CurrentSchemaVersion,
                phase = SlicePhase.Boot,
                savedAtUnixSeconds = 0L,
                realm = new RealmSelectionState(),
                character = new CharacterState(),
                combat = new CombatResultState(),
                kingdom = new KingdomBuildState()
            };
        }

        /// <summary>Deep copy via JSON round-trip so snapshots never alias the live run state.</summary>
        public RunState Clone()
        {
            RunState clone = FromJson(ToJson());
            return clone ?? CreateDefault();
        }

        public string ToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static RunState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                RunState state = JsonUtility.FromJson<RunState>(json);
                if (state == null)
                {
                    return null;
                }

                // Guard against older/newer snapshots that omit a sub-object or the schema version.
                state.realm = state.realm ?? new RealmSelectionState();
                state.character = state.character ?? new CharacterState();
                state.combat = state.combat ?? new CombatResultState();
                state.kingdom = state.kingdom ?? new KingdomBuildState();
                if (string.IsNullOrWhiteSpace(state.schemaVersion))
                {
                    state.schemaVersion = CurrentSchemaVersion;
                }

                return state;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>Phase markers for the slice loop, used to decide where a reload resumes.</summary>
    public static class SlicePhase
    {
        public const string Boot = "boot";
        public const string RealmSelection = "realm_selection";
        public const string CharacterCreation = "character_creation";
        public const string Combat = "combat";
        public const string KingdomBuild = "kingdom_build";
        public const string Complete = "complete";
    }

    /// <summary>Combat outcome markers.</summary>
    public static class SliceOutcome
    {
        public const string Win = "win";
        public const string Loss = "loss";
        public const string Unresolved = "unresolved";
    }

    [Serializable]
    public sealed class RealmSelectionState
    {
        /// <summary>RealmId name (e.g. "Stonehold"); "None" or empty means not yet selected.</summary>
        public string realmId;

        /// <summary>Display name (e.g. "Stonehold Dwarves").</summary>
        public string realmName;

        public bool IsSelected =>
            !string.IsNullOrEmpty(realmId) &&
            !string.Equals(realmId, RealmId.None.ToString(), StringComparison.Ordinal);

        public RealmId RealmIdValue
        {
            get
            {
                if (Enum.TryParse(realmId, ignoreCase: true, out RealmId parsed))
                {
                    return parsed;
                }

                return RealmId.None;
            }
        }
    }

    [Serializable]
    public sealed class CharacterState
    {
        /// <summary>Stable character identifier (e.g. "champion_1").</summary>
        public string id;

        /// <summary>Player-visible name.</summary>
        public string displayName;

        /// <summary>ClassFamily name (e.g. "Warrior", "Mage").</summary>
        public string className;

        /// <summary>SubclassId name (e.g. "Archmage"); optional.</summary>
        public string subclassId;

        public List<StatEntry> stats;

        /// <summary>Loadout / skill ids the champion carries into combat.</summary>
        public List<string> loadout;

        public CharacterState()
        {
            stats = new List<StatEntry>();
            loadout = new List<string>();
        }

        public bool IsCreated => !string.IsNullOrEmpty(id);
    }

    [Serializable]
    public sealed class CombatResultState
    {
        /// <summary>One of <see cref="SliceOutcome"/> values.</summary>
        public string outcome;

        /// <summary>True once the encounter has been fought to a win or loss.</summary>
        public bool completed;

        public string opponentId;
        public string opponentName;
        public int rounds;

        /// <summary>Rewards earned, which feed the kingdom build budget.</summary>
        public List<ResourceEntry> rewards;

        public CombatResultState()
        {
            outcome = SliceOutcome.Unresolved;
            rewards = new List<ResourceEntry>();
        }

        public bool Won => string.Equals(outcome, SliceOutcome.Win, StringComparison.Ordinal);
    }

    [Serializable]
    public sealed class KingdomBuildState
    {
        /// <summary>True once at least one build action has been applied.</summary>
        public bool buildPerformed;

        /// <summary>Human-readable description of the last build action (e.g. "Farm:1->2").</summary>
        public string lastBuildAction;

        /// <summary>Structures and their confirmed levels.</summary>
        public List<BuildingEntry> buildings;

        /// <summary>Treasury / build budget available for the next action.</summary>
        public List<ResourceEntry> budget;

        public KingdomBuildState()
        {
            buildings = new List<BuildingEntry>();
            budget = new List<ResourceEntry>();
        }
    }

    [Serializable]
    public sealed class StatEntry
    {
        public string key;
        public int value;
    }

    [Serializable]
    public sealed class ResourceEntry
    {
        /// <summary>ResourceType name (e.g. "Gold", "Stone").</summary>
        public string type;

        public long amount;
    }

    [Serializable]
    public sealed class BuildingEntry
    {
        public string id;
        public int level;
    }
}
