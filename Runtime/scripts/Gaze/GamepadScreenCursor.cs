using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Gamepad screen cursor: while the cursor is FREE (menu open, primary item
    /// drawn) and the active scheme is Gamepad, the right stick moves the OS
    /// cursor in 2D screen space — look is disabled in that state anyway, so the
    /// stick is repurposed — and A / right trigger click what it points at.
    /// Auto-added by CursorStateController; M&amp;K is untouched (the mouse already
    /// moves freely when the cursor unlocks). Warping the REAL cursor means the
    /// whole existing mouse-pointer UI pipeline (hover, click, drag) just works.
    /// </summary>
    public class GamepadScreenCursor : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";
        private const float SpeedPixelsPerSecond = 1100f; // at full stick deflection
        private const float Deadzone = 0.15f;

        private bool _canLook = true;
        private bool _noMouseWarned;

        private void OnEnable()
        {
            PlayerEvents.MouselookStateChanged += OnMouselookChanged;
        }

        private void OnDisable() => PlayerEvents.MouselookStateChanged -= OnMouselookChanged;

        private void OnMouselookChanged(bool canLook) => _canLook = canLook;

        private void Update()
        {
            if (_canLook) return; // cursor locked: the stick is LOOK, not pointer
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.Gamepad) return;

            var gamepad = Gamepad.current;
            if (gamepad == null) return;
            var mouse = Mouse.current;
            if (mouse == null)
            {
                if (!_noMouseWarned)
                {
                    _noMouseWarned = true;
                    Debug.LogWarning($"{LogPrefix} GamepadScreenCursor: no Mouse device to warp — the gamepad cursor is unavailable.", this);
                }
                return;
            }

            var stick = gamepad.rightStick.ReadValue();
            if (stick.magnitude < Deadzone) return;

            // Unscaled time: the menu (the main reason the cursor is free) may pause the game.
            var position = mouse.position.ReadValue() + stick * (SpeedPixelsPerSecond * Time.unscaledDeltaTime);
            position.x = Mathf.Clamp(position.x, 0f, Screen.width - 1);
            position.y = Mathf.Clamp(position.y, 0f, Screen.height - 1);
            mouse.WarpCursorPosition(position);
        }

        // Gamepad A / right trigger clicking is handled by GazeDesktopClick's
        // scheme-immune screen-pointer actions — this component only moves the cursor.
    }
}
