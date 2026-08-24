using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AL.ChampionMode.Presentation;
using AL.Core;
using AL.Data.Catalogs.WorldAtlas;
using AL.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AL.Editor
{
    public static class FirstSessionAuthoredEvidenceCapture
    {
        private const int Width = 1280;
        private const int Height = 720;

        [MenuItem("Another Life/Dev/Capture Four Realm Authored First Session")]
        public static void CaptureForCli()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(snapshot);
            string outputRoot = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Logs",
                "FirstSessionAuthoredEvidence");
            Directory.CreateDirectory(outputRoot);

            var report = new StringBuilder();
            report.AppendLine("{");
            report.AppendLine("  \"unity_version\": \"" + Application.unityVersion + "\",");
            report.AppendLine("  \"captures\": [");
            RealmId[] realms =
            {
                RealmId.Stonehold,
                RealmId.Eldergrove,
                RealmId.Crownlands,
                RealmId.Umbral
            };

            for (int index = 0; index < realms.Length; index++)
            {
                RealmId realm = realms[index];
                InnerRealmWorldBuildResult built =
                    FirstSessionAuthoredWorldBuilder.Build(
                        layout,
                        realm.ToString().ToLowerInvariant());
                Vector3 center = built.PlayerSpawn;
                GameObject player = CreatePlayer(center, realm);
                GameObject guardian = CreateGuardian(center);
                Light key = CreateDirectionalLight();
                Light fill = CreateFillLight(center, realm);
                Light rim = CreateRimLight(center, realm);
                Camera camera = CreateCamera();

                string slug = realm.ToString().ToLowerInvariant();
                string front = Path.Combine(outputRoot, slug + "-front.png");
                string threeQuarter = Path.Combine(outputRoot, slug + "-three-quarter.png");
                Render(
                    camera,
                    center + new Vector3(0f, 2.55f, -7.0f),
                    center + new Vector3(0f, 1.55f, 2.6f),
                    front);
                Render(
                    camera,
                    center + new Vector3(4.6f, 2.35f, -5.7f),
                    center + new Vector3(0f, 1.22f, -1.45f),
                    threeQuarter);

                Renderer[] renderers = built.Root.GetComponentsInChildren<Renderer>(true);
                Renderer[] visibleRenderers = renderers
                    .Where(renderer => renderer.isVisible)
                    .ToArray();
                int materialCount = visibleRenderers
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .Distinct()
                    .Count();
                report.AppendLine("    {");
                report.AppendLine("      \"realm\": \"" + realm + "\",");
                report.AppendLine("      \"visible_environment_renderers\": " + visibleRenderers.Length + ",");
                report.AppendLine("      \"visible_environment_shared_materials\": " + materialCount + ",");
                report.AppendLine("      \"visible_environment_triangles\": " + CountTriangles(visibleRenderers) + ",");
                report.AppendLine("      \"loaded_lod_renderer_components\": " + renderers.Length + ",");
                report.AppendLine("      \"loaded_lod_triangles\": " + CountTriangles(renderers) + ",");
                report.AppendLine("      \"front\": \"" + Escape(front) + "\",");
                report.AppendLine("      \"three_quarter\": \"" + Escape(threeQuarter) + "\"");
                report.Append("    }");
                report.AppendLine(index == realms.Length - 1 ? string.Empty : ",");

                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(key.gameObject);
                Object.DestroyImmediate(fill.gameObject);
                Object.DestroyImmediate(rim.gameObject);
                ReleaseMotionGraphs(player);
                ReleaseMotionGraphs(guardian);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(guardian);
                Object.DestroyImmediate(built.Root.gameObject);
            }

            report.AppendLine("  ]");
            report.AppendLine("}");
            string reportPath = Path.Combine(outputRoot, "capture-report.json");
            File.WriteAllText(reportPath, report.ToString());
            Debug.Log("[AL-FIRST-SESSION-AUTHORED-EVIDENCE] " + reportPath);
        }

        private static void ReleaseMotionGraphs(GameObject root)
        {
            FirstSessionAuthoredVisualBinder.ReleaseMotionGraphs(root);
        }

        private static GameObject CreatePlayer(Vector3 center, RealmId realm)
        {
            var player = new GameObject("EvidencePlayer");
            player.transform.position = center + new Vector3(-2.0f, 1.08f, -1.9f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            if (!FirstSessionAuthoredVisualBinder.TryBindChampion(
                    player,
                    realm,
                    out string diagnostic))
            {
                throw new InvalidOperationException(diagnostic);
            }

            return player;
        }

        private static GameObject CreateGuardian(Vector3 center)
        {
            var guardian = new GameObject("EvidenceGuardian");
            guardian.transform.position = center + new Vector3(2.0f, 0.95f, -1.2f);
            guardian.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            if (!FirstSessionAuthoredVisualBinder.TryBindGuardian(
                    guardian,
                    out string diagnostic))
            {
                throw new InvalidOperationException(diagnostic);
            }

            return guardian;
        }

        private static Light CreateDirectionalLight()
        {
            var lightObject = new GameObject("EvidenceKeyLight");
            lightObject.transform.rotation = Quaternion.Euler(44f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.94f, 0.88f, 0.78f);
            light.intensity = 1.55f;
            light.shadows = LightShadows.Soft;
            return light;
        }

        private static Light CreateFillLight(Vector3 center, RealmId realm)
        {
            var lightObject = new GameObject("EvidenceRealmFill");
            lightObject.transform.position = center + new Vector3(-3f, 3.5f, -2f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = RealmColor(realm);
            light.intensity = 2.4f;
            light.range = 13f;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Light CreateRimLight(Vector3 center, RealmId realm)
        {
            var lightObject = new GameObject("EvidenceCovenantRim");
            lightObject.transform.position = center + new Vector3(3.8f, 3.1f, 7.2f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.Lerp(RealmColor(realm), Color.white, 0.32f);
            light.intensity = 2.1f;
            light.range = 10f;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("EvidenceCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 43f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 160f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = RenderSettings.fogColor;
            return camera;
        }

        private static void Render(
            Camera camera,
            Vector3 position,
            Vector3 target,
            string outputPath)
        {
            camera.transform.position = position;
            camera.transform.LookAt(target);
            var renderTexture = new RenderTexture(
                Width,
                Height,
                24,
                RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(pixels);
            }
        }

        private static long CountTriangles(IEnumerable<Renderer> renderers)
        {
            long triangles = 0;
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }
                else
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    mesh = filter == null ? null : filter.sharedMesh;
                }

                if (mesh != null)
                {
                    triangles += mesh.triangles.LongLength / 3;
                }
            }

            return triangles;
        }

        private static Color RealmColor(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold:
                    return new Color(0.88f, 0.48f, 0.22f);
                case RealmId.Eldergrove:
                    return new Color(0.28f, 0.78f, 0.42f);
                case RealmId.Crownlands:
                    return new Color(0.38f, 0.62f, 1f);
                case RealmId.Umbral:
                    return new Color(0.72f, 0.32f, 0.96f);
                default:
                    return Color.white;
            }
        }

        private static string Escape(string path)
        {
            return path.Replace("\\", "\\\\");
        }
    }
}
