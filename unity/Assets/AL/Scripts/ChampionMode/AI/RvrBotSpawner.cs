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
        [SerializeField] private RealmId _playerRealm = RealmId.None;

        private bool _spawned;

        public void Configure(Transform player, Transform fallbackObjective, RealmId playerRealm, int botCount)
        {
            _player = player;
            _fallbackObjective = fallbackObjective;
            _playerRealm = playerRealm;
            _botCount = Mathf.Clamp(botCount, 10, 100);
            Spawn();
        }

        public void Spawn()
        {
            if (_spawned)
            {
                return;
            }

            // A committed realm is required before realm-sensitive combatants can
            // classify the player as an ally or enemy. Keep the spawner retryable
            // so a later valid configuration can proceed.
            if (_playerRealm == RealmId.None)
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
            var highDetail = CreateHighDetailModel(bot.transform, realmColor, realmId, index);
            var mediumDetail = CreateMediumDetailModel(bot.transform, realmColor);
            var marker = CreateMarker(bot.transform, realmColor, realmId);

            var lod = bot.AddComponent<LODCombatVisualController>();
            lod.Configure(highDetail, mediumDetail, marker);

            var ai = bot.AddComponent<BotChampionAI>();
            float moveScale = realmId == RealmId.Eldergrove ? 1.12f : 1f;
            ai.Configure(realmId, _playerRealm, _fallbackObjective != null ? _fallbackObjective : _player, transform.position, _arenaRadius, moveScale);
        }

        private GameObject CreateHighDetailModel(Transform parent, Color realmColor, RealmId realmId, int index)
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "HighDetail_ChampionModel";
            model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            SetMaterialColor(model, realmColor);
            ProceduralChampionModelBuilder.EnsureModel(model);
            ApplyBotVisualVariant(model, realmId, realmColor, index);
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
            var marker = new GameObject("LowDetail_RealmMarker");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localRotation = Quaternion.identity;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "MarkerDisc";
            disc.transform.SetParent(marker.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            disc.transform.localScale = new Vector3(0.52f, 0.04f, 0.52f);
            SetMaterialColor(disc, Color.Lerp(realmColor, Color.white, 0.18f));
            Destroy(disc.GetComponent<Collider>());

            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = $"MarkerBanner_{realmId}";
            banner.transform.SetParent(marker.transform, false);
            banner.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            banner.transform.localScale = new Vector3(0.16f, 1.4f, 0.16f);
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

        private static void ApplyBotVisualVariant(GameObject model, RealmId realmId, Color realmColor, int index)
        {
            string hairStyle = Pick(index, "short", "long", "braid", "mohawk", "topknot");
            string armorStyle = Pick(index / 2, "realm_basic", "light_scout", "heavy_plate", "arcane_robes", "assassin_leathers");
            string weaponStyle = Pick(index / 3, "sword", "axe", "staff", "bow", "hammer");
            string offhandStyle = Pick(index / 4, "shield", "orb", "dagger", "tome", "none");
            string faceMark = Pick(index / 5, "none", "scar", "warpaint", "realm_mark", "rune", "tattoo", "beard", "duelist_scar", "ash_mask");

            SetExactPartActive(model.transform, "Hair_Short", hairStyle == "short");
            SetExactPartActive(model.transform, "Hair_Long", hairStyle == "long");
            SetExactPartActive(model.transform, "Hair_Braid", hairStyle == "braid");
            SetExactPartActive(model.transform, "Hair_Mohawk", hairStyle == "mohawk");
            SetExactPartActive(model.transform, "Hair_Topknot", hairStyle == "topknot");
            SetExactPartActive(model.transform, "Hair_Topknot_Tail", hairStyle == "topknot");

            bool isRobe = armorStyle == "arcane_robes";
            bool isAssassin = armorStyle == "assassin_leathers";
            bool isLight = armorStyle == "light_scout" || isAssassin || isRobe;
            bool isHeavy = armorStyle == "heavy_plate";
            SetPartActive(model.transform, "Hood", isRobe);
            SetPartActive(model.transform, "RobePanel", isRobe);
            SetPartActive(model.transform, "ArmorTrim", isRobe || realmId == RealmId.Crownlands);
            SetPartActive(model.transform, "Shoulder", !isLight);
            SetPartActive(model.transform, "Knee", !isRobe);
            SetPartScale(model.transform, "ChestArmor", isRobe ? new Vector3(0.78f, 0.82f, 0.28f) : isAssassin ? new Vector3(0.84f, 0.64f, 0.26f) : isHeavy ? new Vector3(1.05f, 0.82f, 0.38f) : new Vector3(0.92f, 0.74f, 0.32f));

            SetExactPartActive(model.transform, "FaceMark", false);
            SetExactPartActive(model.transform, "FaceMark_Secondary", false);
            SetExactPartActive(model.transform, "FaceMark_Tertiary", false);
            SetPartActive(model.transform, "FacialHair", false);
            SetExactPartActive(model.transform, "Weapon_Head", weaponStyle == "axe" || weaponStyle == "hammer");
            SetExactPartActive(model.transform, "Bow_String", weaponStyle == "bow");
            SetExactPartActive(model.transform, "Shield_Off", offhandStyle == "shield");
            SetExactPartActive(model.transform, "Orb_Off", offhandStyle == "orb");
            SetExactPartActive(model.transform, "Weapon_Off", offhandStyle == "dagger");
            SetExactPartActive(model.transform, "Tome_Off", offhandStyle == "tome");

            ApplyFaceMarkPose(model.transform, faceMark);
            ApplyWeaponPose(model.transform, weaponStyle);
            ApplyBotColors(model, realmId, realmColor, index);
            model.GetComponent<ProceduralChampionMotion>()?.Rebind();
        }

        private static void ApplyFaceMarkPose(Transform root, string faceMark)
        {
            switch (faceMark)
            {
                case "scar":
                    SetExactPartActive(root, "FaceMark", true);
                    SetPartTransform(root, "FaceMark", new Vector3(-0.08f, 0.62f, 0.49f), new Vector3(0.035f, 0.28f, 0.025f), new Vector3(0f, 0f, 24f));
                    break;
                case "warpaint":
                    SetExactPartActive(root, "FaceMark", true);
                    SetPartTransform(root, "FaceMark", new Vector3(0f, 0.68f, 0.49f), new Vector3(0.13f, 0.13f, 0.025f), new Vector3(0f, 0f, 45f));
                    break;
                case "rune":
                    SetExactPartActive(root, "FaceMark", true);
                    SetPartTransform(root, "FaceMark", new Vector3(0.08f, 0.66f, 0.49f), new Vector3(0.12f, 0.12f, 0.025f), Vector3.zero);
                    break;
                case "tattoo":
                    SetExactPartActive(root, "FaceMark", true);
                    SetPartTransform(root, "FaceMark", new Vector3(0f, 0.58f, 0.49f), new Vector3(0.30f, 0.030f, 0.025f), new Vector3(0f, 0f, -18f));
                    break;
                case "beard":
                    SetPartActive(root, "FacialHair", true);
                    break;
                case "duelist_scar":
                    SetExactPartActive(root, "FaceMark", true);
                    SetExactPartActive(root, "FaceMark_Secondary", true);
                    SetPartTransform(root, "FaceMark", new Vector3(-0.10f, 0.68f, 0.494f), new Vector3(0.030f, 0.24f, 0.023f), new Vector3(0f, 0f, -20f));
                    SetPartTransform(root, "FaceMark_Secondary", new Vector3(0.14f, 0.61f, 0.494f), new Vector3(0.028f, 0.18f, 0.023f), new Vector3(0f, 0f, 24f));
                    break;
                case "ash_mask":
                    SetExactPartActive(root, "FaceMark", true);
                    SetExactPartActive(root, "FaceMark_Secondary", true);
                    SetExactPartActive(root, "FaceMark_Tertiary", true);
                    SetPartTransform(root, "FaceMark", new Vector3(0f, 0.59f, 0.496f), new Vector3(0.36f, 0.050f, 0.024f), Vector3.zero);
                    SetPartTransform(root, "FaceMark_Secondary", new Vector3(-0.13f, 0.71f, 0.496f), new Vector3(0.14f, 0.024f, 0.024f), new Vector3(0f, 0f, -8f));
                    SetPartTransform(root, "FaceMark_Tertiary", new Vector3(0.13f, 0.71f, 0.496f), new Vector3(0.14f, 0.024f, 0.024f), new Vector3(0f, 0f, 8f));
                    break;
                default:
                    if (faceMark != "none")
                    {
                        SetExactPartActive(root, "FaceMark", true);
                        SetPartTransform(root, "FaceMark", new Vector3(0f, 0.61f, 0.49f), new Vector3(0.24f, 0.035f, 0.025f), Vector3.zero);
                    }
                    break;
            }
        }

        private static void ApplyWeaponPose(Transform root, string weaponStyle)
        {
            switch (weaponStyle)
            {
                case "axe":
                    SetPartTransform(root, "Weapon_Main", new Vector3(0.72f, 0.02f, 0.16f), new Vector3(0.08f, 0.56f, 0.08f), new Vector3(0f, 0f, 18f));
                    SetPartTransform(root, "Weapon_Head", new Vector3(0.80f, 0.48f, 0.18f), new Vector3(0.28f, 0.18f, 0.10f), new Vector3(0f, 0f, 18f));
                    break;
                case "staff":
                    SetPartTransform(root, "Weapon_Main", new Vector3(0.72f, 0.02f, 0.16f), new Vector3(0.055f, 0.88f, 0.055f), new Vector3(0f, 0f, 8f));
                    break;
                case "bow":
                    SetPartTransform(root, "Weapon_Main", new Vector3(0.72f, 0.10f, 0.16f), new Vector3(0.04f, 0.82f, 0.04f), new Vector3(0f, 0f, 78f));
                    break;
                case "hammer":
                    SetPartTransform(root, "Weapon_Main", new Vector3(0.72f, 0.02f, 0.16f), new Vector3(0.075f, 0.60f, 0.075f), new Vector3(0f, 0f, 20f));
                    SetPartTransform(root, "Weapon_Head", new Vector3(0.84f, 0.50f, 0.18f), new Vector3(0.36f, 0.24f, 0.14f), new Vector3(0f, 0f, 20f));
                    break;
                default:
                    SetPartTransform(root, "Weapon_Main", new Vector3(0.72f, 0.00f, 0.16f), new Vector3(0.06f, 0.70f, 0.06f), new Vector3(0f, 0f, 34f));
                    break;
            }
        }

        private static void ApplyBotColors(GameObject model, RealmId realmId, Color realmColor, int index)
        {
            Color hair = Pick(index + (int)realmId, new Color(0.08f, 0.06f, 0.04f), new Color(0.55f, 0.36f, 0.16f), new Color(0.80f, 0.82f, 0.90f), new Color(0.25f, 0.05f, 0.08f));
            Color skin = Pick(index + 1, new Color(0.72f, 0.56f, 0.42f), new Color(0.55f, 0.38f, 0.26f), new Color(0.86f, 0.70f, 0.54f), new Color(0.42f, 0.34f, 0.40f));
            Color eye = Pick(index + 2, new Color(0.25f, 0.58f, 0.92f), new Color(0.28f, 0.72f, 0.42f), new Color(0.90f, 0.18f, 0.12f));
            Color accent = Color.Lerp(realmColor, Color.white, 0.18f);

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                string objectName = renderer.gameObject.name.ToLowerInvariant();
                Color color = objectName.Contains("hair") || objectName.Contains("brow")
                    ? hair
                    : objectName.Contains("skin") || objectName.Contains("ear")
                        ? skin
                        : objectName.Contains("eye")
                            ? eye
                            : objectName.Contains("facemark") || objectName.Contains("cape") || objectName.Contains("trim") || objectName.Contains("orb") || objectName.Contains("tome")
                                ? accent
                                : objectName.Contains("weapon") || objectName.Contains("shield") || objectName.Contains("armor") || objectName.Contains("shoulder") || objectName.Contains("glove") || objectName.Contains("boot") || objectName.Contains("knee")
                                    ? Color.Lerp(realmColor, Color.white, 0.25f)
                                    : realmColor;
                renderer.material.color = color;
            }
        }

        private static void SetPartActive(Transform root, string partName, bool isActive)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.ToLowerInvariant().Contains(partName.ToLowerInvariant()))
                {
                    child.gameObject.SetActive(isActive);
                }
            }
        }

        private static void SetExactPartActive(Transform root, string partName, bool isActive)
        {
            Transform part = root.Find(partName);
            if (part != null)
            {
                part.gameObject.SetActive(isActive);
            }
        }

        private static void SetPartScale(Transform root, string partName, Vector3 scale)
        {
            Transform part = root.Find(partName);
            if (part != null)
            {
                part.localScale = scale;
            }
        }

        private static void SetPartTransform(Transform root, string partName, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles)
        {
            Transform part = root.Find(partName);
            if (part != null)
            {
                part.localPosition = localPosition;
                part.localScale = localScale;
                part.localRotation = Quaternion.Euler(localEulerAngles);
            }
        }

        private static T Pick<T>(int index, params T[] values)
        {
            return values[Mathf.Abs(index) % values.Length];
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
