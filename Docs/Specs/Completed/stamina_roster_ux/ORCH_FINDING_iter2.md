# Orchestrator finding — iter-2 scene-revert was INCOMPLETE

**Date:** 2026-06-30
**Author:** Orchestrator (main Claude Code thread), post-self-review iter-2.
**Effect:** STATUS forced `SELF_REVIEW_PASS` → `SELF_REVIEW_FAIL`, routing back to the implementer for iter-3.

## Why

The self-reviewer PASSed iter-2 but **under-counted the scene drift**. iter-2 reverted only
`TournamentResultModal` (GUID `08bcfc9e5603e4fe6bcb5342b2287386`). Two OTHER unrelated prefab
instances are still carrying task-introduced override drift in `Assets/Scenes/ShellScene.unity`.

**Proof it is task-introduced (not pre-existing):** the iter-1 kickoff baseline in `HEARTBEAT.log`
shows `ShellScene.unity` was **NOT** in the DIRTY list — the scene was clean against HEAD
(`0fcea9be2`) when the task started. Therefore every change in the current `git diff` of
`ShellScene.unity` was introduced by this task's scene saves. The self-reviewer's "MatchMakingModal
is pre-existing" judgement is disproven by this baseline.

## Exact drift to revert (the ONLY two offenders)

| GUID | Asset | Drift in diff | Action |
|---|---|---|---|
| `8041c091a6bba4bdebae068201a32918` | `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` | 8 NEW PrefabInstance overrides: `m_fontColor32.rgba`, `m_TextStyleHashCode`, `m_AnchoredPosition.x/.y` on child fileIDs | revert to HEAD |
| `2bd69f22d1298854f9d7905d7375fef8` | `Assets/Prefabs/UI/Matchmaking/MatchMakingModal.prefab` | `m_AnchoredPosition.y: -68 → -564` (~496px shift) + `m_AnchorMax.y` | revert to HEAD |

## Confirmed LEGIT — DO NOT touch (these are the genuine roster work)

- New **GhostBar GameObjects** — carry `m_Script` GUID `fe87c0e1cc204ed48ad3b37840f39efc`.
- **Sprite refs** `7a471787…` (LevelUpBlueFill.png) and `ee77d6ed…` (LevelUpWhite.png) — appear only
  as `m_Sprite:` assignments, **0** occurrences inside `target:` override lines. Diff lines 107-108
  are the spec's intended staminaBar sprite swap (LevelUpBlueFill → LevelUpWhite).
- `CharacterDetailPanel.cs` and its wiring — byte-identical to iter-1, already reviewer-PASSed.

## Acceptance for iter-3

After the revert, `git diff -- Assets/Scenes/ShellScene.unity` must contain **ZERO** `target:` lines
referencing `8041c091…` or `2bd69f22…` (and still zero for `08bcfc9e…`). The implementer must produce
a **complete GUID-by-GUID classification** of every `guid:` appearing in the final ShellScene diff,
each marked LEGIT (roster) or reverted — no GUID left unclassified. Verify in-engine via
`PrefabUtility.GetPropertyModifications` on both modal instances (counts back to HEAD) and confirm
`scene.isDirty` resolves correctly after a save.
