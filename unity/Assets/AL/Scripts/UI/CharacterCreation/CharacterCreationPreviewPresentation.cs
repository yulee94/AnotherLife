using UnityEngine;

namespace AL.UI.CharacterCreation
{
    /// <summary>
    /// Deterministic, creator-owned preview lighting. It never relies on or
    /// mutates unrelated lights in the loaded scene.
    /// </summary>
    public static class CharacterCreationPreviewPresentation
    {
        public const string KeyLightName = "CreatorKeyLight";
        public const string FillLightName = "CreatorFillLight";

        public static Light EnsureOwnedLights(Transform owner)
        {
            if (owner == null)
            {
                return null;
            }

            Light key = ConfigureOwnedDirectional(
                owner,
                KeyLightName,
                1.35f,
                new Color(1f, 0.94f, 0.86f, 1f),
                new Vector3(32f, -28f, 0f));
            ConfigureOwnedDirectional(
                owner,
                FillLightName,
                0.45f,
                new Color(0.62f, 0.72f, 1f, 1f),
                new Vector3(18f, 35f, 0f));
            return key;
        }

        public static bool TryFrame(Camera camera, Transform previewRoot)
        {
            if (camera == null || previewRoot == null)
            {
                return false;
            }

            Renderer[] renderers = previewRoot.GetComponentsInChildren<Renderer>(false);
            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            camera.fieldOfView = 30f;
            float verticalTangent = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = Mathf.Max(0.25f, camera.aspect);
            float horizontalTangent = verticalTangent * aspect;
            float verticalDistance =
                bounds.size.y / (2f * verticalTangent * 0.74f);
            float horizontalDistance =
                bounds.size.x / (2f * horizontalTangent * 0.72f);
            float distance = Mathf.Clamp(
                Mathf.Max(verticalDistance, horizontalDistance),
                4.2f,
                5.8f);

            Vector3 viewDirection = bounds.center - camera.transform.position;
            if (viewDirection.sqrMagnitude < 0.001f)
            {
                viewDirection = previewRoot.forward;
            }

            viewDirection.Normalize();
            camera.transform.position = bounds.center - viewDirection * distance;
            camera.transform.rotation = Quaternion.LookRotation(viewDirection, Vector3.up);
            return true;
        }

        private static Light ConfigureOwnedDirectional(
            Transform owner,
            string objectName,
            float intensity,
            Color color,
            Vector3 rotation)
        {
            Transform existing = owner.Find(objectName);
            GameObject lightObject = existing != null
                ? existing.gameObject
                : new GameObject(objectName);
            if (existing == null)
            {
                lightObject.transform.SetParent(owner, false);
            }

            Light light = lightObject.GetComponent<Light>();
            if (light == null)
            {
                light = lightObject.AddComponent<Light>();
            }

            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForceVertex;
            light.cookie = null;
            light.flare = null;
            lightObject.transform.localPosition = Vector3.zero;
            lightObject.transform.localRotation = Quaternion.Euler(rotation);
            lightObject.transform.localScale = Vector3.one;
            return light;
        }
    }
}
