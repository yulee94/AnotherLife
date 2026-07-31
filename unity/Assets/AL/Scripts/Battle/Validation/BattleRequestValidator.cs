using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using AL.Battle.Contracts;

namespace AL.Battle.Validation
{
    public sealed class BattleValidationResult
    {
        private readonly ReadOnlyCollection<BattleDiagnostic> _diagnostics;

        internal BattleValidationResult(
            BattleComputationStatus status,
            IEnumerable<BattleDiagnostic> diagnostics)
        {
            Status = status;
            _diagnostics = Array.AsReadOnly(new List<BattleDiagnostic>(diagnostics).ToArray());
        }

        public BattleComputationStatus Status { get; }
        public IReadOnlyList<BattleDiagnostic> Diagnostics => _diagnostics;
        public bool IsValid => _diagnostics.Count == 0;
    }

    public static class BattleRequestValidator
    {
        public static BattleValidationResult Validate(BattleComputationRequest request)
        {
            var diagnostics = new List<BattleDiagnostic>();
            if (request == null)
            {
                Add(diagnostics, "AL-BATTLE-REQUEST-NULL", "request");
                return Result(BattleComputationStatus.InvalidRequest, diagnostics);
            }

            ValidateRequestIdentity(request, diagnostics);
            if (diagnostics.Count > 0)
                return Result(BattleComputationStatus.InvalidRequest, diagnostics);

            if (request.ExecutionMode != BattleExecutionMode.Preview &&
                request.ExecutionMode != BattleExecutionMode.Authoritative)
            {
                Add(diagnostics, "AL-BATTLE-EXECUTION-MODE-UNSUPPORTED", "request.executionMode");
                return Result(BattleComputationStatus.UnsupportedExecutionMode, diagnostics);
            }

            if (!TryExpectedTypeId(request.BattleKind, out string expectedTypeId) ||
                !string.Equals(request.BattleTypeId, expectedTypeId, StringComparison.Ordinal))
            {
                Add(diagnostics, "AL-BATTLE-TYPE-UNSUPPORTED", "request.battleTypeId");
                return Result(BattleComputationStatus.UnsupportedBattleType, diagnostics);
            }

            bool previewId = request.BattleResultId.StartsWith(
                BattleTechnicalLimits.PreviewResultPrefix,
                StringComparison.Ordinal);
            if ((request.ExecutionMode == BattleExecutionMode.Preview) != previewId)
            {
                Add(diagnostics, "AL-BATTLE-RESULT-NAMESPACE-MISMATCH", "request.battleResultId");
                return Result(BattleComputationStatus.InvalidRequest, diagnostics);
            }

            if (!string.Equals(
                    request.DeterminismVersion,
                    BattleTechnicalLimits.SupportedDeterminismVersion,
                    StringComparison.Ordinal))
            {
                Add(diagnostics, "AL-BATTLE-DETERMINISM-VERSION-UNSUPPORTED", "request.determinismVersion");
                return Result(BattleComputationStatus.UnsupportedVersion, diagnostics);
            }

            if (request.Catalog == null)
            {
                Add(diagnostics, "AL-BATTLE-CATALOG-UNAVAILABLE", "request.catalog");
                return Result(BattleComputationStatus.CatalogUnavailable, diagnostics);
            }

            ValidateCatalog(request, diagnostics);
            if (diagnostics.Count > 0)
                return Result(BattleComputationStatus.CatalogUnavailable, diagnostics);

            ValidateArmy(request.AttackerArmy, request, "request.attackerArmy", diagnostics);
            if (diagnostics.Count > 0)
                return Result(BattleComputationStatus.InvalidArmy, diagnostics);

            ValidateOpponent(request, diagnostics);
            if (diagnostics.Count > 0)
                return Result(BattleComputationStatus.InvalidOpponent, diagnostics);

            ValidateContext(request, diagnostics);
            if (diagnostics.Count > 0)
            {
                BattleComputationStatus status = HasCodePrefix(
                    diagnostics,
                    "AL-BATTLE-TERRAIN-")
                    ? BattleComputationStatus.InvalidTerrainProfile
                    : HasCodePrefix(diagnostics, "AL-BATTLE-MODIFIER-")
                        ? BattleComputationStatus.InvalidModifierSnapshot
                        : BattleComputationStatus.InvalidRealmContext;
                return Result(status, diagnostics);
            }

            ValidateRules(request, diagnostics);
            if (diagnostics.Count > 0)
                return Result(BattleComputationStatus.InvalidRulesProfile, diagnostics);

            ValidateRewards(request, diagnostics);
            if (diagnostics.Count > 0)
                return Result(BattleComputationStatus.InvalidRewardProfile, diagnostics);

            return Result(BattleComputationStatus.Computed, diagnostics);
        }

        public static bool IsStableId(string value)
        {
            return IsBoundedText(value, BattleTechnicalLimits.MaximumIdUtf8Bytes);
        }

        public static bool IsVersion(string value)
        {
            return IsBoundedText(value, BattleTechnicalLimits.MaximumVersionUtf8Bytes);
        }

        public static bool IsLowerSha256(string value)
        {
            if (value == null || value.Length != 64)
                return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                    return false;
            }

            return true;
        }

        private static void ValidateRequestIdentity(
            BattleComputationRequest request,
            List<BattleDiagnostic> diagnostics)
        {
            ValidateId(request.GameId, "request.gameId", diagnostics);
            ValidateId(request.CatalogSetId, "request.catalogSetId", diagnostics);
            ValidateId(request.ProfileId, "request.profileId", diagnostics);
            ValidateId(request.BattleRequestId, "request.battleRequestId", diagnostics);
            ValidateId(request.BattleId, "request.battleId", diagnostics);
            ValidateId(request.BattleResultId, "request.battleResultId", diagnostics);
            ValidateId(
                request.ExpectedResultConsumerId,
                "request.expectedResultConsumerId",
                diagnostics);
            if (!string.Equals(
                    request.ExpectedResultConsumerId,
                    BattleTechnicalLimits.ExpectedResultConsumerId,
                    StringComparison.Ordinal))
                Add(
                    diagnostics,
                    "AL-BATTLE-EXPECTED-CONSUMER-MISMATCH",
                    "request.expectedResultConsumerId");
            if (!IsLowerSha256(request.SeedHex))
                Add(diagnostics, "AL-BATTLE-SEED-INVALID", "request.seedHex");
        }

        private static void ValidateCatalog(
            BattleComputationRequest request,
            List<BattleDiagnostic> diagnostics)
        {
            BattleCatalogSnapshot catalog = request.Catalog;
            int catalogDiagnosticStart = diagnostics.Count;
            ValidateIdentity(catalog.Identity, request, "request.catalog.identity", diagnostics);
            if (catalog.Identity != null &&
                !string.Equals(catalog.Identity.Id, request.GameId + ".battle_catalog", StringComparison.Ordinal))
                Add(diagnostics, "AL-BATTLE-CATALOG-GAME-MISMATCH", "request.catalog.identity.id");
            if (catalog.TroopDefinitions.Count == 0 ||
                catalog.TroopDefinitions.Count > BattleTechnicalLimits.MaximumCollectionCount)
            {
                Add(diagnostics, "AL-BATTLE-CATALOG-TROOPS-INVALID", "request.catalog.troopDefinitions");
                return;
            }

            string previousId = null;
            var archetypes = new HashSet<BattleTroopArchetype>();
            bool canHashCatalog = catalog.Identity != null;
            for (int index = 0; index < catalog.TroopDefinitions.Count; index++)
            {
                BattleTroopDefinition definition = catalog.TroopDefinitions[index];
                string path = "request.catalog.troopDefinitions[" + index + "]";
                if (definition == null)
                {
                    Add(diagnostics, "AL-BATTLE-CATALOG-TROOP-NULL", path);
                    canHashCatalog = false;
                    continue;
                }

                int definitionDiagnosticStart = diagnostics.Count;
                ValidateIdentity(definition.Identity, request, path + ".identity", diagnostics);
                if (definition.Identity == null)
                    canHashCatalog = false;
                string currentId = definition.Identity?.Id;
                if (previousId != null &&
                    string.CompareOrdinal(previousId, currentId) >= 0)
                    Add(diagnostics, "AL-BATTLE-CATALOG-TROOPS-NONCANONICAL", path + ".identity.id");
                previousId = currentId;
                if (!IsKnownArchetype(definition.Archetype) ||
                    definition.BasePower != ExpectedBasePower(definition.Archetype))
                    Add(diagnostics, "AL-BATTLE-CATALOG-TROOP-TUNING-INVALID", path);
                archetypes.Add(definition.Archetype);
                if (definition.Identity != null &&
                    diagnostics.Count == definitionDiagnosticStart &&
                    !string.Equals(
                        definition.Identity.Sha256,
                        BattleCanonicalHash.TroopDefinition(definition),
                        StringComparison.Ordinal))
                    Add(diagnostics, "AL-BATTLE-CATALOG-TROOP-HASH-MISMATCH", path + ".identity.sha256");
            }

            if (archetypes.Count != 4)
                Add(diagnostics, "AL-BATTLE-CATALOG-ARCHETYPE-COVERAGE", "request.catalog.troopDefinitions");
            if (canHashCatalog &&
                diagnostics.Count == catalogDiagnosticStart &&
                !string.Equals(
                    catalog.Identity.Sha256,
                    BattleCanonicalHash.Catalog(catalog),
                    StringComparison.Ordinal))
                Add(diagnostics, "AL-BATTLE-CATALOG-HASH-MISMATCH", "request.catalog.identity.sha256");
        }

        private static void ValidateArmy(
            BattleArmySnapshot army,
            BattleComputationRequest request,
            string path,
            List<BattleDiagnostic> diagnostics)
        {
            if (army == null)
            {
                Add(diagnostics, "AL-BATTLE-ARMY-NULL", path);
                return;
            }

            int armyDiagnosticStart = diagnostics.Count;
            ValidateIdentity(army.Identity, request, path + ".identity", diagnostics);
            if (army.Stacks.Count == 0 ||
                army.Stacks.Count > BattleTechnicalLimits.MaximumCollectionCount)
            {
                Add(diagnostics, "AL-BATTLE-ARMY-STACKS-INVALID", path + ".stacks");
                return;
            }

            long total = 0;
            string previousId = null;
            bool canHashArmy = army.Identity != null;
            for (int index = 0; index < army.Stacks.Count; index++)
            {
                BattleTroopStack stack = army.Stacks[index];
                string stackPath = path + ".stacks[" + index + "]";
                if (stack == null)
                {
                    Add(diagnostics, "AL-BATTLE-ARMY-STACK-NULL", stackPath);
                    canHashArmy = false;
                    continue;
                }

                ValidateId(stack.TroopDefinitionId, stackPath + ".troopDefinitionId", diagnostics);
                if (!IsVersion(stack.TroopContentVersion))
                    Add(diagnostics, "AL-BATTLE-ARMY-TROOP-VERSION-INVALID", stackPath + ".troopContentVersion");
                if (!IsLowerSha256(stack.TroopDefinitionSha256))
                    Add(diagnostics, "AL-BATTLE-ARMY-TROOP-HASH-INVALID", stackPath + ".troopDefinitionSha256");
                if (stack.Count <= 0)
                    Add(diagnostics, "AL-BATTLE-ARMY-COUNT-INVALID", stackPath + ".count");
                if (previousId != null &&
                    string.CompareOrdinal(previousId, stack.TroopDefinitionId) >= 0)
                    Add(diagnostics, "AL-BATTLE-ARMY-STACKS-NONCANONICAL", stackPath + ".troopDefinitionId");
                previousId = stack.TroopDefinitionId;

                BattleTroopDefinition definition = FindDefinition(
                    request.Catalog,
                    stack.TroopDefinitionId);
                if (definition == null)
                    Add(diagnostics, "AL-BATTLE-ARMY-TROOP-UNKNOWN", stackPath + ".troopDefinitionId");
                else if (!string.Equals(
                             definition.Identity.ContentVersion,
                             stack.TroopContentVersion,
                             StringComparison.Ordinal) ||
                         !string.Equals(
                             definition.Identity.Sha256,
                             stack.TroopDefinitionSha256,
                             StringComparison.Ordinal))
                    Add(diagnostics, "AL-BATTLE-ARMY-TROOP-CATALOG-MISMATCH", stackPath);

                try
                {
                    total = checked(total + stack.Count);
                }
                catch (OverflowException)
                {
                    Add(diagnostics, "AL-BATTLE-ARMY-COUNT-OVERFLOW", path + ".stacks");
                }
            }

            if (total > BattleTechnicalLimits.MaximumArmyCount)
                Add(diagnostics, "AL-BATTLE-ARMY-COUNT-LIMIT", path + ".stacks");
            if (canHashArmy &&
                diagnostics.Count == armyDiagnosticStart &&
                !string.Equals(
                    army.Identity.Sha256,
                    BattleCanonicalHash.Army(army),
                    StringComparison.Ordinal))
                Add(diagnostics, "AL-BATTLE-ARMY-HASH-MISMATCH", path + ".identity.sha256");
        }

        private static void ValidateOpponent(
            BattleComputationRequest request,
            List<BattleDiagnostic> diagnostics)
        {
            BattleOpponentSnapshot opponent = request.Opponent;
            if (opponent == null)
            {
                Add(diagnostics, "AL-BATTLE-OPPONENT-NULL", "request.opponent");
                return;
            }

            bool bossBattle = request.BattleKind == BattleKind.Boss;
            if (opponent.Kind != BattleOpponentKind.Army &&
                opponent.Kind != BattleOpponentKind.Boss)
            {
                Add(diagnostics, "AL-BATTLE-OPPONENT-KIND-INVALID", "request.opponent.kind");
                return;
            }

            if (!bossBattle)
            {
                if (opponent.Kind != BattleOpponentKind.Army)
                {
                    Add(diagnostics, "AL-BATTLE-OPPONENT-KIND-MISMATCH", "request.opponent.kind");
                    return;
                }
                if (opponent.BossIdentity != null || opponent.BossPower != 0L)
                    Add(diagnostics, "AL-BATTLE-OPPONENT-BOSS-FORBIDDEN", "request.opponent");
                ValidateArmy(opponent.Army, request, "request.opponent.army", diagnostics);
                if (request.BattleKind == BattleKind.Pvp &&
                    request.AttackerArmy?.Identity != null &&
                    opponent.Army?.Identity != null &&
                    string.Equals(
                        request.AttackerArmy.Identity.Id,
                        opponent.Army.Identity.Id,
                        StringComparison.Ordinal))
                    Add(
                        diagnostics,
                        "AL-BATTLE-PVP-PARTICIPANT-SAME",
                        "request.opponent.army.identity.id");
                return;
            }

            int bossDiagnosticStart = diagnostics.Count;
            ValidateIdentity(
                opponent.BossIdentity,
                request,
                "request.opponent.bossIdentity",
                diagnostics);
            if (opponent.Kind == BattleOpponentKind.Army)
            {
                if (opponent.BossPower != 0L)
                    Add(diagnostics, "AL-BATTLE-OPPONENT-BOSS-ARMY-SHAPE-INVALID", "request.opponent");
                if (opponent.BossIdentity != null &&
                    diagnostics.Count == bossDiagnosticStart &&
                    !string.Equals(
                        opponent.BossIdentity.Sha256,
                        BattleCanonicalHash.BossDefinition(opponent.BossIdentity),
                        StringComparison.Ordinal))
                    Add(
                        diagnostics,
                        "AL-BATTLE-OPPONENT-BOSS-HASH-MISMATCH",
                        "request.opponent.bossIdentity.sha256");
                ValidateArmy(opponent.Army, request, "request.opponent.army", diagnostics);
            }
            else
            {
                if (opponent.Army != null || opponent.BossPower <= 0 ||
                    opponent.BossPower > BattleTechnicalLimits.MaximumFinalPower)
                    Add(diagnostics, "AL-BATTLE-OPPONENT-BOSS-SHAPE-INVALID", "request.opponent");
                if (opponent.BossIdentity != null &&
                    diagnostics.Count == bossDiagnosticStart &&
                    !string.Equals(
                        opponent.BossIdentity.Sha256,
                        BattleCanonicalHash.Boss(opponent.BossIdentity, opponent.BossPower),
                        StringComparison.Ordinal))
                    Add(diagnostics, "AL-BATTLE-OPPONENT-BOSS-HASH-MISMATCH", "request.opponent.bossIdentity.sha256");
            }
        }

        private static void ValidateContext(
            BattleComputationRequest request,
            List<BattleDiagnostic> diagnostics)
        {
            BattleContextSnapshot context = request.Context;
            if (context == null)
            {
                Add(diagnostics, "AL-BATTLE-CONTEXT-NULL", "request.context");
                return;
            }

            int contextDiagnosticStart = diagnostics.Count;
            ValidateIdentity(context.Identity, request, "request.context.identity", diagnostics);
            ValidateId(context.EncounterId, "request.context.encounterId", diagnostics);
            ValidateTerrain(context.Terrain, request, diagnostics);
            if (context.AttackerRealm < BattleRealm.Stonehold ||
                context.AttackerRealm > BattleRealm.Umbral)
                Add(diagnostics, "AL-BATTLE-CONTEXT-ATTACKER-REALM-INVALID", "request.context.attackerRealm");

            BattleRealmContextKind expectedKind;
            if (request.BattleKind == BattleKind.Boss)
                expectedKind = BattleRealmContextKind.BossEncounter;
            else if (request.BattleKind == BattleKind.Pve ||
                     (request.BattleKind == BattleKind.Warzone &&
                      context.OpponentRealm == BattleRealm.Neutral))
                expectedKind = BattleRealmContextKind.NeutralEncounter;
            else
                expectedKind = BattleRealmContextKind.RealmVersusRealm;
            if (context.ContextKind != expectedKind)
                Add(diagnostics, "AL-BATTLE-CONTEXT-KIND-MISMATCH", "request.context.contextKind");
            if (expectedKind == BattleRealmContextKind.RealmVersusRealm)
            {
                if (context.OpponentRealm < BattleRealm.Stonehold ||
                    context.OpponentRealm > BattleRealm.Umbral)
                    Add(diagnostics, "AL-BATTLE-CONTEXT-OPPONENT-REALM-INVALID", "request.context.opponentRealm");
            }
            else if (context.OpponentRealm != BattleRealm.Neutral)
                Add(diagnostics, "AL-BATTLE-CONTEXT-NEUTRAL-REALM-REQUIRED", "request.context.opponentRealm");

            ValidateModifier(
                context.AttackerModifiers,
                request,
                "request.context.attackerModifiers",
                diagnostics);
            ValidateModifier(
                context.OpponentModifiers,
                request,
                "request.context.opponentModifiers",
                diagnostics);
            if (context.Identity != null &&
                diagnostics.Count == contextDiagnosticStart &&
                !string.Equals(
                    context.Identity.Sha256,
                    BattleCanonicalHash.Context(context),
                    StringComparison.Ordinal))
                Add(diagnostics, "AL-BATTLE-CONTEXT-HASH-MISMATCH", "request.context.identity.sha256");
        }

        private static void ValidateTerrain(
            BattleTerrainSnapshot terrain,
            BattleComputationRequest request,
            List<BattleDiagnostic> diagnostics)
        {
            if (terrain == null)
            {
                Add(diagnostics, "AL-BATTLE-TERRAIN-NULL", "request.context.terrain");
                return;
            }

            int terrainDiagnosticStart = diagnostics.Count;
            ValidateIdentity(
                terrain.Identity,
                request,
                "request.context.terrain.identity",
                diagnostics);
            if (terrain.Profile < BattleTerrainProfile.Neutral ||
                terrain.Profile > BattleTerrainProfile.VolcanicShadow)
                Add(
                    diagnostics,
                    "AL-BATTLE-TERRAIN-PROFILE-INVALID",
                    "request.context.terrain.profile");
            if (terrain.Identity != null &&
                !string.Equals(
                    terrain.Identity.Id,
                    BattleTerrainProfiles.GetId(terrain.Profile),
                    StringComparison.Ordinal))
                Add(
                    diagnostics,
                    "AL-BATTLE-TERRAIN-ID-MISMATCH",
                    "request.context.terrain.identity.id");
            if (terrain.Identity != null &&
                diagnostics.Count == terrainDiagnosticStart &&
                !string.Equals(
                    terrain.Identity.Sha256,
                    BattleCanonicalHash.Terrain(terrain),
                    StringComparison.Ordinal))
                Add(
                    diagnostics,
                    "AL-BATTLE-TERRAIN-HASH-MISMATCH",
                    "request.context.terrain.identity.sha256");
        }

        private static void ValidateModifier(
            BattleModifierSnapshot modifier,
            BattleComputationRequest request,
            string path,
            List<BattleDiagnostic> diagnostics)
        {
            if (modifier == null)
            {
                Add(diagnostics, "AL-BATTLE-MODIFIER-NULL", path);
                return;
            }

            int modifierDiagnosticStart = diagnostics.Count;
            ValidateIdentity(modifier.Identity, request, path + ".identity", diagnostics);
            if (modifier.MoraleMicros < 650_000L || modifier.MoraleMicros > 1_300_000L)
                Add(diagnostics, "AL-BATTLE-MODIFIER-MORALE-INVALID", path + ".moraleMicros");
            if (!IsBoundedMultiplier(modifier.ResearchMicros) ||
                !IsBoundedMultiplier(modifier.CommanderMicros))
                Add(diagnostics, "AL-BATTLE-MODIFIER-MULTIPLIER-INVALID", path);
            if (modifier.Identity != null &&
                diagnostics.Count == modifierDiagnosticStart &&
                !string.Equals(
                    modifier.Identity.Sha256,
                    BattleCanonicalHash.Modifier(modifier),
                    StringComparison.Ordinal))
                Add(diagnostics, "AL-BATTLE-MODIFIER-HASH-MISMATCH", path + ".identity.sha256");
        }

        private static void ValidateRules(
            BattleComputationRequest request,
            List<BattleDiagnostic> diagnostics)
        {
            BattleRulesProfile rules = request.Rules;
            if (rules == null)
            {
                Add(diagnostics, "AL-BATTLE-RULES-NULL", "request.rules");
                return;
            }

            int rulesDiagnosticStart = diagnostics.Count;
            ValidateIdentity(rules.Identity, request, "request.rules.identity", diagnostics);
            if (rules.Identity == null ||
                !string.Equals(
                    rules.Identity.Id,
                    BattleTechnicalLimits.MigrationRulesProfileId,
                    StringComparison.Ordinal) ||
                rules.MaximumRounds != 20 ||
                rules.MinimumDamageRateMicros != 80_000L ||
                rules.MaximumDamageRateExclusiveMicros != 160_000L)
                Add(diagnostics, "AL-BATTLE-RULES-ROUND-TUNING-INVALID", "request.rules");
            if (rules.CounterMultiplierMicros != 180_000L ||
                rules.BossSiegeMultiplierMicros != 120_000L ||
                rules.WinnerCasualtyPressureMicros != 380_000L ||
                rules.LoserCasualtyPressureMicros != 700_000L ||
                rules.LoserCasualtyFloorMicros != 80_000L ||
                rules.WinnerKilledShareMicros != 350_000L ||
                rules.LoserKilledShareMicros != 550_000L ||
                rules.InfantryVulnerabilityMicros != 1_000_000L ||
                rules.CavalryVulnerabilityMicros != 920_000L ||
                rules.RangedVulnerabilityMicros != 1_080_000L ||
                rules.SiegeVulnerabilityMicros != 1_180_000L)
                Add(diagnostics, "AL-BATTLE-RULES-MULTIPLIER-INVALID", "request.rules");

            if (rules.Identity != null &&
                diagnostics.Count == rulesDiagnosticStart &&
                !string.Equals(
                    rules.Identity.Sha256,
                    BattleCanonicalHash.Rules(rules),
                    StringComparison.Ordinal))
                Add(diagnostics, "AL-BATTLE-RULES-HASH-MISMATCH", "request.rules.identity.sha256");
        }

        private static void ValidateRewards(
            BattleComputationRequest request,
            List<BattleDiagnostic> diagnostics)
        {
            BattleRewardProfile rewards = request.Rewards;
            if (rewards == null)
            {
                Add(diagnostics, "AL-BATTLE-REWARDS-NULL", "request.rewards");
                return;
            }

            int rewardDiagnosticStart = diagnostics.Count;
            ValidateIdentity(rewards.Identity, request, "request.rewards.identity", diagnostics);
            if (rewards.Identity == null ||
                !string.Equals(
                    rewards.Identity.Id,
                    BattleTechnicalLimits.MigrationRewardProfileId,
                    StringComparison.Ordinal) ||
                rewards.WinCreditsBase != 12 ||
                rewards.LossCreditsBase != 4 ||
                rewards.PowerCreditDivisor != 120 ||
                rewards.PowerCreditMaximum != 40 ||
                rewards.RoundsCreditDivisor != 2 ||
                rewards.RoundsCreditMaximum != 10 ||
                rewards.WinFoodBase != 40 ||
                rewards.WinFoodRandomExclusive != 26 ||
                rewards.WinGoldBase != 12 ||
                rewards.WinGoldRandomExclusive != 14 ||
                rewards.WinXpDivisor != 18 ||
                rewards.LossXpDivisor != 36 ||
                rewards.WinXpMinimum != 8 ||
                rewards.LossXpMinimum != 3)
                Add(diagnostics, "AL-BATTLE-REWARDS-TUNING-INVALID", "request.rewards");
            if (rewards.Identity != null &&
                diagnostics.Count == rewardDiagnosticStart &&
                !string.Equals(
                    rewards.Identity.Sha256,
                    BattleCanonicalHash.Rewards(rewards),
                    StringComparison.Ordinal))
                Add(diagnostics, "AL-BATTLE-REWARDS-HASH-MISMATCH", "request.rewards.identity.sha256");
        }

        private static void ValidateIdentity(
            BattleSnapshotIdentity identity,
            BattleComputationRequest request,
            string path,
            List<BattleDiagnostic> diagnostics)
        {
            if (identity == null)
            {
                Add(diagnostics, "AL-BATTLE-SNAPSHOT-IDENTITY-NULL", path);
                return;
            }

            ValidateId(identity.Id, path + ".id", diagnostics);
            if (!string.Equals(
                    identity.SchemaVersion,
                    BattleTechnicalLimits.SupportedSchemaVersion,
                    StringComparison.Ordinal))
                Add(diagnostics, "AL-BATTLE-SNAPSHOT-SCHEMA-UNSUPPORTED", path + ".schemaVersion");
            if (!IsVersion(identity.ContentVersion))
                Add(diagnostics, "AL-BATTLE-SNAPSHOT-CONTENT-VERSION-INVALID", path + ".contentVersion");
            if (!IsBoundedText(
                    identity.SourceRevision,
                    BattleTechnicalLimits.MaximumSourceRevisionUtf8Bytes))
                Add(diagnostics, "AL-BATTLE-SNAPSHOT-REVISION-INVALID", path + ".sourceRevision");
            if (!IsLowerSha256(identity.Sha256))
                Add(diagnostics, "AL-BATTLE-SNAPSHOT-HASH-INVALID", path + ".sha256");
            if (!string.Equals(identity.CatalogSetId, request.CatalogSetId, StringComparison.Ordinal))
                Add(diagnostics, "AL-BATTLE-SNAPSHOT-CATALOG-MISMATCH", path + ".catalogSetId");
        }

        private static BattleTroopDefinition FindDefinition(
            BattleCatalogSnapshot catalog,
            string id)
        {
            for (int index = 0; index < catalog.TroopDefinitions.Count; index++)
            {
                BattleTroopDefinition definition = catalog.TroopDefinitions[index];
                if (definition?.Identity != null &&
                    string.Equals(definition.Identity.Id, id, StringComparison.Ordinal))
                    return definition;
            }

            return null;
        }

        private static int ExpectedBasePower(BattleTroopArchetype archetype)
        {
            switch (archetype)
            {
                case BattleTroopArchetype.Infantry: return 10;
                case BattleTroopArchetype.Cavalry: return 15;
                case BattleTroopArchetype.Ranged: return 12;
                case BattleTroopArchetype.Siege: return 20;
                default: return -1;
            }
        }

        private static bool IsKnownArchetype(BattleTroopArchetype archetype)
        {
            return archetype >= BattleTroopArchetype.Infantry &&
                   archetype <= BattleTroopArchetype.Siege;
        }

        private static bool TryExpectedTypeId(BattleKind kind, out string typeId)
        {
            switch (kind)
            {
                case BattleKind.Pve:
                    typeId = BattleTechnicalLimits.PveBattleTypeId;
                    return true;
                case BattleKind.Pvp:
                    typeId = BattleTechnicalLimits.PvpBattleTypeId;
                    return true;
                case BattleKind.Boss:
                    typeId = BattleTechnicalLimits.BossBattleTypeId;
                    return true;
                case BattleKind.Warzone:
                    typeId = BattleTechnicalLimits.WarzoneBattleTypeId;
                    return true;
                default:
                    typeId = null;
                    return false;
            }
        }

        private static bool IsBounedTextNoControls(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                    return false;
                if (char.IsHighSurrogate(value[index]))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                        return false;
                    index++;
                }
                else if (char.IsLowSurrogate(value[index]))
                    return false;
            }

            return true;
        }

        private static bool IsBoundedText(string value, int maximumUtf8Bytes)
        {
            return IsBounedTextNoControls(value) &&
                   Encoding.UTF8.GetByteCount(value) <= maximumUtf8Bytes;
        }

        private static bool IsBoundedMultiplier(long value)
        {
            return value >= 500_000L && value <= 2_000_000L;
        }

        private static void ValidateId(
            string value,
            string path,
            List<BattleDiagnostic> diagnostics)
        {
            if (!IsStableId(value))
                Add(diagnostics, "AL-BATTLE-ID-INVALID", path);
        }

        private static void Add(
            List<BattleDiagnostic> diagnostics,
            string code,
            string path)
        {
            diagnostics.Add(new BattleDiagnostic(code, path));
        }

        private static bool HasCodePrefix(
            IEnumerable<BattleDiagnostic> diagnostics,
            string prefix)
        {
            foreach (BattleDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Code.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static BattleValidationResult Result(
            BattleComputationStatus status,
            IEnumerable<BattleDiagnostic> diagnostics)
        {
            return new BattleValidationResult(status, diagnostics);
        }
    }
}
