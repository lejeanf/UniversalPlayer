# Project Validation integration for Universal Player

Status: **implemented in 1.11.0** (`Editor/Validation/UniversalPlayerProjectValidation.cs`)
Decisions: build targets = Standalone + Android; category = "Universal Player".

## Goal

Surface the Universal Player setup checks in a Project-Validation-style window with
per-issue **Fix** / **Edit** buttons, instead of (only) a console dump from
`Tools/UniversalPlayer/ValidateSetup`.

## Is it possible? Yes — and we don't even need to build the window

The Project Validation window (Project Settings ▸ XR Plug-in Management ▸ Project
Validation) is **not** private to XR Plug-in Management. It lives in
`com.unity.xr.core-utils` (installed: 2.5.3, already a transitive dependency of XRI and
already referenced by `jeanf.universalplayer.editor.asmdef` — only the
`Unity.XR.CoreUtils.Editor` reference needs to be added). Any package can register rules:

```csharp
Unity.XR.CoreUtils.Editor.BuildValidator.AddRules(buildTargetGroup, rules);
```

Registered rules appear in the same list as the OpenXR/XRI rules, grouped under our own
`Category`, with the built-in status icon, **Fix**/**Edit** button, help link, "Show all",
and **Fix All** behavior for free. XRI's own Hands Interaction Demo sample does exactly
this (see `HandsInteractionSampleProjectValidation.cs` in the imported samples) — it's the
supported extension pattern, not a hack.

### Considered alternative: custom `EditorWindow`

A bespoke window (like `DoorSetupValidatorWindow`) would give full layout control and no
dependency on core-utils, but it means ~400 lines of IMGUI to maintain, a second place
for users to look, and no Fix All / build-time hook. Rejected: core-utils is already in
the project, and "one validation window for the whole project" is exactly the UX the
Unity ecosystem converged on.

## The rule API (core-utils 2.5.3)

`BuildValidationRule` fields we will use:

| Field | Use |
|---|---|
| `Category` | `"Universal Player"` — our group header in the window |
| `Message` | The check name + failure description (shown in the list) |
| `CheckPredicate` | `() => bool` — `true` = pass. Re-evaluated on window focus/refresh |
| `Error` | `true` for our `Severity.Fail`, `false` for `Severity.Warning` |
| `FixIt` | The fix action (auto-fix, or open the right settings page) |
| `FixItAutomatic` | `true` → "Fix" button (works with Fix All); `false` → "Edit" button |
| `FixItMessage` | Tooltip = our existing `Hint` text |
| `SceneOnlyValidation` | `true` for the open-scene checks (excluded from build-time validation) |
| `IsRuleEnabled` | Gate scene/hand rules to "a player exists in the open scene" so the window isn't red in unrelated scenes |

Registration happens in a `[InitializeOnLoadMethod]` for every relevant
`BuildTargetGroup` (Standalone + Android, matching what the player supports).

## Architecture: one source of truth for checks

Today's pipeline (kept, still used by tests, CI and the console menu item):

```
SetupValidator.ValidateSetup()          — MenuItem, console output
 ├─ RunProjectConfigChecks()            — input system, RP, XR mgmt, OpenXR profiles, run-in-background, diffusion profile
 ├─ ProjectSetupChecks.RunAssetChecks() — player prefab variant, orphaned overrides, stale samples
 ├─ ProjectSetupChecks.RunOpenSceneChecks() — player / NoPeeking / teleport listener / XR health monitor / seating
 └─ HandSetupChecks.RunHandChecks()     — hand visibility, authority, pose managers
```

The mismatch to solve: our checks are **batch functions returning `List<CheckResult>`**,
while the window wants **one predicate per rule**. Bridging plan (per the
"fewer classes" rule, no new abstraction layer — one new file total):

1. **Split where it's one-check-one-result already** (most of them): expose the private
   per-check methods as `internal static CheckResult CheckX()` and wrap each in a rule:
   `CheckPredicate = () => CheckX().Severity == Severity.Pass`.
2. **Aggregate where the count is dynamic** (variant overrides: one result per player
   variant found): a single rule "All player variants free of orphaned overrides" whose
   predicate runs the sub-checks and passes only if all pass. Details stay in the console
   validator; the rule's `FixIt` runs the existing `VariantOverrideFixer`.
3. **Multi-result generators** (`CheckXrManagement` yields settings/provider/init-on-start):
   split into three single-result methods; the batch function keeps its signature by
   calling them, so `SetupValidatorTests` keep passing unchanged.

`CheckResult` itself is unchanged — no fix delegate added to it. Fix actions live only in
the rule declarations (they are editor-window concerns, not check concerns).

## Rules and their Fix behavior

| Rule | Severity | Fix button |
|---|---|---|
| Input System active | Fail | Edit → opens Player settings (`SettingsService.OpenProjectSettings("Project/Player")`) |
| URP/HDRP pipeline assigned | Fail | Edit → Graphics settings |
| XR settings exist for target | Fail | Edit → XR Plug-in Management |
| An XR provider is enabled | Fail | Edit → XR Plug-in Management |
| Initialize XR on Startup | Warning | **Fix** (auto): set `InitManagerOnStart = true` |
| OpenXR interaction profile enabled | Fail | Edit → OpenXR settings page |
| Run In Background | Warning | **Fix** (auto): `PlayerSettings.runInBackground = true` |
| HDRP diffusion profile registered | Fail/Warn | **Fix** (auto): reuse `DiffusionProfileRegistration`'s existing registration code |
| Player prefab variant valid | Fail | Edit → pings the prefab asset (`OnClick` select) |
| No orphaned variant overrides | Fail | **Fix** (auto): run `VariantOverrideFixer` |
| No stale imported samples | Warning | Edit → pings the stale folder |
| Scene: NoPeeking present & wired | Fail/Warn | Edit → pings the player root in the scene |
| Scene: teleport listener present | Warning | Edit → ping |
| Scene: XR health monitor present | Warning | Edit → ping |
| Scene: seating scenario sane | Warning | Edit → ping |
| Scene: hands visible + authority + pose managers | Fail | Edit → pings the offending hand object |

(Scene rules: `SceneOnlyValidation = true`, `IsRuleEnabled` = player found in open scene.)

Auto-fixes are limited to settings we already know the correct value for; anything
requiring judgment stays an Edit/ping action — same philosophy as the existing hints.

## Files

| File | Change |
|---|---|
| `Editor/Validation/UniversalPlayerProjectValidation.cs` | **new** — rule declarations + `[InitializeOnLoadMethod]` registration (the only new file) |
| `Editor/Validation/SetupValidator.cs` | split `CheckXrManagement` into 3 methods; widen per-check methods to `internal` |
| `Editor/Validation/ProjectSetupChecks.cs` / `HandSetupChecks.cs` | widen per-check methods to `internal`; no behavior change |
| `Editor/jeanf.universalplayer.editor.asmdef` | add `Unity.XR.CoreUtils.Editor` reference |
| `Tests/Editor/ProjectValidationRuleTests.cs` | **new** — rules registered, categories/severities correct, predicates agree with `CheckResult` severities |
| `package.json` / `CHANGELOG.md` | bump to 1.11.0 (required for registry publish) |

`Tools/UniversalPlayer/ValidateSetup` stays as-is (tests/CI use it); add a sibling
`Tools/UniversalPlayer/Project Validation` menu item that opens the window
(`SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Project Validation")`).

## Open questions

1. **Build targets to register**: Standalone + Android, or Standalone only? (Rules are
   per-`BuildTargetGroup`; the window shows tabs per target, as in the screenshot.)
2. **Category name**: `"Universal Player"` (proposed) vs `"UVS"` if other fr.jeanf
   packages (Doors, SceneManagement, Tooltip) should later join under one banner.
3. Should the door/navigation/static-collider validators migrate into this window later?
   The same pattern applies package-by-package; out of scope here.
