using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public class QuestDefinitionAssetAuthorityTests
    {
        private const string AuthoritativePath = "Assets/AL/Scripts/Data/Definitions/Narrative/QuestDefinition.cs";
        private const string AuthoritativeTypeName = "AL.Data.Definitions.Narrative.QuestDefinition";
        private const string AuthoritativeGuid = "c385b2b183b74184ca75eeffbe2256ef";
        private const string RemovedRootGuid = "226022aa7500f3e4abc8ac3757707ad8";

        [Test]
        public void AuthoritativeQuestDefinitionScriptKeepsExpectedGuid()
        {
            Assert.AreEqual(AuthoritativeGuid, AssetDatabase.AssetPathToGUID(AuthoritativePath));

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AuthoritativePath);
            Assert.NotNull(script, $"Expected QuestDefinition script at {AuthoritativePath}.");

            Type definitionType = script.GetClass();
            Assert.NotNull(definitionType, "Expected QuestDefinition.cs to contain a loadable class.");
            Assert.AreEqual(AuthoritativeTypeName, definitionType.FullName);
            Assert.True(typeof(ScriptableObject).IsAssignableFrom(definitionType));
        }

        [Test]
        public void SerializedAssetsDoNotReferenceRemovedRootQuestDefinitionGuid()
        {
            string[] matches = FindSerializedAssetGuidOccurrences(RemovedRootGuid).ToArray();

            CollectionAssert.IsEmpty(
                matches,
                "Tracked serialized Unity assets must not reference the removed root QuestDefinition script GUID.");
        }

        [Test]
        public void ExactlyOneProductionQuestDefinitionTypeExists()
        {
            string[] questDefinitionTypes = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(type => type.Name == "QuestDefinition")
                .Select(type => type.FullName)
                .OrderBy(typeName => typeName, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[] { AuthoritativeTypeName },
                questDefinitionTypes,
                "Exactly one production ScriptableObject type named QuestDefinition must be discoverable.");
        }

        [Test]
        public void QuestDefinitionAssetsResolveToNarrativeTypeAndAuthoritativeGuidWithUniqueIds()
        {
            Type definitionType = AssetDatabase.LoadAssetAtPath<MonoScript>(AuthoritativePath).GetClass();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (string path in FindQuestDefinitionAssetPaths())
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;

                Assert.NotNull(asset, $"Expected quest asset at {path}.");
                Assert.IsInstanceOf(definitionType, asset, $"Quest asset {path} must use the narrative QuestDefinition type.");

                MonoScript script = MonoScript.FromScriptableObject(asset);
                Assert.NotNull(script, $"Quest asset {path} must have a script reference.");
                Assert.AreEqual(
                    AuthoritativeGuid,
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script)),
                    $"Quest asset {path} must reference the authoritative QuestDefinition script GUID.");

                string id = definitionType.GetField("Id")?.GetValue(asset) as string;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                Assert.True(seenIds.Add(id), $"Duplicate QuestDefinition Id '{id}' found at {path}.");
            }
        }

        private static IEnumerable<string> FindQuestDefinitionAssetPaths()
        {
            return AssetDatabase.FindAssets("t:QuestDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal);
        }

        private static IEnumerable<string> FindSerializedAssetGuidOccurrences(string guid)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (string path in Directory.EnumerateFiles(Application.dataPath, "*", SearchOption.AllDirectories))
            {
                if (!IsSerializedUnityTextFile(path))
                {
                    continue;
                }

                string text = File.ReadAllText(path);
                if (!text.Contains(guid))
                {
                    continue;
                }

                yield return ToProjectRelativePath(projectRoot, path);
            }
        }

        private static string ToProjectRelativePath(string projectRoot, string path)
        {
            string relative = path.Substring(projectRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static bool IsSerializedUnityTextFile(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".asset":
                case ".controller":
                case ".mat":
                case ".meta":
                case ".overridecontroller":
                case ".playable":
                case ".prefab":
                case ".unity":
                    return true;
                default:
                    return false;
            }
        }
    }
}
