using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.RealmWar.Territories.Runtime;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AL.Terrestrials.Slagfall
{
    [DisallowMultipleComponent]
    public sealed class SlagfallDeviceEvidenceRunner : MonoBehaviour
    {
        private const double MemoryPlateauToleranceBytes =
            4d * 1024d * 1024d;
        private const int LifecycleCycles = 12;
        private const int LifecycleWarmupCycles = 2;

        [SerializeField] private SlagfallRepresentativeSlice _slice;
        [SerializeField] private SlagfallEvidenceLane _lane =
            SlagfallEvidenceLane.MobileLow;
        [SerializeField, Min(SlagfallEvidenceContract.MinimumRunSeconds)]
        private float _durationSeconds =
            SlagfallEvidenceContract.MinimumRunSeconds;
        [SerializeField, Min(1f)] private float _counterIntervalSeconds = 5f;
        [SerializeField, Min(5f)] private float _checkpointIntervalSeconds =
            60f;
        [SerializeField] private bool _effectsOff;
        [SerializeField] private bool _reducedMotion;
        [SerializeField] private bool _runInEditor;
        [SerializeField] private bool _quitPlayerWhenComplete = true;

        private readonly FrameTiming[] _frameTimings =
            new FrameTiming[1];
        private readonly List<CounterRecorder> _recorders =
            new List<CounterRecorder>();

        private SlagfallDeviceEvidenceReport _report;
        private SlagfallEvidenceAccumulator _accumulator;
        private string _checkpointPath;
        private string _completePath;
        private double _startedRealtime;
        private double _nextCounterTime;
        private double _nextCheckpointTime;
        private int _minimumRepresentedUsers =
            SlagfallEvidenceContract.RequiredRepresentedUsers;
        private int _frameSamplesToSkip;
        private int _excludedEvidenceOverheadFrameCount;
        private int _previousTargetFrameRate;
        private bool _targetFrameRateChanged;
        private bool _running;
        private bool _finishing;

        public SlagfallEvidenceLane Lane => _lane;
        public float DurationSeconds => _durationSeconds;
        public bool EffectsOff => _effectsOff;
        public bool ReducedMotion => _reducedMotion;
        public bool RunInEditor => _runInEditor;
        public string CheckpointPath => _checkpointPath;
        public string CompletePath => _completePath;

        public void Configure(
            SlagfallRepresentativeSlice slice,
            SlagfallEvidenceLane lane,
            float durationSeconds,
            bool effectsOff,
            bool reducedMotion,
            bool runInEditor = false,
            bool quitPlayerWhenComplete = true)
        {
            if (slice == null)
            {
                throw new ArgumentNullException(nameof(slice));
            }

            if (float.IsNaN(durationSeconds) ||
                float.IsInfinity(durationSeconds) ||
                durationSeconds <
                    SlagfallEvidenceContract.MinimumRunSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    "Device evidence runs must last at least 30 minutes.");
            }

            _slice = slice;
            _lane = lane;
            _durationSeconds = durationSeconds;
            _effectsOff = effectsOff;
            _reducedMotion = reducedMotion;
            _runInEditor = runInEditor;
            _quitPlayerWhenComplete = quitPlayerWhenComplete;
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!_runInEditor)
            {
                return;
            }
#endif
            Begin();
        }

        private void Update()
        {
            if (!_running || _finishing)
            {
                return;
            }

            double elapsed =
                Time.realtimeSinceStartupAsDouble - _startedRealtime;
            if (_frameSamplesToSkip > 0)
            {
                _frameSamplesToSkip--;
                _excludedEvidenceOverheadFrameCount++;
            }
            else
            {
                CaptureFrame(elapsed);
            }
            CaptureRepresentation();

            if (elapsed >= _nextCounterTime)
            {
                CaptureCounters();
                _nextCounterTime = elapsed + _counterIntervalSeconds;
            }

            if (elapsed >= _nextCheckpointTime)
            {
                WriteCheckpoint(false);
                _nextCheckpointTime =
                    elapsed + _checkpointIntervalSeconds;
            }

            if (elapsed >= _durationSeconds)
            {
                _finishing = true;
                StartCoroutine(FinalizeEvidence());
            }
        }

        private void OnDestroy()
        {
            Application.lowMemory -= HandleLowMemory;
            Application.focusChanged -= HandleFocusChanged;
            Application.logMessageReceived -= HandleLog;
            Application.quitting -= HandleQuitting;
            RestoreTargetFrameRate();
            DisposeRecorders();
        }

        private void OnApplicationPause(bool paused)
        {
            if (_running && paused && _report != null)
            {
                _report.applicationPauseCount++;
                WriteCheckpoint(false);
            }
        }

        private void Begin()
        {
            if (_running)
            {
                return;
            }

            if (_slice == null)
            {
                throw new InvalidOperationException(
                    "Slagfall device evidence runner has no representative slice.");
            }

            if (_durationSeconds <
                SlagfallEvidenceContract.MinimumRunSeconds)
            {
                throw new InvalidOperationException(
                    "Slagfall device evidence cannot start below the 30-minute contract.");
            }

            _slice.Initialize();
            _slice.SetTargetFrameTimeMilliseconds(
                SlagfallEvidenceContract.TargetFrameTimeMilliseconds(
                    _lane));
            _slice.SetSyntheticCrowdActive(true);
            _slice.SetAccessibility(_effectsOff, _reducedMotion);
            _previousTargetFrameRate = Application.targetFrameRate;
            Application.targetFrameRate =
                SlagfallEvidenceContract.TargetFrameRate(_lane);
            _targetFrameRateChanged = true;

            _startedRealtime = Time.realtimeSinceStartupAsDouble;
            _nextCounterTime = _counterIntervalSeconds;
            _nextCheckpointTime = _checkpointIntervalSeconds;
            _frameSamplesToSkip = 0;
            _excludedEvidenceOverheadFrameCount = 0;
            _accumulator =
                new SlagfallEvidenceAccumulator(_durationSeconds);
            _report = CreateReport();
            PreparePaths();
            StartRecorders();

            Application.lowMemory += HandleLowMemory;
            Application.focusChanged += HandleFocusChanged;
            Application.logMessageReceived += HandleLog;
            Application.quitting += HandleQuitting;
            _running = true;

            CaptureCounters();
            CaptureRepresentation();
            WriteCheckpoint(false);
            Debug.Log(
                "SLAGFALL_EVIDENCE_STARTED " +
                $"run={_report.runId} lane={_report.evidenceLane} " +
                $"durationSeconds={_durationSeconds:0}");
        }

        private SlagfallDeviceEvidenceReport CreateReport()
        {
            string now = DateTime.UtcNow.ToString("O");
            var report = new SlagfallDeviceEvidenceReport
            {
                schemaVersion = SlagfallEvidenceContract.SchemaVersion,
                runId = Guid.NewGuid().ToString("N"),
                sourceVersion = SlagfallSourceAuthority.SourceVersion,
                evidenceLane = SlagfallEvidenceContract.StableId(_lane),
                startedUtc = now,
                lastCheckpointUtc = now,
                completedUtc = string.Empty,
                completed = false,
                productionScoringEligible = false,
                productVersion = Application.version,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                operatingSystem = SystemInfo.operatingSystem,
                deviceModel = SystemInfo.deviceModel,
                processorType = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceType =
                    SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceVersion =
                    SystemInfo.graphicsDeviceVersion,
                graphicsMemoryMegabytes =
                    SystemInfo.graphicsMemorySize,
                systemMemoryMegabytes = SystemInfo.systemMemorySize,
                resolution =
                    $"{Screen.width}x{Screen.height}@{Screen.currentResolution.refreshRateRatio.value:0.##}",
                qualityLevel = QualitySettings.GetQualityLevel(),
                targetFrameRate =
                    SlagfallEvidenceContract.TargetFrameRate(_lane),
                targetFrameTimeMilliseconds =
                    SlagfallEvidenceContract.TargetFrameTimeMilliseconds(
                        _lane),
                intendedDurationSeconds = _durationSeconds,
                registeredUserCount = _slice.SyntheticCrowd.Count,
                minimumRepresentedUserCount =
                    SlagfallEvidenceContract.RequiredRepresentedUsers,
                initialLoadLevel =
                    _slice.Controller.CurrentLevel.ToString(),
                effectsOff = _effectsOff,
                reducedMotion = _reducedMotion,
                coldReadySeconds = Time.realtimeSinceStartupAsDouble,
                streamingEvidenceBoundary =
                    "The isolated slice has no production streaming authority; lifecycle timings exercise only the authored direct-prefab prototype.",
                overdrawEvidenceBoundary =
                    "Overdraw requires a retained external GPU-frame capture and is not inferred by this runner.",
                instanceBufferEvidenceBoundary =
                    "Instance-buffer residency requires a retained external platform-profiler capture.",
                batteryLevelAtStart = SystemInfo.batteryLevel,
                batteryStatusAtStart =
                    SystemInfo.batteryStatus.ToString(),
                thermalStateAtStart = ReadThermalState(),
                thermalEvidenceSource = ThermalEvidenceSource(),
                externalGpuCaptureId = ReadEvidenceReference(
                    "-slagfallGpuCaptureId",
                    "SLAGFALL_GPU_CAPTURE_ID"),
                externalThermalCaptureId = ReadEvidenceReference(
                    "-slagfallThermalCaptureId",
                    "SLAGFALL_THERMAL_CAPTURE_ID"),
                externalCrashAnrCaptureId = ReadEvidenceReference(
                    "-slagfallCrashAnrCaptureId",
                    "SLAGFALL_CRASH_ANR_CAPTURE_ID"),
                externalBuildSizeEvidenceId = ReadEvidenceReference(
                    "-slagfallBuildSizeEvidenceId",
                    "SLAGFALL_BUILD_SIZE_EVIDENCE_ID"),
                externalOverdrawCaptureId = ReadEvidenceReference(
                    "-slagfallOverdrawCaptureId",
                    "SLAGFALL_OVERDRAW_CAPTURE_ID"),
                externalResidencyCaptureId = ReadEvidenceReference(
                    "-slagfallResidencyCaptureId",
                    "SLAGFALL_RESIDENCY_CAPTURE_ID"),
                completionMarker = "SLAGFALL_EVIDENCE_INCOMPLETE"
            };
            report.qualityName =
                ResolveQualityName(report.qualityLevel);
            return report;
        }

        private void PreparePaths()
        {
            string folder = Path.Combine(
                Application.persistentDataPath,
                "slagfall-device-evidence",
                _report.evidenceLane);
            Directory.CreateDirectory(folder);
            string stem = $"slagfall-{_report.evidenceLane}-{_report.runId}";
            _checkpointPath =
                Path.Combine(folder, stem + ".checkpoint.json");
            _completePath =
                Path.Combine(folder, stem + ".complete.json");
        }

        private void CaptureFrame(double elapsedSeconds)
        {
            double cpuMilliseconds =
                Math.Max(0d, Time.unscaledDeltaTime * 1000d);
            double gpuMilliseconds = 0d;

            FrameTimingManager.CaptureFrameTimings();
            uint timingCount = FrameTimingManager.GetLatestTimings(
                1,
                _frameTimings);
            if (timingCount > 0)
            {
                if (_frameTimings[0].cpuFrameTime > 0d)
                {
                    cpuMilliseconds =
                        _frameTimings[0].cpuFrameTime;
                }

                gpuMilliseconds =
                    Math.Max(0d, _frameTimings[0].gpuFrameTime);
            }

            _accumulator.AddFrame(
                elapsedSeconds,
                cpuMilliseconds,
                gpuMilliseconds);
        }

        private void CaptureRepresentation()
        {
            int represented =
                _slice.ActiveRepresentedSyntheticUserCount;
            _minimumRepresentedUsers =
                Math.Min(_minimumRepresentedUsers, represented);
            if (represented !=
                SlagfallEvidenceContract.RequiredRepresentedUsers)
            {
                if (_finishing)
                {
                    return;
                }

                _finishing = true;
                _report.severeLogCount++;
                _report.minimumRepresentedUserCount =
                    _minimumRepresentedUsers;
                _report.productionScoringBlockers =
                    new[] { "registered_user_became_unrepresented" };
                Debug.LogError(
                    "SLAGFALL_EVIDENCE_AUTOMATIC_FAILURE " +
                    $"representedUsers={represented}");
                StartCoroutine(
                    FinalizeAutomaticFailure(
                        "registered_user_became_unrepresented"));
            }
        }

        private void CaptureCounters()
        {
            AddCounter(
                "total_allocated_memory",
                Profiler.GetTotalAllocatedMemoryLong());
            AddCounter(
                "total_reserved_memory",
                Profiler.GetTotalReservedMemoryLong());
            AddCounter(
                "graphics_driver_memory",
                Profiler.GetAllocatedMemoryForGraphicsDriver());
            AddCounter(
                "loaded_texture_memory",
                SumRuntimeMemory<Texture>());
            AddCounter(
                "loaded_mesh_memory",
                SumRuntimeMemory<Mesh>());
            AddCounter(
                "loaded_animation_memory",
                SumRuntimeMemory<AnimationClip>());

            Renderer[] renderers = Object.FindObjectsOfType<Renderer>();
            Renderer[] activeRenderers = renderers
                .Where(
                    renderer =>
                        renderer != null &&
                        renderer.enabled &&
                        renderer.gameObject.activeInHierarchy)
                .ToArray();
            AddCounter(
                "active_renderers",
                activeRenderers.Length);
            AddCounter(
                "active_materials",
                activeRenderers
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .Select(material => material.GetInstanceID())
                    .Distinct()
                    .Count());
            AddCounter(
                "active_triangles",
                activeRenderers.Sum(TriangleCount));
            AddCounter(
                "shadow_casters",
                activeRenderers.Count(
                    renderer =>
                        renderer.shadowCastingMode !=
                            ShadowCastingMode.Off));
            AddCounter(
                "particle_systems",
                Object.FindObjectsOfType<ParticleSystem>()
                    .Count(
                        particles =>
                            particles != null &&
                            particles.gameObject.activeInHierarchy));

            foreach (CounterRecorder recorder in _recorders)
            {
                if (recorder.IsValid)
                {
                    AddCounter(recorder.Id, recorder.LastValue);
                }
            }

            MarkEvidenceOverhead();
        }

        private IEnumerator FinalizeEvidence()
        {
            double endOfRun =
                Time.realtimeSinceStartupAsDouble - _startedRealtime;
            _report.observedDurationSeconds = endOfRun;
            _report.minimumRepresentedUserCount =
                _minimumRepresentedUsers;
            _report.finalLoadLevel =
                _slice.Controller.CurrentLevel.ToString();

            double cancellationStarted =
                Time.realtimeSinceStartupAsDouble;
            _slice.CancelOptionalPresentation();
            yield return null;
            _report.optionalTierCancellationSeconds =
                Time.realtimeSinceStartupAsDouble -
                cancellationStarted;
            _report.optionalTierCancellationPassed =
                _slice.Slagwhistle.IsRepresented &&
                _slice.Slagwhistle.CurrentTier ==
                    TerritoryRenderTier.LowDetail;

            yield return RunLifecyclePlateau();

            _report.batteryLevelAtEnd = SystemInfo.batteryLevel;
            _report.batteryStatusAtEnd =
                SystemInfo.batteryStatus.ToString();
            _report.thermalStateAtEnd = ReadThermalState();
            _report.completed = true;
            _report.completedUtc = DateTime.UtcNow.ToString("O");
            _report.completionMarker = "SLAGFALL_EVIDENCE_COMPLETE";
            PopulateSummaries();
            _report.productionScoringEligible =
                SlagfallDeviceEvidenceValidator
                    .ValidateForProductionScoring(
                        _report,
                        out string[] blockers);
            _report.productionScoringBlockers = blockers;
            WriteCheckpoint(true);
            _running = false;
            RestoreTargetFrameRate();
            DisposeRecorders();

            Debug.Log(
                "SLAGFALL_EVIDENCE_COMPLETE " +
                $"run={_report.runId} eligible=" +
                $"{_report.productionScoringEligible} " +
                $"report={_completePath}");

#if !UNITY_EDITOR
            if (_quitPlayerWhenComplete)
            {
                Application.Quit(
                    _report.productionScoringEligible ? 0 : 2);
            }
#endif
        }

        private IEnumerator FinalizeAutomaticFailure(string blocker)
        {
            yield return null;
            _report.observedDurationSeconds =
                Math.Max(
                    0d,
                    Time.realtimeSinceStartupAsDouble -
                        _startedRealtime);
            _report.minimumRepresentedUserCount =
                _minimumRepresentedUsers;
            _report.finalLoadLevel =
                _slice != null
                    ? _slice.Controller.CurrentLevel.ToString()
                    : _report.finalLoadLevel;
            _report.completed = false;
            _report.completedUtc = DateTime.UtcNow.ToString("O");
            _report.completionMarker =
                "SLAGFALL_EVIDENCE_AUTOMATIC_FAILURE";
            PopulateSummaries();
            _report.productionScoringEligible = false;
            _report.productionScoringBlockers =
                new[] { blocker };
            WriteCheckpoint(false);
            _running = false;
            RestoreTargetFrameRate();
            DisposeRecorders();

#if !UNITY_EDITOR
            if (_quitPlayerWhenComplete)
            {
                Application.Quit(3);
            }
#endif
        }

        private IEnumerator RunLifecyclePlateau()
        {
            SlagfallRepresentativeSliceProfile profile = _slice.Profile;
            GameObject prefab =
                profile != null
                    ? profile.RepresentativeSlicePrefab
                    : null;
            if (prefab == null)
            {
                _report.exitReleasePlateauPassed = false;
                _report.exitReleaseSeconds = 0d;
                yield break;
            }

            GameObject originalRoot = _slice.gameObject;
            _slice = null;
            Destroy(originalRoot);
            yield return null;

            var releasedMemory = new List<long>();
            var warmReadySeconds = new List<double>();
            var incrementalMemory = new List<double>();
            bool lifecycleContinuityPassed = true;
            double worstReleaseSeconds = 0d;
            AsyncOperation originalUnload =
                Resources.UnloadUnusedAssets();
            yield return originalUnload;
            long releasedBaseline =
                Profiler.GetTotalAllocatedMemoryLong();
            for (int cycle = 0; cycle < LifecycleCycles; cycle++)
            {
                double warmStarted =
                    Time.realtimeSinceStartupAsDouble;
                GameObject root = Instantiate(prefab);
                SlagfallRepresentativeSlice slice =
                    root.GetComponent<SlagfallRepresentativeSlice>();
                if (slice == null)
                {
                    lifecycleContinuityPassed = false;
                    Destroy(root);
                    yield return null;
                    break;
                }

                slice.SetTargetFrameTimeMilliseconds(
                    SlagfallEvidenceContract
                        .TargetFrameTimeMilliseconds(_lane));
                slice.SetSyntheticCrowdActive(true);
                slice.SetAccessibility(_effectsOff, _reducedMotion);
                slice.ApplySyntheticPressure(
                    Math.Max(
                        60f,
                        (float)SlagfallEvidenceContract
                            .TargetFrameTimeMilliseconds(_lane) * 2f),
                    0.5f);
                yield return null;
                warmReadySeconds.Add(
                    Time.realtimeSinceStartupAsDouble -
                    warmStarted);
                long loadedMemory =
                    Profiler.GetTotalAllocatedMemoryLong();
                incrementalMemory.Add(
                    Math.Max(
                        0d,
                        loadedMemory - releasedBaseline));
                lifecycleContinuityPassed &=
                    slice.SyntheticCrowd.Count ==
                        SlagfallEvidenceContract
                            .RequiredRepresentedUsers &&
                    slice.ActiveRepresentedSyntheticUserCount ==
                        SlagfallEvidenceContract
                            .RequiredRepresentedUsers &&
                    slice.Controller.CurrentPlan.CulledCount == 0 &&
                    slice.Slagwhistle.IsRepresented;
                slice.CancelOptionalPresentation();
                lifecycleContinuityPassed &=
                    slice.Slagwhistle.CurrentTier ==
                        TerritoryRenderTier.LowDetail &&
                    slice.Slagwhistle.IsRepresented;

                double releaseStarted =
                    Time.realtimeSinceStartupAsDouble;
                Destroy(root);
                yield return null;
                AsyncOperation unload =
                    Resources.UnloadUnusedAssets();
                yield return unload;
                worstReleaseSeconds = Math.Max(
                    worstReleaseSeconds,
                    Time.realtimeSinceStartupAsDouble -
                        releaseStarted);
                releasedMemory.Add(
                    Profiler.GetTotalAllocatedMemoryLong());
                releasedBaseline =
                    releasedMemory[releasedMemory.Count - 1];
            }

            _report.warmReadySeconds =
                SlagfallMetricSummary.From(warmReadySeconds);
            _report.incrementalUnityAllocatedMemoryBytes =
                SlagfallMetricSummary.From(incrementalMemory);
            _report.lifecycleCycleCount = releasedMemory.Count;
            _report.lifecycleStressPassed =
                lifecycleContinuityPassed &&
                releasedMemory.Count == LifecycleCycles;
            _report.exitReleaseSeconds = worstReleaseSeconds;
            if (releasedMemory.Count < LifecycleCycles)
            {
                _report.exitReleasePlateauPassed = false;
                yield break;
            }

            List<long> steadyStateMemory = releasedMemory
                .Skip(LifecycleWarmupCycles)
                .ToList();
            long minimum = steadyStateMemory.Min();
            long maximum = steadyStateMemory.Max();
            bool bounded =
                maximum - minimum <= MemoryPlateauToleranceBytes;
            bool finalBounded =
                steadyStateMemory[steadyStateMemory.Count - 1] <=
                    steadyStateMemory[0] +
                    MemoryPlateauToleranceBytes;
            _report.exitReleasePlateauPassed =
                bounded && finalBounded;
        }

        private void PopulateSummaries()
        {
            _report.cpuFrameMilliseconds = _accumulator.Cpu();
            _report.gpuFrameMilliseconds = _accumulator.Gpu();
            _report.firstFiveMinuteCpuMilliseconds =
                _accumulator.FirstCpu();
            _report.firstFiveMinuteGpuMilliseconds =
                _accumulator.FirstGpu();
            _report.finalFiveMinuteCpuMilliseconds =
                _accumulator.FinalCpu();
            _report.finalFiveMinuteGpuMilliseconds =
                _accumulator.FinalGpu();
            _report.totalAllocatedMemoryBytes = Counter(
                "total_allocated_memory",
                "unity_total_allocated_memory_unavailable");
            _report.totalReservedMemoryBytes = Counter(
                "total_reserved_memory",
                "unity_total_reserved_memory_unavailable");
            if (_report.incrementalUnityAllocatedMemoryBytes == null)
            {
                _report.incrementalUnityAllocatedMemoryBytes =
                    SlagfallMetricSummary.Unavailable(
                        "lifecycle_incremental_memory_unavailable");
            }
            _report.graphicsDriverMemoryBytes = Counter(
                "graphics_driver_memory",
                "graphics_driver_memory_unavailable");
            _report.loadedTextureMemoryBytes = Counter(
                "loaded_texture_memory",
                "loaded_texture_memory_unavailable");
            _report.loadedMeshMemoryBytes = Counter(
                "loaded_mesh_memory",
                "loaded_mesh_memory_unavailable");
            _report.loadedAnimationMemoryBytes = Counter(
                "loaded_animation_memory",
                "loaded_animation_memory_unavailable");
            _report.activeRendererCount = Counter(
                "active_renderers",
                "active_renderer_count_unavailable");
            _report.activeMaterialCount = Counter(
                "active_materials",
                "active_material_count_unavailable");
            _report.activeTriangleCount = Counter(
                "active_triangles",
                "active_triangle_count_unavailable");
            _report.drawCallCount = Counter(
                "draw_calls",
                "unity_render_counter_unavailable");
            _report.batchCount = Counter(
                "batches",
                "unity_render_counter_unavailable");
            _report.setPassCallCount = Counter(
                "set_pass_calls",
                "unity_render_counter_unavailable");
            _report.shadowCasterCount = Counter(
                "shadow_casters",
                "shadow_caster_count_unavailable");
            _report.particleSystemCount = Counter(
                "particle_systems",
                "particle_system_count_unavailable");
            if (_report.warmReadySeconds == null)
            {
                _report.warmReadySeconds =
                    SlagfallMetricSummary.Unavailable(
                        "lifecycle_warm_ready_timing_unavailable");
            }
            _report.excludedEvidenceOverheadFrameCount =
                _excludedEvidenceOverheadFrameCount;
        }

        private SlagfallMetricSummary Counter(
            string id,
            string unavailableReason)
        {
            return _accumulator.Counter(id, unavailableReason);
        }

        private void AddCounter(string id, double value)
        {
            _accumulator.AddCounter(id, value);
        }

        private void WriteCheckpoint(bool completed)
        {
            if (_report == null || string.IsNullOrEmpty(_checkpointPath))
            {
                return;
            }

            _report.lastCheckpointUtc = DateTime.UtcNow.ToString("O");
            if (!completed)
            {
                _report.observedDurationSeconds =
                    Math.Max(
                        0d,
                        Time.realtimeSinceStartupAsDouble -
                            _startedRealtime);
                _report.minimumRepresentedUserCount =
                    _minimumRepresentedUsers;
                _report.finalLoadLevel =
                    _slice != null
                        ? _slice.Controller.CurrentLevel.ToString()
                        : _report.finalLoadLevel;
                SlagfallDeviceEvidenceValidator
                    .ValidateForProductionScoring(
                        _report,
                        out string[] blockers);
                _report.productionScoringEligible = false;
                _report.productionScoringBlockers = blockers;
            }

            string json = JsonUtility.ToJson(_report, true);
            WriteAtomic(_checkpointPath, json);
            if (completed)
            {
                WriteAtomic(_completePath, json);
            }

            MarkEvidenceOverhead();
        }

        private void MarkEvidenceOverhead()
        {
            if (_running && !_finishing)
            {
                _frameSamplesToSkip =
                    Math.Max(_frameSamplesToSkip, 1);
            }
        }

        private void RestoreTargetFrameRate()
        {
            if (!_targetFrameRateChanged)
            {
                return;
            }

            Application.targetFrameRate = _previousTargetFrameRate;
            _targetFrameRateChanged = false;
        }

        private static void WriteAtomic(string path, string contents)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, contents);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temporary, path);
        }

        private void HandleLowMemory()
        {
            if (_report != null)
            {
                _report.lowMemoryEventCount++;
                WriteCheckpoint(false);
            }
        }

        private void HandleFocusChanged(bool hasFocus)
        {
            if (_running && !hasFocus && _report != null)
            {
                _report.focusLossCount++;
            }
        }

        private void HandleLog(
            string condition,
            string stackTrace,
            LogType type)
        {
            if (_report == null)
            {
                return;
            }

            if (type == LogType.Error ||
                type == LogType.Assert ||
                type == LogType.Exception)
            {
                _report.severeLogCount++;
            }
        }

        private void HandleQuitting()
        {
            if (_running && !_report.completed)
            {
                WriteCheckpoint(false);
            }
        }

        private void StartRecorders()
        {
            _recorders.Add(
                CounterRecorder.Start(
                    "draw_calls",
                    ProfilerCategory.Render,
                    "Draw Calls Count"));
            _recorders.Add(
                CounterRecorder.Start(
                    "batches",
                    ProfilerCategory.Render,
                    "Batches Count"));
            _recorders.Add(
                CounterRecorder.Start(
                    "set_pass_calls",
                    ProfilerCategory.Render,
                    "SetPass Calls Count"));
        }

        private void DisposeRecorders()
        {
            foreach (CounterRecorder recorder in _recorders)
            {
                recorder.Dispose();
            }
            _recorders.Clear();
        }

        private static long SumRuntimeMemory<T>()
            where T : Object
        {
            long total = 0L;
            T[] assets = Resources.FindObjectsOfTypeAll<T>();
            foreach (T asset in assets)
            {
                if (asset != null)
                {
                    total += Profiler.GetRuntimeMemorySizeLong(asset);
                }
            }
            return total;
        }

        private static int TriangleCount(Renderer renderer)
        {
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
            }
            else
            {
                MeshFilter filter =
                    renderer.GetComponent<MeshFilter>();
                mesh = filter != null ? filter.sharedMesh : null;
            }

            if (mesh == null)
            {
                return 0;
            }

            long triangles = 0L;
            for (int subMesh = 0;
                subMesh < mesh.subMeshCount;
                subMesh++)
            {
                triangles +=
                    (long)mesh.GetIndexCount(subMesh) / 3L;
            }
            return triangles > int.MaxValue
                ? int.MaxValue
                : (int)triangles;
        }

        private static string ResolveQualityName(int qualityLevel)
        {
            string[] names = QualitySettings.names;
            return qualityLevel >= 0 &&
                qualityLevel < names.Length
                ? names[qualityLevel]
                : "unknown";
        }

        private static string ReadEvidenceReference(
            string argumentName,
            string environmentName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0;
                index < arguments.Length - 1;
                index++)
            {
                if (string.Equals(
                    arguments[index],
                    argumentName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return Environment.GetEnvironmentVariable(
                       environmentName) ??
                string.Empty;
        }

        private static string ThermalEvidenceSource()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return "android.os.PowerManager.getCurrentThermalStatus";
#else
            return "external_platform_capture_required";
#endif
        }

        private static string ReadThermalState()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player =
                    new AndroidJavaClass(
                        "com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity =
                    player.GetStatic<AndroidJavaObject>(
                        "currentActivity");
                using AndroidJavaObject manager =
                    activity.Call<AndroidJavaObject>(
                        "getSystemService",
                        "power");
                int status =
                    manager.Call<int>("getCurrentThermalStatus");
                switch (status)
                {
                    case 0:
                        return "none";
                    case 1:
                        return "light";
                    case 2:
                        return "moderate";
                    case 3:
                        return "severe";
                    case 4:
                        return "critical";
                    case 5:
                        return "emergency";
                    case 6:
                        return "shutdown";
                    default:
                        return $"unknown_{status}";
                }
            }
            catch (Exception exception)
            {
                return "unavailable:" +
                    exception.GetType().Name;
            }
#else
            return "external_capture_required";
#endif
        }

        private sealed class CounterRecorder : IDisposable
        {
            private ProfilerRecorder _recorder;

            private CounterRecorder(
                string id,
                ProfilerRecorder recorder)
            {
                Id = id;
                _recorder = recorder;
            }

            public string Id { get; }
            public bool IsValid => _recorder.Valid;
            public long LastValue =>
                _recorder.Valid ? _recorder.LastValue : 0L;

            public static CounterRecorder Start(
                string id,
                ProfilerCategory category,
                string statName)
            {
                try
                {
                    return new CounterRecorder(
                        id,
                        ProfilerRecorder.StartNew(
                            category,
                            statName,
                            1));
                }
                catch (Exception)
                {
                    return new CounterRecorder(
                        id,
                        default);
                }
            }

            public void Dispose()
            {
                if (_recorder.Valid)
                {
                    _recorder.Dispose();
                }
            }
        }
    }
}
