using UnityEngine;

namespace AL.ChampionMode.Interaction
{
    /// <summary>
    /// First-session Talk/Use props. Catalog IDs are the authored C1 objectives.
    /// Meshes stay TEMPORARY greybox — t_2bab542a owns real topology.
    /// </summary>
    public static class FirstSessionWorldInteractables
    {
        public const string GuideCatalogId = "OBJ_C1_MEET_REALM_GUIDE";
        public const string CovenantSiteCatalogId = "OBJ_C1_RESTORE_COVENANT";
        public const string RootName = "FirstSessionInteractables_TEMPORARY";
        public const string GuideObjectName = "OBJ_C1_MEET_REALM_GUIDE_TEMPORARY";
        public const string CovenantSiteObjectName = "OBJ_C1_RESTORE_COVENANT_TEMPORARY";
        public const string DirectorName = "WorldInteractionDirector";

        public static readonly Vector3 GuideOffset = new Vector3(-3.15f, 0f, 2.35f);
        public static readonly Vector3 CovenantSiteOffset = new Vector3(3.25f, 0f, 2.55f);

        public static WorldInteractionDirector Install(Transform player, UnityEngine.Camera camera)
        {
            if (player == null)
            {
                return null;
            }

            var root = new GameObject(RootName);
            Vector3 origin = player.position;
            origin.y = 0f;
            root.transform.position = origin;

            WorldInteractable guide = CreateGuide(root.transform, origin + GuideOffset);
            WorldInteractable site = CreateCovenantSite(root.transform, origin + CovenantSiteOffset);

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

        private static WorldInteractable CreateGuide(Transform parent, Vector3 position)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = GuideObjectName;
            body.transform.SetParent(parent, true);
            body.transform.position = position + Vector3.up * 1.05f;
            body.transform.localScale = new Vector3(0.72f, 1.05f, 0.72f);
            ApplyColor(body, new Color(0.62f, 0.54f, 0.38f));

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plate.name = FirstSessionChampionStart.LabelTemporary("GuideMark");
            plate.transform.SetParent(body.transform, false);
            plate.transform.localPosition = new Vector3(0f, -1.02f, 0f);
            plate.transform.localScale = new Vector3(1.6f, 0.03f, 1.6f);
            ApplyColor(plate, new Color(0.78f, 0.64f, 0.30f));

            var interactable = body.AddComponent<WorldInteractable>();
            interactable.Configure(
                GuideCatalogId,
                WorldInteractionKind.Talk,
                WorldInteractionPromptCopy.GuideSubject,
                WorldInteractionPromptCopy.GuideObjectiveText);
            return interactable;
        }

        private static WorldInteractable CreateCovenantSite(Transform parent, Vector3 position)
        {
            var dais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dais.name = CovenantSiteObjectName;
            dais.transform.SetParent(parent, true);
            dais.transform.position = position + Vector3.up * 0.18f;
            dais.transform.localScale = new Vector3(1.7f, 0.18f, 1.7f);
            ApplyColor(dais, new Color(0.18f, 0.16f, 0.14f));

            var obelisk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obelisk.name = FirstSessionChampionStart.LabelTemporary("CovenantObelisk");
            obelisk.transform.SetParent(dais.transform, false);
            obelisk.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            obelisk.transform.localScale = new Vector3(0.22f, 3.4f, 0.22f);
            ApplyColor(obelisk, new Color(0.42f, 0.36f, 0.28f));

            var ember = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ember.name = FirstSessionChampionStart.LabelTemporary("CovenantEmber");
            ember.transform.SetParent(dais.transform, false);
            ember.transform.localPosition = new Vector3(0f, 8.1f, 0f);
            ember.transform.localScale = new Vector3(0.38f, 0.22f, 0.38f);
            ApplyColor(ember, new Color(0.92f, 0.62f, 0.22f));

            var interactable = dais.AddComponent<WorldInteractable>();
            interactable.Configure(
                CovenantSiteCatalogId,
                WorldInteractionKind.Use,
                WorldInteractionPromptCopy.CovenantSiteSubject,
                WorldInteractionPromptCopy.CovenantObjectiveText);
            return interactable;
        }

        private static void ApplyColor(GameObject target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return;
            }

            var material = new Material(renderer.sharedMaterial);
            material.color = color;
            renderer.sharedMaterial = material;
        }
    }
}
