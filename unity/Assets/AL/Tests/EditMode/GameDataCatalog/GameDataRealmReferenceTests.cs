using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AL.Core;
using AL.Data.Catalogs;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class GameDataRealmReferenceTests
    {
        private static readonly string[] StableIds =
        {
            "crownlands",
            "stonehold",
            "eldergrove",
            "umbral"
        };

        private static readonly string[] LegacyNames =
        {
            "Crownlands",
            "Stonehold",
            "Eldergrove",
            "Umbral"
        };

        private static readonly int[] LegacyValues = { 3, 1, 2, 4 };

        private static readonly string[] NameReferences =
        {
            "realm.crownlands.name",
            "realm.stonehold.name",
            "realm.eldergrove.name",
            "realm.umbral.name"
        };

        private static readonly string[] DescriptionReferences =
        {
            "realm.crownlands.description",
            "realm.stonehold.description",
            "realm.eldergrove.description",
            "realm.umbral.description"
        };

        private static readonly string[] InnerRealmIds =
        {
            "inner_crownlands",
            "inner_stonehold",
            "inner_eldergrove",
            "inner_umbral"
        };

        private static readonly string[] MainGateIds =
        {
            "gate_crownlands_meridian",
            "gate_stonehold_faultline",
            "gate_eldergrove_greenveil",
            "gate_umbral_ashvein"
        };

        private static readonly string[] OuterWarzoneIds =
        {
            "warzone_crownlands",
            "warzone_stonehold",
            "warzone_eldergrove",
            "warzone_umbral"
        };

        private static readonly string[] RareResourceIds =
        {
            "royal_sigil",
            "deep_ore",
            "world_sap",
            "dark_crystal"
        };

        private static readonly string[] AssetReferences =
        {
            "Assets/AL/Art/Heraldry/RuntimeExports/" +
            "S_ArcaneAxis_Crownlands_Flat_256_v001.png",
            "Assets/AL/Art/Heraldry/RuntimeExports/" +
            "S_ArcaneAxis_Stonehold_Flat_256_v001.png",
            "Assets/AL/Art/Heraldry/RuntimeExports/" +
            "S_ArcaneAxis_Eldergrove_Flat_256_v001.png",
            "Assets/AL/Art/Heraldry/RuntimeExports/" +
            "S_ArcaneAxis_Umbral_Flat_256_v001.png"
        };

        private static readonly string[] AssetGuids =
        {
            "ba4dfcc7b514049f79f6ec3424193b46",
            "94d8d9e2cf04a4b769c213a13c164b8e",
            "53001b27fd9d14914984211765be4391",
            "a426041e03b0742999a34b8b5e198406"
        };

        private static readonly string[] AssetHashes =
        {
            "f5c7e351ec930aac69f6df02d03034bc38c465ed8dfa787dd4feba044f33f82b",
            "53d220dc8b938d212963286133ca39e1968fa1421126559dd56bdfde9c437946",
            "1d45fc8fba82ebb3fdc1c4f819026ea8e45b11c248378371c7b2b6923c6e0cac",
            "a9daefa3ea6445ba2db680dad92a456db75becebec8848c678b29d5ea2c85aaa"
        };

        [Test]
        public void RegistryPublishesOneExactReadOnlyRealmAuthority()
        {
            Assert.AreEqual(1, GameDataRealmReferences.Version);
            Assert.AreEqual(4, GameDataRealmReferences.RealmCount);
            Assert.AreEqual(
                GameDataRealmReferences.RealmCount,
                GameDataRealmReferences.Entries.Count);
            CollectionAssert.AreEqual(
                StableIds,
                GameDataRealmReferences.StableIds.ToArray());
            CollectionAssert.AreEqual(
                InnerRealmIds,
                GameDataRealmReferences.InnerRealmIds.ToArray());
            CollectionAssert.AreEqual(
                MainGateIds,
                GameDataRealmReferences.MainGateIds.ToArray());
            CollectionAssert.AreEqual(
                OuterWarzoneIds,
                GameDataRealmReferences.OuterWarzoneIds.ToArray());
            CollectionAssert.AreEqual(
                AssetReferences,
                GameDataRealmReferences.AssetReferences.ToArray());

            foreach (var list in new IList[]
                     {
                         (IList)GameDataRealmReferences.Entries,
                         (IList)GameDataRealmReferences.StableIds,
                         (IList)GameDataRealmReferences.InnerRealmIds,
                         (IList)GameDataRealmReferences.MainGateIds,
                         (IList)GameDataRealmReferences.OuterWarzoneIds,
                         (IList)GameDataRealmReferences.AssetReferences
                     })
            {
                Assert.True(list.IsReadOnly);
                Assert.Throws<NotSupportedException>(() => list[0] = null);
            }

            for (var index = 0; index < StableIds.Length; index++)
            {
                var reference = GameDataRealmReferences.Entries[index];
                Assert.AreEqual(index, reference.Order, StableIds[index]);
                Assert.AreEqual(StableIds[index], reference.StableId, StableIds[index]);
                Assert.AreEqual(
                    LegacyNames[index],
                    reference.LegacyRealmName,
                    StableIds[index]);
                Assert.AreEqual(
                    LegacyValues[index],
                    reference.LegacyRealmValue,
                    StableIds[index]);
                Assert.AreEqual(
                    NameReferences[index],
                    reference.NameReference,
                    StableIds[index]);
                Assert.AreEqual(
                    DescriptionReferences[index],
                    reference.DescriptionReference,
                    StableIds[index]);
                Assert.AreEqual(
                    InnerRealmIds[index],
                    reference.InnerRealmId,
                    StableIds[index]);
                Assert.AreEqual(
                    MainGateIds[index],
                    reference.MainGateId,
                    StableIds[index]);
                Assert.AreEqual(
                    OuterWarzoneIds[index],
                    reference.OuterWarzoneId,
                    StableIds[index]);
                Assert.AreEqual(
                    RareResourceIds[index],
                    reference.RareResourceStableId,
                    StableIds[index]);
                Assert.AreEqual(
                    AssetReferences[index],
                    reference.AssetReference,
                    StableIds[index]);
                Assert.AreEqual(
                    AssetGuids[index],
                    reference.AssetGuid,
                    StableIds[index]);
                Assert.AreEqual(
                    AssetHashes[index],
                    reference.AssetSha256,
                    StableIds[index]);

                Assert.True(
                    GameDataRealmReferences.TryGetByStableId(
                        StableIds[index],
                        out var byStableId),
                    StableIds[index]);
                Assert.AreSame(reference, byStableId, StableIds[index]);
                Assert.True(
                    GameDataRealmReferences.TryGetByLegacyIdentity(
                        LegacyNames[index],
                        LegacyValues[index],
                        out var byLegacyIdentity),
                    StableIds[index]);
                Assert.AreSame(reference, byLegacyIdentity, StableIds[index]);
                Assert.True(
                    GameDataRealmReferences.IsApprovedRareResourceRelation(
                        StableIds[index],
                        LegacyNames[index],
                        LegacyValues[index],
                        RareResourceIds[index]),
                    StableIds[index]);
                Assert.True(
                    GameDataRealmReferences.IsApprovedWorldAssetRelation(
                        StableIds[index],
                        LegacyNames[index],
                        LegacyValues[index],
                        NameReferences[index],
                        DescriptionReferences[index],
                        InnerRealmIds[index],
                        MainGateIds[index],
                        OuterWarzoneIds[index],
                        AssetReferences[index]),
                    StableIds[index]);

                var realm = (RealmId)Enum.Parse(
                    typeof(RealmId),
                    LegacyNames[index],
                    false);
                Assert.AreEqual(LegacyValues[index], (int)realm, StableIds[index]);
                Assert.True(
                    ResourceRules.TryGetRareResourceForRealm(
                        realm,
                        out var rareResource),
                    StableIds[index]);
                Assert.True(
                    ResourceRules.TryGetStableId(
                        rareResource,
                        out var rareResourceId),
                    StableIds[index]);
                Assert.AreEqual(
                    RareResourceIds[index],
                    rareResourceId,
                    StableIds[index]);
            }
        }

        [Test]
        public void RegistryRejectsMalformedSwappedAndFallbackIdentity()
        {
            foreach (var invalid in new[]
                     {
                         null,
                         string.Empty,
                         " ",
                         "Stonehold",
                         "STONEHOLD",
                         "stone_hold",
                         "stonehold ",
                         " stonehold",
                         "unknown_realm"
                     })
            {
                Assert.False(
                    GameDataRealmReferences.TryGetByStableId(
                        invalid,
                        out var reference),
                    invalid ?? "<null>");
                Assert.IsNull(reference, invalid ?? "<null>");
            }

            Assert.False(
                GameDataRealmReferences.TryGetByLegacyIdentity(
                    "None",
                    0,
                    out var none));
            Assert.IsNull(none);
            Assert.False(
                GameDataRealmReferences.TryGetByLegacyIdentity(
                    "stonehold",
                    1,
                    out var wrongCase));
            Assert.IsNull(wrongCase);
            Assert.False(
                GameDataRealmReferences.TryGetByLegacyIdentity(
                    "Stonehold",
                    3,
                    out var wrongValue));
            Assert.IsNull(wrongValue);

            Assert.False(
                GameDataRealmReferences.IsApprovedRareResourceRelation(
                    "stonehold",
                    "Stonehold",
                    1,
                    "royal_sigil"));
            Assert.False(
                GameDataRealmReferences.IsApprovedWorldAssetRelation(
                    "stonehold",
                    "Stonehold",
                    1,
                    "realm.stonehold.name",
                    "realm.stonehold.description",
                    "inner_crownlands",
                    "gate_stonehold_faultline",
                    "warzone_stonehold",
                    AssetReferences[1]));
            Assert.False(
                GameDataRealmReferences.IsApprovedWorldAssetRelation(
                    "stonehold",
                    "Stonehold",
                    1,
                    "realm.stonehold.name",
                    "realm.stonehold.description",
                    "inner_stonehold",
                    "gate_stonehold_faultline",
                    "warzone_stonehold",
                    AssetReferences[0]));
            Assert.False(
                ResourceRules.TryGetRareResourceForRealm(
                    RealmId.None,
                    out _));
            Assert.False(
                ResourceRules.TryGetRareResourceForRealm(
                    (RealmId)9001,
                    out _));
        }

        [Test]
        public void PinnedAssetReferencesMatchCommittedGuidAndRawBytes()
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            Assert.NotNull(projectRoot);

            for (var index = 0; index < GameDataRealmReferences.Entries.Count; index++)
            {
                var reference = GameDataRealmReferences.Entries[index];
                Assert.AreEqual(
                    reference.AssetGuid,
                    AssetDatabase.AssetPathToGUID(reference.AssetReference),
                    reference.StableId);

                var absolutePath = Path.GetFullPath(
                    Path.Combine(
                        projectRoot.FullName,
                        reference.AssetReference.Replace(
                            '/',
                            Path.DirectorySeparatorChar)));
                Assert.True(File.Exists(absolutePath), reference.StableId);
                Assert.AreEqual(
                    reference.AssetSha256,
                    Sha256(File.ReadAllBytes(absolutePath)),
                    reference.StableId);
            }
        }

        private static string Sha256(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(
                    algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }
    }
}
