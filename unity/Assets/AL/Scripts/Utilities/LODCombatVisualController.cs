using UnityEngine;

namespace AL.Utilities
{
    public class LODCombatVisualController : MonoBehaviour
    {
        [Header("Distance Settings")]
        [SerializeField] private float _highDetailRange = 15f;
        [SerializeField] private float _mediumDetailRange = 40f;
        [SerializeField] private float _markerOnlyRange = 80f;

        [Header("References")]
        [SerializeField] private GameObject _highDetailModel;
        [SerializeField] private GameObject _mediumDetailModel;
        [SerializeField] private GameObject _lowDetailMarker;

        private Transform _cameraTransform;
        private int _currentLod = -1;

        private void Start()
        {
            TryFindCamera();
        }

        public void Configure(GameObject highDetailModel, GameObject mediumDetailModel, GameObject lowDetailMarker)
        {
            _highDetailModel = highDetailModel;
            _mediumDetailModel = mediumDetailModel;
            _lowDetailMarker = lowDetailMarker;
            _currentLod = -1;
            ApplyLod(0);
        }

        private void Update()
        {
            if (_cameraTransform == null)
            {
                TryFindCamera();
                if (_cameraTransform == null)
                {
                    return;
                }
            }

            float distance = Vector3.Distance(transform.position, _cameraTransform.position);

            if (distance < _highDetailRange)
            {
                ApplyLod(0);
            }
            else if (distance < _mediumDetailRange)
            {
                ApplyLod(1);
            }
            else if (distance < _markerOnlyRange)
            {
                ApplyLod(2);
            }
            else
            {
                ApplyLod(3);
            }
        }

        private void ApplyLod(int lod)
        {
            if (_currentLod == lod)
            {
                return;
            }

            _currentLod = lod;
            SetActiveIfSafe(_highDetailModel, lod == 0);
            SetActiveIfSafe(_mediumDetailModel, lod == 1);
            SetActiveIfSafe(_lowDetailMarker, lod == 2);
        }

        private void TryFindCamera()
        {
            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void SetActiveIfSafe(GameObject target, bool active)
        {
            if (target == null || target == gameObject || target.activeSelf == active)
            {
                return;
            }

            target.SetActive(active);
        }
    }
}
