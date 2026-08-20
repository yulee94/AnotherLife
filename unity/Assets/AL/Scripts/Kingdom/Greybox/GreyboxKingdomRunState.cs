using System;
using System.Collections.Generic;
using AL.Core;

namespace AL.Kingdom.Greybox
{
    /// <summary>
    /// The kingdom-build slice of the vertical-slice local run state.
    ///
    /// This is a deliberately lightweight, JSON-serializable snapshot used by the
    /// greybox kingdom build scene. It does NOT route through the production
    /// save-authority stack (which is hard-latched closed via
    /// <c>ProfileMutationContainment.ProductionWriteActivationEnabled == false</c>);
    /// the greybox slice is explicitly allowed to keep its own local run state so it
    /// can demonstrate the build loop without blocking on save/determinism authority.
    ///
    /// The integration pass folds this into the shared RunState contract alongside
    /// realm selection, character creation, and combat result.
    /// </summary>
    [Serializable]
    public class GreyboxKingdomRunState
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        /// <summary>Selected realm for this run (lowercase snake_case in future; enum int for greybox).</summary>
        public RealmId Realm = RealmId.Crownlands;

        /// <summary>True once the fixed combat-loot slice budget has been granted.</summary>
        public bool SliceBudgetSeeded;

        /// <summary>Number of build actions performed this run (observability for the find-the-fun pass).</summary>
        public int BuildActionCount;

        /// <summary>Constructed/upgraded structures and their current level.</summary>
        public List<GreyboxStructureState> Structures = new List<GreyboxStructureState>();

        /// <summary>Spendable resource budget earned from combat or granted as a fixed slice.</summary>
        public List<GreyboxResourceAmount> Budget = new List<GreyboxResourceAmount>();

        public GreyboxStructureState FindStructure(string buildingId)
        {
            for (int i = 0; i < Structures.Count; i++)
            {
                GreyboxStructureState structure = Structures[i];
                if (structure != null &&
                    string.Equals(structure.BuildingId, buildingId, StringComparison.Ordinal))
                {
                    return structure;
                }
            }

            return null;
        }

        public long GetBudget(ResourceType resourceType)
        {
            for (int i = 0; i < Budget.Count; i++)
            {
                GreyboxResourceAmount amount = Budget[i];
                if (amount != null && amount.Type == resourceType)
                {
                    return amount.Amount;
                }
            }

            return 0L;
        }

        public void SetBudget(ResourceType resourceType, long amount)
        {
            for (int i = 0; i < Budget.Count; i++)
            {
                GreyboxResourceAmount entry = Budget[i];
                if (entry != null && entry.Type == resourceType)
                {
                    entry.Amount = amount;
                    return;
                }
            }

            Budget.Add(new GreyboxResourceAmount { Type = resourceType, Amount = amount });
        }
    }

    [Serializable]
    public class GreyboxStructureState
    {
        public string BuildingId = string.Empty;
        public int Level;
    }

    [Serializable]
    public class GreyboxResourceAmount
    {
        public ResourceType Type;
        public long Amount;
    }
}
