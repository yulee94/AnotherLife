using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AL.Data.Catalogs;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    [TestFixture]
    public sealed class SaveSemanticCandidateValidationTests
    {
        private const string CurrentFormatId = "anotherlife.local-save";
        private const string Nvs01PacketHash =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private static readonly SaveSemanticValidationAuthority Authority =
            new SaveSemanticValidationAuthority(
                new[] { 0, 1, 2, 3, 4 },
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
                new[] { 0, 1, 2, 3 },
                new[] { 0, 1, 2, 3, 4, 5 },
                new[] { 0, 1, 2, 3 },
                new[] { 0, 1, 2, 3, 4, 5 },
                new[] { new SaveSemanticQuestRule("KNOWN_QUEST", 10) },
                new[]
                {
                    StableId(SaveSemanticStableIdKind.Chapter, "C1"),
                    StableId(SaveSemanticStableIdKind.Npc, "known_npc"),
                    StableId(SaveSemanticStableIdKind.Building, "building_a"),
                    StableId(SaveSemanticStableIdKind.Research, "research_a"),
                    StableId(SaveSemanticStableIdKind.Territory, "territory_a"),
                    StableId(SaveSemanticStableIdKind.RealmGem, "gem_a"),
                    StableId(SaveSemanticStableIdKind.Equipment, "equipment_a"),
                    StableId(SaveSemanticStableIdKind.BodyPreset, "average"),
                    StableId(SaveSemanticStableIdKind.HairStyle, "short"),
                    StableId(SaveSemanticStableIdKind.ArmorStyle, "realm_basic"),
                    StableId(SaveSemanticStableIdKind.FaceMark, "none"),
                    StableId(SaveSemanticStableIdKind.WeaponStyle, "sword"),
                    StableId(SaveSemanticStableIdKind.OffhandStyle, "shield")
                });
        private const string CurrentResources =
            "[{\"Type\":0,\"Amount\":1000}," +
            "{\"Type\":1,\"Amount\":1000}," +
            "{\"Type\":2,\"Amount\":500}," +
            "{\"Type\":3,\"Amount\":500}," +
            "{\"Type\":4,\"Amount\":150}," +
            "{\"Type\":5,\"Amount\":150}," +
            "{\"Type\":6,\"Amount\":0}," +
            "{\"Type\":7,\"Amount\":0}," +
            "{\"Type\":8,\"Amount\":0}," +
            "{\"Type\":9,\"Amount\":0}]";
        private const string LegacyResources =
            "[{\"Type\":0,\"Amount\":1000}," +
            "{\"Type\":1,\"Amount\":1000}," +
            "{\"Type\":2,\"Amount\":500}," +
            "{\"Type\":3,\"Amount\":500}]";
        private const string WishgateJson =
            "{\"IsEarned\":false,\"EarnReason\":\"\",\"LastRewardId\":\"\"," +
            "\"LastRewardChosenTimestamp\":0}";
        private const string WarmasterJson =
            "{\"EquippedSetId\":\"\",\"UnlockedSetIds\":[]," +
            "\"PurchasedPieceIds\":[],\"IsTrueWarmaster\":false," +
            "\"Level\":0,\"Experience\":0}";
        private const string CurrentChampionCustomizationJson =
            "{\"BodyPresetId\":\"average\",\"HairStyleId\":\"short\"," +
            "\"ArmorStyleId\":\"realm_basic\",\"FaceMarkId\":\"none\"," +
            "\"WeaponStyleId\":\"sword\",\"OffhandStyleId\":\"shield\"," +
            "\"PrimaryR\":0.2,\"PrimaryG\":0.4,\"PrimaryB\":1.0," +
            "\"HairR\":0.08,\"HairG\":0.06,\"HairB\":0.04," +
            "\"SkinR\":0.72,\"SkinG\":0.56,\"SkinB\":0.42," +
            "\"EyeR\":0.25,\"EyeG\":0.58,\"EyeB\":0.92," +
            "\"AccentR\":0.85,\"AccentG\":0.62,\"AccentB\":0.18," +
            "\"CapeEnabled\":true,\"HelmetEnabled\":false}";
        private const string LegacyChampionCustomizationJson =
            "{\"BodyPresetId\":\"average\",\"HairStyleId\":\"short\"," +
            "\"ArmorStyleId\":\"realm_basic\",\"PrimaryR\":0.2," +
            "\"PrimaryG\":0.4,\"PrimaryB\":1.0,\"HairR\":0.08," +
            "\"HairG\":0.06,\"HairB\":0.04,\"CapeEnabled\":true," +
            "\"HelmetEnabled\":false}";
        private const string NeutralNvs01ProgressJson =
            "{\"Version\":0,\"PacketVersion\":\"\",\"PacketSha256\":\"\"," +
            "\"QuestId\":\"\",\"Revision\":0,\"StateId\":\"\",\"Objectives\":[]," +
            "\"CurrentDialogueNodeId\":\"\",\"PendingChoice\":false," +
            "\"PendingSemanticActionId\":\"\",\"CommittedRealmId\":\"\"," +
            "\"EncounterStatus\":0,\"HasCurrentEncounter\":false," +
            "\"CurrentEncounter\":{\"ContractVersion\":0,\"RequestId\":\"\"," +
            "\"CorrelationId\":\"\",\"QuestId\":\"\",\"StateId\":\"\"," +
            "\"ObjectiveId\":\"\",\"HookId\":\"\",\"LocationId\":\"\"," +
            "\"RealmId\":\"\",\"SuccessEventId\":\"\",\"FailureEventId\":\"\"," +
            "\"CancelledEventId\":\"\",\"UnavailableEventId\":\"\"," +
            "\"ReturnScene\":\"\"},\"LastEncounterCorrelationId\":\"\"," +
            "\"HasLastEncounterOutcome\":false,\"LastEncounterOutcome\":0," +
            "\"LastEncounterEventId\":\"\",\"LastEncounterSnapshotVersion\":\"\"," +
            "\"LastEncounterSnapshotReference\":\"\",\"HasLastOperation\":false," +
            "\"LastOperation\":{\"OperationId\":\"\",\"PayloadFingerprint\":\"\"," +
            "\"Status\":0,\"Revision\":0,\"StateId\":\"\",\"EventId\":\"\"," +
            "\"CorrelationId\":\"\"},\"ConsequenceIntentIds\":[]," +
            "\"AcquiredArtifactIds\":[],\"AppliedEffectKeys\":[]," +
            "\"UnlockedChapterId\":\"\"}";

        [Test]
        public void CurrentSupportedFingerprintIsValidAndOwnsImmutableRawBytes()
        {
            byte[] input = Bytes(CurrentJson());
            byte[] expected = (byte[])input.Clone();

            SaveSemanticCandidate candidate = Validate(
                input,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.Valid, candidate.Outcome);
            Assert.AreEqual(1, candidate.SaveSchemaVersion);
            Assert.AreEqual(1, candidate.ProfileInitializationVersion);
            Assert.True(candidate.HasExplicitSaveSchemaVersion);
            Assert.True(candidate.HasExplicitProfileInitializationVersion);
            Assert.True(candidate.IsWritable);
            Assert.True(candidate.HasRetainedRawBytes);
            Assert.AreEqual(input.Length, candidate.OriginalRawByteCount);
            Assert.AreEqual(SaveSemanticDomain.None, candidate.DisabledDomains);

            input[0] = (byte)'[';
            CollectionAssert.AreEqual(expected, candidate.CopyRawBytes());

            byte[] firstCopy = candidate.CopyRawBytes();
            firstCopy[0] = (byte)'[';
            CollectionAssert.AreEqual(expected, candidate.CopyRawBytes());
        }

        [Test]
        public void MissingNvs01ProgressUsesOnlyTheApprovedNeutralDefault()
        {
            string json = CurrentJson().Replace(
                "\"Nvs01Progress\":" + NeutralNvs01ProgressJson + ",",
                string.Empty);

            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatibleNormalized,
                candidate.Outcome);
            Assert.True(candidate.IsWritable);
            AssertFlag(
                candidate.NormalizedDomains,
                SaveSemanticDomain.Narrative);
            Assert.That(
                candidate.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("SAVE_NVS01_PROGRESS_DEFAULTED"));
        }

        [Test]
        public void ForwardNvs01ProgressIsPreservedReadOnly()
        {
            string forward = NeutralNvs01ProgressJson.Replace(
                "\"Version\":0",
                "\"Version\":2");

            SaveSemanticCandidate candidate = Validate(
                CurrentJson(nvs01Progress: forward),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatiblePreservedUnknown,
                candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(
                candidate.PreservedUnknownDomains,
                SaveSemanticDomain.Narrative);
            Assert.That(
                candidate.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("SAVE_NVS01_VERSION_FORWARD"));
        }

        [Test]
        public void UnsupportedNvs01PacketIdentityIsPreservedReadOnly()
        {
            string unsupported = NeutralNvs01ProgressJson
                .Replace("\"Version\":0", "\"Version\":1")
                .Replace(
                    "\"PacketVersion\":\"\"",
                    "\"PacketVersion\":\"v002\"")
                .Replace(
                    "\"PacketSha256\":\"\"",
                    "\"PacketSha256\":\"" + Nvs01PacketHash + "\"")
                .Replace(
                    "\"QuestId\":\"\"",
                    "\"QuestId\":\"OMEN_1\"")
                .Replace(
                    "\"StateId\":\"\"",
                    "\"StateId\":\"OFFERED\"");

            SaveSemanticCandidate candidate = Validate(
                CurrentJson(nvs01Progress: unsupported),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatiblePreservedUnknown,
                candidate.Outcome);
            Assert.False(candidate.IsWritable);
            Assert.That(
                candidate.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("SAVE_NVS01_PACKET_IDENTITY_UNSUPPORTED"));
        }

        [Test]
        public void NeutralNvs01VersionRejectsAuthoredState()
        {
            string authoredNeutral = NeutralNvs01ProgressJson.Replace(
                "\"StateId\":\"\"",
                "\"StateId\":\"COMPLETED\"");

            SaveSemanticCandidate candidate = Validate(
                CurrentJson(nvs01Progress: authoredNeutral),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.DegradedMalformed,
                candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(
                candidate.DisabledDomains,
                SaveSemanticDomain.Narrative);
            Assert.That(
                candidate.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("SAVE_NVS01_NEUTRAL_STATE_INVALID"));
        }

        [Test]
        public void DocumentedLegacyFingerprintUsesOnlyApprovedNeutralDefaults()
        {
            SaveSemanticCandidate candidate = Validate(
                LegacyJson(),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatibleNormalized,
                candidate.Outcome);
            Assert.AreEqual(0, candidate.SaveSchemaVersion);
            Assert.AreEqual(0, candidate.ProfileInitializationVersion);
            Assert.False(candidate.HasExplicitSaveSchemaVersion);
            Assert.False(candidate.HasExplicitProfileInitializationVersion);
            Assert.True(candidate.IsWritable);
            AssertFlag(candidate.NormalizedDomains, SaveSemanticDomain.Metadata);
            AssertFlag(candidate.NormalizedDomains, SaveSemanticDomain.Relationships);
            AssertFlag(candidate.NormalizedDomains, SaveSemanticDomain.Equipment);
            Assert.That(
                candidate.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("SAVE_LEGACY_SCHEMA_VERSION"));
            Assert.That(
                candidate.Diagnostics.Select(diagnostic => diagnostic.Code),
                Does.Contain("SAVE_COMPATIBLE_FIELD_DEFAULTED"));
        }

        [Test]
        public void LegacyFingerprintMissingAnOriginalFieldIsInvalid()
        {
            string missingWarmaster = LegacyJson()
                .Replace("\"Warmaster\":" + WarmasterJson + ",", string.Empty);

            SaveSemanticCandidate candidate = Validate(
                missingWarmaster,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.Invalid, candidate.Outcome);
            Assert.AreEqual("SAVE_LEGACY_FINGERPRINT_INCOMPLETE", candidate.Diagnostics.Last().Code);
        }

        [Test]
        public void CurrentAndLegacyFingerprintsRequireTheirHistoricallyInitializedCoreResources()
        {
            string currentMissingOre = CurrentResources.Replace(
                ",{\"Type\":5,\"Amount\":150}",
                string.Empty);
            SaveSemanticCandidate current = Validate(
                CurrentJson(resources: currentMissingOre),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, current.Outcome);
            Assert.False(current.IsWritable);
            AssertFlag(current.DisabledDomains, SaveSemanticDomain.Resources);

            string legacyMissingGold = LegacyResources.Replace(
                ",{\"Type\":3,\"Amount\":500}",
                string.Empty);
            SaveSemanticCandidate legacy = Validate(
                LegacyJson(resources: legacyMissingGold),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, legacy.Outcome);
            Assert.False(legacy.IsWritable);
            AssertFlag(legacy.DisabledDomains, SaveSemanticDomain.Resources);
        }

        [TestCase("Wishgate", "{}", SaveSemanticDomain.Envelope)]
        [TestCase(
            "Wishgate",
            "{\"IsEarned\":false}",
            SaveSemanticDomain.Envelope)]
        [TestCase("Warmaster", "{}", SaveSemanticDomain.Warmaster)]
        [TestCase(
            "Warmaster",
            "{\"Level\":0}",
            SaveSemanticDomain.Warmaster)]
        [TestCase(
            "ChampionCustomization",
            "{}",
            SaveSemanticDomain.Customization)]
        [TestCase(
            "ChampionCustomization",
            "{\"BodyPresetId\":\"average\"}",
            SaveSemanticDomain.Customization)]
        public void CurrentRequiredNestedObjectsRejectEmptyAndTruncatedFingerprints(
            string fieldName,
            string objectJson,
            SaveSemanticDomain expectedDomain)
        {
            SaveSemanticCandidate candidate = Validate(
                CurrentJsonWithNestedObject(fieldName, objectJson),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.DisabledDomains, expectedDomain);
            Assert.True(
                candidate.Diagnostics.Any(
                    diagnostic =>
                        diagnostic.Severity == SaveSemanticDiagnosticSeverity.Error &&
                        diagnostic.Path.StartsWith(
                            "$." + fieldName,
                            StringComparison.Ordinal)));
        }

        [TestCase("Buildings", SaveSemanticDomain.Buildings)]
        [TestCase("Troops", SaveSemanticDomain.Troops)]
        [TestCase("Researches", SaveSemanticDomain.Research)]
        [TestCase("Territories", SaveSemanticDomain.Territories)]
        [TestCase("RealmGems", SaveSemanticDomain.RealmGems)]
        [TestCase("OwnedEquipment", SaveSemanticDomain.Equipment)]
        public void MalformedRowsFailClosedAcrossEveryStructuredSaveDomain(
            string fieldName,
            SaveSemanticDomain expectedDomain)
        {
            SaveSemanticCandidate candidate = Validate(
                CurrentJsonWithRows(fieldName, MalformedRows(fieldName)),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.DisabledDomains, expectedDomain);

            string rowPrefix = "$." + fieldName + "[";
            string[] errorPaths = candidate.Diagnostics
                .Where(
                    diagnostic =>
                        diagnostic.Domain == expectedDomain &&
                        diagnostic.Severity == SaveSemanticDiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.Path)
                .ToArray();
            Assert.That(errorPaths.Length, Is.GreaterThanOrEqualTo(4));
            Assert.True(
                errorPaths.Any(
                    path => path.StartsWith(rowPrefix + "0]", StringComparison.Ordinal)),
                "The null row must be diagnosed.");
            Assert.True(
                errorPaths.Any(
                    path => path.StartsWith(rowPrefix + "1]", StringComparison.Ordinal)),
                "The blank or invalid field must be diagnosed.");
            Assert.True(
                errorPaths.Any(
                    path => path.StartsWith(rowPrefix + "3]", StringComparison.Ordinal)),
                "The duplicate row must be diagnosed.");
            Assert.True(
                errorPaths.Any(
                    path => path.StartsWith(rowPrefix + "4]", StringComparison.Ordinal)),
                "The domain-specific invalid state must be diagnosed.");
        }

        [TestCase("Buildings", SaveSemanticDomain.Buildings)]
        [TestCase("Troops", SaveSemanticDomain.Troops)]
        [TestCase("Researches", SaveSemanticDomain.Research)]
        [TestCase("Territories", SaveSemanticDomain.Territories)]
        [TestCase("RealmGems", SaveSemanticDomain.RealmGems)]
        [TestCase("OwnedEquipment", SaveSemanticDomain.Equipment)]
        public void UnknownPropertiesInStructuredRowsArePreservedAndReadOnly(
            string fieldName,
            SaveSemanticDomain expectedDomain)
        {
            string json = CurrentJsonWithRows(fieldName, RowWithFutureField(fieldName));

            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatiblePreservedUnknown,
                candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.PreservedUnknownDomains, expectedDomain);
            SaveSemanticDiagnostic diagnostic = candidate.Diagnostics.Single(
                item => item.Code == "SAVE_UNKNOWN_NESTED_FIELD");
            Assert.AreEqual("$." + fieldName + "[0].FutureField", diagnostic.Path);
            Assert.AreEqual(json, Encoding.UTF8.GetString(candidate.CopyRawBytes()));
        }

        [Test]
        public void CatalogBackedUnknownStableIdIsPreservedInsteadOfFalseClean()
        {
            const string buildings =
                "[{\"BuildingId\":\"FUTURE_BUILDING\",\"Level\":1," +
                "\"IsUpgrading\":false,\"UpgradeCompleteTimestamp\":0}]";
            SaveSemanticCandidate candidate = Validate(
                CurrentJson(buildings: buildings),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatiblePreservedUnknown,
                candidate.Outcome);
            Assert.True(candidate.IsWritable);
            AssertFlag(candidate.PreservedUnknownDomains, SaveSemanticDomain.Buildings);
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_STABLE_ID_PRESERVED_UNKNOWN"));
        }

        [TestCase(
            11,
            false,
            0,
            "SAVE_BUILDING_LEVEL_ABOVE_CAP")]
        [TestCase(
            10,
            true,
            1700000000,
            "SAVE_BUILDING_UPGRADE_ABOVE_CAP")]
        public void BuildingLevelCapContradictionsFailClosed(
            int level,
            bool isUpgrading,
            long completionTimestamp,
            string expectedCode)
        {
            string buildings =
                "[{\"BuildingId\":\"building_a\",\"Level\":" + level +
                ",\"IsUpgrading\":" + isUpgrading.ToString().ToLowerInvariant() +
                ",\"UpgradeCompleteTimestamp\":" + completionTimestamp + "}]";

            SaveSemanticCandidate candidate = Validate(
                CurrentJson(buildings: buildings),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.DegradedMalformed,
                candidate.Outcome);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Buildings);
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain(expectedCode));
        }

        [Test]
        public void RealmGemHomeRealmCannotUseNone()
        {
            const string gems =
                "[{\"GemId\":\"gem_a\",\"HomeRealm\":0,\"GemIndex\":1," +
                "\"IsAtHome\":true,\"IsDropped\":false,\"CarrierId\":\"\"," +
                "\"LastDroppedTimestamp\":0}]";
            SaveSemanticCandidate candidate = Validate(
                CurrentJson(realmGems: gems),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.RealmGems);
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_GEM_HOME_REALM_NONE"));
        }

        [Test]
        public void OpaqueDisplayTextDoesNotUseStableIdLengthLimit()
        {
            string text = new string('x', 300);
            string territories =
                "[{\"Id\":\"territory_a\",\"Name\":\"" + text + "\"," +
                "\"OwnerRealm\":1,\"BonusType\":0,\"BonusAmount\":1," +
                "\"IsFortress\":false}]";
            string wishgate =
                "{\"IsEarned\":false,\"EarnReason\":\"" + text + "\"," +
                "\"LastRewardId\":\"\",\"LastRewardChosenTimestamp\":0}";
            string equipment = "[" +
                EquipmentRow("equipment_a", 0, 1, 1, false)
                    .Replace("\"DisplayName\":\"Equipment\"", "\"DisplayName\":\"" + text + "\"") +
                "]";

            SaveSemanticCandidate candidate = Validate(
                CurrentJson(
                    territories: territories,
                    wishgate: wishgate,
                    ownedEquipment: equipment),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.Valid, candidate.Outcome);
            Assert.True(candidate.IsWritable);
        }

        [Test]
        public void UnknownResourceEnumIsPreservedExactlyAndMakesCandidateReadOnly()
        {
            string resources = CurrentResources.Substring(
                0,
                CurrentResources.Length - 1) +
                ",{\"Type\":99,\"Amount\":7}]";
            string json = CurrentJson(resources: resources);

            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatiblePreservedUnknown,
                candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.PreservedUnknownDomains, SaveSemanticDomain.Resources);
            Assert.AreEqual(json, Encoding.UTF8.GetString(candidate.CopyRawBytes()));
        }

        [Test]
        public void RelationshipAffinityOutsideFloatSafeRangeIsDegraded()
        {
            const string reputation =
                "[{\"NpcId\":\"npc_positive_overflow\",\"Affinity\":3.5e38}," +
                "{\"NpcId\":\"npc_negative_overflow\",\"Affinity\":-3.5e38}]";

            SaveSemanticCandidate candidate = Validate(
                CurrentJson(reputation: reputation),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Relationships);
            Assert.That(
                candidate.Diagnostics.Count(
                    diagnostic =>
                        diagnostic.Domain == SaveSemanticDomain.Relationships &&
                        diagnostic.Severity == SaveSemanticDiagnosticSeverity.Error),
                Is.GreaterThanOrEqualTo(2));
        }

        [TestCase("1e-4000")]
        [TestCase("5e-324")]
        public void NonzeroFloatUnderflowIsDegradedWithoutChangingRawBytes(string token)
        {
            string reputation =
                "[{\"NpcId\":\"known_npc\",\"Affinity\":" + token + "}]";
            string json = CurrentJson(reputation: reputation);

            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Relationships);
            Assert.AreEqual(json, Encoding.UTF8.GetString(candidate.CopyRawBytes()));

            string customization = CurrentChampionCustomizationJson.Replace(
                "\"PrimaryR\":0.2",
                "\"PrimaryR\":" + token);
            SaveSemanticCandidate customizationCandidate = Validate(
                CurrentJson(championCustomization: customization),
                SaveCandidateSourceGeneration.Primary);
            Assert.AreEqual(
                SaveSemanticCandidateOutcome.DegradedMalformed,
                customizationCandidate.Outcome);
            AssertFlag(
                customizationCandidate.DisabledDomains,
                SaveSemanticDomain.Customization);
        }

        [Test]
        public void UnsupportedBodyBaseIdIsMalformedInsteadOfPreservedUnknown()
        {
            string customization = CurrentChampionCustomizationJson.Insert(
                1,
                "\"BodyBaseId\":\"other\",");

            SaveSemanticCandidate candidate = Validate(
                CurrentJson(championCustomization: customization),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Customization);
            Assert.That(
                candidate.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "SAVE_CUSTOMIZATION_BODY_BASE_INVALID" &&
                    diagnostic.Path == "$.ChampionCustomization.BodyBaseId"),
                Is.True);
        }

        [TestCase("0")]
        [TestCase("-0")]
        [TestCase("0.0")]
        [TestCase("0e4000")]
        public void SyntacticZeroFloatFormsRemainValid(string token)
        {
            string reputation =
                "[{\"NpcId\":\"known_npc\",\"Affinity\":" + token + "}]";
            SaveSemanticCandidate candidate = Validate(
                CurrentJson(reputation: reputation),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.Valid, candidate.Outcome);
            Assert.True(candidate.IsWritable);
        }

        [Test]
        public void UnknownUnderflowNumberRemainsRawPreserved()
        {
            string json = CurrentJson(extraTopLevel: ",\"FutureRatio\":1e-4000");
            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatiblePreservedUnknown,
                candidate.Outcome);
            Assert.False(candidate.IsWritable);
            Assert.AreEqual(json, Encoding.UTF8.GetString(candidate.CopyRawBytes()));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void LastSavedTimestampMustBeStrictlyPositive(long timestamp)
        {
            SaveSemanticCandidate candidate = Validate(
                CurrentJson(lastSavedTimestamp: timestamp),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Envelope);
            Assert.That(
                candidate.Diagnostics.Any(
                    diagnostic => diagnostic.Path == "$.LastSavedTimestamp"),
                Is.True);
        }

        [TestCase("SaveSchemaVersion")]
        [TestCase("ProfileInitializationVersion")]
        public void ExplicitNullMetadataIsInvalidRatherThanLegacyOmission(
            string metadataField)
        {
            SaveSemanticCandidate omitted = Validate(
                LegacyJson(),
                SaveCandidateSourceGeneration.Primary);
            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatibleNormalized,
                omitted.Outcome);

            string explicitNull = LegacyJson().Insert(
                1,
                "\"" + metadataField + "\":null,");
            SaveSemanticCandidate candidate = Validate(
                explicitNull,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.Invalid, candidate.Outcome);
            Assert.False(candidate.IsWritable);
            Assert.That(
                candidate.Diagnostics.Any(
                    diagnostic => diagnostic.Path == "$." + metadataField),
                Is.True);
        }

        [TestCase("SaveSchemaVersion")]
        [TestCase("ProfileInitializationVersion")]
        public void ExplicitZeroLegacyMetadataIsMalformedRatherThanHistoricalOmission(
            string metadataField)
        {
            string json = LegacyJson().Insert(
                1,
                "\"" + metadataField + "\":0,");

            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Metadata);
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_LEGACY_METADATA_EXPLICIT"));
        }

        [Test]
        public void PartialLegacyPersonaIsMalformedWhileWholeOmissionNormalizes()
        {
            SaveSemanticCandidate omitted = Validate(
                LegacyJson(),
                SaveCandidateSourceGeneration.Primary);
            Assert.AreEqual(SaveSemanticCandidateOutcome.CompatibleNormalized, omitted.Outcome);

            string partialJson = LegacyJson().Replace(
                "\"Territories\":[]",
                "\"LordPersona\":{\"Warlord\":0},\"Territories\":[]");
            SaveSemanticCandidate partial = Validate(
                partialJson,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, partial.Outcome);
            Assert.False(partial.IsWritable);
            AssertFlag(partial.DisabledDomains, SaveSemanticDomain.Relationships);
            Assert.That(
                partial.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_PERSONA_FIELD_MISSING"));
        }

        [TestCase("")]
        [TestCase("null")]
        [TestCase("\"credits\"")]
        public void LegacyWarzoneCreditsMustRemainPresentAndInteger(string replacement)
        {
            string fragment = "\"WarzoneCredits\":0,";
            string rewritten = replacement.Length == 0
                ? string.Empty
                : "\"WarzoneCredits\":" + replacement + ",";
            SaveSemanticCandidate candidate = Validate(
                LegacyJson().Replace(fragment, rewritten),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Resources);
        }

        [Test]
        public void LegacyWalletDeclaresZeroMigrationsForLaterCoreResources()
        {
            SaveSemanticCandidate candidate = Validate(
                LegacyJson(),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.CompatibleNormalized, candidate.Outcome);
            AssertFlag(candidate.NormalizedDomains, SaveSemanticDomain.Resources);
            Assert.AreEqual(
                2,
                candidate.Diagnostics.Count(
                    item => item.Code == "SAVE_LEGACY_RESOURCE_ZERO_MIGRATION"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyQuestCollectionOmissionAndNullUseApprovedNeutralNormalization(
            bool explicitNull)
        {
            string questsFragment = "\"Quests\":[],";
            string replacement = explicitNull ? "\"Quests\":null," : string.Empty;
            string json = LegacyJson().Replace(questsFragment, replacement);

            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatibleNormalized,
                candidate.Outcome);
            Assert.True(candidate.IsWritable);
            AssertFlag(candidate.NormalizedDomains, SaveSemanticDomain.Quests);
            Assert.That(
                candidate.Diagnostics.Any(
                    diagnostic =>
                        diagnostic.Code == "SAVE_COMPATIBLE_FIELD_DEFAULTED" &&
                        diagnostic.Path == "$.Quests"),
                Is.True);
        }

        [Test]
        public void UnknownNonblankQuestIsPreservedWithoutDisablingKnownQuestDomain()
        {
            const string quests =
                "[{\"QuestId\":\"FUTURE_QUEST\",\"CurrentValue\":7," +
                "\"IsCompleted\":true,\"IsClaimed\":true}]";
            string json = CurrentJson(quests: quests);

            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatiblePreservedUnknown,
                candidate.Outcome);
            Assert.True(candidate.IsWritable);
            Assert.AreEqual(SaveSemanticDomain.None, candidate.DisabledDomains);
            AssertFlag(candidate.PreservedUnknownDomains, SaveSemanticDomain.Quests);
            SaveSemanticDiagnostic diagnostic = candidate.Diagnostics.Single(
                item => item.Code == "SAVE_QUEST_ID_PRESERVED_UNKNOWN");
            Assert.AreEqual("$.Quests[0].QuestId", diagnostic.Path);
            Assert.AreEqual(json, Encoding.UTF8.GetString(candidate.CopyRawBytes()));
        }

        [Test]
        public void UnknownQuestRetainsFutureSemanticStateWithoutKnownDefinitionRules()
        {
            const string quests =
                "[{\"QuestId\":\"FUTURE_QUEST\",\"CurrentValue\":-7," +
                "\"IsCompleted\":false,\"IsClaimed\":true}]";
            string json = CurrentJson(quests: quests);

            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatiblePreservedUnknown,
                candidate.Outcome);
            Assert.True(candidate.IsWritable);
            Assert.AreEqual(SaveSemanticDomain.None, candidate.DisabledDomains);
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Not.Contain("SAVE_QUEST_PROGRESS_NEGATIVE"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Not.Contain("SAVE_QUEST_STATE_CONTRADICTORY"));
            Assert.AreEqual(json, Encoding.UTF8.GetString(candidate.CopyRawBytes()));
        }

        [Test]
        public void UnexpectedNestedPropertyIsPreservedAndMakesCandidateReadOnly()
        {
            const string quests =
                "[{\"QuestId\":\"KNOWN_QUEST\",\"CurrentValue\":0," +
                "\"IsCompleted\":false,\"IsClaimed\":false,\"FutureState\":9}]";

            SaveSemanticCandidate candidate = Validate(
                CurrentJson(quests: quests),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatiblePreservedUnknown,
                candidate.Outcome);
            Assert.False(candidate.IsWritable);
            Assert.AreEqual(
                "$.Quests[0].FutureState",
                candidate.Diagnostics.Single(
                    item => item.Code == "SAVE_UNKNOWN_NESTED_FIELD").Path);
        }

        [Test]
        public void NullBlankDuplicateRowsDegradeOnlyTheirDomainsAndRemainUnchanged()
        {
            const string resources =
                "[null,{}, {\"Type\":0,\"Amount\":3},{\"Type\":0,\"Amount\":4}]";
            const string quests =
                "[null,{\"QuestId\":\"\",\"CurrentValue\":0,\"IsCompleted\":false," +
                "\"IsClaimed\":false},{\"QuestId\":\"KNOWN_QUEST\",\"CurrentValue\":0," +
                "\"IsCompleted\":false,\"IsClaimed\":false},{\"QuestId\":\"KNOWN_QUEST\"," +
                "\"CurrentValue\":0,\"IsCompleted\":false,\"IsClaimed\":false}]";
            const string reputation =
                "[null,{\"NpcId\":\"\",\"Affinity\":0},{\"NpcId\":\"npc_a\",\"Affinity\":1}," +
                "{\"NpcId\":\"npc_a\",\"Affinity\":2}]";
            const string factions =
                "[{\"FactionId\":\"faction_a\",\"Reputation\":1}," +
                "{\"FactionId\":\"faction_a\",\"Reputation\":2}]";
            string json = CurrentJson(
                resources: resources,
                quests: quests,
                reputation: reputation,
                factionReputations: factions);

            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Resources);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Quests);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Relationships);
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_RESOURCE_ROW_NULL"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_RESOURCE_ID_BLANK_OR_INVALID"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_RESOURCE_ID_DUPLICATE"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_QUEST_ROW_NULL"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_QUEST_ID_BLANK"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_QUEST_ID_DUPLICATE"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_RELATIONSHIP_ROW_NULL"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_RELATIONSHIP_ID_BLANK"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_RELATIONSHIP_ID_DUPLICATE"));
            Assert.AreEqual(json, Encoding.UTF8.GetString(candidate.CopyRawBytes()));
        }

        [Test]
        public void NegativeAndContradictoryValuesAreDegradedWithoutRepair()
        {
            const string resources = "[{\"Type\":0,\"Amount\":-1}]";
            const string quests =
                "[{\"QuestId\":\"KNOWN_QUEST\",\"CurrentValue\":-1," +
                "\"IsCompleted\":false,\"IsClaimed\":true}]";

            SaveSemanticCandidate candidate = Validate(
                CurrentJson(resources: resources, quests: quests),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_RESOURCE_AMOUNT_NEGATIVE"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_QUEST_PROGRESS_NEGATIVE"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_QUEST_STATE_CONTRADICTORY"));
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Not.Contain("SAVE_REPAIR_APPLIED"));
        }

        [Test]
        public void ExactIntegerParsingRejectsExponentAndOverflowForms()
        {
            SaveSemanticCandidate exponentSchema = Validate(
                CurrentJson().Replace("\"SaveSchemaVersion\":1", "\"SaveSchemaVersion\":1e0"),
                SaveCandidateSourceGeneration.Primary);
            Assert.AreEqual(SaveSemanticCandidateOutcome.Invalid, exponentSchema.Outcome);
            Assert.AreEqual("SAVE_SCHEMA_VERSION_INVALID", exponentSchema.Diagnostics.Last().Code);

            SaveSemanticCandidate overflowAmount = Validate(
                CurrentJson(resources:
                    "[{\"Type\":0,\"Amount\":9223372036854775808}]"),
                SaveCandidateSourceGeneration.Primary);
            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, overflowAmount.Outcome);
            Assert.That(
                overflowAmount.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_RESOURCE_AMOUNT_INVALID"));
        }

        [Test]
        public void CurrentSchemaWithMissingInitializationMetadataIsDegraded()
        {
            string missingInitialization = CurrentJson()
                .Replace("\"ProfileInitializationVersion\":1,", string.Empty);

            SaveSemanticCandidate candidate = Validate(
                missingInitialization,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.False(candidate.IsWritable);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Metadata);
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_INITIALIZATION_VERSION_MISSING"));
        }

        [Test]
        public void KnownQuestStateMustMatchItsAuthoritativeTarget()
        {
            const string quests =
                "[{\"QuestId\":\"KNOWN_QUEST\",\"CurrentValue\":0," +
                "\"IsCompleted\":true,\"IsClaimed\":false}]";

            SaveSemanticCandidate candidate = Validate(
                CurrentJson(quests: quests),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            AssertFlag(candidate.DisabledDomains, SaveSemanticDomain.Quests);
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_QUEST_STATE_CONTRADICTORY"));
        }

        [Test]
        public void ForwardSchemaUsesStableFormatIdentityAndRemainsReadOnly()
        {
            SaveSemanticCandidate forward = Validate(
                CurrentJson(schemaVersion: 2),
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.ForwardSchemaReadOnly,
                forward.Outcome);
            Assert.False(forward.IsWritable);
            Assert.AreEqual(SaveSemanticDomain.All, forward.DisabledDomains);

            SaveSemanticCandidate reshapedForward = Validate(
                "{\"SaveFormatId\":\"" + CurrentFormatId + "\"," +
                "\"SaveSchemaVersion\":2,\"ProfileInitializationVersion\":1," +
                "\"FutureEnvelope\":{\"Profile\":true}}",
                SaveCandidateSourceGeneration.Primary);
            Assert.AreEqual(
                SaveSemanticCandidateOutcome.ForwardSchemaReadOnly,
                reshapedForward.Outcome);

            SaveSemanticCandidate unmarkedForward = Validate(
                "{\"SaveSchemaVersion\":2,\"ProfileInitializationVersion\":1}",
                SaveCandidateSourceGeneration.Primary);
            Assert.AreEqual(SaveSemanticCandidateOutcome.Invalid, unmarkedForward.Outcome);
            Assert.That(
                unmarkedForward.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_FORWARD_FINGERPRINT_INCOMPLETE"));
        }

        [Test]
        public void CurrentFormatIdentityWithoutMaterialProfileDataIsInvalid()
        {
            SaveSemanticCandidate candidate = Validate(
                "{\"SaveFormatId\":\"" + CurrentFormatId + "\"," +
                "\"SaveSchemaVersion\":1,\"ProfileInitializationVersion\":1}",
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.Invalid, candidate.Outcome);
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_MATERIAL_FOOTPRINT_INCOMPLETE"));
        }

        [Test]
        public void StageOneNeverLabelsUnsupportedLowerSchemaAsRepaired()
        {
            var futurePolicy = new SaveSemanticValidationPolicy(
                CurrentFormatId,
                2,
                1,
                Authority);
            SaveSemanticCandidate candidate = SaveSemanticCandidateValidator.Validate(
                Bytes(CurrentJson()),
                SaveCandidateSourceGeneration.Primary,
                futurePolicy);

            Assert.AreEqual(SaveSemanticCandidateOutcome.DegradedMalformed, candidate.Outcome);
            Assert.AreNotEqual(
                SaveSemanticCandidateOutcome.RepairableWithDataChange,
                candidate.Outcome);
            Assert.That(
                candidate.Diagnostics.Select(item => item.Code),
                Does.Contain("SAVE_LOWER_SCHEMA_UNSUPPORTED"));
        }

        [Test]
        public void OutcomeModelRetainsEveryPolicyDisposition()
        {
            CollectionAssert.AreEquivalent(
                new[]
                {
                    SaveSemanticCandidateOutcome.Valid,
                    SaveSemanticCandidateOutcome.CompatibleNormalized,
                    SaveSemanticCandidateOutcome.CompatiblePreservedUnknown,
                    SaveSemanticCandidateOutcome.DegradedMalformed,
                    SaveSemanticCandidateOutcome.RepairableWithDataChange,
                    SaveSemanticCandidateOutcome.Invalid,
                    SaveSemanticCandidateOutcome.ForwardSchemaReadOnly,
                    SaveSemanticCandidateOutcome.OversizePreservedReadOnly
                },
                Enum.GetValues(typeof(SaveSemanticCandidateOutcome)));
        }

        [TestCase("{")]
        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase("{\"SelectedRealm\":1}")]
        public void MalformedNonobjectEmptyAndOneFieldInputsAreInvalid(string json)
        {
            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(SaveSemanticCandidateOutcome.Invalid, candidate.Outcome);
            Assert.False(candidate.IsWritable);
        }

        [Test]
        public void StrictJsonRejectsDuplicatePropertiesAndInvalidUtf8()
        {
            SaveSemanticCandidate duplicate = Validate(
                CurrentJson(extraTopLevel: ",\"SaveSchemaVersion\":1"),
                SaveCandidateSourceGeneration.Primary);
            Assert.AreEqual(SaveSemanticCandidateOutcome.Invalid, duplicate.Outcome);
            Assert.That(
                duplicate.Diagnostics.Single().Code,
                Does.Contain("PROPERTY_DUPLICATE"));

            SaveSemanticCandidate invalidUtf8 = SaveSemanticCandidateValidator.Validate(
                new byte[] { 0x7b, 0x22, 0xff, 0x22, 0x3a, 0x31, 0x7d },
                SaveCandidateSourceGeneration.Primary,
                Policy());
            Assert.AreEqual(SaveSemanticCandidateOutcome.Invalid, invalidUtf8.Outcome);
            Assert.That(
                invalidUtf8.Diagnostics.Single().Code,
                Does.Contain("UTF8_INVALID"));
        }

        [Test]
        public void UnknownHugeNumberIsPreservedWithoutLeakingItsPropertyName()
        {
            string privatePropertyName = new string('p', 200);
            string json = CurrentJson(
                extraTopLevel: ",\"" + privatePropertyName + "\":1e400");

            SaveSemanticCandidate candidate = Validate(
                json,
                SaveCandidateSourceGeneration.Primary);

            Assert.AreEqual(
                SaveSemanticCandidateOutcome.CompatiblePreservedUnknown,
                candidate.Outcome);
            Assert.False(candidate.IsWritable);
            SaveSemanticDiagnostic diagnostic = candidate.Diagnostics.Single(
                item => item.Code == "SAVE_UNKNOWN_TOP_LEVEL_FIELD");
            Assert.AreEqual("$.<property>", diagnostic.Path);
            Assert.That(diagnostic.Path, Does.Not.Contain(privatePropertyName));
            Assert.AreEqual(json, Encoding.UTF8.GetString(candidate.CopyRawBytes()));
        }

        [Test]
        public void InputAndDiagnosticBoundsAreEnforced()
        {
            Assert.AreEqual(
                1024 * 1024,
                SaveSemanticValidationPolicy.DefaultMaximumInputBytes);
            Assert.AreEqual(
                SaveSemanticValidationPolicy.DefaultMaximumInputBytes,
                SaveSemanticValidationPolicy.AbsoluteMaximumInputBytes);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SaveSemanticValidationPolicy(
                    CurrentFormatId,
                    1,
                    1,
                    Authority,
                    maximumInputBytes:
                        SaveSemanticValidationPolicy.AbsoluteMaximumInputBytes + 1));

            var smallInputPolicy = new SaveSemanticValidationPolicy(
                CurrentFormatId,
                1,
                1,
                Authority,
                maximumInputBytes: 128);
            byte[] oversized = Enumerable.Repeat((byte)' ', 129).ToArray();
            SaveSemanticCandidate oversizedCandidate =
                SaveSemanticCandidateValidator.Validate(
                    oversized,
                    SaveCandidateSourceGeneration.Primary,
                    smallInputPolicy);
            Assert.AreEqual(
                SaveSemanticCandidateOutcome.OversizePreservedReadOnly,
                oversizedCandidate.Outcome);
            Assert.AreEqual(129, oversizedCandidate.OriginalRawByteCount);
            Assert.False(oversizedCandidate.HasRetainedRawBytes);
            Assert.Null(oversizedCandidate.CopyRawBytes());

            SaveSemanticCandidate validBackup = Validate(
                CurrentJson(),
                SaveCandidateSourceGeneration.Backup);
            SaveSemanticCandidateSelection protectedSelection =
                SaveSemanticCandidateSelector.Select(oversizedCandidate, validBackup);
            Assert.False(protectedSelection.HasSelection);
            Assert.Null(protectedSelection.SelectedCandidate);
            Assert.AreEqual(
                "SAVE_SELECT_OVERSIZE_PRIMARY_RECOVERY_REQUIRED",
                protectedSelection.ReasonCode);

            string manyNullRows = "[" + string.Join(
                ",",
                Enumerable.Repeat("null", 12)) + "]";
            var cappedPolicy = new SaveSemanticValidationPolicy(
                CurrentFormatId,
                1,
                1,
                Authority,
                maximumDiagnostics: 4);
            SaveSemanticCandidate capped = SaveSemanticCandidateValidator.Validate(
                Bytes(CurrentJson(quests: manyNullRows)),
                SaveCandidateSourceGeneration.Primary,
                cappedPolicy);
            Assert.AreEqual(4, capped.Diagnostics.Count);
            Assert.AreEqual("SAVE_DIAGNOSTICS_TRUNCATED", capped.Diagnostics.Last().Code);

            var list = (IList<SaveSemanticDiagnostic>)capped.Diagnostics;
            Assert.Throws<NotSupportedException>(
                () => list.Add(capped.Diagnostics[0]));
        }

        [Test]
        public void OversizeGenerationsArePreservedButNeverPresentedAsLoadCandidates()
        {
            SaveSemanticCandidate oversizedPrimary = Oversize(
                SaveCandidateSourceGeneration.Primary);
            SaveSemanticCandidate oversizedBackup = Oversize(
                SaveCandidateSourceGeneration.Backup);
            SaveSemanticCandidate oversizedPrevious = Oversize(
                SaveCandidateSourceGeneration.Previous);
            SaveSemanticCandidate validPrimary = Validate(
                CurrentJson(),
                SaveCandidateSourceGeneration.Primary);
            SaveSemanticCandidate validBackup = Validate(
                CurrentJson(),
                SaveCandidateSourceGeneration.Backup);
            SaveSemanticCandidate validPrevious = Validate(
                CurrentJson(),
                SaveCandidateSourceGeneration.Previous);
            SaveSemanticCandidate invalidPrimary = Validate(
                "{}",
                SaveCandidateSourceGeneration.Primary);

            SaveSemanticCandidateSelection blockedPrimary =
                SaveSemanticCandidateSelector.Select(oversizedPrimary, validBackup);
            Assert.False(blockedPrimary.HasSelection);
            Assert.AreEqual(
                "SAVE_SELECT_OVERSIZE_PRIMARY_RECOVERY_REQUIRED",
                blockedPrimary.ReasonCode);

            Assert.AreSame(
                validPrimary,
                SaveSemanticCandidateSelector
                    .Select(validPrimary, oversizedBackup)
                    .SelectedCandidate);
            Assert.AreSame(
                validPrevious,
                SaveSemanticCandidateSelector
                    .Select(invalidPrimary, oversizedBackup, validPrevious)
                    .SelectedCandidate);

            SaveSemanticCandidateSelection onlyOversize =
                SaveSemanticCandidateSelector.Select(
                    null,
                    oversizedBackup,
                    oversizedPrevious);
            Assert.False(onlyOversize.HasSelection);
            Assert.AreEqual("SAVE_SELECT_NONE", onlyOversize.ReasonCode);
        }

        [TestCase(-1)]
        [TestCase(99)]
        public void UndefinedSourceGenerationIsRejected(int rawSource)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SaveSemanticCandidateValidator.Validate(
                    Bytes(CurrentJson()),
                    (SaveCandidateSourceGeneration)rawSource,
                    Policy()));
        }

        [Test]
        public void SupportedPrimaryWinsIncludingNormalizedAndPreservedUnknown()
        {
            SaveSemanticCandidate validBackup = Validate(
                CurrentJson(),
                SaveCandidateSourceGeneration.Backup);
            SaveSemanticCandidate normalizedPrimary = Validate(
                LegacyJson(),
                SaveCandidateSourceGeneration.Primary);
            SaveSemanticCandidateSelection normalizedSelection =
                SaveSemanticCandidateSelector.Select(normalizedPrimary, validBackup);
            Assert.AreSame(normalizedPrimary, normalizedSelection.SelectedCandidate);

            SaveSemanticCandidate preservedPrimary = Validate(
                CurrentJson(quests:
                    "[{\"QuestId\":\"FUTURE\",\"CurrentValue\":0," +
                    "\"IsCompleted\":false,\"IsClaimed\":false}]"),
                SaveCandidateSourceGeneration.Primary);
            SaveSemanticCandidateSelection preservedSelection =
                SaveSemanticCandidateSelector.Select(preservedPrimary, validBackup);
            Assert.AreSame(preservedPrimary, preservedSelection.SelectedCandidate);
            Assert.AreEqual(
                "SAVE_SELECT_SUPPORTED_PRIMARY",
                preservedSelection.ReasonCode);
        }

        [Test]
        public void CleanerBackupBeatsDegradedOrInvalidPrimary()
        {
            SaveSemanticCandidate validBackup = Validate(
                CurrentJson(),
                SaveCandidateSourceGeneration.Backup);
            SaveSemanticCandidate degradedPrimary = Validate(
                CurrentJson(resources: "[null]"),
                SaveCandidateSourceGeneration.Primary);
            SaveSemanticCandidate invalidPrimary = Validate(
                "{}",
                SaveCandidateSourceGeneration.Primary);

            Assert.AreSame(
                validBackup,
                SaveSemanticCandidateSelector
                    .Select(degradedPrimary, validBackup)
                    .SelectedCandidate);
            Assert.AreSame(
                validBackup,
                SaveSemanticCandidateSelector
                    .Select(invalidPrimary, validBackup)
                    .SelectedCandidate);
        }

        [Test]
        public void InvalidCandidatesNeverProduceASelection()
        {
            SaveSemanticCandidate invalidPrimary = Validate(
                "{}",
                SaveCandidateSourceGeneration.Primary);
            SaveSemanticCandidate invalidBackup = Validate(
                "{}",
                SaveCandidateSourceGeneration.Backup);
            SaveSemanticCandidate invalidPrevious = Validate(
                "{}",
                SaveCandidateSourceGeneration.Previous);

            SaveSemanticCandidateSelection selection =
                SaveSemanticCandidateSelector.Select(
                    invalidPrimary,
                    invalidBackup,
                    invalidPrevious);

            Assert.False(selection.HasSelection);
            Assert.Null(selection.SelectedCandidate);
            Assert.AreEqual(
                SaveCandidateSourceGeneration.Unknown,
                selection.SelectedSource);
            Assert.False(selection.IsWritable);
            Assert.AreEqual("SAVE_SELECT_NONE", selection.ReasonCode);
        }

        [Test]
        public void SameRankPrefersPrimaryAndForwardPrimaryNeverDowngrades()
        {
            SaveSemanticCandidate degradedPrimary = Validate(
                CurrentJson(resources: "[null]"),
                SaveCandidateSourceGeneration.Primary);
            SaveSemanticCandidate degradedBackup = Validate(
                CurrentJson(quests: "[null]"),
                SaveCandidateSourceGeneration.Backup);
            Assert.AreSame(
                degradedPrimary,
                SaveSemanticCandidateSelector
                    .Select(degradedPrimary, degradedBackup)
                    .SelectedCandidate);

            SaveSemanticCandidate forwardPrimary = Validate(
                CurrentJson(schemaVersion: 2),
                SaveCandidateSourceGeneration.Primary);
            SaveSemanticCandidate validBackup = Validate(
                CurrentJson(),
                SaveCandidateSourceGeneration.Backup);
            SaveSemanticCandidateSelection forwardSelection =
                SaveSemanticCandidateSelector.Select(forwardPrimary, validBackup);
            Assert.AreSame(forwardPrimary, forwardSelection.SelectedCandidate);
            Assert.False(forwardSelection.IsWritable);
            Assert.AreEqual(
                "SAVE_SELECT_FORWARD_PRIMARY_READ_ONLY",
                forwardSelection.ReasonCode);
        }

        [Test]
        public void PreviousIsConsideredOnlyAfterBothActiveCandidates()
        {
            SaveSemanticCandidate invalidPrimary = Validate(
                "{}",
                SaveCandidateSourceGeneration.Primary);
            SaveSemanticCandidate invalidBackup = Validate(
                "{}",
                SaveCandidateSourceGeneration.Backup);
            SaveSemanticCandidate validBackup = Validate(
                CurrentJson(),
                SaveCandidateSourceGeneration.Backup);
            SaveSemanticCandidate validPrevious = Validate(
                CurrentJson(),
                SaveCandidateSourceGeneration.Previous);

            Assert.AreSame(
                validPrevious,
                SaveSemanticCandidateSelector
                    .Select(invalidPrimary, invalidBackup, validPrevious)
                    .SelectedCandidate);
            Assert.AreSame(
                validBackup,
                SaveSemanticCandidateSelector
                    .Select(invalidPrimary, validBackup, validPrevious)
                    .SelectedCandidate);
        }

        [Test]
        public void TempCandidateCannotOccupyAnActiveSelectionSlot()
        {
            SaveSemanticCandidate temp = Validate(
                CurrentJson(),
                SaveCandidateSourceGeneration.Temp);

            Assert.Throws<ArgumentException>(
                () => SaveSemanticCandidateSelector.Select(temp, null));
        }

        private static SaveSemanticCandidate Validate(
            string json,
            SaveCandidateSourceGeneration source)
        {
            return Validate(Bytes(json), source);
        }

        private static SaveSemanticCandidate Validate(
            byte[] bytes,
            SaveCandidateSourceGeneration source)
        {
            return SaveSemanticCandidateValidator.Validate(bytes, source, Policy());
        }

        private static SaveSemanticCandidate Oversize(
            SaveCandidateSourceGeneration source)
        {
            var policy = new SaveSemanticValidationPolicy(
                CurrentFormatId,
                1,
                1,
                Authority,
                maximumInputBytes: 8);
            return SaveSemanticCandidateValidator.Validate(
                Enumerable.Repeat((byte)' ', 9).ToArray(),
                source,
                policy);
        }

        private static void AssertFlag(
            SaveSemanticDomain actual,
            SaveSemanticDomain expectedFlag)
        {
            Assert.AreEqual(expectedFlag, actual & expectedFlag);
        }

        private static SaveSemanticValidationPolicy Policy()
        {
            return new SaveSemanticValidationPolicy(
                CurrentFormatId,
                1,
                1,
                Authority,
                nvs01Rule: new SaveSemanticNvs01Rule(
                    1,
                    "v003",
                    Nvs01PacketHash,
                    "OMEN_1"));
        }

        private static byte[] Bytes(string json)
        {
            return Encoding.UTF8.GetBytes(json);
        }

        private static SaveSemanticStableIdRule StableId(
            SaveSemanticStableIdKind kind,
            string stableId)
        {
            return new SaveSemanticStableIdRule(kind, stableId);
        }

        private static string CurrentJson(
            int schemaVersion = 1,
            int initializationVersion = 1,
            string resources = null,
            string buildings = null,
            string troops = null,
            string researches = null,
            string quests = null,
            string reputation = null,
            string factionReputations = null,
            string territories = null,
            string realmGems = null,
            string wishgate = null,
            string warmaster = null,
            string championCustomization = null,
            string ownedEquipment = null,
            string nvs01Progress = null,
            long lastSavedTimestamp = 123,
            string extraTopLevel = "")
        {
            return "{" +
                   "\"SaveFormatId\":\"" + CurrentFormatId + "\"," +
                   "\"SaveSchemaVersion\":" + schemaVersion + "," +
                   "\"ProfileInitializationVersion\":" + initializationVersion + "," +
                   "\"SelectedRealm\":1," +
                   "\"Resources\":" + (resources ?? CurrentResources) + "," +
                   "\"Buildings\":" + (buildings ?? "[]") + "," +
                   "\"Troops\":" + (troops ?? "[]") + "," +
                   "\"Researches\":" + (researches ?? "[]") + "," +
                   "\"Quests\":" + (quests ?? "[]") + "," +
                   "\"Reputation\":" + (reputation ?? "[]") + "," +
                   "\"FactionReputations\":" + (factionReputations ?? "[]") + "," +
                   "\"LordPersona\":{\"Warlord\":0,\"Diplomat\":0,\"Sage\":0,\"Rogue\":0}," +
                   "\"Territories\":" + (territories ?? "[]") + "," +
                   "\"RealmGems\":" + (realmGems ?? "[]") + "," +
                   "\"Wishgate\":" + (wishgate ?? WishgateJson) + "," +
                   "\"CurrentChapterId\":\"C1\"," +
                   "\"Warmaster\":" + (warmaster ?? WarmasterJson) + "," +
                   "\"ChampionCustomization\":" +
                   (championCustomization ?? CurrentChampionCustomizationJson) + "," +
                   "\"OwnedEquipment\":" + (ownedEquipment ?? "[]") + "," +
                   "\"AppliedBossLootRewards\":[]," +
                   "\"Nvs01Progress\":" +
                   (nvs01Progress ?? NeutralNvs01ProgressJson) + "," +
                   "\"WarzoneCredits\":0," +
                   "\"LastSavedTimestamp\":" + lastSavedTimestamp +
                   extraTopLevel +
                   "}";
        }

        private static string LegacyJson(
            string resources = null,
            long lastSavedTimestamp = 123)
        {
            // This is the exact top-level kind footprint present at a9bffb6. The
            // relationship fields and OwnedEquipment were introduced later.
            return "{" +
                   "\"SelectedRealm\":1," +
                   "\"Resources\":" + (resources ?? LegacyResources) + "," +
                   "\"Buildings\":[]," +
                   "\"Troops\":[]," +
                   "\"Researches\":[]," +
                   "\"Quests\":[]," +
                   "\"Territories\":[]," +
                   "\"RealmGems\":[]," +
                   "\"Wishgate\":" + WishgateJson + "," +
                   "\"CurrentChapterId\":\"C1\"," +
                   "\"Warmaster\":" + WarmasterJson + "," +
                   "\"ChampionCustomization\":" +
                   LegacyChampionCustomizationJson + "," +
                   "\"WarzoneCredits\":0," +
                   "\"LastSavedTimestamp\":" + lastSavedTimestamp +
                   "}";
        }

        private static string CurrentJsonWithNestedObject(
            string fieldName,
            string objectJson)
        {
            switch (fieldName)
            {
                case "Wishgate":
                    return CurrentJson(wishgate: objectJson);
                case "Warmaster":
                    return CurrentJson(warmaster: objectJson);
                case "ChampionCustomization":
                    return CurrentJson(championCustomization: objectJson);
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldName));
            }
        }

        private static string CurrentJsonWithRows(
            string fieldName,
            string rowsJson)
        {
            switch (fieldName)
            {
                case "Buildings":
                    return CurrentJson(buildings: rowsJson);
                case "Troops":
                    return CurrentJson(troops: rowsJson);
                case "Researches":
                    return CurrentJson(researches: rowsJson);
                case "Territories":
                    return CurrentJson(territories: rowsJson);
                case "RealmGems":
                    return CurrentJson(realmGems: rowsJson);
                case "OwnedEquipment":
                    return CurrentJson(ownedEquipment: rowsJson);
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldName));
            }
        }

        private static string MalformedRows(string fieldName)
        {
            switch (fieldName)
            {
                case "Buildings":
                    return "[null," +
                           "{\"BuildingId\":\"\",\"Level\":0,\"IsUpgrading\":false," +
                           "\"UpgradeCompleteTimestamp\":0}," +
                           "{\"BuildingId\":\"duplicate\",\"Level\":1," +
                           "\"IsUpgrading\":false,\"UpgradeCompleteTimestamp\":0}," +
                           "{\"BuildingId\":\"duplicate\",\"Level\":1," +
                           "\"IsUpgrading\":false,\"UpgradeCompleteTimestamp\":0}," +
                           "{\"BuildingId\":\"bad_upgrade\",\"Level\":-1," +
                           "\"IsUpgrading\":true,\"UpgradeCompleteTimestamp\":0}]";
                case "Troops":
                    return "[null," +
                           "{\"Type\":1,\"Count\":-1,\"WoundedCount\":0}," +
                           "{\"Type\":0,\"Count\":3,\"WoundedCount\":0}," +
                           "{\"Type\":0,\"Count\":4,\"WoundedCount\":0}," +
                           "{\"Type\":2,\"Count\":1,\"WoundedCount\":2}]";
                case "Researches":
                    return "[null," +
                           "{\"ResearchId\":\"\",\"Level\":0,\"IsResearching\":false," +
                           "\"CompleteTimestamp\":0}," +
                           "{\"ResearchId\":\"duplicate\",\"Level\":1," +
                           "\"IsResearching\":false,\"CompleteTimestamp\":0}," +
                           "{\"ResearchId\":\"duplicate\",\"Level\":1," +
                           "\"IsResearching\":false,\"CompleteTimestamp\":0}," +
                           "{\"ResearchId\":\"bad_research\",\"Level\":-1," +
                           "\"IsResearching\":true,\"CompleteTimestamp\":0}]";
                case "Territories":
                    return "[null," +
                           "{\"Id\":\"\",\"Name\":\"Blank\",\"OwnerRealm\":1," +
                           "\"BonusType\":0,\"BonusAmount\":1,\"IsFortress\":false}," +
                           "{\"Id\":\"duplicate\",\"Name\":\"A\",\"OwnerRealm\":1," +
                           "\"BonusType\":0,\"BonusAmount\":1,\"IsFortress\":false}," +
                           "{\"Id\":\"duplicate\",\"Name\":\"B\",\"OwnerRealm\":1," +
                           "\"BonusType\":0,\"BonusAmount\":1,\"IsFortress\":false}," +
                           "{\"Id\":\"negative_bonus\",\"Name\":\"Bad\",\"OwnerRealm\":1," +
                           "\"BonusType\":0,\"BonusAmount\":-1,\"IsFortress\":false}]";
                case "RealmGems":
                    return "[null," +
                           "{\"GemId\":\"\",\"HomeRealm\":1,\"GemIndex\":0," +
                           "\"IsAtHome\":true,\"IsDropped\":false,\"CarrierId\":\"\"," +
                           "\"LastDroppedTimestamp\":0}," +
                           "{\"GemId\":\"duplicate\",\"HomeRealm\":1,\"GemIndex\":0," +
                           "\"IsAtHome\":true,\"IsDropped\":false,\"CarrierId\":\"\"," +
                           "\"LastDroppedTimestamp\":0}," +
                           "{\"GemId\":\"duplicate\",\"HomeRealm\":1,\"GemIndex\":1," +
                           "\"IsAtHome\":true,\"IsDropped\":false,\"CarrierId\":\"\"," +
                           "\"LastDroppedTimestamp\":0}," +
                           "{\"GemId\":\"bad_custody\",\"HomeRealm\":1,\"GemIndex\":-1," +
                           "\"IsAtHome\":true,\"IsDropped\":true,\"CarrierId\":\"carrier\"," +
                           "\"LastDroppedTimestamp\":-1}]";
                case "OwnedEquipment":
                    return "[null," +
                           EquipmentRow(string.Empty, 0, 1, 1, false) + "," +
                           EquipmentRow("duplicate", 0, 1, 1, false) + "," +
                           EquipmentRow("duplicate", 0, 1, 1, false) + "," +
                           EquipmentRow("bad_timestamps", 0, 0, 20, true) + "]";
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldName));
            }
        }

        private static string RowWithFutureField(string fieldName)
        {
            switch (fieldName)
            {
                case "Buildings":
                    return "[{\"BuildingId\":\"building_a\",\"Level\":1," +
                           "\"IsUpgrading\":false,\"UpgradeCompleteTimestamp\":0," +
                           "\"FutureField\":true}]";
                case "Troops":
                    return "[{\"Type\":0,\"Count\":1,\"WoundedCount\":0," +
                           "\"FutureField\":true}]";
                case "Researches":
                    return "[{\"ResearchId\":\"research_a\",\"Level\":1," +
                           "\"IsResearching\":false,\"CompleteTimestamp\":0," +
                           "\"FutureField\":true}]";
                case "Territories":
                    return "[{\"Id\":\"territory_a\",\"Name\":\"Territory A\"," +
                           "\"OwnerRealm\":1,\"BonusType\":0,\"BonusAmount\":1," +
                           "\"IsFortress\":false,\"FutureField\":true}]";
                case "RealmGems":
                    return "[{\"GemId\":\"gem_a\",\"HomeRealm\":1,\"GemIndex\":0," +
                           "\"IsAtHome\":true,\"IsDropped\":false,\"CarrierId\":\"\"," +
                           "\"LastDroppedTimestamp\":0,\"FutureField\":true}]";
                case "OwnedEquipment":
                    return "[" +
                           EquipmentRow("equipment_a", 0, 1, 1, false, true) +
                           "]";
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldName));
            }
        }

        private static string EquipmentRow(
            string equipmentId,
            int slot,
            int quantity,
            long firstAcquiredTimestamp,
            bool reverseTimestamps,
            bool includeFutureField = false)
        {
            long lastAcquiredTimestamp = reverseTimestamps
                ? firstAcquiredTimestamp - 1
                : firstAcquiredTimestamp;
            return "{\"EquipmentId\":\"" + equipmentId + "\"," +
                   "\"DisplayName\":\"Equipment\",\"Slot\":" + slot + "," +
                   "\"AttackBonus\":0,\"DefenseBonus\":0,\"HealthBonus\":0," +
                   "\"Quantity\":" + quantity + ",\"SourceBossId\":\"\"," +
                   "\"AnnounceWorldDrop\":false," +
                   "\"FirstAcquiredTimestamp\":" + firstAcquiredTimestamp + "," +
                   "\"LastAcquiredTimestamp\":" + lastAcquiredTimestamp +
                   (includeFutureField ? ",\"FutureField\":true" : string.Empty) +
                   "}";
        }
    }
}
