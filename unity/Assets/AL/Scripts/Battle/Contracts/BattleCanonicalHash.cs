using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace AL.Battle.Contracts
{
    public static class BattleCanonicalHash
    {
        public static string TroopDefinition(BattleTroopDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return Hash(writer =>
            {
                WriteIdentityCore(writer, definition.Identity);
                writer.WriteInt32((int)definition.Archetype);
                writer.WriteInt32(definition.BasePower);
            });
        }

        public static string Catalog(BattleCatalogSnapshot catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return Hash(writer =>
            {
                WriteIdentityCore(writer, catalog.Identity);
                writer.WriteInt32(catalog.TroopDefinitions.Count);
                for (int index = 0; index < catalog.TroopDefinitions.Count; index++)
                {
                    BattleTroopDefinition definition = catalog.TroopDefinitions[index];
                    writer.WriteString(definition.Identity.Id);
                    writer.WriteString(definition.Identity.ContentVersion);
                    writer.WriteString(definition.Identity.Sha256);
                    writer.WriteInt32((int)definition.Archetype);
                    writer.WriteInt32(definition.BasePower);
                }
            });
        }

        public static string Army(BattleArmySnapshot army)
        {
            if (army == null) throw new ArgumentNullException(nameof(army));
            return Hash(writer =>
            {
                WriteIdentityCore(writer, army.Identity);
                writer.WriteInt32(army.Stacks.Count);
                for (int index = 0; index < army.Stacks.Count; index++)
                {
                    BattleTroopStack stack = army.Stacks[index];
                    writer.WriteString(stack.TroopDefinitionId);
                    writer.WriteString(stack.TroopContentVersion);
                    writer.WriteString(stack.TroopDefinitionSha256);
                    writer.WriteInt64(stack.Count);
                }
            });
        }

        public static string Boss(BattleSnapshotIdentity identity, long bossPower)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            return Hash(writer =>
            {
                WriteIdentityCore(writer, identity);
                writer.WriteInt64(bossPower);
            });
        }

        public static string BossDefinition(BattleSnapshotIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            return Hash(writer => WriteIdentityCore(writer, identity));
        }

        public static string Modifier(BattleModifierSnapshot modifier)
        {
            if (modifier == null) throw new ArgumentNullException(nameof(modifier));
            return Hash(writer =>
            {
                WriteIdentityCore(writer, modifier.Identity);
                writer.WriteInt64(modifier.MoraleMicros);
                writer.WriteInt64(modifier.ResearchMicros);
                writer.WriteInt64(modifier.CommanderMicros);
            });
        }

        public static string Terrain(BattleTerrainSnapshot terrain)
        {
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            return Hash(writer =>
            {
                WriteIdentityCore(writer, terrain.Identity);
                writer.WriteInt32((int)terrain.Profile);
            });
        }

        public static string BossArmy(
            BattleSnapshotIdentity bossIdentity,
            BattleArmySnapshot army)
        {
            if (bossIdentity == null) throw new ArgumentNullException(nameof(bossIdentity));
            if (army == null) throw new ArgumentNullException(nameof(army));
            return Hash(writer =>
            {
                WriteSnapshotReference(writer, bossIdentity);
                WriteSnapshotReference(writer, army.Identity);
            });
        }

        public static string Context(BattleContextSnapshot context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return Hash(writer =>
            {
                WriteIdentityCore(writer, context.Identity);
                writer.WriteString(context.EncounterId);
                writer.WriteInt32((int)context.ContextKind);
                writer.WriteInt32((int)context.AttackerRealm);
                writer.WriteInt32((int)context.OpponentRealm);
                WriteSnapshotReference(writer, context.Terrain?.Identity);
                WriteSnapshotReference(writer, context.AttackerModifiers?.Identity);
                WriteSnapshotReference(writer, context.OpponentModifiers?.Identity);
            });
        }

        public static string Rules(BattleRulesProfile rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            return Hash(writer =>
            {
                WriteIdentityCore(writer, rules.Identity);
                writer.WriteInt32(rules.MaximumRounds);
                writer.WriteInt64(rules.MinimumDamageRateMicros);
                writer.WriteInt64(rules.MaximumDamageRateExclusiveMicros);
                writer.WriteInt64(rules.CounterMultiplierMicros);
                writer.WriteInt64(rules.BossSiegeMultiplierMicros);
                writer.WriteInt64(rules.WinnerCasualtyPressureMicros);
                writer.WriteInt64(rules.LoserCasualtyPressureMicros);
                writer.WriteInt64(rules.LoserCasualtyFloorMicros);
                writer.WriteInt64(rules.WinnerKilledShareMicros);
                writer.WriteInt64(rules.LoserKilledShareMicros);
                writer.WriteInt64(rules.InfantryVulnerabilityMicros);
                writer.WriteInt64(rules.CavalryVulnerabilityMicros);
                writer.WriteInt64(rules.RangedVulnerabilityMicros);
                writer.WriteInt64(rules.SiegeVulnerabilityMicros);
            });
        }

        public static string Rewards(BattleRewardProfile rewards)
        {
            if (rewards == null) throw new ArgumentNullException(nameof(rewards));
            return Hash(writer =>
            {
                WriteIdentityCore(writer, rewards.Identity);
                writer.WriteInt32(rewards.WinCreditsBase);
                writer.WriteInt32(rewards.LossCreditsBase);
                writer.WriteInt32(rewards.PowerCreditDivisor);
                writer.WriteInt32(rewards.PowerCreditMaximum);
                writer.WriteInt32(rewards.RoundsCreditDivisor);
                writer.WriteInt32(rewards.RoundsCreditMaximum);
                writer.WriteInt32(rewards.WinFoodBase);
                writer.WriteInt32(rewards.WinFoodRandomExclusive);
                writer.WriteInt32(rewards.WinGoldBase);
                writer.WriteInt32(rewards.WinGoldRandomExclusive);
                writer.WriteInt32(rewards.WinXpDivisor);
                writer.WriteInt32(rewards.LossXpDivisor);
                writer.WriteInt32(rewards.WinXpMinimum);
                writer.WriteInt32(rewards.LossXpMinimum);
            });
        }

        public static string Result(BattleComputedResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return Hash(writer =>
            {
                writer.WriteString(result.GameId);
                writer.WriteString(result.CatalogSetId);
                writer.WriteString(result.ProfileId);
                writer.WriteString(result.BattleRequestId);
                writer.WriteString(result.BattleId);
                writer.WriteString(result.BattleResultId);
                writer.WriteString(result.ExpectedResultConsumerId);
                writer.WriteInt32((int)result.ExecutionMode);
                writer.WriteInt32((int)result.BattleKind);
                writer.WriteString(result.BattleTypeId);
                writer.WriteInt32((int)result.Outcome);
                writer.WriteString(result.OutcomeTechnicalId);
                writer.WriteInt64(result.AttackerPower);
                writer.WriteInt64(result.OpponentPower);
                writer.WriteInt32(result.Rounds.Count);
                for (int index = 0; index < result.Rounds.Count; index++)
                {
                    BattleRoundResult round = result.Rounds[index];
                    writer.WriteInt32(round.RoundIndex);
                    writer.WriteInt64(round.AttackerRateMicros);
                    writer.WriteInt64(round.OpponentRateMicros);
                    writer.WriteInt64(round.DamageToOpponentMicros);
                    writer.WriteInt64(round.DamageToAttackerMicros);
                    writer.WriteInt64(round.AttackerRemainingPowerMicros);
                    writer.WriteInt64(round.OpponentRemainingPowerMicros);
                }

                WriteLosses(writer, result.AttackerLosses);
                WriteLosses(writer, result.OpponentLosses);
                writer.WriteInt32(result.RewardProposal.Credits);
                writer.WriteInt32(result.RewardProposal.Food);
                writer.WriteInt32(result.RewardProposal.Gold);
                writer.WriteInt32(result.RewardProposal.Experience);
                writer.WriteInt32(result.Contributions.Count);
                for (int index = 0; index < result.Contributions.Count; index++)
                {
                    writer.WriteString(result.Contributions[index].TechnicalId);
                    writer.WriteInt64(result.Contributions[index].ValueMicros);
                }

                writer.WriteString(result.CatalogSha256);
                writer.WriteString(result.AttackerArmySha256);
                writer.WriteString(result.OpponentSha256);
                writer.WriteString(result.ContextSha256);
                writer.WriteString(result.RulesSha256);
                writer.WriteString(result.RewardProfileSha256);
                writer.WriteString(result.DeterminismVersion);
                writer.WriteString(result.SeedHex);
            });
        }

        internal static string Hash(Action<BattleCanonicalWriter> write)
        {
            using (var writer = new BattleCanonicalWriter())
            {
                write(writer);
                using (SHA256 sha256 = SHA256.Create())
                    return ToLowerHex(sha256.ComputeHash(writer.ToArray()));
            }
        }

        internal static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
                builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static void WriteIdentityCore(
            BattleCanonicalWriter writer,
            BattleSnapshotIdentity identity)
        {
            writer.WriteString(identity?.Id);
            writer.WriteString(identity?.SchemaVersion);
            writer.WriteString(identity?.ContentVersion);
            writer.WriteString(identity?.SourceRevision);
            writer.WriteString(identity?.CatalogSetId);
        }

        private static void WriteSnapshotReference(
            BattleCanonicalWriter writer,
            BattleSnapshotIdentity identity)
        {
            writer.WriteString(identity?.Id);
            writer.WriteString(identity?.ContentVersion);
            writer.WriteString(identity?.Sha256);
        }

        private static void WriteLosses(
            BattleCanonicalWriter writer,
            IReadOnlyList<BattleTroopLoss> losses)
        {
            writer.WriteInt32(losses.Count);
            for (int index = 0; index < losses.Count; index++)
            {
                BattleTroopLoss loss = losses[index];
                writer.WriteString(loss.TroopDefinitionId);
                writer.WriteInt64(loss.Killed);
                writer.WriteInt64(loss.Wounded);
                writer.WriteInt64(loss.Survived);
            }
        }
    }

    internal sealed class BattleCanonicalWriter : IDisposable
    {
        private readonly MemoryStream _stream = new MemoryStream();

        public void WriteString(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteUInt32((uint)bytes.Length);
            _stream.Write(bytes, 0, bytes.Length);
        }

        public void WriteInt32(int value)
        {
            WriteUInt32(unchecked((uint)value));
        }

        public void WriteInt64(long value)
        {
            ulong raw = unchecked((ulong)value);
            for (int shift = 56; shift >= 0; shift -= 8)
                _stream.WriteByte((byte)(raw >> shift));
        }

        public byte[] ToArray()
        {
            return _stream.ToArray();
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private void WriteUInt32(uint value)
        {
            _stream.WriteByte((byte)(value >> 24));
            _stream.WriteByte((byte)(value >> 16));
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)value);
        }
    }
}
