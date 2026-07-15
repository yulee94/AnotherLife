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
            ItemGrade grade = ResolveBotGrade(index, realmId);
            var highDetail = CreateHighDetailModel(bot.transform, realmColor, realmId, grade, index);
            var mediumDetail = CreateMediumDetailModel(bot.transform, realmColor, grade);
            var marker = CreateMarker(bot.transform, realmColor, realmId, grade);
            CreateTierVisuals(bot.transform, realmColor, realmId, grade, index);

            var lod = bot.AddComponent<LODCombatVisualController>();
            lod.Configure(highDetail, mediumDetail, marker);

            var ai = bot.AddComponent<BotChampionAI>();
            float moveScale = realmId == RealmId.Eldergrove ? 1.12f : 1f;
            ai.Configure(realmId, _playerRealm, _fallbackObjective != null ? _fallbackObjective : _player, transform.position, _arenaRadius, moveScale);
        }

        private GameObject CreateHighDetailModel(Transform parent, Color realmColor, RealmId realmId, ItemGrade grade, int index)
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "HighDetail_ChampionModel";
            model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.12f, GetGradePower(grade));
            SetMaterialColor(model, realmColor);
            ProceduralChampionModelBuilder.EnsureModel(model);
            ApplyBotVisualVariant(model, realmId, realmColor, index);
            ApplyBotTierMaterials(model, realmColor, grade);
            return model;
        }

        private GameObject CreateMediumDetailModel(Transform parent, Color realmColor, ItemGrade grade)
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "MediumDetail_Silhouette";
            model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = new Vector3(0.82f, 0.95f, 0.82f) * Mathf.Lerp(0.96f, 1.14f, GetGradePower(grade));
            SetMaterialColor(model, Color.Lerp(GetGradeColor(grade, realmColor), Color.black, 0.22f));
            Destroy(model.GetComponent<Collider>());
            return model;
        }

        private GameObject CreateMarker(Transform parent, Color realmColor, RealmId realmId, ItemGrade grade)
        {
            var marker = new GameObject("LowDetail_RealmMarker");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localRotation = Quaternion.identity;
            float gradePower = GetGradePower(grade);
            Color gradeColor = GetGradeColor(grade, realmColor);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "MarkerDisc";
            disc.transform.SetParent(marker.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            disc.transform.localScale = new Vector3(0.52f + gradePower * 0.22f, 0.04f, 0.52f + gradePower * 0.22f);
            SetMaterialColor(disc, Color.Lerp(gradeColor, Color.white, 0.18f + gradePower * 0.14f));
            Destroy(disc.GetComponent<Collider>());

            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = $"MarkerBanner_{realmId}_{grade}";
            banner.transform.SetParent(marker.transform, false);
            banner.transform.localPosition = new Vector3(0f, 1.2f + gradePower * 0.18f, 0f);
            banner.transform.localScale = new Vector3(0.16f + gradePower * 0.04f, 1.4f + gradePower * 0.36f, 0.16f + gradePower * 0.04f);
            SetMaterialColor(banner, gradeColor);
            Destroy(banner.GetComponent<Collider>());

            if (grade >= ItemGrade.Epic)
            {
                var crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                crown.name = "MarkerChampionCrown";
                crown.transform.SetParent(marker.transform, false);
                crown.transform.localPosition = new Vector3(0f, 2.12f + gradePower * 0.22f, 0f);
                crown.transform.localScale = new Vector3(0.28f + gradePower * 0.12f, 0.025f, 0.28f + gradePower * 0.12f);
                SetMaterialColor(crown, Color.Lerp(gradeColor, Color.white, 0.26f));
                Destroy(crown.GetComponent<Collider>());
            }

            return marker;
        }

        private void CreateTierVisuals(Transform parent, Color realmColor, RealmId realmId, ItemGrade grade, int index)
        {
            if (grade < ItemGrade.Rare)
            {
                return;
            }

            float gradePower = GetGradePower(grade);
            Color gradeColor = GetGradeColor(grade, realmColor);
            var root = new GameObject($"TierVisuals_{grade}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;

            CreateTierPrimitive(root.transform, "TierAura_Ring", PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0f), new Vector3(0.82f + gradePower * 0.44f, 0.010f, 0.82f + gradePower * 0.44f), Vector3.zero, gradeColor, 0.54f + gradePower * 0.32f);
            CreateTierPrimitive(root.transform, "TierAura_Core", PrimitiveType.Cylinder, new Vector3(0f, 1.25f + gradePower * 0.12f, -0.18f), new Vector3(0.18f + gradePower * 0.06f, 0.014f, 0.18f + gradePower * 0.06f), new Vector3(90f, 0f, 0f), Color.Lerp(gradeColor, Color.white, 0.18f), 0.70f + gradePower * 0.24f);

            int shardCount = Mathf.RoundToInt(Mathf.Lerp(2f, 7f, gradePower));
            for (int i = 0; i < shardCount; i++)
            {
                float angle = i * Mathf.PI * 2f / shardCount + index * 0.17f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * (0.54f + gradePower * 0.16f), 0.55f + i % 2 * 0.22f, Mathf.Sin(angle) * (0.54f + gradePower * 0.16f));
                CreateTierPrimitive(root.transform, "TierAura_Shard_" + i, PrimitiveType.Cube, position, new Vector3(0.045f + gradePower * 0.018f, 0.22f + gradePower * 0.16f, 0.045f + gradePower * 0.018f), new Vector3(0f, -angle * Mathf.Rad2Deg, 18f), i % 2 == 0 ? gradeColor : Color.Lerp(realmColor, Color.white, 0.20f), 0.66f + gradePower * 0.24f);
            }

            if (grade >= ItemGrade.Mythic)
            {
                CreateTierPrimitive(root.transform, "TierAura_BackStandard", PrimitiveType.Cube, new Vector3(0f, 0.72f, -0.42f), new Vector3(0.08f, 1.10f, 0.055f), new Vector3(0f, index % 2 == 0 ? 8f : -8f, 0f), Color.Lerp(gradeColor, realmColor, 0.20f), 0.82f);
            }

            root.AddComponent<BotChampionTierVfx>().Configure(grade, gradeColor, realmId);
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

        private static ItemGrade ResolveBotGrade(int index, RealmId realmId)
        {
            int seed = Mathf.Abs(index * 31 + (int)realmId * 17);
            if (seed % 37 == 0)
            {
                return ItemGrade.Celestial;
            }

            if (seed % 19 == 0)
            {
                return ItemGrade.Mythic;
            }

            if (seed % 11 == 0)
            {
                return ItemGrade.Legendary;
            }

            if (seed % 7 == 0)
            {
                return ItemGrade.Epic;
            }

            if (seed % 5 == 0)
            {
                return ItemGrade.Rare;
            }

            return ItemGrade.Common;
        }

        private static float GetGradePower(ItemGrade grade)
        {
            return grade switch
            {
                ItemGrade.Rare => 0.22f,
                ItemGrade.Epic => 0.42f,
                ItemGrade.Legendary => 0.62f,
                ItemGrade.Mythic => 0.82f,
                ItemGrade.Celestial => 1f,
                _ => 0.06f
            };
        }

        private static Color GetGradeColor(ItemGrade grade, Color realmColor)
        {
            return grade switch
            {
                ItemGrade.Rare => Color.Lerp(realmColor, new Color(0.38f, 0.82f, 1f), 0.55f),
                ItemGrade.Epic => Color.Lerp(realmColor, new Color(0.78f, 0.34f, 1f), 0.60f),
                ItemGrade.Legendary => new Color(1f, 0.72f, 0.22f),
                ItemGrade.Mythic => new Color(1f, 0.30f, 0.12f),
                ItemGrade.Celestial => new Color(0.70f, 0.94f, 1f),
                _ => realmColor
            };
        }

        private static void ApplyBotTierMaterials(GameObject model, Color realmColor, ItemGrade grade)
        {
            if (grade < ItemGrade.Rare)
            {
                return;
            }

            float gradePower = GetGradePower(grade);
            Color gradeColor = GetGradeColor(grade, realmColor);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.material == null)
                {
                    continue;
                }

                string objectName = renderer.gameObject.name.ToLowerInvariant();
                if (!objectName.Contains("trim") && !objectName.Contains("gem") && !objectName.Contains("eye") && !objectName.Contains("rune") && !objectName.Contains("weapon"))
                {
                    continue;
                }

                renderer.material.color = Color.Lerp(renderer.material.color, gradeColor, 0.20f + gradePower * 0.20f);
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", gradeColor * Mathf.Lerp(0.25f, 0.92f, gradePower));
                }
            }
        }

        private static GameObject CreateTierPrimitive(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Color color, float emission)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;
            obj.transform.localRotation = Quaternion.Euler(localEulerAngles);
            Destroy(obj.GetComponent<Collider>());

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", color * Mathf.Clamp(emission, 0.08f, 1.25f));
                }
            }

            return obj;
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

    internal sealed class BotChampionTierVfx : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<Renderer> _renderers = new System.Collections.Generic.List<Renderer>();
        private readonly System.Collections.Generic.List<Color> _baseColors = new System.Collections.Generic.List<Color>();
        private ItemGrade _grade = ItemGrade.Common;
        private Color _accent = Color.white;
        private float _speed = 1f;
        private float _baseScale = 1f;

        public void Configure(ItemGrade grade, Color accent, RealmId realmId)
        {
            _grade = grade;
            _accent = accent;
            _speed = Mathf.Lerp(0.85f, 2.2f, GetGradePower(grade)) + ((int)realmId % 3) * 0.12f;
            _baseScale = transform.localScale.x <= 0f ? 1f : transform.localScale.x;
            CollectRenderers();
        }

        public void PlayDefeatBurst(Vector3 position, RealmId attackerRealm)
        {
            if (_grade < ItemGrade.Rare)
            {
                return;
            }

            float gradePower = GetGradePower(_grade);
            var root = new GameObject("BotChampionTierDefeatBurst_" + _grade);
            root.transform.position = position + Vector3.up * 0.04f;
            Color attackerTint = GetRealmTint(attackerRealm);
            Color burstColor = Color.Lerp(_accent, attackerTint, 0.28f);

            CreateBurstPrimitive(root.transform, "DefeatBurst_Ring", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.72f + gradePower * 0.72f, 0.012f, 0.72f + gradePower * 0.72f), Vector3.zero, burstColor, 0.82f);
            CreateBurstPrimitive(root.transform, "DefeatBurst_Core", PrimitiveType.Sphere, Vector3.up * (0.42f + gradePower * 0.24f), new Vector3(0.20f + gradePower * 0.18f, 0.20f + gradePower * 0.18f, 0.20f + gradePower * 0.18f), Vector3.zero, Color.Lerp(burstColor, Color.white, 0.22f), 0.96f);

            int shardCount = Mathf.RoundToInt(Mathf.Lerp(4f, 12f, gradePower));
            for (int i = 0; i < shardCount; i++)
            {
                float angle = i * Mathf.PI * 2f / shardCount;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                CreateBurstPrimitive(root.transform, "DefeatBurst_Shard_" + i, PrimitiveType.Cube, radial * (0.34f + gradePower * 0.28f) + Vector3.up * (0.34f + i % 3 * 0.16f), new Vector3(0.050f + gradePower * 0.030f, 0.28f + gradePower * 0.24f, 0.050f + gradePower * 0.030f), new Vector3(0f, -angle * Mathf.Rad2Deg, 22f), i % 2 == 0 ? burstColor : attackerTint, 0.72f + gradePower * 0.24f);
            }

            var lightObject = new GameObject("DefeatBurst_Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = Vector3.up * (0.72f + gradePower * 0.36f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = burstColor;
            light.intensity = Mathf.Lerp(1.2f, 3.8f, gradePower);
            light.range = Mathf.Lerp(3.2f, 6.4f, gradePower);

            root.AddComponent<BotChampionTierDefeatBurst>().Configure(_grade, burstColor);
        }

        private void OnEnable()
        {
            CollectRenderers();
        }

        private void CollectRenderers()
        {
            _renderers.Clear();
            _baseColors.Clear();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.material == null)
                {
                    continue;
                }

                _renderers.Add(renderer);
                _baseColors.Add(renderer.material.color);
            }
        }

        private void Update()
        {
            if (_grade < ItemGrade.Rare)
            {
                return;
            }

            float gradePower = GetGradePower(_grade);
            float pulse = (Mathf.Sin(Time.time * (3.2f + _speed)) + 1f) * 0.5f;
            transform.localScale = Vector3.one * (_baseScale + pulse * Mathf.Lerp(0.015f, 0.075f, gradePower));
            transform.Rotate(Vector3.up, Mathf.Lerp(12f, 46f, gradePower) * Time.deltaTime, Space.Self);

            for (int i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null || renderer.material == null)
                {
                    continue;
                }

                Color baseColor = i < _baseColors.Count ? _baseColors[i] : _accent;
                renderer.material.color = Color.Lerp(baseColor, _accent, pulse * Mathf.Lerp(0.08f, 0.34f, gradePower));
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", Color.Lerp(baseColor, _accent, 0.55f) * Mathf.Lerp(0.45f, 1.25f, pulse * gradePower));
                }
            }
        }

        private static float GetGradePower(ItemGrade grade)
        {
            return grade switch
            {
                ItemGrade.Rare => 0.22f,
                ItemGrade.Epic => 0.42f,
                ItemGrade.Legendary => 0.62f,
                ItemGrade.Mythic => 0.82f,
                ItemGrade.Celestial => 1f,
                _ => 0.06f
            };
        }

        private static Color GetRealmTint(RealmId realmId)
        {
            return realmId switch
            {
                RealmId.Stonehold => new Color(0.92f, 0.58f, 0.20f),
                RealmId.Eldergrove => new Color(0.34f, 1f, 0.48f),
                RealmId.Crownlands => new Color(0.32f, 0.58f, 1f),
                RealmId.Umbral => new Color(0.82f, 0.16f, 1f),
                _ => new Color(0.82f, 0.88f, 0.94f)
            };
        }

        private static GameObject CreateBurstPrimitive(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Color color, float emission)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;
            obj.transform.localRotation = Quaternion.Euler(localEulerAngles);
            Destroy(obj.GetComponent<Collider>());

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", color * Mathf.Clamp(emission, 0.12f, 1.35f));
                }
            }

            return obj;
        }
    }

    internal sealed class BotChampionTierDefeatBurst : MonoBehaviour
    {
        private const float Lifetime = 1.15f;
        private readonly System.Collections.Generic.List<Renderer> _renderers = new System.Collections.Generic.List<Renderer>();
        private readonly System.Collections.Generic.List<Light> _lights = new System.Collections.Generic.List<Light>();
        private ItemGrade _grade = ItemGrade.Rare;
        private Color _accent = Color.white;
        private float _age;

        public void Configure(ItemGrade grade, Color accent)
        {
            _grade = grade;
            _accent = accent;
            _renderers.Clear();
            _lights.Clear();
            _renderers.AddRange(GetComponentsInChildren<Renderer>(true));
            _lights.AddRange(GetComponentsInChildren<Light>(true));
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float normalized = Mathf.Clamp01(_age / Lifetime);
            float gradePower = GetGradePower(_grade);
            float pulse = (Mathf.Sin(Time.time * Mathf.Lerp(8f, 14f, gradePower)) + 1f) * 0.5f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.88f, 1.78f + gradePower * 0.34f, normalized);
            transform.Rotate(Vector3.up, Mathf.Lerp(34f, 92f, gradePower) * Time.deltaTime, Space.Self);

            foreach (var renderer in _renderers)
            {
                if (renderer == null || renderer.material == null)
                {
                    continue;
                }

                Color color = renderer.material.color;
                color.a = Mathf.Lerp(0.88f, 0f, normalized);
                renderer.material.color = Color.Lerp(color, _accent, pulse * 0.18f);
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.SetColor("_EmissionColor", _accent * Mathf.Lerp(1.15f, 0.06f, normalized));
                }
            }

            foreach (var light in _lights)
            {
                if (light != null)
                {
                    light.intensity = Mathf.Lerp(light.intensity, 0f, normalized);
                }
            }

            if (_age >= Lifetime)
            {
                Destroy(gameObject);
            }
        }

        private static float GetGradePower(ItemGrade grade)
        {
            return grade switch
            {
                ItemGrade.Rare => 0.22f,
                ItemGrade.Epic => 0.42f,
                ItemGrade.Legendary => 0.62f,
                ItemGrade.Mythic => 0.82f,
                ItemGrade.Celestial => 1f,
                _ => 0.06f
            };
        }
    }
}
