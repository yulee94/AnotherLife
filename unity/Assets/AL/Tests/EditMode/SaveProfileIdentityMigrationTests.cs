using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AL.Core;
using AL.Core.SaveAuthority;
using AL.Data.Catalogs;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    /// <summary>
    /// Exercises the dormant schema-v1 to schema-v2 profile-identity migration
    /// executor and the strict validator's schema-2 recognition. It deliberately
    /// does not assert any Writable publication: the migration remains dormant
    /// until the separately reviewed current-mutator cutover train.
    /// </summary>
    public sealed class SaveProfileIdentityMigrationTests
    {
        private const string ProfileA =
            "alp_0123456789abcdef0123456789abcdef";
        private const string ProfileB =
            "alp_1123456789abcdef0123456789abcdef";

        // ------------------------------------------------------------------
        // Strict-validator schema-2 recognition
        // ------------------------------------------------------------------

        [Test]
        public void SchemaTwoValidatorRecognizesCanonicalProfileId()
        {
            SaveGameData save = CreateRealSchemaOneSave(out byte[] _);
            save.ProfileId = ProfileA;
            save.SaveSchemaVersion =
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion;
            save.ProfileInitializationVersion =
                SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion;

            SaveSemanticCandidate candidate = SaveSemanticCandidateValidator.Validate(
                Serialize(save),
                SaveCandidateSourceGeneration.Primary,
                SchemaTwoPolicy());

            Assert.AreEqual(SaveSemanticCandidateOutcome.Valid, candidate.Outcome);
            Assert.IsTrue(candidate.IsWritable);
            Assert.AreEqual(ProfileA, candidate.ProfileId);
        }

        [Test]
        public void SchemaOneValidatorRequiresMigrationUnderSchemaTwoPolicy()
        {
            SaveGameData save = CreateRealSchemaOneSave(out byte[] _);

            SaveSemanticCandidate candidate = SaveSemanticCandidateValidator.Validate(
                Serialize(save),
                SaveCandidateSourceGeneration.Primary,
                SchemaTwoPolicy());

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.MigrationRequired,
                candidate.Outcome);
            Assert.IsFalse(candidate.IsWritable);
            Assert.AreEqual(string.Empty, candidate.ProfileId);
        }

        [TestCase("")]
        [TestCase("alp_00000000000000000000000000000000")]
        [TestCase("alp_0123456789ABCDEF0123456789abcdef")]
        [TestCase("alp-0123456789abcdef0123456789abcdef")]
        [TestCase("not-a-profile-id")]
        public void SchemaTwoValidatorRejectsMissingBlankOrMalformedProfileId(
            string profileId)
        {
            SaveGameData save = CreateRealSchemaOneSave(out byte[] _);
            save.ProfileId = profileId ?? string.Empty;
            save.SaveSchemaVersion =
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion;
            save.ProfileInitializationVersion =
                SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion;

            SaveSemanticCandidate candidate = SaveSemanticCandidateValidator.Validate(
                Serialize(save),
                SaveCandidateSourceGeneration.Primary,
                SchemaTwoPolicy());

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.DegradedMalformed,
                candidate.Outcome);
            Assert.IsFalse(candidate.IsWritable);
            Assert.That(
                candidate.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("SAVE_SCHEMA_V2_PROFILE_ID_INVALID"));
        }

        // ------------------------------------------------------------------
        // Migration core
        // ------------------------------------------------------------------

        [Test]
        public void MigrationMintsCanonicalProfileIdAndBumpsSchema()
        {
            SaveGameData legacy = SchemaOneSave();
            byte[] predecessorBytes = Serialize(legacy);

            SaveProfileIdentityMigrationResult result =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    legacy,
                    predecessorBytes,
                    ProfileAuthoritySourceGeneration.Primary,
                    new FixedIdentitySource(ProfileA));

            Assert.IsTrue(result.IsMigrated);
            Assert.AreEqual(ProfileA, result.ProfileId);
            Assert.AreEqual(ProfileA, result.MigratedSave.ProfileId);
            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                result.MigratedSave.SaveSchemaVersion);
            Assert.AreEqual(
                SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion,
                result.MigratedSave.ProfileInitializationVersion);
            Assert.IsTrue(result.LedgerVerified);
            Assert.AreEqual(1, result.IdentityAttemptCount);
        }

        [Test]
        public void MigrationPreservesProfileData()
        {
            SaveGameData legacy = SchemaOneSave();
            legacy.SelectedRealm = RealmId.Umbral;
            legacy.CurrentChapterId = "C2";
            legacy.Resources = new List<ResourceData>
            {
                new ResourceData { Type = ResourceType.Food, Amount = 777 }
            };
            byte[] predecessorBytes = Serialize(legacy);

            SaveProfileIdentityMigrationResult result =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    legacy,
                    predecessorBytes,
                    ProfileAuthoritySourceGeneration.Primary,
                    new FixedIdentitySource(ProfileA));

            Assert.IsTrue(result.IsMigrated);
            Assert.AreEqual(RealmId.Umbral, result.MigratedSave.SelectedRealm);
            Assert.AreEqual("C2", result.MigratedSave.CurrentChapterId);
            Assert.AreEqual(1, result.MigratedSave.Resources.Count);
            Assert.AreEqual(777L, result.MigratedSave.Resources[0].Amount);
        }

        [Test]
        public void MigrationWitnessBindsPredecessorAndCandidate()
        {
            SaveGameData legacy = SchemaOneSave();
            byte[] predecessorBytes = Serialize(legacy);

            SaveProfileIdentityMigrationResult result =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    legacy,
                    predecessorBytes,
                    ProfileAuthoritySourceGeneration.Backup,
                    new FixedIdentitySource(ProfileA),
                    operationId: "al.save.test.operation.001");

            Assert.IsTrue(result.IsMigrated);
            ProfileIdentityMigrationWitnessRecord witness = result.Witness;
            Assert.NotNull(witness);
            Assert.AreEqual(
                ProfileIdentityMigrationTechnicalLimits.ContractVersion,
                witness.ContractVersion);
            Assert.AreEqual("al.save.test.operation.001", witness.OperationId);
            Assert.AreEqual(
                (int)ProfileAuthoritySourceGeneration.Backup,
                witness.SelectedLegacySourceGeneration);
            Assert.AreEqual(predecessorBytes.Length, witness.PredecessorByteCount);
            Assert.AreEqual(
                ComputeSha256(predecessorBytes),
                witness.PredecessorSha256);
            Assert.AreEqual(ProfileA, witness.ProfileId);
            Assert.AreEqual(
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion,
                witness.TargetSaveSchemaVersion);
            Assert.AreEqual(
                SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion,
                witness.TargetProfileInitializationVersion);
            Assert.AreEqual(result.CandidateBytes.Length, witness.CandidateByteCount);
            Assert.AreEqual(
                ComputeSha256(result.CandidateBytes),
                witness.CandidateSha256);
        }

        [Test]
        public void MigrationProvidesExactRollbackPredecessor()
        {
            SaveGameData legacy = SchemaOneSave();
            byte[] predecessorBytes = Serialize(legacy);

            SaveProfileIdentityMigrationResult result =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    legacy,
                    predecessorBytes,
                    ProfileAuthoritySourceGeneration.Primary,
                    new FixedIdentitySource(ProfileA));

            Assert.IsTrue(result.IsMigrated);
            CollectionAssert.AreEqual(predecessorBytes, result.PredecessorBytes);
            Assert.That(result.CandidateBytes, Is.Not.EqualTo(predecessorBytes));
        }

        [Test]
        public void MigrationRetriesCollisionsAgainstRetainedIdentities()
        {
            SaveGameData legacy = SchemaOneSave();
            byte[] predecessorBytes = Serialize(legacy);

            var identity = new FixedIdentitySource(
                Enumerable.Repeat(ProfileA, 7).Concat(new[] { ProfileB })
                    .ToArray());

            SaveProfileIdentityMigrationResult result =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    legacy,
                    predecessorBytes,
                    ProfileAuthoritySourceGeneration.Primary,
                    identity,
                    retainedProfileIds: new[] { ProfileA });

            Assert.IsTrue(result.IsMigrated);
            Assert.AreEqual(ProfileB, result.ProfileId);
            Assert.AreEqual(8, result.IdentityAttemptCount);
        }

        [Test]
        public void MigrationRejectsNonSchemaOneProfile()
        {
            SaveGameData migrated = SchemaOneSave();
            migrated.SaveSchemaVersion =
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion;
            migrated.ProfileId = ProfileA;

            SaveProfileIdentityMigrationResult result =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    migrated,
                    Serialize(migrated),
                    ProfileAuthoritySourceGeneration.Primary,
                    new FixedIdentitySource(ProfileB));

            Assert.AreEqual(
                SaveProfileIdentityMigrationStatus.Rejected,
                result.Status);
        }

        [Test]
        public void MigrationRejectsNullOrEmptyInputs()
        {
            SaveGameData legacy = SchemaOneSave();
            byte[] bytes = Serialize(legacy);
            var identity = new FixedIdentitySource(ProfileA);

            SaveProfileIdentityMigrationResult nullSave =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    null,
                    bytes,
                    ProfileAuthoritySourceGeneration.Primary,
                    identity);
            SaveProfileIdentityMigrationResult nullBytes =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    legacy,
                    null,
                    ProfileAuthoritySourceGeneration.Primary,
                    identity);
            SaveProfileIdentityMigrationResult emptyBytes =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    legacy,
                    Array.Empty<byte>(),
                    ProfileAuthoritySourceGeneration.Primary,
                    identity);
            SaveProfileIdentityMigrationResult nullIdentity =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    legacy,
                    bytes,
                    ProfileAuthoritySourceGeneration.Primary,
                    null);

            Assert.AreEqual(
                SaveProfileIdentityMigrationStatus.Rejected,
                nullSave.Status);
            Assert.AreEqual(
                SaveProfileIdentityMigrationStatus.Rejected,
                nullBytes.Status);
            Assert.AreEqual(
                SaveProfileIdentityMigrationStatus.Rejected,
                emptyBytes.Status);
            Assert.AreEqual(
                SaveProfileIdentityMigrationStatus.Rejected,
                nullIdentity.Status);
        }

        // ------------------------------------------------------------------
        // Old-save-load: a real schema-1 save migrates and validates as v2
        // ------------------------------------------------------------------

        [Test]
        public void RealSchemaOneSaveMigratesAndValidatesAsSchemaTwo()
        {
            SaveGameData legacy = CreateRealSchemaOneSave(out byte[] predecessorBytes);

            SaveSemanticCandidate baseline =
                SaveSemanticCandidateValidator.Validate(
                    predecessorBytes,
                    SaveCandidateSourceGeneration.Primary,
                    SchemaOnePolicy());
            Assert.AreEqual(
                SaveSemanticCandidateOutcome.Valid,
                baseline.Outcome,
                "Expected the produced save to be a valid schema-1 save.");

            SaveProfileIdentityMigrationResult result =
                SaveProfileIdentityMigration.MigrateSchemaOne(
                    legacy,
                    predecessorBytes,
                    ProfileAuthoritySourceGeneration.Primary,
                    new CryptographicProfileIdentityCandidateSource(),
                    operationId: "al.save.old-save-load.001");

            Assert.IsTrue(result.IsMigrated);
            Assert.IsTrue(result.LedgerVerified);

            SaveSemanticCandidate migratedCandidate =
                SaveSemanticCandidateValidator.Validate(
                    result.CandidateBytes,
                    SaveCandidateSourceGeneration.Primary,
                    SchemaTwoPolicy());
            Assert.AreEqual(
                SaveSemanticCandidateOutcome.Valid,
                migratedCandidate.Outcome);
            Assert.IsTrue(migratedCandidate.IsWritable);

            Assert.AreEqual(
                legacy.SelectedRealm,
                result.MigratedSave.SelectedRealm);
            Assert.AreEqual(
                legacy.Resources.Count,
                result.MigratedSave.Resources.Count);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static SaveGameData SchemaOneSave()
        {
            return new SaveGameData
            {
                SaveFormatId = SaveGameData.CurrentSaveFormatId,
                SaveSchemaVersion =
                    SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion,
                ProfileInitializationVersion =
                    SaveAuthorityTechnicalLimits
                        .LegacyProfileInitializationVersion,
                SelectedRealm = RealmId.Eldergrove,
                CurrentChapterId = "C1",
                Resources = new List<ResourceData>
                {
                    new ResourceData { Type = ResourceType.Food, Amount = 1000 }
                }
            };
        }

        private static SaveGameData CreateRealSchemaOneSave(out byte[] bytes)
        {
            bytes = File.ReadAllBytes(
                Path.Combine(
                    Application.dataPath,
                    "AL",
                    "Tests",
                    "EditMode",
                    "Fixtures",
                    "SaveSchema1",
                    "current-schema-v1.json"));
            return JsonUtility.FromJson<SaveGameData>(Encoding.UTF8.GetString(bytes));
        }

        private static byte[] Serialize(SaveGameData save) =>
            Encoding.UTF8.GetBytes(JsonUtility.ToJson(save, true));

        private static SaveSemanticValidationPolicy SchemaOnePolicy()
        {
            SaveSemanticValidationPolicy production = ProductionPolicy();
            return new SaveSemanticValidationPolicy(
                production.CurrentSaveFormatId,
                SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion,
                production.CurrentProfileInitializationVersion,
                production.Authority,
                production.MaximumInputBytes,
                production.MaximumDiagnostics,
                production.Nvs01Rule);
        }

        private static SaveSemanticValidationPolicy SchemaTwoPolicy()
        {
            return ProductionPolicy();
        }

        private static SaveSemanticValidationPolicy ProductionPolicy()
        {
            MethodInfo method = typeof(LocalSaveGameService).GetMethod(
                "CreateSemanticPolicy",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method, "Expected the production semantic policy factory.");
            return (SaveSemanticValidationPolicy)method.Invoke(null, null);
        }

        private static object CreateSaveService(string root)
        {
            Type serviceType = typeof(LocalSaveGameService);
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(
                constructor,
                "Expected the testable persistence-path constructor.");
            return constructor.Invoke(new object[] { root });
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType()
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .FirstOrDefault(candidate =>
                    candidate.Name == methodName &&
                    candidate.GetParameters().Length == args.Length);
            Assert.NotNull(method, $"Expected method {methodName}.");
            return method.Invoke(target, args);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (byte value in sha256.ComputeHash(bytes))
                {
                    builder.Append(
                        value.ToString(
                            "x2",
                            System.Globalization.CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private sealed class FixedIdentitySource : IProfileIdentityCandidateSource
        {
            private readonly string[] _candidates;
            private int _index;

            internal FixedIdentitySource(params string[] candidates)
            {
                _candidates = candidates;
            }

            public string GetCandidate(int attemptNumber)
            {
                return _candidates[
                    Math.Min(_index++, _candidates.Length - 1)];
            }
        }
    }
}
