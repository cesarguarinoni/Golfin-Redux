# Tournament Selection Screen (T7) — Code-Proof Implementation Spec

> **Order:** T7 `tournament_selection_screen` (Implementation Plan Phase E). **Class:** FULL PIPELINE (new screen + visual fidelity + state matrix).
> **Figma:** frame **`13386:1758`** "Tournament Selection v7 - states + RP icons" · [node](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13386-1758) · canvas **1170×2532**. On-screen title **"TOURNAMENTS"**.
> **Authority order (RUNTIME_BLUEPRINT §9):** ① literal tokens below + ② Figma node links = **source of truth**; ③ `reference/tournament_selection_screen.png` = **guide only**.
> **GDD:** §16 (Selection screen, U1 expand-in-place, §16.2 filters, §16.3 card anatomy, §16.4 state badges, §16.5 CTA states, §16.6 sign-up/character-lock) + §17 Addendum.

![Tournament Selection (T7)](reference/tournament_selection_screen.png)

---

## 0. Rules
1. **Clone-and-modify, never rebuild (HARD).** Screen scaffold, filter tabs, the gold/silver buttons, RP icon, and persistent nav bars all already exist — reuse them (§1). The only **new** prefab is the tournament card.
2. **Figma tokens are literal law.** Use the §3 values verbatim (Figma px). Unity TMP size = **Figma px ÷ 1.4** (project rule); CanvasScaler **1170×2532, Match 0** → 1 Figma px = 1 unit.
3. **Staged delivery** (§6): Stage 0 prefabs → Stage 1 static screen + nav (Cesar visual gate) → Stage 2 bind `ITournamentBackend.GetTournaments()` → Stage 3 expand-in-place + sign-up modal. **Stages 0–1 need no backend** (build now, exactly like `tournament_screens`); Stage 2 depends on **T1→T4**.
4. **This screen replaces the `TOURNAMENTS (TEMP)` ModeSelection entry** that `tournament_screens` left as a stub (see that task's STATUS).

---

## 1. Reuse map / clone sources (GDD §16.2–16.6)
| Element | REUSE (clone-and-modify) | Note |
|---|---|---|
| Screen scaffold (filter strip + scroll list + persistent bars) | the in-scene **`HoleSelectionScreen`** base — same clone `tournament_screens` used for `TournamentHoleSelectionScreen` | title bar shows **"TOURNAMENTS"** |
| Filter tabs **ALL / OPEN / PLAYING / CLOSED** | the **Rankings period-tab pattern** (TabBar w/ active underline + dividers) — NOT the Hole-Select pills | 4 tabs, dividers `13386:1766/1770/1774` |
| Gold CTA (SIGN UP / CONTINUE) | the **shared gold primary button** (same instance as `Sign Up Button` `13386:1803`) | tokens §3 |
| Silver CTA (LEADERBOARD) | the **silver `TournamentCloseButton`** style built in `tournament_screens` | from the render the entered-finished card uses a silver button |
| RP icon | existing **RP Icon** sprite (`13386:1939`) | 40×40 |
| Persistent nav bars (currency, gear, gold banner title, bottom nav) | `PersistentUIManager` — same path `tournament_screens` wired | add `TournamentSelection` to `showBars`, banner title "TOURNAMENTS" |
| Sign-up / character-lock modal (Stage 3) | `ModalController` | GDD §16.6 / U1 |
| Data (Stage 2) | **`ITournamentBackend.GetTournaments()`** (T1/T4) | each `TournamentDefinition` + derived `TournamentState` |
| **NEW** | `TournamentSelectionCard` prefab (+ state variants) | only net-new prefab; anatomy §2/§3 |

---

## 2. Screen structure (nodes under `13386:1758`)
- **Top UI** `13386:1760` (0,0 1170×313) — currency + gear + gold banner title "TOURNAMENTS" → persistent bars.
- **Filter Strip** `13386:1761` (48,325 1074×56) → `Filter` `13386:1762`: tabs `ALL` `13386:1763`, `OPEN` `13386:1767`, `PLAYING` `13386:1771`, `CLOSED` `13386:1775`; vertical dividers `13386:1766/1770/1774` (24px tall hairlines).
- **Content Container** `13386:1778` (48,405 1074×1780) → **Cards Container** `13386:1779` (vertical list, card pitch **384px** = 360 card + 24 gap).
- **Cards** (978×360 each, x=48 within container) — six states:

| Card | Node | Badge | Badge node | Entry/Reward | CTA (from render) |
|---|---|---|---|---|---|
| Kasumigaseki Open | `13389:1884` | **LIVE** | `13389:1887` | ENTERED · 15,000 + Medal | **CONTINUE** (gold) |
| Hirono Invitational | `13405:1858` | **LIVE** (finished) | `13405:1861` | ENTERED · 18,000 + Trophy | **LEADERBOARD** (silver) |
| Lomond Championship | `13386:1780` | **OPEN** | `13386:1783` | FREE ENTRY · 5,000 + Ticket | **SIGN UP** (gold) |
| Gotemba Masters | `13386:1804` | **ENDING** | `13386:1807` | ENTRY ⓡ500 · 12,000 + Trophy | **SIGN UP** (gold) |
| Kisarazu Cup | `13386:1828` | **UPCOMING** | `13386:1831` | FREE ENTRY · 8,000 | *CTA TBD — flag* |
| Kawana Fuji Open | `13389:1849` | **ENDED** | `13389:1852` | FREE ENTRY · 20,000 + Trophy | *LEADERBOARD? — flag* |

- **Nav Bar** `13386:1852` (bottom, persistent); **Scrollbar** `13386:1853` (AutoHide).

### Card anatomy (canonical = Lomond OPEN `13386:1780`, fully tokenized §3)
`tournament_image` (260×360 left bleed, object-cover) `13386:1781` · chevron `›` `13386:1782` (expand affordance, U1) · **state badge** (top-right pill) · **Content** (flex-col, gap 24, pad 28/32): **Header** = `GOLFIN PRESENTS` eyebrow + tournament name + `club - 18 Holes`; **Hours+Map** row = dates `—` countdown/status; **Separator** hairline; **Action Row** (h100, space-between) = `Entry+Rewards` (entry badge + RP icon + amount) on the left, **CTA button** bottom-right.

---

## 3. Tokens (literal — Lomond OPEN card `13386:1780`, via get_design_context 2026-06-25)
**Card container:** bg linear-gradient ↓ `#133453`→`#091b33`; border **3px** `#3e7ca8`; radius **50**; shadow `0 10 10 rgba(0,0,0,0.4)`; size **978×360**; items-center, overflow-clip.
**tournament_image:** 260×360, object-cover, left.
**Chevron `›`:** Rubik Bold, **80px** (TMP ~57), `#3e7ca8`, right ~53.
**State badge (OPEN):** pill 180×44, radius 22, top 21 / right 29; fill **`#50c878`** (= *Rarity/Rare* token); text "OPEN" Rubik Bold **32** (TMP ~23) `#0a1a30`.
**Content:** flex-col, **gap 24**, padding **28 top/bottom · 32 left/right**.
**GOLFIN PRESENTS** `13386:1788`: Rubik SemiBold **24** (TMP ~17), gradient text white→`#d1d6e0`(40%)→`#828fa1` (metallic eyebrow).
**Tournament name** `13386:1789`: **Noto Sans JP Bold 42** (TMP **30**), white. *(JP font — names may be JP/EN.)*
**Club + holes** `13386:1790`: Rubik Regular **22** (TMP ~16), `#c7d6eb`.
**Hours+Map** `13386:1791` (gap 12, 22px): date/countdown = Rubik SemiBold **white**; dash `—` = Rubik Regular `#c7d6eb`.
**Separator** `13386:1797`: full-width 1px hairline (image fill).
**Entry badge — FREE ENTRY** `13386:1800`: fill `rgba(250,199,77,0.18)`, border 1px `#fac74d`, pad 6/14-16, radius 22; text Rubik SemiBold **22** (TMP ~16) `#fac74d`. *(ENTRY-fee variant `13386:1824`: inline `ENTRY` + RP icon 30 + amount.)*
**RP amount** `13386:1802`: RP icon **40×40** + text Rubik Bold **32** (TMP ~23) `#73e080` (green).
**Gold CTA (SIGN UP)** `13386:1803`: 260×54, radius 20, outer border 1px `#422100`; inner border 2px `#ffe48b`, gradient ↓ `#fcf195`→`#d6ab42`(60%)→`#bb7f1d`; label Rubik SemiBold **39** (TMP ~28) `#321506`, tracking -0.24, line-height 54, white 30% text-shadow; sheen overlay. **= shared gold primary button.**

**Per-state badge fills** (extract exact hex per node in **Stage 0** — render guide): LIVE = red/orange (`13389:1887`/`13405:1861`); OPEN = `#50c878` ✓; ENDING = gold/amber (`13386:1807`); UPCOMING = blue (`13386:1831`); ENDED = grey (`13389:1852`). Badge geometry is constant (180×44 / r22 / top21 right29); only fill + label width change.

---

## 4. State matrix (drives Stage 2 binding)
Each card's **badge**, **entry/reward block**, and **CTA** are a pure function of `TournamentState` + `EntryState`:
- **OPEN**, not entered → green badge, entry badge (FREE/fee), **SIGN UP** (gold) → Register flow.
- **ENDING**, not entered → amber badge, **SIGN UP** (gold).
- **Playing/ENTERED**, in progress → LIVE badge, **CONTINUE** (gold) → resume at `TournamentHoleSelection`.
- **Playing/ENTERED**, finished round → LIVE badge, **LEADERBOARD** (silver) → `TournamentLeaderboard`.
- **UPCOMING** → blue badge, countdown "Starts in …", **CTA = flag** (likely disabled/"Notify").
- **ENDED/CLOSED** → grey badge, **CTA = flag** (likely LEADERBOARD silver, or disabled).
Countdown/status text (`Ends in 3d 04h`, `Starts in 8d`, `Round in progress — Hole 7 of 18`) is computed from `(startUtc, endUtc, entry, ITournamentClock.UtcNow)`.

---

## 5. Navigation
- New **`ScreenId.TournamentSelection`** in `ScreenManager` (+ field + ApplyScreen wiring), in `showBars` + menu-music sets (mirror the two tournament screens).
- **Entry:** replace the `TOURNAMENTS (TEMP)` button on ModeSelection → `TournamentSelection`.
- **Card tap / SIGN UP / CONTINUE** → `TournamentHoleSelection` (T8b) (carry the tournament id).
- **LEADERBOARD** (entered-finished / ended cards) → `TournamentLeaderboard` (T9).
- Flow: `ModeSelection → TournamentSelection → TournamentHoleSelection ⇄ TournamentLeaderboard`.

---

## 6. Staging
- **Stage 0 — prefabs:** `TournamentSelectionCard` base + state variants (OPEN/ENDING→SIGN UP, ENTERED→CONTINUE, ENTERED-finished→LEADERBOARD, UPCOMING, ENDED) with §3 tokens; the 4-tab TabBar. Extract per-state badge hex. *(No backend.)*
- **Stage 1 — static screen + nav:** clone HoleSelection scaffold; TabBar (visual); scroll list = one card per state (static); persistent bars + "TOURNAMENTS" title; `ScreenId` + nav wired (card→T8b, LEADERBOARD→T9); replace ModeSelection TEMP entry. **Cesar visual gate.** *(No backend.)*
- **Stage 2 — bind backend:** `GetTournaments()` → real cards; state-driven badge/entry/CTA; filter-tab logic (ALL/OPEN/PLAYING/CLOSED); live countdowns via `ITournamentClock`; SIGN UP → `Register` (RP debit + character lock). **Depends on T1→T4.**
- **Stage 3 — expand + modal:** chevron → expand-in-place (U1: rules/prize/character-lock picker), sign-up modal via `ModalController`; polish.

---

## 7. Flags / decisions
- **Tab label `PLAYING`** (Figma v7) vs GDD §16.2 `Active` → **Figma canonical**; reconcile the GDD when this ships.
- **UPCOMING & ENDED/CLOSED CTA** — confirm: disabled / "Notify" / LEADERBOARD. (Metadata shows a Sign Up Button instance on every card; render shows silver LEADERBOARD on entered-finished.)
- **Filter semantics:** ALL=all; OPEN=joinable (OPEN+ENDING, not entered); PLAYING=entered & in window; CLOSED=ENDED/CLOSED. Confirm against `TournamentState`.
- **Name font** Noto Sans JP Bold — confirm EN names render in it acceptably (or swap to Rubik for EN, Noto for JP).
- **Per-state badge hex** — Stage 0 extracts from the 6 badge nodes (only OPEN tokenized here).
