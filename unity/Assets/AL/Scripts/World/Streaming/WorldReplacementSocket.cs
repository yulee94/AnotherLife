using UnityEngine;

namespace AL.World.Streaming
{
    [DisallowMultipleComponent]
    public sealed class WorldReplacementSocket : MonoBehaviour
    {
        [SerializeField] private string socketId = string.Empty;
        [SerializeField] private string replacementRole = string.Empty;
        [SerializeField] private Vector3 footprintMeters = Vector3.one;
        [SerializeField] private bool preservePivot = true;
        [SerializeField] private bool preserveTraversalContract = true;

        public string SocketId => socketId;
        public string ReplacementRole => replacementRole;
        public Vector3 FootprintMeters => footprintMeters;
        public bool PreservePivot => preservePivot;
        public bool PreserveTraversalContract => preserveTraversalContract;

        public void Configure(
            string configuredSocketId,
            string configuredReplacementRole,
            Vector3 configuredFootprintMeters)
        {
            socketId = configuredSocketId ?? string.Empty;
            replacementRole = configuredReplacementRole ?? string.Empty;
            footprintMeters = new Vector3(
                Mathf.Max(0.1f, configuredFootprintMeters.x),
                Mathf.Max(0.1f, configuredFootprintMeters.y),
                Mathf.Max(0.1f, configuredFootprintMeters.z));
            preservePivot = true;
            preserveTraversalContract = true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.22f, 0.76f, 1f, 0.65f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * footprintMeters.y * 0.5f, footprintMeters);
        }
    }
}