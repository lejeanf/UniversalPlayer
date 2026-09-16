using System;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.universalplayer
{
    /// <summary>
    /// The player's internal delegate surface. Package components raise and subscribe
    /// HERE — plain C# events: zero inspector wiring, compile-checked names, no
    /// per-frame ScriptableObject indirection. SO event channels exist only at the
    /// project boundary: <see cref="PlayerChannelsSO"/> names them, and
    /// <see cref="PlayerEventBridge"/> is the ONLY script that raises or subscribes
    /// to a channel (enforced by PlayerChannelsIsolationTests).
    ///
    /// House rule applies: subscribe and unsubscribe with NAMED methods only.
    /// (Control-scheme changes stay on <see cref="BroadcastControlsStatus.SendControlScheme"/>,
    /// which already followed this pattern before the bridge existed.)
    ///
    /// Each event states its hub slot when it has one. "internal" events never
    /// leave the package — ask for a PlayerChannelsSO slot if a project needs one.
    /// </summary>
    public static class PlayerEvents
    {
        // ---- outbound: the package raises, the bridge forwards to project channels ----
        public static event Action<bool> HmdStateChanged;                    // hub: hmdState
        public static event Action<bool> HmdConnectionChanged;               // hub: hmdConnection
        public static event Action<string> XrIssueReported;                  // hub: xrIssueMessage
        public static event Action<bool> PlayerMovingChanged;                // hub: playerIsMoving
        public static event Action<bool> SeatedChanged;                      // hub: seatedState
        public static event Action<string> FallRecovered;                    // hub: fallRecoveryMessage
        public static event Action MapTogglePressed;                         // hub: toggleMap
        public static event Action InventoryTogglePressed;                   // hub: toggleInventory
        /// <summary>The primary item was shown in (true) / hidden from (false) a VR hand — the VR flow's own report, distinct from the shared drawn/holstered state.</summary>
        public static event Action<bool> PrimaryItemVrStateChanged;          // hub: primaryItemStateVr
        /// <summary>(taken object, room id, true) — TakeObject picked something up (desktop grab).</summary>
        public static event Action<GameObject, int, bool> ObjectTaken;        // hub: objectTaken
        public static event Action<GameObject> ObjectDropped;                // hub: objectDropped
        /// <summary>ActionRebinder finished an interactive rebind of (action, binding index).</summary>
        public static event Action<InputAction, int> ActionRebound;          // hub: actionRebound
        /// <summary>How many VR hands are currently grabbing (0..2).</summary>
        public static event Action<int> GrabCountChanged;                    // hub: grabCount
        /// <summary>A hand entered (true) / left (false) its pointing pose (opposite-hand grab rule).</summary>
        public static event Action<HandType, bool> HandPointingChanged;      // hub: leftHandIsPointing / rightHandIsPointing
        /// <summary>A PerformAction button matched a press; the payload is the transform that was hit.</summary>
        public static event Action<Transform> ActionPerformed;               // hub: actionMade
        /// <summary>A SnapObject entered (SnapBegun) / left (SnapEnded) a SnapZone; the payload is the snapping object.</summary>
        public static event Action<GameObject> SnapBegun;                    // hub: snapBegun
        public static event Action<GameObject> SnapEnded;                    // hub: snapEnded

        // ---- bidirectional: raised internally AND by project channels ----
        public static event Action CameraResetRequested;                     // hub: cameraReset
        /// <summary>Drawn (true) / holstered (false) — the ONE primary item (tablet) state every system follows. Projects may raise it too.</summary>
        public static event Action<bool> PrimaryItemStateChanged;            // hub: primaryItemState
        /// <summary>A teleport is REQUESTED (SendTeleportTarget or a project channel); TeleportOnEvent performs it, then raises PlayerTeleported / ObjectTeleported.</summary>
        public static event Action<TeleportInformation> TeleportRequested;   // hub: playerTeleport / objectTeleport (by objectIsPlayer)
        public static event Action<bool> MouselookStateChanged;              // hub: mouselookState
        public static event Action<bool> MenuStateChanged;                   // hub: mainMenuState
        public static event Action<bool> PauseRequested;                     // hub: pause

        // ---- inbound: the bridge forwards project channel raises, internals react ----
        public static event Action<bool> SceneLoadingChanged;                // hub: sceneIsLoading
        public static event Action<GameObject> SitRequested;                 // hub: sitRequest
        /// <summary>A text field has focus (true) — locomotion and the draw binding are blocked while typing.</summary>
        public static event Action<bool> InputFieldFocusChanged;             // hub: inputFieldFocused
        public static event Action<bool> GloveStateChanged;                  // hub: gloveState
        /// <summary>Show the primary item in the hand OPPOSITE to the one named ("LeftHand"/"RightHand"), e.g. the hand that pressed a world button.</summary>
        public static event Action<string> PrimaryItemHandRequested;         // hub: primaryItemDrawWithHand
        /// <summary>Haptic burst on "Left" or "Right".</summary>
        public static event Action<string> HapticRequested;                  // hub: hapticFeedback
        /// <summary>Loading status text ("" = not loading) and progress 0..1 — SceneManagement's LoadingInformation broadcasts them.</summary>
        public static event Action<string> LoadingStatusChanged;             // hub: loadingStatus
        public static event Action<float> LoadingProgressChanged;            // hub: loadingProgress
        public static event Action<int> RoomIdChanged;                       // hub: roomId
        /// <summary>The project's rebind UI asks for an interactive rebind of (action, binding index).</summary>
        public static event Action<InputAction, int> RebindRequested;        // hub: rebindRequested

        // ---- internal: "the teleport HAPPENED" (raised by TeleportOnEvent after the move). Not bridged. ----
        public static event Action<TeleportInformation> PlayerTeleported;
        public static event Action<TeleportInformation> ObjectTeleported;

        // ---- internal: FadeMask reports when the world is black from loading /
        // teleporting (a menu-caused black is NOT included — menus need a cursor) ----
        public static event Action<bool> ScreenFadeChanged;

        // ---- internal: something rejected a click/interaction ("you can't do that
        // here"). CursorStateController flashes the reticle its invalid color. ----
        public static event Action InvalidActionSignaled;

        // ---- internal: locomotion moments raised by PlayerMovement (desktop modes —
        // XR has no jump and XRI owns its locomotion). FootstepAudio plays foley off
        // these; animation/VFX/project code can subscribe too. ----
        public static event Action PlayerJumped;
        /// <summary>Feet regained the ground; the payload is the downward impact speed in m/s (positive).</summary>
        public static event Action<float> PlayerLanded;
        /// <summary>Crossed the crouch midpoint: true = now crouched, false = stood back up.</summary>
        public static event Action<bool> CrouchStateChanged;

        // ---- internal: VR hand plumbing (was a web of per-hand channel assets) ----
        /// <summary>A hand's grab pose was claimed (true) / released (false) — HandPoseManager reports it.</summary>
        public static event Action<HandType, bool> HandGrabStateChanged;
        /// <summary>An XRBaseInteractorSender found the interactor driving a hand.</summary>
        public static event Action<HandType, XRBaseInteractor> HandInteractorReported;
        /// <summary>A hand entered / left a HandDetectionZoneReporter trigger (pointing-pose zones).</summary>
        public static event Action HandEnteredDetectionZone;
        public static event Action HandLeftDetectionZone;

        public static void RaiseHmdState(bool mounted) => HmdStateChanged?.Invoke(mounted);
        public static void RaiseHmdConnection(bool connected) => HmdConnectionChanged?.Invoke(connected);
        public static void RaiseXrIssue(string message) => XrIssueReported?.Invoke(message);
        public static void RaisePlayerMoving(bool moving) => PlayerMovingChanged?.Invoke(moving);
        public static void RaiseSeated(bool seated) => SeatedChanged?.Invoke(seated);
        public static void RaiseFallRecovered(string message) => FallRecovered?.Invoke(message);
        public static void RaiseMapToggle() => MapTogglePressed?.Invoke();
        public static void RaiseInventoryToggle() => InventoryTogglePressed?.Invoke();
        public static void RaisePrimaryItemVrState(bool shown) => PrimaryItemVrStateChanged?.Invoke(shown);
        public static void RaiseObjectTaken(GameObject taken, int roomId, bool isTaken) => ObjectTaken?.Invoke(taken, roomId, isTaken);
        public static void RaiseObjectDropped(GameObject dropped) => ObjectDropped?.Invoke(dropped);
        public static void RaiseActionRebound(InputAction action, int bindingIndex) => ActionRebound?.Invoke(action, bindingIndex);
        public static void RaiseGrabCount(int count) => GrabCountChanged?.Invoke(count);
        public static void RaiseHandPointing(HandType hand, bool pointing) => HandPointingChanged?.Invoke(hand, pointing);
        public static void RaiseActionPerformed(Transform hit) => ActionPerformed?.Invoke(hit);
        public static void RaiseSnapBegun(GameObject snapping) => SnapBegun?.Invoke(snapping);
        public static void RaiseSnapEnded(GameObject snapping) => SnapEnded?.Invoke(snapping);

        public static void RaiseCameraReset() => CameraResetRequested?.Invoke();
        public static void RaisePrimaryItemState(bool drawn) => PrimaryItemStateChanged?.Invoke(drawn);
        public static void RaiseTeleportRequested(TeleportInformation info) => TeleportRequested?.Invoke(info);
        public static void RaiseMouselookState(bool canLook) => MouselookStateChanged?.Invoke(canLook);
        public static void RaiseMenuState(bool menuOpen) => MenuStateChanged?.Invoke(menuOpen);
        public static void RaisePause(bool paused) => PauseRequested?.Invoke(paused);

        public static void RaiseSceneLoading(bool loading) => SceneLoadingChanged?.Invoke(loading);
        public static void RaiseSitRequest(GameObject seatObject) => SitRequested?.Invoke(seatObject);
        public static void RaiseInputFieldFocus(bool focused) => InputFieldFocusChanged?.Invoke(focused);
        public static void RaiseGloveState(bool gloved) => GloveStateChanged?.Invoke(gloved);
        public static void RaisePrimaryItemHandRequest(string hand) => PrimaryItemHandRequested?.Invoke(hand);
        public static void RaiseHapticRequest(string hand) => HapticRequested?.Invoke(hand);
        public static void RaiseLoadingStatus(string status) => LoadingStatusChanged?.Invoke(status);
        public static void RaiseLoadingProgress(float progress01) => LoadingProgressChanged?.Invoke(progress01);
        public static void RaiseRoomId(int roomId) => RoomIdChanged?.Invoke(roomId);
        public static void RaiseRebindRequest(InputAction action, int bindingIndex) => RebindRequested?.Invoke(action, bindingIndex);

        public static void RaisePlayerTeleported(TeleportInformation info) => PlayerTeleported?.Invoke(info);
        public static void RaiseObjectTeleported(TeleportInformation info) => ObjectTeleported?.Invoke(info);
        public static void RaiseScreenFade(bool faded) => ScreenFadeChanged?.Invoke(faded);

        /// <summary>Flash the cursor's "invalid request" color — call when a click/interaction is rejected.</summary>
        public static void RaiseInvalidAction() => InvalidActionSignaled?.Invoke();

        public static void RaisePlayerJumped() => PlayerJumped?.Invoke();
        public static void RaisePlayerLanded(float impactSpeed) => PlayerLanded?.Invoke(impactSpeed);
        public static void RaiseCrouchState(bool crouched) => CrouchStateChanged?.Invoke(crouched);

        public static void RaiseHandGrabState(HandType hand, bool grabbing) => HandGrabStateChanged?.Invoke(hand, grabbing);
        public static void RaiseHandInteractorReported(HandType hand, XRBaseInteractor interactor) => HandInteractorReported?.Invoke(hand, interactor);
        public static void RaiseHandEnteredDetectionZone() => HandEnteredDetectionZone?.Invoke();
        public static void RaiseHandLeftDetectionZone() => HandLeftDetectionZone?.Invoke();
    }
}
