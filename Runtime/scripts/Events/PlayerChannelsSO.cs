using jeanf.EventSystem;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// The ONE asset naming every event channel the player exchanges with the project.
    /// Assigned on the <see cref="PlayerEventBridge"/> (Player prefab root) — the single
    /// wiring point. Projects duplicate the packaged default and point it at their own
    /// channel assets; nothing else needs rewiring.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerChannels", menuName = "UniversalPlayer/Player Channels")]
    public class PlayerChannelsSO : ScriptableObject
    {
        [Header("Outbound — the player reports on these")]
        [Tooltip("Raised (Void) every time the control scheme changes; read BroadcastControlsStatus.controlScheme for the value.")]
        public VoidEventChannelSO controlSchemeChanged;
        public BoolEventChannelSO hmdState;
        public BoolEventChannelSO hmdConnection;
        public StringEventChannelSO xrIssueMessage;
        public BoolEventChannelSO playerIsMoving;
        public BoolEventChannelSO seatedState;
        public StringEventChannelSO fallRecoveryMessage;
        [Tooltip("Raised (Void) when the player presses the Map binding (M / gamepad dpad-left). The project owns the map UI and its open state.")]
        public VoidEventChannelSO toggleMap;
        [Tooltip("Raised (Void) when the player presses the Inventory binding (I / gamepad dpad-right). The project owns the inventory UI and its open state.")]
        public VoidEventChannelSO toggleInventory;

        [Header("Inbound — the player reacts to these")]
        public BoolEventChannelSO mouselookState;
        [Tooltip("True while the main menu is open: the cursor frees up and locomotion freezes. Bidirectional — Esc/start toggles it from the player, and the project can raise it too.")]
        public BoolEventChannelSO mainMenuState;
        public BoolEventChannelSO sceneIsLoading;
        [Tooltip("Reserved for the battery warning system's auto-pause (and any project pause flow).")]
        public BoolEventChannelSO pause;
        [Tooltip("Scenario seating: raise a Seat's GameObject to sit the player there (instant while the loading fade is black), null to stand up.")]
        public GameObjectEventChannelSO sitRequest;
        [Tooltip("Player teleports (camera reset reacts to these).")]
        public TeleportEventChannelSO playerTeleport;
        [Tooltip("Object teleports (ground level tracking reacts to these — preserving the original wiring).")]
        public TeleportEventChannelSO objectTeleport;

        [Header("Bidirectional")]
        public VoidEventChannelSO cameraReset;
    }
}
