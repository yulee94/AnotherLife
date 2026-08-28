using System;
using System.Collections.Generic;
using AL.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AL.ChampionMode.UI
{
    /// <summary>
    /// HUD clicks and Shared Menu must not swing the follow camera. Cursor users
    /// acquire independent tokens; the last release restores the exact state
    /// observed by the first owner.
    /// </summary>
    public static class ChampionHudCameraGate
    {
        private static readonly HashSet<int> CursorOwners = new HashSet<int>();
        private static int _nextCursorOwnerId;
        private static int _cursorGeneration;
        private static bool _manualCursorModeOpen;
        private static bool _cursorBaselineCaptured;
        private static CursorLockMode _priorLockState;
        private static bool _priorVisible;
        private static bool _priorCursorSuppressed;

        public static bool MenuOpen { get; set; }

        public static bool RecapOpen { get; set; }

        public static bool CursorModeOpen =>
            _manualCursorModeOpen || CursorOwners.Count > 0;

        public static bool BlocksLook => MenuOpen || RecapOpen || CursorModeOpen;

        public static bool BlocksGameplay => CursorModeOpen;

        internal static bool HasExclusiveOwnedCursorGate =>
            !_manualCursorModeOpen &&
            CursorOwners.Count == 1 &&
            _cursorBaselineCaptured &&
            !_priorCursorSuppressed &&
            !MenuOpen &&
            !RecapOpen &&
            GameInput.CursorModeSuppressed;

        public static IDisposable AcquireCursorOwnership(string owner)
        {
            int id = unchecked(++_nextCursorOwnerId);
            if (id == 0)
            {
                id = unchecked(++_nextCursorOwnerId);
            }

            BeginOwnershipIfNeeded();
            CursorOwners.Add(id);
            ApplyOwnedCursorState();
            return new CursorOwnershipToken(id, _cursorGeneration);
        }

        public static void SetCursorMode(bool active)
        {
            if (active)
            {
                if (!_manualCursorModeOpen)
                {
                    BeginOwnershipIfNeeded();
                    _manualCursorModeOpen = true;
                }

                ApplyOwnedCursorState();
                return;
            }

            if (_manualCursorModeOpen)
            {
                _manualCursorModeOpen = false;
                RestoreIfUnowned();
                return;
            }

            if (CursorOwners.Count == 0 && !_cursorBaselineCaptured)
            {
                GameInput.SetCursorModeSuppressed(false);
                if (!MenuOpen && !RecapOpen)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        public static void ReapplyCursorState()
        {
            if (CursorModeOpen)
            {
                ApplyOwnedCursorState();
            }
        }

        public static bool ShouldIgnoreLook()
        {
            if (BlocksLook)
            {
                return true;
            }

            if (IsPointerOverUi())
            {
                return true;
            }

            return Cursor.lockState != CursorLockMode.Locked;
        }

        public static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        public static void Reset()
        {
            CursorOwners.Clear();
            _manualCursorModeOpen = false;
            _cursorBaselineCaptured = false;
            _nextCursorOwnerId = 0;
            _cursorGeneration = unchecked(_cursorGeneration + 1);
            MenuOpen = false;
            RecapOpen = false;
            ChampionHudSession.ResetOwnershipStatics();
            GameInput.SetCursorModeSuppressed(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ChampionTimeScaleGate.Reset();
        }

        private static void BeginOwnershipIfNeeded()
        {
            if (_manualCursorModeOpen || CursorOwners.Count > 0 || _cursorBaselineCaptured)
            {
                return;
            }

            _priorLockState = Cursor.lockState;
            _priorVisible = Cursor.visible;
            _priorCursorSuppressed = GameInput.CursorModeSuppressed;
            _cursorBaselineCaptured = true;
        }

        private static void ApplyOwnedCursorState()
        {
            GameInput.SetCursorModeSuppressed(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static void Release(int id, int generation)
        {
            if (generation != _cursorGeneration)
            {
                return;
            }

            if (!CursorOwners.Remove(id))
            {
                return;
            }

            RestoreIfUnowned();
        }

        private static void RestoreIfUnowned()
        {
            if (_manualCursorModeOpen || CursorOwners.Count > 0 || !_cursorBaselineCaptured)
            {
                return;
            }

            Cursor.lockState = _priorLockState;
            Cursor.visible = _priorVisible;
            GameInput.SetCursorModeSuppressed(_priorCursorSuppressed);
            _cursorBaselineCaptured = false;
        }

        private sealed class CursorOwnershipToken : IDisposable
        {
            private int _id;
            private readonly int _generation;

            internal CursorOwnershipToken(int id, int generation)
            {
                _id = id;
                _generation = generation;
            }

            public void Dispose()
            {
                int id = _id;
                if (id == 0)
                {
                    return;
                }

                _id = 0;
                Release(id, _generation);
            }
        }
    }

    public static class ChampionTimeScaleGate
    {
        private static readonly Dictionary<int, float> Owners =
            new Dictionary<int, float>();
        private static int _nextOwnerId;
        private static int _generation;
        private static bool _baselineCaptured;
        private static float _baselineTimeScale;
        private static float _baselineFixedDeltaTime;

        public static IDisposable Acquire(string owner, float requestedTimeScale)
        {
            int id = unchecked(++_nextOwnerId);
            if (id == 0)
            {
                id = unchecked(++_nextOwnerId);
            }

            if (!_baselineCaptured)
            {
                _baselineTimeScale = Time.timeScale;
                _baselineFixedDeltaTime = Time.fixedDeltaTime;
                _baselineCaptured = true;
            }

            Owners[id] = Mathf.Clamp01(requestedTimeScale);
            ApplyEffectiveScale();
            return new TimeScaleOwnershipToken(id, _generation);
        }

        public static void Reset()
        {
            Owners.Clear();
            _nextOwnerId = 0;
            _generation = unchecked(_generation + 1);
            RestoreBaseline();
        }

        private static void Release(int id, int generation)
        {
            if (generation != _generation)
            {
                return;
            }

            if (!Owners.Remove(id))
            {
                return;
            }

            if (Owners.Count == 0)
            {
                RestoreBaseline();
                return;
            }

            ApplyEffectiveScale();
        }

        private static void ApplyEffectiveScale()
        {
            float requested = 1f;
            foreach (float ownerScale in Owners.Values)
            {
                requested = Mathf.Min(requested, ownerScale);
            }

            float effective = Mathf.Min(_baselineTimeScale, requested);
            Time.timeScale = effective;
            if (effective <= 0f || _baselineTimeScale <= 0f)
            {
                Time.fixedDeltaTime = _baselineFixedDeltaTime;
                return;
            }

            Time.fixedDeltaTime = Mathf.Max(
                0.001f,
                _baselineFixedDeltaTime * (effective / _baselineTimeScale));
        }

        private static void RestoreBaseline()
        {
            if (!_baselineCaptured)
            {
                return;
            }

            Time.timeScale = _baselineTimeScale;
            Time.fixedDeltaTime = _baselineFixedDeltaTime;
            _baselineCaptured = false;
        }

        private sealed class TimeScaleOwnershipToken : IDisposable
        {
            private int _id;
            private readonly int _generation;

            internal TimeScaleOwnershipToken(int id, int generation)
            {
                _id = id;
                _generation = generation;
            }

            public void Dispose()
            {
                int id = _id;
                if (id == 0)
                {
                    return;
                }

                _id = 0;
                Release(id, _generation);
            }
        }
    }
}
