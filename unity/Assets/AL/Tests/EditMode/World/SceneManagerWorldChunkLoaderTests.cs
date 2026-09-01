using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AL.Data.Catalogs.WorldStreaming;
using AL.World.Streaming;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Tests.EditMode.World
{
    public sealed class SceneManagerWorldChunkLoaderTests
    {
        private const int AsyncTimeoutMilliseconds = 5000;

        [Test]
        public async Task ConcurrentLoadsShareOnePhysicalHandleUntilBothOwnersUnload()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldChunkDefinition chunk = snapshot.GetChunk(
                "chunk_ring_slot_01_capital_core");
            var backend = new ControllableSceneBackend();
            var loader = new SceneManagerWorldChunkLoader(snapshot, backend);

            Task first = loader.LoadAsync(chunk, CancellationToken.None);
            Task second = loader.LoadAsync(chunk, CancellationToken.None);

            Assert.That(backend.LoadCalls, Is.EqualTo(1));
            backend.CompleteAllLoads();
            await AwaitBounded(
                Task.WhenAll(first, second),
                "concurrent chunk load ownership");

            CollectionAssert.AreEqual(new[] { chunk.Id }, loader.LoadedChunkIds);
            await AwaitBounded(
                loader.UnloadAsync(chunk, CancellationToken.None),
                "first shared-owner release");
            Assert.That(backend.UnloadCalls, Is.Zero);
            CollectionAssert.AreEqual(new[] { chunk.Id }, loader.LoadedChunkIds);

            await AwaitBounded(
                loader.UnloadAsync(chunk, CancellationToken.None),
                "final shared-owner release");
            Assert.That(backend.UnloadCalls, Is.EqualTo(1));
            Assert.That(loader.LoadedChunkIds, Is.Empty);
        }

        [Test]
        public async Task CanceledWaiterDoesNotCancelAnotherOwnerOrDuplicateLoad()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldChunkDefinition chunk = snapshot.GetChunk(
                "chunk_ring_slot_01_capital_core");
            var backend = new ControllableSceneBackend();
            var loader = new SceneManagerWorldChunkLoader(snapshot, backend);
            var cancellation = new CancellationTokenSource();

            Task canceledOwner = loader.LoadAsync(chunk, cancellation.Token);
            Task retainedOwner = loader.LoadAsync(chunk, CancellationToken.None);
            cancellation.Cancel();

            await ExpectExceptionBounded<OperationCanceledException>(
                canceledOwner,
                "canceled shared-owner load");
            backend.CompleteAllLoads();
            await AwaitBounded(retainedOwner, "retained shared-owner load");

            Assert.That(backend.LoadCalls, Is.EqualTo(1));
            Assert.That(loader.LoadedChunkIds, Is.EquivalentTo(new[] { chunk.Id }));
            await AwaitBounded(
                loader.UnloadAsync(chunk, CancellationToken.None),
                "retained-owner release");
            Assert.That(backend.UnloadCalls, Is.EqualTo(1));
            Assert.That(loader.LoadedChunkIds, Is.Empty);
        }

        [Test]
        public async Task CanceledOnlyOwnerReleasesLateNonCancellableSceneCompletion()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldChunkDefinition chunk = snapshot.GetChunk(
                "chunk_ring_slot_01_capital_core");
            var backend = new ControllableSceneBackend();
            var loader = new SceneManagerWorldChunkLoader(snapshot, backend);
            var cancellation = new CancellationTokenSource();

            Task request = loader.LoadAsync(chunk, cancellation.Token);
            cancellation.Cancel();
            await ExpectExceptionBounded<OperationCanceledException>(
                request,
                "sole canceled-owner load");

            backend.CompleteAllLoads();
            await WaitUntil(
                () => backend.UnloadCalls == 1,
                "late canceled-load cleanup");

            Assert.That(loader.LoadedChunkIds, Is.Empty);
            Assert.That(
                backend.Handles.All(value => !value.IsLoaded),
                Is.True);
        }

        [Test]
        public async Task MissingBakedNavigationFailsClosedAndUnloadsPhysicalScene()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldChunkDefinition chunk = snapshot.GetChunk(
                "chunk_ring_slot_01_capital_core");
            var backend = new ControllableSceneBackend
            {
                Readiness = WorldChunkSceneReadiness.Rejected(
                    WorldChunkLoadFailureCodes.NavigationMissing,
                    "Injected missing baked navigation data.")
            };
            var loader = new SceneManagerWorldChunkLoader(snapshot, backend);

            Task request = loader.LoadAsync(chunk, CancellationToken.None);
            backend.CompleteAllLoads();

            WorldChunkLoadException error =
                await ExpectExceptionBounded<WorldChunkLoadException>(
                    request,
                    "missing-navigation load rejection");
            Assert.That(
                error.FailureCode,
                Is.EqualTo(WorldChunkLoadFailureCodes.NavigationMissing));
            Assert.That(backend.UnloadCalls, Is.EqualTo(1));
            Assert.That(loader.LoadedChunkIds, Is.Empty);
        }

        [Test]
        public async Task FailedUnloadRemainsReportedAndCanBeRetried()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldChunkDefinition chunk = snapshot.GetChunk(
                "chunk_ring_slot_01_capital_core");
            var backend = new ControllableSceneBackend();
            var loader = new SceneManagerWorldChunkLoader(snapshot, backend);
            Task request = loader.LoadAsync(chunk, CancellationToken.None);
            backend.CompleteAllLoads();
            await AwaitBounded(request, "load before unload-failure injection");
            backend.UnloadFailuresRemaining = 1;

            WorldChunkLoadException error =
                await ExpectExceptionBounded<WorldChunkLoadException>(
                    loader.UnloadAsync(chunk, CancellationToken.None),
                    "injected unload failure");

            Assert.That(
                error.FailureCode,
                Is.EqualTo(WorldChunkLoadFailureCodes.SceneUnloadFailed));
            Assert.That(loader.LoadedChunkIds, Is.EquivalentTo(new[] { chunk.Id }));
            Assert.That(backend.Handles.Single().Visible, Is.False);

            await AwaitBounded(
                loader.UnloadAsync(chunk, CancellationToken.None),
                "unload retry");
            Assert.That(backend.UnloadCalls, Is.EqualTo(2));
            Assert.That(loader.LoadedChunkIds, Is.Empty);
        }

        [Test]
        public async Task FailedRevealRollsEveryResidentBackToHiddenState()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldChunkDefinition chunk = snapshot.GetChunk(
                "chunk_ring_slot_01_capital_core");
            var backend = new ControllableSceneBackend();
            var loader = new SceneManagerWorldChunkLoader(snapshot, backend);
            await AwaitBounded(
                loader.SetSpatialVisibilityAsync(false, CancellationToken.None),
                "initial spatial hide");
            Task request = loader.LoadAsync(chunk, CancellationToken.None);
            backend.CompleteAllLoads();
            await AwaitBounded(request, "hidden chunk load");
            Assert.That(backend.Handles.Single().Visible, Is.False);
            backend.FailNextRevealAfterMutation = true;

            WorldChunkLoadException error =
                await ExpectExceptionBounded<WorldChunkLoadException>(
                    loader.SetSpatialVisibilityAsync(
                        true,
                        CancellationToken.None),
                    "injected reveal failure");

            Assert.That(
                error.FailureCode,
                Is.EqualTo(WorldChunkLoadFailureCodes.VisibilityFailed));
            Assert.That(backend.Handles.Single().Visible, Is.False);
        }

        [Test]
        public void ProductionReadinessRejectsMatchingChunkRootWithoutBakedNavigation()
        {
            WorldStreamingSnapshot snapshot = AcceptedSnapshot();
            WorldChunkDefinition chunk = snapshot.GetChunk(
                "chunk_ring_slot_01_capital_core");
            WorldInstanceDefinition world = snapshot.GetWorld(chunk.WorldId);
            WorldDimensionDefinition dimension = snapshot.GetDimension(world.DimensionId);
            Scene scene = EditorSceneManager.NewPreviewScene();

            try
            {
                var rootObject = new GameObject("CatalogChunkRoot");
                SceneManager.MoveGameObjectToScene(rootObject, scene);
                rootObject.transform.position = new Vector3(
                    chunk.GridX * dimension.ChunkSpanMeters,
                    0f,
                    chunk.GridZ * dimension.ChunkSpanMeters);
                WorldChunkRoot root = rootObject.AddComponent<WorldChunkRoot>();
                root.Configure(
                    dimension.Id,
                    world.Id,
                    chunk.Id,
                    chunk.BlockoutArchetype,
                    dimension.ChunkSpanMeters);
                ConfigureValidPhysicalGround(
                    rootObject,
                    snapshot,
                    chunk,
                    dimension.ChunkSpanMeters);

                WorldChunkSceneReadiness readiness =
                    WorldChunkSceneReadinessValidator.Evaluate(
                        scene,
                        snapshot,
                        chunk);

                Assert.That(readiness.IsReady, Is.False);
                Assert.That(
                    readiness.FailureCode,
                    Is.EqualTo(WorldChunkLoadFailureCodes.NavigationMissing));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ConfigureValidPhysicalGround(
            GameObject chunkRoot,
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk,
            float chunkSpanMeters)
        {
            BoxCollider ground = chunkRoot.AddComponent<BoxCollider>();
            ground.center = new Vector3(0f, -1f, 0f);
            ground.size = new Vector3(chunkSpanMeters, 2f, chunkSpanMeters);
            Physics.SyncTransforms();

            var edges = new[]
            {
                ContinuousEdge(
                    WorldChunkEdge.North,
                    0,
                    1,
                    ground,
                    snapshot,
                    chunk),
                ContinuousEdge(
                    WorldChunkEdge.East,
                    1,
                    0,
                    ground,
                    snapshot,
                    chunk),
                ContinuousEdge(
                    WorldChunkEdge.South,
                    0,
                    -1,
                    ground,
                    snapshot,
                    chunk),
                ContinuousEdge(
                    WorldChunkEdge.West,
                    -1,
                    0,
                    ground,
                    snapshot,
                    chunk)
            };
            WorldChunkPhysicalGroundAuthority authority =
                chunkRoot.AddComponent<WorldChunkPhysicalGroundAuthority>();
            authority.Configure(
                WorldChunkGroundSourceKind.SolidColliderAssembly,
                "test-reviewed-solid-ground-v1",
                new[] { ground },
                edges);
        }

        private static WorldChunkEdgeSafetyBinding ContinuousEdge(
            WorldChunkEdge edge,
            int deltaX,
            int deltaZ,
            Collider ground,
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk)
        {
            string neighborId = chunk.NeighborIds
                .Select(snapshot.GetChunk)
                .Where(value => value != null)
                .Single(value =>
                    value.GridX - chunk.GridX == deltaX &&
                    value.GridZ - chunk.GridZ == deltaZ)
                .Id;
            return new WorldChunkEdgeSafetyBinding(
                edge,
                WorldChunkEdgeSafetyMode.ContinuousNeighbor,
                neighborId,
                ground,
                "test-reviewed-continuous-seam-v1");
        }

        private static WorldStreamingSnapshot AcceptedSnapshot()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_streaming_catalog.json"));
            WorldStreamingLoadResult result = WorldStreamingCatalogLoader.Validate(bytes);
            Assert.That(
                result.Status,
                Is.EqualTo(WorldStreamingLoadStatus.Accepted),
                string.Join("\n", result.Diagnostics.Select(value => value.Fingerprint)));
            return result.Snapshot;
        }

        private static async Task AwaitBounded(
            Task task,
            string operation)
        {
            if (task == null)
            {
                Assert.Fail(operation + " did not return a task.");
            }

            Task timeout = Task.Delay(AsyncTimeoutMilliseconds);
            Task completed = await Task.WhenAny(task, timeout);
            Assert.That(
                completed,
                Is.SameAs(task),
                operation + " exceeded the bounded async test timeout.");
            await task;
        }

        private static async Task<TException> ExpectExceptionBounded<TException>(
            Task task,
            string operation)
            where TException : Exception
        {
            if (task == null)
            {
                Assert.Fail(operation + " did not return a task.");
            }

            Task timeout = Task.Delay(AsyncTimeoutMilliseconds);
            Task completed = await Task.WhenAny(task, timeout);
            Assert.That(
                completed,
                Is.SameAs(task),
                operation + " exceeded the bounded async test timeout.");
            try
            {
                await task;
            }
            catch (TException error)
            {
                return error;
            }
            catch (Exception error)
            {
                Assert.Fail(
                    operation + " threw " + error.GetType().Name +
                    " instead of " + typeof(TException).Name + ".");
            }

            Assert.Fail(
                operation + " completed without the expected " +
                typeof(TException).Name + ".");
            return null;
        }

        private static async Task WaitUntil(
            Func<bool> predicate,
            string operation)
        {
            Task timeout = Task.Delay(AsyncTimeoutMilliseconds);
            while (!predicate() && !timeout.IsCompleted)
            {
                await Task.Yield();
            }
            Assert.That(
                predicate(),
                Is.True,
                operation + " exceeded the bounded async test timeout.");
        }

        private sealed class ControllableSceneBackend : IWorldChunkSceneBackend
        {
            private readonly List<PendingLoad> pendingLoads =
                new List<PendingLoad>();

            internal int LoadCalls { get; private set; }
            internal int UnloadCalls { get; private set; }
            internal int UnloadFailuresRemaining { get; set; }
            internal bool FailNextRevealAfterMutation { get; set; }
            internal WorldChunkSceneReadiness Readiness { get; set; } =
                WorldChunkSceneReadiness.Ready();
            internal List<FakeSceneHandle> Handles { get; } =
                new List<FakeSceneHandle>();

            public Task<IWorldChunkSceneHandle> LoadAdditiveAsync(string scenePath)
            {
                LoadCalls++;
                var completion = new TaskCompletionSource<IWorldChunkSceneHandle>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                pendingLoads.Add(new PendingLoad(scenePath, completion));
                return completion.Task;
            }

            public Task UnloadAsync(IWorldChunkSceneHandle handle)
            {
                UnloadCalls++;
                if (UnloadFailuresRemaining > 0)
                {
                    UnloadFailuresRemaining--;
                    throw new InvalidOperationException("Injected unload failure.");
                }

                ((FakeSceneHandle)handle).IsLoaded = false;
                return Task.CompletedTask;
            }

            public WorldChunkSceneReadiness ValidateReadiness(
                IWorldChunkSceneHandle handle,
                WorldStreamingSnapshot snapshot,
                WorldChunkDefinition chunk)
            {
                return Readiness;
            }

            public void SetSpatialVisibility(
                IWorldChunkSceneHandle handle,
                bool visible)
            {
                var fake = (FakeSceneHandle)handle;
                fake.Visible = visible;
                if (visible && FailNextRevealAfterMutation)
                {
                    FailNextRevealAfterMutation = false;
                    throw new InvalidOperationException("Injected reveal failure.");
                }
            }

            internal void CompleteAllLoads()
            {
                foreach (PendingLoad pending in pendingLoads.ToArray())
                {
                    var handle = new FakeSceneHandle(pending.ScenePath);
                    Handles.Add(handle);
                    pending.Completion.TrySetResult(handle);
                    pendingLoads.Remove(pending);
                }
            }

            private sealed class PendingLoad
            {
                internal PendingLoad(
                    string scenePath,
                    TaskCompletionSource<IWorldChunkSceneHandle> completion)
                {
                    ScenePath = scenePath;
                    Completion = completion;
                }

                internal string ScenePath { get; }
                internal TaskCompletionSource<IWorldChunkSceneHandle> Completion { get; }
            }
        }

        private sealed class FakeSceneHandle : IWorldChunkSceneHandle
        {
            internal FakeSceneHandle(string scenePath)
            {
                ScenePath = scenePath;
                IsLoaded = true;
                Visible = true;
            }

            public string ScenePath { get; }
            public bool IsLoaded { get; internal set; }
            internal bool Visible { get; set; }
        }
    }
}
