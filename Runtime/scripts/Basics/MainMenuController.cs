using jeanf.validationTools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Owns the main menu: Escape/Start TOGGLES it and the game pauses while it
    /// is open. The open state is SHARED (PlayerEvents.MenuStateChanged, forwarded
    /// both ways on the mainMenuState hub channel): the project can open/close the
    /// menu by raising the channel, and this controller follows — menu GameObject +
    /// pause are applied from the shared state, whoever raised it, so the toggle can
    /// never go out of sync.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Main menu settings:")]
        [Validation("Main menu GameObject is required — there is nothing to show/hide when the menu toggles.")]
        [SerializeField] private GameObject mainMenu;
        [Validation("Open-menu action is required — Escape/Start is enabled on it at startup (a null reference throws otherwise).")]
        [SerializeField] private InputActionReference openMainMenuAction;
        public InputActionReference _openMainMenuAction
        {
            get { return openMainMenuAction; }
            set { openMainMenuAction = value; }
        }

        private bool _menuState;

        private void OnEnable()
        {
            openMainMenuAction.action.Enable();
            openMainMenuAction.action.performed += OnOpenMenuPerformed;
            PlayerEvents.MenuStateChanged += ApplyMenuState;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            openMainMenuAction.action.performed -= OnOpenMenuPerformed;
            openMainMenuAction.action.Disable();
            PlayerEvents.MenuStateChanged -= ApplyMenuState;
        }

        // Escape/Start: raise the toggle on the shared event so every listener
        // (this controller, locomotion freeze, cursor, the project's UIs via the
        // bridge) follows the same state.
        private void OnOpenMenuPerformed(InputAction.CallbackContext _) => PlayerEvents.RaiseMenuState(!_menuState);

        private void ApplyMenuState(bool state)
        {
            _menuState = state;
            if (mainMenu) mainMenu.SetActive(state);
            // The game pauses while the menu is open (hub slot: pause).
            PlayerEvents.RaisePause(state);
        }
    }
}
