using UnityEngine;
using UnityEngine.AI;

namespace AL.World.Streaming
{
    [DisallowMultipleComponent]
    public sealed class WorldChunkNavigationData : MonoBehaviour
    {
        [SerializeField] private NavMeshData bakedNavigationData;

        private NavMeshDataInstance registeredInstance;
        private bool hasRegisteredInstance;

        public NavMeshData BakedNavigationData => bakedNavigationData;
        public bool HasBakedNavigationData => bakedNavigationData != null;
        public bool IsRegistered =>
            hasRegisteredInstance && registeredInstance.valid;

        public void Configure(NavMeshData navigationData)
        {
            if (bakedNavigationData == navigationData)
            {
                return;
            }

            RemoveRegisteredData();
            bakedNavigationData = navigationData;
            if (isActiveAndEnabled)
            {
                RegisterBakedData();
            }
        }

        private void OnEnable()
        {
            RegisterBakedData();
        }

        private void OnDisable()
        {
            RemoveRegisteredData();
        }

        private void OnDestroy()
        {
            RemoveRegisteredData();
        }

        private void RegisterBakedData()
        {
            if (bakedNavigationData == null || IsRegistered)
            {
                return;
            }

            registeredInstance = NavMesh.AddNavMeshData(
                bakedNavigationData,
                transform.position,
                transform.rotation);
            hasRegisteredInstance = registeredInstance.valid;
        }

        private void RemoveRegisteredData()
        {
            if (hasRegisteredInstance)
            {
                registeredInstance.Remove();
            }

            registeredInstance = default;
            hasRegisteredInstance = false;
        }
    }
}
