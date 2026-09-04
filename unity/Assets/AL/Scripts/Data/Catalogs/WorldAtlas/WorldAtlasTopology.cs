using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;

namespace AL.Data.Catalogs.WorldAtlas
{
    public static class WorldAtlasContract
    {
        public const string SupportedVersion = "0.3.0";
        public const string CatalogId = "al_world_atlas_narrative_catalog";
        public const string FileName = CatalogId + ".json";
        public const string SourcePacketId = "al_narrative_world_atlas_source_v003";
        public const string TopologyContractId = "al_world_atlas_topology_query_contract_v001";
        public const string ProtectedZoneContractId = "al_world_atlas_protected_zone_query_contract_v001";
        public const int MaximumBytes = 64 * 1024;
        public const int MaximumDiagnostics = 128;
    }

    public enum WorldAtlasLoadStatus { Accepted, Rejected, UnsupportedVersion }
    public enum WorldAtlasQueryStatus { Found, UnknownId, InvalidId, PlacementUnresolved }

    public sealed class WorldAtlasDiagnostic
    {
        public WorldAtlasDiagnostic(string code, string path, string relatedId, string message)
        {
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            RelatedId = relatedId ?? string.Empty;
            Message = message ?? string.Empty;
        }
        public string Code { get; }
        public string Path { get; }
        public string RelatedId { get; }
        public string Message { get; }
        public string Fingerprint => string.Join("|", Code, Path, RelatedId, Message);
    }

    public sealed class WorldAtlasLoadResult
    {
        internal WorldAtlasLoadResult(WorldAtlasLoadStatus status, WorldAtlasSnapshot snapshot, IList<WorldAtlasDiagnostic> diagnostics)
        {
            Status = status;
            Snapshot = snapshot;
            Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<WorldAtlasDiagnostic>()).ToArray());
        }
        public WorldAtlasLoadStatus Status { get; }
        public WorldAtlasSnapshot Snapshot { get; }
        public IReadOnlyList<WorldAtlasDiagnostic> Diagnostics { get; }
        public bool IsAccepted => Status == WorldAtlasLoadStatus.Accepted && Snapshot != null;
    }

    public sealed class WorldAtlasQueryResult<T> where T : class
    {
        internal WorldAtlasQueryResult(WorldAtlasQueryStatus status, T value, string diagnosticCode)
        {
            Status = status;
            Value = value;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }
        public WorldAtlasQueryStatus Status { get; }
        public T Value { get; }
        public string DiagnosticCode { get; }
    }

    public sealed class WorldAtlasNode
    {
        internal WorldAtlasNode(string id, string role, string assignmentStatus, string atlasZoneId)
        { Id = id; Role = role; RealmAssignmentStatus = assignmentStatus; AtlasZoneId = atlasZoneId; }
        public string Id { get; }
        public string Role { get; }
        public string RealmAssignmentStatus { get; }
        public string AtlasZoneId { get; }
    }

    public sealed class WorldAtlasAdjacency
    {
        internal WorldAtlasAdjacency(string id, string nodeAId, string nodeBId)
        { Id = id; NodeAId = nodeAId; NodeBId = nodeBId; }
        public string Id { get; }
        public string NodeAId { get; }
        public string NodeBId { get; }
    }

    public sealed class WorldAtlasBridge
    {
        internal WorldAtlasBridge(string id, string connectionType, string nodeAId, string endpointAId, string nodeBId, string endpointBId, string hookStatus)
        { Id = id; ConnectionType = connectionType; NodeAId = nodeAId; EndpointAId = endpointAId; NodeBId = nodeBId; EndpointBId = endpointBId; HookStatus = hookStatus; }
        public string Id { get; }
        public string ConnectionType { get; }
        public string NodeAId { get; }
        public string EndpointAId { get; }
        public string NodeBId { get; }
        public string EndpointBId { get; }
        public string HookStatus { get; }
    }

    public sealed class WorldAtlasEndpoint
    {
        internal WorldAtlasEndpoint(string id, string bridgeId, string nodeId)
        { Id = id; BridgeId = bridgeId; NodeId = nodeId; }
        public string Id { get; }
        public string BridgeId { get; }
        public string NodeId { get; }
    }

    public sealed class WorldAtlasBoundary
    {
        internal WorldAtlasBoundary(string id, string realmId, string innerRealmId, string innerAtlasZoneId, string innerWallId, string transitionZoneId, string mainGateId, string outerWallId, string outerWarzoneId, string outerAtlasZoneId, IList<string> stages)
        {
            Id = id; RealmId = realmId; InnerRealmId = innerRealmId; InnerAtlasZoneId = innerAtlasZoneId;
            InnerWallId = innerWallId; TransitionZoneId = transitionZoneId; MainGateId = mainGateId;
            OuterWallId = outerWallId; OuterWarzoneId = outerWarzoneId; OuterAtlasZoneId = outerAtlasZoneId;
            OrderedStages = Array.AsReadOnly((stages ?? Array.Empty<string>()).ToArray());
        }
        public string Id { get; }
        public string RealmId { get; }
        public string InnerRealmId { get; }
        public string InnerAtlasZoneId { get; }
        public string InnerWallId { get; }
        public string TransitionZoneId { get; }
        public string MainGateId { get; }
        public string OuterWallId { get; }
        public string OuterWarzoneId { get; }
        public string OuterAtlasZoneId { get; }
        public IReadOnlyList<string> OrderedStages { get; }
    }

    public sealed class WorldAtlasZone
    {
        internal WorldAtlasZone(string id, string realmId, string displayNameKey, string summaryKey, string zoneType, string visibility, string sceneReferenceStatus)
        { Id = id; RealmId = realmId; DisplayNameKey = displayNameKey; SummaryKey = summaryKey; ZoneType = zoneType; Visibility = visibility; SceneReferenceStatus = sceneReferenceStatus; }
        public string Id { get; }
        public string RealmId { get; }
        public string DisplayNameKey { get; }
        public string SummaryKey { get; }
        public string ZoneType { get; }
        public string Visibility { get; }
        public string SceneReferenceStatus { get; }
    }

    public sealed class WorldAtlasProtectedZonePolicy
    {
        internal WorldAtlasProtectedZonePolicy(
            string id,
            string zoneKind,
            string protection,
            string appliesTo,
            string applicationRecheck,
            string warOverride,
            string enforcementStatus,
            string mutationAuthority)
        {
            Id = id;
            ZoneKind = zoneKind;
            Protection = protection;
            AppliesTo = appliesTo;
            ApplicationRecheck = applicationRecheck;
            WarOverride = warOverride;
            EnforcementStatus = enforcementStatus;
            MutationAuthority = mutationAuthority;
        }

        public string Id { get; }
        public string ZoneKind { get; }
        public string Protection { get; }
        public string AppliesTo { get; }
        public string ApplicationRecheck { get; }
        public string WarOverride { get; }
        public string EnforcementStatus { get; }
        public string MutationAuthority { get; }
    }

    public sealed class WorldAtlasProtectedSubzone
    {
        internal WorldAtlasProtectedSubzone(
            string id,
            string realmId,
            string parentAtlasZoneId,
            string zoneKind,
            string policyId,
            string sceneReferenceStatus,
            string boundaryStatus,
            string mutationAuthority)
        {
            Id = id;
            RealmId = realmId;
            ParentAtlasZoneId = parentAtlasZoneId;
            ZoneKind = zoneKind;
            PolicyId = policyId;
            SceneReferenceStatus = sceneReferenceStatus;
            BoundaryStatus = boundaryStatus;
            MutationAuthority = mutationAuthority;
        }

        public string Id { get; }
        public string RealmId { get; }
        public string ParentAtlasZoneId { get; }
        public string ZoneKind { get; }
        public string PolicyId { get; }
        public string SceneReferenceStatus { get; }
        public string BoundaryStatus { get; }
        public string MutationAuthority { get; }
    }

    public sealed class WorldAtlasObjective
    {
        internal WorldAtlasObjective(
            string id,
            string displayNameKey,
            string summaryKey,
            IList<string> requiredZoneTypes,
            IList<string> requiredZoneIds,
            string hookStatus)
        {
            Id = id;
            DisplayNameKey = displayNameKey;
            SummaryKey = summaryKey;
            RequiredZoneTypes = Array.AsReadOnly((requiredZoneTypes ?? Array.Empty<string>()).ToArray());
            RequiredZoneIds = Array.AsReadOnly((requiredZoneIds ?? Array.Empty<string>()).ToArray());
            HookStatus = hookStatus;
        }
        public string Id { get; }
        public string DisplayNameKey { get; }
        public string SummaryKey { get; }
        public IReadOnlyList<string> RequiredZoneTypes { get; }
        public IReadOnlyList<string> RequiredZoneIds { get; }
        public string HookStatus { get; }
    }

    public sealed class WorldAtlasSnapshot
    {
        internal WorldAtlasSnapshot(string version, string topologyId, string sourceSha256, bool placementResolved,
            IList<WorldAtlasNode> nodes, IList<WorldAtlasAdjacency> adjacencies, IList<WorldAtlasBridge> bridges,
            IList<WorldAtlasEndpoint> endpoints, IList<WorldAtlasBoundary> boundaries, IList<WorldAtlasZone> zones,
            IList<WorldAtlasObjective> objectives, IList<WorldAtlasProtectedZonePolicy> protectedZonePolicies,
            IList<WorldAtlasProtectedSubzone> protectedSubzones)
        {
            Version = version; TopologyId = topologyId; SourceSha256 = sourceSha256; PlacementResolved = placementResolved;
            Nodes = Frozen(nodes); Adjacencies = Frozen(adjacencies); Bridges = Frozen(bridges); Endpoints = Frozen(endpoints);
            Boundaries = Frozen(boundaries); Zones = Frozen(zones); Objectives = Frozen(objectives);
            ProtectedZonePolicies = Frozen(protectedZonePolicies); ProtectedSubzones = Frozen(protectedSubzones);
        }
        public string Version { get; }
        public string TopologyId { get; }
        public string SourceSha256 { get; }
        public bool PlacementResolved { get; }
        public IReadOnlyList<WorldAtlasNode> Nodes { get; }
        public IReadOnlyList<WorldAtlasAdjacency> Adjacencies { get; }
        public IReadOnlyList<WorldAtlasBridge> Bridges { get; }
        public IReadOnlyList<WorldAtlasEndpoint> Endpoints { get; }
        public IReadOnlyList<WorldAtlasBoundary> Boundaries { get; }
        public IReadOnlyList<WorldAtlasZone> Zones { get; }
        public IReadOnlyList<WorldAtlasObjective> Objectives { get; }
        public IReadOnlyList<WorldAtlasProtectedZonePolicy> ProtectedZonePolicies { get; }
        public IReadOnlyList<WorldAtlasProtectedSubzone> ProtectedSubzones { get; }
        private static IReadOnlyList<T> Frozen<T>(IList<T> values) => Array.AsReadOnly((values ?? Array.Empty<T>()).ToArray());
    }

    public sealed class WorldAtlasTopologyQuery
    {
        private readonly WorldAtlasSnapshot snapshot;
        private readonly IReadOnlyDictionary<string, WorldAtlasNode> nodes;
        private readonly IReadOnlyDictionary<string, WorldAtlasBridge> bridges;
        private readonly IReadOnlyDictionary<string, WorldAtlasBoundary> boundaries;
        private readonly IReadOnlyDictionary<string, WorldAtlasZone> zones;
        private readonly IReadOnlyDictionary<string, WorldAtlasProtectedZonePolicy> protectedZonePolicies;
        private readonly IReadOnlyDictionary<string, WorldAtlasProtectedSubzone> protectedSubzones;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<WorldAtlasProtectedSubzone>> protectedSubzonesByRealm;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<WorldAtlasBridge>> bridgesByNode;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> neighborsByNode;

        public WorldAtlasTopologyQuery(WorldAtlasSnapshot snapshot)
        {
            this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            nodes = Index(snapshot.Nodes, value => value.Id);
            bridges = Index(snapshot.Bridges, value => value.Id);
            boundaries = Index(snapshot.Boundaries, value => value.RealmId);
            zones = Index(snapshot.Zones, value => value.Id);
            protectedZonePolicies = Index(snapshot.ProtectedZonePolicies, value => value.Id);
            protectedSubzones = Index(snapshot.ProtectedSubzones, value => value.Id);
            protectedSubzonesByRealm = new ReadOnlyDictionary<string, IReadOnlyList<WorldAtlasProtectedSubzone>>(
                snapshot.ProtectedSubzones
                    .GroupBy(value => value.RealmId, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<WorldAtlasProtectedSubzone>)Array.AsReadOnly(group.ToArray()),
                        StringComparer.Ordinal));
            var bridgeMap = new Dictionary<string, List<WorldAtlasBridge>>(StringComparer.Ordinal);
            var neighborMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (WorldAtlasNode node in snapshot.Nodes) { bridgeMap[node.Id] = new List<WorldAtlasBridge>(); neighborMap[node.Id] = new List<string>(); }
            foreach (WorldAtlasBridge bridge in snapshot.Bridges)
            {
                bridgeMap[bridge.NodeAId].Add(bridge); bridgeMap[bridge.NodeBId].Add(bridge);
                AddUnique(neighborMap[bridge.NodeAId], bridge.NodeBId); AddUnique(neighborMap[bridge.NodeBId], bridge.NodeAId);
            }
            bridgesByNode = new ReadOnlyDictionary<string, IReadOnlyList<WorldAtlasBridge>>(bridgeMap.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<WorldAtlasBridge>)Array.AsReadOnly(pair.Value.ToArray()), StringComparer.Ordinal));
            neighborsByNode = new ReadOnlyDictionary<string, IReadOnlyList<string>>(neighborMap.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()), StringComparer.Ordinal));
        }

        public bool TryGetNode(string id, out WorldAtlasNode value) => TryGet(nodes, id, out value);
        public bool TryGetBridge(string id, out WorldAtlasBridge value) => TryGet(bridges, id, out value);
        public bool TryGetBoundary(string realmId, out WorldAtlasBoundary value) => TryGet(boundaries, realmId, out value);
        public bool TryGetZone(string id, out WorldAtlasZone value) => TryGet(zones, id, out value);
        public WorldAtlasQueryResult<WorldAtlasProtectedZonePolicy> GetProtectedZonePolicy(string id) =>
            Get(protectedZonePolicies, id);
        public WorldAtlasQueryResult<WorldAtlasProtectedSubzone> GetProtectedSubzone(string id) =>
            Get(protectedSubzones, id);
        public WorldAtlasQueryResult<IReadOnlyList<WorldAtlasProtectedSubzone>> GetProtectedSubzonesForRealm(string realmId)
        {
            if (!ValidId(realmId))
                return new WorldAtlasQueryResult<IReadOnlyList<WorldAtlasProtectedSubzone>>(WorldAtlasQueryStatus.InvalidId, null, "AL-ATLAS-ID-INVALID");
            return protectedSubzonesByRealm.TryGetValue(realmId, out var values)
                ? new WorldAtlasQueryResult<IReadOnlyList<WorldAtlasProtectedSubzone>>(WorldAtlasQueryStatus.Found, values, string.Empty)
                : new WorldAtlasQueryResult<IReadOnlyList<WorldAtlasProtectedSubzone>>(WorldAtlasQueryStatus.UnknownId, null, "AL-ATLAS-ID-UNKNOWN");
        }
        public IReadOnlyList<WorldAtlasBridge> GetBridgesForNode(string nodeId) => ValidId(nodeId) && bridgesByNode.TryGetValue(nodeId, out var values) ? values : Array.Empty<WorldAtlasBridge>();
        public IReadOnlyList<string> GetNeighborNodeIds(string nodeId) => ValidId(nodeId) && neighborsByNode.TryGetValue(nodeId, out var values) ? values : Array.Empty<string>();
        public WorldAtlasQueryResult<WorldAtlasNode> GetNodeForRealm(string realmId)
        {
            if (!ValidId(realmId)) return new WorldAtlasQueryResult<WorldAtlasNode>(WorldAtlasQueryStatus.InvalidId, null, "AL-ATLAS-VIEWER-INVALID");
            if (!snapshot.PlacementResolved) return new WorldAtlasQueryResult<WorldAtlasNode>(WorldAtlasQueryStatus.PlacementUnresolved, null, "AL-ATLAS-REALM-PLACEMENT-UNRESOLVED");
            return new WorldAtlasQueryResult<WorldAtlasNode>(WorldAtlasQueryStatus.UnknownId, null, "AL-ATLAS-ID-UNKNOWN");
        }
        private static bool TryGet<T>(IReadOnlyDictionary<string, T> index, string id, out T value) where T : class
        { if (ValidId(id)) return index.TryGetValue(id, out value); value = null; return false; }
        private static WorldAtlasQueryResult<T> Get<T>(IReadOnlyDictionary<string, T> index, string id) where T : class
        {
            if (!ValidId(id)) return new WorldAtlasQueryResult<T>(WorldAtlasQueryStatus.InvalidId, null, "AL-ATLAS-ID-INVALID");
            return index.TryGetValue(id, out var value)
                ? new WorldAtlasQueryResult<T>(WorldAtlasQueryStatus.Found, value, string.Empty)
                : new WorldAtlasQueryResult<T>(WorldAtlasQueryStatus.UnknownId, null, "AL-ATLAS-ID-UNKNOWN");
        }
        private static IReadOnlyDictionary<string, T> Index<T>(IEnumerable<T> values, Func<T, string> key) => new ReadOnlyDictionary<string, T>(values.ToDictionary(key, StringComparer.Ordinal));
        private static void AddUnique(ICollection<string> values, string value) { if (!values.Contains(value)) values.Add(value); }
        internal static bool ValidId(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z') return false;
            bool underscore = false;
            for (int i = 1; i < value.Length; i++)
            {
                char c = value[i]; bool valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!valid || (c == '_' && underscore)) return false; underscore = c == '_';
            }
            return !underscore;
        }
    }

    public static class WorldAtlasTopologyLoader
    {
        private static readonly string[] RealmOrder = { "crownlands", "stonehold", "eldergrove", "umbral" };
        private static readonly string[] BoundaryStages = { "protected_inner_safe_zone", "inner_wall", "controlled_main_gate_transition", "outer_wall", "outer_warzone" };
        private static readonly string[] ProtectedZoneKindOrder = { "city", "beginner", "town" };
        private static readonly string[] ProtectedZonePolicyOrder =
        {
            "zone_policy_city_safe_v001",
            "zone_policy_beginner_safe_v001",
            "zone_policy_town_safe_v001"
        };

        public static WorldAtlasLoadResult Validate(byte[] bytes)
        {
            var diagnostics = new List<WorldAtlasDiagnostic>();
            StrictJsonObject root;
            try { root = StrictJsonDocument.Parse(bytes, WorldAtlasContract.MaximumBytes) as StrictJsonObject; }
            catch (StrictJsonException error)
            { return Reject(WorldAtlasLoadStatus.Rejected, diagnostics, "AL-ATLAS-SCHEMA-INVALID", error.Path, string.Empty, error.Code); }
            catch (Exception)
            { return Reject(WorldAtlasLoadStatus.Rejected, diagnostics, "AL-ATLAS-SCHEMA-INVALID", "$", string.Empty, "parse_failed"); }
            if (root == null) return Reject(WorldAtlasLoadStatus.Rejected, diagnostics, "AL-ATLAS-SCHEMA-INVALID", "$", string.Empty, "root_not_object");

            string version = String(root, "version", "$", diagnostics);
            if (!string.Equals(version, WorldAtlasContract.SupportedVersion, StringComparison.Ordinal))
                return Reject(WorldAtlasLoadStatus.UnsupportedVersion, diagnostics, "AL-ATLAS-VERSION-UNSUPPORTED", "$.version", version, "unsupported version");
            RequireEqual(root, "catalogId", WorldAtlasContract.CatalogId, "$", diagnostics);
            RequireEqual(root, "sourcePacketId", WorldAtlasContract.SourcePacketId, "$", diagnostics);
            RequireEqual(root, "idFormat", "lowercase_snake_case", "$", diagnostics);
            StrictJsonObject authorities = Object(root, "sourceAuthorities", "$", diagnostics);
            if (authorities != null)
            {
                StrictJsonObject contract = Object(authorities, "topologyContract", "$.sourceAuthorities", diagnostics);
                if (contract != null) RequireEqual(contract, "id", WorldAtlasContract.TopologyContractId, "$.sourceAuthorities.topologyContract", diagnostics);
                StrictJsonObject protectedZoneContract = Object(authorities, "protectedZoneContract", "$.sourceAuthorities", diagnostics);
                if (protectedZoneContract != null) RequireEqual(protectedZoneContract, "id", WorldAtlasContract.ProtectedZoneContractId, "$.sourceAuthorities.protectedZoneContract", diagnostics);
            }
            StrictJsonObject topology = Object(root, "abstractTopology", "$", diagnostics);
            var nodes = ParseNodes(topology, diagnostics);
            var adjacencies = ParseAdjacencies(topology, diagnostics);
            var bridges = ParseBridges(topology, diagnostics);
            var endpoints = ParseEndpoints(topology, diagnostics);
            string topologyId = topology == null ? string.Empty : String(topology, "topologyId", "$.abstractTopology", diagnostics);
            bool placementResolved = ParsePlacement(topology, diagnostics);
            var transitions = ParseSimpleRefs(root, "transitionZones", "realmId", "mainGateId", 4, diagnostics);
            var walls = ParseWalls(root, diagnostics);
            var boundaries = ParseBoundaries(root, diagnostics);
            var zones = ParseZones(root, diagnostics);
            var objectives = ParseObjectives(root, diagnostics);
            var protectedZonePolicies = ParseProtectedZonePolicies(root, diagnostics);
            var protectedSubzones = ParseProtectedSubzones(root, diagnostics);

            ValidateTopology(nodes, adjacencies, bridges, endpoints, diagnostics);
            ValidateBoundaries(transitions, walls, boundaries, zones, diagnostics);
            ValidateObjectives(objectives, zones, diagnostics);
            ValidateProtectedZoneAuthority(protectedZonePolicies, protectedSubzones, zones, diagnostics);
            ValidateGlobalIds(nodes, adjacencies, bridges, endpoints, transitions.Keys, walls.Keys, boundaries, zones, objectives, protectedZonePolicies, protectedSubzones, diagnostics);
            SortDiagnostics(diagnostics);
            if (diagnostics.Count != 0) return new WorldAtlasLoadResult(WorldAtlasLoadStatus.Rejected, null, diagnostics.Take(WorldAtlasContract.MaximumDiagnostics).ToArray());
            string hash;
            using (SHA256 sha = SHA256.Create()) hash = string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
            return new WorldAtlasLoadResult(WorldAtlasLoadStatus.Accepted,
                new WorldAtlasSnapshot(version, topologyId, hash, placementResolved, nodes, adjacencies, bridges, endpoints, boundaries, zones, objectives, protectedZonePolicies, protectedSubzones), diagnostics);
        }

        private static List<WorldAtlasNode> ParseNodes(StrictJsonObject topology, List<WorldAtlasDiagnostic> d)
        {
            var result = new List<WorldAtlasNode>(); var array = Array(topology, "nodes", "$.abstractTopology", d);
            ParseObjects(array, "$.abstractTopology.nodes", d, (o, p) => result.Add(new WorldAtlasNode(String(o,"id",p,d), String(o,"role",p,d), OptionalString(o,"realmAssignmentStatus"), OptionalString(o,"atlasZoneId"))));
            Count(result.Count, 5, "$.abstractTopology.nodes", d); return result;
        }
        private static List<WorldAtlasAdjacency> ParseAdjacencies(StrictJsonObject topology, List<WorldAtlasDiagnostic> d)
        {
            var result = new List<WorldAtlasAdjacency>(); var array = Array(topology, "adjacency", "$.abstractTopology", d);
            ParseObjects(array, "$.abstractTopology.adjacency", d, (o,p) => result.Add(new WorldAtlasAdjacency(String(o,"id",p,d),String(o,"nodeAId",p,d),String(o,"nodeBId",p,d))));
            Count(result.Count,4,"$.abstractTopology.adjacency",d); return result;
        }
        private static List<WorldAtlasBridge> ParseBridges(StrictJsonObject topology, List<WorldAtlasDiagnostic> d)
        {
            var result = new List<WorldAtlasBridge>(); var array = Array(topology,"bridges","$.abstractTopology",d);
            ParseObjects(array,"$.abstractTopology.bridges",d,(o,p)=>result.Add(new WorldAtlasBridge(String(o,"id",p,d),String(o,"connectionType",p,d),String(o,"nodeAId",p,d),String(o,"endpointAId",p,d),String(o,"nodeBId",p,d),String(o,"endpointBId",p,d),String(o,"hookStatus",p,d))));
            Count(result.Count,12,"$.abstractTopology.bridges",d); return result;
        }
        private static List<WorldAtlasEndpoint> ParseEndpoints(StrictJsonObject topology, List<WorldAtlasDiagnostic> d)
        {
            var result = new List<WorldAtlasEndpoint>(); var array=Array(topology,"endpoints","$.abstractTopology",d);
            ParseObjects(array,"$.abstractTopology.endpoints",d,(o,p)=>result.Add(new WorldAtlasEndpoint(String(o,"id",p,d),String(o,"bridgeId",p,d),String(o,"nodeId",p,d))));
            Count(result.Count,24,"$.abstractTopology.endpoints",d); return result;
        }
        private static bool ParsePlacement(StrictJsonObject topology, List<WorldAtlasDiagnostic> d)
        {
            StrictJsonObject placement=Object(topology,"placement","$.abstractTopology",d); if(placement==null)return false;
            string status=String(placement,"status","$.abstractTopology.placement",d); StrictJsonArray assignments=Array(placement,"assignments","$.abstractTopology.placement",d);
            if(status=="unresolved_user_gate" && assignments!=null && assignments.Items.Count!=0) Add(d,"AL-ATLAS-REALM-PLACEMENT-INVALID","$.abstractTopology.placement.assignments",string.Empty,"unresolved placement must be empty");
            return status!="unresolved_user_gate";
        }
        private sealed class RefPair { internal RefPair(string id,string first,string second){Id=id;First=first;Second=second;} internal string Id{get;} internal string First{get;} internal string Second{get;} }
        private static Dictionary<string,RefPair> ParseSimpleRefs(StrictJsonObject root,string property,string first,string second,int count,List<WorldAtlasDiagnostic>d)
        {
            var result=new Dictionary<string,RefPair>(StringComparer.Ordinal); var array=Array(root,property,"$",d);
            ParseObjects(array,"$."+property,d,(o,p)=>{string id=String(o,"id",p,d); AddUnique(result,id,new RefPair(id,String(o,first,p,d),String(o,second,p,d)),p,d);}); Count(result.Count,count,"$."+property,d); return result;
        }
        private static Dictionary<string,RefPair> ParseWalls(StrictJsonObject root,List<WorldAtlasDiagnostic>d)
        {
            var result=new Dictionary<string,RefPair>(StringComparer.Ordinal); var array=Array(root,"walls","$",d);
            ParseObjects(array,"$.walls",d,(o,p)=>{string id=String(o,"id",p,d); AddUnique(result,id,new RefPair(id,String(o,"realmId",p,d),String(o,"boundaryRole",p,d)),p,d);}); Count(result.Count,8,"$.walls",d); return result;
        }
        private static List<WorldAtlasBoundary> ParseBoundaries(StrictJsonObject root,List<WorldAtlasDiagnostic>d)
        {
            var result=new List<WorldAtlasBoundary>(); var array=Array(root,"boundaries","$",d);
            ParseObjects(array,"$.boundaries",d,(o,p)=>result.Add(new WorldAtlasBoundary(String(o,"id",p,d),String(o,"realmId",p,d),String(o,"innerRealmId",p,d),String(o,"innerAtlasZoneId",p,d),String(o,"innerWallId",p,d),String(o,"transitionZoneId",p,d),String(o,"mainGateId",p,d),String(o,"outerWallId",p,d),String(o,"outerWarzoneId",p,d),String(o,"outerAtlasZoneId",p,d),Strings(Array(o,"orderedStages",p,d),p+".orderedStages",d))));
            Count(result.Count,4,"$.boundaries",d); return result;
        }
        private static List<WorldAtlasZone> ParseZones(StrictJsonObject root,List<WorldAtlasDiagnostic>d)
        {
            var result=new List<WorldAtlasZone>(); var array=Array(root,"zones","$",d);
            ParseObjects(array,"$.zones",d,(o,p)=>result.Add(new WorldAtlasZone(String(o,"id",p,d),OptionalString(o,"realmId"),String(o,"displayNameKey",p,d),String(o,"summaryKey",p,d),String(o,"zoneType",p,d),String(o,"visibility",p,d),String(o,"sceneReferenceStatus",p,d)))); Count(result.Count,11,"$.zones",d); return result;
        }
        private static List<WorldAtlasObjective> ParseObjectives(StrictJsonObject root,List<WorldAtlasDiagnostic>d)
        {
            var result=new List<WorldAtlasObjective>(); var array=Array(root,"objectives","$",d);
            ParseObjects(array,"$.objectives",d,(o,p)=>
            {
                List<string> requiredTypes = OptionalStrings(o, "requiredZoneTypes", p, d);
                List<string> requiredIds = OptionalStrings(o, "requiredZoneIds", p, d);
                if (requiredTypes.Count == 0 && requiredIds.Count == 0)
                    Add(d,"AL-ATLAS-SCHEMA-INVALID",p,string.Empty,"one required-zone selector is required");
                result.Add(new WorldAtlasObjective(
                    String(o,"id",p,d),
                    String(o,"displayNameKey",p,d),
                    String(o,"summaryKey",p,d),
                    requiredTypes,
                    requiredIds,
                    String(o,"hookStatus",p,d)));
            });
            Count(result.Count,5,"$.objectives",d); return result;
        }

        private static List<WorldAtlasProtectedZonePolicy> ParseProtectedZonePolicies(StrictJsonObject root, List<WorldAtlasDiagnostic> d)
        {
            var result = new List<WorldAtlasProtectedZonePolicy>();
            var array = Array(root, "protectedZonePolicies", "$", d);
            ParseObjects(array, "$.protectedZonePolicies", d, (o, p) => result.Add(
                new WorldAtlasProtectedZonePolicy(
                    String(o, "id", p, d),
                    String(o, "zoneKind", p, d),
                    String(o, "protection", p, d),
                    String(o, "appliesTo", p, d),
                    String(o, "applicationRecheck", p, d),
                    String(o, "warOverride", p, d),
                    String(o, "enforcementStatus", p, d),
                    String(o, "mutationAuthority", p, d))));
            Count(result.Count, 3, "$.protectedZonePolicies", d);
            return result;
        }

        private static List<WorldAtlasProtectedSubzone> ParseProtectedSubzones(StrictJsonObject root, List<WorldAtlasDiagnostic> d)
        {
            var result = new List<WorldAtlasProtectedSubzone>();
            var array = Array(root, "protectedSubzones", "$", d);
            ParseObjects(array, "$.protectedSubzones", d, (o, p) => result.Add(
                new WorldAtlasProtectedSubzone(
                    String(o, "id", p, d),
                    String(o, "realmId", p, d),
                    String(o, "parentAtlasZoneId", p, d),
                    String(o, "zoneKind", p, d),
                    String(o, "policyId", p, d),
                    String(o, "sceneReferenceStatus", p, d),
                    String(o, "boundaryStatus", p, d),
                    String(o, "mutationAuthority", p, d))));
            Count(result.Count, 12, "$.protectedSubzones", d);
            return result;
        }

        private static void ValidateTopology(IList<WorldAtlasNode> nodes,IList<WorldAtlasAdjacency> adj,IList<WorldAtlasBridge> bridges,IList<WorldAtlasEndpoint>endpoints,List<WorldAtlasDiagnostic>d)
        {
            var nodeIds=Unique(nodes.Select(v=>v.Id),"$.abstractTopology.nodes",d); var adjacencyPairs=new HashSet<string>(adj.Select(v=>Pair(v.NodeAId,v.NodeBId)),StringComparer.Ordinal);
            foreach(var a in adj){Ref(nodeIds,a.NodeAId,"$.abstractTopology.adjacency",a.Id,d);Ref(nodeIds,a.NodeBId,"$.abstractTopology.adjacency",a.Id,d);if(a.NodeAId==a.NodeBId)Add(d,"AL-ATLAS-ADJACENCY-INVALID","$.abstractTopology.adjacency",a.Id,"self adjacency");}
            var bridgeIds=Unique(bridges.Select(v=>v.Id),"$.abstractTopology.bridges",d); var endpointIds=Unique(endpoints.Select(v=>v.Id),"$.abstractTopology.endpoints",d);
            var bridgePairs=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var b in bridges)
            {
                Ref(nodeIds,b.NodeAId,"$.abstractTopology.bridges",b.Id,d);Ref(nodeIds,b.NodeBId,"$.abstractTopology.bridges",b.Id,d);Ref(endpointIds,b.EndpointAId,"$.abstractTopology.bridges",b.Id,d);Ref(endpointIds,b.EndpointBId,"$.abstractTopology.bridges",b.Id,d);
                if(b.ConnectionType!="physical_bidirectional"||b.NodeAId==b.NodeBId)Add(d,"AL-ATLAS-TOPOLOGY-INVALID","$.abstractTopology.bridges",b.Id,"invalid bridge connection");
                bool center=b.NodeAId=="center_slot"||b.NodeBId=="center_slot"; string pair=Pair(b.NodeAId,b.NodeBId);
                if(!center&&!adjacencyPairs.Contains(pair))Add(d,"AL-ATLAS-TOPOLOGY-INVALID","$.abstractTopology.bridges",b.Id,"bridge pair is not adjacent");
                bridgePairs[pair]=bridgePairs.TryGetValue(pair,out int n)?n+1:1;
            }
            foreach(var pair in adjacencyPairs)if(!bridgePairs.TryGetValue(pair,out int count)||count!=2)Add(d,"AL-ATLAS-TOPOLOGY-INVALID","$.abstractTopology.bridges",pair,"adjacent pair requires two bridges");
            foreach(string ring in new[]{"ring_slot_01","ring_slot_02","ring_slot_03","ring_slot_04"})if(!bridgePairs.TryGetValue(Pair(ring,"center_slot"),out int count)||count!=1)Add(d,"AL-ATLAS-TOPOLOGY-INVALID","$.abstractTopology.bridges",ring,"ring slot requires one center bridge");
            foreach(var e in endpoints){Ref(bridgeIds,e.BridgeId,"$.abstractTopology.endpoints",e.Id,d);Ref(nodeIds,e.NodeId,"$.abstractTopology.endpoints",e.Id,d);var b=bridges.FirstOrDefault(v=>v.Id==e.BridgeId);if(b!=null&&!((b.EndpointAId==e.Id&&b.NodeAId==e.NodeId)||(b.EndpointBId==e.Id&&b.NodeBId==e.NodeId)))Add(d,"AL-ATLAS-ENDPOINT-INVALID","$.abstractTopology.endpoints",e.Id,"endpoint does not match bridge side");}
        }
        private static void ValidateBoundaries(Dictionary<string,RefPair>transitions,Dictionary<string,RefPair>walls,IList<WorldAtlasBoundary>boundaries,IList<WorldAtlasZone>zones,List<WorldAtlasDiagnostic>d)
        {
            var zoneIds=Unique(zones.Select(v=>v.Id),"$.zones",d); var transitionIds=new HashSet<string>(transitions.Keys,StringComparer.Ordinal); var wallIds=new HashSet<string>(walls.Keys,StringComparer.Ordinal);
            if(!boundaries.Select(v=>v.RealmId).SequenceEqual(RealmOrder))Add(d,"AL-ATLAS-BOUNDARY-INVALID","$.boundaries",string.Empty,"boundary realm order mismatch");
            foreach(var b in boundaries)
            {
                if(!BoundaryStages.SequenceEqual(b.OrderedStages))Add(d,"AL-ATLAS-BOUNDARY-INVALID","$.boundaries",b.Id,"ordered stages mismatch");
                Ref(zoneIds,b.InnerAtlasZoneId,"$.boundaries",b.Id,d);Ref(zoneIds,b.OuterAtlasZoneId,"$.boundaries",b.Id,d);Ref(transitionIds,b.TransitionZoneId,"$.boundaries",b.Id,d);Ref(wallIds,b.InnerWallId,"$.boundaries",b.Id,d);Ref(wallIds,b.OuterWallId,"$.boundaries",b.Id,d);
                if(transitions.TryGetValue(b.TransitionZoneId,out var t)&&(t.First!=b.RealmId||t.Second!=b.MainGateId))Add(d,"AL-ATLAS-BOUNDARY-INVALID","$.boundaries",b.Id,"transition cross-reference mismatch");
                if(walls.TryGetValue(b.InnerWallId,out var iw)&&(iw.First!=b.RealmId||iw.Second!="inner_wall"))Add(d,"AL-ATLAS-BOUNDARY-INVALID","$.boundaries",b.Id,"inner wall mismatch");
                if(walls.TryGetValue(b.OuterWallId,out var ow)&&(ow.First!=b.RealmId||ow.Second!="outer_wall"))Add(d,"AL-ATLAS-BOUNDARY-INVALID","$.boundaries",b.Id,"outer wall mismatch");
            }
        }
        private static void ValidateObjectives(IList<WorldAtlasObjective> objectives, IList<WorldAtlasZone> zones, List<WorldAtlasDiagnostic> d)
        {
            var zoneIds = new HashSet<string>(zones.Select(value => value.Id), StringComparer.Ordinal);
            var zoneTypes = new HashSet<string>(zones.Select(value => value.ZoneType), StringComparer.Ordinal);
            Unique(objectives.Select(value => value.Id), "$.objectives", d);
            foreach (WorldAtlasObjective objective in objectives)
            {
                foreach (string zoneId in objective.RequiredZoneIds)
                    Ref(zoneIds, zoneId, "$.objectives", objective.Id, d);
                foreach (string zoneType in objective.RequiredZoneTypes)
                    if (!zoneTypes.Contains(zoneType))
                        Add(d, "AL-ATLAS-REFERENCE-MISSING", "$.objectives", objective.Id, "missing zone type: " + zoneType);
            }
        }

        private static void ValidateProtectedZoneAuthority(
            IList<WorldAtlasProtectedZonePolicy> policies,
            IList<WorldAtlasProtectedSubzone> subzones,
            IList<WorldAtlasZone> zones,
            List<WorldAtlasDiagnostic> d)
        {
            var policyIds = Unique(policies.Select(value => value.Id), "$.protectedZonePolicies", d);
            var zoneIds = new HashSet<string>(zones.Select(value => value.Id), StringComparer.Ordinal);
            Unique(subzones.Select(value => value.Id), "$.protectedSubzones", d);
            if (!policies.Select(value => value.Id).SequenceEqual(ProtectedZonePolicyOrder))
                Add(d, "AL-ATLAS-PROTECTED-ZONE-INVALID", "$.protectedZonePolicies", string.Empty, "protected-zone policy order or identity mismatch");
            for (int index = 0; index < policies.Count; index++)
            {
                WorldAtlasProtectedZonePolicy policy = policies[index];
                string expectedKind = index < ProtectedZoneKindOrder.Length ? ProtectedZoneKindOrder[index] : string.Empty;
                if (policy.ZoneKind != expectedKind || policy.Protection != "forced_non_pvp" ||
                    policy.AppliesTo != "all_player_harmful_effects" || policy.ApplicationRecheck != "required" ||
                    policy.WarOverride != "blocked" || policy.EnforcementStatus != "contract_only" ||
                    policy.MutationAuthority != "none")
                    Add(d, "AL-ATLAS-PROTECTED-ZONE-INVALID", "$.protectedZonePolicies", policy.Id, "protected-zone policy metadata mismatch");
            }

            int subzoneIndex = 0;
            foreach (string realmId in RealmOrder)
            {
                foreach (string zoneKind in ProtectedZoneKindOrder)
                {
                    if (subzoneIndex >= subzones.Count) break;
                    WorldAtlasProtectedSubzone subzone = subzones[subzoneIndex++];
                    string expectedId = "zone_protected_" + realmId + "_" + zoneKind;
                    string expectedParentId = "zone_inner_" + realmId;
                    string expectedPolicyId = "zone_policy_" + zoneKind + "_safe_v001";
                    if (subzone.Id != expectedId || subzone.RealmId != realmId ||
                        subzone.ParentAtlasZoneId != expectedParentId || subzone.ZoneKind != zoneKind ||
                        subzone.PolicyId != expectedPolicyId || subzone.SceneReferenceStatus != "requested" ||
                        subzone.BoundaryStatus != "requested" || subzone.MutationAuthority != "none")
                        Add(d, "AL-ATLAS-PROTECTED-ZONE-INVALID", "$.protectedSubzones", subzone.Id, "protected subzone identity, order, or metadata mismatch");
                    Ref(zoneIds, subzone.ParentAtlasZoneId, "$.protectedSubzones", subzone.Id, d);
                    Ref(policyIds, subzone.PolicyId, "$.protectedSubzones", subzone.Id, d);
                }
            }
        }

        private static void ValidateGlobalIds(IList<WorldAtlasNode>n,IList<WorldAtlasAdjacency>a,IList<WorldAtlasBridge>b,IList<WorldAtlasEndpoint>e,IEnumerable<string>t,IEnumerable<string>w,IList<WorldAtlasBoundary>bounds,IList<WorldAtlasZone>zones,IList<WorldAtlasObjective>objectives,IList<WorldAtlasProtectedZonePolicy>policies,IList<WorldAtlasProtectedSubzone>subzones,List<WorldAtlasDiagnostic>d)
        {
            var seen=new HashSet<string>(StringComparer.Ordinal); foreach(string id in n.Select(v=>v.Id).Concat(a.Select(v=>v.Id)).Concat(b.Select(v=>v.Id)).Concat(e.Select(v=>v.Id)).Concat(t).Concat(w).Concat(bounds.Select(v=>v.Id)).Concat(zones.Select(v=>v.Id)).Concat(objectives.Select(v=>v.Id)).Concat(policies.Select(v=>v.Id)).Concat(subzones.Select(v=>v.Id)))if(!WorldAtlasTopologyQuery.ValidId(id)||!seen.Add(id))Add(d,"AL-ATLAS-ID-DUPLICATE","$",id,"invalid or duplicate global atlas id");
        }

        private static StrictJsonObject Object(StrictJsonObject owner,string name,string path,List<WorldAtlasDiagnostic>d){if(owner!=null&&owner.TryGet(name,out var v)&&v is StrictJsonObject o)return o;Add(d,"AL-ATLAS-SCHEMA-INVALID",path+"."+name,string.Empty,"object required");return null;}
        private static StrictJsonArray Array(StrictJsonObject owner,string name,string path,List<WorldAtlasDiagnostic>d){if(owner!=null&&owner.TryGet(name,out var v)&&v is StrictJsonArray a)return a;Add(d,"AL-ATLAS-SCHEMA-INVALID",path+"."+name,string.Empty,"array required");return null;}
        private static string String(StrictJsonObject owner,string name,string path,List<WorldAtlasDiagnostic>d){if(owner!=null&&owner.TryGet(name,out var v)&&v is StrictJsonString s&&!string.IsNullOrWhiteSpace(s.Value))return s.Value;Add(d,"AL-ATLAS-SCHEMA-INVALID",path+"."+name,string.Empty,"nonblank string required");return string.Empty;}
        private static string OptionalString(StrictJsonObject owner,string name){return owner!=null&&owner.TryGet(name,out var v)&&v is StrictJsonString s?s.Value:string.Empty;}
        private static void RequireEqual(StrictJsonObject o,string name,string expected,string path,List<WorldAtlasDiagnostic>d){string actual=String(o,name,path,d);if(actual!=expected)Add(d,"AL-ATLAS-SOURCE-MISMATCH",path+"."+name,actual,"authority identity mismatch");}
        private static void ParseObjects(StrictJsonArray array,string path,List<WorldAtlasDiagnostic>d,Action<StrictJsonObject,string>action){if(array==null)return;for(int i=0;i<array.Items.Count;i++){if(array.Items[i] is StrictJsonObject o)action(o,path+"["+i+"]");else Add(d,"AL-ATLAS-SCHEMA-INVALID",path+"["+i+"]",string.Empty,"object required");}}
        private static List<string> Strings(StrictJsonArray array,string path,List<WorldAtlasDiagnostic>d){var r=new List<string>();if(array==null)return r;for(int i=0;i<array.Items.Count;i++){if(array.Items[i] is StrictJsonString s&&!string.IsNullOrWhiteSpace(s.Value))r.Add(s.Value);else Add(d,"AL-ATLAS-SCHEMA-INVALID",path+"["+i+"]",string.Empty,"string required");}return r;}
        private static List<string> OptionalStrings(StrictJsonObject owner,string name,string path,List<WorldAtlasDiagnostic>d){if(owner!=null&&owner.TryGet(name,out var value)){if(value is StrictJsonArray array)return Strings(array,path+"."+name,d);Add(d,"AL-ATLAS-SCHEMA-INVALID",path+"."+name,string.Empty,"array required");}return new List<string>();}
        private static void Count(int actual,int expected,string path,List<WorldAtlasDiagnostic>d){if(actual!=expected)Add(d,"AL-ATLAS-COUNT-INVALID",path,string.Empty,"expected "+expected+", actual "+actual);}
        private static HashSet<string> Unique(IEnumerable<string>ids,string path,List<WorldAtlasDiagnostic>d){var s=new HashSet<string>(StringComparer.Ordinal);foreach(string id in ids)if(!WorldAtlasTopologyQuery.ValidId(id)||!s.Add(id))Add(d,"AL-ATLAS-ID-DUPLICATE",path,id,"invalid or duplicate id");return s;}
        private static void Ref(ISet<string>ids,string id,string path,string related,List<WorldAtlasDiagnostic>d){if(!ids.Contains(id))Add(d,"AL-ATLAS-REFERENCE-MISSING",path,related,"missing reference: "+id);}
        private static void AddUnique<T>(IDictionary<string,T>values,string id,T value,string path,List<WorldAtlasDiagnostic>d){if(!WorldAtlasTopologyQuery.ValidId(id)||values.ContainsKey(id))Add(d,"AL-ATLAS-ID-DUPLICATE",path,id,"invalid or duplicate id");else values.Add(id,value);}
        private static string Pair(string a,string b)=>string.CompareOrdinal(a,b)<0?a+"|"+b:b+"|"+a;
        private static void Add(List<WorldAtlasDiagnostic>d,string code,string path,string id,string message){d.Add(new WorldAtlasDiagnostic(code,path,id,message));}
        private static void SortDiagnostics(List<WorldAtlasDiagnostic>d){d.Sort((l,r)=>{int c=string.CompareOrdinal(l.Code,r.Code);if(c!=0)return c;c=string.CompareOrdinal(l.Path,r.Path);return c!=0?c:string.CompareOrdinal(l.RelatedId,r.RelatedId);});}
        private static WorldAtlasLoadResult Reject(WorldAtlasLoadStatus status,List<WorldAtlasDiagnostic>d,string code,string path,string id,string message){Add(d,code,path,id,message);SortDiagnostics(d);return new WorldAtlasLoadResult(status,null,d.Take(WorldAtlasContract.MaximumDiagnostics).ToArray());}
    }
}
