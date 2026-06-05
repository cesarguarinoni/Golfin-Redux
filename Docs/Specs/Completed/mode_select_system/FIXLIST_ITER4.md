# Iteration-4 fix list — Cesar live-Unity review, 2026-06-04

**Binding scope for the next implementer iteration.** Supersedes nothing in SPEC/Step 0/0.1 — adds precise corrections from watching the running scene.

## ROOT CAUSE (read first — this is why regressions keep happening)
The implementer keeps **re-authoring** elements that the clone source (`NextHolePanel` / `HoleCard`) already had correct — recreating the PLAY button, dropping the separator sprite, swapping fonts. **STOP recreating.** The contract is: **DUPLICATE the source → PRESERVE its existing children (sprites, button instance, fonts, separators) → only REBIND data and nudge layout to Figma.** If an element existed and looked right last iteration, it must not change unless Figma/this list says so.

---

## HOME SCREEN — REGRESSIONS (were right last iter, now broken — restore them)
- **R1 — Separator image is GONE.** The home card now shows a grey placeholder box where the separator sprite belongs. Restore the separator Image's sprite (NextHolePanel/the prior card had it). No grey placeholder.
- **R2 — PLAY button wrong size (recreated).** The gold PLAY button appears rebuilt at the wrong size. **Prefab-wins:** restore the exact existing gold PLAY button instance/prefab — keep its original size + typography. Do NOT recreate it.
- **R3 — Fonts changed.** Home-card fonts regressed. Restore the correct fonts (same setup as the working version: Rubik variable font, weight tuned to Figma) — don't wholesale-swap font asset or sizes.

## HOME SCREEN — PERSISTENT problems (both iterations)
- **H1 — Carousel must be CIRCULAR / infinite.** There must be cards to the LEFT and RIGHT of the centered card, and NO left/right end-stops — it wraps around endlessly. Currently it's a bounded strip. `ModeCarouselController` needs looping/wrap logic (recycle/reposition cards so a neighbour is always visible both sides).
- **H2 — Promo Banner z-order.** The Cross-Promotion banner must render OVER `CharacterRoot` (currently the character occludes/sits above it). Fix sibling/hierarchy order so the banner draws above the character.
- **H3 — ModeHomeCard background Image type = SLICED.** Currently Simple → distorts. Set the card-bg Image to Sliced (9-slice) so it scales cleanly.
- **H4 — Card Container is too high.** It should sit **24px from the Promo Banner** (24px gap between promo banner and the card container). Reposition.
- **H5 — Cards bottom-anchored, not top.** Card anchors go to the BOTTOM. Non-selected (shorter, collapsed) cards' BOTTOM edge must align with the selected (taller) card's BOTTOM edge — cards grow upward from a shared baseline.

---

## FULL-SCREEN MODE SELECT ("Hole Select Screen") — PERSISTENT problems
This screen is "based on Hole Select" — it must reuse Hole Select's chrome, not be a bare hand-built screen.
- **F1 — No background.** Add the SAME background as the Hole Select screen (clone/reuse it). Currently bare/dark-navy.
- **F2 — No panel container.** Add Hole Select's panel container; the cards sit inside it.
- **F3 — No scroll bar.** Add the scrollbar (Hole Select has one; Figma 13026 shows it at x≈1090).
- **F4 — Text & card-container sizes mismatched.** Reconcile card + text sizes to Figma 13026 (978-wide cards; see FIGMA_METRICS per-card table). Sizes must be internally consistent across the 4 cards.
- **F5 — Locked-card dark mask is the wrong size.** The dark overlay on locked cards (Driving Range / Missions) is mis-sized. Size it to cover the card exactly (match HoleCard's `lockedOverlay` rect).

---

---

## ITER-5 ADDENDUM — remaining items after iter-4 self-review (2026-06-04)
iter-4 passed 11/13. Remaining + new:
- **F2 (still FAIL) — panel container sizing.** The full-screen ScrollView is full-screen with no TopBar/BottomNav padding, so the top PRACTICE card is CLIPPED under the header. Match HoleSelection's container sizing: `sizeDelta=(-96,-620)`, `anchoredPosition.y=-30` (clone the metrics, not just the structure). This fixes the clip.
- **F3 (still FAIL) — scrollbar invisible.** It exists but `m_VerticalScrollbarVisibility=2` (AutoHide) so it never shows. Make it visible to match Hole Select / Figma 13026 (scrollbar at x≈1090) — set Permanent visibility (or match HoleSelection's scrollbar config exactly).
- **EXPAND DEFAULT (Cesar decided) — ALL CARDS COLLAPSED on the full-screen.** No card auto-expands; the player taps to expand. Remove the auto-expand-first-card behavior in `ModeSelectScreenController` (it currently expands PRACTICE). Figma's expanded-MULTIPLAYER state is just the mockup example, not the default.
- **Carousel arrows + scrollRect unwired** — `ModeCarouselSection`'s side-arrow buttons and scrollRect refs are `fileID: 0`. Wire them so the home side-arrows actually step the carousel.
- **Home card-vs-character z-order** — the CharacterRoot draws over the centered card's top-left. Ensure the carousel cards render ABOVE CharacterRoot (same fix family as H2 for the banner).

---

## Regression guard (do NOT undo these — already correct / already reverted)
- Singletons: the architect reverted the 7-manager + ModesDatabaseCSV inversion to first-wins. The 8 files must NOT reappear as modified. (Editor-replay quirk filed at `Docs/Specs/Quick/editor_replay_singleton_reset.md`.)
- Two distinct prefabs (ModeHomeCard ← NextHolePanel, ModeCard ← HoleCard) — keep.
- Full-screen card order, locked treatment, fee economy, tee→ModeSelection routing, fade swaps — keep.
- Captures: clean 1170×2532 portrait, no editor chrome.
