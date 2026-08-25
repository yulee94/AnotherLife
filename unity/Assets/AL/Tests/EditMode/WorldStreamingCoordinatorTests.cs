using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AL.Data.Catalogs.WorldStreaming;
using AL.World.Streaming;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class WorldStreamingCoordinatorTests
    {
        [Test]
        public async Task SwitchingWorldsUnloadsEveryOldChunkBeforeLoadingDestination()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader(
                "chunk_ring_slot_01_capital_core",
                "chunk_ring_slot_01_area_01");
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");

            WorldResidencyPlan plan = await coordinator.FocusAsync(
                "world_kingdom_private",
                "chunk_kingdom_castle_core",
                CancellationToken.None);

            int firstLoad = loader.Operations.FindIndex(value => value.StartsWith("load:", StringComparison.Ordinal));
            int lastUnload = loader.Operations.FindLastIndex(value => value.StartsWith("unload:", StringComparison.Ordinal));
            Assert.That(firstLoad, Is.GreaterThan(lastUnload));
            Assert.That(
                loader.Operations.Take(firstLoad)
                    .Where(value => !value.StartsWith("visibility:", StringComparison.Ordinal)),
                Has.All.StartsWith("unload:"));
            Assert.That(loader.LoadedChunkIds, Is.EquivalentTo(plan.RequiredChunkIds));
            Assert.That(coordinator.ActiveWorldId, Is.EqualTo("world_kingdom_private"));
        }

        [Test]
        public async Task WorldSwitchHidesResidencyUntilDestinationIsComplete()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader(
                "chunk_ring_slot_01_capital_core",
                "chunk_ring_slot_01_area_01");
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");

            await coordinator.FocusAsync(
                "world_kingdom_private",
                "chunk_kingdom_castle_core",
                CancellationToken.None);

            int hide = loader.Operations.IndexOf("visibility:false");
            int firstUnload = loader.Operations.FindIndex(
                value => value.StartsWith("unload:", StringComparison.Ordinal));
            int lastLoad = loader.Operations.FindLastIndex(
                value => value.StartsWith("load:", StringComparison.Ordinal));
            int reveal = loader.Operations.LastIndexOf("visibility:true");
            Assert.That(hide, Is.GreaterThanOrEqualTo(0));
            Assert.That(hide, Is.LessThan(firstUnload));
            Assert.That(reveal, Is.GreaterThan(lastLoad));
            Assert.That(loader.SpatialVisible, Is.True);
        }

        [Test]
        public async Task InitialFocusHidesBeforeLoadingDestination()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader();
            var coordinator = new WorldStreamingCoordinator(snapshot, loader);

            await coordinator.FocusAsync(
                "world_adventure_ring_slot_01_inner",
                "chunk_ring_slot_01_area_01",
                CancellationToken.None);

            int hide = loader.Operations.IndexOf("visibility:false");
            int firstLoad = loader.Operations.FindIndex(
                value => value.StartsWith("load:", StringComparison.Ordinal));
            Assert.That(hide, Is.EqualTo(0));
            Assert.That(firstLoad, Is.GreaterThan(hide));
            Assert.That(loader.MutationWhileVisible, Is.False);
            Assert.That(loader.SpatialVisible, Is.True);
        }

        [Test]
        public void FailedSameWorldLoadRollsBackNewChunksAndKeepsPreviousResidency()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader("chunk_ring_slot_01_capital_core")
            {
                FailOnLoadId = "chunk_ring_slot_01_main_gate"
            };
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");

            Assert.That(
                async () => await coordinator.FocusAsync(
                    "world_adventure_ring_slot_01_inner",
                    "chunk_ring_slot_01_area_01",
                    CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());

            CollectionAssert.AreEquivalent(
                new[] { "chunk_ring_slot_01_capital_core" },
                loader.LoadedChunkIds);
            Assert.That(
                loader.Operations,
                Does.Contain("unload:chunk_ring_slot_01_area_01"));
            Assert.That(
                coordinator.ActiveWorldId,
                Is.EqualTo("world_adventure_ring_slot_01_inner"));
        }

        [Test]
        public async Task ConcurrentFocusRequestsAreSerialized()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader
            {
                OperationDelayMilliseconds = 25
            };
            var coordinator = new WorldStreamingCoordinator(snapshot, loader);

            Task first = coordinator.FocusAsync(
                "world_adventure_ring_slot_01_inner",
                "chunk_ring_slot_01_area_01",
                CancellationToken.None);
            Task second = coordinator.FocusAsync(
                "world_kingdom_private",
                "chunk_kingdom_castle_core",
                CancellationToken.None);
            await Task.WhenAll(first, second);

            Assert.That(loader.MaximumConcurrentOperations, Is.EqualTo(1));
            Assert.That(coordinator.ActiveWorldId, Is.EqualTo("world_kingdom_private"));
        }

        [Test]
        public void InvalidDestinationDoesNotUnloadActiveWorld()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader("chunk_ring_slot_01_capital_core");
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");

            Assert.That(
                async () => await coordinator.FocusAsync(
                    "world_missing",
                    "chunk_missing",
                    CancellationToken.None),
                Throws.TypeOf<ArgumentException>());

            Assert.That(loader.Operations, Is.Empty);
            CollectionAssert.AreEquivalent(
                new[] { "chunk_ring_slot_01_capital_core" },
                loader.LoadedChunkIds);
            Assert.That(
                coordinator.ActiveWorldId,
                Is.EqualTo("world_adventure_ring_slot_01_inner"));
        }

        [Test]
        public void InitialWorldClaimRejectsMismatchedResidency()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader("chunk_kingdom_castle_core");

            Assert.That(
                () => new WorldStreamingCoordinator(
                    snapshot,
                    loader,
                    "world_adventure_ring_slot_01_inner"),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void FailedWorldSwitchUnloadRestoresPreviousResidency()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader(
                "chunk_ring_slot_01_area_01",
                "chunk_ring_slot_01_capital_core")
            {
                FailOnUnloadId = "chunk_ring_slot_01_capital_core"
            };
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");

            Assert.That(
                async () => await coordinator.FocusAsync(
                    "world_kingdom_private",
                    "chunk_kingdom_castle_core",
                    CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That(
                coordinator.ActiveWorldId,
                Is.EqualTo("world_adventure_ring_slot_01_inner"));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "chunk_ring_slot_01_capital_core",
                    "chunk_ring_slot_01_area_01"
                },
                loader.LoadedChunkIds);
        }

        [Test]
        public void FailedWorldSwitchCompensationHidesPartialResidency()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader(
                "chunk_ring_slot_01_area_01",
                "chunk_ring_slot_01_capital_core")
            {
                FailOnUnloadId = "chunk_ring_slot_01_capital_core",
                FailOnLoadId = "chunk_ring_slot_01_area_01"
            };
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");

            Assert.That(
                async () => await coordinator.FocusAsync(
                    "world_kingdom_private",
                    "chunk_kingdom_castle_core",
                    CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That(coordinator.ActiveWorldId, Is.Null);
            Assert.That(loader.SpatialVisible, Is.False);
        }

        [Test]
        public void FailedWorldSwitchLoadRestoresPreviousResidency()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            string[] previousResidency =
            {
                "chunk_ring_slot_01_capital_core",
                "chunk_ring_slot_01_area_01"
            };
            var loader = new RecordingChunkLoader(previousResidency)
            {
                FailOnLoadId = "chunk_kingdom_area_02"
            };
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");

            Assert.That(
                async () => await coordinator.FocusAsync(
                    "world_kingdom_private",
                    "chunk_kingdom_castle_core",
                    CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());

            CollectionAssert.AreEquivalent(previousResidency, loader.LoadedChunkIds);
            Assert.That(
                coordinator.ActiveWorldId,
                Is.EqualTo("world_adventure_ring_slot_01_inner"));
        }

        [Test]
        public void SameWorldUnloadFailureRestoresExactPreviousResidency()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            string[] previousResidency =
            {
                "chunk_ring_slot_01_capital_core",
                "chunk_ring_slot_01_area_01",
                "chunk_ring_slot_01_area_02",
                "chunk_ring_slot_01_area_03",
                "chunk_ring_slot_01_area_04",
                "chunk_ring_slot_01_main_gate"
            };
            var loader = new RecordingChunkLoader(previousResidency)
            {
                FailOnUnloadId = "chunk_ring_slot_01_area_03"
            };
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");

            Assert.That(
                async () => await coordinator.FocusAsync(
                    "world_adventure_ring_slot_01_inner",
                    "chunk_ring_slot_01_main_gate",
                    CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());

            CollectionAssert.AreEquivalent(previousResidency, loader.LoadedChunkIds);
            Assert.That(
                coordinator.ActiveWorldId,
                Is.EqualTo("world_adventure_ring_slot_01_inner"));
        }

        [Test]
        public void FailedCompensationHidesPartialResidencyInSafeShell()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader(
                "chunk_ring_slot_01_capital_core",
                "chunk_ring_slot_01_area_01",
                "chunk_ring_slot_01_area_02",
                "chunk_ring_slot_01_area_03",
                "chunk_ring_slot_01_area_04",
                "chunk_ring_slot_01_main_gate")
            {
                FailOnUnloadId = "chunk_ring_slot_01_area_03",
                FailOnLoadId = "chunk_ring_slot_01_area_02"
            };
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");

            Assert.That(
                async () => await coordinator.FocusAsync(
                    "world_adventure_ring_slot_01_inner",
                    "chunk_ring_slot_01_main_gate",
                    CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That(coordinator.ActiveWorldId, Is.Null);
            Assert.That(loader.SpatialVisible, Is.False);
        }

        [Test]
        public async Task SuccessfulFocusRevealsSpatialResidencyAfterSafeShell()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader(
                "chunk_ring_slot_01_capital_core",
                "chunk_ring_slot_01_area_01",
                "chunk_ring_slot_01_area_02",
                "chunk_ring_slot_01_area_03",
                "chunk_ring_slot_01_area_04",
                "chunk_ring_slot_01_main_gate")
            {
                FailOnUnloadId = "chunk_ring_slot_01_area_03",
                FailOnLoadId = "chunk_ring_slot_01_area_02"
            };
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");
            Assert.That(
                async () => await coordinator.FocusAsync(
                    "world_adventure_ring_slot_01_inner",
                    "chunk_ring_slot_01_main_gate",
                    CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());

            loader.FailOnUnloadId = null;
            loader.FailOnLoadId = null;
            await coordinator.FocusAsync(
                "world_adventure_ring_slot_01_inner",
                "chunk_ring_slot_01_main_gate",
                CancellationToken.None);

            Assert.That(loader.SpatialVisible, Is.True);
            Assert.That(
                coordinator.ActiveWorldId,
                Is.EqualTo("world_adventure_ring_slot_01_inner"));
        }

        [Test]
        public async Task NullClaimResidualWorldUnloadsBeforeLoadingDestination()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader(
                "chunk_ring_slot_01_capital_core",
                "chunk_ring_slot_01_area_01",
                "chunk_ring_slot_01_area_02",
                "chunk_ring_slot_01_area_03",
                "chunk_ring_slot_01_area_04",
                "chunk_ring_slot_01_main_gate")
            {
                FailOnUnloadId = "chunk_ring_slot_01_area_03",
                FailOnLoadId = "chunk_ring_slot_01_area_02"
            };
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");
            Assert.That(
                async () => await coordinator.FocusAsync(
                    "world_adventure_ring_slot_01_inner",
                    "chunk_ring_slot_01_main_gate",
                    CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(coordinator.ActiveWorldId, Is.Null);

            loader.FailOnUnloadId = null;
            loader.FailOnLoadId = null;
            loader.Operations.Clear();
            await coordinator.FocusAsync(
                "world_kingdom_private",
                "chunk_kingdom_castle_core",
                CancellationToken.None);

            int firstLoad = loader.Operations.FindIndex(
                value => value.StartsWith("load:", StringComparison.Ordinal));
            int lastUnload = loader.Operations.FindLastIndex(
                value => value.StartsWith("unload:", StringComparison.Ordinal));
            Assert.That(firstLoad, Is.GreaterThan(lastUnload));
            Assert.That(loader.SpatialVisible, Is.True);
            Assert.That(coordinator.ActiveWorldId, Is.EqualTo("world_kingdom_private"));
        }

        [Test]
        public async Task FailedVisibilityActivationKeepsSafeShellHidden()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            var loader = new RecordingChunkLoader();
            await loader.SetSpatialVisibilityAsync(false, CancellationToken.None);
            loader.FailAfterVisibilityValue = true;
            var coordinator = new WorldStreamingCoordinator(snapshot, loader);

            Assert.That(
                async () => await coordinator.FocusAsync(
                    "world_adventure_ring_slot_01_inner",
                    "chunk_ring_slot_01_area_01",
                    CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That(coordinator.ActiveWorldId, Is.Null);
            Assert.That(loader.SpatialVisible, Is.False);
            Assert.That(loader.LoadedChunkIds, Is.Empty);
            Assert.That(loader.MutationWhileVisible, Is.False);
        }

        [Test]
        public void FailedPreviousWorldVisibilityRestoreKeepsSafeShellHidden()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            string[] previousResidency =
            {
                "chunk_ring_slot_01_capital_core",
                "chunk_ring_slot_01_area_01"
            };
            var loader = new RecordingChunkLoader(previousResidency)
            {
                FailOnLoadId = "chunk_kingdom_area_02",
                FailAfterVisibilityValue = true
            };
            var coordinator = new WorldStreamingCoordinator(
                snapshot,
                loader,
                "world_adventure_ring_slot_01_inner");

            Assert.That(
                async () => await coordinator.FocusAsync(
                    "world_kingdom_private",
                    "chunk_kingdom_castle_core",
                    CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());

            CollectionAssert.AreEquivalent(previousResidency, loader.LoadedChunkIds);
            Assert.That(coordinator.ActiveWorldId, Is.Null);
            Assert.That(loader.SpatialVisible, Is.False);
        }

        private static WorldStreamingSnapshot AcceptedSnapshot()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_streaming_catalog.json"));
            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);
            Assert.That(result.Status, Is.EqualTo(WorldStreamingLoadStatus.Accepted));
            return result.Snapshot;
        }

        private sealed class RecordingChunkLoader : IWorldChunkLoader
        {
            private readonly HashSet<string> loaded;

            internal RecordingChunkLoader(params string[] initiallyLoaded)
            {
                loaded = new HashSet<string>(initiallyLoaded, StringComparer.Ordinal);
            }

            internal List<string> Operations { get; } = new List<string>();
            internal string FailOnLoadId { get; set; }
            internal string FailOnUnloadId { get; set; }
            internal int OperationDelayMilliseconds { get; set; }
            internal int MaximumConcurrentOperations { get; private set; }
            internal bool SpatialVisible { get; private set; } = true;
            internal bool? FailAfterVisibilityValue { get; set; }
            internal bool MutationWhileVisible { get; private set; }
            public IReadOnlyCollection<string> LoadedChunkIds => loaded;

            public Task SetSpatialVisibilityAsync(
                bool visible,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SpatialVisible = visible;
                Operations.Add("visibility:" + visible.ToString().ToLowerInvariant());
                if (FailAfterVisibilityValue == visible)
                {
                    FailAfterVisibilityValue = null;
                    throw new InvalidOperationException("Injected visibility failure.");
                }
                return Task.CompletedTask;
            }

            public async Task LoadAsync(
                WorldChunkDefinition chunk,
                CancellationToken cancellationToken)
            {
                int active = Interlocked.Increment(ref activeOperations);
                MaximumConcurrentOperations = Math.Max(MaximumConcurrentOperations, active);
                try
                {
                    MutationWhileVisible |= SpatialVisible;
                    if (OperationDelayMilliseconds > 0)
                    {
                        await Task.Delay(OperationDelayMilliseconds, cancellationToken);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    Operations.Add("load:" + chunk.Id);
                    if (string.Equals(chunk.Id, FailOnLoadId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Injected load failure.");
                    }
                    loaded.Add(chunk.Id);
                }
                finally
                {
                    Interlocked.Decrement(ref activeOperations);
                }
            }

            public Task UnloadAsync(
                WorldChunkDefinition chunk,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MutationWhileVisible |= SpatialVisible;
                Operations.Add("unload:" + chunk.Id);
                if (string.Equals(chunk.Id, FailOnUnloadId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Injected unload failure.");
                }
                loaded.Remove(chunk.Id);
                return Task.CompletedTask;
            }

            private int activeOperations;
        }
    }
}
