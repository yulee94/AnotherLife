using System;
using System.Collections.Generic;
using AL.UI.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace AL.UI.DesignSystem
{
    public sealed class ProductionHudComponentView
    {
        private readonly UiProductionDesignTokens _tokens;
        private readonly Font _font;
        private readonly float _textScale;
        private readonly RectTransform _rowsRoot;
        private readonly RectTransform _metersRoot;
        private readonly Outline _surfaceOutline;
        private readonly List<Text> _rowLabels = new List<Text>();
        private readonly List<Image> _meterFills = new List<Image>();

        internal ProductionHudComponentView(
            HudSlotDefinition definition,
            HudComponentAuthoringDefinition authoring,
            UiProductionDesignTokens tokens,
            Font font,
            float textScale,
            RectTransform root,
            Rect projectedRect,
            Image surface,
            Outline surfaceOutline,
            Text header,
            Text primary,
            Text secondary,
            RectTransform rowsRoot,
            RectTransform metersRoot,
            RectTransform nonColorCueRoot,
            RectTransform patternRoot)
        {
            Definition = definition;
            Authoring = authoring;
            _tokens = tokens;
            _font = font;
            _textScale = textScale;
            Root = root;
            ProjectedRect = projectedRect;
            Surface = surface;
            _surfaceOutline = surfaceOutline;
            Header = header;
            Primary = primary;
            Secondary = secondary;
            _rowsRoot = rowsRoot;
            _metersRoot = metersRoot;
            NonColorCueRoot = nonColorCueRoot;
            PatternRoot = patternRoot;
        }

        public HudSlotDefinition Definition { get; }
        public HudComponentAuthoringDefinition Authoring { get; }
        public RectTransform Root { get; }
        public Rect ProjectedRect { get; }
        public Image Surface { get; }
        public Text Header { get; }
        public Text Primary { get; }
        public Text Secondary { get; }
        public RectTransform NonColorCueRoot { get; }
        public RectTransform PatternRoot { get; }
        public IReadOnlyList<Text> RowLabels => _rowLabels;
        public IReadOnlyList<Image> MeterFills => _meterFills;
        public int VisibleRowCount { get; private set; }
        public int OverflowCount { get; private set; }

        public void Apply(
            UiSemanticState state,
            string header,
            string primary,
            string secondary,
            string[] rows,
            float[] meters)
        {
            UiStateTreatment treatment = _tokens.GetStateTreatment(state);
            Header.text = treatment.LabelPrefix + "  " + (header ?? string.Empty);
            Header.color = treatment.Color;
            Primary.text = primary ?? string.Empty;
            Primary.color = Definition.IsWorldCueLayer
                ? treatment.Color
                : _tokens.TextPrimaryColor;
            Secondary.text = secondary ?? string.Empty;
            Secondary.color = _tokens.TextSecondaryColor;
            if (_surfaceOutline != null)
            {
                _surfaceOutline.effectColor = ProductionHudRenderer.WithAlpha(
                    treatment.Color,
                    0.92f);
                float border = Mathf.Max(1f, treatment.BorderWidth);
                _surfaceOutline.effectDistance = new Vector2(border, -border);
            }

            ProductionHudRenderer.RebuildNonColorCue(NonColorCueRoot, treatment);
            ProductionHudRenderer.RebuildPattern(
                PatternRoot,
                treatment,
                Definition.IsWorldCueLayer);
            RebuildRows(rows ?? Array.Empty<string>(), treatment);
            RebuildMeters(meters ?? Array.Empty<float>(), treatment);
        }

        private void RebuildRows(string[] rows, UiStateTreatment treatment)
        {
            ProductionHudRenderer.ClearChildren(_rowsRoot);
            _rowLabels.Clear();
            int capacity = Mathf.Max(1, Authoring.MaxVisibleRows);
            int directCount = Mathf.Min(capacity, rows.Length);
            OverflowCount = Mathf.Max(0, rows.Length - directCount);
            if (OverflowCount > 0 && Authoring.AggregateOverflow)
            {
                directCount = Mathf.Max(0, capacity - 1);
                OverflowCount = rows.Length - directCount;
            }

            for (int i = 0; i < directCount; i++)
            {
                _rowLabels.Add(ProductionHudRenderer.CreateRowLabel(
                    _rowsRoot,
                    _font,
                    "Row_" + i,
                    rows[i],
                    _tokens.GetTypography(Authoring.SecondaryRole),
                    _textScale,
                    i,
                    capacity,
                    _tokens.TextSecondaryColor));
            }

            if (OverflowCount > 0 && Authoring.AggregateOverflow)
            {
                _rowLabels.Add(ProductionHudRenderer.CreateRowLabel(
                    _rowsRoot,
                    _font,
                    "OverflowSummary",
                    "+" + OverflowCount + " MORE",
                    _tokens.GetTypography(Authoring.SecondaryRole),
                    _textScale,
                    directCount,
                    capacity,
                    treatment.Color));
            }

            VisibleRowCount = _rowLabels.Count;
        }

        private void RebuildMeters(float[] meters, UiStateTreatment treatment)
        {
            ProductionHudRenderer.ClearChildren(_metersRoot);
            _meterFills.Clear();
            int maximum = Authoring.Template == HudComponentTemplate.Vitals ||
                          Authoring.Template == HudComponentTemplate.CurrentTarget
                ? 2
                : 1;
            int count = Mathf.Min(maximum, meters.Length);
            for (int i = 0; i < count; i++)
            {
                RectTransform track = ProductionHudRenderer.CreateAnchoredRegion(
                    _metersRoot,
                    "MeterTrack_" + i,
                    new Rect(0.04f, 0.54f - i * 0.25f, 0.92f, 0.16f));
                Image trackImage = track.gameObject.AddComponent<Image>();
                trackImage.color = ProductionHudRenderer.WithAlpha(
                    _tokens.InsetSurfaceColor,
                    0.96f);
                trackImage.raycastTarget = false;

                RectTransform fillRect = ProductionHudRenderer.CreateAnchoredRegion(
                    track,
                    "MeterFill_" + i,
                    new Rect(0f, 0f, 1f, 1f));
                Image fill = fillRect.gameObject.AddComponent<Image>();
                fill.color = ProductionHudRenderer.WithAlpha(
                    treatment.Color,
                    i == 0 ? 0.92f : 0.68f);
                fill.raycastTarget = false;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = Mathf.Clamp01(meters[i]);
                _meterFills.Add(fill);
            }
        }
    }

    public sealed class ProductionHudRenderResult
    {
        private readonly Dictionary<HudSlotId, ProductionHudComponentView> _views;

        internal ProductionHudRenderResult(
            RectTransform root,
            HudCompositionDefinition composition,
            Rect usableSafeArea,
            Rect protectedScanRect,
            RectTransform protectedScanPath,
            RectTransform standardLayer,
            RectTransform transientLayer,
            RectTransform criticalPanelLayer,
            RectTransform criticalWorldCueLayer,
            List<ProductionHudComponentView> componentViews)
        {
            Root = root;
            Composition = composition;
            UsableSafeArea = usableSafeArea;
            ProtectedScanRect = protectedScanRect;
            ProtectedScanPath = protectedScanPath;
            StandardLayer = standardLayer;
            TransientLayer = transientLayer;
            CriticalPanelLayer = criticalPanelLayer;
            CriticalWorldCueLayer = criticalWorldCueLayer;
            ComponentViews = componentViews.AsReadOnly();
            _views = new Dictionary<HudSlotId, ProductionHudComponentView>(componentViews.Count);
            for (int i = 0; i < componentViews.Count; i++)
            {
                _views.Add(componentViews[i].Definition.Id, componentViews[i]);
            }
        }

        public RectTransform Root { get; }
        public HudCompositionDefinition Composition { get; }
        public Rect UsableSafeArea { get; }
        public Rect ProtectedScanRect { get; }
        public RectTransform ProtectedScanPath { get; }
        public RectTransform StandardLayer { get; }
        public RectTransform TransientLayer { get; }
        public RectTransform CriticalPanelLayer { get; }
        public RectTransform CriticalWorldCueLayer { get; }
        public IReadOnlyList<ProductionHudComponentView> ComponentViews { get; }

        public ProductionHudComponentView Get(HudSlotId slot)
        {
            if (_views.TryGetValue(slot, out ProductionHudComponentView view))
            {
                return view;
            }

            throw new InvalidOperationException($"Production HUD component {slot} is unavailable.");
        }

        public void ApplyContent(
            HudSlotId slot,
            UiSemanticState state,
            string header,
            string primary,
            string secondary,
            string[] rows,
            float[] meters)
        {
            Get(slot).Apply(state, header, primary, secondary, rows, meters);
        }
    }

    /// <summary>
    /// Builds one selected, fixed authored composition. Runtime content is injected
    /// after construction; this renderer never chooses or invents gameplay values.
    /// Standard/transient/critical canvases keep protected cues above decorative UI.
    /// </summary>
    public static class ProductionHudRenderer
    {
        public const int StandardSortingOrder = 700;
        public const int TransientSortingOrder = 800;
        public const int CriticalPanelSortingOrder = 900;
        public const int CriticalWorldCueSortingOrder = 1000;

        public static ProductionHudRenderResult Build(
            RectTransform parent,
            HudCompositionDefinition composition,
            UiProductionDesignTokens tokens,
            HudComponentAuthoringProfile authoring,
            UiAccessibilitySettings accessibility,
            Rect viewport,
            Rect physicalSafeArea)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));

            Rect safeArea = Intersect(viewport, physicalSafeArea);
            Rect usable = HudLayoutProjection.ApplySafeAreaPadding(safeArea, composition);
            Rect protectedRect = HudLayoutProjection.Project(usable, composition.ProtectedScanPath);
            UiAccessibilityPresentation presentation = tokens.ResolveAccessibility(accessibility);
            float textScale = HudLayoutProjection.ClampTextScale(
                composition,
                presentation.TextScale);
            Font font = PresentationChrome.ResolveFont();

            RectTransform root = CreateAbsoluteRegion(
                parent,
                "ProductionHud_" + composition.FormFactor,
                viewport,
                viewport);
            CanvasGroup rootGroup = root.gameObject.AddComponent<CanvasGroup>();
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;

            RectTransform standard = CreateLayer(
                root,
                "StandardHudLayer",
                viewport,
                StandardSortingOrder);
            RectTransform transient = CreateLayer(
                root,
                "TransientHudLayer",
                viewport,
                TransientSortingOrder);
            RectTransform criticalPanel = CreateLayer(
                root,
                "CriticalPanelHudLayer",
                viewport,
                CriticalPanelSortingOrder);
            RectTransform criticalWorld = CreateLayer(
                root,
                "CriticalWorldCueHudLayer",
                viewport,
                CriticalWorldCueSortingOrder);
            RectTransform scanPath = CreateAbsoluteRegion(
                criticalWorld,
                "ProtectedPvpScanPath",
                protectedRect,
                viewport);

            var views = new List<ProductionHudComponentView>(composition.Slots?.Length ?? 0);
            if (composition.Slots != null)
            {
                for (int i = 0; i < composition.Slots.Length; i++)
                {
                    HudSlotDefinition slot = composition.Slots[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    HudComponentAuthoringDefinition definition = authoring.Get(slot.Id);
                    Rect projected = HudLayoutProjection.Project(usable, slot.NormalizedRect);
                    RectTransform layer = LayerFor(
                        definition.Layer,
                        standard,
                        criticalPanel,
                        criticalWorld);
                    views.Add(BuildComponent(
                        layer,
                        viewport,
                        projected,
                        slot,
                        definition,
                        tokens,
                        font,
                        textScale));
                }
            }

            return new ProductionHudRenderResult(
                root,
                composition,
                usable,
                protectedRect,
                scanPath,
                standard,
                transient,
                criticalPanel,
                criticalWorld,
                views);
        }

        private static ProductionHudComponentView BuildComponent(
            RectTransform parent,
            Rect viewport,
            Rect projected,
            HudSlotDefinition slot,
            HudComponentAuthoringDefinition authoring,
            UiProductionDesignTokens tokens,
            Font font,
            float textScale)
        {
            RectTransform root = CreateAbsoluteRegion(
                parent,
                "HudComponent_" + slot.Id,
                projected,
                viewport);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            UiStateTreatment treatment = tokens.GetStateTreatment(authoring.DefaultState);
            Image surface = null;
            Outline surfaceOutline = null;
            if (authoring.ShowSurface)
            {
                surface = root.gameObject.AddComponent<Image>();
                surface.color = WithAlpha(tokens.SurfaceColor, tokens.SurfaceOpacity);
                surface.raycastTarget = false;
                surfaceOutline = root.gameObject.AddComponent<Outline>();
                surfaceOutline.effectColor = WithAlpha(treatment.Color, 0.92f);
                float border = Mathf.Max(1f, treatment.BorderWidth);
                surfaceOutline.effectDistance = new Vector2(border, -border);
                surfaceOutline.useGraphicAlpha = false;
            }

            RectTransform patternRoot = CreateAnchoredRegion(
                root,
                "SemanticPattern",
                new Rect(0f, 0f, 1f, 1f));
            RebuildPattern(patternRoot, treatment, slot.IsWorldCueLayer);
            RectTransform nonColorCueRoot = CreateAnchoredRegion(
                root,
                "SemanticNonColorCue",
                new Rect(0f, 0f, 1f, 1f));
            RebuildNonColorCue(nonColorCueRoot, treatment);
            Text header = CreateLabel(
                root,
                "Header",
                font,
                treatment.LabelPrefix + "  " + HeaderFor(slot.Id),
                tokens.GetTypography(authoring.HeaderRole),
                textScale,
                treatment.Color,
                new Rect(0.04f, 0.77f, 0.92f, 0.19f),
                slot.IsWorldCueLayer ? TextAnchor.UpperCenter : TextAnchor.UpperLeft);
            Text primary = CreateLabel(
                root,
                "Primary",
                font,
                string.Empty,
                tokens.GetTypography(authoring.PrimaryRole),
                textScale,
                slot.IsWorldCueLayer ? treatment.Color : tokens.TextPrimaryColor,
                new Rect(0.04f, 0.48f, 0.92f, 0.31f),
                slot.IsWorldCueLayer ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft);
            Text secondary = CreateLabel(
                root,
                "Secondary",
                font,
                string.Empty,
                tokens.GetTypography(authoring.SecondaryRole),
                textScale,
                tokens.TextSecondaryColor,
                new Rect(0.04f, 0.30f, 0.92f, 0.20f),
                slot.IsWorldCueLayer ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft);
            RectTransform rowsRoot = CreateAnchoredRegion(
                root,
                "Rows",
                new Rect(0.04f, 0.03f, 0.92f, 0.29f));
            RectTransform metersRoot = CreateAnchoredRegion(
                root,
                "Meters",
                new Rect(0.46f, 0.05f, 0.50f, 0.24f));
            if (authoring.Template == HudComponentTemplate.Allegiance)
            {
                CreateGlyphSocket(root, treatment, tokens);
            }

            return new ProductionHudComponentView(
                slot,
                authoring,
                tokens,
                font,
                textScale,
                root,
                projected,
                surface,
                surfaceOutline,
                header,
                primary,
                secondary,
                rowsRoot,
                metersRoot,
                nonColorCueRoot,
                patternRoot);
        }

        private static RectTransform LayerFor(
            HudComponentLayer layer,
            RectTransform standard,
            RectTransform criticalPanel,
            RectTransform criticalWorld)
        {
            switch (layer)
            {
                case HudComponentLayer.CriticalPanel:
                    return criticalPanel;
                case HudComponentLayer.CriticalWorldCue:
                    return criticalWorld;
                default:
                    return standard;
            }
        }

        private static RectTransform CreateLayer(
            RectTransform parent,
            string name,
            Rect viewport,
            int sortingOrder)
        {
            RectTransform layer = CreateAbsoluteRegion(parent, name, viewport, viewport);
            Canvas canvas = layer.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            CanvasGroup group = layer.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return layer;
        }

        private static RectTransform CreateAbsoluteRegion(
            Transform parent,
            string name,
            Rect absoluteRect,
            Rect viewport)
        {
            var region = new GameObject(name, typeof(RectTransform));
            region.transform.SetParent(parent, false);
            RectTransform rect = region.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(
                absoluteRect.xMin - viewport.xMin,
                absoluteRect.yMin - viewport.yMin);
            rect.sizeDelta = absoluteRect.size;
            return rect;
        }

        internal static RectTransform CreateAnchoredRegion(
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
            string name,
            Font font,
            string value,
            UiTypographyToken typography,
            float textScale,
            Color color,
            Rect normalizedRect,
            TextAnchor alignment)
        {
            RectTransform rect = CreateAnchoredRegion(parent, name, normalizedRect);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = value ?? string.Empty;
            label.fontSize = Mathf.Max(11, Mathf.RoundToInt(typography.BaseSize * textScale));
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = false;
            label.raycastTarget = false;
            if (alignment == TextAnchor.MiddleCenter || alignment == TextAnchor.UpperCenter)
            {
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                outline.useGraphicAlpha = false;
            }
            return label;
        }

        internal static Text CreateRowLabel(
            Transform parent,
            Font font,
            string name,
            string value,
            UiTypographyToken typography,
            float textScale,
            int index,
            int capacity,
            Color color)
        {
            float rowHeight = 1f / Mathf.Max(1, capacity);
            float y = 1f - (index + 1) * rowHeight;
            return CreateLabel(
                parent,
                name,
                font,
                value,
                typography,
                textScale,
                color,
                new Rect(0f, y, 1f, rowHeight),
                TextAnchor.MiddleLeft);
        }

        private static void CreateGlyphSocket(
            Transform parent,
            UiStateTreatment treatment,
            UiProductionDesignTokens tokens)
        {
            RectTransform socket = CreateAnchoredRegion(
                parent,
                "RealmGlyphSocket",
                new Rect(0.76f, 0.52f, 0.18f, 0.36f));
            Image image = socket.gameObject.AddComponent<Image>();
            image.color = WithAlpha(tokens.InsetSurfaceColor, 0.88f);
            image.raycastTarget = false;
            Outline outline = socket.gameObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(treatment.Color, tokens.GlyphGlowOpacity);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;
        }

        internal static void RebuildNonColorCue(
            RectTransform root,
            UiStateTreatment treatment)
        {
            ClearChildren(root);
            Color color = WithAlpha(treatment.Color, 0.94f);
            switch (treatment.NonColorCue)
            {
                case UiNonColorCue.DoubleRail:
                    AddMark(root, "UpperRail", new Vector2(0.08f, 0.91f), new Vector2(42f, 2f), 0f, color);
                    AddMark(root, "LowerRail", new Vector2(0.08f, 0.86f), new Vector2(42f, 2f), 0f, color);
                    break;
                case UiNonColorCue.RoundedShield:
                    AddMark(root, "ShieldSpine", new Vector2(0.025f, 0.52f), new Vector2(3f, 28f), 0f, color);
                    AddMark(root, "ShieldPoint", new Vector2(0.025f, 0.35f), new Vector2(9f, 9f), 45f, color);
                    break;
                case UiNonColorCue.SawtoothFrame:
                    AddMark(root, "ToothA", new Vector2(0.04f, 0.38f), new Vector2(10f, 10f), 45f, color);
                    AddMark(root, "ToothB", new Vector2(0.04f, 0.52f), new Vector2(10f, 10f), 45f, color);
                    AddMark(root, "ToothC", new Vector2(0.04f, 0.66f), new Vector2(10f, 10f), 45f, color);
                    break;
                case UiNonColorCue.DiamondNotch:
                    AddMark(root, "Diamond", new Vector2(0.5f, 0.98f), new Vector2(11f, 11f), 45f, color);
                    break;
                case UiNonColorCue.UpwardChevron:
                    AddMark(root, "ChevronLeft", new Vector2(0.47f, 0.94f), new Vector2(24f, 3f), 35f, color);
                    AddMark(root, "ChevronRight", new Vector2(0.53f, 0.94f), new Vector2(24f, 3f), -35f, color);
                    break;
                case UiNonColorCue.CrossedBar:
                    AddMark(root, "CrossA", new Vector2(0.06f, 0.5f), new Vector2(24f, 3f), 45f, color);
                    AddMark(root, "CrossB", new Vector2(0.06f, 0.5f), new Vector2(24f, 3f), -45f, color);
                    break;
                case UiNonColorCue.BrokenFrame:
                    AddMark(root, "BrokenLeft", new Vector2(0.22f, 0.96f), new Vector2(32f, 3f), 0f, color);
                    AddMark(root, "BrokenRight", new Vector2(0.78f, 0.96f), new Vector2(32f, 3f), 0f, color);
                    break;
                case UiNonColorCue.CornerBrackets:
                    AddMark(root, "TopLeft", new Vector2(0.06f, 0.94f), new Vector2(28f, 3f), 0f, color);
                    AddMark(root, "BottomRight", new Vector2(0.94f, 0.06f), new Vector2(28f, 3f), 0f, color);
                    break;
            }
        }

        internal static void RebuildPattern(
            RectTransform root,
            UiStateTreatment treatment,
            bool worldCueLayer)
        {
            ClearChildren(root);
            Color color = WithAlpha(treatment.Color, worldCueLayer ? 0.20f : 0.10f);
            float rotation = PatternRotation(treatment.Pattern);
            for (int i = 0; i < 3; i++)
            {
                AddMark(
                    root,
                    "PatternMark_" + i,
                    new Vector2(0.22f + i * 0.28f, 0.12f + i * 0.035f),
                    new Vector2(38f, 1.5f),
                    rotation,
                    color);
            }
        }

        private static float PatternRotation(UiSurfacePattern pattern)
        {
            switch (pattern)
            {
                case UiSurfacePattern.WovenFiber:
                case UiSurfacePattern.CrossHatch:
                    return 45f;
                case UiSurfacePattern.ScoredStone:
                    return 24f;
                case UiSurfacePattern.DiagonalCut:
                    return -36f;
                case UiSurfacePattern.RisingWeave:
                    return 32f;
                case UiSurfacePattern.InterruptedGrain:
                    return -18f;
                default:
                    return 0f;
            }
        }

        private static void AddMark(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 size,
            float rotation,
            Color color)
        {
            var mark = new GameObject(name, typeof(RectTransform));
            mark.transform.SetParent(parent, false);
            RectTransform rect = mark.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = mark.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static string HeaderFor(HudSlotId slot)
        {
            switch (slot)
            {
                case HudSlotId.PlayerVitals: return "PLAYER VITALS";
                case HudSlotId.CurrentTarget: return "CURRENT TARGET";
                case HudSlotId.HostileTelegraphs: return "HOSTILE TELEGRAPH";
                case HudSlotId.PartySupport: return "PARTY / SUPPORT";
                case HudSlotId.Objectives: return "OBJECTIVES";
                case HudSlotId.Route: return "ROUTE";
                case HudSlotId.Allegiance: return "ALLEGIANCE";
                default: return slot.ToString().ToUpperInvariant();
            }
        }

        private static Rect Intersect(Rect viewport, Rect safeArea)
        {
            float xMin = Mathf.Max(viewport.xMin, safeArea.xMin);
            float yMin = Mathf.Max(viewport.yMin, safeArea.yMin);
            float xMax = Mathf.Min(viewport.xMax, safeArea.xMax);
            float yMax = Mathf.Min(viewport.yMax, safeArea.yMax);
            if (xMax <= xMin || yMax <= yMin)
            {
                return viewport;
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        internal static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                child.SetParent(null, false);
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        internal static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
