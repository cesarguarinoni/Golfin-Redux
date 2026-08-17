# SPEC — `tournament_signup_modal`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. `STATUS.md` tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Current: `SPEC_READY`.

## Goal

Rebuild the tournament sign-up confirmation modal from the four-line summary card it is today
(Figma `13480:2479`, 978 × 531) into the full pre-entry briefing (Figma `13498:2067`
"INFO + Banner", 978 × 1411): a cross-promotion banner, the existing header and date line, the
tournament's card art beside a description blurb, a RULES block, then entry / prize and the
BACK · CONFIRM pair.

Every value except the RULES block comes from the tournament admin. RULES ships hardcoded this
pass — but **localized**, through `LocalizationManager`, not as literals in C#.

## Decisions of record (Cesar, 2026-08-17)

| # | Decision | Consequence |
|---|---|---|
| D1 | **BOTH layouts ship, from one prefab** (revised 2026-08-17) | `13498:2067` "INFO + Banner" when the tournament has a banner; `13892:3454` "INFO" when it does not. They are **not** the same layout minus one instance — the content container's top padding differs too. See §Layout variants. |
| D1b | **Title renders in Rubik, not Noto Sans JP** (Cesar, 2026-08-17) | Overrides the Figma type on `13498:2074` / `13892:3462`. Carries a Japanese-glyph risk that §Layout variants' note pins down. |
| D2 | **Banner comes from the Banners panel, assigned per tournament** | `tournaments.modal_banner_id` → a `game_banners` row with `placement = 'tournament_modal'`. Specced in `Docs/Specs/Active/game_banners/SPEC.md` §9. |
| D3 | **RULES hardcoded, localized** | Five lines, `LocalizationManager.Get` on new keys. Not admin-editable this pass. |
| D4 | **Blurb: new per-tournament EN/JA columns, with a loc key that overrides** | Same ladder shape as the title: `localize(description_key)` wins when it resolves, then JA for JP players, then EN. |

## Reference

- **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd` — Golfin Game Redux, page **Tournaments** (`13330:2582`)
- **Current:** `13480:2479` "Signup" — 978 × 531 → `reference/current_13480-2479_signup.png`
- **Target A** (banner present): `13498:2067` "INFO + Banner" — 978 × 1411 →
  `reference/target_13498-2067_info_banner.png`
- **Second target** (the no-banner state): `13892:3454` "INFO" — 978 × 1167 →
  `reference/variant_13892-3454_info_no_banner.png`
- **Related, NOT in this task:** `13894:3628` "Prize + Banner" — the post-tournament RANK / CLAIM
  modal, which shares the banner, thumbnail and blurb → `reference/related_13894-3628_prize_banner.png`

⚠️ The blurb, RULES body and "12,000 + Trophy" strings in the Figma are **mockup content**. The
blurb and prize are data; the RULES text is the only copy this task treats as canonical, and it
is reproduced verbatim in §4.

## Figma Fidelity (enumerate EVERY element — Rule 18)

Root `13498:2067`: 978 × 1411, radius **50**, **3px solid #FFFFFF** border, vertical gradient
**#133453 → #091B33**, shadow `0 10 20 rgba(0,0,0,0.4)`. Inner `Pop-Up` `13498:2068` adds a 1px
`#0A1D35` border, radius 50, `padding-bottom: 32`. Content container `13498:2070`:
`padding: 0 48 32`, **vertical gap 24** between every block below.

| # | Element | Figma node | Property → value |
|---|---|---|---|
| 1 | Cross Promotion Banner | `13892:3435` | 970 × 252, radius **20**, image `object-cover`, drop-shadow `0 4 2 rgba(0,0,0,0.25)`. First child, above the header. |
| 2 | Header group | `13498:2072` | 882 wide, column, **gap 4**, centered |
| 2a | GOLFIN PRESENTS | `13498:2073` | Rubik **SemiBold 24**; vertical gradient fill `#FFFFFF → #D1D6E0 @40% → #828FA1` |
| 2b | Title | `13498:2074` / `13892:3462` | **Rubik Bold 42**, `#FFFFFF`. ⚠️ The Figma says Noto Sans JP Bold 42; **Cesar overrode it to Rubik (D1b)** — build Rubik and expect this one row to differ from the render. See the Japanese-glyph note below. |
| 2c | Venue line | `13498:2075` | Rubik Regular **22**, `#C7D6EB`, literal double-space around the dash: `Club  -  18 Holes` |
| 3 | Date line group | `13498:2076` | row, **gap 12**, centered, all **40px** |
| 3a | Date range | `13498:2077` | Rubik SemiBold 40, `#FFFFFF` |
| 3b | Em dash | `13498:2078` | Rubik Regular 40, `#C7D6EB` |
| 3c | Countdown | `13498:2080` | Rubik SemiBold 40, `#FFFFFF` |
| 3d | 📍 pin | `13498:2079` | **hidden in the design (`hidden="true"`) — do not build it** |
| 4 | Separator ×4 | `13498:2081`, `13892:3252`, `13498:2105`, `13498:2069` | 882 wide (the top one `13498:2069` is 978 and sits above the banner), **2px**, full-bleed hairline |
| 5 | Info row | `13498:2107` | row, **gap 32**, `items-start`, 882 wide, 360 tall |
| 5a | tournament_image | `13892:3440` | **260 × 360**, radius **50**, **1px #3E7CA8** border, `object-cover` |
| 5b | Description blurb | `13892:3250` | fills remaining **590**; Rubik **Medium 30 / line-height 36 / letter-spacing −0.5**, `#FFFFFF`, vertically centered in the 360 box |
| 6 | RULES row | `13892:3254` | row, **gap 32**, `items-start`, 882 wide, 180 tall, centered as a pair |
| 6a | "RULES" label | `13892:3255` | width **127**; Rubik **SemiBold 39 / lh 54 / ls −0.24**, `#FFFFFF`, vertically centered |
| 6b | Rules body | `13892:3442` | width **396**, height 180; Rubik **Medium 30 / lh 36 / ls −0.5**, `#FFFFFF`, 5 lines, left-aligned |
| 7 | Entry pill | `13892:3445` | fill `rgba(250,199,77,0.18)`, **1px #FAC74D**, radius **22**, padding `6 16 6 14`, inner gap **8** |
| 7a | "ENTRY" | `13892:3447` | Rubik SemiBold **22**, `#FAC74D` |
| 7b | RP icon | `13892:3448` | **30 × 30**, `object-contain` |
| 7c | Fee amount | `13892:3449` | Rubik SemiBold **22**, `#FAC74D` |
| 8 | Reward row | `13892:3450` | row, gap **8**, **16px** below the pill |
| 8a | RP icon | `13892:3451` | **40 × 40** |
| 8b | Prize text | `13892:3452` | Rubik **Bold 32**, `#73E080` |
| 9 | Buttons container | `13892:3264` | 782 × 120 at `x=98, y=1259`, row, **gap 32**, sits OUTSIDE the padded content container |
| 9a | BACK | `13892:3272` | **359 × 120**, radius 20, **2px #F7F8F9** border, gradient `#FFFFFF → #D1D5DB @40% → #818EA1`; label Rubik **SemiBold 66 / lh 84 / ls −0.78**, `#1E293B`, text-shadow `0 1 0 rgba(255,255,255,0.3)`; sheen overlay on the top half, `mix-blend: hard-light` |
| 9b | CONFIRM | `13892:3265` | **391 × 120**, radius 20, **2px #FFE48B** border, gradient `#FCF195 → #D6AB42 @59.9% → #BB7F1D`; label same type, `#321506`; same sheen |

**Deltas from the current modal** — these are the changes, everything else is new:

- Banner strip (1), info row (5), RULES row (6) and three separators are **added**.
- **CANCEL → BACK.** Label and node change; the handler's behaviour does not (§5.2).
- Buttons stop being equal width: **359 / 391**, not the current symmetric pair.
- Modal height **531 → 1411** (banner) / **1167** (no banner) — see §Layout variants.
- Header, date line, entry pill and reward row are **unchanged in content** — same bindings the
  controller already does. Do not rewrite `Populate`'s existing branches; extend them.

## Layout variants — the two states are NOT one layout with a hidden row

`13498:2067` and `13892:3454` are the SAME modal in two states, but the difference is **not just
the banner instance**. Measured from both frames' metadata:

| | **A — banner present** `13498:2067` | **B — no banner** `13892:3454` |
|---|---|---|
| Frame height | **1411** | **1167** |
| Pop-Up | `13498:2068` — first child is a full-bleed **978-wide Separator** `13498:2069` | `13892:3455` — **no top separator** |
| Content container | `13498:2070`, h 1259, padding **`0 48 32`** | `13892:3457`, h 1015, padding **`32 48 32`** |
| Banner | `13892:3435`, 970 × 252 at `y=0` | absent |
| Header top | y 276 | y 32 |
| Separator 1 | y 469 | y 225 |
| Info row | y 493 | y 249 |
| Separator 2 | y 877 | y 633 |
| RULES row | y 901 | y 657 |
| Separator 3 | y 1105 | y 861 |
| Entry + Rewards | y 1129 | y 885 |
| Buttons container | y 1259 | y 1015 |

The height delta checks out exactly: **1411 − 1167 = 244 = 252 (banner) + 24 (its gap) − 32 (the
top padding B adds back)**. Everything else — type, colours, the 882 content width, the 24px
vertical rhythm, the 359 / 391 buttons — is **identical**. So:

- **One prefab, two states.** Toggling `_bannerRoot` alone gives you a 1379-tall modal with a
  32px hole at the top where the banner was. The container's **top padding must switch 0 ↔ 32**
  in the same code path that toggles the banner. Drive the height from a vertical layout group
  rather than hard-coding 1411 / 1167.
- ⚠️ **The banner ignores the side padding.** Content is 882 wide inside `padding-x: 48`; the
  banner is **970 wide with 4px side margins**. Do not let it inherit the 48px inset — it will
  come out 882 wide and visibly narrower than the design.
- The full-bleed 978 separator `13498:2069` sits at the modal's very top edge, above the banner,
  and is **not discernible in `reference/target_13498-2067_info_banner.png`**. Treat it as a
  Figma artifact: do not build it unless the A/B shows a difference, and say which you concluded
  in `IMPLEMENTER_REPORT.md`.

### ⚠️ Rubik and Japanese titles (D1b)

The title is the one field that renders **operator-supplied Japanese** — `title_ja`, and
localized names like `tourn.lomond` → `ロモンドチャンピオンシップ`. **Rubik has no CJK glyphs.**
Switching this field to Rubik without a working fallback turns every Japanese tournament name
into tofu boxes.

Verified in this project:

- `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` **already lists**
  `NotoSansJP-VariableFont_wght SDF` (guid `8f62f163976fae841ad23d559ebdf279`) in its
  `m_FallbackFontAssetTable`. **Use this asset for the title.**
- `Assets/Fonts/Rubik-SemiBold SDF.asset` has an **empty** fallback table. It would still resolve
  through the project-wide fallback in TMP Settings (same Noto guid), but that is a second-chance
  path — prefer the asset that declares it directly.

Acceptance requires a screenshot of a **Japanese** tournament title rendering correctly in this
field. This is not a formality: it is the specific thing D1b puts at risk.

## Architecture context

- **Asmdef:** `Assembly-CSharp` throughout. `Golfin.Tournaments` (`Assets/Scripts/Tournaments/`)
  gains two DTO-ish string properties on `TournamentDefinition` and must stay network-free.
- **Existing code, by path:**
  - `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` (324 lines) — extends
    `ModalController`; `Open(string tournamentId)` → `Populate(def)` → `Show()`. Its
    `OnConfirm` flow (RP pre-check → `TrySpendAsync` → `Register(id, 0L, charId)` →
    `ScreenManager.ShowScreen(_holeSelectionTarget)`) is **correct and load-bearing — do not touch it.**
  - `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` — the prefab being rebuilt.
  - `Assets/Scripts/UI/Modals/ModalController.cs` — `Show`/`Hide`/`OnShow`/`OnHide`/`OnDisable`,
    `OpenModalCount` leak guard.
  - `Assets/Scripts/Tournaments/TournamentDefinition.cs` — `Id`, `NameKey`, `Title`, `TitleJa`,
    `ClubId`, `HoleSet`, `StartUtc`, `EndUtc`, `EntryFeeRP`, `SponsorKey`, `BannerUrl`, …
  - `Assets/Scripts/TournamentsRuntime/TournamentDisplayName.cs` — the name ladder. **§3.2's
    blurb ladder mirrors it and must live beside it, not inside the controller.**
  - `Assets/Scripts/TournamentsRuntime/TournamentArtService.cs` +
    `TournamentArtPolicy.cs` — already downloads and caches `BannerUrl`. The thumbnail (5a) is
    the same URL the selection card already shows; request it the same way.
  - `Assets/Scripts/TournamentsRuntime/RemoteTournamentDtos.cs`,
    `TournamentScheduleMapper.cs` — wire DTOs and the string→UTC parsing discipline.
  - `Assets/Localization/LocalizationManager.cs` — `Get(key)`, `CurrentLanguage`,
    `OnLanguageChanged`. **The echo-check idiom** (`localized != key` means it resolved) is used
    by the venue line at `TournamentSignupModalController:276-278` and in
    `TournamentDisplayName`. Note the manager lives in the **`Golfin.Localization`** asmdef, not
    Assembly-CSharp; the global-namespace `LocalizationManager` is already reachable from both
    `TournamentDisplayName` and this controller, so no asmdef change is needed.
- **Depends on:** `game_banners` §9 for `modal_banner_id` and the `modal_banner` payload. This
  task can be built and shipped **without** the banner — see §7 sequencing.

---

## 1. Schema

New migration `playlife/backend/migrations/2026_08_17_tournament_description.sql`
(+ the usual copy into `Tools/admin-dashboard/migrations/`).

```sql
alter table public.tournaments
  add column if not exists description_en  text,
  add column if not exists description_ja  text,
  add column if not exists description_key text;
```

Three columns, deliberately **not** the existing `public.tournaments.description`. That column
predates this work (`2026_04_24_memberships_tournaments.sql:51`), is GPS-owned, already carries
GPS copy, and is single-locale. Overloading it would put two meanings in one field across two
products — the same mistake `is_active` was created to avoid when `status` was the tempting reuse.

Comment each column. `description_key` is a **build-time localization key**: it only resolves if
it shipped in this build, which is exactly why the ladder in §3.2 falls through it rather than
trusting it.

Migration first, verify over PostgREST, then deploy — `Docs/ADMIN_DASHBOARD_OPS.md` §3.2.

## 2. Backend

`backend/routers/tournaments.py::list_golfin` — add `description_en, description_ja,
description_key` to the existing `.select(...)` string. Nothing else: they are plain
pass-through strings in the per-tournament object, exactly like `title_ja`.

The `modal_banner` join is `game_banners` §9.4's, not this task's.

## 3. Client — data

### 3.1 DTO and definition

- `RemoteTournamentDtos.cs`: `[JsonProperty("description_en")] public string? DescriptionEn;`
  and the same for `description_ja`, `description_key`. Plain strings — the file-level
  `DateParseHandling.None` discipline applies as it does to `title_ja`.
- `TournamentDefinition.cs`: three new `string?` properties, defaulted null in the constructor so
  **every existing call site and test compiles untouched** (the same courtesy `TitleJa` was given).
- `TournamentScheduleMapper.cs`: pass them through. No validation — empty is a legitimate value
  and simply hides the block (§5.1).
- `TournamentCsvLoader` / the bundled `tournaments.csv`: **leave alone.** A CSV-sourced
  tournament has no blurb and renders without one. Do not add columns to the shipped CSV.

### 3.2 The blurb ladder — new file, beside the name ladder

`Assets/Scripts/TournamentsRuntime/TournamentDescription.cs`, namespace `Golfin.Tournaments`,
mirroring `TournamentDisplayName` in shape, comments and test surface:

```csharp
public static string Resolve(TournamentDefinition? def);
public static string Resolve(string? descriptionKey, string? en, string? ja);
```

Ladder, in order:

1. `descriptionKey` non-blank **and** `LocalizationManager.Get(key)` returns something that is not
   the key back → that. A shipped key is a real translation pair and outranks operator copy in
   both languages.
2. `CurrentLanguage == Language.Japanese` **and** `ja` non-blank → `ja.Trim()`.
3. `en` non-blank → `en.Trim()`.
4. `string.Empty`.

⚠️ Rung 2 is **JP-only**, exactly as `TournamentDisplayName` rung 2 is. An English player must
never fall into the Japanese blurb, even when `en` is empty — they get rung 4 and the block hides.
This asymmetry is intentional; do not "fix" it into a symmetric fallback.

Unlike the name ladder there is **no rung that returns an id**. An empty blurb hides its row; it
never renders a slug or a raw key.

## 4. Client — the RULES block (hardcoded, localized)

Six keys, added as rows to **`Assets/Localization/LocalizationText.csv`** (header
`key,English,Japanese`) and imported with **Tools → Localization → Import Text CSV**
(`Assets/Localization/Editor/LocalizationTextImporter.cs`, which rewrites
`LocalizationTextTable.asset`). Editing the `.asset` by hand is not the workflow — the CSV is the
source. Existing `tourn.*` rows start at line 184; file these beside them.

| Key | English | Japanese |
|---|---|---|
| `tourn.rules.label` | `RULES` | `ルール` |
| `tourn.rules.max_players` | `MAX PLAYERS: Unlimited` | `最大参加人数：無制限` |
| `tourn.rules.divisions` | `DIVISIONS: Level based` | `ディビジョン：レベル別` |
| `tourn.rules.per_division` | `PLAYERS PER DIVISION: 100` | `1ディビジョン：100人` |
| `tourn.rules.gear` | `GEAR :  Supplied by GOLFIN` | `ギア：GOLFIN提供` |
| `tourn.rules.characters` | `CHARACTERS:  Unrestricted` | `キャラクター：制限なし` |

Paste these into the CSV exactly as written. The Japanese uses the **full-width colon `：`**,
matching `TOURN_SPONSORED_BY,SPONSORED BY,スポンサー：` at line 279 — do not substitute an ASCII
colon. `GOLFIN` stays Latin in the JA column, as it does everywhere else in the table.

Verbatim from `13892:3442`, double spaces included. The body is built by joining the five value
keys with `\n` at runtime — **not** authored as one pre-joined string — so a JA line can be a
different length without breaking the others, and so a future admin-driven version replaces
values one at a time.

> **NOTE — review the JA before shipping.** The Japanese above was written by the Architect, not
> by a native reviewer. It is idiomatic for a game UI and follows the table's existing
> conventions, but `1ディビジョン：100人` in particular is a compression of "PLAYERS PER DIVISION"
> and is worth a second pair of eyes. Flag in `IMPLEMENTER_REPORT.md` that it is unreviewed;
> changing a CSV cell later is a one-line edit plus a re-import.

## 5. Client — the modal

### 5.1 New serialized fields on `TournamentSignupModalController`

```csharp
[Header("Cross Promotion Banner (13892:3435)")]
[SerializeField] private GameObject _bannerRoot   = null!;   // hidden when there is no banner
[SerializeField] private Image      _bannerImage  = null!;
[SerializeField] private Button     _bannerButton = null!;

[Header("Info Row (13498:2107)")]
[SerializeField] private GameObject      _infoRow          = null!;
[SerializeField] private Image           _tournamentImage  = null!;   // 260×360
[SerializeField] private TextMeshProUGUI _descriptionText  = null!;

[Header("Rules (13892:3254)")]
[SerializeField] private TextMeshProUGUI _rulesLabelText = null!;
[SerializeField] private TextMeshProUGUI _rulesBodyText  = null!;

[Header("Separators")]
[SerializeField] private List<GameObject> _separators = new List<GameObject>();
```

Plus a reference to the content container, because the banner state changes its padding:

```csharp
[Header("Layout")]
[SerializeField] private RectTransform    _contentContainer = null!;   // 13498:2070 / 13892:3457
[SerializeField] private VerticalLayoutGroup _contentLayout = null!;
```

**State switch — the banner is not a simple hide.** Per §Layout variants:

| | `_bannerRoot` | `_contentLayout.padding.top` | Resulting height |
|---|---|---|---|
| Banner present (A) | active | **0** | 1411 |
| No banner (B) | inactive | **32** | 1167 |

Both must move together, in one method, or state B renders 1379 tall with a bare 32px gap where
the banner was. Let the layout group drive height; do not hard-code either number.

**There is exactly ONE conditional layout in this modal: banner vs no banner.**

| Condition | Result |
|---|---|
| No `modal_banner` on the tournament, or its art URL fails `BannerPolicy.IsArtAllowed` | State **B** — `_bannerRoot` inactive **and** `padding.top = 32` |

⚠️ **CORRECTED 2026-08-17 (Cesar).** An earlier draft of this section made the info row collapse
when `TournamentDescription.Resolve` came back empty. **That was never asked for and is not the
behaviour.** The blurb does not drive layout. A short blurb, a long blurb or no blurb all leave
the info row exactly where it is, with the thumbnail beside it.

The only guard on the info row is the degenerate case where it would be **completely empty** —
no art *and* no blurb — which is `showRow = hasBlurb || hasArt` as built. That is an empty-row
guard, not a blurb collapse, and in practice it never fires: the thumbnail falls back to the
bundled course sprite, so `hasArt` is true for every course the game ships.

RULES never collapses — it is static content.

### 5.2 `Populate` — extend, do not rewrite

Keep every existing branch (`_sponsorText`, `_titleText`, `_venueText`, `_dateLineText`,
`_entryLabelText`, `_entryAmountText`, `_entryCoinIcon`, `_rewardCoinIcon`, `_rewardText`)
byte-for-byte. Add:

- `_descriptionText.text = TournamentDescription.Resolve(def);`. **No collapse** — see §5.1.
- `_tournamentImage` ← `TournamentArtService.Instance.Request(def.BannerUrl, s => …)`, with the
  bundled course art as the already-drawn fallback, exactly as the selection card does.
- `_rulesLabelText` / `_rulesBodyText` from §4's keys.
- Banner: `_bannerImage` ← `TournamentArtService.Banners.Request(...)`;
  `_bannerButton.onClick` → `Application.OpenURL(link)` re-checking `BannerPolicy.IsLinkAllowed`
  at the call site; `interactable = false` when there is no link.

### 5.3 Buttons

`_cancelButton` keeps its field name and its `OnCancel` handler — **only the visible label
changes to BACK.** Renaming the serialized field would break the prefab reference for zero gain;
if the name grates, that is a separate cleanup.

`OnConfirm`, `CompleteSignup`, the RP pre-check, `TrySpendAsync`, the `GetMyEntry` short-circuit
and the `TryGetTournament`-returns-null path are **untouched**. This task is presentation.

### 5.4 Prefab

`Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` — rebuild the body to §Figma Fidelity.
Reuse the existing header, entry pill and reward objects rather than deleting and re-adding them,
so their serialized references survive. Check
`Docs/Architecture/UI_ELEMENT_PALETTE.md` and `UI_HIERARCHY.md` for the project's existing
separator, pill and button prefabs before authoring new ones.

## 6. Admin dashboard

`Tools/admin-dashboard`, Tournaments panel only:

- `lib/types.ts`: `descriptionEn`, `descriptionJa`, `descriptionKey` on `TournamentRow` and
  `TournamentInput`.
- `lib/tournamentData.ts`: map all three via the existing `str()` helper, so an un-migrated DB
  yields nulls instead of throwing.
- `lib/tournamentMutations.ts`: persist on create and update; `validateInput` caps
  `description_en` / `description_ja` at **600 characters** — the Figma blurb is 268 and the box
  is fixed at 360px tall, so anything much longer overflows on device. `description_key` follows
  the same shape rule `name_key` already uses.
- `tournament-editor.tsx`, **Details** tab: two textareas (EN / JA) with a live character count,
  and a `description_key` input grouped with the existing `name_key` field. A one-line hint that
  the key wins when it resolves in the build.
- Audit rides in the existing `tournament_update` before/after snapshot.

## 7. Sequencing

The banner half depends on `game_banners`. **Build this task's §1–§6 first and ship it** — the
modal is complete and correct without a banner, because §5.1 collapses `_bannerRoot` when there
is none. Wire the banner when `game_banners` §9 lands. Do not block the modal on it.

## 8. Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item `PASS` or `FAIL` with a one-sentence justification citing what was measured.

**Fidelity** — reproduce the §Figma Fidelity table with PASS/FAIL per row, A/B'd against
**both** `reference/target_13498-2067_info_banner.png` **and**
`reference/variant_13892-3454_info_no_banner.png` at 1170 × 2532.

- [ ] Every row of the fidelity table reproduced with a verdict.
- [ ] Title renders in **Rubik Bold 42** per D1b — deliberately differing from the render, which shows Noto Sans JP.
- [ ] A tournament whose resolved title is Japanese (e.g. `tourn.lomond` → ロモンドチャンピオンシップ) renders **without tofu**, screenshot attached. Title uses `Rubik-VariableFont_wght SDF`, the asset that declares the Noto fallback.
- [ ] The hidden 📍 (`13498:2079`) was **not** built.
- [ ] BACK is 359 wide and CONFIRM is 391 — not equal.
- [ ] The banner renders **970 wide with 4px side margins**, not 882 — it does not inherit the container's 48px padding.
- [ ] Stated in the report whether the full-bleed 978 separator `13498:2069` was built, and why.

**Layout variants**

- [ ] Banner present → 1411 tall, container `padding-top: 0`, matches target A.
- [ ] No banner → **1167** tall, container `padding-top: 32`, matches target B. Not 1379 — the padding moved with the banner.
- [ ] Height is driven by the layout group, not a hard-coded 1411 / 1167 — verified by reading the prefab, not by eye.
- [ ] Toggling a tournament's banner assignment in the admin flips the modal between the two states with no other visual change.

**Data**

- [ ] Migration applied and verified over PostgREST by name.
- [ ] `GET /tournaments/golfin` carries the three description fields.
- [ ] A tournament with `description_en` only shows it to both EN and JP players.
- [ ] A tournament with both shows JA to a JP player and EN to an EN player.
- [ ] `description_ja` set, `description_en` empty, player in English → **block hides**; the JA text is never shown.
- [ ] A `description_key` that resolves beats both columns in both languages; one that does not resolve falls through and never renders the raw key.
- [ ] A tournament with **no blurb** still shows the info row and the thumbnail, unchanged in position — the blurb does not drive layout.
- [ ] The empty-row guard (`hasBlurb || hasArt`) only fires when there is neither art nor blurb, which the bundled course sprite makes unreachable for every shipped course.

**Behaviour (regression — this task must change none of it)**

- [ ] CONFIRM with sufficient RP still debits once, registers, and navigates to HoleSelection.
- [ ] CONFIRM with insufficient RP still toasts and does not register.
- [ ] Already-registered still short-circuits with no second charge.
- [ ] A tournament that leaves the schedule while the modal is open still toasts and closes.
- [ ] BACK registers nothing, charges nothing, navigates nowhere.
- [ ] `OpenModalCount` returns to its prior value after open→close (the `ModalController` leak guard).

**Rules**

- [ ] Five rules lines render from `LocalizationManager`, not literals — verified by changing one table value and seeing the modal change.
- [ ] The six keys are in `LocalizationText.csv` AND the importer has been run, so
      `LocalizationTextTable.asset` carries them — a CSV row alone is not enough.
- [ ] Switching to Japanese re-renders the block; missing JA cells fall back to EN rather than showing keys.

**Banner (only if `game_banners` §9 has landed)**

- [ ] An assigned, active banner renders at 970 × 252 radius 20 and its link opens.
- [ ] Unassigned, or the banner row switched inactive → `_bannerRoot` hidden, no gap.

**Always**

- [ ] All `[SerializeField]` references wired in the Inspector.
- [ ] No white-box placeholders in the screenshot.
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations flagged at the bottom of the report with justification.

## 9. Smoke evidence

Presentation change on a modal with a payment path behind it, so both kinds of evidence are
required (`Docs/Diagnostics/PIPELINE_LESSONS.md` Lesson O):

- Screenshots at 1170 × 2532: state A (banner + blurb); **state B (no banner)**, which must be
  1167 tall with no gap at the top; the same tournament with no blurb (info row **unchanged**); and
  one in Japanese showing a Japanese title rendering in Rubik-with-Noto-fallback.
- A human-in-the-loop CONFIRM run described in prose — RP before, RP after, where it navigated.
  A dispatch log is not sufficient for the visual half.
- EditMode tests for `TournamentDescription.Resolve` covering all four rungs and the JP-only
  asymmetry, in the same test assembly as the `TournamentDisplayName` tests.

## 10. Files this task touches

**New**

- `playlife/backend/migrations/2026_08_17_tournament_description.sql` (+ dashboard copy)
- `Assets/Scripts/TournamentsRuntime/TournamentDescription.cs`
- EditMode tests for the blurb ladder
- Six rows in `Assets/Localization/LocalizationText.csv` (§4), EN + JA, then re-import

**Modified**

- `playlife/backend/routers/tournaments.py` — three columns added to one `.select()`
- `Assets/Scripts/TournamentsRuntime/RemoteTournamentDtos.cs`, `TournamentScheduleMapper.cs`
- `Assets/Scripts/Tournaments/TournamentDefinition.cs` — three null-defaulted properties
- `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` — new fields, `Populate` extended
- `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` — rebuilt body, two-state layout,
  title switched to `Assets/Fonts/Rubik-VariableFont_wght SDF.asset`
- `Tools/admin-dashboard/lib/{types,tournamentData,tournamentMutations}.ts`,
  `app/(panels)/tournaments/tournament-editor.tsx`
- `Assets/Localization/LocalizationTextTable.asset` — regenerated by the importer, never hand-edited
- `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`

## 11. Out of scope (do NOT do these)

- **The result / CLAIM modal** `13894:3628` "Prize + Banner" and
  `TournamentResultModalController`. It shares the banner, thumbnail and blurb and is the obvious
  next task — but it is a different modal with a different action, and folding it in doubles the
  regression surface of a payment path.
- A **second prefab** for the bannerless state. One prefab, two states (§Layout variants) — the
  frames differ only by the banner and 32px of top padding, and forking them guarantees they drift.
- Any change to `OnConfirm`, `CompleteSignup`, `Register`, `TrySpendAsync`, the RP pre-check or
  the navigation target.
- Making RULES admin-editable, per-tournament, or per-league.
- Reusing or migrating `public.tournaments.description` (the GPS column).
- Uploading banner art from the tournament editor — that is `game_banners` §9.3's picker, by design.
- Adding description columns to the shipped `tournaments.csv`.
