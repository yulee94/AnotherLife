using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AL.ChampionMode.Death
{
    public static class DeathPenaltyIds
    {
        public const string SchemaOperationId = "al.save.schema2.death-penalty.v1";
        public const string PolicyVersion = "al.death-penalty.v1";
        public const string LevelCapPolicyId = "al.level-cap.v1";
        public const string LevelCapPolicyRevision = "al.level-cap.rev.1";
        public const string LedgerVersion = "al.death-penalty.ledger.v1";
        public const string InnerCombatSessionId = "al.combat.inner-realm";
        public const string InnerEncounterAttemptId = "al.encounter.inner";
        public const int DefaultMaximumLevel = 50;
        public const int DefaultStartingLevel = 1;
        public const long DefaultExperienceUnitsPerLevel = 100L;

        public static string NewOperationId()
        {
            return "al.death.op." + Guid.NewGuid().ToString("N");
        }

        public static string NewDeathEventId()
        {
            return "al.death.evt." + Guid.NewGuid().ToString("N");
        }

        public static string InstanceId(string realmName)
        {
            return "al.instance." + (realmName ?? "none");
        }

        public static string NextProgressionRevision(string previous)
        {
            return "al.prog." + HexPrefix((previous ?? string.Empty) + ":next", 32);
        }

        public static string NextDeathStateRevision(string previous)
        {
            return "al.death.rev." + HexPrefix((previous ?? string.Empty) + ":next", 32);
        }

        public static string NextLedgerRevision(string previous)
        {
            return "al.ledger.rev." + HexPrefix((previous ?? string.Empty) + ":next", 32);
        }

        public static string CharacterId(string profileId)
        {
            return profileId ?? string.Empty;
        }

        private static string HexPrefix(string value, int characterCount)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(characterCount);
                for (int i = 0; i < hash.Length && builder.Length < characterCount; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.Length > characterCount
                    ? builder.ToString(0, characterCount)
                    : builder.ToString();
            }
        }
    }
}
