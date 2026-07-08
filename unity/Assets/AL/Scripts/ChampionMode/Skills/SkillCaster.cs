using System.Collections;
using System.Collections.Generic;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.Core;
using AL.Core.Interfaces;
using UnityEngine;

namespace AL.ChampionMode.Skills
{
    [RequireComponent(typeof(ChampionCombat))]
    public class SkillCaster : MonoBehaviour
    {
        private const int SlotCount = 4;

        private readonly string[] _skillNames =
        {
            "Realm Strike",
            "Renewing Guard",
            "Warzone Burst",
            "Warmaster Breaker"
        };

        private readonly string[] _skillIds =
        {
            "realm_strike",
            "renewing_guard",
            "warzone_burst",
            "warmaster_breaker"
        };

        private readonly string[] _vfxKeys =
        {
            "realm_slash",
            "renewing_guard",
            "warzone_shockwave",
            "warmaster_breaker"
        };

        private readonly float[] _cooldowns = { 4f, 8f, 10f, 14f };
        private readonly float[] _manaCosts = { 20f, 30f, 45f, 60f };
        private readonly float[] _castTimes = { 0.05f, 0.35f, 0.45f, 0.65f };
        private readonly float[] _ranges = { 2.6f, 0f, 4.2f, 3.4f };
        private readonly float[] _powers = { 150f, 180f, 115f, 260f };
        private readonly float[] _botDamageMultipliers = { 0.72f, 0f, 0.72f, 0.72f };
        private readonly float[] _nextReadyTimes = new float[SlotCount];

        private ChampionCombat _combat;
        private ChampionController _controller;
        private Coroutine _castRoutine;
        private int _activeSlot = -1;

        public bool IsCasting => _castRoutine != null;

        private void Awake()
        {
            _combat = GetComponent<ChampionCombat>();
            _controller = GetComponent<ChampionController>();
            if (!TryApplySharedSkillLoadouts())
            {
                StartCoroutine(ApplySharedSkillLoadoutsAsync());
            }
        }

        public bool TryCastSkill(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || IsCasting || Time.time < _nextReadyTimes[slotIndex])
            {
                return false;
            }

            if (_combat != null && !_combat.TrySpendMana(_manaCosts[slotIndex]))
            {
                Debug.Log($"Not enough mana for {_skillNames[slotIndex]}.");
                return false;
            }

            _activeSlot = slotIndex;
            _castRoutine = StartCoroutine(CastRoutine(slotIndex));
            return true;
        }

        public void CancelCurrentSkill()
        {
            if (_castRoutine == null)
            {
                return;
            }

            StopCoroutine(_castRoutine);
            _castRoutine = null;
            _activeSlot = -1;
            Debug.Log("Skill cast cancelled.");
        }

        public float GetCooldownRemaining(int slotIndex)
        {
            if (!IsValidSlot(slotIndex))
            {
                return 0f;
            }

            return Mathf.Max(0f, _nextReadyTimes[slotIndex] - Time.time);
        }

        public float GetCooldownDuration(int slotIndex)
        {
            return IsValidSlot(slotIndex) ? _cooldowns[slotIndex] : 0f;
        }

        public float GetManaCost(int slotIndex)
        {
            return IsValidSlot(slotIndex) ? _manaCosts[slotIndex] : 0f;
        }

        public string GetSkillName(int slotIndex)
        {
            return IsValidSlot(slotIndex) ? _skillNames[slotIndex] : "Unknown";
        }

        public string GetSkillId(int slotIndex)
        {
            return IsValidSlot(slotIndex) ? _skillIds[slotIndex] : string.Empty;
        }

        public string GetSkillVfxKey(int slotIndex)
        {
            return IsValidSlot(slotIndex) ? _vfxKeys[slotIndex] : string.Empty;
        }

        private IEnumerator CastRoutine(int slotIndex)
        {
            Debug.Log($"Casting {_skillNames[slotIndex]}.");
            var realmId = GetCurrentRealmId();
            SkillEffectFactory.SpawnSkillCastRing(transform.position, realmId, GetSkillPreviewRadius(slotIndex), _castTimes[slotIndex] + 0.15f);
            yield return new WaitForSeconds(_castTimes[slotIndex]);

            ResolveSkill(slotIndex);
            _nextReadyTimes[slotIndex] = Time.time + _cooldowns[slotIndex];
            _castRoutine = null;
            _activeSlot = -1;
        }

        private void ResolveSkill(int slotIndex)
        {
            var realmId = GetCurrentRealmId();
            Vector3 forward = transform.forward.sqrMagnitude > 0.01f ? transform.forward.normalized : Vector3.forward;
            Vector3 groundCenter = transform.position + forward * Mathf.Max(1.5f, _ranges[slotIndex]);
            Vector3 hitCenter = groundCenter + Vector3.up;

            switch (slotIndex)
            {
                case 1:
                    _combat?.Heal(_powers[slotIndex]);
                    SkillEffectFactory.SpawnRenewingGuard(transform.position, realmId);
                    break;
                case 2:
                    DamageTargets(hitCenter, _ranges[slotIndex], _powers[slotIndex], realmId, _botDamageMultipliers[slotIndex]);
                    SkillEffectFactory.SpawnWarzoneShockwave(groundCenter, realmId, _ranges[slotIndex]);
                    break;
                case 3:
                    DamageTargets(hitCenter, _ranges[slotIndex], _powers[slotIndex], realmId, _botDamageMultipliers[slotIndex]);
                    SkillEffectFactory.SpawnWarmasterBreaker(groundCenter, realmId, _ranges[slotIndex]);
                    break;
                default:
                    DamageTargets(hitCenter, _ranges[slotIndex], _powers[slotIndex], realmId, _botDamageMultipliers[slotIndex]);
                    SkillEffectFactory.SpawnRealmSlash(groundCenter, forward, realmId);
                    break;
            }
        }

        private float GetSkillPreviewRadius(int slotIndex)
        {
            return slotIndex == 1 ? 1.35f : Mathf.Clamp(_ranges[slotIndex], 1.15f, 4.5f);
        }

        private void DamageTargets(Vector3 center, float radius, float power, RealmId attackerRealm, float botDamageMultiplier)
        {
            Collider[] hitColliders = Physics.OverlapSphere(center, radius);
            int destroyedDummies = 0;
            var damagedBots = new HashSet<int>();
            var damagedBosses = new HashSet<int>();

            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider == null)
                {
                    continue;
                }

                if (hitCollider.gameObject.name.StartsWith("Dummy_"))
                {
                    Object.Destroy(hitCollider.gameObject);
                    destroyedDummies++;
                    continue;
                }

                var boss = hitCollider.GetComponentInParent<BossDummyAI>();
                if (boss != null && damagedBosses.Add(boss.GetInstanceID()))
                {
                    boss.TakeDamage(power);
                    continue;
                }

                var bot = hitCollider.GetComponentInParent<BotChampionAI>();
                if (bot != null && bot.IsAlive && bot.RealmId != attackerRealm && damagedBots.Add(bot.GetInstanceID()))
                {
                    bot.TakeDamage(power * Mathf.Max(0f, botDamageMultiplier), attackerRealm);
                }
            }

            if (destroyedDummies > 0)
            {
                _controller ??= GetComponent<ChampionController>();
                _controller?.CheckVictory(destroyedDummies);
            }
        }

        private bool TryApplySharedSkillLoadouts()
        {
            if (!SkillLoadoutCatalog.TryLoad(out var loadouts))
            {
                return false;
            }

            ApplySkillLoadouts(loadouts);
            return true;
        }

        private IEnumerator ApplySharedSkillLoadoutsAsync()
        {
            bool applied = false;
            yield return SkillLoadoutCatalog.LoadAsync(loadouts =>
            {
                if (loadouts == null || loadouts.Length == 0)
                {
                    return;
                }

                ApplySkillLoadouts(loadouts);
                applied = true;
            });

            if (applied)
            {
                Debug.Log("[SkillCaster] Applied shared skill loadouts from StreamingAssets.");
            }
        }

        private void ApplySkillLoadouts(SkillLoadoutData[] loadouts)
        {
            if (loadouts == null)
            {
                return;
            }

            foreach (var loadout in loadouts)
            {
                if (loadout == null || !IsValidSlot(loadout.slot))
                {
                    continue;
                }

                int slot = loadout.slot;
                if (!string.IsNullOrWhiteSpace(loadout.id))
                {
                    _skillIds[slot] = loadout.id;
                }

                if (!string.IsNullOrWhiteSpace(loadout.displayName))
                {
                    _skillNames[slot] = loadout.displayName;
                }

                if (!string.IsNullOrWhiteSpace(loadout.vfxKey))
                {
                    _vfxKeys[slot] = loadout.vfxKey;
                }

                _cooldowns[slot] = UseCatalogValue(loadout.cooldownSeconds, _cooldowns[slot], 0f);
                _manaCosts[slot] = UseCatalogValue(loadout.manaCost, _manaCosts[slot], 0f);
                _castTimes[slot] = UseCatalogValue(loadout.castTimeSeconds, _castTimes[slot], 0f);
                _ranges[slot] = UseCatalogValue(loadout.rangeMeters, _ranges[slot], 0f);
                _powers[slot] = UseCatalogValue(loadout.power, _powers[slot], 0f);
                _botDamageMultipliers[slot] = UseCatalogValue(loadout.botDamageMultiplier, _botDamageMultipliers[slot], 0f);
            }
        }

        private static float UseCatalogValue(float value, float fallback, float minimum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Mathf.Max(minimum, value);
        }

        private RealmId GetCurrentRealmId()
        {
            try
            {
                var realmId = ServiceLocator.Get<IRealmService>().CurrentRealmId;
                return realmId == RealmId.None ? RealmId.Crownlands : realmId;
            }
            catch (System.Exception)
            {
                return RealmId.Crownlands;
            }
        }

        private static bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SlotCount;
        }
    }
}
