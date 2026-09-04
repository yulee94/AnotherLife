using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AL.Core;

namespace AL.RealmSelection
{
    public static class RealmSelectionAuthority
    {
        public const string OperationId = "al.save.schema2.realm-selection.v1";
        public const int CurrentVersion = 1;
        public const int MinimumIdentityCharacters = 8;
        public const int MaximumIdentityCharacters = 64;
        public const string InitialProvenance = "initial";
        public const string LegacyMigrationProvenance = "legacy-migration";

        public static bool IsDefinedPlayable(RealmId id)
        {
            return id != RealmId.None && Enum.IsDefined(typeof(RealmId), id);
        }

        public static bool IsBoundedIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length < MinimumIdentityCharacters ||
                value.Length > MaximumIdentityCharacters)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= 'A' && c <= 'Z') ||
                      (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') ||
                      c == '_' ||
                      c == '-' ||
                      c == '.'))
                {
                    return false;
                }
            }

            return true;
        }

        public static string MigrationTransactionId(string profileId, RealmId realm)
        {
            return "alr_mig_" + HexPrefix(profileId + ":" + ((int)realm).ToString(CultureInfo.InvariantCulture), 32);
        }

        public static string EventId(string transactionId)
        {
            return "alr_evt_" + HexPrefix(transactionId ?? string.Empty, 32);
        }

        public static string ComputeReceiptFingerprint(
            string profileId,
            RealmId realm,
            string transactionId,
            string correlationId,
            string operationId,
            string eventId,
            string provenance,
            long revision)
        {
            string payload = string.Join(
                "\n",
                profileId ?? string.Empty,
                ((int)realm).ToString(CultureInfo.InvariantCulture),
                transactionId ?? string.Empty,
                correlationId ?? string.Empty,
                operationId ?? string.Empty,
                eventId ?? string.Empty,
                provenance ?? string.Empty,
                revision.ToString(CultureInfo.InvariantCulture));
            return HexPrefix(payload, 64);
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

                if (builder.Length > characterCount)
                {
                    return builder.ToString(0, characterCount);
                }

                return builder.ToString();
            }
        }
    }
}
