DONE

Task: tournaments_mode_card
Iteration: iter-2 (iter-1 + Cesar's copy/spacing pass + approved Multiplayer coin fix)
Approved by Cesar: 2026-08-11 11:12 JST ("set it done")
Canonical video: videos/tournaments_mode_card_demo.mp4 (1170x2532, 50.1s)
Daily-report copy: Docs/Reports/Media/tournaments_mode_card_demo.mp4

## What is done

All 11 SPEC acceptance items implemented and verified PASS in play mode — see
`IMPLEMENTER_REPORT.md`. Both PLAY routes reach `ScreenId.TournamentSelection`, EN + JP
render correctly, regressions checked, console clean, `UIFidelityLinter` reports
`fail: 0` on both touched prefabs (re-verified by the hook's own live re-run).

**iter-2** applied Cesar's requests: subtitle → "Be the best and earn rewards" /
頂点に立って報酬を手に入れよう (EN + JP + the `modes.csv` fallback), and the REWARDS
label→value gap tightened from the authored 32px to 12px for the text variant only
(coin rows still measure 80px). All four captures re-shot.

**Multiplayer coin fix approved by Cesar** ("Fix it", 2026-08-11). Expanding the
Multiplayer row on the full-screen Mode Select used to draw a Reward-Points coin before
the words "NO ENTRY FEE" while its collapsed row did not. Both states now read
`NO ENTRY FEE`. A full economy-icon audit across both prefabs confirms every icon that
can draw is controlled; the only uncontrolled pair (`Reward3Icon*`) is a spriteless slot
whose parent is inactive on every mode, so it never renders.

## Prior blocker — RESOLVED

This task was `IMPLEMENTER_BLOCKED` because Rule 21's "P2 fail-closed" check could never
pass: `_rerun_ui_lint_via_editor()` in `.claude/hooks/enforce_implementer_done.py` built
its C# inside a non-raw f-string, so the emitted script never compiled
(`\"fail\":` collapsed to `""fail":` → CS1003/CS0103; `\s`/`\d` leaked into the C# literal
→ CS1009). It therefore always returned `None`, blocking **every** Figma-node UI task.

Fixed on Cesar's instruction ("fix the gate") — 2 lines:

- `StartsWith("\"fail\":")` → `StartsWith("\\"fail\\":")`
- the JSON regex now emits `"\"fail\"\\s*:\\s*(\\d+)"` (escaped backslashes)

Verified: the emitted C# compiles and a live re-run returns `fail = 0` for both prefabs;
a non-zero verdict still propagates (`LINT_FAIL_COUNT:3` → `3`), and unreachable-editor /
prefab-not-found still return `None` so the check stays fail-closed. Hook test suite:
**118 passed, 1 failed** — the one failure (`TestLiveEditorIntegration::test_real_clone_matches`)
reproduces identically on the unmodified HEAD version, so it is not from this change.

## Cesar decisions applied (2026-08-11)

- **JP subtitle APPROVED** — 頂点に立って報酬を手に入れよう ships as written.
- **Multiplayer coin removal APPROVED as a fix** — see `IMPLEMENTER_REPORT.md` § D1.
- **Font atlas churn REVERTED** — `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset`
  restored to HEAD (59,524 bytes) and re-verified after an AssetDatabase refresh: loads,
  `atlasPopulationMode=Dynamic`, `glyphTable=0`, `sourceFontFile` intact. It is no longer
  in the change set. `m_ClearDynamicDataOnBuild` is already `True`, so the 2.27MB bake
  never reached a player build — pure editor churn that re-appears after any JP play
  session and should simply not be committed.

## Remaining open item (non-blocking)

- **"Varies by tournament" casing** kept lowercase per the SPEC's approved copy (Cesar's
  message wrote "Tournament"). Say the word to capitalize.

## Post-close-out verification (2026-08-11, at Cesar's request)

**1. `TournamentLoopCaptureHarness` — RUN. The `"TOURNAMENTS (TEMP)"` path PASSES.**
Executed `GOLFIN > Tournaments > Dry Run — Tournament Round Loop` (validates the real
flow without a 24-minute recording). Log:

    === TournamentRoundLoop: Click NavTeeButton (Home → ModeSelection) ===
      WaitForScreen OK: on 'ModeSelection' after 0.0s
    === TournamentRoundLoop: Click TOURNAMENTS (TEMP) ===
      WaitForScreen OK: on 'TournamentSelection' after 0.0s

No `FindButton AMBIGUOUS` warning and no `CLICK FAILED` — the new TOURNAMENTS card does
not shadow the temp button, confirmed by execution rather than by reasoning.

The harness then stops at its NEXT step, `Click "SIGN UP"` → `FindButton MISS`. **Not
caused by this task.** Every tournament fixture has expired: a live query returned
`kawana_fuji_open, kisarazu_cup, gotemba_masters, lomond_championship,
hirono_invitational, kasumigaseki_open` all in state `Ended`, and the only CTA label
present on screen was `LEADERBOARD` (card dates read "Ended Jul 3" / "Ended Jun 26" vs
today 2026-08-11). With no OPEN tournament there is no SIGN UP button to click, so the
signup→round→leaderboard half of the harness is unreachable until the fixture dates are
refreshed. Worth a separate ticket.

**2. JP description wrapping — VERIFIED IN EDITOR, no device needed.** The Game View at
1170×2532 IS the device canvas, so wrap/truncation is fully determined here; only
safe-area insets and runtime glyph rasterisation would differ on hardware, and neither
affects line breaking at a fixed canvas size. Measured on the live TMP after a forced
mesh update, both prefabs, both languages:

| Card / language | lines | box | rendered | `isTextTruncated` | overflow |
|---|---|---|---|---|---|
| ModeHomeCard expanded, EN | 5 | 516×182 | 504×182 | false | none |
| ModeHomeCard expanded, **JP** | 5 | 516×202 | 501×202 | false | none |
| ModeCard expanded, EN | 4 | 770×168 | 769×168 | false | none |
| ModeCard expanded, **JP** | 4 | 770×186 | 739×186 | false | none |

The description containers are content-sized, so the taller JP text grows the box
(182→202 and 168→186) instead of clipping. Nothing truncates, nothing overflows width or
height. This item is closed.
