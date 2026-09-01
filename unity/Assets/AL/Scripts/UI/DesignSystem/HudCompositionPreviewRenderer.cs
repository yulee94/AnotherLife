using System;
using System.Collections.Generic;
using AL.UI.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace AL.UI.DesignSystem
{
    public sealed class HudSlotView
    {
        public HudSlotView(
            HudSlotDefinition definition,
            RectTransform root,
            Text label,
            RectTransform nonColorCueRoot,
            RectTransform patternRoot)
        {
            Definition = definition;
            Root = root;
            Label = label;
            NonColorCueRoot = nonColorCueRoot;
            PatternRoot = patternRoot;
        }

        public HudSlotDefinition Definition { get; }
        public RectTransform Root { get; }
        public Text Label { get; }
        public RectTransform NonColorCueRoot { get; }
        public RectTransform PatternRoot { get; }
    }

    public sealed class HudCompositionRenderResult
    {
        public HudCompositionRenderResult(
            RectTransform root,
            RectTransform protectedScanPath,
            IReadOnlyList<HudSlotView> slotViews)
        {
            Root = root;
            ProtectedScanPath = protectedScanPath;
            SlotViews = slotViews;
        }

        public RectTransform Root { get; }
        public RectTransform ProtectedScanPath { get; }
        public IReadOnlyList<HudSlotView> SlotViews { get; }
    }

    /// <summary>
    /// Reusable composition renderer for deterministic review fixtures and HUD hosts.
    /// It renders slots and the protected world-cue layer; gameplay data is bound by
    /// downstream components and is never authored here.
    /// </summary>
    public static class HudCompositionPreviewRenderer
    {
        public static HudCompositionRenderResult Build(
            RectTransform parent,
            HudCompositionDefinition composition,
            UiProductionDesignTokens tokens,
            UiAccessibilitySettings accessibility)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (composition == null)
            {
                throw new ArgumentNullException(nameof(composition));
            }

            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            var rootObject = new GameObject(
                "HudComposition_" + composition.FormFactor,
                typeof(RectTransform),
                typeof(CanvasGroup));
            rootObject.transform.SetParent(parent, false);
            RectTransform root = rootObject.GetComponent<RectTransform>();
            Stretch(root);
            CanvasGroup canvasGroup = rootObject.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            RectTransform scanPath = CreateRegion(
                root,
                "ProtectedPvpScanPath",
                composition.ProtectedScanPath);
            Image scanImage = scanPath.gameObject.AddComponent<Image>();
            scanImage.color = WithAlpha(tokens.EdgeColor, 0.025f);
            scanImage.raycastTarget = false;
            var scanOutline = scanPath.gameObject.AddComponent<Outline>();
            scanOutline.effectColor = WithAlpha(tokens.EdgeColor, 0.32f);
            scanOutline.effectDistance = new Vector2(1f, -1f);
            scanOutline.useGraphicAlpha = false;

            UiAccessibilityPresentation presentation =
                tokens.ResolveAccessibility(accessibility);
            Font font = PresentationChrome.ResolveFont();
            var views = new List<HudSlotView>(composition.Slots?.Length ?? 0);
            if (composition.Slots != null)
            {
                for (int i = 0; i < composition.Slots.Length; i++)
                {
                    HudSlotDefinition slot = composition.Slots[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    UiStateTreatment treatment = tokens.GetStateTreatment(StateFor(slot.Id));
                    RectTransform slotRoot = CreateRegion(
                        root,
                        "Slot_" + slot.Id,
                        slot.NormalizedRect);
                    if (!slot.IsWorldCueLayer)
                    {
                        Image surface = slotRoot.gameObject.AddComponent<Image>();
                        surface.color = WithAlpha(tokens.SurfaceColor, tokens.SurfaceOpacity);
                        surface.raycastTarget = false;
                        var outline = slotRoot.gameObject.AddComponent<Outline>();
                        outline.effectColor = WithAlpha(treatment.Color, 0.88f);
                        float border = Mathf.Max(1f, treatment.BorderWidth);
                        outline.effectDistance = new Vector2(border, -border);
                        outline.useGraphicAlpha = false;
                    }

                    RectTransform patternRoot = CreateSurfacePattern(
                        slotRoot,
                        treatment,
                        slot.IsWorldCueLayer);
                    RectTransform cueRoot = CreateNonColorCue(
                        slotRoot,
                        treatment);
                    Text label = CreateLabel(
                        slotRoot,
                        font,
                        LabelFor(slot.Id, treatment),
                        slot.IsWorldCueLayer ? treatment.Color : tokens.TextPrimaryColor,
                        Mathf.RoundToInt(
                            tokens.GetTypography(slot.TypographyRole).BaseSize *
                            presentation.TextScale),
                        slot.IsWorldCueLayer);
                    views.Add(new HudSlotView(
                        slot,
                        slotRoot,
                        label,
                        cueRoot,
                        patternRoot));
                }
            }

            return new HudCompositionRenderResult(root, scanPath, views);
        }

        private static RectTransform CreateRegion(
            Transform parent,
            string name,
            Rect normalizedRect)
        {
            var region = new GameObject(name, typeof(RectTransform));
            region.transform.SetParent(parent, false);
            RectTransform rect = region.GetComponent<RectTransform>();
            rect.anchorMin = normalizedRect.min;
            rect.anchorMax = normalizedRect.max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text CreateLabel(
            Transform parent,
            Font font,
            string value,
            Color color,
            int size,
            bool worldCueLayer)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = worldCueLayer
                ? new Vector2(0.18f, 0.92f)
                : new Vector2(0f, 0f);
            rect.anchorMax = worldCueLayer
                ? new Vector2(0.82f, 1f)
                : new Vector2(1f, 1f);
            rect.offsetMin = worldCueLayer ? Vector2.zero : new Vector2(10f, 6f);
            rect.offsetMax = worldCueLayer ? Vector2.zero : new Vector2(-10f, -6f);

            Text label = labelObject.AddComponent<Text>();
            label.font = font;
            label.text = value;
            label.fontSize = Mathf.Max(11, size);
            label.color = color;
            label.alignment = worldCueLayer
                ? TextAnchor.UpperCenter
                : TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform CreateNonColorCue(
            Transform parent,
            UiStateTreatment treatment)
        {
            RectTransform root = CreateTreatmentRoot(
                parent,
                "NonColorCue_" + treatment.NonColorCue);
            Color color = WithAlpha(treatment.Color, 0.9f);
            switch (treatment.NonColorCue)
            {
                case UiNonColorCue.DoubleRail:
                    AddBar(root, "UpperRail", new Vector2(0.12f, 0.76f), new Vector2(42f, 2f), 0f, color);
                    AddBar(root, "LowerRail", new Vector2(0.12f, 0.66f), new Vector2(42f, 2f), 0f, color);
                    break;
                case UiNonColorCue.RoundedShield:
                    AddBar(root, "ShieldSpine", new Vector2(0.05f, 0.5f), new Vector2(3f, 28f), 0f, color);
                    AddBar(root, "ShieldPoint", new Vector2(0.05f, 0.34f), new Vector2(9f, 9f), 45f, color);
                    break;
                case UiNonColorCue.SawtoothFrame:
                    AddBar(root, "ToothA", new Vector2(0.025f, 0.36f), new Vector2(8f, 8f), 45f, color);
                    AddBar(root, "ToothB", new Vector2(0.025f, 0.5f), new Vector2(8f, 8f), 45f, color);
                    AddBar(root, "ToothC", new Vector2(0.025f, 0.64f), new Vector2(8f, 8f), 45f, color);
                    break;
                case UiNonColorCue.DiamondNotch:
                    AddBar(root, "Diamond", new Vector2(0.5f, 0.98f), new Vector2(10f, 10f), 45f, color);
                    break;
                case UiNonColorCue.UpwardChevron:
                    AddBar(root, "ChevronLeft", new Vector2(0.48f, 0.91f), new Vector2(20f, 3f), 35f, color);
                    AddBar(root, "ChevronRight", new Vector2(0.52f, 0.91f), new Vector2(20f, 3f), -35f, color);
                    break;
                case UiNonColorCue.CrossedBar:
                    AddBar(root, "CrossA", new Vector2(0.06f, 0.5f), new Vector2(22f, 3f), 45f, color);
                    AddBar(root, "CrossB", new Vector2(0.06f, 0.5f), new Vector2(22f, 3f), -45f, color);
                    break;
                case UiNonColorCue.BrokenFrame:
                    AddBar(root, "BrokenLeft", new Vector2(0.2f, 0.96f), new Vector2(32f, 3f), 0f, color);
                    AddBar(root, "BrokenRight", new Vector2(0.8f, 0.96f), new Vector2(32f, 3f), 0f, color);
                    break;
                case UiNonColorCue.CornerBrackets:
                    AddBar(root, "CornerTopLeft", new Vector2(0.05f, 0.92f), new Vector2(24f, 3f), 0f, color);
                    AddBar(root, "CornerBottomRight", new Vector2(0.95f, 0.08f), new Vector2(24f, 3f), 0f, color);
                    AddBar(root, "CornerLeft", new Vector2(0.02f, 0.86f), new Vector2(3f, 18f), 0f, color);
                    AddBar(root, "CornerRight", new Vector2(0.98f, 0.14f), new Vector2(3f, 18f), 0f, color);
                    break;
            }

            return root;
        }

        private static RectTransform CreateSurfacePattern(
            Transform parent,
            UiStateTreatment treatment,
            bool worldCueLayer)
        {
            RectTransform root = CreateTreatmentRoot(
                parent,
                "SurfacePattern_" + treatment.Pattern);
            Color color = WithAlpha(treatment.Color, worldCueLayer ? 0.24f : 0.12f);
            switch (treatment.Pattern)
            {
                case UiSurfacePattern.BrushedMetal:
                    AddParallelBars(root, color, 0f, 3, 0.18f);
                    break;
                case UiSurfacePattern.WovenFiber:
                    AddParallelBars(root, color, 0f, 2, 0.18f);
                    AddParallelBars(root, color, 90f, 2, 0.18f);
                    break;
                case UiSurfacePattern.ScoredStone:
                    AddParallelBars(root, color, 24f, 3, 0.09f);
                    break;
                case UiSurfacePattern.DiagonalCut:
                    AddParallelBars(root, color, -36f, 3, 0.09f);
                    break;
                case UiSurfacePattern.RisingWeave:
                    AddParallelBars(root, color, 32f, 2, 0.12f);
                    AddParallelBars(root, color, -32f, 2, 0.12f);
                    break;
                case UiSurfacePattern.CrossHatch:
                    AddParallelBars(root, color, 45f, 3, 0.09f);
                    AddParallelBars(root, color, -45f, 3, 0.09f);
                    break;
                case UiSurfacePattern.InterruptedGrain:
                    AddBar(root, "GrainA", new Vector2(0.28f, 0.12f), new Vector2(38f, 2f), 0f, color);
                    AddBar(root, "GrainB", new Vector2(0.68f, 0.12f), new Vector2(54f, 2f), 0f, color);
                    AddBar(root, "GrainC", new Vector2(0.47f, 0.2f), new Vector2(28f, 2f), 0f, color);
                    break;
                case UiSurfacePattern.FineInlay:
                    AddBar(root, "InlayTop", new Vector2(0.5f, 0.9f), new Vector2(70f, 1.5f), 0f, color);
                    AddBar(root, "InlayBottom", new Vector2(0.5f, 0.1f), new Vector2(70f, 1.5f), 0f, color);
                    AddBar(root, "InlayLeft", new Vector2(0.08f, 0.5f), new Vector2(1.5f, 34f), 0f, color);
                    AddBar(root, "InlayRight", new Vector2(0.92f, 0.5f), new Vector2(1.5f, 34f), 0f, color);
                    break;
            }

            return root;
        }

        private static RectTransform CreateTreatmentRoot(Transform parent, string name)
        {
            var rootObject = new GameObject(name, typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            RectTransform root = rootObject.GetComponent<RectTransform>();
            Stretch(root);
            return root;
        }

        private static void AddParallelBars(
            Transform parent,
            Color color,
            float rotation,
            int count,
            float baseY)
        {
            for (int i = 0; i < count; i++)
            {
                float x = (i + 1f) / (count + 1f);
                AddBar(
                    parent,
                    "Mark_" + i,
                    new Vector2(x, baseY + i * 0.045f),
                    new Vector2(34f, 1.5f),
                    rotation,
                    color);
            }
        }

        private static Image AddBar(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 size,
            float rotation,
            Color color)
        {
            var barObject = new GameObject(name, typeof(RectTransform));
            barObject.transform.SetParent(parent, false);
            RectTransform rect = barObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = barObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static UiSemanticState StateFor(HudSlotId id)
        {
            switch (id)
            {
                case HudSlotId.CurrentTarget:
                    return UiSemanticState.Focused;
                case HudSlotId.HostileTelegraphs:
                    return UiSemanticState.Hostile;
                case HudSlotId.PartySupport:
                case HudSlotId.Allegiance:
                    return UiSemanticState.Friendly;
                case HudSlotId.Objectives:
                    return UiSemanticState.Warning;
                default:
                    return UiSemanticState.Neutral;
            }
        }

        private static string LabelFor(HudSlotId id, UiStateTreatment treatment)
        {
            string name;
            switch (id)
            {
                case HudSlotId.PlayerVitals:
                    name = "PLAYER VITALS / CONTROL";
                    break;
                case HudSlotId.CurrentTarget:
                    name = "CURRENT TARGET / CAST / BREAK";
                    break;
                case HudSlotId.HostileTelegraphs:
                    name = "HOSTILE TELEGRAPHS — WORLD CUES ONLY";
                    break;
                case HudSlotId.PartySupport:
                    name = "PARTY / SUPPORT STATE";
                    break;
                case HudSlotId.Objectives:
                    name = "OBJECTIVE / CONTEST / TIMER";
                    break;
                case HudSlotId.Route:
                    name = "ROUTE / NEXT ANCHOR";
                    break;
                case HudSlotId.Allegiance:
                    name = "ALLEGIANCE / COMMAND";
                    break;
                default:
                    name = id.ToString().ToUpperInvariant();
                    break;
            }

            return treatment.LabelPrefix + "  " + name;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
