using System;
using System.IO;
using AL.Data.Catalogs;
using AL.Services.Local;
using UnityEngine;

namespace AL.ChampionMode.Control
{
    public sealed class CombatControlProfile
    {
        public CombatControlProfile(
            float jumpHeightMeters,
            float gravityMetersPerSecondSquared,
            float coyoteTimeSeconds,
            float jumpBufferSeconds,
            float airControlMultiplier,
            float resolveMinimumDurationMultiplier,
            float resolveGainPerSecond,
            float resolveDecayDelaySeconds,
            float resolveDecayPerSecond,
            float hardControlMaximumSeconds,
            float hardControlImmunitySeconds,
            float defaultControlResistance)
        {
            JumpHeightMeters = jumpHeightMeters;
            GravityMetersPerSecondSquared = gravityMetersPerSecondSquared;
            CoyoteTimeSeconds = coyoteTimeSeconds;
            JumpBufferSeconds = jumpBufferSeconds;
            AirControlMultiplier = airControlMultiplier;
            ResolveMinimumDurationMultiplier = resolveMinimumDurationMultiplier;
            ResolveGainPerSecond = resolveGainPerSecond;
            ResolveDecayDelaySeconds = resolveDecayDelaySeconds;
            ResolveDecayPerSecond = resolveDecayPerSecond;
            HardControlMaximumSeconds = hardControlMaximumSeconds;
            HardControlImmunitySeconds = hardControlImmunitySeconds;
            DefaultControlResistance = defaultControlResistance;
        }

        public float JumpHeightMeters { get; }
        public float GravityMetersPerSecondSquared { get; }
        public float CoyoteTimeSeconds { get; }
        public float JumpBufferSeconds { get; }
        public float AirControlMultiplier { get; }
        public float ResolveMinimumDurationMultiplier { get; }
        public float ResolveGainPerSecond { get; }
        public float ResolveDecayDelaySeconds { get; }
        public float ResolveDecayPerSecond { get; }
        public float HardControlMaximumSeconds { get; }
        public float HardControlImmunitySeconds { get; }
        public float DefaultControlResistance { get; }
    }

    public static class CombatControlCatalog
    {
        public static bool TryLoad(out CombatControlProfile profile)
        {
            profile = null;
            string path = BuildCatalogPath();
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                return TryParse(File.ReadAllText(path), out profile);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[CombatControlCatalog] Could not load combat-control profile. " +
                    exception.Message);
                return false;
            }
        }

        public static bool TryParse(string json, out CombatControlProfile profile)
        {
            profile = null;
            GameDataFamilyCatalogSnapshot family;
            string diagnosticCode;
            if (!WireFamilyCatalogLoader.TryLoad(
                    "skill_weather",
                    json,
                    out family,
                    out diagnosticCode))
            {
                return false;
            }

            var records = WireFamilyCatalogLoader.RecordsOfKind(
                family,
                "combat_control_profile");
            if (records.Count != 1)
            {
                return false;
            }

            GameDataCatalogRecord record = records[0];
            if (!TryRequiredFloat(record, "jump_height_meters", out float jumpHeight) ||
                !TryRequiredFloat(record, "gravity_meters_per_second_squared", out float gravity) ||
                !TryRequiredFloat(record, "coyote_time_seconds", out float coyoteTime) ||
                !TryRequiredFloat(record, "jump_buffer_seconds", out float jumpBuffer) ||
                !TryRequiredFloat(record, "air_control_multiplier", out float airControl) ||
                !TryRequiredFloat(record, "resolve_min_duration_multiplier", out float minimumDuration) ||
                !TryRequiredFloat(record, "resolve_gain_per_second", out float resolveGain) ||
                !TryRequiredFloat(record, "resolve_decay_delay_seconds", out float resolveDelay) ||
                !TryRequiredFloat(record, "resolve_decay_per_second", out float resolveDecay) ||
                !TryRequiredFloat(record, "hard_control_max_seconds", out float hardControlMaximum) ||
                !TryRequiredFloat(record, "hard_control_immunity_seconds", out float hardControlImmunity) ||
                !TryRequiredFloat(record, "default_control_resistance", out float defaultResistance))
            {
                return false;
            }

            if (jumpHeight <= 0f ||
                gravity >= 0f ||
                coyoteTime < 0f ||
                jumpBuffer < 0f ||
                airControl < 0f || airControl > 1f ||
                minimumDuration < 0f || minimumDuration > 1f ||
                resolveGain < 0f ||
                resolveDelay < 0f ||
                resolveDecay < 0f ||
                hardControlMaximum <= 0f ||
                hardControlImmunity < 0f ||
                defaultResistance < 0f || defaultResistance > 1f)
            {
                return false;
            }

            profile = new CombatControlProfile(
                jumpHeight,
                gravity,
                coyoteTime,
                jumpBuffer,
                airControl,
                minimumDuration,
                resolveGain,
                resolveDelay,
                resolveDecay,
                hardControlMaximum,
                hardControlImmunity,
                defaultResistance);
            return true;
        }

        private static bool TryRequiredFloat(
            GameDataCatalogRecord record,
            string field,
            out float value)
        {
            return WireFamilyCatalogLoader.TryGetFloat(record, field, out value) &&
                   !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private static string BuildCatalogPath()
        {
            if (SixFamilyRuntimeCatalog.TryResolveGameDataDirectory(
                    out string gameDataDirectory))
            {
                return Path.Combine(gameDataDirectory, "skill_weather.v1.json");
            }

            return Application.streamingAssetsPath.TrimEnd('/', '\\') +
                   "/GameData/skill_weather.v1.json";
        }
    }
}
