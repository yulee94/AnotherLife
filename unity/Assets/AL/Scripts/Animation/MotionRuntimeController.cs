using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace AL.Motion
{
    public sealed class MotionLayerDefinition
    {
        public MotionLayerDefinition(
            string layerId,
            bool additive,
            AvatarMask mask,
            int priority)
        {
            if (string.IsNullOrWhiteSpace(layerId))
            {
                throw new ArgumentException("Layer ID is required.", nameof(layerId));
            }

            LayerId = layerId;
            Additive = additive;
            Mask = mask;
            Priority = priority;
        }

        public string LayerId { get; }
        public bool Additive { get; }
        public AvatarMask Mask { get; }
        public int Priority { get; }
    }

    public interface IMotionRootMotionConsumer
    {
        bool AllowsVerticalRootMotion { get; }
        void ConsumeRootMotion(MotionRootDelta delta, long actionSequence);
    }

    public sealed class MotionRuntimeController : MonoBehaviour
    {
        private sealed class LayerSlot
        {
            public int InputIndex;
            public MotionLayerDefinition Definition;
            public AnimationClipPlayable Playable;
            public float Weight;
        }

        private readonly Dictionary<string, LayerSlot> _layers =
            new Dictionary<string, LayerSlot>(StringComparer.Ordinal);
        private readonly MotionEventDeduplicator _eventDeduplicator =
            new MotionEventDeduplicator();

        private Animator _animator;
        private MotionCatalogSnapshot _catalog;
        private MotionTransitionMachine _transitions;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _baseMixer;
        private AnimationLayerMixerPlayable _layerMixer;
        private AnimationClipPlayable _currentPlayable;
        private AnimationClipPlayable _incomingPlayable;
        private MotionClipDefinition _currentDefinition;
        private float _blendElapsed;
        private float _blendDuration = MotionTransitionMachine.MaximumBlendSeconds;
        private double _runtimeSeconds;
        private float _playbackSpeed = 1f;
        private int _actorInstanceId;
        private IReadOnlyDictionary<string, MotionEventTimeline> _timelines;
        private MotionEventNameRegistry _eventNames;
        private MotionWindowTracker _windowTracker;
        private IMotionRootMotionConsumer _rootMotionConsumer;
        private float _maximumRootHorizontalMeters = 0.35f;
        private float _maximumRootYawDegrees = 30f;

        public event Action<MotionEventDispatch> MotionEventDispatched;

        public bool IsGraphValid => _graph.IsValid();
        public string CurrentMotionKey => _currentDefinition?.MotionKey ?? string.Empty;
        public double CurrentLocalTime => _incomingPlayable.IsValid()
            ? _incomingPlayable.GetTime()
            : _currentPlayable.IsValid()
                ? _currentPlayable.GetTime()
                : 0d;
        public bool LastRequestUsedFallback { get; private set; }
        public int ActiveLayerCount => _layers.Values.Count(slot =>
            slot.Playable.IsValid() && slot.Weight > 0f);

        public void Configure(
            Animator animator,
            MotionCatalogSnapshot catalog,
            IEnumerable<MotionLayerDefinition> layers)
        {
            Release();
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _transitions = new MotionTransitionMachine(_catalog);
            _actorInstanceId = gameObject.GetInstanceID();

            MotionLayerDefinition[] orderedLayers = (layers ??
                    Array.Empty<MotionLayerDefinition>())
                .OrderBy(value => value.Priority)
                .ThenBy(value => value.LayerId, StringComparer.Ordinal)
                .ToArray();
            if (orderedLayers.Select(value => value.LayerId)
                .Distinct(StringComparer.Ordinal).Count() != orderedLayers.Length)
            {
                throw new InvalidOperationException("Motion layer IDs must be unique.");
            }

            _graph = PlayableGraph.Create("AL.Motion." + _actorInstanceId);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _baseMixer = AnimationMixerPlayable.Create(_graph, 2, true);
            _layerMixer = AnimationLayerMixerPlayable.Create(
                _graph,
                orderedLayers.Length + 1);
            _graph.Connect(_baseMixer, 0, _layerMixer, 0);
            _layerMixer.SetInputWeight(0, 1f);

            for (int index = 0; index < orderedLayers.Length; index++)
            {
                MotionLayerDefinition definition = orderedLayers[index] ??
                                                   throw new InvalidOperationException(
                                                       "Motion layers cannot contain null entries.");
                int inputIndex = index + 1;
                _layerMixer.SetLayerAdditive((uint)inputIndex, definition.Additive);
                if (definition.Mask != null)
                {
                    _layerMixer.SetLayerMaskFromAvatarMask(
                        (uint)inputIndex,
                        definition.Mask);
                }

                _layers.Add(
                    definition.LayerId,
                    new LayerSlot
                    {
                        InputIndex = inputIndex,
                        Definition = definition
                    });
            }

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(
                _graph,
                "MotionOutput",
                _animator);
            output.SetSourcePlayable(_layerMixer);
            _animator.applyRootMotion = true;
            PlayImmediate(_transitions.Current);
            _graph.Play();
        }

        public void ConfigureEventRuntime(
            int actorInstanceId,
            IReadOnlyDictionary<string, MotionEventTimeline> timelines,
            MotionWindowTracker windowTracker,
            MotionEventNameRegistry eventNames = null)
        {
            _actorInstanceId = actorInstanceId;
            _timelines = timelines;
            _windowTracker = windowTracker;
            _eventNames = eventNames;
            _eventDeduplicator.Clear();
        }

        public void AL_MotionEventV1(string envelopeJson)
        {
            if (_eventNames == null || _currentDefinition == null || _transitions == null ||
                _transitions.ActionSequence <= 0 || string.IsNullOrWhiteSpace(envelopeJson))
            {
                return;
            }

            MotionAnimationEventPayload envelope;
            try
            {
                envelope = JsonUtility.FromJson<MotionAnimationEventPayload>(envelopeJson);
            }
            catch (ArgumentException)
            {
                return;
            }
            if (envelope == null || envelope.schemaVersion != 1 ||
                envelope.eventOrdinal < 0 || envelope.normalizedTime < 0f ||
                envelope.normalizedTime > 1f ||
                (envelope.actionSequence != 0 &&
                 envelope.actionSequence != _transitions.ActionSequence) ||
                !_eventNames.TryResolve(envelope.eventId, out string eventName))
            {
                return;
            }

            var payload = new MotionStaticPayload
            {
                Phase = envelope.phase,
                ContactId = envelope.contactId,
                WindowId = envelope.windowId,
                CueId = envelope.cueId
            };
            float normalizedTime = _currentDefinition.Clip.length <= Mathf.Epsilon
                ? 0f
                : Mathf.Clamp01((float)(CurrentLocalTime / _currentDefinition.Clip.length));
            var definition = new MotionEventDefinition(
                envelope.eventId,
                eventName,
                envelope.eventOrdinal,
                envelope.eventOrdinal,
                payload);
            var dispatch = new MotionEventDispatch(
                definition,
                _transitions.ActionSequence,
                normalizedTime);
            if (!_eventDeduplicator.TryAccept(_actorInstanceId, dispatch))
            {
                return;
            }

            _windowTracker?.Apply(dispatch);
            MotionEventDispatched?.Invoke(dispatch);
        }

        public void ConfigureRootMotion(
            IMotionRootMotionConsumer consumer,
            float maximumHorizontalMeters,
            float maximumYawDegrees)
        {
            if (maximumHorizontalMeters < 0f || maximumYawDegrees < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumHorizontalMeters),
                    "Root motion bounds cannot be negative.");
            }

            _rootMotionConsumer = consumer;
            _maximumRootHorizontalMeters = maximumHorizontalMeters;
            _maximumRootYawDegrees = maximumYawDegrees;
        }

        public bool RequestMotion(string motionKey, long actionSequence, float playbackSpeed = 1f)
        {
            if (!_graph.IsValid() || playbackSpeed <= 0f)
            {
                return false;
            }

            bool exact = _catalog.TryGetExact(motionKey, out _);
            if (!_transitions.TryRequest(
                    motionKey,
                    actionSequence,
                    out MotionTransitionResult result))
            {
                return false;
            }

            ClosePresentationWindows();
            LastRequestUsedFallback = !exact ||
                                      !string.Equals(
                                          result.Active.MotionKey,
                                          motionKey,
                                          StringComparison.Ordinal);
            _playbackSpeed = playbackSpeed;
            BeginBlend(result.Active, result.BlendSeconds);
            return true;
        }

        public bool MarkCommitted(long actionSequence)
        {
            return _transitions != null && _transitions.MarkCommitted(actionSequence);
        }

        public bool Cancel(long actionSequence, bool gameplayAccepted)
        {
            if (_transitions == null)
            {
                return false;
            }

            MotionTransitionResult result = _transitions.Cancel(
                actionSequence,
                gameplayAccepted);
            if (result.Outcome != MotionTransitionOutcome.CancelledPreCommit &&
                result.Outcome != MotionTransitionOutcome.InterruptedPostCommit)
            {
                return false;
            }

            ClosePresentationWindows();
            BeginBlend(result.Active, result.BlendSeconds);
            return true;
        }

        public void CompleteCurrent()
        {
            if (_transitions == null)
            {
                return;
            }

            MotionTransitionResult result = _transitions.CompleteCurrent();
            ClosePresentationWindows();
            BeginBlend(result.Active, result.BlendSeconds);
        }

        public bool SetLayer(
            string layerId,
            AnimationClip clip,
            float weight,
            float playbackSpeed)
        {
            if (!_graph.IsValid() || clip == null || playbackSpeed <= 0f ||
                !_layers.TryGetValue(layerId ?? string.Empty, out LayerSlot slot))
            {
                return false;
            }

            if (slot.Playable.IsValid())
            {
                _layerMixer.DisconnectInput(slot.InputIndex);
                _graph.DestroyPlayable(slot.Playable);
            }

            slot.Playable = AnimationClipPlayable.Create(_graph, clip);
            slot.Playable.SetSpeed(playbackSpeed);
            slot.Playable.SetApplyFootIK(true);
            slot.Playable.SetApplyPlayableIK(true);
            _graph.Connect(slot.Playable, 0, _layerMixer, slot.InputIndex);
            slot.Weight = Mathf.Clamp01(weight);
            _layerMixer.SetInputWeight(slot.InputIndex, slot.Weight);
            return true;
        }

        public void ClearLayer(string layerId)
        {
            if (!_layers.TryGetValue(layerId ?? string.Empty, out LayerSlot slot))
            {
                return;
            }

            if (slot.Playable.IsValid())
            {
                _layerMixer.DisconnectInput(slot.InputIndex);
                _graph.DestroyPlayable(slot.Playable);
            }

            slot.Playable = default;
            slot.Weight = 0f;
        }

        public void Tick(float deltaSeconds)
        {
            if (!_graph.IsValid() || deltaSeconds < 0f)
            {
                return;
            }

            double previousRuntime = _runtimeSeconds;
            _runtimeSeconds += deltaSeconds;
            _graph.Evaluate(deltaSeconds);
            DispatchTimeline(previousRuntime, _runtimeSeconds);
            UpdateBlend(deltaSeconds);
            WrapLoopIfNeeded();
        }

        public void Release()
        {
            ClosePresentationWindows();
            _eventDeduplicator.Clear();
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            _layers.Clear();
            _currentPlayable = default;
            _incomingPlayable = default;
            _baseMixer = default;
            _layerMixer = default;
            _currentDefinition = null;
            _catalog = null;
            _transitions = null;
            _timelines = null;
            _eventNames = null;
            _windowTracker = null;
            _rootMotionConsumer = null;
            _runtimeSeconds = 0d;
            LastRequestUsedFallback = false;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnAnimatorMove()
        {
            if (_animator == null || _currentDefinition == null ||
                _rootMotionConsumer == null)
            {
                return;
            }

            MotionRootDelta accepted = MotionRootPolicy.Resolve(
                _currentDefinition.RootMode,
                _animator.deltaPosition,
                _animator.deltaRotation.eulerAngles.y,
                _maximumRootHorizontalMeters,
                _maximumRootYawDegrees,
                _rootMotionConsumer.AllowsVerticalRootMotion);
            _rootMotionConsumer.ConsumeRootMotion(
                accepted,
                _transitions?.ActionSequence ?? 0);
        }

        private void OnDisable()
        {
            Release();
        }

        private void OnDestroy()
        {
            Release();
        }

        private void PlayImmediate(MotionClipDefinition definition)
        {
            _currentDefinition = definition;
            _currentPlayable = CreateClipPlayable(definition);
            _graph.Connect(_currentPlayable, 0, _baseMixer, 0);
            _baseMixer.SetInputWeight(0, 1f);
            _baseMixer.SetInputWeight(1, 0f);
            _runtimeSeconds = 0d;
        }

        private void BeginBlend(MotionClipDefinition definition, float blendSeconds)
        {
            if (_incomingPlayable.IsValid())
            {
                PromoteIncoming();
            }

            _currentDefinition = definition;
            _incomingPlayable = CreateClipPlayable(definition);
            _graph.Connect(_incomingPlayable, 0, _baseMixer, 1);
            _baseMixer.SetInputWeight(0, 1f);
            _baseMixer.SetInputWeight(1, 0f);
            _blendElapsed = 0f;
            _blendDuration = Mathf.Clamp(
                blendSeconds,
                0f,
                MotionTransitionMachine.MaximumBlendSeconds);
            _runtimeSeconds = 0d;
            if (_blendDuration <= Mathf.Epsilon)
            {
                PromoteIncoming();
            }
        }

        private AnimationClipPlayable CreateClipPlayable(MotionClipDefinition definition)
        {
            AnimationClipPlayable playable = AnimationClipPlayable.Create(
                _graph,
                definition.Clip);
            playable.SetSpeed(_playbackSpeed);
            playable.SetApplyFootIK(true);
            playable.SetApplyPlayableIK(true);
            return playable;
        }

        private void UpdateBlend(float deltaSeconds)
        {
            if (!_incomingPlayable.IsValid())
            {
                return;
            }

            _blendElapsed += deltaSeconds;
            float weight = _blendDuration <= Mathf.Epsilon
                ? 1f
                : Mathf.Clamp01(_blendElapsed / _blendDuration);
            _baseMixer.SetInputWeight(0, 1f - weight);
            _baseMixer.SetInputWeight(1, weight);
            if (weight >= 1f)
            {
                PromoteIncoming();
            }
        }

        private void PromoteIncoming()
        {
            if (!_incomingPlayable.IsValid())
            {
                return;
            }

            _baseMixer.DisconnectInput(1);
            if (_currentPlayable.IsValid())
            {
                _baseMixer.DisconnectInput(0);
                _graph.DestroyPlayable(_currentPlayable);
            }

            _currentPlayable = _incomingPlayable;
            _incomingPlayable = default;
            _graph.Connect(_currentPlayable, 0, _baseMixer, 0);
            _baseMixer.SetInputWeight(0, 1f);
            _baseMixer.SetInputWeight(1, 0f);
        }

        private void WrapLoopIfNeeded()
        {
            AnimationClipPlayable playable = _incomingPlayable.IsValid()
                ? _incomingPlayable
                : _currentPlayable;
            if (!playable.IsValid() || _currentDefinition == null ||
                !_currentDefinition.Loop || _currentDefinition.Clip.length <= 0f)
            {
                return;
            }

            double time = playable.GetTime();
            if (time >= _currentDefinition.Clip.length)
            {
                playable.SetTime(time % _currentDefinition.Clip.length);
                playable.SetDone(false);
                double runtimeCycleSeconds =
                    _currentDefinition.Clip.length / _playbackSpeed;
                if (runtimeCycleSeconds > 0d)
                {
                    _runtimeSeconds %= runtimeCycleSeconds;
                }
            }
        }

        private void DispatchTimeline(double previousRuntime, double currentRuntime)
        {
            if (_timelines == null || _currentDefinition == null ||
                _transitions == null || _transitions.ActionSequence <= 0 ||
                !_timelines.TryGetValue(
                    _currentDefinition.ClipId,
                    out MotionEventTimeline timeline))
            {
                return;
            }

            long actionSequence = _transitions.ActionSequence;
            if (!_currentDefinition.Loop || _currentDefinition.Clip.length <= Mathf.Epsilon)
            {
                DispatchCollected(
                    timeline.Collect(
                        previousRuntime,
                        currentRuntime,
                        _playbackSpeed,
                        actionSequence));
                return;
            }

            double runtimeCycleSeconds = _currentDefinition.Clip.length / _playbackSpeed;
            if (currentRuntime < runtimeCycleSeconds)
            {
                DispatchCollected(
                    timeline.Collect(
                        previousRuntime,
                        currentRuntime,
                        _playbackSpeed,
                        actionSequence));
                return;
            }

            DispatchCollected(
                timeline.Collect(
                    previousRuntime,
                    runtimeCycleSeconds,
                    _playbackSpeed,
                    actionSequence));
            double remainingRuntime = currentRuntime - runtimeCycleSeconds;
            while (remainingRuntime >= 0d)
            {
                _eventDeduplicator.RetireThrough(actionSequence);
                double cycleEnd = Math.Min(remainingRuntime, runtimeCycleSeconds);
                DispatchCollected(
                    timeline.Collect(
                        0d,
                        cycleEnd,
                        _playbackSpeed,
                        actionSequence));
                if (remainingRuntime < runtimeCycleSeconds)
                {
                    break;
                }

                remainingRuntime -= runtimeCycleSeconds;
            }
        }

        private void DispatchCollected(IReadOnlyList<MotionEventDispatch> dispatches)
        {
            for (int index = 0; index < dispatches.Count; index++)
            {
                MotionEventDispatch dispatch = dispatches[index];
                if (!_eventDeduplicator.TryAccept(_actorInstanceId, dispatch))
                {
                    continue;
                }

                _windowTracker?.Apply(dispatch);
                MotionEventDispatched?.Invoke(dispatch);
            }
        }

        private void ClosePresentationWindows()
        {
            if (_windowTracker == null)
            {
                return;
            }

            long sequence = _transitions?.ActionSequence ?? 0;
            if (sequence > 0)
            {
                _windowTracker.CloseAll(sequence);
            }
            else
            {
                _windowTracker.CloseAll();
            }
        }
    }
}
