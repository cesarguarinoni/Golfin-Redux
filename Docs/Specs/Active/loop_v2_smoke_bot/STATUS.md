# STATUS — loop_v2_smoke_bot

**Status:** SPEC_READY (architect, 2026-05-19)
**Type:** TELLCODE — pattern matches existing SmokeRunner§2f exactly. Bot drives PRODUCTION flow (ShellScene PLAY → matchmaking → gameplay → InCup) instead of lab flow.
**Parent:** `Docs/Specs/Active/loop_v2_scope/SPEC.md` (inserted between C0 and C1)
**Notion:** Loop v2 Order 335

## Why this exists
Stage C0 unlocked the production playthrough. Stages C1/D/E/F each carry a Cesar visual gate that means "play through the loop and confirm the new feature works." Without a bot, every gate burns 30+ minutes of manual play across multiple iterations. Bot pays for itself by Stage D.

## Pattern alignment
- Asmdef: `Golfin.Physics.Viewer` (same as §2c-§2f hosts)
- `#if UNITY_EDITOR` guarded throughout
- Two-file pattern: `LoopV2SmokeBot.cs` (host MonoBehaviour, mirrors `SmokeRunner2fHost`) + `Editor/LoopV2SmokeBotMenu.cs` (mirrors `SmokeRunner2fMenu`)
- Capture path: `CaptureCore.SnapPlayModeSafe` exclusively (no Lesson K traps)
- SessionState armed flag + self-destruct + timeScale=1 guard + WaitForSecondsRealtime (all §2f lessons inherited)

## Key behavior
- 8 captures: home, matchmaking searching, opponent found, loading, gameplay armed, ball-in-cup, result modal, history log
- Each capture MD5-distinct (proven on §2f pattern)
- Failure modes: log + capture-current + abort with clean self-destruct (still writes history.log)

## Reusability
After this ships, the bot becomes the **default visual gate** for Stages C1/D/E/F. Cesar reviews captures rather than playing manually. Bot may be extended in Stage D (PLAY NEXT button press) and Stage E (parameterized hole number), but the C1 visual gate uses the bot as-is.
