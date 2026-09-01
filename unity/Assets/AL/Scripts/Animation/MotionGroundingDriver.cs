using System;
using System.Collections.Generic;
using UnityEngine;

namespace AL.Motion
{
    public sealed class MotionGroundContactBinding
    {
        public MotionGroundContactBinding(
            string contactId,
            Transform source,
            Transform target,
            bool useHumanoidGoal,
            AvatarIKGoal humanoidGoal)
        {
            if (string.IsNullOrWhiteSpace(contactId))
            {
                throw new ArgumentException("Contact ID is required.", nameof(contactId));
            }

            ContactId = contactId;
            Source = source != null
                ? source
                : throw new ArgumentNullException(nameof(source));
            Target = target != null
                ? target
                : throw new ArgumentNullException(nameof(target));
            UseHumanoidGoal = useHumanoidGoal;
            HumanoidGoal = humanoidGoal;
        }

        public string ContactId { get; }
        public Transform Source { get; }
        public Transform Target { get; }
        public bool UseHumanoidGoal { get; }
        public AvatarIKGoal HumanoidGoal { get; }
    }

    public sealed class MotionGroundingDriver : MonoBehaviour
    {
        [SerializeField] private LayerMask physicalGroundLayers = ~0;
        [SerializeField] private float probeHeightMeters = 0.25f;
        [SerializeField] private float probeDistanceMeters = 0.75f;

        private readonly Dictionary<string, MotionGroundContactBinding> _contacts =
            new Dictionary<string, MotionGroundContactBinding>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _weights =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private float _maximumHorizontalMeters = 0.02f;
        private float _maximumVerticalMeters = 0.01f;
        private Animator _animator;

        public void Configure(
            IEnumerable<MotionGroundContactBinding> contacts,
            float maximumHorizontalMeters,
            float maximumVerticalMeters)
        {
            if (maximumHorizontalMeters < 0f || maximumVerticalMeters < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumHorizontalMeters),
                    "Ground correction limits cannot be negative.");
            }

            _contacts.Clear();
            _weights.Clear();
            foreach (MotionGroundContactBinding contact in contacts ??
                     throw new ArgumentNullException(nameof(contacts)))
            {
                if (contact == null || !_contacts.TryAdd(contact.ContactId, contact))
                {
                    throw new InvalidOperationException(
                        "Ground contacts must be non-null and unique.");
                }

                _weights.Add(contact.ContactId, 0f);
            }

            _maximumHorizontalMeters = maximumHorizontalMeters;
            _maximumVerticalMeters = maximumVerticalMeters;
            _animator = GetComponentInChildren<Animator>(true);
        }

        public bool SetContactWeight(string contactId, float weight)
        {
            if (!_weights.ContainsKey(contactId ?? string.Empty))
            {
                return false;
            }

            _weights[contactId] = Mathf.Clamp01(weight);
            return true;
        }

        public float GetContactWeight(string contactId)
        {
            return _weights.TryGetValue(contactId ?? string.Empty, out float weight)
                ? weight
                : 0f;
        }

        public bool ApplyGroundSample(
            string contactId,
            Vector3 groundPosition,
            Vector3 groundNormal)
        {
            if (!_contacts.TryGetValue(
                    contactId ?? string.Empty,
                    out MotionGroundContactBinding contact) ||
                !_weights.TryGetValue(contact.ContactId, out float weight) ||
                weight <= 0f || groundNormal.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            Vector3 correction = MotionGroundingMath.ClampContactCorrection(
                contact.Source.position,
                groundPosition,
                _maximumHorizontalMeters,
                _maximumVerticalMeters);
            contact.Target.position = contact.Source.position + correction * weight;
            contact.Target.rotation =
                Quaternion.FromToRotation(contact.Target.up, groundNormal.normalized) *
                contact.Target.rotation;
            return true;
        }

        private void LateUpdate()
        {
            foreach (KeyValuePair<string, MotionGroundContactBinding> pair in _contacts)
            {
                if (GetContactWeight(pair.Key) <= 0f)
                {
                    continue;
                }

                MotionGroundContactBinding contact = pair.Value;
                var ray = new Ray(
                    contact.Source.position + Vector3.up * probeHeightMeters,
                    Vector3.down);
                if (Physics.Raycast(
                        ray,
                        out RaycastHit hit,
                        probeHeightMeters + probeDistanceMeters,
                        physicalGroundLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    ApplyGroundSample(pair.Key, hit.point, hit.normal);
                }
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || !_animator.isHuman)
            {
                return;
            }

            foreach (KeyValuePair<string, MotionGroundContactBinding> pair in _contacts)
            {
                MotionGroundContactBinding contact = pair.Value;
                if (!contact.UseHumanoidGoal)
                {
                    continue;
                }

                float weight = GetContactWeight(pair.Key);
                _animator.SetIKPositionWeight(contact.HumanoidGoal, weight);
                _animator.SetIKRotationWeight(contact.HumanoidGoal, weight);
                if (weight > 0f)
                {
                    _animator.SetIKPosition(contact.HumanoidGoal, contact.Target.position);
                    _animator.SetIKRotation(contact.HumanoidGoal, contact.Target.rotation);
                }
            }
        }
    }
}
