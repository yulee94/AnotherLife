using System;
using System.Collections.Generic;
using AL.Core;
using UnityEngine;

namespace AL.Data.Definitions
{
    [Serializable]
    public sealed class BuildingConstructionCostDefinition
    {
        public ResourceType ResourceType;
        public long Amount;
    }

    [Serializable]
    public sealed class BuildingConstructionLevelDefinition
    {
        public int TargetLevel;
        public int DurationSeconds;
        public List<BuildingConstructionCostDefinition> Costs =
            new List<BuildingConstructionCostDefinition>();
    }

    [CreateAssetMenu(fileName = "New Building", menuName = "AL/Data/Building")]
    public class BuildingDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public Sprite Icon;
        public int MaxLevel;
        public List<BuildingConstructionLevelDefinition> ConstructionLevels =
            new List<BuildingConstructionLevelDefinition>();
    }
}
