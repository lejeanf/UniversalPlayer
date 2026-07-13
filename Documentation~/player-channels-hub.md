# Design v2: internal delegates + one channel bridge (single wiring point)

Status: **implemented** (2026-07-11) in 0.10.0 — `Runtime/scripts/Events/`
(PlayerEvents, PlayerChannelsSO, PlayerEventBridge + the packaged
UniversalPlayerChannels asset, wired on the Player prefab root). Migrated:
FPSCameraMovement, PlayerMovement, BroadcastControlsStatus, SitController,
XrHealthMonitor, FallRecovery, HandsDisplayer, NoPeeking (its BoolEventListener
left the prefab), CursorStateController, TeleportOnEvent's camera reset.
MIGRATION NOTE for variants that overrode individual channel fields with custom
assets: those fields no longer exist — duplicate UniversalPlayerChannels, point it
at your assets, assign it on the bridge. ValidateSetup's "player event bridge"
check reports missing bridge/asset/slots.

PROJECTS MUST USE A LOCAL COPY of the channels asset: the packaged
UniversalPlayerChannels is immutable in consumer projects (lives under Packages/)
and package updates overwrite it. `Tools/UniversalPlayer/Create Local Player
Channels` duplicates it into Assets/ and assigns it to the bridge in the open
scene (apply the override to the variant afterwards); ValidateSetup warns whenever
the bridge still points at the packaged asset.

## Principle

SO event channels exist for designer-wireable, cross-scene, cross-assembly
decoupling — that is the PROJECT boundary. Inside the player prefab, components are
compiled together and shipped together: they gain nothing from asset-based wiring
except the wiring mistakes. So:

- **inside the package: C# events** — zero wiring, compile-checked renames, no
  per-frame ScriptableObject indirection (channel raise → canonical resolve →
  diagnostics → invoke becomes a plain delegate call);
- **at the boundary: one bridge** — the only place channel assets are referenced.

Precedent already in the code: `BroadcastControlsStatus.SendControlScheme` and
`XrHealthMonitor.OnHealthEvent` are C# events today.

## Pieces

### 1. `PlayerEvents` — the internal delegate surface

One plain class (owned by the bridge, exposed via `PlayerEventBridge.Events` and a
`static Instance` for scene-side helpers), with one C# event per logical signal:

    event Action<ControlScheme> ControlSchemeChanged;
    event Action<bool>   HmdStateChanged;
    event Action<TeleportInformation> TeleportRequested;   // inbound from scenes too
    event Action         CameraResetRequested;
    event Action<bool>   MouselookStateChanged;
    event Action<bool>   PlayerMovingChanged;
    event Action<bool>   SceneLoadingChanged;              // inbound from world manager
    event Action<bool>   SeatedChanged;
    event Action<string> XrIssueReported;
    event Action<bool>   HmdConnectionChanged;
    event Action<string> FallRecovered;
    event Action<bool>   PauseRequested;                   // battery task #33

Package components subscribe/raise HERE — their per-component channel fields are
removed. Named-method subscribe/unsubscribe only (house rule; the `-= null` sweep
history is why).

### 2. `PlayerChannelsSO` — one asset naming the boundary channels

Unchanged from v1: one field per logical channel, package ships a default asset
pointing at the sample channels; projects duplicate and repoint it once.

### 3. `PlayerEventBridge` — the single wiring point (Player root)

Holds the `PlayerChannelsSO`. Pipes BOTH directions:
- outbound: internal event fires → `channel.RaiseEvent(...)` (skipped when the slot
  is empty — with a validator warning, not silence);
- inbound: `channel.OnEventRaised` → internal event (teleport targets raised by scene
  objects, SceneIsLoading raised by the world manager, pause raised by project UI).

Loop protection: the bridge marks re-entrant forwards so an inbound channel raise
that fires the internal event does not get re-raised outbound onto the same channel.

### 4. Migration & back-compat (the honest part)

Removing per-component channel fields is a BREAKING change for variants that
overrode individual fields with custom assets: those overrides point at fields that
no longer exist. Mitigation:
- the bridge ships wired with the same default channel assets the components used,
  so consumers on the standard channels see zero behavior change;
- variants with custom channels re-wire them ONCE on the bridge asset — which is the
  entire point of the feature;
- ValidateSetup detects orphaned overrides (existing check) and a new check lists
  empty bridge slots naming the features that go quiet;
- version bump: this is a 2.x → next-minor with loud CHANGELOG note, or ride the
  next major — decide at implementation time.

### 5. Validation

- bridge present on the player + asset assigned, else Fail;
- empty channel slots → Warning naming the silent features;
- leftover components with legacy channel fields (from a stale package mix) → Fail.

## Open questions

1. Item-flow channels (PerformAction / primary item) are shared with scene
   interactables — first pass keeps them as-is, second pass decides.
2. Ship the bridge as REQUIRED (validator Fail when missing) or optional-with-fallback?
   Proposal: required — optionality is where wiring bugs live.

## Design rule + channel migration backlog (two phases)

RULE: exactly ONE script — PlayerEventBridge — subscribes to / raises SO event
channels. Everything internal communicates over PlayerEvents C# delegates.
Project-boundary traffic gets a slot on PlayerChannelsSO, forwarded by the
bridge (see sitRequest for the reference implementation).

Audit (2026-07) found violations, to be fixed in two phases:

Phase 1 — internal-only, safe now (no project-facing wiring changes):
- PrimaryItemState web (PrimaryItemController, CursorStateController,
  GetPrimaryInHandItemWithVRController, PrimaryItemBehaviour) -> a
  bidirectional hub slot + PlayerEvents.PrimaryItemStateChanged
- HandsPhysics.controlSchemeChangeEvent -> BroadcastControlsStatus.SendControlScheme
- FPSCameraMovement._canLookStateChannel -> delete (dead)
- Hand-internal channels: _leftGrab/_rightGrab/_noGrab, primaryItemStateWithUsedHand,
  PoseContainer hover, PlayerInputEventManager (6 channels), grab-count/pointing,
  HandVibration haptics -> PlayerEvents delegates

Phase 2 — project-boundary channels -> hub slots (AFTER the UVS 1.0.0 import
validates; changes the wiring surface consumers see):
- LocomotionManager.isInputFieldFocused / isLoadingScene
- PrimaryItemController.loginFieldIsOpened
- HandsAppearanceManager.gloveStateChannel
- TakeObject objectTaken/objectDropped/snap/roomId
- MainMenuController.GeneralPauseEventChannel (menu state already routed)
- PerformAction.actionMade

Fine as-is: SendTeleportTarget (world-side broadcaster), XRBaseInteractor
sender/listener (typed data channel), ActionRebinder set (rebind UI boundary).
