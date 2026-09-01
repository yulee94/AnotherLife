using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AL.Motion
{
    [CreateAssetMenu(
        fileName = "MotionImportPresetRegistry",
        menuName = "Another Life/Motion/Import Preset Registry")]
    public sealed class MotionImportPresetRegistry : ScriptableObject
    {
        [SerializeField] private MotionImportBinding[] bindings =
            Array.Empty<MotionImportBinding>();

        private Dictionary<string, MotionImportBinding> _byPath;

        public IReadOnlyList<MotionImportBinding> Bindings => Array.AsReadOnly(bindings);

        public bool TryResolve(string assetPath, out MotionImportBinding binding)
        {
            EnsureIndex();
            return _byPath.TryGetValue(assetPath ?? string.Empty, out binding);
        }

        public void ValidateOrThrow()
        {
            EnsureIndex();
            foreach (MotionImportBinding binding in _byPath.Values)
            {
                if (binding.Preset == null || !binding.Preset.HasValidTechnicalIdentity())
                {
                    throw new InvalidOperationException(
                        "Motion import binding has no valid preset: " + binding.AssetPath);
                }

                string[] duplicateClipIds = binding.Clips
                    .Where(value => value != null)
                    .GroupBy(value => value.ClipId, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToArray();
                if (duplicateClipIds.Length != 0)
                {
                    throw new InvalidOperationException(
                        "Motion import binding has duplicate clip IDs: " +
                        string.Join(",", duplicateClipIds));
                }
            }
        }

        private void EnsureIndex()
        {
            if (_byPath != null)
            {
                return;
            }

            _byPath = new Dictionary<string, MotionImportBinding>(StringComparer.Ordinal);
            for (int index = 0; index < bindings.Length; index++)
            {
                MotionImportBinding binding = bindings[index];
                if (binding == null || string.IsNullOrWhiteSpace(binding.AssetPath) ||
                    !_byPath.TryAdd(binding.AssetPath, binding))
                {
                    throw new InvalidOperationException(
                        "Motion import paths must be non-empty, exact, and unique.");
                }
            }
        }

        private void OnValidate()
        {
            _byPath = null;
        }
    }
}
