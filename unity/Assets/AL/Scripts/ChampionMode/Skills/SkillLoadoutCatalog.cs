using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AL.ChampionMode.Control;
using AL.Data.Catalogs;
using AL.Services.Local;
using UnityEngine;
using UnityEngine.Networking;

namespace AL.ChampionMode.Skills
{
    [Serializable]
    public class SkillWeatherCatalogData
    {
        public string version;
        public SkillLoadoutData[] skillLoadouts;
    }

    [Serializable]
    public class SkillLoadoutData
    {
        public int slot;
        public string id;
        public string displayName;
        public string role;
        public string vfxKey;
        public float cooldownSeconds;
        public float manaCost;
        public float castTimeSeconds;
        public float rangeMeters;
        public float power;
        public float botDamageMultiplier;
        public CrowdControlKind controlKind;
        public float controlDurationSeconds;
        public float controlSeverity;
        public bool cleanseSoftControl;
        public float controlWardSeconds;
    }

    internal enum MvpSkillIdentity
    {
        RealmStrike,
        RenewingGuard,
        WarzoneBurst,
        WarmasterBreaker
    }

    /// <summary>
    /// One detached, immutable skill row. Mutable catalog DTOs never escape into
    /// the live caster through this type.
    /// </summary>
    public sealed class SkillLoadoutSlot
    {
        internal SkillLoadoutSlot(SkillLoadoutData source, MvpSkillIdentity identity)
        {
            Slot = source.slot;
            Id = source.id;
            DisplayName = source.displayName;
            Role = source.role;
            VfxKey = source.vfxKey;
            CooldownSeconds = source.cooldownSeconds;
            ManaCost = source.manaCost;
            CastTimeSeconds = source.castTimeSeconds;
            RangeMeters = source.rangeMeters;
            Power = source.power;
            BotDamageMultiplier = source.botDamageMultiplier;
            ControlKind = source.controlKind;
            ControlDurationSeconds = source.controlDurationSeconds;
            ControlSeverity = source.controlSeverity;
            CleanseSoftControl = source.cleanseSoftControl;
            ControlWardSeconds = source.controlWardSeconds;
            Identity = identity;
        }

        public int Slot { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public string Role { get; }
        public string VfxKey { get; }
        public float CooldownSeconds { get; }
        public float ManaCost { get; }
        public float CastTimeSeconds { get; }
        public float RangeMeters { get; }
        public float Power { get; }
        public float BotDamageMultiplier { get; }
        public CrowdControlKind ControlKind { get; }
        public float ControlDurationSeconds { get; }
        public float ControlSeverity { get; }
        public bool CleanseSoftControl { get; }
        public float ControlWardSeconds { get; }
        internal MvpSkillIdentity Identity { get; }

        internal SkillLoadoutData ToData()
        {
            return new SkillLoadoutData
            {
                slot = Slot,
                id = Id,
                displayName = DisplayName,
                role = Role,
                vfxKey = VfxKey,
                cooldownSeconds = CooldownSeconds,
                manaCost = ManaCost,
                castTimeSeconds = CastTimeSeconds,
                rangeMeters = RangeMeters,
                power = Power,
                botDamageMultiplier = BotDamageMultiplier,
                controlKind = ControlKind,
                controlDurationSeconds = ControlDurationSeconds,
                controlSeverity = ControlSeverity,
                cleanseSoftControl = CleanseSoftControl,
                controlWardSeconds = ControlWardSeconds
            };
        }
    }

    /// <summary>
    /// The complete playable four-slot loadout. Publication is all-or-nothing,
    /// and neither its backing array nor mutable catalog DTOs are exposed.
    /// </summary>
    public sealed class SkillLoadoutSnapshot
    {
        private readonly SkillLoadoutSlot[] _slots;

        internal SkillLoadoutSnapshot(SkillLoadoutSlot[] slots)
        {
            _slots = (SkillLoadoutSlot[])slots.Clone();
        }

        public int Count => _slots.Length;

        public bool TryGetSlot(int slot, out SkillLoadoutSlot value)
        {
            if (slot < 0 || slot >= _slots.Length)
            {
                value = null;
                return false;
            }

            value = _slots[slot];
            return value != null;
        }

        internal SkillLoadoutData[] ToDataArray()
        {
            var copy = new SkillLoadoutData[_slots.Length];
            for (var index = 0; index < _slots.Length; index++)
            {
                copy[index] = _slots[index].ToData();
            }

            return copy;
        }
    }

    public static class SkillLoadoutCatalog
    {
        public const string CatalogRelativePath = "GameData/skill_weather.v1.json";
        public const int RequiredSlotCount = 4;

        private const string RealmStrikeRole = "melee_damage";
        private const string RealmStrikeVfxKey = "realm_slash";
        private const string RenewingGuardId = "renewing_guard";
        private const string RenewingGuardRole = "self_heal_guard";
        private const string RenewingGuardVfxKey = "renewing_guard";
        private const string WarzoneBurstId = "warzone_burst";
        private const string WarzoneBurstRole = "area_damage";
        private const string WarzoneBurstVfxKey = "warzone_shockwave";
        private const string WarmasterBreakerId = "warmaster_breaker";
        private const string WarmasterBreakerRole = "elite_break_damage";
        private const string WarmasterBreakerVfxKey = "warmaster_breaker";

        [Serializable]
        private sealed class SkillWireOrderEnvelope
        {
            public SkillWireOrderRecord[] records;
        }

        [Serializable]
        private sealed class SkillWireOrderRecord
        {
            public string id;
            public string kind;
            public int slot;
        }

        public static bool TryLoad(out SkillLoadoutData[] loadouts)
        {
            loadouts = null;

            if (!TryLoadSnapshot(out var snapshot))
            {
                return false;
            }

            loadouts = snapshot.ToDataArray();
            return true;
        }

        public static bool TryLoadSnapshot(out SkillLoadoutSnapshot snapshot)
        {
            snapshot = null;

            string path = BuildCatalogPath();
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                return TryParseSnapshot(File.ReadAllText(path), out snapshot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SkillLoadoutCatalog] Could not load the playable skill snapshot. Skills remain unavailable. {ex.Message}");
                return false;
            }
        }

        public static IEnumerator LoadAsync(Action<SkillLoadoutData[]> onLoaded)
        {
            yield return LoadSnapshotAsync(snapshot => onLoaded?.Invoke(snapshot?.ToDataArray()));
        }

        public static IEnumerator LoadSnapshotAsync(Action<SkillLoadoutSnapshot> onLoaded)
        {
            if (TryLoadSnapshot(out var fileSnapshot))
            {
                onLoaded?.Invoke(fileSnapshot);
                yield break;
            }

            string path = BuildCatalogPath();
            if (!path.Contains("://"))
            {
                onLoaded?.Invoke(null);
                yield break;
            }

            using (var request = UnityWebRequest.Get(path))
            {
                yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                bool failed = request.result != UnityWebRequest.Result.Success;
#else
                bool failed = request.isNetworkError || request.isHttpError;
#endif
                if (failed)
                {
                    Debug.LogWarning($"[SkillLoadoutCatalog] Could not load the playable skill snapshot from StreamingAssets. Skills remain unavailable. {request.error}");
                    onLoaded?.Invoke(null);
                    yield break;
                }

                onLoaded?.Invoke(
                    TryParseSnapshot(request.downloadHandler.text, out var webSnapshot)
                        ? webSnapshot
                        : null);
            }
        }

        public static bool TryParse(string json, out SkillLoadoutData[] loadouts)
        {
            loadouts = null;

            if (!TryParseSnapshot(json, out var snapshot))
            {
                return false;
            }

            loadouts = snapshot.ToDataArray();
            return true;
        }

        public static bool TryParseSnapshot(string json, out SkillLoadoutSnapshot snapshot)
        {
            snapshot = null;
            if (!HasCanonicalWireSlotOrder(json))
            {
                return false;
            }

            GameDataFamilyCatalogSnapshot family;
            string diagnosticCode;
            if (!WireFamilyCatalogLoader.TryLoad("skill_weather", json, out family, out diagnosticCode))
            {
                return false;
            }

            var records = WireFamilyCatalogLoader.RecordsOfKind(family, "skill_loadout");
            if (records.Count != RequiredSlotCount)
            {
                return false;
            }

            var loadouts = new SkillLoadoutData[RequiredSlotCount];
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                int slot;
                string displayName;
                string role;
                string vfxKey;
                float cooldownSeconds;
                float manaCost;
                float castTimeSeconds;
                float rangeMeters;
                float power;
                float botDamageMultiplier;
                if (!WireFamilyCatalogLoader.TryGetInt(record, "slot", out slot) ||
                    !WireFamilyCatalogLoader.TryGetString(record, "display_name", out displayName) ||
                    !WireFamilyCatalogLoader.TryGetString(record, "role", out role) ||
                    !WireFamilyCatalogLoader.TryGetString(record, "vfx_key", out vfxKey) ||
                    !WireFamilyCatalogLoader.TryGetFloat(record, "cooldown_seconds", out cooldownSeconds) ||
                    !WireFamilyCatalogLoader.TryGetFloat(record, "mana_cost", out manaCost) ||
                    !WireFamilyCatalogLoader.TryGetFloat(record, "cast_time_seconds", out castTimeSeconds) ||
                    !WireFamilyCatalogLoader.TryGetFloat(record, "range_meters", out rangeMeters) ||
                    !WireFamilyCatalogLoader.TryGetFloat(record, "power", out power) ||
                    !WireFamilyCatalogLoader.TryGetFloat(record, "bot_damage_multiplier", out botDamageMultiplier))
                {
                    return false;
                }

                CrowdControlKind controlKind = CrowdControlKind.None;
                string controlKindValue;
                if (WireFamilyCatalogLoader.TryGetString(record, "control_kind", out controlKindValue) &&
                    !Enum.TryParse(controlKindValue, true, out controlKind))
                {
                    return false;
                }

                float controlDurationSeconds;
                float controlSeverity;
                bool cleanseSoftControl;
                float controlWardSeconds;
                WireFamilyCatalogLoader.TryGetFloat(record, "control_duration_seconds", out controlDurationSeconds);
                WireFamilyCatalogLoader.TryGetFloat(record, "control_severity", out controlSeverity);
                WireFamilyCatalogLoader.TryGetBool(record, "cleanse_soft_control", out cleanseSoftControl);
                WireFamilyCatalogLoader.TryGetFloat(record, "control_ward_seconds", out controlWardSeconds);

                if (slot < 0 || slot >= loadouts.Length || loadouts[slot] != null)
                {
                    return false;
                }

                loadouts[slot] = new SkillLoadoutData
                {
                    slot = slot,
                    id = record.Id,
                    displayName = displayName,
                    role = role,
                    vfxKey = vfxKey,
                    cooldownSeconds = cooldownSeconds,
                    manaCost = manaCost,
                    castTimeSeconds = castTimeSeconds,
                    rangeMeters = rangeMeters,
                    power = power,
                    botDamageMultiplier = botDamageMultiplier,
                    controlKind = controlKind,
                    controlDurationSeconds = controlDurationSeconds,
                    controlSeverity = controlSeverity,
                    cleanseSoftControl = cleanseSoftControl,
                    controlWardSeconds = controlWardSeconds
                };
            }

            // Record order is part of the four-slot authority contract. Repairing a
            // reordered catalog here would make malformed source data appear valid
            // and would differ from TryCreateSnapshot's fail-closed behavior.
            return TryCreateSnapshot(loadouts, out snapshot);
        }

        private static bool HasCanonicalWireSlotOrder(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                SkillWireOrderEnvelope envelope =
                    JsonUtility.FromJson<SkillWireOrderEnvelope>(json);
                if (envelope?.records == null)
                {
                    return false;
                }

                int nextSlot = 0;
                for (var index = 0; index < envelope.records.Length; index++)
                {
                    SkillWireOrderRecord record = envelope.records[index];
                    if (record == null ||
                        !string.Equals(
                            record.kind,
                            "skill_loadout",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (nextSlot >= RequiredSlotCount ||
                        record.slot != nextSlot ||
                        string.IsNullOrWhiteSpace(record.id))
                    {
                        return false;
                    }

                    nextSlot++;
                }

                return nextSlot == RequiredSlotCount;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryCreateSnapshot(
            SkillLoadoutData[] loadouts,
            out SkillLoadoutSnapshot snapshot)
        {
            snapshot = null;
            if (loadouts == null || loadouts.Length != RequiredSlotCount)
            {
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var slots = new SkillLoadoutSlot[RequiredSlotCount];
            for (var index = 0; index < loadouts.Length; index++)
            {
                SkillLoadoutData loadout = loadouts[index];
                if (loadout == null ||
                    loadout.slot != index ||
                    !ids.Add(loadout.id ?? string.Empty) ||
                    !TryResolveIdentity(loadout, out var identity) ||
                    !HasCompleteValidFields(loadout))
                {
                    return false;
                }

                slots[index] = new SkillLoadoutSlot(loadout, identity);
            }

            snapshot = new SkillLoadoutSnapshot(slots);
            return true;
        }

        private static bool TryResolveIdentity(SkillLoadoutData loadout, out MvpSkillIdentity identity)
        {
            switch (loadout.slot)
            {
                case FirstSessionChampionStart.SpecialSkillSlot:
                    identity = MvpSkillIdentity.RealmStrike;
                    return MatchesIdentity(
                        loadout,
                        FirstSessionChampionStart.SpecialSkillId,
                        RealmStrikeRole,
                        RealmStrikeVfxKey);
                case 1:
                    identity = MvpSkillIdentity.RenewingGuard;
                    return MatchesIdentity(
                        loadout,
                        RenewingGuardId,
                        RenewingGuardRole,
                        RenewingGuardVfxKey);
                case 2:
                    identity = MvpSkillIdentity.WarzoneBurst;
                    return MatchesIdentity(
                        loadout,
                        WarzoneBurstId,
                        WarzoneBurstRole,
                        WarzoneBurstVfxKey);
                case 3:
                    identity = MvpSkillIdentity.WarmasterBreaker;
                    return MatchesIdentity(
                        loadout,
                        WarmasterBreakerId,
                        WarmasterBreakerRole,
                        WarmasterBreakerVfxKey);
                default:
                    identity = default;
                    return false;
            }
        }

        private static bool MatchesIdentity(
            SkillLoadoutData loadout,
            string skillId,
            string role,
            string vfxKey)
        {
            return string.Equals(loadout.id, skillId, StringComparison.Ordinal) &&
                   string.Equals(loadout.role, role, StringComparison.Ordinal) &&
                   string.Equals(loadout.vfxKey, vfxKey, StringComparison.Ordinal);
        }

        private static bool HasCompleteValidFields(SkillLoadoutData loadout)
        {
            return !string.IsNullOrWhiteSpace(loadout.id) &&
                   !string.IsNullOrWhiteSpace(loadout.displayName) &&
                   !string.IsNullOrWhiteSpace(loadout.role) &&
                   !string.IsNullOrWhiteSpace(loadout.vfxKey) &&
                   IsFiniteNonNegative(loadout.cooldownSeconds) &&
                   IsFiniteNonNegative(loadout.manaCost) &&
                   IsFiniteNonNegative(loadout.castTimeSeconds) &&
                   IsFiniteNonNegative(loadout.rangeMeters) &&
                   IsFinitePositive(loadout.power) &&
                   IsFiniteNonNegative(loadout.botDamageMultiplier);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static string BuildCatalogPath()
        {
            if (SixFamilyRuntimeCatalog.TryResolveGameDataDirectory(out string gameDataDirectory))
            {
                return Path.Combine(gameDataDirectory, Path.GetFileName(CatalogRelativePath));
            }

            return Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + CatalogRelativePath;
        }
    }
}
