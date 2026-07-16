using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AL.Data.Definitions.Narrative;
using UnityEditor;
using UnityEngine;

namespace AL.Editor.Validation
{
    public static class QuestDefinitionAssetAuthorityValidator
    {
        public const string AuthoritativePath = "Assets/AL/Scripts/Data/Definitions/Narrative/QuestDefinition.cs";
        public const string AuthoritativeTypeName = "AL.Data.Definitions.Narrative.QuestDefinition";
        public const string HistoricalTypeName = "AL.Data.Definitions.QuestDefinition";
        public const string AuthoritativeGuid = "c385b2b183b74184ca75eeffbe2256ef";
        public const string RemovedRootGuid = "226022aa7500f3e4abc8ac3757707ad8";

        private const string ZeroGuid = "00000000000000000000000000000000";

        private static readonly string[] ExpectedQuestFields =
        {
            "Id",
            "Title",
            "Description",
            "Type",
            "TargetValue",
            "IsHidden",
            "RequiredItemId",
            "Trigger",
            "ConflictHint",
            "RewardResources",
            "RewardCredits",
            "RewardXP"
        };

        private static readonly HashSet<string> ExpectedFieldSet = new HashSet<string>(ExpectedQuestFields, StringComparer.Ordinal);

        private static readonly HashSet<string> IgnoredTopLevelFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "m_ObjectHideFlags",
            "m_CorrespondingSourceObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
            "m_GameObject",
            "m_Enabled",
            "m_EditorHideFlags",
            "m_Script",
            "m_Name",
            "m_EditorClassIdentifier"
        };

        private static readonly string[] SerializedFileExtensions =
        {
            ".anim",
            ".asset",
            ".controller",
            ".mat",
            ".meta",
            ".overridecontroller",
            ".playable",
            ".prefab",
            ".unity"
        };

        public static QuestDefinitionAuthoritySnapshot ValidateProject()
        {
            var snapshot = new QuestDefinitionAuthoritySnapshot
            {
                SerializationMode = EditorSettings.serializationMode.ToString(),
                ScriptPath = AuthoritativePath,
                ScriptGuid = AssetDatabase.AssetPathToGUID(AuthoritativePath),
                ScriptTypeFullName = ResolveAuthoritativeTypeName()
            };

            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                snapshot.AddDiagnostic("AL-QDA-SERIALIZATION-MODE", string.Empty, null, "Unity serialization mode must be Force Text.", "ForceText", EditorSettings.serializationMode.ToString());
            }

            ValidateAuthoritativeScript(snapshot);
            ValidateProductionTypes(snapshot);
            ValidateAuthoritativeFieldSchema(snapshot);
            ValidateSerializedGuidOccurrences(snapshot);
            ValidateAssetYamlCandidates(snapshot);

            snapshot.SortAndSeal();
            return snapshot;
        }

        public static QuestDefinitionAuthoritySnapshot ValidateYamlTextForTests(string projectRelativePath, string yamlText)
        {
            var snapshot = new QuestDefinitionAuthoritySnapshot
            {
                SerializationMode = SerializationMode.ForceText.ToString(),
                ScriptPath = AuthoritativePath,
                ScriptGuid = AuthoritativeGuid,
                ScriptTypeFullName = AuthoritativeTypeName
            };

            var ids = new Dictionary<string, QuestYamlDocument>(StringComparer.Ordinal);
            foreach (QuestYamlDocument document in ParseYamlDocuments(projectRelativePath, yamlText, snapshot))
            {
                ValidateCandidateDocument(snapshot, document, ids, false);
            }

            snapshot.SortAndSeal();
            return snapshot;
        }

        private static string ResolveAuthoritativeTypeName()
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AuthoritativePath);
            return script != null && script.GetClass() != null ? script.GetClass().FullName : string.Empty;
        }

        private static void ValidateAuthoritativeScript(QuestDefinitionAuthoritySnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(snapshot.ScriptGuid))
            {
                snapshot.AddDiagnostic("AL-QDA-AUTHORITATIVE-SCRIPT-MISSING", AuthoritativePath, null, "Authoritative QuestDefinition script is missing.", AuthoritativeGuid, string.Empty);
                return;
            }

            if (!string.Equals(snapshot.ScriptGuid, AuthoritativeGuid, StringComparison.OrdinalIgnoreCase))
            {
                snapshot.AddDiagnostic("AL-QDA-AUTHORITATIVE-GUID-CHANGED", AuthoritativePath, null, "Authoritative QuestDefinition script GUID changed.", AuthoritativeGuid, snapshot.ScriptGuid);
            }

            if (!string.Equals(snapshot.ScriptTypeFullName, AuthoritativeTypeName, StringComparison.Ordinal))
            {
                snapshot.AddDiagnostic("AL-QDA-AUTHORITATIVE-TYPE-MISMATCH", AuthoritativePath, null, "Authoritative QuestDefinition script type changed.", AuthoritativeTypeName, snapshot.ScriptTypeFullName);
            }

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AuthoritativePath);
            Type definitionType = script != null ? script.GetClass() : null;
            if (definitionType == null)
            {
                return;
            }

            var menu = definitionType.GetCustomAttribute<CreateAssetMenuAttribute>();
            string actualMenu = menu != null ? menu.menuName : string.Empty;
            string actualFileName = menu != null ? menu.fileName : string.Empty;
            if (!string.Equals(actualFileName, "New Quest", StringComparison.Ordinal))
            {
                snapshot.AddDiagnostic("AL-QDA-MENU-PATH-MISMATCH", AuthoritativePath, null, "Authoritative QuestDefinition CreateAssetMenu file name changed.", "New Quest", actualFileName);
            }

            if (!string.Equals(actualMenu, "AL/Narrative/Quest", StringComparison.Ordinal))
            {
                snapshot.AddDiagnostic("AL-QDA-MENU-PATH-MISMATCH", AuthoritativePath, null, "Authoritative QuestDefinition CreateAssetMenu path changed.", "AL/Narrative/Quest", actualMenu);
            }
        }

        private static void ValidateProductionTypes(QuestDefinitionAuthoritySnapshot snapshot)
        {
            string[] questDefinitionTypes = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(type => type.Name == "QuestDefinition")
                .Select(type => type.FullName)
                .OrderBy(typeName => typeName, StringComparer.Ordinal)
                .ToArray();

            if (questDefinitionTypes.Length != 1 ||
                !string.Equals(questDefinitionTypes[0], AuthoritativeTypeName, StringComparison.Ordinal))
            {
                snapshot.AddDiagnostic(
                    "AL-QDA-DUPLICATE-PRODUCTION-TYPE",
                    string.Empty,
                    null,
                    "Exactly one production ScriptableObject type named QuestDefinition must exist.",
                    AuthoritativeTypeName,
                    string.Join(", ", questDefinitionTypes));
            }
        }

        private static void ValidateAuthoritativeFieldSchema(QuestDefinitionAuthoritySnapshot snapshot)
        {
            Type definitionType = typeof(QuestDefinition);
            string[] actualFields = definitionType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(IsUnitySerializedField)
                .OrderBy(field => field.MetadataToken)
                .Select(field => field.Name)
                .ToArray();

            if (!ExpectedQuestFields.SequenceEqual(actualFields))
            {
                snapshot.AddDiagnostic(
                    "AL-QDA-SERIALIZED-FIELD-CONTRACT",
                    AuthoritativePath,
                    null,
                    "Authoritative QuestDefinition serialized field contract changed.",
                    string.Join(", ", ExpectedQuestFields),
                    string.Join(", ", actualFields));
            }

            FieldInfo idField = definitionType.GetField("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (idField == null || idField.FieldType != typeof(string) || !IsUnitySerializedField(idField))
            {
                snapshot.AddDiagnostic(
                    "AL-QDA-ID-FIELD-CONTRACT",
                    AuthoritativePath,
                    null,
                    "QuestDefinition.Id must remain an explicitly serialized string field.",
                    "serialized System.String Id",
                    idField == null ? "missing" : idField.FieldType.FullName);
            }
        }

        private static bool IsUnitySerializedField(FieldInfo field)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral)
            {
                return false;
            }

            if (field.GetCustomAttribute<NonSerializedAttribute>() != null)
            {
                return false;
            }

            return field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
        }

        private static void ValidateSerializedGuidOccurrences(QuestDefinitionAuthoritySnapshot snapshot)
        {
            foreach (string path in EnumerateSerializedUnityTextFiles())
            {
                string text;
                try
                {
                    text = File.ReadAllText(ToAbsoluteProjectPath(path));
                }
                catch (Exception ex)
                {
                    snapshot.AddDiagnostic("AL-QDA-YAML-UNREADABLE", path, null, "Serialized Unity text file could not be read.", string.Empty, ex.Message);
                    continue;
                }

                if (text.IndexOf(RemovedRootGuid, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    snapshot.RemovedGuidOccurrenceCount++;
                    snapshot.AddDiagnostic("AL-QDA-REMOVED-ROOT-GUID", path, null, "Serialized Unity file references the removed root QuestDefinition GUID.", AuthoritativeGuid, RemovedRootGuid);
                }
            }
        }

        private static void ValidateAssetYamlCandidates(QuestDefinitionAuthoritySnapshot snapshot)
        {
            var ids = new Dictionary<string, QuestYamlDocument>(StringComparer.Ordinal);

            foreach (string path in EnumerateSerializedUnityTextFiles().Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)))
            {
                string text;
                try
                {
                    text = File.ReadAllText(ToAbsoluteProjectPath(path));
                }
                catch (Exception ex)
                {
                    snapshot.AddDiagnostic("AL-QDA-YAML-UNREADABLE", path, null, "Asset YAML could not be read.", string.Empty, ex.Message);
                    continue;
                }

                UnityEngine.Object[] loadedAssets = null;
                foreach (QuestYamlDocument document in ParseYamlDocuments(path, text, snapshot))
                {
                    if (!document.IsQuestCandidate)
                    {
                        continue;
                    }

                    loadedAssets ??= AssetDatabase.LoadAllAssetsAtPath(ToAssetDatabasePath(path));
                    ValidateCandidateDocument(snapshot, document, ids, true, loadedAssets);
                }
            }
        }

        private static void ValidateCandidateDocument(
            QuestDefinitionAuthoritySnapshot snapshot,
            QuestYamlDocument document,
            Dictionary<string, QuestYamlDocument> ids,
            bool requireLoadedObject,
            UnityEngine.Object[] loadedAssets = null)
        {
            if (!document.IsQuestCandidate)
            {
                return;
            }

            snapshot.CandidateCount++;
            snapshot.AddCandidate(document);

            ValidateScriptReference(snapshot, document);
            ValidateFieldContract(snapshot, document);

            if (!string.Equals(document.ScriptGuid, AuthoritativeGuid, StringComparison.OrdinalIgnoreCase))
            {
                snapshot.MalformedCandidateCount++;
                return;
            }

            if (requireLoadedObject)
            {
                ValidateLoadedAuthoritativeAsset(snapshot, document, loadedAssets, out QuestDefinition loadedQuest);
                if (loadedQuest == null)
                {
                    snapshot.MalformedCandidateCount++;
                    return;
                }

                string id = loadedQuest.Id;
                ValidateQuestId(snapshot, document, id, ids);
                snapshot.ValidAssetCount++;
                return;
            }

            ValidateQuestId(snapshot, document, document.GetScalar("Id"), ids);
            snapshot.ValidAssetCount++;
        }

        private static void ValidateScriptReference(QuestDefinitionAuthoritySnapshot snapshot, QuestYamlDocument document)
        {
            if (!document.HasScriptField)
            {
                snapshot.AddDiagnostic("AL-QDA-SCRIPT-REFERENCE-MISSING", document.Path, document.LocalFileId, "Quest-shaped YAML document has no m_Script field.", AuthoritativeGuid, string.Empty);
                return;
            }

            if (!document.ScriptMappingParsed)
            {
                snapshot.AddDiagnostic("AL-QDA-YAML-UNPARSEABLE", document.Path, document.LocalFileId, "Quest-shaped YAML document has a malformed m_Script mapping.", "m_Script: {fileID: 11500000, guid: <guid>, type: 3}", document.ScriptLine);
                return;
            }

            if (document.ScriptFileId == 0)
            {
                snapshot.AddDiagnostic("AL-QDA-SCRIPT-FILEID-ZERO", document.Path, document.LocalFileId, "Quest-shaped YAML document has m_Script fileID 0.", "11500000", "0");
            }

            if (string.IsNullOrWhiteSpace(document.ScriptGuid))
            {
                snapshot.AddDiagnostic("AL-QDA-SCRIPT-REFERENCE-MISSING", document.Path, document.LocalFileId, "Quest-shaped YAML document has no m_Script GUID.", AuthoritativeGuid, string.Empty);
                return;
            }

            if (string.Equals(document.ScriptGuid, ZeroGuid, StringComparison.OrdinalIgnoreCase))
            {
                snapshot.AddDiagnostic("AL-QDA-SCRIPT-GUID-ZERO", document.Path, document.LocalFileId, "Quest-shaped YAML document has an all-zero m_Script GUID.", AuthoritativeGuid, document.ScriptGuid);
                return;
            }

            if (string.Equals(document.ScriptGuid, RemovedRootGuid, StringComparison.OrdinalIgnoreCase))
            {
                snapshot.AddDiagnostic("AL-QDA-REMOVED-ROOT-GUID", document.Path, document.LocalFileId, "Quest-shaped YAML document references the removed root QuestDefinition GUID.", AuthoritativeGuid, document.ScriptGuid);
                return;
            }

            if (!string.Equals(document.ScriptGuid, AuthoritativeGuid, StringComparison.OrdinalIgnoreCase))
            {
                snapshot.AddDiagnostic("AL-QDA-NONAUTHORITATIVE-SCRIPT", document.Path, document.LocalFileId, "Quest-shaped YAML document references a non-authoritative script GUID.", AuthoritativeGuid, document.ScriptGuid);
            }
        }

        private static void ValidateFieldContract(QuestDefinitionAuthoritySnapshot snapshot, QuestYamlDocument document)
        {
            string[] missing = ExpectedQuestFields
                .Where(field => !document.TopLevelFields.Contains(field))
                .ToArray();

            if (missing.Length > 0)
            {
                snapshot.AddDiagnostic("AL-QDA-REQUIRED-SERIALIZED-FIELD-MISSING", document.Path, document.LocalFileId, "QuestDefinition YAML is missing required serialized fields.", string.Join(", ", ExpectedQuestFields), string.Join(", ", missing));
            }

            string[] unexpected = document.TopLevelFields
                .Where(field => !ExpectedFieldSet.Contains(field) && !IgnoredTopLevelFields.Contains(field))
                .OrderBy(field => field, StringComparer.Ordinal)
                .ToArray();

            if (unexpected.Length > 0)
            {
                snapshot.AddDiagnostic("AL-QDA-UNEXPECTED-SERIALIZED-FIELD", document.Path, document.LocalFileId, "QuestDefinition YAML contains unapproved top-level serialized fields.", string.Empty, string.Join(", ", unexpected));
            }
        }

        private static void ValidateLoadedAuthoritativeAsset(
            QuestDefinitionAuthoritySnapshot snapshot,
            QuestYamlDocument document,
            UnityEngine.Object[] loadedAssets,
            out QuestDefinition loadedQuest)
        {
            loadedQuest = null;
            UnityEngine.Object matched = loadedAssets?.FirstOrDefault(asset =>
                document.LocalFileId.HasValue &&
                TryGetLocalFileId(asset, out long localId) &&
                localId == document.LocalFileId.Value);
            if (matched == null)
            {
                snapshot.AddDiagnostic("AL-QDA-LOCAL-FILEID-UNRESOLVED", document.Path, document.LocalFileId, "Authoritative QuestDefinition YAML document did not map to a loaded object by local file ID.", document.LocalFileId?.ToString(), string.Empty);
                return;
            }

            loadedQuest = matched as QuestDefinition;
            if (loadedQuest == null)
            {
                snapshot.AddDiagnostic("AL-QDA-ASSET-TYPE-MISMATCH", document.Path, document.LocalFileId, "Loaded object for authoritative QuestDefinition YAML is not the authoritative type.", AuthoritativeTypeName, matched.GetType().FullName);
                return;
            }

            MonoScript script = MonoScript.FromScriptableObject(loadedQuest);
            string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
            string scriptGuid = string.IsNullOrWhiteSpace(scriptPath) ? string.Empty : AssetDatabase.AssetPathToGUID(scriptPath);
            if (!string.Equals(scriptPath, AuthoritativePath, StringComparison.Ordinal) ||
                !string.Equals(scriptGuid, AuthoritativeGuid, StringComparison.OrdinalIgnoreCase))
            {
                snapshot.AddDiagnostic("AL-QDA-NONAUTHORITATIVE-SCRIPT", document.Path, document.LocalFileId, "Loaded QuestDefinition object resolves to the wrong script.", AuthoritativeGuid, scriptGuid);
            }
        }

        private static bool TryGetLocalFileId(UnityEngine.Object asset, out long localFileId)
        {
            localFileId = 0;
            return asset != null &&
                   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out localFileId);
        }

        private static void ValidateQuestId(
            QuestDefinitionAuthoritySnapshot snapshot,
            QuestYamlDocument document,
            string id,
            Dictionary<string, QuestYamlDocument> ids)
        {
            string trimmed = id?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                snapshot.AddDiagnostic("AL-QDA-BLANK-ASSET-ID", document.Path, document.LocalFileId, "QuestDefinition candidate has a blank Id.", "nonblank Id", id ?? string.Empty);
                return;
            }

            if (ids == null)
            {
                return;
            }

            if (ids.TryGetValue(trimmed, out QuestYamlDocument existing))
            {
                snapshot.DuplicateIdCount++;
                snapshot.AddDiagnostic("AL-QDA-DUPLICATE-ASSET-ID", document.Path, document.LocalFileId, $"Duplicate QuestDefinition Id '{trimmed}'.", existing.Path, document.Path);
                return;
            }

            ids.Add(trimmed, document);
        }

        private static IEnumerable<QuestYamlDocument> ParseYamlDocuments(string projectRelativePath, string yamlText, QuestDefinitionAuthoritySnapshot snapshot)
        {
            string normalized = (yamlText ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            var matches = Regex.Matches(normalized, @"(?m)^--- !u!(?<classId>-?\d+) &(?<localId>-?\d+)\s*$");
            if (matches.Count == 0)
            {
                if (projectRelativePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
                    normalized.Contains("m_Script:", StringComparison.Ordinal))
                {
                    snapshot.AddDiagnostic("AL-QDA-YAML-UNPARSEABLE", projectRelativePath, null, "YAML asset contains m_Script but no Unity document header.", string.Empty, string.Empty);
                }

                yield break;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                Match header = matches[i];
                int start = header.Index;
                int end = i + 1 < matches.Count ? matches[i + 1].Index : normalized.Length;
                string documentText = normalized.Substring(start, end - start);
                if (!long.TryParse(header.Groups["localId"].Value, out long localFileId))
                {
                    snapshot.AddDiagnostic("AL-QDA-YAML-UNPARSEABLE", projectRelativePath, null, "Unity YAML document has an invalid local file ID.", string.Empty, header.Value);
                    continue;
                }

                yield return new QuestYamlDocument(
                    projectRelativePath,
                    int.TryParse(header.Groups["classId"].Value, out int classId) ? classId : 0,
                    localFileId,
                    documentText);
            }
        }

        private static IEnumerable<string> EnumerateSerializedUnityTextFiles()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (string path in Directory.EnumerateFiles(Application.dataPath, "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(path);
                if (!SerializedFileExtensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                yield return ToProjectRelativePath(projectRoot, path);
            }
        }

        private static string ToAbsoluteProjectPath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ToAssetDatabasePath(string projectRelativePath) =>
            projectRelativePath.StartsWith("unity/", StringComparison.Ordinal)
                ? projectRelativePath.Substring("unity/".Length)
                : projectRelativePath;

        private static string ToProjectRelativePath(string projectRoot, string path)
        {
            string relative = path.Substring(projectRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        public sealed class QuestDefinitionAuthoritySnapshot
        {
            private readonly List<QuestDefinitionAuthorityDiagnostic> _diagnostics = new List<QuestDefinitionAuthorityDiagnostic>();
            private readonly List<QuestYamlDocument> _candidates = new List<QuestYamlDocument>();

            public bool Succeeded => _diagnostics.Count == 0;
            public string SerializationMode { get; set; }
            public string ScriptPath { get; set; }
            public string ScriptGuid { get; set; }
            public string ScriptTypeFullName { get; set; }
            public int CandidateCount { get; set; }
            public int ValidAssetCount { get; set; }
            public int MalformedCandidateCount { get; set; }
            public int RemovedGuidOccurrenceCount { get; set; }
            public int DuplicateIdCount { get; set; }
            public IReadOnlyList<QuestDefinitionAuthorityDiagnostic> Diagnostics => _diagnostics;
            public IReadOnlyList<QuestYamlDocument> Candidates => _candidates;

            public void AddDiagnostic(string code, string path, long? localFileId, string message, string expected, string actual)
            {
                _diagnostics.Add(new QuestDefinitionAuthorityDiagnostic(code, path, localFileId, message, expected, actual));
            }

            public void AddCandidate(QuestYamlDocument document)
            {
                _candidates.Add(document);
            }

            public void SortAndSeal()
            {
                _diagnostics.Sort(QuestDefinitionAuthorityDiagnostic.Compare);
            }
        }

        public sealed class QuestDefinitionAuthorityDiagnostic
        {
            public QuestDefinitionAuthorityDiagnostic(string code, string path, long? localFileId, string message, string expected, string actual)
            {
                Code = code;
                Severity = "Error";
                Path = path ?? string.Empty;
                LocalFileId = localFileId;
                Message = message ?? string.Empty;
                Expected = expected ?? string.Empty;
                Actual = actual ?? string.Empty;
            }

            public string Code { get; }
            public string Severity { get; }
            public string Path { get; }
            public long? LocalFileId { get; }
            public string Message { get; }
            public string Expected { get; }
            public string Actual { get; }

            public static int Compare(QuestDefinitionAuthorityDiagnostic left, QuestDefinitionAuthorityDiagnostic right)
            {
                int path = string.Compare(left.Path, right.Path, StringComparison.Ordinal);
                if (path != 0)
                {
                    return path;
                }

                int id = Nullable.Compare(left.LocalFileId, right.LocalFileId);
                return id != 0 ? id : string.Compare(left.Code, right.Code, StringComparison.Ordinal);
            }
        }

        public sealed class QuestYamlDocument
        {
            private static readonly Regex ScriptMappingRegex = new Regex(@"m_Script:\s*\{(?<mapping>[^}]*)\}", RegexOptions.Compiled);
            private static readonly Regex MappingFieldRegex = new Regex(@"(?<key>fileID|guid|type)\s*:\s*(?<value>[^,\s}]+)", RegexOptions.Compiled);

            public QuestYamlDocument(string path, int classId, long localFileId, string text)
            {
                Path = path;
                ClassId = classId;
                LocalFileId = localFileId;
                Text = text;
                TopLevelFields = ExtractTopLevelFields(text);
                Scalars = ExtractScalars(text);
                EditorClassIdentifier = Scalars.TryGetValue("m_EditorClassIdentifier", out string editorClassIdentifier)
                    ? editorClassIdentifier
                    : string.Empty;

                ParseScriptMapping(text);
                IsQuestCandidate = DetermineQuestCandidate();
            }

            public string Path { get; }
            public int ClassId { get; }
            public long? LocalFileId { get; }
            public string Text { get; }
            public IReadOnlyCollection<string> TopLevelFields { get; }
            public IReadOnlyDictionary<string, string> Scalars { get; }
            public string EditorClassIdentifier { get; }
            public bool HasScriptField { get; private set; }
            public bool ScriptMappingParsed { get; private set; }
            public string ScriptLine { get; private set; } = string.Empty;
            public long ScriptFileId { get; private set; }
            public string ScriptGuid { get; private set; } = string.Empty;
            public bool IsQuestCandidate { get; }

            public string GetScalar(string fieldName) =>
                Scalars.TryGetValue(fieldName, out string value) ? value : string.Empty;

            private void ParseScriptMapping(string text)
            {
                string scriptLine = text.Split('\n').FirstOrDefault(line => line.TrimStart().StartsWith("m_Script:", StringComparison.Ordinal));
                if (scriptLine == null)
                {
                    return;
                }

                HasScriptField = true;
                ScriptLine = scriptLine.Trim();
                Match match = ScriptMappingRegex.Match(scriptLine);
                if (!match.Success)
                {
                    return;
                }

                var mappingValues = MappingFieldRegex.Matches(match.Groups["mapping"].Value)
                    .Cast<Match>()
                    .ToDictionary(
                        mapping => mapping.Groups["key"].Value,
                        mapping => mapping.Groups["value"].Value.Trim(),
                        StringComparer.Ordinal);

                if (!mappingValues.TryGetValue("fileID", out string fileIdValue) ||
                    !long.TryParse(fileIdValue, out long fileId) ||
                    !mappingValues.TryGetValue("guid", out string guid))
                {
                    return;
                }

                ScriptMappingParsed = true;
                ScriptFileId = fileId;
                ScriptGuid = guid;
            }

            private bool DetermineQuestCandidate()
            {
                if (string.Equals(ScriptGuid, AuthoritativeGuid, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ScriptGuid, RemovedRootGuid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(EditorClassIdentifier) &&
                    (EditorClassIdentifier.Contains(AuthoritativeTypeName) ||
                     EditorClassIdentifier.Contains(HistoricalTypeName)))
                {
                    return true;
                }

                return ExpectedFieldSet.IsSubsetOf(TopLevelFields);
            }

            private static IReadOnlyCollection<string> ExtractTopLevelFields(string text)
            {
                var fields = new HashSet<string>(StringComparer.Ordinal);
                foreach (string line in text.Split('\n'))
                {
                    Match match = Regex.Match(line, @"^  (?<field>[A-Za-z_][A-Za-z0-9_]*):");
                    if (match.Success)
                    {
                        fields.Add(match.Groups["field"].Value);
                    }
                }

                return fields;
            }

            private static IReadOnlyDictionary<string, string> ExtractScalars(string text)
            {
                var scalars = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string line in text.Split('\n'))
                {
                    Match match = Regex.Match(line, @"^  (?<field>[A-Za-z_][A-Za-z0-9_]*):\s*(?<value>.*)$");
                    if (match.Success)
                    {
                        scalars[match.Groups["field"].Value] = match.Groups["value"].Value.Trim().Trim('"');
                    }
                }

                return scalars;
            }
        }
    }
}
