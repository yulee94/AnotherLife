using UnityEngine;

namespace AL.ChampionMode.Customization
{
    public static class ProceduralChampionModelBuilder
    {
        public static void EnsureModel(GameObject champion)
        {
            if (champion == null)
            {
                return;
            }

            Transform root = champion.transform;
            RemoveLegacyPart(root, "Hair");

            EnsurePart(root, "Hair_Short", PrimitiveType.Sphere, new Vector3(0f, 0.95f, -0.04f), new Vector3(0.55f, 0.22f, 0.45f), Vector3.zero, new Color(0.08f, 0.06f, 0.04f));
            EnsurePart(root, "Hair_Long", PrimitiveType.Cube, new Vector3(0f, 0.70f, -0.34f), new Vector3(0.58f, 0.70f, 0.12f), Vector3.zero, new Color(0.08f, 0.06f, 0.04f));
            EnsurePart(root, "Hair_Braid", PrimitiveType.Cylinder, new Vector3(0f, 0.32f, -0.42f), new Vector3(0.10f, 0.45f, 0.10f), Vector3.zero, new Color(0.08f, 0.06f, 0.04f));
            EnsurePart(root, "Hair_Mohawk", PrimitiveType.Cube, new Vector3(0f, 1.06f, -0.02f), new Vector3(0.16f, 0.38f, 0.58f), Vector3.zero, new Color(0.08f, 0.06f, 0.04f));
            EnsurePart(root, "Hair_Topknot", PrimitiveType.Sphere, new Vector3(0f, 1.16f, -0.06f), new Vector3(0.24f, 0.24f, 0.24f), Vector3.zero, new Color(0.08f, 0.06f, 0.04f));
            EnsurePart(root, "Hair_Topknot_Tail", PrimitiveType.Cylinder, new Vector3(0f, 0.92f, -0.34f), new Vector3(0.075f, 0.34f, 0.075f), Vector3.zero, new Color(0.08f, 0.06f, 0.04f));
            EnsurePart(root, "Skin_Head", PrimitiveType.Sphere, new Vector3(0f, 0.66f, 0.08f), new Vector3(0.46f, 0.50f, 0.42f), Vector3.zero, new Color(0.72f, 0.56f, 0.42f));
            EnsurePart(root, "Skin_Neck", PrimitiveType.Cube, new Vector3(0f, 0.34f, 0.06f), new Vector3(0.22f, 0.22f, 0.18f), Vector3.zero, new Color(0.72f, 0.56f, 0.42f));
            EnsurePart(root, "Skin_Ear_L", PrimitiveType.Sphere, new Vector3(-0.28f, 0.68f, 0.10f), new Vector3(0.08f, 0.16f, 0.04f), new Vector3(0f, 0f, 18f), new Color(0.72f, 0.56f, 0.42f));
            EnsurePart(root, "Skin_Ear_R", PrimitiveType.Sphere, new Vector3(0.28f, 0.68f, 0.10f), new Vector3(0.08f, 0.16f, 0.04f), new Vector3(0f, 0f, -18f), new Color(0.72f, 0.56f, 0.42f));
            EnsurePart(root, "Eye_L", PrimitiveType.Sphere, new Vector3(-0.12f, 0.70f, 0.45f), new Vector3(0.08f, 0.04f, 0.04f), Vector3.zero, new Color(0.25f, 0.58f, 0.92f));
            EnsurePart(root, "Eye_R", PrimitiveType.Sphere, new Vector3(0.12f, 0.70f, 0.45f), new Vector3(0.08f, 0.04f, 0.04f), Vector3.zero, new Color(0.25f, 0.58f, 0.92f));
            EnsurePart(root, "Brow_L", PrimitiveType.Cube, new Vector3(-0.12f, 0.78f, 0.47f), new Vector3(0.13f, 0.025f, 0.025f), new Vector3(0f, 0f, 8f), new Color(0.08f, 0.06f, 0.04f));
            EnsurePart(root, "Brow_R", PrimitiveType.Cube, new Vector3(0.12f, 0.78f, 0.47f), new Vector3(0.13f, 0.025f, 0.025f), new Vector3(0f, 0f, -8f), new Color(0.08f, 0.06f, 0.04f));
            EnsurePart(root, "FaceMark", PrimitiveType.Cube, new Vector3(0f, 0.61f, 0.48f), new Vector3(0.22f, 0.035f, 0.025f), Vector3.zero, new Color(0.85f, 0.62f, 0.18f));

            EnsurePart(root, "Helmet", PrimitiveType.Sphere, new Vector3(0f, 0.88f, 0.02f), new Vector3(0.62f, 0.30f, 0.54f), Vector3.zero, new Color(0.42f, 0.45f, 0.48f));
            EnsurePart(root, "Hood", PrimitiveType.Sphere, new Vector3(0f, 0.82f, -0.02f), new Vector3(0.66f, 0.42f, 0.58f), Vector3.zero, new Color(0.12f, 0.16f, 0.32f));
            EnsurePart(root, "ChestArmor", PrimitiveType.Cube, new Vector3(0f, 0.13f, 0.02f), new Vector3(0.92f, 0.74f, 0.32f), Vector3.zero, new Color(0.20f, 0.40f, 1.00f));
            EnsurePart(root, "RobePanel", PrimitiveType.Cube, new Vector3(0f, -0.32f, 0.15f), new Vector3(0.62f, 0.82f, 0.07f), Vector3.zero, new Color(0.12f, 0.16f, 0.32f));
            EnsurePart(root, "ArmorTrim_L", PrimitiveType.Cube, new Vector3(-0.36f, 0.12f, 0.22f), new Vector3(0.055f, 0.82f, 0.08f), Vector3.zero, new Color(0.82f, 0.65f, 0.22f));
            EnsurePart(root, "ArmorTrim_R", PrimitiveType.Cube, new Vector3(0.36f, 0.12f, 0.22f), new Vector3(0.055f, 0.82f, 0.08f), Vector3.zero, new Color(0.82f, 0.65f, 0.22f));
            EnsurePart(root, "Belt", PrimitiveType.Cube, new Vector3(0f, -0.24f, 0.20f), new Vector3(0.78f, 0.12f, 0.12f), Vector3.zero, new Color(0.24f, 0.18f, 0.10f));
            EnsurePart(root, "Shoulder_L", PrimitiveType.Sphere, new Vector3(-0.55f, 0.36f, 0.02f), new Vector3(0.26f, 0.20f, 0.28f), Vector3.zero, new Color(0.42f, 0.45f, 0.48f));
            EnsurePart(root, "Shoulder_R", PrimitiveType.Sphere, new Vector3(0.55f, 0.36f, 0.02f), new Vector3(0.26f, 0.20f, 0.28f), Vector3.zero, new Color(0.42f, 0.45f, 0.48f));
            EnsurePart(root, "Glove_L", PrimitiveType.Cube, new Vector3(-0.48f, -0.25f, 0.08f), new Vector3(0.18f, 0.24f, 0.18f), Vector3.zero, new Color(0.24f, 0.25f, 0.27f));
            EnsurePart(root, "Glove_R", PrimitiveType.Cube, new Vector3(0.48f, -0.25f, 0.08f), new Vector3(0.18f, 0.24f, 0.18f), Vector3.zero, new Color(0.24f, 0.25f, 0.27f));
            EnsurePart(root, "Knee_L", PrimitiveType.Sphere, new Vector3(-0.20f, -0.56f, 0.16f), new Vector3(0.17f, 0.11f, 0.11f), Vector3.zero, new Color(0.42f, 0.45f, 0.48f));
            EnsurePart(root, "Knee_R", PrimitiveType.Sphere, new Vector3(0.20f, -0.56f, 0.16f), new Vector3(0.17f, 0.11f, 0.11f), Vector3.zero, new Color(0.42f, 0.45f, 0.48f));
            EnsurePart(root, "Boot_L", PrimitiveType.Cube, new Vector3(-0.22f, -0.86f, 0.10f), new Vector3(0.25f, 0.22f, 0.35f), Vector3.zero, new Color(0.14f, 0.12f, 0.10f));
            EnsurePart(root, "Boot_R", PrimitiveType.Cube, new Vector3(0.22f, -0.86f, 0.10f), new Vector3(0.25f, 0.22f, 0.35f), Vector3.zero, new Color(0.14f, 0.12f, 0.10f));
            EnsurePart(root, "Cape", PrimitiveType.Cube, new Vector3(0f, 0.06f, -0.48f), new Vector3(0.78f, 1.18f, 0.08f), Vector3.zero, new Color(0.12f, 0.16f, 0.32f));
            EnsurePart(root, "Weapon_Main", PrimitiveType.Cylinder, new Vector3(0.72f, 0.00f, 0.16f), new Vector3(0.06f, 0.70f, 0.06f), new Vector3(0f, 0f, 34f), new Color(0.74f, 0.76f, 0.78f));
            EnsurePart(root, "Weapon_Head", PrimitiveType.Cube, new Vector3(0.78f, 0.54f, 0.18f), new Vector3(0.28f, 0.18f, 0.10f), new Vector3(0f, 0f, 34f), new Color(0.74f, 0.76f, 0.78f));
            EnsurePart(root, "Bow_String", PrimitiveType.Cube, new Vector3(0.58f, 0.10f, 0.16f), new Vector3(0.025f, 0.78f, 0.025f), new Vector3(0f, 0f, 78f), new Color(0.92f, 0.88f, 0.72f));
            EnsurePart(root, "Shield_Off", PrimitiveType.Cube, new Vector3(-0.72f, 0.02f, 0.18f), new Vector3(0.12f, 0.56f, 0.42f), Vector3.zero, new Color(0.35f, 0.38f, 0.42f));
            EnsurePart(root, "Orb_Off", PrimitiveType.Sphere, new Vector3(-0.72f, 0.08f, 0.22f), new Vector3(0.24f, 0.24f, 0.24f), Vector3.zero, new Color(0.35f, 0.70f, 1.00f));
            EnsurePart(root, "Weapon_Off", PrimitiveType.Cylinder, new Vector3(-0.72f, -0.02f, 0.16f), new Vector3(0.045f, 0.45f, 0.045f), new Vector3(0f, 0f, -34f), new Color(0.74f, 0.76f, 0.78f));
            EnsurePart(root, "Tome_Off", PrimitiveType.Cube, new Vector3(-0.72f, 0.10f, 0.22f), new Vector3(0.30f, 0.36f, 0.08f), new Vector3(0f, 0f, -10f), new Color(0.20f, 0.12f, 0.34f));
            EnsurePart(root, "BackAttachment", PrimitiveType.Cube, new Vector3(0f, 0.34f, -0.60f), new Vector3(0.20f, 0.75f, 0.08f), Vector3.zero, new Color(0.82f, 0.65f, 0.22f));

            EnsureAnchor(root, "VFX_ChestAnchor", new Vector3(0f, 0.48f, 0.38f));
            EnsureAnchor(root, "VFX_Hand_L", new Vector3(-0.55f, -0.10f, 0.20f));
            EnsureAnchor(root, "VFX_Hand_R", new Vector3(0.55f, -0.10f, 0.20f));
            EnsureAnchor(root, "PetAnchor", new Vector3(-0.95f, -0.50f, -0.20f));
            EnsureAnchor(root, "MountAnchor", new Vector3(0f, -0.88f, 0f));
        }

        private static GameObject EnsurePart(Transform root, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Color color)
        {
            Transform existing = root.Find(name);
            bool created = existing == null;
            GameObject part = existing != null ? existing.gameObject : GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEulerAngles);
            part.transform.localScale = localScale;

            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null && created)
            {
                renderer.material.color = color;
            }

            return part;
        }

        private static void EnsureAnchor(Transform root, string name, Vector3 localPosition)
        {
            Transform existing = root.Find(name);
            if (existing != null)
            {
                existing.localPosition = localPosition;
                return;
            }

            var anchor = new GameObject(name);
            anchor.transform.SetParent(root, false);
            anchor.transform.localPosition = localPosition;
        }

        private static void RemoveLegacyPart(Transform root, string name)
        {
            Transform legacy = root.Find(name);
            if (legacy != null)
            {
                Object.Destroy(legacy.gameObject);
            }
        }
    }
}
