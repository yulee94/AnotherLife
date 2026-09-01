using UnityEngine;

namespace AL.ChampionMode.Interaction
{
    [DisallowMultipleComponent]
    public sealed class WorldSpaceTextBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera != null)
            {
                Face(camera.transform);
            }
        }

        public void Face(Transform cameraTransform)
        {
            if (cameraTransform == null)
            {
                return;
            }

            Vector3 awayFromCamera = transform.position - cameraTransform.position;
            if (awayFromCamera.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(awayFromCamera.normalized, Vector3.up);
        }
    }
}
