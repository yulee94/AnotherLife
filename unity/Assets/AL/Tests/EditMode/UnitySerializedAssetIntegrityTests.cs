using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class UnitySerializedAssetIntegrityTests
    {
        private static readonly string[] SerializedExtensions =
        {
            ".unity",
            ".prefab",
            ".asset"
        };

        [Test]
        public void TrackedUnityScenesPrefabsAndAssetsContainNoMissingScriptReferences()
        {
#if UNITY_EDITOR
            var failures = new List<string>();
            string assetsRoot = Application.dataPath;
            foreach (string path in Directory.EnumerateFiles(assetsRoot, "*.*", SearchOption.AllDirectories)
                         .Where(IsSerializedUnityAsset)
                         .Select(ToAssetPath)
                         .OrderBy(path => path))
            {
                string[] dependencies = AssetDatabase.GetDependencies(path, recursive: true);
                if (dependencies.Any(dependency => string.IsNullOrWhiteSpace(dependency)))
                {
                    failures.Add(path + " contains an empty dependency path.");
                }

                string text = File.ReadAllText(path);
                if (text.Contains("m_Script: {fileID: 0}") ||
                    text.Contains("guid: 00000000000000000000000000000000"))
                {
                    failures.Add(path + " contains a missing or zero script GUID reference.");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
#else
            Assert.Pass("Unity editor asset database is required for serialized asset integrity validation.");
#endif
        }

        private static bool IsSerializedUnityAsset(string path)
        {
            string extension = Path.GetExtension(path);
            return SerializedExtensions.Any(candidate => string.Equals(candidate, extension, System.StringComparison.OrdinalIgnoreCase));
        }

        private static string ToAssetPath(string fullPath)
        {
            string normalized = fullPath.Replace('\\', '/');
            int index = normalized.IndexOf("/Assets/", System.StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? normalized.Substring(index + 1) : normalized;
        }
    }
}
