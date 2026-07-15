using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.ChampionMode.Customization
{
    public class ChampionGearTierVisualController : MonoBehaviour
    {
        [SerializeField] private RealmId _fallbackRealm = RealmId.Crownlands;
        [SerializeField] private ItemGrade _fallbackGrade = ItemGrade.Common;

        private readonly List<Renderer> _auraRenderers = new List<Renderer>();
        private Transform _auraRoot;
        private ItemGrade _activeGrade = ItemGrade.Common;
        private RealmId _activeRealm = RealmId.Crownlands;
        private Color _primary;
        private Color _secondary;
        private float _pulseOffset;
        private float _nextRefreshTime;

        public void Configure(RealmId fallbackRealm, ItemGrade fallbackGrade = ItemGrade.Common)
        {
            _fallbackRealm = fallbackRealm == RealmId.None ? RealmId.Crownlands : fallbackRealm;
            _fallbackGrade = fallbackGrade;
            EnsureVisuals();
            RefreshNow();
        }

        public void RefreshNow()
        {
            ResolveOwnedGearProfile(out ItemGrade grade, out RealmId realm, out Color primary, out Color secondary);
            _activeGrade = grade;
            _activeRealm = realm == RealmId.None ? _fallbackRealm : realm;
            _primary = primary;
            _secondary = secondary;
            ApplyStaticProfile();
        }

        private void OnEnable()
        {
            EnsureVisuals();
            RefreshNow();
        }

        private void Update()
        {
            if (Time.time >= _nextRefreshTime)
            {
                _nextRefreshTime = Time.time + 1.35f;
                RefreshNow();
            }

            TickPulse();
        }

        private void EnsureVisuals()
        {
            if (_auraRoot != null)
            {
                return;
            }

            _auraRoot = new GameObject("ChampionGearTierVFX").transform;
            _auraRoot.SetParent(transform, false);
            _auraRoot.localPosition = Vector3.zero;
            _auraRoot.localRotation = Quaternion.identity;

            Transform chest = FindOrCreateAnchor("VFX_ChestAnchor", new Vector3(0f, 0.48f, 0.38f));
            Transform leftHand = FindOrCreateAnchor("VFX_Hand_L", new Vector3(-0.55f, -0.10f, 0.20f));
            Transform rightHand = FindOrCreateAnchor("VFX_Hand_R", new Vector3(0.55f, -0.10f, 0.20f));

            CreateAuraPart(chest, "GearAura_ChestHalo", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.55f, 0.014f, 0.55f), new Vector3(90f, 0f, 0f));
            CreateAuraPart(chest, "GearAura_ChestCore", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.035f), new Vector3(0.13f, 0.13f, 0.050f), Vector3.zero);
            CreateAuraPart(leftHand, "GearAura_LeftHandRune", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.18f, 0.010f, 0.18f), new Vector3(90f, 0f, 0f));
            CreateAuraPart(rightHand, "GearAura_RightHandRune", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.18f, 0.010f, 0.18f), new Vector3(90f, 0f, 0f));

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 0.42f, Mathf.Sin(angle) * 0.05f, Mathf.Sin(angle) * 0.18f);
                CreateAuraPart(chest, "GearAura_OrbitShard_" + i, PrimitiveType.Cube, position, new Vector3(0.040f, 0.18f, 0.040f), new Vector3(0f, -angle * Mathf.Rad2Deg, 18f));
            }
        }

        private Transform FindOrCreateAnchor(string name, Vector3 localPosition)
        {
            Transform anchor = transform.Find(name);
            if (anchor != null)
            {
                return anchor;
            }

            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = localPosition;
            return obj.transform;
        }

        private void CreateAuraPart(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localRotation = Quaternion.Euler(localEulerAngles);
            obj.transform.localScale = localScale;

            var collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Standard");
                var material = shader != null ? new Material(shader) : new Material(renderer.material);
                material.EnableKeyword("_EMISSION");
                renderer.material = material;
                _auraRenderers.Add(renderer);
            }
        }

        private void ResolveOwnedGearProfile(out ItemGrade grade, out RealmId realm, out Color primary, out Color secondary)
        {
            grade = _fallbackGrade;
            realm = _fallbackRealm;
            primary = GetRealmColor(realm, 0.54f);
            secondary = Color.Lerp(primary, Color.white, 0.24f);

            try
            {
                var lootService = ServiceLocator.Get<IBossLootService>();
                IEnumerable<OwnedEquipmentState> ownedEquipment = lootService.GetOwnedEquipment();
                if (ownedEquipment == null)
                {
                    return;
                }

                foreach (var equipment in ownedEquipment)
                {
                    if (equipment == null || equipment.Grade < grade)
                    {
                        continue;
                    }

                    grade = equipment.Grade;
                    realm = equipment.VisualRealm == RealmId.None ? _fallbackRealm : equipment.VisualRealm;
                    primary = ResolveEquipmentColor(equipment, true, realm, 0.72f);
                    secondary = ResolveEquipmentColor(equipment, false, realm, 0.48f);
                }
            }
            catch (System.Exception)
            {
                // Gear service is optional in scene smoke contexts.
            }
        }

        private void ApplyStaticProfile()
        {
            EnsureVisuals();
            float gradePower = GetGradePower(_activeGrade);
            bool visible = _activeGrade >= ItemGrade.Rare;
            if (_auraRoot != null)
            {
                _auraRoot.gameObject.SetActive(visible);
            }

            for (int i = 0; i < _auraRenderers.Count; i++)
            {
                var renderer = _auraRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color color = i % 2 == 0 ? _primary : _secondary;
                renderer.material.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.32f, 0.72f, gradePower));
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.SetColor("_EmissionColor", color * Mathf.Lerp(0.18f, 1.18f, gradePower));
                }
            }
        }

        private void TickPulse()
        {
            if (_auraRoot == null || !_auraRoot.gameObject.activeSelf)
            {
                return;
            }

            float gradePower = GetGradePower(_activeGrade);
            _pulseOffset += Time.deltaTime * Mathf.Lerp(0.55f, 1.65f, gradePower);
            float pulse = 0.5f + Mathf.Sin(_pulseOffset * Mathf.PI * 2f) * 0.5f;
            _auraRoot.localScale = Vector3.one * Mathf.Lerp(0.92f + pulse * 0.04f, 1.18f + pulse * 0.14f, gradePower);
            _auraRoot.Rotate(Vector3.up, Mathf.Lerp(18f, 62f, gradePower) * Time.deltaTime, Space.Self);

            for (int i = 0; i < _auraRenderers.Count; i++)
            {
                var renderer = _auraRenderers[i];
                if (renderer == null || !renderer.material.HasProperty("_EmissionColor"))
                {
                    continue;
                }

                Color color = i % 2 == 0 ? _primary : _secondary;
                renderer.material.SetColor("_EmissionColor", color * Mathf.Lerp(0.22f + pulse * 0.08f, 1.20f + pulse * 0.58f, gradePower));
            }
        }

        private static Color ResolveEquipmentColor(OwnedEquipmentState equipment, bool primary, RealmId realm, float alpha)
        {
            Color fallback = primary
                ? Color.Lerp(GetRealmColor(realm, alpha), Color.white, 0.14f)
                : Color.Lerp(GetRealmColor(realm, alpha), new Color(0.10f, 0.08f, 0.12f, alpha), 0.32f);

            Color color = primary
                ? new Color(equipment.PrimaryR, equipment.PrimaryG, equipment.PrimaryB, alpha)
                : new Color(equipment.SecondaryR, equipment.SecondaryG, equipment.SecondaryB, alpha);

            if (Mathf.Approximately(color.r, 0f) && Mathf.Approximately(color.g, 0f) && Mathf.Approximately(color.b, 0f))
            {
                color = fallback;
            }

            color.a = alpha;
            return color;
        }

        private static float GetGradePower(ItemGrade grade)
        {
            return grade switch
            {
                ItemGrade.Rare => 0.24f,
                ItemGrade.Epic => 0.44f,
                ItemGrade.Legendary => 0.66f,
                ItemGrade.Mythic => 0.84f,
                ItemGrade.Celestial => 1f,
                _ => 0.08f
            };
        }

        private static Color GetRealmColor(RealmId realm, float alpha)
        {
            Color color = realm switch
            {
                RealmId.Stonehold => new Color(0.95f, 0.48f, 0.16f, alpha),
                RealmId.Eldergrove => new Color(0.32f, 0.95f, 0.54f, alpha),
                RealmId.Crownlands => new Color(0.28f, 0.52f, 1f, alpha),
                RealmId.Umbral => new Color(0.72f, 0.12f, 0.94f, alpha),
                _ => new Color(0.85f, 0.92f, 1f, alpha)
            };
            color.a = alpha;
            return color;
        }
    }
}
