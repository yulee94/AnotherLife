using System;
using System.Collections.Generic;
using AL.ChampionMode.UI;
using AL.Data.Catalogs.WorldAtlas;
using AL.UI.RealmSelection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.UI.WorldMap
{
    /// <summary>
    /// Runtime-built BDO-adjacent open-map chrome. Not the 2.5D kingdom map.
    /// </summary>
    public sealed class WorldMapOverlay : MonoBehaviour
    {
        private WorldMapPresentation _presentation;
        private WorldAtlasSnapshot _snapshot;
        private MainQuestMapMarkerCatalog _markerCatalog;
        private GameObject _mapRoot;
        private Transform _questMarkerRoot;

        public WorldMapPresentation Presentation => _presentation;

        public static WorldMapOverlay Ensure(WorldAtlasSnapshot snapshot)
        {
            WorldMapOverlay existing = FindObjectOfType<WorldMapOverlay>();
            if (existing != null)
            {
                existing.Bind(snapshot);
                existing.HookSession();
                existing.Refresh();
                return existing;
            }

            var host = new GameObject(WorldMapIds.OverlayRootName);
            WorldMapOverlay overlay = host.AddComponent<WorldMapOverlay>();
            overlay.Bind(snapshot);
            overlay.Build();
            overlay.HookSession();
            overlay.Refresh();
            return overlay;
        }

        public void Bind(WorldAtlasSnapshot snapshot)
        {
            _snapshot = snapshot;
            _presentation = WorldMapPresentation.FromSnapshot(snapshot);
            try
            {
                _markerCatalog = MainQuestMapMarkerCatalog.LoadCanonical();
            }
            catch (Exception exception)
            {
                _markerCatalog = null;
                Debug.LogWarning("Main-quest world-map marker unavailable: " + exception.Message);
            }
        }

        public void HookSession()
        {
            WorldMapSession.Changed -= Refresh;
            WorldMapSession.Changed += Refresh;
            MainQuestMapSession.Changed -= Refresh;
            MainQuestMapSession.Changed += Refresh;
        }

        public void Refresh()
        {
            if (_mapRoot != null)
            {
                _mapRoot.SetActive(WorldMapSession.IsMapOpen);
            }

            RefreshQuestMarker();

            bool mapOpen = WorldMapSession.IsMapOpen;
            GameInputBridge.ApplySuppression(mapOpen);
            if (mapOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (!ChampionHudCameraGate.BlocksLook)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnEnable()
        {
            WorldMapSession.Changed += Refresh;
            MainQuestMapSession.Changed += Refresh;
        }

        private void OnDisable()
        {
            WorldMapSession.Changed -= Refresh;
            MainQuestMapSession.Changed -= Refresh;
        }

        private void OnDestroy()
        {
            GameInputBridge.ApplySuppression(false);
        }

        private void Build()
        {
            EnsureEventSystem();
            Canvas canvas = CreateCanvas(transform);
            Font font = RealmSelectionIdentity.ResolvePresentationFont(22);
            _mapRoot = BuildMap(canvas.transform, font);
        }

        private GameObject BuildMap(Transform parent, Font font)
        {
            Image veil = CreatePanel(parent, "WorldMap_Veil", new Color(0.02f, 0.03f, 0.045f, 0.86f), Vector2.zero, Vector2.one);
            veil.raycastTarget = true;

            Image plate = CreatePanel(
                veil.transform,
                "WorldMap_Plate",
                new Color(0.055f, 0.06f, 0.07f, 0.97f),
                new Vector2(0.05f, 0.06f),
                new Vector2(0.95f, 0.94f));
            CreatePanel(plate.transform, "WorldMap_GoldEdge", new Color(0.78f, 0.68f, 0.42f, 0.55f), new Vector2(0f, 0f), new Vector2(1f, 0.012f));
            CreatePanel(plate.transform, "WorldMap_GoldEdgeTop", new Color(0.78f, 0.68f, 0.42f, 0.55f), new Vector2(0f, 0.988f), new Vector2(1f, 1f));

            CreateText(plate.transform, "WorldMap_Title", font, WorldMapIds.TitleCopy, 34, new Vector2(0.04f, 0.9f), new Vector2(0.6f, 0.98f), TextAnchor.MiddleLeft, new Color(0.93f, 0.86f, 0.62f));
            CreateText(plate.transform, "WorldMap_Temporary", font, WorldMapIds.TemporaryLabel, 14, new Vector2(0.62f, 0.91f), new Vector2(0.78f, 0.97f), TextAnchor.MiddleLeft, new Color(0.72f, 0.62f, 0.38f, 0.9f));
            CreateText(plate.transform, "WorldMap_Hint", font, WorldMapIds.CloseHintCopy, 16, new Vector2(0.04f, 0.02f), new Vector2(0.55f, 0.08f), TextAnchor.MiddleLeft, new Color(0.7f, 0.68f, 0.6f, 0.88f));

            Button close = CreateButton(plate.transform, "WorldMap_Close", font, "✕", new Vector2(0.92f, 0.9f), new Vector2(0.98f, 0.98f), WorldMapSession.CloseMap);

            Image viewport = CreatePanel(
                plate.transform,
                "WorldMap_Viewport",
                new Color(0.035f, 0.04f, 0.05f, 1f),
                new Vector2(0.06f, 0.1f),
                new Vector2(0.94f, 0.88f));

            DrawCompass(viewport.transform, font);
            DrawIsle(viewport.transform, font, _presentation.AccordantIsle);

            for (int i = 0; i < _presentation.Inners.Count; i++)
            {
                DrawInner(viewport.transform, font, _presentation.Inners[i]);
            }

            var questRoot = new GameObject("WorldMapQuestMarkers", typeof(RectTransform));
            questRoot.transform.SetParent(viewport.transform, false);
            RectTransform questRect = questRoot.GetComponent<RectTransform>();
            questRect.anchorMin = Vector2.zero;
            questRect.anchorMax = Vector2.one;
            questRect.offsetMin = Vector2.zero;
            questRect.offsetMax = Vector2.zero;
            _questMarkerRoot = questRoot.transform;
            RefreshQuestMarker();

            close.transform.SetAsLastSibling();
            return veil.gameObject;
        }

        private void RefreshQuestMarker()
        {
            if (_questMarkerRoot == null)
            {
                return;
            }

            ClearChildren(_questMarkerRoot);
            MainQuestMapState state = MainQuestMapSession.Current;
            if (state == null || _snapshot == null || _markerCatalog == null)
            {
                return;
            }

            IReadOnlyList<MainQuestMapMarker> markers =
                MainQuestMapMarkerResolver.ResolveCurrent(
                    _snapshot,
                    _markerCatalog,
                    state.ObjectiveId,
                    state.Realm,
                    state.WhatToDo);
            if (markers.Count != 1)
            {
                return;
            }

            MainQuestMapMarker marker = markers[0];
            Font font = RealmSelectionIdentity.ResolvePresentationFont(18);
            Image icon = CreateAnchored(
                _questMarkerRoot,
                "WorldMapQuestMarker_" + marker.MarkerId,
                new Color(1f, 0.68f, 0.14f, 1f),
                marker.FullMapUv.AsVector,
                new Vector2(25f, 25f));
            icon.transform.SetAsLastSibling();
            Image objectiveCard = CreatePanel(
                _questMarkerRoot,
                "WorldMapQuestObjectiveCard",
                new Color(0.055f, 0.06f, 0.07f, 0.96f),
                new Vector2(0.34f, 0.76f),
                new Vector2(0.66f, 0.9f));
            CreatePanel(
                objectiveCard.transform,
                "WorldMapQuestObjectiveAccent",
                new Color(1f, 0.68f, 0.14f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0.018f, 1f));
            CreateText(
                objectiveCard.transform,
                "WorldMapQuestWhatToDo",
                font,
                "MAIN QUEST\n" + marker.WhatToDo,
                15,
                new Vector2(0.05f, 0.08f),
                new Vector2(0.96f, 0.92f),
                TextAnchor.MiddleLeft,
                new Color(1f, 0.88f, 0.55f, 1f));
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


        private static void DrawInner(Transform viewport, Font font, WorldMapInnerRealm inner)
        {
            Rect land = CornerLandRect(inner.Capital.Uv);
            Image landmass = CreatePanel(viewport, inner.InnerAtlasZoneId, LandColor(inner.RealmId), land.min, land.max);
            landmass.transform.SetAsFirstSibling();

            CreateLine(viewport, inner.InnerWallId, inner.WallFrom, inner.WallTo, new Color(0.42f, 0.55f, 0.72f, 0.95f));
            DrawSettlement(viewport, font, inner.Capital, 18f, new Color(0.92f, 0.78f, 0.42f));
            DrawSettlement(viewport, font, inner.OutpostA, 10f, new Color(0.78f, 0.76f, 0.7f));
            DrawSettlement(viewport, font, inner.OutpostB, 10f, new Color(0.78f, 0.76f, 0.7f));
            Rect label = LabelRect(inner.Capital.Uv, 0.18f, 0.05f);
            CreateText(
                viewport,
                inner.InnerAtlasZoneId + "_label",
                font,
                inner.DisplayName,
                16,
                label.min,
                label.max,
                TextAnchor.MiddleCenter,
                new Color(0.93f, 0.9f, 0.8f));
        }

        private static void DrawIsle(Transform viewport, Font font, WorldMapSettlement isle)
        {
            CreatePanel(
                viewport,
                isle.Id,
                new Color(0.18f, 0.2f, 0.24f, 0.95f),
                new Vector2(0.445f, 0.445f),
                new Vector2(0.555f, 0.555f));
            CreateText(
                viewport,
                isle.Id + "_label",
                font,
                isle.Label,
                13,
                new Vector2(0.4f, 0.38f),
                new Vector2(0.6f, 0.44f),
                TextAnchor.MiddleCenter,
                new Color(0.86f, 0.82f, 0.7f));
        }

        private static void DrawSettlement(Transform viewport, Font font, WorldMapSettlement settlement, float size, Color color)
        {
            Vector2 min = new Vector2(settlement.Uv.X, settlement.Uv.Y);
            Image mark = CreateAnchored(viewport, settlement.Id, color, min, new Vector2(size, size));
            mark.transform.SetAsLastSibling();
            CreateText(
                viewport,
                settlement.Id + "_label",
                font,
                settlement.Label,
                12,
                new Vector2(settlement.Uv.X - 0.07f, settlement.Uv.Y - 0.055f),
                new Vector2(settlement.Uv.X + 0.07f, settlement.Uv.Y - 0.018f),
                TextAnchor.MiddleCenter,
                new Color(0.86f, 0.84f, 0.76f));
        }

        private static void DrawCompass(Transform viewport, Font font)
        {
            CreateText(viewport, "Compass_N", font, "N", 14, new Vector2(0.47f, 0.94f), new Vector2(0.53f, 0.99f), TextAnchor.MiddleCenter, new Color(0.82f, 0.74f, 0.5f));
            CreateText(viewport, "Compass_S", font, "S", 14, new Vector2(0.47f, 0.01f), new Vector2(0.53f, 0.06f), TextAnchor.MiddleCenter, new Color(0.82f, 0.74f, 0.5f));
            CreateText(viewport, "Compass_W", font, "W", 14, new Vector2(0.01f, 0.47f), new Vector2(0.06f, 0.53f), TextAnchor.MiddleCenter, new Color(0.82f, 0.74f, 0.5f));
            CreateText(viewport, "Compass_E", font, "E", 14, new Vector2(0.94f, 0.47f), new Vector2(0.99f, 0.53f), TextAnchor.MiddleCenter, new Color(0.82f, 0.74f, 0.5f));
        }

        private static Rect CornerLandRect(WorldMapUv capital)
        {
            bool west = capital.X < 0.5f;
            bool south = capital.Y < 0.5f;
            float x0 = west ? 0.01f : 0.68f;
            float x1 = west ? 0.32f : 0.99f;
            float y0 = south ? 0.01f : 0.68f;
            float y1 = south ? 0.32f : 0.99f;
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        private static Rect LabelRect(WorldMapUv uv, float w, float h)
        {
            bool west = uv.X < 0.5f;
            bool south = uv.Y < 0.5f;
            float x = west ? uv.X + 0.02f : uv.X - w - 0.02f;
            float y = south ? uv.Y + 0.03f : uv.Y - h - 0.03f;
            return Rect.MinMaxRect(x, y, x + w, y + h);
        }

        private static Color LandColor(string realmId)
        {
            switch (realmId)
            {
                case "stonehold":
                    return new Color(0.22f, 0.2f, 0.18f, 0.96f);
                case "eldergrove":
                    return new Color(0.13f, 0.2f, 0.15f, 0.96f);
                case "crownlands":
                    return new Color(0.2f, 0.2f, 0.16f, 0.96f);
                case "umbral":
                    return new Color(0.16f, 0.12f, 0.2f, 0.96f);
                default:
                    return new Color(0.16f, 0.16f, 0.18f, 0.96f);
            }
        }

        private static Canvas CreateCanvas(Transform parent)
        {
            var canvasObject = new GameObject("WorldMap_Canvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 420;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static Image CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        private static Image CreateAnchored(Transform parent, string name, Color color, Vector2 normalized, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = normalized;
            rect.anchorMax = normalized;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return image;
        }

        private static void CreateLine(Transform parent, string name, WorldMapUv from, WorldMapUv to, Color color)
        {
            Vector2 a = from.AsVector;
            Vector2 b = to.AsVector;
            Vector2 mid = (a + b) * 0.5f;
            Vector2 delta = b - a;
            float length = delta.magnitude;
            Image line = CreateAnchored(parent, name, color, mid, new Vector2(length * 920f, 7f));
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            string value,
            int size,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TextAnchor align,
            Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = align;
            text.color = color;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            Font font,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            UnityEngine.Events.UnityAction onClick)
        {
            Image image = CreatePanel(parent, name, new Color(0.12f, 0.11f, 0.09f, 0.96f), anchorMin, anchorMax);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            CreateText(image.transform, name + "_label", font, label, 18, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, new Color(0.9f, 0.86f, 0.74f));
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            return button;
        }
    }

    internal static class GameInputBridge
    {
        public static void ApplySuppression(bool suppressed)
        {
            AL.Input.GameInput.SetGameplaySuppressed(suppressed);
        }
    }
}
