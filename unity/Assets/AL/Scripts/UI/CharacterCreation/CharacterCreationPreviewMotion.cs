using UnityEngine;

namespace AL.UI.CharacterCreation
{
    /// <summary>
    /// Gives the live creator stage restrained motion without moving player UI:
    /// a slow champion turn, breathing key light, and subtle background drift.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterCreationPreviewMotion : MonoBehaviour
    {
        public const string ComponentName = "CreatorPreviewMotion";

        private Transform _preview;
        private Light _keyLight;
        private float _keyLightBaseIntensity;
        private Camera _camera;
        private Quaternion _previewBaseRotation;
        private Quaternion _lightBaseRotation;
        private Color _backgroundBase;
        private float _seed;

        public void Configure(Transform preview, Light keyLight, Camera previewCamera)
        {
            _preview = preview;
            _keyLight = keyLight;
            _camera = previewCamera;
            if (_preview != null)
            {
                _previewBaseRotation = _preview.rotation;
            }
            if (_keyLight != null)
            {
                _lightBaseRotation = _keyLight.transform.rotation;
                _keyLightBaseIntensity = _keyLight.intensity;
            }
            if (_camera != null)
            {
                _backgroundBase = _camera.backgroundColor;
            }
            _seed = Mathf.Abs((preview != null ? preview.name : name).GetHashCode() % 997) * 0.001f;
        }

        private void Update()
        {
            float time = Time.unscaledTime + _seed;
            if (_preview != null)
            {
                float yaw = Mathf.Sin(time * 0.24f) * 7.5f;
                _preview.rotation = _previewBaseRotation * Quaternion.Euler(0f, yaw, 0f);
            }

            if (_keyLight != null)
            {
                float lightYaw = Mathf.Sin(time * 0.18f + 1.2f) * 5f;
                _keyLight.transform.rotation = _lightBaseRotation * Quaternion.Euler(0f, lightYaw, 0f);
                _keyLight.intensity = _keyLightBaseIntensity + (Mathf.Sin(time * 0.42f) * 0.075f);
            }

            if (_camera != null)
            {
                float drift = (Mathf.Sin(time * 0.16f) + 1f) * 0.5f;
                _camera.backgroundColor = Color.Lerp(
                    _backgroundBase,
                    new Color(
                        _backgroundBase.r + 0.018f,
                        _backgroundBase.g + 0.014f,
                        _backgroundBase.b + 0.028f,
                        1f),
                    drift);
            }
        }
    }
}
