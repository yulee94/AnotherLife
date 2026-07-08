using System;
using AL.Core;
using UnityEngine;

namespace AL.RealmWar.Warzone
{
    [Serializable]
    public class WeatherProfileData
    {
        public string Id = "neutral_battle_fog";
        public string DisplayName = "Neutral Battle Fog";
        public RealmId RealmId = RealmId.None;

        [Header("Particles")]
        public Color ParticleStartColor = new Color(0.45f, 0.42f, 0.38f, 0.38f);
        public Color ParticleEndColor = new Color(0.72f, 0.78f, 0.82f, 0.16f);
        public int MaxParticles = 140;
        public float Radius = 24f;
        public float FallSpeed = 1.1f;
        public float ParticleSize = 0.15f;
        public float ParticleLifetime = 6f;
        public float EmissionRateMultiplier = 0.125f;
        public float HorizontalDrift = 0.35f;
        public float NoiseStrength = 0.16f;
        public float NoiseFrequency = 0.08f;

        [Header("Wind")]
        public float WindYawDegrees = 25f;
        public float WindMain = 0.22f;
        public float WindTurbulence = 0.18f;
        public float WindPulseAmplitude = 0.04f;
        public float WindPulseFrequency = 0.35f;

        [Header("Lighting")]
        public bool ApplyFog = true;
        public Color FogColor = new Color(0.28f, 0.30f, 0.32f);
        public float FogDensity = 0.012f;
        public Color AmbientColor = new Color(0.36f, 0.38f, 0.40f);
        public Color DirectionalLightColor = new Color(0.86f, 0.89f, 0.92f);
        public float DirectionalLightIntensity = 1.0f;

        [Header("Lightning")]
        public bool EnableLightning;
        public Color LightningColor = new Color(0.82f, 0.88f, 1.0f);
        public float LightningFlashIntensity = 2.4f;
        public float LightningDuration = 0.07f;
        public float LightningMinDelay = 9f;
        public float LightningMaxDelay = 18f;

        public static WeatherProfileData CreateDefault()
        {
            return new WeatherProfileData();
        }

        public static WeatherProfileData CreateForRealm(RealmId realmId)
        {
            switch (realmId)
            {
                case RealmId.Stonehold:
                    return new WeatherProfileData
                    {
                        Id = "stonehold_mountain_snow_wind",
                        DisplayName = "Mountain Snow Wind",
                        RealmId = realmId,
                        ParticleStartColor = new Color(0.82f, 0.92f, 1.0f, 0.58f),
                        ParticleEndColor = new Color(0.95f, 0.98f, 1.0f, 0.20f),
                        MaxParticles = 260,
                        Radius = 30f,
                        FallSpeed = 2.2f,
                        ParticleSize = 0.085f,
                        ParticleLifetime = 7.5f,
                        EmissionRateMultiplier = 0.16f,
                        HorizontalDrift = 1.05f,
                        NoiseStrength = 0.34f,
                        NoiseFrequency = 0.11f,
                        WindYawDegrees = 310f,
                        WindMain = 0.62f,
                        WindTurbulence = 0.42f,
                        WindPulseAmplitude = 0.18f,
                        FogColor = new Color(0.58f, 0.66f, 0.72f),
                        FogDensity = 0.019f,
                        AmbientColor = new Color(0.42f, 0.47f, 0.52f),
                        DirectionalLightColor = new Color(0.78f, 0.86f, 0.95f),
                        DirectionalLightIntensity = 0.92f
                    };
                case RealmId.Eldergrove:
                    return new WeatherProfileData
                    {
                        Id = "eldergrove_sunrain",
                        DisplayName = "Worldroot Sunrain",
                        RealmId = realmId,
                        ParticleStartColor = new Color(0.46f, 0.98f, 0.66f, 0.44f),
                        ParticleEndColor = new Color(1.0f, 0.90f, 0.34f, 0.20f),
                        MaxParticles = 190,
                        Radius = 27f,
                        FallSpeed = 1.35f,
                        ParticleSize = 0.11f,
                        ParticleLifetime = 6.8f,
                        EmissionRateMultiplier = 0.13f,
                        HorizontalDrift = 0.38f,
                        NoiseStrength = 0.22f,
                        NoiseFrequency = 0.06f,
                        WindYawDegrees = 70f,
                        WindMain = 0.18f,
                        WindTurbulence = 0.26f,
                        WindPulseAmplitude = 0.05f,
                        FogColor = new Color(0.24f, 0.48f, 0.34f),
                        FogDensity = 0.009f,
                        AmbientColor = new Color(0.40f, 0.50f, 0.36f),
                        DirectionalLightColor = new Color(0.94f, 0.92f, 0.74f),
                        DirectionalLightIntensity = 1.08f
                    };
                case RealmId.Crownlands:
                    return new WeatherProfileData
                    {
                        Id = "crownlands_clear_storm",
                        DisplayName = "Royal Road Storm",
                        RealmId = realmId,
                        ParticleStartColor = new Color(0.55f, 0.62f, 0.82f, 0.40f),
                        ParticleEndColor = new Color(0.90f, 0.78f, 0.38f, 0.14f),
                        MaxParticles = 150,
                        Radius = 28f,
                        FallSpeed = 1.65f,
                        ParticleSize = 0.12f,
                        ParticleLifetime = 5.6f,
                        EmissionRateMultiplier = 0.11f,
                        HorizontalDrift = 0.58f,
                        NoiseStrength = 0.28f,
                        NoiseFrequency = 0.10f,
                        WindYawDegrees = 210f,
                        WindMain = 0.34f,
                        WindTurbulence = 0.34f,
                        WindPulseAmplitude = 0.12f,
                        FogColor = new Color(0.30f, 0.33f, 0.44f),
                        FogDensity = 0.011f,
                        AmbientColor = new Color(0.38f, 0.40f, 0.48f),
                        DirectionalLightColor = new Color(0.82f, 0.86f, 1.0f),
                        DirectionalLightIntensity = 1.0f,
                        EnableLightning = true,
                        LightningFlashIntensity = 2.9f,
                        LightningMinDelay = 8f,
                        LightningMaxDelay = 16f
                    };
                case RealmId.Umbral:
                    return new WeatherProfileData
                    {
                        Id = "umbral_ashfall",
                        DisplayName = "Void Rift Ashfall",
                        RealmId = realmId,
                        ParticleStartColor = new Color(0.25f, 0.18f, 0.18f, 0.62f),
                        ParticleEndColor = new Color(0.95f, 0.16f, 0.08f, 0.18f),
                        MaxParticles = 280,
                        Radius = 25f,
                        FallSpeed = 0.92f,
                        ParticleSize = 0.13f,
                        ParticleLifetime = 8.2f,
                        EmissionRateMultiplier = 0.17f,
                        HorizontalDrift = 0.72f,
                        NoiseStrength = 0.46f,
                        NoiseFrequency = 0.14f,
                        WindYawDegrees = 135f,
                        WindMain = 0.42f,
                        WindTurbulence = 0.58f,
                        WindPulseAmplitude = 0.14f,
                        FogColor = new Color(0.12f, 0.08f, 0.10f),
                        FogDensity = 0.032f,
                        AmbientColor = new Color(0.22f, 0.14f, 0.16f),
                        DirectionalLightColor = new Color(0.84f, 0.38f, 0.28f),
                        DirectionalLightIntensity = 0.72f,
                        EnableLightning = true,
                        LightningColor = new Color(1.0f, 0.20f, 0.12f),
                        LightningFlashIntensity = 1.8f,
                        LightningMinDelay = 13f,
                        LightningMaxDelay = 24f
                    };
                default:
                    return CreateDefault();
            }
        }
    }
}
