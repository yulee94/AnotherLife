using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using AL.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace AL.RealmGems
{
    public enum RealmGemWishgateCatalogRuntimeStatus
    {
        NotStarted,
        Loading,
        Ready,
        Unavailable
    }

    public sealed class RealmGemCatalogEntry
    {
        internal RealmGemCatalogEntry(string id, string realmId, RealmId runtimeRealmId, int gemIndex)
        {
            Id = id;
            RealmId = realmId;
            RuntimeRealmId = runtimeRealmId;
            GemIndex = gemIndex;
        }

        public string Id { get; }
        public string RealmId { get; }
        public RealmId RuntimeRealmId { get; }
        public int GemIndex { get; }
    }

    public sealed class WishgateCatalogEntry
    {
        internal WishgateCatalogEntry(
            string id,
            string entryZoneId,
            int requiredGemCount,
            bool eligibilityAuthorityAvailable,
            bool rewardAuthorityAvailable,
            IEnumerable<string> approvedRewardIds)
        {
            Id = id;
            EntryZoneId = entryZoneId;
            RequiredGemCount = requiredGemCount;
            EligibilityAuthorityAvailable = eligibilityAuthorityAvailable;
            RewardAuthorityAvailable = rewardAuthorityAvailable;
            ApprovedRewardIds = new ReadOnlyCollection<string>((approvedRewardIds ?? Array.Empty<string>()).ToArray());
        }

        public string Id { get; }
        public string EntryZoneId { get; }
        public int RequiredGemCount { get; }
        public bool EligibilityAuthorityAvailable { get; }
        public bool RewardAuthorityAvailable { get; }
        public IReadOnlyList<string> ApprovedRewardIds { get; }
    }

    public sealed class RealmGemWishgateCatalogSnapshot
    {
        private readonly IReadOnlyDictionary<string, RealmGemCatalogEntry> _realmGemsById;
        private readonly HashSet<string> _approvedRewardIds;

        internal RealmGemWishgateCatalogSnapshot(
            string catalogId,
            int contentVersion,
            string authorityId,
            int authorityVersion,
            string sourceCatalogId,
            string sourcePacketId,
            string sourceSha256,
            bool custodyAuthorityAvailable,
            IEnumerable<RealmGemCatalogEntry> realmGems,
            WishgateCatalogEntry wishgate)
        {
            CatalogId = catalogId;
            ContentVersion = contentVersion;
            AuthorityId = authorityId;
            AuthorityVersion = authorityVersion;
            SourceCatalogId = sourceCatalogId;
            SourcePacketId = sourcePacketId;
            SourceSha256 = sourceSha256;
            CustodyAuthorityAvailable = custodyAuthorityAvailable;
            RealmGems = new ReadOnlyCollection<RealmGemCatalogEntry>(
                realmGems.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray());
            _realmGemsById = new ReadOnlyDictionary<string, RealmGemCatalogEntry>(
                RealmGems.ToDictionary(entry => entry.Id, StringComparer.Ordinal));
            Wishgate = wishgate;
            _approvedRewardIds = new HashSet<string>(wishgate.ApprovedRewardIds, StringComparer.Ordinal);
        }

        public string CatalogId { get; }
        public int ContentVersion { get; }
        public string AuthorityId { get; }
        public int AuthorityVersion { get; }
        public string SourceCatalogId { get; }
        public string SourcePacketId { get; }
        public string SourceSha256 { get; }
        public bool CustodyAuthorityAvailable { get; }
        public IReadOnlyList<RealmGemCatalogEntry> RealmGems { get; }
        public WishgateCatalogEntry Wishgate { get; }

        public bool TryGetRealmGem(string id, out RealmGemCatalogEntry entry) =>
            _realmGemsById.TryGetValue(id ?? string.Empty, out entry);

        public bool IsApprovedReward(string rewardId) =>
            Wishgate.RewardAuthorityAvailable &&
            !string.IsNullOrWhiteSpace(rewardId) &&
            _approvedRewardIds.Contains(rewardId);
    }

    public sealed class RealmGemWishgateCatalogLoadResult
    {
        internal RealmGemWishgateCatalogLoadResult(
            RealmGemWishgateCatalogSnapshot snapshot,
            string technicalCode)
        {
            Snapshot = snapshot;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public RealmGemWishgateCatalogSnapshot Snapshot { get; }
        public string TechnicalCode { get; }
        public bool IsSuccess => Snapshot != null;
    }

    /// <summary>
    /// Production activation boundary for the generated Realm Gem/Wishgate runtime artifact.
    /// It deliberately does not parse or reference the narrative authoring catalog at runtime.
    /// </summary>
    public static class RealmGemWishgateRuntimeCatalog
    {
        public const string RelativePath = "GameData/al_realm_gem_wishgate_runtime_catalog.json";
        public const int MaximumByteLength = 16 * 1024;

        private const string ExpectedGameId = "another-life";
        private const string ExpectedCatalogId = "al_realm_gem_wishgate_runtime_catalog";
        private const string ExpectedAuthorityId = "another-life.realm-gem-wishgate.production";
        private const string ExpectedSourceCatalogId = "al_realm_gem_wishgate_content_catalog";
        private const string ExpectedSourcePacketId = "al_narrative_realm_gem_wishgate_source_v001";
        private const string ExpectedSourceSha256 = "899892f08ecb330359586f3d2ba767c193e4e6c9c91f380cb5c35ccec9a057ce";
        private const string ExpectedArtifactSha256 = "4f1c1bf17b4657d6e879cd816cecbf2a63ab65840a732eb377ad2bfecf208da2";
        private const string ExpectedWishgateId = "wishgate_eightfold_concordance";
        private const string ExpectedWishgateZoneId = "zone_accordant_isle";
        private const int SupportedSchemaVersion = 1;
        private const int CurrentContentVersion = 1;
        private const int CurrentAuthorityVersion = 1;

        public static RealmGemWishgateCatalogRuntimeStatus Status { get; private set; }
        public static RealmGemWishgateCatalogSnapshot Current { get; private set; }
        public static int CurrentGeneration { get; private set; }
        public static string TechnicalCode { get; private set; } = "AL-RGW-CATALOG-NOT-STARTED";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            Status = RealmGemWishgateCatalogRuntimeStatus.NotStarted;
            Current = null;
            CurrentGeneration = 0;
            TechnicalCode = "AL-RGW-CATALOG-NOT-STARTED";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeginLoad()
        {
            TryStartAttempt();
        }

        public static bool TryRetry()
        {
            if (Status != RealmGemWishgateCatalogRuntimeStatus.Unavailable &&
                Status != RealmGemWishgateCatalogRuntimeStatus.NotStarted)
            {
                return false;
            }

            return TryStartAttempt();
        }

        public static RealmGemWishgateCatalogLoadResult Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Reject("AL-RGW-CATALOG-MISSING");
            if (Encoding.UTF8.GetByteCount(json) > MaximumByteLength) return Reject("AL-RGW-CATALOG-OVERSIZE");
            if (!string.Equals(ComputeSha256(json), ExpectedArtifactSha256, StringComparison.Ordinal))
                return Reject("AL-RGW-CATALOG-ARTIFACT-HASH");

            return ParseValidatedBytes(json);
        }

        internal static RealmGemWishgateCatalogLoadResult ParseForValidationTests(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Reject("AL-RGW-CATALOG-MISSING");
            if (Encoding.UTF8.GetByteCount(json) > MaximumByteLength) return Reject("AL-RGW-CATALOG-OVERSIZE");
            return ParseValidatedBytes(json);
        }

        private static RealmGemWishgateCatalogLoadResult ParseValidatedBytes(string json)
        {

            RuntimeCatalogDocument document;
            try
            {
                document = JsonUtility.FromJson<RuntimeCatalogDocument>(json);
            }
            catch (Exception)
            {
                return Reject("AL-RGW-CATALOG-MALFORMED");
            }

            if (document == null ||
                document.gameId != ExpectedGameId ||
                document.catalogId != ExpectedCatalogId ||
                document.schemaVersion != SupportedSchemaVersion)
            {
                return Reject("AL-RGW-CATALOG-IDENTITY");
            }

            if (document.contentVersion != CurrentContentVersion)
                return Reject("AL-RGW-CATALOG-VERSION");
            if (document.authorityId != ExpectedAuthorityId ||
                document.authorityVersion != CurrentAuthorityVersion)
            {
                return Reject("AL-RGW-CATALOG-AUTHORITY");
            }

            if (document.sourceCatalogId != ExpectedSourceCatalogId ||
                document.sourcePacketId != ExpectedSourcePacketId ||
                !string.Equals(document.sourceSha256, ExpectedSourceSha256, StringComparison.Ordinal))
            {
                return Reject("AL-RGW-CATALOG-SOURCE");
            }

            if (!document.custodyAuthorityAvailable || document.realmGems == null || document.realmGems.Length != 8)
                return Reject("AL-RGW-CATALOG-GEM");

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenSlots = new HashSet<string>(StringComparer.Ordinal);
            var countsByRealm = new Dictionary<RealmId, int>();
            var entries = new List<RealmGemCatalogEntry>(8);
            foreach (RuntimeRealmGemDocument gem in document.realmGems)
            {
                RealmId runtimeRealmId;
                string expectedStableRealmId;
                if (gem == null ||
                    !IsStableId(gem.id) ||
                    !seenIds.Add(gem.id) ||
                    !TryRuntimeRealmId(gem.runtimeRealmId, out runtimeRealmId) ||
                    !TryStableRealmId(runtimeRealmId, out expectedStableRealmId) ||
                    gem.realmId != expectedStableRealmId ||
                    gem.gemIndex < 1 || gem.gemIndex > 2 ||
                    !seenSlots.Add(gem.realmId + ":" + gem.gemIndex))
                {
                    return Reject("AL-RGW-CATALOG-GEM");
                }

                countsByRealm[runtimeRealmId] = countsByRealm.TryGetValue(runtimeRealmId, out int count)
                    ? count + 1
                    : 1;
                entries.Add(new RealmGemCatalogEntry(gem.id, gem.realmId, runtimeRealmId, gem.gemIndex));
            }

            RealmId[] requiredRealms =
            {
                RealmId.Crownlands,
                RealmId.Stonehold,
                RealmId.Eldergrove,
                RealmId.Umbral
            };
            if (requiredRealms.Any(realm => !countsByRealm.TryGetValue(realm, out int count) || count != 2))
                return Reject("AL-RGW-CATALOG-GEM");

            RuntimeWishgateDocument wishgate = document.wishgate;
            if (wishgate == null ||
                wishgate.id != ExpectedWishgateId ||
                wishgate.entryZoneId != ExpectedWishgateZoneId ||
                wishgate.requiredGemCount != 8 ||
                wishgate.approvedRewardIds == null ||
                wishgate.approvedRewardIds.Any(id => !IsStableId(id)) ||
                wishgate.approvedRewardIds.Distinct(StringComparer.Ordinal).Count() != wishgate.approvedRewardIds.Length ||
                (wishgate.rewardAuthorityAvailable && wishgate.approvedRewardIds.Length == 0))
            {
                return Reject("AL-RGW-CATALOG-WISHGATE");
            }

            var snapshot = new RealmGemWishgateCatalogSnapshot(
                document.catalogId,
                document.contentVersion,
                document.authorityId,
                document.authorityVersion,
                document.sourceCatalogId,
                document.sourcePacketId,
                document.sourceSha256,
                document.custodyAuthorityAvailable,
                entries,
                new WishgateCatalogEntry(
                    wishgate.id,
                    wishgate.entryZoneId,
                    wishgate.requiredGemCount,
                    wishgate.eligibilityAuthorityAvailable,
                    wishgate.rewardAuthorityAvailable,
                    wishgate.approvedRewardIds));
            return new RealmGemWishgateCatalogLoadResult(snapshot, "AL-RGW-CATALOG-READY");
        }

        internal static void ResetForTests()
        {
            ResetRuntimeState();
        }

        internal static void MarkLoadingForTests(int generation)
        {
            CurrentGeneration = generation;
            MarkLoading();
        }

        internal static void PublishForTests(int generation, RealmGemWishgateCatalogLoadResult result)
        {
            Publish(generation, result);
        }

        internal static RealmGemWishgateCatalogLoadResult ReadFailure() =>
            Reject("AL-RGW-CATALOG-READ-FAILED");

        internal static RealmGemWishgateCatalogLoadResult OversizeFailure() =>
            Reject("AL-RGW-CATALOG-OVERSIZE");

        internal static void Publish(int generation, RealmGemWishgateCatalogLoadResult result)
        {
            if (generation != CurrentGeneration ||
                Status != RealmGemWishgateCatalogRuntimeStatus.Loading ||
                result == null)
            {
                return;
            }

            Current = result.Snapshot;
            TechnicalCode = result.TechnicalCode;
            Status = Current == null
                ? RealmGemWishgateCatalogRuntimeStatus.Unavailable
                : RealmGemWishgateCatalogRuntimeStatus.Ready;
        }

        private static bool TryStartAttempt()
        {
            if (Status == RealmGemWishgateCatalogRuntimeStatus.Loading ||
                Status == RealmGemWishgateCatalogRuntimeStatus.Ready)
            {
                return false;
            }

            if (CurrentGeneration == int.MaxValue)
            {
                Current = null;
                Status = RealmGemWishgateCatalogRuntimeStatus.Unavailable;
                TechnicalCode = "AL-RGW-CATALOG-GENERATION-EXHAUSTED";
                return false;
            }

            CurrentGeneration++;
            MarkLoading();
            var host = new GameObject("AL.RealmGemWishgateRuntimeCatalog");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<RealmGemWishgateRuntimeCatalogHost>().Initialize(CurrentGeneration);
            return true;
        }

        private static void MarkLoading()
        {
            Current = null;
            Status = RealmGemWishgateCatalogRuntimeStatus.Loading;
            TechnicalCode = "AL-RGW-CATALOG-LOADING";
        }

        private static bool TryRuntimeRealmId(string value, out RealmId id) =>
            TryNamedRuntimeRealmId(value, out id);

        private static bool TryNamedRuntimeRealmId(string value, out RealmId id)
        {
            switch (value)
            {
                case "Crownlands": id = RealmId.Crownlands; return true;
                case "Stonehold": id = RealmId.Stonehold; return true;
                case "Eldergrove": id = RealmId.Eldergrove; return true;
                case "Umbral": id = RealmId.Umbral; return true;
                default: id = RealmId.None; return false;
            }
        }

        private static bool TryStableRealmId(RealmId id, out string stableId)
        {
            switch (id)
            {
                case RealmId.Crownlands: stableId = "crownlands"; return true;
                case RealmId.Stonehold: stableId = "stonehold"; return true;
                case RealmId.Eldergrove: stableId = "eldergrove"; return true;
                case RealmId.Umbral: stableId = "umbral"; return true;
                default: stableId = string.Empty; return false;
            }
        }

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z') return false;
            bool previousUnderscore = false;
            for (int i = 1; i < value.Length; i++)
            {
                char character = value[i];
                bool valid = (character >= 'a' && character <= 'z') ||
                             (character >= '0' && character <= '9') ||
                             character == '_';
                if (!valid || (character == '_' && previousUnderscore)) return false;
                previousUnderscore = character == '_';
            }

            return !previousUnderscore;
        }

        private static RealmGemWishgateCatalogLoadResult Reject(string code) =>
            new RealmGemWishgateCatalogLoadResult(null, code);

        private static string ComputeSha256(string text)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                var builder = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        [Serializable]
        private sealed class RuntimeCatalogDocument
        {
            public string gameId;
            public string catalogId;
            public int schemaVersion;
            public int contentVersion;
            public string authorityId;
            public int authorityVersion;
            public string sourceCatalogId;
            public string sourcePacketId;
            public string sourceSha256;
            public bool custodyAuthorityAvailable;
            public RuntimeRealmGemDocument[] realmGems;
            public RuntimeWishgateDocument wishgate;
        }

        [Serializable]
        private sealed class RuntimeRealmGemDocument
        {
            public string id;
            public string realmId;
            public string runtimeRealmId;
            public int gemIndex;
        }

        [Serializable]
        private sealed class RuntimeWishgateDocument
        {
            public string id;
            public string entryZoneId;
            public int requiredGemCount;
            public bool eligibilityAuthorityAvailable;
            public bool rewardAuthorityAvailable;
            public string[] approvedRewardIds;
        }
    }

    internal sealed class RealmGemWishgateRuntimeCatalogHost : MonoBehaviour
    {
        private int _generation;

        internal void Initialize(int generation)
        {
            _generation = generation;
        }

        public static string BuildRequestUri(string streamingAssetsRoot)
        {
            if (string.IsNullOrWhiteSpace(streamingAssetsRoot))
                throw new ArgumentException("A StreamingAssets root is required.", nameof(streamingAssetsRoot));

            string relativePath = RealmGemWishgateRuntimeCatalog.RelativePath.Replace('\\', '/');
            if (streamingAssetsRoot.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                streamingAssetsRoot.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
            {
                return streamingAssetsRoot.TrimEnd('/', '\\') + "/" + relativePath;
            }

            string path = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(streamingAssetsRoot, RealmGemWishgateRuntimeCatalog.RelativePath));
            return new Uri(path).AbsoluteUri;
        }

        internal static string BuildEditorRequestUri(string assetsRoot)
        {
            string path = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    assetsRoot,
                    "StreamingAssets",
                    RealmGemWishgateRuntimeCatalog.RelativePath));
            return new Uri(path).AbsoluteUri;
        }

        private IEnumerator Start()
        {
            if (_generation <= 0)
            {
                Destroy(gameObject);
                yield break;
            }

            RealmGemWishgateCatalogLoadResult result = null;
            try
            {
                string uri = Application.isEditor
                    ? BuildEditorRequestUri(Application.dataPath)
                    : BuildRequestUri(Application.streamingAssetsPath);
                var handler = new BoundedRealmGemWishgateDownloadHandler(
                    RealmGemWishgateRuntimeCatalog.MaximumByteLength);
                using (var request = new UnityWebRequest(uri, "GET", handler, null))
                {
                    request.timeout = 10;
                    yield return request.SendWebRequest();
                    result = handler.IsOversize
                        ? RealmGemWishgateRuntimeCatalog.OversizeFailure()
                        : request.result == UnityWebRequest.Result.Success
                            ? RealmGemWishgateRuntimeCatalog.Parse(handler.GetUtf8Text())
                            : RealmGemWishgateRuntimeCatalog.ReadFailure();
                }
            }
            finally
            {
                RealmGemWishgateRuntimeCatalog.Publish(
                    _generation,
                    result ?? RealmGemWishgateRuntimeCatalog.ReadFailure());
            }

            Destroy(gameObject);
        }
    }

    internal sealed class BoundedRealmGemWishgateDownloadHandler : DownloadHandlerScript
    {
        private readonly byte[] _buffer;
        private int _length;

        internal BoundedRealmGemWishgateDownloadHandler(int maximumByteLength)
            : base(new byte[Math.Min(maximumByteLength, 8192)])
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

        internal string GetUtf8Text() => Encoding.UTF8.GetString(_buffer, 0, _length);
    }
}
