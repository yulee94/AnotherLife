using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AL.Tests.PlayMode
{
    // Representative PlayMode profile-isolation smoke (issue #127) — lifecycle-correction rewrite.
    //
    // Correction summary vs. the reopened head (#127 post-merge audit):
    //   * ONE awaited ordered cleanup routine (RunOrderedCleanup) replaces the split
    //     finally + [UnityTearDown] double-restore. The representative smoke performs NO in-body
    //     profile restore; the single awaited [UnityTearDown] is the guaranteed cleanup that runs
    //     on pass and on failure. The ordered steps are exactly:
    //       stop collectors/callbacks
    //       -> quiesce/disable representative roots
    //       -> unload the exact representative scene with a realtime timeout
    //       -> assert no relevant roots/scenes remain
    //       -> clear ServiceLocator and assert empty
    //       -> delete disposable artifacts
    //       -> restore and verify exact originals
    //       -> restore globals ONLY when captured
    //       -> delete the snapshot after verification succeeds
    //     The protections against a callback writing to a restored profile are (a) quiescing the
    //     representative roots (SetActive(false) stops Update writes) and fully unloading the scene
    //     (all OnDisable/OnDestroy run) BEFORE any restore, and (b) Bootloader's cached _ownedMarker,
    //     which makes owner release safe even after ServiceLocator is cleared. The specific
    //     unload-BEFORE-clear ordering additionally avoids drift-log noise and marker-state races: a
    //     live owner Bootloader whose marker is wiped mid-tick emits BOOT_STACK_RUNTIME_DRIFT. That
    //     drift signal is pinned as a red-on-regression anchor by
    //     ClearingServicesUnderLiveOwnerEmitsRuntimeDrift, which arranges the WRONG order on purpose.
    //   * GlobalStateGuard restores Time.timeScale / LogAssert.ignoreFailingMessages ONLY when it
    //     actually captured them, closing the default-Time.timeScale=0 restore bug that let a helper
    //     [Test] pause the runner.
    //   * A fake late-write lifecycle fixture (LateWriteProbe) proves OnDisable/OnDestroy/late-Update
    //     callbacks cannot corrupt the restored profile, covering Bootloader standby-reclaim
    //     RunAutoLoad as a concrete production writer.
    //   * The full failure/second-run matrix from PlayMode_Profile_Isolation_Spec.md section 13.
    //
    // HARD-CRASH LIMITATION (spec section 11): [UnityTearDown], try/finally, and this ordered routine
    // CANNOT run after an editor/process/OS crash, a forced kill, or a power loss. This suite does not
    // and cannot claim crash-proof protection from test teardown alone. The external recovery snapshot
    // directory is logged (see the "[Issue127] Profile snapshot directory:" line) BEFORE any original
    // artifact is removed and is retained whenever restoration verification fails, so a developer can
    // manually restore an interrupted run from that directory. Validation should still prefer a
    // disposable OS/test profile or an isolated CI account.
    //
    // Additional honest limitations:
    //   * Failure-path teardown (finding 1): a genuinely UNCAUGHT test failure cannot be kept green in
    //     NUnit/UTF, so Sentinel1_FailingBodyStillTriggersTeardownRestoration raises and records the
    //     failure WITHOUT rethrowing, then Sentinel2 verifies the framework-driven [UnityTearDown]
    //     restored the fake-root profile, disposable artifact, and globals. This proves teardown
    //     restoration after a raised body; it cannot, without a red suite, prove restoration after a
    //     process-level uncaught crash (that is the hard-crash limitation above).
    //   * Incoming active-scene handling (finding 5) is disposition-only: the incoming active-scene
    //     identity is recorded and asserted, but the suite deliberately does not reload/reactivate the
    //     developer's originally-open editor scene; cleanup leaves a clean non-representative scene active.
    //   * Unload/load timeout coverage (finding 6) is indirect: the realtime timeout waiter is
    //     unit-tested directly (RealtimeTimeoutWaiterIgnoresScaledTimeAndFires) rather than by forcing a
    //     real scene unload to hang.
    public sealed class RepresentativeSceneSmokeTests
    {
        private const string RepresentativeScenePath = "Assets/Test.unity";
        private const string MarkerServiceTypeName = "AL.Core.IOfflineServiceStackMarker";
        private const string BootloaderTypeName = "AL.Core.Bootloader";
        private const float LoadTimeoutSeconds = 15f;
        private const float ObservationSeconds = 0.5f;
        private const int ObservationFrames = 5;

        // Instance state is engaged ONLY by the tests that load the real representative scene against
        // the developer's real persistentDataPath (RepresentativeTestSceneLoads... and the
        // second-complete-smoke test). The single awaited [UnityTearDown] restores exactly those.
        // Focused failure-path/fixture tests use fake roots and manage their own cleanup, so they
        // leave these fields untouched and [UnityTearDown] is a clean no-op for them.
        private ProfileArtifactSnapshot _profileSnapshot;
        private GlobalStateGuard _globals;
        private SevereLogCollector _severeLogs;
        private bool _isolationEngaged;
        private Scene _representativeScene;
        private bool _hasRepresentativeScene;

        // Cross-test sentinel state (finding 1): Sentinel1 engages fake-root isolation and raises a
        // simulated failure; the paired Sentinel2 verifies the framework-driven [UnityTearDown]
        // restored everything. Static so it survives across the two ordered tests.
        private static string _sentinelRoot;
        private static byte[] _sentinelOriginalBytes;
        private static bool _sentinelBodyRaised;
        private static float _sentinelCapturedTimeScale;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!_isolationEngaged && !_hasRepresentativeScene && _profileSnapshot == null && _severeLogs == null)
            {
                // Nothing was engaged by this test (e.g. a pure helper [Test] or a self-contained
                // fake-root fixture). No split cleanup, no default-global restoration.
                yield break;
            }

            var context = new CleanupContext
            {
                Collector = _severeLogs,
                HasTargetScene = _hasRepresentativeScene,
                TargetScene = _representativeScene,
                ExpectedNotLoadedPath = _hasRepresentativeScene ? RepresentativeScenePath : null,
                ClearAndAssertServices = _isolationEngaged,
                Snapshot = _profileSnapshot,
                Globals = _globals,
                RestoreAndVerifyProfile = true,
                RealtimeTimeout = LoadTimeoutSeconds
            };

            GlobalStateGuard globals = _globals;
            try
            {
                yield return RunOrderedCleanup(context);
            }
            finally
            {
                // Field reset is unconditional so a thrown ProfileRestorationException (which retains
                // the recovery snapshot) cannot leak isolation state into the next test. The original
                // exception still propagates out of the finally; Build Settings are never mutated so
                // the byte assertion below does not throw and therefore cannot mask it.
                _severeLogs = null;
                _profileSnapshot = null;
                _globals = null;
                _isolationEngaged = false;
                _hasRepresentativeScene = false;
                _representativeScene = default;
                globals?.AssertBuildSettingsUnchanged(RepresentativeScenePath);
            }
        }

        // ------------------------------------------------------------------------------------------
        // The single awaited ordered cleanup routine. Every step is idempotent so a second invocation
        // (or a partially-completed first invocation) cannot corrupt restored files.
        // ------------------------------------------------------------------------------------------
        private static IEnumerator RunOrderedCleanup(CleanupContext context)
        {
            // 1. Stop test-owned collectors/callbacks so no further severe-log capture or late
            //    subscription fires during teardown.
            context.Collector?.Stop();
            if (context.StopCallbacks != null)
            {
                foreach (Action stop in context.StopCallbacks)
                {
                    stop?.Invoke();
                }
            }

            // 2. Quiesce/disable representative roots BEFORE unload so OnDisable runs now and no late
            //    Update can write after restoration.
            if (context.HasTargetScene && context.TargetScene.IsValid() && context.TargetScene.isLoaded)
            {
                foreach (GameObject root in context.TargetScene.GetRootGameObjects())
                {
                    if (root != null)
                    {
                        root.SetActive(false);
                    }
                }
            }

            // 3. Unload the exact target scene with a realtime timeout. Another scene must be active
            //    first because Unity forbids unloading the last loaded scene; prefer an existing scene
            //    and otherwise reuse ONE persistent cleanup scene so tests never accumulate placeholders.
            if (context.HasTargetScene && context.TargetScene.IsValid() && context.TargetScene.isLoaded)
            {
                ActivateNonTargetScene(context.TargetScene);

                AsyncOperation unload = SceneManager.UnloadSceneAsync(context.TargetScene);
                bool timedOut = false;
                yield return WaitUntilOrTimeout(
                    () => unload == null || unload.isDone,
                    context.RealtimeTimeout,
                    () => timedOut = true);

                if (timedOut)
                {
                    Assert.Fail($"Timed out after {context.RealtimeTimeout:0.0}s realtime unloading the target scene.");
                }
            }

            // 4. Assert no relevant roots/scenes remain active.
            if (context.HasTargetScene)
            {
                Assert.That(context.TargetScene.isLoaded, Is.False, "Target scene must be fully unloaded before profile restoration.");
            }

            if (!string.IsNullOrEmpty(context.ExpectedNotLoadedPath))
            {
                Scene byPath = SceneManager.GetSceneByPath(context.ExpectedNotLoadedPath);
                Assert.That(byPath.IsValid() && byPath.isLoaded, Is.False,
                    $"{context.ExpectedNotLoadedPath} must not remain loaded after cleanup.");
            }

            // 5. Clear ServiceLocator and assert empty. This happens AFTER the owning scene is gone so
            //    a live owner Bootloader can never observe a wiped marker (the #241 drift path).
            if (context.ClearAndAssertServices)
            {
                ClearServiceLocator();
                Assert.That(GetServiceLocatorCount(), Is.EqualTo(0), "ServiceLocator must be empty after cleanup.");
            }

            // 6. Delete disposable (test-created) artifacts, then restore and verify the exact
            //    originals. Restoration is skipped for intermediate between-pass resets that keep the
            //    profile isolated for a following pass.
            if (context.Snapshot != null)
            {
                context.Snapshot.DeleteDisposableArtifacts();
                if (context.RestoreAndVerifyProfile)
                {
                    context.Snapshot.RestoreAndVerifyOriginals();
                }
            }

            // 7. Restore captured globals only when they were captured.
            context.Globals?.Restore();

            // 8. Delete the external snapshot directory only after restoration verification succeeded.
            if (context.Snapshot != null && context.RestoreAndVerifyProfile)
            {
                context.Snapshot.DeleteSnapshotDirectory();
            }
        }

        private sealed class CleanupContext
        {
            public SevereLogCollector Collector;
            public List<Action> StopCallbacks;
            public bool HasTargetScene;
            public Scene TargetScene;
            public string ExpectedNotLoadedPath;
            public bool ClearAndAssertServices;
            public ProfileArtifactSnapshot Snapshot;
            public GlobalStateGuard Globals;
            public bool RestoreAndVerifyProfile = true;
            public float RealtimeTimeout = LoadTimeoutSeconds;
        }

        // Realtime (not scaled) bounded wait. onTimeout runs exactly once on expiry; production callers
        // pass Assert.Fail, while the timeout test passes a flag so it can assert the timeout fired.
        private static IEnumerator WaitUntilOrTimeout(Func<bool> isDone, float timeoutSeconds, Action onTimeout)
        {
            float started = Time.realtimeSinceStartup;
            while (!isDone())
            {
                if (Time.realtimeSinceStartup - started > timeoutSeconds)
                {
                    onTimeout();
                    yield break;
                }

                yield return null;
            }
        }

        // ==========================================================================================
        // Representative smoke (the heart of #127).
        // ==========================================================================================
        [UnityTest]
        public IEnumerator RepresentativeTestSceneLoadsWithIsolatedProfileAndNoSevereLogs()
        {
            _globals = new GlobalStateGuard();
            _globals.Capture();

            _profileSnapshot = ProfileArtifactSnapshot.Create(Application.persistentDataPath);
            Debug.Log("[Issue127] Profile snapshot directory: " + _profileSnapshot.SnapshotDirectory);
            _profileSnapshot.SnapshotVerifiedOriginals();
            _profileSnapshot.RemoveOriginalArtifacts();
            Assert.That(ProfileArtifactSnapshot.GetMatchingArtifacts(Application.persistentDataPath), Is.Empty);
            _isolationEngaged = true;

            ClearServiceLocator();
            Assert.That(GetServiceLocatorCount(), Is.EqualTo(0), "ServiceLocator must be empty before representative scene startup.");

            _severeLogs = new SevereLogCollector();
            _severeLogs.Start();

            yield return LoadRepresentativeSceneWithTimeout();
            _representativeScene = SceneManager.GetSceneByPath(RepresentativeScenePath);
            _hasRepresentativeScene = true;

            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(RepresentativeScenePath));
            var manager = GameObject.Find("Demo_Manager");
            Assert.That(manager, Is.Not.Null, "Representative scene startup signal `Demo_Manager` should exist.");
            Assert.That(manager.activeInHierarchy, Is.True, "Representative startup manager should be active.");

            yield return WaitForCoreServices();
            yield return ObserveRealtimeWindow();

            Assert.That(_severeLogs.Messages, Is.Empty, FormatSevereLogs(_severeLogs.Messages));

            // No in-body cleanup: the single awaited [UnityTearDown] performs the ordered restoration
            // on both pass and failure. This is the deliberate fix for the reopened double-restore.
        }

        // ==========================================================================================
        // Second complete smoke in the same runner: proves no service/artifact inheritance across two
        // full representative loads. The final restoration is performed by the single [UnityTearDown].
        // ==========================================================================================
        [UnityTest]
        public IEnumerator SecondCompleteSmokeInSameRunnerDoesNotInheritServicesOrArtifacts()
        {
            _globals = new GlobalStateGuard();
            _globals.Capture();

            _profileSnapshot = ProfileArtifactSnapshot.Create(Application.persistentDataPath);
            Debug.Log("[Issue127] Profile snapshot directory: " + _profileSnapshot.SnapshotDirectory);
            _profileSnapshot.SnapshotVerifiedOriginals();
            _profileSnapshot.RemoveOriginalArtifacts();
            _isolationEngaged = true;

            _severeLogs = new SevereLogCollector();
            _severeLogs.Start();

            // ---- Pass 1 ----
            ClearServiceLocator();
            Assert.That(GetServiceLocatorCount(), Is.EqualTo(0), "Pass 1 must start with an empty ServiceLocator.");
            yield return LoadRepresentativeSceneWithTimeout();
            _representativeScene = SceneManager.GetSceneByPath(RepresentativeScenePath);
            _hasRepresentativeScene = true;
            yield return WaitForCoreServices();
            yield return ObserveRealtimeWindow();
            Assert.That(GetServiceLocatorCount(), Is.GreaterThan(0), "Pass 1 should register the offline stack.");
            Assert.That(_severeLogs.Messages, Is.Empty, FormatSevereLogs(_severeLogs.Messages));

            // ---- Intermediate ordered cleanup: unload + clear services + purge disposable artifacts,
            //      but KEEP the snapshot so the profile stays isolated for pass 2 (no restore yet). ----
            var intermediate = new CleanupContext
            {
                Collector = null,
                HasTargetScene = true,
                TargetScene = _representativeScene,
                ExpectedNotLoadedPath = RepresentativeScenePath,
                ClearAndAssertServices = true,
                Snapshot = _profileSnapshot,
                Globals = null,
                RestoreAndVerifyProfile = false,
                RealtimeTimeout = LoadTimeoutSeconds
            };
            yield return RunOrderedCleanup(intermediate);
            _hasRepresentativeScene = false;
            _representativeScene = default;

            Assert.That(GetServiceLocatorCount(), Is.EqualTo(0), "Services must not leak between passes.");
            Assert.That(ProfileArtifactSnapshot.GetMatchingArtifacts(Application.persistentDataPath), Is.Empty,
                "Profile artifacts must not be inherited by pass 2.");

            // ---- Pass 2: must not inherit anything from pass 1. ----
            yield return LoadRepresentativeSceneWithTimeout();
            _representativeScene = SceneManager.GetSceneByPath(RepresentativeScenePath);
            _hasRepresentativeScene = true;
            yield return WaitForCoreServices();
            yield return ObserveRealtimeWindow();
            Assert.That(GetServiceLocatorCount(), Is.GreaterThan(0), "Pass 2 should register a fresh offline stack.");
            Assert.That(_severeLogs.Messages, Is.Empty, FormatSevereLogs(_severeLogs.Messages));

            // Final restoration is the single awaited [UnityTearDown] against the pass-2 scene.
        }

        // ==========================================================================================
        // Bootloader owner claim/release proof (#241). The representative scene only calls the STATIC
        // Bootloader.InitializeIfMissing(); it hosts no live owner MonoBehaviour, so the owner
        // claim/release lifecycle is proven directly here (prove, don't assume) by driving real
        // Bootloader components via reflection inside the isolation envelope.
        // The actual write-safety protections are quiescing + full unload before restore and the
        // cached _ownedMarker release; the unload-before-clear ordering additionally avoids the
        // drift-log noise / marker-state race pinned by ClearingServicesUnderLiveOwnerEmitsRuntimeDrift.
        // ==========================================================================================
        [UnityTest]
        public IEnumerator BootloaderOwnerReleaseIsDriftSafeAndAllowsFreshClaim()
        {
            Type bootloaderType = ResolveTypeByName(BootloaderTypeName);
            Assert.That(bootloaderType, Is.Not.Null, "Bootloader type must be resolvable via reflection.");

            var guard = new GlobalStateGuard();
            guard.Capture();
            var snapshot = ProfileArtifactSnapshot.Create(Application.persistentDataPath);
            Debug.Log("[Issue127] Profile snapshot directory: " + snapshot.SnapshotDirectory);
            snapshot.SnapshotVerifiedOriginals();
            snapshot.RemoveOriginalArtifacts();

            var collector = new SevereLogCollector();
            GameObject owner1 = null;
            GameObject owner2 = null;

            try
            {
                ClearServiceLocator();
                Assert.That(GetServiceLocatorCount(), Is.EqualTo(0));
                collector.Start();

                // ---- Owner 1 claims the fresh stack (Awake -> InitializeIfMissing -> claim -> load). ----
                Scene ownerScene1 = CreateSyntheticScene("Issue127_Owner1", "Owner1_Bootloader", out owner1);
                owner1.AddComponent(bootloaderType);
                yield return null;
                yield return null;

                Assert.That(GetServiceLocatorCount(), Is.GreaterThan(0), "Owner 1 should publish the offline stack.");
                string owner1Id = GetMarkerRuntimeOwnerId();
                Assert.That(string.IsNullOrEmpty(owner1Id), Is.False, "Owner 1 should claim runtime ownership.");
                Assert.That(collector.Messages, Is.Empty, FormatSevereLogs(collector.Messages));

                // ---- Ordered cleanup of owner 1 MUST be drift-safe: scene/owner unloaded BEFORE the
                //      ServiceLocator marker is cleared, so no RuntimeDrift / RuntimeOwnerRejected log. ----
                var ownerCleanup1 = new CleanupContext
                {
                    Collector = null,
                    HasTargetScene = true,
                    TargetScene = ownerScene1,
                    ClearAndAssertServices = true,
                    Snapshot = snapshot,
                    Globals = null,
                    RestoreAndVerifyProfile = false,
                    RealtimeTimeout = LoadTimeoutSeconds
                };
                yield return RunOrderedCleanup(ownerCleanup1);
                yield return null;

                Assert.That(GetServiceLocatorCount(), Is.EqualTo(0), "ServiceLocator cleared after owner 1 cleanup.");
                Assert.That(collector.Messages, Is.Empty,
                    "Drift-safe cleanup must not emit severe logs: " + FormatSevereLogs(collector.Messages));

                // ---- Owner 2 must be able to claim a fresh stack after owner 1's OnDestroy release
                //      (which used the cached _ownedMarker even though the marker left ServiceLocator). ----
                Scene ownerScene2 = CreateSyntheticScene("Issue127_Owner2", "Owner2_Bootloader", out owner2);
                owner2.AddComponent(bootloaderType);
                yield return null;
                yield return null;

                Assert.That(GetServiceLocatorCount(), Is.GreaterThan(0), "Owner 2 should publish a fresh offline stack.");
                string owner2Id = GetMarkerRuntimeOwnerId();
                Assert.That(string.IsNullOrEmpty(owner2Id), Is.False, "Owner 2 should claim runtime ownership after release.");
                Assert.That(owner2Id, Is.Not.EqualTo(owner1Id), "Owner 2 must be a genuinely fresh claim, not the released owner id.");
                Assert.That(collector.Messages, Is.Empty,
                    "Fresh claim must not emit RuntimeOwnerRejected/drift: " + FormatSevereLogs(collector.Messages));

                var ownerCleanup2 = new CleanupContext
                {
                    Collector = collector,
                    HasTargetScene = true,
                    TargetScene = ownerScene2,
                    ClearAndAssertServices = true,
                    Snapshot = snapshot,
                    Globals = guard,
                    RestoreAndVerifyProfile = true,
                    RealtimeTimeout = LoadTimeoutSeconds
                };
                yield return RunOrderedCleanup(ownerCleanup2);
                owner2 = null;
            }
            finally
            {
                // Synchronous last-ditch guarantee: destroy any live owner (so no Bootloader keeps
                // ticking against a cleared marker in a later test), clear services, and restore the
                // real profile/globals exactly once even if an assertion threw above.
                collector.Stop();
                if (owner1 != null)
                {
                    Object.DestroyImmediate(owner1);
                }

                if (owner2 != null)
                {
                    Object.DestroyImmediate(owner2);
                }

                ClearServiceLocator();
                snapshot.RestoreAndVerifyOriginals();
                snapshot.DeleteSnapshotDirectory();
                guard.Restore();
            }
        }

        // ==========================================================================================
        // Finding 2 — red-on-regression anchor for the unload-before-clear ordering. Arranges the
        // WRONG order on purpose: a live, active owner Bootloader whose ServiceLocator marker is wiped
        // mid-tick MUST emit BOOT_STACK_RUNTIME_DRIFT. This pins the exact drift signal the ordered
        // cleanup avoids by unloading/quiescing the owner BEFORE clearing ServiceLocator. If clearing
        // under a live owner ever stops drifting, this test goes red and the ordering rationale is
        // re-examined.
        // ==========================================================================================
        [UnityTest]
        public IEnumerator ClearingServicesUnderLiveOwnerEmitsRuntimeDrift()
        {
            Type bootloaderType = ResolveTypeByName(BootloaderTypeName);
            Assert.That(bootloaderType, Is.Not.Null, "Bootloader type must be resolvable via reflection.");

            var guard = new GlobalStateGuard();
            guard.Capture();
            var snapshot = ProfileArtifactSnapshot.Create(Application.persistentDataPath);
            Debug.Log("[Issue127] Profile snapshot directory: " + snapshot.SnapshotDirectory);
            snapshot.SnapshotVerifiedOriginals();
            snapshot.RemoveOriginalArtifacts();

            GameObject owner = null;
            Scene ownerScene = default;
            try
            {
                ClearServiceLocator();
                Assert.That(GetServiceLocatorCount(), Is.EqualTo(0));

                ownerScene = CreateSyntheticScene("Issue127_DriftAnchor", "DriftOwner", out owner);
                owner.AddComponent(bootloaderType);
                yield return null; // Awake claims ownership + RunAutoLoad
                Assert.That(string.IsNullOrEmpty(GetMarkerRuntimeOwnerId()), Is.False, "Owner must have claimed ownership.");

                // WRONG ORDER on purpose: wipe ServiceLocator (removing the marker) while the owner is
                // still alive and ticking. This is exactly what the ordered cleanup refuses to do.
                ClearServiceLocator();
                Assert.That(GetServiceLocatorCount(), Is.EqualTo(0));

                // The next Update tick observes the wiped marker and MUST emit drift exactly once.
                LogAssert.Expect(LogType.Error, new Regex("BOOT_STACK_RUNTIME_DRIFT"));
                yield return null;
                yield return null;
            }
            finally
            {
                if (owner != null)
                {
                    Object.DestroyImmediate(owner);
                }

                if (ownerScene.IsValid() && ownerScene.isLoaded && SceneManager.sceneCount > 1)
                {
                    SceneManager.UnloadSceneAsync(ownerScene);
                }

                ClearServiceLocator();
                snapshot.RestoreAndVerifyOriginals();
                snapshot.DeleteSnapshotDirectory();
                guard.Restore();
            }
        }

        // ==========================================================================================
        // Finding 1 — failing-body teardown sentinel (paired). Sentinel1 engages fake-root isolation
        // through the instance fields so the framework's [UnityTearDown] performs the ordered
        // restoration, plants a disposable sentinel artifact, mutates globals, then RAISES a simulated
        // smoke failure. The raise is recorded but not rethrown (a genuinely uncaught failure cannot be
        // kept green in NUnit/UTF — see the header limitation). Sentinel2, ordered after, verifies the
        // teardown-driven restoration actually happened on disk and in globals.
        // ==========================================================================================
        [UnityTest, Order(1)]
        public IEnumerator Sentinel1_FailingBodyStillTriggersTeardownRestoration()
        {
            _sentinelRoot = CreateFakeRoot();
            _sentinelOriginalBytes = Encoding.UTF8.GetBytes("{\"sentinel-original\":true}");
            File.WriteAllBytes(Path.Combine(_sentinelRoot, "save.json"), _sentinelOriginalBytes);
            _sentinelBodyRaised = false;

            _sentinelCapturedTimeScale = Time.timeScale;
            _globals = new GlobalStateGuard();
            _globals.Capture();

            _profileSnapshot = ProfileArtifactSnapshot.Create(_sentinelRoot);
            _profileSnapshot.SnapshotVerifiedOriginals();
            _profileSnapshot.RemoveOriginalArtifacts();
            _isolationEngaged = true;

            // A disposable artifact + synthetic scene + mutated globals: a smoke caught mid-flight.
            File.WriteAllText(Path.Combine(_sentinelRoot, "save.tmp.json"), "sentinel-disposable");
            _representativeScene = CreateSyntheticScene("Issue127_Sentinel", "SentinelRoot", out _);
            _hasRepresentativeScene = true;
            Time.timeScale = 0.23f;
            yield return null;

            // Simulate a failing smoke body. Record the raise; do NOT rethrow so the suite stays green.
            // The guaranteed [UnityTearDown] still runs the single ordered cleanup — that is the proof.
            try
            {
                throw new InvalidOperationException("Simulated post-load smoke failure (sentinel).");
            }
            catch (InvalidOperationException)
            {
                _sentinelBodyRaised = true;
            }
        }

        [Test, Order(2)]
        public void Sentinel2_TeardownRestoredProfileAndGlobalsAfterFailingBody()
        {
            Assert.That(_sentinelBodyRaised, Is.True, "Sentinel1 must have raised a simulated failure before teardown ran.");
            Assert.That(_sentinelRoot, Is.Not.Null.And.Not.Empty, "Sentinel1 must have engaged fake-root isolation.");

            try
            {
                Assert.That(File.Exists(Path.Combine(_sentinelRoot, "save.tmp.json")), Is.False,
                    "Teardown must have deleted the disposable sentinel artifact after the failing body.");
                Assert.That(File.ReadAllBytes(Path.Combine(_sentinelRoot, "save.json")), Is.EqualTo(_sentinelOriginalBytes),
                    "Teardown must have restored the exact original after the failing body.");
                Assert.That(Time.timeScale, Is.EqualTo(_sentinelCapturedTimeScale),
                    "Teardown must have restored captured globals (not the mutated 0.23) after the failing body.");
            }
            finally
            {
                DeleteFakeRoot(_sentinelRoot);
                _sentinelRoot = null;
                _sentinelOriginalBytes = null;
                _sentinelBodyRaised = false;
            }
        }

        // ==========================================================================================
        // Fake late-write lifecycle fixture: OnDisable/OnDestroy/late-Update writes cannot corrupt the
        // restored profile, because restoration happens strictly AFTER the writing root is destroyed
        // and its disposable output purged. Uses a fake root — never the developer profile.
        // ==========================================================================================
        [UnityTest]
        public IEnumerator LateWriteCallbackCannotCorruptRestoredProfile()
        {
            string root = CreateFakeRoot();
            byte[] original = Encoding.UTF8.GetBytes("{\"original\":\"do-not-lose-me\"}");
            byte[] lateWrite = Encoding.UTF8.GetBytes("LATE-WRITE-SHOULD-NEVER-SURVIVE");
            File.WriteAllBytes(Path.Combine(root, "save.json"), original);

            var snapshot = ProfileArtifactSnapshot.Create(root);
            LateWriteProbe probe = null;

            try
            {
                snapshot.SnapshotVerifiedOriginals();
                snapshot.RemoveOriginalArtifacts();
                Assert.That(ProfileArtifactSnapshot.GetMatchingArtifacts(root), Is.Empty);

                Scene scene = CreateSyntheticScene("Issue127_LateWrite", "LateWriteRoot", out GameObject probeRoot);
                probe = probeRoot.AddComponent<LateWriteProbe>();
                probe.TargetDirectory = root;
                probe.ArtifactFileName = "save.json";
                probe.LateBytes = lateWrite;

                // Let the probe run a couple of live frames so it actively writes while alive.
                yield return null;
                yield return null;
                Assert.That(probe.WriteCount, Is.GreaterThan(0), "Probe should write while alive to be a real late-writer.");

                var context = new CleanupContext
                {
                    HasTargetScene = true,
                    TargetScene = scene,
                    ClearAndAssertServices = false,
                    Snapshot = snapshot,
                    Globals = null,
                    RestoreAndVerifyProfile = true,
                    RealtimeTimeout = LoadTimeoutSeconds
                };
                yield return RunOrderedCleanup(context);

                Assert.That(scene.isLoaded, Is.False, "Fixture scene must be unloaded.");
                Assert.That(probe == null, Is.True, "Probe MonoBehaviour must be destroyed (cannot write again).");
                Assert.That(File.ReadAllBytes(Path.Combine(root, "save.json")), Is.EqualTo(original),
                    "Late callback writes must be neutralized; restored profile must equal the exact original.");
                Assert.That(Directory.Exists(snapshot.SnapshotDirectory), Is.False, "Snapshot deleted after verification.");
            }
            finally
            {
                snapshot.DeleteSnapshotDirectory();
                DeleteFakeRoot(root);
            }
        }

        // ==========================================================================================
        // Ordered cleanup unit lifecycle over a fake root + synthetic scene + seeded services.
        // ==========================================================================================
        [UnityTest]
        public IEnumerator OrderedCleanupUnloadsSceneClearsServicesAndRestoresProfile()
        {
            string root = CreateFakeRoot();
            byte[] primary = Encoding.UTF8.GetBytes("{\"primary\":1}");
            byte[] backup = Encoding.UTF8.GetBytes("{\"backup\":1}");
            File.WriteAllBytes(Path.Combine(root, "save.json"), primary);
            File.WriteAllBytes(Path.Combine(root, "save.backup.json"), backup);

            var snapshot = ProfileArtifactSnapshot.Create(root);

            try
            {
                snapshot.SnapshotVerifiedOriginals();
                snapshot.RemoveOriginalArtifacts();
                Assert.That(ProfileArtifactSnapshot.GetMatchingArtifacts(root), Is.Empty);

                // A disposable artifact the cleanup must delete.
                File.WriteAllText(Path.Combine(root, "save.tmp.json"), "disposable");

                Scene scene = CreateSyntheticScene("Issue127_Ordered", "OrderedRoot", out _);
                SeedServiceLocator(2);
                Assert.That(GetServiceLocatorCount(), Is.EqualTo(2));

                var context = new CleanupContext
                {
                    HasTargetScene = true,
                    TargetScene = scene,
                    ClearAndAssertServices = true,
                    Snapshot = snapshot,
                    Globals = null,
                    RestoreAndVerifyProfile = true,
                    RealtimeTimeout = LoadTimeoutSeconds
                };
                yield return RunOrderedCleanup(context);

                Assert.That(scene.isLoaded, Is.False, "Scene unloaded.");
                Assert.That(GetServiceLocatorCount(), Is.EqualTo(0), "Services cleared.");
                Assert.That(File.Exists(Path.Combine(root, "save.tmp.json")), Is.False, "Disposable artifact removed.");
                Assert.That(File.ReadAllBytes(Path.Combine(root, "save.json")), Is.EqualTo(primary));
                Assert.That(File.ReadAllBytes(Path.Combine(root, "save.backup.json")), Is.EqualTo(backup));
                Assert.That(Directory.Exists(snapshot.SnapshotDirectory), Is.False, "Snapshot deleted after verification.");
            }
            finally
            {
                ClearServiceLocator();
                snapshot.DeleteSnapshotDirectory();
                DeleteFakeRoot(root);
            }
        }

        // ==========================================================================================
        // Ordered cleanup is idempotent: a second invocation cannot corrupt restored files.
        // ==========================================================================================
        [UnityTest]
        public IEnumerator OrderedCleanupIsIdempotentOnSecondInvocation()
        {
            string root = CreateFakeRoot();
            byte[] primary = Encoding.UTF8.GetBytes("{\"idempotent\":true}");
            File.WriteAllBytes(Path.Combine(root, "save.json"), primary);

            var snapshot = ProfileArtifactSnapshot.Create(root);

            try
            {
                snapshot.SnapshotVerifiedOriginals();
                snapshot.RemoveOriginalArtifacts();

                Scene scene = CreateSyntheticScene("Issue127_Idempotent", "IdempotentRoot", out _);

                var context = new CleanupContext
                {
                    HasTargetScene = true,
                    TargetScene = scene,
                    ClearAndAssertServices = false,
                    Snapshot = snapshot,
                    Globals = null,
                    RestoreAndVerifyProfile = true,
                    RealtimeTimeout = LoadTimeoutSeconds
                };

                yield return RunOrderedCleanup(context);
                Assert.That(File.ReadAllBytes(Path.Combine(root, "save.json")), Is.EqualTo(primary));

                // Second invocation over the same (now consumed) snapshot must be a safe no-op.
                yield return RunOrderedCleanup(context);
                Assert.That(File.ReadAllBytes(Path.Combine(root, "save.json")), Is.EqualTo(primary),
                    "Second cleanup pass must not corrupt the restored original.");
            }
            finally
            {
                snapshot.DeleteSnapshotDirectory();
                DeleteFakeRoot(root);
            }
        }

        // ==========================================================================================
        // Post-load state (a stand-in for a failed assertion after load) still yields exact profile
        // and global restoration through the same ordered routine that [UnityTearDown] invokes.
        // ==========================================================================================
        [UnityTest]
        public IEnumerator OrderedCleanupRestoresProfileAndGlobalsAfterPostLoadState()
        {
            string root = CreateFakeRoot();
            byte[] original = Encoding.UTF8.GetBytes("{\"keep\":\"me\"}");
            File.WriteAllBytes(Path.Combine(root, "save.json"), original);

            var snapshot = ProfileArtifactSnapshot.Create(root);
            float runnerTimeScale = Time.timeScale;
            var guard = new GlobalStateGuard();

            try
            {
                snapshot.SnapshotVerifiedOriginals();
                snapshot.RemoveOriginalArtifacts();

                guard.Capture();                 // records runnerTimeScale, forces Time.timeScale = 1
                Time.timeScale = 0.42f;           // simulate mid-test mutation after "load"

                Scene scene = CreateSyntheticScene("Issue127_PostLoad", "PostLoadRoot", out _);
                yield return null;

                var context = new CleanupContext
                {
                    HasTargetScene = true,
                    TargetScene = scene,
                    ClearAndAssertServices = false,
                    Snapshot = snapshot,
                    Globals = guard,
                    RestoreAndVerifyProfile = true,
                    RealtimeTimeout = LoadTimeoutSeconds
                };
                yield return RunOrderedCleanup(context);

                Assert.That(scene.isLoaded, Is.False);
                Assert.That(File.ReadAllBytes(Path.Combine(root, "save.json")), Is.EqualTo(original));
                Assert.That(Time.timeScale, Is.EqualTo(runnerTimeScale),
                    "Globals must restore to the captured original, not the mutated value or a default.");
            }
            finally
            {
                Time.timeScale = runnerTimeScale;
                snapshot.DeleteSnapshotDirectory();
                DeleteFakeRoot(root);
            }
        }

        // ==========================================================================================
        // A severe log fails the smoke, but the ordered cleanup still runs and restores the profile.
        // ==========================================================================================
        [UnityTest]
        public IEnumerator SevereLogFailureStillRunsOrderedCleanup()
        {
            string root = CreateFakeRoot();
            byte[] original = Encoding.UTF8.GetBytes("{\"severe\":\"restore-me\"}");
            File.WriteAllBytes(Path.Combine(root, "save.json"), original);

            var snapshot = ProfileArtifactSnapshot.Create(root);
            var collector = new SevereLogCollector();

            try
            {
                snapshot.SnapshotVerifiedOriginals();
                snapshot.RemoveOriginalArtifacts();

                Scene scene = CreateSyntheticScene("Issue127_Severe", "SevereRoot", out _);
                collector.Start();

                const string message = "[Issue127][fake] simulated severe failure";
                LogAssert.Expect(LogType.Error, message);
                Debug.LogError(message);
                yield return null;

                Assert.That(collector.Messages.Count, Is.GreaterThan(0), "Collector should have captured the severe log.");

                var context = new CleanupContext
                {
                    Collector = collector,
                    HasTargetScene = true,
                    TargetScene = scene,
                    ClearAndAssertServices = false,
                    Snapshot = snapshot,
                    Globals = null,
                    RestoreAndVerifyProfile = true,
                    RealtimeTimeout = LoadTimeoutSeconds
                };
                yield return RunOrderedCleanup(context);

                Assert.That(scene.isLoaded, Is.False, "Cleanup still unloads the scene after a severe log.");
                Assert.That(File.ReadAllBytes(Path.Combine(root, "save.json")), Is.EqualTo(original),
                    "Cleanup still restores the profile after a severe log.");
            }
            finally
            {
                collector.Stop();
                snapshot.DeleteSnapshotDirectory();
                DeleteFakeRoot(root);
            }
        }

        // ==========================================================================================
        // Ordinary informational/warning logs do not trip the severe collector.
        // ==========================================================================================
        [UnityTest]
        public IEnumerator OrdinaryLogsDuringObservationDoNotTriggerSevereCollector()
        {
            var collector = new SevereLogCollector();
            collector.Start();
            try
            {
                Debug.Log("[Issue127] ordinary informational log");
                Debug.LogWarning("[Issue127] ordinary warning log");
                yield return null;
                yield return null;
                Assert.That(collector.Messages, Is.Empty, "Ordinary Log/Warning messages must not be treated as severe.");
            }
            finally
            {
                collector.Stop();
            }
        }

        // ==========================================================================================
        // Post-cleanup ServiceLocator is empty.
        // ==========================================================================================
        [UnityTest]
        public IEnumerator PostCleanupServiceLocatorIsEmpty()
        {
            SeedServiceLocator(3);
            Assert.That(GetServiceLocatorCount(), Is.EqualTo(3));

            var context = new CleanupContext
            {
                HasTargetScene = false,
                ClearAndAssertServices = true,
                Snapshot = null,
                Globals = null
            };
            yield return RunOrderedCleanup(context);

            Assert.That(GetServiceLocatorCount(), Is.EqualTo(0));
        }

        // ==========================================================================================
        // Incoming active-scene disposition: the guard records the incoming active scene, and cleanup
        // never leaves the target scene loaded or active.
        // ==========================================================================================
        [UnityTest]
        public IEnumerator IncomingActiveSceneDispositionIsRecordedAndTargetNotLeftActive()
        {
            var guard = new GlobalStateGuard();
            Scene incoming = SceneManager.GetActiveScene();
            guard.Capture();
            Assert.That(guard.IncomingActiveScenePath, Is.EqualTo(incoming.path),
                "Incoming active scene identity must be recorded before any load.");

            Scene target = CreateSyntheticScene("Issue127_Incoming", "IncomingRoot", out _);
            SceneManager.SetActiveScene(target);
            yield return null;

            var context = new CleanupContext
            {
                HasTargetScene = true,
                TargetScene = target,
                ClearAndAssertServices = false,
                Snapshot = null,
                Globals = guard,
                RealtimeTimeout = LoadTimeoutSeconds
            };
            yield return RunOrderedCleanup(context);

            Assert.That(target.isLoaded, Is.False, "Target scene must not remain loaded.");
            Assert.That(SceneManager.GetActiveScene().handle, Is.Not.EqualTo(target.handle),
                "Target scene must not remain the active scene after cleanup.");
        }

        // ==========================================================================================
        // Realtime timeout waiter ignores scaled time (Time.timeScale = 0) and fires the timeout.
        // ==========================================================================================
        [UnityTest]
        public IEnumerator RealtimeTimeoutWaiterIgnoresScaledTimeAndFires()
        {
            float runnerTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f; // scaled time is frozen; the waiter must still expire in realtime.
                bool timedOut = false;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                yield return WaitUntilOrTimeout(() => false, 0.5f, () => timedOut = true);

                stopwatch.Stop();
                Assert.That(timedOut, Is.True, "The waiter must fire onTimeout when the condition never completes.");
                Assert.That(stopwatch.Elapsed.TotalSeconds, Is.GreaterThanOrEqualTo(0.45),
                    "Timeout must be accounted in realtime, not frozen scaled time.");
            }
            finally
            {
                Time.timeScale = runnerTimeScale;
            }
        }

        // ==========================================================================================
        // Global-state guard: does NOT restore when it never captured (the default Time.timeScale=0 bug).
        // ==========================================================================================
        [Test]
        public void GlobalStateGuardDoesNotRestoreUncapturedGlobals()
        {
            float runnerTimeScale = Time.timeScale;
            bool runnerIgnore = LogAssert.ignoreFailingMessages;
            try
            {
                var guard = new GlobalStateGuard(); // never captured
                Time.timeScale = 0.37f;
                LogAssert.ignoreFailingMessages = true;

                guard.Restore();

                Assert.That(guard.Captured, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(0.37f),
                    "Uncaptured guard must not reset Time.timeScale to a default (this was the reopened bug).");
                Assert.That(LogAssert.ignoreFailingMessages, Is.True,
                    "Uncaptured guard must not reset LogAssert.ignoreFailingMessages.");
            }
            finally
            {
                Time.timeScale = runnerTimeScale;
                LogAssert.ignoreFailingMessages = runnerIgnore;
            }
        }

        // ==========================================================================================
        // Global-state guard: restores exactly to captured values.
        // ==========================================================================================
        [Test]
        public void GlobalStateGuardRestoresCapturedGlobalsExactly()
        {
            float runnerTimeScale = Time.timeScale;
            bool runnerIgnore = LogAssert.ignoreFailingMessages;
            try
            {
                Time.timeScale = 2.5f;
                LogAssert.ignoreFailingMessages = true;

                var guard = new GlobalStateGuard();
                guard.Capture();
                Assert.That(guard.Captured, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(1f), "Capture normalizes Time.timeScale to 1 for the run.");
                Assert.That(LogAssert.ignoreFailingMessages, Is.False, "Capture normalizes ignoreFailingMessages to false.");

                Time.timeScale = 0.1f;
                LogAssert.ignoreFailingMessages = false;

                guard.Restore();
                Assert.That(Time.timeScale, Is.EqualTo(2.5f), "Restore returns the captured Time.timeScale.");
                Assert.That(LogAssert.ignoreFailingMessages, Is.True, "Restore returns the captured ignoreFailingMessages.");
            }
            finally
            {
                Time.timeScale = runnerTimeScale;
                LogAssert.ignoreFailingMessages = runnerIgnore;
            }
        }

        // ==========================================================================================
        // Severe-log classification covers exactly Error/Assert/Exception.
        // ==========================================================================================
        [Test]
        public void SevereLogCollectorClassifiesOnlyErrorAssertException()
        {
            Assert.That(SevereLogCollector.IsSevere(LogType.Error), Is.True);
            Assert.That(SevereLogCollector.IsSevere(LogType.Assert), Is.True);
            Assert.That(SevereLogCollector.IsSevere(LogType.Exception), Is.True);
            Assert.That(SevereLogCollector.IsSevere(LogType.Warning), Is.False);
            Assert.That(SevereLogCollector.IsSevere(LogType.Log), Is.False);
        }

        // ==========================================================================================
        // Artifact matcher — current/legacy/quarantine names.
        // ==========================================================================================
        [Test]
        public void ArtifactMatcherCoversCurrentLegacyAndQuarantineNames()
        {
            string[] matching =
            {
                "save.json",
                "save.backup.json",
                "save.tmp.json",
                "save.previous.json",
                "save.json.previous",
                "save.json.corrupt-20260715000000-abcdef",
                "save.backup.json.corrupt-20260715000000-abcdef"
            };

            foreach (string fileName in matching)
            {
                Assert.That(ProfileArtifactSnapshot.IsProfileArtifactFileName(fileName), Is.True, fileName);
            }
        }

        // ==========================================================================================
        // Artifact matcher — rejects unrelated/similar/nested names.
        // ==========================================================================================
        [Test]
        public void ArtifactMatcherRejectsUnrelatedSimilarAndNestedNames()
        {
            string[] rejected =
            {
                string.Empty,
                "save",
                "save.json.old",
                "my-save.json",
                "save.backup",
                "save.tmp.json.extra",
                "profile/save.json",
                "save.json.corrupt",
                "save.other.json.corrupt-123"
            };

            foreach (string fileName in rejected)
            {
                Assert.That(ProfileArtifactSnapshot.IsProfileArtifactFileName(fileName), Is.False, fileName);
            }
        }

        // ==========================================================================================
        // Artifact matcher — platform case-sensitivity policy is ordinal-case-insensitive.
        // ==========================================================================================
        [Test]
        public void ArtifactMatcherCaseSensitivityPolicyIsOrdinalIgnoreCase()
        {
            string[] caseVariants =
            {
                "SAVE.JSON",
                "Save.Backup.Json",
                "SAVE.TMP.JSON",
                "Save.Previous.Json",
                "SAVE.JSON.PREVIOUS",
                "SAVE.JSON.CORRUPT-20260715000000-ABCDEF",
                "Save.Backup.Json.Corrupt-20260715000000-abcdef"
            };

            foreach (string fileName in caseVariants)
            {
                Assert.That(ProfileArtifactSnapshot.IsProfileArtifactFileName(fileName), Is.True,
                    "Matcher must apply the ordinal case-insensitive policy consistently: " + fileName);
            }

            // A different token is still rejected regardless of casing.
            Assert.That(ProfileArtifactSnapshot.IsProfileArtifactFileName("SAVE.OTHER.JSON"), Is.False);
        }

        // ==========================================================================================
        // Snapshot/restore — exact bytes, disposable removal.
        // ==========================================================================================
        [Test]
        public void SnapshotRestorePreservesBytesAndRemovesTestArtifacts()
        {
            string root = CreateFakeRoot();

            try
            {
                var expected = new Dictionary<string, byte[]>
                {
                    ["save.json"] = new byte[] { 0, 1, 2, 3, 255 },
                    ["save.backup.json"] = Encoding.UTF8.GetBytes("{\"backup\":true}"),
                    ["save.tmp.json"] = Array.Empty<byte>(),
                    ["save.previous.json"] = Encoding.UTF8.GetBytes("approved-previous"),
                    ["save.json.previous"] = Encoding.UTF8.GetBytes("legacy-previous"),
                    ["save.json.corrupt-20260715000000-deadbeef"] = Encoding.UTF8.GetBytes("corrupt-primary"),
                    ["save.backup.json.corrupt-20260715000000-feedface"] = Encoding.UTF8.GetBytes("corrupt-backup")
                };

                foreach (var item in expected)
                {
                    File.WriteAllBytes(Path.Combine(root, item.Key), item.Value);
                }

                File.WriteAllText(Path.Combine(root, "unrelated.txt"), "leave me alone");

                var snapshot = ProfileArtifactSnapshot.Create(root);
                snapshot.SnapshotVerifiedOriginals();
                snapshot.RemoveOriginalArtifacts();
                Assert.That(ProfileArtifactSnapshot.GetMatchingArtifacts(root), Is.Empty);

                File.WriteAllText(Path.Combine(root, "save.json"), "test-created");
                File.WriteAllText(Path.Combine(root, "save.backup.json.corrupt-test"), "test-created-corrupt");
                snapshot.RestoreAndVerifyOriginals();
                snapshot.DeleteSnapshotDirectory();

                string[] finalMatchingNames = ProfileArtifactSnapshot.GetMatchingArtifacts(root).Select(Path.GetFileName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
                Assert.That(finalMatchingNames, Is.EqualTo(expected.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()));
                foreach (var item in expected)
                {
                    Assert.That(File.ReadAllBytes(Path.Combine(root, item.Key)), Is.EqualTo(item.Value), item.Key);
                }

                Assert.That(File.ReadAllText(Path.Combine(root, "unrelated.txt")), Is.EqualTo("leave me alone"));
            }
            finally
            {
                DeleteFakeRoot(root);
            }
        }

        // ==========================================================================================
        // Snapshot/restore — idempotent for empty roots.
        // ==========================================================================================
        [Test]
        public void SnapshotRestoreIsIdempotentForEmptyRoots()
        {
            string root = CreateFakeRoot();

            try
            {
                var snapshot = ProfileArtifactSnapshot.Create(root);
                snapshot.SnapshotVerifiedOriginals();
                snapshot.RemoveOriginalArtifacts();
                snapshot.RestoreAndVerifyOriginals();
                snapshot.RestoreAndVerifyOriginals();
                snapshot.DeleteSnapshotDirectory();
                snapshot.DeleteSnapshotDirectory();

                Assert.That(ProfileArtifactSnapshot.GetMatchingArtifacts(root), Is.Empty);
            }
            finally
            {
                DeleteFakeRoot(root);
            }
        }

        // ==========================================================================================
        // Snapshot/restore — attributes and last-write timestamps round-trip.
        // ==========================================================================================
        [Test]
        public void SnapshotRestoreRestoresAttributesAndTimestamps()
        {
            string root = CreateFakeRoot();

            try
            {
                string artifactPath = Path.Combine(root, "save.json");
                File.WriteAllBytes(artifactPath, Encoding.UTF8.GetBytes("attr-and-time"));
                DateTime stamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(artifactPath, stamp);

                // Read back what the OS actually stored so the assertion is platform-robust.
                FileAttributes expectedAttributes = File.GetAttributes(artifactPath);
                DateTime expectedStamp = File.GetLastWriteTimeUtc(artifactPath);

                var snapshot = ProfileArtifactSnapshot.Create(root);
                snapshot.SnapshotVerifiedOriginals();
                snapshot.RemoveOriginalArtifacts();

                File.WriteAllText(artifactPath, "decoy-created-by-test");
                File.SetLastWriteTimeUtc(artifactPath, DateTime.UtcNow);

                snapshot.RestoreAndVerifyOriginals();
                snapshot.DeleteSnapshotDirectory();

                Assert.That(File.GetLastWriteTimeUtc(artifactPath), Is.EqualTo(expectedStamp), "Last-write timestamp must be restored.");
                Assert.That(File.GetAttributes(artifactPath), Is.EqualTo(expectedAttributes), "File attributes must be restored.");
            }
            finally
            {
                DeleteFakeRoot(root);
            }
        }

        // ==========================================================================================
        // Snapshot/restore — restoration failure retains the recovery directory and reports its path.
        // ==========================================================================================
        [Test]
        public void SnapshotRestorationFailureRetainsRecoveryDirectoryWithPath()
        {
            string root = CreateFakeRoot();

            try
            {
                File.WriteAllBytes(Path.Combine(root, "save.json"), Encoding.UTF8.GetBytes("authentic-original"));

                var snapshot = ProfileArtifactSnapshot.Create(root);
                snapshot.SnapshotVerifiedOriginals();
                snapshot.RemoveOriginalArtifacts();

                // Corrupt the snapshot copy so the post-restore hash/length verification fails.
                string copy = Directory.GetFiles(snapshot.SnapshotDirectory).Single();
                File.WriteAllText(copy, "corrupted-snapshot-bytes-that-differ");

                var exception = Assert.Throws<ProfileRestorationException>(() => snapshot.RestoreAndVerifyOriginals());
                Assert.That(exception.Message, Does.Contain(snapshot.SnapshotDirectory),
                    "Restoration failure must report the recovery directory path.");
                Assert.That(Directory.Exists(snapshot.SnapshotDirectory), Is.True,
                    "Recovery snapshot directory must be retained on restoration failure.");

                snapshot.DeleteSnapshotDirectory();
            }
            finally
            {
                DeleteFakeRoot(root);
            }
        }

        // ==========================================================================================
        // Representative scene load helpers.
        // ==========================================================================================
        private IEnumerator LoadRepresentativeSceneWithTimeout()
        {
#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                RepresentativeScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            AsyncOperation load = SceneManager.LoadSceneAsync("Test", LoadSceneMode.Single);
#endif
            Assert.That(load, Is.Not.Null, "Expected representative scene load operation to start.");

            bool timedOut = false;
            yield return WaitUntilOrTimeout(() => load.isDone, LoadTimeoutSeconds, () => timedOut = true);
            if (timedOut)
            {
                Assert.Fail($"Timed out after {LoadTimeoutSeconds:0.0}s loading {RepresentativeScenePath}.");
            }
        }

        private static IEnumerator WaitForCoreServices()
        {
            bool timedOut = false;
            yield return WaitUntilOrTimeout(
                () => ServiceLocatorContains("AL.Core.Interfaces.ISaveGameService") &&
                      ServiceLocatorContains("AL.Core.Interfaces.IResourceService"),
                LoadTimeoutSeconds,
                () => timedOut = true);

            if (timedOut)
            {
                Assert.Fail("Timed out waiting for representative scene core services.");
            }
        }

        private static IEnumerator ObserveRealtimeWindow()
        {
            float started = Time.realtimeSinceStartup;
            int frames = 0;
            while (frames < ObservationFrames || Time.realtimeSinceStartup - started < ObservationSeconds)
            {
                frames++;
                yield return null;
            }
        }

        // ==========================================================================================
        // Scene / ServiceLocator / reflection helpers.
        // ==========================================================================================
        // One persistent, reused cleanup scene (finding 3): never accumulate a new placeholder per unload.
        private static Scene _reusableCleanupScene;

        private static void ActivateNonTargetScene(Scene target)
        {
            // Prefer any already-loaded scene other than the target.
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene candidate = SceneManager.GetSceneAt(index);
                if (candidate.isLoaded && candidate.handle != target.handle)
                {
                    SceneManager.SetActiveScene(candidate);
                    return;
                }
            }

            // The target is the only loaded scene: create the reusable cleanup scene once and reuse it.
            if (!_reusableCleanupScene.IsValid() || !_reusableCleanupScene.isLoaded)
            {
                _reusableCleanupScene = SceneManager.CreateScene("Issue127_ReusableCleanupScene");
            }

            SceneManager.SetActiveScene(_reusableCleanupScene);
        }

        private static Scene CreateSyntheticScene(string sceneLabel, string rootName, out GameObject root)
        {
            Scene scene = SceneManager.CreateScene(sceneLabel + "_" + Guid.NewGuid().ToString("N"));
            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            root = new GameObject(rootName);
            if (previous.IsValid())
            {
                SceneManager.SetActiveScene(previous);
            }

            return scene;
        }

        private static string FormatSevereLogs(IReadOnlyList<string> messages)
        {
            if (messages.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder("Unexpected severe logs during representative scene startup:");
            foreach (string message in messages)
            {
                builder.AppendLine().Append(message);
            }

            return builder.ToString();
        }

        private static string CreateFakeRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLifeProfileFake_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteFakeRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private static void ClearServiceLocator()
        {
            IDictionary services = GetServiceLocatorDictionary();
            services?.Clear();
        }

        private static void SeedServiceLocator(int count)
        {
            IDictionary services = GetServiceLocatorDictionary();
            if (services == null)
            {
                Assert.Fail("ServiceLocator dictionary could not be resolved via reflection.");
                return;
            }

            // Sentinel keys that never collide with real service interface types.
            Type[] sentinelKeys = { typeof(int), typeof(long), typeof(float), typeof(string), typeof(bool) };
            for (int index = 0; index < count && index < sentinelKeys.Length; index++)
            {
                services[sentinelKeys[index]] = new object();
            }
        }

        private static int GetServiceLocatorCount()
        {
            return GetServiceLocatorDictionary()?.Count ?? 0;
        }

        private static bool ServiceLocatorContains(string fullTypeName)
        {
            IDictionary services = GetServiceLocatorDictionary();
            if (services == null)
            {
                return false;
            }

            foreach (object key in services.Keys)
            {
                if (key is Type type && type.FullName == fullTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetMarkerRuntimeOwnerId()
        {
            IDictionary services = GetServiceLocatorDictionary();
            if (services == null)
            {
                return null;
            }

            foreach (object key in services.Keys)
            {
                if (key is Type type && type.FullName == MarkerServiceTypeName)
                {
                    object marker = services[key];
                    PropertyInfo property = marker?.GetType().GetProperty("RuntimeOwnerId", BindingFlags.Public | BindingFlags.Instance);
                    return property?.GetValue(marker) as string;
                }
            }

            return null;
        }

        private static IDictionary GetServiceLocatorDictionary()
        {
            Type serviceLocator = ResolveTypeByName("AL.Core.ServiceLocator");
            FieldInfo servicesField = serviceLocator?.GetField("Services", BindingFlags.Static | BindingFlags.NonPublic);
            return servicesField?.GetValue(null) as IDictionary;
        }

        private static Type ResolveTypeByName(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
        }
    }

    // A MonoBehaviour that attempts to write a profile artifact while alive and during OnDisable /
    // OnDestroy, standing in for any late production writer (e.g. Bootloader standby-reclaim
    // RunAutoLoad, OnApplicationPause save). It only ever targets a fake root supplied by the test.
    internal sealed class LateWriteProbe : MonoBehaviour
    {
        public string TargetDirectory;
        public string ArtifactFileName = "save.json";
        public byte[] LateBytes = Array.Empty<byte>();
        public int WriteCount { get; private set; }

        private void Update()
        {
            TryWrite();
        }

        private void OnDisable()
        {
            TryWrite();
        }

        private void OnDestroy()
        {
            TryWrite();
        }

        private void TryWrite()
        {
            if (string.IsNullOrEmpty(TargetDirectory) || !Directory.Exists(TargetDirectory))
            {
                return;
            }

            File.WriteAllBytes(Path.Combine(TargetDirectory, ArtifactFileName), LateBytes);
            WriteCount++;
        }
    }

    internal sealed class SevereLogCollector
    {
        private readonly List<string> _messages = new List<string>();
        private bool _started;

        public IReadOnlyList<string> Messages => _messages;

        public static bool IsSevere(LogType type)
        {
            return type == LogType.Error || type == LogType.Assert || type == LogType.Exception;
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            Application.logMessageReceived += HandleLog;
            _started = true;
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            Application.logMessageReceived -= HandleLog;
            _started = false;
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (!IsSevere(type))
            {
                return;
            }

            _messages.Add($"{type}: {condition}\n{stackTrace}");
        }
    }

    // Captures and restores global test/runtime state, restoring ONLY when a capture actually
    // happened. This is the fix for the reopened defect where a helper [Test] that never captured
    // could have its teardown restore the default Time.timeScale value of 0 and pause the runner.
    internal sealed class GlobalStateGuard
    {
        private bool _originalIgnoreFailingMessages;
        private float _originalTimeScale;
        private string _incomingActiveScenePath;
        private byte[] _buildSettingsBytesBefore;

        public bool Captured { get; private set; }
        public string IncomingActiveScenePath => _incomingActiveScenePath;

        public void Capture()
        {
            _originalIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            _originalTimeScale = Time.timeScale;
            _incomingActiveScenePath = SceneManager.GetActiveScene().path;
            _buildSettingsBytesBefore = ReadBuildSettingsBytes();

            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;
            Captured = true;
        }

        public void Restore()
        {
            if (!Captured)
            {
                return;
            }

            Time.timeScale = _originalTimeScale;
            LogAssert.ignoreFailingMessages = _originalIgnoreFailingMessages;
        }

        public void AssertBuildSettingsUnchanged(string representativeScenePath)
        {
            if (!Captured || _buildSettingsBytesBefore == null)
            {
                return;
            }

            byte[] after = ReadBuildSettingsBytes();
            Assert.That(after, Is.EqualTo(_buildSettingsBytesBefore), "EditorBuildSettings.asset changed during #127 PlayMode smoke.");
            string text = Encoding.UTF8.GetString(after);
            Assert.That(text, Does.Not.Contain(representativeScenePath), "Representative scene must remain absent from production Build Settings.");
        }

        private static byte[] ReadBuildSettingsBytes()
        {
            string path = Path.Combine(Application.dataPath, "..", "ProjectSettings", "EditorBuildSettings.asset");
            return File.ReadAllBytes(Path.GetFullPath(path));
        }
    }

    internal sealed class ProfileRestorationException : Exception
    {
        public ProfileRestorationException(string message)
            : base(message)
        {
        }
    }

    internal sealed class ProfileArtifactSnapshot
    {
        private static readonly string[] ExactFileNames =
        {
            "save.json",
            "save.backup.json",
            "save.tmp.json",
            "save.previous.json",
            "save.json.previous"
        };

        private readonly string _persistentRoot;
        private readonly List<ProfileArtifactRecord> _originals = new List<ProfileArtifactRecord>();
        private bool _snapshotVerified;

        private ProfileArtifactSnapshot(string persistentRoot, string snapshotDirectory)
        {
            _persistentRoot = Path.GetFullPath(persistentRoot);
            SnapshotDirectory = snapshotDirectory;
        }

        public string SnapshotDirectory { get; }

        // True once the verified snapshot has been consumed (restored and deleted). Guarantees the
        // ordered cleanup is idempotent: further delete/restore calls become no-ops rather than
        // deleting restored originals or copying from a deleted snapshot directory.
        private bool Consumed => _snapshotVerified && !Directory.Exists(SnapshotDirectory);

        public static ProfileArtifactSnapshot Create(string persistentRoot)
        {
            Directory.CreateDirectory(persistentRoot);
            string snapshotRoot = Path.Combine(Path.GetTempPath(), "AnotherLifePlayModeProfileSnapshots");
            Directory.CreateDirectory(snapshotRoot);
            string snapshotDirectory = Path.Combine(snapshotRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(snapshotDirectory);
            return new ProfileArtifactSnapshot(persistentRoot, snapshotDirectory);
        }

        public static bool IsProfileArtifactFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            if (fileName.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0)
            {
                return false;
            }

            if (ExactFileNames.Any(name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return fileName.StartsWith("save.json.corrupt-", StringComparison.OrdinalIgnoreCase) ||
                   fileName.StartsWith("save.backup.json.corrupt-", StringComparison.OrdinalIgnoreCase);
        }

        public static string[] GetMatchingArtifacts(string root)
        {
            if (!Directory.Exists(root))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(root)
                .Where(path => IsProfileArtifactFileName(Path.GetFileName(path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public void SnapshotVerifiedOriginals()
        {
            _originals.Clear();
            Directory.CreateDirectory(SnapshotDirectory);

            string[] artifactPaths = GetMatchingArtifacts(_persistentRoot);
            for (int index = 0; index < artifactPaths.Length; index++)
            {
                string artifactPath = artifactPaths[index];
                string fileName = Path.GetFileName(artifactPath);
                string snapshotPath = Path.Combine(SnapshotDirectory, $"{index:D3}_{fileName}");
                File.Copy(artifactPath, snapshotPath, overwrite: false);

                var record = new ProfileArtifactRecord(
                    fileName,
                    artifactPath,
                    snapshotPath,
                    new FileInfo(artifactPath).Length,
                    Sha256(artifactPath),
                    File.GetAttributes(artifactPath),
                    File.GetLastWriteTimeUtc(artifactPath));
                Assert.That(Sha256(snapshotPath), Is.EqualTo(record.Hash), "Snapshot hash verification failed for " + fileName);
                _originals.Add(record);
            }

            _snapshotVerified = true;
        }

        public void RemoveOriginalArtifacts()
        {
            Assert.That(_snapshotVerified, Is.True, "Profile artifacts must be snapshotted and verified before removal.");

            try
            {
                foreach (ProfileArtifactRecord original in _originals)
                {
                    if (File.Exists(original.OriginalPath))
                    {
                        File.Delete(original.OriginalPath);
                    }
                }

                Assert.That(GetMatchingArtifacts(_persistentRoot), Is.Empty, "Persistent root must contain zero matching artifacts before scene startup.");
            }
            catch
            {
                RestoreAndVerifyOriginals();
                throw;
            }
        }

        // Purges every matching artifact currently present in the profile root. The verified originals
        // live safely in the external snapshot, so this only ever removes test-created (disposable)
        // artifacts and prepares the root for the exact restore that follows.
        public void DeleteDisposableArtifacts()
        {
            if (!_snapshotVerified || Consumed)
            {
                return;
            }

            foreach (string path in GetMatchingArtifacts(_persistentRoot))
            {
                File.Delete(path);
            }
        }

        public void RestoreAndVerifyOriginals()
        {
            if (!_snapshotVerified || Consumed)
            {
                return;
            }

            Directory.CreateDirectory(_persistentRoot);

            foreach (string artifactPath in GetMatchingArtifacts(_persistentRoot))
            {
                File.Delete(artifactPath);
            }

            foreach (ProfileArtifactRecord original in _originals)
            {
                File.Copy(original.SnapshotPath, original.OriginalPath, overwrite: true);
                File.SetAttributes(original.OriginalPath, original.Attributes);
                File.SetLastWriteTimeUtc(original.OriginalPath, original.LastWriteUtc);
            }

            string[] finalNames = GetMatchingArtifacts(_persistentRoot).Select(Path.GetFileName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            string[] originalNames = _originals.Select(item => item.FileName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            if (!finalNames.SequenceEqual(originalNames, StringComparer.OrdinalIgnoreCase))
            {
                throw new ProfileRestorationException(
                    $"Profile artifact set was not restored exactly. Recovery snapshot retained at: {SnapshotDirectory}. " +
                    $"Expected [{string.Join(", ", originalNames)}] but found [{string.Join(", ", finalNames)}].");
            }

            foreach (ProfileArtifactRecord original in _originals)
            {
                long restoredLength = new FileInfo(original.OriginalPath).Length;
                if (restoredLength != original.Length)
                {
                    throw new ProfileRestorationException(
                        $"Restored length mismatch for {original.FileName} ({restoredLength} != {original.Length}). " +
                        $"Recovery snapshot retained at: {SnapshotDirectory}.");
                }

                string restoredHash = Sha256(original.OriginalPath);
                if (!string.Equals(restoredHash, original.Hash, StringComparison.Ordinal))
                {
                    throw new ProfileRestorationException(
                        $"Restored hash mismatch for {original.FileName}. Recovery snapshot retained at: {SnapshotDirectory}.");
                }
            }
        }

        public void DeleteSnapshotDirectory()
        {
            if (Directory.Exists(SnapshotDirectory))
            {
                Directory.Delete(SnapshotDirectory, recursive: true);
            }
        }

        private static string Sha256(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            byte[] hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        private readonly struct ProfileArtifactRecord
        {
            public ProfileArtifactRecord(string fileName, string originalPath, string snapshotPath, long length, string hash, FileAttributes attributes, DateTime lastWriteUtc)
            {
                FileName = fileName;
                OriginalPath = originalPath;
                SnapshotPath = snapshotPath;
                Length = length;
                Hash = hash;
                Attributes = attributes;
                LastWriteUtc = lastWriteUtc;
            }

            public string FileName { get; }
            public string OriginalPath { get; }
            public string SnapshotPath { get; }
            public long Length { get; }
            public string Hash { get; }
            public FileAttributes Attributes { get; }
            public DateTime LastWriteUtc { get; }
        }
    }
}
