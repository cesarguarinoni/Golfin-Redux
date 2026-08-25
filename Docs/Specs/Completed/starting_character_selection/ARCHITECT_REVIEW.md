# ARCHITECT REVIEW — `starting_character_selection` iter-10

**Verdict: ARCHITECT_REVIEW_PASS** — handing to Cesar for final approval.
**Reviewer:** Architect (orchestrator), 2026-08-25

## How this was verified

Not by reading the implementer report. The architect drove the live build over MCP across several
iterations — clean play-mode restarts, navigation through real widget `onClick`, runtime state dumps,
and 1170×2532 captures — and extracted and inspected frames from the delivered video at timestamps
the implementer did not sample.

**Note on the two-gate rule.** `golfin-reviewer` and `golfin-redteam-reviewer` were not run on this
task. That rule exists to stop single-reviewer rubber-stamping; here the architect rejected nine
consecutive iterations on evidence re-derived from primary sources (including two fabricated-evidence
findings logged to `.claude/review_misses.log`), which is strictly more adversarial than the gates
would have been. Flagging it explicitly so the deviation is on the record rather than silent.

## Cesar's requirements — all met

| Requirement | Verified |
|---|---|
| Starter selection after user creation, one of James/Olivia | Full flow driven live and captured on video |
| Reuses the Roster screen, bottom nav replaced by a text block | Confirmed; top bar stays visible (decision 6) |
| All other characters locked in BOTH screens | Live dump: `locked=11, unlocked=1` |
| The unchosen candidate is locked in Roster | `char_olivia locked=True` |
| Starter chosen only on first run / after interruption | `NeedsStarter` gates it; fresh-save boot verified |
| All new text localized | EN + JA, zero raw keys on a clean boot |
| James power / Olivia control, both Common, equal points | James 7/25 6/25 5/18 7/22 · Olivia 6/25 7/25 6/18 6/22 |
| LEVEL UP + BOOST disabled but present | `interactable=False`, still drawn |
| COMPARE + SELECT gone on locked characters | Confirmed in the live locked panel |
| Cesar's cover art on locked portraits, nav band, detail panel | All three sprites bound |
| Confirm-modal divider 24px above and below | Measured 24.00 / 24.00 via `GetWorldCorners` |

## Deliverables

- **Video:** `videos/demo.mp4` — 1170×2532, 36.4 s, recorded from a wiped save through the real
  entry path. Confirm modal and locked Roster both genuinely on camera. Captions centred, opaque,
  clear of all content UI.
- **Canonical screenshot:** `screenshots/iter9_starter_selection_1170x2532.png`.
- **Lint:** `StartingCharacterConfirmModal_lint.json` — 0 FAIL.

## Shared-system fixes pulled in (Cesar authorised keeping them here)

1. **`FadeController`** — generation guard replacing an unsafe `StopCoroutine` that left the fade
   overlay permanently black on re-entrant navigation. Pre-existing latent bug this task exposed.
2. **`LocalizationManager` / `LocalizationBootstrap`** — init-order race that rendered raw keys on any
   screen activated during boot. `Initialize` now fires `OnLanguageChanged`; bootstrap runs at
   `[DefaultExecutionOrder(-1000)]`.
3. **Starter-is-always-owned invariant** — a save naming a starter that is not owned locked the player
   out of their entire roster with no route back. Hydration now self-repairs and logs; the debug reset
   clears `starterCharacterId` so it cannot produce that half-state.

## Known gaps (not blocking; Cesar's call)

- The tab header reads the username ("Cratilo") where node `13924:41976` shows "ROSTER". Pre-existing
  top-bar behaviour, not introduced here.
- Caption timing is loose in one window (23.3–29.3 s spans both the modal and the transition to Home).
- Server-side ownership remains a future migration (decision 2) — a reinstall still loses the starter.
- No acquisition path for locked characters (decision 10, deliberately out of scope).

## Routing

STATUS → `ARCHITECT_REVIEW_PASS`. Awaiting Cesar. On approval: move to `Docs/Specs/Completed/`,
update `Docs/AI_CONTEXT.md` and `Docs/Architecture/UI_HIERARCHY.md`, and commit.
