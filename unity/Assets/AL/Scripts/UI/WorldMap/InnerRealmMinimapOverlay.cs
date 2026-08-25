using System;
using System.Collections.Generic;
using AL.ChampionMode.Control;
using AL.Data.Catalogs.WorldAtlas;
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
        private RectTransform _playerMarker;
        private RectTransform _playerLabel;
        private RectTransform _questMarker;
        private InnerRealmSlotLayout _inner;
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
                existing.Bind(snapshot, player);
                return existing;
            }

            var root = new GameObject(RootName);
            InnerRealmMinimapOverlay overlay = root.AddComponent<InnerRealmMinimapOverlay>();
            overlay.Build();
            overlay.Bind(snapshot, player);
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

        private void OnEnable()
        {
            MainQuestMapSession.Changed += Refresh;
        }

        private void OnDisable()
        {
            MainQuestMapSession.Changed -= Refresh;
        }

        private void LateUpdate()
        {
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
            var canvasObject = new GameObject("InnerRealmMinimapCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 310;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            Font font = RealmSelectionIdentity.ResolvePresentationFont(18);
            Image plate = CreatePanel(canvasObject.transform, "MinimapPlate", new Color(0.025f, 0.03f, 0.04f, 0.92f));
            RectTransform plateRect = plate.rectTransform;
            plateRect.anchorMin = Vector2.one;
            plateRect.anchorMax = Vector2.one;
            plateRect.pivot = Vector2.one;
            plateRect.anchoredPosition = new Vector2(-24f, -24f);
            plateRect.sizeDelta = new Vector2(360f, 330f);

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
            mapRect.anchorMin = new Vector2(0.5f, 0f);
            mapRect.anchorMax = new Vector2(0.5f, 0f);
            mapRect.pivot = new Vector2(0.5f, 0f);
            mapRect.anchoredPosition = new Vector2(0f, 16f);
            mapRect.sizeDelta = new Vector2(318f, 274f);

            Color compassColor = new Color(0.84f, 0.78f, 0.60f, 0.92f);
            CreateText(map.transform, "CompassNorth", font, "N", 12,
                new Vector2(151f, -4f), new Vector2(16f, 18f), TextAnchor.MiddleCenter, compassColor);
            CreateText(map.transform, "CompassEast", font, "E", 12,
                new Vector2(298f, -128f), new Vector2(16f, 18f), TextAnchor.MiddleCenter, compassColor);
            CreateText(map.transform, "CompassSouth", font, "S", 12,
                new Vector2(151f, -252f), new Vector2(16f, 18f), TextAnchor.MiddleCenter, compassColor);
            CreateText(map.transform, "CompassWest", font, "W", 12,
                new Vector2(4f, -128f), new Vector2(16f, 18f), TextAnchor.MiddleCenter, compassColor);

            var content = new GameObject("MinimapContent", typeof(RectTransform));
            content.transform.SetParent(map.transform, false);
            _content = content.GetComponent<RectTransform>();
            _content.anchorMin = Vector2.zero;
            _content.anchorMax = Vector2.one;
            _content.offsetMin = new Vector2(12f, 12f);
            _content.offsetMax = new Vector2(-12f, -12f);
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

            float pulse = (Mathf.Sin(Time.unscaledTime * 3.6f) + 1f) * 0.5f;
            _questMarker.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.24f, pulse);
            Image image = _questMarker.GetComponent<Image>();
            if (image != null)
            {
                Color color = image.color;
                color.a = Mathf.Lerp(0.68f, 1f, pulse);
                image.color = color;
            }
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
            Color color)
        {
            Text text = CreateText(
                parent,
                name,
                font,
                value,
                13,
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
