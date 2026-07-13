using jeanf.EventSystem;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// The single wiring point between the player's internal delegate surface
    /// (<see cref="PlayerEvents"/>) and the project's SO event channels
    /// (<see cref="PlayerChannelsSO"/>). Sits on the Player prefab root.
    ///
    /// Outbound: internal events are forwarded onto the assigned channels so the
    /// project keeps hearing everything it heard before.
    /// Inbound: channel raises from scenes/UI (teleports, scene loading, mouselook
    /// lock, pause, camera reset) are forwarded onto the internal events.
    /// A re-entrancy guard keeps bidirectional signals (camera reset) from looping.
    /// </summary>
    public class PlayerEventBridge : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("The one asset naming every boundary channel. Duplicate the packaged default and point it at your project's channels.")]
        [SerializeField] private PlayerChannelsSO channels;
        public PlayerChannelsSO Channels => channels;

        private bool forwardingInbound;
        private bool forwardingOutbound;

        private void OnEnable()
        {
            if (channels == null)
            {
                Debug.LogError($"{LogPrefix} PlayerEventBridge on '{name}': no PlayerChannelsSO assigned — EVERY event " +
                    "between the player and the project is silent (teleports in, movement/seated/XR reports out). " +
                    "Assign the packaged UniversalPlayerChannels asset or your project's copy.", this);
                return;
            }

            // outbound: internal delegates -> project channels
            BroadcastControlsStatus.SendControlScheme += OnControlSchemeChanged;
            PlayerEvents.HmdStateChanged += OnHmdStateChanged;
            PlayerEvents.HmdConnectionChanged += OnHmdConnectionChanged;
            PlayerEvents.XrIssueReported += OnXrIssueReported;
            PlayerEvents.PlayerMovingChanged += OnPlayerMovingChanged;
            PlayerEvents.SeatedChanged += OnSeatedChanged;
            PlayerEvents.FallRecovered += OnFallRecovered;
            PlayerEvents.MapTogglePressed += OnMapTogglePressed;
            PlayerEvents.InventoryTogglePressed += OnInventoryTogglePressed;
            PlayerEvents.CameraResetRequested += OnCameraResetRequested;
            // Bidirectional: internals RAISE these too (CursorStateController raises the
            // mouselook lock, BatteryWarningSystem raises pause) and project listeners
            // sit on the channels — the guard prevents inbound echoes.
            PlayerEvents.MouselookStateChanged += OnMouselookStateChangedInternal;
            PlayerEvents.PauseRequested += OnPauseRequestedInternal;
            PlayerEvents.MenuStateChanged += OnMenuStateChangedInternal;

            // inbound: project channels -> internal delegates
            if (channels.mouselookState != null) channels.mouselookState.OnEventRaised += OnMouselookChannel;
            if (channels.mainMenuState != null) channels.mainMenuState.OnEventRaised += OnMainMenuChannel;
            if (channels.sceneIsLoading != null) channels.sceneIsLoading.OnEventRaised += OnSceneLoadingChannel;
            if (channels.pause != null) channels.pause.OnEventRaised += OnPauseChannel;
            if (channels.sitRequest != null) channels.sitRequest.OnEventRaised += OnSitRequestChannel;
            if (channels.playerTeleport != null) channels.playerTeleport.OnEventRaised += OnPlayerTeleportChannel;
            if (channels.objectTeleport != null) channels.objectTeleport.OnEventRaised += OnObjectTeleportChannel;
            if (channels.cameraReset != null) channels.cameraReset.OnEventRaised += OnCameraResetChannel;
        }

        private void OnDisable()
        {
            BroadcastControlsStatus.SendControlScheme -= OnControlSchemeChanged;
            PlayerEvents.HmdStateChanged -= OnHmdStateChanged;
            PlayerEvents.HmdConnectionChanged -= OnHmdConnectionChanged;
            PlayerEvents.XrIssueReported -= OnXrIssueReported;
            PlayerEvents.PlayerMovingChanged -= OnPlayerMovingChanged;
            PlayerEvents.SeatedChanged -= OnSeatedChanged;
            PlayerEvents.FallRecovered -= OnFallRecovered;
            PlayerEvents.MapTogglePressed -= OnMapTogglePressed;
            PlayerEvents.InventoryTogglePressed -= OnInventoryTogglePressed;
            PlayerEvents.CameraResetRequested -= OnCameraResetRequested;
            PlayerEvents.MouselookStateChanged -= OnMouselookStateChangedInternal;
            PlayerEvents.PauseRequested -= OnPauseRequestedInternal;
            PlayerEvents.MenuStateChanged -= OnMenuStateChangedInternal;

            if (channels == null) return;
            if (channels.mouselookState != null) channels.mouselookState.OnEventRaised -= OnMouselookChannel;
            if (channels.mainMenuState != null) channels.mainMenuState.OnEventRaised -= OnMainMenuChannel;
            if (channels.sceneIsLoading != null) channels.sceneIsLoading.OnEventRaised -= OnSceneLoadingChannel;
            if (channels.pause != null) channels.pause.OnEventRaised -= OnPauseChannel;
            if (channels.sitRequest != null) channels.sitRequest.OnEventRaised -= OnSitRequestChannel;
            if (channels.playerTeleport != null) channels.playerTeleport.OnEventRaised -= OnPlayerTeleportChannel;
            if (channels.objectTeleport != null) channels.objectTeleport.OnEventRaised -= OnObjectTeleportChannel;
            if (channels.cameraReset != null) channels.cameraReset.OnEventRaised -= OnCameraResetChannel;
        }

        // ---- outbound handlers ----
        private void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme _) { if (!forwardingInbound && channels.controlSchemeChanged != null) channels.controlSchemeChanged.RaiseEvent(); }
        private void OnHmdStateChanged(bool mounted) { if (!forwardingInbound && channels.hmdState != null) channels.hmdState.RaiseEvent(mounted); }
        private void OnHmdConnectionChanged(bool connected) { if (!forwardingInbound && channels.hmdConnection != null) channels.hmdConnection.RaiseEvent(connected); }
        private void OnXrIssueReported(string message) { if (!forwardingInbound && channels.xrIssueMessage != null) channels.xrIssueMessage.RaiseEvent(message); }
        private void OnPlayerMovingChanged(bool moving) { if (!forwardingInbound && channels.playerIsMoving != null) channels.playerIsMoving.RaiseEvent(moving); }
        private void OnSeatedChanged(bool seated) { if (!forwardingInbound && channels.seatedState != null) channels.seatedState.RaiseEvent(seated); }
        private void OnFallRecovered(string message) { if (!forwardingInbound && channels.fallRecoveryMessage != null) channels.fallRecoveryMessage.RaiseEvent(message); }
        private void OnMapTogglePressed() { if (!forwardingInbound && channels.toggleMap != null) channels.toggleMap.RaiseEvent(); }
        private void OnInventoryTogglePressed() { if (!forwardingInbound && channels.toggleInventory != null) channels.toggleInventory.RaiseEvent(); }
        private void OnCameraResetRequested() { ForwardOutbound(channels.cameraReset); }
        private void OnMouselookStateChangedInternal(bool canLook) { ForwardOutbound(channels.mouselookState, canLook); }
        private void OnPauseRequestedInternal(bool paused) { ForwardOutbound(channels.pause, paused); }
        private void OnMenuStateChangedInternal(bool menuOpen) { ForwardOutbound(channels.mainMenuState, menuOpen); }

        private void ForwardOutbound(VoidEventChannelSO channel)
        {
            if (forwardingInbound || channel == null) return;
            forwardingOutbound = true;
            try { channel.RaiseEvent(); }
            finally { forwardingOutbound = false; }
        }

        private void ForwardOutbound(BoolEventChannelSO channel, bool value)
        {
            if (forwardingInbound || channel == null) return;
            forwardingOutbound = true;
            try { channel.RaiseEvent(value); }
            finally { forwardingOutbound = false; }
        }

        // ---- inbound handlers ----
        private void OnMouselookChannel(bool canLook) => ForwardInbound(() => PlayerEvents.RaiseMouselookState(canLook));
        private void OnMainMenuChannel(bool menuOpen) => ForwardInbound(() => PlayerEvents.RaiseMenuState(menuOpen));
        private void OnSceneLoadingChannel(bool loading) => ForwardInbound(() => PlayerEvents.RaiseSceneLoading(loading));
        private void OnPauseChannel(bool paused) => ForwardInbound(() => PlayerEvents.RaisePause(paused));
        private void OnSitRequestChannel(GameObject seatObject) => ForwardInbound(() => PlayerEvents.RaiseSitRequest(seatObject));
        private void OnPlayerTeleportChannel(TeleportInformation info) => ForwardInbound(() => PlayerEvents.RaisePlayerTeleported(info));
        private void OnObjectTeleportChannel(TeleportInformation info) => ForwardInbound(() => PlayerEvents.RaiseObjectTeleported(info));
        private void OnCameraResetChannel() => ForwardInbound(PlayerEvents.RaiseCameraReset);

        private void ForwardInbound(System.Action raise)
        {
            // An outbound forward raising this very channel echoes back here: the
            // internal event already fired, do not deliver it a second time.
            if (forwardingOutbound) return;
            forwardingInbound = true;
            try { raise(); }
            finally { forwardingInbound = false; }
        }
    }
}
