# Design: SO-based action → animation binding (Mouse & Keyboard mode)

Status: **proposal, not implemented** (2026-07-10).

## Problem

In M&K mode the player performs contextual actions (take an object, flip a light
switch, wash hands, ...) usually bound to the same physical key (e.g. `E`). The
package should play a context-appropriate first-person animation for each, and:

- the system must know the **interaction type**, not the raw key — `E` on a
  grabbable plays "take", `E` on a switch plays "press";
- bindings must be **per-project assets**, never baked into the package;
- there is no M&K arm rig yet — the VR hands (with their Animator) act as the
  placeholder rig;
- projects must be able to add new action types **without touching package code**.

## Proposed architecture

Three ScriptableObject types (package code, project assets), one runtime driver,
one validator extension.

### 1. `InteractionTypeSO` — the identity of "what kind of action is this"

An empty, named ScriptableObject (plus a description field). Projects author one
asset per action kind: `Take`, `ToggleSwitch`, `WashHands`, `PushButton`, ...
The package ships the *type* and may ship a couple of common instances under
`Runtime/Interaction/CommonTypes/` for convenience; projects can ignore them.

Using an SO (not an enum, not a string) means: no package recompile to add
types, asset references survive renames, and the inspector offers pickers.

### 2. `InteractionTypeTag` — marking scene objects

`MonoBehaviour { public InteractionTypeSO type; }` placed on the interactable
(next to `PoseContainer`, which plays the same role for VR grab poses — the
designs mirror each other on purpose). `PerformAction` already raycasts the
object under the crosshair and raises `actionMade(Transform)`; the tag is how
the transform resolves to a type.

### 3. `ActionAnimationBindingsSO` — the per-project binding table

```
ActionAnimationBindingsSO : ScriptableObject
  entries: List<Entry>
    Entry:
      InteractionTypeSO   type
      string              animatorStateName   // state on the actions layer
      float               crossFadeSeconds    // default 0.1
      Pose                handPoseOverride    // optional, reuses the VR pose system
      VoidEventChannelSO  startedChannel      // optional gameplay hooks
      VoidEventChannelSO  finishedChannel     // optional
```

One asset per project, assigned on the **Player prefab variant** (variant
workflow as everywhere else). Animations themselves come from a per-project
`AnimatorOverrideController` extending the package's base controller, so the
package defines the state graph ("Actions" layer with named states) and
projects swap the clips — the same split as bindings vs. types.

### 4. `MnkActionAnimator` — the runtime driver (on the Player)

- Listens on the existing `actionMade` TransformEventChannel from `PerformAction`
  (no new input path; M&K-only by checking `BroadcastControlsStatus.controlScheme`).
- Resolves `hit.GetComponentInParent<InteractionTypeTag>()` → looks up the entry
  in the bindings asset.
- Plays `animator.CrossFade(entry.animatorStateName, entry.crossFadeSeconds)` on
  the arms rig (today: the VR hands' Animator), applies `handPoseOverride` if
  set, raises `startedChannel`; raises `finishedChannel` when the state exits
  (state machine behaviour or normalizedTime polling).
- **Loud on misses** (house rule): a tagged object with no binding entry, or a
  binding whose state doesn't exist on the animator, logs a one-shot warning
  naming the type/state and the bindings asset.

### 5. Validation (extends `Tools/UniversalPlayer/ValidateSetup`)

- every `InteractionTypeTag` in the open scene has an entry in the player's
  bindings asset (Warning: "E will do nothing visible on '<object>'");
- every entry's `animatorStateName` exists on the player's animator
  (`Animator.HasState`) — catches clip/controller renames;
- bindings asset assigned on the Player variant when any tag exists in the scene.

### Testability (no headset, no hand-authored scenes)

- EditMode: bindings asset round-trip, validator checks above.
- PlayMode: fake `actionMade` raise + assert `CrossFade` target state becomes
  active on a code-built Animator; miss-path warnings fire once.

## Open questions for review

1. Should `InteractionTypeTag` merge into `PoseContainer` (one "interaction
   metadata" component) or stay separate? Separate is proposed: VR grab pose and
   M&K animation are independent concerns and either can exist alone.
2. `finishedChannel` timing: state machine behaviour (precise, more moving
   parts) vs. duration polling (simple, good enough for UI/gameplay gating)?
3. Does "take object" also need the object to animate/parent (like
   `GetPrimaryInHandItemWithVRController` does in VR), or is that out of scope
   for v1 (animation only)?
