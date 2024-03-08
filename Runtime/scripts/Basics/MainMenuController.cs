using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer
{
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
            openMainMenuAction.action.performed += ctx => InvertState();
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            openMainMenuAction.action.performed -= null;
            openMainMenuAction.action.Disable();
        }

        private void InvertState()
        {
            _menuState = !_menuState;
            SetMenu(_menuState);
        }

        private void SetMenu(bool state)
        {
            mainMenuStateChannel.RaiseEvent(state);
            if (GeneralPauseEventChannel != null )
            {
                GeneralPauseEventChannel.RaiseEvent(state);
            }
            if (mainMenu) mainMenu.SetActive(state);
        }
    }
}