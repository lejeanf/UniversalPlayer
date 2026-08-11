# Sitting on chairs

Implemented 2026-07-10. Works in every mode; one component per chair, zero wiring
for M&K/gamepad, one UnityEvent for VR.

## Setup

1. Add a `Seat` component to the chair (or any sittable thing). Done — the chair
   itself (its colliders) is now sittable in M&K/gamepad mode.
2. Optional tuning on the Seat:
   - `sitAnchor`: where the hips go and which way the player faces (defaults to
     the Seat's own transform — add an empty child on the seat surface for precision);
   - `exitAnchor`: where the player stands when getting up (default: back where
     they sat down from);
   - `eyeHeightAboveSeat`: seated eye height above the anchor (default 0.7).
3. VR: nothing — the Seat adds an `XRSimpleInteractable` and wires its Select to
   sit by itself at startup (the chair needs a collider, which desktop mode
   needs anyway). The interactable is **sit-only**: grabbing (grip) or triggering
   the chair (trigger while the interaction ray hovers it) sits you; standing is
   the LEFT STICK's job (see below). If you prefer your own interactable, add it
   and wire Select to `ToggleSit` in the inspector — the auto-wiring detects that
   and stays out of the way.

## Behavior per mode

- **M&K / gamepad**: aim at the chair and press Interact (the existing `FPS/Interact`
  action) → the player teleports onto the anchor, the camera drops to seated eye
  height, locomotion locks, and the body (placeholder mannequin or your rigged
  Animator via the `IsSeated` parameter) plays the sit pose. Stand up with **Jump**
  (Space / gamepad south) or Interact again — Interact stays usable for things
  around the seat, and move input deliberately does nothing while seated
  (`exitOnMoveInput` restores the old eject-on-move behavior if a project wants it).
- **VR**: sitting teleports the rig and lowers the root so the user's *real*
  head lands at the seat's eye height — deliberately nothing more (no forced camera
  animation in VR). Sit with **grip** (grab the chair) or **trigger** (press it while
  the interaction ray hovers the chair). Stand up by **pushing the LEFT STICK and
  holding it** for `standUpHoldSeconds` (default 0.2 s) — any direction. Two
  debounces keep it deliberate: a stick already held when you sat down doesn't
  count (release it and push again), and a short flick under the hold time does
  nothing. XRI's joystick locomotion providers are disabled while seated, so the
  stick cannot slide a seated player off the chair; the hold that stands you up
  seamlessly becomes walking once you're up.
- **All modes**: the CharacterController is disabled while seated (no gravity/collision
  fighting the chair) and fully restored on exit; the mouselook reset channel is raised
  so the view realigns with the seat's facing.

## Player-side (ships wired on the Player prefab)

`SitController` on the Move object. `Seat.ToggleSit()` complains loudly in the console
if no SitController is alive in the scene (older variant, missing player). The
`seatedStateChannel` (Bool) slot is optional for gameplay hooks on your variant.

## Sit/stand hint tooltip (project-side)

Chairs can show a "how to sit / how to stand" hint with the TooltipSystem package
(`fr.jeanf.tooltipsystem` ≥ 3.1.0), wired in the project: put an
`InteractableTooltipController` on the chair (a `TooltipActionContentSo` gives each
control scheme its own text/icon) and park it above the Seat's sit anchor. To switch
the hint between sit and stand, compare `SitController.Instance.CurrentSeatId`
against the seat's `GetSeatData().SeatId` on `PlayerEvents.SeatedChanged` and call
`InteractableTooltipController.SetActionContent(...)` with the matching content asset.

## Scenario-driven seating (event channel)

Scenarios can seat the player without any input, through a
`GameObjectEventChannelSO` assigned to the SitController's **Sit Request
Channel** (optional field on the Player variant):

- **Raise the Seat's GameObject** → the player sits there (the Seat's anchor
  defines position AND facing).
- **Raise `null`** → the player stands up.
- **While the screen is black** (loading fade) the placement is **instant** —
  no glide, no camera motion. This is the scenario-load flow: load fades to
  black → raise the sit request → loading completes → the reveal shows the
  player already seated in place.
- With the world visible, the same request plays the normal sit/stand glide.
- Raising a different Seat while seated swaps seats (silent instant release,
  then the new sit).

### SitPlayerOnEnable (zero-code scenario placement)

For "this scenario starts with the player seated HERE", drop a
`SitPlayerOnEnable` in the scenario's scene and reference the Seat: the moment
the object is enabled it raises the sit request, **hidden behind a fade to
black** (default on). Screen already black (scenario loading): the player is
seated behind it and the loading flow keeps owning the fade. World visible: the
component **triggers the fade itself**, seats the player once the black covers
the screen, holds `holdBlackSeconds`, then fades back in (and always fades back
in if it gets disabled mid-sequence). Turn `fadeToBlackForPlacement` off to
seat immediately, glide and all. It also waits up to `waitTimeout` for the
Player to exist — additive loading can enable it before the player scene is in.
