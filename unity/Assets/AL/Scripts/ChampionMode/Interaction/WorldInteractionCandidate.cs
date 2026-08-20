using UnityEngine;

namespace AL.ChampionMode.Interaction
{
    public readonly struct WorldInteractionCandidate
    {
        public WorldInteractionCandidate(
            string catalogId,
            Vector3 position,
            WorldInteractionKind kind,
            float maxDistance,
            float maxAngleDegrees)
        {
            CatalogId = catalogId;
            Position = position;
            Kind = kind;
            MaxDistance = maxDistance;
            MaxAngleDegrees = maxAngleDegrees;
        }

        public string CatalogId { get; }
        public Vector3 Position { get; }
        public WorldInteractionKind Kind { get; }
        public float MaxDistance { get; }
        public float MaxAngleDegrees { get; }
    }
}
