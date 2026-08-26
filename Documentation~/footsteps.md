# Footsteps & friction audio (`FootstepAudio`)

Surface-aware footstep and shoe-friction sounds, driven directly off `PlayerMovement`
state. Lives on the Player prefab (`Move/FootstepAudio`) with two child `AudioSource`s
(`FootstepSource`, `ScuffSource`).

## Why it lives in this package

The previous footstep system was a project-side component (AudioSystems'
`FootstepController`) that listened to the `playerIsMoving` bool channel. That design
broke twice over:

- **Wiring**: the player prefab could not ship the component (wrong package), so every
  project had to hand-wire it — and the 1.0 player rewrite shipped without footsteps
  and nobody noticed until playtesting.
- **Signal**: a bool "is moving" cannot express cadence (sprint vs sneak), landings, or
  friction. All of that state already exists on `PlayerMovement`.

The audio *data* stays project-owned: the component plays plain `AudioResource`s
(an `AudioClip` or an Audio Random Container), so there is no dependency on any audio
package. The AudioSystems package's samples ship ready-made containers
(`ARC_FootstepsOnConcrete`, `ARC_FootstepsOnLinoleum`).

## Footsteps

- **Distance-based stepping**: a step fires every `strideLength` meters actually
  travelled (`ActualPlanarVelocity`, so pushing a wall is silent). Cadence follows
  walk/sprint/crouch speed automatically; sprint also lengthens the stride
  (`sprintStrideMultiplier`), crouch shortens it and plays quieter (`crouchVolume`).
- **Surface resolution**: a short raycast under the feet (`PlayerMovement.FootPosition`)
  reads the ground collider's **tag** and matches it against the `surfaces` profiles
  (first match wins; anything else uses `defaultSurface`). The result is cached for
  `surfaceCacheSeconds`. The ray mask defaults to *every layer the capsule collides
  with* (the physics collision matrix — the same floors the player can stand on), so
  there is nothing to wire; `groundLayerOverride` narrows it if needed.
- **L/R alternation**: each step flips the (3D) footstep source `stepStereoSeparation`
  meters left/right of center.
- **Landings**: touching down faster than `minLandingFallSpeed` plays an immediate step.
- Silent while: paused, menu open, scene loading, seated (`LocomotionLocked`), FreeCam,
  airborne, or below `minStepSpeed`.

## Friction scuffs

When the player fights their own momentum — an abrupt direction reversal or a hard stop
— the shoe bites the floor. The detector measures horizontal acceleration on the
**commanded** velocity (which ramps at `PlayerMovement.speedChangeRate`) and keeps only
the part that opposes or crosses the direction of travel, so accelerating from
standstill never scuffs. Past `scuffAccelThreshold` (default 6 m/s², i.e. "braking or
turning at nearly full rate") and above `scuffMinSpeed`, the surface profile's `scuffs`
resource plays, louder the faster the player was moving. `scuffCooldown` stops chatter.

Desktop modes only: XRI locomotion snaps velocity, so every VR stick reversal would
squeak.

## Validation (every footstep failure mode is silence — so everything is loud)

- **Inspector** (`jeanf.validationTools`): `movement` and `footstepSource` carry
  `[Validation]` attributes (field-level messages when unset), and the component
  implements `IValidatable` — `IsValid` is false while unwired **or wired-but-silent**
  (no footstep resource anywhere), which drives the inspector banner / hierarchy
  markers. The packaged prefab therefore shows invalid on purpose until a variant
  assigns sounds.
- **`Tools/Jeanf/UniversalPlayer/ValidateSetup`** (and the same rules in Unity's
  Project Validation window, `FootstepSetupChecks`):
  - `Scene: footsteps` — component present on the scene player, wiring assigned,
    and at least one footstep resource (wired-but-silent is a **Fail**).
  - `Scene: footstep surfaces` — every surface profile tag exists in the project's
    tag list (an undefined tag can never match a floor → Warning naming the tag),
    and warns when no scuff resource is assigned anywhere (friction layer silent).
- **Runtime**: a one-shot warning fires on the first silent step, naming the fix.

## Project setup

1. The packaged prefab ships the component wired but **silent** — surface profiles
   (`Concrete`, `Linoleum` pre-seeded) have empty resources. Assign footstep and scuff
   resources on your Player **variant** (a one-shot warning fires at runtime if none
   are set).
2. Tag your floor colliders (`Concrete`, `Linoleum`, or add your own profiles).
3. Optional: route the two AudioSources into your project mixer, and give surfaces
   distinct scuff character — a squeak on linoleum, a gritty scrape on concrete or
   gravel.
