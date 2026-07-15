using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
    public sealed class RepresentativeSceneSmokeTests
    {
        private const string RepresentativeScenePath = "Assets/Test.unity";
        private const float LoadTimeoutSeconds = 15f;
        private const float ObservationSeconds = 0.5f;
        private const int ObservationFrames = 5;

        private ProfileArtifactSnapshot _profileSnapshot;
        private SevereLogCollector _severeLogs;
        private bool _originalIgnoreFailingMessages;
        private float _originalTimeScale;
        private byte[] _buildSettingsBytesBefore;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return LoadEmptyCleanupScene();
            CleanupProfileAndGlobals();
        }

        [UnityTest]
        public IEnumerator RepresentativeTestSceneLoadsWithIsolatedProfileAndNoSevereLogs()
        {
            CaptureGlobalState();

            try
            {
                _profileSnapshot = ProfileArtifactSnapshot.Create(Application.persistentDataPath);
                Debug.Log("[Issue127] Profile snapshot directory: " + _profileSnapshot.SnapshotDirectory);
                _profileSnapshot.SnapshotVerifiedOriginals();
                _profileSnapshot.RemoveOriginalArtifacts();
                Assert.That(ProfileArtifactSnapshot.GetMatchingArtifacts(Application.persistentDataPath), Is.Empty);

                ClearServiceLocator();
                Assert.That(GetServiceLocatorCount(), Is.EqualTo(0), "ServiceLocator must be empty before representative scene startup.");

                _severeLogs = new SevereLogCollector();
                _severeLogs.Start();

                yield return LoadRepresentativeSceneWithTimeout();

                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(RepresentativeScenePath));
                var manager = GameObject.Find("Demo_Manager");
                Assert.That(manager, Is.Not.Null, "Representative scene startup signal `Demo_Manager` should exist.");
                Assert.That(manager.activeInHierarchy, Is.True, "Representative startup manager should be active.");

                yield return WaitForCoreServices();
                yield return ObserveRealtimeWindow();

                Assert.That(_severeLogs.Messages, Is.Empty, FormatSevereLogs(_severeLogs.Messages));
            }
            finally
            {
                CleanupProfileAndGlobals();
            }
        }

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

        [Test]
        public void SnapshotRestorePreservesBytesAndRemovesTestArtifacts()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLifeProfileFake_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

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
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void SnapshotRestoreIsIdempotentForEmptyRoots()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLifeProfileEmpty_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

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
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private void CaptureGlobalState()
        {
            _originalIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            _originalTimeScale = Time.timeScale;
            _buildSettingsBytesBefore = ReadBuildSettingsBytes();
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;
        }

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

            float started = Time.realtimeSinceStartup;
            while (!load.isDone)
            {
                if (Time.realtimeSinceStartup - started > LoadTimeoutSeconds)
                {
                    Assert.Fail($"Timed out after {LoadTimeoutSeconds:0.0}s loading {RepresentativeScenePath}.");
                }

                yield return null;
            }
        }

        private static IEnumerator WaitForCoreServices()
        {
            float started = Time.realtimeSinceStartup;
            while (!(ServiceLocatorContains("AL.Core.Interfaces.ISaveGameService") &&
                     ServiceLocatorContains("AL.Core.Interfaces.IResourceService")))
            {
                if (Time.realtimeSinceStartup - started > LoadTimeoutSeconds)
                {
                    Assert.Fail("Timed out waiting for representative scene core services.");
                }

                yield return null;
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

        private static IEnumerator LoadEmptyCleanupScene()
        {
            var scene = SceneManager.CreateScene("Issue127_CleanupScene_" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            yield return null;
        }

        private void CleanupProfileAndGlobals()
        {
            _severeLogs?.Stop();
            _severeLogs = null;

            DestroyActiveSceneRoots();
            ClearServiceLocator();

            _profileSnapshot?.RestoreAndVerifyOriginals();
            _profileSnapshot?.DeleteSnapshotDirectory();
            _profileSnapshot = null;

            Time.timeScale = _originalTimeScale;
            LogAssert.ignoreFailingMessages = _originalIgnoreFailingMessages;

            AssertBuildSettingsUnchanged();
        }

        private static void DestroyActiveSceneRoots()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root != null)
                {
                    Object.Destroy(root);
                }
            }
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

        private void AssertBuildSettingsUnchanged()
        {
            if (_buildSettingsBytesBefore == null)
            {
                return;
            }

            byte[] after = ReadBuildSettingsBytes();
            Assert.That(after, Is.EqualTo(_buildSettingsBytesBefore), "EditorBuildSettings.asset changed during #127 PlayMode smoke.");
            string text = Encoding.UTF8.GetString(after);
            Assert.That(text, Does.Not.Contain(RepresentativeScenePath), "Representative scene must remain absent from production Build Settings.");
        }

        private static byte[] ReadBuildSettingsBytes()
        {
            string path = Path.Combine(Application.dataPath, "..", "ProjectSettings", "EditorBuildSettings.asset");
            return File.ReadAllBytes(Path.GetFullPath(path));
        }

        private static void ClearServiceLocator()
        {
            IDictionary services = GetServiceLocatorDictionary();
            services?.Clear();
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

        private static IDictionary GetServiceLocatorDictionary()
        {
            Type serviceLocator = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("AL.Core.ServiceLocator"))
                .FirstOrDefault(type => type != null);
            FieldInfo servicesField = serviceLocator?.GetField("Services", BindingFlags.Static | BindingFlags.NonPublic);
            return servicesField?.GetValue(null) as IDictionary;
        }
    }

    internal sealed class SevereLogCollector
    {
        private readonly List<string> _messages = new List<string>();
        private bool _started;

        public IReadOnlyList<string> Messages => _messages;

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
            if (type != LogType.Error && type != LogType.Assert && type != LogType.Exception)
            {
                return;
            }

            _messages.Add($"{type}: {condition}\n{stackTrace}");
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

            var removed = new List<string>();
            try
            {
                foreach (ProfileArtifactRecord original in _originals)
                {
                    if (File.Exists(original.OriginalPath))
                    {
                        File.Delete(original.OriginalPath);
                        removed.Add(original.OriginalPath);
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

        public void RestoreAndVerifyOriginals()
        {
            if (!_snapshotVerified)
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
            Assert.That(finalNames, Is.EqualTo(originalNames), "Profile artifact set was not restored exactly.");

            foreach (ProfileArtifactRecord original in _originals)
            {
                Assert.That(new FileInfo(original.OriginalPath).Length, Is.EqualTo(original.Length), "Restored length mismatch for " + original.FileName);
                Assert.That(Sha256(original.OriginalPath), Is.EqualTo(original.Hash), "Restored hash mismatch for " + original.FileName);
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
