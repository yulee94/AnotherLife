using System;
using System.Collections.Generic;
using System.IO;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Data.Catalogs.WorldAtlas;
using AL.Services.Local;
using AL.World;
using UnityEngine;

namespace AL.UI.WorldMap
{
    [Serializable]
    internal sealed class MainQuestMapMarkerCatalogFile
    {
        public string version;
        public string catalogId;
        public MainQuestMapMarkerSourceAuthorities sourceAuthorities;
        public MainQuestMapMarkerRecord[] markers;
    }

    [Serializable]
    internal sealed class MainQuestMapMarkerSourceAuthorities
    {
        public string worldAtlasCatalog;
        public string questPreviewCatalog;
        public string mainQuestRuntime;
    }

    [Serializable]
    public sealed class MainQuestMapMarkerRecord
    {
        public string objectiveId;
        public string atlasPoiRole;
    }

    public sealed class MainQuestMapMarkerCatalog
    {
        public const string FileName = "al_main_quest_map_marker_catalog.json";
        public const string Version = "1.0.0";
        public const string CatalogId = "al_main_quest_map_marker_catalog";
        internal const string QuestPreviewCatalogId = "al_quest_preview_content_catalog";
        internal const string MainQuestRuntimeId = "OMEN_1_then_MQ_C1_PROOF_OF_WORTH";

        private static readonly string[] RequiredObjectiveIds =
        {
            ProofOfWorthIds.OmenTalkObjectiveId,
            ProofOfWorthIds.OmenArenaObjectiveId,
            ProofOfWorthIds.OmenReportObjectiveId,
            ProofOfWorthIds.MeetGuideObjectiveId,
            ProofOfWorthIds.RestoreCovenantObjectiveId,
            ProofOfWorthIds.FaceGuardianObjectiveId,
            ProofOfWorthIds.AcceptMarkObjectiveId
        };

        private readonly Dictionary<string, MainQuestMapMarkerRecord> _byObjective;
        private readonly IReadOnlyList<string> _objectiveIds;

        private MainQuestMapMarkerCatalog(Dictionary<string, MainQuestMapMarkerRecord> byObjective)
        {
            _byObjective = byObjective;
            var ids = new List<string>(byObjective.Keys);
            ids.Sort(StringComparer.Ordinal);
            _objectiveIds = ids.AsReadOnly();
        }

        public IReadOnlyList<string> ObjectiveIds => _objectiveIds;

        public static MainQuestMapMarkerCatalog LoadCanonical()
        {
            string path = ResolvePath();
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Main-quest map marker catalog is missing.", path);
            }

            return Parse(File.ReadAllText(path), path);
        }

        internal static MainQuestMapMarkerCatalog Parse(string payload, string sourceName)
        {
            MainQuestMapMarkerCatalogFile file;
            try
            {
                file = JsonUtility.FromJson<MainQuestMapMarkerCatalogFile>(payload);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Main-quest map marker catalog JSON is invalid: " + sourceName,
                    exception);
            }

            if (file == null ||
                !string.Equals(file.version, Version, StringComparison.Ordinal) ||
                !string.Equals(file.catalogId, CatalogId, StringComparison.Ordinal) ||
                file.sourceAuthorities == null ||
                !string.Equals(
                    file.sourceAuthorities.worldAtlasCatalog,
                    WorldAtlasContract.CatalogId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    file.sourceAuthorities.questPreviewCatalog,
                    QuestPreviewCatalogId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    file.sourceAuthorities.mainQuestRuntime,
                    MainQuestRuntimeId,
                    StringComparison.Ordinal) ||
                file.markers == null ||
                file.markers.Length != RequiredObjectiveIds.Length)
            {
                throw new InvalidDataException("Main-quest map marker catalog is invalid.");
            }

            var byObjective = new Dictionary<string, MainQuestMapMarkerRecord>(StringComparer.Ordinal);
            for (int i = 0; i < file.markers.Length; i++)
            {
                MainQuestMapMarkerRecord marker = file.markers[i];
                if (marker == null ||
                    string.IsNullOrWhiteSpace(marker.objectiveId) ||
                    !IsSupportedRole(marker.atlasPoiRole) ||
                    byObjective.ContainsKey(marker.objectiveId))
                {
                    throw new InvalidDataException("Main-quest map marker catalog contains an invalid marker row.");
                }

                byObjective.Add(marker.objectiveId, marker);
            }

            for (int i = 0; i < RequiredObjectiveIds.Length; i++)
            {
                if (!byObjective.ContainsKey(RequiredObjectiveIds[i]))
                {
                    throw new InvalidDataException(
                        "Main-quest map marker catalog does not match the active Proof-of-Worth objective set.");
                }
            }

            return new MainQuestMapMarkerCatalog(byObjective);
        }

        public bool TryGet(string objectiveId, out MainQuestMapMarkerRecord marker)
        {
            if (!string.IsNullOrEmpty(objectiveId))
            {
                return _byObjective.TryGetValue(objectiveId, out marker);
            }

            marker = null;
            return false;
        }

        private static bool IsSupportedRole(string role)
        {
            return string.Equals(role, MainQuestMapMarkerResolver.RoleCapital, StringComparison.Ordinal) ||
                   string.Equals(role, MainQuestMapMarkerResolver.RoleAreaA, StringComparison.Ordinal) ||
                   string.Equals(role, MainQuestMapMarkerResolver.RoleAreaB, StringComparison.Ordinal);
        }

        private static string ResolvePath()
        {
            if (SixFamilyRuntimeCatalog.TryResolveGameDataDirectory(out string directory))
            {
                return Path.Combine(directory, FileName);
            }

            return ResolveFallbackPath(
                Application.dataPath,
                Application.streamingAssetsPath,
                Application.isEditor);
        }

        internal static string ResolveFallbackPath(
            string dataPath,
            string streamingAssetsPath,
            bool isEditor)
        {
            if (isEditor)
            {
                return Path.Combine(dataPath, "AL", "StreamingAssets", "GameData", FileName);
            }

            return Path.Combine(
                (streamingAssetsPath ?? string.Empty).TrimEnd('/', '\\'),
                "GameData",
                FileName);
        }
    }

    public sealed class MainQuestMapMarker
    {
        internal MainQuestMapMarker(
            string objectiveId,
            string markerId,
            string zoneId,
            string whatToDo,
            string displayName,
            Vector3 worldPosition,
            WorldMapUv fullMapUv,
            WorldMapUv minimapUv)
        {
            ObjectiveId = objectiveId;
            MarkerId = markerId;
            ZoneId = zoneId;
            WhatToDo = whatToDo ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            WorldPosition = worldPosition;
            FullMapUv = fullMapUv;
            MinimapUv = minimapUv;
        }

        public string ObjectiveId { get; }
        public string MarkerId { get; }
        public string ZoneId { get; }
        public string WhatToDo { get; }
        public string DisplayName { get; }
        public Vector3 WorldPosition { get; }
        public WorldMapUv FullMapUv { get; }
        public WorldMapUv MinimapUv { get; }
        public bool IsInnerRealm =>
            !string.IsNullOrEmpty(ZoneId) &&
            ZoneId.StartsWith("zone_inner_", StringComparison.Ordinal) &&
            !KingdomWorldMapQuery.IsForbiddenId(MarkerId);
    }

    public static class MainQuestMapMarkerResolver
    {
        public const string RoleCapital = "capital";
        public const string RoleAreaA = "area_a";
        public const string RoleAreaB = "area_b";

        public static IReadOnlyList<MainQuestMapMarker> ResolveCurrent(
            WorldAtlasSnapshot snapshot,
            MainQuestMapMarkerCatalog catalog,
            string objectiveId,
            RealmId realm,
            string whatToDo)
        {
            if (snapshot == null ||
                catalog == null ||
                !catalog.TryGet(objectiveId, out MainQuestMapMarkerRecord record))
            {
                return Array.Empty<MainQuestMapMarker>();
            }

            string realmId = InnerRealmWorldLayout.RealmCatalogId(realm);
            if (string.IsNullOrEmpty(realmId))
            {
                return Array.Empty<MainQuestMapMarker>();
            }

            InnerRealmWorldLayout worldLayout = InnerRealmWorldLayout.FromSnapshot(snapshot);
            if (!worldLayout.TryGetInner(realmId, out InnerRealmSlotLayout inner))
            {
                return Array.Empty<MainQuestMapMarker>();
            }

            WorldMapPresentation worldMap = WorldMapPresentation.FromSnapshot(snapshot);
            WorldMapInnerRealm mapInner = FindMapInner(worldMap, realmId);
            if (mapInner == null || !TryResolvePoi(record.atlasPoiRole, inner, mapInner, out ResolvedPoi poi))
            {
                return Array.Empty<MainQuestMapMarker>();
            }

            if (KingdomWorldMapQuery.IsForbiddenId(poi.MarkerId) || !inner.InnerSafe.Contains(poi.WorldPosition))
            {
                return Array.Empty<MainQuestMapMarker>();
            }

            var marker = new MainQuestMapMarker(
                objectiveId,
                poi.MarkerId,
                inner.InnerAtlasZoneId,
                whatToDo,
                poi.DisplayName,
                poi.WorldPosition,
                poi.FullMapUv,
                ProjectToInnerMap(inner, poi.WorldPosition));
            return new[] { marker };
        }

        private static WorldMapInnerRealm FindMapInner(WorldMapPresentation presentation, string realmId)
        {
            for (int i = 0; i < presentation.Inners.Count; i++)
            {
                if (string.Equals(presentation.Inners[i].RealmId, realmId, StringComparison.Ordinal))
                {
                    return presentation.Inners[i];
                }
            }

            return null;
        }

        private static bool TryResolvePoi(
            string role,
            InnerRealmSlotLayout inner,
            WorldMapInnerRealm mapInner,
            out ResolvedPoi poi)
        {
            if (string.Equals(role, RoleCapital, StringComparison.Ordinal))
            {
                poi = new ResolvedPoi(
                    inner.CapitalPoiId,
                    InnerRealmWorldIds.DisplayCapital(),
                    inner.CapitalPosition,
                    mapInner.Capital.Uv);
                return true;
            }

            if (string.Equals(role, RoleAreaA, StringComparison.Ordinal))
            {
                poi = new ResolvedPoi(
                    inner.OutpostAPoiId,
                    "Area I",
                    inner.OutpostAPosition,
                    mapInner.OutpostA.Uv);
                return true;
            }

            if (string.Equals(role, RoleAreaB, StringComparison.Ordinal))
            {
                poi = new ResolvedPoi(
                    inner.OutpostBPoiId,
                    "Area II",
                    inner.OutpostBPosition,
                    mapInner.OutpostB.Uv);
                return true;
            }

            poi = default;
            return false;
        }

        internal static WorldMapUv ProjectToInnerMap(InnerRealmSlotLayout inner, Vector3 worldPosition)
        {
            float diameter = inner.InnerSafe.HalfExtent * 2f;
            return new WorldMapUv(
                Mathf.Clamp01((worldPosition.x - inner.InnerSafe.MinX) / diameter),
                Mathf.Clamp01((worldPosition.z - inner.InnerSafe.MinZ) / diameter));
        }

        private readonly struct ResolvedPoi
        {
            internal ResolvedPoi(string markerId, string displayName, Vector3 worldPosition, WorldMapUv fullMapUv)
            {
                MarkerId = markerId;
                DisplayName = displayName;
                WorldPosition = worldPosition;
                FullMapUv = fullMapUv;
            }

            internal string MarkerId { get; }
            internal string DisplayName { get; }
            internal Vector3 WorldPosition { get; }
            internal WorldMapUv FullMapUv { get; }
        }
    }
}
