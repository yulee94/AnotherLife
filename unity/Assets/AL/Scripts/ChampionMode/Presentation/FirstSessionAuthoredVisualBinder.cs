using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.World;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine;

namespace AL.ChampionMode.Presentation
{
    public sealed class AuthoredGuardianMotion : MonoBehaviour
    {
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private PlayableGraph _graph;
        private AnimationClipPlayable _clipPlayable;

        public AnimationClip Clip { get; private set; }
        public bool IsPlaying => _graph.IsValid() && _graph.IsPlaying();

        private void Awake()
        {
            _baseLocalPosition = transform.localPosition;
            _baseLocalRotation = transform.localRotation;
        }

        public void Configure(AnimationClip clip)
        {
            Clip = clip;
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator == null || clip == null)
            {
                return;
            }

            Release();

            _clipPlayable = AnimationPlayableUtilities.PlayClip(
                animator,
                clip,
                out _graph);
            _clipPlayable.SetApplyFootIK(false);
            _clipPlayable.SetApplyPlayableIK(false);
            _graph.Play();
            SamplePose(Mathf.Min(0.42f, clip.length * 0.35f));
        }

        public void Release()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            _clipPlayable = default;
        }

        public void SamplePose(float timeSeconds)
        {
            if (!_clipPlayable.IsValid() || Clip == null)
            {
                return;
            }

            _clipPlayable.SetTime(Mathf.Clamp(timeSeconds, 0f, Clip.length));
            _clipPlayable.SetDone(false);
            _graph.Evaluate(0f);
        }

        private void Update()
        {
            float phase = Time.time * 1.35f;
            transform.localPosition = _baseLocalPosition +
                                      Vector3.up * (Mathf.Sin(phase) * 0.035f);
            transform.localRotation = _baseLocalRotation *
                                      Quaternion.Euler(0f, Mathf.Sin(phase * 0.47f) * 2.5f, 0f);

            if (_clipPlayable.IsValid() && Clip != null &&
                _clipPlayable.GetTime() >= Clip.length)
            {
                _clipPlayable.SetTime(0d);
                _clipPlayable.SetDone(false);
            }
        }

        private void OnDestroy()
        {
            Release();
        }
    }

    /// <summary>
    /// Replaces visible primitive presentation while preserving the established
    /// player and guardian gameplay roots, colliders, controllers and save flow.
    /// </summary>
    public static class FirstSessionAuthoredVisualBinder
    {
        public const string ChampionVisualName = "AuthoredChampionVisual";
        public const string GuardianVisualName = "AuthoredCovenantGuardianVisual";

        public static void ReleaseMotionGraphs(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            AuthoredGuardianMotion[] motions =
                root.GetComponentsInChildren<AuthoredGuardianMotion>(true);
            for (int index = 0; index < motions.Length; index++)
            {
                motions[index].Release();
            }
        }

        public static bool TryBindChampion(GameObject player, RealmId realm, out string diagnostic)
        {
            ChampionCustomizationState appearance = null;
            if (ServiceLocator.TryGet<ISaveGameService>(out ISaveGameService save) &&
                save?.CurrentSave != null)
            {
                appearance = save.CurrentSave.ChampionCustomization;
            }

            return TryBindChampion(player, realm, appearance, out diagnostic);
        }

        public static bool TryBindChampion(
            GameObject player,
            RealmId realm,
            ChampionCustomizationState appearance,
            out string diagnostic)
        {
            if (!TryLoadCatalog(out FirstSessionAuthoredAssetCatalog catalog, out diagnostic) ||
                player == null)
            {
                diagnostic = player == null ? "authored_champion_player_missing" : diagnostic;
                return false;
            }

            string bodyBaseId = ResolveBodyBaseId(appearance);
            if (!catalog.TryResolveChampionBaseVisual(
                    bodyBaseId,
                    out FirstSessionChampionBaseVisualAsset visual))
            {
                diagnostic = "authored_champion_base_missing:" + bodyBaseId;
                return false;
            }

            RemoveExistingChampionVisual(player);
            HideExistingRenderers(player);
            DestroyTemporaryPlaques(player);

            var authoredRoot = new GameObject(ChampionVisualName).transform;
            authoredRoot.SetParent(player.transform, false);
            authoredRoot.localPosition = Vector3.down * 1.08f;
            authoredRoot.localRotation = Quaternion.identity;

            GameObject body = InstantiateModel(
                visual.Prefab,
                authoredRoot,
                "ImportedAuthoredChampion_" + bodyBaseId,
                Quaternion.identity);

            if (body.GetComponentInChildren<Animator>(true) == null)
            {
                body.AddComponent<Animator>();
            }

            ScaleAndGround(body, bodyBaseId == "female" ? 1.72f : 1.8f);
            ApplyChampionPbr(body, visual, appearance, bodyBaseId, realm);
            ApplyBodyBlendshape(body, appearance?.BodyPresetId);
            AuthoredGuardianMotion championMotion = body.AddComponent<AuthoredGuardianMotion>();
            championMotion.Configure(visual.LocomotionClip);
            diagnostic = string.Empty;
            return true;
        }

        private static void RemoveExistingChampionVisual(GameObject player)
        {
            Transform existing = player.transform.Find(ChampionVisualName);
            while (existing != null)
            {
                ReleaseMotionGraphs(existing.gameObject);
                if (Application.isPlaying)
                {
                    existing.SetParent(null, true);
                    Object.Destroy(existing.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                existing = player.transform.Find(ChampionVisualName);
            }
        }

        public static bool TryBindGuardian(GameObject gameplayRoot, out string diagnostic)
        {
            if (!TryLoadCatalog(out FirstSessionAuthoredAssetCatalog catalog, out diagnostic) ||
                gameplayRoot == null)
            {
                diagnostic = gameplayRoot == null ? "authored_guardian_root_missing" : diagnostic;
                return false;
            }

            HideExistingRenderers(gameplayRoot);
            GameObject guardian = InstantiateModel(
                catalog.GuardianPrefab,
                gameplayRoot.transform,
                GuardianVisualName,
                Quaternion.identity);
            guardian.transform.localPosition = Vector3.down * 0.95f;
            if (guardian.GetComponentInChildren<Animator>(true) == null)
            {
                guardian.AddComponent<Animator>();
            }

            AuthoredGuardianMotion motion = guardian.AddComponent<AuthoredGuardianMotion>();
            motion.Configure(catalog.GuardianLocomotionClip);
            ApplyGuardianPbr(guardian, catalog);
            diagnostic = string.Empty;
            return true;
        }

        private static bool TryLoadCatalog(
            out FirstSessionAuthoredAssetCatalog catalog,
            out string diagnostic)
        {
            catalog = Resources.Load<FirstSessionAuthoredAssetCatalog>(
                FirstSessionAuthoredAssetCatalog.ResourcesPath);
            if (catalog == null || !catalog.HasRequiredAssets())
            {
                diagnostic = "first_session_authored_catalog_missing_or_incomplete";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static GameObject InstantiateModel(
            GameObject source,
            Transform parent,
            string name,
            Quaternion localRotation)
        {
            GameObject instance = Object.Instantiate(source, parent);
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);
            return instance;
        }

        private static void HideExistingRenderers(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].enabled = false;
            }
        }


        private static void ScaleAndGround(GameObject root, float targetHeight)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = CalculateBounds(renderers);
            if (bounds.size.y > 0.01f)
            {
                root.transform.localScale *= targetHeight / bounds.size.y;
                bounds = CalculateBounds(renderers);
            }

            root.transform.position += Vector3.up *
                                       (root.transform.parent.position.y - bounds.min.y);
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static void DestroyTemporaryPlaques(GameObject player)
        {
            Transform[] children = player.GetComponentsInChildren<Transform>(true);
            for (int index = children.Length - 1; index >= 0; index--)
            {
                Transform child = children[index];
                if (child != null && child != player.transform &&
                    child.name.IndexOf("TEMPORARY", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Object.Destroy(child.gameObject);
                }
            }
        }

        private static void ApplyRealmSurface(
            GameObject root,
            RealmId realm,
            float metallic,
            float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                return;
            }

            Color accent = ResolveRealmColor(realm);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] sourceMaterials = renderer.sharedMaterials;
                if (sourceMaterials.Length == 0)
                {
                    sourceMaterials = new Material[1];
                }

                var replacements = new Material[sourceMaterials.Length];
                for (int materialIndex = 0; materialIndex < replacements.Length; materialIndex++)
                {
                    Material source = sourceMaterials[materialIndex];
                    Material material = source != null
                        ? new Material(source)
                        : new Material(shader);
                    material.name = root.name + "_PBR_" + materialIndex;
                    Color sourceColor = material.HasProperty("_Color")
                        ? material.color
                        : Color.white;
                    material.color = Color.Lerp(sourceColor, accent, 0.32f);
                    material.SetFloat("_Metallic", metallic);
                    material.SetFloat("_Glossiness", smoothness);
                    replacements[materialIndex] = material;
                }

                renderer.sharedMaterials = replacements;
                renderer.enabled = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static void ApplyChampionPbr(
            GameObject champion,
            FirstSessionChampionBaseVisualAsset visual,
            ChampionCustomizationState appearance,
            string bodyBaseId,
            RealmId realm)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("Standard shader unavailable for authored champion.");
                return;
            }

            Color accent = ResolveRealmColor(realm);
            Renderer[] renderers = champion.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] sourceMaterials = renderer.sharedMaterials;
                var replacements = new Material[sourceMaterials.Length];
                for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
                {
                    string region = ResolveMaterialRegion(sourceMaterials[materialIndex]);
                    var material = new Material(shader)
                    {
                        name = "ChampionRuntime_" + bodyBaseId + "_" + region
                    };
                    material.SetTexture("_MainTex", visual.BaseColor);
                    material.SetTexture("_BumpMap", visual.Normal);
                    material.EnableKeyword("_NORMALMAP");
                    material.SetTexture("_MetallicGlossMap", visual.Metallic);
                    material.EnableKeyword("_METALLICGLOSSMAP");
                    material.SetTexture("_EmissionMap", visual.Emission);
                    material.SetColor("_EmissionColor", Color.Lerp(Color.black, accent, 0.08f));
                    material.EnableKeyword("_EMISSION");
                    ApplyRegionSurface(material, region, appearance, accent);
                    replacements[materialIndex] = material;
                }

                renderer.sharedMaterials = replacements;
                renderer.enabled = true;
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static string ResolveBodyBaseId(ChampionCustomizationState appearance)
        {
            return appearance == null || string.IsNullOrEmpty(appearance.BodyBaseId)
                ? "male"
                : appearance.BodyBaseId;
        }

        private static string ResolveMaterialRegion(Material source)
        {
            string name = source != null ? source.name : string.Empty;
            if (name.IndexOf("Hair", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Hair";
            }

            if (name.IndexOf("Metal", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Metal";
            }

            if (name.IndexOf("Skin", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Skin";
            }

            return "Cloth";
        }

        private static void ApplyRegionSurface(
            Material material,
            string region,
            ChampionCustomizationState appearance,
            Color realmAccent)
        {
            Color primary = appearance != null
                ? new Color(appearance.PrimaryR, appearance.PrimaryG, appearance.PrimaryB)
                : realmAccent;
            Color hair = appearance != null
                ? new Color(appearance.HairR, appearance.HairG, appearance.HairB)
                : new Color(0.12f, 0.08f, 0.06f);
            Color skin = appearance != null
                ? new Color(appearance.SkinR, appearance.SkinG, appearance.SkinB)
                : new Color(0.68f, 0.50f, 0.44f);
            Color accent = appearance != null
                ? new Color(appearance.AccentR, appearance.AccentG, appearance.AccentB)
                : realmAccent;

            switch (region)
            {
                case "Hair":
                    material.color = hair;
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Glossiness", 0.32f);
                    material.SetFloat("_GlossMapScale", 0.32f);
                    break;
                case "Metal":
                    material.color = accent;
                    material.SetFloat("_Metallic", 0.82f);
                    material.SetFloat("_Glossiness", 0.52f);
                    material.SetFloat("_GlossMapScale", 0.72f);
                    break;
                case "Skin":
                    material.color = skin;
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Glossiness", 0.28f);
                    material.SetFloat("_GlossMapScale", 0.28f);
                    break;
                default:
                    material.color = primary;
                    material.SetFloat("_Metallic", 0.24f);
                    material.SetFloat("_Glossiness", 0.44f);
                    material.SetFloat("_GlossMapScale", 0.44f);
                    break;
            }
        }

        private static void ApplyBodyBlendshape(GameObject champion, string bodyPresetId)
        {
            string selected = bodyPresetId switch
            {
                "slim" => "Body_Slim",
                "broad" => "Body_Broad",
                "massive" => "Body_Broad",
                "tall" => "Body_Tall",
                "statuesque" => "Body_Tall",
                "stout" => "Body_Stout",
                _ => string.Empty
            };

            foreach (SkinnedMeshRenderer renderer in
                     champion.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                for (int index = 0; index < mesh.blendShapeCount; index++)
                {
                    renderer.SetBlendShapeWeight(
                        index,
                        string.Equals(mesh.GetBlendShapeName(index), selected,
                            System.StringComparison.Ordinal)
                            ? 100f
                            : 0f);
                }
            }
        }

        private static void ApplyGuardianPbr(
            GameObject guardian,
            FirstSessionAuthoredAssetCatalog catalog)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("Standard shader unavailable for authored guardian.");
                return;
            }

            Renderer[] renderers = guardian.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                var material = new Material(shader)
                {
                    name = "CovenantGuardianPbr_Runtime"
                };
                material.SetTexture("_MainTex", catalog.GuardianBaseColor);
                material.SetTexture("_BumpMap", catalog.GuardianNormal);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", catalog.GuardianMetallic);
                material.EnableKeyword("_METALLICGLOSSMAP");
                material.SetFloat("_Metallic", 0.68f);
                material.SetFloat("_Glossiness", 0.44f);
                material.SetFloat("_GlossMapScale", 0.68f);
                material.SetTexture("_EmissionMap", catalog.GuardianEmission);
                material.SetColor("_EmissionColor", Color.white * 0.42f);
                material.EnableKeyword("_EMISSION");
                renderers[index].sharedMaterial = material;
                renderers[index].enabled = true;
                renderers[index].shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.On;
                renderers[index].receiveShadows = true;
            }
        }

        private static Color ResolveRealmColor(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold:
                    return new Color(0.66f, 0.33f, 0.15f);
                case RealmId.Eldergrove:
                    return new Color(0.18f, 0.48f, 0.31f);
                case RealmId.Crownlands:
                    return new Color(0.28f, 0.46f, 0.78f);
                case RealmId.Umbral:
                    return new Color(0.46f, 0.20f, 0.62f);
                default:
                    return Color.gray;
            }
        }
    }
}
