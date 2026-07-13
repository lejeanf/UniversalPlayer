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
   `ToggleSit()` by itself at startup (the chair needs a collider, which desktop mode
   needs anyway). If you prefer your own interactable, add it and wire Select to
   `ToggleSit` in the inspector — the auto-wiring detects that and stays out of the
   way; `autoConfigureXrInteractable` turns the behavior off entirely.

## Behavior per mode

- **M&K / gamepad**: aim at the chair and press Interact (the existing `FPS/Interact`
  action) → the player teleports onto the anchor, the camera drops to seated eye
  height, locomotion locks, and the body (placeholder mannequin or your rigged
  Animator via the `IsSeated` parameter) plays the sit pose. Stand up with **Jump**
  (Space / gamepad south) or Interact again — Interact stays usable for things
  around the seat, and move input deliberately does nothing while seated
  (`exitOnMoveInput` restores the old eject-on-move behavior if a project wants it).
- **VR**: `ToggleSit()` teleports the rig and lowers the root so the user's *real*
  head lands at the seat's eye height — deliberately nothing more (no forced camera
  animation in VR). Toggle again (Select on the chair) to restore the exact pre-sit
  position and height. XRI's joystick locomotion providers are disabled while
  seated, so the stick cannot slide a seated player off the chair.
- **All modes**: the CharacterController is disabled while seated (no gravity/collision
  fighting the chair) and fully restored on exit; the mouselook reset channel is raised
  so the view realigns with the seat's facing.

## Player-side (ships wired on the Player prefab)

`SitController` on the Move object. `Seat.ToggleSit()` complains loudly in the console
if no SitController is alive in the scene (older variant, missing player). The
`seatedStateChannel` (Bool) slot is optional for gameplay hooks on your variant.

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
