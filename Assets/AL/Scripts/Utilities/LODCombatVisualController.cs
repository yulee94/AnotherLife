using UnityEngine;

namespace AL.Utilities
{
    public class LODCombatVisualController : MonoBehaviour
    {
        [Header("Distance Settings")]
        [SerializeField] private float _highDetailRange = 15f;
        [SerializeField] private float _mediumDetailRange = 40f;

        [Header("References")]
        [SerializeField] private GameObject _highDetailModel;
        [SerializeField] private GameObject _mediumDetailModel;
        [SerializeField] private GameObject _lowDetailMarker;

        private Transform _cameraTransform;

        private void Start()
        {
            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            if (_cameraTransform == null) return;

            float distance = Vector3.Distance(transform.position, _cameraTransform.position);

            if (distance < _highDetailRange)
            {
                SetLOD(true, false, false);
            }
            else if (distance < _mediumDetailRange)
            {
                SetLOD(false, true, false);
            }
            else
            {
                SetLOD(false, false, true);
            }
        }

        private void SetLOD(bool high, bool medium, bool low)
        {
            if (_highDetailModel != null) _highDetailModel.SetActive(high);
            if (_mediumDetailModel != null) _mediumDetailModel.SetActive(medium);
            if (_lowDetailMarker != null) _lowDetailMarker.SetActive(low);
        }
    }
}
