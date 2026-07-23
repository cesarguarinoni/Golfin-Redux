# SPEC — `localize_shellscene`

> **Authoritative spec.** Implementer reads this and ONLY this. STATUS.md tracks pipeline state.

## Status

`SPEC_READY`.

## Goal

Localize the genuinely-**unbound** static UI labels baked into **`ShellScene.unity`** (the main scene where screens live as scene GameObjects) by attaching `LocalizedText` binders to them. This is the deferred follow-up from batch 6. **This is the highest-risk task of the localization effort — it mutates the boot-critical main scene** — so it is scoped tightly and gated hard on boot-safety.

**What's already handled (do NOT re-do):**
- The **Settings modal is already fully bound** (21 `SETTINGS_` keys wired to LocalizedText in the scene) — leave it. Its JP was already fixed separately.
- Many screens are **already code-localized** by their controllers (the Persistent/Home pilot proved HomeScreen's text is `Get()`-driven) — those GOs need NO binder.
- ~30 LocalizedText binders already exist in ShellScene.

**This task's scope — ONLY labels that are BOTH (a) genuinely unbound (the GO has no LocalizedText and no controller writes its `.text`) AND (b) map to an EXISTING CSV key (no new keys this task).** Deferring new-key labels keeps this first scene-edit pass lowest-risk and highest-confidence. Most of batch-6's "~67 static labels" now have existing keys with real JP.

## Step 0 — Build the verified unbound inventory (READ-ONLY, no scene edit)

Cross-reference batch-6's LIKELY_STATIC_NEEDS_SCENE_BINDER list (see `Docs/Specs/Completed/localize_other/IMPLEMENTER_REPORT.md` § Deferred) against reality:
1. For each candidate `m_text` static label, find its GameObject in ShellScene and check: does it ALREADY have a `LocalizedText` component? (grep the scene / gameobject-component-list-all). If yes → SKIP (already bound).
2. Does a controller write its `.text` at runtime? (grep controllers for the field/GO). If yes → SKIP (code-localized; a binder would fight the write — the 5a scar).
3. Does an EXISTING CSV key match its English EXACTLY (incl. casing)? If yes → it's IN SCOPE (bind to that key). If it needs a NEW key → DEFER to a follow-up (out of scope here), document.

Produce a `## Unbound inventory` table: label → GameObject path → verdict (BIND-to-KEY / SKIP-already-bound / SKIP-code-localized / DEFER-needs-new-key).

**Reuse-key map (existing keys with real JP — verify EN-exact per label):** stat labels `STRENGTH→ROSTER_STRENGTH`, `CLUB CONTROL→ROSTER_CLUB_CONTROL`, `RECOVERY→ROSTER_RECOVERY`, `STAMINA→ROSTER_STAMINA`, `POWER→CLUB_POWER`, `ACCURACY→CLUB_ACCURACY`, `LOFT→CLUB_LOFT`, `DURABILITY→CLUB_DURABILITY`, `DISTANCE→CLUB_DISTANCE`, `LIE RESIST.→CLUB_LIE_RESISTANCE`; rarities `COMMON/UNCOMMON/RARE/MYTHIC/LEGENDARY/SUPREME→RARITY_*`; actions `PLAY→BTN_START`, `CANCEL→MODAL_CANCEL`, `CLOSE→SETTINGS_CLOSE`, `LEVEL UP→ROSTER_LEVEL_UP`, `SWAP→ROSTER_SWAP`, `NEXT HOLE→HOME_NEXT_HOLE`, `COST→MODAL_COST`, `LOCKED→UI_LOCKED`; tabs `DAILY→RANK_DAILY`, `WEEKLY→RANK_WEEKLY`, `MONTHLY→RANK_MONTHLY`, `HISTORY→RANK_HISTORY`, `ALL→TOURN_FILTER_ALL`, `INFO→CLUB_INFO` (verify exists), `BIO→?` (defer if no key); nav `TOURNAMENTS→NAV_TOURNAMENTS`, `MAINTENANCE NOTICE→HOME_MAINTENANCE_TITLE`, `BOOST→?`. For each, CONFIRM the existing key's EN matches EXACTLY before binding — if casing differs (e.g. `STRENGHT` typo variant, `Level Up` vs `LEVEL UP`), DEFER, do not force a mismatched reuse.

## Step 1 — Bind (scene mutation) — the ONLY mutation allowed

For each IN-SCOPE label, attach `LocalizedText` via `LocalizationEditorHelper.AddLocalizedText(go, key)` (the sanctioned helper) and set the key. Save the scene. **Nothing else may change in the scene.**

## ⚠️ HARD BOOT-SAFETY + SCENE-INTEGRITY GATES (this is the whole risk)

1. **`git diff HEAD -- Assets/Scenes/ShellScene.unity` must show ONLY added `LocalizedText` MonoBehaviour blocks + their `m_Component` entries.** ZERO `m_IsActive:` changes, ZERO `m_Enabled` toggles, ZERO RectTransform position/anchor/sizeDelta/scale changes, ZERO reparenting (`m_Father`), ZERO deletions, ZERO changes to boot-critical containers (`ScreensRoot`, `PersistentUI`, any manager GO). If the diff touches anything but added-component blocks, HARD FAIL. Quote the diff shape.
2. **Boot proof:** after saving, boot the app through the REAL flow (title → PLAY) and confirm Home loads and is interactive — capture it. If the app doesn't boot to a working Home, HARD FAIL (revert the scene).
3. **No GameObject deactivated:** explicitly grep the diff for `m_IsActive: 0` — must be absent.
4. Follow CLAUDE.md capture rules + the orchestrator scene-mutation guardrail (§14). Use Unity-API component-add (not raw YAML edits).

## Recipe / JP / anti-fabrication

- Real JP already exists for all in-scope reuse keys (no `[JP-TODO]`). Per the new policy, any string you touch must have real JP — since this task reuses only existing (already-real-JP) keys, no translation needed here.
- Never bind a controller-written label (5a scar). Never reuse a key whose EN casing differs (batch-3 scar).
- Anti-fabrication: EN/JP captures byte-distinct real play-mode; keep the folder clean; capture code-site JP-first (N/A here — no code-site). `[JP-TODO]` overflow N/A (real JP). Gates md5 + open JP.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] **`## Unbound inventory`** table: every batch-6 static candidate verdicted (BIND / SKIP-bound / SKIP-code-localized / DEFER-new-key). Primary deliverable — corrects the batch-6 over-count.
- [ ] **Binders added** only for in-scope (unbound + existing-key + EN-exact) labels; live component `key` read-back quoted; reuse-casing EN-exact verdict per label.
- [ ] **HARD scene-integrity gate:** `git diff HEAD -- ShellScene.unity` is added-LocalizedText-blocks-ONLY; zero m_IsActive/position/anchor/reparent/deletion; boot-critical containers untouched. Quote the diff summary + a `grep 'm_IsActive: 0'` = empty proof.
- [ ] **Boot proof:** real title→PLAY→Home boot capture (EN) + a JP capture of a screen whose newly-bound labels render real Japanese (stat labels ストレングス/パワー, rarities, etc.). Byte-distinct, real.
- [ ] **Scope:** git status shows ONLY `ShellScene.unity` (+ task folder). NO CSV/table change (reusing existing keys — no new keys), NO other prefab/scene/script, NO Physics/asmdef/builder. Quote it.
- [ ] Compiles clean; app boots; no missing-key errors in console for the bound keys. HEARTBEAT baseline.
- [ ] `## Deferred` — labels needing new keys (for a follow-up), and any code-localized/blocked ones.
- [ ] Spec deviations flagged.

## Not a Figma task

No Figma node — Rules 16/17/18/21 N/A. Visual gate: EN unchanged, JP renders real Japanese on the newly-bound labels, no layout shift, **app still boots**.

## Out of scope / Deferred

- Labels needing NEW keys (follow-up scene-binder pass); Account screens (`login_signup_screens` owns them); gameplay-asmdef strings; dev/debug/test scenes; the already-bound Settings; any non-ShellScene asset; inventing Japanese; asmdef changes; `Assets/Scripts/Physics/`; `M_Splash*.mat`. **No scene change beyond adding LocalizedText components.**

---
