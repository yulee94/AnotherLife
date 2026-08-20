using System.Collections;
using System.Collections.Generic;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.Core;
using UnityEngine;

namespace AL.ChampionMode.Skills
{
    [RequireComponent(typeof(ChampionCombat))]
    public class SkillCaster : MonoBehaviour
    {
        private const int SlotCount = 4;
        private const float DeniedFeedbackCooldown = 0.55f;

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
        private float _activeCastStartTime;
        private float _activeCastDuration;
        private float _lastDeniedFeedbackTime = -999f;
        private RealmId _realmId = RealmId.None;

        public bool IsCasting => _castRoutine != null;
        public int ActiveSlot => _activeSlot;
        public string ActiveSkillName => IsValidSlot(_activeSlot) ? _skillNames[_activeSlot] : string.Empty;
        public float ActiveCastProgress
        {
            get
            {
                if (!IsCasting)
                {
                    return 0f;
                }

                if (_activeCastDuration <= 0.001f)
                {
                    return 1f;
                }

                return Mathf.Clamp01((Time.time - _activeCastStartTime) / _activeCastDuration);
            }
        }

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
            if (!IsValidSlot(slotIndex))
            {
                return false;
            }

            if (_realmId == RealmId.None)
            {
                return false;
            }

            if (IsCasting)
            {
                ShowDeniedFeedback("CASTING", new Color(0.80f, 0.86f, 1f));
                return false;
            }

            float cooldownRemaining = GetCooldownRemaining(slotIndex);
            if (cooldownRemaining > 0.05f)
            {
                ShowDeniedFeedback(Mathf.CeilToInt(cooldownRemaining) + "s", new Color(1f, 0.74f, 0.32f));
                return false;
            }

            if (_combat != null && !_combat.TrySpendMana(_manaCosts[slotIndex]))
            {
                GameDebug.Log($"Not enough mana for {_skillNames[slotIndex]}.");
                ShowDeniedFeedback("NO MANA", new Color(0.42f, 0.72f, 1f));
                return false;
            }

            _activeSlot = slotIndex;
            _castRoutine = StartCoroutine(CastRoutine(slotIndex, _realmId));
            return true;
        }

        public void ConfigureRealmContext(RealmId realmId)
        {
            RealmId normalized = ChampionRealmContext.Normalize(realmId);
            if (_realmId != RealmId.None && normalized != _realmId)
            {
                return;
            }

            _realmId = normalized;
        }

        public void CancelCurrentSkill()
        {
            if (_castRoutine == null)
            {
                return;
            }

            StopCoroutine(_castRoutine);
            _castRoutine = null;
            ClearActiveCast();
            GameDebug.Log("Skill cast cancelled.");
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

        private IEnumerator CastRoutine(int slotIndex, RealmId realmId)
        {
            GameDebug.Log($"Casting {_skillNames[slotIndex]}.");
            _activeCastStartTime = Time.time;
            _activeCastDuration = Mathf.Max(0f, _castTimes[slotIndex]);
            Vector3 forward = transform.forward.sqrMagnitude > 0.01f ? transform.forward.normalized : Vector3.forward;
            Vector3 previewCenter = GetSkillGroundCenter(slotIndex, forward);
            SkillEffectFactory.SpawnSkillCastRing(transform.position, realmId, GetSkillPreviewRadius(slotIndex), _castTimes[slotIndex] + 0.15f);
            if (slotIndex != 1)
            {
                SkillEffectFactory.SpawnSkillTargetPreview(transform.position, previewCenter, forward, realmId, _ranges[slotIndex], _castTimes[slotIndex] + 0.18f);
            }

            yield return new WaitForSeconds(_castTimes[slotIndex]);

            ResolveSkill(slotIndex, realmId);
            _nextReadyTimes[slotIndex] = Time.time + _cooldowns[slotIndex];
            _castRoutine = null;
            ClearActiveCast();
        }

        private void ClearActiveCast()
        {
            _activeSlot = -1;
            _activeCastStartTime = 0f;
            _activeCastDuration = 0f;
        }

        private void ResolveSkill(int slotIndex, RealmId realmId)
        {
            Vector3 forward = transform.forward.sqrMagnitude > 0.01f ? transform.forward.normalized : Vector3.forward;
            Vector3 groundCenter = GetSkillGroundCenter(slotIndex, forward);
            Vector3 hitCenter = groundCenter + Vector3.up;

            switch (slotIndex)
            {
                case 1:
                    _combat?.Heal(_powers[slotIndex]);
                    SkillEffectFactory.SpawnRenewingGuard(transform.position, realmId);
                    SkillEffectFactory.SpawnFloatingCombatText(transform.position + Vector3.up * 1.85f, "+" + Mathf.CeilToInt(_powers[slotIndex]), new Color(0.48f, 1f, 0.62f), 0.28f, 0.95f);
                    SkillEffectFactory.ShakeCamera(0.06f, 0.08f);
                    RuntimeCombatAudio.PlayHeal();
                    break;
                case 2:
                    DamageTargets(hitCenter, _ranges[slotIndex], _powers[slotIndex], realmId, _botDamageMultipliers[slotIndex]);
                    SkillEffectFactory.SpawnWarzoneShockwave(groundCenter, realmId, _ranges[slotIndex]);
                    SkillEffectFactory.ShakeCamera(0.18f, 0.14f);
                    RuntimeCombatAudio.PlaySkillCast();
                    break;
                case 3:
                    DamageTargets(hitCenter, _ranges[slotIndex], _powers[slotIndex], realmId, _botDamageMultipliers[slotIndex]);
                    SkillEffectFactory.SpawnWarmasterBreaker(groundCenter, realmId, _ranges[slotIndex]);
                    SkillEffectFactory.ShakeCamera(0.24f, 0.18f);
                    RuntimeCombatAudio.PlayHeavySkill();
                    break;
                default:
                    DamageTargets(hitCenter, _ranges[slotIndex], _powers[slotIndex], realmId, _botDamageMultipliers[slotIndex]);
                    SkillEffectFactory.SpawnRealmSlash(groundCenter, forward, realmId);
                    SkillEffectFactory.ShakeCamera(0.12f, 0.10f);
                    RuntimeCombatAudio.PlaySkillCast();
                    break;
            }
        }

        private float GetSkillPreviewRadius(int slotIndex)
        {
            return slotIndex == 1 ? 1.35f : Mathf.Clamp(_ranges[slotIndex], 1.15f, 4.5f);
        }

        private Vector3 GetSkillGroundCenter(int slotIndex, Vector3 forward)
        {
            Vector3 safeForward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            return transform.position + safeForward * Mathf.Max(1.5f, _ranges[slotIndex]);
        }

        private void ShowDeniedFeedback(string message, Color color)
        {
            if (Time.time - _lastDeniedFeedbackTime < DeniedFeedbackCooldown)
            {
                return;
            }

            _lastDeniedFeedbackTime = Time.time;
            SkillEffectFactory.SpawnFloatingCombatText(transform.position + Vector3.up * 1.95f, message, color, 0.22f, 0.72f);
            RuntimeCombatAudio.PlayWarning();
        }

        private void DamageTargets(Vector3 center, float radius, float power, RealmId attackerRealm, float botDamageMultiplier)
        {
            Collider[] hitColliders = Physics.OverlapSphere(center, radius);
            int destroyedDummies = 0;
            bool hitAnyTarget = false;
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
                    hitAnyTarget = true;
                    SkillEffectFactory.SpawnFloatingCombatText(hitCollider.transform.position + Vector3.up * 1.45f, "KO", new Color(1f, 0.78f, 0.22f), 0.26f, 0.8f);
                    RuntimeCombatAudio.PlayImpact();
                    Object.Destroy(hitCollider.gameObject);
                    destroyedDummies++;
                    continue;
                }

                var boss = hitCollider.GetComponentInParent<BossDummyAI>();
                if (boss != null && damagedBosses.Add(boss.GetInstanceID()))
                {
                    hitAnyTarget = true;
                    boss.TakeDamage(power);
                    continue;
                }

                var bot = hitCollider.GetComponentInParent<BotChampionAI>();
                if (bot != null && bot.IsAlive && bot.RealmId != attackerRealm && damagedBots.Add(bot.GetInstanceID()))
                {
                    hitAnyTarget = true;
                    float botDamage = power * Mathf.Max(0f, botDamageMultiplier);
                    bot.TakeDamage(botDamage, attackerRealm);
                    SkillEffectFactory.SpawnFloatingCombatText(bot.transform.position + Vector3.up * 1.85f, Mathf.CeilToInt(botDamage).ToString(), new Color(1f, 0.62f, 0.22f), 0.24f, 0.82f);
                    RuntimeCombatAudio.PlayImpact();
                }
            }

            if (destroyedDummies > 0)
            {
                _controller ??= GetComponent<ChampionController>();
                _controller?.CheckVictory(destroyedDummies);
            }

            if (hitAnyTarget)
            {
                bool heavyImpact = power >= 200f;
                SkillEffectFactory.RequestHitPause(heavyImpact ? 0.060f : 0.040f, heavyImpact ? 0.08f : 0.12f);
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
                GameDebug.Log("[SkillCaster] Applied shared skill loadouts from StreamingAssets.");
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

        private static bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SlotCount;
        }
    }
}
