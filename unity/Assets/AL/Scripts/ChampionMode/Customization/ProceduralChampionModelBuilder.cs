using UnityEngine;

namespace AL.ChampionMode.Customization
{
    public static class ProceduralChampionModelBuilder
    {
        public static void EnsureModel(GameObject champion, bool fullDetail = true)
        {
            if (champion == null)
            {
                return;
            }

            HideRootDebugRenderer(champion);
            Transform root = champion.transform;
            RemoveLegacyPart(root, "Hair");

            EnsureSkin(root);
            EnsureHair(root);
            EnsureArmor(root);
            EnsureWeapons(root);
            if (fullDetail)
            {
                // Hero/prestige layers are cosmetic micro-detail (~80 extra primitive
                // parts each with their own material) that only the player champion and
                // hero showcase need. Crowd bots pass fullDetail:false to keep their
                // primitive/material count down without losing the base body, armor,
                // weapon, or realm identity that bot behavior and LOD rely on.
                EnsureHeroDetailLayer(root);
                EnsurePrestigeSilhouetteLayer(root);
            }
            EnsureAnchors(root);
            var motion = champion.GetComponent<ProceduralChampionMotion>() ?? champion.AddComponent<ProceduralChampionMotion>();
            motion.Rebind();
            var surfaceResponse = champion.GetComponent<ProceduralChampionSurfaceResponse>() ?? champion.AddComponent<ProceduralChampionSurfaceResponse>();
            surfaceResponse.Rebind();
        }

        private static void EnsureSkin(Transform root)
        {
            var skin = new Color(0.72f, 0.56f, 0.42f);
            var shadowSkin = Color.Lerp(skin, new Color(0.38f, 0.26f, 0.20f), 0.24f);

            EnsurePart(root, "Skin_Head", PrimitiveType.Sphere, new Vector3(0f, 0.70f, 0.07f), new Vector3(0.42f, 0.48f, 0.39f), Vector3.zero, skin, 0f, 0.32f);
            EnsurePart(root, "Skin_Jaw", PrimitiveType.Cube, new Vector3(0f, 0.50f, 0.16f), new Vector3(0.28f, 0.16f, 0.22f), Vector3.zero, shadowSkin, 0f, 0.28f);
            EnsurePart(root, "Skin_Neck", PrimitiveType.Cube, new Vector3(0f, 0.30f, 0.06f), new Vector3(0.22f, 0.24f, 0.18f), Vector3.zero, skin, 0f, 0.28f);
            EnsurePart(root, "Skin_Nose", PrimitiveType.Cube, new Vector3(0f, 0.68f, 0.46f), new Vector3(0.07f, 0.08f, 0.08f), Vector3.zero, shadowSkin, 0f, 0.22f);
            EnsurePart(root, "Skin_Shadow_Cheek_L", PrimitiveType.Cube, new Vector3(-0.18f, 0.61f, 0.45f), new Vector3(0.12f, 0.035f, 0.026f), new Vector3(0f, 0f, -12f), shadowSkin, 0f, 0.24f);
            EnsurePart(root, "Skin_Shadow_Cheek_R", PrimitiveType.Cube, new Vector3(0.18f, 0.61f, 0.45f), new Vector3(0.12f, 0.035f, 0.026f), new Vector3(0f, 0f, 12f), shadowSkin, 0f, 0.24f);
            EnsurePart(root, "Skin_Shadow_Mouth", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.48f), new Vector3(0.18f, 0.024f, 0.026f), Vector3.zero, Color.Lerp(shadowSkin, Color.black, 0.30f), 0f, 0.20f);
            EnsurePart(root, "Skin_LowerLip", PrimitiveType.Cube, new Vector3(0f, 0.52f, 0.47f), new Vector3(0.13f, 0.018f, 0.022f), Vector3.zero, Color.Lerp(skin, new Color(0.58f, 0.32f, 0.30f), 0.28f), 0f, 0.26f);
            EnsurePart(root, "Skin_Ear_L", PrimitiveType.Sphere, new Vector3(-0.32f, 0.70f, 0.06f), new Vector3(0.08f, 0.17f, 0.04f), new Vector3(0f, 0f, 18f), skin, 0f, 0.28f);
            EnsurePart(root, "Skin_Ear_R", PrimitiveType.Sphere, new Vector3(0.32f, 0.70f, 0.06f), new Vector3(0.08f, 0.17f, 0.04f), new Vector3(0f, 0f, -18f), skin, 0f, 0.28f);
            EnsurePart(root, "Eye_L", PrimitiveType.Sphere, new Vector3(-0.12f, 0.70f, 0.45f), new Vector3(0.075f, 0.04f, 0.035f), Vector3.zero, new Color(0.25f, 0.58f, 0.92f), 0f, 0.72f, 0.18f);
            EnsurePart(root, "Eye_R", PrimitiveType.Sphere, new Vector3(0.12f, 0.70f, 0.45f), new Vector3(0.075f, 0.04f, 0.035f), Vector3.zero, new Color(0.25f, 0.58f, 0.92f), 0f, 0.72f, 0.18f);
            EnsurePart(root, "Brow_L", PrimitiveType.Cube, new Vector3(-0.12f, 0.79f, 0.45f), new Vector3(0.14f, 0.026f, 0.028f), new Vector3(0f, 0f, 8f), new Color(0.08f, 0.06f, 0.04f), 0f, 0.35f);
            EnsurePart(root, "Brow_R", PrimitiveType.Cube, new Vector3(0.12f, 0.79f, 0.45f), new Vector3(0.14f, 0.026f, 0.028f), new Vector3(0f, 0f, -8f), new Color(0.08f, 0.06f, 0.04f), 0f, 0.35f);
            EnsurePart(root, "FaceMark", PrimitiveType.Cube, new Vector3(0f, 0.61f, 0.49f), new Vector3(0.22f, 0.035f, 0.025f), Vector3.zero, new Color(0.85f, 0.62f, 0.18f), 0f, 0.62f, 0.2f);
            EnsurePart(root, "Skin_ChinPlane", PrimitiveType.Cube, new Vector3(0f, 0.43f, 0.35f), new Vector3(0.20f, 0.045f, 0.035f), Vector3.zero, Color.Lerp(shadowSkin, skin, 0.24f), 0f, 0.24f);
            EnsurePart(root, "Skin_Cheekbone_L", PrimitiveType.Cube, new Vector3(-0.18f, 0.66f, 0.47f), new Vector3(0.14f, 0.024f, 0.026f), new Vector3(0f, 0f, -16f), Color.Lerp(skin, Color.white, 0.08f), 0f, 0.26f);
            EnsurePart(root, "Skin_Cheekbone_R", PrimitiveType.Cube, new Vector3(0.18f, 0.66f, 0.47f), new Vector3(0.14f, 0.024f, 0.026f), new Vector3(0f, 0f, 16f), Color.Lerp(skin, Color.white, 0.08f), 0f, 0.26f);
            EnsurePart(root, "Skin_EyeSocket_L", PrimitiveType.Cube, new Vector3(-0.12f, 0.72f, 0.475f), new Vector3(0.14f, 0.028f, 0.020f), Vector3.zero, Color.Lerp(shadowSkin, Color.black, 0.16f), 0f, 0.22f);
            EnsurePart(root, "Skin_EyeSocket_R", PrimitiveType.Cube, new Vector3(0.12f, 0.72f, 0.475f), new Vector3(0.14f, 0.028f, 0.020f), Vector3.zero, Color.Lerp(shadowSkin, Color.black, 0.16f), 0f, 0.22f);
            EnsurePart(root, "Eye_Glint_L", PrimitiveType.Sphere, new Vector3(-0.105f, 0.714f, 0.478f), new Vector3(0.024f, 0.018f, 0.012f), Vector3.zero, Color.white, 0f, 0.88f, 0.22f);
            EnsurePart(root, "Eye_Glint_R", PrimitiveType.Sphere, new Vector3(0.135f, 0.714f, 0.478f), new Vector3(0.024f, 0.018f, 0.012f), Vector3.zero, Color.white, 0f, 0.88f, 0.22f);
            EnsurePart(root, "Skin_Shadow_BrowPlane_L", PrimitiveType.Cube, new Vector3(-0.13f, 0.758f, 0.486f), new Vector3(0.155f, 0.018f, 0.018f), new Vector3(0f, 0f, 8f), Color.Lerp(shadowSkin, Color.black, 0.14f), 0f, 0.22f);
            EnsurePart(root, "Skin_Shadow_BrowPlane_R", PrimitiveType.Cube, new Vector3(0.13f, 0.758f, 0.486f), new Vector3(0.155f, 0.018f, 0.018f), new Vector3(0f, 0f, -8f), Color.Lerp(shadowSkin, Color.black, 0.14f), 0f, 0.22f);
            EnsurePart(root, "Skin_Shadow_NoseBridge", PrimitiveType.Cube, new Vector3(0f, 0.715f, 0.492f), new Vector3(0.038f, 0.116f, 0.018f), Vector3.zero, Color.Lerp(shadowSkin, Color.black, 0.12f), 0f, 0.22f);
            EnsurePart(root, "Skin_Shadow_Nostril_L", PrimitiveType.Cube, new Vector3(-0.035f, 0.628f, 0.505f), new Vector3(0.030f, 0.018f, 0.016f), new Vector3(0f, 0f, -8f), Color.Lerp(shadowSkin, Color.black, 0.34f), 0f, 0.20f);
            EnsurePart(root, "Skin_Shadow_Nostril_R", PrimitiveType.Cube, new Vector3(0.035f, 0.628f, 0.505f), new Vector3(0.030f, 0.018f, 0.016f), new Vector3(0f, 0f, 8f), Color.Lerp(shadowSkin, Color.black, 0.34f), 0f, 0.20f);
            EnsurePart(root, "Skin_Shadow_Temple_L", PrimitiveType.Cube, new Vector3(-0.285f, 0.69f, 0.30f), new Vector3(0.040f, 0.16f, 0.026f), new Vector3(0f, 0f, -12f), Color.Lerp(shadowSkin, Color.black, 0.12f), 0f, 0.22f);
            EnsurePart(root, "Skin_Shadow_Temple_R", PrimitiveType.Cube, new Vector3(0.285f, 0.69f, 0.30f), new Vector3(0.040f, 0.16f, 0.026f), new Vector3(0f, 0f, 12f), Color.Lerp(shadowSkin, Color.black, 0.12f), 0f, 0.22f);
            EnsurePart(root, "Skin_Shadow_NeckTendon_L", PrimitiveType.Cube, new Vector3(-0.075f, 0.26f, 0.23f), new Vector3(0.030f, 0.22f, 0.018f), new Vector3(0f, 0f, -7f), Color.Lerp(shadowSkin, Color.black, 0.18f), 0f, 0.22f);
            EnsurePart(root, "Skin_Shadow_NeckTendon_R", PrimitiveType.Cube, new Vector3(0.075f, 0.26f, 0.23f), new Vector3(0.030f, 0.22f, 0.018f), new Vector3(0f, 0f, 7f), Color.Lerp(shadowSkin, Color.black, 0.18f), 0f, 0.22f);
            EnsurePart(root, "FaceMark_Secondary", PrimitiveType.Cube, new Vector3(0.0f, 0.64f, 0.492f), new Vector3(0.18f, 0.026f, 0.024f), Vector3.zero, new Color(0.85f, 0.62f, 0.18f), 0f, 0.62f, 0.16f);
            EnsurePart(root, "FaceMark_Tertiary", PrimitiveType.Cube, new Vector3(0.0f, 0.58f, 0.494f), new Vector3(0.16f, 0.024f, 0.024f), Vector3.zero, new Color(0.85f, 0.62f, 0.18f), 0f, 0.62f, 0.16f);
            EnsurePart(root, "FacialHair_Mustache", PrimitiveType.Cube, new Vector3(0f, 0.57f, 0.50f), new Vector3(0.28f, 0.040f, 0.030f), Vector3.zero, new Color(0.08f, 0.06f, 0.04f), 0f, 0.36f);
            EnsurePart(root, "FacialHair_Chin", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0.42f), new Vector3(0.24f, 0.11f, 0.055f), Vector3.zero, new Color(0.08f, 0.06f, 0.04f), 0f, 0.34f);
            EnsurePart(root, "FacialHair_Jaw_L", PrimitiveType.Cube, new Vector3(-0.18f, 0.50f, 0.42f), new Vector3(0.075f, 0.18f, 0.050f), new Vector3(0f, 0f, -10f), new Color(0.08f, 0.06f, 0.04f), 0f, 0.34f);
            EnsurePart(root, "FacialHair_Jaw_R", PrimitiveType.Cube, new Vector3(0.18f, 0.50f, 0.42f), new Vector3(0.075f, 0.18f, 0.050f), new Vector3(0f, 0f, 10f), new Color(0.08f, 0.06f, 0.04f), 0f, 0.34f);
        }

        private static void EnsureHair(Transform root)
        {
            var hair = new Color(0.08f, 0.06f, 0.04f);
            var hairAccent = new Color(0.18f, 0.12f, 0.08f);

            EnsurePart(root, "Hair_Short", PrimitiveType.Sphere, new Vector3(0f, 0.93f, -0.03f), new Vector3(0.54f, 0.23f, 0.44f), Vector3.zero, hair, 0f, 0.38f);
            EnsurePart(root, "Hair_Short_Front", PrimitiveType.Cube, new Vector3(0f, 0.82f, 0.34f), new Vector3(0.34f, 0.07f, 0.08f), new Vector3(0f, 0f, -4f), hair, 0f, 0.34f);
            EnsurePart(root, "Hair_Long", PrimitiveType.Cube, new Vector3(0f, 0.67f, -0.35f), new Vector3(0.58f, 0.76f, 0.12f), Vector3.zero, hair, 0f, 0.32f);
            EnsurePart(root, "Hair_Long_Side_L", PrimitiveType.Cube, new Vector3(-0.31f, 0.58f, -0.10f), new Vector3(0.08f, 0.52f, 0.10f), new Vector3(0f, 0f, -8f), hair, 0f, 0.32f);
            EnsurePart(root, "Hair_Long_Side_R", PrimitiveType.Cube, new Vector3(0.31f, 0.58f, -0.10f), new Vector3(0.08f, 0.52f, 0.10f), new Vector3(0f, 0f, 8f), hair, 0f, 0.32f);
            EnsurePart(root, "Hair_Braid", PrimitiveType.Cylinder, new Vector3(0f, 0.27f, -0.44f), new Vector3(0.10f, 0.48f, 0.10f), Vector3.zero, hair, 0f, 0.34f);
            EnsurePart(root, "Hair_Braid_Band", PrimitiveType.Cube, new Vector3(0f, -0.10f, -0.44f), new Vector3(0.18f, 0.045f, 0.08f), Vector3.zero, hairAccent, 0f, 0.5f);
            EnsurePart(root, "Hair_Mohawk", PrimitiveType.Cube, new Vector3(0f, 1.05f, -0.02f), new Vector3(0.16f, 0.40f, 0.58f), Vector3.zero, hair, 0f, 0.34f);
            EnsurePart(root, "Hair_Mohawk_Tip", PrimitiveType.Cube, new Vector3(0f, 1.26f, -0.02f), new Vector3(0.12f, 0.16f, 0.42f), Vector3.zero, hairAccent, 0f, 0.42f);
            EnsurePart(root, "Hair_Topknot", PrimitiveType.Sphere, new Vector3(0f, 1.16f, -0.06f), new Vector3(0.24f, 0.24f, 0.24f), Vector3.zero, hair, 0f, 0.36f);
            EnsurePart(root, "Hair_Topknot_Tail", PrimitiveType.Cylinder, new Vector3(0f, 0.91f, -0.34f), new Vector3(0.075f, 0.34f, 0.075f), Vector3.zero, hair, 0f, 0.34f);
            EnsurePart(root, "Hair_Topknot_Band", PrimitiveType.Cube, new Vector3(0f, 1.03f, -0.16f), new Vector3(0.18f, 0.045f, 0.10f), Vector3.zero, hairAccent, 0f, 0.5f);
            EnsurePart(root, "Hair_Short_Fade_L", PrimitiveType.Cube, new Vector3(-0.30f, 0.76f, 0.08f), new Vector3(0.045f, 0.24f, 0.16f), new Vector3(0f, 0f, -8f), hairAccent, 0f, 0.34f);
            EnsurePart(root, "Hair_Short_Fade_R", PrimitiveType.Cube, new Vector3(0.30f, 0.76f, 0.08f), new Vector3(0.045f, 0.24f, 0.16f), new Vector3(0f, 0f, 8f), hairAccent, 0f, 0.34f);
            EnsurePart(root, "Hair_Long_Fold_L", PrimitiveType.Cube, new Vector3(-0.22f, 0.56f, -0.40f), new Vector3(0.09f, 0.62f, 0.065f), new Vector3(0f, 0f, -8f), hairAccent, 0f, 0.34f);
            EnsurePart(root, "Hair_Long_Fold_R", PrimitiveType.Cube, new Vector3(0.22f, 0.56f, -0.40f), new Vector3(0.09f, 0.62f, 0.065f), new Vector3(0f, 0f, 8f), hairAccent, 0f, 0.34f);
            EnsurePart(root, "Hair_Braid_Segment_Upper", PrimitiveType.Sphere, new Vector3(0f, 0.48f, -0.45f), new Vector3(0.13f, 0.11f, 0.10f), Vector3.zero, hairAccent, 0f, 0.36f);
            EnsurePart(root, "Hair_Braid_Segment_Mid", PrimitiveType.Sphere, new Vector3(0f, 0.22f, -0.45f), new Vector3(0.115f, 0.10f, 0.09f), Vector3.zero, hair, 0f, 0.36f);
            EnsurePart(root, "Hair_Braid_Segment_Lower", PrimitiveType.Sphere, new Vector3(0f, -0.02f, -0.45f), new Vector3(0.095f, 0.085f, 0.075f), Vector3.zero, hairAccent, 0f, 0.36f);
            EnsurePart(root, "Hair_Mohawk_Ridge", PrimitiveType.Cube, new Vector3(0f, 1.02f, 0.22f), new Vector3(0.09f, 0.32f, 0.16f), Vector3.zero, hairAccent, 0f, 0.42f);
            EnsurePart(root, "Hair_Topknot_Pin", PrimitiveType.Cylinder, new Vector3(0f, 1.16f, -0.02f), new Vector3(0.035f, 0.34f, 0.035f), new Vector3(0f, 0f, 90f), hairAccent, 0f, 0.54f);
        }

        private static void EnsureArmor(Transform root)
        {
            var primary = new Color(0.20f, 0.40f, 1.00f);
            var darkPlate = new Color(0.10f, 0.13f, 0.18f);
            var metal = new Color(0.44f, 0.47f, 0.50f);
            var accent = new Color(0.82f, 0.65f, 0.22f);
            var leather = new Color(0.18f, 0.12f, 0.08f);
            var cloth = new Color(0.12f, 0.16f, 0.32f);

            EnsurePart(root, "Helmet", PrimitiveType.Sphere, new Vector3(0f, 0.88f, 0.01f), new Vector3(0.64f, 0.30f, 0.54f), Vector3.zero, metal, 0.48f, 0.64f);
            EnsurePart(root, "Helmet_BrowGuard", PrimitiveType.Cube, new Vector3(0f, 0.78f, 0.40f), new Vector3(0.46f, 0.06f, 0.08f), Vector3.zero, accent, 0.3f, 0.62f, 0.08f);
            EnsurePart(root, "Helmet_CheekGuard_L", PrimitiveType.Cube, new Vector3(-0.26f, 0.62f, 0.34f), new Vector3(0.06f, 0.24f, 0.055f), new Vector3(0f, 0f, -10f), metal, 0.44f, 0.62f);
            EnsurePart(root, "Helmet_CheekGuard_R", PrimitiveType.Cube, new Vector3(0.26f, 0.62f, 0.34f), new Vector3(0.06f, 0.24f, 0.055f), new Vector3(0f, 0f, 10f), metal, 0.44f, 0.62f);
            EnsurePart(root, "Helmet_Crest", PrimitiveType.Cube, new Vector3(0f, 1.08f, -0.03f), new Vector3(0.10f, 0.18f, 0.42f), Vector3.zero, accent, 0.34f, 0.68f, 0.08f);
            EnsurePart(root, "Hood", PrimitiveType.Sphere, new Vector3(0f, 0.82f, -0.02f), new Vector3(0.66f, 0.42f, 0.58f), Vector3.zero, cloth, 0f, 0.46f);
            EnsurePart(root, "Assassin_Mask", PrimitiveType.Cube, new Vector3(0f, 0.60f, 0.45f), new Vector3(0.34f, 0.16f, 0.042f), Vector3.zero, Color.Lerp(cloth, Color.black, 0.24f), 0f, 0.48f);

            EnsurePart(root, "ChestArmor", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.02f), new Vector3(0.92f, 0.76f, 0.32f), Vector3.zero, primary, 0.36f, 0.58f);
            EnsurePart(root, "Cloth_Undersuit_Torso", PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.03f), new Vector3(0.78f, 0.92f, 0.24f), Vector3.zero, Color.Lerp(cloth, Color.black, 0.18f), 0f, 0.40f);
            EnsurePart(root, "Armor_Pectoral_L", PrimitiveType.Cube, new Vector3(-0.18f, 0.25f, 0.24f), new Vector3(0.30f, 0.28f, 0.08f), new Vector3(0f, 0f, 4f), Color.Lerp(primary, Color.white, 0.12f), 0.38f, 0.62f);
            EnsurePart(root, "Armor_Pectoral_R", PrimitiveType.Cube, new Vector3(0.18f, 0.25f, 0.24f), new Vector3(0.30f, 0.28f, 0.08f), new Vector3(0f, 0f, -4f), Color.Lerp(primary, Color.white, 0.12f), 0.38f, 0.62f);
            EnsurePart(root, "Armor_Collar", PrimitiveType.Cube, new Vector3(0f, 0.48f, 0.12f), new Vector3(0.64f, 0.10f, 0.20f), Vector3.zero, darkPlate, 0.42f, 0.56f);
            EnsurePart(root, "Armor_Gorget_Front", PrimitiveType.Cube, new Vector3(0f, 0.40f, 0.30f), new Vector3(0.34f, 0.070f, 0.050f), Vector3.zero, metal, 0.48f, 0.66f, 0.04f);
            EnsurePart(root, "Armor_Gorget_L", PrimitiveType.Cube, new Vector3(-0.22f, 0.42f, 0.24f), new Vector3(0.20f, 0.060f, 0.046f), new Vector3(0f, 0f, -14f), metal, 0.48f, 0.64f, 0.03f);
            EnsurePart(root, "Armor_Gorget_R", PrimitiveType.Cube, new Vector3(0.22f, 0.42f, 0.24f), new Vector3(0.20f, 0.060f, 0.046f), new Vector3(0f, 0f, 14f), metal, 0.48f, 0.64f, 0.03f);
            EnsurePart(root, "Armor_Shadow_CollarUnder", PrimitiveType.Cube, new Vector3(0f, 0.365f, 0.255f), new Vector3(0.50f, 0.034f, 0.032f), Vector3.zero, Color.Lerp(darkPlate, Color.black, 0.34f), 0.30f, 0.42f);
            EnsurePart(root, "Armor_CollarRivet_L", PrimitiveType.Sphere, new Vector3(-0.28f, 0.47f, 0.285f), new Vector3(0.032f, 0.032f, 0.022f), Vector3.zero, accent, 0.32f, 0.72f, 0.08f);
            EnsurePart(root, "Armor_CollarRivet_R", PrimitiveType.Sphere, new Vector3(0.28f, 0.47f, 0.285f), new Vector3(0.032f, 0.032f, 0.022f), Vector3.zero, accent, 0.32f, 0.72f, 0.08f);
            EnsurePart(root, "Armor_AbPlate", PrimitiveType.Cube, new Vector3(0f, -0.12f, 0.23f), new Vector3(0.46f, 0.26f, 0.07f), Vector3.zero, darkPlate, 0.38f, 0.52f);
            EnsurePart(root, "Armor_SternumPlate", PrimitiveType.Cube, new Vector3(0f, 0.16f, 0.335f), new Vector3(0.12f, 0.44f, 0.045f), Vector3.zero, darkPlate, 0.40f, 0.58f);
            EnsurePart(root, "Armor_Etching_L", PrimitiveType.Cube, new Vector3(-0.17f, 0.28f, 0.335f), new Vector3(0.16f, 0.022f, 0.026f), new Vector3(0f, 0f, -18f), accent, 0.24f, 0.70f, 0.08f);
            EnsurePart(root, "Armor_Etching_R", PrimitiveType.Cube, new Vector3(0.17f, 0.28f, 0.335f), new Vector3(0.16f, 0.022f, 0.026f), new Vector3(0f, 0f, 18f), accent, 0.24f, 0.70f, 0.08f);
            EnsurePart(root, "Armor_Rivet_L_Upper", PrimitiveType.Sphere, new Vector3(-0.34f, 0.39f, 0.30f), new Vector3(0.034f, 0.034f, 0.026f), Vector3.zero, accent, 0.30f, 0.72f, 0.08f);
            EnsurePart(root, "Armor_Rivet_R_Upper", PrimitiveType.Sphere, new Vector3(0.34f, 0.39f, 0.30f), new Vector3(0.034f, 0.034f, 0.026f), Vector3.zero, accent, 0.30f, 0.72f, 0.08f);
            EnsurePart(root, "Armor_Rivet_L_Lower", PrimitiveType.Sphere, new Vector3(-0.32f, -0.10f, 0.30f), new Vector3(0.030f, 0.030f, 0.024f), Vector3.zero, accent, 0.30f, 0.72f, 0.08f);
            EnsurePart(root, "Armor_Rivet_R_Lower", PrimitiveType.Sphere, new Vector3(0.32f, -0.10f, 0.30f), new Vector3(0.030f, 0.030f, 0.024f), Vector3.zero, accent, 0.30f, 0.72f, 0.08f);
            EnsurePart(root, "Armor_Rib_L_Upper", PrimitiveType.Cube, new Vector3(-0.24f, 0.15f, 0.29f), new Vector3(0.25f, 0.040f, 0.045f), new Vector3(0f, 0f, -8f), accent, 0.28f, 0.68f, 0.08f);
            EnsurePart(root, "Armor_Rib_R_Upper", PrimitiveType.Cube, new Vector3(0.24f, 0.15f, 0.29f), new Vector3(0.25f, 0.040f, 0.045f), new Vector3(0f, 0f, 8f), accent, 0.28f, 0.68f, 0.08f);
            EnsurePart(root, "Armor_Rib_L_Lower", PrimitiveType.Cube, new Vector3(-0.21f, -0.02f, 0.29f), new Vector3(0.22f, 0.036f, 0.043f), new Vector3(0f, 0f, -6f), Color.Lerp(accent, darkPlate, 0.28f), 0.26f, 0.62f, 0.06f);
            EnsurePart(root, "Armor_Rib_R_Lower", PrimitiveType.Cube, new Vector3(0.21f, -0.02f, 0.29f), new Vector3(0.22f, 0.036f, 0.043f), new Vector3(0f, 0f, 6f), Color.Lerp(accent, darkPlate, 0.28f), 0.26f, 0.62f, 0.06f);
            EnsurePart(root, "Armor_CenterGem", PrimitiveType.Sphere, new Vector3(0f, 0.22f, 0.34f), new Vector3(0.105f, 0.105f, 0.050f), Vector3.zero, accent, 0.18f, 0.78f, 0.28f);
            EnsurePart(root, "RobePanel", PrimitiveType.Cube, new Vector3(0f, -0.34f, 0.18f), new Vector3(0.62f, 0.84f, 0.07f), Vector3.zero, cloth, 0f, 0.48f);
            EnsurePart(root, "RobeBackPanel", PrimitiveType.Cube, new Vector3(0f, -0.32f, -0.24f), new Vector3(0.66f, 0.86f, 0.06f), Vector3.zero, cloth, 0f, 0.44f);
            EnsurePart(root, "RobeSleeve_L", PrimitiveType.Cube, new Vector3(-0.54f, 0.02f, 0.03f), new Vector3(0.18f, 0.70f, 0.20f), new Vector3(0f, 0f, -8f), cloth, 0f, 0.44f);
            EnsurePart(root, "RobeSleeve_R", PrimitiveType.Cube, new Vector3(0.54f, 0.02f, 0.03f), new Vector3(0.18f, 0.70f, 0.20f), new Vector3(0f, 0f, 8f), cloth, 0f, 0.44f);
            EnsurePart(root, "RobeTrim_Front", PrimitiveType.Cube, new Vector3(0f, -0.34f, 0.24f), new Vector3(0.09f, 0.72f, 0.035f), Vector3.zero, accent, 0.08f, 0.66f, 0.10f);
            EnsurePart(root, "RobeTrim_Hem", PrimitiveType.Cube, new Vector3(0f, -0.72f, 0.24f), new Vector3(0.52f, 0.045f, 0.035f), Vector3.zero, accent, 0.08f, 0.66f, 0.10f);
            EnsurePart(root, "RobeTrim_Sash_L", PrimitiveType.Cube, new Vector3(-0.18f, -0.30f, 0.245f), new Vector3(0.060f, 0.68f, 0.032f), new Vector3(0f, 0f, -6f), accent, 0.08f, 0.66f, 0.10f);
            EnsurePart(root, "RobeTrim_Sash_R", PrimitiveType.Cube, new Vector3(0.18f, -0.30f, 0.245f), new Vector3(0.060f, 0.68f, 0.032f), new Vector3(0f, 0f, 6f), accent, 0.08f, 0.66f, 0.10f);
            EnsurePart(root, "Arcane_FocusHalo", PrimitiveType.Cylinder, new Vector3(0f, 0.26f, 0.39f), new Vector3(0.34f, 0.018f, 0.34f), new Vector3(90f, 0f, 0f), accent, 0.10f, 0.82f, 0.38f);

            EnsurePart(root, "ArmorTrim_L", PrimitiveType.Cube, new Vector3(-0.36f, 0.12f, 0.24f), new Vector3(0.055f, 0.82f, 0.08f), Vector3.zero, accent, 0.24f, 0.66f, 0.08f);
            EnsurePart(root, "ArmorTrim_R", PrimitiveType.Cube, new Vector3(0.36f, 0.12f, 0.24f), new Vector3(0.055f, 0.82f, 0.08f), Vector3.zero, accent, 0.24f, 0.66f, 0.08f);
            EnsurePart(root, "ArmorTrim_Center", PrimitiveType.Cube, new Vector3(0f, 0.10f, 0.28f), new Vector3(0.065f, 0.66f, 0.075f), Vector3.zero, accent, 0.24f, 0.66f, 0.08f);
            EnsurePart(root, "ArmorTrim_Collar", PrimitiveType.Cube, new Vector3(0f, 0.50f, 0.24f), new Vector3(0.54f, 0.045f, 0.08f), Vector3.zero, accent, 0.24f, 0.66f, 0.08f);

            EnsurePart(root, "Belt", PrimitiveType.Cube, new Vector3(0f, -0.25f, 0.21f), new Vector3(0.80f, 0.12f, 0.13f), Vector3.zero, leather, 0f, 0.42f);
            EnsurePart(root, "Belt_Buckle", PrimitiveType.Cube, new Vector3(0f, -0.24f, 0.31f), new Vector3(0.18f, 0.14f, 0.04f), Vector3.zero, accent, 0.32f, 0.68f, 0.1f);
            EnsurePart(root, "PlateSkirt_Front", PrimitiveType.Cube, new Vector3(0f, -0.54f, 0.19f), new Vector3(0.42f, 0.44f, 0.06f), Vector3.zero, darkPlate, 0.32f, 0.46f);
            EnsurePart(root, "Armor_HipPlate_L", PrimitiveType.Cube, new Vector3(-0.34f, -0.42f, 0.08f), new Vector3(0.18f, 0.34f, 0.08f), new Vector3(0f, 0f, -8f), metal, 0.4f, 0.58f);
            EnsurePart(root, "Armor_HipPlate_R", PrimitiveType.Cube, new Vector3(0.34f, -0.42f, 0.08f), new Vector3(0.18f, 0.34f, 0.08f), new Vector3(0f, 0f, 8f), metal, 0.4f, 0.58f);

            EnsurePart(root, "Shoulder_L", PrimitiveType.Sphere, new Vector3(-0.55f, 0.35f, 0.02f), new Vector3(0.28f, 0.20f, 0.30f), Vector3.zero, metal, 0.44f, 0.62f);
            EnsurePart(root, "Shoulder_R", PrimitiveType.Sphere, new Vector3(0.55f, 0.35f, 0.02f), new Vector3(0.28f, 0.20f, 0.30f), Vector3.zero, metal, 0.44f, 0.62f);
            EnsurePart(root, "Shoulder_Ridge_L", PrimitiveType.Cube, new Vector3(-0.55f, 0.46f, 0.04f), new Vector3(0.32f, 0.055f, 0.12f), new Vector3(0f, 0f, -12f), accent, 0.34f, 0.70f, 0.08f);
            EnsurePart(root, "Shoulder_Ridge_R", PrimitiveType.Cube, new Vector3(0.55f, 0.46f, 0.04f), new Vector3(0.32f, 0.055f, 0.12f), new Vector3(0f, 0f, 12f), accent, 0.34f, 0.70f, 0.08f);
            EnsurePart(root, "ShoulderSpike_L", PrimitiveType.Cube, new Vector3(-0.72f, 0.42f, 0.02f), new Vector3(0.20f, 0.08f, 0.08f), new Vector3(0f, 0f, -20f), accent, 0.34f, 0.66f, 0.08f);
            EnsurePart(root, "ShoulderSpike_R", PrimitiveType.Cube, new Vector3(0.72f, 0.42f, 0.02f), new Vector3(0.20f, 0.08f, 0.08f), new Vector3(0f, 0f, 20f), accent, 0.34f, 0.66f, 0.08f);
            EnsurePart(root, "Armor_UpperArm_L", PrimitiveType.Cube, new Vector3(-0.50f, 0.04f, 0.06f), new Vector3(0.18f, 0.34f, 0.18f), new Vector3(0f, 0f, -6f), darkPlate, 0.30f, 0.50f);
            EnsurePart(root, "Armor_UpperArm_R", PrimitiveType.Cube, new Vector3(0.50f, 0.04f, 0.06f), new Vector3(0.18f, 0.34f, 0.18f), new Vector3(0f, 0f, 6f), darkPlate, 0.30f, 0.50f);
            EnsurePart(root, "Armor_ForearmBlade_L", PrimitiveType.Cube, new Vector3(-0.61f, -0.23f, 0.18f), new Vector3(0.18f, 0.045f, 0.055f), new Vector3(0f, 0f, -18f), accent, 0.38f, 0.70f, 0.10f);
            EnsurePart(root, "Armor_ForearmBlade_R", PrimitiveType.Cube, new Vector3(0.61f, -0.23f, 0.18f), new Vector3(0.18f, 0.045f, 0.055f), new Vector3(0f, 0f, 18f), accent, 0.38f, 0.70f, 0.10f);
            EnsurePart(root, "Glove_L", PrimitiveType.Cube, new Vector3(-0.50f, -0.27f, 0.08f), new Vector3(0.18f, 0.26f, 0.18f), Vector3.zero, metal, 0.42f, 0.56f);
            EnsurePart(root, "Glove_R", PrimitiveType.Cube, new Vector3(0.50f, -0.27f, 0.08f), new Vector3(0.18f, 0.26f, 0.18f), Vector3.zero, metal, 0.42f, 0.56f);
            EnsurePart(root, "Glove_Cuff_L", PrimitiveType.Cube, new Vector3(-0.50f, -0.12f, 0.08f), new Vector3(0.22f, 0.07f, 0.21f), Vector3.zero, accent, 0.28f, 0.64f);
            EnsurePart(root, "Glove_Cuff_R", PrimitiveType.Cube, new Vector3(0.50f, -0.12f, 0.08f), new Vector3(0.22f, 0.07f, 0.21f), Vector3.zero, accent, 0.28f, 0.64f);
            EnsurePart(root, "Glove_Finger_L_0", PrimitiveType.Cube, new Vector3(-0.58f, -0.43f, 0.16f), new Vector3(0.045f, 0.14f, 0.040f), new Vector3(0f, 0f, -8f), metal, 0.34f, 0.56f);
            EnsurePart(root, "Glove_Finger_L_1", PrimitiveType.Cube, new Vector3(-0.50f, -0.44f, 0.18f), new Vector3(0.045f, 0.15f, 0.040f), Vector3.zero, metal, 0.34f, 0.56f);
            EnsurePart(root, "Glove_Finger_L_2", PrimitiveType.Cube, new Vector3(-0.42f, -0.43f, 0.16f), new Vector3(0.045f, 0.14f, 0.040f), new Vector3(0f, 0f, 8f), metal, 0.34f, 0.56f);
            EnsurePart(root, "Glove_Finger_R_0", PrimitiveType.Cube, new Vector3(0.42f, -0.43f, 0.16f), new Vector3(0.045f, 0.14f, 0.040f), new Vector3(0f, 0f, -8f), metal, 0.34f, 0.56f);
            EnsurePart(root, "Glove_Finger_R_1", PrimitiveType.Cube, new Vector3(0.50f, -0.44f, 0.18f), new Vector3(0.045f, 0.15f, 0.040f), Vector3.zero, metal, 0.34f, 0.56f);
            EnsurePart(root, "Glove_Finger_R_2", PrimitiveType.Cube, new Vector3(0.58f, -0.43f, 0.16f), new Vector3(0.045f, 0.14f, 0.040f), new Vector3(0f, 0f, 8f), metal, 0.34f, 0.56f);

            EnsurePart(root, "Armor_Thigh_L", PrimitiveType.Cube, new Vector3(-0.20f, -0.56f, 0.06f), new Vector3(0.20f, 0.36f, 0.18f), Vector3.zero, darkPlate, 0.32f, 0.48f);
            EnsurePart(root, "Armor_Thigh_R", PrimitiveType.Cube, new Vector3(0.20f, -0.56f, 0.06f), new Vector3(0.20f, 0.36f, 0.18f), Vector3.zero, darkPlate, 0.32f, 0.48f);
            EnsurePart(root, "ThighStrap_L", PrimitiveType.Cube, new Vector3(-0.20f, -0.50f, 0.20f), new Vector3(0.22f, 0.045f, 0.052f), Vector3.zero, leather, 0.02f, 0.40f);
            EnsurePart(root, "ThighStrap_R", PrimitiveType.Cube, new Vector3(0.20f, -0.50f, 0.20f), new Vector3(0.22f, 0.045f, 0.052f), Vector3.zero, leather, 0.02f, 0.40f);
            EnsurePart(root, "Knee_L", PrimitiveType.Sphere, new Vector3(-0.20f, -0.76f, 0.17f), new Vector3(0.17f, 0.11f, 0.11f), Vector3.zero, metal, 0.42f, 0.58f);
            EnsurePart(root, "Knee_R", PrimitiveType.Sphere, new Vector3(0.20f, -0.76f, 0.17f), new Vector3(0.17f, 0.11f, 0.11f), Vector3.zero, metal, 0.42f, 0.58f);
            EnsurePart(root, "Boot_L", PrimitiveType.Cube, new Vector3(-0.22f, -0.97f, 0.11f), new Vector3(0.25f, 0.24f, 0.36f), Vector3.zero, leather, 0.08f, 0.42f);
            EnsurePart(root, "Boot_R", PrimitiveType.Cube, new Vector3(0.22f, -0.97f, 0.11f), new Vector3(0.25f, 0.24f, 0.36f), Vector3.zero, leather, 0.08f, 0.42f);
            EnsurePart(root, "Boot_Cuff_L", PrimitiveType.Cube, new Vector3(-0.22f, -0.82f, 0.08f), new Vector3(0.28f, 0.08f, 0.24f), Vector3.zero, metal, 0.34f, 0.54f);
            EnsurePart(root, "Boot_Cuff_R", PrimitiveType.Cube, new Vector3(0.22f, -0.82f, 0.08f), new Vector3(0.28f, 0.08f, 0.24f), Vector3.zero, metal, 0.34f, 0.54f);
            EnsurePart(root, "BootStrap_L", PrimitiveType.Cube, new Vector3(-0.22f, -0.91f, 0.30f), new Vector3(0.25f, 0.040f, 0.040f), Vector3.zero, leather, 0.04f, 0.42f);
            EnsurePart(root, "BootStrap_R", PrimitiveType.Cube, new Vector3(0.22f, -0.91f, 0.30f), new Vector3(0.25f, 0.040f, 0.040f), Vector3.zero, leather, 0.04f, 0.42f);
            EnsurePart(root, "Boot_Toe_L", PrimitiveType.Cube, new Vector3(-0.22f, -1.03f, 0.31f), new Vector3(0.24f, 0.10f, 0.18f), Vector3.zero, leather, 0.12f, 0.48f);
            EnsurePart(root, "Boot_Toe_R", PrimitiveType.Cube, new Vector3(0.22f, -1.03f, 0.31f), new Vector3(0.24f, 0.10f, 0.18f), Vector3.zero, leather, 0.12f, 0.48f);
            EnsurePart(root, "Boot_Sole_L", PrimitiveType.Cube, new Vector3(-0.22f, -1.12f, 0.14f), new Vector3(0.28f, 0.045f, 0.42f), Vector3.zero, Color.Lerp(leather, Color.black, 0.28f), 0.06f, 0.36f);
            EnsurePart(root, "Boot_Sole_R", PrimitiveType.Cube, new Vector3(0.22f, -1.12f, 0.14f), new Vector3(0.28f, 0.045f, 0.42f), Vector3.zero, Color.Lerp(leather, Color.black, 0.28f), 0.06f, 0.36f);

            EnsurePart(root, "Cape", PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.50f), new Vector3(0.82f, 1.26f, 0.08f), new Vector3(8f, 0f, 0f), cloth, 0f, 0.42f);
            EnsurePart(root, "Cape_LeftFold", PrimitiveType.Cube, new Vector3(-0.32f, -0.08f, -0.54f), new Vector3(0.16f, 1.10f, 0.07f), new Vector3(8f, 0f, -6f), Color.Lerp(cloth, Color.black, 0.08f), 0f, 0.42f);
            EnsurePart(root, "Cape_RightFold", PrimitiveType.Cube, new Vector3(0.32f, -0.08f, -0.54f), new Vector3(0.16f, 1.10f, 0.07f), new Vector3(8f, 0f, 6f), Color.Lerp(cloth, Color.black, 0.08f), 0f, 0.42f);
            EnsurePart(root, "Cape_InnerShadow", PrimitiveType.Cube, new Vector3(0f, -0.08f, -0.61f), new Vector3(0.58f, 1.06f, 0.045f), new Vector3(8f, 0f, 0f), Color.Lerp(cloth, Color.black, 0.30f), 0f, 0.38f);
            EnsurePart(root, "Cape_LeftEdge", PrimitiveType.Cube, new Vector3(-0.45f, -0.10f, -0.53f), new Vector3(0.045f, 1.12f, 0.045f), new Vector3(8f, 0f, -4f), accent, 0.04f, 0.60f, 0.05f);
            EnsurePart(root, "Cape_RightEdge", PrimitiveType.Cube, new Vector3(0.45f, -0.10f, -0.53f), new Vector3(0.045f, 1.12f, 0.045f), new Vector3(8f, 0f, 4f), accent, 0.04f, 0.60f, 0.05f);
            EnsurePart(root, "Cape_Seam_Center", PrimitiveType.Cube, new Vector3(0f, -0.10f, -0.58f), new Vector3(0.050f, 1.05f, 0.045f), new Vector3(8f, 0f, 0f), accent, 0.04f, 0.60f, 0.05f);
            EnsurePart(root, "Cape_Hem", PrimitiveType.Cube, new Vector3(0f, -0.66f, -0.57f), new Vector3(0.68f, 0.055f, 0.045f), new Vector3(8f, 0f, 0f), accent, 0.04f, 0.60f, 0.05f);
            EnsurePart(root, "Cape_Rune_L", PrimitiveType.Cube, new Vector3(-0.17f, -0.34f, -0.62f), new Vector3(0.070f, 0.20f, 0.028f), new Vector3(8f, 0f, -24f), accent, 0.04f, 0.70f, 0.18f);
            EnsurePart(root, "Cape_Rune_R", PrimitiveType.Cube, new Vector3(0.17f, -0.34f, -0.62f), new Vector3(0.070f, 0.20f, 0.028f), new Vector3(8f, 0f, 24f), accent, 0.04f, 0.70f, 0.18f);
            EnsurePart(root, "Cape_Clasp", PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0.24f), new Vector3(0.14f, 0.08f, 0.05f), Vector3.zero, accent, 0.34f, 0.7f, 0.1f);
            EnsurePart(root, "BackAttachment", PrimitiveType.Cube, new Vector3(0f, 0.34f, -0.64f), new Vector3(0.20f, 0.78f, 0.08f), Vector3.zero, accent, 0.34f, 0.66f, 0.12f);
            EnsurePart(root, "BackAttachment_Core", PrimitiveType.Sphere, new Vector3(0f, 0.46f, -0.70f), new Vector3(0.13f, 0.13f, 0.08f), Vector3.zero, accent, 0.20f, 0.76f, 0.35f);
        }

        private static void EnsureWeapons(Transform root)
        {
            var metal = new Color(0.74f, 0.76f, 0.78f);
            var darkMetal = new Color(0.20f, 0.22f, 0.25f);
            var accent = new Color(0.82f, 0.65f, 0.22f);
            var leather = new Color(0.18f, 0.12f, 0.08f);
            var arcane = new Color(0.35f, 0.70f, 1.00f);

            EnsurePart(root, "Weapon_Main", PrimitiveType.Cylinder, new Vector3(0.72f, 0.00f, 0.16f), new Vector3(0.06f, 0.70f, 0.06f), new Vector3(0f, 0f, 34f), metal, 0.46f, 0.66f);
            EnsurePart(root, "Weapon_Grip", PrimitiveType.Cylinder, new Vector3(0.63f, -0.32f, 0.15f), new Vector3(0.07f, 0.18f, 0.07f), new Vector3(0f, 0f, 34f), leather, 0.04f, 0.42f);
            EnsurePart(root, "Sword_Blade", PrimitiveType.Cube, new Vector3(0.84f, 0.36f, 0.18f), new Vector3(0.09f, 0.56f, 0.035f), new Vector3(0f, 0f, 34f), metal, 0.52f, 0.74f, 0.06f);
            EnsurePart(root, "Sword_Guard", PrimitiveType.Cube, new Vector3(0.68f, -0.02f, 0.17f), new Vector3(0.28f, 0.05f, 0.06f), new Vector3(0f, 0f, 34f), accent, 0.34f, 0.68f, 0.08f);
            EnsurePart(root, "Sword_Edge_L", PrimitiveType.Cube, new Vector3(0.78f, 0.38f, 0.22f), new Vector3(0.030f, 0.52f, 0.020f), new Vector3(0f, 0f, 34f), Color.Lerp(metal, Color.white, 0.30f), 0.54f, 0.78f, 0.08f);
            EnsurePart(root, "Sword_Edge_R", PrimitiveType.Cube, new Vector3(0.90f, 0.35f, 0.22f), new Vector3(0.030f, 0.52f, 0.020f), new Vector3(0f, 0f, 34f), Color.Lerp(metal, Color.white, 0.30f), 0.54f, 0.78f, 0.08f);
            EnsurePart(root, "Sword_Fuller", PrimitiveType.Cube, new Vector3(0.84f, 0.36f, 0.235f), new Vector3(0.026f, 0.42f, 0.018f), new Vector3(0f, 0f, 34f), Color.Lerp(metal, Color.black, 0.20f), 0.46f, 0.70f, 0.03f);
            EnsurePart(root, "Sword_Gem", PrimitiveType.Sphere, new Vector3(0.68f, -0.02f, 0.23f), new Vector3(0.07f, 0.07f, 0.035f), Vector3.zero, accent, 0.12f, 0.78f, 0.28f);
            EnsurePart(root, "Weapon_Head", PrimitiveType.Cube, new Vector3(0.78f, 0.54f, 0.18f), new Vector3(0.28f, 0.18f, 0.10f), new Vector3(0f, 0f, 34f), metal, 0.48f, 0.64f);
            EnsurePart(root, "Axe_Blade_L", PrimitiveType.Cube, new Vector3(0.66f, 0.50f, 0.17f), new Vector3(0.16f, 0.24f, 0.045f), new Vector3(0f, 0f, -8f), metal, 0.48f, 0.68f);
            EnsurePart(root, "Axe_Blade_R", PrimitiveType.Cube, new Vector3(0.93f, 0.48f, 0.17f), new Vector3(0.16f, 0.24f, 0.045f), new Vector3(0f, 0f, 28f), metal, 0.48f, 0.68f);
            EnsurePart(root, "Axe_Edge_L", PrimitiveType.Cube, new Vector3(0.60f, 0.56f, 0.22f), new Vector3(0.12f, 0.035f, 0.030f), new Vector3(0f, 0f, -8f), Color.Lerp(metal, Color.white, 0.30f), 0.54f, 0.76f, 0.06f);
            EnsurePart(root, "Axe_Edge_R", PrimitiveType.Cube, new Vector3(0.99f, 0.55f, 0.22f), new Vector3(0.12f, 0.035f, 0.030f), new Vector3(0f, 0f, 28f), Color.Lerp(metal, Color.white, 0.30f), 0.54f, 0.76f, 0.06f);
            EnsurePart(root, "Hammer_Face", PrimitiveType.Cube, new Vector3(0.84f, 0.50f, 0.18f), new Vector3(0.40f, 0.22f, 0.16f), new Vector3(0f, 0f, 20f), darkMetal, 0.5f, 0.62f);
            EnsurePart(root, "Hammer_Rune", PrimitiveType.Cube, new Vector3(0.84f, 0.50f, 0.28f), new Vector3(0.24f, 0.040f, 0.035f), new Vector3(0f, 0f, 20f), accent, 0.24f, 0.74f, 0.22f);
            EnsurePart(root, "Staff_Crystal", PrimitiveType.Sphere, new Vector3(0.82f, 0.76f, 0.18f), new Vector3(0.16f, 0.16f, 0.16f), Vector3.zero, arcane, 0f, 0.82f, 0.55f);
            EnsurePart(root, "Staff_Ring", PrimitiveType.Cylinder, new Vector3(0.82f, 0.66f, 0.18f), new Vector3(0.20f, 0.018f, 0.20f), new Vector3(90f, 0f, 0f), accent, 0.24f, 0.72f, 0.18f);
            EnsurePart(root, "Staff_RuneBand", PrimitiveType.Cube, new Vector3(0.79f, 0.50f, 0.22f), new Vector3(0.17f, 0.040f, 0.030f), new Vector3(0f, 0f, 8f), accent, 0.18f, 0.72f, 0.14f);
            EnsurePart(root, "Bow_Limb_Top", PrimitiveType.Cube, new Vector3(0.52f, 0.40f, 0.16f), new Vector3(0.045f, 0.52f, 0.045f), new Vector3(0f, 0f, 66f), leather, 0f, 0.44f);
            EnsurePart(root, "Bow_Limb_Bottom", PrimitiveType.Cube, new Vector3(0.66f, -0.24f, 0.16f), new Vector3(0.045f, 0.52f, 0.045f), new Vector3(0f, 0f, 66f), leather, 0f, 0.44f);
            EnsurePart(root, "Bow_GripWrap", PrimitiveType.Cube, new Vector3(0.60f, 0.07f, 0.20f), new Vector3(0.055f, 0.20f, 0.036f), new Vector3(0f, 0f, 76f), accent, 0.08f, 0.58f, 0.04f);
            EnsurePart(root, "Bow_String", PrimitiveType.Cube, new Vector3(0.58f, 0.10f, 0.16f), new Vector3(0.025f, 0.78f, 0.025f), new Vector3(0f, 0f, 78f), new Color(0.92f, 0.88f, 0.72f), 0f, 0.5f);
            EnsurePart(root, "Bow_ArrowNock", PrimitiveType.Cube, new Vector3(0.62f, 0.10f, 0.215f), new Vector3(0.040f, 0.28f, 0.028f), new Vector3(0f, 0f, 78f), accent, 0.06f, 0.60f, 0.08f);

            EnsurePart(root, "Shield_Off", PrimitiveType.Cube, new Vector3(-0.72f, 0.02f, 0.18f), new Vector3(0.13f, 0.58f, 0.42f), Vector3.zero, darkMetal, 0.36f, 0.56f);
            EnsurePart(root, "Shield_Crest", PrimitiveType.Cube, new Vector3(-0.82f, 0.02f, 0.34f), new Vector3(0.045f, 0.34f, 0.22f), Vector3.zero, accent, 0.30f, 0.66f, 0.08f);
            EnsurePart(root, "Shield_Rim_Top", PrimitiveType.Cube, new Vector3(-0.72f, 0.32f, 0.36f), new Vector3(0.060f, 0.08f, 0.44f), Vector3.zero, accent, 0.32f, 0.68f, 0.08f);
            EnsurePart(root, "Shield_Rim_Bottom", PrimitiveType.Cube, new Vector3(-0.72f, -0.28f, 0.36f), new Vector3(0.060f, 0.08f, 0.44f), Vector3.zero, accent, 0.32f, 0.68f, 0.08f);
            EnsurePart(root, "Shield_Rivet_Top", PrimitiveType.Sphere, new Vector3(-0.82f, 0.24f, 0.38f), new Vector3(0.035f, 0.035f, 0.026f), Vector3.zero, accent, 0.28f, 0.68f, 0.06f);
            EnsurePart(root, "Shield_Rivet_Bottom", PrimitiveType.Sphere, new Vector3(-0.82f, -0.22f, 0.38f), new Vector3(0.035f, 0.035f, 0.026f), Vector3.zero, accent, 0.28f, 0.68f, 0.06f);
            EnsurePart(root, "Orb_Off", PrimitiveType.Sphere, new Vector3(-0.72f, 0.08f, 0.22f), new Vector3(0.24f, 0.24f, 0.24f), Vector3.zero, arcane, 0f, 0.82f, 0.55f);
            EnsurePart(root, "Orb_Ring", PrimitiveType.Cylinder, new Vector3(-0.72f, 0.08f, 0.22f), new Vector3(0.30f, 0.018f, 0.30f), new Vector3(90f, 0f, 0f), accent, 0.22f, 0.72f, 0.16f);
            EnsurePart(root, "Weapon_Off", PrimitiveType.Cylinder, new Vector3(-0.72f, -0.02f, 0.16f), new Vector3(0.045f, 0.45f, 0.045f), new Vector3(0f, 0f, -34f), metal, 0.46f, 0.66f);
            EnsurePart(root, "Dagger_Blade", PrimitiveType.Cube, new Vector3(-0.83f, 0.18f, 0.18f), new Vector3(0.06f, 0.32f, 0.035f), new Vector3(0f, 0f, -34f), metal, 0.52f, 0.74f);
            EnsurePart(root, "Dagger_Guard", PrimitiveType.Cube, new Vector3(-0.74f, 0.02f, 0.20f), new Vector3(0.18f, 0.035f, 0.035f), new Vector3(0f, 0f, -34f), accent, 0.28f, 0.66f, 0.08f);
            EnsurePart(root, "Tome_Off", PrimitiveType.Cube, new Vector3(-0.72f, 0.10f, 0.22f), new Vector3(0.30f, 0.36f, 0.08f), new Vector3(0f, 0f, -10f), new Color(0.20f, 0.12f, 0.34f), 0f, 0.52f);
            EnsurePart(root, "Tome_Page", PrimitiveType.Cube, new Vector3(-0.70f, 0.10f, 0.27f), new Vector3(0.24f, 0.30f, 0.025f), new Vector3(0f, 0f, -10f), new Color(0.82f, 0.76f, 0.58f), 0f, 0.48f);
            EnsurePart(root, "Tome_Clasp", PrimitiveType.Cube, new Vector3(-0.72f, 0.10f, 0.28f), new Vector3(0.20f, 0.045f, 0.035f), new Vector3(0f, 0f, -10f), accent, 0.30f, 0.66f, 0.12f);
        }

        private static void EnsureHeroDetailLayer(Transform root)
        {
            var darkPlate = new Color(0.08f, 0.10f, 0.13f);
            var edgeMetal = new Color(0.70f, 0.72f, 0.72f);
            var accent = new Color(0.86f, 0.66f, 0.22f);
            var leather = new Color(0.18f, 0.11f, 0.07f);
            var arcane = new Color(0.35f, 0.72f, 1.00f);
            var clothShadow = new Color(0.055f, 0.065f, 0.09f);

            EnsurePart(root, "Armor_Bevel_Top", PrimitiveType.Cube, new Vector3(0f, 0.43f, 0.335f), new Vector3(0.50f, 0.040f, 0.036f), Vector3.zero, edgeMetal, 0.48f, 0.70f, 0.04f);
            EnsurePart(root, "Armor_Bevel_Left", PrimitiveType.Cube, new Vector3(-0.42f, 0.08f, 0.295f), new Vector3(0.040f, 0.64f, 0.035f), new Vector3(0f, 0f, -4f), edgeMetal, 0.46f, 0.66f, 0.03f);
            EnsurePart(root, "Armor_Bevel_Right", PrimitiveType.Cube, new Vector3(0.42f, 0.08f, 0.295f), new Vector3(0.040f, 0.64f, 0.035f), new Vector3(0f, 0f, 4f), edgeMetal, 0.46f, 0.66f, 0.03f);
            EnsurePart(root, "Armor_SidePlate_L", PrimitiveType.Cube, new Vector3(-0.48f, 0.03f, 0.12f), new Vector3(0.08f, 0.54f, 0.20f), new Vector3(0f, 0f, -6f), darkPlate, 0.36f, 0.54f);
            EnsurePart(root, "Armor_SidePlate_R", PrimitiveType.Cube, new Vector3(0.48f, 0.03f, 0.12f), new Vector3(0.08f, 0.54f, 0.20f), new Vector3(0f, 0f, 6f), darkPlate, 0.36f, 0.54f);
            EnsurePart(root, "Armor_BackPlate", PrimitiveType.Cube, new Vector3(0f, 0.10f, -0.23f), new Vector3(0.74f, 0.64f, 0.075f), Vector3.zero, darkPlate, 0.36f, 0.52f);
            EnsurePart(root, "Armor_BackSpine", PrimitiveType.Cube, new Vector3(0f, 0.16f, -0.31f), new Vector3(0.07f, 0.62f, 0.045f), Vector3.zero, accent, 0.28f, 0.70f, 0.10f);
            EnsurePart(root, "Armor_Undersuit_Seam", PrimitiveType.Cube, new Vector3(0f, -0.03f, 0.365f), new Vector3(0.040f, 0.48f, 0.026f), Vector3.zero, clothShadow, 0f, 0.40f);
            EnsurePart(root, "Armor_Shadow_PectoralCut_L", PrimitiveType.Cube, new Vector3(-0.15f, 0.18f, 0.368f), new Vector3(0.026f, 0.30f, 0.018f), new Vector3(0f, 0f, 10f), Color.Lerp(darkPlate, Color.black, 0.28f), 0.28f, 0.42f);
            EnsurePart(root, "Armor_Shadow_PectoralCut_R", PrimitiveType.Cube, new Vector3(0.15f, 0.18f, 0.368f), new Vector3(0.026f, 0.30f, 0.018f), new Vector3(0f, 0f, -10f), Color.Lerp(darkPlate, Color.black, 0.28f), 0.28f, 0.42f);
            EnsurePart(root, "Armor_Battlewear_Score_L", PrimitiveType.Cube, new Vector3(-0.28f, 0.06f, 0.372f), new Vector3(0.18f, 0.020f, 0.016f), new Vector3(0f, 0f, -18f), Color.Lerp(edgeMetal, Color.black, 0.20f), 0.24f, 0.48f);
            EnsurePart(root, "Armor_Battlewear_Score_R", PrimitiveType.Cube, new Vector3(0.29f, 0.32f, 0.370f), new Vector3(0.16f, 0.018f, 0.016f), new Vector3(0f, 0f, 22f), Color.Lerp(edgeMetal, Color.black, 0.18f), 0.24f, 0.48f);
            EnsurePart(root, "Armor_Battlewear_Notch", PrimitiveType.Cube, new Vector3(0.02f, -0.10f, 0.370f), new Vector3(0.12f, 0.018f, 0.016f), new Vector3(0f, 0f, -8f), Color.Lerp(edgeMetal, Color.black, 0.22f), 0.24f, 0.46f);

            EnsurePart(root, "Shoulder_Layer_L", PrimitiveType.Cube, new Vector3(-0.63f, 0.32f, 0.08f), new Vector3(0.34f, 0.075f, 0.24f), new Vector3(0f, 0f, -14f), darkPlate, 0.38f, 0.58f);
            EnsurePart(root, "Shoulder_Layer_R", PrimitiveType.Cube, new Vector3(0.63f, 0.32f, 0.08f), new Vector3(0.34f, 0.075f, 0.24f), new Vector3(0f, 0f, 14f), darkPlate, 0.38f, 0.58f);
            EnsurePart(root, "Shoulder_Edge_L", PrimitiveType.Cube, new Vector3(-0.70f, 0.50f, 0.05f), new Vector3(0.24f, 0.035f, 0.105f), new Vector3(0f, 0f, -17f), edgeMetal, 0.50f, 0.72f, 0.04f);
            EnsurePart(root, "Shoulder_Edge_R", PrimitiveType.Cube, new Vector3(0.70f, 0.50f, 0.05f), new Vector3(0.24f, 0.035f, 0.105f), new Vector3(0f, 0f, 17f), edgeMetal, 0.50f, 0.72f, 0.04f);

            EnsurePart(root, "Glove_Knuckle_L", PrimitiveType.Cube, new Vector3(-0.50f, -0.36f, 0.205f), new Vector3(0.20f, 0.035f, 0.035f), Vector3.zero, accent, 0.34f, 0.68f, 0.08f);
            EnsurePart(root, "Glove_Knuckle_R", PrimitiveType.Cube, new Vector3(0.50f, -0.36f, 0.205f), new Vector3(0.20f, 0.035f, 0.035f), Vector3.zero, accent, 0.34f, 0.68f, 0.08f);
            EnsurePart(root, "Glove_Thumb_L", PrimitiveType.Cube, new Vector3(-0.64f, -0.31f, 0.12f), new Vector3(0.05f, 0.13f, 0.052f), new Vector3(0f, 0f, 34f), edgeMetal, 0.34f, 0.58f);
            EnsurePart(root, "Glove_Thumb_R", PrimitiveType.Cube, new Vector3(0.64f, -0.31f, 0.12f), new Vector3(0.05f, 0.13f, 0.052f), new Vector3(0f, 0f, -34f), edgeMetal, 0.34f, 0.58f);

            EnsurePart(root, "Belt_Pouch_L", PrimitiveType.Cube, new Vector3(-0.42f, -0.28f, 0.27f), new Vector3(0.15f, 0.18f, 0.065f), new Vector3(0f, 0f, -8f), leather, 0.02f, 0.44f);
            EnsurePart(root, "Belt_Pouch_R", PrimitiveType.Cube, new Vector3(0.42f, -0.28f, 0.27f), new Vector3(0.15f, 0.18f, 0.065f), new Vector3(0f, 0f, 8f), leather, 0.02f, 0.44f);
            EnsurePart(root, "Belt_CommandSeal", PrimitiveType.Sphere, new Vector3(0f, -0.18f, 0.345f), new Vector3(0.08f, 0.08f, 0.028f), Vector3.zero, accent, 0.24f, 0.76f, 0.20f);

            EnsurePart(root, "PlateSkirt_Side_L", PrimitiveType.Cube, new Vector3(-0.31f, -0.56f, 0.11f), new Vector3(0.15f, 0.42f, 0.055f), new Vector3(0f, 0f, -8f), darkPlate, 0.32f, 0.50f);
            EnsurePart(root, "PlateSkirt_Side_R", PrimitiveType.Cube, new Vector3(0.31f, -0.56f, 0.11f), new Vector3(0.15f, 0.42f, 0.055f), new Vector3(0f, 0f, 8f), darkPlate, 0.32f, 0.50f);
            EnsurePart(root, "Knee_Ridge_L", PrimitiveType.Cube, new Vector3(-0.20f, -0.72f, 0.265f), new Vector3(0.18f, 0.028f, 0.030f), Vector3.zero, accent, 0.30f, 0.66f, 0.05f);
            EnsurePart(root, "Knee_Ridge_R", PrimitiveType.Cube, new Vector3(0.20f, -0.72f, 0.265f), new Vector3(0.18f, 0.028f, 0.030f), Vector3.zero, accent, 0.30f, 0.66f, 0.05f);
            EnsurePart(root, "Boot_Heel_L", PrimitiveType.Cube, new Vector3(-0.22f, -1.08f, -0.06f), new Vector3(0.24f, 0.09f, 0.13f), Vector3.zero, Color.Lerp(leather, Color.black, 0.30f), 0.08f, 0.38f);
            EnsurePart(root, "Boot_Heel_R", PrimitiveType.Cube, new Vector3(0.22f, -1.08f, -0.06f), new Vector3(0.24f, 0.09f, 0.13f), Vector3.zero, Color.Lerp(leather, Color.black, 0.30f), 0.08f, 0.38f);
            EnsurePart(root, "Boot_Tread_L", PrimitiveType.Cube, new Vector3(-0.22f, -1.155f, 0.28f), new Vector3(0.26f, 0.026f, 0.16f), Vector3.zero, Color.Lerp(leather, Color.black, 0.42f), 0.06f, 0.34f);
            EnsurePart(root, "Boot_Tread_R", PrimitiveType.Cube, new Vector3(0.22f, -1.155f, 0.28f), new Vector3(0.26f, 0.026f, 0.16f), Vector3.zero, Color.Lerp(leather, Color.black, 0.42f), 0.06f, 0.34f);

            EnsurePart(root, "Cape_Chain_L", PrimitiveType.Cube, new Vector3(-0.18f, 0.44f, 0.285f), new Vector3(0.24f, 0.030f, 0.030f), new Vector3(0f, 0f, -16f), accent, 0.34f, 0.70f, 0.08f);
            EnsurePart(root, "Cape_Chain_R", PrimitiveType.Cube, new Vector3(0.18f, 0.44f, 0.285f), new Vector3(0.24f, 0.030f, 0.030f), new Vector3(0f, 0f, 16f), accent, 0.34f, 0.70f, 0.08f);
            EnsurePart(root, "Cape_Pin_L", PrimitiveType.Sphere, new Vector3(-0.35f, 0.44f, 0.26f), new Vector3(0.070f, 0.070f, 0.030f), Vector3.zero, accent, 0.30f, 0.72f, 0.12f);
            EnsurePart(root, "Cape_Pin_R", PrimitiveType.Sphere, new Vector3(0.35f, 0.44f, 0.26f), new Vector3(0.070f, 0.070f, 0.030f), Vector3.zero, accent, 0.30f, 0.72f, 0.12f);
            EnsurePart(root, "BackAttachment_Wing_L", PrimitiveType.Cube, new Vector3(-0.18f, 0.34f, -0.70f), new Vector3(0.07f, 0.55f, 0.06f), new Vector3(0f, 0f, -14f), accent, 0.30f, 0.70f, 0.18f);
            EnsurePart(root, "BackAttachment_Wing_R", PrimitiveType.Cube, new Vector3(0.18f, 0.34f, -0.70f), new Vector3(0.07f, 0.55f, 0.06f), new Vector3(0f, 0f, 14f), accent, 0.30f, 0.70f, 0.18f);

            EnsurePart(root, "Sword_CoreLine", PrimitiveType.Cube, new Vector3(0.84f, 0.48f, 0.252f), new Vector3(0.018f, 0.28f, 0.014f), new Vector3(0f, 0f, 34f), arcane, 0.12f, 0.80f, 0.34f);
            EnsurePart(root, "Axe_Rivet_L", PrimitiveType.Sphere, new Vector3(0.69f, 0.51f, 0.225f), new Vector3(0.035f, 0.035f, 0.024f), Vector3.zero, accent, 0.30f, 0.70f, 0.08f);
            EnsurePart(root, "Axe_Rivet_R", PrimitiveType.Sphere, new Vector3(0.92f, 0.51f, 0.225f), new Vector3(0.035f, 0.035f, 0.024f), Vector3.zero, accent, 0.30f, 0.70f, 0.08f);
            EnsurePart(root, "Hammer_SideCap_L", PrimitiveType.Cube, new Vector3(0.62f, 0.50f, 0.26f), new Vector3(0.070f, 0.18f, 0.030f), new Vector3(0f, 0f, 20f), edgeMetal, 0.48f, 0.68f);
            EnsurePart(root, "Hammer_SideCap_R", PrimitiveType.Cube, new Vector3(1.04f, 0.50f, 0.26f), new Vector3(0.070f, 0.18f, 0.030f), new Vector3(0f, 0f, 20f), edgeMetal, 0.48f, 0.68f);
            EnsurePart(root, "Hammer_ImpactCore", PrimitiveType.Sphere, new Vector3(0.84f, 0.50f, 0.30f), new Vector3(0.08f, 0.08f, 0.028f), Vector3.zero, accent, 0.20f, 0.78f, 0.24f);
            EnsurePart(root, "Staff_OrbitStone_L", PrimitiveType.Sphere, new Vector3(0.68f, 0.72f, 0.19f), new Vector3(0.055f, 0.055f, 0.055f), Vector3.zero, arcane, 0f, 0.86f, 0.48f);
            EnsurePart(root, "Staff_OrbitStone_R", PrimitiveType.Sphere, new Vector3(0.96f, 0.72f, 0.19f), new Vector3(0.055f, 0.055f, 0.055f), Vector3.zero, arcane, 0f, 0.86f, 0.48f);
            EnsurePart(root, "Bow_ArrowShaft", PrimitiveType.Cube, new Vector3(0.66f, 0.08f, 0.25f), new Vector3(0.030f, 0.62f, 0.020f), new Vector3(0f, 0f, 78f), edgeMetal, 0.12f, 0.58f);
            EnsurePart(root, "Bow_ArrowHead", PrimitiveType.Cube, new Vector3(0.78f, 0.38f, 0.25f), new Vector3(0.070f, 0.080f, 0.026f), new Vector3(0f, 0f, 78f), edgeMetal, 0.46f, 0.72f, 0.05f);
            EnsurePart(root, "Bow_Fletching", PrimitiveType.Cube, new Vector3(0.54f, -0.18f, 0.25f), new Vector3(0.095f, 0.045f, 0.024f), new Vector3(0f, 0f, 32f), accent, 0.08f, 0.62f, 0.06f);

            EnsurePart(root, "Shield_Boss", PrimitiveType.Sphere, new Vector3(-0.84f, 0.02f, 0.42f), new Vector3(0.10f, 0.10f, 0.035f), Vector3.zero, accent, 0.32f, 0.72f, 0.12f);
            EnsurePart(root, "Shield_Scar", PrimitiveType.Cube, new Vector3(-0.86f, 0.03f, 0.445f), new Vector3(0.038f, 0.42f, 0.025f), new Vector3(0f, 0f, -20f), edgeMetal, 0.28f, 0.64f);
            EnsurePart(root, "Orb_Core", PrimitiveType.Sphere, new Vector3(-0.72f, 0.08f, 0.23f), new Vector3(0.11f, 0.11f, 0.11f), Vector3.zero, arcane, 0f, 0.88f, 0.60f);
            EnsurePart(root, "Dagger_Edge", PrimitiveType.Cube, new Vector3(-0.87f, 0.20f, 0.225f), new Vector3(0.020f, 0.27f, 0.018f), new Vector3(0f, 0f, -34f), Color.Lerp(edgeMetal, Color.white, 0.20f), 0.54f, 0.76f, 0.06f);
            EnsurePart(root, "Tome_Rune", PrimitiveType.Cube, new Vector3(-0.70f, 0.10f, 0.292f), new Vector3(0.12f, 0.028f, 0.020f), new Vector3(0f, 0f, -10f), arcane, 0.08f, 0.78f, 0.28f);
        }

        private static void EnsurePrestigeSilhouetteLayer(Transform root)
        {
            var shadowSteel = new Color(0.075f, 0.085f, 0.105f);
            var edgeMetal = new Color(0.70f, 0.72f, 0.72f);
            var accent = new Color(0.86f, 0.66f, 0.22f);
            var leather = new Color(0.18f, 0.11f, 0.07f);
            var cloth = new Color(0.08f, 0.10f, 0.16f);
            var arcane = new Color(0.35f, 0.72f, 1.00f);

            EnsurePart(root, "Prestige_Mantle_L", PrimitiveType.Cube, new Vector3(-0.43f, 0.50f, -0.02f), new Vector3(0.36f, 0.09f, 0.36f), new Vector3(0f, 0f, -12f), shadowSteel, 0.38f, 0.58f);
            EnsurePart(root, "Prestige_Mantle_R", PrimitiveType.Cube, new Vector3(0.43f, 0.50f, -0.02f), new Vector3(0.36f, 0.09f, 0.36f), new Vector3(0f, 0f, 12f), shadowSteel, 0.38f, 0.58f);
            EnsurePart(root, "Prestige_Mantle_Trim_L", PrimitiveType.Cube, new Vector3(-0.47f, 0.55f, 0.10f), new Vector3(0.30f, 0.032f, 0.050f), new Vector3(0f, 0f, -12f), accent, 0.32f, 0.70f, 0.08f);
            EnsurePart(root, "Prestige_Mantle_Trim_R", PrimitiveType.Cube, new Vector3(0.47f, 0.55f, 0.10f), new Vector3(0.30f, 0.032f, 0.050f), new Vector3(0f, 0f, 12f), accent, 0.32f, 0.70f, 0.08f);
            EnsurePart(root, "Prestige_Mantle_Rivet_L", PrimitiveType.Sphere, new Vector3(-0.33f, 0.54f, 0.19f), new Vector3(0.040f, 0.040f, 0.024f), Vector3.zero, accent, 0.30f, 0.72f, 0.08f);
            EnsurePart(root, "Prestige_Mantle_Rivet_R", PrimitiveType.Sphere, new Vector3(0.33f, 0.54f, 0.19f), new Vector3(0.040f, 0.040f, 0.024f), Vector3.zero, accent, 0.30f, 0.72f, 0.08f);

            EnsurePart(root, "Prestige_Sash_Diagonal", PrimitiveType.Cube, new Vector3(-0.03f, 0.04f, 0.382f), new Vector3(0.080f, 0.88f, 0.034f), new Vector3(0f, 0f, -28f), cloth, 0.02f, 0.50f);
            EnsurePart(root, "Prestige_Sash_Edge", PrimitiveType.Cube, new Vector3(0.07f, 0.04f, 0.407f), new Vector3(0.030f, 0.82f, 0.026f), new Vector3(0f, 0f, -28f), accent, 0.16f, 0.68f, 0.08f);
            EnsurePart(root, "Prestige_SashPlate", PrimitiveType.Sphere, new Vector3(0.22f, -0.18f, 0.405f), new Vector3(0.090f, 0.090f, 0.030f), Vector3.zero, accent, 0.28f, 0.76f, 0.16f);
            EnsurePart(root, "Prestige_FieldMedal_L", PrimitiveType.Sphere, new Vector3(-0.23f, 0.34f, 0.396f), new Vector3(0.045f, 0.045f, 0.022f), Vector3.zero, accent, 0.28f, 0.72f, 0.10f);
            EnsurePart(root, "Prestige_FieldMedal_R", PrimitiveType.Sphere, new Vector3(-0.13f, 0.30f, 0.398f), new Vector3(0.035f, 0.035f, 0.020f), Vector3.zero, edgeMetal, 0.36f, 0.72f, 0.04f);
            EnsurePart(root, "Prestige_BattleChain_L", PrimitiveType.Cube, new Vector3(-0.17f, 0.42f, 0.360f), new Vector3(0.24f, 0.026f, 0.026f), new Vector3(0f, 0f, -18f), accent, 0.34f, 0.70f, 0.08f);
            EnsurePart(root, "Prestige_BattleChain_R", PrimitiveType.Cube, new Vector3(0.17f, 0.42f, 0.360f), new Vector3(0.24f, 0.026f, 0.026f), new Vector3(0f, 0f, 18f), accent, 0.34f, 0.70f, 0.08f);
            EnsurePart(root, "Prestige_WaistWrap", PrimitiveType.Cube, new Vector3(0f, -0.33f, 0.285f), new Vector3(0.72f, 0.075f, 0.040f), Vector3.zero, cloth, 0.02f, 0.48f);
            EnsurePart(root, "Prestige_WaistWrap_Tail_L", PrimitiveType.Cube, new Vector3(-0.27f, -0.48f, 0.290f), new Vector3(0.070f, 0.34f, 0.034f), new Vector3(0f, 0f, 8f), cloth, 0.02f, 0.48f);
            EnsurePart(root, "Prestige_WaistWrap_Tail_R", PrimitiveType.Cube, new Vector3(0.27f, -0.48f, 0.290f), new Vector3(0.070f, 0.34f, 0.034f), new Vector3(0f, 0f, -8f), cloth, 0.02f, 0.48f);

            EnsurePart(root, "Cape_Tassel_L", PrimitiveType.Cube, new Vector3(-0.42f, -0.63f, -0.61f), new Vector3(0.045f, 0.26f, 0.030f), new Vector3(8f, 0f, -6f), accent, 0.08f, 0.66f, 0.06f);
            EnsurePart(root, "Cape_Tassel_R", PrimitiveType.Cube, new Vector3(0.42f, -0.63f, -0.61f), new Vector3(0.045f, 0.26f, 0.030f), new Vector3(8f, 0f, 6f), accent, 0.08f, 0.66f, 0.06f);
            EnsurePart(root, "Cape_Tassel_Weight_L", PrimitiveType.Sphere, new Vector3(-0.42f, -0.80f, -0.62f), new Vector3(0.050f, 0.050f, 0.030f), Vector3.zero, edgeMetal, 0.34f, 0.70f, 0.05f);
            EnsurePart(root, "Cape_Tassel_Weight_R", PrimitiveType.Sphere, new Vector3(0.42f, -0.80f, -0.62f), new Vector3(0.050f, 0.050f, 0.030f), Vector3.zero, edgeMetal, 0.34f, 0.70f, 0.05f);

            EnsurePart(root, "Back_Harness", PrimitiveType.Cube, new Vector3(0f, 0.12f, -0.37f), new Vector3(0.62f, 0.070f, 0.052f), new Vector3(0f, 0f, 0f), leather, 0.06f, 0.46f);
            EnsurePart(root, "Back_HarnessStrap_L", PrimitiveType.Cube, new Vector3(-0.20f, 0.08f, -0.38f), new Vector3(0.055f, 0.62f, 0.044f), new Vector3(0f, 0f, -18f), leather, 0.06f, 0.46f);
            EnsurePart(root, "Back_HarnessStrap_R", PrimitiveType.Cube, new Vector3(0.20f, 0.08f, -0.38f), new Vector3(0.055f, 0.62f, 0.044f), new Vector3(0f, 0f, 18f), leather, 0.06f, 0.46f);
            EnsurePart(root, "Back_HarnessRing_L", PrimitiveType.Sphere, new Vector3(-0.32f, 0.20f, -0.42f), new Vector3(0.055f, 0.055f, 0.030f), Vector3.zero, accent, 0.30f, 0.72f, 0.08f);
            EnsurePart(root, "Back_HarnessRing_R", PrimitiveType.Sphere, new Vector3(0.32f, 0.20f, -0.42f), new Vector3(0.055f, 0.055f, 0.030f), Vector3.zero, accent, 0.30f, 0.72f, 0.08f);

            EnsurePart(root, "Back_Scabbard", PrimitiveType.Cube, new Vector3(-0.34f, -0.10f, -0.54f), new Vector3(0.070f, 0.86f, 0.060f), new Vector3(0f, 0f, -32f), leather, 0.08f, 0.48f);
            EnsurePart(root, "Back_Scabbard_Cap_Top", PrimitiveType.Cube, new Vector3(-0.54f, 0.28f, -0.56f), new Vector3(0.12f, 0.045f, 0.050f), new Vector3(0f, 0f, -32f), edgeMetal, 0.44f, 0.72f, 0.05f);
            EnsurePart(root, "Back_Scabbard_Cap_Bottom", PrimitiveType.Cube, new Vector3(-0.14f, -0.46f, -0.56f), new Vector3(0.12f, 0.045f, 0.050f), new Vector3(0f, 0f, -32f), edgeMetal, 0.44f, 0.72f, 0.05f);
            EnsurePart(root, "Back_Scabbard_Rune", PrimitiveType.Cube, new Vector3(-0.36f, -0.08f, -0.60f), new Vector3(0.030f, 0.22f, 0.022f), new Vector3(0f, 0f, -32f), accent, 0.16f, 0.78f, 0.22f);
            EnsurePart(root, "Back_Quiver", PrimitiveType.Cube, new Vector3(0.34f, -0.06f, -0.54f), new Vector3(0.20f, 0.76f, 0.13f), new Vector3(0f, 0f, 16f), leather, 0.06f, 0.48f);
            EnsurePart(root, "Back_Quiver_Rim", PrimitiveType.Cube, new Vector3(0.22f, 0.32f, -0.44f), new Vector3(0.25f, 0.050f, 0.050f), new Vector3(0f, 0f, 16f), accent, 0.20f, 0.66f, 0.08f);
            EnsurePart(root, "Back_QuiverArrow_0", PrimitiveType.Cube, new Vector3(0.18f, 0.55f, -0.42f), new Vector3(0.022f, 0.44f, 0.018f), new Vector3(0f, 0f, 12f), edgeMetal, 0.18f, 0.62f, 0.03f);
            EnsurePart(root, "Back_QuiverArrow_1", PrimitiveType.Cube, new Vector3(0.30f, 0.58f, -0.42f), new Vector3(0.022f, 0.46f, 0.018f), new Vector3(0f, 0f, 18f), edgeMetal, 0.18f, 0.62f, 0.03f);
            EnsurePart(root, "Back_QuiverArrow_2", PrimitiveType.Cube, new Vector3(0.42f, 0.52f, -0.42f), new Vector3(0.022f, 0.40f, 0.018f), new Vector3(0f, 0f, 22f), edgeMetal, 0.18f, 0.62f, 0.03f);
            EnsurePart(root, "Back_RelicFrame", PrimitiveType.Cube, new Vector3(0f, 0.30f, -0.58f), new Vector3(0.32f, 0.42f, 0.052f), Vector3.zero, edgeMetal, 0.44f, 0.72f, 0.05f);
            EnsurePart(root, "Back_RelicCore", PrimitiveType.Sphere, new Vector3(0f, 0.30f, -0.64f), new Vector3(0.13f, 0.13f, 0.060f), Vector3.zero, arcane, 0f, 0.86f, 0.58f);
            EnsurePart(root, "Back_RelicHalo", PrimitiveType.Cylinder, new Vector3(0f, 0.30f, -0.66f), new Vector3(0.23f, 0.016f, 0.23f), new Vector3(90f, 0f, 0f), accent, 0.16f, 0.78f, 0.22f);
            EnsurePart(root, "Back_HammerHook", PrimitiveType.Cube, new Vector3(0.36f, -0.12f, -0.52f), new Vector3(0.17f, 0.38f, 0.055f), new Vector3(0f, 0f, -18f), edgeMetal, 0.46f, 0.70f, 0.04f);
            EnsurePart(root, "Back_HammerHook_Rivet", PrimitiveType.Sphere, new Vector3(0.30f, 0.08f, -0.57f), new Vector3(0.050f, 0.050f, 0.030f), Vector3.zero, accent, 0.30f, 0.72f, 0.08f);
        }

        private static void EnsureAnchors(Transform root)
        {
            EnsureAnchor(root, "VFX_ChestAnchor", new Vector3(0f, 0.48f, 0.38f));
            EnsureAnchor(root, "VFX_Hand_L", new Vector3(-0.55f, -0.10f, 0.20f));
            EnsureAnchor(root, "VFX_Hand_R", new Vector3(0.55f, -0.10f, 0.20f));
            EnsureAnchor(root, "PetAnchor", new Vector3(-0.95f, -0.50f, -0.20f));
            EnsureAnchor(root, "MountAnchor", new Vector3(0f, -0.88f, 0f));
        }

        private static GameObject EnsurePart(
            Transform root,
            string name,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Color color,
            float metallic,
            float smoothness,
            float emissionStrength = 0f)
        {
            Transform existing = root.Find(name);
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

            ConfigureMaterial(part.GetComponent<Renderer>(), color, metallic, smoothness, emissionStrength);
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

        private static void HideRootDebugRenderer(GameObject champion)
        {
            var rootRenderer = champion.GetComponent<Renderer>();
            if (rootRenderer != null)
            {
                rootRenderer.enabled = false;
            }
        }

        private static void RemoveLegacyPart(Transform root, string name)
        {
            Transform legacy = root.Find(name);
            if (legacy != null)
            {
                Object.Destroy(legacy.gameObject);
            }
        }

        private static void ConfigureMaterial(Renderer renderer, Color color, float metallic, float smoothness, float emissionStrength)
        {
            if (renderer == null)
            {
                return;
            }

            var shader = Shader.Find("Standard");
            var material = shader != null ? new Material(shader) : renderer.material;
            material.color = color;
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
            }

            if (emissionStrength > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emissionStrength);
            }
            else if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            renderer.material = material;
        }
    }

    public sealed class ProceduralChampionMotion : MonoBehaviour
    {
        private struct MotionPart
        {
            public Transform Transform;
            public Vector3 BasePosition;
            public Quaternion BaseRotation;
        }

        private MotionPart _chest;
        private MotionPart _head;
        private MotionPart _neck;
        private MotionPart _leftShoulder;
        private MotionPart _rightShoulder;
        private MotionPart _leftGlove;
        private MotionPart _rightGlove;
        private MotionPart _weaponMain;
        private MotionPart _weaponOff;
        private MotionPart[] _capeParts = System.Array.Empty<MotionPart>();
        private MotionPart[] _hairParts = System.Array.Empty<MotionPart>();
        private MotionPart[] _robeParts = System.Array.Empty<MotionPart>();
        private MotionPart[] _detailParts = System.Array.Empty<MotionPart>();
        private MotionPart[] _backGearParts = System.Array.Empty<MotionPart>();
        private Vector3 _lastWorldPosition;
        private float _moveAmount;
        private float _seed;
        private bool _isBound;

        public void Rebind()
        {
            _chest = Bind("ChestArmor");
            _head = Bind("Skin_Head");
            _neck = Bind("Skin_Neck");
            _leftShoulder = Bind("Shoulder_L");
            _rightShoulder = Bind("Shoulder_R");
            _leftGlove = Bind("Glove_L");
            _rightGlove = Bind("Glove_R");
            _weaponMain = Bind("Weapon_Main");
            _weaponOff = Bind("Weapon_Off");
            _capeParts = BindExact("Cape", "Cape_LeftFold", "Cape_RightFold", "Cape_InnerShadow", "Cape_LeftEdge", "Cape_RightEdge", "Cape_Seam_Center", "Cape_Hem", "Cape_Rune_L", "Cape_Rune_R", "Cape_Chain_L", "Cape_Chain_R", "Cape_Pin_L", "Cape_Pin_R", "Cape_Tassel_L", "Cape_Tassel_R", "Cape_Tassel_Weight_L", "Cape_Tassel_Weight_R");
            _hairParts = BindContaining("Hair_Long", "Hair_Braid", "Hair_Topknot_Tail");
            _robeParts = BindContaining("RobePanel", "RobeBackPanel", "RobeSleeve");
            _detailParts = BindContaining("Prestige_Sash", "Prestige_BattleChain", "Prestige_WaistWrap", "Prestige_FieldMedal", "Back_Harness");
            _backGearParts = BindContaining("Back_Scabbard", "Back_Quiver", "Back_Relic", "Back_HammerHook");
            _lastWorldPosition = transform.position;
            _seed = Mathf.Abs(GetInstanceID() * 0.173f) % 10f;
            _isBound = true;
        }

        private void LateUpdate()
        {
            if (!_isBound)
            {
                Rebind();
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 delta = transform.position - _lastWorldPosition;
            delta.y = 0f;
            _lastWorldPosition = transform.position;

            float targetMove = Mathf.Clamp01(delta.magnitude / deltaTime / 4.2f);
            _moveAmount = Mathf.Lerp(_moveAmount, targetMove, deltaTime * 5.5f);

            float time = Time.time + _seed;
            float idle = Mathf.Sin(time * 1.22f);
            float breath = Mathf.Sin(time * 1.72f) * (0.008f + _moveAmount * 0.004f);
            float stride = Mathf.Sin(time * Mathf.Lerp(1.65f, 8.2f, _moveAmount));
            float counterStride = Mathf.Sin(time * Mathf.Lerp(1.65f, 8.2f, _moveAmount) + Mathf.PI);
            float sway = Mathf.Sin(time * 0.86f + _seed) * 0.45f;

            Apply(_chest, new Vector3(0f, breath, 0f), new Vector3(-_moveAmount * 3.5f, 0f, sway + stride * _moveAmount * 2.2f));
            Apply(_neck, new Vector3(0f, breath * 0.65f, 0f), new Vector3(idle * 0.5f, 0f, sway * 0.35f));
            Apply(_head, new Vector3(0f, breath * 1.4f, 0f), new Vector3(idle * 1.1f, sway * 2.4f, sway * 0.45f));

            Apply(_leftShoulder, Vector3.zero, new Vector3(counterStride * _moveAmount * 9.0f, 0f, -4f - _moveAmount * 3.0f));
            Apply(_rightShoulder, Vector3.zero, new Vector3(stride * _moveAmount * 9.0f, 0f, 4f + _moveAmount * 3.0f));
            Apply(_leftGlove, new Vector3(0f, counterStride * _moveAmount * 0.025f, 0f), new Vector3(counterStride * _moveAmount * 12.0f, 0f, -2.0f));
            Apply(_rightGlove, new Vector3(0f, stride * _moveAmount * 0.025f, 0f), new Vector3(stride * _moveAmount * 12.0f, 0f, 2.0f));

            Apply(_weaponMain, new Vector3(0f, stride * _moveAmount * 0.030f + breath * 0.5f, 0f), new Vector3(stride * _moveAmount * 5.0f, 0f, idle * 1.0f + _moveAmount * 2.5f));
            Apply(_weaponOff, new Vector3(0f, counterStride * _moveAmount * 0.026f + breath * 0.4f, 0f), new Vector3(counterStride * _moveAmount * 5.0f, 0f, -idle * 1.0f - _moveAmount * 2.5f));

            ApplyGroup(_capeParts, new Vector3(0f, breath * 0.7f, -0.020f - _moveAmount * 0.050f), new Vector3(5.0f + _moveAmount * 8.0f + idle * 1.8f, 0f, stride * _moveAmount * 2.2f));
            ApplyGroup(_hairParts, new Vector3(0f, breath * 0.45f, -_moveAmount * 0.012f), new Vector3(idle * 1.4f + _moveAmount * 2.0f, 0f, sway * 0.8f));
            ApplyGroup(_robeParts, new Vector3(0f, breath * 0.5f, -_moveAmount * 0.026f), new Vector3(2.0f + _moveAmount * 5.5f, 0f, stride * _moveAmount * 1.4f));
            ApplyGroup(_detailParts, new Vector3(0f, breath * 0.35f, 0f), new Vector3(idle * 0.4f, 0f, sway * 0.25f));
            ApplyGroup(_backGearParts, new Vector3(0f, breath * 0.35f, -_moveAmount * 0.016f), new Vector3(2.0f + _moveAmount * 3.8f, 0f, stride * _moveAmount * 1.2f));
        }

        private MotionPart Bind(string partName)
        {
            Transform part = FindPart(partName);
            return new MotionPart
            {
                Transform = part,
                BasePosition = part != null ? part.localPosition : Vector3.zero,
                BaseRotation = part != null ? part.localRotation : Quaternion.identity
            };
        }

        private MotionPart[] BindContaining(params string[] names)
        {
            var parts = new System.Collections.Generic.List<MotionPart>();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (child.name.Contains(names[i]))
                    {
                        parts.Add(new MotionPart
                        {
                            Transform = child,
                            BasePosition = child.localPosition,
                            BaseRotation = child.localRotation
                        });
                        break;
                    }
                }
            }

            return parts.ToArray();
        }

        private MotionPart[] BindExact(params string[] names)
        {
            var parts = new System.Collections.Generic.List<MotionPart>();
            for (int i = 0; i < names.Length; i++)
            {
                Transform part = FindPart(names[i]);
                if (part == null)
                {
                    continue;
                }

                parts.Add(new MotionPart
                {
                    Transform = part,
                    BasePosition = part.localPosition,
                    BaseRotation = part.localRotation
                });
            }

            return parts.ToArray();
        }

        private Transform FindPart(string partName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == partName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void Apply(MotionPart part, Vector3 positionOffset, Vector3 eulerOffset)
        {
            if (part.Transform == null)
            {
                return;
            }

            part.Transform.localPosition = part.BasePosition + positionOffset;
            part.Transform.localRotation = part.BaseRotation * Quaternion.Euler(eulerOffset);
        }

        private static void ApplyGroup(MotionPart[] parts, Vector3 positionOffset, Vector3 eulerOffset)
        {
            if (parts == null)
            {
                return;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                Apply(parts[i], positionOffset, eulerOffset);
            }
        }
    }

    public sealed class ProceduralChampionSurfaceResponse : MonoBehaviour
    {
        private struct SurfacePart
        {
            public Renderer Renderer;
            public Material Material;
            public Transform Transform;
            public Vector3 BaseScale;
            public float Strength;
            public float Speed;
            public float Phase;
            public bool ScalePulse;
        }

        private SurfacePart[] _parts = System.Array.Empty<SurfacePart>();
        private float _seed;

        public void Rebind()
        {
            var parts = new System.Collections.Generic.List<SurfacePart>();
            _seed = Mathf.Abs(GetInstanceID() * 0.137f) % 10f;

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !TryGetSurfaceProfile(renderer.gameObject.name, out float strength, out float speed, out bool scalePulse))
                {
                    continue;
                }

                var material = renderer.material;
                if (material != null && material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                }

                parts.Add(new SurfacePart
                {
                    Renderer = renderer,
                    Material = material,
                    Transform = renderer.transform,
                    BaseScale = renderer.transform.localScale,
                    Strength = strength,
                    Speed = speed,
                    Phase = i * 0.47f + _seed,
                    ScalePulse = scalePulse
                });
            }

            _parts = parts.ToArray();
        }

        private void LateUpdate()
        {
            if (_parts == null || _parts.Length == 0)
            {
                Rebind();
                return;
            }

            float time = Time.time + _seed;
            for (int i = 0; i < _parts.Length; i++)
            {
                var part = _parts[i];
                if (part.Renderer == null || part.Material == null)
                {
                    continue;
                }

                float pulse = 0.68f + Mathf.Sin(time * part.Speed + part.Phase) * 0.18f + Mathf.Sin(time * (part.Speed * 0.43f) + part.Phase * 1.7f) * 0.10f;
                pulse = Mathf.Clamp01(pulse);
                Color baseColor = part.Material.HasProperty("_Color") ? part.Material.GetColor("_Color") : part.Material.color;

                if (part.Material.HasProperty("_EmissionColor"))
                {
                    part.Material.SetColor("_EmissionColor", baseColor * (part.Strength * pulse));
                }

                if (part.Material.HasProperty("_Glossiness"))
                {
                    part.Material.SetFloat("_Glossiness", Mathf.Clamp01(0.56f + part.Strength * 0.42f + pulse * 0.08f));
                }

                if (part.ScalePulse && part.Transform != null)
                {
                    float scale = 1f + pulse * 0.018f;
                    part.Transform.localScale = part.BaseScale * scale;
                }
            }
        }

        private static bool TryGetSurfaceProfile(string partName, out float strength, out float speed, out bool scalePulse)
        {
            string name = partName.ToLowerInvariant();
            strength = 0f;
            speed = 1f;
            scalePulse = false;

            if (name.Contains("eye_glint"))
            {
                strength = 0.42f;
                speed = 2.7f;
                scalePulse = true;
                return true;
            }

            if (name.Contains("eye_"))
            {
                strength = 0.22f;
                speed = 1.9f;
                return true;
            }

            if (name.Contains("orb") ||
                name.Contains("crystal") ||
                name.Contains("backattachment_core") ||
                name.Contains("reliccore") ||
                name.Contains("relichalo") ||
                name.Contains("gem") ||
                name.Contains("rune") ||
                name.Contains("scabbard_rune") ||
                name.Contains("coreline") ||
                name.Contains("impactcore") ||
                name.Contains("orbitstone") ||
                name.Contains("commandseal") ||
                name.Contains("arcane_focus"))
            {
                strength = 0.54f;
                speed = 1.45f;
                scalePulse = name.Contains("orb") || name.Contains("crystal") || name.Contains("reliccore") || name.Contains("gem") || name.Contains("impactcore") || name.Contains("orbitstone");
                return true;
            }

            if (name.Contains("facemark") ||
                name.Contains("etching") ||
                name.Contains("crest") ||
                name.Contains("trim") ||
                name.Contains("sashplate") ||
                name.Contains("fieldmedal") ||
                name.Contains("harnessring") ||
                name.Contains("quiver_rim") ||
                name.Contains("hammerhook_rivet") ||
                name.Contains("tassel_weight") ||
                name.Contains("cape_pin") ||
                name.Contains("cape_chain") ||
                name.Contains("backattachment_wing") ||
                name.Contains("hammer_rune") ||
                name.Contains("tome_clasp"))
            {
                strength = 0.16f;
                speed = 1.18f;
                return true;
            }

            if (name.Contains("edge") ||
                name.Contains("blade") ||
                name.Contains("rivet") ||
                name.Contains("ridge") ||
                name.Contains("boss") ||
                name.Contains("buckle"))
            {
                strength = 0.07f;
                speed = 0.96f;
                return true;
            }

            return false;
        }
    }
}
