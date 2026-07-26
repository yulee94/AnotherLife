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
                document.selectionPolicy.crossRealmCreationPolicy != "reject")
                return Reject("AL-REALM-CATALOG-POLICY-MISMATCH");
            if (document.realms == null || document.realms.Length != 4 || document.realmOrder == null || document.realmOrder.Length != 4)
                return Reject("AL-REALM-CATALOG-REALM-COUNT");

            var expected = new HashSet<string>(StringComparer.Ordinal) { "crownlands", "stonehold", "eldergrove", "umbral" };
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenRuntime = new HashSet<RealmId>();
            var entries = new List<RealmCatalogEntry>(4);
            for (int i = 0; i < document.realms.Length; i++)
            {
                RealmCatalogRealm realm = document.realms[i];
                RealmId runtimeId;
                if (realm == null || !expected.Contains(realm.id) || !seenIds.Add(realm.id) || string.IsNullOrWhiteSpace(realm.displayName) ||
                    !TryRuntimeId(realm.legacyRuntimeId, out runtimeId) || !seenRuntime.Add(runtimeId) || realm.realmGemIds == null || realm.realmGemIds.Length != 2 ||
                    string.IsNullOrWhiteSpace(realm.realmGemIds[0]) || string.IsNullOrWhiteSpace(realm.realmGemIds[1]))
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

        private static bool TryRuntimeId(string value, out RealmId id)
        {
            return Enum.TryParse(value, false, out id) && id != RealmId.None && Enum.IsDefined(typeof(RealmId), id);
        }
        private static RealmCatalogLoadResult Reject(string code) => new RealmCatalogLoadResult(null, code);

        [Serializable] private sealed class RealmCatalogDocument { public string version; public string catalogId; public RealmCatalogSelectionPolicy selectionPolicy; public string[] realmOrder; public RealmCatalogRealm[] realms; }
        [Serializable] private sealed class RealmCatalogSelectionPolicy { public string selectionMode; public string realmLockScope; public string subCharacterPolicy; public string crossRealmCreationPolicy; }
        [Serializable] private sealed class RealmCatalogRealm { public string id; public string legacyRuntimeId; public string displayName; public string[] realmGemIds; }

    }

    public sealed class RealmCatalogRuntimeHost : MonoBehaviour
    {
        private IEnumerator Start()
        {
            Status = RealmCatalogRuntimeStatus.Loading;
            TechnicalCode = "AL-REALM-CATALOG-LOADING";
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, RelativePath);
            using (UnityWebRequest request = UnityWebRequest.Get(path))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                RealmCatalogLoadResult result = request.result == UnityWebRequest.Result.Success ? Parse(request.downloadHandler.text) : Reject("AL-REALM-CATALOG-READ-FAILED");
                Current = result.Snapshot;
                TechnicalCode = result.TechnicalCode;
                Status = Current == null ? RealmCatalogRuntimeStatus.Unavailable : RealmCatalogRuntimeStatus.Ready;
            }
            Destroy(gameObject);
        }
    }

}