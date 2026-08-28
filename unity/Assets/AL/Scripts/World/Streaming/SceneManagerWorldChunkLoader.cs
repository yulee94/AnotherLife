using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AL.Data.Catalogs.WorldStreaming;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.World.Streaming
{
    public static class WorldChunkLoadFailureCodes
    {
        public const string UnknownCatalogChunk = "AL-WORLD-CHUNK-UNKNOWN";
        public const string CatalogChunkMismatch = "AL-WORLD-CHUNK-CATALOG-MISMATCH";
        public const string SceneUnavailable = "AL-WORLD-CHUNK-SCENE-UNAVAILABLE";
        public const string SceneLoadFailed = "AL-WORLD-CHUNK-SCENE-LOAD-FAILED";
        public const string SceneHandleInvalid = "AL-WORLD-CHUNK-SCENE-HANDLE-INVALID";
        public const string ChunkRootInvalid = "AL-WORLD-CHUNK-ROOT-INVALID";
        public const string NavigationMissing = "AL-WORLD-CHUNK-NAVIGATION-MISSING";
        public const string NavigationNotRegistered =
            "AL-WORLD-CHUNK-NAVIGATION-NOT-REGISTERED";
        public const string PhysicalGroundAuthorityMissing =
            "AL-WORLD-CHUNK-PHYSICAL-GROUND-AUTHORITY-MISSING";
        public const string GroundColliderMissing =
            "AL-WORLD-CHUNK-GROUND-COLLIDER-MISSING";
        public const string GroundColliderDisabled =
            "AL-WORLD-CHUNK-GROUND-COLLIDER-DISABLED";
        public const string GroundColliderUnbound =
            "AL-WORLD-CHUNK-GROUND-COLLIDER-UNBOUND";
        public const string GroundColliderInvalid =
            "AL-WORLD-CHUNK-GROUND-COLLIDER-INVALID";
        public const string GroundRenderMeshReused =
            "AL-WORLD-CHUNK-GROUND-RENDER-MESH-REUSED";
        public const string GroundReviewMissing =
            "AL-WORLD-CHUNK-GROUND-REVIEW-MISSING";
        public const string ChunkEdgeUnsafe =
            "AL-WORLD-CHUNK-EDGE-UNSAFE";
        public const string ChunkSeamContinuityUnproven =
            "AL-WORLD-CHUNK-SEAM-CONTINUITY-UNPROVEN";
        public const string SceneUnloadFailed = "AL-WORLD-CHUNK-SCENE-UNLOAD-FAILED";
        public const string VisibilityFailed = "AL-WORLD-CHUNK-VISIBILITY-FAILED";
    }

    public sealed class WorldChunkLoadException : InvalidOperationException
    {
        public WorldChunkLoadException(
            string failureCode,
            string chunkId,
            string message,
            Exception innerException = null)
            : base(message, innerException)
        {
            FailureCode = failureCode ?? string.Empty;
            ChunkId = chunkId ?? string.Empty;
        }

        public string FailureCode { get; }
        public string ChunkId { get; }
    }

    public sealed class WorldChunkSceneReadiness
    {
        private WorldChunkSceneReadiness(
            bool isReady,
            string failureCode,
            string message)
        {
            IsReady = isReady;
            FailureCode = failureCode ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool IsReady { get; }
        public string FailureCode { get; }
        public string Message { get; }

        public static WorldChunkSceneReadiness Ready()
        {
            return new WorldChunkSceneReadiness(true, string.Empty, string.Empty);
        }

        public static WorldChunkSceneReadiness Rejected(
            string failureCode,
            string message)
        {
            return new WorldChunkSceneReadiness(false, failureCode, message);
        }
    }

    public interface IWorldChunkSceneHandle
    {
        string ScenePath { get; }
        bool IsLoaded { get; }
    }

    public interface IWorldChunkSceneBackend
    {
        Task<IWorldChunkSceneHandle> LoadAdditiveAsync(string scenePath);

        Task UnloadAsync(IWorldChunkSceneHandle handle);

        WorldChunkSceneReadiness ValidateReadiness(
            IWorldChunkSceneHandle handle,
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk);

        void SetSpatialVisibility(
            IWorldChunkSceneHandle handle,
            bool visible);
    }

    public static class WorldChunkSceneReadinessValidator
    {
        public static WorldChunkSceneReadiness Evaluate(
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
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return WorldChunkSceneReadiness.Rejected(
                    WorldChunkLoadFailureCodes.SceneHandleInvalid,
                    "The additive scene handle is not loaded.");
            }

            WorldInstanceDefinition world = snapshot.GetWorld(chunk.WorldId);
            WorldDimensionDefinition dimension = world == null
                ? null
                : snapshot.GetDimension(world.DimensionId);
            if (world == null || dimension == null)
            {
                return WorldChunkSceneReadiness.Rejected(
                    WorldChunkLoadFailureCodes.CatalogChunkMismatch,
                    "The chunk does not resolve to a catalog world and dimension.");
            }

            GameObject[] sceneRoots = scene.GetRootGameObjects();
            WorldChunkRoot[] chunkRoots = sceneRoots
                .SelectMany(value =>
                    value.GetComponentsInChildren<WorldChunkRoot>(true))
                .ToArray();
            if (chunkRoots.Length != 1 ||
                !MatchesCatalog(chunkRoots[0], dimension, world, chunk))
            {
                return WorldChunkSceneReadiness.Rejected(
                    WorldChunkLoadFailureCodes.ChunkRootInvalid,
                    "The additive scene must contain exactly one catalog-matching WorldChunkRoot.");
            }

            WorldChunkPhysicalGroundReadiness physicalGround =
                WorldChunkPhysicalGroundValidator.Evaluate(
                    scene,
                    snapshot,
                    chunk,
                    chunkRoots[0]);
            if (!physicalGround.IsReady)
            {
                WorldChunkPhysicalGroundDiagnostic first =
                    physicalGround.Diagnostics[0];
                return WorldChunkSceneReadiness.Rejected(
                    first.Code,
                    first.Message);
            }

            WorldChunkNavigationData[] navigationSources = chunkRoots[0]
                .GetComponentsInChildren<WorldChunkNavigationData>(true);
            if (navigationSources.Length == 0 ||
                navigationSources.Any(value =>
                    value == null || !value.HasBakedNavigationData))
            {
                return WorldChunkSceneReadiness.Rejected(
                    WorldChunkLoadFailureCodes.NavigationMissing,
                    "The chunk has no complete serialized baked NavMeshData ownership.");
            }
            if (navigationSources.Any(value =>
                    !value.isActiveAndEnabled || !value.IsRegistered))
            {
                return WorldChunkSceneReadiness.Rejected(
                    WorldChunkLoadFailureCodes.NavigationNotRegistered,
                    "The chunk's baked NavMeshData did not register when the scene activated.");
            }

            return WorldChunkSceneReadiness.Ready();
        }

        private static bool MatchesCatalog(
            WorldChunkRoot root,
            WorldDimensionDefinition dimension,
            WorldInstanceDefinition world,
            WorldChunkDefinition chunk)
        {
            if (root == null)
            {
                return false;
            }

            Vector3 expectedOrigin = new Vector3(
                chunk.GridX * dimension.ChunkSpanMeters,
                0f,
                chunk.GridZ * dimension.ChunkSpanMeters);
            return string.Equals(
                       root.DimensionId,
                       dimension.Id,
                       StringComparison.Ordinal) &&
                string.Equals(root.WorldId, world.Id, StringComparison.Ordinal) &&
                string.Equals(root.ChunkId, chunk.Id, StringComparison.Ordinal) &&
                string.Equals(
                    root.BlockoutArchetype,
                    chunk.BlockoutArchetype,
                    StringComparison.Ordinal) &&
                root.ProvisionalCoordinates &&
                Mathf.Abs(root.ChunkSpanMeters - dimension.ChunkSpanMeters) <= 0.001f &&
                Vector3.Distance(root.transform.position, expectedOrigin) <= 0.001f;
        }
    }

    public sealed class SceneManagerWorldChunkSceneBackend : IWorldChunkSceneBackend
    {
        public async Task<IWorldChunkSceneHandle> LoadAdditiveAsync(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath) ||
                !Application.CanStreamedLevelBeLoaded(scenePath))
            {
                throw new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.SceneUnavailable,
                    string.Empty,
                    "The catalog scene is not included in the player Build Settings: " +
                    (scenePath ?? string.Empty));
            }

            if (FindLoadedSceneByPath(scenePath).IsValid())
            {
                throw new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.SceneLoadFailed,
                    string.Empty,
                    "The catalog scene is already loaded outside this loader's ownership: " +
                    scenePath);
            }

            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(
                    scenePath,
                    LoadSceneMode.Additive);
            }
            catch (Exception error)
            {
                throw new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.SceneLoadFailed,
                    string.Empty,
                    "Unity could not start the additive scene load: " + scenePath,
                    error);
            }

            if (operation == null)
            {
                throw new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.SceneLoadFailed,
                    string.Empty,
                    "Unity did not return an additive scene operation: " + scenePath);
            }

            await AwaitOperationAsync(operation);
            Scene scene = FindLoadedSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.SceneHandleInvalid,
                    string.Empty,
                    "Unity completed the additive load without a matching loaded scene: " +
                    scenePath);
            }

            return new SceneManagerWorldChunkSceneHandle(scene, scenePath);
        }

        public async Task UnloadAsync(IWorldChunkSceneHandle handle)
        {
            SceneManagerWorldChunkSceneHandle sceneHandle = RequireHandle(handle);
            if (!sceneHandle.IsLoaded)
            {
                return;
            }

            AsyncOperation operation;
            try
            {
                operation = SceneManager.UnloadSceneAsync(sceneHandle.Scene);
            }
            catch (Exception error)
            {
                throw new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.SceneUnloadFailed,
                    string.Empty,
                    "Unity could not start the additive scene unload: " +
                    sceneHandle.ScenePath,
                    error);
            }

            if (operation == null)
            {
                throw new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.SceneUnloadFailed,
                    string.Empty,
                    "Unity did not return an additive scene unload operation: " +
                    sceneHandle.ScenePath);
            }

            await AwaitOperationAsync(operation);
        }

        public WorldChunkSceneReadiness ValidateReadiness(
            IWorldChunkSceneHandle handle,
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk)
        {
            SceneManagerWorldChunkSceneHandle sceneHandle = RequireHandle(handle);
            if (!string.Equals(
                    sceneHandle.ScenePath,
                    chunk.ScenePath,
                    StringComparison.Ordinal))
            {
                return WorldChunkSceneReadiness.Rejected(
                    WorldChunkLoadFailureCodes.SceneHandleInvalid,
                    "The loaded scene path does not match the catalog chunk path.");
            }

            return WorldChunkSceneReadinessValidator.Evaluate(
                sceneHandle.Scene,
                snapshot,
                chunk);
        }

        public void SetSpatialVisibility(
            IWorldChunkSceneHandle handle,
            bool visible)
        {
            SceneManagerWorldChunkSceneHandle sceneHandle = RequireHandle(handle);
            if (!sceneHandle.IsLoaded)
            {
                if (visible)
                {
                    throw new InvalidOperationException(
                        "Cannot reveal an unloaded chunk scene.");
                }
                return;
            }

            Exception firstFailure = null;
            foreach (GameObject root in sceneHandle.Scene.GetRootGameObjects())
            {
                try
                {
                    root.SetActive(visible);
                }
                catch (Exception error)
                {
                    firstFailure = firstFailure ?? error;
                }
            }

            if (firstFailure != null)
            {
                throw new InvalidOperationException(
                    "One or more chunk roots could not change spatial visibility.",
                    firstFailure);
            }
        }

        private static SceneManagerWorldChunkSceneHandle RequireHandle(
            IWorldChunkSceneHandle handle)
        {
            if (!(handle is SceneManagerWorldChunkSceneHandle sceneHandle))
            {
                throw new ArgumentException(
                    "The handle was not created by the SceneManager backend.",
                    nameof(handle));
            }
            return sceneHandle;
        }

        private static Scene FindLoadedSceneByPath(string scenePath)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded &&
                    string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                {
                    return scene;
                }
            }
            return default;
        }

        private static Task AwaitOperationAsync(AsyncOperation operation)
        {
            if (operation.isDone)
            {
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            operation.completed += _ => completion.TrySetResult(true);
            return completion.Task;
        }

        private sealed class SceneManagerWorldChunkSceneHandle :
            IWorldChunkSceneHandle
        {
            internal SceneManagerWorldChunkSceneHandle(
                Scene scene,
                string scenePath)
            {
                Scene = scene;
                ScenePath = scenePath;
            }

            internal Scene Scene { get; }
            public string ScenePath { get; }
            public bool IsLoaded => Scene.IsValid() && Scene.isLoaded;
        }
    }

    public sealed class SceneManagerWorldChunkLoader : IWorldChunkLoader
    {
        private readonly WorldStreamingSnapshot snapshot;
        private readonly IWorldChunkSceneBackend backend;
        private readonly object stateLock = new object();
        private readonly Dictionary<string, ChunkEntry> entries =
            new Dictionary<string, ChunkEntry>(StringComparer.Ordinal);

        private bool spatialVisible = true;

        public SceneManagerWorldChunkLoader(WorldStreamingSnapshot snapshot)
            : this(snapshot, new SceneManagerWorldChunkSceneBackend())
        {
        }

        public SceneManagerWorldChunkLoader(
            WorldStreamingSnapshot snapshot,
            IWorldChunkSceneBackend backend)
        {
            this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public IReadOnlyCollection<string> LoadedChunkIds
        {
            get
            {
                lock (stateLock)
                {
                    return Array.AsReadOnly(entries.Values
                        .Where(value => value.IsResident)
                        .Select(value => value.Chunk.Id)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray());
                }
            }
        }

        public Task SetSpatialVisibilityAsync(
            bool visible,
            CancellationToken cancellationToken)
        {
            Exception failure = null;
            bool canceled;
            lock (stateLock)
            {
                spatialVisible = false;
                canceled = cancellationToken.IsCancellationRequested;
                if (!visible || canceled)
                {
                    failure = HideEveryResidentLocked();
                }
                else
                {
                    foreach (ChunkEntry entry in entries.Values
                                 .Where(value => value.IsResident && value.IsReady)
                                 .OrderBy(value => value.Chunk.Id, StringComparer.Ordinal))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            canceled = true;
                            break;
                        }

                        try
                        {
                            backend.SetSpatialVisibility(entry.Handle, true);
                            WorldChunkSceneReadiness readiness =
                                backend.ValidateReadiness(
                                    entry.Handle,
                                    snapshot,
                                    entry.Chunk);
                            if (readiness == null || !readiness.IsReady)
                            {
                                throw ReadinessFailure(entry.Chunk, readiness);
                            }
                        }
                        catch (Exception error)
                        {
                            failure = failure ?? error;
                            break;
                        }
                    }

                    canceled |= cancellationToken.IsCancellationRequested;
                    if (failure != null || canceled)
                    {
                        Exception hideFailure = HideEveryResidentLocked();
                        failure = Combine(failure, hideFailure);
                    }
                    else
                    {
                        spatialVisible = true;
                    }
                }
            }

            if (failure != null)
            {
                return Task.FromException(new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.VisibilityFailed,
                    string.Empty,
                    "Spatial chunk visibility could not be applied safely.",
                    failure));
            }
            if (canceled)
            {
                return Task.FromCanceled(cancellationToken);
            }
            return Task.CompletedTask;
        }

        public async Task LoadAsync(
            WorldChunkDefinition chunk,
            CancellationToken cancellationToken)
        {
            WorldChunkDefinition canonical = RequireCanonicalChunk(chunk);
            cancellationToken.ThrowIfCancellationRequested();

            ChunkEntry entry;
            Task<IWorldChunkSceneHandle> readyTask;
            lock (stateLock)
            {
                if (entries.TryGetValue(canonical.Id, out entry))
                {
                    if (entry.IsUnloading)
                    {
                        readyTask = null;
                    }
                    else
                    {
                        entry.OwnerCount++;
                        readyTask = entry.Ready.Task;
                    }
                }
                else
                {
                    entry = new ChunkEntry(canonical);
                    entries.Add(canonical.Id, entry);
                    entry.OwnerCount = 1;
                    readyTask = entry.Ready.Task;
                    _ = CompleteLoadAsync(entry);
                }
            }

            if (readyTask == null)
            {
                Task unloadTask;
                lock (stateLock)
                {
                    unloadTask = entry.UnloadCompletion.Task;
                }
                await AwaitWithCancellation(unloadTask, cancellationToken);
                await LoadAsync(canonical, cancellationToken);
                return;
            }

            try
            {
                await AwaitWithCancellation(readyTask, cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                ReleaseCanceledOwner(entry);
                throw;
            }
        }

        public async Task UnloadAsync(
            WorldChunkDefinition chunk,
            CancellationToken cancellationToken)
        {
            WorldChunkDefinition canonical = RequireCanonicalChunk(chunk);
            cancellationToken.ThrowIfCancellationRequested();

            while (true)
            {
                Task pendingTask = null;
                Task unloadTask = null;
                lock (stateLock)
                {
                    if (!entries.TryGetValue(canonical.Id, out ChunkEntry entry))
                    {
                        return;
                    }

                    if (entry.IsUnloading)
                    {
                        unloadTask = entry.UnloadCompletion.Task;
                    }
                    else if (!entry.IsResident)
                    {
                        if (entry.OwnerCount > 0)
                        {
                            entry.OwnerCount--;
                        }
                        if (entry.OwnerCount > 0)
                        {
                            return;
                        }
                        pendingTask = entry.Ready.Task;
                    }
                    else
                    {
                        if (entry.OwnerCount > 0)
                        {
                            entry.OwnerCount--;
                        }
                        if (entry.OwnerCount > 0)
                        {
                            return;
                        }
                        unloadTask = BeginUnloadLocked(entry);
                    }
                }

                if (pendingTask != null)
                {
                    try
                    {
                        await AwaitWithCancellation(
                            pendingTask,
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // A failed load performs its own cleanup. Loop once more so an
                        // unload-cleanup failure can be retried from actual residency.
                    }
                    continue;
                }

                if (unloadTask != null)
                {
                    await unloadTask;
                }
                return;
            }
        }

        private async Task CompleteLoadAsync(ChunkEntry entry)
        {
            IWorldChunkSceneHandle handle = null;
            try
            {
                handle = await backend.LoadAdditiveAsync(entry.Chunk.ScenePath);
                if (handle == null || !handle.IsLoaded)
                {
                    throw new WorldChunkLoadException(
                        WorldChunkLoadFailureCodes.SceneHandleInvalid,
                        entry.Chunk.Id,
                        "The scene backend returned no loaded handle.");
                }

                lock (stateLock)
                {
                    entry.Handle = handle;
                    entry.IsResident = true;
                }

                WorldChunkSceneReadiness readiness = backend.ValidateReadiness(
                    handle,
                    snapshot,
                    entry.Chunk);
                if (readiness == null || !readiness.IsReady)
                {
                    throw ReadinessFailure(entry.Chunk, readiness);
                }

                bool releaseUnowned;
                lock (stateLock)
                {
                    backend.SetSpatialVisibility(handle, spatialVisible);
                    entry.IsReady = true;
                    entry.Ready.TrySetResult(handle);
                    releaseUnowned = entry.OwnerCount == 0;
                }

                if (releaseUnowned)
                {
                    BeginUnloadIfUnowned(entry);
                }
            }
            catch (Exception error)
            {
                WorldChunkLoadException failure = NormalizeLoadFailure(
                    entry.Chunk,
                    error);
                Task cleanupTask = null;
                Exception hideFailure = null;
                lock (stateLock)
                {
                    if (handle != null && handle.IsLoaded)
                    {
                        entry.Handle = handle;
                        entry.IsResident = true;
                    }
                    entry.OwnerCount = 0;
                    entry.IsReady = false;
                    if (entry.IsResident)
                    {
                        try
                        {
                            backend.SetSpatialVisibility(entry.Handle, false);
                        }
                        catch (Exception visibilityError)
                        {
                            hideFailure = visibilityError;
                        }
                        cleanupTask = BeginUnloadLocked(entry);
                    }
                    else
                    {
                        entries.Remove(entry.Chunk.Id);
                    }
                }

                Exception cleanupFailure = null;
                if (cleanupTask != null)
                {
                    try
                    {
                        await cleanupTask;
                    }
                    catch (Exception unloadError)
                    {
                        cleanupFailure = unloadError;
                    }
                }

                Exception reconciliationFailure = Combine(
                    hideFailure,
                    cleanupFailure);
                if (reconciliationFailure != null)
                {
                    failure = new WorldChunkLoadException(
                        failure.FailureCode,
                        entry.Chunk.Id,
                        failure.Message +
                        " The failed physical load could not be fully reconciled.",
                        new AggregateException(failure, reconciliationFailure));
                }
                entry.Ready.TrySetException(failure);
            }
        }

        private void ReleaseCanceledOwner(ChunkEntry entry)
        {
            bool releaseUnowned = false;
            lock (stateLock)
            {
                if (!entries.TryGetValue(entry.Chunk.Id, out ChunkEntry current) ||
                    !ReferenceEquals(current, entry))
                {
                    return;
                }
                if (entry.OwnerCount > 0)
                {
                    entry.OwnerCount--;
                }
                releaseUnowned = entry.OwnerCount == 0 && entry.IsResident;
            }

            if (releaseUnowned)
            {
                BeginUnloadIfUnowned(entry);
            }
        }

        private void BeginUnloadIfUnowned(ChunkEntry entry)
        {
            lock (stateLock)
            {
                BeginUnloadLocked(entry);
            }
        }

        private Task BeginUnloadLocked(ChunkEntry entry)
        {
            if (entry == null ||
                entry.OwnerCount != 0 ||
                !entry.IsResident)
            {
                return null;
            }
            if (entry.IsUnloading)
            {
                return entry.UnloadCompletion.Task;
            }

            entry.IsUnloading = true;
            entry.UnloadCompletion = NewCompletion<bool>();
            _ = CompleteUnloadAsync(entry, entry.UnloadCompletion);
            return entry.UnloadCompletion.Task;
        }

        private async Task CompleteUnloadAsync(
            ChunkEntry entry,
            TaskCompletionSource<bool> completion)
        {
            try
            {
                await backend.UnloadAsync(entry.Handle);
                lock (stateLock)
                {
                    entry.IsResident = false;
                    entry.IsReady = false;
                    entry.IsUnloading = false;
                    entry.Handle = null;
                    if (entries.TryGetValue(
                            entry.Chunk.Id,
                            out ChunkEntry current) &&
                        ReferenceEquals(current, entry))
                    {
                        entries.Remove(entry.Chunk.Id);
                    }
                }
                completion.TrySetResult(true);
            }
            catch (Exception error)
            {
                Exception hideFailure = null;
                lock (stateLock)
                {
                    entry.IsUnloading = false;
                    try
                    {
                        backend.SetSpatialVisibility(entry.Handle, false);
                    }
                    catch (Exception visibilityError)
                    {
                        hideFailure = visibilityError;
                    }
                }

                Exception combined = Combine(error, hideFailure);
                completion.TrySetException(new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.SceneUnloadFailed,
                    entry.Chunk.Id,
                    "The chunk remains reported as resident because its scene could not be unloaded.",
                    combined));
            }
        }

        private Exception HideEveryResidentLocked()
        {
            Exception failure = null;
            foreach (ChunkEntry entry in entries.Values
                         .Where(value => value.IsResident)
                         .OrderBy(value => value.Chunk.Id, StringComparer.Ordinal))
            {
                try
                {
                    backend.SetSpatialVisibility(entry.Handle, false);
                }
                catch (Exception error)
                {
                    failure = Combine(failure, error);
                }
            }
            return failure;
        }

        private WorldChunkDefinition RequireCanonicalChunk(
            WorldChunkDefinition chunk)
        {
            if (chunk == null || string.IsNullOrWhiteSpace(chunk.Id))
            {
                throw new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.UnknownCatalogChunk,
                    string.Empty,
                    "A catalog chunk is required.");
            }

            WorldChunkDefinition canonical = snapshot.GetChunk(chunk.Id);
            if (canonical == null)
            {
                throw new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.UnknownCatalogChunk,
                    chunk.Id,
                    "The chunk is not present in the accepted world streaming catalog.");
            }
            if (!string.Equals(
                    canonical.WorldId,
                    chunk.WorldId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    canonical.ScenePath,
                    chunk.ScenePath,
                    StringComparison.Ordinal))
            {
                throw new WorldChunkLoadException(
                    WorldChunkLoadFailureCodes.CatalogChunkMismatch,
                    chunk.Id,
                    "The chunk definition does not match the accepted catalog snapshot.");
            }
            return canonical;
        }

        private static WorldChunkLoadException ReadinessFailure(
            WorldChunkDefinition chunk,
            WorldChunkSceneReadiness readiness)
        {
            return new WorldChunkLoadException(
                readiness?.FailureCode ??
                    WorldChunkLoadFailureCodes.SceneHandleInvalid,
                chunk.Id,
                readiness?.Message ?? "The scene backend returned no readiness result.");
        }

        private static WorldChunkLoadException NormalizeLoadFailure(
            WorldChunkDefinition chunk,
            Exception error)
        {
            if (error is WorldChunkLoadException chunkError)
            {
                if (string.Equals(
                        chunkError.ChunkId,
                        chunk.Id,
                        StringComparison.Ordinal))
                {
                    return chunkError;
                }
                return new WorldChunkLoadException(
                    chunkError.FailureCode,
                    chunk.Id,
                    chunkError.Message,
                    chunkError);
            }

            return new WorldChunkLoadException(
                WorldChunkLoadFailureCodes.SceneLoadFailed,
                chunk.Id,
                "The additive chunk scene did not reach a ready state.",
                error);
        }

        private static async Task<T> AwaitWithCancellation<T>(
            Task<T> task,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                return await task;
            }
            cancellationToken.ThrowIfCancellationRequested();

            var canceled = NewCompletion<bool>();
            using (cancellationToken.Register(() => canceled.TrySetResult(true)))
            {
                Task completed = await Task.WhenAny(task, canceled.Task);
                if (completed != task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            return await task;
        }

        private static async Task AwaitWithCancellation(
            Task task,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                await task;
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();

            var canceled = NewCompletion<bool>();
            using (cancellationToken.Register(() => canceled.TrySetResult(true)))
            {
                Task completed = await Task.WhenAny(task, canceled.Task);
                if (completed != task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            await task;
        }

        private static TaskCompletionSource<T> NewCompletion<T>()
        {
            return new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static Exception Combine(Exception first, Exception second)
        {
            if (first == null)
            {
                return second;
            }
            if (second == null)
            {
                return first;
            }
            return new AggregateException(first, second);
        }

        private sealed class ChunkEntry
        {
            internal ChunkEntry(WorldChunkDefinition chunk)
            {
                Chunk = chunk;
                Ready = NewCompletion<IWorldChunkSceneHandle>();
                UnloadCompletion = NewCompletion<bool>();
            }

            internal WorldChunkDefinition Chunk { get; }
            internal TaskCompletionSource<IWorldChunkSceneHandle> Ready { get; }
            internal TaskCompletionSource<bool> UnloadCompletion { get; set; }
            internal IWorldChunkSceneHandle Handle { get; set; }
            internal int OwnerCount { get; set; }
            internal bool IsResident { get; set; }
            internal bool IsReady { get; set; }
            internal bool IsUnloading { get; set; }
        }
    }
}
