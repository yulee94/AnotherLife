using AL.ChampionMode.Customization;
using AL.Core;
using AL.Utilities;
using UnityEngine;

namespace AL.ChampionMode.AI
{
    public class RvrBotSpawner : MonoBehaviour
    {
        private static readonly RealmId[] SpawnRealms =
        {
            RealmId.Stonehold,
            RealmId.Eldergrove,
            RealmId.Crownlands,
            RealmId.Umbral
        };

        [Header("Crowd")]
        [SerializeField, Range(10, 100)] private int _botCount = 40;
        [SerializeField] private float _spawnRadius = 18f;
        [SerializeField] private float _arenaRadius = 28f;

        [Header("References")]
        [SerializeField] private Transform _player;
        [SerializeField] private Transform _fallbackObjective;
        [SerializeField] private RealmId _playerRealm = RealmId.Crownlands;

        private bool _spawned;

        public void Configure(Transform player, Transform fallbackObjective, RealmId playerRealm, int botCount)
        {
            _player = player;
            _fallbackObjective = fallbackObjective;
            _playerRealm = playerRealm == RealmId.None ? RealmId.Crownlands : playerRealm;
            _botCount = Mathf.Clamp(botCount, 10, 100);
            Spawn();
        }

        public void Spawn()
        {
            if (_spawned)
            {
                return;
            }

            _spawned = true;
            int count = Mathf.Clamp(_botCount, 10, 100);
            for (int i = 0; i < count; i++)
            {
                RealmId realmId = SpawnRealms[i % SpawnRealms.Length];
                float angle = i * Mathf.PI * 2f / count;
                float ringJitter = Random.Range(-2.5f, 2.5f);
                Vector3 position = transform.position + new Vector3(
                    Mathf.Cos(angle) * (_spawnRadius + ringJitter),
                    1.1f,
                    Mathf.Sin(angle) * (_spawnRadius + ringJitter));

                CreateBot(i, realmId, position);
            }
        }

        private void CreateBot(int index, RealmId realmId, Vector3 position)
        {
            var bot = new GameObject($"BotChampion_{realmId}_{index:00}");
            bot.transform.SetParent(transform, true);
            bot.transform.position = position;

            Color realmColor = GetRealmColor(realmId);
            var highDetail = CreateHighDetailModel(bot.transform, realmColor);
            var mediumDetail = CreateMediumDetailModel(bot.transform, realmColor);
            var marker = CreateMarker(bot.transform, realmColor, realmId);

            var lod = bot.AddComponent<LODCombatVisualController>();
            lod.Configure(highDetail, mediumDetail, marker);

            var ai = bot.AddComponent<BotChampionAI>();
            float moveScale = realmId == RealmId.Eldergrove ? 1.12f : 1f;
            ai.Configure(realmId, _playerRealm, _fallbackObjective != null ? _fallbackObjective : _player, transform.position, _arenaRadius, moveScale);
        }

        private GameObject CreateHighDetailModel(Transform parent, Color realmColor)
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "HighDetail_ChampionModel";
            model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            SetMaterialColor(model, realmColor);
            ProceduralChampionModelBuilder.EnsureModel(model);
            return model;
        }

        private GameObject CreateMediumDetailModel(Transform parent, Color realmColor)
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "MediumDetail_Silhouette";
            model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = new Vector3(0.82f, 0.95f, 0.82f);
            SetMaterialColor(model, Color.Lerp(realmColor, Color.black, 0.28f));
            Destroy(model.GetComponent<Collider>());
            return model;
        }

        private GameObject CreateMarker(Transform parent, Color realmColor, RealmId realmId)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "LowDetail_RealmMarker";
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = new Vector3(0.52f, 0.04f, 0.52f);
            SetMaterialColor(marker, Color.Lerp(realmColor, Color.white, 0.18f));
            Destroy(marker.GetComponent<Collider>());

            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = $"MarkerBanner_{realmId}";
            banner.transform.SetParent(marker.transform, false);
            banner.transform.localPosition = new Vector3(0f, 8f, 0f);
            banner.transform.localScale = new Vector3(0.16f, 5.5f, 0.16f);
            SetMaterialColor(banner, realmColor);
            Destroy(banner.GetComponent<Collider>());
            return marker;
        }

        private static Color GetRealmColor(RealmId realmId)
        {
            return realmId switch
            {
                RealmId.Stonehold => new Color(0.72f, 0.48f, 0.24f),
                RealmId.Eldergrove => new Color(0.18f, 0.76f, 0.34f),
                RealmId.Crownlands => new Color(0.22f, 0.46f, 0.92f),
                RealmId.Umbral => new Color(0.58f, 0.08f, 0.72f),
                _ => Color.gray
            };
        }

        private static void SetMaterialColor(GameObject target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }
    }
}
