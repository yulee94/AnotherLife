using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AL.UI
{
    public static class LaunchCinematicMediaPath
    {
        public static bool TryResolve(
            string streamingAssetsRoot,
            string relativePath,
            out string mediaUrl)
        {
            mediaUrl = string.Empty;
            if (string.IsNullOrWhiteSpace(streamingAssetsRoot) ||
                string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            string normalized = relativePath.Replace('\\', '/');
            string[] segments = normalized.Split('/');
            bool hasDrivePrefix = normalized.Length >= 2 &&
                                  char.IsLetter(normalized[0]) &&
                                  normalized[1] == ':';
            if (normalized.StartsWith("/", StringComparison.Ordinal) ||
                hasDrivePrefix ||
                normalized.Contains("://", StringComparison.Ordinal) ||
                Array.Exists(
                    segments,
                    segment => string.Equals(segment, "..", StringComparison.Ordinal)))
            {
                return false;
            }

            mediaUrl = streamingAssetsRoot.TrimEnd('/', '\\') + "/" +
                       normalized.TrimStart('/');
            return true;
        }
    }

    /// <summary>
    /// Single-use Unity VideoPlayer adapter for one validated launch-media attempt. Production
    /// remains on the static fallback until an approved runtime record and scene-owned surface are
    /// supplied. Every optional-media failure returns a terminal fallback signal to the owner.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VideoPlayer))]
    [RequireComponent(typeof(AudioSource))]
    public sealed class LaunchCinematicVideoPlayerHost : MonoBehaviour
    {
        [SerializeField] private RawImage _surface;

        private readonly LaunchCinematicPlaybackCoordinator _coordinator =
            new LaunchCinematicPlaybackCoordinator();

        private VideoPlayer _videoPlayer;
        private AudioSource _audioSource;
        private RenderTexture _ownedRenderTexture;
        private int _activeGeneration;
        private int _publishedTerminalGeneration;
        private float _prepareStartedAt;
        private float _lastPlaybackProgressAt;
        private bool _attemptStarted;
        private bool _eventsSubscribed;

        public event Action<LaunchCinematicPlaybackTerminal> Terminated;

        public LaunchCinematicPlaybackState State => _coordinator.State;
        public LaunchCinematicPlaybackTerminal Terminal => _coordinator.Terminal;
        public bool IsSkipEligible =>
            State == LaunchCinematicPlaybackState.SkipEligible;

        private void Awake()
        {
            ResolveDependencies();
            SubscribeEvents();
            if (_surface != null)
            {
                _surface.enabled = false;
            }
        }

        private void Update()
        {
            if (_activeGeneration <= 0)
            {
                return;
            }

            LaunchCinematicPlaybackState state = _coordinator.State;
            if (state == LaunchCinematicPlaybackState.Preparing ||
                state == LaunchCinematicPlaybackState.AwaitingFirstFrame)
            {
                float elapsed = Time.realtimeSinceStartup - _prepareStartedAt;
                if (_coordinator.TryPrepareTimedOut(_activeGeneration, elapsed))
                {
                    PublishTerminalAndRelease();
                }
            }
            else if (state == LaunchCinematicPlaybackState.Playing ||
                     state == LaunchCinematicPlaybackState.SkipEligible)
            {
                float elapsed = Time.realtimeSinceStartup - _lastPlaybackProgressAt;
                if (_coordinator.TryPlaybackStalled(_activeGeneration, elapsed))
                {
                    Debug.LogWarning(
                        "Launch cinematic playback stopped advancing; using the static fallback.");
                    PublishTerminalAndRelease();
                }
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused &&
                _activeGeneration > 0 &&
                _coordinator.TryFail(_activeGeneration, "application-paused"))
            {
                PublishTerminalAndRelease();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused &&
                _activeGeneration > 0 &&
                _coordinator.TryFail(_activeGeneration, "application-focus-lost"))
            {
                PublishTerminalAndRelease();
            }
        }

        private void OnDisable()
        {
            if (_activeGeneration > 0 &&
                _coordinator.TryFail(_activeGeneration, "component-disabled"))
            {
                PublishTerminalAndRelease();
            }
        }

        private void OnDestroy()
        {
            if (_activeGeneration > 0 &&
                _coordinator.TryFail(_activeGeneration, "component-destroyed"))
            {
                PublishTerminalAndRelease();
            }

            UnsubscribeEvents();
            ReleasePlayerResources();
        }

        public bool TryBegin(
            LaunchCinematicRuntimeRecord record,
            LaunchCinematicPlatform buildPlatform,
            bool releaseBuild,
            bool reducedMotion)
        {
            // A host owns one decoder lifetime. Reusing the same native VideoPlayer could allow a
            // late callback from the retired URL to be mistaken for a new attempt.
            if (_attemptStarted)
            {
                return false;
            }

            _attemptStarted = true;
            ResolveDependencies();
            SubscribeEvents();

            LaunchCinematicPlaybackAttempt attempt = _coordinator.Begin(
                record,
                buildPlatform,
                releaseBuild,
                reducedMotion);
            _activeGeneration = attempt.Generation;
            if (!attempt.Accepted)
            {
                PublishTerminalAndRelease();
                return false;
            }

            if (_videoPlayer == null ||
                _audioSource == null ||
                _surface == null ||
                !LaunchCinematicMediaPath.TryResolve(
                    Application.streamingAssetsPath,
                    attempt.StreamingAssetsPath,
                    out string mediaUrl))
            {
                _coordinator.TryFail(_activeGeneration, "playback-owner-unavailable");
                PublishTerminalAndRelease();
                return false;
            }

            bool renderTargetReady = false;
            if (!TryRunDecoderOperation(
                    () => renderTargetReady = TryCreateRenderTarget(attempt),
                    "render-target-failed") ||
                !renderTargetReady)
            {
                if (_coordinator.TryFail(
                        _activeGeneration,
                        "playback-owner-unavailable"))
                {
                    PublishTerminalAndRelease();
                }
                return false;
            }

            if (!TryRunDecoderOperation(
                    () =>
                    {
                        ConfigurePlayer(mediaUrl);
                        _prepareStartedAt = Time.realtimeSinceStartup;
                        _videoPlayer.Prepare();
                    },
                    "decoder-start-failed"))
            {
                return false;
            }

            return true;
        }

        public bool TrySkip()
        {
            if (_activeGeneration <= 0 ||
                !_coordinator.TrySkip(_activeGeneration))
            {
                return false;
            }

            PublishTerminalAndRelease();
            return true;
        }

        public void SetMuted(bool muted)
        {
            ResolveDependencies();
            if (_audioSource != null)
            {
                _audioSource.mute = muted;
            }
        }

        private void ResolveDependencies()
        {
            if (_videoPlayer == null)
            {
                _videoPlayer = GetComponent<VideoPlayer>();
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }
        }

        private void ConfigurePlayer(string mediaUrl)
        {
            _videoPlayer.playOnAwake = false;
            _videoPlayer.waitForFirstFrame = true;
            _videoPlayer.isLooping = false;
            _videoPlayer.skipOnDrop = false;
            _videoPlayer.sendFrameReadyEvents = true;
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = mediaUrl;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _ownedRenderTexture;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _videoPlayer.controlledAudioTrackCount = 1;
        }

        private bool TryCreateRenderTarget(LaunchCinematicPlaybackAttempt attempt)
        {
            ReleaseOwnedRenderTexture();
            _ownedRenderTexture = new RenderTexture(
                attempt.Width,
                attempt.Height,
                0,
                RenderTextureFormat.ARGB32)
            {
                name = "LaunchCinematic_" + attempt.CinematicId,
                useMipMap = false,
                autoGenerateMips = false
            };

            if (!_ownedRenderTexture.Create())
            {
                ReleaseOwnedRenderTexture();
                return false;
            }

            _surface.texture = _ownedRenderTexture;
            // Keep the truthful loading fallback visible until this attempt has produced an
            // actual frame. Allocating a RenderTexture does not prove that the decoder can render
            // into it, and exposing it here would cover the fallback with a blank surface while
            // preparation is still pending.
            _surface.enabled = false;
            return true;
        }

        private void SubscribeEvents()
        {
            if (_eventsSubscribed || _videoPlayer == null)
            {
                return;
            }

            _videoPlayer.prepareCompleted += OnPrepareCompleted;
            _videoPlayer.frameReady += OnFrameReady;
            _videoPlayer.loopPointReached += OnLoopPointReached;
            _videoPlayer.errorReceived += OnErrorReceived;
            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_eventsSubscribed || _videoPlayer == null)
            {
                return;
            }

            _videoPlayer.prepareCompleted -= OnPrepareCompleted;
            _videoPlayer.frameReady -= OnFrameReady;
            _videoPlayer.loopPointReached -= OnLoopPointReached;
            _videoPlayer.errorReceived -= OnErrorReceived;
            _eventsSubscribed = false;
        }

        private void OnPrepareCompleted(VideoPlayer source)
        {
            if (source != _videoPlayer ||
                !_coordinator.TryMarkPrepared(_activeGeneration))
            {
                return;
            }

            TryRunDecoderOperation(
                () =>
                {
                    if (source.audioTrackCount > 0)
                    {
                        source.EnableAudioTrack(0, true);
                        source.SetTargetAudioSource(0, _audioSource);
                    }

                    source.Play();
                },
                "decoder-play-failed");
        }

        private void OnFrameReady(VideoPlayer source, long frameIndex)
        {
            if (source != _videoPlayer || _activeGeneration <= 0)
            {
                return;
            }

            if (_coordinator.State == LaunchCinematicPlaybackState.AwaitingFirstFrame)
            {
                if (_coordinator.TryMarkFirstFrameVisible(
                        _activeGeneration,
                        frameIndex))
                {
                    _lastPlaybackProgressAt = Time.realtimeSinceStartup;
                    if (_surface != null &&
                        _surface.texture == _ownedRenderTexture)
                    {
                        _surface.enabled = true;
                    }
                }
                return;
            }

            if (_coordinator.TryObservePlaybackFrame(_activeGeneration, frameIndex))
            {
                _lastPlaybackProgressAt = Time.realtimeSinceStartup;
            }
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            if (source == _videoPlayer &&
                _coordinator.TryComplete(_activeGeneration))
            {
                PublishTerminalAndRelease();
            }
        }

        private void OnErrorReceived(VideoPlayer source, string message)
        {
            if (source == _videoPlayer &&
                _coordinator.TryFail(_activeGeneration, "decoder-error"))
            {
                Debug.LogWarning("Launch cinematic playback failed; using the static fallback.");
                PublishTerminalAndRelease();
            }
        }

        private bool TryRunDecoderOperation(Action operation, string failureDetail)
        {
            if (_activeGeneration <= 0 || operation == null)
            {
                return false;
            }

            try
            {
                operation();
                return true;
            }
            catch (Exception)
            {
                // Decoder and render-target setup are optional. A synchronous platform failure
                // must converge on the same bounded fallback as asynchronous VideoPlayer errors
                // without exposing media paths or raw platform exception text.
                if (_coordinator.TryFail(_activeGeneration, failureDetail))
                {
                    Debug.LogWarning(
                        "Launch cinematic decoder operation failed; using the static fallback.");
                    PublishTerminalAndRelease();
                }
                return false;
            }
        }

        private void PublishTerminalAndRelease()
        {
            LaunchCinematicPlaybackTerminal terminal = _coordinator.Terminal;
            if (!terminal.IsTerminal ||
                terminal.Generation == _publishedTerminalGeneration)
            {
                return;
            }

            _publishedTerminalGeneration = terminal.Generation;
            _activeGeneration = 0;
            _lastPlaybackProgressAt = 0f;
            // Teardown is best-effort optional-media cleanup. It must finish before publication
            // when possible, but a platform cleanup exception cannot suppress the terminal signal.
            TryRunCleanupOperation(ReleasePlayerResources);
            Terminated?.Invoke(terminal);
        }

        private void ReleasePlayerResources()
        {
            if (_videoPlayer != null)
            {
                VideoPlayer player = _videoPlayer;
                TryRunCleanupOperation(player.Stop);
                TryRunCleanupOperation(() => player.targetTexture = null);
                TryRunCleanupOperation(() => player.url = string.Empty);
            }

            if (_audioSource != null)
            {
                AudioSource audioSource = _audioSource;
                TryRunCleanupOperation(audioSource.Stop);
            }

            TryRunCleanupOperation(ReleaseOwnedRenderTexture);
        }

        private void ReleaseOwnedRenderTexture()
        {
            if (_ownedRenderTexture == null)
            {
                return;
            }

            RenderTexture renderTexture = _ownedRenderTexture;
            _ownedRenderTexture = null;

            bool surfaceOwnsRenderTexture = false;
            if (_surface != null)
            {
                RawImage surface = _surface;
                TryRunCleanupOperation(
                    () => surfaceOwnsRenderTexture = surface.texture == renderTexture);
                if (surfaceOwnsRenderTexture)
                {
                    TryRunCleanupOperation(() => surface.texture = null);
                    TryRunCleanupOperation(() => surface.enabled = false);
                }
            }

            TryRunCleanupOperation(renderTexture.Release);
            TryRunCleanupOperation(
                () =>
                {
                    if (Application.isPlaying)
                    {
                        Destroy(renderTexture);
                    }
                    else
                    {
                        DestroyImmediate(renderTexture);
                    }
                });
        }

        private bool TryRunCleanupOperation(Action operation)
        {
            if (operation == null)
            {
                return false;
            }

            try
            {
                operation();
                return true;
            }
            catch (Exception)
            {
                Debug.LogWarning(
                    "Launch cinematic resource cleanup failed; continuing fallback teardown.");
                return false;
            }
        }
    }
}
