using System;
using System.IO;
using AL.Core;
using AL.Data.Catalogs.WorldAtlas;
using UnityEngine;

namespace AL.World
{
    /// <summary>
    /// First-session spawn contract: Boot → create → 3D lands the champion
    /// inside the chosen realm's inner safe zone, at that realm's unnamed Capital.
    /// Reuses World Atlas IDs. The continent is still the TEMPORARY greybox from
    /// InnerRealmWorld — colored city names are not invented.
    /// Never Warzone / outer belt / Accordant Isle / Kingdom.
    /// </summary>
    public sealed class FirstSessionInnerRealmSpawn
    {
        public const string DiagnosticPrefix = "[AL-FIRST-SESSION-SPAWN]";
        public const string KingdomSceneName = "Kingdom";
        public const string AccordantIsleZoneId = "zone_accordant_isle";
        public const string WarzoneCenterId = "warzone_center_unplayable";

        public const string StoneholdZoneId = "zone_inner_stonehold";
        public const string EldergroveZoneId = "zone_inner_eldergrove";
        public const string CrownlandsZoneId = "zone_inner_crownlands";
        public const string UmbralZoneId = "zone_inner_umbral";

        private FirstSessionInnerRealmSpawn(
            string realmId,
            string innerAtlasZoneId,
            string capitalPoiId,
            string innerWallId,
            string mainGateId,
            Vector3 position,
            Vector3 opponentPosition,
            Vector3 cameraPosition,
            string placementStatus,
            string coloredMapNote)
        {
            RealmId = realmId;
            InnerAtlasZoneId = innerAtlasZoneId;
            CapitalPoiId = capitalPoiId;
            InnerWallId = innerWallId;
            MainGateId = mainGateId;
            Position = position;
            OpponentPosition = opponentPosition;
            CameraPosition = cameraPosition;
            PlacementStatus = placementStatus;
            ColoredMapNote = coloredMapNote;
        }

        public string RealmId { get; }
        public string InnerAtlasZoneId { get; }
        public string CapitalPoiId { get; }
        public string InnerWallId { get; }
        public string MainGateId { get; }
        public Vector3 Position { get; }
        public Vector3 OpponentPosition { get; }
        public Vector3 CameraPosition { get; }
        public string PlacementStatus { get; }
        public string ColoredMapNote { get; }
        public string TemporaryLabel => InnerRealmWorldIds.TemporaryLabel;
        public string DisplayCapital => InnerRealmWorldIds.DisplayCapital();

        public string ReportLine
        {
            get
            {
                return DiagnosticPrefix +
                    " realm=" + RealmId +
                    " zone=" + InnerAtlasZoneId +
                    " poi=" + CapitalPoiId +
                    " wall=" + InnerWallId +
                    " gate=" + MainGateId +
                    " label=" + TemporaryLabel +
                    " placement=" + PlacementStatus;
            }
        }

        public static WorldAtlasSnapshot LoadCanonicalSnapshot()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json"));
            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(bytes);
            if (!result.IsAccepted)
            {
                throw new InvalidOperationException("World atlas rejected: " + result.Status);
            }

            return result.Snapshot;
        }

        public static FirstSessionInnerRealmSpawn Resolve(RealmId realm)
        {
            return Resolve(InnerRealmWorldLayout.RealmCatalogId(realm), LoadCanonicalSnapshot());
        }

        public static FirstSessionInnerRealmSpawn Resolve(string realmId)
        {
            return Resolve(realmId, LoadCanonicalSnapshot());
        }

        public static FirstSessionInnerRealmSpawn Resolve(string realmId, WorldAtlasSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (string.IsNullOrEmpty(realmId))
            {
                throw new InvalidOperationException("First-session spawn requires a committed realm.");
            }

            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(snapshot);
            if (!layout.TryGetInner(realmId, out InnerRealmSlotLayout inner))
            {
                throw new InvalidOperationException("No inner-realm slot for " + realmId);
            }

            return FromInner(inner, layout);
        }

        public static FirstSessionInnerRealmSpawn[] ResolveAllFour(WorldAtlasSnapshot snapshot)
        {
            return new[]
            {
                Resolve("stonehold", snapshot),
                Resolve("eldergrove", snapshot),
                Resolve("crownlands", snapshot),
                Resolve("umbral", snapshot)
            };
        }

        public static bool IsForbiddenDestination(string sceneName, string zoneId)
        {
            if (string.Equals(sceneName, KingdomSceneName, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.IsNullOrEmpty(zoneId))
            {
                return true;
            }

            if (string.Equals(zoneId, AccordantIsleZoneId, StringComparison.Ordinal))
            {
                return true;
            }

            if (zoneId.IndexOf("warzone", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (zoneId.IndexOf("outer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return !zoneId.StartsWith("zone_inner_", StringComparison.Ordinal);
        }

        public bool IsInsideInnerSafe(InnerRealmSlotLayout inner)
        {
            if (inner == null)
            {
                return false;
            }

            return inner.InnerSafe.Contains(new Vector3(Position.x, 0f, Position.z)) &&
                inner.InnerSafe.Contains(new Vector3(OpponentPosition.x, 0f, OpponentPosition.z));
        }

        private static FirstSessionInnerRealmSpawn FromInner(
            InnerRealmSlotLayout inner,
            InnerRealmWorldLayout layout)
        {
            Vector3 spawn = inner.WalkableSpawn;
            if (!inner.InnerSafe.Contains(new Vector3(spawn.x, 0f, spawn.z)))
            {
                throw new InvalidOperationException("Spawn left inner safe for " + inner.RealmId);
            }

            Vector3 towardCenter = inner.InnerSafe.Center - inner.CapitalPosition;
            towardCenter.y = 0f;
            if (towardCenter.sqrMagnitude < 0.01f)
            {
                towardCenter = inner.GatePosition - inner.CapitalPosition;
                towardCenter.y = 0f;
            }

            towardCenter.Normalize();
            Vector3 opponent = inner.CapitalPosition + towardCenter * 6.2f + Vector3.up * 1.8f;
            if (!inner.InnerSafe.Contains(new Vector3(opponent.x, 0f, opponent.z)))
            {
                opponent = inner.InnerSafe.Center + Vector3.up * 1.8f;
            }

            Vector3 back = -towardCenter;
            Vector3 camera = spawn + new Vector3(back.x * 8.6f, 7.2f, back.z * 8.6f);

            return new FirstSessionInnerRealmSpawn(
                inner.RealmId,
                inner.InnerAtlasZoneId,
                inner.CapitalPoiId,
                inner.InnerWallId,
                inner.MainGateId,
                spawn,
                opponent,
                camera,
                layout.PlacementStatus,
                layout.ColoredMapNote);
        }
    }
}
