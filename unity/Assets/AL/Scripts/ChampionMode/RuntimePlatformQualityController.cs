using System;
using UnityEngine;

namespace AL.ChampionMode
{
    [Serializable]
    public class RuntimeQualityProfile
    {
        public string Tier = "desktop_standard";
        public int TargetFrameRate = 60;
        public int BotChampionBudget = 40;
        public int DummyBudget = 16;
        public int WorldMarkerBudget = 8;
        public int AmbientTerrestrialBudget = 10;
        public float WeatherParticleMultiplier = 1.0f;
        public float ShadowDistance = 42f;
        public int PixelLightCount = 2;
        public float LodBias = 1.0f;
        public int TextureMipmapLimit;
    }

    public class RuntimePlatformQualityController : MonoBehaviour
    {
        [SerializeField] private RuntimeQualityProfile _currentProfile = new RuntimeQualityProfile();
        [SerializeField] private bool _applyOnAwake;

        public RuntimeQualityProfile CurrentProfile => _currentProfile;

        private void Awake()
        {
            if (_applyOnAwake)
            {
                Apply();
            }
        }

        public RuntimeQualityProfile Apply()
        {
            _currentProfile = DetectProfile();
            ApplyUnityQuality(_currentProfile);
            Debug.Log($"[RuntimeQuality] Applied {_currentProfile.Tier}: {_currentProfile.TargetFrameRate}fps, bots {_currentProfile.BotChampionBudget}, weather x{_currentProfile.WeatherParticleMultiplier:0.00}");
            return _currentProfile;
        }

        public int GetBotChampionBudget(int requested)
        {
            return Mathf.Clamp(requested, 0, Mathf.Max(0, _currentProfile.BotChampionBudget));
        }

        public int GetDummyBudget(int requested)
        {
            return Mathf.Clamp(requested, 0, Mathf.Max(0, _currentProfile.DummyBudget));
        }

        public int GetWorldMarkerBudget(int requested)
        {
            return Mathf.Clamp(requested, 0, Mathf.Max(1, _currentProfile.WorldMarkerBudget));
        }

        public int GetAmbientTerrestrialBudget(int requested)
        {
            return Mathf.Clamp(requested, 0, Mathf.Max(0, _currentProfile.AmbientTerrestrialBudget));
        }

        public float GetWeatherParticleMultiplier()
        {
            return Mathf.Clamp(_currentProfile.WeatherParticleMultiplier, 0.15f, 1.25f);
        }

        private static RuntimeQualityProfile DetectProfile()
        {
            bool mobile = Application.isMobilePlatform;
            int memoryMb = SystemInfo.systemMemorySize;
            int graphicsMemoryMb = SystemInfo.graphicsMemorySize;
            bool constrainedMemory = memoryMb > 0 && memoryMb < 4096;
            bool constrainedGraphics = graphicsMemoryMb > 0 && graphicsMemoryMb < 1536;

            if (mobile && (constrainedMemory || constrainedGraphics))
            {
                return new RuntimeQualityProfile
                {
                    Tier = "mobile_low",
                    TargetFrameRate = 30,
                    BotChampionBudget = 16,
                    DummyBudget = 8,
                    WorldMarkerBudget = 5,
                    AmbientTerrestrialBudget = 3,
                    WeatherParticleMultiplier = 0.45f,
                    ShadowDistance = 16f,
                    PixelLightCount = 0,
                    LodBias = 0.55f,
                    TextureMipmapLimit = 1
                };
            }

            if (mobile)
            {
                return new RuntimeQualityProfile
                {
                    Tier = "mobile_standard",
                    TargetFrameRate = 45,
                    BotChampionBudget = 24,
                    DummyBudget = 12,
                    WorldMarkerBudget = 6,
                    AmbientTerrestrialBudget = 5,
                    WeatherParticleMultiplier = 0.65f,
                    ShadowDistance = 24f,
                    PixelLightCount = 1,
                    LodBias = 0.72f,
                    TextureMipmapLimit = 0
                };
            }

            if (constrainedMemory || constrainedGraphics)
            {
                return new RuntimeQualityProfile
                {
                    Tier = "desktop_low",
                    TargetFrameRate = 60,
                    BotChampionBudget = 32,
                    DummyBudget = 12,
                    WorldMarkerBudget = 7,
                    AmbientTerrestrialBudget = 7,
                    WeatherParticleMultiplier = 0.80f,
                    ShadowDistance = 32f,
                    PixelLightCount = 1,
                    LodBias = 0.85f,
                    TextureMipmapLimit = 0
                };
            }

            return new RuntimeQualityProfile();
        }

        private static void ApplyUnityQuality(RuntimeQualityProfile profile)
        {
            Application.targetFrameRate = Mathf.Max(30, profile.TargetFrameRate);
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = Mathf.Max(0f, profile.ShadowDistance);
            QualitySettings.pixelLightCount = Mathf.Max(0, profile.PixelLightCount);
            QualitySettings.lodBias = Mathf.Max(0.25f, profile.LodBias);
            QualitySettings.globalTextureMipmapLimit = Mathf.Max(0, profile.TextureMipmapLimit);
        }
    }
}
