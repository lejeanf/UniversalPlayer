using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Turns UI-facing bindings into <see cref="PlayerEvents"/> raises:
    ///   Map (M / gamepad dpad-left)        → MapTogglePressed
    ///   Inventory (I / gamepad dpad-right) → InventoryTogglePressed
    ///   Main menu (Esc / gamepad start)    → MenuStateChanged toggle
    /// The bridge forwards them onto the toggleMap / toggleInventory /
    /// mainMenuState channels. The project owns the UIs themselves — the menu
    /// open state is shared: a project can also open/close the menu by raising
    /// the mainMenuState channel, and this component keeps in sync.
    /// </summary>
    public class UiToggleInput : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [SerializeField] private PlayerInput playerInput;
        [Tooltip("Esc/start toggles the menu state from the package. TURN OFF on your Player variant when the project has " +
            "its own Escape/menu handling that raises the mainMenuState channel — two drivers on the same key fight each other.")]
        [SerializeField] private bool escapeTogglesMenu = true;

        private InputAction mapAction;
        private InputAction inventoryAction;
        private InputAction mainMenuAction;
        private bool menuOpen;

        private void OnEnable()
        {
            mapAction = ResolveOptionalAction("ToggleMap");
            inventoryAction = ResolveOptionalAction("ToggleInventory");
            mainMenuAction = escapeTogglesMenu ? ResolveOptionalAction("MainMenu") : null;

            // Single owner for Escape: when a MainMenuController is in the scene
            // it drives the menu (and the pause that goes with it). Two drivers
            // on the same key toggle TWICE per press — the menu opens and
            // instantly closes again.
            if (mainMenuAction != null)
            {
                var menuOwner = FindFirstObjectByType<MainMenuController>();
                if (menuOwner != null)
                {
                    Debug.Log($"{LogPrefix} UiToggleInput on '{name}': '{menuOwner.name}' has a MainMenuController — " +
                        "it owns the Escape/menu toggle, so UiToggleInput stands down on MainMenu (two drivers would toggle twice per press).", this);
                    mainMenuAction = null;
                }
            }
            if (mapAction != null) mapAction.performed += OnMapPerformed;
            if (inventoryAction != null) inventoryAction.performed += OnInventoryPerformed;
            if (mainMenuAction != null) mainMenuAction.performed += OnMainMenuPerformed;
            PlayerEvents.MenuStateChanged += OnMenuStateChanged;
        }

        private void OnDisable()
        {
            if (mapAction != null) mapAction.performed -= OnMapPerformed;
            if (inventoryAction != null) inventoryAction.performed -= OnInventoryPerformed;
            if (mainMenuAction != null) mainMenuAction.performed -= OnMainMenuPerformed;
            PlayerEvents.MenuStateChanged -= OnMenuStateChanged;
        }

        private void OnMapPerformed(InputAction.CallbackContext _) => PlayerEvents.RaiseMapToggle();
        private void OnInventoryPerformed(InputAction.CallbackContext _) => PlayerEvents.RaiseInventoryToggle();

        private void OnMainMenuPerformed(InputAction.CallbackContext _)
        {
            // Esc/start toggles the menu state package-side, so the cursor frees and
            // locomotion freezes even before a project menu UI is hooked up.
            PlayerEvents.RaiseMenuState(!menuOpen);
        }

        // Mirrors every raise (ours or the project's) so the next toggle is correct.
        private void OnMenuStateChanged(bool isOpen) => menuOpen = isOpen;

        private InputAction ResolveOptionalAction(string actionName)
        {
            if (playerInput == null || playerInput.actions == null)
            {
                Debug.LogWarning($"{LogPrefix} UiToggleInput on '{name}': playerInput (or its actions asset) is not assigned — " +
                    $"the '{actionName}' action cannot be resolved, so that binding is disabled.", this);
                return null;
            }
            var action = playerInput.actions.FindAction($"FPS/{actionName}", throwIfNotFound: false);
            if (action == null)
            {
                Debug.LogWarning($"{LogPrefix} UiToggleInput on '{name}': no '{actionName}' action in the FPS map of " +
                    $"'{playerInput.actions.name}' — that binding is disabled. The package ships it in UniversalPlayer_InputActions; " +
                    $"if your project uses its own copy of the asset, add a Button action named '{actionName}' to the FPS map.", this);
            }
            return action;
        }
    }
}
