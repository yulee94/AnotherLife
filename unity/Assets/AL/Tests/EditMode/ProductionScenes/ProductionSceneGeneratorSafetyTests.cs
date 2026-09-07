using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using R = AL.Tests.EditMode.ProductionScenes.ProductionSceneTestReflection;

namespace AL.Tests.EditMode.ProductionScenes
{
    /// <summary>
    /// Generator-safety contract (#223 "Required tests" generator-safety family): valid scenes are a typed
    /// no-op, generation/validation leave EditorBuildSettings.asset byte-for-byte unchanged, runs are
    /// idempotent and preserve scene/.meta GUIDs, drift blocks generation, save failure is honest, the
    /// reviewed-regeneration token is enforced, and batch status maps to a stable exit code.
    /// </summary>
    public sealed class ProductionSceneGeneratorSafetyTests
    {
        private static string BuildSettingsPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ProjectSettings", "EditorBuildSettings.asset"));

        [Test]
        public void CharacterCreationFolderHasAStableValidAssetGuid()
        {
            const string folderPath = "Assets/AL/Scripts/UI/CharacterCreation";
            string metaPath = Path.Combine(Application.dataPath, "AL/Scripts/UI/CharacterCreation.meta");
            string guidLine = File.ReadAllLines(metaPath).Single(line => line.StartsWith("guid: ", StringComparison.Ordinal));
            string guid = guidLine.Substring("guid: ".Length);

            Assert.That(guid, Does.Match("^[0-9a-f]{32}$"), "A malformed folder GUID breaks asset refresh during scene generation.");
            Assert.That(AssetDatabase.IsValidFolder(folderPath), Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(folderPath), Is.EqualTo(guid));
            Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(folderPath));
        }

        [Test]
        public void ValidCommittedScenesAreATypedNoOp()
        {
            object result = R.StaticMethod(R.GeneratorType, "GenerateMissingProductionScenes");
            Assert.AreEqual("AllValid", R.Prop(result, "Status").ToString());
            Assert.IsEmpty(R.AsStrings(R.Prop(result, "CreatedScenes")), "No scene should be created when all are valid.");
            Assert.IsTrue(R.PropBool(result, "Succeeded"));
        }

        [Test]
        public void CommittedScenesValidateCleanWithNoMissingScripts()
        {
            object report = R.StaticMethod(R.GeneratorType, "ValidateProductionScenes");
            Assert.IsTrue(R.PropBool(report, "IsValid"), R.Invoke(report, "Summarize").ToString());

            foreach (object scene in R.AsObjects(R.Prop(report, "Scenes")))
            {
                Assert.AreEqual("Valid", R.Prop(scene, "Status").ToString(), R.PropString(scene, "SceneId"));
                Assert.AreEqual(0, R.PropInt(scene, "MissingScriptCount"), R.PropString(scene, "SceneId") + " missing scripts");
                Assert.AreEqual(1, R.PropInt(scene, "ControllerCount"));
                Assert.AreEqual(1, R.PropInt(scene, "EventSystemCount"));
                Assert.AreEqual(1, R.PropInt(scene, "MarkerCount"));
                Assert.AreEqual(1, R.PropInt(scene, "BootloaderCount"));
            }
        }

        [Test]
        public void GenerationAndValidationLeaveBuildSettingsByteForByteUnchanged()
        {
            byte[] before = File.ReadAllBytes(BuildSettingsPath);

            R.StaticMethod(R.GeneratorType, "GenerateMissingProductionScenes");
            R.StaticMethod(R.GeneratorType, "ValidateProductionScenes");

            byte[] after = File.ReadAllBytes(BuildSettingsPath);
            Assert.AreEqual(before, after, "EditorBuildSettings.asset must not change during authoring/validation.");

            CollectionAssert.AreEqual(
                new[]
                {
                    "Assets/AL/Scenes/Boot.unity",
                    "Assets/AL/Scenes/RealmSelection.unity",
                    "Assets/AL/Scenes/CharacterCreation.unity",
                    "Assets/AL/Scenes/ChampionArena.unity",
                    "Assets/AL/Scenes/Kingdom.unity"
                },
                EditorBuildSettings.scenes.Select(scene => scene.path).ToArray(),
                "Authoring must preserve the exact committed ShellFoundation Build Settings order.");
            Assert.That(EditorBuildSettings.scenes.All(scene => scene.enabled), Is.True,
                "Every committed ShellFoundation scene must remain enabled.");

            string text = System.Text.Encoding.UTF8.GetString(after);
            Assert.That(text, Does.Not.Contain("Assets/Test.unity"));
            Assert.That(text, Does.Contain("Assets/AL/Scenes/ChampionArena.unity"));
            Assert.That(text, Does.Contain("Assets/AL/Scenes/CharacterCreation.unity"));
        }

        [Test]
        public void RepeatRunsAreIdempotentAndPreserveSceneAndMetaBytes()
        {
            Dictionary<string, byte[]> before = SnapshotSceneFiles();

            object first = R.StaticMethod(R.GeneratorType, "GenerateMissingProductionScenes");
            object second = R.StaticMethod(R.GeneratorType, "GenerateMissingProductionScenes");
            Assert.AreEqual("AllValid", R.Prop(first, "Status").ToString());
            Assert.AreEqual("AllValid", R.Prop(second, "Status").ToString());

            Dictionary<string, byte[]> after = SnapshotSceneFiles();
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            foreach (var pair in before)
            {
                Assert.AreEqual(pair.Value, after[pair.Key], $"File changed across idempotent runs: {pair.Key}");
            }
        }

        [Test]
        public void SceneGuidsArePreservedAcrossValidation()
        {
            var before = SceneGuids();
            R.StaticMethod(R.GeneratorType, "GenerateMissingProductionScenes");
            R.StaticMethod(R.GeneratorType, "ValidateProductionScenes");
            var after = SceneGuids();
            CollectionAssert.AreEqual(before, after, "Scene .meta GUIDs must be preserved.");
            Assert.IsTrue(before.Values.All(g => g.Length == 32));
        }

        [Test]
        public void DecideGenerationBlocksOnDrift()
        {
            object report = BuildInspectionReport(("al_scene_boot", "Drifted"), ("al_scene_kingdom", "Valid"));
            object plan = R.StaticMethod(R.GeneratorType, "DecideGeneration", report);
            Assert.IsTrue(R.PropBool(plan, "IsBlocked"));
            Assert.That(R.AsStrings(R.Prop(plan, "DriftedSceneIds")), Has.Member("al_scene_boot"));
        }

        [Test]
        public void DecideGenerationSchedulesOnlyMissingScenes()
        {
            object report = BuildInspectionReport(
                ("al_scene_boot", "Valid"),
                ("al_scene_realm_selection", "Missing"),
                ("al_scene_kingdom", "Missing"),
                ("al_scene_champion_arena", "Valid"));
            object plan = R.StaticMethod(R.GeneratorType, "DecideGeneration", report);
            Assert.IsFalse(R.PropBool(plan, "IsBlocked"));
            Assert.That(R.AsStrings(R.Prop(plan, "MissingSceneIds")),
                Is.EquivalentTo(new[] { "al_scene_realm_selection", "al_scene_kingdom" }));
        }

        [Test]
        public void BatchExitCodeMapsSuccessToZeroAndFailureToOne()
        {
            Type statusType = R.Runtime("AL.EditorTools.SceneGenerationStatus");
            Assert.AreEqual(0, ToExitCode(statusType, "AllValid"));
            Assert.AreEqual(0, ToExitCode(statusType, "CreatedMissing"));
            Assert.AreEqual(1, ToExitCode(statusType, "DriftBlocked"));
            Assert.AreEqual(1, ToExitCode(statusType, "SaveFailed"));
            Assert.AreEqual(1, ToExitCode(statusType, "SerializationModeInvalid"));
        }

        [Test]
        public void SaveFailureResultIsHonestAndClaimsNoOverwrite()
        {
            Type resultType = R.Runtime("AL.EditorTools.SceneGenerationResult");
            object created = new[] { "Assets/AL/Scenes/Boot.unity" };
            object result = resultType
                .GetMethod("SaveFailure", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { "Assets/AL/Scenes/RealmSelection.unity", created });

            Assert.AreEqual("SaveFailed", R.Prop(result, "Status").ToString());
            Assert.IsFalse(R.PropBool(result, "Succeeded"));
            Assert.That(R.AsStrings(R.Prop(result, "Messages")).Any(m => m.Contains("No existing scene was overwritten")));
        }

        [Test]
        public void RegenerationRequiresExactReviewedToken()
        {
            object result = R.StaticMethod(R.GeneratorType, "RegenerateProductionScenes", "wrong-token");
            Assert.AreEqual("RegenerationTokenRejected", R.Prop(result, "Status").ToString());
            Assert.IsFalse(R.PropBool(result, "Succeeded"));
        }

        [Test]
        public void RegenerationWithTokenPreservesGuidsAndSemanticValidityThenRestoresBytes()
        {
            Dictionary<string, byte[]> before = SnapshotSceneFiles();
            SortedDictionary<string, string> guidsBefore = SceneGuids();
            try
            {
                object result = R.StaticMethod(R.GeneratorType, "RegenerateProductionScenes", RegenerateToken());
                Assert.IsTrue(R.PropBool(result, "Succeeded"), "Reviewed-token regeneration must succeed: " + Messages(result));

                CollectionAssert.AreEqual(guidsBefore, SceneGuids(), "Regeneration must preserve every .meta GUID.");

                object report = R.StaticMethod(R.GeneratorType, "ValidateProductionScenes");
                Assert.IsTrue(R.PropBool(report, "IsValid"), "Regenerated scenes must be semantically valid: " + R.Invoke(report, "Summarize"));
            }
            finally
            {
                RestoreSceneFiles(before);
            }

            Dictionary<string, byte[]> after = SnapshotSceneFiles();
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            foreach (var pair in before)
            {
                Assert.AreEqual(pair.Value, after[pair.Key], "Working tree must be byte-identical after restore: " + pair.Key);
            }
        }

        [Test]
        public void MidBatchSaveFailureViaSeamLeavesCommittedScenesUnchanged()
        {
            Dictionary<string, byte[]> before = SnapshotSceneFiles();
            int calls = 0;
            // Intercept saves without touching disk: succeed on the first, fail on a later scene (N>1).
            Func<Scene, string, bool> failLater = (scene, path) => { calls++; return calls < 2; };
            R.SetStaticField(R.GeneratorType, "SaveSceneOverride", failLater);
            try
            {
                object result = R.StaticMethod(R.GeneratorType, "RegenerateProductionScenes", RegenerateToken());
                Assert.AreEqual("SaveFailed", R.Prop(result, "Status").ToString(), Messages(result));
                Assert.IsFalse(R.PropBool(result, "Succeeded"));
                Assert.GreaterOrEqual(calls, 2, "Failure must occur after the first save (mid-batch).");
            }
            finally
            {
                R.SetStaticField(R.GeneratorType, "SaveSceneOverride", null);
            }

            Dictionary<string, byte[]> after = SnapshotSceneFiles();
            foreach (var pair in before)
            {
                Assert.AreEqual(pair.Value, after[pair.Key], "No committed scene may change on a mid-batch save failure: " + pair.Key);
            }
        }

        [Test]
        public void DriftedCommittedSceneBlocksGenerationAndOverwritesNothing()
        {
            Dictionary<string, byte[]> before = SnapshotSceneFiles();
            const string kingdomPath = "Assets/AL/Scenes/Kingdom.unity";
            try
            {
                // Introduce real on-disk drift: a second EventSystem saved into the committed Kingdom scene.
                Scene scene = EditorSceneManager.OpenScene(kingdomPath, OpenSceneMode.Additive);
                var extra = new GameObject("DriftEventSystem");
                SceneManager.MoveGameObjectToScene(extra, scene);
                extra.AddComponent<EventSystem>();
                EditorSceneManager.SaveScene(scene, kingdomPath);
                EditorSceneManager.CloseScene(scene, removeScene: true);
                AssetDatabase.Refresh();

                object result = R.StaticMethod(R.GeneratorType, "GenerateMissingProductionScenes");
                Assert.AreEqual("DriftBlocked", R.Prop(result, "Status").ToString(), Messages(result));
                Assert.IsFalse(R.PropBool(result, "Succeeded"));

                // The generator authored nothing: every non-drifted scene is byte-unchanged.
                Dictionary<string, byte[]> mid = SnapshotSceneFiles();
                foreach (var pair in before)
                {
                    if (pair.Key.Contains("Kingdom"))
                    {
                        continue;
                    }

                    Assert.AreEqual(pair.Value, mid[pair.Key], "Drift refusal must not overwrite other scenes: " + pair.Key);
                }
            }
            finally
            {
                RestoreSceneFiles(before);
            }

            Dictionary<string, byte[]> after = SnapshotSceneFiles();
            foreach (var pair in before)
            {
                Assert.AreEqual(pair.Value, after[pair.Key], "Working tree must be byte-identical after restore: " + pair.Key);
            }
        }

        // -------------------------------------------------------------------

        private static string RegenerateToken()
        {
            return (string)R.StaticField(R.GeneratorType, "RegenerateConfirmToken");
        }

        private static string Messages(object result)
        {
            return string.Join(" | ", R.AsStrings(R.Prop(result, "Messages")));
        }

        private static void RestoreSceneFiles(Dictionary<string, byte[]> snapshot)
        {
            foreach (var pair in snapshot)
            {
                const int maxAttempts = 50;
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        File.WriteAllBytes(pair.Key, pair.Value);
                        break;
                    }
                    catch (IOException) when (attempt < maxAttempts)
                    {
                        Thread.Sleep(100);
                    }
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static int ToExitCode(Type statusType, string statusName)
        {
            object status = Enum.Parse(statusType, statusName);
            return (int)R.StaticMethod(R.GeneratorType, "ToExitCode", status);
        }

        private static object BuildInspectionReport(params (string sceneId, string status)[] entries)
        {
            Type entryType = R.Runtime("AL.EditorTools.SceneInspectionEntry");
            Type reportType = R.Runtime("AL.EditorTools.SceneInspectionReport");
            Type statusType = R.Runtime("AL.EditorTools.SceneValidationStatus");

            Array entryArray = Array.CreateInstance(entryType, entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                object status = Enum.Parse(statusType, entries[i].status);
                object entry = Activator.CreateInstance(entryType, entries[i].sceneId, "Assets/AL/Scenes/x.unity", status, new string[0]);
                entryArray.SetValue(entry, i);
            }

            return Activator.CreateInstance(reportType, entryArray, new string[0]);
        }

        private static Dictionary<string, byte[]> SnapshotSceneFiles()
        {
            var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (object record in R.DescriptorProduction())
            {
                string path = R.PropString(record, "AssetPath");
                if (File.Exists(path))
                {
                    map[path] = File.ReadAllBytes(path);
                }

                if (File.Exists(path + ".meta"))
                {
                    map[path + ".meta"] = File.ReadAllBytes(path + ".meta");
                }
            }

            return map;
        }

        private static SortedDictionary<string, string> SceneGuids()
        {
            var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (object record in R.DescriptorProduction())
            {
                string path = R.PropString(record, "AssetPath");
                string metaPath = path + ".meta";
                if (File.Exists(metaPath))
                {
                    string guidLine = File.ReadAllLines(metaPath).FirstOrDefault(l => l.StartsWith("guid:", StringComparison.Ordinal));
                    map[path] = guidLine?.Substring("guid:".Length).Trim() ?? string.Empty;
                }
            }

            return map;
        }
    }
}
