using System;
using AL.ChampionMode.Presentation;
using AL.Core;
using AL.Data.Runtime;
using AL.World;
using UnityEngine;

namespace AL.ChampionMode.Interaction
{
    /// <summary>
    /// First-session Talk/Use props. Catalog IDs are the authored C1 objectives.
    /// Interaction roots stay temporary while visible guide presentation uses admitted authored art.
    /// </summary>
    public static class FirstSessionWorldInteractables
    {
        public const string GuideCatalogId = "OBJ_C1_MEET_REALM_GUIDE";
        public const string CovenantSiteCatalogId = "OBJ_C1_RESTORE_COVENANT";
        public const string RootName = "FirstSessionInteractables";
        public const string GuideObjectName = "CaptainValerius";
        public const string CovenantSiteObjectName = "CovenantSite";
        public const string DirectorName = "WorldInteractionDirector";

        public static WorldInteractionDirector Install(
            Transform player,
            UnityEngine.Camera camera,
            RealmId realm = RealmId.Crownlands,
            FirstSessionAuthoredRealmRoute route = null)
        {
            if (player == null)
            {
                return null;
            }
            if (route == null)
            {
                route = UnityEngine.Object.FindFirstObjectByType<FirstSessionAuthoredRealmRoute>();
            }
            if (route == null || !route.HasCompleteRoute())
            {
                Debug.LogError("[AL-FIRST-SESSION-ROUTE-MISSING] interactables_not_installed");
                return null;
            }

            var root = new GameObject(RootName);
            WorldInteractable guide = CreateGuide(
                root.transform,
                route.CaptainValerius,
                realm);
            WorldInteractable site = CreateCovenantSite(
                root.transform,
                route.CovenantSite);

            var directorObject = new GameObject(DirectorName);
            directorObject.transform.SetParent(root.transform, false);
            WorldInteractionDirector director = directorObject.AddComponent<WorldInteractionDirector>();
            WorldInteractionPromptView prompt = WorldInteractionPromptView.Create(
                directorObject.transform,
                () => { director.TryConfirmFocused(); });
            director.Configure(player, camera, prompt);
            director.Register(guide);
            director.Register(site);
            return director;
        }

        private static WorldInteractable CreateGuide(
            Transform parent,
            Transform routeAnchor,
            RealmId realm)
        {
            var body = new GameObject(GuideObjectName);
            body.name = GuideObjectName;
            body.transform.SetParent(parent, false);
            body.transform.position = routeAnchor.position + Vector3.up * 1.05f;
            body.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.height = 2.1f;
            capsule.radius = 0.48f;

            if (!FirstSessionAuthoredVisualBinder.TryBindChampion(
                    body,
                    realm,
                    new ChampionCustomizationState { BodyBaseId = "male" },
                    out string diagnostic))
            {
                throw new InvalidOperationException(
                    "Captain Valerius authored presentation failed: " + diagnostic);
            }
            GroundAuthoredHumanoid(body, routeAnchor.position.y);
            AddValeriusVisibilityLight(body.transform);

            var interactable = body.AddComponent<WorldInteractable>();
            interactable.Configure(
                GuideCatalogId,
                WorldInteractionKind.Talk,
                WorldInteractionPromptCopy.GuideSubject,
                WorldInteractionPromptCopy.GuideObjectiveText);
            CreateGuideLabel(body.transform);
            return interactable;
        }

        private static void CreateGuideLabel(Transform parent)
        {
            var labelObject = new GameObject("CaptainValeriusLabel");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = "CAPTAIN VALERIUS";
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.045f;
            label.color = new Color(0.86f, 0.92f, 1f, 1f);
            labelObject.AddComponent<WorldSpaceTextBillboard>();
        }

        private static void AddValeriusVisibilityLight(Transform parent)
        {
            var lightObject = new GameObject("CaptainValeriusVisibilityLight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.35f, -0.35f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.48f);
            light.range = 6f;
            light.intensity = 1.8f;
            light.shadows = LightShadows.None;
        }

        private static WorldInteractable CreateCovenantSite(
            Transform parent,
            Transform routeAnchor)
        {
            var dais = new GameObject(CovenantSiteObjectName);
            dais.name = CovenantSiteObjectName;
            dais.transform.SetParent(parent, false);
            dais.transform.position = routeAnchor.position;
            AddAuthoredCovenantProp(dais.transform);

            var collider = dais.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.8f, 0f);
            collider.height = 1.6f;
            collider.radius = 1.2f;

            var interactable = dais.AddComponent<WorldInteractable>();
            interactable.Configure(
                CovenantSiteCatalogId,
                WorldInteractionKind.Use,
                WorldInteractionPromptCopy.CovenantSiteSubject,
                WorldInteractionPromptCopy.CovenantObjectiveText);
            return interactable;
        }

        private static void GroundAuthoredHumanoid(GameObject root, float groundY)
        {
            SkinnedMeshRenderer[] renderers =
                root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "Captain Valerius authored presentation has no skinned renderer.");
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].enabled = true;
                renderers[index].forceRenderingOff = false;
                renderers[index].updateWhenOffscreen = true;
            }

            Transform visualRoot = root.transform.Find(
                FirstSessionAuthoredVisualBinder.ChampionVisualName);
            if (visualRoot == null)
            {
                throw new InvalidOperationException(
                    "Captain Valerius authored visual root is missing.");
            }

            visualRoot.localScale *= 1.18f;
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            visualRoot.position += Vector3.up * (groundY - bounds.min.y);
        }

        private static void AddAuthoredCovenantProp(Transform parent)
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            if (catalog == null || catalog.CovenantHallPrefab == null)
            {
                throw new InvalidOperationException("Authored covenant prop catalog is missing.");
            }

            GameObject kit = UnityEngine.Object.Instantiate(catalog.CovenantHallPrefab);
            try
            {
                Transform brazier = FindDescendant(kit.transform, "BrazierProp");
                if (brazier == null)
                {
                    throw new InvalidOperationException(
                        "Authored covenant hall kit has no BrazierProp.");
                }

                GameObject prop = UnityEngine.Object.Instantiate(brazier.gameObject, parent);
                prop.name = "AuthoredCovenantBrazier";
                prop.SetActive(true);
                prop.transform.localPosition = Vector3.zero;
                prop.transform.localRotation = Quaternion.identity;
                prop.transform.localScale = Vector3.one * 1.35f;
                Renderer renderer = prop.GetComponentInChildren<Renderer>(true);
                if (renderer != null)
                {
                    prop.transform.position += Vector3.up *
                                               (parent.position.y - renderer.bounds.min.y);
                }
            }
            finally
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(kit);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(kit);
                }
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                if (string.Equals(descendants[index].name, name, StringComparison.Ordinal))
                {
                    return descendants[index];
                }
            }

            return null;
        }
    }
}
