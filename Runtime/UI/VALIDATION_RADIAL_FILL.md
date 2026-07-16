# Cursor validation feedback — radial fill (TODO)

The last piece of the screen-space HUD migration. Everything it builds on is already in
place and compiling; this is the feature that finally retires `validationFeedbackImage`.

---

## 1. What it must do

For actions that require **looking at something for a duration** (dwell / hold-to-confirm):

1. **Fill the cursor's ring radially, clockwise**, from 12 o'clock, as the dwell progresses
   (0 → 1).
2. **Auto-unfill** if the dwell is interrupted before the threshold — it drains back to 0
   on its own, no caller bookkeeping.
3. **On completion** (progress reaches 1):
   - the cursor **grows ~25% briefly** (a burst) to mark the validation, then settles;
   - during that burst the colour is modulated to the **Click colour**
     (`CursorStateController.ClickColor`).

Design constraint: **pure styling, no sprite** — the whole point of the UI Toolkit cursor
(sharper, one less image).

---

## 2. Why `Painter2D`

UI Toolkit has no "fill a border radially" style. The clean way is a **custom
`VisualElement`** that draws the arc itself in `generateVisualContent` using
[`Painter2D`](https://docs.unity3d.com/Manual/UIE-vector-api.html) (`painter.Arc(...)`).

- Vector-drawn ⇒ sharp at any scale, no texture, no mask hacks.
- The existing ring can stay a plain CSS `border` (it already is); the arc is drawn **on
  top** of it, so the "unfilled" ring remains visible underneath.

This is why the UXML needs a change — `#Cursor` becomes a custom control rather than a bare
`VisualElement`.

---

## 3. What already exists (don't rebuild it)

| Piece | Where | Notes |
|---|---|---|
| HUD owner + element queries | `Runtime/scripts/Gaze/ScreenspaceHud.cs` | lazy, self-healing query; `PickingMode.Ignore` throughout |
| Cursor render API | `ScreenspaceHud.ApplyCursor(visible, color, fill, screenPosition, scale)` | `fill` here is the **tablet background fill** (0..1) — *not* the validation arc |
| Cursor state authority | `Runtime/scripts/Gaze/CursorStateController.cs` | `_resolvedColor`, `_cursorVisible`, `SetResolvedColor()`, `ClickColor`, `RestingColor` |
| Smooth default↔tablet easing | `CursorStateController.PushToHud` | one curve: `cursorScaleLerpSeconds` drives size + colour + fill |
| Legacy image | `cursorImage` / `validationFeedbackImage` | now **optional** everywhere; guarded; safe to leave empty |
| UXML | `Runtime/UI/ScreenspaceUI.uxml` | `#Root > #Cursor`, `#Loading > #Information`, `#Progress` |

`validationFeedbackImage` (SVGImage) is the thing this feature **replaces**. Once the arc
works, delete the field and the old canvas.

---

## 4. Implementation plan

### 4.1 Custom control
Add e.g. `Runtime/scripts/Gaze/CursorRingElement.cs`:

```csharp
[UxmlElement]                       // Unity 6 API (project is on 6000.3)
public partial class CursorRingElement : VisualElement
{
    private float _progress;        // 0..1 validation arc
    public float Progress
    {
        get => _progress;
        set { if (Mathf.Approximately(_progress, value)) return; _progress = value; MarkDirtyRepaint(); }
    }
    public Color ArcColor { get; set; }
    public float ArcWidth { get; set; } = 2f;

    public CursorRingElement() => generateVisualContent += OnGenerateVisualContent;

    private void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        if (_progress <= 0f) return;
        var p = ctx.painter2D;
        var r = contentRect;
        var center = r.center;
        var radius = Mathf.Min(r.width, r.height) * 0.5f;
        p.lineWidth = ArcWidth;
        p.strokeColor = ArcColor;
        p.BeginPath();
        // 12 o'clock = -90deg; clockwise ⇒ sweep positive in UI Toolkit's y-down space.
        p.Arc(center, radius, -90f, -90f + 360f * Mathf.Clamp01(_progress));
        p.Stroke();
    }
}
```

⚠️ Verify the sweep direction on screen — UI Toolkit's y-axis points **down**, so
"clockwise" may need the angle signs flipped. Check visually before trusting the math.

### 4.2 UXML
Swap `#Cursor` from `<ui:VisualElement …>` to the custom control, keeping its current
inline style (border colour/width, 20×20, radius 20px). The border stays the ring; the arc
draws over it.

### 4.3 HUD API
Add to `ScreenspaceHud`:

```csharp
public void ApplyValidation(float progress01, Color arcColor);
```
which forwards to the `CursorRingElement`. Keep it **separate** from `ApplyCursor` — the
tablet `fill` and the validation `progress` are different concerns.

### 4.4 State + burst (in `CursorStateController`)
It already owns cursor state and the easing, so it should own this too:

```csharp
/// Drive from a dwell/look-at system. Stop calling it and the arc drains on its own.
public void SetValidationProgress(float progress01);
```

- Keep `_validationTarget` (what the caller pushed **this frame**) and `_validationDisplayed`
  (eased). If nothing pushed this frame → target 0 ⇒ **auto-unfill** falls out for free.
- Separate fill/unfill speeds (unfill should feel a touch faster).
- **Completion:** when displayed progress crosses 1 → start a burst timer:
  - scale × `1.25` eased in then out over ~`0.15s` (multiply into the existing
    `_baseScale * _pulseScale` product in `PushToHud`);
  - colour pushed to `ClickColor` for the burst duration (route via `SetResolvedColor`, and
    make sure `ReticleHoverFeedback` doesn't stomp it — it re-asserts colour **every frame**,
    so the burst needs priority, like `IsFlashingInvalid` already has).

⚠️ **`ReticleHoverFeedback.Apply()` re-asserts the colour every frame** and already yields to
`IsFlashingInvalid`. The burst needs the same treatment — add an `IsBursting`-style guard, or
it will be overwritten instantly.

---

## 5. Open questions

1. **Who drives the dwell?** Nothing in UniversalPlayer produces look-at-duration progress
   today. Options: a public API call from uvs, or a `FloatEventChannelSO` (consistent with
   the loading-progress decoupling). **Channel is probably right** — keeps uvs from
   referencing the player package.
2. **Burst on completion: automatic or explicit?** Auto (progress ≥ 1) is simplest, but a
   caller may want to validate *without* the burst. An explicit `SignalValidated()` is more
   flexible.
3. **Does the arc show in tablet mode** (where the ring is a filled dot)? Probably yes, but
   the contrast needs a look.
4. **Arc colour** — `ClickColor`? `HoverColor`? Its own serialized colour? Currently unspecified.

---

## 6. Definition of done

- [ ] `#Cursor` is a `CursorRingElement`; arc draws clockwise from 12 o'clock.
- [ ] Progress fills; interruption auto-unfills.
- [ ] Completion: +25% burst, tinted `ClickColor`, not stomped by `ReticleHoverFeedback`.
- [ ] Driven from outside (channel or API) — decision from §5.1.
- [ ] `validationFeedbackImage` field deleted from `CursorStateController`.
- [ ] Old cursor canvas deleted; nothing regressed (hover tint, invalid flash, tablet ease).
- [ ] Compile-checked (see `unity-compile-check-without-editor` — Roslyn + `.rsp`, always
      run a negative control).

---

## 7. Also outstanding (unrelated to this file)

- Wire the loading channels on **both** sides (`LoadingInformation` *Broadcasting on* ↔
  `ScreenspaceHud` *Listening on*) — otherwise no text/bar, which is expected, not a bug.
- If the cursor renders invisible: `normalCursorColor` / `tabletCursorColor` on
  `CursorStateController` default to **`Color.white`** and overwrite the UXML's orange ring.
