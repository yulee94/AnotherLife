using System;
using System.Collections.Generic;
using UnityEngine;

namespace AL.UI.DesignSystem
{
    /// <summary>
    /// Designer-facing runtime host for the fixed production HUD compositions.
    /// It selects one authored composition from the resource set and delegates
    /// construction to ProductionHudRenderer; gameplay systems only inject content.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ProductionHudHost : MonoBehaviour
    {
        [Header("Authored composition selection")]
        [SerializeField]
        private bool _touchPrimary = true;

        [Header("Accessibility preview and defaults")]
        [SerializeField]
        [Range(0.85f, 2f)]
        private float _textScale = 1f;

        [SerializeField]
        private bool _reducedMotion;

        [SerializeField]
        private bool _reducedFlash;

        [SerializeField]
        private bool _reducedVfx;

        [Header("Runtime safe-area handling")]
        [SerializeField]
        private bool _rebuildOnViewportChange = true;

        private RectTransform _parent;
        private Rect _lastViewport;
        private Rect _lastSafeArea;
        private bool _activeTouchPrimary;
        private UiAccessibilitySettings _activeAccessibility;
        private bool _hasActiveConfiguration;
        private readonly Dictionary<HudSlotId, ContentSnapshot> _content =
            new Dictionary<HudSlotId, ContentSnapshot>();

        public ProductionHudRenderResult Current { get; private set; }

        public void Rebuild(
            Rect viewport,
            Rect physicalSafeArea,
            bool touchPrimary,
            UiAccessibilitySettings accessibility)
        {
            _activeTouchPrimary = touchPrimary;
            _activeAccessibility = accessibility;
            _hasActiveConfiguration = true;
            RefreshGeometry(viewport, physicalSafeArea);
        }

        public void RefreshGeometry(Rect viewport, Rect physicalSafeArea)
        {
            EnsureActiveConfiguration();
            ClearCurrent();
            _parent = _parent != null ? _parent : GetComponent<RectTransform>();
            HudResponsiveCompositionSet compositions = HudResponsiveCompositionSet.LoadDefault();
            HudCompositionDefinition composition = compositions.Resolve(
                Mathf.RoundToInt(viewport.width),
                Mathf.RoundToInt(viewport.height),
                _activeTouchPrimary);
            Current = ProductionHudRenderer.Build(
                _parent,
                composition,
                UiProductionDesignTokens.LoadDefault(),
                HudComponentAuthoringProfile.LoadDefault(),
                _activeAccessibility,
                viewport,
                physicalSafeArea);
            foreach (ContentSnapshot snapshot in _content.Values)
            {
                snapshot.Apply(Current);
            }
            _lastViewport = viewport;
            _lastSafeArea = physicalSafeArea;
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
            var snapshot = new ContentSnapshot(
                slot,
                state,
                header,
                primary,
                secondary,
                rows,
                meters);
            _content[slot] = snapshot;
            if (Current != null)
            {
                snapshot.Apply(Current);
            }
        }

        private void OnEnable()
        {
            _parent = GetComponent<RectTransform>();
            EnsureActiveConfiguration();
            if (Application.isPlaying)
            {
                RebuildForCurrentScreen();
            }
        }

        private void Update()
        {
            if (!_rebuildOnViewportChange)
            {
                return;
            }

            Rect viewport = CurrentViewport();
            Rect safeArea = CurrentSafeArea(viewport);
            if (viewport != _lastViewport || safeArea != _lastSafeArea)
            {
                RebuildForCurrentScreen();
            }
        }

        private void OnDisable()
        {
            ClearCurrent();
        }

        private void RebuildForCurrentScreen()
        {
            Rect viewport = CurrentViewport();
            RefreshGeometry(
                viewport,
                CurrentSafeArea(viewport));
        }

        private void EnsureActiveConfiguration()
        {
            if (_hasActiveConfiguration)
            {
                return;
            }

            _activeTouchPrimary = _touchPrimary;
            _activeAccessibility = new UiAccessibilitySettings(
                _textScale,
                _reducedMotion,
                _reducedFlash,
                _reducedVfx);
            _hasActiveConfiguration = true;
        }

        public static Rect ProjectScreenSafeArea(
            Rect localViewport,
            Vector2Int screenSize,
            Rect screenSafeArea)
        {
            if (screenSize.x <= 0 || screenSize.y <= 0)
            {
                return localViewport;
            }

            float scaleX = localViewport.width / screenSize.x;
            float scaleY = localViewport.height / screenSize.y;
            return new Rect(
                localViewport.xMin + screenSafeArea.xMin * scaleX,
                localViewport.yMin + screenSafeArea.yMin * scaleY,
                screenSafeArea.width * scaleX,
                screenSafeArea.height * scaleY);
        }

        private Rect CurrentViewport()
        {
            _parent = _parent != null ? _parent : GetComponent<RectTransform>();
            return _parent.rect;
        }

        private static Rect CurrentSafeArea(Rect viewport)
        {
            return ProjectScreenSafeArea(
                viewport,
                new Vector2Int(Screen.width, Screen.height),
                Screen.safeArea);
        }

        private void ClearCurrent()
        {
            if (Current == null || Current.Root == null)
            {
                Current = null;
                return;
            }

            GameObject root = Current.Root.gameObject;
            root.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(root);
            }
            else
            {
                DestroyImmediate(root);
            }

            Current = null;
        }

        private sealed class ContentSnapshot
        {
            private readonly HudSlotId _slot;
            private readonly UiSemanticState _state;
            private readonly string _header;
            private readonly string _primary;
            private readonly string _secondary;
            private readonly string[] _rows;
            private readonly float[] _meters;

            public ContentSnapshot(
                HudSlotId slot,
                UiSemanticState state,
                string header,
                string primary,
                string secondary,
                string[] rows,
                float[] meters)
            {
                _slot = slot;
                _state = state;
                _header = header;
                _primary = primary;
                _secondary = secondary;
                _rows = rows == null ? Array.Empty<string>() : (string[])rows.Clone();
                _meters = meters == null ? Array.Empty<float>() : (float[])meters.Clone();
            }

            public void Apply(ProductionHudRenderResult target)
            {
                target.ApplyContent(
                    _slot,
                    _state,
                    _header,
                    _primary,
                    _secondary,
                    _rows,
                    _meters);
            }
        }
    }
}
