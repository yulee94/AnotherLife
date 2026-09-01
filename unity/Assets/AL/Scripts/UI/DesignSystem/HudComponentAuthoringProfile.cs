using System;
using UnityEngine;

namespace AL.UI.DesignSystem
{
    public enum HudComponentLayer
    {
        Standard = 0,
        CriticalPanel = 1,
        CriticalWorldCue = 2
    }

    public enum HudComponentTemplate
    {
        Vitals = 0,
        CurrentTarget = 1,
        HostileTelegraph = 2,
        PartySupport = 3,
        Objectives = 4,
        Route = 5,
        Allegiance = 6
    }

    [Serializable]
    public sealed class HudComponentAuthoringDefinition
    {
        public HudSlotId Slot;
        public HudComponentLayer Layer;
        public HudComponentTemplate Template;
        public UiSemanticState DefaultState;
        public UiTypographyRole HeaderRole;
        public UiTypographyRole PrimaryRole;
        public UiTypographyRole SecondaryRole;
        public int MaxVisibleRows;
        public float LocalizationExpansion;
        public bool ShowSurface;
        public bool ProtectFromOcclusion;
        public bool AggregateOverflow;
    }

    /// <summary>
    /// Designer-authored component behavior. It controls purpose, layer, density,
    /// localization allowance, and overflow policy without owning gameplay values.
    /// </summary>
    [Serializable]
    public sealed class HudComponentAuthoringProfile
    {
        public const string DefaultResourcePath =
            "UI/DesignSystem/AL_UI_HudComponentAuthoring";
        public const string DefaultAssetPath =
            "Assets/AL/Resources/UI/DesignSystem/AL_UI_HudComponentAuthoring.json";

        public string SystemId = string.Empty;
        public HudComponentAuthoringDefinition[] Components =
            Array.Empty<HudComponentAuthoringDefinition>();

        public static HudComponentAuthoringProfile LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>(DefaultResourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Missing HUD component authoring asset at Resources/{DefaultResourcePath}.json.");
            }

            HudComponentAuthoringProfile profile =
                JsonUtility.FromJson<HudComponentAuthoringProfile>(asset.text);
            if (profile == null ||
                string.IsNullOrWhiteSpace(profile.SystemId) ||
                profile.Components == null ||
                profile.Components.Length == 0)
            {
                throw new InvalidOperationException("The HUD component authoring asset is invalid.");
            }

            return profile;
        }

        public HudComponentAuthoringDefinition Get(HudSlotId slot)
        {
            if (Components != null)
            {
                for (int i = 0; i < Components.Length; i++)
                {
                    HudComponentAuthoringDefinition candidate = Components[i];
                    if (candidate != null && candidate.Slot == slot)
                    {
                        return candidate;
                    }
                }
            }

            throw new InvalidOperationException($"Missing HUD component authoring for {slot}.");
        }
    }
}
