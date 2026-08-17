using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Core.Relationships
{
    public enum RelationshipValueQueryStatus
    {
        Found,
        MissingValue,
        UnknownIdentity,
        MalformedSnapshot,
        StalePolicy
    }

    public sealed class RelationshipValueQuery
    {
        internal RelationshipValueQuery(
            RelationshipValueQueryStatus status,
            string canonicalId,
            double value,
            string snapshotRevision)
        {
            Status = status;
            CanonicalId = canonicalId ?? string.Empty;
            Value = value;
            SnapshotRevision = snapshotRevision ?? string.Empty;
        }

        public RelationshipValueQueryStatus Status { get; }
        public string CanonicalId { get; }
        public double Value { get; }
        public string SnapshotRevision { get; }
    }

    /// <summary>
    /// One operation-scoped, immutable view of all relationship domains. Consumers capture
    /// once, then query only this object so later mutations cannot mix generations.
    /// </summary>
    public sealed class RelationshipConsumerSnapshot
    {
        private RelationshipConsumerSnapshot(
            RelationshipNumericSnapshot affinity,
            RelationshipNumericSnapshot faction,
            RelationshipPersonaSnapshot persona)
        {
            Affinity = affinity;
            Faction = faction;
            Persona = persona;
            CaptureRevision = RelationshipSnapshotBuilder.Fingerprint(
                "relationship-consumer-v1",
                new[]
                {
                    affinity.PolicyRevision,
                    affinity.SnapshotRevision,
                    faction.SnapshotRevision,
                    persona.SnapshotRevision
                });
        }

        public RelationshipNumericSnapshot Affinity { get; }
        public RelationshipNumericSnapshot Faction { get; }
        public RelationshipPersonaSnapshot Persona { get; }
        public string CaptureRevision { get; }

        public static RelationshipConsumerSnapshot Capture(
            IRelationshipIdentityResolver resolver,
            SaveGameData save,
            bool profileWritable)
        {
            RelationshipNumericRow[] affinityRows =
                (save?.Reputation ?? new List<NpcAffinityData>())
                .Select(row => row == null
                    ? null
                    : new RelationshipNumericRow(row.NpcId, row.Affinity))
                .ToArray();
            RelationshipNumericRow[] factionRows =
                (save?.FactionReputations ?? new List<FactionRepData>())
                .Select(row => row == null
                    ? null
                    : new RelationshipNumericRow(row.FactionId, row.Reputation))
                .ToArray();
            PersonaData persona = save?.LordPersona;
            int warlord = persona?.Warlord ?? 0;
            int diplomat = persona?.Diplomat ?? 0;
            int sage = persona?.Sage ?? 0;
            int rogue = persona?.Rogue ?? 0;

            return new RelationshipConsumerSnapshot(
                RelationshipSnapshotBuilder.BuildNpcAffinity(
                    resolver, affinityRows, profileWritable),
                RelationshipSnapshotBuilder.BuildFactionReputation(
                    resolver, factionRows, profileWritable),
                RelationshipSnapshotBuilder.BuildPersona(
                    warlord,
                    diplomat,
                    sage,
                    rogue,
                    profileWritable));
        }

        public RelationshipValueQuery QueryAffinity(
            IRelationshipIdentityResolver resolver,
            string npcId)
        {
            return Query(resolver, Affinity, npcId, true);
        }

        public RelationshipValueQuery QueryFaction(
            IRelationshipIdentityResolver resolver,
            string factionId)
        {
            return Query(resolver, Faction, factionId, false);
        }

        private static RelationshipValueQuery Query(
            IRelationshipIdentityResolver resolver,
            RelationshipNumericSnapshot snapshot,
            string requestedId,
            bool npc)
        {
            if (resolver == null ||
                !string.Equals(resolver.PolicyRevision, snapshot.PolicyRevision, StringComparison.Ordinal))
            {
                return Result(RelationshipValueQueryStatus.StalePolicy, requestedId, 0, snapshot);
            }
            if (snapshot.Status != RelationshipSnapshotStatus.Valid &&
                snapshot.Status != RelationshipSnapshotStatus.ValidSparse &&
                snapshot.Status != RelationshipSnapshotStatus.ValidWithPreservedUnknown)
            {
                return Result(RelationshipValueQueryStatus.MalformedSnapshot, requestedId, 0, snapshot);
            }

            RelationshipIdentityResolution resolution = npc
                ? resolver.ResolveNpc(requestedId)
                : resolver.ResolveFaction(requestedId);
            if (resolution.Status != RelationshipResolutionStatus.Found &&
                resolution.Status != RelationshipResolutionStatus.AliasResolved)
            {
                return Result(RelationshipValueQueryStatus.UnknownIdentity, requestedId, 0, snapshot);
            }

            double value;
            return snapshot.Values.TryGetValue(resolution.Identity.CanonicalId, out value)
                ? Result(RelationshipValueQueryStatus.Found, resolution.Identity.CanonicalId, value, snapshot)
                : Result(RelationshipValueQueryStatus.MissingValue, resolution.Identity.CanonicalId, 0, snapshot);
        }

        private static RelationshipValueQuery Result(
            RelationshipValueQueryStatus status,
            string id,
            double value,
            RelationshipNumericSnapshot snapshot)
        {
            return new RelationshipValueQuery(status, id, value, snapshot?.SnapshotRevision);
        }
    }

    public static class RelationshipProductionResolver
    {
        private const string CatalogFileName =
            "al_relationship_authority_content_catalog.json";
        private static readonly object Sync = new object();
        private static IRelationshipIdentityResolver cached;

        public static IRelationshipIdentityResolver Current
        {
            get
            {
                lock (Sync)
                {
                    if (cached != null) return cached;
                    cached = Load();
                    return cached;
                }
            }
        }

        private static IRelationshipIdentityResolver Load()
        {
            string editorPath = Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData",
                CatalogFileName);
            string playerPath = Path.Combine(
                Application.streamingAssetsPath,
                "GameData",
                CatalogFileName);
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                byte[] bytes;
                using (var reader = new AndroidJavaClass(
                           "com.anotherlife.relationship.RelationshipCatalogReader"))
                {
                    bytes = reader.CallStatic<byte[]>("readApprovedCatalog", CatalogFileName);
                }

                return new RelationshipCatalogResolver(bytes);
#else
                string path = File.Exists(editorPath) ? editorPath : playerPath;
                return File.Exists(path)
                    ? LoadFromFile(path)
                    : new RelationshipCatalogResolver(null);
#endif
            }
            catch (IOException)
            {
                return new RelationshipCatalogResolver(null);
            }
            catch (UnauthorizedAccessException)
            {
                return new RelationshipCatalogResolver(null);
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            catch (AndroidJavaException)
            {
                return new RelationshipCatalogResolver(null);
            }
#endif
        }

        private static IRelationshipIdentityResolver LoadFromFile(string path)
        {
            var bytes = new byte[RelationshipCatalogResolver.MaximumSourceBytes + 1];
            int total = 0;
            using (var stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       bufferSize: 4096,
                       useAsync: false))
            {
                while (total < bytes.Length)
                {
                    int read = stream.Read(bytes, total, bytes.Length - total);
                    if (read == 0) break;
                    total += read;
                }
            }

            if (total > RelationshipCatalogResolver.MaximumSourceBytes)
            {
                return new RelationshipCatalogResolver(null);
            }

            Array.Resize(ref bytes, total);
            return new RelationshipCatalogResolver(bytes);
        }
    }
}
