using System;

namespace AL.UI
{
    public enum LaunchCinematicPlaybackState
    {
        Idle,
        Preparing,
        AwaitingFirstFrame,
        Playing,
        SkipEligible,
        Completed,
        Fallback
    }

    public enum LaunchCinematicPlaybackTerminalReason
    {
        None,
        Completed,
        Skipped,
        PrepareTimedOut,
        MediaUnavailable,
        ReducedMotionFallback,
        ManifestFallbackOnly,
        PlaybackStalled,
        PlaybackFailed
    }

    public readonly struct LaunchCinematicPlaybackTerminal
    {
        internal LaunchCinematicPlaybackTerminal(
            int generation,
            LaunchCinematicPlaybackTerminalReason reason,
            string detail)
        {
            Generation = generation;
            Reason = reason;
            Detail = detail ?? string.Empty;
        }

        public int Generation { get; }
        public LaunchCinematicPlaybackTerminalReason Reason { get; }
        public string Detail { get; }
        public bool IsTerminal => Reason != LaunchCinematicPlaybackTerminalReason.None;
    }

    public readonly struct LaunchCinematicPlaybackAttempt
    {
        internal LaunchCinematicPlaybackAttempt(
            int generation,
            bool accepted,
            LaunchCinematicValidationResult validation,
            LaunchCinematicRuntimeRecord snapshot)
        {
            Generation = generation;
            Accepted = accepted;
            Validation = validation;
            CinematicId = snapshot?.CinematicId ?? string.Empty;
            StreamingAssetsPath = snapshot?.StreamingAssetsPath ?? string.Empty;
            Width = snapshot?.Width ?? 0;
            Height = snapshot?.Height ?? 0;
        }

        public int Generation { get; }
        public bool Accepted { get; }
        public LaunchCinematicValidationResult Validation { get; }
        public string CinematicId { get; }
        public string StreamingAssetsPath { get; }
        public int Width { get; }
        public int Height { get; }
    }

    /// <summary>
    /// Pure lifecycle authority for one optional launch-media owner. Each attempt receives a
    /// generation so callbacks from stopped or replaced decoders cannot complete a newer attempt.
    /// The coordinator owns no input, file access, player, render target, or scene transition.
    /// </summary>
    public sealed class LaunchCinematicPlaybackCoordinator
    {
        private const int MaximumDetailLength = 160;

        private int _generation;
        private int _skipEligibilityFrame;
        private float _prepareTimeoutSeconds;
        private long _lastObservedFrame;

        public LaunchCinematicPlaybackState State { get; private set; } =
            LaunchCinematicPlaybackState.Idle;

        public LaunchCinematicPlaybackTerminal Terminal { get; private set; }
        public int TerminalCount { get; private set; }
        public int Generation => _generation;

        public LaunchCinematicPlaybackAttempt Begin(
            LaunchCinematicRuntimeRecord record,
            LaunchCinematicPlatform buildPlatform,
            bool releaseBuild,
            bool reducedMotion)
        {
            AdvanceGeneration();
            State = LaunchCinematicPlaybackState.Idle;
            Terminal = default;
            TerminalCount = 0;
            _skipEligibilityFrame = 0;
            _prepareTimeoutSeconds = 0f;
            _lastObservedFrame = -1;

            LaunchCinematicRuntimeRecord snapshot = Snapshot(record);
            LaunchCinematicValidationResult validation =
                LaunchCinematicRuntimeValidator.Validate(
                    snapshot,
                    buildPlatform,
                    releaseBuild);

            if (reducedMotion)
            {
                Finish(
                    LaunchCinematicPlaybackTerminalReason.ReducedMotionFallback,
                    "reduced-motion");
                return new LaunchCinematicPlaybackAttempt(
                    _generation,
                    accepted: false,
                    validation,
                    snapshot);
            }

            if (snapshot != null && snapshot.ReducedMotionFallbackOnly)
            {
                Finish(
                    LaunchCinematicPlaybackTerminalReason.ManifestFallbackOnly,
                    "manifest-fallback-only");
                return new LaunchCinematicPlaybackAttempt(
                    _generation,
                    accepted: false,
                    validation,
                    snapshot);
            }

            if (!validation.IsValid)
            {
                Finish(
                    LaunchCinematicPlaybackTerminalReason.MediaUnavailable,
                    "runtime-record-invalid");
                return new LaunchCinematicPlaybackAttempt(
                    _generation,
                    accepted: false,
                    validation,
                    snapshot);
            }

            // Copy the mutable record values once. Later caller mutation cannot change the
            // active attempt's timeout or skip boundary.
            _skipEligibilityFrame = snapshot.SkipEligibilityFrame;
            _prepareTimeoutSeconds = snapshot.PrepareTimeoutSeconds;
            State = LaunchCinematicPlaybackState.Preparing;
            return new LaunchCinematicPlaybackAttempt(
                _generation,
                accepted: true,
                validation,
                snapshot);
        }

        public bool TryMarkPrepared(int generation)
        {
            if (!IsCurrent(generation) ||
                State != LaunchCinematicPlaybackState.Preparing)
            {
                return false;
            }

            State = LaunchCinematicPlaybackState.AwaitingFirstFrame;
            return true;
        }

        public bool TryMarkFirstFrameVisible(int generation, long frame)
        {
            if (!IsCurrent(generation) ||
                State != LaunchCinematicPlaybackState.AwaitingFirstFrame ||
                frame < 0)
            {
                return false;
            }

            State = frame >= _skipEligibilityFrame
                ? LaunchCinematicPlaybackState.SkipEligible
                : LaunchCinematicPlaybackState.Playing;
            _lastObservedFrame = frame;
            return true;
        }

        public bool TryObservePlaybackFrame(int generation, long frame)
        {
            if (!IsCurrent(generation) ||
                (State != LaunchCinematicPlaybackState.Playing &&
                 State != LaunchCinematicPlaybackState.SkipEligible) ||
                frame <= _lastObservedFrame)
            {
                return false;
            }

            _lastObservedFrame = frame;
            if (State == LaunchCinematicPlaybackState.Playing &&
                frame >= _skipEligibilityFrame)
            {
                State = LaunchCinematicPlaybackState.SkipEligible;
            }

            return true;
        }

        public bool TryAdvanceFrame(int generation, long frame)
        {
            if (!IsCurrent(generation) ||
                State != LaunchCinematicPlaybackState.Playing ||
                frame < _skipEligibilityFrame)
            {
                return false;
            }

            return TryObservePlaybackFrame(generation, frame);
        }

        public bool TrySkip(int generation)
        {
            if (!IsCurrent(generation) ||
                State != LaunchCinematicPlaybackState.SkipEligible)
            {
                return false;
            }

            return Finish(
                LaunchCinematicPlaybackTerminalReason.Skipped,
                "eligible-skip");
        }

        public bool TryComplete(int generation)
        {
            if (!IsCurrent(generation) ||
                (State != LaunchCinematicPlaybackState.Playing &&
                 State != LaunchCinematicPlaybackState.SkipEligible))
            {
                return false;
            }

            return Finish(
                LaunchCinematicPlaybackTerminalReason.Completed,
                "playback-complete");
        }

        public bool TryPrepareTimedOut(int generation, float elapsedSeconds)
        {
            if (!IsCurrent(generation) ||
                (State != LaunchCinematicPlaybackState.Preparing &&
                 State != LaunchCinematicPlaybackState.AwaitingFirstFrame) ||
                float.IsNaN(elapsedSeconds) ||
                elapsedSeconds < _prepareTimeoutSeconds)
            {
                return false;
            }

            return Finish(
                LaunchCinematicPlaybackTerminalReason.PrepareTimedOut,
                "prepare-timeout");
        }

        public bool TryPlaybackStalled(int generation, float elapsedWithoutProgressSeconds)
        {
            if (!IsCurrent(generation) ||
                (State != LaunchCinematicPlaybackState.Playing &&
                 State != LaunchCinematicPlaybackState.SkipEligible) ||
                float.IsNaN(elapsedWithoutProgressSeconds) ||
                elapsedWithoutProgressSeconds < _prepareTimeoutSeconds)
            {
                return false;
            }

            // The approved manifest's bounded media-prepare window is also the maximum interval
            // in which an active decoder may produce no strictly newer frame. This keeps optional
            // playback from stranding onboarding without introducing an unversioned second timeout.
            return Finish(
                LaunchCinematicPlaybackTerminalReason.PlaybackStalled,
                "playback-stalled");
        }

        public bool TryFail(int generation, string detail)
        {
            if (!IsCurrent(generation) || IsTerminalState(State))
            {
                return false;
            }

            return Finish(
                LaunchCinematicPlaybackTerminalReason.PlaybackFailed,
                NormalizeDetail(detail, "playback-failed"));
        }

        private bool Finish(
            LaunchCinematicPlaybackTerminalReason reason,
            string detail)
        {
            if (reason == LaunchCinematicPlaybackTerminalReason.None ||
                IsTerminalState(State))
            {
                return false;
            }

            State = reason == LaunchCinematicPlaybackTerminalReason.Completed
                ? LaunchCinematicPlaybackState.Completed
                : LaunchCinematicPlaybackState.Fallback;
            Terminal = new LaunchCinematicPlaybackTerminal(
                _generation,
                reason,
                NormalizeDetail(detail, "fallback"));
            TerminalCount++;
            return true;
        }

        private bool IsCurrent(int generation)
        {
            return generation > 0 && generation == _generation;
        }

        private static bool IsTerminalState(LaunchCinematicPlaybackState state)
        {
            return state == LaunchCinematicPlaybackState.Completed ||
                   state == LaunchCinematicPlaybackState.Fallback;
        }

        private void AdvanceGeneration()
        {
            _generation = _generation == int.MaxValue ? 1 : _generation + 1;
        }

        private static LaunchCinematicRuntimeRecord Snapshot(
            LaunchCinematicRuntimeRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new LaunchCinematicRuntimeRecord
            {
                Schema = record.Schema,
                Version = record.Version,
                CinematicId = record.CinematicId,
                Platform = record.Platform,
                StreamingAssetsPath = record.StreamingAssetsPath,
                Container = record.Container,
                CodecProfile = record.CodecProfile,
                Width = record.Width,
                Height = record.Height,
                FramesPerSecond = record.FramesPerSecond,
                FrameCount = record.FrameCount,
                DurationSeconds = record.DurationSeconds,
                ByteLength = record.ByteLength,
                Sha256 = record.Sha256,
                PrepareTimeoutSeconds = record.PrepareTimeoutSeconds,
                SkipEligibilityFrame = record.SkipEligibilityFrame,
                ApprovedForProduction = record.ApprovedForProduction,
                ProbeEvidenceApproved = record.ProbeEvidenceApproved,
                ReducedMotionFallbackOnly = record.ReducedMotionFallbackOnly
            };
        }

        private static string NormalizeDetail(string detail, string fallback)
        {
            string normalized = string.IsNullOrWhiteSpace(detail)
                ? fallback
                : detail.Trim();
            return normalized.Length <= MaximumDetailLength
                ? normalized
                : normalized.Substring(0, MaximumDetailLength);
        }
    }
}
