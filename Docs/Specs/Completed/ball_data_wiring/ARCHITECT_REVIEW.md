# ARCHITECT_REVIEW — `ball_data_wiring` (+ `ball_art_and_stats`)

**Verdict: PASS** — consistent with Cesar's approval of 2026-09-01. Reviewed 2026-09-01 by the
Cowork/Architect session that ran the art batch and wrote the spec. Claims below were re-checked
against the working tree, not taken from the report.

## Verified against the repo

| Claim | Checked | Result |
|---|---|---|
| `Balls.csv` 20 rows, `rarity` after `brand`, tiers per the approved sheet | `cut -d, -f1,4` over the file | 20/20 tiers match `BALL_IDENTITY.md`; histogram 5/6/5/3/1 |
| §7 remedy applied — `thumbnailSprite` repointed to 200×200 PascalCase copies | CSV columns 10/11 + `ls Resources/Balls/Thumbnails` | 18 × `<Name>.png` present; `thumbnailSprite == fullSprite` stem for all 20; `S_Controls_Ball_GOLFIN.png` kept (the hardcoded shot-UI fallback) |
| `stats.csv` + `LoadStatCoefficients()` retired | `ls Resources/Physics`, grep | file gone; zero non-comment hits |
| `BallWindCutPerPoint` 0.02 | `StatCoefficients.cs:44` | 0.02 with the house-style comment |
| `BallDataRuntime.rarity` via `ClubCsvParser.ParseRarity` | `BallDatabaseCSV.cs:203`, `BallData.cs:26` | as specced, no second parser |
| Dedicated `/balls` admin panel | `app/(panels)/balls/`, `registry.ts:64` | present, after `clubs` |
| Commits | `git log` | `b4d21ba2c` (catalog + art), `bd028f744` (§7 thumbnails), `f84d2dd3e` (close-out) |

The three unanticipated defects Code fixed (Default-texture import, 5.95× unmipped thumbnail
downscale, blank `rarity` passing REQUIRED) are all real and all correctly attributed; the
first would have shipped 18 non-renderable balls and is the most valuable thing in the report.
The mock-mode admin screenshots are an honest limitation (password sign-in), not a gap in the work.

## Open items carried out of this task (none block it)

1. **Negative Wind is a dead stat.** `StatModifierResolver.cs:85` clamps `windCutFraction` to
   `[0, WindCutMax]`, so a ball with `windResistance < 0` behaves exactly like 0 — Code's
   perceptibility table shows Wind −10 bit-identical to Wind 0. Two shipped rows are built on the
   negative: `ball_ace_attire` (−4) and `ball_cirq` (−4), and both blurbs promise it ("gets shoved
   around in the wind", "genuinely wild once the wind picks up"). Today those sentences are not true
   and both balls net +4 above their printed budget in practice (rule 2 still holds — each carries
   a real −5/−6 Spin). Two ways to close it, both small, **Cesar's call**:
   - **(a) Physics — make Wind symmetric.** Clamp to `[−WindCutMax, +WindCutMax]` and let a negative
     fraction *add* wind scale in `BallSimulation` where `WindCutFraction` is subtracted (one sign
     path + one resolver test + F17). The UI already draws the stat as −10..+10, so this is what
     the bar promises. Recommended.
   - **(b) Data — retire the negatives.** Move the −4 on those two rows onto another stat, rewrite
     the two blurbs (EN+JA), re-import `balls,texts`. No physics change.
2. **0.02 vs 0.03** — the numbers are in the report: at 8 m/s crosswind a max-Wind ball recovers
   1.96 m of 9.44 m push at 0.02, 2.92 m at 0.03; 0.03 is the value where +10 fills the 0.30 cap,
   the same shape Rebound (Order 417) and Roll (F8) have. 0.02 is what Cesar chose and what shipped;
   revisit only if the lane still feels invisible on device.
3. **Play-mode pass not run** (shared Editor). The catalog-level facts are proven; what remains is
   opening one new ball's detail panel in EN and JA on device. Note Code's correct observation that
   the carousel is an *inventory* view (`GetAllOwnedBallIds`), so "20 entries" needs the balls
   granted — the acceptance line was mis-specified, not missed.
4. **Live `/balls` render** needs Cesar's admin sign-in once — the 20 rows are proven published
   (balls v8), only the browser render of them is unconfirmed.
5. `Golfin.Gameplay.Tests` — one deleted test method (T8, which read the retired CSV); the
   assembly compiles. Worth a green run whenever the Editor is next uncontended.

## Spec lessons (for the next runner)

- §7 predicted the wrong failure (layout push) — the real one was filtering. Next time a spec puts
  a 5× oversized sprite in front of a UI, say "measure the draw size against the source size" rather
  than guessing which symptom appears.
- "The carousel shows N entries" is only a valid acceptance line for a catalog view; check whether
  the screen reads inventory before writing it.
- §9's "open them in the Editor so the .meta files exist" should have said "and confirm
  `textureType = Sprite`" — Unity's default import is Default texture, and `Resources.Load<Sprite>`
  fails silently on it.
