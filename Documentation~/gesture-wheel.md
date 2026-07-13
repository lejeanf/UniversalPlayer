# Gesture Wheel — design

A runtime way for the player to *make a hand gesture on demand*: hold a button, a
radial wheel of gestures appears, the thumbstick (or mouse) selects one with a live
preview, releasing commits it. The committed gesture plays for a couple of seconds
and then the hand returns to normal.

Status: **design only — not implemented.** This document is the plan.

## Decisions (locked with the user)

| Decision | Choice |
|---|---|
| Navigation | **Radial + scroll pages** — 8 slots in a ring, flick to page through more |
| Platforms in v1 | **All three** — VR, gamepad, mouse & keyboard |
| Commit lifetime | **Momentary** — plays ~2.5 s (per-gesture), then auto-clears |

## Guiding principle

Same as the rest of the package: **simple setup, seamless across MKB / gamepad / VR.**
Gestures are authored once (Pose Editor → Pose assets), collected into one
ScriptableObject, and the wheel is the single runtime consumer. Zero per-scene
wiring; a project supplies its own gesture set and nothing else.

## Data model

```
[Serializable] GestureDefinition
    Pose            pose            // authored in the Pose Editor
    string          label           // "Thumbs up"
    Sprite          icon            // REQUIRED for flat modes (no hand on screen)
    float           holdSeconds     // 0 = use the set default (momentary lifetime)
    VoidEventChannelSO onPerformed   // optional: gameplay/NPC/networking hook (#18)

GestureSetSO : ScriptableObject
    List<GestureDefinition> gestures   // ordered; paged 8 at a time
    float                   defaultHoldSeconds = 2.5
```

Ship a **default GestureSetSO** built from the poses being authored now (Open, Fist,
Point, Thumbs up, Rock, I-love-you, …). Projects duplicate and edit it — hospital
gestures (wave, "stop", "come here", "one moment") drop in without code.

## Input abstraction (one path, three bindings)

All resolve-by-name on the shared action asset (`FPS/…`), the package's zero-wiring
pattern. The controller reads an **abstract** gesture-input struct so the three modes
share one state machine:

| Abstract input | VR | Gamepad | Mouse & Keyboard |
|---|---|---|---|
| Open wheel (hold) | `secondaryButton` (B/Y — free today) | `buttonEast` (B — free) | hold `G` |
| Which hand | the controller that opened it | right hand (default) | right hand |
| Point (Vector2) | that hand's `primary2DAxis` | right stick | mouse delta from wheel center |
| Page (±) | stick flick up/down (past a re-arm deadzone) | shoulder buttons `LB`/`RB` | scroll wheel |
| Commit | release the open button on a slot | release `B` on a slot | release `G` / left-click |
| Cancel | release with stick centered | release centered | `Esc` / release centered |

While the wheel is open, the stick's normal jobs (look / stick-teleport) are
suspended for that hand — the controller sets a "wheel open" flag the other systems
already know how to yield to (same pattern as menu/pause gating).

## Runtime pieces

- **`GestureWheelController`** (Player root). Owns the state machine: closed →
  opening → selecting(page, slot) → commit/cancel. Reads the abstract input, drives
  the preview and the UI, runs the momentary timer.
- **Per-mode input adapters** behind one interface (`IGestureWheelInput`) — keeps VR
  `primary2DAxis` vs mouse-delta vs stick out of the controller's core logic and
  makes each mode unit-testable with a fake input (the `Func<>` probe-seam pattern
  used across the package).
- **`GestureWheelUI`** — two presenters behind one interface:
  - **VR**: world-space canvas anchored ~15 cm above the active controller, ring of
    8 slots, highlight on the aimed slot, page dots.
  - **Flat (gamepad/MKB)**: screen-space radial overlay, same ring, driven by
    stick/mouse. Icons carry the meaning here since there's no hand preview.

## Preview & commit lifecycle

Uses the **pose-hold API that already exists** on `HandPoseManager`
(`AcquirePoseHold` / `ApplyPose` / `ReleasePoseHold`), so `ControllerHandPoseDriver`
(grip/trigger) yields cleanly while the wheel drives the pose.

```
Open      → AcquirePoseHold() on the active hand
Selecting → ApplyPose(previewed) every time the aimed slot changes   // live previz
Commit    → keep the hold, raise onPerformed, start holdSeconds timer
Timer end → ReleasePoseHold()  → the driver/grip pose resumes
Cancel    → ReleasePoseHold() immediately
```

The preview *is* the committed pose (not a ghost), so in VR what you see is exactly
what you get. In flat modes the same pose plays on the **first-person body** hand
(see open problem below) and/or is sent to the networked avatar via `onPerformed`.

## Cross-mode rendering & the body problem

- **VR** is the clean case: hands exist, preview and commit both show on the real hand.
- **Gamepad / MKB**: no hand in the player's own view. Options for what the *local*
  player sees: (a) the screen-space wheel + icon is enough feedback and the gesture is
  primarily an outward/networked signal; (b) play it on the first-person body's hand.
  The current `FirstPersonBody` is a **placeholder mannequin without articulated
  fingers**, so (b) needs either finger bones on the body or a project-supplied
  humanoid. **Recommendation for v1:** wheel + icon feedback locally, gesture applied
  to the body's hands only when the body rig actually has them (guarded, warns once) —
  mirrors how the body driver already degrades.

## Events / networking hook (folds in task #18)

`onPerformed` per gesture is the seam for everything downstream: an NPC reacting to a
thumbs-up, a networked avatar replicating the gesture, an analytics ping. This is
exactly task #18 (hand-tracking gestures → event channels) reached from the deliberate
side instead of recognition — worth merging the two.

## Open problems / risks

1. **First-person body fingers** — placeholder mannequin can't show finger gestures
   locally in flat modes. Scope v1 to VR-hand preview + icon feedback; body fingers
   when the rig supports them.
2. **Stick contention** — the wheel borrows the stick from look/teleport; the
   open-flag gate must be airtight or the camera drifts while selecting (we have the
   menu/pause gate pattern to copy).
3. **Icon authoring** — flat modes need a Sprite per gesture. Could auto-render pose
   thumbnails from the Pose Editor later; hand-assigned icons for v1.
4. **Page re-arm on stick** — flicking pages with the same stick that selects needs a
   deadzone-return before the next page flick (the stick-teleport code already solves
   this shape).

## Phased build plan

1. **Data + default set** — `GestureDefinition`, `GestureSetSO`, a starter asset from
   the authored poses. (Small, unblocks everything.)
2. **Controller core + VR** — state machine, VR input adapter, pose-hold preview,
   momentary timer, world-space wheel UI. The headline experience.
3. **Flat-mode adapters + screen-space UI** — gamepad and MKB input, radial overlay.
4. **Events + body** — `onPerformed` channels wired; body-hand playback where a
   finger rig exists.
5. **Tests** — state machine with a fake input per mode (open→page→select→commit /
   cancel), momentary release, pose-hold acquire/release balance, event raised once.

## Reused infrastructure (little new plumbing)

Pose assets & the Pose Editor · `HandPoseManager` pose-hold refcount ·
`ControllerHandPoseDriver` yielding · resolve-by-name input · PlayerEvents / channel
SOs · the menu/pause open-flag gate · the stick deadzone-rearm from stick-teleport.
