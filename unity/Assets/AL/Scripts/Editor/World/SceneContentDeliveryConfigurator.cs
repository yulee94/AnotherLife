#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using AL.Data.Catalogs.WorldStreaming;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AL.Editor.World
{
    public sealed class SceneContentDeliveryValidationReport
    {
        internal SceneContentDeliveryValidationReport(
            int groupCount,
            int entryCount,
            int unexpectedEntryCount,
            bool remoteCatalogsEnabled,
            bool allGroupsUseLocalPaths,
            IEnumerable<string> diagnostics)
        {
            GroupCount = groupCount;
            EntryCount = entryCount;
            UnexpectedEntryCount = unexpectedEntryCount;
            RemoteCatalogsEnabled = remoteCatalogsEnabled;
            AllGroupsUseLocalPaths = allGroupsUseLocalPaths;
            Diagnostics = new ReadOnlyCollection<string>(
                (diagnostics ?? Array.Empty<string>()).ToArray());
        }

        public int GroupCount { get; }
        public int EntryCount { get; }
        public int UnexpectedEntryCount { get; }
        public bool RemoteCatalogsEnabled { get; }
        public bool AllGroupsUseLocalPaths { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;

        public string Summarize()
        {
            string header =
                $"[AL-SCENE-CONTENT-DELIVERY] valid={IsValid} groups={GroupCount} " +
                $"entries={EntryCount} unexpected={UnexpectedEntryCount} " +
                $"remoteCatalogs={RemoteCatalogsEnabled} localPaths={AllGroupsUseLocalPaths}";
            return Diagnostics.Count == 0
                ? header
                : header + "\n  " + string.Join("\n  ", Diagnostics);
        }
    }

    /// <summary>
    /// Applies owner-approved DEC-SCENE-DELIVERY-001 as amended by DEC-SCENE-DELIVERY-002.
    /// The five shell scenes remain direct Build Settings entries; all 78 catalog chunks are
    /// local-only Addressables grouped by world.
    /// </summary>
    public static class SceneContentDeliveryConfigurator
    {
        private const string CatalogRelativePath =
            "AL/StreamingAssets/GameData/al_world_streaming_catalog.json";
        private const string GroupPrefix = "AL.World.";
        private const string GeneratedSceneLabel = "al-generated-scene";
        private const string ConfigureMenu =
            "AnotherLife/World/Configure Approved Local Addressables";

        private sealed class ExpectedEntry
        {
            internal ExpectedEntry(string guid, string groupName, string address)
            {
                Guid = guid;
                GroupName = groupName;
                Address = address;
            }

            internal string Guid { get; }
            internal string GroupName { get; }
            internal string Address { get; }
        }

        [MenuItem(ConfigureMenu)]
        public static void ConfigureFromMenu()
        {
            ConfigureApprovedLocalAddressables();
            SceneContentDeliveryValidationReport report = ValidateCurrentConfiguration();
            if (!report.IsValid)
            {
                throw new InvalidOperationException(report.Summarize());
            }

            Debug.Log(report.Summarize());
        }

        public static void ConfigureApprovedLocalAddressables()
        {
            IReadOnlyList<ExpectedEntry> expected = BuildExpectedEntries();
            string[] expectedGroupNames = expected
                .Select(value => value.GroupName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var expectedGuids = new HashSet<string>(
                expected.Select(value => value.Guid),
                StringComparer.Ordinal);

            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.GetSettings(create: true);
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables settings could not be created.");
            }

            settings.BuildRemoteCatalog = false;
            settings.DisableCatalogUpdateOnStartup = true;
            settings.UniqueBundleIds = false;

            var groups = new Dictionary<string, AddressableAssetGroup>(StringComparer.Ordinal);
            foreach (string groupName in expectedGroupNames)
            {
                AddressableAssetGroup group = settings.FindGroup(groupName) ??
                    settings.CreateGroup(
                        groupName,
                        setAsDefaultGroup: false,
                        readOnly: false,
                        postEvent: false,
                        schemasToCopy: null,
                        typeof(BundledAssetGroupSchema));
                ConfigureLocalSchema(group, settings);
                groups.Add(groupName, group);
            }

            settings.DefaultGroup = groups[expectedGroupNames[0]];
            foreach (AddressableAssetGroup group in settings.groups
                         .Where(value => value != null && !groups.ContainsKey(value.Name))
                         .ToArray())
            {
                settings.RemoveGroup(group);
            }

            foreach (ExpectedEntry item in expected.OrderBy(value => value.Address, StringComparer.Ordinal))
            {
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(
                    item.Guid,
                    groups[item.GroupName],
                    readOnly: false,
                    postEvent: false);
                if (entry == null)
                {
                    throw new InvalidOperationException(
                        $"Could not configure Addressable scene GUID {item.Guid}.");
                }

                entry.address = item.Address;
                entry.SetLabel(GeneratedSceneLabel, enable: true, force: true, postEvent: false);
            }

            foreach (AddressableAssetGroup group in settings.groups.Where(value => value != null))
            {
                foreach (AddressableAssetEntry entry in group.entries
                             .Where(value => value != null && !expectedGuids.Contains(value.guid))
                             .ToArray())
                {
                    settings.RemoveAssetEntry(entry.guid, postEvent: false);
                }
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static SceneContentDeliveryValidationReport ValidateCurrentConfiguration()
        {
            IReadOnlyList<ExpectedEntry> expected = BuildExpectedEntries();
            var expectedByGuid = expected.ToDictionary(value => value.Guid, StringComparer.Ordinal);
            string[] expectedGroupNames = expected
                .Select(value => value.GroupName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var diagnostics = new List<string>();
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.GetSettings(create: false);
            if (settings == null)
            {
                diagnostics.Add("Addressables settings are missing.");
                return new SceneContentDeliveryValidationReport(
                    0, 0, expected.Count, false, false, diagnostics);
            }

            AddressableAssetGroup[] groups = settings.groups
                .Where(value => value != null)
                .ToArray();
            string[] actualGroupNames = groups.Select(value => value.Name).ToArray();
            if (!new HashSet<string>(actualGroupNames, StringComparer.Ordinal)
                    .SetEquals(expectedGroupNames) ||
                actualGroupNames.Length != expectedGroupNames.Length)
            {
                diagnostics.Add(
                    "Addressables groups differ from the eleven approved world groups.");
            }

            bool allLocal = true;
            int unexpected = 0;
            var seenGuids = new HashSet<string>(StringComparer.Ordinal);
            foreach (AddressableAssetGroup group in groups)
            {
                BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
                bool groupUsesLocalPaths = schema != null &&
                    string.Equals(
                        schema.BuildPath.GetName(settings),
                        AddressableAssetSettings.kLocalBuildPath,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        schema.LoadPath.GetName(settings),
                        AddressableAssetSettings.kLocalLoadPath,
                        StringComparison.Ordinal);
                allLocal &= groupUsesLocalPaths;
                if (!groupUsesLocalPaths)
                {
                    diagnostics.Add($"Group '{group.Name}' does not use Local Build/Load paths.");
                }

                if (group.GetSchema<ContentUpdateGroupSchema>() != null)
                {
                    diagnostics.Add($"Group '{group.Name}' enables content-update schema.");
                }

                foreach (AddressableAssetEntry entry in group.entries.Where(value => value != null))
                {
                    seenGuids.Add(entry.guid);
                    if (!expectedByGuid.TryGetValue(entry.guid, out ExpectedEntry item) ||
                        !string.Equals(group.Name, item.GroupName, StringComparison.Ordinal) ||
                        !string.Equals(entry.address, item.Address, StringComparison.Ordinal) ||
                        !entry.labels.Contains(GeneratedSceneLabel))
                    {
                        unexpected++;
                    }
                }
            }

            int missing = expectedByGuid.Keys.Count(guid => !seenGuids.Contains(guid));
            unexpected += missing;
            if (unexpected > 0)
            {
                diagnostics.Add(
                    $"Addressable scene membership/address/label drift count is {unexpected}.");
            }

            if (settings.BuildRemoteCatalog)
            {
                diagnostics.Add("Remote catalogs are enabled without owner approval.");
            }

            if (!settings.DisableCatalogUpdateOnStartup)
            {
                diagnostics.Add("Runtime catalog-update checks are enabled without owner approval.");
            }

            int entryCount = groups.Sum(group => group.entries.Count);
            if (entryCount != expected.Count)
            {
                diagnostics.Add($"Addressables contains {entryCount} entries; expected {expected.Count}.");
            }

            return new SceneContentDeliveryValidationReport(
                groups.Length,
                entryCount,
                unexpected,
                settings.BuildRemoteCatalog,
                allLocal,
                diagnostics);
        }

        public static void ConfigureAndValidateForBatch()
        {
            ConfigureApprovedLocalAddressables();
            SceneContentDeliveryValidationReport report = ValidateCurrentConfiguration();
            if (!report.IsValid)
            {
                throw new InvalidOperationException(report.Summarize());
            }

            Debug.Log(report.Summarize());
        }

        public static void BuildApprovedLocalContentForBatch()
        {
            ConfigureAndValidateForBatch();
            AddressableAssetSettings.BuildPlayerContent(
                out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException($"Local Addressables content build failed: {result.Error}");
            }

            int worldBundleCount = result.AssetBundleBuildResults.Count(bundle =>
                bundle.SourceAssetGroup != null &&
                bundle.SourceAssetGroup.Name.StartsWith(GroupPrefix, StringComparison.Ordinal) &&
                !bundle.InternalBundleName.EndsWith("_unitybuiltinassets", StringComparison.Ordinal) &&
                !bundle.InternalBundleName.EndsWith("_monoscripts", StringComparison.Ordinal));
            if (worldBundleCount != 11)
            {
                throw new InvalidOperationException(
                    $"Expected 11 local world bundles; built {worldBundleCount}.");
            }

            Debug.Log(
                $"[AL-SCENE-CONTENT-BUILD] worldBundles={worldBundleCount} " +
                $"totalBundles={result.AssetBundleBuildResults.Count} " +
                $"locations={result.LocationCount} output='{result.OutputPath}'");
        }

        private static void ConfigureLocalSchema(
            AddressableAssetGroup group,
            AddressableAssetSettings settings)
        {
            foreach (AddressableAssetGroupSchema schema in group.Schemas
                         .Where(value => value != null && !(value is BundledAssetGroupSchema))
                         .ToArray())
            {
                group.RemoveSchema(schema.GetType(), postEvent: false);
            }

            BundledAssetGroupSchema bundled =
                group.GetSchema<BundledAssetGroupSchema>() ??
                group.AddSchema<BundledAssetGroupSchema>(postEvent: false);
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
        }

        private static IReadOnlyList<ExpectedEntry> BuildExpectedEntries()
        {
            string catalogPath = Path.Combine(Application.dataPath, CatalogRelativePath);
            WorldStreamingLoadResult result =
                WorldStreamingCatalogLoader.Validate(File.ReadAllBytes(catalogPath));
            if (!result.IsAccepted)
            {
                throw new InvalidOperationException(
                    "World streaming catalog rejected:\n" +
                    string.Join("\n", result.Diagnostics.Select(value => value.Fingerprint)));
            }

            var expected = new List<ExpectedEntry>(result.Snapshot.Chunks.Count);
            foreach (WorldChunkDefinition chunk in result.Snapshot.Chunks
                         .OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                string guid = AssetDatabase.AssetPathToGUID(chunk.ScenePath);
                if (string.IsNullOrWhiteSpace(guid))
                {
                    throw new InvalidOperationException(
                        $"Catalog scene is missing or has no GUID: {chunk.ScenePath}");
                }

                expected.Add(new ExpectedEntry(
                    guid,
                    GroupPrefix + chunk.WorldId,
                    $"scene/{chunk.WorldId}/{chunk.Id}"));
            }

            if (expected.Count != 78)
            {
                throw new InvalidOperationException(
                    $"Approved scene delivery requires 78 chunks; found {expected.Count}.");
            }

            return new ReadOnlyCollection<ExpectedEntry>(expected);
        }
    }
}
#endif
