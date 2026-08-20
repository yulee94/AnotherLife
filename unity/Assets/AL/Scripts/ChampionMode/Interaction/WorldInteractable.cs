using UnityEngine;

namespace AL.ChampionMode.Interaction
{
    public readonly struct WorldInteractionResult
    {
        public WorldInteractionResult(bool accepted, string catalogId, WorldInteractionKind kind, string feedback)
        {
            Accepted = accepted;
            CatalogId = catalogId;
            Kind = kind;
            Feedback = feedback;
        }

        public bool Accepted { get; }
        public string CatalogId { get; }
        public WorldInteractionKind Kind { get; }
        public string Feedback { get; }
    }

    public sealed class WorldInteractable : MonoBehaviour
    {
        public const float DefaultMaxDistance = 4.6f;
        public const float DefaultMaxAngleDegrees = 48f;

        public string CatalogId { get; private set; }
        public WorldInteractionKind Kind { get; private set; }
        public string Subject { get; private set; }
        public string AuthoredObjectiveText { get; private set; }
        public float MaxDistance { get; private set; } = DefaultMaxDistance;
        public float MaxAngleDegrees { get; private set; } = DefaultMaxAngleDegrees;
        public int ConfirmCount { get; private set; }

        public void Configure(
            string catalogId,
            WorldInteractionKind kind,
            string subject,
            string authoredObjectiveText,
            float maxDistance = DefaultMaxDistance,
            float maxAngleDegrees = DefaultMaxAngleDegrees)
        {
            CatalogId = catalogId;
            Kind = kind;
            Subject = subject;
            AuthoredObjectiveText = authoredObjectiveText;
            MaxDistance = maxDistance;
            MaxAngleDegrees = maxAngleDegrees;
        }

        public WorldInteractionCandidate ToCandidate()
        {
            return new WorldInteractionCandidate(
                CatalogId,
                transform.position,
                Kind,
                MaxDistance,
                MaxAngleDegrees);
        }

        public WorldInteractionResult Confirm(bool actorAvailable)
        {
            if (!WorldInteractionPolicy.CanConfirm(actorAvailable) || string.IsNullOrEmpty(CatalogId))
            {
                return new WorldInteractionResult(false, CatalogId, Kind, string.Empty);
            }

            ConfirmCount++;
            return new WorldInteractionResult(true, CatalogId, Kind, AuthoredObjectiveText);
        }
    }
}
