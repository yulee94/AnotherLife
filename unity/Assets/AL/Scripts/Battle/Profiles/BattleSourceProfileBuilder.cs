using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using AL.Battle.Validation;

namespace AL.Battle.Profiles
{
    /// <summary>
    /// Converts immutable authority-owned state into the pure simulator contract.
    /// It never accepts presentation models, live services, or mutable save rows.
    /// </summary>
    public static class BattleSourceProfileBuilder
    {
        public static BattleSourceProfileResult Build(BattleAuthoritativeSourceState state)
        {
            var diagnostics = new List<BattleDiagnostic>();
            if (state == null)
            {
                Add(diagnostics, "AL-BATTLE-SOURCE-STATE-MISSING", "source");
                return Failure(diagnostics);
            }

            if (state.Configuration == null)
                Add(diagnostics, "AL-BATTLE-SOURCE-CONFIGURATION-MISSING", "source.configuration");
            if (state.Opponent == null)
                Add(diagnostics, "AL-BATTLE-SOURCE-OPPONENT-MISSING", "source.opponent");
            if (state.Player == null)
                Add(diagnostics, "AL-BATTLE-SOURCE-PLAYER-MISSING", "source.player");
            if (diagnostics.Count > 0)
                return Failure(diagnostics);

            BattleConfigurationSourceProfile configuration = state.Configuration;
            ValidateConfiguration(configuration, diagnostics);
            ValidateParticipant(
                state.Player,
                configuration,
                "source.player",
                diagnostics);
            ValidateOpponent(state.Opponent, configuration, diagnostics);
            if (diagnostics.Count > 0)
                return Failure(diagnostics);

            BattleCatalogSnapshot catalog = configuration.Catalog;
            BattleArmySnapshot attacker = CreateArmy(state.Player, configuration, catalog);
            BattleOpponentSnapshot opponent = CreateOpponent(
                state.Opponent,
                configuration,
                catalog);
            BattleModifierSnapshot attackerModifiers = CreateModifier(
                state.Player,
                configuration,
                "modifier.attacker");
            BattleModifierSnapshot opponentModifiers =
                state.Opponent.Kind == BattleOpponentKind.Army
                    ? CreateModifier(
                        state.Opponent.ArmyParticipant,
                        configuration,
                        "modifier.opponent")
                    : BattleMigrationProfiles.CreateModifier(
                        "modifier.opponent.boss",
                        configuration.CatalogSetId,
                        configuration.Catalog.Identity.ContentVersion,
                        configuration.Catalog.Identity.SourceRevision,
                        BattleTechnicalLimits.MicrosPerUnit,
                        BattleTechnicalLimits.MicrosPerUnit,
                        BattleTechnicalLimits.MicrosPerUnit);
            BattleContextSnapshot context = BattleMigrationProfiles.CreateContext(
                configuration.ContextId,
                configuration.CatalogSetId,
                configuration.Catalog.Identity.ContentVersion,
                configuration.Catalog.Identity.SourceRevision,
                configuration.EncounterId,
                configuration.ContextKind,
                state.Player.Realm,
                ResolveOpponentRealm(state.Opponent),
                configuration.TerrainProfile,
                attackerModifiers,
                opponentModifiers);
            var request = new BattleComputationRequest(
                configuration.GameId,
                configuration.CatalogSetId,
                state.Player.Identity.Id,
                configuration.BattleRequestId,
                configuration.BattleId,
                configuration.BattleResultId,
                configuration.ExpectedResultConsumerId,
                configuration.ExecutionMode,
                configuration.BattleKind,
                configuration.BattleTypeId,
                configuration.DeterminismVersion,
                configuration.SeedHex,
                catalog,
                attacker,
                opponent,
                context,
                configuration.Rules,
                configuration.Rewards);

            BattleValidationResult validation = BattleRequestValidator.Validate(request);
            if (!validation.IsValid)
            {
                for (int index = 0; index < validation.Diagnostics.Count; index++)
                {
                    BattleDiagnostic diagnostic = validation.Diagnostics[index];
                    Add(
                        diagnostics,
                        "AL-BATTLE-SOURCE-REQUEST-" + diagnostic.Code,
                        diagnostic.FieldPath);
                }
                return Failure(diagnostics);
            }

            return new BattleSourceProfileResult(request, Array.Empty<BattleDiagnostic>());
        }

        private static void ValidateConfiguration(
            BattleConfigurationSourceProfile configuration,
            List<BattleDiagnostic> diagnostics)
        {
            ValidateId(configuration.GameId, "source.configuration.gameId", diagnostics);
            ValidateId(configuration.CatalogSetId, "source.configuration.catalogSetId", diagnostics);
            ValidateId(configuration.BattleRequestId, "source.configuration.battleRequestId", diagnostics);
            ValidateId(configuration.BattleId, "source.configuration.battleId", diagnostics);
            ValidateId(configuration.BattleResultId, "source.configuration.battleResultId", diagnostics);
            ValidateId(configuration.ExpectedResultConsumerId, "source.configuration.expectedResultConsumerId", diagnostics);
            ValidateId(configuration.BattleTypeId, "source.configuration.battleTypeId", diagnostics);
            ValidateId(configuration.ContextId, "source.configuration.contextId", diagnostics);
            ValidateId(configuration.EncounterId, "source.configuration.encounterId", diagnostics);
            if (!BattleRequestValidator.IsVersion(configuration.DeterminismVersion))
                Add(diagnostics, "AL-BATTLE-SOURCE-DETERMINISM-VERSION-INVALID", "source.configuration.determinismVersion");
            if (!BattleRequestValidator.IsLowerSha256(configuration.SeedHex))
                Add(diagnostics, "AL-BATTLE-SOURCE-SEED-INVALID", "source.configuration.seedHex");
            if (configuration.Catalog == null)
                Add(diagnostics, "AL-BATTLE-SOURCE-CATALOG-MISSING", "source.configuration.catalog");
            if (configuration.Rules == null)
                Add(diagnostics, "AL-BATTLE-SOURCE-RULES-MISSING", "source.configuration.rules");
            if (configuration.Rewards == null)
                Add(diagnostics, "AL-BATTLE-SOURCE-REWARDS-MISSING", "source.configuration.rewards");
            if (configuration.Catalog != null)
            {
                ValidateIdentity(
                    configuration.Catalog.Identity,
                    configuration,
                    "source.configuration.catalog.identity",
                    diagnostics);
                if (configuration.Catalog.Identity != null &&
                    !string.Equals(
                        configuration.Catalog.Identity.Id,
                        configuration.GameId + ".battle_catalog",
                        StringComparison.Ordinal))
                    Add(diagnostics, "AL-BATTLE-SOURCE-CATALOG-GAME-MISMATCH", "source.configuration.catalog.identity.id");
            }
            if (configuration.Rules != null)
                ValidateIdentity(configuration.Rules.Identity, configuration, "source.configuration.rules.identity", diagnostics);
            if (configuration.Rewards != null)
                ValidateIdentity(configuration.Rewards.Identity, configuration, "source.configuration.rewards.identity", diagnostics);
        }

        private static void ValidateOpponent(
            BattleOpponentSourceProfile opponent,
            BattleConfigurationSourceProfile configuration,
            List<BattleDiagnostic> diagnostics)
        {
            if (opponent.Kind == BattleOpponentKind.Army)
            {
                if (opponent.ArmyParticipant == null || opponent.BossIdentity != null || opponent.BossPower != 0L)
                {
                    Add(diagnostics, "AL-BATTLE-SOURCE-OPPONENT-SHAPE-INVALID", "source.opponent");
                    return;
                }
                ValidateParticipant(opponent.ArmyParticipant, configuration, "source.opponent.army", diagnostics);
                return;
            }

            if (opponent.Kind != BattleOpponentKind.Boss ||
                opponent.ArmyParticipant != null ||
                opponent.BossPower <= 0L ||
                opponent.BossPower > BattleTechnicalLimits.MaximumFinalPower)
            {
                Add(diagnostics, "AL-BATTLE-SOURCE-OPPONENT-SHAPE-INVALID", "source.opponent");
                return;
            }
            ValidateIdentity(opponent.BossIdentity, configuration, "source.opponent.bossIdentity", diagnostics);
        }

        private static void ValidateParticipant(
            BattleParticipantSourceProfile participant,
            BattleConfigurationSourceProfile configuration,
            string path,
            List<BattleDiagnostic> diagnostics)
        {
            if (participant == null)
            {
                Add(diagnostics, "AL-BATTLE-SOURCE-PARTICIPANT-MISSING", path);
                return;
            }

            ValidateIdentity(participant.Identity, configuration, path + ".identity", diagnostics);
            ValidateIdentity(participant.ArmyIdentity, configuration, path + ".armyIdentity", diagnostics);
            if (participant.Realm < BattleRealm.Stonehold || participant.Realm > BattleRealm.Umbral)
                Add(diagnostics, "AL-BATTLE-SOURCE-REALM-INVALID", path + ".realm");
            if (participant.Equipment == null)
                Add(diagnostics, "AL-BATTLE-SOURCE-EQUIPMENT-MISSING", path + ".equipment");
            else
            {
                ValidateIdentity(participant.Equipment.Identity, configuration, path + ".equipment.identity", diagnostics);
                if (participant.Equipment.EquippedDefinitions.Count > BattleTechnicalLimits.MaximumCollectionCount)
                    Add(diagnostics, "AL-BATTLE-SOURCE-EQUIPMENT-LIMIT", path + ".equipment.equippedDefinitions");
                var equipmentIds = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < participant.Equipment.EquippedDefinitions.Count; index++)
                {
                    BattleSnapshotIdentity identity = participant.Equipment.EquippedDefinitions[index];
                    ValidateIdentity(identity, configuration, path + ".equipment.equippedDefinitions[" + index + "]", diagnostics);
                    if (identity != null && !equipmentIds.Add(identity.Id))
                        Add(diagnostics, "AL-BATTLE-SOURCE-EQUIPMENT-DUPLICATE", path + ".equipment.equippedDefinitions[" + index + "].id");
                }
                if (!IsMultiplier(participant.Equipment.CommanderMultiplierMicros))
                    Add(diagnostics, "AL-BATTLE-SOURCE-COMMANDER-MULTIPLIER-INVALID", path + ".equipment.commanderMultiplierMicros");
            }

            if (participant.Progression == null)
                Add(diagnostics, "AL-BATTLE-SOURCE-PROGRESSION-MISSING", path + ".progression");
            else
            {
                ValidateIdentity(participant.Progression.Identity, configuration, path + ".progression.identity", diagnostics);
                if (participant.Progression.MoraleMicros < 650_000L || participant.Progression.MoraleMicros > 1_300_000L)
                    Add(diagnostics, "AL-BATTLE-SOURCE-MORALE-INVALID", path + ".progression.moraleMicros");
                if (!IsMultiplier(participant.Progression.ResearchMultiplierMicros))
                    Add(diagnostics, "AL-BATTLE-SOURCE-RESEARCH-MULTIPLIER-INVALID", path + ".progression.researchMultiplierMicros");
            }

            if (participant.Troops.Count == 0 || participant.Troops.Count > BattleTechnicalLimits.MaximumCollectionCount)
            {
                Add(diagnostics, "AL-BATTLE-SOURCE-TROOPS-INVALID", path + ".troops");
                return;
            }
            var troopIds = new HashSet<string>(StringComparer.Ordinal);
            long total = 0L;
            for (int index = 0; index < participant.Troops.Count; index++)
            {
                BattleSourceTroopCount troop = participant.Troops[index];
                string troopPath = path + ".troops[" + index + "]";
                if (troop == null)
                {
                    Add(diagnostics, "AL-BATTLE-SOURCE-TROOP-MISSING", troopPath);
                    continue;
                }
                if (!BattleRequestValidator.IsStableId(troop.TroopDefinitionId))
                    Add(diagnostics, "AL-BATTLE-SOURCE-TROOP-ID-INVALID", troopPath + ".troopDefinitionId");
                else if (!troopIds.Add(troop.TroopDefinitionId))
                    Add(diagnostics, "AL-BATTLE-SOURCE-TROOP-DUPLICATE", troopPath + ".troopDefinitionId");
                else if (configuration.Catalog != null &&
                    !configuration.Catalog.TroopDefinitions.Any(definition =>
                        definition != null && definition.Identity != null &&
                        string.Equals(definition.Identity.Id, troop.TroopDefinitionId, StringComparison.Ordinal)))
                    Add(diagnostics, "AL-BATTLE-SOURCE-TROOP-UNKNOWN", troopPath + ".troopDefinitionId");
                if (troop.Count <= 0L)
                    Add(diagnostics, "AL-BATTLE-SOURCE-TROOP-COUNT-INVALID", troopPath + ".count");
                try { total = checked(total + troop.Count); }
                catch (OverflowException) { Add(diagnostics, "AL-BATTLE-SOURCE-TROOP-COUNT-OVERFLOW", path + ".troops"); }
            }
            if (total > BattleTechnicalLimits.MaximumArmyCount)
                Add(diagnostics, "AL-BATTLE-SOURCE-TROOP-COUNT-LIMIT", path + ".troops");
        }

        private static BattleArmySnapshot CreateArmy(
            BattleParticipantSourceProfile participant,
            BattleConfigurationSourceProfile configuration,
            BattleCatalogSnapshot catalog)
        {
            return BattleMigrationProfiles.CreateArmy(
                participant.ArmyIdentity.Id,
                configuration.CatalogSetId,
                participant.ArmyIdentity.ContentVersion,
                participant.ArmyIdentity.SourceRevision,
                catalog,
                participant.Troops.Select(troop =>
                    new KeyValuePair<string, long>(troop.TroopDefinitionId, troop.Count)));
        }

        private static BattleOpponentSnapshot CreateOpponent(
            BattleOpponentSourceProfile source,
            BattleConfigurationSourceProfile configuration,
            BattleCatalogSnapshot catalog)
        {
            if (source.Kind == BattleOpponentKind.Army)
                return new BattleOpponentSnapshot(
                    BattleOpponentKind.Army,
                    CreateArmy(source.ArmyParticipant, configuration, catalog),
                    null,
                    0L);
            BattleSnapshotIdentity provisionalIdentity = source.BossIdentity.WithSha256(new string('0', 64));
            return new BattleOpponentSnapshot(
                BattleOpponentKind.Boss,
                null,
                provisionalIdentity.WithSha256(BattleCanonicalHash.Boss(provisionalIdentity, source.BossPower)),
                source.BossPower);
        }

        private static BattleModifierSnapshot CreateModifier(
            BattleParticipantSourceProfile participant,
            BattleConfigurationSourceProfile configuration,
            string id)
        {
            return BattleMigrationProfiles.CreateModifier(
                id,
                configuration.CatalogSetId,
                participant.Progression.Identity.ContentVersion,
                ModifierSourceRevision(participant),
                participant.Progression.MoraleMicros,
                participant.Progression.ResearchMultiplierMicros,
                participant.Equipment.CommanderMultiplierMicros);
        }

        private static string ModifierSourceRevision(BattleParticipantSourceProfile participant)
        {
            var fields = new List<string>
            {
                participant.Progression.Identity.Id,
                participant.Progression.Identity.ContentVersion,
                participant.Progression.Identity.SourceRevision,
                participant.Progression.Identity.Sha256,
                participant.Equipment.Identity.Id,
                participant.Equipment.Identity.ContentVersion,
                participant.Equipment.Identity.SourceRevision,
                participant.Equipment.Identity.Sha256
            };
            fields.AddRange(
                participant.Equipment.EquippedDefinitions
                    .OrderBy(identity => identity.Id, StringComparer.Ordinal)
                    .Select(identity => identity.Id + ":" + identity.ContentVersion + ":" + identity.Sha256));
            byte[] bytes = Encoding.UTF8.GetBytes(string.Join("|", fields));
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(bytes))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static BattleRealm ResolveOpponentRealm(BattleOpponentSourceProfile opponent)
        {
            return opponent.Kind == BattleOpponentKind.Army
                ? opponent.ArmyParticipant.Realm
                : BattleRealm.Neutral;
        }

        private static void ValidateIdentity(
            BattleSnapshotIdentity identity,
            BattleConfigurationSourceProfile configuration,
            string path,
            List<BattleDiagnostic> diagnostics)
        {
            if (identity == null)
            {
                Add(diagnostics, "AL-BATTLE-SOURCE-IDENTITY-MISSING", path);
                return;
            }
            ValidateId(identity.Id, path + ".id", diagnostics);
            if (!string.Equals(identity.SchemaVersion, BattleTechnicalLimits.SupportedSchemaVersion, StringComparison.Ordinal))
                Add(diagnostics, "AL-BATTLE-SOURCE-SCHEMA-UNSUPPORTED", path + ".schemaVersion");
            if (!BattleRequestValidator.IsVersion(identity.ContentVersion))
                Add(diagnostics, "AL-BATTLE-SOURCE-CONTENT-VERSION-INVALID", path + ".contentVersion");
            if (!BattleRequestValidator.IsStableId(identity.SourceRevision))
                Add(diagnostics, "AL-BATTLE-SOURCE-REVISION-INVALID", path + ".sourceRevision");
            if (!BattleRequestValidator.IsLowerSha256(identity.Sha256))
                Add(diagnostics, "AL-BATTLE-SOURCE-HASH-INVALID", path + ".sha256");
            if (!string.Equals(identity.CatalogSetId, configuration.CatalogSetId, StringComparison.Ordinal))
                Add(diagnostics, "AL-BATTLE-SOURCE-CATALOG-MISMATCH", path + ".catalogSetId");
        }

        private static bool IsMultiplier(long value)
        {
            return value >= 500_000L && value <= 2_000_000L;
        }

        private static void ValidateId(string value, string path, List<BattleDiagnostic> diagnostics)
        {
            if (!BattleRequestValidator.IsStableId(value))
                Add(diagnostics, "AL-BATTLE-SOURCE-ID-INVALID", path);
        }

        private static BattleSourceProfileResult Failure(List<BattleDiagnostic> diagnostics)
        {
            return new BattleSourceProfileResult(
                null,
                diagnostics
                    .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.FieldPath, StringComparer.Ordinal));
        }

        private static void Add(List<BattleDiagnostic> diagnostics, string code, string path)
        {
            diagnostics.Add(new BattleDiagnostic(code, path));
        }
    }
}
