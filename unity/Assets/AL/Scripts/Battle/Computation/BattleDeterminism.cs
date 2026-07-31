using System;
using System.Security.Cryptography;
using AL.Battle.Contracts;

namespace AL.Battle.Computation
{
    public static class BattleDeterminism
    {
        public static byte[] BuildCanonicalDrawInput(
            BattleComputationRequest request,
            string drawNamespace,
            int roundIndex)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using (var writer = new BattleCanonicalWriter())
            {
                writer.WriteString(request.DeterminismVersion);
                writer.WriteString(request.CatalogSetId);
                writer.WriteString(request.BattleResultId);
                writer.WriteString(request.BattleRequestId);
                writer.WriteString(request.Rules.Identity.Id);
                writer.WriteString(request.Rules.Identity.ContentVersion);
                writer.WriteString(request.Rules.Identity.Sha256);
                writer.WriteString(request.Context.Identity.Sha256);
                writer.WriteString(request.AttackerArmy.Identity.Sha256);
                writer.WriteString(OpponentSha256(request.Opponent));
                writer.WriteString(request.SeedHex);
                writer.WriteString(drawNamespace);
                writer.WriteInt32(roundIndex);
                return writer.ToArray();
            }
        }

        public static uint DrawUInt32(
            BattleComputationRequest request,
            string drawNamespace,
            int roundIndex)
        {
            byte[] canonical = BuildCanonicalDrawInput(request, drawNamespace, roundIndex);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(canonical);
                return ReadUInt32BigEndian(digest);
            }
        }

        public static uint ReadUInt32BigEndian(byte[] digest)
        {
            if (digest == null || digest.Length < 4)
                throw new ArgumentException("At least four digest bytes are required.", nameof(digest));
            return ((uint)digest[0] << 24) |
                   ((uint)digest[1] << 16) |
                   ((uint)digest[2] << 8) |
                   digest[3];
        }

        internal static string OpponentSha256(BattleOpponentSnapshot opponent)
        {
            if (opponent == null)
                return string.Empty;
            if (opponent.Kind == BattleOpponentKind.Army &&
                opponent.BossIdentity != null &&
                opponent.Army != null)
                return BattleCanonicalHash.BossArmy(opponent.BossIdentity, opponent.Army);
            return opponent.Kind == BattleOpponentKind.Army
                ? opponent.Army?.Identity?.Sha256 ?? string.Empty
                : opponent.BossIdentity?.Sha256 ?? string.Empty;
        }
    }
}
