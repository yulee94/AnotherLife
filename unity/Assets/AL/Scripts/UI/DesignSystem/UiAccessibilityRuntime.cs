using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.UI.DesignSystem
{
    public static class UiAccessibilityPreferences
    {
        public static float TextScale { get; private set; } = 1f;
        public static bool ReduceMotion { get; private set; }
        public static bool ReduceFlash { get; private set; }
        public static bool ReduceVfx { get; private set; }

        public static void Configure(
            float textScale,
            bool reduceMotion,
            bool reduceFlash,
            bool reduceVfx)
        {
            TextScale = Mathf.Clamp(textScale, 0.85f, 2f);
            ReduceMotion = reduceMotion;
            ReduceFlash = reduceFlash;
            ReduceVfx = reduceVfx;
        }

        public static void Configure(UiAccessibilitySettings settings)
        {
            Configure(
                settings.TextScale,
                settings.ReducedMotion,
                settings.ReducedFlash,
                settings.ReducedVfx);
        }

        public static void Reset()
        {
            Configure(1f, false, false, false);
        }
    }

    [DisallowMultipleComponent]
    public sealed class UiScalableText : MonoBehaviour
    {
        [SerializeField] private int baseFontSize;
        [SerializeField] private Vector2 baseSizeDelta;
        [SerializeField] private bool layoutCaptured;

        public int BaseFontSize => baseFontSize;

        private void Awake()
        {
            CaptureBaseSize();
        }

        public void Apply(float scale)
        {
            Text text = GetComponent<Text>();
            if (text == null)
                return;

            CaptureBaseSize();
            float clampedScale = Mathf.Clamp(scale, 0.5f, 2f);
            text.fontSize = Mathf.Max(14, Mathf.RoundToInt(baseFontSize * clampedScale));
            text.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rect = text.rectTransform;
            float layoutScale = Mathf.Max(1f, clampedScale);
            Vector2 size = rect.sizeDelta;
            if (rect.anchorMin.x == rect.anchorMax.x)
                size.x = Mathf.Max(baseSizeDelta.x * layoutScale, baseSizeDelta.x);
            if (rect.anchorMin.y == rect.anchorMax.y)
                size.y = Mathf.Max(
                    baseSizeDelta.y * layoutScale,
                    text.fontSize * 1.25f);
            rect.sizeDelta = size;
        }

        private void CaptureBaseSize()
        {
            if (baseFontSize > 0)
                return;

            Text text = GetComponent<Text>();
            if (text != null)
                baseFontSize = Mathf.Max(1, text.fontSize);

            if (!layoutCaptured)
            {
                RectTransform rect = transform as RectTransform;
                baseSizeDelta = rect != null ? rect.sizeDelta : Vector2.zero;
                layoutCaptured = true;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class UiFocusVisibility : MonoBehaviour
    {
        [SerializeField] private Color focusColor = new Color(0.96f, 0.86f, 0.42f, 1f);
        [SerializeField] private Vector2 focusDistance = new Vector2(3f, -3f);

        private Outline outline;

        public bool IsFocusVisible =>
            EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;

        private void Awake()
        {
            EnsureOutline();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            EnsureOutline();
            if (outline != null)
                outline.enabled = IsFocusVisible;
        }

        private void EnsureOutline()
        {
            if (outline != null)
                return;

            Graphic graphic = GetComponent<Graphic>();
            if (graphic == null)
                return;

            outline = GetComponent<Outline>();
            if (outline == null)
                outline = gameObject.AddComponent<Outline>();
            outline.effectColor = focusColor;
            outline.effectDistance = focusDistance;
            outline.useGraphicAlpha = false;
        }
    }

    public sealed class UiAccessibilityFocusScope : IDisposable
    {
        private readonly List<Selectable> eligible = new List<Selectable>();
        private readonly List<Selectable> restorationCandidates = new List<Selectable>();
        private EventSystem eventSystem;
        private GameObject previousSelection;
        private RectTransform viewport;
        private IReadOnlyList<Selectable> controls;
        private bool active;

        public IReadOnlyList<Selectable> EligibleSelectables => eligible;
        public IReadOnlyList<Selectable> FocusableControls => eligible;

        public UiAccessibilityFocusScope()
        {
        }

        public UiAccessibilityFocusScope(EventSystem activeEventSystem)
        {
            eventSystem = activeEventSystem;
        }

        public UiAccessibilityFocusScope(RectTransform focusViewport)
        {
            eventSystem = ResolveEventSystem();
            viewport = focusViewport;
        }

        public void Activate(IReadOnlyList<Selectable> orderedControls)
        {
            Selectable initialSelection = orderedControls != null && orderedControls.Count > 0
                ? orderedControls[0]
                : null;
            Activate(eventSystem ?? ResolveEventSystem(), viewport, orderedControls, initialSelection);
        }

        public void Activate(
            RectTransform focusViewport,
            IReadOnlyList<Selectable> orderedControls,
            Selectable initialSelection)
        {
            Activate(eventSystem, focusViewport, orderedControls, initialSelection);
        }

        public void Activate(
            EventSystem activeEventSystem,
            RectTransform focusViewport,
            IReadOnlyList<Selectable> orderedControls,
            Selectable initialSelection)
        {
            RestorePreviousFocus();

            eventSystem = activeEventSystem;
            viewport = focusViewport;
            controls = orderedControls;
            previousSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            CaptureRestorationCandidates();
            active = true;

            Refresh();
            Selectable target = IsEligible(initialSelection) ? initialSelection :
                (eligible.Count > 0 ? eligible[0] : null);
            SetSelection(target);
        }

        public void Refresh()
        {
            if (!active)
                return;

            eligible.Clear();
            if (controls != null)
            {
                for (int i = 0; i < controls.Count; i++)
                {
                    Selectable selectable = controls[i];
                    if (!IsEligible(selectable))
                        continue;

                    eligible.Add(selectable);
                    UiFocusVisibility focusVisibility = selectable.GetComponent<UiFocusVisibility>();
                    if (focusVisibility == null)
                        focusVisibility = selectable.gameObject.AddComponent<UiFocusVisibility>();
                    focusVisibility.Refresh();
                }
            }

            ConfigureExplicitNavigation();

            GameObject current = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (current != null && !ContainsGameObject(current))
                SetSelection(eligible.Count > 0 ? eligible[0] : null);
            else if (current == null && eligible.Count > 0)
                SetSelection(eligible[0]);
        }

        public void RestorePreviousFocus()
        {
            if (!active)
                return;

            active = false;
            if (eventSystem != null)
            {
                GameObject restoreTarget = IsValidRestorationTarget(previousSelection)
                    ? previousSelection
                    : FindRestorationFallback();
                eventSystem.SetSelectedGameObject(restoreTarget);
            }

            eligible.Clear();
            restorationCandidates.Clear();
            controls = null;
            viewport = null;
            previousSelection = null;
            eventSystem = null;
        }

        public void Deactivate(bool restoreFocus = true)
        {
            if (restoreFocus)
            {
                RestorePreviousFocus();
                return;
            }

            active = false;
            eligible.Clear();
            restorationCandidates.Clear();
            controls = null;
            viewport = null;
            previousSelection = null;
            eventSystem = null;
        }

        public bool SubmitCurrent()
        {
            GameObject current = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            return current != null &&
                   ExecuteEvents.Execute(current, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
        }

        public void Dispose()
        {
            RestorePreviousFocus();
        }

        private bool IsEligible(Selectable selectable)
        {
            if (!CanReceiveFocus(selectable))
                return false;

            RectTransform selectableRect = selectable.transform as RectTransform;
            return viewport == null || selectableRect == null || IntersectsViewport(selectableRect);
        }

        private static bool CanReceiveFocus(Selectable selectable)
        {
            if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())
                return false;
            if (!selectable.gameObject.activeInHierarchy)
                return false;

            CanvasGroup[] groups = selectable.GetComponentsInParent<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                if (!group.enabled)
                    continue;
                if (group.alpha <= 0.001f || !group.interactable)
                    return false;
                if (group.ignoreParentGroups)
                    break;
            }
            return true;
        }

        private void CaptureRestorationCandidates()
        {
            restorationCandidates.Clear();
            if (previousSelection == null)
                return;

            Canvas canvas = previousSelection.GetComponentInParent<Canvas>();
            Transform root = canvas != null ? canvas.transform : previousSelection.transform.parent;
            if (root == null)
                return;

            Selectable[] candidates = root.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                Selectable candidate = candidates[i];
                if (!IsModalControl(candidate))
                    restorationCandidates.Add(candidate);
            }
        }

        private bool IsModalControl(Selectable candidate)
        {
            if (controls == null)
                return false;
            for (int i = 0; i < controls.Count; i++)
            {
                if (controls[i] == candidate)
                    return true;
            }
            return false;
        }

        private static bool IsValidRestorationTarget(GameObject candidate)
        {
            if (candidate == null || !candidate.activeInHierarchy)
                return false;
            Selectable selectable = candidate.GetComponent<Selectable>();
            return selectable == null || CanReceiveFocus(selectable);
        }

        private GameObject FindRestorationFallback()
        {
            for (int i = 0; i < restorationCandidates.Count; i++)
            {
                Selectable candidate = restorationCandidates[i];
                if (CanReceiveFocus(candidate))
                    return candidate.gameObject;
            }
            return null;
        }

        private bool IntersectsViewport(RectTransform selectableRect)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selectableRect);
            Rect rect = viewport.rect;
            return bounds.max.x >= rect.xMin && bounds.min.x <= rect.xMax &&
                   bounds.max.y >= rect.yMin && bounds.min.y <= rect.yMax;
        }

        private void ConfigureExplicitNavigation()
        {
            for (int i = 0; i < eligible.Count; i++)
            {
                Selectable current = eligible[i];
                Selectable previous = eligible[(i - 1 + eligible.Count) % eligible.Count];
                Selectable next = eligible[(i + 1) % eligible.Count];
                Navigation navigation = current.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = previous;
                navigation.selectOnLeft = previous;
                navigation.selectOnDown = next;
                navigation.selectOnRight = next;
                current.navigation = navigation;
            }
        }

        private bool ContainsGameObject(GameObject candidate)
        {
            for (int i = 0; i < eligible.Count; i++)
            {
                if (eligible[i] != null && eligible[i].gameObject == candidate)
                    return true;
            }
            return false;
        }

        private void SetSelection(Selectable selectable)
        {
            if (eventSystem == null)
                return;

            eventSystem.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
            for (int i = 0; i < eligible.Count; i++)
            {
                UiFocusVisibility focusVisibility = eligible[i].GetComponent<UiFocusVisibility>();
                if (focusVisibility != null)
                    focusVisibility.Refresh();
            }
        }

        private static EventSystem ResolveEventSystem()
        {
            return EventSystem.current ?? UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        }
    }

    public static class UiAccessibilityRuntime
    {
        public const float MinimumTouchTarget = 56f;

        public static void ApplyTextScale(GameObject root, float scale)
        {
            if (root == null)
                return;

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                UiScalableText scalable = texts[i].GetComponent<UiScalableText>();
                if (scalable == null)
                    scalable = texts[i].gameObject.AddComponent<UiScalableText>();
                scalable.Apply(scale);
            }
        }

        public static void ApplyTextScale(Transform root, float scale)
        {
            ApplyTextScale(root != null ? root.gameObject : null, scale);
        }

        public static void ApplyPreferences(GameObject root)
        {
            ApplyTextScale(root, UiAccessibilityPreferences.TextScale);
        }

        public static void ApplySettings(GameObject root, UiAccessibilitySettings settings)
        {
            UiAccessibilityPreferences.Configure(settings);
            ApplyPreferences(root);
        }

        public static void EnsureMinimumTouchTarget(RectTransform target, float minimum = MinimumTouchTarget)
        {
            if (target == null)
                return;

            Vector2 size = target.sizeDelta;
            size.x = Mathf.Max(size.x, minimum);
            size.y = Mathf.Max(size.y, minimum);
            target.sizeDelta = size;
        }
    }

}
