using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AL.UI.DesignSystem;
using AL.UI.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.Tests.EditMode.UI
{
    public sealed class ProductionHudComponentTests
    {
        private GameObject _canvas;

        [TearDown]
        public void TearDown()
        {
            UiAccessibilityPreferences.Reset();
            ProgressiveMapSession.ResetForTests();
            if (_canvas != null)
            {
                UnityEngine.Object.DestroyImmediate(_canvas);
            }
        }

        [Test]
        public void RuntimeAssemblyExposesReusableProductionHudAuthoringAndRenderer()
        {
            Type profileType = RequireRuntimeType("AL.UI.DesignSystem.HudComponentAuthoringProfile");
            Type rendererType = RequireRuntimeType("AL.UI.DesignSystem.ProductionHudRenderer");
            object profile = InvokeStatic(profileType, "LoadDefault");

            Assert.That(profile, Is.Not.Null);
            Assert.That(GetField<string>(profile, "SystemId"), Is.EqualTo("al.ui.hud.components.v1"));
            Assert.That(AsObjects(GetField<object>(profile, "Components")), Has.Count.EqualTo(7));
            Assert.That(rendererType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
        }

        [Test]
        public void RuntimeHostSelectsAuthoredCompositionAndAppliesSafeArea()
        {
            Type hostType = RequireRuntimeType("AL.UI.DesignSystem.ProductionHudHost");
            _canvas = new GameObject("ProductionHudHostTest", typeof(RectTransform));
            RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.zero;
            canvasRect.pivot = Vector2.zero;
            canvasRect.sizeDelta = new Vector2(2400f, 1080f);
            Component host = _canvas.AddComponent(hostType);

            Invoke(
                host,
                "Rebuild",
                new Rect(0f, 0f, 2400f, 1080f),
                new Rect(180f, 80f, 2040f, 920f),
                true,
                new UiAccessibilitySettings(1.5f, true, true, true));

            object current = GetProperty<object>(host, "Current");
            Assert.That(current, Is.Not.Null);
            Assert.That(
                GetProperty<HudCompositionDefinition>(current, "Composition").FormFactor,
                Is.EqualTo(UiFormFactor.PhoneLandscape));
            Assert.That(GetProperty<Rect>(current, "UsableSafeArea").xMin, Is.GreaterThan(180f));
            Assert.That(
                GetProperty<RectTransform>(current, "Root").name,
                Is.EqualTo("ProductionHud_PhoneLandscape"));
        }

        [Test]
        public void RuntimeHostExposesFullAuthoredTextScaleRange()
        {
            Type hostType = RequireRuntimeType("AL.UI.DesignSystem.ProductionHudHost");
            FieldInfo textScale = hostType.GetField(
                "_textScale",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(textScale, Is.Not.Null);
            UnityEngine.RangeAttribute range =
                textScale.GetCustomAttribute<UnityEngine.RangeAttribute>();

            Assert.That(range, Is.Not.Null);
            Assert.That(range.min, Is.EqualTo(0.85f));
            Assert.That(range.max, Is.EqualTo(2f));
        }

        [Test]
        public void RuntimeHostProjectsPixelSafeAreaIntoScaledCanvasCoordinates()
        {
            Type hostType = RequireRuntimeType("AL.UI.DesignSystem.ProductionHudHost");
            MethodInfo project = hostType.GetMethod(
                "ProjectScreenSafeArea",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(project, Is.Not.Null);

            Rect projected = (Rect)project.Invoke(null, new object[]
            {
                new Rect(-960f, -540f, 1920f, 1080f),
                new Vector2Int(2400, 1080),
                new Rect(180f, 80f, 2040f, 920f)
            });

            Assert.That(projected, Is.EqualTo(new Rect(-816f, -460f, 1632f, 920f)));
        }

        [Test]
        public void GeometryRefreshRetainsInjectedContent()
        {
            Type hostType = RequireRuntimeType("AL.UI.DesignSystem.ProductionHudHost");
            _canvas = new GameObject("ProductionHudHostRefreshTest", typeof(RectTransform));
            RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            Component host = _canvas.AddComponent(hostType);

            Invoke(
                host,
                "Rebuild",
                new Rect(0f, 0f, 1920f, 1080f),
                new Rect(48f, 36f, 1824f, 1008f),
                false,
                new UiAccessibilitySettings(1.5f, true, true, true));
            Invoke(
                host,
                "ApplyContent",
                HudSlotId.CurrentTarget,
                UiSemanticState.Hostile,
                "CURRENT TARGET",
                "Interrupt window",
                "Break vulnerable",
                new[] { "Cast 1.2 s" },
                new[] { 0.74f, 0.38f });

            Invoke(
                host,
                "RefreshGeometry",
                new Rect(0f, 0f, 1920f, 1080f),
                new Rect(96f, 72f, 1728f, 936f));

            object current = GetProperty<object>(host, "Current");
            object target = Invoke(current, "Get", HudSlotId.CurrentTarget);
            Assert.That(GetProperty<Text>(target, "Primary").text, Is.EqualTo("Interrupt window"));
            Assert.That(GetProperty<Text>(target, "Secondary").text, Is.EqualTo("Break vulnerable"));
            Assert.That(GetProperty<int>(target, "VisibleRowCount"), Is.EqualTo(1));
            Assert.That(AsObjects(GetProperty<object>(target, "MeterFills")), Has.Count.EqualTo(2));
        }

        [Test]
        public void GeometryRefreshRetainsExplicitInputAndAccessibilityConfiguration()
        {
            Type hostType = RequireRuntimeType("AL.UI.DesignSystem.ProductionHudHost");
            _canvas = new GameObject("ProductionHudHostConfigurationTest", typeof(RectTransform));
            RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            Component host = _canvas.AddComponent(hostType);

            Invoke(
                host,
                "Rebuild",
                new Rect(0f, 0f, 1920f, 1080f),
                new Rect(48f, 36f, 1824f, 1008f),
                false,
                new UiAccessibilitySettings(2f, true, true, true));
            Invoke(
                host,
                "RefreshGeometry",
                new Rect(0f, 0f, 1920f, 1080f),
                new Rect(96f, 72f, 1728f, 936f));

            object current = GetProperty<object>(host, "Current");
            Assert.That(
                GetProperty<HudCompositionDefinition>(current, "Composition").FormFactor,
                Is.EqualTo(UiFormFactor.Pc16By9));
            object target = Invoke(current, "Get", HudSlotId.CurrentTarget);
            Assert.That(GetProperty<Text>(target, "Primary").fontSize, Is.EqualTo(36));
            Assert.That(UiAccessibilityPreferences.TextScale, Is.EqualTo(2f));
            Assert.That(UiAccessibilityPreferences.ReduceMotion, Is.True);
            Assert.That(UiAccessibilityPreferences.ReduceFlash, Is.True);
            Assert.That(UiAccessibilityPreferences.ReduceVfx, Is.True);
            Assert.That(ProgressiveMapSession.Accessibility.Settings.TextScale, Is.EqualTo(2f));
            Assert.That(ProgressiveMapSession.Accessibility.Settings.ReducedMotion, Is.True);
            Assert.That(ProgressiveMapSession.Accessibility.Settings.ReducedFlash, Is.True);
            Assert.That(ProgressiveMapSession.Accessibility.Settings.ReducedVfx, Is.True);
        }

        [TestCase(2400, 1080, true, 180f, 80f, 2040f, 920f, UiFormFactor.PhoneLandscape)]
        [TestCase(2732, 2048, true, 80f, 80f, 2572f, 1888f, UiFormFactor.TabletLandscape)]
        [TestCase(1920, 1080, false, 48f, 36f, 1824f, 1008f, UiFormFactor.Pc16By9)]
        [TestCase(3440, 1440, false, 140f, 44f, 3160f, 1352f, UiFormFactor.PcUltrawide)]
        public void RequiredFormFactorsBuildFixedSafePurposeAuthoredHierarchy(
            int width,
            int height,
            bool touchPrimary,
            float safeX,
            float safeY,
            float safeWidth,
            float safeHeight,
            UiFormFactor expectedFormFactor)
        {
            object result = Build(width, height, touchPrimary, new Rect(
                safeX,
                safeY,
                safeWidth,
                safeHeight));
            HudResponsiveCompositionSet compositions = HudResponsiveCompositionSet.LoadDefault();
            HudCompositionDefinition expected = compositions.Resolve(width, height, touchPrimary);

            Assert.That(expected.FormFactor, Is.EqualTo(expectedFormFactor));
            Assert.That(AsObjects(GetProperty<object>(result, "ComponentViews")), Has.Count.EqualTo(7));
            Rect protectedRect = GetProperty<Rect>(result, "ProtectedScanRect");
            Rect usable = GetProperty<Rect>(result, "UsableSafeArea");
            Assert.That(new Rect(safeX, safeY, safeWidth, safeHeight).Contains(usable.min), Is.True);
            Assert.That(new Rect(safeX, safeY, safeWidth, safeHeight).Contains(usable.max), Is.True);

            foreach (HudSlotDefinition slot in expected.Slots)
            {
                object view = Invoke(result, "Get", slot.Id);
                Rect projected = GetProperty<Rect>(view, "ProjectedRect");
                Assert.That(usable.Contains(projected.min), Is.True, slot.Id.ToString());
                Assert.That(usable.Contains(projected.max), Is.True, slot.Id.ToString());
                Assert.That(GetProperty<RectTransform>(view, "Root"), Is.Not.Null, slot.Id.ToString());
                Assert.That(GetProperty<Text>(view, "Header"), Is.Not.Null, slot.Id.ToString());
                Assert.That(GetProperty<RectTransform>(view, "NonColorCueRoot"), Is.Not.Null, slot.Id.ToString());
                if (slot.Id == HudSlotId.HostileTelegraphs)
                {
                    Assert.That(projected, Is.EqualTo(protectedRect));
                    Assert.That(GetProperty<Image>(view, "Surface"), Is.Null);
                }
                else
                {
                    Assert.That(projected.Overlaps(protectedRect), Is.False, slot.Id.ToString());
                    Assert.That(GetProperty<Image>(view, "Surface"), Is.Not.Null, slot.Id.ToString());
                }
            }
        }

        [Test]
        public void LargeTextDenseCombatStressAggregatesSecondaryRowsBelowProtectedCues()
        {
            object result = Build(
                2400,
                1080,
                true,
                new Rect(180f, 80f, 2040f, 920f),
                textScale: 2f);
            string[] denseParty = Enumerable.Range(1, 8)
                .Select(index => "Support member " + index + " — revive and role state")
                .ToArray();
            string[] denseObjectives = Enumerable.Range(1, 8)
                .Select(index => "Contested objective " + index + " — localized timer state")
                .ToArray();

            Invoke(result, "ApplyContent",
                HudSlotId.PartySupport,
                UiSemanticState.Friendly,
                "PARTY / SUPPORT",
                "Squad stable",
                "Revive priority visible",
                denseParty,
                Array.Empty<float>());
            Invoke(result, "ApplyContent",
                HudSlotId.Objectives,
                UiSemanticState.Warning,
                "OBJECTIVES",
                "Central reliquary contested",
                "Route and timer remain authoritative",
                denseObjectives,
                new[] { 0.62f });
            Invoke(result, "ApplyContent",
                HudSlotId.CurrentTarget,
                UiSemanticState.Hostile,
                "CURRENT TARGET",
                "Hostile cast — interrupt window",
                "Defense and break state",
                new[] { "Cast 1.2 s", "Break vulnerable" },
                new[] { 0.74f, 0.38f });
            Invoke(result, "ApplyContent",
                HudSlotId.HostileTelegraphs,
                UiSemanticState.Hostile,
                "HOSTILE TELEGRAPH",
                "DODGE — FORWARD CLEAVE",
                "Direction and boundary preserved",
                new[] { "Impact 0.8 s" },
                Array.Empty<float>());

            object party = Invoke(result, "Get", HudSlotId.PartySupport);
            object objectives = Invoke(result, "Get", HudSlotId.Objectives);
            object target = Invoke(result, "Get", HudSlotId.CurrentTarget);
            object telegraphs = Invoke(result, "Get", HudSlotId.HostileTelegraphs);
            Assert.That(GetProperty<int>(party, "VisibleRowCount"), Is.LessThan(denseParty.Length));
            Assert.That(GetProperty<int>(party, "OverflowCount"), Is.GreaterThan(0));
            Assert.That(GetProperty<int>(objectives, "VisibleRowCount"), Is.LessThan(denseObjectives.Length));
            Assert.That(GetProperty<int>(objectives, "OverflowCount"), Is.GreaterThan(0));
            Assert.That(GetProperty<Text>(target, "Primary").fontSize, Is.GreaterThanOrEqualTo(26));
            Assert.That(GetProperty<Text>(telegraphs, "Primary").fontSize, Is.GreaterThanOrEqualTo(26));
            Assert.That(GetProperty<Text>(target, "Primary").resizeTextForBestFit, Is.False);
            Assert.That(GetProperty<Text>(telegraphs, "Primary").resizeTextForBestFit, Is.False);

            Canvas standard = GetProperty<RectTransform>(result, "StandardLayer").GetComponent<Canvas>();
            Canvas transient = GetProperty<RectTransform>(result, "TransientLayer").GetComponent<Canvas>();
            Canvas criticalPanel = GetProperty<RectTransform>(result, "CriticalPanelLayer").GetComponent<Canvas>();
            Canvas criticalWorld = GetProperty<RectTransform>(result, "CriticalWorldCueLayer").GetComponent<Canvas>();
            Assert.That(transient.sortingOrder, Is.GreaterThan(standard.sortingOrder));
            Assert.That(criticalPanel.sortingOrder, Is.GreaterThan(transient.sortingOrder));
            Assert.That(criticalWorld.sortingOrder, Is.GreaterThan(criticalPanel.sortingOrder));
            Assert.That(GetProperty<Rect>(target, "ProjectedRect").Overlaps(
                GetProperty<Rect>(result, "ProtectedScanRect")), Is.False);
            Assert.That(GetProperty<Rect>(telegraphs, "ProjectedRect"),
                Is.EqualTo(GetProperty<Rect>(result, "ProtectedScanRect")));
        }

        [Test]
        public void HostileWorldCueUpdatesEverySemanticReadWhenStateChanges()
        {
            object result = Build(
                1920,
                1080,
                false,
                new Rect(48f, 36f, 1824f, 1008f));
            UiProductionDesignTokens tokens = UiProductionDesignTokens.LoadDefault();
            UiStateTreatment warning = tokens.GetStateTreatment(UiSemanticState.Warning);

            Invoke(result, "ApplyContent",
                HudSlotId.HostileTelegraphs,
                UiSemanticState.Warning,
                "AREA DENIAL",
                "MOVE OUT",
                "Impact boundary remains visible",
                Array.Empty<string>(),
                Array.Empty<float>());

            object telegraph = Invoke(result, "Get", HudSlotId.HostileTelegraphs);
            Text header = GetProperty<Text>(telegraph, "Header");
            Text primary = GetProperty<Text>(telegraph, "Primary");
            RectTransform cueRoot = GetProperty<RectTransform>(telegraph, "NonColorCueRoot");
            Assert.That(header.text, Does.StartWith(warning.LabelPrefix));
            Assert.That(header.color, Is.EqualTo(warning.Color));
            Assert.That(primary.color, Is.EqualTo(warning.Color));
            Assert.That(cueRoot.Find("Diamond"), Is.Not.Null);
            Assert.That(cueRoot.Find("ToothA"), Is.Null);
        }

        [Test]
        public void FocusScopeFiltersUnavailableResponsiveVariantsAndContainsNavigation()
        {
            var eventSystemRoot = new GameObject(
                "AccessibilityFocusEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            EventSystem eventSystem = eventSystemRoot.GetComponent<EventSystem>();
            _canvas = new GameObject("AccessibilityFocusCanvas", typeof(RectTransform));
            try
            {
                RectTransform viewport = _canvas.GetComponent<RectTransform>();
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.zero;
                viewport.pivot = Vector2.zero;
                viewport.sizeDelta = new Vector2(400f, 300f);
                Button first = CreateFocusButton(viewport, "First", new Vector2(20f, 20f));
                Button second = CreateFocusButton(viewport, "Second", new Vector2(20f, 100f));
                Button hidden = CreateFocusButton(viewport, "Hidden", new Vector2(20f, 180f));
                Button disabled = CreateFocusButton(viewport, "Disabled", new Vector2(120f, 20f));
                Button offscreen = CreateFocusButton(viewport, "Offscreen", new Vector2(900f, 20f));
                hidden.gameObject.SetActive(false);
                disabled.interactable = false;
                Canvas.ForceUpdateCanvases();

                var scope = new UiAccessibilityFocusScope(viewport);
                scope.Activate(new Selectable[] { first, hidden, disabled, offscreen, second });

                Assert.That(scope.FocusableControls, Is.EqualTo(new[] { first, second }));
                Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(first.gameObject));
                Assert.That(first.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(first.navigation.selectOnUp, Is.SameAs(second));
                Assert.That(first.navigation.selectOnDown, Is.SameAs(second));
                Assert.That(second.navigation.selectOnUp, Is.SameAs(first));
                Assert.That(second.navigation.selectOnDown, Is.SameAs(first));
                Assert.That(first.GetComponent<UiFocusVisibility>(), Is.Not.Null);
                Assert.That(second.GetComponent<UiFocusVisibility>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(eventSystemRoot);
            }
        }

        [Test]
        public void FocusScopeRestoresPriorFocusAndKeyboardSubmitActivatesCurrentControl()
        {
            var eventSystemRoot = new GameObject(
                "AccessibilityRestoreEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            EventSystem eventSystem = eventSystemRoot.GetComponent<EventSystem>();
            _canvas = new GameObject("AccessibilityRestoreCanvas", typeof(RectTransform));
            try
            {
                RectTransform viewport = _canvas.GetComponent<RectTransform>();
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.zero;
                viewport.pivot = Vector2.zero;
                viewport.sizeDelta = new Vector2(400f, 300f);
                Button prior = CreateFocusButton(viewport, "Prior", new Vector2(20f, 20f));
                Button modal = CreateFocusButton(viewport, "Modal", new Vector2(20f, 100f));
                int activations = 0;
                modal.onClick.AddListener(() => activations++);
                Canvas.ForceUpdateCanvases();
                eventSystem.SetSelectedGameObject(prior.gameObject);

                var scope = new UiAccessibilityFocusScope(viewport);
                scope.Activate(new Selectable[] { modal });
                Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(modal.gameObject));
                Assert.That(scope.SubmitCurrent(), Is.True);
                Assert.That(activations, Is.EqualTo(1));

                scope.Deactivate(restoreFocus: true);

                Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(prior.gameObject));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(eventSystemRoot);
            }
        }

        [Test]
        public void FocusScopeRestoresAValidSiblingWhenPriorControlBecomesUnavailable()
        {
            var eventSystemRoot = new GameObject(
                "AccessibilityFallbackEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            EventSystem eventSystem = eventSystemRoot.GetComponent<EventSystem>();
            _canvas = new GameObject(
                "AccessibilityFallbackCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            try
            {
                RectTransform viewport = _canvas.GetComponent<RectTransform>();
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.zero;
                viewport.pivot = Vector2.zero;
                viewport.sizeDelta = new Vector2(400f, 300f);
                Button prior = CreateFocusButton(viewport, "Prior", new Vector2(20f, 20f));
                Button fallback = CreateFocusButton(viewport, "Fallback", new Vector2(120f, 20f));
                Button modal = CreateFocusButton(viewport, "Modal", new Vector2(20f, 100f));
                Canvas.ForceUpdateCanvases();
                eventSystem.SetSelectedGameObject(prior.gameObject);

                var scope = new UiAccessibilityFocusScope(viewport);
                scope.Activate(new Selectable[] { modal });
                prior.interactable = false;

                scope.Deactivate(restoreFocus: true);

                Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(fallback.gameObject));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(eventSystemRoot);
            }
        }

        [Test]
        public void AccessibilityTextScalingIsStableAndClampedAcrossRepeatedRefreshes()
        {
            _canvas = new GameObject("AccessibilityTextCanvas", typeof(RectTransform));
            Text text = new GameObject("ScaledText", typeof(RectTransform), typeof(Text))
                .GetComponent<Text>();
            text.transform.SetParent(_canvas.transform, false);
            text.fontSize = 16;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.zero;
            textRect.sizeDelta = new Vector2(100f, 20f);

            UiAccessibilityRuntime.ApplyTextScale(_canvas.transform, 2f);
            UiAccessibilityRuntime.ApplyTextScale(_canvas.transform, 2f);

            Assert.That(text.fontSize, Is.EqualTo(32));
            Assert.That(text.GetComponent<UiScalableText>().BaseFontSize, Is.EqualTo(16));
            Assert.That(textRect.sizeDelta.x, Is.GreaterThanOrEqualTo(200f));
            Assert.That(textRect.sizeDelta.y, Is.GreaterThanOrEqualTo(40f));
            Assert.That(text.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow));

            UiAccessibilityRuntime.ApplyTextScale(_canvas.transform, 0.1f);
            Assert.That(text.fontSize, Is.EqualTo(14),
                "Readable map text must not shrink below the approved runtime floor.");
        }

        private object Build(
            int width,
            int height,
            bool touchPrimary,
            Rect safeArea,
            float textScale = 1f)
        {
            UiProductionDesignTokens tokens = UiProductionDesignTokens.LoadDefault();
            HudResponsiveCompositionSet compositions = HudResponsiveCompositionSet.LoadDefault();
            HudCompositionDefinition composition = compositions.Resolve(width, height, touchPrimary);
            Type profileType = RequireRuntimeType("AL.UI.DesignSystem.HudComponentAuthoringProfile");
            Type rendererType = RequireRuntimeType("AL.UI.DesignSystem.ProductionHudRenderer");
            object profile = InvokeStatic(profileType, "LoadDefault");

            _canvas = new GameObject("ProductionHudTestCanvas", typeof(RectTransform));
            RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.zero;
            canvasRect.pivot = Vector2.zero;
            canvasRect.sizeDelta = new Vector2(width, height);
            MethodInfo build = rendererType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
            Assert.That(build, Is.Not.Null);
            return build.Invoke(null, new[]
            {
                canvasRect,
                composition,
                tokens,
                profile,
                new UiAccessibilitySettings(textScale, true, true, true),
                new Rect(0f, 0f, width, height),
                safeArea
            });
        }

        private static Button CreateFocusButton(
            RectTransform parent,
            string name,
            Vector2 position)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(80f, 56f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            return button;
        }

        private static Type RequireRuntimeType(string name)
        {
            Type type = typeof(UiProductionDesignTokens).Assembly.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static object InvokeStatic(Type type, string method)
        {
            MethodInfo target = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            Assert.That(target, Is.Not.Null, type.FullName + "." + method);
            return target.Invoke(null, null);
        }

        private static object Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo member = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(member, Is.Not.Null, target.GetType().FullName + "." + method);
            return member.Invoke(target, arguments);
        }

        private static T GetProperty<T>(object target, string property)
        {
            PropertyInfo member = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(member, Is.Not.Null, target.GetType().FullName + "." + property);
            return (T)member.GetValue(target);
        }

        private static T GetField<T>(object target, string field)
        {
            FieldInfo member = target.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(member, Is.Not.Null, target.GetType().FullName + "." + field);
            return (T)member.GetValue(target);
        }

        private static IList AsObjects(object value)
        {
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>().ToList();
        }
    }
}
