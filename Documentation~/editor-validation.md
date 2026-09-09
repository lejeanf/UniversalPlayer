# Editor validation (propertyDrawer toolkit)

Status: **package-wide since 1.18.0**. Every component in `Runtime/` that needs a
reference to do its job says so in the inspector, using the validation tools of
`fr.jeanf.propertydrawer` (namespace `jeanf.validationTools`).

## What the user sees

- An unset required field turns **orange** (translucent wash + help box with the reason).
- The component gets an orange **"⚠ ComponentName needs setup"** banner under its title,
  the GameObject an orange dot in the hierarchy, and the console a one-line report.
  `Tools/Jeanf/propertyDrawer` scans a whole scene.
- Components implementing `IValidatable` that report `IsValid == false` additionally
  **fail the build** (`SceneValidationOnBuild`).

## The rules we apply

A serialized reference gets `[Validation("...")]` when **all** of these hold:

1. it is a Unity object reference, a string, or a list of Unity objects (anything else is
   invisible to the scanner — `ValidationCoverageTests` fails on it);
2. the component dereferences it unguarded, or its core purpose is a silent no-op without it;
3. there is **no automatic fallback** (`GetComponent` on Awake, `Camera.main`, a packaged
   default asset, …) — auto-resolved fields stay unmarked, optionally with a tooltip saying so;
4. the shipped prefabs wire it — a field the packaged `Player.prefab` legitimately leaves
   empty is optional by definition (`ValidationCoverageTests.PackagedPrefabs_HaveNoValidationIssues`).

The message states the consequence, not the type:
`"PlayerInput is required — control-mode detection reads from it. Every binding is dead without it."`

- **Required only in one mode** → `[Validation("...", RequiredIf = nameof(someBool))]`
  (`"!someBool"` inverts). The gate must name a bool member; a typo would make the field
  always required, so `EveryRequiredIfGate_NamesABoolMember` checks it.
- **Rule beyond a null check** (wrong pipeline profile, empty list *and* no fallback) →
  `IValidatable` with a computed `IsValid`. Because it fails builds, it is reserved for a
  setup that cannot work at all (`FadeMask`, `FootstepAudio`, `SnapObject`).
- **Field already carrying `[DrawIf]`** → since propertyDrawer 1.5.0 the two attributes stack:
  whichever drawer Unity picks applies both (hide / grey per DrawIf, orange per Validation).
  The scanner behind the banner does NOT read DrawIf, so always gate the requirement on the
  same condition: `[DrawIf("isUsingFilter", true, ...)]` + `[Validation("...", RequiredIf = nameof(isUsingFilter))]`
  (`EveryValidationOnADrawIfField_IsGatedWithRequiredIf`). `SendTeleportTarget` and
  `PointOnCollisionTriggerWhenGrab` are the examples.

## Custom editors keep the banner

A `[CustomEditor]` replaces the toolkit's fallback inspector, so every custom editor in
`Editor/` starts its `OnInspectorGUI` with `ValidationUi.DrawIssuesBanner(target as Component)`.
The field tint, hierarchy dot and console log need nothing from the editor.

## Deliberately unmarked

- `PlayerChannelsSO` channels — the bridge null-guards every one; projects only wire what they use.
- Vendored XRI sample scripts under `Runtime/xrToolkit/` (sample scenes only).
- Layer masks, UnityEvents, primitives — express those with `IValidatable` or `ValidateSetup`.
