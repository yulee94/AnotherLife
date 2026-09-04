using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AL.Marketplace
{
    internal static class MarketplaceDeterminism
    {
        private const int MaximumStableTextUtf8Bytes = 128;
        private const int MaximumCanonicalCharacters = 16384;

        internal static bool IsOpaqueId(string value) =>
            IsBoundedTechnicalText(value, MaximumStableTextUtf8Bytes);

        internal static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool digit = character >= '0' && character <= '9';
                bool lowerHex = character >= 'a' && character <= 'f';
                if (!digit && !lowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        internal static string RequestFingerprint(MarketplaceSettlementRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            return HashFields(
                "market-request-v1",
                request.OperationId,
                Integer((int)request.Operation),
                request.ActorAccountId,
                request.ListingId,
                request.ItemId,
                request.SellerAccountId,
                request.BuyerAccountId,
                Long(request.ListedPrice),
                Integer((int)request.OfferedWalletKind));
        }

        internal static string PlanHash(
            MarketplaceSettlementRequest request,
            string requestFingerprint,
            MarketplaceSettlementQuote quote,
            long resultingAuthorityRevision)
        {
            if (request == null || quote == null)
            {
                return string.Empty;
            }

            return HashFields(
                "market-plan-v1",
                requestFingerprint,
                request.OperationId,
                Integer((int)request.Operation),
                request.ListingId,
                request.ItemId,
                request.SellerAccountId,
                request.BuyerAccountId,
                Long(quote.ListedPrice),
                Long(quote.BuyerDebit),
                Long(quote.TaxDestroyed),
                Long(quote.SellerProceeds),
                Long(resultingAuthorityRevision));
        }

        internal static string HashFields(params string[] fields)
        {
            var builder = new StringBuilder();
            foreach (string raw in fields ?? Array.Empty<string>())
            {
                string value = raw ?? string.Empty;
                builder
                    .Append(value.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(value);
            }

            if (builder.Length > MaximumCanonicalCharacters)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fields),
                    "Marketplace canonical payload exceeds its bounded contract.");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            var hex = new StringBuilder(digest.Length * 2);
            for (int index = 0; index < digest.Length; index++)
            {
                hex.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }

        internal static string Long(long value) =>
            value.ToString(CultureInfo.InvariantCulture);

        private static string Integer(int value) =>
            value.ToString(CultureInfo.InvariantCulture);

        private static bool IsBoundedTechnicalText(string value, int maximumUtf8Bytes)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !StringComparer.Ordinal.Equals(value, value.Trim()))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsControl(character) || char.IsWhiteSpace(character))
                {
                    return false;
                }
            }

            return Encoding.UTF8.GetByteCount(value) <= maximumUtf8Bytes;
        }
    }
}
