using AL.Core;
using UnityEngine;

namespace AL.World
{
    public sealed class InnerRealmWorldBuildResult
    {
        internal InnerRealmWorldBuildResult(Transform root, InnerRealmWorldLayout layout, InnerRealmSlotLayout walkable)
        {
            Root = root;
            Layout = layout;
            WalkableInner = walkable;
        }

        public Transform Root { get; }
        public InnerRealmWorldLayout Layout { get; }
        public InnerRealmSlotLayout WalkableInner { get; }
        public Vector3 PlayerSpawn => WalkableInner.WalkableSpawn;
    }

    public static class InnerRealmWorldGreyboxBuilder
    {
        public static InnerRealmWorldBuildResult Build(InnerRealmWorldLayout layout, string walkableRealmId)
        {
            if (layout == null)
            {
                throw new System.ArgumentNullException(nameof(layout));
            }

            InnerRealmSlotLayout walkable = layout.GetWalkableInner(walkableRealmId);
            var root = new GameObject("InnerRealmWorld_TEMPORARY").transform;
            Label(root.gameObject, layout.TemporaryLabel);

            CreatePrimitive(
                root,
                "world_continent_basin",
                PrimitiveType.Cube,
                Vector3.down * 0.35f,
                new Vector3(InnerRealmWorldLayout.ContinentHalfExtent * 2.2f, 0.4f, InnerRealmWorldLayout.ContinentHalfExtent * 2.2f),
                Vector3.zero,
                new Color(0.11f, 0.12f, 0.10f),
                false);

            CreatePrimitive(
                root,
                "warzone_center_unplayable",
                PrimitiveType.Cube,
                new Vector3(0f, -0.05f, 0f),
                new Vector3(96f, 0.18f, 96f),
                Vector3.zero,
                new Color(0.18f, 0.09f, 0.07f),
                false);

            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 90f;
                CreatePrimitive(
                    root,
                    "warzone_fort_" + i,
                    PrimitiveType.Cube,
                    Quaternion.Euler(0f, yaw, 0f) * new Vector3(18f, 0f, 18f) + Vector3.up * 2.2f,
                    new Vector3(6.4f, 4.4f, 6.4f),
                    new Vector3(0f, yaw, 0f),
                    new Color(0.22f, 0.16f, 0.12f),
                    true);
            }

            for (int i = 0; i < layout.Inners.Count; i++)
            {
                BuildInner(root, layout.Inners[i], layout.Inners[i] == walkable);
            }

            for (int i = 0; i < layout.Bridges.Count; i++)
            {
                BuildBridge(root, layout.Bridges[i]);
            }

            BuildAccordantIsle(root, layout);
            return new InnerRealmWorldBuildResult(root, layout, walkable);
        }

        private static void BuildInner(Transform root, InnerRealmSlotLayout inner, bool walkable)
        {
            var zone = new GameObject(inner.InnerAtlasZoneId).transform;
            zone.SetParent(root, false);
            Label(zone.gameObject, InnerRealmWorldIds.TemporaryLabel);

            Color ground = GroundColor(inner.Realm);
            CreatePrimitive(
                zone,
                inner.InnerAtlasZoneId + "_ground",
                PrimitiveType.Cube,
                inner.InnerSafe.Center + Vector3.up * 0.02f,
                new Vector3(inner.InnerSafe.HalfExtent * 2.05f, 0.16f, inner.InnerSafe.HalfExtent * 2.05f),
                Vector3.zero,
                ground,
                false);

            BuildTerrainIdentity(zone, inner);
            BuildWallRing(zone, inner.InnerWallId, inner.InnerSafe, inner.GatePosition, inner.MainGateId, 6.2f, new Color(0.22f, 0.21f, 0.20f));
            BuildOuterWall(zone, inner);
            BuildSettlement(zone, inner);
            BuildCave(zone, inner);

            if (walkable)
            {
                CreatePrimitive(
                    zone,
                    "walkable_spawn_sigil",
                    PrimitiveType.Cylinder,
                    inner.CapitalPosition + Vector3.up * 0.08f,
                    new Vector3(2.4f, 0.04f, 2.4f),
                    Vector3.zero,
                    new Color(0.85f, 0.72f, 0.28f),
                    true);
            }
        }

        private static void BuildTerrainIdentity(Transform zone, InnerRealmSlotLayout inner)
        {
            Vector3 c = inner.InnerSafe.Center;
            switch (inner.Realm)
            {
                case RealmId.Stonehold:
                    for (int i = 0; i < 5; i++)
                    {
                        CreatePrimitive(
                            zone,
                            "stonehold_terrace_" + i,
                            PrimitiveType.Cube,
                            c + new Vector3(-10f + i * 4.2f, 0.35f + i * 0.22f, -8f),
                            new Vector3(8.4f, 0.55f + i * 0.18f, 3.2f),
                            Vector3.zero,
                            new Color(0.28f, 0.18f, 0.12f),
                            true);
                    }

                    CreatePrimitive(zone, "stonehold_basalt_spire", PrimitiveType.Cylinder, c + new Vector3(10f, 3.4f, 8f), new Vector3(2.2f, 3.6f, 2.2f), Vector3.zero, new Color(0.16f, 0.10f, 0.09f), true);
                    break;
                case RealmId.Eldergrove:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI * 2f / 6f;
                        Vector3 p = c + new Vector3(Mathf.Cos(a) * 16f, 0f, Mathf.Sin(a) * 16f);
                        CreatePrimitive(zone, "eldergrove_trunk_" + i, PrimitiveType.Cylinder, p + Vector3.up * 3.2f, new Vector3(1.1f, 3.4f, 1.1f), Vector3.zero, new Color(0.22f, 0.14f, 0.08f), true);
                        CreatePrimitive(zone, "eldergrove_canopy_" + i, PrimitiveType.Sphere, p + Vector3.up * 6.4f, new Vector3(4.6f, 2.8f, 4.6f), Vector3.zero, new Color(0.12f, 0.32f, 0.16f), true);
                    }

                    CreatePrimitive(zone, "eldergrove_root_lake", PrimitiveType.Cylinder, c + new Vector3(8f, 0.06f, -6f), new Vector3(7.2f, 0.05f, 5.4f), Vector3.zero, new Color(0.10f, 0.22f, 0.28f), true);
                    break;
                case RealmId.Crownlands:
                    for (int x = -2; x <= 2; x++)
                    {
                        CreatePrimitive(zone, "crownlands_grain_row_" + x, PrimitiveType.Cube, c + new Vector3(x * 5.4f, 0.18f, 10f), new Vector3(4.6f, 0.28f, 14f), Vector3.zero, new Color(0.55f, 0.48f, 0.24f), false);
                    }

                    CreatePrimitive(zone, "crownlands_chalk_rise", PrimitiveType.Cube, c + new Vector3(-12f, 1.4f, -10f), new Vector3(14f, 2.6f, 8f), Vector3.zero, new Color(0.72f, 0.70f, 0.62f), true);
                    break;
                default:
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * 0.9f;
                        CreatePrimitive(
                            zone,
                            "umbral_shard_" + i,
                            PrimitiveType.Cube,
                            c + new Vector3(Mathf.Cos(a) * 14f, 2.4f, Mathf.Sin(a) * 12f),
                            new Vector3(1.4f, 5.6f, 1.1f),
                            new Vector3(18f, i * 25f, -12f),
                            new Color(0.10f, 0.06f, 0.16f),
                            true);
                    }

                    CreatePrimitive(zone, "umbral_void_pit", PrimitiveType.Cylinder, c + new Vector3(6f, -0.4f, 4f), new Vector3(6.4f, 0.8f, 6.4f), Vector3.zero, new Color(0.04f, 0.02f, 0.08f), true);
                    CreatePrimitive(zone, "umbral_glass_rod", PrimitiveType.Cylinder, c + new Vector3(-8f, 3.8f, -6f), new Vector3(0.45f, 4.2f, 0.45f), Vector3.zero, new Color(0.42f, 0.18f, 0.62f), true);
                    break;
            }
        }

        private static void BuildWallRing(
            Transform zone,
            string wallId,
            InnerRealmRect rect,
            Vector3 gatePosition,
            string gateId,
            float height,
            Color color)
        {
            var wallRoot = new GameObject(wallId).transform;
            wallRoot.SetParent(zone, false);
            Label(wallRoot.gameObject, InnerRealmWorldIds.TemporaryLabel);

            float thickness = 1.6f;
            float widthX = rect.HalfExtent * 2f;
            float widthZ = rect.HalfExtent * 2f;
            CreatePrimitive(wallRoot, wallId + "_north", PrimitiveType.Cube, new Vector3(rect.Center.x, height * 0.5f, rect.MaxZ), new Vector3(widthX + thickness, height, thickness), Vector3.zero, color, true);
            CreatePrimitive(wallRoot, wallId + "_south", PrimitiveType.Cube, new Vector3(rect.Center.x, height * 0.5f, rect.MinZ), new Vector3(widthX + thickness, height, thickness), Vector3.zero, color, true);
            CreatePrimitive(wallRoot, wallId + "_east", PrimitiveType.Cube, new Vector3(rect.MaxX, height * 0.5f, rect.Center.z), new Vector3(thickness, height, widthZ), Vector3.zero, color, true);
            CreatePrimitive(wallRoot, wallId + "_west", PrimitiveType.Cube, new Vector3(rect.MinX, height * 0.5f, rect.Center.z), new Vector3(thickness, height, widthZ), Vector3.zero, color, true);

            Vector3 gateDir = (Vector3.zero - new Vector3(gatePosition.x, 0f, gatePosition.z)).normalized;
            float yaw = Mathf.Atan2(gateDir.x, gateDir.z) * Mathf.Rad2Deg;
            CreatePrimitive(wallRoot, gateId, PrimitiveType.Cube, gatePosition + Vector3.up * (height * 0.55f), new Vector3(InnerRealmWorldLayout.GateWidth, height * 1.15f, 2.4f), new Vector3(0f, yaw, 0f), new Color(0.32f, 0.28f, 0.20f), true);
            CreatePrimitive(wallRoot, gateId + "_leaf_left", PrimitiveType.Cube, gatePosition + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-3.2f, height * 0.45f, 0.2f), new Vector3(2.6f, height * 0.9f, 0.35f), new Vector3(0f, yaw, 0f), new Color(0.18f, 0.14f, 0.10f), true);
            CreatePrimitive(wallRoot, gateId + "_leaf_right", PrimitiveType.Cube, gatePosition + Quaternion.Euler(0f, yaw, 0f) * new Vector3(3.2f, height * 0.45f, 0.2f), new Vector3(2.6f, height * 0.9f, 0.35f), new Vector3(0f, yaw, 0f), new Color(0.18f, 0.14f, 0.10f), true);
        }

        private static void BuildOuterWall(Transform zone, InnerRealmSlotLayout inner)
        {
            Vector3 towardCenter = (Vector3.zero - inner.InnerSafe.Center);
            towardCenter.y = 0f;
            towardCenter.Normalize();
            Vector3 lateral = Vector3.Cross(Vector3.up, towardCenter);
            CreatePrimitive(
                zone,
                inner.OuterWallId,
                PrimitiveType.Cube,
                inner.OuterWallCenter + Vector3.up * 3.4f,
                new Vector3(42f, 5.4f, 1.8f),
                new Vector3(0f, Mathf.Atan2(towardCenter.x, towardCenter.z) * Mathf.Rad2Deg, 0f),
                new Color(0.16f, 0.15f, 0.14f),
                true);
            CreatePrimitive(
                zone,
                inner.TransitionZoneId,
                PrimitiveType.Cube,
                inner.OuterWallCenter - towardCenter * 8f + Vector3.up * 0.06f,
                new Vector3(20f, 0.1f, 14f),
                Vector3.zero,
                new Color(0.24f, 0.18f, 0.12f),
                false);
            CreatePrimitive(
                zone,
                inner.OuterAtlasZoneId + "_unplayable",
                PrimitiveType.Cube,
                inner.OuterWallCenter + towardCenter * 10f + Vector3.up * 1.6f,
                new Vector3(1.2f, 3.2f, 16f),
                new Vector3(0f, Mathf.Atan2(towardCenter.x, towardCenter.z) * Mathf.Rad2Deg, 0f),
                new Color(0.08f, 0.04f, 0.04f),
                true);
            _ = lateral;
        }

        private static void BuildSettlement(Transform zone, InnerRealmSlotLayout inner)
        {
            BuildNamedKeep(zone, inner.CapitalPoiId, InnerRealmWorldIds.DisplayCapital(), inner.CapitalPosition, inner.Realm, 1.15f);
            BuildNamedKeep(zone, inner.OutpostAPoiId, InnerRealmWorldIds.DisplayOutpostA(), inner.OutpostAPosition, inner.Realm, 0.62f);
            BuildNamedKeep(zone, inner.OutpostBPoiId, InnerRealmWorldIds.DisplayOutpostB(), inner.OutpostBPosition, inner.Realm, 0.58f);

            Vector3 lateral = Vector3.Cross(Vector3.up, inner.CapitalPosition.normalized);
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = inner.CapitalPosition + lateral * ((i - 1.5f) * 4.6f) + inner.CornerSign.ToWorld() * -6f;
                CreatePrimitive(
                    zone,
                    inner.InnerAtlasZoneId + "_house_" + i,
                    PrimitiveType.Cube,
                    p + Vector3.up * 1.3f,
                    new Vector3(3.2f, 2.6f, 3.6f),
                    Vector3.zero,
                    HouseColor(inner.Realm),
                    true);
            }
        }

        private static void BuildNamedKeep(
            Transform zone,
            string id,
            string display,
            Vector3 position,
            RealmId realm,
            float scale)
        {
            var keep = new GameObject(id).transform;
            keep.SetParent(zone, false);
            Label(keep.gameObject, display + " / " + InnerRealmWorldIds.TemporaryLabel);
            Vector3 size = KeepSize(realm) * scale;
            CreatePrimitive(keep, id + "_hall", PrimitiveType.Cube, position + Vector3.up * (size.y * 0.5f), size, Vector3.zero, KeepColor(realm), true);
            CreatePrimitive(keep, id + "_keep", PrimitiveType.Cube, position + new Vector3(0f, size.y + 1.4f * scale, 0f), new Vector3(size.x * 0.45f, 3.2f * scale, size.z * 0.45f), Vector3.zero, KeepColor(realm) * 0.85f, true);
            if (realm == RealmId.Eldergrove)
            {
                CreatePrimitive(keep, id + "_crown", PrimitiveType.Sphere, position + Vector3.up * (size.y + 3.2f * scale), new Vector3(4.2f, 2.2f, 4.2f) * scale, Vector3.zero, new Color(0.10f, 0.30f, 0.14f), true);
            }
            else if (realm == RealmId.Crownlands)
            {
                CreatePrimitive(keep, id + "_spire", PrimitiveType.Cylinder, position + Vector3.up * (size.y + 3.6f * scale), new Vector3(0.7f, 2.8f, 0.7f) * scale, Vector3.zero, new Color(0.78f, 0.68f, 0.32f), true);
            }
        }

        private static void BuildCave(Transform zone, InnerRealmSlotLayout inner)
        {
            var cave = new GameObject(inner.DragonCaveId).transform;
            cave.SetParent(zone, false);
            Label(cave.gameObject, "Sealed dragon cave / " + InnerRealmWorldIds.TemporaryLabel);
            Color mouth = CaveColor(inner.Realm);
            CreatePrimitive(cave, inner.DragonCaveId + "_mouth", PrimitiveType.Cylinder, inner.CavePosition + Vector3.up * 1.1f, new Vector3(4.4f, 1.6f, 4.4f), new Vector3(90f, 0f, 0f), mouth, true);
            CreatePrimitive(cave, inner.DragonCaveId + "_seal", PrimitiveType.Cube, inner.CavePosition + Vector3.up * 1.2f, new Vector3(3.6f, 2.4f, 0.6f), Vector3.zero, new Color(0.08f, 0.08f, 0.08f), true);
        }

        private static void BuildBridge(Transform root, WorldBridgeLayout bridge)
        {
            Vector3 delta = bridge.End - bridge.Start;
            float length = Mathf.Max(4f, delta.magnitude);
            Vector3 mid = bridge.Midpoint + Vector3.up * (bridge.SealedEvent ? 3.4f : 1.1f);
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            Color color = bridge.SealedEvent
                ? new Color(0.22f, 0.24f, 0.32f)
                : new Color(0.28f, 0.22f, 0.16f);
            CreatePrimitive(root, bridge.Id, PrimitiveType.Cube, mid, new Vector3(4.2f, 0.55f, length), new Vector3(0f, yaw, 0f), color, true);
            if (bridge.SealedEvent)
            {
                GameObject seal = CreatePrimitive(root, bridge.Id + "_seal", PrimitiveType.Cube, mid + Vector3.up * 1.6f, new Vector3(3.2f, 3.2f, 1.1f), new Vector3(0f, yaw, 0f), new Color(0.08f, 0.10f, 0.16f), true);
                Label(seal, "Sealed event bridge / " + InnerRealmWorldIds.TemporaryLabel);
            }
        }

        private static void BuildAccordantIsle(Transform root, InnerRealmWorldLayout layout)
        {
            var isle = new GameObject(layout.AccordantIsleZoneId).transform;
            isle.SetParent(root, false);
            Label(isle.gameObject, InnerRealmWorldIds.TemporaryLabel);
            Vector3 c = layout.AccordantIsleCenter;
            CreatePrimitive(isle, "accordant_isle_rock", PrimitiveType.Cylinder, c, new Vector3(22f, 3.2f, 22f), Vector3.zero, new Color(0.42f, 0.40f, 0.48f), true);
            CreatePrimitive(isle, "accordant_isle_castle", PrimitiveType.Cube, c + Vector3.up * 6.4f, new Vector3(12f, 7.2f, 12f), Vector3.zero, new Color(0.62f, 0.60f, 0.68f), true);
            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 90f;
                Vector3 p = c + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 3.2f, 7.4f);
                CreatePrimitive(isle, "accordant_isle_gate_" + i, PrimitiveType.Cube, p, new Vector3(4.4f, 4.6f, 1.2f), new Vector3(0f, yaw, 0f), new Color(0.30f, 0.28f, 0.36f), true);
            }

            CreatePrimitive(isle, "accordant_wish_dragon_cavern", PrimitiveType.Cylinder, c + Vector3.down * 6f, new Vector3(10f, 5f, 10f), Vector3.zero, new Color(0.18f, 0.32f, 0.42f), true);
        }

        private static GameObject CreatePrimitive(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 position,
            Vector3 scale,
            Vector3 euler,
            Color color,
            bool emit)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(euler);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                var material = shader != null
                    ? new Material(shader)
                    : new Material(renderer.sharedMaterial);
                material.color = color;
                if (emit)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 0.22f);
                }

                renderer.sharedMaterial = material;
            }

            return go;
        }

        private static void Label(GameObject target, string text)
        {
            if (target == null)
            {
                return;
            }

            var existing = target.GetComponent<TextMesh>();
            if (existing == null)
            {
                var label = new GameObject("label_" + InnerRealmWorldIds.TemporaryLabel);
                label.transform.SetParent(target.transform, false);
                label.transform.localPosition = Vector3.up * 2.4f;
                existing = label.AddComponent<TextMesh>();
            }

            existing.text = text;
            existing.characterSize = 0.18f;
            existing.fontSize = 32;
            existing.anchor = TextAnchor.MiddleCenter;
            existing.alignment = TextAlignment.Center;
            existing.color = new Color(0.92f, 0.86f, 0.55f, 0.92f);
        }

        private static Color GroundColor(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold: return new Color(0.30f, 0.20f, 0.14f);
                case RealmId.Eldergrove: return new Color(0.16f, 0.28f, 0.14f);
                case RealmId.Crownlands: return new Color(0.42f, 0.40f, 0.28f);
                default: return new Color(0.14f, 0.10f, 0.18f);
            }
        }

        private static Color KeepColor(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold: return new Color(0.34f, 0.24f, 0.18f);
                case RealmId.Eldergrove: return new Color(0.28f, 0.20f, 0.12f);
                case RealmId.Crownlands: return new Color(0.62f, 0.58f, 0.48f);
                default: return new Color(0.16f, 0.10f, 0.22f);
            }
        }

        private static Color HouseColor(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold: return new Color(0.26f, 0.18f, 0.14f);
                case RealmId.Eldergrove: return new Color(0.20f, 0.26f, 0.14f);
                case RealmId.Crownlands: return new Color(0.52f, 0.46f, 0.34f);
                default: return new Color(0.12f, 0.08f, 0.16f);
            }
        }

        private static Color CaveColor(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold: return new Color(0.72f, 0.22f, 0.08f);
                case RealmId.Eldergrove: return new Color(0.18f, 0.36f, 0.16f);
                case RealmId.Crownlands: return new Color(0.72f, 0.64f, 0.28f);
                default: return new Color(0.28f, 0.10f, 0.42f);
            }
        }

        private static Vector3 KeepSize(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold: return new Vector3(10f, 6.4f, 8.4f);
                case RealmId.Eldergrove: return new Vector3(8.2f, 5.2f, 8.2f);
                case RealmId.Crownlands: return new Vector3(12.4f, 5.6f, 7.2f);
                default: return new Vector3(7.4f, 6.8f, 7.4f);
            }
        }
    }

    internal static class InnerRealmCornerExtensions
    {
        internal static Vector3 ToWorld(this Vector2 sign)
        {
            return new Vector3(sign.x, 0f, sign.y);
        }
    }
}
