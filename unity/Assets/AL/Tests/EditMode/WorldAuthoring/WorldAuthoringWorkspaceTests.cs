using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Data.Catalogs.WorldStreaming;
using AL.Editor.World;
using AL.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Tests.EditMode.WorldAuthoring
{
    public sealed class WorldAuthoringWorkspaceTests
    {
        [Test]
        public void CanonicalProviderUsesExistingStreamingCatalogAuthority()
        {
            WorldAuthoringCatalogRead read =
                WorldAuthoringCatalogProvider.LoadCanonical(true);

            Assert.That(
                read.IsAccepted,
                Is.True,
                string.Join("\n", read.Diagnostics));
            Assert.That(read.Snapshot.Dimensions, Has.Count.EqualTo(3));
            Assert.That(read.Snapshot.Worlds, Has.Count.EqualTo(11));
            Assert.That(read.Snapshot.Chunks, Has.Count.EqualTo(78));
        }

        [Test]
        public void StableIdSelectionSurvivesOrderingAndRepairsStaleOwnership()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();

            WorldAuthoringSelection exact =
                WorldAuthoringSelectionResolver.Resolve(
                    snapshot,
                    "dimension_kingdom_25d",
                    "world_kingdom_private",
                    "chunk_kingdom_area_07");
            WorldAuthoringSelection repaired =
                WorldAuthoringSelectionResolver.Resolve(
                    snapshot,
                    "dimension_kingdom_25d",
                    "world_adventure_outer_warzone",
                    "chunk_warzone_crossroads");

            Assert.That(exact.DimensionId, Is.EqualTo("dimension_kingdom_25d"));
            Assert.That(exact.WorldId, Is.EqualTo("world_kingdom_private"));
            Assert.That(exact.ChunkId, Is.EqualTo("chunk_kingdom_area_07"));
            Assert.That(repaired.WorldId, Is.EqualTo("world_kingdom_private"));
            Assert.That(
                repaired.ChunkId,
                Is.EqualTo(snapshot.GetWorld(repaired.WorldId).SeedChunkId));
        }

        [Test]
        public void FocusSetContainsOnlyCatalogNeighborsFromOneWorld()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldAuthoringSelection selection =
                WorldAuthoringSelectionResolver.Resolve(
                    snapshot,
                    "dimension_adventure_3d",
                    "world_adventure_ring_slot_01_inner",
                    "chunk_ring_slot_01_capital_core");

            WorldAuthoringSelectionContext context =
                WorldAuthoringSelectionResolver.BuildContext(
                    snapshot,
                    selection);

            CollectionAssert.AreEqual(
                context.Focus.NeighborIds,
                context.Neighbors.Select(value => value.Id));
            Assert.That(context.FocusAndNeighbors.First(), Is.SameAs(context.Focus));
            Assert.That(
                context.FocusAndNeighbors.Select(value => value.Id).Distinct().Count(),
                Is.EqualTo(context.FocusAndNeighbors.Count));
            Assert.That(
                context.FocusAndNeighbors.All(value =>
                    string.Equals(
                        value.WorldId,
                        context.World.Id,
                        StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        public void SceneEnvelopeUsesCatalogCoordinatesAndDimensionSpan()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldChunkDefinition chunk = snapshot.GetChunk(
                "chunk_warzone_sector_03");
            WorldDimensionDefinition dimension = snapshot.GetDimension(
                "dimension_adventure_3d");

            WorldAuthoringChunkEnvelope envelope =
                WorldAuthoringSelectionResolver.BuildEnvelope(snapshot, chunk);

            Assert.That(envelope.Dimension, Is.SameAs(dimension));
            Assert.That(
                envelope.Bounds.center.x,
                Is.EqualTo(chunk.GridX * dimension.ChunkSpanMeters));
            Assert.That(
                envelope.Bounds.center.z,
                Is.EqualTo(chunk.GridZ * dimension.ChunkSpanMeters));
            Assert.That(
                envelope.Bounds.size.x,
                Is.EqualTo(dimension.ChunkSpanMeters));
            Assert.That(
                envelope.Bounds.size.z,
                Is.EqualTo(dimension.ChunkSpanMeters));
        }

        [Test]
        public void PersistedWorkspaceSelectionParticipatesInUnityUndo()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldAuthoringWorkspaceState state =
                WorldAuthoringWorkspaceState.instance;
            WorldAuthoringSelection original = state.Selection;
            bool originalOverlay = state.ShowSceneOverlay;
            bool originalLabels = state.ShowNeighborLabels;
            WorldAuthoringSelection first =
                WorldAuthoringSelectionResolver.Resolve(
                    snapshot,
                    "dimension_adventure_3d",
                    "world_adventure_ring_slot_01_inner",
                    "chunk_ring_slot_01_area_01");
            WorldAuthoringSelection second =
                WorldAuthoringSelectionResolver.Resolve(
                    snapshot,
                    "dimension_kingdom_25d",
                    "world_kingdom_private",
                    "chunk_kingdom_area_01");

            try
            {
                state.ApplySelection(first, "World Authoring Test First");
                state.ApplySelection(second, "World Authoring Test Second");

                Undo.PerformUndo();

                Assert.That(state.SelectedDimensionId, Is.EqualTo(first.DimensionId));
                Assert.That(state.SelectedWorldId, Is.EqualTo(first.WorldId));
                Assert.That(state.SelectedChunkId, Is.EqualTo(first.ChunkId));
            }
            finally
            {
                Undo.ClearUndo(state);
                state.ApplySelection(original, "Restore World Authoring Test State");
                state.ApplyOverlayOptions(
                    originalOverlay,
                    originalLabels,
                    "Restore World Authoring Overlay State");
                Undo.ClearUndo(state);
            }
        }

        [Test]
        public void FailClosedPreflightReportsEveryMissingDependencyDeterministically()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldAuthoringSelectionContext context = CapitalContext(snapshot);
            WorldAuthoringChunkInspection[] inspections = context.FocusAndNeighbors
                .Select(chunk => new WorldAuthoringChunkInspection(
                    chunk.Id,
                    chunk.ScenePath,
                    true,
                    0,
                    false,
                    0,
                    false,
                    Array.Empty<string>()))
                .ToArray();
            var dependencies = new WorldAuthoringDependencyStatus(
                false,
                false,
                false);

            WorldAuthoringPreflightReport first =
                WorldAuthoringPreflight.Evaluate(
                    context,
                    inspections,
                    dependencies);
            WorldAuthoringPreflightReport second =
                WorldAuthoringPreflight.Evaluate(
                    context,
                    inspections.Reverse(),
                    dependencies);

            Assert.That(first.IsReadyForPlay, Is.False);
            Assert.That(
                first.Issues.Select(value => value.Code),
                Does.Contain(WorldAuthoringPreflight.MissingProductionLoaderCode));
            Assert.That(
                first.Issues.Select(value => value.Code),
                Does.Contain(WorldAuthoringPreflight.MissingPlayBridgeCode));
            Assert.That(
                first.Issues.Select(value => value.Code),
                Does.Contain(WorldAuthoringPreflight.MissingContentBudgetCode));
            Assert.That(
                first.Issues.Select(value => value.Code),
                Does.Contain(WorldAuthoringPreflight.InvalidChunkRootCode));
            Assert.That(
                first.Issues.Select(value => value.Code),
                Does.Contain(WorldAuthoringPreflight.MissingColliderCode));
            Assert.That(
                first.Issues.Select(value => value.Code),
                Does.Contain(WorldAuthoringPreflight.MissingNavigationCode));
            Assert.That(
                first.Issues.Select(value => value.Code),
                Does.Contain(
                    WorldAuthoringPreflight.MissingPhysicalGroundAuthorityCode));
            Assert.That(
                first.Issues.Select(value => value.Code),
                Does.Contain(WorldAuthoringPreflight.ReplacementSocketMismatchCode));
            CollectionAssert.AreEqual(
                first.Issues.Select(value => value.Fingerprint),
                second.Issues.Select(value => value.Fingerprint));
        }

        [Test]
        public void CompleteDependenciesProduceReadyPreflight()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldAuthoringSelectionContext context = CapitalContext(snapshot);
            WorldAuthoringChunkInspection[] inspections = context.FocusAndNeighbors
                .Select(chunk => new WorldAuthoringChunkInspection(
                    chunk.Id,
                    chunk.ScenePath,
                    true,
                    1,
                    true,
                    1,
                    true,
                    chunk.ReplacementSocketIds.ToArray(),
                    Array.Empty<WorldChunkPhysicalGroundDiagnostic>()))
                .ToArray();

            WorldAuthoringPreflightReport report =
                WorldAuthoringPreflight.Evaluate(
                    context,
                    inspections,
                    new WorldAuthoringDependencyStatus(true, true, true));

            Assert.That(report.IsReadyForPlay, Is.True);
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void CanonicalGeneratedChunkInspectionIsReadOnlyAndHonest()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldChunkDefinition chunk = snapshot.GetChunk(
                "chunk_ring_slot_01_capital_core");
            string[] scenePathsBefore = LoadedScenePaths();
            byte[] sceneBytesBefore = File.ReadAllBytes(chunk.ScenePath);

            WorldAuthoringChunkInspection inspection =
                WorldAuthoringSceneInspector.Inspect(snapshot, chunk);

            Assert.That(inspection.SceneExists, Is.True);
            Assert.That(inspection.ChunkRootCount, Is.EqualTo(1));
            Assert.That(inspection.ChunkRootMatchesCatalog, Is.True);
            Assert.That(inspection.SolidColliderCount, Is.GreaterThan(0));
            Assert.That(inspection.HasNavigationData, Is.False);
            Assert.That(inspection.HasSafePhysicalGround, Is.False);
            Assert.That(
                inspection.PhysicalGroundDiagnostics.Select(value => value.Code),
                Does.Contain(
                    WorldChunkLoadFailureCodes.PhysicalGroundAuthorityMissing));
            CollectionAssert.AreEquivalent(
                chunk.ReplacementSocketIds,
                inspection.ReplacementSocketIds);
            CollectionAssert.AreEqual(scenePathsBefore, LoadedScenePaths());
            CollectionAssert.AreEqual(
                sceneBytesBefore,
                File.ReadAllBytes(chunk.ScenePath),
                "Read-only preflight inspection must not rewrite generated output.");
        }

        [Test]
        public void PhysicalGroundDiagnosticsRemainDistinctInAuthoringPreflight()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldAuthoringSelectionContext context = CapitalContext(snapshot);
            WorldChunkPhysicalGroundDiagnostic[] injected =
            {
                new WorldChunkPhysicalGroundDiagnostic(
                    WorldChunkLoadFailureCodes.GroundColliderDisabled,
                    "DisabledGround",
                    "Injected disabled-ground diagnostic."),
                new WorldChunkPhysicalGroundDiagnostic(
                    WorldChunkLoadFailureCodes.GroundColliderUnbound,
                    "UnboundGround",
                    "Injected unbound-ground diagnostic."),
                new WorldChunkPhysicalGroundDiagnostic(
                    WorldChunkLoadFailureCodes.ChunkEdgeUnsafe,
                    "North",
                    "Injected unsafe-edge diagnostic."),
                new WorldChunkPhysicalGroundDiagnostic(
                    WorldChunkLoadFailureCodes.ChunkSeamContinuityUnproven,
                    "East",
                    "Injected unproven-seam diagnostic.")
            };
            WorldAuthoringChunkInspection[] inspections = context.FocusAndNeighbors
                .Select(chunk => new WorldAuthoringChunkInspection(
                    chunk.Id,
                    chunk.ScenePath,
                    true,
                    1,
                    true,
                    1,
                    true,
                    chunk.ReplacementSocketIds.ToArray(),
                    string.Equals(
                        chunk.Id,
                        context.Focus.Id,
                        StringComparison.Ordinal)
                        ? injected
                        : Array.Empty<WorldChunkPhysicalGroundDiagnostic>()))
                .ToArray();

            WorldAuthoringPreflightReport report =
                WorldAuthoringPreflight.Evaluate(
                    context,
                    inspections,
                    new WorldAuthoringDependencyStatus(true, true, true));

            Assert.That(report.IsReadyForPlay, Is.False);
            Assert.That(
                report.Issues.Select(value => value.Code),
                Does.Contain(WorldAuthoringPreflight.GroundColliderDisabledCode));
            Assert.That(
                report.Issues.Select(value => value.Code),
                Does.Contain(WorldAuthoringPreflight.GroundColliderUnboundCode));
            Assert.That(
                report.Issues.Select(value => value.Code),
                Does.Contain(WorldAuthoringPreflight.UnsafeChunkEdgeCode));
            Assert.That(
                report.Issues.Select(value => value.Code),
                Does.Contain(WorldAuthoringPreflight.UnprovenChunkSeamCode));
        }

        private static WorldStreamingSnapshot AcceptedSnapshot()
        {
            WorldAuthoringCatalogRead read =
                WorldAuthoringCatalogProvider.LoadCanonical();
            Assert.That(
                read.IsAccepted,
                Is.True,
                string.Join("\n", read.Diagnostics));
            return read.Snapshot;
        }

        private static WorldAuthoringSelectionContext CapitalContext(
            WorldStreamingSnapshot snapshot)
        {
            WorldAuthoringSelection selection =
                WorldAuthoringSelectionResolver.Resolve(
                    snapshot,
                    "dimension_adventure_3d",
                    "world_adventure_ring_slot_01_inner",
                    "chunk_ring_slot_01_capital_core");
            return WorldAuthoringSelectionResolver.BuildContext(
                snapshot,
                selection);
        }

        private static string[] LoadedScenePaths()
        {
            var paths = new List<string>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                paths.Add(SceneManager.GetSceneAt(index).path);
            }
            return paths.ToArray();
        }
    }
}
