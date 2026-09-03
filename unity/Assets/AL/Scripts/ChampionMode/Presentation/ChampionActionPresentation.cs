using System;
using UnityEngine;

namespace AL.ChampionMode.Presentation
{
    public enum ChampionActionKind
    {
        None = 0,
        Locomotion = 1,
        BasicAttack = 2,
        Skill = 3,
        Control = 4
    }

    public enum ChampionActionPhase
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        JumpStart = 3,
        Falling = 4,
        Landing = 5,
        Anticipation = 10,
        Casting = 11,
        Channeling = 12,
        Commit = 13,
        Impact = 14,
        Recovery = 15,
        HitReaction = 20,
        Rooted = 21,
        Silenced = 22,
        Stunned = 23,
        Knockdown = 24,
        GetUp = 25,
        Interrupted = 26
    }

    public readonly struct ChampionActionSignal
    {
        public ChampionActionSignal(
            ChampionActionKind kind,
            ChampionActionPhase phase,
            int slot,
            string actionId,
            float emittedAt)
        {
            Kind = kind;
            Phase = phase;
            Slot = slot;
            ActionId = actionId ?? string.Empty;
            EmittedAt = emittedAt;
        }

        public ChampionActionKind Kind { get; }
        public ChampionActionPhase Phase { get; }
        public int Slot { get; }
        public string ActionId { get; }
        public float EmittedAt { get; }
    }

    [DisallowMultipleComponent]
    public sealed class ChampionActionPresentation : MonoBehaviour
    {
        public event Action<ChampionActionSignal> SignalEmitted;

        public ChampionActionSignal CurrentSignal { get; private set; }

        public void Emit(
            ChampionActionKind kind,
            ChampionActionPhase phase,
            int slot = -1,
            string actionId = "")
        {
            CurrentSignal = new ChampionActionSignal(
                kind,
                phase,
                slot,
                actionId,
                Time.time);
            SignalEmitted?.Invoke(CurrentSignal);
        }
    }
}
