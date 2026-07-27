using System;
using System.Globalization;
using System.IO;
using AL.Core.BossRewards;
using NUnit.Framework;

namespace AL.Tests.EditMode.BossRewards
{
    public class BossRewardDeterminismVectorTests
    {
        [TestCase(
            "chance_zero",
            "equipment_alpha",
            0,
            "21d3940756e11ceb8c365394fa2cce335f5e102c7a53426625a452e72dc93eb5",
            "567514119",
            "0",
            false,
            "f287f9f206f4b5e4862b05cf2ae139ca43e9135f92424d32a1318dacf08e2c1d")]
        [TestCase(
            "chance_one_micro",
            "equipment_alpha",
            1,
            "21d3940756e11ceb8c365394fa2cce335f5e102c7a53426625a452e72dc93eb5",
            "567514119",
            "4294",
            false,
            "f287f9f206f4b5e4862b05cf2ae139ca43e9135f92424d32a1318dacf08e2c1d")]
        [TestCase(
            "rare_12500",
            "equipment_beta",
            12500,
            "5e08ec06120d34486e6b161cfd9abcf3d7788d427196a58a1d19aa33d54aac87",
            "1577643014",
            "53687091",
            false,
            "f287f9f206f4b5e4862b05cf2ae139ca43e9135f92424d32a1318dacf08e2c1d")]
        [TestCase(
            "chance_999999",
            "equipment_beta",
            999999,
            "5e08ec06120d34486e6b161cfd9abcf3d7788d427196a58a1d19aa33d54aac87",
            "1577643014",
            "4294963001",
            true,
            "03beb0e739d58bc5d76495966c59862f49614265c9363893506f55920d11b0fc")]
        [TestCase(
            "chance_full",
            "equipment_unicode_한",
            1000000,
            "fb40422e9469909946f4b3dd0de18885aa7d6851e1c5ea7741174e875a9e8489",
            "4215292462",
            "4294967296",
            true,
            "36c00f3a461a2d53a26cee8c738a18307625fb3e083a63778eee4e114dfc4e57")]
        public void RetainedCrossRuntimeVectorIsExact(
            string vectorName,
            string equipmentId,
            int chanceMicros,
            string expectedSha256,
            string expectedDraw,
            string expectedThreshold,
            bool expectedHit,
            string expectedComputationHash)
        {
            BossRewardComputationRequest request = BossRewardTestFixtures.Request();
            byte[] canonical = BossRewardDeterministicRoll.BuildCanonicalInput(
                request.DeterminismVersion,
                request.CatalogSetId,
                request.RewardResultId,
                request.EncounterCompletionId,
                request.BossDefinitionId,
                request.RewardProfileId,
                request.RewardProfileContentVersion,
                equipmentId);
            byte[] digest = BossRewardDeterministicRoll.ComputeDigest(canonical);
            uint draw = BossRewardDeterministicRoll.ReadBigEndianDraw(digest);
            ulong threshold =
                BossRewardDeterministicRoll.ComputeThresholdExclusive(chanceMicros);

            Assert.AreEqual(expectedSha256, BossRewardDeterministicRoll.ToLowerHex(digest));
            Assert.AreEqual(
                uint.Parse(expectedDraw, CultureInfo.InvariantCulture),
                draw);
            Assert.AreEqual(
                ulong.Parse(expectedThreshold, CultureInfo.InvariantCulture),
                threshold);
            Assert.AreEqual(
                expectedHit,
                BossRewardDeterministicRoll.IsHit(draw, chanceMicros));

            var profile = BossRewardTestFixtures.Profile(
                entries: new[]
                {
                    new BossRewardEntry(
                        equipmentId,
                        chanceMicros,
                        1,
                        BossRewardTestFixtures.ItemPolicyId)
                },
                credits: 0);
            var definition = BossRewardTestFixtures.Equipment(equipmentId);
            BossRewardComputationResult result = BossRewardComputation.Compute(
                request,
                BossRewardTestFixtures.Catalog(
                    profile,
                    equipment: new[] { definition }));

            Assert.AreEqual(BossRewardComputationStatus.Computed, result.Status);
            Assert.AreEqual(expectedHit ? 1 : 0, result.Value.Drops.Count);
            Assert.AreEqual(expectedComputationHash, result.Value.ComputationHash);

            string vectorFile = File.ReadAllText(VectorPath());
            StringAssert.Contains("\"name\": \"" + vectorName + "\"", vectorFile);
            StringAssert.Contains("\"canonicalHex\": \"" + ToHex(canonical) + "\"", vectorFile);
            StringAssert.Contains("\"sha256\": \"" + expectedSha256 + "\"", vectorFile);
            StringAssert.Contains(
                "\"expectedComputationHash\": \"" + expectedComputationHash + "\"",
                vectorFile);
        }

        [Test]
        public void VectorArtifactDeclaresSupportedSchemaAndAlgorithm()
        {
            string vectorFile = File.ReadAllText(VectorPath());

            StringAssert.Contains(
                "\"vectorSchemaVersion\": \"boss_reward_vector_v1\"",
                vectorFile);
            StringAssert.Contains(
                "\"determinismVersion\": \"boss_reward_sha256_v1\"",
                vectorFile);
            StringAssert.Contains(
                "\"catalogSetId\": \"catalog_set_test\"",
                vectorFile);
            StringAssert.Contains(
                "\"rewardResultId\": \"result_test\"",
                vectorFile);
            StringAssert.Contains(
                "\"encounterCompletionId\": \"completion_test\"",
                vectorFile);
            StringAssert.Contains(
                "\"bossDefinitionId\": \"boss_test\"",
                vectorFile);
            StringAssert.Contains(
                "\"rewardProfileId\": \"reward_profile_test\"",
                vectorFile);
            StringAssert.Contains(
                "\"rewardProfileContentVersion\": \"reward_v1\"",
                vectorFile);
            StringAssert.Contains(
                "\"gameId\": \"another_life\"",
                vectorFile);
            StringAssert.Contains(
                "\"profileId\": \"profile_test\"",
                vectorFile);
            StringAssert.Contains(
                "\"stackPolicyId\": \"stack_quantity\"",
                vectorFile);
            Assert.AreEqual(5, Count(vectorFile, "\"name\":"));
        }

        private static string VectorPath()
        {
            return Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "AL",
                "Tests",
                "EditMode",
                "BossRewards",
                "TestVectors",
                "boss_reward_sha256_v1.json");
        }

        private static string ToHex(byte[] bytes)
        {
            return BossRewardDeterministicRoll.ToLowerHex(bytes);
        }

        private static int Count(string source, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count;
        }
    }
}
