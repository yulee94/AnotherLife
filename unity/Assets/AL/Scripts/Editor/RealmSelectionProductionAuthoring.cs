using System.Collections.Generic;
using System.IO;
using AL.Core;
using AL.Data.Definitions;
using AL.UI.Presentation;
using AL.UI.RealmSelection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.EditorTools
{
    public static class RealmSelectionProductionAuthoring
    {
        public const string ScenePath = "Assets/AL/Scenes/RealmSelection.unity";
        public const string ColorShotPath = "Logs/realm-select-production-color.png";
        public const string GreyShotPath = "Logs/realm-select-production-grey.png";
        public const string CommitShotPath = "Logs/realm-select-production-commit.png";

        [MenuItem("Another Life/UI/Author Realm Selection Production Layout")]
        public static void AuthorFromMenu()
        {
            Debug.Log(AuthorAndCapture());
        }

        public static string AuthorAndCapture()
        {
            Directory.CreateDirectory("Assets/AL/Prefabs/UI/RealmSelection");
            Directory.CreateDirectory("Logs");

            List<RealmDefinition> definitions = CreateDefinitions();
            Font font = RealmSelectionIdentity.ResolvePresentationFont();
            RealmSelectionProductionScreen screen = RealmSelectionProductionLayout.Build(
                definitions,
                LoadEmblem,
                null,
                font);

            string cardPath = RealmSelectionProductionLayout.CardPrefabPath;
            string screenPath = RealmSelectionProductionLayout.ScreenPrefabPath;
            if (screen.RealmButtons.Count > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(screen.RealmButtons[0].gameObject, cardPath);
            }

            PrefabUtility.SaveAsPrefabAsset(screen.CanvasObject, screenPath);
            CaptureLayout(screen, ColorShotPath, greyscale: false);
            CaptureLayout(screen, GreyShotPath, greyscale: true);

            if (screen.Commit != null && definitions.Count > 0)
            {
                screen.Commit.Present(
                    RealmSelectionIdentity.Resolve(definitions[0], AL.RealmSelection.RealmCatalogRuntime.Current),
                    LoadEmblem(definitions[0].Id));
                CaptureLayout(screen, CommitShotPath, greyscale: false);
                screen.Commit.Hide();
            }

            WireScene(screenPath);
            Object.DestroyImmediate(screen.CanvasObject);
            for (int i = 0; i < definitions.Count; i++)
            {
                Object.DestroyImmediate(definitions[i]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "authored " + screenPath + " + shots in Logs/";
        }

        private static void WireScene(string screenPrefabPath)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(screenPrefabPath);
            RealmSelectionController controller = Object.FindObjectOfType<RealmSelectionController>();
            if (controller == null || prefab == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("_screenPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void CaptureLayout(RealmSelectionProductionScreen screen, string relativePath, bool greyscale)
        {
            const int width = 1920;
            const int height = 1080;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("RealmSelectionCaptureCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PresentationChrome.StoneVoid;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.targetTexture = rt;
            var canvas = screen.CanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            if (greyscale)
            {
                Color[] pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    float l = pixels[i].grayscale;
                    pixels[i] = new Color(l, l, l, 1f);
                }

                texture.SetPixels(pixels);
                texture.Apply();
            }

            File.WriteAllBytes(relativePath, texture.EncodeToPNG());
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraObject);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        private static List<RealmDefinition> CreateDefinitions()
        {
            var ids = new[] { RealmId.Crownlands, RealmId.Stonehold, RealmId.Eldergrove, RealmId.Umbral };
            var list = new List<RealmDefinition>(4);
            for (int i = 0; i < ids.Length; i++)
            {
                var definition = ScriptableObject.CreateInstance<RealmDefinition>();
                definition.Id = ids[i];
                definition.RealmName = ids[i].ToString();
                list.Add(definition);
            }

            return list;
        }

        private static Sprite LoadEmblem(RealmId id)
        {
            string guid = id switch
            {
                RealmId.Stonehold => "94d8d9e2cf04a4b769c213a13c164b8e",
                RealmId.Eldergrove => "53001b27fd9d14914984211765be4391",
                RealmId.Crownlands => "ba4dfcc7b514049f79f6ec3424193b46",
                RealmId.Umbral => "a426041e03b0742999a34b8b5e198406",
                _ => null
            };
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
