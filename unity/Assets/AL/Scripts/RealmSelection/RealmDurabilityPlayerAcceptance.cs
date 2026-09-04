using System;
using System.IO;
using System.Reflection;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Narrative.Nvs01;
using AL.Services.Local;
using UnityEngine;

namespace AL.RealmSelection
{
    public readonly struct RealmDurabilityAcceptanceResult
    {
        public RealmDurabilityAcceptanceResult(
            bool passed,
            string marker,
            string technicalCode,
            RealmId committedRealmId,
            bool nvsEligible)
        {
            Passed = passed;
            Marker = marker ?? string.Empty;
            TechnicalCode = technicalCode ?? string.Empty;
            CommittedRealmId = committedRealmId;
            NvsEligible = nvsEligible;
        }

        public bool Passed { get; }
        public string Marker { get; }
        public string TechnicalCode { get; }
        public RealmId CommittedRealmId { get; }
        public bool NvsEligible { get; }
    }

    public static class RealmDurabilityPlayerAcceptance
    {
        public const string PassedMarker = "AL-REALM-DURABILITY-PLAYER-ACCEPTANCE-PASSED";
        public const string EnableArgument = "--al-realm-durability-acceptance";
        public const string RootArgument = "--al-realm-durability-root";
        public const string PhaseArgument = "--al-realm-durability-phase";
        public const string OutputArgument = "--al-realm-durability-output";
        public const string CommitPhase = "commit";
        public const string ReloadPhase = "reload";
        public const string LifecyclePhase = "lifecycle";
        public const string CommitTransactionId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        public const RealmId ExpectedRealm = RealmId.Stonehold;

        public static bool TryParseCommandLine(
            string[] arguments,
            out string root,
            out string phase,
            out string outputPath)
        {
            root = string.Empty;
            phase = LifecyclePhase;
            outputPath = string.Empty;
            if (arguments == null)
            {
                return false;
            }

            bool enabled = false;
            string previous = null;
            for (int i = 0; i < arguments.Length; i++)
            {
                string argument = arguments[i];
                if (string.Equals(argument, EnableArgument, StringComparison.Ordinal))
                {
                    enabled = true;
                }
                else if (string.Equals(previous, RootArgument, StringComparison.Ordinal))
                {
                    root = argument ?? string.Empty;
                }
                else if (string.Equals(previous, PhaseArgument, StringComparison.Ordinal))
                {
                    phase = argument ?? string.Empty;
                }
                else if (string.Equals(previous, OutputArgument, StringComparison.Ordinal))
                {
                    outputPath = argument ?? string.Empty;
                }

                previous = argument;
            }

            return enabled;
        }

        public static RealmDurabilityAcceptanceResult Run(string root, string phase)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return Fail("AL-REALM-DURABILITY-ROOT-MISSING", RealmId.None, false);
            }

            string normalizedPhase = string.IsNullOrWhiteSpace(phase) ? LifecyclePhase : phase;
            if (!string.Equals(normalizedPhase, CommitPhase, StringComparison.Ordinal) &&
                !string.Equals(normalizedPhase, ReloadPhase, StringComparison.Ordinal) &&
                !string.Equals(normalizedPhase, LifecyclePhase, StringComparison.Ordinal))
            {
                return Fail("AL-REALM-DURABILITY-PHASE-INVALID", RealmId.None, false);
            }

            RealmCatalogSnapshot catalog;
            string catalogCode;
            if (!TryLoadCatalog(out catalog, out catalogCode))
            {
                return Fail(catalogCode, RealmId.None, false);
            }

            Directory.CreateDirectory(root);
            if (string.Equals(normalizedPhase, CommitPhase, StringComparison.Ordinal) ||
                string.Equals(normalizedPhase, LifecyclePhase, StringComparison.Ordinal))
            {
                RealmDurabilityAcceptanceResult committed = CommitExpectedRealm(root, catalog);
                if (!committed.Passed)
                {
                    return committed;
                }

                if (string.Equals(normalizedPhase, CommitPhase, StringComparison.Ordinal))
                {
                    return committed;
                }
            }

            return ReloadAndProve(root, catalog);
        }

        public static void WriteOutput(string outputPath, RealmDurabilityAcceptanceResult result)
        {
            string line = result.Marker + " " + result.TechnicalCode;
            Debug.Log(line);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, line + Environment.NewLine, new UTF8Encoding(false));
        }

        private static RealmDurabilityAcceptanceResult CommitExpectedRealm(
            string root,
            RealmCatalogSnapshot catalog)
        {
            LocalSaveGameService save = CreateUncommittedWritable(root);
            var realm = new LocalRealmService(save, null, catalog);
            RealmSelectionResult result = realm.TrySelectRealm(
                new RealmSelectionRequest(CommitTransactionId, ExpectedRealm));
            if (result.Status != RealmSelectionStatus.Committed ||
                !result.Persisted ||
                save.CurrentSave == null ||
                save.CurrentSave.SelectedRealm != ExpectedRealm ||
                save.CurrentSave.RealmSelection == null ||
                !save.CurrentSave.RealmSelection.Committed)
            {
                return Fail(
                    string.IsNullOrEmpty(result.TechnicalCode)
                        ? "AL-REALM-DURABILITY-COMMIT-FAILED"
                        : result.TechnicalCode,
                    save.CurrentSave != null ? save.CurrentSave.SelectedRealm : RealmId.None,
                    false);
            }

            return Pass(save, catalog);
        }

        private static RealmDurabilityAcceptanceResult ReloadAndProve(
            string root,
            RealmCatalogSnapshot catalog)
        {
            LocalSaveGameService restarted = CreateService(root);
            restarted.Load();
            if (restarted.CurrentSave == null)
            {
                return Fail("AL-REALM-DURABILITY-RELOAD-PROFILE-MISSING", RealmId.None, false);
            }

            var realm = new LocalRealmService(restarted, null, catalog);
            if (!realm.Identity.IsCommittedValid ||
                realm.CurrentRealmId != ExpectedRealm ||
                restarted.CurrentSave.SelectedRealm != ExpectedRealm)
            {
                return Fail("AL-REALM-DURABILITY-RELOAD-RECEIPT-MISSING", realm.CurrentRealmId, false);
            }

            if (realm.CurrentRealmId == RealmId.Crownlands)
            {
                return Fail("AL-REALM-DURABILITY-CROWNLANDS-SUBSTITUTED", realm.CurrentRealmId, false);
            }

            RealmSelectionResult replay = realm.TrySelectRealm(
                new RealmSelectionRequest(CommitTransactionId, ExpectedRealm));
            if (replay.Status != RealmSelectionStatus.AlreadyCommittedSameRealm ||
                replay.MutationOccurred)
            {
                return Fail("AL-REALM-DURABILITY-REPLAY-NOT-IDEMPOTENT", realm.CurrentRealmId, false);
            }

            byte[] beforeReject = File.ReadAllBytes(Path.Combine(root, "save.json"));
            RealmSelectionResult rejected = realm.TrySelectRealm(
                new RealmSelectionRequest("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", RealmId.Crownlands));
            if (rejected.Status != RealmSelectionStatus.RejectedDifferentRealm ||
                rejected.MutationOccurred ||
                restarted.CurrentSave.SelectedRealm != ExpectedRealm)
            {
                return Fail("AL-REALM-DURABILITY-DIFFERENT-REALM-NOT-REJECTED", realm.CurrentRealmId, false);
            }

            byte[] afterReject = File.ReadAllBytes(Path.Combine(root, "save.json"));
            if (beforeReject.Length != afterReject.Length)
            {
                return Fail("AL-REALM-DURABILITY-REJECT-MUTATED-BYTES", realm.CurrentRealmId, false);
            }

            for (int i = 0; i < beforeReject.Length; i++)
            {
                if (beforeReject[i] != afterReject[i])
                {
                    return Fail("AL-REALM-DURABILITY-REJECT-MUTATED-BYTES", realm.CurrentRealmId, false);
                }
            }

            return Pass(restarted, catalog);
        }

        private static RealmDurabilityAcceptanceResult Pass(
            LocalSaveGameService save,
            RealmCatalogSnapshot catalog)
        {
            Nvs01RealmContext nvs = Nvs01RealmContextAdapter.FromPersistedIdentity(
                new LocalRealmService(save, null, catalog).Identity,
                save.CurrentSave.RealmSelection,
                catalog);
            if (!nvs.IsCommittedValid ||
                !string.Equals(nvs.RealmId, "stonehold", StringComparison.Ordinal))
            {
                return Fail("AL-REALM-DURABILITY-NVS-INELIGIBLE", save.CurrentSave.SelectedRealm, false);
            }

            if (save.CurrentSave.SelectedRealm == RealmId.Crownlands)
            {
                return Fail("AL-REALM-DURABILITY-CROWNLANDS-SUBSTITUTED", RealmId.Crownlands, true);
            }

            return new RealmDurabilityAcceptanceResult(
                true,
                PassedMarker,
                "AL-REALM-COMMITTED-VALID",
                ExpectedRealm,
                true);
        }

        private static RealmDurabilityAcceptanceResult Fail(
            string technicalCode,
            RealmId committedRealmId,
            bool nvsEligible)
        {
            return new RealmDurabilityAcceptanceResult(
                false,
                "AL-REALM-DURABILITY-PLAYER-ACCEPTANCE-FAILED",
                technicalCode,
                committedRealmId,
                nvsEligible);
        }

        private static bool TryLoadCatalog(out RealmCatalogSnapshot catalog, out string technicalCode)
        {
            catalog = null;
            technicalCode = "AL-REALM-CATALOG-MISSING";
            string path = ResolveCatalogPath();
            if (!File.Exists(path))
            {
                if (RealmCatalogRuntime.Current != null)
                {
                    catalog = RealmCatalogRuntime.Current;
                    technicalCode = RealmCatalogRuntime.TechnicalCode;
                    return true;
                }

                return false;
            }

            RealmCatalogLoadResult parsed = RealmCatalogRuntime.Parse(File.ReadAllText(path));
            if (!parsed.IsSuccess)
            {
                technicalCode = parsed.TechnicalCode;
                return false;
            }

            catalog = parsed.Snapshot;
            technicalCode = parsed.TechnicalCode;
            return true;
        }

        private static string ResolveCatalogPath()
        {
            string[] candidates =
            {
                Path.Combine(
                    Application.dataPath,
                    "AL",
                    "StreamingAssets",
                    "GameData",
                    "realm_specialized.v1.json"),
                Path.Combine(Application.streamingAssetsPath, "GameData", "realm_specialized.v1.json"),
                Path.Combine(
                    Application.streamingAssetsPath,
                    "AL",
                    "StreamingAssets",
                    "GameData",
                    "realm_specialized.v1.json")
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return candidates[0];
        }

        private static LocalSaveGameService CreateUncommittedWritable(string root)
        {
            LocalSaveGameService save = CreateService(root);
            save.Load();
            if (save.CurrentSave != null &&
                save.CurrentSave.SelectedRealm == RealmId.None &&
                save.GetCurrentAuthority().Status == ProfileWriteAuthorityStatus.Writable)
            {
                return save;
            }

            WriteSchemaTwo(root, RealmId.None, "alp_0123456789abcdef0123456789abcdef");
            save = CreateService(root);
            save.Load();
            return save;
        }

        private static LocalSaveGameService CreateService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(ISaveFileOperations) },
                null);
            if (constructor == null)
            {
                throw new InvalidOperationException("AL-REALM-DURABILITY-SAVE-CTOR-MISSING");
            }

            return (LocalSaveGameService)constructor.Invoke(
                new object[] { root, new SystemSaveFileOperations() });
        }

        private static void WriteSchemaTwo(string root, RealmId realm, string profileId)
        {
            var save = new SaveGameData
            {
                SaveFormatId = SaveGameData.CurrentSaveFormatId,
                SaveSchemaVersion = 2,
                ProfileInitializationVersion = 1,
                ProfileId = profileId,
                SelectedRealm = realm,
                CurrentChapterId = "C1"
            };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(save, true));
            File.WriteAllBytes(Path.Combine(root, "save.json"), bytes);
            File.WriteAllBytes(Path.Combine(root, "save.backup.json"), bytes);
        }
    }
}
