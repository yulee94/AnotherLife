using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AL.Core;
using AL.Data.Catalogs;
using UnityEngine;
using UnityEngine.Networking;

namespace AL.RealmSelection
{
    public enum RealmCatalogRuntimeStatus { NotStarted, Loading, Ready, Unavailable }

    public sealed class RealmCatalogEntry
    {
        internal RealmCatalogEntry(
            string id,
            RealmId runtimeId,
            string displayName,
            string peopleName,
            string markName,
            string silhouetteLanguage,
            string materialLanguage)
        {
            Id = id;
            RuntimeId = runtimeId;
            DisplayName = displayName;
            PeopleName = peopleName ?? string.Empty;
            MarkName = markName ?? string.Empty;
            SilhouetteLanguage = silhouetteLanguage ?? string.Empty;
            MaterialLanguage = materialLanguage ?? string.Empty;
        }
        public string Id { get; }
        public RealmId RuntimeId { get; }
        public string DisplayName { get; }
        public string PeopleName { get; }
        public string MarkName { get; }
        public string SilhouetteLanguage { get; }
        public string MaterialLanguage { get; }
    }

    public sealed class RealmCatalogSnapshot
    {
        private readonly Dictionary<RealmId, RealmCatalogEntry> _entries;
        internal RealmCatalogSnapshot(string version, IList<RealmCatalogEntry> entries)
        {
            Version = version;
            var copy = new RealmCatalogEntry[entries.Count];
            entries.CopyTo(copy, 0);
            Realms = Array.AsReadOnly(copy);
            _entries = new Dictionary<RealmId, RealmCatalogEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++) _entries.Add(entries[i].RuntimeId, entries[i]);
        }
        public string Version { get; }
        public IReadOnlyList<RealmCatalogEntry> Realms { get; }
        public bool TryGet(RealmId id, out RealmCatalogEntry entry) => _entries.TryGetValue(id, out entry);
    }

    public sealed class RealmCatalogLoadResult
    {
        internal RealmCatalogLoadResult(RealmCatalogSnapshot snapshot, string technicalCode)
        {
            Snapshot = snapshot;
            TechnicalCode = technicalCode ?? string.Empty;
        }
        public RealmCatalogSnapshot Snapshot { get; }
        public string TechnicalCode { get; }
        public bool IsSuccess => Snapshot != null;
    }

    public static class RealmCatalogRuntime
    {
        public const string RelativePath = "GameData/realm_specialized.v1.json";
        public const string SupportedVersion = "0.1.0";
        public const int MaximumByteLength = 32768;

        public static RealmCatalogRuntimeStatus Status { get; private set; }
        public static RealmCatalogSnapshot Current { get; private set; }
        public static int CurrentGeneration { get; private set; }
        public static string TechnicalCode { get; private set; } = "AL-REALM-CATALOG-NOT-STARTED";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            Status = RealmCatalogRuntimeStatus.NotStarted;
            Current = null;
            CurrentGeneration = 0;
            TechnicalCode = "AL-REALM-CATALOG-NOT-STARTED";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeginLoad()
        {
            TryStartAttempt();
        }

        public static bool TryRetry()
        {
            if (Status != RealmCatalogRuntimeStatus.Unavailable &&
                Status != RealmCatalogRuntimeStatus.NotStarted)
            {
                return false;
            }

            return TryStartAttempt();
        }

        private static bool TryStartAttempt()
        {
            if (Status == RealmCatalogRuntimeStatus.Loading ||
                Status == RealmCatalogRuntimeStatus.Ready)
            {
                return false;
            }

            if (CurrentGeneration == int.MaxValue)
            {
                Current = null;
                Status = RealmCatalogRuntimeStatus.Unavailable;
                TechnicalCode = "AL-REALM-CATALOG-GENERATION-EXHAUSTED";
                return false;
            }

            CurrentGeneration++;
            MarkLoading();
            var host = new GameObject("AL.RealmCatalogRuntime");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<RealmCatalogRuntimeHost>().Initialize(CurrentGeneration);
            return true;
        }

        public static RealmCatalogLoadResult Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return Reject("AL-REALM-CATALOG-MISSING");
            if (Encoding.UTF8.GetByteCount(json) > MaximumByteLength) return Reject("AL-REALM-CATALOG-OVERSIZE");
            GameDataFamilyCatalogSnapshot family;
            string diagnosticCode;
            if (!WireFamilyCatalogLoader.TryLoad("realm_specialized", json, out family, out diagnosticCode))
                return Reject("AL-REALM-CATALOG-MALFORMED");
            return Project(family);
        }

        private static RealmCatalogLoadResult Project(GameDataFamilyCatalogSnapshot family)
        {
            GameDataCatalogRecord policy;
            if (!WireFamilyCatalogLoader.TryGetRecord(family, "selection_policy", out policy) ||
                !HasExact(policy, "selection_mode", "one_realm_per_account") ||
                !HasExact(policy, "realm_lock_scope", "account") ||
                !HasExact(policy, "sub_character_policy", "same_realm_only") ||
                !HasExact(policy, "shared_storage_policy", "same_realm_account_storage") ||
                !HasExact(policy, "cross_realm_creation_policy", "reject") ||
                !HasExact(policy, "realm_change_policy", "not_supported_after_commit") ||
                !HasExact(policy, "uncommitted_profile_state", "realm_unselected") ||
                !HasExact(policy, "committed_profile_state", "realm_locked"))
            {
                return Reject("AL-REALM-CATALOG-POLICY-MISMATCH");
            }

            GameDataCatalogRecord orderRecord;
            string[] realmOrder;
            if (!WireFamilyCatalogLoader.TryGetRecord(family, "realm_order", out orderRecord) ||
                !WireFamilyCatalogLoader.TryGetStringArray(orderRecord, "realm_ids", out realmOrder) ||
                realmOrder == null ||
                realmOrder.Length != 4)
            {
                return Reject("AL-REALM-CATALOG-REALM-COUNT");
            }

            var realmRecords = WireFamilyCatalogLoader.RecordsOfKind(family, "realm");
            if (realmRecords.Count != 4)
            {
                return Reject("AL-REALM-CATALOG-REALM-COUNT");
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenRuntime = new HashSet<RealmId>();
            var seenGemIds = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<RealmCatalogEntry>(4);
            for (int i = 0; i < realmRecords.Count; i++)
            {
                GameDataCatalogRecord realm = realmRecords[i];
                string displayName;
                string legacyRuntimeId;
                string[] realmGemIds;
                RealmId expectedRuntimeId;
                RealmId runtimeId;
                if (realm == null ||
                    !TryStableRuntimeId(realm.Id, out expectedRuntimeId) ||
                    !seenIds.Add(realm.Id) ||
                    !WireFamilyCatalogLoader.TryGetString(realm, "display_name", out displayName) ||
                    !WireFamilyCatalogLoader.TryGetString(realm, "legacy_runtime_id", out legacyRuntimeId) ||
                    !TryRuntimeId(legacyRuntimeId, out runtimeId) ||
                    runtimeId != expectedRuntimeId ||
                    !seenRuntime.Add(runtimeId) ||
                    !WireFamilyCatalogLoader.TryGetStringArray(realm, "realm_gem_ids", out realmGemIds) ||
                    realmGemIds == null ||
                    realmGemIds.Length != 2 ||
                    !IsStableId(realmGemIds[0]) ||
                    !IsStableId(realmGemIds[1]) ||
                    !seenGemIds.Add(realmGemIds[0]) ||
                    !seenGemIds.Add(realmGemIds[1]))
                {
                    return Reject("AL-REALM-CATALOG-INVALID-REALM");
                }

                string peopleName;
                if (!WireFamilyCatalogLoader.TryGetString(realm, "people_name", out peopleName))
                {
                    peopleName = string.Empty;
                }

                string markName;
                string silhouetteLanguage;
                string materialLanguage;
                TryReadVisualIdentity(realm, out markName, out silhouetteLanguage, out materialLanguage);
                entries.Add(new RealmCatalogEntry(
                    realm.Id,
                    runtimeId,
                    displayName,
                    peopleName,
                    markName,
                    silhouetteLanguage,
                    materialLanguage));
            }

            for (int i = 0; i < realmOrder.Length; i++)
                if (!seenIds.Contains(realmOrder[i])) return Reject("AL-REALM-CATALOG-ORDER-MISMATCH");
            if (new HashSet<string>(realmOrder, StringComparer.Ordinal).Count != 4)
                return Reject("AL-REALM-CATALOG-ORDER-MISMATCH");
            entries.Sort((left, right) => Array.IndexOf(realmOrder, left.Id).CompareTo(Array.IndexOf(realmOrder, right.Id)));
            return new RealmCatalogLoadResult(new RealmCatalogSnapshot(SupportedVersion, entries), "AL-REALM-CATALOG-READY");
        }

        private static bool HasExact(GameDataCatalogRecord record, string field, string expected)
        {
            string value;
            return WireFamilyCatalogLoader.TryGetString(record, field, out value) &&
                   string.Equals(value, expected, StringComparison.Ordinal);
        }

        internal static void MarkLoading()
        {
            Status = RealmCatalogRuntimeStatus.Loading;
            TechnicalCode = "AL-REALM-CATALOG-LOADING";
        }

        internal static void Publish(RealmCatalogLoadResult result)
        {
            Publish(CurrentGeneration, result);
        }

        internal static void Publish(int attemptGeneration, RealmCatalogLoadResult result)
        {
            if (attemptGeneration != CurrentGeneration ||
                Status != RealmCatalogRuntimeStatus.Loading ||
                result == null)
            {
                return;
            }

            Current = result.Snapshot;
            TechnicalCode = result.TechnicalCode;
            Status = Current == null ? RealmCatalogRuntimeStatus.Unavailable : RealmCatalogRuntimeStatus.Ready;
        }

        internal static RealmCatalogLoadResult ReadFailure() => Reject("AL-REALM-CATALOG-READ-FAILED");
        internal static RealmCatalogLoadResult OversizeFailure() => Reject("AL-REALM-CATALOG-OVERSIZE");

        private static bool TryStableRuntimeId(string stableId, out RealmId id)
        {
            switch (stableId)
            {
                case "crownlands": id = RealmId.Crownlands; return true;
                case "stonehold": id = RealmId.Stonehold; return true;
                case "eldergrove": id = RealmId.Eldergrove; return true;
                case "umbral": id = RealmId.Umbral; return true;
                default: id = RealmId.None; return false;
            }
        }

        private static bool TryRuntimeId(string value, out RealmId id)
        {
            return Enum.TryParse(value, false, out id) && id != RealmId.None && Enum.IsDefined(typeof(RealmId), id);
        }

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z') return false;
            bool previousUnderscore = false;
            for (int i = 1; i < value.Length; i++)
            {
                char character = value[i];
                bool isLowercaseLetter = character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';
                bool isUnderscore = character == '_';
                if (!isLowercaseLetter && !isDigit && !isUnderscore) return false;
                if (isUnderscore && previousUnderscore) return false;
                previousUnderscore = isUnderscore;
            }
            return !previousUnderscore;
        }

        private static RealmCatalogLoadResult Reject(string code) => new RealmCatalogLoadResult(null, code);

        private static void TryReadVisualIdentity(
            GameDataCatalogRecord realm,
            out string markName,
            out string silhouetteLanguage,
            out string materialLanguage)
        {
            markName = string.Empty;
            silhouetteLanguage = string.Empty;
            materialLanguage = string.Empty;
            GameDataValue raw;
            if (realm == null || !realm.TryGetField("visual_identity", out raw))
            {
                return;
            }

            var visual = raw as GameDataObjectValue;
            if (visual == null)
            {
                return;
            }

            markName = NestedString(visual, "mark_name");
            silhouetteLanguage = NestedString(visual, "silhouette_language");
            materialLanguage = NestedString(visual, "material_language");
        }

        private static string NestedString(GameDataObjectValue obj, string name)
        {
            GameDataValue raw;
            if (obj == null || !obj.TryGetValue(name, out raw))
            {
                return string.Empty;
            }

            var text = raw as GameDataStringValue;
            return text == null || string.IsNullOrEmpty(text.Value) ? string.Empty : text.Value;
        }
    }

    public sealed class RealmCatalogRuntimeHost : MonoBehaviour
    {
        private int _attemptGeneration;

        internal void Initialize(int attemptGeneration)
        {
            _attemptGeneration = attemptGeneration;
        }

        public static string BuildRequestUri(string streamingAssetsRoot)
        {
            if (string.IsNullOrWhiteSpace(streamingAssetsRoot))
            {
                throw new ArgumentException("A StreamingAssets root is required.", nameof(streamingAssetsRoot));
            }

            string relativePath = RealmCatalogRuntime.RelativePath.Replace('\\', '/');
            if (streamingAssetsRoot.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                streamingAssetsRoot.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
            {
                return streamingAssetsRoot.TrimEnd('/', '\\') + "/" + relativePath;
            }

            string filePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(streamingAssetsRoot, RealmCatalogRuntime.RelativePath));
            return new Uri(filePath).AbsoluteUri;
        }

        internal static string BuildEditorRequestUri(string assetsRoot)
        {
            if (string.IsNullOrWhiteSpace(assetsRoot))
            {
                throw new ArgumentException("An Assets root is required.", nameof(assetsRoot));
            }

            string authorityPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    assetsRoot,
                    "AL",
                    "StreamingAssets",
                    RealmCatalogRuntime.RelativePath));
            return new Uri(authorityPath).AbsoluteUri;
        }

        private IEnumerator Start()
        {
            int attemptGeneration = _attemptGeneration > 0
                ? _attemptGeneration
                : RealmCatalogRuntime.CurrentGeneration;
            string path = Application.isEditor
                ? BuildEditorRequestUri(Application.dataPath)
                : BuildRequestUri(Application.streamingAssetsPath);
            var handler = new BoundedRealmCatalogDownloadHandler(RealmCatalogRuntime.MaximumByteLength);
            using (var request = new UnityWebRequest(path, "GET", handler, null))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                RealmCatalogLoadResult result;
                if (handler.IsOversize)
                    result = RealmCatalogRuntime.OversizeFailure();
                else if (request.result == UnityWebRequest.Result.Success)
                    result = RealmCatalogRuntime.Parse(handler.GetUtf8Text());
                else
                    result = RealmCatalogRuntime.ReadFailure();
                RealmCatalogRuntime.Publish(attemptGeneration, result);
            }
            Destroy(gameObject);
        }
    }

    internal sealed class BoundedRealmCatalogDownloadHandler : DownloadHandlerScript
    {
        private readonly byte[] _buffer;
        private int _length;

        internal BoundedRealmCatalogDownloadHandler(int maximumByteLength)
            : base(CreateReceiveBuffer(maximumByteLength))
        {
            _buffer = new byte[maximumByteLength];
        }

        internal bool IsOversize { get; private set; }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0) return true;
            if (dataLength > _buffer.Length - _length)
            {
                IsOversize = true;
                return false;
            }

            Buffer.BlockCopy(data, 0, _buffer, _length, dataLength);
            _length += dataLength;
            return true;
        }

        internal string GetUtf8Text()
        {
            return Encoding.UTF8.GetString(_buffer, 0, _length);
        }

        private static byte[] CreateReceiveBuffer(int maximumByteLength)
        {
            if (maximumByteLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumByteLength));
            return new byte[Math.Min(maximumByteLength, 8192)];
        }
    }

}
