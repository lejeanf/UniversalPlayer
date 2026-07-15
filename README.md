# Universal Player

`fr.jeanf.universal.player` — **one player, every input.** A single player prefab that works in **Mouse & Keyboard**, **Gamepad** and **VR**, switching between them automatically, in both **URP** and **HDRP**.


---

## Philosophy

This package was built for a hospital simulator (the UVS project) where the same scenario must be playable by a student at a desk, on a couch with a controller, or inside a headset — **without authoring the interaction three times**. Everything in the package follows from four principles:

1. **One player, every input.** The player watches all connected devices and switches to whichever one you actually use — no menu, no setup, *"most recent meaningful input wins"*. Using the mouse, keyboard or gamepad switches to desktop; picking up and pressing a VR controller (or donning the headset where the runtime reports presence) switches to VR — with `Ctrl`+`Alt`+`K` / `Ctrl`+`Alt`+`V` as explicit fallbacks for runtimes that don't report headset presence (e.g. some setups over Link).
2. **Feature parity across modes.** Interacting, grabbing, sitting, teleporting, menus — every feature raises the *same events* whatever the input device. If it works in VR it works on desktop, and vice versa. Gameplay code never needs to know which mode is active.
3. **Simple to set up, loud when broken.** One menu click creates a working player for your render pipeline. A validation tool (`Tools/UniversalPlayer/ValidateSetup`) and runtime guards report misconfiguration with actionable messages instead of failing silently.
4. **The project owns the game; the package owns the player.** Communication happens through ScriptableObject event channels and C# delegates (single `PlayerChannels` hub — see `Documentation~/player-channels-hub.md`). The map, the inventory, the menus, the scenario logic all live in *your* project; the player just reports what the user did.

---

## Features

- **Automatic input switching** between Mouse & Keyboard, Gamepad and VR (plus a free-fly debug camera)
- **Locomotion & camera feel**: walk, sprint, crouch, jump, momentum, head bob, turn tilt, landing dip
- **True first-person body**: a visible, animated body (Mixamo locomotion set) with one-click template setup
- **Sitting** on chairs in every mode (`Documentation~/sitting.md`)
- **Teleportation**: XRI-style stick teleport in VR, event-driven teleports (SO channels) everywhere
- **Hands**: tracked hand poses, controller grip/trigger pose ladder, grab highlight + **ghost-hand grab preview**, fingertip UI ray
- **Hand appearance**: swap gender, skin color, add gloves — all through events
- **Pose & gesture authoring tools**: a step-by-step Pose Editor wizard with IK, mirroring, auto-pose and gesture presets (see below)
- **Reticle** (desktop/gamepad): tints over anything usable, fills up for gaze-validated actions, flashes pinkish-red on rejected requests (see [Cursor feedback](#cursor-feedback))
- **Safety nets**: infinite-fall recovery, XR health diagnostics, controller battery warnings + auto-pause

---

## Installation

Add the scoped registry in `Project Settings → Package Manager`:

- Click **+** under *Scoped Registries*
- **Name:** `jeanf`
- **URL:** `https://registry.npmjs.com`
- **Scope:** `fr.jeanf`

Then install **Universal Player** from the Package Manager (*My Registries*). Dependencies (Input System, XR Interaction Toolkit, XR Hands, OpenXR, event system…) are pulled in automatically.

Make sure the **new Input System** is active: `Edit → Project Settings → Player → Configuration → Active Input Handling` → *Input System Package* (or *Both*).

**Requires Unity 2022.3+**, URP or HDRP.

---

## Getting started

1. In your scene's Hierarchy: **Right click → Create Universal Player**. This creates a player for your current rendering pipeline, with the camera bound to everything that needs it.
2. Run **`Tools/UniversalPlayer/ValidateSetup`** — it checks the scene wiring and tells you exactly what to fix if anything is off.
3. Press Play. Move with WASD, plug in a gamepad and touch a stick, or put on a headset — the player follows you.

To integrate with your project, copy the `PlayerChannels` asset locally (the player offers a project-local copy on request) and subscribe to its events — teleports, map/inventory toggles, interactions — from your own systems. This keeps your wiring safe from package updates.

---

## Supported modes

| Mode | How it activates | Notes |
|---|---|---|
| **Mouse & Keyboard** | Any key press, click or mouse movement | First-person, center-screen reticle (tints green over anything usable, fills up for gaze-validated actions) |
| **Gamepad** | Any button/stick input on a connected gamepad | Same first-person experience as Mouse & Keyboard |
| **VR** | Press a VR controller button, or don the headset where the runtime reports presence (`Ctrl`+`Alt`+`V` to force) | XR Interaction Toolkit rig: tracked hands with poses, grab preview (highlight + ghost hand), fingertip UI ray |
| **Free camera** | Toggled from Mouse & Keyboard or Gamepad (see bindings) | Detached fly-through camera for debugging/inspection; toggling again returns to the previous mode |

> **Leaving VR:** any deliberate keyboard, mouse or gamepad input drops back to desktop; `Ctrl`+`Alt`+`K` forces it even when the headset still reports as worn. **Entering VR:** a controller button or `Ctrl`+`Alt`+`V` is the reliable path on runtimes that don't report `userPresence`. A debug HUD (`Ctrl`+`Alt`+`H`) shows the live mode/presence state.

---

## Default bindings (Mouse & Keyboard / Gamepad)

All bindings live in `Runtime/InputActions/UniversalPlayer_InputActions.inputactions` (FPS action map) and can be remapped per project.

| Action | Mouse & Keyboard | Gamepad |
|---|---|---|
| Move | `W` `A` `S` `D` / arrow keys | Left stick |
| Look around | Mouse | Right stick |
| Sprint | `Left Shift` | Left stick press (L3) |
| Crouch (toggle) | `C` | Right stick press (R3) |
| Jump | `Space` | `Y` |
| Interact / take object | `E` / left click | `A` or right trigger |
| Draw primary item | `1` | D-pad up |
| Draw secondary item | `2` | D-pad down |
| Map (raises `toggleMap` channel) | `M` | D-pad left |
| Inventory (raises `toggleInventory` channel) | `I` | D-pad right |
| Main menu | `Esc` | `Start` |
| Pause | `P` | `Select` |
| Toggle free camera | `Ctrl`+`Alt`+`F` | Hold D-pad down + `Y` |
| Force Keyboard & Mouse (leave VR) | `Ctrl`+`Alt`+`K` | — |
| Force VR | `Ctrl`+`Alt`+`V` | Press any controller button |

Map and Inventory only *report the press* on their ScriptableObject event channels (`PlayerChannelsSO.toggleMap` / `toggleInventory`) — the project owns both UIs and their open/close state.

Free camera, while active:

| Action | Mouse & Keyboard | Gamepad |
|---|---|---|
| Move | `W` `A` `S` `D` | Left stick |
| Look around | Mouse (hold right click to rotate) | Right stick |
| Up / down | `E` / `Q` | Right trigger / left trigger |
| Exit | `Ctrl`+`Alt`+`F` | Hold D-pad down + `Y` |

VR uses the standard XR Interaction Toolkit bindings: sticks for locomotion/turning, grip to grab (with highlight + ghost-hand grab preview), trigger to select and click UI, plus gaze-timed validation where the scene uses it. The **menu button** (left controller) opens the main menu, like `Esc`/`Start` on desktop. Item, seat and UI interactions are the same events as on desktop — features behave identically across modes.

Opening the main menu (any mode) pauses the game and fades the world to black; closing it fades back to whatever the world was doing (still loading → stays black, head in wall → desaturated, otherwise clear).

---

## Cursor feedback

The desktop / gamepad reticle is driven by a single **`CursorStateController`** on the
Player's `_settings` object — one place for every cursor color and icon:

| Field | When it shows |
|---|---|
| **Normal / Tablet Cursor Color** | resting color (tablet = while the primary item is drawn) |
| **Normal / Tablet Cursor Sprite** | optional icon overrides (tablet falls back to normal, normal falls back to the authored sprite) |
| **Hover Color** | aiming at anything usable — interactables, seats, pickables, tooltip objects |
| **Click Color** | while the interact/take button is held on something usable |
| **Invalid Cursor Color** (pinkish red) | flashed when a click/interaction is **rejected** |

### Signalling an invalid request

When your game logic rejects a click ("you can't do that here", not enough resources,
locked door, …), flash the reticle its invalid color from **any** script:

```csharp
using jeanf.universalplayer;

// e.g. inside your interaction handler when the request is denied:
PlayerEvents.RaiseInvalidAction();
```

That raises the package's internal `InvalidActionSignaled` delegate; `CursorStateController`
flashes the reticle **Invalid Cursor Color** for *Invalid Flash Seconds* (default 0.35 s),
overriding hover/click/resting, then returns to the correct resting color. No inspector
wiring — it's a static call. Tune the color and duration on the `CursorStateController`.

> VR hides the reticle, so this is M&K / gamepad feedback. A rejected VR poke/ray should
> be signalled on the interactable itself (haptics / sound). If you drive rejections from a
> ScriptableObject "action denied" channel today, call `PlayerEvents.RaiseInvalidAction()`
> from that channel's listener — a dedicated hub slot arrives with the channel-hub phase 2.

---

## Hand pose authoring

Open **`Tools/UniversalPlayer/Pose Editor`**. The window is a step-by-step wizard:

1. **Pose** — select an existing pose asset or create a new one
2. **Target** — is the pose for a **held object** or a bare **hand gesture**?
3. **Object** — assign the object (held-object poses only; the hands auto-spawn around it at correct scale, whatever the object's scale)
4. **Edit** — pose in the Scene view, live-saved into the asset
5. **Save / mirror** — mirror left↔right across a selectable, visualized plane


Editing tools in the Scene view:

- **Joint dots** (deep blue, depth-graded) — click to get a rotation handle; joints only rotate, bone lengths can never change
- **Orange root dot** — move/rotate the whole hand
- **Pink fingertip cubes** — IK targets with a plane-drag move gizmo; the solver is a planar hinge chain with anatomical flexion limits (no twisted or broken fingers)
- **Auto-pose** — curls the fingers until they contact the object *or an already-posed finger*
- **Gesture presets** — one-click fist, point, thumbs up, rock horns, "I love you"… with a flexibility slider driving anatomical coupling (neighbour drag, fingertip lag, convergence)
- **Toggles** for the VR controller model, finger colliders, mirror plane and player-view reference
- A **Reset** footer if things ever get janky


Hands get **per-phalanx box colliders** at runtime (the bind-pose mesh collider is replaced automatically); a button in the editor bakes them into your hand prefabs.

---

## Documentation

Design docs live in `Documentation~/`:

- `player-channels-hub.md` — how the package talks to your project (delegates + one channel bridge)
- `true-first-person-body.md` — the visible body architecture
- `sitting.md` — Seat + SitController across modes
- `action-animation-binding.md` — SO-based action → animation binding (proposal)
- `gesture-wheel.md` — controller gesture radial menu (design, upcoming)

---

## Developing the package

To work on the package itself (instead of consuming it from the registry):

- Add the scoped registry (see *Installation*) and install **Event System** and **Property Drawer** from it
- Clone this repository into your project's `Assets/` folder
- Unity resolves the remaining dependencies from the manifest; if anything is missing, `ValidateSetup` and the EditMode integrity tests will point at it

> ⚠️ Pushing to `main` publishes a signed release to the registry. Work on branches; merge deliberately.

---

## Compatibility

- **Unity 2022.3+**
- **URP** and **HDRP**
- OpenXR (incl. Oculus/Meta) via XR Interaction Toolkit 3.x

---

## License

<img src="https://licensebuttons.net/l/by-nc-sa/3.0/88x31.png"></img>

---

## Credits

- This repo was started during an artist residency called *fantomas* at <a href="https://www.medrar.org/">Medrar</a> in Cairo, October–December 2022, teaching artists to create artworks in VR.
- Since then it is supported by a research group at UQAR university (Canada): **Laboratoire Onirique**, and specifically the project **UVS** (Unité virtuelle de soins / Virtual Health Unit) initiated by Daniel Milhomme and Frédérique Banville.
- [3D] <a href="https://www.linkedin.com/in/jonathan-l%C3%A9pinay/?originalSubdomain=ca">Jonathan Lepinay</a>
- [Code] Nicolas Chouin, <a href="https://github.com/Percevent13">Felix Cotes-Charlebois</a> & <a href="https://jeanfrancoisrobin.art">Jean-François Robin</a>
- Partly inspired by <a href="https://github.com/UnityTechnologies/open-project-1/tree/devlogs/2-scriptable-objects">Unity Open Project 1</a> for the event system.
