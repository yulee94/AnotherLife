using AL.ChampionMode;
using AL.ChampionMode.Camera;
using AL.ChampionMode.Control;
using AL.ChampionMode.Customization;
using AL.Core;
using AL.Data.Catalogs.WorldAtlas;
using UnityEngine;
using UnityEngine.UI;

namespace AL.World
{
    public sealed class InnerRealmWorldSceneController : MonoBehaviour
    {
        [SerializeField] private string _walkableRealmId = "stonehold";

        private InnerRealmWorldBuildResult _built;

        private void Start()
        {
            Bootloader.InitializeIfMissing();
            string realmId = ResolveWalkableRealmId();
            WorldAtlasSnapshot snapshot = LoadCanonicalSnapshot();
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(snapshot);
            _built = InnerRealmWorldGreyboxBuilder.Build(layout, realmId);
            SpawnWalker(_built);
            BuildTemporaryBanner(_built, realmId);
        }

        public InnerRealmWorldBuildResult Built => _built;

        internal static WorldAtlasSnapshot LoadCanonicalSnapshot()
        {
            return FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
        }

        private string ResolveWalkableRealmId()
        {
            ChampionRealmContextResult context = ChampionRealmContext.ResolveRegistered();
            if (context.IsAvailable)
            {
                string catalogId = InnerRealmWorldLayout.RealmCatalogId(context.RealmId);
                if (!string.IsNullOrEmpty(catalogId))
                {
                    return catalogId;
                }
            }

            return string.IsNullOrEmpty(_walkableRealmId) ? "stonehold" : _walkableRealmId;
        }

        private static void SpawnWalker(InnerRealmWorldBuildResult built)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player_Champion";
            player.tag = "Player";
            player.transform.position = built.PlayerSpawn;
            CapsuleCollider primitiveCollider = player.GetComponent<CapsuleCollider>();
            if (primitiveCollider != null)
            {
                primitiveCollider.enabled = false;
                UnityEngine.Object.Destroy(primitiveCollider);
            }

            if (player.GetComponent<CharacterController>() == null)
            {
                CharacterController controller = player.AddComponent<CharacterController>();
                controller.center = Vector3.zero;
                controller.height = 2f;
                controller.radius = 0.45f;
                controller.stepOffset = 0.3f;
                controller.minMoveDistance = 0f;
            }

            player.AddComponent<ChampionController>();
            ProceduralChampionModelBuilder.EnsureModel(player);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.22f, 0.28f, 0.34f);
            camera.farClipPlane = 420f;
            camera.fieldOfView = 48f;
            cameraObject.AddComponent<AudioListener>();
            CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
            follow.ConfigureChampion(player.transform);

            var lightObject = new GameObject("Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.94f, 0.86f);
            lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            RenderSettings.ambientLight = new Color(0.28f, 0.30f, 0.32f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.36f, 0.40f, 0.42f);
            RenderSettings.fogDensity = 0.0045f;
        }

        private static void BuildTemporaryBanner(InnerRealmWorldBuildResult built, string realmId)
        {
            var canvasObject = new GameObject("TEMPORARY_WorldBanner");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(canvasObject.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 18;
            text.color = new Color(0.92f, 0.86f, 0.52f, 0.92f);
            text.alignment = TextAnchor.UpperLeft;
            text.text =
                InnerRealmWorldIds.TemporaryLabel +
                " greybox — " + built.WalkableInner.InnerAtlasZoneId +
                " / unnamed " + InnerRealmWorldIds.DisplayCapital() +
                "\nplacement=" + built.Layout.PlacementStatus +
                " walk=" + realmId +
                "\n" + built.Layout.ColoredMapNote;
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(760f, 84f);
        }
    }
}
