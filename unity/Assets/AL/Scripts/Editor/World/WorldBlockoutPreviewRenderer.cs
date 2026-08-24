using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Editor.World
{
    public static class WorldBlockoutPreviewRenderer
    {
        private const string MenuPath =
            "AnotherLife/World/Render Representative World Blockout Previews";
        private const string PreviewRoot =
            "Assets/AL/Worlds/Generated/Previews";

        private sealed class PreviewDefinition
        {
            internal PreviewDefinition(
                string scenePath,
                string outputName,
                bool orthographic,
                Vector3 viewDirection)
            {
                ScenePath = scenePath;
                OutputName = outputName;
                Orthographic = orthographic;
                ViewDirection = viewDirection.normalized;
            }

            internal string ScenePath { get; }
            internal string OutputName { get; }
            internal bool Orthographic { get; }
            internal Vector3 ViewDirection { get; }
        }

        [MenuItem(MenuPath)]
        public static void RenderRepresentativePreviews()
        {
            ThrowIfAnyLoadedSceneIsDirty();
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Directory.CreateDirectory(PreviewRoot);
                foreach (PreviewDefinition preview in Definitions())
                {
                    Render(preview);
                }
                AssetDatabase.Refresh();
                Debug.Log("Rendered representative modular-world blockout previews.");
            }
            finally
            {
                if (previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        private static void ThrowIfAnyLoadedSceneIsDirty()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    throw new InvalidOperationException(
                        "Save modified scenes before rendering world previews: " + scene.path);
                }
            }
        }

        private static IReadOnlyList<PreviewDefinition> Definitions()
        {
            return new[]
            {
                new PreviewDefinition(
                    "Assets/AL/Worlds/Generated/Adventure3D/world_adventure_ring_slot_01_inner/chunk_ring_slot_01_capital_core.unity",
                    "adventure_capital.png",
                    true,
                    new Vector3(0.82f, 0.88f, -0.82f)),
                new PreviewDefinition(
                    "Assets/AL/Worlds/Generated/Adventure3D/world_adventure_outer_warzone/chunk_warzone_bridge_01_02_a.unity",
                    "warzone_bridge.png",
                    false,
                    new Vector3(0.74f, 0.42f, -0.92f)),
                new PreviewDefinition(
                    "Assets/AL/Worlds/Generated/SpecialEvent3D/world_event_accordant_isle/chunk_accordant_center_bridge_ring_slot_01.unity",
                    "accordant_center_bridge.png",
                    false,
                    new Vector3(0.78f, 0.48f, -0.90f)),
                new PreviewDefinition(
                    "Assets/AL/Worlds/Generated/Kingdom25D/world_kingdom_private/chunk_kingdom_castle_core.unity",
                    "kingdom_castle.png",
                    true,
                    new Vector3(0.78f, 0.88f, -0.78f)),
                new PreviewDefinition(
                    "Assets/AL/Worlds/Generated/SpecialEvent3D/world_event_accordant_isle/chunk_accordant_wish_dragon_cavern.unity",
                    "wish_dragon_cavern.png",
                    false,
                    new Vector3(0.72f, 0.36f, -0.88f))
            };
        }

        private static void Render(PreviewDefinition preview)
        {
            Scene scene = EditorSceneManager.OpenScene(
                preview.ScenePath,
                OpenSceneMode.Single);
            Renderer[] renderers = scene.GetRootGameObjects()
                .SelectMany(value => value.GetComponentsInChildren<Renderer>(true))
                .Where(value => value.enabled &&
                                !value.name.Contains("VolumeBlockout", StringComparison.Ordinal))
                .ToArray();
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "Preview scene has no visible blockout geometry: " + preview.ScenePath);
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
            {
                bounds.Encapsulate(renderer.bounds);
            }

            var cameraObject = new GameObject("PreviewCamera");
            var lightObject = new GameObject("PreviewKeyLight");
            var fillObject = new GameObject("PreviewFillLight");
            var originalLayers = renderers
                .Select(value => value.gameObject)
                .Distinct()
                .ToDictionary(value => value, value => value.layer);
            try
            {
                foreach (GameObject target in originalLayers.Keys)
                {
                    target.layer = 31;
                }

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.032f, 0.048f, 1f);
                camera.cullingMask = 1 << 31;
                camera.allowHDR = true;
                camera.fieldOfView = 42f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = Mathf.Max(5000f, bounds.extents.magnitude * 8f);
                camera.orthographic = preview.Orthographic;

                float radius = Mathf.Max(20f, bounds.extents.magnitude);
                float distance = preview.Orthographic
                    ? radius * 2.1f
                    : radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.12f;
                camera.transform.position = bounds.center + preview.ViewDirection * distance;
                camera.transform.LookAt(bounds.center + Vector3.up * bounds.extents.y * 0.08f);
                if (preview.Orthographic)
                {
                    camera.orthographicSize =
                        Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.25f;
                }

                Light key = lightObject.AddComponent<Light>();
                key.type = LightType.Directional;
                key.color = new Color(1f, 0.90f, 0.78f);
                key.intensity = 1.18f;
                key.transform.rotation = Quaternion.Euler(38f, -32f, 0f);

                Light fill = fillObject.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.color = new Color(0.32f, 0.48f, 0.76f);
                fill.intensity = 0.42f;
                fill.transform.rotation = Quaternion.Euler(22f, 148f, 0f);

                Color previousAmbient = RenderSettings.ambientLight;
                AmbientModeScope ambientScope = new AmbientModeScope(previousAmbient);
                try
                {
                    RenderSettings.ambientLight = new Color(0.24f, 0.27f, 0.34f);
                    WriteCameraPng(camera, PreviewRoot + "/" + preview.OutputName);
                }
                finally
                {
                    ambientScope.Dispose();
                }
            }
            finally
            {
                foreach (KeyValuePair<GameObject, int> pair in originalLayers)
                {
                    if (pair.Key != null)
                    {
                        pair.Key.layer = pair.Value;
                    }
                }
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(fillObject);
            }
        }

        private static void WriteCameraPng(Camera camera, string outputPath)
        {
            const int width = 1280;
            const int height = 720;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2
            };
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                target.Create();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private readonly struct AmbientModeScope : IDisposable
        {
            private readonly Color previousAmbient;

            internal AmbientModeScope(Color previousAmbient)
            {
                this.previousAmbient = previousAmbient;
            }

            public void Dispose()
            {
                RenderSettings.ambientLight = previousAmbient;
            }
        }
    }
}
