using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using AL.ChampionMode.Skills;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;

namespace AL.ChampionMode.AI
{
    public class BossDummyAI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _maxHealth = 1200f;
        [SerializeField] private float _attackRange = 5f;
        [SerializeField] private float _attackCooldown = 3f;
        [SerializeField] private float _telegraphDuration = 1.35f;
        [SerializeField] private float _slamDamage = 80f;
        [SerializeField] private float _enrageThreshold = 0.3f;
        [SerializeField] private float _timedEnrageSeconds = 90f;
        [SerializeField] private BossDefinition _bossDefinition;
        [SerializeField] private string _bossId = "boss_dummy";
        [SerializeField] private string _bossName = "Boss Dummy";
        [SerializeField] private int _warzoneCreditReward = 500;
        [SerializeField] private List<EquipmentDefinition> _possibleLoot = new List<EquipmentDefinition>();

        [Header("Break Bar")]
        [SerializeField] private float _breakBarMax = 100f;
        [SerializeField] private float _breakRecoverPerSecond = 4f;
        [SerializeField] private float _brokenDuration = 3f;
        [SerializeField] private float _brokenDamageMultiplier = 1.25f;

        private Transform _player;
        private bool _isAttacking;
        private bool _isDead;
        private bool _isBroken;
        private bool _phase70;
        private bool _phase40;
        private bool _phase15;
        private bool _enraged;
        private bool _isTelegraphing;
        private float _currentHealth;
        private float _currentBreak;
        private float _healthPercent = 1.0f;
        private float _fightStartTime;
        private float _telegraphStartTime;
        private float _activeTelegraphDuration;
        private Vector3 _baseScale;
        private Coroutine _hitReactRoutine;
        private BossVisualFeedback _visualFeedback;
        private string _lootEncounterId;
        private string _lootRewardResultId;

        public event Action<BossLootResult> LootRolled;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public float CurrentBreak => _currentBreak;
        public float MaxBreak => _breakBarMax;
        public string BossName => _bossName;
        public bool IsBroken => _isBroken;
        public bool IsEnraged => _enraged;
        public bool IsDead => _isDead;
        public bool IsTelegraphing => _isTelegraphing;
        public float TelegraphProgress
        {
            get
            {
                if (!_isTelegraphing || _activeTelegraphDuration <= 0.001f)
                {
                    return 0f;
                }

                return Mathf.Clamp01((Time.time - _telegraphStartTime) / _activeTelegraphDuration);
            }
        }

        private void Start()
        {
            ApplyBossDefinition();
            _lootEncounterId = Guid.NewGuid().ToString("N");
            _lootRewardResultId = Guid.NewGuid().ToString("N");
            _currentHealth = _maxHealth;
            _currentBreak = _breakBarMax;
            _fightStartTime = Time.time;
            _baseScale = transform.localScale;
            _visualFeedback = GetComponent<BossVisualFeedback>() ?? gameObject.AddComponent<BossVisualFeedback>();
            _visualFeedback.Bind();
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;

            StartCoroutine(BehaviorLoop());
        }

        private void ApplyBossDefinition()
        {
            if (_bossDefinition == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_bossDefinition.Id))
            {
                _bossId = _bossDefinition.Id;
            }

            if (!string.IsNullOrWhiteSpace(_bossDefinition.BossName))
            {
                _bossName = _bossDefinition.BossName;
            }

            if (_bossDefinition.Health > 0)
            {
                _maxHealth = _bossDefinition.Health;
            }

            if (_bossDefinition.PossibleLoot != null && _bossDefinition.PossibleLoot.Count > 0)
            {
                _possibleLoot = _bossDefinition.PossibleLoot;
            }
        }

        private IEnumerator BehaviorLoop()
        {
            while (true)
            {
                if (_isDead)
                {
                    yield break;
                }

                TickTimedEnrage();
                TickBreakRecovery();

                if (_isBroken)
                {
                    yield return null;
                    continue;
                }

                if (_player != null)
                {
                    float distance = Vector3.Distance(transform.position, _player.position);

                    if (distance <= _attackRange && !_isAttacking)
                    {
                        yield return StartCoroutine(PerformTelegraphedAttack());
                    }
                    else
                    {
                        // Rotate towards player
                        Vector3 dir = (_player.position - transform.position).normalized;
                        dir.y = 0;
                        if (dir != Vector3.zero)
                            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 2f);
                    }
                }
                yield return null;
            }
        }

        private IEnumerator PerformTelegraphedAttack()
        {
            _isAttacking = true;
            Debug.Log("BOSS: Telegraphing Slam Attack...");
            Vector3 impactCenter = _player != null ? Grounded(_player.position) : Grounded(transform.position + transform.forward * 2.5f);
            float impactRadius = _enraged ? _attackRange * 1.12f : _attackRange;
            SkillEffectFactory.SpawnBossSlamTelegraph(impactCenter, transform.position, impactRadius, _telegraphDuration, _enraged);
            RuntimeCombatAudio.PlayWarning();
            StartTelegraphReadout();

            float windup = 0f;
            while (windup < _telegraphDuration)
            {
                if (_isDead || _isBroken)
                {
                    _isAttacking = false;
                    ClearTelegraphReadout();
                    yield break;
                }

                FacePosition(impactCenter, 5.5f);
                windup += Time.deltaTime;
                yield return null;
            }

            if (_isDead || _isBroken)
            {
                _isAttacking = false;
                ClearTelegraphReadout();
                yield break;
            }

            ClearTelegraphReadout();
            Debug.Log("BOSS: SLAM!");
            SkillEffectFactory.SpawnBossSlamImpact(impactCenter, impactRadius, GetCurrentRealmId());
            RuntimeCombatAudio.PlayHeavySkill();

            if (_player != null && DistanceOnGround(_player.position, impactCenter) <= impactRadius)
            {
                var combat = _player.GetComponent<AL.ChampionMode.Control.ChampionCombat>();
                combat?.TakeDamage(_slamDamage);
                SkillEffectFactory.SpawnFloatingCombatText(_player.position + Vector3.up * 1.65f, "-" + Mathf.CeilToInt(_slamDamage), new Color(1f, 0.32f, 0.20f), 0.28f, 0.85f);
                SkillEffectFactory.ShakeCamera(0.24f, 0.16f);
                SkillEffectFactory.RequestHitPause(0.055f, 0.10f);
            }
            else if (_player != null)
            {
                SkillEffectFactory.SpawnFloatingCombatText(_player.position + Vector3.up * 1.65f, "EVADE", new Color(0.46f, 1f, 0.82f), 0.26f, 0.78f);
            }

            yield return new WaitForSeconds(_attackCooldown);
            _isAttacking = false;
        }

        public void UpdateHealth(float current, float max)
        {
            _healthPercent = current / max;
            if (!_phase70 && _healthPercent <= 0.70f)
            {
                _phase70 = true;
                Debug.Log("BOSS: Phase 2. Wider telegraphs.");
                _attackRange += 1f;
            }

            if (!_phase40 && _healthPercent <= 0.40f)
            {
                _phase40 = true;
                Debug.Log("BOSS: Phase 3. Faster attacks.");
                _attackCooldown *= 0.75f;
            }

            if (!_phase15 && _healthPercent <= 0.15f)
            {
                _phase15 = true;
                Debug.Log("BOSS: Final phase.");
                SkillEffectFactory.SpawnCurseMark(transform.position + Vector3.up);
            }

            if (!_enraged && _healthPercent <= _enrageThreshold)
            {
                TriggerEnrage("low health");
            }
        }

        public void TakeDamage(float amount)
        {
            if (_isDead)
            {
                return;
            }

            float finalAmount = _isBroken ? amount * _brokenDamageMultiplier : amount;
            _currentHealth = Mathf.Max(0f, _currentHealth - finalAmount);
            ApplyBreakDamage(amount);
            UpdateHealth(_currentHealth, _maxHealth);
            SkillEffectFactory.SpawnForgeBurst(transform.position + Vector3.up);
            SkillEffectFactory.SpawnFloatingCombatText(transform.position + Vector3.up * 2.8f, Mathf.CeilToInt(finalAmount).ToString(), _isBroken ? new Color(0.40f, 1f, 0.95f) : new Color(1f, 0.62f, 0.22f), _isBroken ? 0.34f : 0.28f, 0.92f);
            SkillEffectFactory.ShakeCamera(_isBroken ? 0.20f : 0.12f, _isBroken ? 0.16f : 0.10f);
            SkillEffectFactory.RequestHitPause(_isBroken ? 0.060f : 0.035f, _isBroken ? 0.08f : 0.14f);
            PlayHitReaction(_isBroken ? 1.08f : 1.04f);
            _visualFeedback?.PulseHit(_isBroken ? 1f : 0.72f);
            RuntimeCombatAudio.PlayImpact();
            Debug.Log($"BOSS: Took {finalAmount} damage. HP {_currentHealth}/{_maxHealth}. Break {_currentBreak}/{_breakBarMax}");

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        private void ApplyBreakDamage(float sourceDamage)
        {
            if (_isBroken || sourceDamage <= 0f)
            {
                return;
            }

            float breakDamage = Mathf.Clamp(sourceDamage * 0.18f, 8f, 30f);
            _currentBreak = Mathf.Max(0f, _currentBreak - breakDamage);
            if (_currentBreak <= 0f)
            {
                StartCoroutine(BreakRoutine());
            }
        }

        private void TickBreakRecovery()
        {
            if (_isBroken || _currentBreak >= _breakBarMax)
            {
                return;
            }

            _currentBreak = Mathf.Min(_breakBarMax, _currentBreak + _breakRecoverPerSecond * Time.deltaTime);
        }

        private IEnumerator BreakRoutine()
        {
            _isBroken = true;
            _isAttacking = false;
            ClearTelegraphReadout();
            _visualFeedback?.SetBroken(true);
            Debug.Log("BOSS: BREAK! Damage window opened.");
            SkillEffectFactory.SpawnBossTelegraph(transform.position, 2.25f, _brokenDuration);
            SkillEffectFactory.SpawnFloatingCombatText(transform.position + Vector3.up * 3.15f, "BREAK", new Color(0.40f, 1f, 0.95f), 0.38f, 1.1f);
            SkillEffectFactory.ShakeCamera(0.28f, 0.20f);
            SkillEffectFactory.RequestHitPause(0.075f, 0.07f);
            RuntimeCombatAudio.PlayBreak();

            yield return new WaitForSeconds(_brokenDuration);

            if (_isDead)
            {
                yield break;
            }

            _currentBreak = _breakBarMax;
            _isBroken = false;
            _visualFeedback?.SetBroken(false);
            Debug.Log("BOSS: Break recovered.");
        }

        private void TickTimedEnrage()
        {
            if (_enraged || Time.time - _fightStartTime < _timedEnrageSeconds)
            {
                return;
            }

            TriggerEnrage("timer");
        }

        private void TriggerEnrage(string reason)
        {
            if (_enraged)
            {
                return;
            }

            _enraged = true;
            Debug.Log($"BOSS: ENRAGED by {reason}!");
            _attackCooldown *= 0.5f;
            _attackRange += 0.5f;
            _visualFeedback?.SetEnraged(true);
            SkillEffectFactory.SpawnCurseMark(transform.position + Vector3.up);
            SkillEffectFactory.SpawnFloatingCombatText(transform.position + Vector3.up * 3.15f, "ENRAGE", new Color(1f, 0.20f, 0.12f), 0.36f, 1.2f);
            SkillEffectFactory.ShakeCamera(0.22f, 0.18f);
            RuntimeCombatAudio.PlayWarning();
        }

        private void Die()
        {
            _isDead = true;
            ClearTelegraphReadout();
            Debug.Log("BOSS: Defeated.");
            _visualFeedback?.PulseDefeated();
            SkillEffectFactory.SpawnFloatingCombatText(transform.position + Vector3.up * 3.15f, "DEFEATED", new Color(0.85f, 1f, 0.62f), 0.38f, 1.25f);
            SkillEffectFactory.ShakeCamera(0.26f, 0.22f);

            try
            {
                BossLootResult lootResult = ServiceLocator.Get<IBossLootService>().RollLoot(CreateLootRequest());
                LootRolled?.Invoke(lootResult);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Boss loot application failed closed. {ex.Message}");
                LootRolled?.Invoke(new BossLootResult
                {
                    ApplicationStatus = BossLootApplicationStatus.CommitUncertain,
                    DiagnosticCode = "AL-BOSS-LOOT-SERVICE-EXCEPTION",
                    EncounterId = _lootEncounterId,
                    RewardResultId = _lootRewardResultId,
                    BossId = _bossId,
                    BossName = _bossName
                });
            }

            Destroy(gameObject);
        }

        private BossLootRequest CreateLootRequest()
        {
            return new BossLootRequest
            {
                EncounterId = _lootEncounterId,
                RewardResultId = _lootRewardResultId,
                BossId = _bossId,
                BossName = _bossName,
                WarzoneCreditReward = _warzoneCreditReward,
                RandomSeed = StableSeed(_lootRewardResultId),
                LootTable = _possibleLoot ?? new List<EquipmentDefinition>()
            };
        }

        private static int StableSeed(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619;
                }

                return (int)hash;
            }
        }

        private void PlayHitReaction(float scaleMultiplier)
        {
            if (_hitReactRoutine != null)
            {
                StopCoroutine(_hitReactRoutine);
            }

            _hitReactRoutine = StartCoroutine(HitReactRoutine(scaleMultiplier));
        }

        private void FacePosition(Vector3 position, float speed)
        {
            Vector3 direction = position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
            {
                return;
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction.normalized), Time.deltaTime * speed);
        }

        private void StartTelegraphReadout()
        {
            _isTelegraphing = true;
            _telegraphStartTime = Time.time;
            _activeTelegraphDuration = Mathf.Max(0.001f, _telegraphDuration);
        }

        private void ClearTelegraphReadout()
        {
            _isTelegraphing = false;
            _telegraphStartTime = 0f;
            _activeTelegraphDuration = 0f;
        }

        private static Vector3 Grounded(Vector3 position)
        {
            return new Vector3(position.x, 0f, position.z);
        }

        private static float DistanceOnGround(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private IEnumerator HitReactRoutine(float scaleMultiplier)
        {
            float elapsed = 0f;
            const float duration = 0.12f;
            Vector3 targetScale = new Vector3(_baseScale.x * scaleMultiplier, _baseScale.y * 0.96f, _baseScale.z * scaleMultiplier);

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(targetScale, _baseScale, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localScale = _baseScale;
            _hitReactRoutine = null;
        }

        private RealmId GetCurrentRealmId()
        {
            try
            {
                var realmId = ServiceLocator.Get<IRealmService>().CurrentRealmId;
                return realmId == RealmId.None ? RealmId.Crownlands : realmId;
            }
            catch (Exception)
            {
                return RealmId.Crownlands;
            }
        }
    }

    internal sealed class BossVisualFeedback : MonoBehaviour
    {
        private struct MaterialState
        {
            public Material Material;
            public Color BaseColor;
            public Color BaseEmission;
            public float PressureWeight;
        }

        private struct LightState
        {
            public Light Light;
            public Color BaseColor;
            public float BaseIntensity;
            public float PressureWeight;
        }

        private BossDummyAI _boss;
        private readonly List<MaterialState> _materials = new List<MaterialState>();
        private readonly List<LightState> _lights = new List<LightState>();
        private readonly List<Transform> _orbitShards = new List<Transform>();
        private Transform _auraRing;
        private Vector3 _auraRingBaseScale = Vector3.one;
        private bool _isBroken;
        private bool _isEnraged;
        private float _hitPulse;
        private float _defeatPulse;
        private float _time;

        public void Bind()
        {
            CollectTargets();
        }

        public void PulseHit(float intensity)
        {
            _hitPulse = Mathf.Max(_hitPulse, Mathf.Clamp01(intensity));
        }

        public void SetBroken(bool isBroken)
        {
            _isBroken = isBroken;
            if (isBroken)
            {
                _hitPulse = Mathf.Max(_hitPulse, 0.88f);
            }
        }

        public void SetEnraged(bool isEnraged)
        {
            _isEnraged = isEnraged;
            if (isEnraged)
            {
                _hitPulse = Mathf.Max(_hitPulse, 0.72f);
            }
        }

        public void PulseDefeated()
        {
            _defeatPulse = 1f;
            _hitPulse = 1f;
        }

        private void OnEnable()
        {
            if (_materials.Count == 0 && _lights.Count == 0)
            {
                CollectTargets();
            }
        }

        private void OnDisable()
        {
            RestoreTargets();
        }

        private void CollectTargets()
        {
            _boss = GetComponent<BossDummyAI>();

            _materials.Clear();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.material == null)
                {
                    continue;
                }

                Material material = renderer.material;
                Color baseColor = material.HasProperty("_Color") ? material.GetColor("_Color") : material.color;
                Color baseEmission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                }

                _materials.Add(new MaterialState
                {
                    Material = material,
                    BaseColor = baseColor,
                    BaseEmission = baseEmission,
                    PressureWeight = GetPressureWeight(renderer.gameObject.name)
                });
            }

            _lights.Clear();
            foreach (var light in GetComponentsInChildren<Light>(true))
            {
                if (light == null)
                {
                    continue;
                }

                _lights.Add(new LightState
                {
                    Light = light,
                    BaseColor = light.color,
                    BaseIntensity = light.intensity,
                    PressureWeight = GetPressureWeight(light.gameObject.name)
                });
            }

            _orbitShards.Clear();
            _auraRing = null;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name.Contains("Boss_OrbitShard"))
                {
                    _orbitShards.Add(child);
                }

                if (child != null && child.name.Contains("Boss_AuraRing"))
                {
                    _auraRing = child;
                    _auraRingBaseScale = child.localScale;
                }
            }
        }

        private void Update()
        {
            _time += Time.deltaTime;
            _hitPulse = Mathf.MoveTowards(_hitPulse, 0f, Time.deltaTime * 3.8f);
            _defeatPulse = Mathf.MoveTowards(_defeatPulse, 0f, Time.deltaTime * 1.6f);

            if (_boss == null)
            {
                _boss = GetComponent<BossDummyAI>();
            }

            bool isBroken = _boss != null ? _boss.IsBroken : _isBroken;
            bool isEnraged = _boss != null ? _boss.IsEnraged : _isEnraged;
            bool isTelegraphing = _boss != null && _boss.IsTelegraphing;
            float telegraphProgress = _boss != null ? _boss.TelegraphProgress : 0f;
            float healthRatio = _boss != null && _boss.MaxHealth > 0f ? Mathf.Clamp01(_boss.CurrentHealth / _boss.MaxHealth) : 1f;
            float healthPressure = 1f - healthRatio;

            Color pressureColor = Color.Lerp(new Color(1f, 0.46f, 0.16f), new Color(1f, 0.12f, 0.035f), healthPressure);
            Color stateColor = isBroken
                ? new Color(0.34f, 1f, 0.95f)
                : isTelegraphing
                    ? new Color(1f, 0.06f, 0.025f)
                    : isEnraged
                        ? new Color(1f, 0.22f, 0.06f)
                        : pressureColor;
            Color hitColor = isBroken ? new Color(0.60f, 1f, 0.98f) : new Color(1f, 0.80f, 0.46f);
            Color defeatColor = new Color(0.88f, 1f, 0.62f);
            float urgency = isBroken
                ? 0.56f
                : isTelegraphing
                    ? Mathf.Lerp(0.68f, 1f, telegraphProgress)
                    : isEnraged
                        ? 0.78f
                        : Mathf.Lerp(0.18f, 0.54f, healthPressure);
            float rhythm = Mathf.Lerp(2.2f, 9.6f, urgency);
            float pulse = (Mathf.Sin(_time * rhythm) + 1f) * 0.5f;
            float statePulse = Mathf.Clamp01(0.16f + urgency * 0.38f + pulse * (0.08f + urgency * 0.22f));
            if (isTelegraphing)
            {
                statePulse = Mathf.Clamp01(statePulse + telegraphProgress * 0.24f);
            }

            for (int i = 0; i < _materials.Count; i++)
            {
                MaterialState state = _materials[i];
                if (state.Material == null)
                {
                    continue;
                }

                float pressureWeight = Mathf.Clamp01(state.PressureWeight);
                float stateBlend = Mathf.Clamp01(statePulse * Mathf.Lerp(0.10f, 0.38f, pressureWeight) + healthPressure * 0.08f * pressureWeight);
                Color targetColor = Color.Lerp(state.BaseColor, stateColor, stateBlend);
                targetColor = Color.Lerp(targetColor, hitColor, _hitPulse * 0.48f);
                targetColor = Color.Lerp(targetColor, defeatColor, _defeatPulse * 0.62f);
                ApplyColor(state.Material, targetColor);

                if (state.Material.HasProperty("_EmissionColor"))
                {
                    float pressureEmission = (0.24f + statePulse * 0.86f) * Mathf.Lerp(0.32f, isTelegraphing ? 2.15f : isBroken ? 1.82f : isEnraged ? 1.52f : 1.04f, pressureWeight);
                    Color emission = Color.Lerp(state.BaseEmission, stateColor * pressureEmission, Mathf.Clamp01(pressureWeight));
                    emission = Color.Lerp(emission, hitColor, _hitPulse * 1.05f);
                    emission = Color.Lerp(emission, defeatColor, _defeatPulse);
                    state.Material.SetColor("_EmissionColor", emission);
                }
            }

            for (int i = 0; i < _lights.Count; i++)
            {
                LightState state = _lights[i];
                if (state.Light == null)
                {
                    continue;
                }

                float lightWeight = Mathf.Clamp01(state.PressureWeight);
                float pulseBoost = 1f + urgency * (0.12f + pulse * 0.62f) * lightWeight + _hitPulse * 0.55f + _defeatPulse * 0.82f;
                state.Light.color = Color.Lerp(state.BaseColor, _defeatPulse > 0.01f ? defeatColor : stateColor, Mathf.Clamp01(statePulse + _hitPulse + _defeatPulse));
                state.Light.intensity = Mathf.Max(0f, state.BaseIntensity * pulseBoost);
            }

            if (_auraRing != null)
            {
                float ringScale = 1f + urgency * (0.04f + pulse * 0.14f);
                if (isTelegraphing)
                {
                    ringScale += telegraphProgress * 0.18f;
                }

                _auraRing.localScale = _auraRingBaseScale * ringScale;
            }

            float orbitSpeed = isTelegraphing
                ? Mathf.Lerp(76f, 174f, telegraphProgress)
                : isBroken
                    ? 78f
                    : isEnraged
                        ? 126f
                        : Mathf.Lerp(34f, 66f, healthPressure);
            for (int i = 0; i < _orbitShards.Count; i++)
            {
                Transform shard = _orbitShards[i];
                if (shard == null)
                {
                    continue;
                }

                shard.Rotate(Vector3.up, orbitSpeed * Time.deltaTime, Space.World);
            }
        }

        private void RestoreTargets()
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                MaterialState state = _materials[i];
                if (state.Material == null)
                {
                    continue;
                }

                ApplyColor(state.Material, state.BaseColor);
                if (state.Material.HasProperty("_EmissionColor"))
                {
                    state.Material.SetColor("_EmissionColor", state.BaseEmission);
                }
            }

            for (int i = 0; i < _lights.Count; i++)
            {
                LightState state = _lights[i];
                if (state.Light == null)
                {
                    continue;
                }

                state.Light.color = state.BaseColor;
                state.Light.intensity = state.BaseIntensity;
            }

            if (_auraRing != null)
            {
                _auraRing.localScale = _auraRingBaseScale;
            }
        }

        private static void ApplyColor(Material material, Color color)
        {
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.color = color;
        }

        private static float GetPressureWeight(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return 0.18f;
            }

            string name = objectName.ToLowerInvariant();
            if (name.Contains("auraring") || name.Contains("core") || name.Contains("eye") || name.Contains("orbitshard") || name.Contains("backshard"))
            {
                return 1f;
            }

            if (name.Contains("crown") || name.Contains("horn") || name.Contains("pauldron") || name.Contains("claw") || name.Contains("light"))
            {
                return 0.68f;
            }

            if (name.Contains("torso") || name.Contains("rib") || name.Contains("face") || name.Contains("shoulder") || name.Contains("blade"))
            {
                return 0.42f;
            }

            return 0.18f;
        }
    }
}
