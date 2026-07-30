using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AL.Data.Catalogs;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class GameDataRealmCapabilityProfileTests
    {
        private static readonly string[] StableIds =
        {
            "battle_realm_crownlands",
            "battle_realm_stonehold",
            "battle_realm_eldergrove",
            "battle_realm_umbral"
        };

        private static readonly string[] RealmStableIds =
        {
            "crownlands",
            "stonehold",
            "eldergrove",
            "umbral"
        };

        private static readonly GameDataRealmCapabilityCondition[] Conditions =
        {
            GameDataRealmCapabilityCondition.Constant,
            GameDataRealmCapabilityCondition.OwnArmyHasSiege,
            GameDataRealmCapabilityCondition.OwnArmyHasRanged,
            GameDataRealmCapabilityCondition.OwnSideIsAttackerOrBattleIsPvp
        };

        private static readonly string[] ConditionTokens =
        {
            "constant",
            "own_army_has_siege",
            "own_army_has_ranged",
            "own_side_is_attacker_or_battle_is_pvp"
        };

        private static readonly int[] MatchedMultipliers =
        {
            1060000,
            1100000,
            1100000,
            1090000
        };

        private static readonly int[] DefaultMultipliers =
        {
            1060000,
            1060000,
            1050000,
            1040000
        };

        [Test]
        public void RegistryPublishesExactImmutableAuthoredOrder()
        {
            Assert.AreEqual(1, GameDataRealmCapabilityProfiles.Version);
            Assert.AreEqual(4, GameDataRealmCapabilityProfiles.ProfileCount);
            Assert.AreEqual(
                1000000,
                GameDataRealmCapabilityProfiles.NeutralMultiplierMillionths);
            CollectionAssert.AreEqual(
                StableIds,
                GameDataRealmCapabilityProfiles.StableIds.ToArray());
            CollectionAssert.AreEqual(
                StableIds,
                GameDataRealmCapabilityProfiles.Entries
                    .Select(profile => profile.StableId)
                    .ToArray());
            CollectionAssert.AreEqual(
                RealmStableIds,
                GameDataRealmCapabilityProfiles.Entries
                    .Select(profile => profile.RealmStableId)
                    .ToArray());
            CollectionAssert.AreEqual(
                Conditions,
                GameDataRealmCapabilityProfiles.Entries
                    .Select(profile => profile.Condition)
                    .ToArray());
            CollectionAssert.AreEqual(
                ConditionTokens,
                GameDataRealmCapabilityProfiles.Entries
                    .Select(profile => profile.ConditionToken)
                    .ToArray());
            CollectionAssert.AreEqual(
                MatchedMultipliers,
                GameDataRealmCapabilityProfiles.Entries
                    .Select(profile => profile.MatchedMultiplierMillionths)
                    .ToArray());
            CollectionAssert.AreEqual(
                DefaultMultipliers,
                GameDataRealmCapabilityProfiles.Entries
                    .Select(profile => profile.DefaultMultiplierMillionths)
                    .ToArray());

            for (var index = 0;
                 index < GameDataRealmCapabilityProfiles.Entries.Count;
                 index++)
            {
                Assert.AreEqual(
                    index,
                    GameDataRealmCapabilityProfiles.Entries[index].Order);
            }

            var entries = (IList)GameDataRealmCapabilityProfiles.Entries;
            var ids = (IList)GameDataRealmCapabilityProfiles.StableIds;
            Assert.True(entries.IsReadOnly);
            Assert.True(ids.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => entries[0] = entries[1]);
            Assert.Throws<NotSupportedException>(() => ids[0] = "replacement");
        }

        [Test]
        public void ExactResolversAndRelationsRejectAliasesAndMalformedInputs()
        {
            for (var index = 0; index < StableIds.Length; index++)
            {
                GameDataRealmCapabilityProfile byProfileId;
                GameDataRealmCapabilityProfile byRealmId;
                Assert.True(
                    GameDataRealmCapabilityProfiles.TryGetByStableId(
                        StableIds[index],
                        out byProfileId));
                Assert.True(
                    GameDataRealmCapabilityProfiles.TryGetByRealmStableId(
                        RealmStableIds[index],
                        out byRealmId));
                Assert.AreSame(byProfileId, byRealmId);
                Assert.True(
                    GameDataRealmCapabilityProfiles.IsApprovedRealmRelation(
                        RealmStableIds[index],
                        new[] { StableIds[index] }));
            }

            foreach (var invalidProfileId in new[]
                     {
                         null,
                         string.Empty,
                         "Battle_realm_stonehold",
                         "battle-realm-stonehold",
                         "battle realm stonehold",
                         " battle_realm_stonehold",
                         "battle_realm_stonehold ",
                         "unknown_profile"
                     })
            {
                GameDataRealmCapabilityProfile profile;
                Assert.False(
                    GameDataRealmCapabilityProfiles.TryGetByStableId(
                        invalidProfileId,
                        out profile),
                    invalidProfileId);
                Assert.IsNull(profile, invalidProfileId);
            }

            foreach (var invalidRealmId in new[]
                     {
                         null,
                         string.Empty,
                         "Stonehold",
                         "stone_hold",
                         "stone-hold",
                         " stonehold",
                         "stonehold ",
                         "unknown_realm"
                     })
            {
                GameDataRealmCapabilityProfile profile;
                Assert.False(
                    GameDataRealmCapabilityProfiles.TryGetByRealmStableId(
                        invalidRealmId,
                        out profile),
                    invalidRealmId);
                Assert.IsNull(profile, invalidRealmId);
            }

            Assert.False(
                GameDataRealmCapabilityProfiles.IsApprovedRealmRelation(
                    "stonehold",
                    null));
            Assert.False(
                GameDataRealmCapabilityProfiles.IsApprovedRealmRelation(
                    "stonehold",
                    new string[0]));
            Assert.False(
                GameDataRealmCapabilityProfiles.IsApprovedRealmRelation(
                    "stonehold",
                    new[]
                    {
                        "battle_realm_stonehold",
                        "battle_realm_stonehold"
                    }));
            Assert.False(
                GameDataRealmCapabilityProfiles.IsApprovedRealmRelation(
                    "stonehold",
                    new[]
                    {
                        "battle_realm_stonehold",
                        "battle_realm_crownlands"
                    }));
            Assert.False(
                GameDataRealmCapabilityProfiles.IsApprovedRealmRelation(
                    "stonehold",
                    new[] { "battle_realm_crownlands" }));
            Assert.False(
                GameDataRealmCapabilityProfiles.IsApprovedRealmRelation(
                    "stonehold",
                    new[] { "Battle_realm_stonehold" }));
        }

        [Test]
        public void ConditionTokensAreExactAndUndefinedConditionsReject()
        {
            for (var index = 0; index < Conditions.Length; index++)
            {
                string token;
                Assert.True(
                    GameDataRealmCapabilityProfiles.TryGetConditionToken(
                        Conditions[index],
                        out token));
                Assert.AreEqual(ConditionTokens[index], token);
            }

            string undefinedToken;
            Assert.False(
                GameDataRealmCapabilityProfiles.TryGetConditionToken(
                    (GameDataRealmCapabilityCondition)(-1),
                    out undefinedToken));
            Assert.IsNull(undefinedToken);
            Assert.False(
                GameDataRealmCapabilityProfiles.TryGetConditionToken(
                    (GameDataRealmCapabilityCondition)4,
                    out undefinedToken));
            Assert.IsNull(undefinedToken);
        }

        [Test]
        public void ProfilesEvaluateEveryMatchedAndDefaultCondition()
        {
            var crownlands = GameDataRealmCapabilityProfiles.Entries[0];
            Assert.AreEqual(1060000, Evaluate(crownlands));
            Assert.AreEqual(
                1060000,
                Evaluate(
                    crownlands,
                    ownArmyHasSiege: true,
                    ownArmyHasRanged: true,
                    ownSideIsAttacker: true,
                    battleIsPvp: true));

            var stonehold = GameDataRealmCapabilityProfiles.Entries[1];
            Assert.AreEqual(1060000, Evaluate(stonehold));
            Assert.AreEqual(
                1100000,
                Evaluate(stonehold, ownArmyHasSiege: true));
            Assert.AreEqual(
                1060000,
                Evaluate(stonehold, ownArmyHasRanged: true));

            var eldergrove = GameDataRealmCapabilityProfiles.Entries[2];
            Assert.AreEqual(1050000, Evaluate(eldergrove));
            Assert.AreEqual(
                1100000,
                Evaluate(eldergrove, ownArmyHasRanged: true));
            Assert.AreEqual(
                1050000,
                Evaluate(eldergrove, ownArmyHasSiege: true));

            var umbral = GameDataRealmCapabilityProfiles.Entries[3];
            Assert.AreEqual(1040000, Evaluate(umbral));
            Assert.AreEqual(
                1090000,
                Evaluate(umbral, ownSideIsAttacker: true));
            Assert.AreEqual(
                1090000,
                Evaluate(umbral, battleIsPvp: true),
                "An Umbral defender receives the current PvP branch.");
            Assert.AreEqual(
                1090000,
                Evaluate(
                    umbral,
                    ownSideIsAttacker: true,
                    battleIsPvp: true),
                "An Umbral attacker receives the current PvP branch.");
        }

        [Test]
        public void LegacySimulatorExpressionsRemainExactMigrationEvidence()
        {
            var sourcePath = Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "Battle",
                "Simulator",
                "DeterministicBattleSimulator.cs");
            var source = File.ReadAllText(sourcePath);
            var expressions = new[]
            {
                "RealmId.Stonehold => 1f + " +
                "(GetTotalCountStatic(troops, TroopType.Siege) > 0 ? 0.10f : 0.06f),",
                "RealmId.Eldergrove => 1f + " +
                "(GetTotalCountStatic(troops, TroopType.Ranged) > 0 ? 0.10f : 0.05f),",
                "RealmId.Crownlands => 1.06f,",
                "RealmId.Umbral => 1f + " +
                "(isAttacker || battleType == BattleType.PvP ? 0.09f : 0.04f),"
            };

            foreach (var expression in expressions)
            {
                Assert.AreEqual(
                    1,
                    Regex.Matches(source, Regex.Escape(expression)).Count,
                    expression);
            }
        }

        private static int Evaluate(
            GameDataRealmCapabilityProfile profile,
            bool ownArmyHasSiege = false,
            bool ownArmyHasRanged = false,
            bool ownSideIsAttacker = false,
            bool battleIsPvp = false)
        {
            return profile.EvaluateMultiplierMillionths(
                ownArmyHasSiege,
                ownArmyHasRanged,
                ownSideIsAttacker,
                battleIsPvp);
        }
    }
}
