using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AL.Data.Catalogs
{
    public sealed class OathmarkWalletPolicy
    {
        private OathmarkWalletPolicy() { }
        public string CurrencyId { get; private set; }
        public string Hash { get; private set; }
        public int SaveSchemaVersion { get; private set; }
        public long InitialBalance { get; private set; }
        public int MaximumReceipts { get; private set; }
        public string WalletId(string profileId) => profileId + ":" + CurrencyId;

        public static bool TryLoad(byte[] currencyBytes, byte[] walletBytes, out OathmarkWalletPolicy policy)
        {
            policy = null;
            try
            {
                var source = WalletJson.Object(StrictJsonDocument.Parse(currencyBytes, 65536));
                var file = WalletJson.Object(StrictJsonDocument.Parse(walletBytes, 65536));
                WalletJson.Exact(file, "schemaVersion", "catalogId", "sourceRevision", "currencyCatalogId",
                    "saveSchemaVersion", "accountBinding", "initialBalance", "maximumReceipts", "earningSourcesEnabled");
                var currency = WalletJson.Object(WalletJson.Get(source, "currency"));
                WalletJson.Exact(currency, "technicalId", "playerFacingSingular", "playerFacingPlural", "domain",
                    "integerUnitScale", "fractionalUnits", "conversion", "premiumOrRealMoney", "soleMainCurrency", "forbiddenWallets");
                if (WalletJson.Long(source, "schemaVersion") != 1 ||
                    WalletJson.Long(file, "schemaVersion") != 1 ||
                    WalletJson.Text(file, "catalogId") != "al_oathmark_wallet_policy" ||
                    WalletJson.Text(file, "sourceRevision") != "oathmark_wallet_v1" ||
                    WalletJson.Text(file, "currencyCatalogId") != WalletJson.Text(source, "catalogId") ||
                    WalletJson.Text(source, "catalogId") != "al_oathmark_marketplace_policy" ||
                    WalletJson.Long(file, "saveSchemaVersion") != 2 ||
                    WalletJson.Text(file, "accountBinding") != "local_profile_identity" ||
                    WalletJson.Long(file, "initialBalance") != 0 ||
                    WalletJson.Bool(file, "earningSourcesEnabled") ||
                    WalletJson.Long(currency, "integerUnitScale") != 1 ||
                    WalletJson.Bool(currency, "fractionalUnits") ||
                    WalletJson.Text(currency, "conversion") != "forbidden" ||
                    WalletJson.Text(currency, "premiumOrRealMoney") != "forbidden" ||
                    !WalletJson.Bool(currency, "soleMainCurrency") ||
                    WalletJson.Text(currency, "domain") != "three_dimensional_player_main") return false;
                string id = WalletJson.Text(currency, "technicalId");
                // Match the schema identity, never derive this from a caller or presentation label.
                if (id != "oathmark") return false;
                var forbidden = WalletJson.Get(currency, "forbiddenWallets") as StrictJsonArray;
                string[] expected = { "legacy_gold", "kingdom_resource", "guild_treasury", "realm_resource", "warzone_credits", "premium", "real_money" };
                if (forbidden == null || forbidden.Items.Count != expected.Length) return false;
                for (int i = 0; i < expected.Length; i++)
                    if (!(forbidden.Items[i] is StrictJsonString text) || text.Value != expected[i]) return false;
                long limit = WalletJson.Long(file, "maximumReceipts");
                if (limit < 1 || limit > 2048) return false;
                policy = new OathmarkWalletPolicy
                {
                    CurrencyId = id,
                    Hash = Digest(Encoding.UTF8.GetBytes(Digest(currencyBytes) + Digest(walletBytes))),
                    SaveSchemaVersion = (int)WalletJson.Long(file, "saveSchemaVersion"),
                    InitialBalance = WalletJson.Long(file, "initialBalance"),
                    MaximumReceipts = (int)limit
                };
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is StrictJsonException || ex is OverflowException)
            {
                return false;
            }
        }

        public static string Digest(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var result = new StringBuilder(64);
                foreach (byte b in sha.ComputeHash(bytes)) result.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }

    internal static class WalletJson
    {
        internal static StrictJsonObject Object(StrictJsonValue value) => value as StrictJsonObject ?? throw new FormatException();
        internal static StrictJsonValue Get(StrictJsonObject obj, string key) =>
            obj.TryGet(key, out var value) ? value : throw new FormatException(key);
        internal static string Text(StrictJsonObject obj, string key) =>
            (Get(obj, key) as StrictJsonString)?.Value ?? throw new FormatException(key);
        internal static bool Bool(StrictJsonObject obj, string key) =>
            Get(obj, key) is StrictJsonBoolean value ? value.Value : throw new FormatException(key);
        internal static long Long(StrictJsonObject obj, string key)
        {
            var number = Get(obj, key) as StrictJsonNumber;
            if (number == null || !long.TryParse(number.RawValue, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out long value)) throw new FormatException(key);
            return value;
        }
        internal static void Exact(StrictJsonObject obj, params string[] keys)
        {
            if (obj.Properties.Count != keys.Length) throw new FormatException();
            foreach (string key in keys) Get(obj, key);
        }
    }
}
