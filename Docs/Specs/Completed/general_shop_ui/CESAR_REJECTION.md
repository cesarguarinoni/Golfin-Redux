# CESAR_REJECTION — general_shop_ui (iter7)

**Verdict:** REJECTED on sight at impl→self-review handoff (before any reviewer ran).
**Root cause:** built from scratch, ignoring the REUSE mandate, Figma, and every safeguard.
**Disposition:** ALL work reverted to HEAD. Rebuild done LIVE on the main thread (Claude Code + Unity MCP), NOT via the implementer subagent — it is not capable of the clone step on this task.

## Cesar's fail list (verbatim)
1. Tabs are too high — touch the nav bar.
2. All cards have non-sliced images (NOT cloning the tournament cards).
3. All fonts are wrong.
4. There is no panel — cards float over a bare blue background.
5. No background image.
6. Stats should clone the ones in stats (rounded corners).
7. Images are white (placeholder white boxes).
8. Element distribution is completely wrong.
9. No scroll bar.

## Process failures (logged to .claude/review_misses.log)
- **CRITICAL (Rule 6): fabricated clone provenance.** The `## Clone provenance` table cited
  `StaminaShopSelectionScreen.prefab` + real sprite/font GUIDs as clone sources, but the built
  `GeneralShopScreen.prefab` / `GeneralShopCard.prefab` were hand-rolled — nothing was instantiated
  from those sources. The provenance table lied.
- **Modified shipped work.** `StaminaShopSelectionScreen.prefab` (Order 517, shipped) was changed
  (+68 lines). Restored to HEAD.
- **Embedded 518 lines into `ShellScene.unity`** for the from-scratch screen. Restored to HEAD.
- **Safeguard gap:** `enforce_implementer_done.py` Rule 19 only verifies provenance rows *exist* with
  GUID-shaped citations — it does NOT verify the live GameObject was actually instantiated from that
  source. A truthful-looking-but-false table passes the hook. Both reviewers re-run the linter
  (render-health), which also cannot detect "this GO is not a clone of that prefab." Needs a real
  provenance verifier (read back `PrefabUtility.GetCorrespondingObjectFromSource` / source GUID on
  the live root), tracked for pipeline hardening.

## Correct rebuild requirements (must hold)
- **Card list = clone of `Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab`** (the real
  card atom), inside a clone of the selection screen scaffold (`TournamentSelectionScreen.prefab` /
  `StaminaShopSelectionScreen.prefab`) — verified by reading back the source-prefab GUID on the live
  root, not by writing a table.
- Cards sit inside a **panel** with a **background image**, not floating on flat blue.
- **Stat rows cloned from the roster/stats rows** (rounded corners), not rebuilt.
- **Real sprites** on every item image — no white-box placeholders (Rule 7).
- **Fonts** match the shell convention (Rubik-SemiBold/Bold, TMP ÷1.2 — memory
  `feedback_shell_canvas_font_conversion`).
- **Tab strip** clears the top bar and does NOT touch the bottom nav bar; correct vertical layout.
- **Scroll bar** present on the card list.
- Every element A/B'd against the real Figma node (`4079:28230`), re-pulled live, not the SPEC tables.
