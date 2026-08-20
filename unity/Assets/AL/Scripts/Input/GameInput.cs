using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace AL.Input
{
    /// <summary>
    /// Central facade over the Unity Input System. All gameplay input flows through here
    /// (keyboard, mouse, and gamepad bindings are defined in code; touch is exposed via
    /// the EnhancedTouch API so that cameras and gesture code can read raw touch data).
    ///
    /// The action map is built lazily and enabled on first read, so callers do not need to
    /// wire anything up explicitly. <see cref="EnsureEnabled"/> is idempotent.
    /// </summary>
    public static class GameInput
    {
        private const string MapName = "Gameplay";

        private static InputActionMap _map;
        private static bool _enabled;

        private static InputActionMap Map
        {
            get
            {
                if (_map == null)
                {
                    _map = BuildMap();
                }

                EnsureEnabled();
                return _map;
            }
        }

        // ---- Actions ----

        public static InputAction Move => Map["Move"];
        public static InputAction Look => Map["Look"];
        public static InputAction Scroll => Map["Scroll"];
        public static InputAction Attack => Map["Attack"];
        public static InputAction Dodge => Map["Dodge"];
        public static InputAction Block => Map["Block"];
        public static InputAction Skill1 => Map["Skill1"];
        public static InputAction Skill2 => Map["Skill2"];
        public static InputAction Skill3 => Map["Skill3"];
        public static InputAction Skill4 => Map["Skill4"];
        public static InputAction Submit => Map["Submit"];
        public static InputAction Cancel => Map["Cancel"];
        public static InputAction WorldMap => Map["WorldMap"];
        public static InputAction SharedMenu => Map["SharedMenu"];

        private static bool _gameplaySuppressed;

        // ---- Lifecycle ----

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Guards against stale state when domain reload is disabled in play mode.
            _map = null;
            _enabled = false;
            _gameplaySuppressed = false;
        }

        public static void SetGameplaySuppressed(bool suppressed)
        {
            _gameplaySuppressed = suppressed;
        }

        public static bool GameplaySuppressed => _gameplaySuppressed;

        public static void EnsureEnabled()
        {
            if (_enabled)
            {
                return;
            }

            if (_map == null)
            {
                _map = BuildMap();
            }

            EnhancedTouchSupport.Enable();
            _map.Enable();
            _enabled = true;
        }

        public static void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _map?.Disable();
            _enabled = false;
        }

        // ---- Typed reads ----

        public static Vector2 ReadMove() => _gameplaySuppressed ? Vector2.zero : Move.ReadValue<Vector2>();

        public static Vector2 ReadLook() => _gameplaySuppressed ? Vector2.zero : Look.ReadValue<Vector2>();

        public static float ReadScroll() => _gameplaySuppressed ? 0f : Scroll.ReadValue<float>();

        public static bool AttackPressed() => !_gameplaySuppressed && Attack.WasPressedThisFrame();

        public static bool DodgePressed() => !_gameplaySuppressed && Dodge.WasPressedThisFrame();

        public static bool BlockHeld() => !_gameplaySuppressed && Block.IsPressed();

        public static bool BlockPressed() => !_gameplaySuppressed && Block.WasPressedThisFrame();

        public static bool SkillPressed(int index)
        {
            if (_gameplaySuppressed)
            {
                return false;
            }

            switch (index)
            {
                case 0:
                    return Skill1.WasPressedThisFrame();
                case 1:
                    return Skill2.WasPressedThisFrame();
                case 2:
                    return Skill3.WasPressedThisFrame();
                case 3:
                    return Skill4.WasPressedThisFrame();
                default:
                    return false;
            }
        }

        public static bool SubmitPressed() => !_gameplaySuppressed && Submit.WasPressedThisFrame();

        public static bool SubmitHeld() => !_gameplaySuppressed && Submit.IsPressed();

        public static bool CancelPressed() => Cancel.WasPressedThisFrame();

        public static bool WorldMapPressed() => WorldMap.WasPressedThisFrame();

        public static bool SharedMenuPressed() => SharedMenu.WasPressedThisFrame();

        // ---- Touch ----

        public static int TouchCount
        {
            get
            {
                EnsureEnabled();
                return EnhancedTouch.activeTouches.Count;
            }
        }

        public static EnhancedTouch GetTouch(int index)
        {
            EnsureEnabled();
            return EnhancedTouch.activeTouches[index];
        }

        public static bool TouchscreenPresent
        {
            get
            {
                EnsureEnabled();
                return Touchscreen.current != null;
            }
        }

        // ---- Definition ----

        private static InputActionMap BuildMap()
        {
            var map = new InputActionMap(MapName);

            var move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow")
                .With("Up", "<Gamepad>/leftStick/up")
                .With("Down", "<Gamepad>/leftStick/down")
                .With("Left", "<Gamepad>/leftStick/left")
                .With("Right", "<Gamepad>/leftStick/right");

            var look = map.AddAction("Look", InputActionType.Value);
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            var scroll = map.AddAction("Scroll", InputActionType.Value);
            scroll.AddBinding("<Mouse>/scroll/y");

            var attack = map.AddAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            attack.AddBinding("<Gamepad>/buttonWest");

            var dodge = map.AddAction("Dodge", InputActionType.Button, "<Keyboard>/space");
            dodge.AddBinding("<Gamepad>/buttonSouth");

            var block = map.AddAction("Block", InputActionType.Button, "<Keyboard>/leftShift");
            block.AddBinding("<Gamepad>/leftTrigger");

            map.AddAction("Skill1", InputActionType.Button, "<Keyboard>/1");
            map.AddAction("Skill2", InputActionType.Button, "<Keyboard>/2");
            map.AddAction("Skill3", InputActionType.Button, "<Keyboard>/3");
            map.AddAction("Skill4", InputActionType.Button, "<Keyboard>/4");

            var submit = map.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/numpadEnter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Gamepad>/buttonSouth");
            submit.AddBinding("<Gamepad>/start");

            var cancel = map.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");

            var worldMap = map.AddAction("WorldMap", InputActionType.Button, "<Keyboard>/m");
            worldMap.AddBinding("<Gamepad>/select");

            map.AddAction("SharedMenu", InputActionType.Button, "<Keyboard>/tab");

            return map;
        }
    }
}
