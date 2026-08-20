using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using R = AL.Tests.EditMode.ProductionScenes.ProductionSceneTestReflection;

namespace AL.Tests.EditMode.ProductionScenes
{
    /// <summary>
    /// Focused #150 contract tests for the pure ShellFoundation Build Settings validator. Malformed
    /// profiles are supplied through immutable snapshots, so no test needs to rewrite Build Settings.
    /// </summary>
    public sealed class ProductionBuildSettingsTests
    {
        private const string SnapshotEntryType = "AL.EditorTools.ProductionBuildSettingsSnapshotEntry";

        private static string BuildSettingsPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ProjectSettings", "EditorBuildSettings.asset"));

        [Test]
        public void IosPlatformBaselineUsesVersion15AndAppleSiliconSimulator()
        {
            Assert.That(PlayerSettings.iOS.targetOSVersionString, Is.EqualTo("15.0"));
            Assert.That(
                PlayerSettings.iOS.simulatorSdkArchitecture,
                Is.EqualTo(AppleMobileArchitectureSimulator.ARM64));
        }

        [Test]
        public void StatusCodesMatchTheReviewedSpecificationExactly()
        {
            CollectionAssert.AreEqual(new[]
            {
                "Valid",
                "MissingBuildSettings",
                "EmptyBuildSettings",
                "WrongEntryScene",
                "MissingRequiredScene",
                "UnexpectedScene",
                "DeferredSceneEnabled",
                "TestSceneEnabled",
                "DisabledStaleScene",
                "MissingPath",
                "DuplicatePath",
                "DuplicateName",
                "GuidMismatch",
                "DescriptorDrift",
                "TransitionUnavailable",
                "DeferredTransitionReachable"
            }, Enum.GetNames(R.Runtime("AL.EditorTools.ProductionBuildSettingsValidationStatus")));
        }

        [Test]
        public void ExactShellFoundationPassesAndReturnsImmutableOrderedPaths()
        {
            Array snapshot = ValidSnapshot();
            object report = Validate(true, true, snapshot);

            Assert.IsTrue(R.PropBool(report, "IsValid"), R.Invoke(report, "Summarize").ToString());
            Assert.AreEqual("Valid", R.Prop(report, "PrimaryStatus").ToString());
            Assert.IsEmpty(R.AsObjects(R.Prop(report, "Diagnostics")));
            CollectionAssert.AreEqual(
                R.DescriptorShellFoundation().Select(record => R.PropString(record, "AssetPath")).ToArray(),
                R.AsStrings(R.Prop(report, "ScenePaths")));

            IList paths = (IList)R.Prop(report, "ScenePaths");
            IList diagnostics = (IList)R.Prop(report, "Diagnostics");
            Assert.IsTrue(paths.IsReadOnly);
            Assert.IsTrue(diagnostics.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => paths.Add("Assets/Injected.unity"));
            Assert.Throws<NotSupportedException>(() => diagnostics.Add(null));

            snapshot.SetValue(Snapshot("Assets/Injected.unity", "Injected", "0", true, true), 0);
            Assert.AreEqual(
                R.PropString(R.DescriptorShellFoundation()[0], "AssetPath"),
                R.AsStrings(R.Prop(report, "ScenePaths"))[0],
                "The report must copy the supplied snapshot paths.");
        }

        [Test]
        public void MissingBuildSettingsReturnsExactStatus()
        {
            object report = Validate(false, true, EmptySnapshot());
            AssertInvalid(report, "MissingBuildSettings");
            Assert.That(Statuses(report), Is.EqualTo(new[] { "MissingBuildSettings" }));
        }

        [Test]
        public void EmptyBuildSettingsReturnsExactStatus()
        {
            object report = Validate(true, true, EmptySnapshot());
            AssertInvalid(report, "EmptyBuildSettings");
            Assert.That(Statuses(report), Is.EqualTo(new[] { "EmptyBuildSettings" }));
        }

        [Test]
        public void MissingBootAndWrongEntryAreBothReported()
        {
            IReadOnlyList<object> records = R.DescriptorShellFoundation();
            object report = Validate(true, true, SnapshotArray(ValidEntry(records[1]), ValidEntry(records[2])));

            AssertInvalid(report, "WrongEntryScene");
            AssertStatus(report, "MissingRequiredScene");
        }

        [Test]
        public void MissingRequiredScenesAfterBootAreReportedDeterministically()
        {
            object report = Validate(true, true,
                SnapshotArray(ValidEntry(R.DescriptorShellFoundation()[0])));

            AssertInvalid(report, "MissingRequiredScene");
            Assert.AreEqual(
                R.DescriptorShellFoundation().Count - 1,
                Statuses(report).Count(status => status == "MissingRequiredScene"));
            AssertStatus(report, "TransitionUnavailable");
        }

        [Test]
        public void WrongOrderAfterBootReturnsDescriptorDrift()
        {
            IReadOnlyList<object> records = R.DescriptorShellFoundation();
            var entries = ValidEntries();
            object tmp = entries[1];
            entries[1] = entries[2];
            entries[2] = tmp;

            object report = Validate(true, true, SnapshotArray(entries));

            AssertInvalid(report, "DescriptorDrift");
        }

        [Test]
        public void DisabledRequiredSceneReturnsDisabledStaleScene()
        {
            Array snapshot = ValidSnapshot();
            object realm = R.DescriptorShellFoundation()[1];
            snapshot.SetValue(Entry(realm, false, true), 1);

            object report = Validate(true, true, snapshot);
            AssertInvalid(report, "DisabledStaleScene");
            AssertStatus(report, "TransitionUnavailable");
        }

        [Test]
        public void UnexpectedSceneReturnsUnexpectedScene()
        {
            var entries = ValidEntries();
            entries.Add(Snapshot("Assets/AL/Scenes/Unexpected.unity", "Unexpected",
                "11111111111111111111111111111111", true, true));

            object report = Validate(true, true, SnapshotArray(entries));
            AssertInvalid(report, "UnexpectedScene");
        }

        [Test]
        public void DisabledUnexpectedSceneAlsoReturnsDisabledStaleScene()
        {
            var entries = ValidEntries();
            entries.Add(Snapshot("Assets/AL/Scenes/Stale.unity", "Stale",
                "22222222222222222222222222222222", false, true));

            object report = Validate(true, true, SnapshotArray(entries));
            AssertInvalid(report, "UnexpectedScene");
            AssertStatus(report, "DisabledStaleScene");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RepresentativeTestIsRejectedEvenWhenDisabled(bool enabled)
        {
            var entries = ValidEntries();
            object testRecord = R.RecordById("al_scene_test_representative");
            entries.Add(Entry(testRecord, enabled, true));

            object report = Validate(true, true, SnapshotArray(entries));
            AssertInvalid(report, "TestSceneEnabled");
            AssertStatus(report, "TestSceneEnabled");
            if (!enabled)
            {
                AssertStatus(report, "DisabledStaleScene");
            }
        }

        [Test]
        public void ChampionArenaIsRequiredInShellFoundation()
        {
            object report = Validate(true, true, ValidSnapshot());
            Assert.IsTrue(R.PropBool(report, "IsValid"), R.Invoke(report, "Summarize").ToString());
            Assert.That(
                R.AsStrings(R.Prop(report, "ScenePaths")),
                Does.Contain("Assets/AL/Scenes/ChampionArena.unity"));
            Assert.That(
                R.AsStrings(R.Prop(report, "ScenePaths")),
                Does.Contain("Assets/AL/Scenes/CharacterCreation.unity"));
        }

        [Test]
        public void DisabledChampionArenaReturnsDisabledStaleScene()
        {
            Array snapshot = ValidSnapshot();
            IReadOnlyList<object> records = R.DescriptorShellFoundation();
            int championIndex = -1;
            for (int i = 0; i < records.Count; i++)
            {
                if (R.PropString(records[i], "SceneId") == "al_scene_champion_arena")
                {
                    championIndex = i;
                    break;
                }
            }

            Assert.GreaterOrEqual(championIndex, 0);
            snapshot.SetValue(Entry(records[championIndex], false, true), championIndex);

            object report = Validate(true, true, snapshot);
            AssertInvalid(report, "DisabledStaleScene");
        }

        [Test]
        public void MissingPathReturnsMissingPath()
        {
            Array snapshot = ValidSnapshot();
            snapshot.SetValue(Entry(R.DescriptorShellFoundation()[2], true, false), 2);

            object report = Validate(true, true, snapshot);
            AssertInvalid(report, "MissingPath");
        }

        [Test]
        public void DuplicatePathReturnsDuplicatePath()
        {
            var entries = ValidEntries();
            entries.Add(ValidEntry(R.DescriptorShellFoundation()[0]));

            object report = Validate(true, true, SnapshotArray(entries));
            AssertStatus(report, "DuplicatePath");
        }

        [Test]
        public void DuplicateNameReturnsDuplicateName()
        {
            var entries = ValidEntries();
            object boot = R.DescriptorShellFoundation()[0];
            entries.Add(Snapshot(
                "Assets/AL/Scenes/BootCopy.unity",
                R.PropString(boot, "SceneName"),
                "33333333333333333333333333333333",
                true,
                true));

            object report = Validate(true, true, SnapshotArray(entries));
            AssertStatus(report, "DuplicateName");
        }

        [Test]
        public void CaseOnlyDuplicateNameReturnsDuplicateName()
        {
            var entries = ValidEntries();
            object boot = R.DescriptorShellFoundation()[0];
            entries.Add(Snapshot(
                "Assets/AL/Scenes/BootCaseCopy.unity",
                R.PropString(boot, "SceneName").ToLowerInvariant(),
                "55555555555555555555555555555555",
                true,
                true));

            object report = Validate(true, true, SnapshotArray(entries));
            AssertStatus(report, "DuplicateName");
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ActiveDeferredOrResetImplementationReachabilityFailsClosed(
            bool deferredChampionHandlerReachable,
            bool resetToBootReachable)
        {
            object report = Validate(
                true,
                true,
                ValidSnapshot(),
                deferredChampionHandlerReachable,
                resetToBootReachable);

            AssertInvalid(report, "DeferredTransitionReachable");
        }

        [Test]
        public void GuidMismatchReturnsGuidMismatch()
        {
            Array snapshot = ValidSnapshot();
            object boot = R.DescriptorShellFoundation()[0];
            snapshot.SetValue(Snapshot(
                R.PropString(boot, "AssetPath"),
                R.PropString(boot, "SceneName"),
                "44444444444444444444444444444444",
                true,
                true), 0);

            object report = Validate(true, true, snapshot);
            AssertInvalid(report, "GuidMismatch");
        }

        [Test]
        public void ExactPathWithWrongNameReturnsDescriptorDrift()
        {
            Array snapshot = ValidSnapshot();
            object realm = R.DescriptorShellFoundation()[1];
            snapshot.SetValue(Snapshot(
                R.PropString(realm, "AssetPath"),
                "realmSelection",
                R.PropString(realm, "AssetGuid"),
                true,
                true), 1);

            object report = Validate(true, true, snapshot);
            AssertInvalid(report, "DescriptorDrift");
        }

        [Test]
        public void CaseMismatchedPathFailsClosed()
        {
            Array snapshot = ValidSnapshot();
            object realm = R.DescriptorShellFoundation()[1];
            snapshot.SetValue(Snapshot(
                R.PropString(realm, "AssetPath").ToLowerInvariant(),
                R.PropString(realm, "SceneName"),
                R.PropString(realm, "AssetGuid"),
                true,
                true), 1);

            object report = Validate(true, true, snapshot);
            Assert.IsFalse(R.PropBool(report, "IsValid"));
            AssertStatus(report, "MissingRequiredScene");
            AssertStatus(report, "UnexpectedScene");
        }

        [Test]
        public void InvalidCommittedDescriptorReturnsDescriptorDrift()
        {
            object report = Validate(true, false, ValidSnapshot());
            AssertInvalid(report, "DescriptorDrift");
        }

        [Test]
        public void DiagnosticsAndSummaryHaveDeterministicOrdering()
        {
            var entries = ValidEntries();
            entries[0] = Snapshot(string.Empty, string.Empty, string.Empty, false, false);
            entries.Add(Entry(R.RecordById("al_scene_test_representative"), false, true));

            object first = Validate(true, false, SnapshotArray(entries));
            object second = Validate(true, false, SnapshotArray(entries.AsEnumerable().Reverse().Reverse()));
            CollectionAssert.AreEqual(Statuses(first), Statuses(second));
            Assert.AreEqual(R.Invoke(first, "Summarize").ToString(), R.Invoke(second, "Summarize").ToString());

            int[] statusValues = R.AsObjects(R.Prop(first, "Diagnostics"))
                .Select(diagnostic => Convert.ToInt32(R.Prop(diagnostic, "Status")))
                .ToArray();
            CollectionAssert.AreEqual(statusValues.OrderBy(value => value).ToArray(), statusValues,
                "Diagnostics must be ordered by the stable status-code declaration.");
        }

        [Test]
        public void CurrentValidationLeavesBuildSettingsByteForByteUnchanged()
        {
            byte[] before = File.ReadAllBytes(BuildSettingsPath);
            object report = R.StaticMethod(R.BuildSettingsValidatorType, "ValidateCurrentShellFoundation");
            Assert.NotNull(report);
            Assert.IsTrue(R.PropBool(report, "IsValid"), R.Invoke(report, "Summarize").ToString());
            byte[] after = File.ReadAllBytes(BuildSettingsPath);
            Assert.AreEqual(before, after,
                "ShellFoundation validation must never rewrite EditorBuildSettings.asset.");
        }

        private static void AssertInvalid(object report, string primaryStatus)
        {
            Assert.IsFalse(R.PropBool(report, "IsValid"));
            Assert.AreEqual(primaryStatus, R.Prop(report, "PrimaryStatus").ToString(),
                R.Invoke(report, "Summarize").ToString());
        }

        private static void AssertStatus(object report, string status)
        {
            Assert.That(Statuses(report), Has.Member(status), R.Invoke(report, "Summarize").ToString());
        }

        private static IReadOnlyList<string> Statuses(object report)
        {
            return R.AsObjects(R.Prop(report, "Diagnostics"))
                .Select(diagnostic => R.Prop(diagnostic, "Status").ToString())
                .ToList();
        }

        private static object Validate(bool buildSettingsPresent, bool descriptorValid, Array snapshot)
        {
            return R.StaticMethod(
                R.BuildSettingsValidatorType,
                "ValidateSnapshot",
                buildSettingsPresent,
                snapshot,
                descriptorValid);
        }

        private static object Validate(
            bool buildSettingsPresent,
            bool descriptorValid,
            Array snapshot,
            bool deferredChampionHandlerReachable,
            bool resetToBootReachable)
        {
            return R.StaticMethod(
                R.BuildSettingsValidatorType,
                "ValidateSnapshot",
                buildSettingsPresent,
                snapshot,
                descriptorValid,
                deferredChampionHandlerReachable,
                resetToBootReachable);
        }

        private static Array ValidSnapshot()
        {
            return SnapshotArray(ValidEntries());
        }

        private static List<object> ValidEntries()
        {
            return R.DescriptorShellFoundation().Select(ValidEntry).ToList();
        }

        private static object ValidEntry(object descriptorRecord)
        {
            return Entry(descriptorRecord, true, true);
        }

        private static object Entry(object descriptorRecord, bool enabled, bool pathExists)
        {
            return Snapshot(
                R.PropString(descriptorRecord, "AssetPath"),
                R.PropString(descriptorRecord, "SceneName"),
                R.PropString(descriptorRecord, "AssetGuid"),
                enabled,
                pathExists);
        }

        private static object Snapshot(string path, string name, string guid, bool enabled, bool pathExists)
        {
            return Activator.CreateInstance(
                R.Runtime(SnapshotEntryType),
                path,
                name,
                guid,
                enabled,
                pathExists);
        }

        private static Array EmptySnapshot()
        {
            return Array.CreateInstance(R.Runtime(SnapshotEntryType), 0);
        }

        private static Array SnapshotArray(params object[] entries)
        {
            return SnapshotArray((IEnumerable<object>)entries);
        }

        private static Array SnapshotArray(IEnumerable<object> entries)
        {
            object[] copied = (entries ?? Array.Empty<object>()).ToArray();
            Array array = Array.CreateInstance(R.Runtime(SnapshotEntryType), copied.Length);
            for (int index = 0; index < copied.Length; index++)
            {
                array.SetValue(copied[index], index);
            }

            return array;
        }
    }
}
