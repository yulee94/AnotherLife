using System;
using System.Collections.Generic;
using System.Linq;
using AL.Data.Catalogs.WorldStreaming;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.World.Streaming
{
    public enum WorldChunkGroundSourceKind
    {
        Unspecified,
        TerrainHeightfield,
        DedicatedCollisionMesh,
        SolidColliderAssembly,
        ReviewedCaveOrModularCollision
    }

    public enum WorldChunkEdge
    {
        North,
        East,
        South,
        West
    }

    public enum WorldChunkEdgeSafetyMode
    {
        Unspecified,
        ContinuousNeighbor,
        SolidBoundary,
        ReviewedPortal
    }

    [Serializable]
    public sealed class WorldChunkEdgeSafetyBinding
    {
        [SerializeField] private WorldChunkEdge edge;
        [SerializeField] private WorldChunkEdgeSafetyMode safetyMode;
        [SerializeField] private string connectedChunkId = string.Empty;
        [SerializeField] private Collider safetyCollider;
        [SerializeField] private string reviewReceiptId = string.Empty;

        public WorldChunkEdgeSafetyBinding()
        {
        }

        public WorldChunkEdgeSafetyBinding(
            WorldChunkEdge edge,
            WorldChunkEdgeSafetyMode safetyMode,
            string connectedChunkId,
            Collider safetyCollider,
            string reviewReceiptId = null)
        {
            this.edge = edge;
            this.safetyMode = safetyMode;
            this.connectedChunkId = connectedChunkId ?? string.Empty;
            this.safetyCollider = safetyCollider;
            this.reviewReceiptId = reviewReceiptId ?? string.Empty;
        }

        public WorldChunkEdge Edge => edge;
        public WorldChunkEdgeSafetyMode SafetyMode => safetyMode;
        public string ConnectedChunkId => connectedChunkId;
        public Collider SafetyCollider => safetyCollider;
        public string ReviewReceiptId => reviewReceiptId;
    }

    [DisallowMultipleComponent]
    public sealed class WorldChunkPhysicalGroundAuthority : MonoBehaviour
    {
        [SerializeField] private WorldChunkGroundSourceKind sourceKind;
        [SerializeField] private string reviewReceiptId = string.Empty;
        [SerializeField] private Collider[] groundColliders = new Collider[0];
        [SerializeField] private WorldChunkEdgeSafetyBinding[] edgeSafety =
            new WorldChunkEdgeSafetyBinding[0];

        public WorldChunkGroundSourceKind SourceKind => sourceKind;
        public string ReviewReceiptId => reviewReceiptId;
        public IReadOnlyList<Collider> GroundColliders =>
            groundColliders ?? Array.Empty<Collider>();
        public IReadOnlyList<WorldChunkEdgeSafetyBinding> EdgeSafety =>
            edgeSafety ?? Array.Empty<WorldChunkEdgeSafetyBinding>();

        public void Configure(
            WorldChunkGroundSourceKind configuredSourceKind,
            string configuredReviewReceiptId,
            IEnumerable<Collider> configuredGroundColliders,
            IEnumerable<WorldChunkEdgeSafetyBinding> configuredEdgeSafety)
        {
            sourceKind = configuredSourceKind;
            reviewReceiptId = configuredReviewReceiptId ?? string.Empty;
            groundColliders = (configuredGroundColliders ??
                    Array.Empty<Collider>())
                .ToArray();
            edgeSafety = (configuredEdgeSafety ??
                    Array.Empty<WorldChunkEdgeSafetyBinding>())
                .ToArray();
        }
    }

    public sealed class WorldChunkPhysicalGroundDiagnostic
    {
        public WorldChunkPhysicalGroundDiagnostic(
            string code,
            string relatedObject,
            string message)
        {
            Code = code ?? string.Empty;
            RelatedObject = relatedObject ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string RelatedObject { get; }
        public string Message { get; }
        public string Fingerprint => string.Join(
            "|",
            Code,
            RelatedObject,
            Message);
    }

    public sealed class WorldChunkPhysicalGroundReadiness
    {
        internal WorldChunkPhysicalGroundReadiness(
            IEnumerable<WorldChunkPhysicalGroundDiagnostic> diagnostics)
        {
            Diagnostics = Array.AsReadOnly((diagnostics ??
                    Array.Empty<WorldChunkPhysicalGroundDiagnostic>())
                .Where(value => value != null)
                .GroupBy(value => value.Fingerprint, StringComparer.Ordinal)
                .Select(value => value.First())
                .OrderBy(value => value.Code, StringComparer.Ordinal)
                .ThenBy(value => value.RelatedObject, StringComparer.Ordinal)
                .ThenBy(value => value.Message, StringComparer.Ordinal)
                .ToArray());
        }

        public IReadOnlyList<WorldChunkPhysicalGroundDiagnostic> Diagnostics { get; }
        public bool IsReady => Diagnostics.Count == 0;
    }

    public static class WorldChunkPhysicalGroundValidator
    {
        private const float EdgeContactToleranceMeters = 0.05f;

        public static WorldChunkPhysicalGroundReadiness Evaluate(
            Scene scene,
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (chunk == null)
            {
                throw new ArgumentNullException(nameof(chunk));
            }

            WorldChunkRoot[] roots = !scene.IsValid() || !scene.isLoaded
                ? Array.Empty<WorldChunkRoot>()
                : scene.GetRootGameObjects()
                    .SelectMany(value =>
                        value.GetComponentsInChildren<WorldChunkRoot>(true))
                    .Where(value => string.Equals(
                        value.ChunkId,
                        chunk.Id,
                        StringComparison.Ordinal))
                    .ToArray();
            return Evaluate(
                scene,
                snapshot,
                chunk,
                roots.Length == 1 ? roots[0] : null);
        }

        internal static WorldChunkPhysicalGroundReadiness Evaluate(
            Scene scene,
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk,
            WorldChunkRoot chunkRoot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (chunk == null)
            {
                throw new ArgumentNullException(nameof(chunk));
            }

            var diagnostics = new List<WorldChunkPhysicalGroundDiagnostic>();
            if (!scene.IsValid() || !scene.isLoaded || chunkRoot == null)
            {
                Add(
                    diagnostics,
                    WorldChunkLoadFailureCodes.PhysicalGroundAuthorityMissing,
                    chunk.Id,
                    "A loaded catalog chunk root is required for physical-ground validation.");
                return new WorldChunkPhysicalGroundReadiness(diagnostics);
            }

            WorldChunkPhysicalGroundAuthority[] authorities = chunkRoot
                .GetComponentsInChildren<WorldChunkPhysicalGroundAuthority>(true);
            if (authorities.Length != 1)
            {
                Add(
                    diagnostics,
                    WorldChunkLoadFailureCodes.PhysicalGroundAuthorityMissing,
                    chunk.Id,
                    "The chunk must contain exactly one WorldChunkPhysicalGroundAuthority.");
                return new WorldChunkPhysicalGroundReadiness(diagnostics);
            }

            WorldChunkPhysicalGroundAuthority authority = authorities[0];
            if (!authority.isActiveAndEnabled)
            {
                Add(
                    diagnostics,
                    WorldChunkLoadFailureCodes.PhysicalGroundAuthorityMissing,
                    authority.name,
                    "The physical-ground authority must be active and enabled.");
            }
            if (authority.SourceKind == WorldChunkGroundSourceKind.Unspecified)
            {
                Add(
                    diagnostics,
                    WorldChunkLoadFailureCodes.GroundColliderInvalid,
                    authority.name,
                    "The physical-ground source kind is unspecified.");
            }
            if (authority.SourceKind != WorldChunkGroundSourceKind.TerrainHeightfield &&
                string.IsNullOrWhiteSpace(authority.ReviewReceiptId))
            {
                Add(
                    diagnostics,
                    WorldChunkLoadFailureCodes.GroundReviewMissing,
                    authority.name,
                    "Non-Terrain ground requires an explicit collision-review receipt ID.");
            }

            HashSet<Mesh> renderedMeshes = RenderedMeshes(chunkRoot);
            Collider[] groundColliders = authority.GroundColliders.ToArray();
            if (groundColliders.Length == 0)
            {
                Add(
                    diagnostics,
                    WorldChunkLoadFailureCodes.GroundColliderMissing,
                    authority.name,
                    "No physical ground colliders are bound to the chunk authority.");
            }
            foreach (Collider collider in groundColliders)
            {
                ValidateCollider(
                    collider,
                    authority.SourceKind,
                    renderedMeshes,
                    diagnostics,
                    chunkRoot.transform);
            }

            ValidateEdges(
                snapshot,
                chunk,
                chunkRoot,
                authority,
                groundColliders,
                renderedMeshes,
                diagnostics);
            return new WorldChunkPhysicalGroundReadiness(diagnostics);
        }

        private static void ValidateEdges(
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk,
            WorldChunkRoot chunkRoot,
            WorldChunkPhysicalGroundAuthority authority,
            Collider[] groundColliders,
            HashSet<Mesh> renderedMeshes,
            ICollection<WorldChunkPhysicalGroundDiagnostic> diagnostics)
        {
            WorldChunkEdgeSafetyBinding[] edges = authority.EdgeSafety
                .Where(value => value != null)
                .ToArray();
            foreach (WorldChunkEdge expected in Enum.GetValues(
                         typeof(WorldChunkEdge)))
            {
                WorldChunkEdgeSafetyBinding[] matches = edges
                    .Where(value => value.Edge == expected)
                    .ToArray();
                if (matches.Length != 1)
                {
                    Add(
                        diagnostics,
                        WorldChunkLoadFailureCodes.ChunkEdgeUnsafe,
                        expected.ToString(),
                        "Every chunk edge requires exactly one explicit safety binding.");
                    continue;
                }

                WorldChunkEdgeSafetyBinding binding = matches[0];
                if (binding.SafetyMode == WorldChunkEdgeSafetyMode.Unspecified)
                {
                    Add(
                        diagnostics,
                        WorldChunkLoadFailureCodes.ChunkEdgeUnsafe,
                        expected.ToString(),
                        "The chunk edge safety mode is unspecified.");
                    continue;
                }

                ValidateCollider(
                    binding.SafetyCollider,
                    WorldChunkGroundSourceKind.ReviewedCaveOrModularCollision,
                    renderedMeshes,
                    diagnostics,
                    chunkRoot.transform,
                    WorldChunkLoadFailureCodes.ChunkEdgeUnsafe);
                if (binding.SafetyCollider == null)
                {
                    continue;
                }

                if (binding.SafetyMode == WorldChunkEdgeSafetyMode.ContinuousNeighbor)
                {
                    if (string.IsNullOrWhiteSpace(binding.ReviewReceiptId))
                    {
                        Add(
                            diagnostics,
                            WorldChunkLoadFailureCodes.ChunkSeamContinuityUnproven,
                            binding.Edge.ToString(),
                            "Local collider coverage cannot prove a crack-free neighbor seam; a cross-scene sampled continuity-review receipt is required.");
                    }
                    if (!groundColliders.Contains(binding.SafetyCollider) ||
                        !IsCardinalCatalogNeighbor(
                            snapshot,
                            chunk,
                            binding.ConnectedChunkId,
                            binding.Edge) ||
                        !ReachesCatalogEdge(
                            binding.SafetyCollider.bounds,
                            chunkRoot,
                            binding.Edge))
                    {
                        Add(
                            diagnostics,
                            WorldChunkLoadFailureCodes.ChunkEdgeUnsafe,
                            binding.Edge.ToString(),
                            "A continuous edge must bind catalog-neighbor ground that reaches the authored chunk envelope.");
                    }
                }
                else if (binding.SafetyMode == WorldChunkEdgeSafetyMode.SolidBoundary)
                {
                    if (!string.IsNullOrWhiteSpace(binding.ConnectedChunkId) ||
                        !ReachesCatalogEdge(
                            binding.SafetyCollider.bounds,
                            chunkRoot,
                            binding.Edge))
                    {
                        Add(
                            diagnostics,
                            WorldChunkLoadFailureCodes.ChunkEdgeUnsafe,
                            binding.Edge.ToString(),
                            "A solid boundary must have no neighbor binding and must reach the authored chunk envelope.");
                    }
                }
                else if (binding.SafetyMode == WorldChunkEdgeSafetyMode.ReviewedPortal)
                {
                    if (string.IsNullOrWhiteSpace(binding.ReviewReceiptId) ||
                        !IsOptionalCatalogNeighbor(
                            snapshot,
                            chunk,
                            binding.ConnectedChunkId))
                    {
                        Add(
                            diagnostics,
                            WorldChunkLoadFailureCodes.ChunkEdgeUnsafe,
                            binding.Edge.ToString(),
                            "A cave/modular portal requires a review receipt and any linked chunk must be a catalog neighbor.");
                    }
                }
            }
        }

        private static void ValidateCollider(
            Collider collider,
            WorldChunkGroundSourceKind sourceKind,
            HashSet<Mesh> renderedMeshes,
            ICollection<WorldChunkPhysicalGroundDiagnostic> diagnostics,
            Transform requiredOwnerRoot,
            string invalidCode = null)
        {
            string fallbackCode = invalidCode ??
                WorldChunkLoadFailureCodes.GroundColliderInvalid;
            if (collider == null)
            {
                Add(
                    diagnostics,
                    invalidCode ?? WorldChunkLoadFailureCodes.GroundColliderUnbound,
                    string.Empty,
                    "A physical-ground collider reference is unbound.");
                return;
            }
            if (requiredOwnerRoot == null ||
                collider.transform != requiredOwnerRoot &&
                !collider.transform.IsChildOf(requiredOwnerRoot))
            {
                Add(
                    diagnostics,
                    invalidCode ?? WorldChunkLoadFailureCodes.GroundColliderUnbound,
                    collider.name,
                    "The physical-ground collider must belong to the catalog chunk root hierarchy.");
            }
            if (!collider.enabled ||
                !collider.gameObject.activeInHierarchy ||
                collider.isTrigger)
            {
                Add(
                    diagnostics,
                    invalidCode ?? WorldChunkLoadFailureCodes.GroundColliderDisabled,
                    collider.name,
                    "Physical-ground colliders must be active, enabled, and non-trigger.");
            }
            if (collider.attachedRigidbody != null &&
                !collider.attachedRigidbody.isKinematic)
            {
                Add(
                    diagnostics,
                    fallbackCode,
                    collider.name,
                    "Physical ground cannot be owned by a dynamic Rigidbody.");
            }

            if (collider is TerrainCollider terrainCollider)
            {
                if (terrainCollider.terrainData == null)
                {
                    Add(
                        diagnostics,
                        invalidCode ?? WorldChunkLoadFailureCodes.GroundColliderUnbound,
                        collider.name,
                        "The TerrainCollider has no TerrainData.");
                }
                if (sourceKind != WorldChunkGroundSourceKind.TerrainHeightfield &&
                    sourceKind != WorldChunkGroundSourceKind.ReviewedCaveOrModularCollision)
                {
                    Add(
                        diagnostics,
                        fallbackCode,
                        collider.name,
                        "The collider type does not match the declared ground source kind.");
                }
                return;
            }

            if (collider is MeshCollider meshCollider)
            {
                if (meshCollider.sharedMesh == null)
                {
                    Add(
                        diagnostics,
                        invalidCode ?? WorldChunkLoadFailureCodes.GroundColliderUnbound,
                        collider.name,
                        "The MeshCollider has no dedicated collision mesh.");
                }
                else if (renderedMeshes.Contains(meshCollider.sharedMesh))
                {
                    Add(
                        diagnostics,
                        invalidCode ?? WorldChunkLoadFailureCodes.GroundRenderMeshReused,
                        collider.name,
                        "A rendered mesh cannot also serve as physical-ground authority.");
                }
                if (sourceKind == WorldChunkGroundSourceKind.DedicatedCollisionMesh &&
                    !meshCollider.convex)
                {
                    Add(
                        diagnostics,
                        fallbackCode,
                        collider.name,
                        "Dedicated mesh ground must be an explicitly closed convex collision volume; reviewed non-convex cave or modular collision must use its reviewed source kind.");
                }
                if (sourceKind != WorldChunkGroundSourceKind.DedicatedCollisionMesh &&
                    sourceKind != WorldChunkGroundSourceKind.ReviewedCaveOrModularCollision)
                {
                    Add(
                        diagnostics,
                        fallbackCode,
                        collider.name,
                        "The collider type does not match the declared ground source kind.");
                }
                return;
            }

            if (sourceKind != WorldChunkGroundSourceKind.SolidColliderAssembly &&
                sourceKind != WorldChunkGroundSourceKind.ReviewedCaveOrModularCollision)
            {
                Add(
                    diagnostics,
                    fallbackCode,
                    collider.name,
                    "The collider type does not match the declared ground source kind.");
            }
        }

        private static HashSet<Mesh> RenderedMeshes(WorldChunkRoot chunkRoot)
        {
            var meshes = new HashSet<Mesh>();
            foreach (MeshFilter filter in chunkRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                {
                    meshes.Add(filter.sharedMesh);
                }
            }
            foreach (SkinnedMeshRenderer renderer in
                     chunkRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh != null)
                {
                    meshes.Add(renderer.sharedMesh);
                }
            }
            return meshes;
        }

        private static bool IsCardinalCatalogNeighbor(
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk,
            string neighborId,
            WorldChunkEdge edge)
        {
            if (!chunk.NeighborIds.Contains(neighborId))
            {
                return false;
            }
            WorldChunkDefinition neighbor = snapshot.GetChunk(neighborId);
            if (neighbor == null ||
                !string.Equals(
                    neighbor.WorldId,
                    chunk.WorldId,
                    StringComparison.Ordinal) ||
                !neighbor.NeighborIds.Contains(chunk.Id))
            {
                return false;
            }

            int deltaX = neighbor.GridX - chunk.GridX;
            int deltaZ = neighbor.GridZ - chunk.GridZ;
            switch (edge)
            {
                case WorldChunkEdge.North:
                    return deltaX == 0 && deltaZ == 1;
                case WorldChunkEdge.East:
                    return deltaX == 1 && deltaZ == 0;
                case WorldChunkEdge.South:
                    return deltaX == 0 && deltaZ == -1;
                case WorldChunkEdge.West:
                    return deltaX == -1 && deltaZ == 0;
                default:
                    return false;
            }
        }

        private static bool IsOptionalCatalogNeighbor(
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk,
            string neighborId)
        {
            return string.IsNullOrWhiteSpace(neighborId) ||
                chunk.NeighborIds.Contains(neighborId) &&
                snapshot.GetChunk(neighborId) != null;
        }

        private static bool ReachesCatalogEdge(
            Bounds colliderBounds,
            WorldChunkRoot chunkRoot,
            WorldChunkEdge edge)
        {
            float halfSpan = chunkRoot.ChunkSpanMeters * 0.5f;
            Vector3 origin = chunkRoot.transform.position;
            float plane;
            switch (edge)
            {
                case WorldChunkEdge.North:
                    plane = origin.z + halfSpan;
                    return colliderBounds.min.z <= plane + EdgeContactToleranceMeters &&
                        colliderBounds.max.z >= plane - EdgeContactToleranceMeters &&
                        CoversRange(
                            colliderBounds.min.x,
                            colliderBounds.max.x,
                            origin.x - halfSpan,
                            origin.x + halfSpan);
                case WorldChunkEdge.East:
                    plane = origin.x + halfSpan;
                    return colliderBounds.min.x <= plane + EdgeContactToleranceMeters &&
                        colliderBounds.max.x >= plane - EdgeContactToleranceMeters &&
                        CoversRange(
                            colliderBounds.min.z,
                            colliderBounds.max.z,
                            origin.z - halfSpan,
                            origin.z + halfSpan);
                case WorldChunkEdge.South:
                    plane = origin.z - halfSpan;
                    return colliderBounds.min.z <= plane + EdgeContactToleranceMeters &&
                        colliderBounds.max.z >= plane - EdgeContactToleranceMeters &&
                        CoversRange(
                            colliderBounds.min.x,
                            colliderBounds.max.x,
                            origin.x - halfSpan,
                            origin.x + halfSpan);
                case WorldChunkEdge.West:
                    plane = origin.x - halfSpan;
                    return colliderBounds.min.x <= plane + EdgeContactToleranceMeters &&
                        colliderBounds.max.x >= plane - EdgeContactToleranceMeters &&
                        CoversRange(
                            colliderBounds.min.z,
                            colliderBounds.max.z,
                            origin.z - halfSpan,
                            origin.z + halfSpan);
                default:
                    return false;
            }
        }

        private static bool CoversRange(
            float boundsMinimum,
            float boundsMaximum,
            float requiredMinimum,
            float requiredMaximum)
        {
            return boundsMinimum <= requiredMinimum + EdgeContactToleranceMeters &&
                boundsMaximum >= requiredMaximum - EdgeContactToleranceMeters;
        }

        private static void Add(
            ICollection<WorldChunkPhysicalGroundDiagnostic> diagnostics,
            string code,
            string relatedObject,
            string message)
        {
            diagnostics.Add(new WorldChunkPhysicalGroundDiagnostic(
                code,
                relatedObject,
                message));
        }
    }
}
