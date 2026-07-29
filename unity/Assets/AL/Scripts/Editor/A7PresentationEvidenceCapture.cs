#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using AL.ChampionMode;
using AL.ChampionMode.Camera;
using AL.ChampionMode.World;
using AL.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace AL.Editor
{
    /// <summary>
    /// Editor-only, non-persistent capture harness for the A7 presentation review.
    /// Invoke with:
    /// -executeMethod AL.Editor.A7PresentationEvidenceCapture.CaptureFromCommandLine
    /// -a7EvidenceOutput &lt;absolute output directory&gt;
    /// </summary>
    public static class A7PresentationEvidenceCapture
    {
        private const int CaptureWidth = 1600;
        private const int CaptureHeight = 900;
        private const string OutputArgument = "-a7EvidenceOutput";
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Another Life/A7/Capture Presentation Evidence")]
        public static void CaptureFromMenu()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                 Application.dataPath;
            RunCapture(Path.Combine(projectRoot, "Temp", "A7PresentationEvidence"));
        }

        public static void CaptureFromCommandLine()
        {
            string outputDirectory = ReadRequiredOutputDirectory(
                Environment.GetCommandLineArgs());
            RunCapture(outputDirectory);
        }

        private static void RunCapture(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException(
                    "A7 evidence output directory must not be empty.",
                    nameof(outputDirectory));
            }

            string absoluteOutput = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(absoluteOutput);

            var report = CreateReport();
            try
            {
                WarmPresentationConstruction();
                double[] constructionMedians =
                    MeasurePresentationConstructionMedians();
                EvidenceMetric high = CaptureArena(
                    absoluteOutput,
                    report,
                    "arena_desktop_high",
                    "desktop_standard",
                    includeHud: false,
                    "Desktop / non-reduced arena environment");
                high.buildCpuMilliseconds = constructionMedians[0];
                EvidenceMetric reduced = CaptureArena(
                    absoluteOutput,
                    report,
                    "arena_mobile_reduced",
                    "mobile_low",
                    includeHud: false,
                    "Mobile / reduced arena environment");
                reduced.buildCpuMilliseconds = constructionMedians[1];
                CaptureArena(
                    absoluteOutput,
                    report,
                    "realm_crownlands_hud",
                    "desktop_standard",
                    includeHud: true,
                    "Staged Crownlands-themed Champion HUD presentation");
                CaptureCameraEvidence(absoluteOutput, report);
                report.comparison = BuildComparison(high, reduced);
                report.succeeded = true;
            }
            catch (Exception exception)
            {
                report.succeeded = false;
                report.errors.Add(exception.ToString());
                WriteReports(absoluteOutput, report);
                Debug.LogException(exception);
                throw;
            }

            WriteReports(absoluteOutput, report);
            Debug.Log(
                $"[A7 Evidence] Captured {report.captures.Count} PNGs and " +
                $"{report.metrics.Count} metric records in {absoluteOutput}.");
        }

        private static void WarmPresentationConstruction()
        {
            using (var session = new CaptureSession())
            {
                ChampionArenaSceneController controller =
                    CreatePresentationController(session, "desktop_standard");
                InvokePrivate(controller, "ConfigureArenaLighting");
                InvokePrivate(controller, "BuildArenaEnvironment");
                CreateCombatReadabilityMarkers();
            }
        }

        private static double[] MeasurePresentationConstructionMedians()
        {
            const int sampleCount = 3;
            var standardSamples = new double[sampleCount];
            var reducedSamples = new double[sampleCount];
            for (int sample = 0; sample < sampleCount; sample++)
            {
                if (sample % 2 == 0)
                {
                    standardSamples[sample] =
                        MeasurePresentationConstruction("desktop_standard");
                    reducedSamples[sample] =
                        MeasurePresentationConstruction("mobile_low");
                }
                else
                {
                    reducedSamples[sample] =
                        MeasurePresentationConstruction("mobile_low");
                    standardSamples[sample] =
                        MeasurePresentationConstruction("desktop_standard");
                }
            }

            Array.Sort(standardSamples);
            Array.Sort(reducedSamples);
            return new[]
            {
                standardSamples[sampleCount / 2],
                reducedSamples[sampleCount / 2]
            };
        }

        private static double MeasurePresentationConstruction(
            string qualityTier)
        {
            using (var session = new CaptureSession())
            {
                var timer = Stopwatch.StartNew();
                ChampionArenaSceneController controller =
                    CreatePresentationController(session, qualityTier);
                InvokePrivate(controller, "ConfigureArenaLighting");
                InvokePrivate(controller, "BuildArenaEnvironment");
                CreateCombatReadabilityMarkers();
                timer.Stop();
                return timer.Elapsed.TotalMilliseconds;
            }
        }

        private static EvidenceMetric CaptureArena(
            string outputDirectory,
            EvidenceReport report,
            string scenarioId,
            string qualityTier,
            bool includeHud,
            string description)
        {
            using (var session = new CaptureSession())
            {
                var buildTimer = Stopwatch.StartNew();
                ChampionArenaSceneController controller =
                    CreatePresentationController(session, qualityTier);
                InvokePrivate(controller, "ConfigureArenaLighting");
                InvokePrivate(controller, "BuildArenaEnvironment");
                CreateCombatReadabilityMarkers();

                UnityEngine.Camera camera = CreateCaptureCamera(
                    new Vector3(0f, 9.6f, -19.4f),
                    new Vector3(0f, 1.35f, 2.1f),
                    46f);
                SetPrivateField(controller, "_arenaCamera", camera);

                if (includeHud)
                {
                    InvokePrivate(controller, "BuildHud");
                    ConfigureCrownlandsHud(controller);
                }

                CreateEvidenceLabel(
                    camera,
                    includeHud
                        ? "CROWNLANDS-THEMED HUD // STAGED PRESENTATION"
                        : qualityTier == "desktop_standard"
                            ? "DESKTOP_STANDARD (NON-REDUCED) // ARENA PRESENTATION"
                            : $"{qualityTier.ToUpperInvariant()} // ARENA PRESENTATION",
                    includeHud ? new Vector2(0f, -218f) : new Vector2(0f, -34f));
                PrepareCanvasesForOffscreenRender(session, camera);
                buildTimer.Stop();

                string fileName = scenarioId + ".png";
                string filePath = Path.Combine(outputDirectory, fileName);
                double renderMilliseconds = RenderPng(camera, filePath);
                EvidenceMetric metric = CollectMetrics(
                    session,
                    scenarioId,
                    qualityTier,
                    buildTimer.Elapsed.TotalMilliseconds,
                    renderMilliseconds);
                report.metrics.Add(metric);
                report.captures.Add(new CaptureRecord
                {
                    scenario = scenarioId,
                    file = fileName,
                    description = description,
                    width = CaptureWidth,
                    height = CaptureHeight
                });
                return metric;
            }
        }

        private static void CaptureCameraEvidence(
            string outputDirectory,
            EvidenceReport report)
        {
            using (var session = new CaptureSession())
            {
                var buildTimer = Stopwatch.StartNew();
                ChampionArenaSceneController controller =
                    CreatePresentationController(session, "desktop_standard");
                InvokePrivate(controller, "ConfigureArenaLighting");
                InvokePrivate(controller, "BuildArenaEnvironment");

                GameObject target = CreatePrimitive(
                    "A7_CameraTarget",
                    PrimitiveType.Capsule,
                    new Vector3(0f, 1.05f, -1.8f),
                    new Vector3(1.05f, 1.05f, 1.05f),
                    new Color(0.24f, 0.54f, 1f),
                    0.18f,
                    0.58f);
                UnityEngine.Camera camera = CreateCaptureCamera(
                    new Vector3(0f, 5.2f, -11.6f),
                    target.transform.position + Vector3.up * 0.85f,
                    43f);
                var follow = camera.gameObject.AddComponent<CameraFollow>();
                follow.enabled = false;
                follow.Configure(target.transform, 8.6f, 1.65f, 18f, 0f);
                const int obstructionLayer = 30;
                SetPrivateField(
                    follow,
                    "_collisionMask",
                    (LayerMask)(1 << obstructionLayer));

                Vector3 pivot = target.transform.position + Vector3.up * 1.65f;
                Quaternion rotation = Quaternion.Euler(18f, 0f, 0f);
                Vector3 requestedPosition =
                    pivot + rotation * new Vector3(0f, 0f, -8.6f);
                Vector3 castDirection = (requestedPosition - pivot).normalized;

                GameObject obstruction = CreatePrimitive(
                    "A7_CloseCameraObstruction",
                    PrimitiveType.Cube,
                    Vector3.Lerp(pivot, requestedPosition, 0.46f),
                    new Vector3(5.4f, 4.2f, 0.46f),
                    new Color(0.08f, 0.095f, 0.13f),
                    0.08f,
                    0.32f);
                obstruction.transform.rotation = Quaternion.LookRotation(
                    -castDirection,
                    Vector3.up);
                obstruction.layer = obstructionLayer;
                Physics.SyncTransforms();

                Text label = CreateEvidenceLabel(
                    camera,
                    string.Empty,
                    new Vector2(0f, -34f));
                PrepareCanvasesForOffscreenRender(session, camera);
                buildTimer.Stop();

                Vector3 closePosition = ResolveFinalCameraPosition(
                    follow,
                    pivot,
                    requestedPosition,
                    Vector3.zero);
                CaptureCameraState(
                    outputDirectory,
                    report,
                    session,
                    camera,
                    label,
                    "camera_close_obstruction",
                    "CLOSE OBSTRUCTION // COLLISION PULL-IN",
                    buildTimer.Elapsed.TotalMilliseconds,
                    pivot,
                    requestedPosition,
                    closePosition,
                    0f,
                    collisionSafetyPassed:
                        Vector3.Distance(pivot, closePosition) <
                        Vector3.Distance(pivot, requestedPosition));

                Collider obstructionCollider = obstruction.GetComponent<Collider>();
                Renderer obstructionRenderer = obstruction.GetComponent<Renderer>();
                obstructionCollider.enabled = false;
                obstructionRenderer.enabled = false;
                Physics.SyncTransforms();
                Vector3 recoveredPosition = closePosition;
                const int recoveryFrameCount = 90;
                const float recoveryDeltaTime = 1f / 60f;
                for (int frame = 0; frame < recoveryFrameCount; frame++)
                {
                    recoveredPosition = ResolveFollowCameraPosition(
                        follow,
                        pivot,
                        recoveredPosition,
                        requestedPosition,
                        Vector3.zero,
                        recoveryDeltaTime);
                }
                CaptureCameraState(
                    outputDirectory,
                    report,
                    session,
                    camera,
                    label,
                    "camera_recovery",
                    "OBSTRUCTION REMOVED // SMOOTH-DAMP RECOVERY",
                    buildTimer.Elapsed.TotalMilliseconds,
                    pivot,
                    requestedPosition,
                    recoveredPosition,
                    0f,
                    collisionSafetyPassed:
                        Vector3.Distance(recoveredPosition, requestedPosition) <
                        0.02f);

                obstructionCollider.enabled = true;
                obstructionRenderer.enabled = true;
                Physics.SyncTransforms();
                Vector3 shakeOffset = castDirection * 1.15f;
                Vector3 shakenRequest = requestedPosition + shakeOffset;
                Vector3 postShakePosition = ResolveFinalCameraPosition(
                    follow,
                    pivot,
                    requestedPosition,
                    shakeOffset);
                CaptureCameraState(
                    outputDirectory,
                    report,
                    session,
                    camera,
                    label,
                    "camera_post_shake_collision",
                    "POST-SHAKE // COLLISION RESOLVED LAST",
                    buildTimer.Elapsed.TotalMilliseconds,
                    pivot,
                    shakenRequest,
                    postShakePosition,
                    shakeOffset.magnitude,
                    collisionSafetyPassed:
                        Vector3.Distance(pivot, postShakePosition) <
                        Vector3.Distance(pivot, shakenRequest));
            }
        }

        private static void CaptureCameraState(
            string outputDirectory,
            EvidenceReport report,
            CaptureSession session,
            UnityEngine.Camera camera,
            Text label,
            string scenarioId,
            string labelText,
            double buildMilliseconds,
            Vector3 pivot,
            Vector3 requestedPosition,
            Vector3 resolvedPosition,
            float shakeMagnitude,
            bool collisionSafetyPassed)
        {
            if (!collisionSafetyPassed)
            {
                throw new InvalidOperationException(
                    $"Camera evidence acceptance failed for {scenarioId}: " +
                    $"requested={Vector3.Distance(pivot, requestedPosition):0.000}, " +
                    $"resolved={Vector3.Distance(pivot, resolvedPosition):0.000}.");
            }

            camera.transform.position = resolvedPosition;
            camera.transform.LookAt(pivot - Vector3.up * 0.80f);
            label.text =
                $"{labelText}\n" +
                $"REQUEST {Vector3.Distance(pivot, requestedPosition):0.00}  " +
                $"RESOLVED {Vector3.Distance(pivot, resolvedPosition):0.00}";
            Canvas.ForceUpdateCanvases();

            string fileName = scenarioId + ".png";
            double renderMilliseconds = RenderPng(
                camera,
                Path.Combine(outputDirectory, fileName));
            EvidenceMetric metric = CollectMetrics(
                session,
                scenarioId,
                "desktop_standard",
                buildMilliseconds,
                renderMilliseconds);
            metric.requestedCameraDistance =
                Vector3.Distance(pivot, requestedPosition);
            metric.resolvedCameraDistance =
                Vector3.Distance(pivot, resolvedPosition);
            metric.shakeMagnitude = shakeMagnitude;
            metric.cameraCollisionSafetyPassed = collisionSafetyPassed;
            report.metrics.Add(metric);
            report.captures.Add(new CaptureRecord
            {
                scenario = scenarioId,
                file = fileName,
                description = labelText,
                width = CaptureWidth,
                height = CaptureHeight
            });
        }

        private static ChampionArenaSceneController CreatePresentationController(
            CaptureSession session,
            string qualityTier)
        {
            var controllerObject = new GameObject(
                "A7_Evidence_ChampionArenaSceneController");
            var controller =
                controllerObject.AddComponent<ChampionArenaSceneController>();
            controller.enabled = false;

            var qualityObject = new GameObject("A7_Evidence_RuntimeQuality");
            var quality =
                qualityObject.AddComponent<RuntimePlatformQualityController>();
            quality.enabled = false;
            quality.CurrentProfile.Tier = qualityTier;

            SetPrivateField(controller, "_realmId", RealmId.Crownlands);
            SetPrivateField(controller, "_qualityController", quality);
            SetPrivateField(controller, "_presentationLease", session.Lease);
            session.RegisterController(controller);
            return controller;
        }

        private static void CreateCombatReadabilityMarkers()
        {
            CreatePrimitive(
                "Player_Champion_Evidence",
                PrimitiveType.Capsule,
                new Vector3(0f, 1.1f, -7.4f),
                Vector3.one,
                new Color(0.20f, 0.46f, 1f),
                0.16f,
                0.58f);
            CreatePrimitive(
                "BossDummy_Evidence",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.8f, 8.6f),
                new Vector3(1.55f, 1.8f, 1.55f),
                new Color(0.34f, 0.045f, 0.075f),
                0.20f,
                0.42f);
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType primitiveType,
            Vector3 position,
            Vector3 scale,
            Color color,
            float metallic,
            float smoothness)
        {
            GameObject instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.localScale = scale;
            if (primitiveType == PrimitiveType.Cube)
            {
                MeshFilter filter = instance.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    filter.sharedMesh =
                        RuntimeWorldPresentation.GetBeveledCubeMesh();
                }
            }

            RuntimeWorldPresentation.ApplySurfaceMaterial(
                instance.GetComponent<Renderer>(),
                color,
                metallic,
                smoothness,
                smoothness > 0.68f ? 0.45f : 0f);
            return instance;
        }

        private static UnityEngine.Camera CreateCaptureCamera(
            Vector3 position,
            Vector3 lookAt,
            float fieldOfView)
        {
            var cameraObject = new GameObject("A7_Evidence_Camera");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.018f, 0.024f, 0.034f);
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 180f;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.transform.position = position;
            camera.transform.LookAt(lookAt);
            return camera;
        }

        private static Text CreateEvidenceLabel(
            UnityEngine.Camera camera,
            string text,
            Vector2 anchoredPosition)
        {
            var canvasObject = new GameObject("A7_Evidence_LabelCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;
            canvas.sortingOrder = 500;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(CaptureWidth, CaptureHeight);
            scaler.matchWidthOrHeight = 0.5f;

            var plateObject = new GameObject("EvidenceLabelPlate");
            plateObject.transform.SetParent(canvasObject.transform, false);
            var plate = plateObject.AddComponent<Image>();
            plate.color = new Color(0.008f, 0.014f, 0.024f, 0.88f);
            RectTransform plateRect =
                plateObject.GetComponent<RectTransform>();
            plateRect.anchorMin = new Vector2(0.5f, 1f);
            plateRect.anchorMax = new Vector2(0.5f, 1f);
            plateRect.pivot = new Vector2(0.5f, 1f);
            plateRect.anchoredPosition = anchoredPosition;
            plateRect.sizeDelta = new Vector2(820f, 64f);

            var textObject = new GameObject("EvidenceLabel");
            textObject.transform.SetParent(plateObject.transform, false);
            var label = textObject.AddComponent<Text>();
            label.font =
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.90f, 0.95f, 1f);
            label.text = text;
            RectTransform textRect =
                textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 5f);
            textRect.offsetMax = new Vector2(-12f, -5f);
            return label;
        }

        private static void ConfigureCrownlandsHud(
            ChampionArenaSceneController controller)
        {
            SetTextField(controller, "_healthText", "HP 1000 / 1000");
            SetTextField(controller, "_manaText", "MP 100 / 100");
            SetTextField(
                controller,
                "_bossText",
                "CROWNWARD SENTINEL  100%  CONTROLLED\nGuard 100%");
            SetTextField(
                controller,
                "_skillText",
                "Crownlands loadout: Solar Cut / Aegis / Skyfall / Guardbreak");
            SetTextField(
                controller,
                "_combatFeedText",
                "Crownlands identity committed. Break the guard, read the " +
                "telegraph, and preserve the recovery window.");
            SetTextField(
                controller,
                "_combatGoalsText",
                "[ ] Break Guard\n[ ] Defeat Boss");
            SetTextField(
                controller,
                "_encounterResultText",
                "Grade pending\nhold pressure");
            SetTextField(
                controller,
                "_appearanceProfileText",
                "CROWNLANDS / VANGUARD");
            SetTextField(
                controller,
                "_appearanceSummaryText",
                "Crownlands Vanguard\nAzure steel / gold signal accents");
            SetTextField(controller, "_controlModeText", "CONTROL MANUAL");
            SetImageFillField(controller, "_bossHealthFill", 1f);
            SetImageFillField(controller, "_bossBreakFill", 1f);
        }

        private static void SetTextField(
            object target,
            string fieldName,
            string value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                PrivateInstance);
            if (field?.GetValue(target) is Text text)
            {
                text.text = value;
            }
        }

        private static void SetImageFillField(
            object target,
            string fieldName,
            float value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                PrivateInstance);
            if (field?.GetValue(target) is Image image)
            {
                image.fillAmount = Mathf.Clamp01(value);
            }
        }

        private static void PrepareCanvasesForOffscreenRender(
            CaptureSession session,
            UnityEngine.Camera camera)
        {
            List<GameObject> createdObjects =
                session.GetCreatedSceneObjects();
            for (int i = 0; i < createdObjects.Count; i++)
            {
                Canvas canvas = createdObjects[i].GetComponent<Canvas>();
                if (canvas == null)
                {
                    continue;
                }

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 0.45f + i * 0.0001f;
            }

            Canvas.ForceUpdateCanvases();
        }

        private static double RenderPng(
            UnityEngine.Camera camera,
            string filePath)
        {
            var renderTexture = new RenderTexture(
                CaptureWidth,
                CaptureHeight,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "A7_Evidence_1600x900",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture.Create();

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Texture2D readback = null;
            try
            {
                camera.targetTexture = renderTexture;
                camera.aspect = CaptureWidth / (float)CaptureHeight;
                Canvas.ForceUpdateCanvases();

                // The first render pays Editor shader/material warm-up. The
                // second wall-clock value is the comparable CPU submission.
                camera.Render();
                var renderTimer = Stopwatch.StartNew();
                camera.Render();
                renderTimer.Stop();

                RenderTexture.active = renderTexture;
                readback = new Texture2D(
                    CaptureWidth,
                    CaptureHeight,
                    TextureFormat.RGB24,
                    false,
                    false);
                readback.ReadPixels(
                    new Rect(0f, 0f, CaptureWidth, CaptureHeight),
                    0,
                    0,
                    false);
                readback.Apply(false, false);
                File.WriteAllBytes(filePath, readback.EncodeToPNG());
                return renderTimer.Elapsed.TotalMilliseconds;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (readback != null)
                {
                    Object.DestroyImmediate(readback);
                }

                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
        }

        private static EvidenceMetric CollectMetrics(
            CaptureSession session,
            string scenario,
            string qualityTier,
            double buildMilliseconds,
            double renderMilliseconds)
        {
            List<GameObject> objects = session.GetCreatedSceneObjects();
            var meshes = new HashSet<Mesh>();
            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            int renderers = 0;
            int lights = 0;
            int colliders = 0;
            int canvases = 0;
            int particles = 0;

            for (int i = 0; i < objects.Count; i++)
            {
                GameObject gameObject = objects[i];
                Renderer renderer = gameObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderers++;
                    if (renderer is SkinnedMeshRenderer skinned &&
                        skinned.sharedMesh != null)
                    {
                        meshes.Add(skinned.sharedMesh);
                    }

                    Material[] sharedMaterials = renderer.sharedMaterials;
                    for (int materialIndex = 0;
                         materialIndex < sharedMaterials.Length;
                         materialIndex++)
                    {
                        Material material = sharedMaterials[materialIndex];
                        if (material != null)
                        {
                            materials.Add(material);
                        }
                    }
                }

                MeshFilter filter = gameObject.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    meshes.Add(filter.sharedMesh);
                }

                if (gameObject.GetComponent<Light>() != null)
                {
                    lights++;
                }

                if (gameObject.GetComponent<Collider>() != null)
                {
                    colliders++;
                }

                if (gameObject.GetComponent<Canvas>() != null)
                {
                    canvases++;
                }

                if (gameObject.GetComponent<ParticleSystem>() != null)
                {
                    particles++;
                }
            }

            foreach (Material material in materials)
            {
                string[] texturePropertyNames =
                    material.GetTexturePropertyNames();
                for (int i = 0; i < texturePropertyNames.Length; i++)
                {
                    Texture texture =
                        material.GetTexture(texturePropertyNames[i]);
                    if (texture != null)
                    {
                        textures.Add(texture);
                    }
                }
            }

            long vertexCount = 0L;
            long resourceMemoryBytes = 0L;
            foreach (Mesh mesh in meshes)
            {
                vertexCount += mesh.vertexCount;
                resourceMemoryBytes +=
                    Profiler.GetRuntimeMemorySizeLong(mesh);
            }

            foreach (Material material in materials)
            {
                resourceMemoryBytes +=
                    Profiler.GetRuntimeMemorySizeLong(material);
            }

            foreach (Texture texture in textures)
            {
                resourceMemoryBytes +=
                    Profiler.GetRuntimeMemorySizeLong(texture);
            }

            return new EvidenceMetric
            {
                scenario = scenario,
                qualityTier = qualityTier,
                buildCpuMilliseconds = buildMilliseconds,
                renderCpuMilliseconds = renderMilliseconds,
                sceneObjectCount = objects.Count,
                rendererCount = renderers,
                lightCount = lights,
                colliderCount = colliders,
                canvasCount = canvases,
                particleSystemCount = particles,
                distinctMeshCount = meshes.Count,
                distinctMaterialCount = materials.Count,
                distinctTextureCount = textures.Count,
                distinctMeshVertexCount = vertexCount,
                resourceRuntimeMemoryBytes = resourceMemoryBytes,
                profilerTotalAllocatedMemoryBytes =
                    Profiler.GetTotalAllocatedMemoryLong(),
                profilerTotalReservedMemoryBytes =
                    Profiler.GetTotalReservedMemoryLong(),
                profilerMonoUsedMemoryBytes =
                    Profiler.GetMonoUsedSizeLong(),
                gpuTimingAvailable = false,
                gpuTimingNote =
                    "Unavailable: synchronous Editor Camera.Render wall-clock " +
                    "is CPU submission time; it is not a reliable GPU frame " +
                    "timing API."
            };
        }

        private static Vector3 ResolveFinalCameraPosition(
            CameraFollow follow,
            Vector3 pivot,
            Vector3 smoothedPosition,
            Vector3 shakeOffset)
        {
            object result = InvokePrivate(
                follow,
                "ResolveFinalCameraPosition",
                pivot,
                smoothedPosition,
                shakeOffset);
            return result is Vector3 position
                ? position
                : throw new InvalidOperationException(
                    "CameraFollow collision method returned no position.");
        }

        private static Vector3 ResolveFollowCameraPosition(
            CameraFollow follow,
            Vector3 pivot,
            Vector3 currentPosition,
            Vector3 desiredPosition,
            Vector3 shakeOffset,
            float deltaTime)
        {
            object result = InvokePrivate(
                follow,
                "ResolveFollowCameraPosition",
                pivot,
                currentPosition,
                desiredPosition,
                shakeOffset,
                deltaTime);
            return result is Vector3 position
                ? position
                : throw new InvalidOperationException(
                    "CameraFollow smoothing method returned no position.");
        }

        private static object InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                PrivateInstance);
            if (method == null)
            {
                throw new MissingMethodException(
                    target.GetType().FullName,
                    methodName);
            }

            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                PrivateInstance);
            if (field == null)
            {
                throw new MissingFieldException(
                    target.GetType().FullName,
                    fieldName);
            }

            field.SetValue(target, value);
        }

        private static QualityComparison BuildComparison(
            EvidenceMetric high,
            EvidenceMetric reduced)
        {
            return new QualityComparison
            {
                highScenario = high.scenario,
                reducedScenario = reduced.scenario,
                buildCpuDeltaMilliseconds =
                    high.buildCpuMilliseconds -
                    reduced.buildCpuMilliseconds,
                renderCpuDeltaMilliseconds =
                    high.renderCpuMilliseconds -
                    reduced.renderCpuMilliseconds,
                rendererDelta =
                    high.rendererCount - reduced.rendererCount,
                vertexDelta =
                    high.distinctMeshVertexCount -
                    reduced.distinctMeshVertexCount,
                resourceRuntimeMemoryDeltaBytes =
                    high.resourceRuntimeMemoryBytes -
                    reduced.resourceRuntimeMemoryBytes
            };
        }

        private static EvidenceReport CreateReport()
        {
            var report = new EvidenceReport
            {
                generatedUtc = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                operatingSystem = SystemInfo.operatingSystem,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceType =
                    SystemInfo.graphicsDeviceType.ToString(),
                graphicsMemoryMegabytes =
                    SystemInfo.graphicsMemorySize,
                systemMemoryMegabytes = SystemInfo.systemMemorySize,
                captureWidth = CaptureWidth,
                captureHeight = CaptureHeight,
                gpuTimingAvailable = false,
                gpuTimingNote =
                    "GPU timing is intentionally reported unavailable because " +
                    "the Editor-only synchronous capture has no reliable " +
                    "cross-platform GPU frame timing sample."
            };
            report.limitations.Add(
                "The mobile/reduced capture is deterministic Editor tier " +
                "simulation, not physical-device GPU evidence.");
            report.limitations.Add(
                "Non-reduced/reduced construction values are medians of three " +
                "warmed alternating-order Editor wall-clock samples; render " +
                "values and other construction values are single samples, not " +
                "device frame-time benchmarks.");
            report.limitations.Add(
                "Profiler process totals include Unity Editor overhead; " +
                "scenario resource bytes are the narrower owned-resource sample.");
            report.limitations.Add(
                "The Crownlands HUD frame stages realm, label, and bar state " +
                "for presentation; live realm resolution is verified separately " +
                "by ChampionRealmContextTests.");
            report.limitations.Add(
                "The staged HUD frame retains current production layout and " +
                "exposes unresolved overlap at 1600x900; it is audit evidence, " +
                "not visual approval.");
            report.limitations.Add(
                "Both tier captures retain 19 lights and zero particles in the " +
                "harness, so mobile lighting and weather cost are not validated.");
            report.limitations.Add(
                "Captures are fixed 1600x900 (16:9); additional aspect ratios " +
                "are not exercised by this harness.");
            report.limitations.Add(
                "Controller and touch navigation are not exercised by this " +
                "non-interactive capture.");
            report.limitations.Add(
                "Reduced motion, camera shake scaling, contrast, subtitle, and " +
                "UI-scale accessibility controls require interactive review.");
            return report;
        }

        private static void WriteReports(
            string outputDirectory,
            EvidenceReport report)
        {
            File.WriteAllText(
                Path.Combine(
                    outputDirectory,
                    "a7_presentation_evidence_metrics.json"),
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(
                    outputDirectory,
                    "a7_presentation_evidence_metrics.txt"),
                BuildTextReport(report),
                new UTF8Encoding(false));
        }

        private static string BuildTextReport(EvidenceReport report)
        {
            var text = new StringBuilder(4096);
            text.AppendLine("AnotherLife A7 Presentation Evidence");
            text.AppendLine($"Succeeded: {report.succeeded}");
            text.AppendLine($"Generated UTC: {report.generatedUtc}");
            text.AppendLine(
                $"Unity: {report.unityVersion} | OS: {report.operatingSystem}");
            text.AppendLine(
                $"Graphics: {report.graphicsDeviceName} " +
                $"({report.graphicsDeviceType}), " +
                $"{report.graphicsMemoryMegabytes} MB");
            text.AppendLine(
                $"Capture: {report.captureWidth}x{report.captureHeight}");
            text.AppendLine(
                $"GPU timing: unavailable - {report.gpuTimingNote}");
            text.AppendLine();
            text.AppendLine(
                "Scenario | Tier | Build CPU ms | Render CPU ms | " +
                "Renderers | Meshes | Vertices | Materials | Textures | " +
                "Lights | Colliders | Resource bytes");

            for (int i = 0; i < report.metrics.Count; i++)
            {
                EvidenceMetric metric = report.metrics[i];
                text.Append(metric.scenario).Append(" | ");
                text.Append(metric.qualityTier).Append(" | ");
                text.Append(Format(metric.buildCpuMilliseconds)).Append(" | ");
                text.Append(Format(metric.renderCpuMilliseconds)).Append(" | ");
                text.Append(metric.rendererCount).Append(" | ");
                text.Append(metric.distinctMeshCount).Append(" | ");
                text.Append(metric.distinctMeshVertexCount).Append(" | ");
                text.Append(metric.distinctMaterialCount).Append(" | ");
                text.Append(metric.distinctTextureCount).Append(" | ");
                text.Append(metric.lightCount).Append(" | ");
                text.Append(metric.colliderCount).Append(" | ");
                text.AppendLine(
                    metric.resourceRuntimeMemoryBytes.ToString(
                        CultureInfo.InvariantCulture));
            }

            if (report.comparison != null)
            {
                text.AppendLine();
                text.AppendLine(
                    "Non-reduced versus reduced delta " +
                    "(desktop_standard - mobile_low)");
                text.AppendLine(
                    $"Build CPU ms: " +
                    $"{Format(report.comparison.buildCpuDeltaMilliseconds)}");
                text.AppendLine(
                    $"Render CPU ms: " +
                    $"{Format(report.comparison.renderCpuDeltaMilliseconds)}");
                text.AppendLine(
                    $"Renderers: {report.comparison.rendererDelta}");
                text.AppendLine(
                    $"Vertices: {report.comparison.vertexDelta}");
                text.AppendLine(
                    $"Resource bytes: " +
                    $"{report.comparison.resourceRuntimeMemoryDeltaBytes}");
            }

            text.AppendLine();
            text.AppendLine("Limitations");
            for (int i = 0; i < report.limitations.Count; i++)
            {
                text.Append("- ").AppendLine(report.limitations[i]);
            }

            if (report.errors.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Errors");
                for (int i = 0; i < report.errors.Count; i++)
                {
                    text.AppendLine(report.errors[i]);
                }
            }

            return text.ToString();
        }

        private static string Format(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string ReadRequiredOutputDirectory(string[] arguments)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                string argument = arguments[i];
                if (string.Equals(
                        argument,
                        OutputArgument,
                        StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < arguments.Length)
                {
                    return arguments[i + 1];
                }

                string prefix = OutputArgument + "=";
                if (argument.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return argument.Substring(prefix.Length);
                }
            }

            throw new ArgumentException(
                $"Missing required command-line argument {OutputArgument}.");
        }

        private sealed class CaptureSession : IDisposable
        {
            private const string KeyLightName = "Key Light - Moonforge";
            private readonly HashSet<int> _baselineObjectIds =
                new HashSet<int>();
            private readonly List<RenamedObject> _renamedKeyLights =
                new List<RenamedObject>();
            private readonly List<ChampionArenaSceneController> _controllers =
                new List<ChampionArenaSceneController>();
            private bool _disposed;

            public CaptureSession()
            {
                GameObject[] objects =
                    Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < objects.Length; i++)
                {
                    GameObject gameObject = objects[i];
                    if (!IsSceneObject(gameObject))
                    {
                        continue;
                    }

                    _baselineObjectIds.Add(gameObject.GetInstanceID());
                    if (gameObject.name == KeyLightName)
                    {
                        _renamedKeyLights.Add(new RenamedObject
                        {
                            gameObject = gameObject,
                            originalName = gameObject.name
                        });
                        gameObject.name =
                            $"{KeyLightName} [A7 baseline " +
                            $"{gameObject.GetInstanceID()}]";
                    }
                }

                try
                {
                    Lease =
                        RuntimeWorldPresentation.BeginScenePresentation();
                }
                catch
                {
                    RestoreRenamedObjects();
                    throw;
                }
            }

            public RuntimeWorldPresentation.SceneLease Lease { get; }

            public void RegisterController(
                ChampionArenaSceneController controller)
            {
                if (controller != null)
                {
                    _controllers.Add(controller);
                }
            }

            public List<GameObject> GetCreatedSceneObjects()
            {
                var created = new List<GameObject>();
                GameObject[] objects =
                    Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < objects.Length; i++)
                {
                    GameObject gameObject = objects[i];
                    if (IsSceneObject(gameObject) &&
                        !_baselineObjectIds.Contains(
                            gameObject.GetInstanceID()))
                    {
                        created.Add(gameObject);
                    }
                }

                return created;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                try
                {
                    for (int i = 0; i < _controllers.Count; i++)
                    {
                        ChampionArenaSceneController controller =
                            _controllers[i];
                        if (controller != null)
                        {
                            SetPrivateField(
                                controller,
                                "_presentationLease",
                                null);
                        }
                    }

                    List<GameObject> created =
                        GetCreatedSceneObjects();
                    var createdIds = new HashSet<int>();
                    for (int i = 0; i < created.Count; i++)
                    {
                        createdIds.Add(created[i].GetInstanceID());
                    }

                    for (int i = 0; i < created.Count; i++)
                    {
                        GameObject gameObject = created[i];
                        if (gameObject == null)
                        {
                            continue;
                        }

                        Transform parent = gameObject.transform.parent;
                        if (parent == null ||
                            !createdIds.Contains(
                                parent.gameObject.GetInstanceID()))
                        {
                            Object.DestroyImmediate(gameObject);
                        }
                    }
                }
                finally
                {
                    try
                    {
                        Lease?.Dispose();
                    }
                    finally
                    {
                        RestoreRenamedObjects();
                    }
                }
            }

            private void RestoreRenamedObjects()
            {
                for (int i = 0; i < _renamedKeyLights.Count; i++)
                {
                    RenamedObject renamed = _renamedKeyLights[i];
                    if (renamed.gameObject != null)
                    {
                        renamed.gameObject.name = renamed.originalName;
                    }
                }
            }

            private static bool IsSceneObject(GameObject gameObject)
            {
                return gameObject != null &&
                       gameObject.scene.IsValid() &&
                       !EditorUtility.IsPersistent(gameObject);
            }

            private sealed class RenamedObject
            {
                public GameObject gameObject;
                public string originalName;
            }
        }

        [Serializable]
        private sealed class EvidenceReport
        {
            public bool succeeded;
            public string generatedUtc;
            public string unityVersion;
            public string operatingSystem;
            public string graphicsDeviceName;
            public string graphicsDeviceType;
            public int graphicsMemoryMegabytes;
            public int systemMemoryMegabytes;
            public int captureWidth;
            public int captureHeight;
            public bool gpuTimingAvailable;
            public string gpuTimingNote;
            public List<CaptureRecord> captures =
                new List<CaptureRecord>();
            public List<EvidenceMetric> metrics =
                new List<EvidenceMetric>();
            public QualityComparison comparison;
            public List<string> limitations = new List<string>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class CaptureRecord
        {
            public string scenario;
            public string file;
            public string description;
            public int width;
            public int height;
        }

        [Serializable]
        private sealed class EvidenceMetric
        {
            public string scenario;
            public string qualityTier;
            public double buildCpuMilliseconds;
            public double renderCpuMilliseconds;
            public int sceneObjectCount;
            public int rendererCount;
            public int lightCount;
            public int colliderCount;
            public int canvasCount;
            public int particleSystemCount;
            public int distinctMeshCount;
            public int distinctMaterialCount;
            public int distinctTextureCount;
            public long distinctMeshVertexCount;
            public long resourceRuntimeMemoryBytes;
            public long profilerTotalAllocatedMemoryBytes;
            public long profilerTotalReservedMemoryBytes;
            public long profilerMonoUsedMemoryBytes;
            public bool gpuTimingAvailable;
            public string gpuTimingNote;
            public float requestedCameraDistance;
            public float resolvedCameraDistance;
            public float shakeMagnitude;
            public bool cameraCollisionSafetyPassed;
        }

        [Serializable]
        private sealed class QualityComparison
        {
            public string highScenario;
            public string reducedScenario;
            public double buildCpuDeltaMilliseconds;
            public double renderCpuDeltaMilliseconds;
            public int rendererDelta;
            public long vertexDelta;
            public long resourceRuntimeMemoryDeltaBytes;
        }
    }
}
#endif
