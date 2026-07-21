using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Scene-structure contract (#223 "Required tests" scene-structure family). Opens each committed
    /// production scene (the generator's PopulateProductionScene output) additively and drives the drift
    /// validator against it: controllers/EventSystem/marker/Bootloader exactly once, zero missing scripts,
    /// exact serialized transition strings, and marker fields == descriptor. Drift fixtures mutate the
    /// in-memory scene and are closed WITHOUT saving, so the committed assets are never modified.
    /// </summary>
    public sealed class ProductionSceneStructureTests
    {
        private readonly List<Scene> _openScenes = new List<Scene>();

        [TearDown]
        public void CloseScenes()
        {
            foreach (Scene scene in _openScenes)
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    // removeScene:true discards in-memory drift mutations without saving the asset.
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }

            _openScenes.Clear();
        }

        [Test]
        public void EachCommittedProductionSceneHasExactMinimumStructure()
        {
            foreach (object record in R.DescriptorProduction())
            {
                object result = OpenAndValidate(record, out _);
                Assert.AreEqual("Valid", R.Prop(result, "Status").ToString(),
                    Label(record, "status") + ": " + string.Join(" | ", R.AsStrings(R.Prop(result, "Diffs"))));
                Assert.AreEqual(1, R.PropInt(result, "ControllerCount"), Label(record, "controller count"));
                Assert.AreEqual(1, R.PropInt(result, "EventSystemCount"), Label(record, "EventSystem count"));
                Assert.AreEqual(1, R.PropInt(result, "MarkerCount"), Label(record, "marker count"));
                Assert.AreEqual(1, R.PropInt(result, "BootloaderCount"), Label(record, "Bootloader owner count"));
                Assert.AreEqual(0, R.PropInt(result, "MissingScriptCount"), Label(record, "missing scripts"));
            }
        }

        [Test]
        public void BootSceneCarriesBootloaderAndBootControllerAndMarker()
        {
            object boot = R.RecordById("al_scene_boot");
            OpenScene(boot, out Scene scene);

            Assert.IsNotNull(FindComponent(scene, R.Runtime("AL.Core.Bootloader")), "Boot must carry a Bootloader owner.");
            Assert.IsNotNull(FindComponent(scene, R.Runtime("AL.UI.BootController")), "Boot must carry a BootController.");
            Assert.IsNotNull(FindComponent(scene, R.Runtime(R.MarkerType)), "Boot must carry a SceneStartupMarker.");
            Assert.IsNotNull(FindComponent(scene, typeof(EventSystem)), "Boot must carry an EventSystem.");
        }

        [Test]
        public void MarkerFieldsMatchDescriptor()
        {
            foreach (object record in R.DescriptorProduction())
            {
                OpenScene(record, out Scene scene);
                object marker = FindComponent(scene, R.Runtime(R.MarkerType));
                Assert.NotNull(marker, Label(record, "marker present"));
                Assert.AreEqual(R.PropString(record, "SceneId"), R.PropString(marker, "SceneId"), Label(record, "marker sceneId"));
                Assert.AreEqual(R.PropString(record, "AssetPath"), R.PropString(marker, "ExpectedAssetPath"), Label(record, "marker path"));
                Assert.AreEqual(R.PropString(record, "Role"), R.PropString(marker, "Role"), Label(record, "marker role"));
                Assert.AreEqual(R.SourceVersion(), R.PropString(marker, "SourceVersion"), Label(record, "marker version"));
            }
        }

        [Test]
        public void ActiveTransitionStringsMatchDescriptorExactly()
        {
            AssertTransition("al_scene_realm_selection", "_nextScene", "Kingdom");
            AssertTransition("al_scene_boot", "_realmSelectionScene", "RealmSelection");
            AssertTransition("al_scene_boot", "_kingdomScene", "Kingdom");
            AssertTransition("al_scene_champion_arena", "_kingdomSceneName", "Kingdom");
        }

        [Test]
        public void DuplicateEventSystemIsDrift()
        {
            object record = R.RecordById("al_scene_kingdom");
            OpenScene(record, out Scene scene);
            var extra = new GameObject("ExtraEventSystem");
            SceneManager.MoveGameObjectToScene(extra, scene);
            extra.AddComponent<EventSystem>();

            object result = Validate(record, scene);
            Assert.AreEqual(2, R.PropInt(result, "EventSystemCount"));
            AssertHasDiff(result, "EventSystem");
        }

        [Test]
        public void MissingControllerIsDrift()
        {
            object record = R.RecordById("al_scene_kingdom");
            OpenScene(record, out Scene scene);
            object controller = FindComponent(scene, R.Runtime("AL.UI.Kingdom.KingdomSceneController"));
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)controller);

            object result = Validate(record, scene);
            Assert.AreEqual(0, R.PropInt(result, "ControllerCount"));
            AssertHasDiff(result, "KingdomSceneController");
        }

        [Test]
        public void DuplicateBootloaderOwnerIsDrift()
        {
            object record = R.RecordById("al_scene_kingdom");
            OpenScene(record, out Scene scene);
            var extra = new GameObject("ExtraBootloader");
            SceneManager.MoveGameObjectToScene(extra, scene);
            extra.AddComponent(R.Runtime("AL.Core.Bootloader"));

            object result = Validate(record, scene);
            Assert.AreEqual(2, R.PropInt(result, "BootloaderCount"));
            AssertHasDiff(result, "Bootloader owner");
        }

        [Test]
        public void MissingMarkerIsDrift()
        {
            object record = R.RecordById("al_scene_kingdom");
            OpenScene(record, out Scene scene);
            object marker = FindComponent(scene, R.Runtime(R.MarkerType));
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)marker);

            object result = Validate(record, scene);
            Assert.AreEqual(0, R.PropInt(result, "MarkerCount"));
            AssertHasDiff(result, "SceneStartupMarker");
        }

        [Test]
        public void MarkerFieldDriftIsDetected()
        {
            object record = R.RecordById("al_scene_kingdom");
            OpenScene(record, out Scene scene);
            object marker = FindComponent(scene, R.Runtime(R.MarkerType));
            var serialized = new SerializedObject((UnityEngine.Object)marker);
            serialized.FindProperty("_role").stringValue = "wrong_role";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            object result = Validate(record, scene);
            AssertHasDiff(result, "marker role");
        }

        [Test]
        public void TransitionStringCaseDriftIsDetected()
        {
            object record = R.RecordById("al_scene_realm_selection");
            OpenScene(record, out Scene scene);
            object controller = FindComponent(scene, R.Runtime("AL.UI.RealmSelection.RealmSelectionController"));
            var serialized = new SerializedObject((UnityEngine.Object)controller);
            serialized.FindProperty("_nextScene").stringValue = "kingdom"; // wrong case
            serialized.ApplyModifiedPropertiesWithoutUndo();

            object result = Validate(record, scene);
            AssertHasDiff(result, "_nextScene");
        }

        // -------------------------------------------------------------------

        private void AssertTransition(string sceneId, string field, string expectedValue)
        {
            object record = R.RecordById(sceneId);
            object result = OpenAndValidate(record, out _);
            var values = (System.Collections.IDictionary)R.Prop(result, "TransitionValues");
            Assert.IsTrue(values.Contains(field), $"{sceneId} should expose transition field {field}.");
            Assert.AreEqual(expectedValue, values[field], $"{sceneId} transition {field}");
        }

        private object OpenAndValidate(object record, out Scene scene)
        {
            OpenScene(record, out scene);
            return Validate(record, scene);
        }

        private void OpenScene(object record, out Scene scene)
        {
            string path = R.PropString(record, "AssetPath");
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            _openScenes.Add(scene);
        }

        private static object Validate(object record, Scene scene)
        {
            string path = R.PropString(record, "AssetPath");
            string guid = AssetDatabase.AssetPathToGUID(path);
            return R.StaticMethod(R.ValidatorType, "ValidateOpenScene", scene, record, path, guid);
        }

        private static void AssertHasDiff(object result, string fragment)
        {
            var diffs = R.AsStrings(R.Prop(result, "Diffs"));
            Assert.IsTrue(diffs.Any(d => d.Contains(fragment)),
                $"Expected a diff containing '{fragment}'. Diffs: {string.Join(" | ", diffs)}");
            Assert.AreEqual("Drifted", R.Prop(result, "Status").ToString());
        }

        private static object FindComponent(Scene scene, Type type)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Component component = root.GetComponentInChildren(type, true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static string Label(object record, string what)
        {
            return $"{R.PropString(record, "SceneId")} {what}";
        }
    }
}
