using System;
using System.Collections.Generic;
using System.Linq;
using AL.Data.Catalogs.MapDisclosure;
using AL.UI.DesignSystem;
using UnityEngine;

namespace AL.UI.WorldMap
{
    public enum MapSurfaceKind
    {
        Minimap = 0,
        WorldMap = 1
    }

    public enum MapDisplayItemKind
    {
        Feature = 0,
        Route = 1,
        Objective = 2,
        Allegiance = 3,
        Player = 4,
        Party = 5
    }

    public enum MapAuthorityPresentationState
    {
        Awaiting = 0,
        Authoritative = 1,
        Correcting = 2
    }

    public enum MapDetailLevel
    {
        Local = 0,
        Regional = 1,
        Strategic = 2
    }

    public sealed class MapIndicator
    {
        public MapIndicator(
            string id,
            string featureId,
            string label,
            string nonColorShape,
            Vector2 normalizedPosition)
        {
            Id = id ?? string.Empty;
            FeatureId = featureId ?? string.Empty;
            Label = label ?? string.Empty;
            NonColorShape = nonColorShape ?? string.Empty;
            NormalizedPosition = new Vector2(
                Mathf.Clamp01(normalizedPosition.x),
                Mathf.Clamp01(normalizedPosition.y));
        }

        public string Id { get; }
        public string FeatureId { get; }
        public string Label { get; }
        public string NonColorShape { get; }
        public Vector2 NormalizedPosition { get; }
    }

    public sealed class MapIndicatorAuthoritySnapshot
    {
        public MapIndicatorAuthoritySnapshot(
            long authorityEpoch,
            long authorityRevision,
            MapIndicator player,
            IEnumerable<MapIndicator> party)
        {
            AuthorityEpoch = authorityEpoch;
            AuthorityRevision = authorityRevision;
            Player = player;
            Party = Array.AsReadOnly((party ?? Array.Empty<MapIndicator>())
                .Where(value => value != null)
                .OrderBy(value => value.Id, StringComparer.Ordinal)
                .ToArray());
        }

        public long AuthorityEpoch { get; }
        public long AuthorityRevision { get; }
        public MapIndicator Player { get; }
        public IReadOnlyList<MapIndicator> Party { get; }
    }

    public sealed class MapDisplayItem
    {
        internal MapDisplayItem(
            string id,
            MapDisplayItemKind kind,
            string sourceId,
            string featureId,
            string label,
            string assetReference,
            string nonColorShape,
            Vector2 normalizedPosition)
        {
            Id = id ?? string.Empty;
            Kind = kind;
            SourceId = sourceId ?? string.Empty;
            FeatureId = featureId ?? string.Empty;
            Label = label ?? string.Empty;
            AssetReference = assetReference ?? string.Empty;
            NonColorShape = nonColorShape ?? string.Empty;
            NormalizedPosition = normalizedPosition;
        }

        public string Id { get; }
        public MapDisplayItemKind Kind { get; }
        public string SourceId { get; }
        public string FeatureId { get; }
        public string Label { get; }
        public string AssetReference { get; }
        public string NonColorShape { get; }
        public Vector2 NormalizedPosition { get; }
    }

    public sealed class MapSurfaceProjection
    {
        internal MapSurfaceProjection(
            MapSurfaceKind surface,
            MapDetailLevel detailLevel,
            IEnumerable<MapDisplayItem> items)
        {
            Surface = surface;
            DetailLevel = detailLevel;
            Items = Array.AsReadOnly((items ?? Array.Empty<MapDisplayItem>())
                .OrderBy(value => value.Kind)
                .ThenBy(value => value.Id, StringComparer.Ordinal)
                .ToArray());
            ItemIds = Array.AsReadOnly(Items.Select(value => value.Id).ToArray());
        }

        public MapSurfaceKind Surface { get; }
        public MapDetailLevel DetailLevel { get; }
        public IReadOnlyList<MapDisplayItem> Items { get; }
        public IReadOnlyList<string> ItemIds { get; }
    }

    public sealed class ProgressiveMapSnapshot
    {
        internal ProgressiveMapSnapshot(
            MapAuthorityPresentationState authorityState,
            bool requiresRefresh,
            long authorityEpoch,
            long authorityRevision,
            MapSurfaceProjection minimap,
            MapSurfaceProjection worldMap)
        {
            AuthorityState = authorityState;
            RequiresRefresh = requiresRefresh;
            AuthorityEpoch = authorityEpoch;
            AuthorityRevision = authorityRevision;
            Minimap = minimap;
            WorldMap = worldMap;
        }

        public MapAuthorityPresentationState AuthorityState { get; }
        public bool RequiresRefresh { get; }
        public long AuthorityEpoch { get; }
        public long AuthorityRevision { get; }
        public MapSurfaceProjection Minimap { get; }
        public MapSurfaceProjection WorldMap { get; }
    }

    public readonly struct MapAccessibilityProfile
    {
        public MapAccessibilityProfile(
            UiAccessibilitySettings settings,
            bool highContrast)
        {
            Settings = settings;
            HighContrast = highContrast;
        }

        public UiAccessibilitySettings Settings { get; }
        public bool HighContrast { get; }

        public static MapAccessibilityProfile Default =>
            new MapAccessibilityProfile(
                new UiAccessibilitySettings(
                    textScale: 1f,
                    reducedMotion: false,
                    reducedFlash: false,
                    reducedVfx: false),
                highContrast: false);
    }

    public readonly struct MapItemVisualTreatment
    {
        internal MapItemVisualTreatment(
            Color color,
            float textScale,
            bool animate,
            float flashOpacity)
        {
            Color = color;
            TextScale = textScale;
            Animate = animate;
            FlashOpacity = flashOpacity;
        }

        public Color Color { get; }
        public float TextScale { get; }
        public bool Animate { get; }
        public float FlashOpacity { get; }
    }

    public static class MapInterfaceAccessibility
    {
        public static MapItemVisualTreatment Resolve(
            UiProductionDesignTokens tokens,
            MapDisplayItemKind kind,
            MapAccessibilityProfile profile)
        {
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            UiSemanticState state = kind == MapDisplayItemKind.Objective
                ? UiSemanticState.Warning
                : kind == MapDisplayItemKind.Player || kind == MapDisplayItemKind.Party
                    ? UiSemanticState.Friendly
                    : kind == MapDisplayItemKind.Allegiance
                        ? UiSemanticState.Focused
                        : UiSemanticState.Neutral;
            Color color = tokens.GetStateTreatment(state).Color;
            if (profile.HighContrast)
            {
                float brightest = Mathf.Max(color.r, color.g, color.b);
                float scale = brightest <= 0.0001f ? 1f : 0.9f / brightest;
                color = new Color(
                    Mathf.Clamp01(color.r * scale),
                    Mathf.Clamp01(color.g * scale),
                    Mathf.Clamp01(color.b * scale),
                    1f);
            }

            UiAccessibilityPresentation presentation =
                tokens.ResolveAccessibility(profile.Settings);
            return new MapItemVisualTreatment(
                color,
                presentation.TextScale,
                !profile.Settings.ReducedMotion,
                presentation.FlashOpacity);
        }
    }

    /// <summary>
    /// Stable provisional placement while the atlas placement authority remains
    /// unresolved. The identifier comes from catalogs; no feature coordinates or
    /// realm-specific layout data are duplicated in UI code.
    /// </summary>
    public static class MapInterfacePlacement
    {
        public static Vector2 ProjectIdentifier(string identifier, MapSurfaceKind surface)
        {
            uint hash = 2166136261u;
            string value = identifier ?? string.Empty;
            unchecked
            {
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
            }

            float angle = (hash % 3600u) * (Mathf.PI * 2f / 3600f);
            float radialUnit = ((hash >> 12) % 1000u) / 999f;
            float maximumRadius = surface == MapSurfaceKind.Minimap ? 0.32f : 0.24f;
            float radius = Mathf.Lerp(0.1f, maximumRadius, radialUnit);
            return new Vector2(
                Mathf.Clamp01(0.5f + Mathf.Cos(angle) * radius),
                Mathf.Clamp01(0.5f + Mathf.Sin(angle) * radius));
        }
    }

    public static class MapDisplayLabels
    {
        public static bool IsSafeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }
            bool previousUnderscore = false;
            for (int i = 1; i < value.Length; i++)
            {
                char character = value[i];
                bool underscore = character == '_';
                bool valid = (character >= 'a' && character <= 'z') ||
                             (character >= '0' && character <= '9') ||
                             underscore;
                if (!valid || (underscore && previousUnderscore))
                {
                    return false;
                }
                previousUnderscore = underscore;
            }
            return !previousUnderscore;
        }

        public static string FromIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return string.Empty;
            }
            char[] characters = identifier.Replace('_', ' ').ToCharArray();
            bool capitalize = true;
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] == ' ')
                {
                    capitalize = true;
                }
                else if (capitalize)
                {
                    characters[i] = char.ToUpperInvariant(characters[i]);
                    capitalize = false;
                }
            }
            return new string(characters);
        }
    }

    /// <summary>
    /// Projects one catalog plus one reconciled server snapshot into both map surfaces.
    /// Relationship checks intentionally fail closed so a route, objective, allegiance,
    /// or delayed party update cannot reveal an undiscovered feature.
    /// </summary>
    public sealed class ProgressiveMapController
    {
        private readonly MapDisclosureCatalogSnapshot _catalog;
        private MapDisclosureProjection _disclosure;
        private MapIndicatorAuthoritySnapshot _indicators;
        private MapDetailLevel _detailLevel = MapDetailLevel.Local;

        public ProgressiveMapController(MapDisclosureCatalogSnapshot catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _disclosure = MapDisclosureProjection.AwaitingAuthority(catalog);
            Rebuild();
        }

        public event Action Changed;

        public ProgressiveMapSnapshot Current { get; private set; }
        public string CatalogSha256 => _catalog.SourceSha256;

        public MapDisclosureReconcileDisposition ApplyAuthoritative(
            MapDisclosureAuthoritySnapshot incoming)
        {
            if (incoming == null)
            {
                throw new ArgumentNullException(nameof(incoming));
            }

            MapDisclosureReconcileResult result = MapDisclosureReconciler.ApplyAuthoritative(
                _disclosure,
                incoming,
                _catalog);
            _disclosure = result.Projection;
            if (_indicators != null &&
                (_disclosure.RequiresRefresh ||
                 _indicators.AuthorityEpoch != _disclosure.AuthorityEpoch ||
                 _indicators.AuthorityRevision != _disclosure.AuthorityRevision))
            {
                _indicators = null;
            }
            Rebuild();
            Changed?.Invoke();
            return result.Disposition;
        }

        public bool ApplyIndicators(MapIndicatorAuthoritySnapshot incoming)
        {
            if (incoming == null ||
                !_disclosure.HasAuthority ||
                _disclosure.RequiresRefresh ||
                incoming.AuthorityEpoch != _disclosure.AuthorityEpoch ||
                incoming.AuthorityRevision != _disclosure.AuthorityRevision)
            {
                return false;
            }

            _indicators = incoming;
            Rebuild();
            Changed?.Invoke();
            return true;
        }

        public void BeginReconnect()
        {
            _disclosure = MapDisclosureProjection.AwaitingAuthority(_catalog);
            _indicators = null;
            Rebuild();
            Changed?.Invoke();
        }

        public MapDetailLevel SetZoom(float normalizedZoom)
        {
            float zoom = Mathf.Clamp01(normalizedZoom);
            MapDetailLevel next = zoom <= 0.25f
                ? MapDetailLevel.Local
                : zoom < 0.75f ? MapDetailLevel.Regional : MapDetailLevel.Strategic;
            if (_detailLevel != next)
            {
                _detailLevel = next;
                Rebuild();
                Changed?.Invoke();
            }
            return _detailLevel;
        }

        private void Rebuild()
        {
            MapAuthorityPresentationState state = !_disclosure.HasAuthority
                ? MapAuthorityPresentationState.Awaiting
                : _disclosure.RequiresRefresh
                    ? MapAuthorityPresentationState.Correcting
                    : MapAuthorityPresentationState.Authoritative;
            List<MapDisplayItem> minimap = BuildSurface(MapSurfaceKind.Minimap);
            List<MapDisplayItem> worldMap = BuildSurface(MapSurfaceKind.WorldMap);
            Current = new ProgressiveMapSnapshot(
                state,
                _disclosure.RequiresRefresh,
                _disclosure.AuthorityEpoch,
                _disclosure.AuthorityRevision,
                new MapSurfaceProjection(MapSurfaceKind.Minimap, _detailLevel, minimap),
                new MapSurfaceProjection(MapSurfaceKind.WorldMap, _detailLevel, worldMap));
        }

        private List<MapDisplayItem> BuildSurface(MapSurfaceKind surface)
        {
            var items = new List<MapDisplayItem>();
            if (!_disclosure.HasAuthority)
            {
                return items;
            }

            var visibleFeatures = new HashSet<string>(
                _disclosure.VisibleFeatureIds,
                StringComparer.Ordinal);
            foreach (string id in _disclosure.VisibleFeatureIds)
            {
                if (_catalog.TryGetFeature(id, out MapDisclosureFeature feature) &&
                    Supports(feature.Surfaces, surface))
                {
                    items.Add(new MapDisplayItem(
                        feature.Id,
                        MapDisplayItemKind.Feature,
                        feature.SourceId,
                        feature.Id,
                        MapDisplayLabels.FromIdentifier(feature.SourceId),
                        feature.IconReference,
                        feature.NonColorShape,
                        Vector2.zero));
                }
            }

            foreach (string id in _disclosure.VisibleRouteIds)
            {
                if (_catalog.TryGetRoute(id, out MapDisclosureRoute route) &&
                    Supports(route.Surfaces, surface) &&
                    route.FeatureIds.All(visibleFeatures.Contains))
                {
                    items.Add(new MapDisplayItem(
                        route.Id,
                        MapDisplayItemKind.Route,
                        route.Id,
                        route.FeatureIds.FirstOrDefault(),
                        MapDisplayLabels.FromIdentifier(route.Id),
                        string.Empty,
                        route.NonColorShape,
                        Vector2.zero));
                }
            }

            foreach (string id in _disclosure.VisibleObjectiveIds)
            {
                if (_catalog.TryGetObjective(id, out MapDisclosureObjective objective) &&
                    Supports(objective.Surfaces, surface) &&
                    visibleFeatures.Contains(objective.FeatureId))
                {
                    items.Add(new MapDisplayItem(
                        objective.Id,
                        MapDisplayItemKind.Objective,
                        objective.SourceObjectiveId,
                        objective.FeatureId,
                        MapDisplayLabels.FromIdentifier(objective.SourceObjectiveId),
                        objective.IconReference,
                        objective.NonColorShape,
                        Vector2.zero));
                }
            }

            foreach (string id in _disclosure.VisibleAllegianceMarkerIds)
            {
                if (_catalog.TryGetAllegianceMarker(
                        id,
                        out MapDisclosureAllegianceMarker allegiance) &&
                    Supports(allegiance.Surfaces, surface) &&
                    visibleFeatures.Contains(allegiance.FeatureId) &&
                    _catalog.TryGetRealmGlyph(
                        allegiance.RealmGlyphId,
                        out MapDisclosureRealmGlyph glyph))
                {
                    items.Add(new MapDisplayItem(
                        allegiance.Id,
                        MapDisplayItemKind.Allegiance,
                        allegiance.RealmId,
                        allegiance.FeatureId,
                        MapDisplayLabels.FromIdentifier(allegiance.RealmId),
                        glyph.AssetReference,
                        allegiance.NonColorShape,
                        Vector2.zero));
                }
            }

            if (_indicators != null)
            {
                var emittedIds = new HashSet<string>(
                    items.Select(value => value.Id),
                    StringComparer.Ordinal);
                AddIndicator(
                    items,
                    _indicators.Player,
                    MapDisplayItemKind.Player,
                    visibleFeatures,
                    emittedIds);
                for (int i = 0; i < _indicators.Party.Count; i++)
                {
                    AddIndicator(
                        items,
                        _indicators.Party[i],
                        MapDisplayItemKind.Party,
                        visibleFeatures,
                        emittedIds);
                }
            }

            return items;
        }

        private static void AddIndicator(
            ICollection<MapDisplayItem> items,
            MapIndicator indicator,
            MapDisplayItemKind kind,
            ISet<string> visibleFeatures,
            ISet<string> emittedIds)
        {
            if (indicator == null ||
                !MapDisplayLabels.IsSafeIdentifier(indicator.Id) ||
                !MapDisplayLabels.IsSafeIdentifier(indicator.NonColorShape) ||
                !visibleFeatures.Contains(indicator.FeatureId) ||
                !emittedIds.Add(indicator.Id))
            {
                return;
            }

            string label = string.IsNullOrWhiteSpace(indicator.Label)
                ? MapDisplayLabels.FromIdentifier(indicator.Id)
                : indicator.Label.Trim();
            if (label.Length > 48)
            {
                label = label.Substring(0, 48);
            }

            items.Add(new MapDisplayItem(
                indicator.Id,
                kind,
                indicator.Id,
                indicator.FeatureId,
                label,
                string.Empty,
                indicator.NonColorShape,
                indicator.NormalizedPosition));
        }

        private static bool Supports(
            IReadOnlyList<string> surfaces,
            MapSurfaceKind surface)
        {
            string expected = surface == MapSurfaceKind.Minimap ? "minimap" : "world_map";
            return surfaces != null && surfaces.Contains(expected, StringComparer.Ordinal);
        }
    }

    public sealed class MapSurfaceLayout
    {
        internal MapSurfaceLayout(
            Rect minimapRect,
            Rect expandedMinimapRect,
            Rect worldMapRect,
            Rect protectedScanRect)
        {
            MinimapRect = minimapRect;
            ExpandedMinimapRect = expandedMinimapRect;
            WorldMapRect = worldMapRect;
            ProtectedScanRect = protectedScanRect;
        }

        public Rect MinimapRect { get; }
        public Rect ExpandedMinimapRect { get; }
        public Rect WorldMapRect { get; }
        public Rect ProtectedScanRect { get; }
    }

    /// <summary>
    /// Runtime seam shared by the minimap and world map. Networking code publishes
    /// server snapshots here; both surfaces receive the same immutable projection.
    /// </summary>
    public static class ProgressiveMapSession
    {
        private static ProgressiveMapController _controller;
        private static MapAccessibilityProfile _accessibility =
            MapAccessibilityProfile.Default;

        public static event Action Changed;

        public static bool IsConfigured => _controller != null;
        public static ProgressiveMapSnapshot Current => _controller?.Current;
        public static MapAccessibilityProfile Accessibility => _accessibility;
        public static bool MinimapExpanded { get; private set; }
        public static bool CombatDense { get; private set; }

        public static void Configure(MapDisclosureCatalogSnapshot catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            if (_controller != null &&
                string.Equals(
                    _controller.CatalogSha256,
                    catalog.SourceSha256,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (_controller != null)
            {
                _controller.Changed -= ForwardChanged;
            }
            _controller = new ProgressiveMapController(catalog);
            _controller.Changed += ForwardChanged;
            PruneDestroyedSubscribers();
            Changed?.Invoke();
        }

        public static MapDisclosureReconcileDisposition ApplyAuthoritative(
            MapDisclosureAuthoritySnapshot incoming)
        {
            RequireConfigured();
            return _controller.ApplyAuthoritative(incoming);
        }

        public static bool ApplyIndicators(MapIndicatorAuthoritySnapshot incoming)
        {
            RequireConfigured();
            return _controller.ApplyIndicators(incoming);
        }

        public static void BeginReconnect()
        {
            RequireConfigured();
            _controller.BeginReconnect();
        }

        public static MapDetailLevel SetZoom(float normalizedZoom)
        {
            RequireConfigured();
            return _controller.SetZoom(normalizedZoom);
        }

        public static void ConfigureAccessibility(MapAccessibilityProfile profile)
        {
            _accessibility = profile;
            PruneDestroyedSubscribers();
            Changed?.Invoke();
        }

        public static void SetMinimapPresentation(bool expanded, bool combatDense)
        {
            if (MinimapExpanded == expanded && CombatDense == combatDense)
            {
                return;
            }
            MinimapExpanded = expanded;
            CombatDense = combatDense;
            PruneDestroyedSubscribers();
            Changed?.Invoke();
        }

        public static void ResetForTests()
        {
            if (_controller != null)
            {
                _controller.Changed -= ForwardChanged;
            }
            _controller = null;
            _accessibility = MapAccessibilityProfile.Default;
            MinimapExpanded = false;
            CombatDense = false;
            Changed = null;
        }

        private static void ForwardChanged()
        {
            PruneDestroyedSubscribers();
            Changed?.Invoke();
        }

        private static void PruneDestroyedSubscribers()
        {
            if (Changed == null)
            {
                return;
            }
            Delegate[] subscribers = Changed.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                Delegate subscriber = subscribers[i];
                if (subscriber.Target is UnityEngine.Object unityTarget &&
                    unityTarget == null)
                {
                    Changed -= (Action)subscriber;
                }
            }
        }

        private static void RequireConfigured()
        {
            if (_controller == null)
            {
                throw new InvalidOperationException(
                    "The progressive map session has not loaded its catalog.");
            }
        }
    }

    public static class MapInterfaceLayout
    {
        public static MapSurfaceLayout Resolve(
            HudCompositionDefinition composition,
            Rect safeArea,
            bool combatDense)
        {
            if (composition == null)
            {
                throw new ArgumentNullException(nameof(composition));
            }
            if (!composition.TryGetSlot(HudSlotId.Route, out HudSlotDefinition route) ||
                !composition.TryGetSlot(HudSlotId.Objectives, out HudSlotDefinition objectives))
            {
                throw new InvalidOperationException(
                    "The selected HUD composition does not define map route/objective rails.");
            }

            Rect compact = HudLayoutProjection.Project(safeArea, route.NormalizedRect);
            Rect objectiveRect = HudLayoutProjection.Project(safeArea, objectives.NormalizedRect);
            Rect expanded = combatDense ? compact : Union(compact, objectiveRect);
            Rect protectedRect = HudLayoutProjection.Project(
                safeArea,
                composition.ProtectedScanPath);
            if (compact.Overlaps(protectedRect) || expanded.Overlaps(protectedRect))
            {
                throw new InvalidOperationException(
                    "Authored minimap placement enters the protected PvP scan path.");
            }

            return new MapSurfaceLayout(
                compact,
                expanded,
                safeArea,
                protectedRect);
        }

        private static Rect Union(Rect left, Rect right)
        {
            return Rect.MinMaxRect(
                Mathf.Min(left.xMin, right.xMin),
                Mathf.Min(left.yMin, right.yMin),
                Mathf.Max(left.xMax, right.xMax),
                Mathf.Max(left.yMax, right.yMax));
        }
    }
}
