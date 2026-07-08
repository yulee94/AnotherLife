using UnityEngine;
using AL.ChampionMode.Camera;

namespace AL.Utilities
{
    public class CameraFixer : MonoBehaviour
    {
        [ContextMenu("Force Camera Reset")]
        public void ForceReset()
        {
            Debug.Log("<color=yellow>[CameraFixer] Manually forcing camera reconnection...</color>");

            CameraFollow camFollow = FindObjectOfType<CameraFollow>();
            if (camFollow == null)
            {
                camFollow = Camera.main.gameObject.AddComponent<CameraFollow>();
                Debug.Log("[CameraFixer] Added CameraFollow component to Main Camera.");
            }

            GameObject player = GameObject.Find("Player_Champion");
            if (player != null)
            {
                // We use reflection to set private target if necessary,
                // but since it's a demo we can just let auto-discovery handle it
                Debug.Log("[CameraFixer] Player found. CameraFollow should lock in next LateUpdate.");
            }
            else
            {
                Debug.LogError("[CameraFixer] Could not find 'Player_Champion'. Ensure WorldBuilder has run!");
            }
        }
    }
}
