using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using AL.Core;
using AL.Data.Runtime;

namespace AL.Battle.Profiles
{
    /// <summary>
    /// Compatibility mapper for the prototype war-drill caller. It snapshots caller inputs into the
    /// authoritative source-profile shape while permanently selecting preview execution.
    /// </summary>
    public static class BattlePreviewSourceFactory
    {
        private const string GameId = "another_life";
        private const string CatalogSetId = "catalog.battle.migration";
        private const string ContentVersion = "1.0.0";
        private const string SourceRevision = "battle.caller.migration";

        public static BattleAuthoritativeSourceState CreateWarDrillPreview(
            string invocationId,
            RealmId playerRealm,
            IEnumerable<TroopStack> attackerTroops,
            RealmId opponentRealm,
            IEnumerable<TroopStack> opponentTroops,
            int randomSeed)
        {
            string stableInvocationId = NormalizeInvocationId(invocationId);
            BattleCatalogSnapshot catalog = BattleMigrationProfiles.CreateCatalog(
                GameId,
                CatalogSetId,
                ContentVersion,
                SourceRevision);
            BattleParticipantSourceProfile player = Participant(
                "profile.preview.player",
                "army.preview.player." + stableInvocationId,
                MapRealm(playerRealm),
                attackerTroops,
                catalog);
            BattleParticipantSourceProfile opponent = Participant(
                "profile.preview.opponent",
                "army.preview.opponent." + stableInvocationId,
                MapRealm(opponentRealm),
                opponentTroops,
                catalog);
            var configuration = new BattleConfigurationSourceProfile(
                GameId,
                CatalogSetId,
                "request.preview." + stableInvocationId,
                "battle.preview." + stableInvocationId,
                BattleTechnicalLimits.PreviewResultPrefix + stableInvocationId,
                BattleTechnicalLimits.ExpectedResultConsumerId,
                BattleExecutionMode.Preview,
                BattleKind.Warzone,
                BattleTechnicalLimits.WarzoneBattleTypeId,
                BattleTechnicalLimits.SupportedDeterminismVersion,
                Hash("seed:" + randomSeed),
                "context.preview." + stableInvocationId,
                "encounter.preview.war_drill",
                BattleRealmContextKind.RealmVersusRealm,
                BattleTerrainProfile.RoadField,
                catalog,
                BattleMigrationProfiles.CreateRules(CatalogSetId, ContentVersion, SourceRevision),
                BattleMigrationProfiles.CreateRewards(CatalogSetId, ContentVersion, SourceRevision));
            return new BattleAuthoritativeSourceState(
                player,
                BattleOpponentSourceProfile.ForArmy(opponent),
                configuration);
        }

        private static BattleParticipantSourceProfile Participant(
            string profileId,
            string armyId,
            BattleRealm realm,
            IEnumerable<TroopStack> sourceTroops,
            BattleCatalogSnapshot catalog)
        {
            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            if (sourceTroops != null)
            {
                foreach (TroopStack stack in sourceTroops)
                {
                    if (stack == null) throw new ArgumentException("A preview troop row cannot be null.", nameof(sourceTroops));
                    string troopId = MapTroop(stack.Type);
                    counts.TryGetValue(troopId, out long existing);
                    counts[troopId] = checked(existing + stack.Count);
                }
            }
            var frozen = new List<BattleSourceTroopCount>(counts.Count);
            foreach (KeyValuePair<string, long> pair in counts)
                frozen.Add(new BattleSourceTroopCount(pair.Key, pair.Value));
            return new BattleParticipantSourceProfile(
                Identity(profileId),
                realm,
                Identity(armyId),
                frozen,
                new BattleEquipmentSourceProfile(
                    Identity(profileId + ".equipment"),
                    Array.Empty<BattleSnapshotIdentity>(),
                    BattleTechnicalLimits.MicrosPerUnit),
                new BattleProgressionSourceProfile(
                    Identity(profileId + ".progression"),
                    BattleTechnicalLimits.MicrosPerUnit,
                    BattleTechnicalLimits.MicrosPerUnit));
        }

        private static BattleSnapshotIdentity Identity(string id)
        {
            return new BattleSnapshotIdentity(
                id,
                BattleTechnicalLimits.SupportedSchemaVersion,
                ContentVersion,
                SourceRevision,
                Hash(id + ":" + ContentVersion + ":" + SourceRevision),
                CatalogSetId);
        }

        private static string MapTroop(TroopType type)
        {
            switch (type)
            {
                case TroopType.Infantry: return "troop.infantry";
                case TroopType.Cavalry: return "troop.cavalry";
                case TroopType.Ranged: return "troop.ranged";
                case TroopType.Siege: return "troop.siege";
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private static BattleRealm MapRealm(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold: return BattleRealm.Stonehold;
                case RealmId.Eldergrove: return BattleRealm.Eldergrove;
                case RealmId.Crownlands: return BattleRealm.Crownlands;
                case RealmId.Umbral: return BattleRealm.Umbral;
                default: throw new ArgumentOutOfRangeException(nameof(realm), "A war drill requires explicit realms.");
            }
        }

        private static string NormalizeInvocationId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A preview invocation ID is required.", nameof(value));
            var result = new StringBuilder(value.Length);
            foreach (char character in value.Trim().ToLowerInvariant())
            {
                if ((character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9') ||
                    character == '.' || character == '_' || character == '-')
                    result.Append(character);
            }
            if (result.Length == 0) throw new ArgumentException("The preview invocation ID is invalid.", nameof(value));
            return result.ToString();
        }

        private static string Hash(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
