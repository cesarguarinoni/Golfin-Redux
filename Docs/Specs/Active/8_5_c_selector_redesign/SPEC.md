# 8.5.C — Selector Redesign (hold-to-slide + tap-to-modal)

> **Tier 3 — Full pipeline.** New input handler with non-trivial spatial math (drag-tracking across cards), visual fidelity check against Figma, and the failure-prone case of two interaction modes from the same trigger.
> **Created:** 2026-04-30 16:25 JST
> **Owner:** golfin-implementer → golfin-self-reviewer → golfin-architect
> **Depends on:** `8_5_a_csv_consolidation` ✅ DONE, `8_5_b_lab_inventory_seeder` ✅ DONE
> **Blocks:** none — final 8.5 piece before central ball / TargetingLine work

---

## What we're building

Replace the current broken selector overlay with the new Figma design. Two interaction modes from the same trigger button:

1. **Hold-to-slide** — finger-down on Driver/Golfin button → selector appears beside it → slide finger up over cards to highlight → release on a card commits, release outside cancels.
2. **Tap-to-modal** — quick tap on Driver/Golfin (no drag) → selector stays open as a modal → tap a card or arrow to commit, tap outside to cancel.

Other action buttons fade to 50% opacity and become non-interactive while the selector is open.

**Figma:** file `5gEAHjl6xAtW8iYY7NMvWd`, page `In-game`, frame `Selector` (node `12942:1079`). Test 10 (`12941:7178`) shows it in HUD context.

---

## Reference (Figma 1170 ref, 1 Figma px = 1 Unity unit)

### Card (existing — reuse from 8.5)
- 145 × 240, white bg, 3px `#F3ECC2` border, 20px corner radius, drop shadow `0/4/4 rgba(0,0,0,0.25)`
- Top half (120 tall) = portrait area, `px=33` inset
- Bottom half (120 tall) = navy `#001E39` data block; primary line "DRIVER" 30px, secondary line "195.7 yrds" — "195.7" Medium 30, " yrds" SemiBold 20
- **Already built and rendering correctly per 8.5.B** — do not rebuild

### Stack
- `flex-col gap-[34px] items-start` — vertical stack, **34px gap between cards**
- Selected card is the **bottom** card (touches the trigger button)
- Cards above are the candidates, top-to-bottom in inventory order minus the selected one
- 8px gap between stack and each arrow

### Arrows (top + bottom)
- 80 × 25 visible chevron, wrapped in a 24px-padding container (so total tap target is bigger)
- Drop shadow `0/4/2 rgba(0,0,0,0.25)`
- Top arrow points up (rotate top arrow 180° from the SVG which points down)
- Bottom arrow points down

### Position relative to trigger
- **Driver selector** (right side of screen): selector right edge aligns with Driver button right edge (-58 from screen right). Selector grows upward from Driver. Bottom card sits where Driver button was visually — i.e. its bottom edge aligns with Driver's bottom edge.
- **Golfin selector** (left side): mirror — left edge aligns with Golfin's left edge (+58 from screen left).
- The trigger button is **hidden** while its selector is open (the bottom card visually replaces it). Other action buttons fade to 50% but stay visible.

### State during selection
- SPIN, FADE/DRAW, and the *other* selector button (Golfin if Driver is open, vice versa) → `CanvasGroup.alpha = 0.5`, `interactable = false`, `blocksRaycasts = false`
- Trigger button itself → hidden (`alpha = 0`)
- Background → no dim, no overlay (per Figma — selector floats on the live game scene)

---

## Two interaction modes — exact behavior

### Common state machine

```
Idle → PointerDown on trigger button
     ↓
   Selector spawns + other buttons fade
     ↓
   ──────── timer starts (HOLD_THRESHOLD_MS = 150) ────────
     ↓                                            ↓
   pointer moves > DRAG_PIXELS (8)           pointer stays still
   OR timer < threshold AND finger up        AND timer ≥ threshold
     ↓                                            ↓
   HOLD-MODE                                   MODAL-MODE
```

So:
- If user lifts within 150ms without moving → it was a tap → enter modal mode (selector stays)
- If user moves more than 8px before lifting → enter hold mode (selector follows finger; release commits or cancels based on where finger is)
- If user lifts after 150ms but didn't move → also modal mode (slow tap)

Actually simpler: **hold mode is the default whenever the finger is down**. The transition to modal mode happens **only on finger lift inside the trigger button without having moved more than 8px**. Re-stating:

```
PointerDown on trigger → HOLD-MODE active
  - selector spawns, follows hover
  - card under finger highlighted (scaled 1.05× + slight shadow boost)
  - hovering on top arrow scrolls stack up; bottom arrow scrolls down
PointerUp:
  - Released on a card        → commit that card → close selector
  - Released on an arrow      → no commit; selector stays in MODAL-MODE
  - Released outside any card or arrow:
      - if pointer never moved >8px AND elapsed <150ms (i.e. it was a tap)
        AND release was on the trigger button:
          → MODAL-MODE: selector stays, trigger button stays hidden,
            now waits for tap-to-commit on any card/arrow,
            tap-outside to close without commit
      - otherwise: cancel → close selector, no commit
```

### MODAL-MODE specifics
- Tap card → commit → close
- Tap arrow → scroll stack (don't commit)
- Tap anywhere outside selector → close without commit
- Background tap detection: full-screen invisible `OutsideClickCatcher` Image (already exists from 8.5; reuse)

### HOLD-MODE highlight
- The card directly under the finger gets `localScale = 1.05` and an extra inner glow (or just the scale for v1 — keep simple)
- Only one card highlighted at a time
- If finger leaves the selector bounds (off the side), no highlight; release in that area = cancel

### Arrow scroll behavior
- Stack shows up to N cards visible at a time (see Visible-cards rule below). Tapping the top arrow scrolls the stack so that card index `selectedIndex - 1` becomes the new selected (bottom) card; tapping bottom arrow does `selectedIndex + 1`.
- During HOLD-MODE: hovering on arrow for 300ms triggers a scroll, and continues scrolling at 150ms intervals while the finger stays on the arrow (auto-repeat for fast skimming through long bags).

### Visible-cards rule
- v1: show **all** cards in the stack (no virtualization). Lab has 4 clubs / 2 balls — fits comfortably. If a real bag has 14 clubs, the stack would extend off-screen — that's a future polish (clamp visible count + arrow scroll handles the rest).
- v1 arrow behavior with all cards visible: arrows still work but they just re-order the stack (move selected to next/prev item). Tapping bottom arrow when on the last item is a no-op; same for top on first.

---

## File changes

### 1. New: `Assets/Scripts/Gameplay/UI/ShotUI/SelectorDragRouter.cs`

Owns the hold/tap state machine. Implements `IPointerDownHandler`, `IPointerUpHandler`, `IDragHandler`, `IPointerExitHandler` on the trigger button. On `PointerDown` opens the selector; on drag updates the highlight; on `PointerUp` commits, cancels, or transitions to modal.

### 2. Rewrite: `Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs`

- New layout: `VerticalLayoutGroup` with `spacing=34`, `childAlignment=LowerCenter` (so the stack pins to the bottom; selected card is at the index that sits at the bottom).
- Add chevron arrow GOs above + below the stack (gap=8 from the cards).
- Add public methods: `SetHighlightAt(int idx)`, `ScrollUp()`, `ScrollDown()`, `EnterHoldMode()`, `EnterModalMode()`, `Commit(int idx)`, `Cancel()`.
- Selector position math — anchor matches existing 8.5 wiring (right-anchored for clubs, left for balls). Bottom card's bottom edge must align with the trigger button's bottom edge. Math:
  - Selector container pivot = (1, 0) for clubs (right side, bottom), (0, 0) for balls (left, bottom).
  - `anchoredPosition` = same as the trigger button's `anchoredPosition` (so they overlap at the bottom card position).

### 3. Modify: `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs`

- Remove the existing selector layout build (`VerticalLayoutGroup spacing=12, childAlignment=MiddleCenter`).
- Replace with: stack `spacing=34, childAlignment=LowerCenter`, add `ArrowTop` chevron GO above, `ArrowBottom` chevron GO below, with 24px padding on each.
- Add `SelectorDragRouter` component to both `DriverButton` and `GolfinButton`. Wire `_selectorOverlay`.
- Add a new `OtherButtonsFader` MonoBehaviour reference (see #4) so the router can call `Fade()` / `Restore()`.

### 4. New: `Assets/Scripts/Gameplay/UI/ShotUI/OtherButtonsFader.cs`

Helper sitting on the `ActionButtons_Cluster` root. Tracks the four action buttons + provides:
- `FadeAllExcept(GameObject keep)` → set CanvasGroup alpha=0.5 + non-interactive on all except `keep`; hide `keep` entirely (alpha=0)
- `RestoreAll()` → reset all to alpha=1, interactive

Each action button gets its own `CanvasGroup` for this to work (currently the cluster has one group at root).

### 5. Modify: `Assets/Scripts/Gameplay/UI/ShotUI/SelectorCardWidget.cs`

- Add `SetHighlight(bool on)` method: scales between 1.0 and 1.05.
- Keep existing `SetClub` / `SetBall` API.

### Asset confirmations

- **Chevron arrow PNG:** Figma uses `imgArrow`. Check `Assets/Art/In-Game UI/` for an existing `Arrow*.png` or chevron asset. If missing, surface to architect — don't fall back to text. Likely candidates: `Icon - Straight.png` (already used as up arrow placeholder in 8.5 v1).

---

## Acceptance checklist

### Layout (static, selector open)

- [ ] Driver tap-and-hold → selector appears with **all 4 clubs as cards**, stacked bottom-to-top in inventory order minus selected (selected at bottom).
- [ ] 34px gap between cards.
- [ ] Top chevron arrow visible above top card, ~32px (24+8) gap.
- [ ] Bottom chevron arrow visible below bottom card, ~32px gap. (Arrows wrap with 24px padding so visual gap to card edge is 24px; stack spacing adds 8px more.)
- [ ] Selector right edge (for Driver) aligns with Driver button right edge (`x = -58` from screen right).
- [ ] Bottom card's bottom Y aligns with where Driver button's bottom Y was (`y = 96`).
- [ ] Driver button itself is **hidden** while selector is open.
- [ ] Other 3 action buttons (SPIN, FADE/DRAW, GOLFIN) at 50% opacity, non-interactive (verify by trying to tap one — no response).
- [ ] Same for Golfin selector (mirrored to left side).

### Hold-mode interaction

- [ ] PointerDown on Driver → selector opens immediately (no delay).
- [ ] Drag finger up over cards → card under finger scales to 1.05.
- [ ] Drag back over a different card → previous loses highlight, new card highlights.
- [ ] Drag finger off cards (to side) → no card highlighted.
- [ ] Release on a highlighted card → that club becomes selected, selector closes, Driver button reappears showing the new club.
- [ ] Release outside any card → selector closes, no change to selected club.
- [ ] Hover finger over top arrow >300ms during hold → stack scrolls up by one. Continue holding → repeats every 150ms.
- [ ] Same for bottom arrow.
- [ ] Release on an arrow → selector enters modal mode (does NOT close).

### Tap-mode (modal) interaction

- [ ] Quick tap on Driver (finger down + up <150ms, no drag) → selector opens AND stays open.
- [ ] Tap a card → commits, selector closes.
- [ ] Tap an arrow → scrolls stack, selector stays open.
- [ ] Tap anywhere outside selector → selector closes, no commit.
- [ ] Tap on the trigger button area while modal is open → closes without commit (treat as outside).

### Lab integration

- [ ] Selecting Iron card via hold-mode → `ClubSelectionBroadcast.Raise` fires, `PhysicsLabController.OnClubBroadcastReceived` switches to `LabClubs[1]`. Fire a shot — Iron trajectory.
- [ ] Selecting Wood card → uses `LabClubs[0]` (Driver slot — confirmed in 8.5.B). Driver-like trajectory.
- [ ] Selecting Putter card → `LabClubs[3]`, ground-level camera, putt physics.
- [ ] Switching balls (Golfin ↔ Putt Ace) updates the GOLFIN button label without errors.

### Visual fidelity

- [ ] Side-by-side diff: `screenshots/diff-selector-vN.png` — Figma reference (node 12942:1079) on left, play-mode capture on right, scaled identically. Cards align, gap is 34px, chevrons positioned correctly.
- [ ] Card scaling on highlight (1.05) is subtle but visible — not jarring.

### Edge cases

- [ ] Open Driver selector, drag finger off the right edge of the screen → release → selector closes, no commit.
- [ ] Open Golfin selector while Driver selector was the last opened (sequential, not concurrent) → no state leak from previous session.
- [ ] Spam-tap Driver 5× rapidly → selector opens/closes correctly, no orphaned overlays.

---

## Open design questions (answer in spec, don't surface during impl)

**Q1.** Hold threshold: 150ms feels right but might need tuning. Document as a `[SerializeField] float _holdThresholdMs = 150f` on `SelectorDragRouter` so we can adjust without rebuilding.

**Q2.** Drag distance threshold: `8px` (Unity units = 8px at 1170 ref). Same — `[SerializeField]`.

**Q3.** Highlight scale: 1.05 is conservative. If it reads as "nothing happened" in playtest, bump to 1.08. Inspector field.

**Q4.** Arrow auto-repeat delay/interval: 300ms initial, 150ms repeat. Inspector fields.

All four become inspector knobs on `SelectorDragRouter` for post-impl tuning.

---

## Out of scope

- **Visible-card virtualization** for long bags (clamp + scroll). v1 shows all.
- **Animations** for selector spawn/dismiss (fade, slide). v1 is instant on/off.
- **Localization** of labels (existing tech debt from 8.5.A spec — clubs CSV not wired to localization yet).
- **Haptics** on highlight change.

---

## Done report

In `Docs/Specs/Active/8_5_c_selector_redesign/IMPLEMENTER_REPORT.md`:

- Files created/modified list.
- Side-by-side diff screenshot path.
- Per-checklist-item PASS/FAIL with one-line justification.
- Inspector defaults shipped (`_holdThresholdMs`, `_dragDistanceThreshold`, `_highlightScale`, `_arrowRepeatDelay`, `_arrowRepeatInterval`).
- Any deviations from spec.
- Open questions for architect (if any — surface, don't invent).

Set STATUS to `READY_FOR_SELF_REVIEW`.
