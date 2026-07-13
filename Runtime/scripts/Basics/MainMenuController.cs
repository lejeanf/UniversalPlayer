using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Owns the main menu: Escape/Start TOGGLES it and the game pauses while it
    /// is open. The open state is SHARED (PlayerEvents + mainMenuState channel):
    /// the project can open/close the menu by raising the channel, and this
    /// controller follows — menu GameObject + pause are applied from the shared
    /// state, whoever raised it, so the toggle can never go out of sync.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Main menu settings:")]
        [SerializeField] private GameObject mainMenu;
        [SerializeField] private InputActionReference openMainMenuAction;
        public InputActionReference _openMainMenuAction
        {
            get { return openMainMenuAction; }
            set { openMainMenuAction = value; }
        }

        [Header("Broadcasting on:")]
        [SerializeField] private BoolEventChannelSO mainMenuStateChannel;
        [SerializeField] private BoolEventChannelSO GeneralPauseEventChannel;
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

        // Escape/Start: apply the toggle locally AND raise it on the shared
        // channel so every other listener (locomotion freeze, cursor, project
        // UIs) follows. The bridge mirrors the channel back as
        // PlayerEvents.MenuStateChanged — ApplyMenuState is idempotent, so the
        // echo is harmless and external raises keep this controller in sync.
        private void OnOpenMenuPerformed(InputAction.CallbackContext _)
        {
            var next = !_menuState;
            ApplyMenuState(next);
            mainMenuStateChannel.RaiseEvent(next);
        }

        private void ApplyMenuState(bool state)
        {
            _menuState = state;
            if (mainMenu) mainMenu.SetActive(state);
            if (GeneralPauseEventChannel != null) GeneralPauseEventChannel.RaiseEvent(state);
        }
    }
}
