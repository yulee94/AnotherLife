using System.Collections;
using System.Collections.Generic;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.ChampionMode.Encounter;
using AL.ChampionMode.Presentation;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Input;
using AL.Pvp;
using UnityEngine;

namespace AL.ChampionMode.Skills
{
    public enum SkillLoadoutState
    {
        Loading,
        Ready,
        Unavailable
    }

    [RequireComponent(typeof(ChampionCombat))]
    public class SkillCaster : MonoBehaviour, IChampionEncounterRuntimeHost
    {
        private const int SlotCount = SkillLoadoutCatalog.RequiredSlotCount;
        private const float DeniedFeedbackCooldown = 0.55f;

        private readonly float[] _nextReadyTimes = new float[SlotCount];

        private SkillLoadoutSnapshot _loadout;
        private ChampionEncounterLoadReceipt _productionEncounterLoad;
        private ChampionCombat _combat;
        private ChampionController _controller;
        private ChampionCrowdControl _crowdControl;
        private ChampionActionPresentation _actionPresentation;
        private Coroutine _loadRoutine;
        private Coroutine _castRoutine;
        private int _activeSlot = -1;
        private float _activeCastStartTime;
        private float _activeCastDuration;
        private float _lastDeniedFeedbackTime = -999f;
        private RealmId _realmId = RealmId.None;
        private SkillLoadoutState _loadoutState = SkillLoadoutState.Loading;
        private bool _hasAwakened;
        private bool _isDestroyed;

        public SkillLoadoutState LoadoutState => _loadoutState;
        public bool IsLoadoutReady =>
            _loadoutState == SkillLoadoutState.Ready && _loadout != null;
        public SkillLoadoutSnapshot LoadoutSnapshot => IsLoadoutReady ? _loadout : null;
        public ChampionEncounterLoadReceipt ProductionEncounterLoad => _productionEncounterLoad;
        public bool IsCasting => _castRoutine != null;
        public int ActiveSlot => _activeSlot;
        public string ActiveSkillName =>
            TryGetSkill(_activeSlot, out var skill) ? skill.DisplayName : string.Empty;
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
            _hasAwakened = true;
            _combat = GetComponent<ChampionCombat>();
            _controller = GetComponent<ChampionController>();
            _crowdControl = GetComponent<ChampionCrowdControl>();
            _actionPresentation =
                GetComponent<ChampionActionPresentation>() ??
                gameObject.AddComponent<ChampionActionPresentation>();
            BeginLoadoutLoad();
        }

        private void OnEnable()
        {
            if (_hasAwakened && !IsLoadoutReady && _loadRoutine == null)
            {
                BeginLoadoutLoad();
            }
        }

        private void OnDisable()
        {
            StopLoadoutRoutine();
            StopCastRoutine(false);
            CombatEffectOwnership.Retire(gameObject);
            RuntimeCombatAudio.StopOwned(gameObject);
            if (!IsLoadoutReady && !_isDestroyed)
            {
                _loadoutState = SkillLoadoutState.Loading;
            }
        }

        private void OnDestroy()
        {
            _isDestroyed = true;
            _loadRoutine = null;
            _castRoutine = null;
            ClearActiveCast();
        }

        public bool TryGetLoadoutSnapshot(out SkillLoadoutSnapshot snapshot)
        {
            snapshot = LoadoutSnapshot;
            return snapshot != null;
        }

        public bool TryBind(ChampionEncounterLoadReceipt receipt)
        {
            return TryApplyEncounterLoad(receipt);
        }

        public bool TryApplyEncounterLoad(ChampionEncounterLoadReceipt receipt)
        {
            if (_isDestroyed || !ChampionEncounterRuntimeGateway.ValidReceipt(receipt))
            {
                return false;
            }

            _productionEncounterLoad = receipt;
            return true;
        }

        public bool RetryLoadoutLoad()
        {
            if (_isDestroyed || !isActiveAndEnabled || IsLoadoutReady || _loadRoutine != null)
            {
                return false;
            }

            BeginLoadoutLoad();
            return IsLoadoutReady || _loadRoutine != null;
        }

        public bool TryCastSkill(int slotIndex)
        {
            if (GameplaySuppressed)
            {
                return false;
            }

            _crowdControl ??= GetComponent<ChampionCrowdControl>();
            if (_crowdControl != null && _crowdControl.BlocksSkillCasting)
            {
                return false;
            }

            if (!TryGetSkill(slotIndex, out var skill))
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

            if (_combat != null && _combat.CurrentMana + 0.001f < skill.ManaCost)
            {
                GameDebug.Log($"Not enough mana for {skill.DisplayName}.");
                ShowDeniedFeedback("NO MANA", new Color(0.42f, 0.72f, 1f));
                return false;
            }

            _activeSlot = skill.Slot;
            _castRoutine = StartCoroutine(CastRoutine(skill, _realmId));
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
            if (_castRoutine != null)
            {
                StopCastRoutine(true);
            }

            CombatEffectOwnership.Retire(gameObject);
            RuntimeCombatAudio.StopOwned(gameObject);
        }

        public float GetCooldownRemaining(int slotIndex)
        {
            if (!TryGetSkill(slotIndex, out _))
            {
                return 0f;
            }

            return Mathf.Max(0f, _nextReadyTimes[slotIndex] - Time.time);
        }

        public float GetCooldownDuration(int slotIndex)
        {
            return TryGetSkill(slotIndex, out var skill) ? skill.CooldownSeconds : 0f;
        }

        public float GetManaCost(int slotIndex)
        {
            return TryGetSkill(slotIndex, out var skill) ? skill.ManaCost : 0f;
        }

        public string GetSkillName(int slotIndex)
        {
            if (!IsValidSlot(slotIndex))
            {
                return "Unknown";
            }

            if (TryGetSkill(slotIndex, out var skill))
            {
                return skill.DisplayName;
            }

            return _loadoutState == SkillLoadoutState.Loading
                ? "Loading"
                : "Unavailable";
        }

        public string GetSkillId(int slotIndex)
        {
            return TryGetSkill(slotIndex, out var skill) ? skill.Id : string.Empty;
        }

        public string GetSkillVfxKey(int slotIndex)
        {
            return TryGetSkill(slotIndex, out var skill) ? skill.VfxKey : string.Empty;
        }

        private IEnumerator CastRoutine(SkillLoadoutSlot skill, RealmId realmId)
        {
            GameDebug.Log($"Casting {skill.DisplayName}.");
            SkillCastBinding binding = ResolveBinding(skill);
            _actionPresentation?.Emit(
                ChampionActionKind.Skill,
                ChampionActionPhase.Anticipation,
                skill.Slot,
                skill.Id);
            _actionPresentation?.Emit(
                ChampionActionKind.Skill,
                ChampionActionPhase.Casting,
                skill.Slot,
                skill.Id);
            _activeCastStartTime = Time.time;
            _activeCastDuration = skill.CastTimeSeconds;
            Vector3 forward = transform.forward.sqrMagnitude > 0.01f ? transform.forward.normalized : Vector3.forward;
            Vector3 previewCenter = GetSkillGroundCenter(skill, forward);
            using (CombatEffectOwnership.Begin(gameObject))
            {
                SkillCastEffectRouter.PlayTelegraph(
                    binding,
                    transform.position,
                    previewCenter,
                    forward,
                    realmId,
                    GetSkillPreviewRadius(skill),
                    skill.CastTimeSeconds + 0.18f);
            }

            float remainingCastTime = Mathf.Max(0f, skill.CastTimeSeconds);
            bool channelingEmitted = false;
            while (remainingCastTime > 0f)
            {
                if (GameplaySuppressed)
                {
                    EmitInterrupted(skill);
                    _castRoutine = null;
                    ClearActiveCast();
                    yield break;
                }

                remainingCastTime -= Time.deltaTime;
                if (!channelingEmitted &&
                    _activeCastDuration >= 0.10f &&
                    remainingCastTime <= _activeCastDuration * 0.5f)
                {
                    channelingEmitted = true;
                    _actionPresentation?.Emit(
                        ChampionActionKind.Skill,
                        ChampionActionPhase.Channeling,
                        skill.Slot,
                        skill.Id);
                }

                yield return null;
            }

            if (GameplaySuppressed)
            {
                EmitInterrupted(skill);
                _castRoutine = null;
                ClearActiveCast();
                yield break;
            }

            if (_combat != null && !_combat.TrySpendMana(skill.ManaCost))
            {
                ShowDeniedFeedback("NO MANA", new Color(0.42f, 0.72f, 1f));
                EmitInterrupted(skill);
                _castRoutine = null;
                ClearActiveCast();
                yield break;
            }

            _actionPresentation?.Emit(
                ChampionActionKind.Skill,
                ChampionActionPhase.Commit,
                skill.Slot,
                skill.Id);
            using (CombatEffectOwnership.Begin(gameObject))
            {
                ResolveSkill(skill, binding, realmId);
            }

            _nextReadyTimes[skill.Slot] = Time.time + skill.CooldownSeconds;
            _actionPresentation?.Emit(
                ChampionActionKind.Skill,
                ChampionActionPhase.Recovery,
                skill.Slot,
                skill.Id);
            SkillCastEffectRouter.PlayCleanup(binding, transform.position, realmId);
            _castRoutine = null;
            ClearActiveCast();
        }

        private bool GameplaySuppressed
        {
            get
            {
                if (_controller == null)
                {
                    _controller = GetComponent<ChampionController>();
                }

                return GameInput.GameplaySuppressed ||
                       ChampionHudCameraGate.BlocksGameplay ||
                       (_controller != null && _controller.BlocksGameplayEntry);
            }
        }

        private void ClearActiveCast()
        {
            _activeSlot = -1;
            _activeCastStartTime = 0f;
            _activeCastDuration = 0f;
        }

        private void ResolveSkill(SkillLoadoutSlot skill, SkillCastBinding binding, RealmId realmId)
        {
            Vector3 forward = transform.forward.sqrMagnitude > 0.01f ? transform.forward.normalized : Vector3.forward;
            Vector3 groundCenter = GetSkillGroundCenter(skill, forward);
            Vector3 hitCenter = groundCenter + Vector3.up;
            bool committedHit = false;

            switch (skill.Identity)
            {
                case MvpSkillIdentity.RealmStrike:
                    committedHit = DamageTargets(
                        skill,
                        binding,
                        hitCenter,
                        skill.RangeMeters,
                        skill.Power,
                        realmId,
                        skill.BotDamageMultiplier);
                    RuntimeCombatAudio.PlaySkillCast();
                    break;
                case MvpSkillIdentity.RenewingGuard:
                    _combat?.Heal(skill.Power);
                    if (skill.CleanseSoftControl)
                    {
                        _crowdControl ??= GetComponent<ChampionCrowdControl>();
                        _crowdControl?.CleanseSoftControl(skill.ControlWardSeconds);
                    }

                    SkillCastEffectRouter.PlayImpact(binding, transform.position, realmId);
                    SkillEffectFactory.SpawnFloatingCombatText(
                        transform.position + Vector3.up * 1.85f,
                        "+" + Mathf.CeilToInt(skill.Power),
                        new Color(0.48f, 1f, 0.62f),
                        0.28f,
                        0.95f);
                    RuntimeCombatAudio.PlayHeal();
                    break;
                case MvpSkillIdentity.WarzoneBurst:
                    committedHit = DamageTargets(
                        skill,
                        binding,
                        hitCenter,
                        skill.RangeMeters,
                        skill.Power,
                        realmId,
                        skill.BotDamageMultiplier);
                    RuntimeCombatAudio.PlaySkillCast();
                    break;
                case MvpSkillIdentity.WarmasterBreaker:
                    committedHit = DamageTargets(
                        skill,
                        binding,
                        hitCenter,
                        skill.RangeMeters,
                        skill.Power,
                        realmId,
                        skill.BotDamageMultiplier);
                    RuntimeCombatAudio.PlayHeavySkill();
                    break;
                default:
                    GameDebug.Log($"[SkillCaster] Rejected unresolved skill identity '{skill.Id}'.");
                    return;
            }

            SkillCastEffectRouter.PlayActive(
                binding,
                skill.Identity == MvpSkillIdentity.RenewingGuard ? transform.position : groundCenter,
                forward,
                realmId,
                Mathf.Max(1.15f, skill.RangeMeters));
            if (committedHit)
            {
                _actionPresentation?.Emit(
                    ChampionActionKind.Skill,
                    ChampionActionPhase.Impact,
                    skill.Slot,
                    skill.Id);
            }

            if (SkillCastEffectRouter.AllowsCameraImpulse())
            {
                float shake = skill.Identity == MvpSkillIdentity.WarmasterBreaker
                    ? 0.24f
                    : skill.Identity == MvpSkillIdentity.WarzoneBurst
                        ? 0.18f
                        : skill.Identity == MvpSkillIdentity.RenewingGuard
                            ? 0.06f
                            : 0.12f;
                SkillEffectFactory.ShakeCamera(shake, shake >= 0.18f ? 0.16f : 0.10f);
            }
        }

        private static float GetSkillPreviewRadius(SkillLoadoutSlot skill)
        {
            return skill.Identity == MvpSkillIdentity.RenewingGuard
                ? 1.35f
                : Mathf.Clamp(skill.RangeMeters, 1.15f, 4.5f);
        }

        private Vector3 GetSkillGroundCenter(SkillLoadoutSlot skill, Vector3 forward)
        {
            Vector3 safeForward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            return transform.position + safeForward * Mathf.Max(1.5f, skill.RangeMeters);
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

        private bool DamageTargets(
            SkillLoadoutSlot skill,
            SkillCastBinding binding,
            Vector3 center,
            float radius,
            float power,
            RealmId attackerRealm,
            float botDamageMultiplier)
        {
            Collider[] hitColliders = Physics.OverlapSphere(center, radius);
            int destroyedDummies = 0;
            bool hitAnyTarget = false;
            var damagedBots = new HashSet<int>();
            var damagedBosses = new HashSet<int>();
            var damagedPlayers = new HashSet<int>();

            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider == null)
                {
                    continue;
                }

                if (hitCollider.gameObject.name.StartsWith("Dummy_"))
                {
                    hitAnyTarget = true;
                    SkillCastEffectRouter.PlayImpact(binding, hitCollider.transform.position, attackerRealm);
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
                    ApplyControlAtCommit(boss.transform, skill);
                    SkillCastEffectRouter.PlayImpact(binding, boss.transform.position, attackerRealm);
                    continue;
                }

                var bot = hitCollider.GetComponentInParent<BotChampionAI>();
                if (bot != null && bot.IsAlive && bot.RealmId != attackerRealm && damagedBots.Add(bot.GetInstanceID()))
                {
                    hitAnyTarget = true;
                    float botDamage = power * Mathf.Max(0f, botDamageMultiplier);
                    bot.TakeDamage(botDamage, attackerRealm);
                    ApplyControlAtCommit(bot.transform, skill);
                    SkillCastEffectRouter.PlayImpact(binding, bot.transform.position, attackerRealm);
                    SkillEffectFactory.SpawnFloatingCombatText(bot.transform.position + Vector3.up * 1.85f, Mathf.CeilToInt(botDamage).ToString(), new Color(1f, 0.62f, 0.22f), 0.24f, 0.82f);
                    RuntimeCombatAudio.PlayImpact();
                    continue;
                }

                var playerCombat = hitCollider.GetComponentInParent<ChampionCombat>();
                if (playerCombat != null &&
                    playerCombat != _combat &&
                    damagedPlayers.Add(playerCombat.GetInstanceID()))
                {
                    PvpHarmfulEffectKind harmKind = radius > 0.01f
                        ? PvpHarmfulEffectKind.AreaOfEffect
                        : PvpHarmfulEffectKind.DirectHit;
                    PvpHarmfulEffectApplicationReceipt harm =
                        PvpHarmfulEffectRuntimeGate.ApplyOverlap(harmKind, null);
                    if (!harm.Applied)
                    {
                        continue;
                    }

                    hitAnyTarget = true;
                    playerCombat.TakeDamage(power);
                    if (skill != null && skill.ControlKind != CrowdControlKind.None)
                    {
                        PvpHarmfulEffectApplicationReceipt control =
                            PvpHarmfulEffectRuntimeGate.ApplyOverlap(
                                PvpHarmfulEffectKind.CrowdControl,
                                null);
                        if (control.Applied)
                        {
                            ApplyControlAtCommit(playerCombat.transform, skill);
                        }
                    }

                    SkillCastEffectRouter.PlayImpact(binding, playerCombat.transform.position, attackerRealm);
                    SkillEffectFactory.SpawnFloatingCombatText(
                        playerCombat.transform.position + Vector3.up * 1.85f,
                        Mathf.CeilToInt(power).ToString(),
                        new Color(1f, 0.42f, 0.42f),
                        0.24f,
                        0.82f);
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

            return hitAnyTarget;
        }

        private void ApplyControlAtCommit(Transform target, SkillLoadoutSlot skill)
        {
            if (target == null || skill == null || skill.ControlKind == CrowdControlKind.None)
            {
                return;
            }

            ChampionCrowdControl controls = target.GetComponentInParent<ChampionCrowdControl>();
            controls?.Apply(
                skill.ControlKind,
                skill.ControlDurationSeconds,
                skill.ControlSeverity);
        }

        private static SkillCastBinding ResolveBinding(SkillLoadoutSlot skill)
        {
            if (skill == null ||
                !SkillCastPresentationCatalog.TryLoad(out SkillCastPresentationSnapshot snapshot) ||
                !snapshot.TryGetBinding(skill.Id, out SkillCastBinding binding))
            {
                return null;
            }

            return binding;
        }

        private void EmitInterrupted(SkillLoadoutSlot skill)
        {
            _actionPresentation?.Emit(
                ChampionActionKind.Skill,
                ChampionActionPhase.Interrupted,
                skill != null ? skill.Slot : _activeSlot,
                skill != null ? skill.Id : GetSkillId(_activeSlot));
        }

        private bool TryApplySharedSkillLoadouts()
        {
            if (!SkillLoadoutCatalog.TryLoadSnapshot(out var snapshot))
            {
                return false;
            }

            PublishLoadout(snapshot);
            return true;
        }

        private IEnumerator ApplySharedSkillLoadoutsAsync()
        {
            SkillLoadoutSnapshot loadedSnapshot = null;
            yield return SkillLoadoutCatalog.LoadSnapshotAsync(snapshot =>
            {
                loadedSnapshot = snapshot;
            });

            _loadRoutine = null;
            if (_isDestroyed || !isActiveAndEnabled)
            {
                yield break;
            }

            if (loadedSnapshot != null)
            {
                PublishLoadout(loadedSnapshot);
                GameDebug.Log("[SkillCaster] Published the validated skill loadout snapshot from StreamingAssets.");
            }
            else
            {
                _loadoutState = SkillLoadoutState.Unavailable;
                Debug.LogWarning("[SkillCaster] Playable skill loadout is unavailable. Skill casting remains disabled.");
            }
        }

        private void BeginLoadoutLoad()
        {
            if (_isDestroyed || IsLoadoutReady || _loadRoutine != null)
            {
                return;
            }

            _loadoutState = SkillLoadoutState.Loading;
            if (TryApplySharedSkillLoadouts())
            {
                return;
            }

            if (isActiveAndEnabled)
            {
                _loadRoutine = StartCoroutine(ApplySharedSkillLoadoutsAsync());
            }
        }

        private void PublishLoadout(SkillLoadoutSnapshot snapshot)
        {
            if (_isDestroyed || snapshot == null ||
                snapshot.Count != SkillLoadoutCatalog.RequiredSlotCount)
            {
                return;
            }

            _loadout = snapshot;
            _loadoutState = SkillLoadoutState.Ready;
        }

        private void StopLoadoutRoutine()
        {
            if (_loadRoutine == null)
            {
                return;
            }

            StopCoroutine(_loadRoutine);
            _loadRoutine = null;
        }

        private void StopCastRoutine(bool reportCancellation)
        {
            if (_castRoutine == null)
            {
                return;
            }

            int slot = _activeSlot;
            string actionId = GetSkillId(slot);
            StopCoroutine(_castRoutine);
            _castRoutine = null;
            if (reportCancellation)
            {
                _actionPresentation?.Emit(
                    ChampionActionKind.Skill,
                    ChampionActionPhase.Interrupted,
                    slot,
                    actionId);
                GameDebug.Log("Skill cast cancelled.");
            }

            ClearActiveCast();
        }

        private bool TryGetSkill(int slotIndex, out SkillLoadoutSlot skill)
        {
            if (_loadout == null || !IsValidSlot(slotIndex))
            {
                skill = null;
                return false;
            }

            return _loadout.TryGetSlot(slotIndex, out skill);
        }

        private static bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SlotCount;
        }
    }
}
