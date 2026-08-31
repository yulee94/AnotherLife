using System;
using UnityEngine;

namespace AL.UI.DesignSystem
{
    public enum UiTypographyRole
    {
        Display = 0,
        Title = 1,
        Body = 2,
        Action = 3,
        Caption = 4,
        Numeric = 5
    }

    public enum UiSemanticState
    {
        Neutral = 0,
        Friendly = 1,
        Hostile = 2,
        Warning = 3,
        Success = 4,
        Disabled = 5,
        Stale = 6,
        Focused = 7
    }

    public enum UiNonColorCue
    {
        None = 0,
        DoubleRail = 1,
        RoundedShield = 2,
        SawtoothFrame = 3,
        DiamondNotch = 4,
        UpwardChevron = 5,
        CrossedBar = 6,
        BrokenFrame = 7,
        CornerBrackets = 8
    }

    public enum UiSurfacePattern
    {
        None = 0,
        BrushedMetal = 1,
        WovenFiber = 2,
        ScoredStone = 3,
        DiagonalCut = 4,
        RisingWeave = 5,
        CrossHatch = 6,
        InterruptedGrain = 7,
        FineInlay = 8
    }

    [Serializable]
    public sealed class UiTypographyToken
    {
        public UiTypographyRole Role;
        public string Family = string.Empty;
        public float BaseSize;
        public int Weight;
        public float LineHeight;
        public float Tracking;
        public bool Uppercase;
    }

    [Serializable]
    public sealed class UiStateTreatment
    {
        public UiSemanticState State;
        public Color Color = Color.white;
        public UiNonColorCue NonColorCue;
        public UiSurfacePattern Pattern;
        public string LabelPrefix = string.Empty;
        public float BorderWidth;
    }

    [Serializable]
    public sealed class UiMotionTokens
    {
        public float FocusTransitionSeconds;
        public float PanelTransitionSeconds;
        public float FocusHoldSeconds;
        public float AmbientMotionScale;
        public float FlashOpacity;
        public float VfxDensity;
    }

    public readonly struct UiAccessibilitySettings
    {
        public UiAccessibilitySettings(
            float textScale,
            bool reducedMotion,
            bool reducedFlash,
            bool reducedVfx)
        {
            TextScale = textScale;
            ReducedMotion = reducedMotion;
            ReducedFlash = reducedFlash;
            ReducedVfx = reducedVfx;
        }

        public float TextScale { get; }
        public bool ReducedMotion { get; }
        public bool ReducedFlash { get; }
        public bool ReducedVfx { get; }
    }

    public readonly struct UiAccessibilityPresentation
    {
        public UiAccessibilityPresentation(
            float textScale,
            float focusTransitionSeconds,
            float panelTransitionSeconds,
            float focusHoldSeconds,
            float ambientMotionScale,
            float flashOpacity,
            float vfxDensity)
        {
            TextScale = textScale;
            FocusTransitionSeconds = focusTransitionSeconds;
            PanelTransitionSeconds = panelTransitionSeconds;
            FocusHoldSeconds = focusHoldSeconds;
            AmbientMotionScale = ambientMotionScale;
            FlashOpacity = flashOpacity;
            VfxDensity = vfxDensity;
        }

        public float TextScale { get; }
        public float FocusTransitionSeconds { get; }
        public float PanelTransitionSeconds { get; }
        public float FocusHoldSeconds { get; }
        public float AmbientMotionScale { get; }
        public float FlashOpacity { get; }
        public float VfxDensity { get; }
    }

    /// <summary>
    /// Asset-backed visual tokens for the post-MVP UI candidate. The JSON asset is
    /// presentation data only; gameplay and save authority remain in GameData catalogs.
    /// </summary>
    [Serializable]
    public sealed class UiProductionDesignTokens
    {
        public const string DefaultResourcePath =
            "UI/DesignSystem/AL_UI_ProductionDesignTokens";
        public const string DefaultAssetPath =
            "Assets/AL/Resources/UI/DesignSystem/AL_UI_ProductionDesignTokens.json";

        public string SystemId = string.Empty;
        public string StyleName = string.Empty;
        public string DisplayFamilyStatus = string.Empty;
        public string IconGrammar = string.Empty;
        public string MaterialGrammar = string.Empty;
        public UiTypographyToken[] Typography = Array.Empty<UiTypographyToken>();
        public float[] Spacing = Array.Empty<float>();
        public float[] ElevationLevels = Array.Empty<float>();
        public float MinimumHitTarget;
        public float SurfaceOpacity;
        public float GlyphGlowOpacity;
        public float CornerRadiusSmall;
        public float CornerRadiusLarge;
        public Color CanvasColor = Color.black;
        public Color SurfaceColor = Color.black;
        public Color RaisedSurfaceColor = Color.black;
        public Color InsetSurfaceColor = Color.black;
        public Color EdgeColor = Color.white;
        public Color TextPrimaryColor = Color.white;
        public Color TextSecondaryColor = Color.gray;
        public UiMotionTokens Motion = new UiMotionTokens();
        public UiMotionTokens ReducedMotion = new UiMotionTokens();
        public UiStateTreatment[] StateTreatments = Array.Empty<UiStateTreatment>();

        public static UiProductionDesignTokens LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>(DefaultResourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Missing UI token asset at Resources/{DefaultResourcePath}.json.");
            }

            UiProductionDesignTokens tokens =
                JsonUtility.FromJson<UiProductionDesignTokens>(asset.text);
            if (tokens == null || string.IsNullOrWhiteSpace(tokens.SystemId))
            {
                throw new InvalidOperationException("The production UI token asset is invalid.");
            }

            return tokens;
        }

        public UiStateTreatment GetStateTreatment(UiSemanticState state)
        {
            if (StateTreatments != null)
            {
                for (int i = 0; i < StateTreatments.Length; i++)
                {
                    UiStateTreatment treatment = StateTreatments[i];
                    if (treatment != null && treatment.State == state)
                    {
                        return treatment;
                    }
                }
            }

            throw new InvalidOperationException($"Missing UI state treatment for {state}.");
        }

        public UiTypographyToken GetTypography(UiTypographyRole role)
        {
            if (Typography != null)
            {
                for (int i = 0; i < Typography.Length; i++)
                {
                    UiTypographyToken token = Typography[i];
                    if (token != null && token.Role == role)
                    {
                        return token;
                    }
                }
            }

            throw new InvalidOperationException($"Missing UI typography token for {role}.");
        }

        public UiAccessibilityPresentation ResolveAccessibility(UiAccessibilitySettings settings)
        {
            float textScale = Mathf.Clamp(settings.TextScale, 0.85f, 2f);
            UiMotionTokens selectedMotion = settings.ReducedMotion ? ReducedMotion : Motion;
            float flashOpacity = settings.ReducedFlash
                ? Mathf.Min(selectedMotion.FlashOpacity, 0.08f)
                : selectedMotion.FlashOpacity;
            float vfxDensity = settings.ReducedVfx
                ? Mathf.Min(selectedMotion.VfxDensity, 0.35f)
                : selectedMotion.VfxDensity;

            return new UiAccessibilityPresentation(
                textScale,
                selectedMotion.FocusTransitionSeconds,
                selectedMotion.PanelTransitionSeconds,
                selectedMotion.FocusHoldSeconds,
                settings.ReducedMotion ? 0f : selectedMotion.AmbientMotionScale,
                flashOpacity,
                vfxDensity);
        }
    }
}
