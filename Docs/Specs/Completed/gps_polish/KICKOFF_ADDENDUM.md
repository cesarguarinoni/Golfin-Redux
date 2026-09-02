# KICKOFF ADDENDUM — `gps_polish` continuation (Architect, 2026-09-02 evening)

Cesar's call on the report: **the push is approved and stays**; deviations D-1…D-7 are accepted
(D-5 — the nav bar wired off the hub — is welcome, it fixed a real "no way out of Profile /
Badges / Avatar" defect). **Finish the remainder before the device pass.** The
`IMPLEMENTER_REPORT.md § 5 · Not done` table is now the checklist; nothing else in `SPEC.md`
changes. Same builder/lint/importer rules, same evidence standard.

## Scope of this continuation (all from § Not done)

| # | item | done when |
|---|---|---|
| R1 | **D3 staggers** — `Stagger`-rise on the FIRST paint of a fetch result: hub round rows, badge cells, gift Popular Golfers + Top Supporters, vote cards | log line per site distinguishing `paint(cache)` (instant) from `paint(fetch)` (staggered); one still mid-stagger per site |
| R2 | **D4 panel fades** — Gift `BUY GIFT ITEMS` / `TOP SUPPORTERS` / `POPULAR GOLFERS` fade in with their data; vote `PUBLIC`↔`MINE` list cross-fade | in video (d′) below |
| R3 | **D6 selection bumps** — 1.0→1.06→1.0 over 0.10 s + two-Image alpha cross-fade (no sprite tinting) on avatar swatches, experience chips, vote filter chips, gift amount buttons | in videos (e) and (f) |
| R4 | **D7 count-ups** — Gift `GIFTS RECEIVED`, gift `Your balance`, Top-UI RP after a vote (GPS-originated delta only), Score Posted total `Pop`, profile badge count; badge `Pulse` on newly earned; vote bar fill width animates old→new after a cast | in video (f) + the Score Posted still |
| R5 | **D8 shimmer** — place `ShimmerBlock` hosts at the five cold-fetch sites (hub rounds ×3, badges ×6, supporters ×3, golfers ×3, vote ×2), cache-hit gate, hidden on error in favour of the fading empty/error label | A8: one frame per site during a cold fetch (quote how the paint cache was cleared) + a log line proving the cache-hit path skipped it |
| R6 | **D9 keyboard** — Golf Profile nickname/handicap and Vote CREATE fields scroll above the keyboard on `onSelect`, restore on `onDeselect`; `TouchScreenKeyboard.area` when available, no-op in Editor | code + EditMode test of the offset math; **flagged for the device pass** (only observable on the phone) |
| R7 | **A4 videos (c)(e)(f)** — Score Upload step walk; Golf Profile → Welcome → hub; a live cast with bar fill + RP count-up (needs R4) — plus a short (d′) of the gift/vote panel fades and filter cross-fade | ≥50 KB each, drawtext-captioned via the Rule 17 idiom, one still each in `screenshots/` |
| R8 | **A7 pending frame** — one captured frame of a `…` button mid-call | still in `screenshots/` |
| R9 | **A13 perf / GC** — Profiler over the push walkthrough: per-frame GC alloc after warm-up, worst frame ms; fix any per-frame allocation | numbers quoted; the claim becomes a measurement |

Videos (a) push walkthrough and (b) nav-bar sweep exist from the first iteration — do not re-record
unless R1–R5 change what they show (they will: (b) now has staggers and shimmer — re-record (b)).

## Rules that still apply

- Rest-state parity stays **0 px** on all 7 GPS screens after every change (re-run A2 at the end).
- A1 invariants re-run at the end — `fail=0` still, 10 transitions.
- No new strings expected; if one appears, Build rule 7 (importer → publish → `--check`).
- No haptics, no `FadeController` change, no non-GPS prefab change (`git status Assets/Prefabs` quoted).
- Close-out: when R1–R9 are green, STATUS → `READY_FOR_SELF_REVIEW` with every A-item filled; the
  Architect re-reviews against HEAD, Cesar approves, folder → `Completed/`.

## Live vote to burn

Four seeded `GOLFIN AI` votes remain uncast on prod (backlog row). Use **one** of them for video
(f) — Cesar approved burning test votes for the first cast; the same approval covers this one.
Name which vote id was used in the report. The other three stay for the device pass.
