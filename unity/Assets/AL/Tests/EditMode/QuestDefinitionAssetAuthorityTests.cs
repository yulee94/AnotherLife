using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public class QuestDefinitionAssetAuthorityTests
    {
        private const string GeneratedRoot = "Assets/AL/Tests/Generated";
        private const string FixtureFolder = GeneratedRoot + "/QuestDefinitionAuthority";
        private const string FixtureAssetPath = FixtureFolder + "/quest_definition_authority_roundtrip.asset";

        [Test]
        public void ProjectQuestDefinitionAuthorityValidationPasses()
        {
            object snapshot = ValidateProject();

            AssertAuthoritySnapshotSucceeded(snapshot);
            Assert.AreEqual("ForceText", GetProperty(snapshot, "SerializationMode"));
            Assert.AreEqual(ValidatorConstant("AuthoritativeGuid"), GetProperty(snapshot, "ScriptGuid"));
            Assert.AreEqual(ValidatorConstant("AuthoritativeTypeName"), GetProperty(snapshot, "ScriptTypeFullName"));
            Assert.AreEqual(0, GetProperty(snapshot, "RemovedGuidOccurrenceCount"));
            Assert.AreEqual(0, GetProperty(snapshot, "DuplicateIdCount"));
        }

        [Test]
        public void MalformedYamlFixturesProduceExpectedDiagnostics()
        {
            AssertSyntheticCodes(BuildQuestYaml(), Array.Empty<string>(), 1, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(scriptGuid: ValidatorConstant("RemovedRootGuid")), new[] { "AL-QDA-REMOVED-ROOT-GUID" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(includeScript: false), new[] { "AL-QDA-SCRIPT-REFERENCE-MISSING" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(scriptFileId: 0), new[] { "AL-QDA-SCRIPT-FILEID-ZERO" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(scriptFileId: 42), new[] { "AL-QDA-SCRIPT-FILEID-MISMATCH" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(includeScriptType: false), new[] { "AL-QDA-SCRIPT-TYPE-MISSING" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(scriptType: 2), new[] { "AL-QDA-SCRIPT-TYPE-MISMATCH" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(scriptGuid: "00000000000000000000000000000000"), new[] { "AL-QDA-SCRIPT-GUID-ZERO" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(scriptGuid: "not-a-guid"), new[] { "AL-QDA-SCRIPT-GUID-MALFORMED" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(scriptGuid: "11111111111111111111111111111111"), new[] { "AL-QDA-NONAUTHORITATIVE-SCRIPT" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(malformedScript: true), new[] { "AL-QDA-YAML-UNPARSEABLE" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(scriptMappingOverride: "  m_Script: {fileID: 11500000, fileID: 11500000, guid: " + ValidatorConstant("AuthoritativeGuid") + ", type: 3}"), new[] { "AL-QDA-YAML-UNPARSEABLE" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(scriptMappingOverride: "  m_Script: {type: 3, guid: " + ValidatorConstant("AuthoritativeGuid") + ", fileID: 11500000}"), Array.Empty<string>(), 1, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(scriptMappingOverride: "  m_Script: {fileID: 11500000, guid: " + ValidatorConstant("AuthoritativeGuid") + ", type: 3, extra: ignored}"), Array.Empty<string>(), 1, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(classId: 115), new[] { "AL-QDA-YAML-CLASSID-MISMATCH" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(rootObjectName: "ScriptableObject"), new[] { "AL-QDA-YAML-ROOT-MISMATCH" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(omitField: "RewardXP"), new[] { "AL-QDA-REQUIRED-SERIALIZED-FIELD-MISSING" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(extraField: "LegacyRewardId: old_reward"), new[] { "AL-QDA-UNEXPECTED-SERIALIZED-FIELD" }, 1, 0);

            object partial = ValidateYamlTextForTests(
                "Assets/AL/Tests/Fixtures/partial.asset",
                "--- !u!114 &11400000\nMonoBehaviour:\n  Id: PARTIAL\n  Title: Partial\n");

            Assert.AreEqual(0, GetProperty(partial, "CandidateCount"));
            CollectionAssert.IsEmpty(DiagnosticCodes(partial));

            string multiDocumentYaml =
                "--- !u!114 &100\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  Id: PARTIAL\n" +
                BuildQuestYaml(localFileId: 200);
            object subasset = ValidateYamlTextForTests("Assets/AL/Tests/Fixtures/subasset.asset", multiDocumentYaml);

            AssertAuthoritySnapshotSucceeded(subasset);
            Assert.AreEqual(1, GetProperty(subasset, "CandidateCount"));
            Assert.AreEqual(200L, GetProperty(Candidates(subasset).Single(), "LocalFileId"));

            string lf = BuildQuestYaml(localFileId: 300);
            string crlf = lf.Replace("\n", "\r\n");
            CollectionAssert.AreEqual(
                DiagnosticCodes(ValidateYamlTextForTests("Assets/AL/Tests/Fixtures/lf.asset", lf)),
                DiagnosticCodes(ValidateYamlTextForTests("Assets/AL/Tests/Fixtures/crlf.asset", crlf)));
        }

        [Test]
        public void BlankAndDuplicateIdsAreReportedFromYaml()
        {
            AssertSyntheticCodes(BuildQuestYaml(id: string.Empty), new[] { "AL-QDA-BLANK-ASSET-ID" }, 1, 0);
            AssertSyntheticCodes(BuildQuestYaml(id: "   "), new[] { "AL-QDA-BLANK-ASSET-ID" }, 1, 0);

            string duplicateYaml = BuildQuestYaml(localFileId: 400, id: "DUPLICATE_ID") +
                                   BuildQuestYaml(localFileId: 401, id: "DUPLICATE_ID");
            AssertSyntheticCodes(duplicateYaml, new[] { "AL-QDA-DUPLICATE-ASSET-ID" }, 2, 1, 1);

            string caseDifferentYaml = BuildQuestYaml(localFileId: 410, id: "CaseSensitiveId") +
                                       BuildQuestYaml(localFileId: 411, id: "casesensitiveid");
            object caseDifferent = ValidateYamlTextForTests("Assets/AL/Tests/Fixtures/case.asset", caseDifferentYaml);

            AssertAuthoritySnapshotSucceeded(caseDifferent);
            Assert.AreEqual(2, GetProperty(caseDifferent, "ValidAssetCount"), "Quest IDs use ordinal exact identity; case-different IDs are distinct.");
        }

        [Test]
        public void SerializedFieldSchemaRequiresExactTypes()
        {
            string[] names = InvokeStatic(ValidatorType(), "ExpectedFieldNamesForTests") as string[];
            string[] validTypes = InvokeStatic(ValidatorType(), "ExpectedFieldTypeNamesForTests") as string[];
            Assert.NotNull(names);
            Assert.NotNull(validTypes);

            object valid = ValidateFieldSchemaForTests(names, validTypes);
            AssertAuthoritySnapshotSucceeded(valid);

            string[] wrongTypes = (string[])validTypes.Clone();
            wrongTypes[4] = "System.Int64";
            object wrong = ValidateFieldSchemaForTests(names, wrongTypes);
            CollectionAssert.AreEquivalent(new[] { "AL-QDA-SERIALIZED-FIELD-CONTRACT" }, DiagnosticCodes(wrong));
        }

        [Test]
        public void ValidQuestAssetRoundTripsAndMapsByLocalFileId()
        {
            AssetDatabase.DeleteAsset(FixtureFolder);
            bool createdGeneratedRoot = false;
            if (!AssetDatabase.IsValidFolder(GeneratedRoot))
            {
                AssetDatabase.CreateFolder("Assets/AL/Tests", "Generated");
                createdGeneratedRoot = true;
            }

            AssetDatabase.CreateFolder(GeneratedRoot, "QuestDefinitionAuthority");

            try
            {
                Type questType = RuntimeType("AL.Data.Definitions.Narrative.QuestDefinition");
                var asset = (ScriptableObject)ScriptableObject.CreateInstance(questType);
                PopulateQuest(asset, "QDA_ROUNDTRIP_FIXTURE");
                QuestSnapshot before = QuestSnapshot.From(asset);

                AssetDatabase.CreateAsset(asset, FixtureAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(FixtureAssetPath, ImportAssetOptions.ForceUpdate);

                AssertProjectValidationContainsFixture(out long localFileId);
                Assert.Greater(localFileId, 0);

                AssetDatabase.ImportAsset(FixtureAssetPath, ImportAssetOptions.ForceUpdate);
                UnityEngine.Object reloaded = AssetDatabase.LoadAssetAtPath(FixtureAssetPath, questType);
                Assert.NotNull(reloaded);
                Assert.AreEqual(before, QuestSnapshot.From(reloaded));
            }
            finally
            {
                AssetDatabase.DeleteAsset(FixtureFolder);
                if (createdGeneratedRoot)
                {
                    AssetDatabase.DeleteAsset(GeneratedRoot);
                }

                AssetDatabase.Refresh();
            }
        }

        private static void AssertProjectValidationContainsFixture(out long localFileId)
        {
            object snapshot = ValidateProject();
            AssertAuthoritySnapshotSucceeded(snapshot);

            object candidate = Candidates(snapshot)
                .Single(document => string.Equals((string)GetProperty(document, "Path"), FixtureAssetPath, StringComparison.Ordinal));

            object localFileIdValue = GetProperty(candidate, "LocalFileId");
            Assert.NotNull(localFileIdValue);
            localFileId = (long)localFileIdValue;
            Assert.AreEqual("QDA_ROUNDTRIP_FIXTURE", Invoke(candidate, "GetScalar", "Id"));
        }

        private static void PopulateQuest(UnityEngine.Object quest, string id)
        {
            SetField(quest, "Id", id);
            SetField(quest, "Title", "Authority Round Trip");
            SetField(quest, "Description", "Validator fixture");
            SetField(quest, "Type", EnumValue("AL.Core.QuestType", "Main"));
            SetField(quest, "TargetValue", 3);
            SetField(quest, "IsHidden", true);
            SetField(quest, "RequiredItemId", "authority_key");
            SetField(quest, "Trigger", EnumValue("AL.Core.TriggerCondition", "Item"));
            SetField(quest, "ConflictHint", "authority_conflict");
            SetField(quest, "RewardResources", CreateRewardResources());
            SetField(quest, "RewardCredits", 11);
            SetField(quest, "RewardXP", 13);
        }

        private static object CreateRewardResources()
        {
            Type resourceDataType = RuntimeType("AL.Data.Runtime.ResourceData");
            Type resourceType = RuntimeType("AL.Core.ResourceType");
            Type listType = typeof(List<>).MakeGenericType(resourceDataType);
            IList rewards = (IList)Activator.CreateInstance(listType);
            object reward = Activator.CreateInstance(resourceDataType);
            SetField(reward, "Type", Enum.Parse(resourceType, "Food"));
            SetField(reward, "Amount", 7L);
            rewards.Add(reward);
            return rewards;
        }

        private static void AssertSyntheticCodes(string yaml, IReadOnlyCollection<string> expectedCodes, int expectedCandidates, int expectedValidAssets, int? expectedMalformedCandidates = null)
        {
            object snapshot = ValidateYamlTextForTests("Assets/AL/Tests/Fixtures/synthetic.asset", yaml);

            CollectionAssert.AreEquivalent(expectedCodes, DiagnosticCodes(snapshot));
            Assert.AreEqual(expectedCandidates, GetProperty(snapshot, "CandidateCount"));
            Assert.AreEqual(expectedValidAssets, GetProperty(snapshot, "ValidAssetCount"));
            if (expectedMalformedCandidates.HasValue)
            {
                Assert.AreEqual(expectedMalformedCandidates.Value, GetProperty(snapshot, "MalformedCandidateCount"));
            }
        }

        private static string[] DiagnosticCodes(object snapshot) =>
            Diagnostics(snapshot)
                .Select(diagnostic => (string)GetProperty(diagnostic, "Code"))
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();

        private static void AssertAuthoritySnapshotSucceeded(object snapshot)
        {
            string diagnostics = string.Join(
                "\n",
                Diagnostics(snapshot).Select(diagnostic =>
                    $"{GetProperty(diagnostic, "Code")} {GetProperty(diagnostic, "Path")} {GetProperty(diagnostic, "LocalFileId")}: {GetProperty(diagnostic, "Message")} expected={GetProperty(diagnostic, "Expected")} actual={GetProperty(diagnostic, "Actual")}"));

            Assert.True((bool)GetProperty(snapshot, "Succeeded"), diagnostics);
        }

        private static string BuildQuestYaml(
            long localFileId = 11400000,
            int classId = 114,
            string rootObjectName = "MonoBehaviour",
            string id = "QDA_SYNTHETIC_FIXTURE",
            bool includeScript = true,
            bool malformedScript = false,
            long scriptFileId = 11500000,
            int scriptType = 3,
            bool includeScriptType = true,
            string scriptGuid = null,
            string omitField = null,
            string extraField = null,
            string scriptMappingOverride = null)
        {
            scriptGuid ??= ValidatorConstant("AuthoritativeGuid");
            var fields = new List<string>
            {
                "--- !u!" + classId + " &" + localFileId,
                rootObjectName + ":",
                "  m_ObjectHideFlags: 0",
                "  m_CorrespondingSourceObject: {fileID: 0}",
                "  m_PrefabInstance: {fileID: 0}",
                "  m_PrefabAsset: {fileID: 0}",
                "  m_GameObject: {fileID: 0}",
                "  m_Enabled: 1",
                "  m_EditorHideFlags: 0"
            };

            if (includeScript)
            {
                if (!string.IsNullOrEmpty(scriptMappingOverride))
                {
                    fields.Add(scriptMappingOverride);
                }
                else if (malformedScript)
                {
                    fields.Add("  m_Script: not-a-map");
                }
                else
                {
                    string typePart = includeScriptType ? $", type: {scriptType}" : string.Empty;
                    fields.Add($"  m_Script: {{fileID: {scriptFileId}, guid: {scriptGuid}{typePart}}}");
                }
            }

            fields.Add("  m_Name: QdaSyntheticQuest");
            fields.Add("  m_EditorClassIdentifier:");
            AddQuestField(fields, "Id", id, omitField);
            AddQuestField(fields, "Title", "Synthetic Quest", omitField);
            AddQuestField(fields, "Description", "Synthetic description", omitField);
            AddQuestField(fields, "Type", "0", omitField);
            AddQuestField(fields, "TargetValue", "1", omitField);
            AddQuestField(fields, "IsHidden", "0", omitField);
            AddQuestField(fields, "RequiredItemId", string.Empty, omitField);
            AddQuestField(fields, "Trigger", "0", omitField);
            AddQuestField(fields, "ConflictHint", string.Empty, omitField);
            AddQuestField(fields, "RewardResources", "[]", omitField);
            AddQuestField(fields, "RewardCredits", "1", omitField);
            AddQuestField(fields, "RewardXP", "2", omitField);

            if (!string.IsNullOrEmpty(extraField))
            {
                fields.Add("  " + extraField);
            }

            return string.Join("\n", fields) + "\n";
        }

        private static void AddQuestField(List<string> fields, string fieldName, string value, string omitField)
        {
            if (string.Equals(fieldName, omitField, StringComparison.Ordinal))
            {
                return;
            }

            fields.Add(string.IsNullOrEmpty(value) ? $"  {fieldName}:" : $"  {fieldName}: {value}");
        }

        private static object ValidateProject() =>
            InvokeStatic(ValidatorType(), "ValidateProject");

        private static object ValidateYamlTextForTests(string path, string yaml) =>
            InvokeStatic(ValidatorType(), "ValidateYamlTextForTests", path, yaml);

        private static object ValidateFieldSchemaForTests(string[] fieldNames, string[] fieldTypeFullNames) =>
            InvokeStatic(ValidatorType(), "ValidateFieldContractArraysForTests", fieldNames, fieldTypeFullNames);

        private static string ValidatorConstant(string name) =>
            (string)ValidatorType().GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);

        private static Type ValidatorType() =>
            RuntimeType("AL.Editor.Validation.QuestDefinitionAssetAuthorityValidator", "Assembly-CSharp-Editor");

        private static Type RuntimeType(string fullName, string assemblyName = "Assembly-CSharp")
        {
            Type type = Type.GetType($"{fullName}, {assemblyName}");
            Assert.NotNull(type, $"Expected type {fullName} in {assemblyName}.");
            return type;
        }

        private static object EnumValue(string fullName, string value) =>
            Enum.Parse(RuntimeType(fullName), value);

        private static IEnumerable<object> Diagnostics(object snapshot) =>
            ((IEnumerable)GetProperty(snapshot, "Diagnostics")).Cast<object>();

        private static IEnumerable<object> Candidates(object snapshot) =>
            ((IEnumerable)GetProperty(snapshot, "Candidates")).Cast<object>();

        private static object GetProperty(object target, string propertyName) =>
            target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public).GetValue(target);

        private static object GetField(object target, string fieldName) =>
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);

        private static void SetField(object target, string fieldName, object value) =>
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);

        private static object Invoke(object target, string methodName, params object[] args) =>
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);

        private static object InvokeStatic(Type type, string methodName, params object[] args) =>
            type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);

        private sealed class QuestSnapshot
        {
            public string Id { get; private set; }
            public string Title { get; private set; }
            public string Description { get; private set; }
            public object Type { get; private set; }
            public int TargetValue { get; private set; }
            public bool IsHidden { get; private set; }
            public string RequiredItemId { get; private set; }
            public object Trigger { get; private set; }
            public string ConflictHint { get; private set; }
            public object RewardResourceType { get; private set; }
            public long RewardResourceAmount { get; private set; }
            public int RewardCredits { get; private set; }
            public int RewardXP { get; private set; }

            public static QuestSnapshot From(object quest)
            {
                object reward = ((IEnumerable)GetField(quest, "RewardResources")).Cast<object>().Single();
                return new QuestSnapshot
                {
                    Id = (string)GetField(quest, "Id"),
                    Title = (string)GetField(quest, "Title"),
                    Description = (string)GetField(quest, "Description"),
                    Type = GetField(quest, "Type"),
                    TargetValue = (int)GetField(quest, "TargetValue"),
                    IsHidden = (bool)GetField(quest, "IsHidden"),
                    RequiredItemId = (string)GetField(quest, "RequiredItemId"),
                    Trigger = GetField(quest, "Trigger"),
                    ConflictHint = (string)GetField(quest, "ConflictHint"),
                    RewardResourceType = GetField(reward, "Type"),
                    RewardResourceAmount = (long)GetField(reward, "Amount"),
                    RewardCredits = (int)GetField(quest, "RewardCredits"),
                    RewardXP = (int)GetField(quest, "RewardXP")
                };
            }

            public override bool Equals(object obj)
            {
                if (obj is not QuestSnapshot other)
                {
                    return false;
                }

                return Id == other.Id &&
                       Title == other.Title &&
                       Description == other.Description &&
                       Equals(Type, other.Type) &&
                       TargetValue == other.TargetValue &&
                       IsHidden == other.IsHidden &&
                       RequiredItemId == other.RequiredItemId &&
                       Equals(Trigger, other.Trigger) &&
                       ConflictHint == other.ConflictHint &&
                       Equals(RewardResourceType, other.RewardResourceType) &&
                       RewardResourceAmount == other.RewardResourceAmount &&
                       RewardCredits == other.RewardCredits &&
                       RewardXP == other.RewardXP;
            }

            public override int GetHashCode()
            {
                return Id != null ? Id.GetHashCode() : 0;
            }
        }
    }
}
