# Design: true first-person body (visible torso, legs and feet in M&K mode)

Status: **implemented** (2026-07-10) — `Runtime/scripts/Body/FirstPersonBody.cs`,
wired on the `Body` node of Player.prefab. Question 1 was answered "yes, placeholder":
since shipping a rigged humanoid would cost tens of MB and an art style, the placeholder
is a **procedural primitive mannequin** built at runtime (headless torso + swinging
legs/arms, no colliders, pipeline-appropriate Lit material, walk cadence matched to the
head bob). Assigning `bodyAnimator` on the variant disables the mannequin and switches
the driver to the animator-parameter contract below. Question 2 (hands vs body arms in
M&K) is still open — today the VR hands are hidden in M&K anyway, so the mannequin's
arms and the drawn-item flow coexist; revisit when the action→animation binding lands.

## Problem

In M&K mode the player is a floating camera with (at most) a pair of VR hand
meshes. Looking down shows nothing — no torso, no legs — which breaks the
sense of being rooted in the environment. The goal is a full-body character
model whose head the camera sits in:

- look down → see your own chest, legs, feet;
- the body walks/sprints/crouches in sync with the actual locomotion
  (PlayerMovement already exposes `PlanarVelocity`, `NormalizedSpeed`,
  `IsSprinting`, `CrouchBlend`, `IsGrounded`, `MoveInput` for exactly this).
  The `MoveX`/`MoveY` animator parameters are the planar velocity in the
  body's local space divided by the walk speed (forward walk = (0,1),
  sprint ≈ (0,1.8)) — velocity-based so animation follows momentum.
  `Tools/UniversalPlayer/Setup Template Body` rebuilds the packaged
  controller (2D locomotion blend tree + Airborne + Seated) from the
  Mixamo clip pack and switches all the FBX rigs to Humanoid;
- M&K/gamepad only — in XR the body stays hidden (a mismatched fake body in
  VR is worse than none; XR body tracking is a separate, later concern);
- the package must not force an art style: the **model is a per-project asset
  assigned on the Player variant**, same split as poses/bindings. The package
  may ship a simple neutral placeholder body so the feature works out of the
  box.

## Proposed architecture

### 1. `FirstPersonBody` (runtime driver, package code)

MonoBehaviour on a `Body` child of the Player prefab (sibling of
CameraOffset, so it does NOT inherit the look pitch — the body yaws, the
camera pitches):

- `[SerializeField] Animator bodyAnimator` — the project's rigged body
  (humanoid), assigned on the variant;
- listens to `BroadcastControlsStatus.SendControlScheme`: enables renderers in
  KeyboardMouse/Gamepad, disables them in XR/FreeCam;
- drives animator parameters every frame from PlayerMovement:
  `Speed` (m/s), `NormalizedSpeed`, `MoveX`/`MoveY` (local, from `MoveInput`,
  for strafing blend), `IsSprinting`, `CrouchBlend`, `IsGrounded`;
- yaws the body root toward the camera's flattened forward with a small
  dead-zone + turn speed, so the body doesn't twitch on tiny mouse moves but
  follows real turns (classic TFP trick);
- loud one-shot warning when `bodyAnimator` is set but its controller lacks
  one of the expected parameters (name the parameter and the controller).

### 2. Hiding the head (so it doesn't clip the camera)

The camera sits where the head is. Options, in preference order:

1. **Shadow-only head**: `SkinnedMeshRenderer.shadowCastingMode = ShadowsOnly`
   on a separated head mesh — best result (your shadow still has a head).
   Requires the model to have the head as its own mesh/submesh.
2. **Bone scale zero**: scale the head bone to ~0 in `LateUpdate` (after
   animation). Works on any humanoid rig — this is the default, driven by
   `Animator.GetBoneTransform(HumanBodyBones.Head)`.
3. Per-layer camera culling — rejected: steals a project layer (we already
   burn one on NoPeeking) and kills the shadow.

Default = 2 with 1 available via a checkbox (`headIsSeparateMesh`).

### 3. Animation set (package base controller + project override)

Same split as the action→animation design (`action-animation-binding.md`):
the package ships a base `AnimatorController` with the expected state graph —
a 2D locomotion blend tree (idle/walk/sprint × strafe) + crouch layer blended
by `CrouchBlend` — and projects extend it via `AnimatorOverrideController`
to swap clips. The placeholder body ships with CC-compatible basic clips
(idle, walk, sprint, crouch-idle, crouch-walk); Unity's humanoid retargeting
makes them usable on any humanoid model.

### 4. Camera anchoring

The camera does NOT parent to the head bone (head-bone-driven cameras nauseate
even on flat screens — bob is already handled by FpsCameraFeel, and two bob
sources would fight). CameraOffset stays where it is; instead the body is
positioned so its head lands at CameraOffset (offset field, auto-computed
from the rig's head height when possible, exposed for tweaking). Crouch: the
camera already lowers via PlayerMovement's crouch handling; the body plays the
crouch animation — the `CrouchBlend` drive keeps them in sync.

### 5. Validation (`Tools/UniversalPlayer/ValidateSetup`)

- Player variant has a `FirstPersonBody` with an assigned animator → else
  Warning: "M&K mode shows floating hands only — assign a body model on your
  variant (see true-first-person-body.md)";
- animator controller has the expected parameters (list the missing ones);
- assigned model is humanoid (`Animator.isHuman`) when head-hiding mode 2 is
  selected.

### Testability (no scenes, no art)

- EditMode: parameter-contract check against the packaged base controller
  (every parameter the driver writes exists).
- PlayMode: code-built rig (capsule + Animator with the base controller):
  walk input → `Speed`/`MoveY` parameters change; scheme→XR → renderers off;
  head bone scaled to ~0 after one frame in mode 2; body yaw follows a
  camera turn beyond the dead-zone.

## Open questions for review

1. Should the package ship a placeholder body at all, or is a warning +
   docs enough? (Shipping one costs package size and a rig to maintain, but
   makes the feature demoable in the test bench.)
2. Where do the VR hands go in M&K mode once a body exists — keep the current
   floating-hands rendering for item interactions, or hide them and rely on
   the body's animated arms (+ the action→animation binding system for
   contextual arm moves)?
3. ~~Is gamepad "controller mode" allowed a third-person toggle later, or is
   this strictly first-person?~~ **Answered (2026-07-10): third-person for
   M&K/gamepad is wanted, but later.** Consequence for this design: the body
   driver must not assume the camera is inside the head — head hiding and the
   camera anchor live behind a small `CameraPerspective` seam (first-person =
   hide head + anchor at head height; a future third-person mode reuses the
   same body/animator contract, shows the head, and anchors a boom instead).
   Nothing else changes.
