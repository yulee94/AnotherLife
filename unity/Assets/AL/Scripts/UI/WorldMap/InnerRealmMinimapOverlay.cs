using System;
using System.Collections.Generic;
using AL.ChampionMode.Control;
using AL.Data.Catalogs.WorldAtlas;
using AL.UI.DesignSystem;
using AL.UI.RealmSelection;
using AL.World;
using UnityEngine;
using UnityEngine.UI;

namespace AL.UI.WorldMap
{
    /// <summary>
    /// Inner-realm-only HUD minimap for 3D scenes. Atlas-derived castle/Area
    /// positions, the champion, and the current main-quest marker are its only
    /// enumerable destinations.
    /// </summary>
    public sealed class InnerRealmMinimapOverlay : MonoBehaviour
    {
        public const string RootName = "InnerRealmMinimap";
        public const string PlayerMarkerName = "MinimapPlayerMarker";

        private WorldAtlasSnapshot _snapshot;
        private MainQuestMapMarkerCatalog _catalog;
        private Transform _player;
        private RectTransform _content;
        private RectTransform _plate;
        private RectTransform _playerMarker;
        private RectTransform _playerLabel;
        private RectTransform _questMarker;
        private InnerRealmSlotLayout _inner;
        private HudResponsiveCompositionSet _compositions;
        private Vector2Int _lastScreenSize;
        private Rect _lastSafeArea;
        private bool _lastExpanded;
        private bool _lastCombatDense;
        private readonly List<string> _visibleMarkerIds = new List<string>();
        private IReadOnlyList<MainQuestMapMarker> _currentQuestMarkers =
            Array.Empty<MainQuestMapMarker>();

        public IReadOnlyList<string> VisibleMarkerIds => _visibleMarkerIds.AsReadOnly();
        public IReadOnlyList<MainQuestMapMarker> CurrentQuestMarkers => _currentQuestMarkers;

        public static InnerRealmMinimapOverlay Ensure(
            WorldAtlasSnapshot snapshot,
            Transform player = null)
        {
            InnerRealmMinimapOverlay existing = FindObjectOfType<InnerRealmMinimapOverlay>();
            if (existing != null)
            {
                existing.EnsureSurfaceHealthy(snapshot, player);
                return existing;
            }

            var root = new GameObject(RootName);
            InnerRealmMinimapOverlay overlay = root.AddComponent<InnerRealmMinimapOverlay>();
            overlay.EnsureSurfaceHealthy(snapshot, player);
            return overlay;
        }

        public void Bind(WorldAtlasSnapshot snapshot, Transform player = null)
        {
            _snapshot = snapshot;
            if (player != null)
            {
                _player = player;
            }

            try
            {
                _catalog = MainQuestMapMarkerCatalog.LoadCanonical();
            }
            catch (Exception exception)
            {
                _catalog = null;
                Debug.LogWarning("Main-quest minimap markers unavailable: " + exception.Message);
            }

            Refresh();
        }

        internal bool IsSurfaceHealthy()
        {
            if (!gameObject.activeSelf || !enabled)
            {
                return false;
            }

            Transform canvasRoot = transform.Find("InnerRealmMinimapCanvas");
            Transform plate = canvasRoot != null ? canvasRoot.Find("MinimapPlate") : null;
            Transform viewport = plate != null ? plate.Find("MinimapViewport") : null;
            Transform content = viewport != null ? viewport.Find("MinimapContent") : null;
            Canvas canvas = canvasRoot != null ? canvasRoot.GetComponent<Canvas>() : null;
            CanvasScaler scaler =
                canvasRoot != null ? canvasRoot.GetComponent<CanvasScaler>() : null;
            GraphicRaycaster raycaster =
                canvasRoot != null ? canvasRoot.GetComponent<GraphicRaycaster>() : null;

            return canvasRoot != null && canvasRoot.gameObject.activeSelf &&
                   canvas != null && canvas.enabled &&
                   scaler != null && scaler.enabled &&
                   raycaster != null && raycaster.enabled &&
                   plate != null && plate.gameObject.activeSelf &&
                   viewport != null && viewport.gameObject.activeSelf &&
                   content != null && content.gameObject.activeSelf &&
                   _content == content;
        }

        internal void EnsureSurfaceHealthy(
            WorldAtlasSnapshot snapshot,
            Transform player = null)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (!enabled)
            {
                enabled = true;
            }

            if (!IsSurfaceHealthy())
            {
                Build();
            }

            Bind(snapshot, player);
        }

        private void OnEnable()
        {
            MainQuestMapSession.Changed += Refresh;
            ProgressiveMapSession.Changed += Refresh;
        }

        private void OnDisable()
        {
            MainQuestMapSession.Changed -= Refresh;
            ProgressiveMapSession.Changed -= Refresh;
        }

        private void LateUpdate()
        {
            ApplyResponsiveLayout();
            if (_player == null)
            {
                ChampionController champion = FindObjectOfType<ChampionController>();
                if (champion != null)
                {
                    _player = champion.transform;
                }
            }

            UpdatePlayerMarker();
            UpdateQuestPulse();
        }

        private void Build()
        {
            ClearVisualTree();
            _content = null;
            _plate = null;
            _playerMarker = null;
            _playerLabel = null;
            var canvasObject = new GameObject("InnerRealmMinimapCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 310;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Font font = RealmSelectionIdentity.ResolvePresentationFont(18);
            Image plate = CreatePanel(canvasObject.transform, "MinimapPlate", new Color(0.025f, 0.03f, 0.04f, 0.92f));
            RectTransform plateRect = plate.rectTransform;
            _plate = plateRect;
            ApplyResponsiveLayout(force: true);

            CreateText(
                plate.transform,
                "MinimapTitle",
                font,
                "INNER REALM",
                16,
                new Vector2(16f, -12f),
                new Vector2(328f, 24f),
                TextAnchor.MiddleLeft,
                new Color(0.93f, 0.86f, 0.62f));
            Image map = CreatePanel(plate.transform, "MinimapViewport", new Color(0.055f, 0.065f, 0.075f, 1f));
            RectTransform mapRect = map.rectTransform;
            mapRect.anchorMin = new Vector2(0.04f, 0.06f);
            mapRect.anchorMax = new Vector2(0.96f, 0.82f);
            mapRect.offsetMin = Vector2.zero;
            mapRect.offsetMax = Vector2.zero;

            Color compassColor = new Color(0.84f, 0.78f, 0.60f, 0.92f);
            CreateCompassText(map.transform, "CompassNorth", font, "N", new Vector2(0.5f, 0.96f), compassColor);
            CreateCompassText(map.transform, "CompassEast", font, "E", new Vector2(0.96f, 0.5f), compassColor);
            CreateCompassText(map.transform, "CompassSouth", font, "S", new Vector2(0.5f, 0.04f), compassColor);
            CreateCompassText(map.transform, "CompassWest", font, "W", new Vector2(0.04f, 0.5f), compassColor);

            var content = new GameObject("MinimapContent", typeof(RectTransform));
            content.transform.SetParent(map.transform, false);
            _content = content.GetComponent<RectTransform>();
            _content.anchorMin = Vector2.zero;
            _content.anchorMax = Vector2.one;
            _content.offsetMin = new Vector2(12f, 12f);
            _content.offsetMax = new Vector2(-12f, -12f);
            ApplyAccessibility();
        }

        private void ApplyResponsiveLayout(bool force = false)
        {
            if (_plate == null)
            {
                return;
            }
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            Vector2Int screenSize = new Vector2Int(width, height);
            Rect physicalSafeArea = Screen.safeArea;
            bool expanded = ProgressiveMapSession.MinimapExpanded;
            bool combatDense = ProgressiveMapSession.CombatDense;
            if (!force &&
                screenSize == _lastScreenSize &&
                physicalSafeArea == _lastSafeArea &&
                expanded == _lastExpanded &&
                combatDense == _lastCombatDense)
            {
                return;
            }

            _compositions ??= HudResponsiveCompositionSet.LoadDefault();
            bool touchPrimary =
                Application.isMobilePlatform || UnityEngine.Input.touchSupported;
            HudCompositionDefinition composition =
                _compositions.Resolve(width, height, touchPrimary);
            Rect safeArea = HudLayoutProjection.ApplySafeAreaPadding(
                physicalSafeArea,
                composition);
            MapSurfaceLayout layout = MapInterfaceLayout.Resolve(
                composition,
                safeArea,
                combatDense);
            Rect target = expanded ? layout.ExpandedMinimapRect : layout.MinimapRect;
            _plate.anchorMin = new Vector2(target.xMin / width, target.yMin / height);
            _plate.anchorMax = new Vector2(target.xMax / width, target.yMax / height);
            _plate.pivot = new Vector2(0.5f, 0.5f);
            _plate.offsetMin = Vector2.zero;
            _plate.offsetMax = Vector2.zero;

            _lastScreenSize = screenSize;
            _lastSafeArea = physicalSafeArea;
            _lastExpanded = expanded;
            _lastCombatDense = combatDense;
        }

        private void ClearVisualTree()
        {
            while (transform.childCount > 0)
            {
                Transform child = transform.GetChild(transform.childCount - 1);
                child.SetParent(null, false);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void Refresh()
        {
            if (_content == null || _snapshot == null)
            {
                return;
            }

            ClearChildren(_content);
            _visibleMarkerIds.Clear();
            _currentQuestMarkers = Array.Empty<MainQuestMapMarker>();
            _playerMarker = null;
            _playerLabel = null;
            _questMarker = null;
            _inner = null;

            if (ProgressiveMapSession.IsConfigured)
            {
                RefreshProgressiveMap();
                return;
            }

            MainQuestMapState state = MainQuestMapSession.Current;
            if (state == null)
            {
                return;
            }

            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(_snapshot);
            string realmId = InnerRealmWorldLayout.RealmCatalogId(state.Realm);
            if (!layout.TryGetInner(realmId, out _inner))
            {
                return;
            }

            KingdomWorldMapQueryResult destinations =
                KingdomWorldMapQuery.Enumerate(_snapshot, state.Realm);
            Font font = RealmSelectionIdentity.ResolvePresentationFont(12);
            for (int i = 0; i < destinations.Markers.Count; i++)
            {
                KingdomWorldMapMarker destination = destinations.Markers[i];
                if (!TryGetWorldPosition(destination.Id, out Vector3 position))
                {
                    continue;
                }

                _visibleMarkerIds.Add(destination.Id);
                WorldMapUv uv = MainQuestMapMarkerResolver.ProjectToInnerMap(_inner, position);
                CreateMarker(
                    _content,
                    destination.Id,
                    uv,
                    i == 0 ? 12f : 8f,
                    new Color(0.68f, 0.72f, 0.76f, 0.9f));
                CreateMarkerLabel(
                    _content,
                    destination.Id + "_label",
                    font,
                    i == 0 ? "Capital" : i == 1 ? "Area I" : "Area II",
                    uv,
                    new Vector2(0f, -17f),
                    new Color(0.76f, 0.78f, 0.78f, 0.92f));
            }

            _currentQuestMarkers = MainQuestMapMarkerResolver.ResolveCurrent(
                _snapshot,
                _catalog,
                state.ObjectiveId,
                state.Realm,
                state.WhatToDo);
            if (_currentQuestMarkers.Count == 1)
            {
                MainQuestMapMarker marker = _currentQuestMarkers[0];
                Image questMarker = CreateMarker(
                    _content,
                    "MinimapQuestMarker_" + marker.MarkerId,
                    marker.MinimapUv,
                    22f,
                    new Color(1f, 0.72f, 0.18f, 0.96f));
                _questMarker = questMarker.rectTransform;
                CreateMarkerLabel(
                    _content,
                    "MinimapQuestMarkerLabel",
                    font,
                    "MAIN QUEST",
                    marker.MinimapUv,
                    new Vector2(0f, 20f),
                    new Color(1f, 0.82f, 0.38f, 1f));
            }

            Image player = CreateMarker(
                _content,
                PlayerMarkerName,
                new WorldMapUv(0.5f, 0.5f),
                14f,
                new Color(0.28f, 0.78f, 1f, 1f));
            _playerMarker = player.rectTransform;
            Text playerLabel = CreateMarkerLabel(
                _content,
                "MinimapPlayerLabel",
                font,
                "YOU",
                new WorldMapUv(0.5f, 0.5f),
                new Vector2(0f, 16f),
                new Color(0.48f, 0.84f, 1f, 1f));
            _playerLabel = playerLabel.rectTransform;
            UpdatePlayerMarker();
            ApplyAccessibility();
        }

        private void RefreshProgressiveMap()
        {
            ProgressiveMapSnapshot snapshot = ProgressiveMapSession.Current;
            if (snapshot == null)
            {
                return;
            }

            Font font = RealmSelectionIdentity.ResolvePresentationFont(12);
            UiProductionDesignTokens tokens = UiProductionDesignTokens.LoadDefault();
            for (int i = 0; i < snapshot.Minimap.Items.Count; i++)
            {
                MapDisplayItem item = snapshot.Minimap.Items[i];
                WorldMapUv uv = item.Kind == MapDisplayItemKind.Player ||
                                item.Kind == MapDisplayItemKind.Party
                    ? new WorldMapUv(
                        item.NormalizedPosition.x,
                        item.NormalizedPosition.y)
                    : ResolveProgressiveUv(item);
                MapItemVisualTreatment visual = MapInterfaceAccessibility.Resolve(
                    tokens,
                    item.Kind,
                    ProgressiveMapSession.Accessibility);
                float size = item.Kind == MapDisplayItemKind.Player ? 16f : 12f;
                CreateMarker(_content, item.Id, uv, size, visual.Color);
                string shape = string.IsNullOrWhiteSpace(item.NonColorShape)
                    ? item.Kind.ToString()
                    : item.NonColorShape;
                CreateMarkerLabel(
                    _content,
                    item.Id + "_label",
                    font,
                    "[" + shape.ToUpperInvariant() + "] " + item.Label,
                    uv,
                    new Vector2(0f, 17f),
                    visual.Color);
                _visibleMarkerIds.Add(item.Id);
            }
            ApplyAccessibility();
        }

        private static WorldMapUv ResolveProgressiveUv(MapDisplayItem item)
        {
            string identifier = string.IsNullOrEmpty(item.SourceId)
                ? string.IsNullOrEmpty(item.FeatureId) ? item.Id : item.FeatureId
                : item.SourceId;
            Vector2 projected = MapInterfacePlacement.ProjectIdentifier(
                identifier,
                MapSurfaceKind.Minimap);
            return new WorldMapUv(projected.x, projected.y);
        }

        private bool TryGetWorldPosition(string markerId, out Vector3 position)
        {
            if (string.Equals(markerId, _inner.CapitalPoiId, StringComparison.Ordinal))
            {
                position = _inner.CapitalPosition;
                return true;
            }

            if (string.Equals(markerId, _inner.OutpostAPoiId, StringComparison.Ordinal))
            {
                position = _inner.OutpostAPosition;
                return true;
            }

            if (string.Equals(markerId, _inner.OutpostBPoiId, StringComparison.Ordinal))
            {
                position = _inner.OutpostBPosition;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private void UpdatePlayerMarker()
        {
            if (_playerMarker == null || _player == null || _inner == null)
            {
                return;
            }

            WorldMapUv uv = MainQuestMapMarkerResolver.ProjectToInnerMap(_inner, _player.position);
            Vector2 anchor = uv.AsVector;
            _playerMarker.anchorMin = anchor;
            _playerMarker.anchorMax = anchor;
            _playerMarker.localRotation = Quaternion.Euler(0f, 0f, -_player.eulerAngles.y + 45f);
            if (_playerLabel != null)
            {
                _playerLabel.anchorMin = anchor;
                _playerLabel.anchorMax = anchor;
            }
        }

        private void UpdateQuestPulse()
        {
            if (_questMarker == null)
            {
                return;
            }

            UiAccessibilitySettings settings = ProgressiveMapSession.Accessibility.Settings;
            Image image = _questMarker.GetComponent<Image>();
            if (settings.ReducedMotion || settings.ReducedFlash || settings.ReducedVfx)
            {
                _questMarker.localScale = Vector3.one;
                if (image != null)
                {
                    Color staticColor = image.color;
                    staticColor.a = 1f;
                    image.color = staticColor;
                }
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * 3.6f) + 1f) * 0.5f;
            _questMarker.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.24f, pulse);
            if (image != null)
            {
                Color color = image.color;
                color.a = Mathf.Lerp(0.68f, 1f, pulse);
                image.color = color;
            }
        }

        private void ApplyAccessibility()
        {
            UiAccessibilityRuntime.ApplySettings(
                gameObject,
                ProgressiveMapSession.Accessibility.Settings);
        }

        private static Image CreateMarker(
            Transform parent,
            string name,
            WorldMapUv uv,
            float size,
            Color color)
        {
            Image marker = CreatePanel(parent, name, color);
            RectTransform rect = marker.rectTransform;
            Vector2 anchor = uv.AsVector;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            return marker;
        }

        private static Text CreateMarkerLabel(
            Transform parent,
            string name,
            Font font,
            string value,
            WorldMapUv uv,
            Vector2 offset,
            Color color,
            float textScale = 1f)
        {
            Text text = CreateText(
                parent,
                name,
                font,
                value,
                Mathf.RoundToInt(13f * textScale),
                Vector2.zero,
                new Vector2(100f, 20f),
                TextAnchor.MiddleCenter,
                color);
            RectTransform rect = text.rectTransform;
            Vector2 anchor = uv.AsVector;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            return text;
        }

        private static Text CreateCompassText(
            Transform parent,
            string name,
            Font font,
            string value,
            Vector2 anchor,
            Color color)
        {
            Text text = CreateText(
                parent,
                name,
                font,
                value,
                12,
                Vector2.zero,
                new Vector2(20f, 20f),
                TextAnchor.MiddleCenter,
                color);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return text;
        }

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            string value,
            int size,
            Vector2 position,
            Vector2 dimensions,
            TextAnchor alignment,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            return text;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}
