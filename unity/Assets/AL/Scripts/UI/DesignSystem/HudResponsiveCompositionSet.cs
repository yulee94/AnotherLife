using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AL.UI.DesignSystem
{
    public enum UiFormFactor
    {
        PhoneLandscape = 0,
        TabletLandscape = 1,
        Pc16By9 = 2,
        PcUltrawide = 3
    }

    public enum HudSlotId
    {
        PlayerVitals = 0,
        CurrentTarget = 1,
        HostileTelegraphs = 2,
        PartySupport = 3,
        Objectives = 4,
        Route = 5,
        Allegiance = 6
    }

    [Serializable]
    public sealed class HudSlotDefinition
    {
        public HudSlotId Id;
        public Rect NormalizedRect;
        public int Priority;
        public int CollapseRank;
        public bool IsWorldCueLayer;
        public UiTypographyRole TypographyRole;
    }

    [Serializable]
    public sealed class HudCompositionDefinition
    {
        public UiFormFactor FormFactor;
        public Vector2Int ReferenceResolution;
        public Vector4 SafeAreaPadding;
        public float TextScaleMinimum;
        public float TextScaleMaximum;
        public Rect ProtectedScanPath;
        public HudSlotDefinition[] Slots = Array.Empty<HudSlotDefinition>();

        public string Signature
        {
            get
            {
                var value = new StringBuilder();
                value.Append(FormFactor);
                value.Append('|');
                AppendRect(value, ProtectedScanPath);
                if (Slots != null)
                {
                    for (int i = 0; i < Slots.Length; i++)
                    {
                        value.Append('|');
                        value.Append(Slots[i].Id);
                        value.Append(':');
                        AppendRect(value, Slots[i].NormalizedRect);
                    }
                }

                return value.ToString();
            }
        }

        public IReadOnlyList<HudSlotDefinition> SlotList => Slots;

        public bool TryGetSlot(HudSlotId id, out HudSlotDefinition slot)
        {
            if (Slots != null)
            {
                for (int i = 0; i < Slots.Length; i++)
                {
                    if (Slots[i] != null && Slots[i].Id == id)
                    {
                        slot = Slots[i];
                        return true;
                    }
                }
            }

            slot = null;
            return false;
        }

        private static void AppendRect(StringBuilder value, Rect rect)
        {
            value.Append(rect.x.ToString("0.###", CultureInfo.InvariantCulture));
            value.Append(',');
            value.Append(rect.y.ToString("0.###", CultureInfo.InvariantCulture));
            value.Append(',');
            value.Append(rect.width.ToString("0.###", CultureInfo.InvariantCulture));
            value.Append(',');
            value.Append(rect.height.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Purpose-built HUD compositions. Touch/desktop selection is explicit so
    /// identical aspect ratios cannot create an input-based information advantage.
    /// </summary>
    [Serializable]
    public sealed class HudResponsiveCompositionSet
    {
        public const string DefaultResourcePath =
            "UI/DesignSystem/AL_UI_HudResponsiveCompositions";
        public const string DefaultAssetPath =
            "Assets/AL/Resources/UI/DesignSystem/AL_UI_HudResponsiveCompositions.json";

        public string SystemId = string.Empty;
        public HudCompositionDefinition[] Compositions =
            Array.Empty<HudCompositionDefinition>();

        public static HudResponsiveCompositionSet LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>(DefaultResourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Missing HUD composition asset at Resources/{DefaultResourcePath}.json.");
            }

            HudResponsiveCompositionSet set =
                JsonUtility.FromJson<HudResponsiveCompositionSet>(asset.text);
            if (set == null ||
                string.IsNullOrWhiteSpace(set.SystemId) ||
                set.Compositions == null ||
                set.Compositions.Length == 0)
            {
                throw new InvalidOperationException("The HUD composition asset is invalid.");
            }

            return set;
        }

        public bool TryGet(UiFormFactor formFactor, out HudCompositionDefinition composition)
        {
            if (Compositions != null)
            {
                for (int i = 0; i < Compositions.Length; i++)
                {
                    HudCompositionDefinition candidate = Compositions[i];
                    if (candidate != null && candidate.FormFactor == formFactor)
                    {
                        composition = candidate;
                        return true;
                    }
                }
            }

            composition = null;
            return false;
        }

        public HudCompositionDefinition Resolve(int width, int height, bool touchPrimary)
        {
            float safeWidth = Mathf.Max(1f, width);
            float safeHeight = Mathf.Max(1f, height);
            float aspect = Mathf.Max(safeWidth, safeHeight) / Mathf.Min(safeWidth, safeHeight);
            UiFormFactor target = touchPrimary
                ? (aspect >= 1.8f
                    ? UiFormFactor.PhoneLandscape
                    : UiFormFactor.TabletLandscape)
                : (aspect >= 2f
                    ? UiFormFactor.PcUltrawide
                    : UiFormFactor.Pc16By9);

            if (TryGet(target, out HudCompositionDefinition composition))
            {
                return composition;
            }

            throw new InvalidOperationException($"Missing authored HUD composition for {target}.");
        }
    }

    public static class HudLayoutProjection
    {
        public static Rect ApplySafeAreaPadding(
            Rect physicalSafeArea,
            HudCompositionDefinition composition)
        {
            if (composition == null ||
                composition.ReferenceResolution.x <= 0 ||
                composition.ReferenceResolution.y <= 0)
            {
                return physicalSafeArea;
            }

            float scaleX = physicalSafeArea.width / composition.ReferenceResolution.x;
            float scaleY = physicalSafeArea.height / composition.ReferenceResolution.y;
            float left = Mathf.Max(0f, composition.SafeAreaPadding.x * scaleX);
            float bottom = Mathf.Max(0f, composition.SafeAreaPadding.y * scaleY);
            float right = Mathf.Max(0f, composition.SafeAreaPadding.z * scaleX);
            float top = Mathf.Max(0f, composition.SafeAreaPadding.w * scaleY);
            FitPadding(ref left, ref right, physicalSafeArea.width);
            FitPadding(ref bottom, ref top, physicalSafeArea.height);

            return new Rect(
                physicalSafeArea.xMin + left,
                physicalSafeArea.yMin + bottom,
                Mathf.Max(0f, physicalSafeArea.width - left - right),
                Mathf.Max(0f, physicalSafeArea.height - bottom - top));
        }

        public static Rect Project(Rect safeArea, Rect normalizedRect)
        {
            float x = safeArea.xMin + normalizedRect.xMin * safeArea.width;
            float y = safeArea.yMin + normalizedRect.yMin * safeArea.height;
            return new Rect(
                x,
                y,
                normalizedRect.width * safeArea.width,
                normalizedRect.height * safeArea.height);
        }

        public static float ClampTextScale(
            HudCompositionDefinition composition,
            float requestedScale)
        {
            if (composition == null)
            {
                return 1f;
            }

            return Mathf.Clamp(
                requestedScale,
                composition.TextScaleMinimum,
                composition.TextScaleMaximum);
        }

        public static bool HasPanelOverlapWithProtectedScanPath(
            HudCompositionDefinition composition)
        {
            if (composition == null || composition.Slots == null)
            {
                return true;
            }

            return composition.Slots.Any(slot =>
                slot == null ||
                (!slot.IsWorldCueLayer &&
                 slot.NormalizedRect.Overlaps(composition.ProtectedScanPath)));
        }

        private static void FitPadding(ref float leading, ref float trailing, float extent)
        {
            float total = leading + trailing;
            float maximum = Mathf.Max(0f, extent * 0.9f);
            if (total <= maximum || total <= 0f)
            {
                return;
            }

            float scale = maximum / total;
            leading *= scale;
            trailing *= scale;
        }
    }
}
