# Player channels hub: internal delegates + one channel bridge

Status: **complete** (1.19.0). Every SO event channel the player exchanges with a project
is named in ONE asset (`PlayerChannelsSO`, packaged default `UniversalPlayerChannels`)
and touched by ONE script (`PlayerEventBridge`, on the Player prefab under
Settings/Events). Every other package component talks over `PlayerEvents` C# delegates.
`Tests/Editor/PlayerChannelsIsolationTests` fail the build the moment a component,
prefab or ScriptableObject references a channel elsewhere.

## The rule

- **Inside the package: C# events.** `PlayerEvents` is the delegate surface — zero
  inspector wiring, compile-checked names, no per-frame ScriptableObject indirection.
  Named-method subscribe/unsubscribe only (the `-= lambda` leak is why).
- **At the boundary: one bridge.** `PlayerEventBridge` holds the `PlayerChannelsSO` and
  forwards each slot in its stated direction (outbound / inbound / bidirectional, with a
  re-entrancy guard so a bidirectional signal never echoes). A project-facing signal gets
  a slot on the asset and one forward in the bridge — nothing else.
- **Nothing else names a channel.** Not a component field, not a UnityEvent on a
  packaged prefab, not a second "hub" ScriptableObject. The one documented exception is
  `ActionSO.eventChannel` (project data: which channel an input action forwards to); the
  bridge does that raising too (`PlayerEventBridge.ForwardInputAction`).

Precedent that already followed the pattern: `BroadcastControlsStatus.SendControlScheme`,
`XrHealthMonitor.OnHealthEvent`, `GetPrimaryInHandItemWithVRController.OnIpadStateChanged`.

## Slots (PlayerChannelsSO) and the PlayerEvents member behind each

| Direction | Slot | Type | PlayerEvents | Raised by / consumed by |
|---|---|---|---|---|
| out | controlSchemeChanged | Void | `BroadcastControlsStatus.SendControlScheme` | scheme switch |
| out | hmdState / hmdConnection / xrIssueMessage | Bool / Bool / String | HmdStateChanged / HmdConnectionChanged / XrIssueReported | XrHealthMonitor |
| out | playerIsMoving | Bool | PlayerMovingChanged | PlayerMovement |
| out | seatedState | Bool | SeatedChanged | SitController |
| out | fallRecoveryMessage | String | FallRecovered | FallRecovery |
| out | toggleMap / toggleInventory | Void | MapTogglePressed / InventoryTogglePressed | UiToggleInput |
| out | primaryItemStateVr | Bool | PrimaryItemVrStateChanged | GetPrimaryInHandItemWithVRController (legacy "PrimaryItemState Event VR_Channel") |
| out | objectTaken | GameObjectIntBool | ObjectTaken | TakeObject (desktop grab) |
| out | objectDropped | GameObject | ObjectDropped | TakeObject |
| out | actionRebound | ActionRebind | ActionRebound | ActionRebinder → PlayerInputInterface, PlayerActionManager, the project's rebind UI |
| out | grabCount | Int | GrabCountChanged | PointOnCollisionTriggerWhenGrab |
| out | leftHandIsPointing / rightHandIsPointing | Bool | HandPointingChanged(hand, bool) | PointOnCollisionTriggerWhenGrab → PointingPoseManager |
| out | actionMade | Transform | ActionPerformed | PerformAction (world buttons; filter on the transform) |
| out | snapBegun / snapEnded | GameObject | SnapBegun / SnapEnded | SnapObject (payload = the object) |
| in | sceneIsLoading | Bool | SceneLoadingChanged | LocomotionManager, PlayerMovement, NoPeeking, FootstepAudio |
| in | sitRequest | GameObject | SitRequested | SitController |
| in | inputFieldFocused | Bool | InputFieldFocusChanged | LocomotionManager, PrimaryItemController (legacy "loginFieldIsOpened") |
| in | gloveState | Bool | GloveStateChanged | HandsAppearanceManager |
| in | primaryItemDrawWithHand | String | PrimaryItemHandRequested | GetPrimaryInHandItemWithVRController (legacy "PrimaryItemStateWithUsedHandChannel") |
| in | hapticFeedback | String | HapticRequested | HandVibration ("Left" / "Right") |
| in | loadingStatus / loadingProgress | String / Float | LoadingStatusChanged / LoadingProgressChanged | ScreenspaceHud (SceneManagement's `LoadingInformation` broadcasts; it sits on the project's loading setup, e.g. the AdditiveLoading sample prefab, NOT on the player) |
| in | roomId | Int | RoomIdChanged | TakeObject |
| in | rebindRequested | ActionRebind | RebindRequested | ActionRebinder |
| both | cameraReset | Void | CameraResetRequested | FPSCameraMovement, TeleportOnEvent |
| both | mainMenuState | Bool | MenuStateChanged | MainMenuController, cursor, locomotion, FadeMask |
| both | mouselookState | Bool | MouselookStateChanged | CursorStateController → FPSCameraMovement |
| both | pause | Bool | PauseRequested | MainMenuController, BatteryWarningSystem → PlayerMovement, FootstepAudio |
| both | primaryItemState | Bool | PrimaryItemStateChanged | PrimaryItemController, PrimaryItemBehaviour, CursorStateController, ActionRebinder |
| both | playerTeleport / objectTeleport | Teleport | TeleportRequested | SendTeleportTarget → TeleportOnEvent (the bridge mirrors requests both ways) |

Internal-only events (no slot): PlayerTeleported / ObjectTeleported (the move HAPPENED,
raised by TeleportOnEvent), ScreenFadeChanged, InvalidActionSignaled, PlayerJumped /
PlayerLanded / CrouchStateChanged, HandGrabStateChanged, HandInteractorReported,
HandEnteredDetectionZone / HandLeftDetectionZone. Ask for a slot if a project needs one.

## Projects

PROJECTS MUST USE A LOCAL COPY of the channels asset: the packaged
`UniversalPlayerChannels` is immutable in consumer projects (lives under Packages/) and
package updates overwrite it. `Tools/Jeanf/UniversalPlayer/Create Local Player Channels`
duplicates it into Assets/ and assigns it to the bridge in the open scene (apply the
override to the Player variant afterwards). ValidateSetup's "player event bridge" check
reports a missing bridge/asset, a bridge still on the packaged asset, and empty slots the
player itself depends on (`ProjectSetupChecks.RequiredChannelSlots`); every other empty
slot is a feature the project does not use.

The package OWNS one channel asset per slot, all in `Runtime/Channels/` and named after the
slot (`PackagedChannelAssets_AllLiveInRuntimeChannels` enforces both the folder and that
every packaged slot points there). The historic assets were MOVED here with their guids
(19 from EventSystem's `Samples/PlayerChannels`, `playerIsMoving` and `hapticFeedback` from
the dev project's `Resources/Player/Channels`), so every existing reference — a project's
local hub copy, scene listeners — keeps resolving. Only `objectTaken`, `actionMade`,
`snapBegun`, `snapEnded` and `roomId` are new assets (nothing existed for them).

PUBLISH TOGETHER: EventSystem (samples without those 19 assets) and UniversalPlayer 1.19.0
must land in a consumer at the same time — an older EventSystem next to the new player
would carry the same guids twice, and Unity keeps only one of the duplicates.

## Migration from 1.18.x (per-component channel fields removed)

Prefab-variant overrides of the removed fields are dead (ValidateSetup lists orphaned
overrides). Re-point the project's copy of the channels asset instead:

- `PrimaryItemController.loginFieldIsOpened`, `LocomotionManager.isInputFieldFocused` → `inputFieldFocused`
- `LocomotionManager.isLoadingScene` → `sceneIsLoading` (already a slot)
- `PrimaryItemController / CursorStateController / GetPrimaryInHandItemWithVRController / PrimaryItemBehaviour` primary item channels → `primaryItemState`, `primaryItemStateVr`, `primaryItemDrawWithHand`
- `MainMenuController.mainMenuStateChannel / GeneralPauseEventChannel` → `mainMenuState` / `pause` (already slots)
- `HandsAppearanceManager.gloveStateChannel` → `gloveState`
- `HandVibration.hapticFeedbackOnSpecificHandSO` → `hapticFeedback`
- `ScreenspaceHud.loadingStatusChannel / loadingProgressChannel` → `loadingStatus` / `loadingProgress`.
  SceneManagement's `LoadingInformation` LEFT the Player prefab (it was the last component
  holding channel fields there): keep one on your loading setup — the AdditiveLoading sample
  prefab ships it on its root — broadcasting on the same two assets the hub names.
- `TakeObject.objectTakenChannel / objectDropped / roomIdChannelSO` → `objectTaken` / `objectDropped` / `roomId`
  (`snapEventChannelSO` was never read and is gone)
- `ActionRebinder`, `PlayerInputInterface`, `PlayerActionManager` rebind channels → `rebindRequested` / `actionRebound`
- `PointOnCollisionTriggerWhenGrab` grab/pointing channels → `grabCount`, `leftHandIsPointing`, `rightHandIsPointing`
  (the hand-side grab/detection channels are internal now: HandPoseManager, HandDetectionZoneReporter)
- `PerformAction.actionMade` → the single `actionMade` slot; the payload is the hit transform — a
  project that used one channel per button type now filters on the transform.
- `SnapObject.snapBegun / snapEnded` → `snapBegun` / `snapEnded`, now GameObject channels (payload =
  the snapping object) instead of one Void channel per object.
- `SendTeleportTarget._teleportChannel` and `TeleportOnEvent`'s channel + `OnEventRaised`: gone.
  Requests travel over `PlayerEvents.TeleportRequested`; the bridge mirrors them on
  `playerTeleport` / `objectTeleport` for project listeners, and a project can still raise those
  channels to teleport. A `TeleportOnEvent` only needs its Player (ValidateSetup + Fix assign it).
- `XRBaseInteractorSender / XRBaseInteractorListener`: the channel became a `hand` side; the
  `XRBaseInteractorEventChannelSO` type and its two assets are deleted.
- `PlayerInputEventManager` (the channel-asset factory) is deleted.
- The EventSystem `VoidEventSender` / `BoolEventSender` / `VoidEventListener` components left the
  Player, hand and HandDetector prefabs (HandPoseManager reports grabs itself;
  HandDetectionZoneReporter reports zone enter/exit; SetPoseOnTrigger follows the tablet hand).

Behaviour note: `SetPoseOnTrigger` zones with "Is Using Grab Check" now really follow which hand
holds the tablet (the old listeners raised "no hand is grabbing" right after "left/right hand is
grabbing", so the gate was always open).
