using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Kingdom
{
    public enum KingdomBuildingPresentationStatus
    {
        Unbuilt = 0,
        Built = 1,
        InvalidState = 2
    }

    public sealed class KingdomBuildingSlotDefinition
    {
        internal KingdomBuildingSlotDefinition(
            string slotId,
            string buildingId,
            RealmId realmId,
            Vector2Int gridPosition,
            int rotationQuarterTurns)
        {
            SlotId = slotId;
            BuildingId = buildingId;
            RealmId = realmId;
            GridPosition = gridPosition;
            RotationQuarterTurns = rotationQuarterTurns;
        }

        public string SlotId { get; }
        public string BuildingId { get; }
        public RealmId RealmId { get; }
        public Vector2Int GridPosition { get; }
        public int RotationQuarterTurns { get; }
    }

    public sealed class KingdomBuildingPresentation
    {
        internal KingdomBuildingPresentation(
            KingdomBuildingSlotDefinition slot,
            KingdomBuildingPresentationStatus status,
            int confirmedLevel,
            bool isUpgrading,
            long upgradeCompleteTimestamp,
            string diagnosticCode)
        {
            Slot = slot;
            Status = status;
            ConfirmedLevel = confirmedLevel;
            IsUpgrading = isUpgrading;
            UpgradeCompleteTimestamp = upgradeCompleteTimestamp;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public KingdomBuildingSlotDefinition Slot { get; }
        public string BuildingId => Slot.BuildingId;
        public KingdomBuildingPresentationStatus Status { get; }
        public int ConfirmedLevel { get; }
        public bool IsUpgrading { get; }
        public long UpgradeCompleteTimestamp { get; }
        public string DiagnosticCode { get; }
        public bool IsBuilt => Status == KingdomBuildingPresentationStatus.Built;
    }

    /// <summary>
    /// Versioned spatial authority for the current kingdom board. Slot identity
    /// remains stable even when save rows or catalog queries arrive in a
    /// different order. Coordinates preserve the existing realm layout
    /// character and can be revised in a later layout version without changing
    /// the slot IDs.
    /// </summary>
    public static class KingdomBuildingLayoutCatalog
    {
        public const string LayoutVersion = "kingdom.layout.v1";

        private static readonly SlotIdentity[] SlotIdentities =
        {
            new SlotIdentity("kingdom.slot.town-hall", "TownHall"),
            new SlotIdentity("kingdom.slot.farm", "Farm"),
            new SlotIdentity("kingdom.slot.lumber-mill", "LumberMill"),
            new SlotIdentity("kingdom.slot.quarry", "Quarry"),
            new SlotIdentity("kingdom.slot.gold-mine", "GoldMine"),
            new SlotIdentity("kingdom.slot.mana-shrine", "ManaShrine"),
            new SlotIdentity("kingdom.slot.mine", "Mine"),
            new SlotIdentity("kingdom.slot.barracks", "Barracks"),
            new SlotIdentity("kingdom.slot.academy", "Academy"),
            new SlotIdentity("kingdom.slot.market", "Market"),
            new SlotIdentity("kingdom.slot.storehouse", "Storehouse"),
            new SlotIdentity("kingdom.slot.forge", "Forge"),
            new SlotIdentity("kingdom.slot.stable", "Stable"),
            new SlotIdentity("kingdom.slot.workshop", "Workshop"),
            new SlotIdentity("kingdom.slot.embassy", "Embassy"),
            new SlotIdentity("kingdom.slot.wall", "Wall"),
            new SlotIdentity("kingdom.slot.watchtower", "Watchtower")
        };

        private static readonly IReadOnlyDictionary<RealmId, IReadOnlyList<KingdomBuildingSlotDefinition>>
            SlotsByRealm = CreateLayouts();

        public static IReadOnlyList<KingdomBuildingSlotDefinition> GetSlots(RealmId realmId)
        {
            return SlotsByRealm.TryGetValue(realmId, out var slots)
                ? slots
                : Array.Empty<KingdomBuildingSlotDefinition>();
        }

        private static IReadOnlyDictionary<RealmId, IReadOnlyList<KingdomBuildingSlotDefinition>>
            CreateLayouts()
        {
            return new Dictionary<RealmId, IReadOnlyList<KingdomBuildingSlotDefinition>>
            {
                {
                    RealmId.Stonehold,
                    CreateLayout(
                        RealmId.Stonehold,
                        new[]
                        {
                            P(0, 0),
                            P(3, 0),
                            P(2, 2),
                            P(0, 3),
                            P(-2, 2),
                            P(-3, 0),
                            P(-2, -2),
                            P(0, -3),
                            P(2, -2),
                            P(6, 0),
                            P(4, 4),
                            P(0, 6),
                            P(-4, 4),
                            P(-6, 0),
                            P(-4, -4),
                            P(0, -6),
                            P(4, -4)
                        })
                },
                {
                    RealmId.Eldergrove,
                    CreateLayout(
                        RealmId.Eldergrove,
                        new[]
                        {
                            P(0, 0),
                            P(-3, 1),
                            P(2, 2),
                            P(-1, 4),
                            P(4, -1),
                            P(-4, -2),
                            P(1, -4),
                            P(5, 3),
                            P(-5, 4),
                            P(3, 6),
                            P(-2, 7),
                            P(6, -4),
                            P(-6, -5),
                            P(0, -7),
                            P(7, 0),
                            P(-7, 0),
                            P(5, -7)
                        })
                },
                {
                    RealmId.Crownlands,
                    CreateLayout(
                        RealmId.Crownlands,
                        new[]
                        {
                            P(0, 0),
                            P(3, 0),
                            P(6, 0),
                            P(9, 0),
                            P(0, 3),
                            P(3, 3),
                            P(6, 3),
                            P(9, 3),
                            P(0, 6),
                            P(3, 6),
                            P(6, 6),
                            P(9, 6),
                            P(0, 9),
                            P(3, 9),
                            P(6, 9),
                            P(9, 9),
                            P(0, 12)
                        })
                },
                {
                    RealmId.Umbral,
                    CreateLayout(
                        RealmId.Umbral,
                        new[]
                        {
                            P(0, 0),
                            P(2, 1),
                            P(-2, -1),
                            P(4, -2),
                            P(-4, 2),
                            P(5, 4),
                            P(-5, -4),
                            P(7, -1),
                            P(-7, 1),
                            P(8, 5),
                            P(-8, -5),
                            P(10, -3),
                            P(-10, 3),
                            P(11, 6),
                            P(-11, -6),
                            P(13, -5),
                            P(-13, 5)
                        })
                }
            };
        }

        private static IReadOnlyList<KingdomBuildingSlotDefinition> CreateLayout(
            RealmId realmId,
            IReadOnlyList<Vector2Int> coordinates)
        {
            if (coordinates == null || coordinates.Count != SlotIdentities.Length)
            {
                throw new InvalidOperationException(
                    $"Realm {realmId} must declare exactly {SlotIdentities.Length} stable building slots.");
            }

            var slots = new KingdomBuildingSlotDefinition[SlotIdentities.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                SlotIdentity identity = SlotIdentities[i];
                Vector2Int position = coordinates[i];
                slots[i] = new KingdomBuildingSlotDefinition(
                    identity.SlotId,
                    identity.BuildingId,
                    realmId,
                    position,
                    CalculateEntranceRotation(position));
            }

            return Array.AsReadOnly(slots);
        }

        private static int CalculateEntranceRotation(Vector2Int position)
        {
            if (Mathf.Abs(position.x) >= Mathf.Abs(position.y))
            {
                return position.x > 0 ? 3 : position.x < 0 ? 1 : 0;
            }

            return position.y > 0 ? 2 : 0;
        }

        private static Vector2Int P(int x, int y) => new Vector2Int(x, y);

        private readonly struct SlotIdentity
        {
            public SlotIdentity(string slotId, string buildingId)
            {
                SlotId = slotId;
                BuildingId = buildingId;
            }

            public string SlotId { get; }
            public string BuildingId { get; }
        }
    }

    /// <summary>
    /// Converts immutable gameplay snapshots into a stable visual layout. The
    /// resolver never creates, edits, or saves BuildingState rows.
    /// </summary>
    public static class KingdomBuildingPresentationResolver
    {
        public const int MaximumVisualLevel = 10;
        public const string DuplicateStateDiagnostic = "KINGDOM_BUILDING_STATE_DUPLICATE";
        public const string InvalidLevelDiagnostic = "KINGDOM_BUILDING_LEVEL_INVALID";
        public const string InvalidTimerDiagnostic = "KINGDOM_BUILDING_TIMER_INVALID";

        public static IReadOnlyList<KingdomBuildingPresentation> Resolve(
            RealmId realmId,
            IEnumerable<BuildingState> buildingStates)
        {
            IReadOnlyList<KingdomBuildingSlotDefinition> slots =
                KingdomBuildingLayoutCatalog.GetSlots(realmId);
            var statesByBuildingId = GroupStates(buildingStates);
            var presentations = new KingdomBuildingPresentation[slots.Count];

            for (int i = 0; i < slots.Count; i++)
            {
                KingdomBuildingSlotDefinition slot = slots[i];
                if (!statesByBuildingId.TryGetValue(slot.BuildingId, out var matchingStates))
                {
                    presentations[i] = Unbuilt(slot);
                    continue;
                }

                if (matchingStates.Count != 1)
                {
                    presentations[i] = Invalid(slot, DuplicateStateDiagnostic);
                    continue;
                }

                BuildingState state = matchingStates[0];
                if (state.Level < 0 || state.Level > MaximumVisualLevel)
                {
                    presentations[i] = Invalid(slot, InvalidLevelDiagnostic);
                    continue;
                }

                if (state.IsUpgrading && state.UpgradeCompleteTimestamp <= 0)
                {
                    presentations[i] = Invalid(slot, InvalidTimerDiagnostic);
                    continue;
                }

                presentations[i] = new KingdomBuildingPresentation(
                    slot,
                    state.Level == 0
                        ? KingdomBuildingPresentationStatus.Unbuilt
                        : KingdomBuildingPresentationStatus.Built,
                    state.Level,
                    state.IsUpgrading,
                    state.UpgradeCompleteTimestamp,
                    string.Empty);
            }

            return Array.AsReadOnly(presentations);
        }

        private static Dictionary<string, List<BuildingState>> GroupStates(
            IEnumerable<BuildingState> buildingStates)
        {
            var grouped = new Dictionary<string, List<BuildingState>>(StringComparer.Ordinal);
            if (buildingStates == null)
            {
                return grouped;
            }

            foreach (BuildingState state in buildingStates)
            {
                if (state == null || string.IsNullOrWhiteSpace(state.BuildingId))
                {
                    continue;
                }

                if (!grouped.TryGetValue(state.BuildingId, out var states))
                {
                    states = new List<BuildingState>();
                    grouped.Add(state.BuildingId, states);
                }

                states.Add(state);
            }

            return grouped;
        }

        private static KingdomBuildingPresentation Unbuilt(
            KingdomBuildingSlotDefinition slot)
        {
            return new KingdomBuildingPresentation(
                slot,
                KingdomBuildingPresentationStatus.Unbuilt,
                0,
                false,
                0,
                string.Empty);
        }

        private static KingdomBuildingPresentation Invalid(
            KingdomBuildingSlotDefinition slot,
            string diagnosticCode)
        {
            return new KingdomBuildingPresentation(
                slot,
                KingdomBuildingPresentationStatus.InvalidState,
                0,
                false,
                0,
                diagnosticCode);
        }
    }
}
