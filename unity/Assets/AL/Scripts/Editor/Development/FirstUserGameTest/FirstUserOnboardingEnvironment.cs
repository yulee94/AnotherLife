#if !UNITY_EDITOR
#error The isolated first-user onboarding environment seam is Editor-only.
#endif

using System;
using System.Collections.Generic;
using AL.ChampionMode.Control;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace AL.Editor.Development.FirstUserGameTest
{
    public static class FirstUserOnboardingEnvironmentBudget
    {
        public const float RoomWidthMeters = 8f;
        public const float RoomLengthMeters = 12f;
        public const float GridMeters = 0.5f;
        public const float CellMeters = 2f;
        public const float BayMeters = 4f;
        public const float VerticalTierMeters = 1f;
        public const int MaximumVisibleTriangles = 12000;
        public const int MaximumRenderers = 35;
        public const int MaximumSharedMaterials = 3;
        public const int MaximumShadowedDirectionalLights = 1;
        public const int MaximumNonShadowedLocalLights = 2;
        public const int MaximumAmbientParticles = 48;
        public const int MaximumEnemyHitColliders = 16;
        public const int MaximumEnemyHitPoints = 100;
        public const int MaximumEncounterResetSequence = 1024;
        public const int AuthoringTexelsPerMeter = 256;
        public const int LowTierEffectiveTexelsPerMeter = 128;
    }

    public readonly struct FirstUserOnboardingEnvironmentRequest
    {
        public FirstUserOnboardingEnvironmentRequest(
            string sessionId,
            int generation,
            Scene scene,
            bool allowUnitTestDouble = false,
            IFirstUserOnboardingAssetInventoryVerifier assetInventoryVerifier = null)
        {
            SessionId = sessionId ?? string.Empty;
            Generation = generation;
            Scene = scene;
            AllowUnitTestDouble = allowUnitTestDouble;
            AssetInventoryVerifier = assetInventoryVerifier;
        }

        public string SessionId { get; }
        public int Generation { get; }
        public Scene Scene { get; }
        public bool AllowUnitTestDouble { get; }
        public IFirstUserOnboardingAssetInventoryVerifier AssetInventoryVerifier { get; }
    }

    public enum FirstUserOnboardingEnvironmentSourceKind
    {
        Invalid = 0,
        AuthoredModule = 1,
        UnitTestDouble = 2
    }

    public enum FirstUserOnboardingEnemyCandidateKind
    {
        Invalid = 0,
        Normal = 1,
        Elite = 2,
        Boss = 3
    }

    public enum FirstUserOnboardingEncounterMode
    {
        Invalid = 0,
        BoundedMechanicsEncounter = 1
    }

    public enum FirstUserOnboardingEncounterResult
    {
        Invalid = 0,
        HitConfirmed = 1,
        Defeated = 2
    }

    public enum FirstUserOnboardingEncounterPresentationState
    {
        Invalid = 0,
        Idle = 1,
        HitReaction = 2,
        Defeated = 3
    }

    public enum FirstUserOnboardingKingdomStructureMode
    {
        Invalid = 0,
        LockedPreviewOnly = 1
    }

    public enum FirstUserOnboardingAssetRole
    {
        Invalid = 0,
        EnvironmentModule = 1,
        ModularChampion = 2,
        SelectedBasicArmor = 3,
        SelectedBasicWeapon = 4,
        CommonEnemy = 5,
        KingdomBaseStructure = 6,
        FloorMaterial = 7,
        WallMaterial = 8,
        TrimMaterial = 9
    }

    /// <summary>
    /// Separate source-of-truth verifier supplied by the asset-integration lane. The environment
    /// factory cannot establish membership merely by returning a caller-chosen ID.
    /// </summary>
    public interface IFirstUserOnboardingAssetInventoryVerifier
    {
        string InventoryFingerprint { get; }

        bool TryVerifyExactAsset(
            FirstUserOnboardingAssetRole role,
            string assetId,
            UnityEngine.Object sourceAsset,
            UnityEngine.Object runtimeInstance,
            out string diagnostic);

        bool TryVerifyModularKit(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic);

        bool TryVerifyChampionRigAndLoadout(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic);

        bool TryVerifyMechanicsEncounterSlot(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic);

        bool TryVerifyLockedKingdomStructureSlot(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic);

        bool TryVerifyCharacterControllerSafeTraversal(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic);

        bool TryVerifyRuntimeComponentInventory(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic);

        bool TryVerifyBuiltInPbrMaterial(
            FirstUserOnboardingAssetRole role,
            Material material,
            out string diagnostic);
    }

    public interface IFirstUserOnboardingEnvironmentFactory
    {
        bool TryCreate(
            FirstUserOnboardingEnvironmentRequest request,
            out IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic);
    }

    public readonly struct FirstUserOnboardingAttackRequest
    {
        public FirstUserOnboardingAttackRequest(
            string sessionId,
            int generation,
            int attackSequence,
            int frame,
            string enemyAssetId,
            Vector3 attackCenter,
            float attackRadius)
        {
            SessionId = sessionId ?? string.Empty;
            Generation = generation;
            AttackSequence = attackSequence;
            Frame = frame;
            EnemyAssetId = enemyAssetId ?? string.Empty;
            AttackCenter = attackCenter;
            AttackRadius = attackRadius;
        }

        public string SessionId { get; }
        public int Generation { get; }
        public int AttackSequence { get; }
        public int Frame { get; }
        public string EnemyAssetId { get; }
        public Vector3 AttackCenter { get; }
        public float AttackRadius { get; }
    }

    public readonly struct FirstUserOnboardingAttackReceipt
    {
        public FirstUserOnboardingAttackReceipt(
            string sessionId,
            int generation,
            int attackSequence,
            string enemyAssetId,
            FirstUserOnboardingEncounterResult result,
            int hitPointsBefore,
            int hitPointsAfter,
            int resetSequence)
        {
            SessionId = sessionId ?? string.Empty;
            Generation = generation;
            AttackSequence = attackSequence;
            EnemyAssetId = enemyAssetId ?? string.Empty;
            Result = result;
            HitPointsBefore = hitPointsBefore;
            HitPointsAfter = hitPointsAfter;
            ResetSequence = resetSequence;
        }

        public string SessionId { get; }
        public int Generation { get; }
        public int AttackSequence { get; }
        public string EnemyAssetId { get; }
        public FirstUserOnboardingEncounterResult Result { get; }
        public int HitPointsBefore { get; }
        public int HitPointsAfter { get; }
        public int ResetSequence { get; }
    }

    public interface IFirstUserOnboardingEnemyEncounter
    {
        string SessionId { get; }
        int Generation { get; }
        string EnemyAssetId { get; }
        GameObject EnemyRoot { get; }
        int InitialHitPoints { get; }
        int CurrentHitPoints { get; }
        int ResetSequence { get; }
        bool IsReady { get; }
        FirstUserOnboardingEncounterPresentationState PresentationState { get; }

        bool TryApplyBasicAttack(
            FirstUserOnboardingAttackRequest request,
            out FirstUserOnboardingAttackReceipt receipt,
            out string diagnostic);

        bool TryReset(
            string sessionId,
            int generation,
            int expectedNextResetSequence,
            out int appliedResetSequence,
            out string diagnostic);
    }

    public static class FirstUserOnboardingEncounterContract
    {
        public static bool IsValidRequest(FirstUserOnboardingAttackRequest request)
        {
            return IsCanonicalSessionId(request.SessionId) &&
                   request.Generation > 0 && request.AttackSequence > 0 &&
                   request.Frame >= 0 && IsCanonicalAssetId(request.EnemyAssetId) &&
                   IsFinite(request.AttackCenter) &&
                   !float.IsNaN(request.AttackRadius) &&
                   !float.IsInfinity(request.AttackRadius) &&
                   request.AttackRadius > 0f && request.AttackRadius <= 10f;
        }

        public static bool IsValidReceipt(
            FirstUserOnboardingAttackRequest request,
            FirstUserOnboardingAttackReceipt receipt)
        {
            if (!IsValidRequest(request) ||
                !string.Equals(receipt.SessionId, request.SessionId, StringComparison.Ordinal) ||
                receipt.Generation != request.Generation ||
                receipt.AttackSequence != request.AttackSequence ||
                !string.Equals(
                    receipt.EnemyAssetId,
                    request.EnemyAssetId,
                    StringComparison.Ordinal) ||
                receipt.ResetSequence < 0 ||
                receipt.ResetSequence >
                    FirstUserOnboardingEnvironmentBudget.MaximumEncounterResetSequence ||
                receipt.HitPointsBefore <= 0 ||
                receipt.HitPointsBefore > FirstUserOnboardingEnvironmentBudget.MaximumEnemyHitPoints ||
                receipt.HitPointsAfter < 0 ||
                receipt.HitPointsAfter >= receipt.HitPointsBefore)
            {
                return false;
            }

            return receipt.Result == FirstUserOnboardingEncounterResult.Defeated
                ? receipt.HitPointsAfter == 0
                : receipt.Result == FirstUserOnboardingEncounterResult.HitConfirmed &&
                  receipt.HitPointsAfter > 0;
        }

        private static bool IsCanonicalSessionId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
            {
                return false;
            }

            bool anyNonZero = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool valid = character >= '0' && character <= '9' ||
                             character >= 'a' && character <= 'f';
                if (!valid)
                {
                    return false;
                }

                anyNonZero |= character != '0';
            }

            return anyNonZero;
        }

        private static bool IsCanonicalAssetId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 96)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool valid = character >= 'a' && character <= 'z' ||
                             character >= '0' && character <= '9' ||
                             character == '_' || character == '-' || character == '.';
                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    /// <summary>
    /// Editor-domain registration boundary for the separately owned authored environment module.
    /// Registration never falls back to a test double and an unrelated owner cannot replace or
    /// unregister the active provider.
    /// </summary>
    public static class FirstUserOnboardingEnvironmentRegistry
    {
        private static readonly object Gate = new object();

        private static object _owner;
        private static IFirstUserOnboardingEnvironmentFactory _factory;
        private static IFirstUserOnboardingAssetInventoryVerifier _inventoryVerifier;

        public static bool IsReadyForUserPlaytest
        {
            get
            {
                lock (Gate)
                {
                    return _owner != null && _factory != null &&
                           _inventoryVerifier != null;
                }
            }
        }

        public static bool TryRegister(
            object owner,
            IFirstUserOnboardingEnvironmentFactory factory)
        {
            if (owner == null || factory == null ||
                !FirstUserOnboardingFixedAssetManifestGate.TryAuthorizeRegistration(
                    owner,
                    factory,
                    out IFirstUserOnboardingAssetInventoryVerifier inventoryVerifier))
            {
                return false;
            }

            lock (Gate)
            {
                if (_owner != null && !ReferenceEquals(_owner, owner))
                {
                    return false;
                }

                if (_owner != null)
                {
                    return ReferenceEquals(_factory, factory) &&
                           ReferenceEquals(_inventoryVerifier, inventoryVerifier);
                }

                _owner = owner;
                _factory = factory;
                _inventoryVerifier = inventoryVerifier;
                return true;
            }
        }

        public static bool TryUnregister(object owner)
        {
            if (owner == null)
            {
                return false;
            }

            lock (Gate)
            {
                if (!ReferenceEquals(_owner, owner))
                {
                    return false;
                }

                _owner = null;
                _factory = null;
                _inventoryVerifier = null;
                return true;
            }
        }

        internal static bool TryResolve(
            out IFirstUserOnboardingEnvironmentFactory factory,
            out IFirstUserOnboardingAssetInventoryVerifier inventoryVerifier)
        {
            lock (Gate)
            {
                factory = _factory;
                inventoryVerifier = _inventoryVerifier;
                return _owner != null && factory != null && inventoryVerifier != null;
            }
        }
    }

    /// <summary>
    /// Registry-owned trust boundary for the admitted MVP asset packet. Only the sealed authored
    /// provider may register, and every canonical path, GUID, file SHA, and admitted dependency is
    /// reverified through AssetDatabase before the registry exposes the provider.
    /// </summary>
    internal static class FirstUserOnboardingFixedAssetManifestGate
    {
        internal static bool TryAuthorizeRegistration(
            object owner,
            IFirstUserOnboardingEnvironmentFactory factory,
            out IFirstUserOnboardingAssetInventoryVerifier inventoryVerifier)
        {
            inventoryVerifier = null;
            if (!FirstUserOnboardingAuthoredEnvironmentProvider.Owns(owner, factory))
            {
                return false;
            }

            FirstUserOnboardingFixedAssetInventoryVerifier verifier =
                FirstUserOnboardingFixedAssetInventoryVerifier.Instance;
            if (!verifier.TryVerifyManifest(out _))
            {
                return false;
            }

            inventoryVerifier = verifier;
            return true;
        }
    }

    public interface IFirstUserOnboardingEnvironmentLease : IDisposable
    {
        string SessionId { get; }
        int Generation { get; }
        string ModuleId { get; }
        string ContentFingerprint { get; }
        string AssetInventoryFingerprint { get; }
        FirstUserOnboardingEnvironmentSourceKind SourceKind { get; }
        GameObject OwnedRoot { get; }
        UnityEngine.Object EnvironmentModuleSourceAsset { get; }
        string EnvironmentModuleAssetId { get; }
        GameObject NeutralEnvironmentRoot { get; }
        Transform SceneAnchor { get; }
        Transform SpawnAnchor { get; }
        Bounds WalkableBounds { get; }
        Vector3 MovementProofStart { get; }
        Vector3 MovementProofEnd { get; }
        Bounds AttackSafeBounds { get; }
        CharacterController PlayerController { get; }
        ChampionController PlayerChampion { get; }
        Camera PrimaryCamera { get; }
        Transform PrimaryCameraAnchor { get; }
        Transform PrimaryCameraTarget { get; }
        Transform OmenAnchor { get; }
        Transform LightingHook { get; }
        Transform PresentationHook { get; }
        GameObject ModularChampionRoot { get; }
        string ChampionAssetId { get; }
        UnityEngine.Object ChampionSourceAsset { get; }
        GameObject SelectedArmorRoot { get; }
        string ArmorAssetId { get; }
        UnityEngine.Object ArmorSourceAsset { get; }
        GameObject SelectedWeaponRoot { get; }
        string WeaponAssetId { get; }
        UnityEngine.Object WeaponSourceAsset { get; }
        GameObject EnemyRoot { get; }
        string EnemyAssetId { get; }
        UnityEngine.Object EnemySourceAsset { get; }
        FirstUserOnboardingEnemyCandidateKind EnemyCandidateKind { get; }
        FirstUserOnboardingEncounterMode EncounterMode { get; }
        IFirstUserOnboardingEnemyEncounter EnemyEncounter { get; }
        Transform EnemySpawnAnchor { get; }
        GameObject KingdomStructureRoot { get; }
        string KingdomStructureAssetId { get; }
        UnityEngine.Object KingdomStructureSourceAsset { get; }
        FirstUserOnboardingKingdomStructureMode KingdomStructureMode { get; }
        Material FloorMaterial { get; }
        string FloorMaterialAssetId { get; }
        Material WallMaterial { get; }
        string WallMaterialAssetId { get; }
        Material TrimMaterial { get; }
        string TrimMaterialAssetId { get; }
        Transform PropsRoot { get; }
        GameObject FloorModuleRoot { get; }
        GameObject WallModuleRoot { get; }
        GameObject InnerCornerModuleRoot { get; }
        GameObject OuterCornerModuleRoot { get; }
        GameObject DoorwayModuleRoot { get; }
        GameObject CeilingBeamModuleRoot { get; }
        GameObject TrimModuleRoot { get; }
        GameObject BrazierPropRoot { get; }
        GameObject BannerStandPropRoot { get; }
        GameObject CrateBarrelPropRoot { get; }
        int EffectiveTexelsPerMeter { get; }
        bool IsDisposed { get; }
    }

    internal enum FirstUserOnboardingEnvironmentFailure
    {
        None = 0,
        RequestInvalid = 1,
        LeaseMissing = 2,
        IdentityMismatch = 3,
        SceneMismatch = 4,
        OwnedRootInvalid = 5,
        AnchorInvalid = 6,
        WalkableBoundsInvalid = 7,
        MovementPathInvalid = 8,
        AttackSpaceInvalid = 9,
        PlayerControllerInvalid = 10,
        CameraInvalid = 11,
        OmenAnchorInvalid = 12,
        PresentationHookInvalid = 13,
        RendererBudgetExceeded = 14,
        TriangleBudgetExceeded = 15,
        MaterialBudgetExceeded = 16,
        LightBudgetExceeded = 17,
        ParticleBudgetExceeded = 18,
        TexelDensityInvalid = 19,
        ForbiddenAuthorityPresent = 20,
        Disposed = 21,
        UnitTestDoubleNotAllowed = 22,
        AssetCompletenessMissing = 23,
        EncounterInvalid = 24,
        AssetInventoryUnavailable = 25,
        AssetInventoryRejected = 26,
        PbrMaterialContractInvalid = 27,
        ModularGeometryIncomplete = 28,
        EncounterAuthorityPresent = 29
    }

    internal readonly struct FirstUserOnboardingEnvironmentValidation
    {
        internal FirstUserOnboardingEnvironmentValidation(
            bool isValid,
            FirstUserOnboardingEnvironmentFailure failure,
            int visibleTriangles,
            int rendererCount,
            int sharedMaterialCount,
            int shadowedDirectionalLightCount,
            int nonShadowedLocalLightCount,
            int ambientParticleCount)
        {
            IsValid = isValid;
            Failure = failure;
            VisibleTriangles = visibleTriangles;
            RendererCount = rendererCount;
            SharedMaterialCount = sharedMaterialCount;
            ShadowedDirectionalLightCount = shadowedDirectionalLightCount;
            NonShadowedLocalLightCount = nonShadowedLocalLightCount;
            AmbientParticleCount = ambientParticleCount;
        }

        internal bool IsValid { get; }
        internal FirstUserOnboardingEnvironmentFailure Failure { get; }
        internal int VisibleTriangles { get; }
        internal int RendererCount { get; }
        internal int SharedMaterialCount { get; }
        internal int ShadowedDirectionalLightCount { get; }
        internal int NonShadowedLocalLightCount { get; }
        internal int AmbientParticleCount { get; }
    }

    internal static class FirstUserOnboardingEnvironmentValidator
    {
        private const float BoundsTolerance = 0.01f;
        private const float MinimumMovementProofDistance = 1f;

        private static readonly string[] ForbiddenComponentTypeNames =
        {
            "AL.Core.Bootloader",
            "AL.UI.BootController",
            "AL.UI.RealmSelection.RealmSelectionController",
            "AL.ChampionMode.ChampionArenaSceneController",
            "AL.ChampionMode.AI.BossDummyAI"
        };

        internal static FirstUserOnboardingEnvironmentValidation Validate(
            FirstUserOnboardingEnvironmentRequest request,
            IFirstUserOnboardingEnvironmentLease lease)
        {
            if (!FirstUserCoreGameplayPlanner.IsCanonicalSessionId(request.SessionId) ||
                request.Generation <= 0 ||
                !request.Scene.IsValid() ||
                !request.Scene.isLoaded)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.RequestInvalid);
            }

            if (lease == null)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.LeaseMissing);
            }

            if (lease.IsDisposed)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.Disposed);
            }

            if (!string.Equals(request.SessionId, lease.SessionId, StringComparison.Ordinal) ||
                request.Generation != lease.Generation ||
                !IsCanonicalModuleId(lease.ModuleId) ||
                !IsLowercaseSha256(lease.ContentFingerprint))
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.IdentityMismatch);
            }

            if (lease.SourceKind == FirstUserOnboardingEnvironmentSourceKind.UnitTestDouble)
            {
                if (!request.AllowUnitTestDouble)
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.UnitTestDoubleNotAllowed);
                }
            }
            else if (lease.SourceKind !=
                     FirstUserOnboardingEnvironmentSourceKind.AuthoredModule)
            {
                return Invalid(
                    FirstUserOnboardingEnvironmentFailure.AssetCompletenessMissing);
            }

            IFirstUserOnboardingAssetInventoryVerifier inventoryVerifier =
                request.AssetInventoryVerifier;

            GameObject root = lease.OwnedRoot;
            if (root == null || root.transform == null || root.transform.parent != null)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.OwnedRootInvalid);
            }

            if (root.scene != request.Scene)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.SceneMismatch);
            }

            if (!IsOwnedTransform(root, lease.SceneAnchor) ||
                !IsOwnedTransform(root, lease.SpawnAnchor))
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.AnchorInvalid);
            }

            Bounds walkable = lease.WalkableBounds;
            if (!IsFinite(walkable.center) || !IsFinite(walkable.size) ||
                Math.Abs(walkable.size.x - FirstUserOnboardingEnvironmentBudget.RoomWidthMeters) >
                BoundsTolerance ||
                Math.Abs(walkable.size.z - FirstUserOnboardingEnvironmentBudget.RoomLengthMeters) >
                BoundsTolerance ||
                walkable.size.y <= 0f ||
                !walkable.Contains(lease.SpawnAnchor.position))
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.WalkableBoundsInvalid);
            }

            Vector3 movementStart = lease.MovementProofStart;
            Vector3 movementEnd = lease.MovementProofEnd;
            Vector3 movementDelta = movementEnd - movementStart;
            movementDelta.y = 0f;
            if (!IsFinite(movementStart) || !IsFinite(movementEnd) ||
                !walkable.Contains(movementStart) ||
                !walkable.Contains(movementEnd) ||
                movementDelta.magnitude < MinimumMovementProofDistance)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.MovementPathInvalid);
            }

            Bounds attackSafe = lease.AttackSafeBounds;
            if (!IsFinite(attackSafe.center) || !IsFinite(attackSafe.size) ||
                attackSafe.size.x <= 0f || attackSafe.size.y <= 0f ||
                attackSafe.size.z <= 0f ||
                !walkable.Contains(attackSafe.min) ||
                !walkable.Contains(attackSafe.max) ||
                !attackSafe.Contains(movementEnd))
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.AttackSpaceInvalid);
            }

            CharacterController player = lease.PlayerController;
            if (player == null || !IsOwnedTransform(root, player.transform) ||
                !player.enabled || player.radius <= 0f || player.height <= 0f ||
                !IsFinite(player.transform.position) ||
                !walkable.Contains(player.transform.position))
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.PlayerControllerInvalid);
            }

            if (lease.PlayerChampion == null ||
                !IsOwnedTransform(root, lease.PlayerChampion.transform) ||
                !ReferenceEquals(lease.PlayerChampion.gameObject, player.gameObject) ||
                root.GetComponentsInChildren<CharacterController>(true).Length != 1 ||
                root.GetComponentsInChildren<ChampionController>(true).Length != 1)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.PlayerControllerInvalid);
            }

            if (lease.PrimaryCamera == null ||
                !IsOwnedTransform(root, lease.PrimaryCamera.transform) ||
                !lease.PrimaryCamera.enabled ||
                !lease.PrimaryCamera.CompareTag("MainCamera") ||
                !IsOwnedTransform(root, lease.PrimaryCameraAnchor) ||
                !IsOwnedTransform(root, lease.PrimaryCameraTarget) ||
                root.GetComponentsInChildren<Camera>(true).Length != 1)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.CameraInvalid);
            }

            AudioListener[] listeners = root.GetComponentsInChildren<AudioListener>(true);
            if (listeners.Length != 1 || listeners[0] == null ||
                !listeners[0].enabled ||
                !ReferenceEquals(
                    listeners[0].gameObject,
                    lease.PrimaryCamera.gameObject))
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.CameraInvalid);
            }

            if (!IsOwnedTransform(root, lease.OmenAnchor) ||
                !walkable.Contains(lease.OmenAnchor.position))
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.OmenAnchorInvalid);
            }

            if (!IsOwnedTransform(root, lease.LightingHook) ||
                !IsOwnedTransform(root, lease.PresentationHook))
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.PresentationHookInvalid);
            }

            if (lease.SourceKind == FirstUserOnboardingEnvironmentSourceKind.AuthoredModule)
            {
                if (lease.EnvironmentModuleSourceAsset == null ||
                    !IsCanonicalAssetId(lease.EnvironmentModuleAssetId) ||
                    !IsOwnedActiveGameObject(root, lease.NeutralEnvironmentRoot) ||
                    !IsOwnedActiveGameObject(root, lease.ModularChampionRoot) ||
                    !IsCanonicalAssetId(lease.ChampionAssetId) ||
                    lease.ChampionSourceAsset == null ||
                    !IsOwnedActiveGameObject(root, lease.SelectedArmorRoot) ||
                    !IsCanonicalAssetId(lease.ArmorAssetId) ||
                    lease.ArmorSourceAsset == null ||
                    !IsOwnedActiveGameObject(root, lease.SelectedWeaponRoot) ||
                    !IsCanonicalAssetId(lease.WeaponAssetId) ||
                    lease.WeaponSourceAsset == null ||
                    !IsOwnedActiveGameObject(root, lease.EnemyRoot) ||
                    !IsCanonicalAssetId(lease.EnemyAssetId) ||
                    lease.EnemySourceAsset == null ||
                    !IsOwnedTransform(root, lease.EnemySpawnAnchor) ||
                    !IsOwnedActiveGameObject(root, lease.KingdomStructureRoot) ||
                    !IsCanonicalAssetId(lease.KingdomStructureAssetId) ||
                    lease.KingdomStructureSourceAsset == null ||
                    lease.FloorMaterial == null ||
                    !IsCanonicalAssetId(lease.FloorMaterialAssetId) ||
                    lease.WallMaterial == null ||
                    !IsCanonicalAssetId(lease.WallMaterialAssetId) ||
                    lease.TrimMaterial == null ||
                    !IsCanonicalAssetId(lease.TrimMaterialAssetId) ||
                    ReferenceEquals(lease.FloorMaterial, lease.WallMaterial) ||
                    ReferenceEquals(lease.FloorMaterial, lease.TrimMaterial) ||
                    ReferenceEquals(lease.WallMaterial, lease.TrimMaterial) ||
                    !IsOwnedTransform(root, lease.PropsRoot))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.AssetCompletenessMissing);
                }

                if (IsOwnedGameObject(
                        lease.NeutralEnvironmentRoot,
                        lease.ModularChampionRoot) ||
                    IsOwnedGameObject(
                        lease.NeutralEnvironmentRoot,
                        lease.EnemyRoot) ||
                    IsOwnedGameObject(
                        lease.NeutralEnvironmentRoot,
                        lease.KingdomStructureRoot))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.AssetCompletenessMissing);
                }

                if (!HasCompleteModularGeometry(
                        lease.NeutralEnvironmentRoot,
                        lease))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.ModularGeometryIncomplete);
                }

                if (inventoryVerifier == null ||
                    !IsLowercaseSha256(inventoryVerifier.InventoryFingerprint) ||
                    !string.Equals(
                        inventoryVerifier.InventoryFingerprint,
                        lease.AssetInventoryFingerprint,
                        StringComparison.Ordinal))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.AssetInventoryUnavailable);
                }

                bool encounterAuthorityPresent = HasAttackAuthority(lease.EnemyRoot);
                if ((lease.EnemyCandidateKind !=
                        FirstUserOnboardingEnemyCandidateKind.Normal &&
                     lease.EnemyCandidateKind !=
                        FirstUserOnboardingEnemyCandidateKind.Elite &&
                     lease.EnemyCandidateKind !=
                        FirstUserOnboardingEnemyCandidateKind.Boss) ||
                     lease.EncounterMode !=
                        FirstUserOnboardingEncounterMode.BoundedMechanicsEncounter ||
                     lease.KingdomStructureMode !=
                        FirstUserOnboardingKingdomStructureMode.LockedPreviewOnly ||
                     !walkable.Contains(lease.EnemySpawnAnchor.position) ||
                     !attackSafe.Contains(lease.EnemySpawnAnchor.position) ||
                     encounterAuthorityPresent ||
                     !HasValidMechanicsEncounter(lease, attackSafe))
                {
                    return Invalid(
                        encounterAuthorityPresent
                            ? FirstUserOnboardingEnvironmentFailure.EncounterAuthorityPresent
                            : FirstUserOnboardingEnvironmentFailure.EncounterInvalid);
                }

                if (!TryVerifyAuthoredAssets(inventoryVerifier, lease))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.AssetInventoryRejected);
                }

                if (!inventoryVerifier.TryVerifyModularKit(lease, out _))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.ModularGeometryIncomplete);
                }

                if (!inventoryVerifier.TryVerifyChampionRigAndLoadout(lease, out _))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.AssetInventoryRejected);
                }

                if (!inventoryVerifier.TryVerifyMechanicsEncounterSlot(lease, out _))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.EncounterInvalid);
                }

                if (!inventoryVerifier.TryVerifyLockedKingdomStructureSlot(
                        lease,
                        out _))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.AssetInventoryRejected);
                }

                if (!inventoryVerifier.TryVerifyCharacterControllerSafeTraversal(
                        lease,
                        out _))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.MovementPathInvalid);
                }

                if (!inventoryVerifier.TryVerifyRuntimeComponentInventory(
                        lease,
                        out _))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.ForbiddenAuthorityPresent);
                }

                if (!TryVerifyPbrMaterials(inventoryVerifier, lease))
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.PbrMaterialContractInvalid);
                }
            }

            if (ContainsForbiddenAuthority(root) ||
                root.GetComponentsInChildren<EventSystem>(true).Length != 0 ||
                root.GetComponentsInChildren<BaseInputModule>(true).Length != 0)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.ForbiddenAuthorityPresent);
            }

            GameObject environmentBudgetRoot =
                lease.SourceKind == FirstUserOnboardingEnvironmentSourceKind.AuthoredModule
                    ? lease.NeutralEnvironmentRoot
                    : root;
            Renderer[] renderers = environmentBudgetRoot
                .GetComponentsInChildren<Renderer>(true);
            int visibleRendererCount = 0;
            int triangleCount = 0;
            var materials = new HashSet<Material>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                visibleRendererCount++;
                if (visibleRendererCount >
                    FirstUserOnboardingEnvironmentBudget.MaximumRenderers)
                {
                    return Invalid(
                        FirstUserOnboardingEnvironmentFailure.RendererBudgetExceeded);
                }

                Material[] sharedMaterials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    if (sharedMaterials[materialIndex] != null)
                    {
                        materials.Add(sharedMaterials[materialIndex]);
                    }
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter == null ? null : filter.sharedMesh;
                if (mesh == null && renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    mesh = skinnedRenderer.sharedMesh;
                }

                if (mesh != null)
                {
                    for (int subMeshIndex = 0;
                         subMeshIndex < mesh.subMeshCount;
                         subMeshIndex++)
                    {
                        if (mesh.GetTopology(subMeshIndex) != MeshTopology.Triangles)
                        {
                            continue;
                        }

                        uint indexCount = mesh.GetIndexCount(subMeshIndex);
                        triangleCount = checked(
                            triangleCount + (int)(indexCount / 3u));
                    }
                }
            }

            if (triangleCount > FirstUserOnboardingEnvironmentBudget.MaximumVisibleTriangles)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.TriangleBudgetExceeded);
            }

            if (materials.Count > FirstUserOnboardingEnvironmentBudget.MaximumSharedMaterials)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.MaterialBudgetExceeded);
            }


            if (lease.SourceKind == FirstUserOnboardingEnvironmentSourceKind.AuthoredModule &&
                (!materials.Contains(lease.FloorMaterial) ||
                 !materials.Contains(lease.WallMaterial) ||
                 !materials.Contains(lease.TrimMaterial)))
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.PbrMaterialContractInvalid);
            }

            int shadowedDirectional = 0;
            int nonShadowedLocal = 0;
            Light[] lights = environmentBudgetRoot.GetComponentsInChildren<Light>(true);
            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (light.type == LightType.Directional && light.shadows != LightShadows.None)
                {
                    shadowedDirectional++;
                }
                else if ((light.type == LightType.Point || light.type == LightType.Spot) &&
                         light.shadows == LightShadows.None)
                {
                    nonShadowedLocal++;
                }
                else
                {
                    return Invalid(FirstUserOnboardingEnvironmentFailure.LightBudgetExceeded);
                }
            }

            if (shadowedDirectional >
                    FirstUserOnboardingEnvironmentBudget.MaximumShadowedDirectionalLights ||
                nonShadowedLocal >
                    FirstUserOnboardingEnvironmentBudget.MaximumNonShadowedLocalLights)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.LightBudgetExceeded);
            }

            if (lease.SourceKind == FirstUserOnboardingEnvironmentSourceKind.AuthoredModule &&
                shadowedDirectional != 1)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.LightBudgetExceeded);
            }

            int ambientParticles = 0;
            ParticleSystem[] particleSystems = environmentBudgetRoot
                .GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ambientParticles = checked(
                    ambientParticles + particleSystems[index].main.maxParticles);
            }

            if (ambientParticles > FirstUserOnboardingEnvironmentBudget.MaximumAmbientParticles)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.ParticleBudgetExceeded);
            }

            if (lease.EffectiveTexelsPerMeter !=
                    FirstUserOnboardingEnvironmentBudget.AuthoringTexelsPerMeter &&
                lease.EffectiveTexelsPerMeter !=
                    FirstUserOnboardingEnvironmentBudget.LowTierEffectiveTexelsPerMeter)
            {
                return Invalid(FirstUserOnboardingEnvironmentFailure.TexelDensityInvalid);
            }

            return new FirstUserOnboardingEnvironmentValidation(
                isValid: true,
                FirstUserOnboardingEnvironmentFailure.None,
                triangleCount,
                visibleRendererCount,
                materials.Count,
                shadowedDirectional,
                nonShadowedLocal,
                ambientParticles);
        }

        private static bool HasCompleteModularGeometry(
            GameObject root,
            IFirstUserOnboardingEnvironmentLease lease)
        {
            GameObject[] required =
            {
                lease.FloorModuleRoot,
                lease.WallModuleRoot,
                lease.InnerCornerModuleRoot,
                lease.OuterCornerModuleRoot,
                lease.DoorwayModuleRoot,
                lease.CeilingBeamModuleRoot,
                lease.TrimModuleRoot,
                lease.BrazierPropRoot,
                lease.BannerStandPropRoot,
                lease.CrateBarrelPropRoot
            };

            var identities = new HashSet<int>();
            for (int index = 0; index < required.Length; index++)
            {
                GameObject candidate = required[index];
                if (!IsOwnedActiveGameObject(root, candidate) ||
                    !identities.Add(candidate.GetInstanceID()))
                {
                    return false;
                }
            }

            return IsOwnedTransform(root, lease.PropsRoot) &&
                   lease.BrazierPropRoot.transform.IsChildOf(lease.PropsRoot) &&
                   lease.BannerStandPropRoot.transform.IsChildOf(lease.PropsRoot) &&
                   lease.CrateBarrelPropRoot.transform.IsChildOf(lease.PropsRoot) &&
                   HasEnabledSolidCollider(lease.FloorModuleRoot) &&
                   HasEnabledSolidCollider(lease.WallModuleRoot) &&
                   HasEnabledSolidCollider(lease.InnerCornerModuleRoot) &&
                   HasEnabledSolidCollider(lease.OuterCornerModuleRoot) &&
                   HasEnabledSolidCollider(lease.DoorwayModuleRoot);
        }

        private static bool HasEnabledSolidCollider(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider candidate = colliders[index];
                if (candidate != null && candidate.enabled && !candidate.isTrigger &&
                    candidate.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryVerifyAuthoredAssets(
            IFirstUserOnboardingAssetInventoryVerifier verifier,
            IFirstUserOnboardingEnvironmentLease lease)
        {
            return verifier != null &&
                   verifier.TryVerifyExactAsset(
                       FirstUserOnboardingAssetRole.EnvironmentModule,
                       lease.EnvironmentModuleAssetId,
                       lease.EnvironmentModuleSourceAsset,
                       lease.NeutralEnvironmentRoot,
                       out _) &&
                   verifier.TryVerifyExactAsset(
                       FirstUserOnboardingAssetRole.ModularChampion,
                       lease.ChampionAssetId,
                       lease.ChampionSourceAsset,
                       lease.ModularChampionRoot,
                       out _) &&
                   verifier.TryVerifyExactAsset(
                       FirstUserOnboardingAssetRole.SelectedBasicArmor,
                       lease.ArmorAssetId,
                       lease.ArmorSourceAsset,
                       lease.SelectedArmorRoot,
                       out _) &&
                   verifier.TryVerifyExactAsset(
                       FirstUserOnboardingAssetRole.SelectedBasicWeapon,
                       lease.WeaponAssetId,
                       lease.WeaponSourceAsset,
                       lease.SelectedWeaponRoot,
                       out _) &&
                   verifier.TryVerifyExactAsset(
                       FirstUserOnboardingAssetRole.CommonEnemy,
                       lease.EnemyAssetId,
                       lease.EnemySourceAsset,
                       lease.EnemyRoot,
                       out _) &&
                   verifier.TryVerifyExactAsset(
                       FirstUserOnboardingAssetRole.KingdomBaseStructure,
                       lease.KingdomStructureAssetId,
                       lease.KingdomStructureSourceAsset,
                       lease.KingdomStructureRoot,
                       out _) &&
                   verifier.TryVerifyExactAsset(
                       FirstUserOnboardingAssetRole.FloorMaterial,
                       lease.FloorMaterialAssetId,
                       lease.FloorMaterial,
                       lease.FloorMaterial,
                       out _) &&
                   verifier.TryVerifyExactAsset(
                       FirstUserOnboardingAssetRole.WallMaterial,
                       lease.WallMaterialAssetId,
                       lease.WallMaterial,
                       lease.WallMaterial,
                       out _) &&
                   verifier.TryVerifyExactAsset(
                       FirstUserOnboardingAssetRole.TrimMaterial,
                       lease.TrimMaterialAssetId,
                       lease.TrimMaterial,
                       lease.TrimMaterial,
                       out _);
        }

        private static bool TryVerifyPbrMaterials(
            IFirstUserOnboardingAssetInventoryVerifier verifier,
            IFirstUserOnboardingEnvironmentLease lease)
        {
            return verifier != null &&
                   verifier.TryVerifyBuiltInPbrMaterial(
                       FirstUserOnboardingAssetRole.FloorMaterial,
                       lease.FloorMaterial,
                       out _) &&
                   verifier.TryVerifyBuiltInPbrMaterial(
                       FirstUserOnboardingAssetRole.WallMaterial,
                       lease.WallMaterial,
                       out _) &&
                   verifier.TryVerifyBuiltInPbrMaterial(
                       FirstUserOnboardingAssetRole.TrimMaterial,
                       lease.TrimMaterial,
                       out _);
        }

        private static bool HasAttackAuthority(GameObject enemyRoot)
        {
            if (enemyRoot == null)
            {
                return true;
            }

            MonoBehaviour[] behaviours = enemyRoot.GetComponentsInChildren<MonoBehaviour>(true);
            if (behaviours.Length != 0)
            {
                return true;
            }

            Transform[] transforms = enemyRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] != null &&
                    transforms[index].name.StartsWith("Dummy_", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasValidMechanicsEncounter(
            IFirstUserOnboardingEnvironmentLease lease,
            Bounds attackSafe)
        {
            IFirstUserOnboardingEnemyEncounter encounter = lease.EnemyEncounter;
            if (encounter == null || !encounter.IsReady ||
                !ReferenceEquals(encounter.EnemyRoot, lease.EnemyRoot) ||
                !string.Equals(encounter.SessionId, lease.SessionId, StringComparison.Ordinal) ||
                encounter.Generation != lease.Generation ||
                !string.Equals(
                    encounter.EnemyAssetId,
                    lease.EnemyAssetId,
                    StringComparison.Ordinal) ||
                encounter.InitialHitPoints <= 0 ||
                encounter.InitialHitPoints >
                    FirstUserOnboardingEnvironmentBudget.MaximumEnemyHitPoints ||
                encounter.CurrentHitPoints != encounter.InitialHitPoints ||
                encounter.ResetSequence < 0 ||
                encounter.ResetSequence >
                    FirstUserOnboardingEnvironmentBudget.MaximumEncounterResetSequence ||
                encounter.PresentationState !=
                    FirstUserOnboardingEncounterPresentationState.Idle)
            {
                return false;
            }

            Collider[] colliders = lease.EnemyRoot.GetComponentsInChildren<Collider>(true);
            int enabledSolidCount = 0;
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null || !collider.gameObject.activeInHierarchy ||
                    !collider.enabled)
                {
                    continue;
                }

                if (collider.isTrigger || !attackSafe.Contains(collider.bounds.min) ||
                    !attackSafe.Contains(collider.bounds.max))
                {
                    return false;
                }

                enabledSolidCount++;
            }

            return enabledSolidCount > 0 &&
                   enabledSolidCount <=
                       FirstUserOnboardingEnvironmentBudget.MaximumEnemyHitColliders;
        }

        private static bool ContainsForbiddenAuthority(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null)
                {
                    continue;
                }

                string fullName = behaviour.GetType().FullName ?? string.Empty;
                for (int typeIndex = 0; typeIndex < ForbiddenComponentTypeNames.Length; typeIndex++)
                {
                    if (string.Equals(
                            fullName,
                            ForbiddenComponentTypeNames[typeIndex],
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsOwnedTransform(GameObject root, Transform candidate)
        {
            return candidate != null &&
                   (candidate == root.transform || candidate.IsChildOf(root.transform));
        }

        private static bool IsOwnedGameObject(GameObject root, GameObject candidate)
        {
            return candidate != null && IsOwnedTransform(root, candidate.transform);
        }

        private static bool IsOwnedActiveGameObject(GameObject root, GameObject candidate)
        {
            return IsOwnedGameObject(root, candidate) && candidate.activeInHierarchy;
        }

        private static bool IsCanonicalAssetId(string value)
        {
            return IsCanonicalModuleId(value);
        }

        private static bool IsCanonicalModuleId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool lower = character >= 'a' && character <= 'z';
                bool digit = character >= '0' && character <= '9';
                if (!lower && !digit && character != '_' && character != '.' && character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsLowercaseSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static FirstUserOnboardingEnvironmentValidation Invalid(
            FirstUserOnboardingEnvironmentFailure failure)
        {
            return new FirstUserOnboardingEnvironmentValidation(
                isValid: false,
                failure,
                visibleTriangles: 0,
                rendererCount: 0,
                sharedMaterialCount: 0,
                shadowedDirectionalLightCount: 0,
                nonShadowedLocalLightCount: 0,
                ambientParticleCount: 0);
        }
    }
}
