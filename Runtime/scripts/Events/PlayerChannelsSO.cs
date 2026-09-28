using jeanf.EventSystem;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// The ONE asset naming every event channel the player exchanges with the project.
    /// Assigned on the <see cref="PlayerEventBridge"/> (Player prefab root) — the single
    /// wiring point. Projects duplicate the packaged default and point it at their own
    /// channel assets; nothing else needs rewiring.
    ///
    /// No other package component references a channel asset: internals talk over
    /// <see cref="PlayerEvents"/> delegates and the bridge forwards each slot below in the
    /// stated direction (enforced by PlayerChannelsIsolationTests). Every slot is optional
    /// at runtime — an empty slot simply keeps that signal inside the package.
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
        [Tooltip("The VR hand flow showed (true) / hid (false) the primary item in a hand — separate from the shared primaryItemState below (legacy 'PrimaryItemState Event VR_Channel').")]
        public BoolEventChannelSO primaryItemStateVr;
        [Tooltip("(object, room id, true) when TakeObject picks something up (desktop grab).")]
        public GameObjectIntBoolEventChannelSO objectTaken;
        [Tooltip("The object TakeObject just released / put back into the world.")]
        public GameObjectEventChannelSO objectDropped;
        [Tooltip("ActionRebinder finished an interactive rebind (action, binding index) — the project's rebind UI refreshes its labels from this.")]
        public ActionRebindEventChannelSO actionRebound;
        [Tooltip("How many VR hands are grabbing (0..2), from PointOnCollisionTriggerWhenGrab.")]
        public IntEventChannelSO grabCount;
        [Tooltip("The left hand entered (true) / left (false) its pointing pose (opposite-hand grab rule).")]
        public BoolEventChannelSO leftHandIsPointing;
        [Tooltip("The right hand entered (true) / left (false) its pointing pose (opposite-hand grab rule).")]
        public BoolEventChannelSO rightHandIsPointing;
        [Tooltip("A PerformAction button matched a press; payload = the transform that was hit (filter on it when several buttons share the slot).")]
        public TransformEventChannelSO actionMade;
        [Tooltip("A SnapObject entered a SnapZone; payload = the snapping object.")]
        public GameObjectEventChannelSO snapBegun;
        [Tooltip("A SnapObject left its SnapZone; payload = the snapping object.")]
        public GameObjectEventChannelSO snapEnded;

        [Header("Inbound — the player reacts to these")]
        public BoolEventChannelSO sceneIsLoading;
        [Tooltip("Scenario seating: raise a Seat's GameObject to sit the player there (instant while the loading fade is black), null to stand up.")]
        public GameObjectEventChannelSO sitRequest;
        [Tooltip("True while a text field has focus (tablet login...): locomotion and the draw binding are blocked so typing never moves the player (legacy 'loginFieldIsOpened').")]
        public BoolEventChannelSO inputFieldFocused;
        [Tooltip("Gloves on (true) / off (false) — HandsAppearanceManager switches the hand material.")]
        public BoolEventChannelSO gloveState;
        [Tooltip("Show the primary item in the hand OPPOSITE to the one named (\"LeftHand\" / \"RightHand\"), e.g. the hand that pressed a world button (legacy 'PrimaryItemStateWithUsedHandChannel').")]
        public StringEventChannelSO primaryItemDrawWithHand;
        [Tooltip("Haptic burst on \"Left\" or \"Right\" (HandVibration). Leave empty if the project never asks for haptics by channel.")]
        public StringEventChannelSO hapticFeedback;
        [Tooltip("Loading status text (\"\" = not loading) — SceneManagement's LoadingInformation (on the project's loading setup, e.g. the AdditiveLoading prefab) broadcasts it; the screenspace HUD shows it.")]
        public StringEventChannelSO loadingStatus;
        [Tooltip("Loading progress 0..1 — SceneManagement's LoadingInformation (on the project's loading setup) broadcasts it; the screenspace HUD's bar follows it.")]
        public FloatEventChannelSO loadingProgress;
        [Tooltip("The room the player is in — TakeObject stamps it on objectTaken. Leave empty if the project has no rooms.")]
        public IntEventChannelSO roomId;
        [Tooltip("The project's rebind UI asks for an interactive rebind of (action, binding index) — ActionRebinder starts listening for the new key.")]
        public ActionRebindEventChannelSO rebindRequested;

        [Header("Bidirectional — raised by the player AND by the project")]
        public VoidEventChannelSO cameraReset;
        [Tooltip("True while the main menu is open: the cursor frees up and locomotion freezes. Esc/start toggles it from the player, and the project can raise it too.")]
        public BoolEventChannelSO mainMenuState;
        [Tooltip("Mouselook on/off. The cursor controller raises it; a project can force it.")]
        public BoolEventChannelSO mouselookState;
        [Tooltip("Pause on/off. The main menu and the battery warning raise it; a project pause flow can raise it too.")]
        public BoolEventChannelSO pause;
        [Tooltip("Drawn (true) / holstered (false) — the ONE primary item (tablet) state. The draw binding, a grab and the VR flow raise it; a project can raise it to force the state.")]
        public BoolEventChannelSO primaryItemState;
        [Tooltip("Player teleport REQUESTS: SendTeleportTarget raises them here for the project; the project can raise its own (TeleportOnEvent performs them).")]
        public TeleportEventChannelSO playerTeleport;
        [Tooltip("Object teleport requests (objectIsPlayer = false) — same flow as playerTeleport.")]
        public TeleportEventChannelSO objectTeleport;
    }
}
