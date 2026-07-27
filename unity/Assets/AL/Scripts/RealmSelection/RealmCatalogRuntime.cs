using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AL.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace AL.RealmSelection
{
    public enum RealmCatalogRuntimeStatus { NotStarted, Loading, Ready, Unavailable }

    public sealed class RealmCatalogEntry
    {
        internal RealmCatalogEntry(string id, RealmId runtimeId, string displayName)
        {
            Id = id;
            RuntimeId = runtimeId;
            DisplayName = displayName;
        }
        public string Id { get; }
        public RealmId RuntimeId { get; }
        public string DisplayName { get; }
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
        public const string RelativePath = "GameData/al_realm_catalog.json";
        public const string SupportedVersion = "0.1.0";
        public const int MaximumByteLength = 32768;

        public static RealmCatalogRuntimeStatus Status { get; private set; }
        public static RealmCatalogSnapshot Current { get; private set; }
        public static string TechnicalCode { get; private set; } = "AL-REALM-CATALOG-NOT-STARTED";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeginLoad()
        {
            if (Status == RealmCatalogRuntimeStatus.Loading || Status == RealmCatalogRuntimeStatus.Ready) return;
            MarkLoading();
            var host = new GameObject("AL.RealmCatalogRuntime");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<RealmCatalogRuntimeHost>();
        }

        public static RealmCatalogLoadResult Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return Reject("AL-REALM-CATALOG-MISSING");
            if (Encoding.UTF8.GetByteCount(json) > MaximumByteLength) return Reject("AL-REALM-CATALOG-OVERSIZE");
            RealmCatalogDocument document;
            try { document = JsonUtility.FromJson<RealmCatalogDocument>(json); }
            catch (Exception) { return Reject("AL-REALM-CATALOG-MALFORMED"); }
            if (document == null || document.catalogId != "al_realm_catalog" || document.version != SupportedVersion)
                return Reject("AL-REALM-CATALOG-UNSUPPORTED");
            if (document.selectionPolicy == null || document.selectionPolicy.selectionMode != "one_realm_per_account" ||
                document.selectionPolicy.realmLockScope != "account" || document.selectionPolicy.subCharacterPolicy != "same_realm_only" ||
                document.selectionPolicy.sharedStoragePolicy != "same_realm_account_storage" ||
                document.selectionPolicy.crossRealmCreationPolicy != "reject" ||
                document.selectionPolicy.realmChangePolicy != "not_supported_after_commit" ||
                document.selectionPolicy.uncommittedProfileState != "realm_unselected" ||
                document.selectionPolicy.committedProfileState != "realm_locked")
                return Reject("AL-REALM-CATALOG-POLICY-MISMATCH");
            if (document.realms == null || document.realms.Length != 4 || document.realmOrder == null || document.realmOrder.Length != 4)
                return Reject("AL-REALM-CATALOG-REALM-COUNT");

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenRuntime = new HashSet<RealmId>();
            var seenGemIds = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<RealmCatalogEntry>(4);
            for (int i = 0; i < document.realms.Length; i++)
            {
                RealmCatalogRealm realm = document.realms[i];
                RealmId expectedRuntimeId;
                RealmId runtimeId;
                if (realm == null || !TryStableRuntimeId(realm.id, out expectedRuntimeId) || !seenIds.Add(realm.id) || string.IsNullOrWhiteSpace(realm.displayName) ||
                    !TryRuntimeId(realm.legacyRuntimeId, out runtimeId) || runtimeId != expectedRuntimeId || !seenRuntime.Add(runtimeId) ||
                    realm.realmGemIds == null || realm.realmGemIds.Length != 2 ||
                    !IsStableId(realm.realmGemIds[0]) || !IsStableId(realm.realmGemIds[1]) ||
                    !seenGemIds.Add(realm.realmGemIds[0]) || !seenGemIds.Add(realm.realmGemIds[1]))
                    return Reject("AL-REALM-CATALOG-INVALID-REALM");
                entries.Add(new RealmCatalogEntry(realm.id, runtimeId, realm.displayName));
            }
            for (int i = 0; i < document.realmOrder.Length; i++)
                if (!seenIds.Contains(document.realmOrder[i])) return Reject("AL-REALM-CATALOG-ORDER-MISMATCH");
            if (new HashSet<string>(document.realmOrder, StringComparer.Ordinal).Count != 4)
                return Reject("AL-REALM-CATALOG-ORDER-MISMATCH");
            entries.Sort((left, right) => Array.IndexOf(document.realmOrder, left.Id).CompareTo(Array.IndexOf(document.realmOrder, right.Id)));
            return new RealmCatalogLoadResult(new RealmCatalogSnapshot(document.version, entries), "AL-REALM-CATALOG-READY");
        }

        internal static void MarkLoading()
        {
            Status = RealmCatalogRuntimeStatus.Loading;
            TechnicalCode = "AL-REALM-CATALOG-LOADING";
        }

        internal static void Publish(RealmCatalogLoadResult result)
        {
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

        [Serializable] private sealed class RealmCatalogDocument { public string version; public string catalogId; public RealmCatalogSelectionPolicy selectionPolicy; public string[] realmOrder; public RealmCatalogRealm[] realms; }
        [Serializable] private sealed class RealmCatalogSelectionPolicy
        {
            public string selectionMode;
            public string realmLockScope;
            public string subCharacterPolicy;
            public string sharedStoragePolicy;
            public string crossRealmCreationPolicy;
            public string realmChangePolicy;
            public string uncommittedProfileState;
            public string committedProfileState;
        }
        [Serializable] private sealed class RealmCatalogRealm { public string id; public string legacyRuntimeId; public string displayName; public string[] realmGemIds; }

    }

    public sealed class RealmCatalogRuntimeHost : MonoBehaviour
    {
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

        private IEnumerator Start()
        {
            string path = BuildRequestUri(Application.streamingAssetsPath);
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
                RealmCatalogRuntime.Publish(result);
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
