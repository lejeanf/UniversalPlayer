using jeanf.EventSystem;
using jeanf.validationTools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    /// <summary>
    /// The single wiring point between the player's internal delegate surface
    /// (<see cref="PlayerEvents"/>) and the project's SO event channels
    /// (<see cref="PlayerChannelsSO"/>). Sits on the Player prefab under Settings/Events.
    ///
    /// This is the ONLY package script that raises or subscribes to an event channel
    /// (PlayerChannelsIsolationTests enforce it). Every other component talks over
    /// PlayerEvents; a signal a project needs gets a slot on the channels asset and
    /// one forward here.
    ///
    /// Outbound: internal events are forwarded onto the assigned channels so the
    /// project keeps hearing everything it heard before.
    /// Inbound: channel raises from scenes/UI (teleports, scene loading, mouselook
    /// lock, pause, focus, gloves, haptics...) are forwarded onto the internal events.
    /// A per-channel re-entrancy guard keeps bidirectional signals (camera reset, menu
    /// state, primary item state, teleport requests...) from echoing back and forth,
    /// while a DIFFERENT signal raised in reaction (menu opened by the project → pause)
    /// still reaches the project.
    /// </summary>
    public class PlayerEventBridge : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("The one asset naming every boundary channel. Duplicate the packaged default and point it at your project's channels.")]
        [Validation("No PlayerChannelsSO — every event between the player and the project is silent (teleports in, movement/seated/XR reports out).")]
        [SerializeField] private PlayerChannelsSO channels;
        public PlayerChannelsSO Channels => channels;

        // The channel whose raise is being forwarded inward / outward right now (null
        // when idle). Only that very channel is muted in the opposite direction.
        private Object inboundChannel;
        private Object outboundChannel;

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
            PlayerEvents.PrimaryItemVrStateChanged += OnPrimaryItemVrStateChanged;
            PlayerEvents.ObjectTaken += OnObjectTaken;
            PlayerEvents.ObjectDropped += OnObjectDropped;
            PlayerEvents.ActionRebound += OnActionRebound;
            PlayerEvents.GrabCountChanged += OnGrabCountChanged;
            PlayerEvents.HandPointingChanged += OnHandPointingChanged;
            PlayerEvents.ActionPerformed += OnActionPerformed;
            PlayerEvents.SnapBegun += OnSnapBegun;
            PlayerEvents.SnapEnded += OnSnapEnded;
            // Bidirectional: internals RAISE these too (CursorStateController raises the
            // mouselook lock, MainMenuController raises menu + pause, SendTeleportTarget
            // raises teleport requests...) and project listeners sit on the channels —
            // the guard prevents inbound echoes.
            PlayerEvents.CameraResetRequested += OnCameraResetRequested;
            PlayerEvents.MouselookStateChanged += OnMouselookStateChangedInternal;
            PlayerEvents.PauseRequested += OnPauseRequestedInternal;
            PlayerEvents.MenuStateChanged += OnMenuStateChangedInternal;
            PlayerEvents.PrimaryItemStateChanged += OnPrimaryItemStateChangedInternal;
            PlayerEvents.TeleportRequested += OnTeleportRequestedInternal;

            // inbound: project channels -> internal delegates
            if (channels.mouselookState != null) channels.mouselookState.OnEventRaised += OnMouselookChannel;
            if (channels.mainMenuState != null) channels.mainMenuState.OnEventRaised += OnMainMenuChannel;
            if (channels.sceneIsLoading != null) channels.sceneIsLoading.OnEventRaised += OnSceneLoadingChannel;
            if (channels.pause != null) channels.pause.OnEventRaised += OnPauseChannel;
            if (channels.sitRequest != null) channels.sitRequest.OnEventRaised += OnSitRequestChannel;
            if (channels.playerTeleport != null) channels.playerTeleport.OnEventRaised += OnTeleportChannel;
            if (channels.objectTeleport != null && channels.objectTeleport != channels.playerTeleport) channels.objectTeleport.OnEventRaised += OnTeleportChannel;
            if (channels.cameraReset != null) channels.cameraReset.OnEventRaised += OnCameraResetChannel;
            if (channels.primaryItemState != null) channels.primaryItemState.OnEventRaised += OnPrimaryItemStateChannel;
            if (channels.inputFieldFocused != null) channels.inputFieldFocused.OnEventRaised += OnInputFieldFocusedChannel;
            if (channels.gloveState != null) channels.gloveState.OnEventRaised += OnGloveStateChannel;
            if (channels.primaryItemDrawWithHand != null) channels.primaryItemDrawWithHand.OnEventRaised += OnPrimaryItemDrawWithHandChannel;
            if (channels.hapticFeedback != null) channels.hapticFeedback.OnEventRaised += OnHapticFeedbackChannel;
            if (channels.loadingStatus != null) channels.loadingStatus.OnEventRaised += OnLoadingStatusChannel;
            if (channels.loadingProgress != null) channels.loadingProgress.OnEventRaised += OnLoadingProgressChannel;
            if (channels.roomId != null) channels.roomId.OnEventRaised += OnRoomIdChannel;
            if (channels.rebindRequested != null) channels.rebindRequested.OnEventRaised += OnRebindRequestedChannel;
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
            PlayerEvents.PrimaryItemVrStateChanged -= OnPrimaryItemVrStateChanged;
            PlayerEvents.ObjectTaken -= OnObjectTaken;
            PlayerEvents.ObjectDropped -= OnObjectDropped;
            PlayerEvents.ActionRebound -= OnActionRebound;
            PlayerEvents.GrabCountChanged -= OnGrabCountChanged;
            PlayerEvents.HandPointingChanged -= OnHandPointingChanged;
            PlayerEvents.ActionPerformed -= OnActionPerformed;
            PlayerEvents.SnapBegun -= OnSnapBegun;
            PlayerEvents.SnapEnded -= OnSnapEnded;
            PlayerEvents.CameraResetRequested -= OnCameraResetRequested;
            PlayerEvents.MouselookStateChanged -= OnMouselookStateChangedInternal;
            PlayerEvents.PauseRequested -= OnPauseRequestedInternal;
            PlayerEvents.MenuStateChanged -= OnMenuStateChangedInternal;
            PlayerEvents.PrimaryItemStateChanged -= OnPrimaryItemStateChangedInternal;
            PlayerEvents.TeleportRequested -= OnTeleportRequestedInternal;

            if (channels == null) return;
            if (channels.mouselookState != null) channels.mouselookState.OnEventRaised -= OnMouselookChannel;
            if (channels.mainMenuState != null) channels.mainMenuState.OnEventRaised -= OnMainMenuChannel;
            if (channels.sceneIsLoading != null) channels.sceneIsLoading.OnEventRaised -= OnSceneLoadingChannel;
            if (channels.pause != null) channels.pause.OnEventRaised -= OnPauseChannel;
            if (channels.sitRequest != null) channels.sitRequest.OnEventRaised -= OnSitRequestChannel;
            if (channels.playerTeleport != null) channels.playerTeleport.OnEventRaised -= OnTeleportChannel;
            if (channels.objectTeleport != null && channels.objectTeleport != channels.playerTeleport) channels.objectTeleport.OnEventRaised -= OnTeleportChannel;
            if (channels.cameraReset != null) channels.cameraReset.OnEventRaised -= OnCameraResetChannel;
            if (channels.primaryItemState != null) channels.primaryItemState.OnEventRaised -= OnPrimaryItemStateChannel;
            if (channels.inputFieldFocused != null) channels.inputFieldFocused.OnEventRaised -= OnInputFieldFocusedChannel;
            if (channels.gloveState != null) channels.gloveState.OnEventRaised -= OnGloveStateChannel;
            if (channels.primaryItemDrawWithHand != null) channels.primaryItemDrawWithHand.OnEventRaised -= OnPrimaryItemDrawWithHandChannel;
            if (channels.hapticFeedback != null) channels.hapticFeedback.OnEventRaised -= OnHapticFeedbackChannel;
            if (channels.loadingStatus != null) channels.loadingStatus.OnEventRaised -= OnLoadingStatusChannel;
            if (channels.loadingProgress != null) channels.loadingProgress.OnEventRaised -= OnLoadingProgressChannel;
            if (channels.roomId != null) channels.roomId.OnEventRaised -= OnRoomIdChannel;
            if (channels.rebindRequested != null) channels.rebindRequested.OnEventRaised -= OnRebindRequestedChannel;
        }

        // ---- outbound handlers ----
        private void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme _) => ForwardOutbound(channels.controlSchemeChanged);
        private void OnHmdStateChanged(bool mounted) => ForwardOutbound(channels.hmdState, mounted);
        private void OnHmdConnectionChanged(bool connected) => ForwardOutbound(channels.hmdConnection, connected);
        private void OnXrIssueReported(string message) => ForwardOutbound(channels.xrIssueMessage, message);
        private void OnPlayerMovingChanged(bool moving) => ForwardOutbound(channels.playerIsMoving, moving);
        private void OnSeatedChanged(bool seated) => ForwardOutbound(channels.seatedState, seated);
        private void OnFallRecovered(string message) => ForwardOutbound(channels.fallRecoveryMessage, message);
        private void OnMapTogglePressed() => ForwardOutbound(channels.toggleMap);
        private void OnInventoryTogglePressed() => ForwardOutbound(channels.toggleInventory);
        private void OnPrimaryItemVrStateChanged(bool shown) => ForwardOutbound(channels.primaryItemStateVr, shown);
        private void OnObjectTaken(GameObject taken, int roomId, bool isTaken) => ForwardOutbound(channels.objectTaken, taken, roomId, isTaken);
        private void OnObjectDropped(GameObject dropped) => ForwardOutbound(channels.objectDropped, dropped);
        private void OnActionRebound(InputAction action, int bindingIndex) => ForwardOutbound(channels.actionRebound, action, bindingIndex);
        private void OnGrabCountChanged(int count) => ForwardOutbound(channels.grabCount, count);
        private void OnHandPointingChanged(HandType hand, bool pointing) =>
            ForwardOutbound(hand == HandType.Left ? channels.leftHandIsPointing : hand == HandType.Right ? channels.rightHandIsPointing : null, pointing);
        private void OnActionPerformed(Transform hit) => ForwardOutbound(channels.actionMade, hit);
        private void OnSnapBegun(GameObject snapping) => ForwardOutbound(channels.snapBegun, snapping);
        private void OnSnapEnded(GameObject snapping) => ForwardOutbound(channels.snapEnded, snapping);
        private void OnCameraResetRequested() => ForwardOutbound(channels.cameraReset);
        private void OnMouselookStateChangedInternal(bool canLook) => ForwardOutbound(channels.mouselookState, canLook);
        private void OnPauseRequestedInternal(bool paused) => ForwardOutbound(channels.pause, paused);
        private void OnMenuStateChangedInternal(bool menuOpen) => ForwardOutbound(channels.mainMenuState, menuOpen);
        private void OnPrimaryItemStateChangedInternal(bool drawn) => ForwardOutbound(channels.primaryItemState, drawn);
        private void OnTeleportRequestedInternal(TeleportInformation info) => ForwardOutbound(TeleportChannelFor(info), info);

        private TeleportEventChannelSO TeleportChannelFor(TeleportInformation info) =>
            info != null && info.objectIsPlayer ? channels.playerTeleport : channels.objectTeleport;

        // An inbound raise of this very channel is what triggered the internal event:
        // raising it again would echo. Any OTHER channel still gets the signal.
        private bool MutedOutbound(Object channel) => channel == null || channel == inboundChannel;

        private void ForwardOutbound(VoidEventChannelSO channel)
        {
            if (MutedOutbound(channel)) return;
            var previous = outboundChannel; outboundChannel = channel;
            try { channel.RaiseEvent(); } finally { outboundChannel = previous; }
        }

        private void ForwardOutbound(BoolEventChannelSO channel, bool value)
        {
            if (MutedOutbound(channel)) return;
            var previous = outboundChannel; outboundChannel = channel;
            try { channel.RaiseEvent(value); } finally { outboundChannel = previous; }
        }

        private void ForwardOutbound(StringEventChannelSO channel, string value)
        {
            if (MutedOutbound(channel)) return;
            var previous = outboundChannel; outboundChannel = channel;
            try { channel.RaiseEvent(value); } finally { outboundChannel = previous; }
        }

        private void ForwardOutbound(IntEventChannelSO channel, int value)
        {
            if (MutedOutbound(channel)) return;
            var previous = outboundChannel; outboundChannel = channel;
            try { channel.RaiseEvent(value); } finally { outboundChannel = previous; }
        }

        private void ForwardOutbound(GameObjectEventChannelSO channel, GameObject value)
        {
            if (MutedOutbound(channel)) return;
            var previous = outboundChannel; outboundChannel = channel;
            try { channel.RaiseEvent(value); } finally { outboundChannel = previous; }
        }

        private void ForwardOutbound(TransformEventChannelSO channel, Transform value)
        {
            if (MutedOutbound(channel)) return;
            var previous = outboundChannel; outboundChannel = channel;
            try { channel.RaiseEvent(value); } finally { outboundChannel = previous; }
        }

        private void ForwardOutbound(GameObjectIntBoolEventChannelSO channel, GameObject go, int number, bool value)
        {
            if (MutedOutbound(channel)) return;
            var previous = outboundChannel; outboundChannel = channel;
            try { channel.RaiseEvent(go, number, value); } finally { outboundChannel = previous; }
        }

        private void ForwardOutbound(ActionRebindEventChannelSO channel, InputAction action, int bindingIndex)
        {
            if (MutedOutbound(channel)) return;
            var previous = outboundChannel; outboundChannel = channel;
            try { channel.RaiseEvent(action, bindingIndex); } finally { outboundChannel = previous; }
        }

        private void ForwardOutbound(TeleportEventChannelSO channel, TeleportInformation info)
        {
            if (MutedOutbound(channel)) return;
            var previous = outboundChannel; outboundChannel = channel;
            try { channel.RaiseEvent(info); } finally { outboundChannel = previous; }
        }

        // ---- inbound handlers ----
        private void OnMouselookChannel(bool canLook) => ForwardInbound(channels.mouselookState, () => PlayerEvents.RaiseMouselookState(canLook));
        private void OnMainMenuChannel(bool menuOpen) => ForwardInbound(channels.mainMenuState, () => PlayerEvents.RaiseMenuState(menuOpen));
        private void OnSceneLoadingChannel(bool loading) => ForwardInbound(channels.sceneIsLoading, () => PlayerEvents.RaiseSceneLoading(loading));
        private void OnPauseChannel(bool paused) => ForwardInbound(channels.pause, () => PlayerEvents.RaisePause(paused));
        private void OnSitRequestChannel(GameObject seatObject) => ForwardInbound(channels.sitRequest, () => PlayerEvents.RaiseSitRequest(seatObject));
        private void OnTeleportChannel(TeleportInformation info) => ForwardInbound(TeleportChannelFor(info), () => PlayerEvents.RaiseTeleportRequested(info));
        private void OnCameraResetChannel() => ForwardInbound(channels.cameraReset, PlayerEvents.RaiseCameraReset);
        private void OnPrimaryItemStateChannel(bool drawn) => ForwardInbound(channels.primaryItemState, () => PlayerEvents.RaisePrimaryItemState(drawn));
        private void OnInputFieldFocusedChannel(bool focused) => ForwardInbound(channels.inputFieldFocused, () => PlayerEvents.RaiseInputFieldFocus(focused));
        private void OnGloveStateChannel(bool gloved) => ForwardInbound(channels.gloveState, () => PlayerEvents.RaiseGloveState(gloved));
        private void OnPrimaryItemDrawWithHandChannel(string hand) => ForwardInbound(channels.primaryItemDrawWithHand, () => PlayerEvents.RaisePrimaryItemHandRequest(hand));
        private void OnHapticFeedbackChannel(string hand) => ForwardInbound(channels.hapticFeedback, () => PlayerEvents.RaiseHapticRequest(hand));
        private void OnLoadingStatusChannel(string status) => ForwardInbound(channels.loadingStatus, () => PlayerEvents.RaiseLoadingStatus(status));
        private void OnLoadingProgressChannel(float progress01) => ForwardInbound(channels.loadingProgress, () => PlayerEvents.RaiseLoadingProgress(progress01));
        private void OnRoomIdChannel(int roomId) => ForwardInbound(channels.roomId, () => PlayerEvents.RaiseRoomId(roomId));
        private void OnRebindRequestedChannel(InputAction action, int bindingIndex) => ForwardInbound(channels.rebindRequested, () => PlayerEvents.RaiseRebindRequest(action, bindingIndex));

        private void ForwardInbound(Object channel, System.Action raise)
        {
            // An outbound forward raising this very channel echoes back here: the
            // internal event already fired, do not deliver it a second time.
            if (channel != null && channel == outboundChannel) return;
            var previous = inboundChannel; inboundChannel = channel;
            try { raise(); }
            finally { inboundChannel = previous; }
        }

        // ---- data-driven forward: ActionSO.eventChannel ----

        /// <summary>
        /// Raises the channel an <see cref="ActionSO"/> names with the value of a performed
        /// input action (PlayerActionManager calls this for every action of the input
        /// asset). Boundary code — it lives here so no other script touches a channel.
        /// Returns false when the channel is empty or of an unsupported type.
        /// </summary>
        public static bool ForwardInputAction(DescriptionBaseSO channel, InputAction.CallbackContext ctx)
        {
            switch (channel)
            {
                case null: return false;
                case VoidEventChannelSO voidChannel: voidChannel.RaiseEvent(); return true;
                case BoolEventChannelSO boolChannel: boolChannel.RaiseEvent(ctx.ReadValueAsButton()); return true;
                case IntEventChannelSO intChannel: intChannel.RaiseEvent(ctx.ReadValue<int>()); return true;
                case FloatEventChannelSO floatChannel: floatChannel.RaiseEvent(ctx.ReadValue<float>()); return true;
                case Vector2EventChannelSO vector2Channel: vector2Channel.RaiseEvent(ctx.ReadValue<Vector2>()); return true;
                case Vector3EventChannelSO vector3Channel: vector3Channel.RaiseEvent(ctx.ReadValue<Vector3>()); return true;
                case QuaternionEventChannelSO quaternionChannel: quaternionChannel.RaiseEvent(ctx.ReadValue<Quaternion>()); return true;
                default:
                    Debug.LogWarning($"{LogPrefix} ActionSO channel '{channel.name}' is a {channel.GetType().Name} — " +
                        "input actions can only be forwarded to Void/Bool/Int/Float/Vector2/Vector3/Quaternion channels.", channel);
                    return false;
            }
        }
    }
}
