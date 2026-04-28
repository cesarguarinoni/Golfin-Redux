# Tomorrow's Kickoff

Paste the content of this file into a fresh Claude.ai chat to resume work. The full project context lives in memory — this kickoff just orients on where to start.

---

## Paste this into a fresh Claude.ai chat

```
# GOLFIN Redux — resuming after 2026-04-28 session

## Where we left off

Spent yesterday building the multi-agent pipeline (.claude/agents + hooks + per-task folders) and ran the first task through it: 8_3_topbar (top-bar HUD: settings + player card + hole card). Three iterations:

- Iteration 1: rejected by self-reviewer for player chip text issues
- Iteration 2: passed architect-review but I (Cesar) manually rejected for missing rounded corners + chips touching at center
- Iteration 3: in progress when I closed the day. Implementer attempted rounded corners with PNG mask, fell back to UISprite (unacceptable for production). Chip width fix from 298 to 248 was applied. I'll fix the rounded corners manually in Unity today.

Workflow improvements landed yesterday:
- New states: CESAR_REJECTED, IMPLEMENTER_BLOCKED
- HEARTBEAT.log convention so route hook can detect stuck sessions
- Email notification path (opt-in) alongside toast + always-on alerts.log
- Circuit breakers for the implementer (3 failures, 3 min wait, 2 search attempts → BLOCKED)
- Implementer prompt: wait 3s+ before screenshot, read CESAR_REJECTION.md if STATUS contradicts review file
- Lightweight workflow at Docs/Specs/Quick/ for small tasks where the full pipeline is overkill
- Lessons file at Docs/Diagnostics/PIPELINE_LESSONS.md (J entries A through J recorded)

## Today's plan

**First order of business: investigate the Figma-vs-Unity size mismatch.** Spec at Docs/Specs/Queued/FIGMA_UNITY_SIZE_MISMATCH.md. Symptom: Unity 180×180 renders at Figma-equivalent ~216×216 (~1.20× too big). Five hypotheses ranked there, leading candidate is CanvasScaler MatchWidthOrHeight. This affects every UI spec going forward; fix the root cause once.

**Second order of business: continue UI implementation using the workflow.** Probably 8_3_topbar wrap-up first (Cesar will fix rounded corners manually, then we move to Completed/), then 8_4 (wind/hole indicators) or wherever we want to go next.

**Side issues to address when convenient:**
- Toast notifications never appeared on Windows (need to diagnose: focus assist, notifications setting, or PS script issue)
- Code "stopped there" after subagent stops (no auto-chain) — may be a Claude Code orchestrator config thing
- Cross-platform port (Mac + 2nd PC) — refactor route_subagent.py for OS detection
- Periodic heartbeat is implemented as a file, but no separate watchdog process (route hook only checks staleness when fired)
```

---

## Files to read at session start

The new chat may not have full context. Once it loads, these will catch you up:

1. `Docs/AI_CONTEXT.md` — overall project state (already updated yesterday)
2. `Docs/Diagnostics/PIPELINE_LESSONS.md` — what we learned from yesterday's iterations
3. `Docs/Specs/Queued/FIGMA_UNITY_SIZE_MISMATCH.md` — the size investigation
4. `Docs/Specs/Active/8_3_topbar/` — current state of the inaugural task (may be DONE by then if Cesar approves)
5. `CLAUDE.md` § Multi-Agent Workflow — the workflow rules

## Quick reference

- Pipeline state for any task: `cat Docs/Specs/Active/<task>/STATUS.md`
- Latest alerts: `Get-Content .claude\alerts.log -Tail 10`
- Live alert tail: `Get-Content .claude\alerts.log -Wait -Tail 5`
- Heartbeat tail (during a run): `Get-Content Docs/Specs/Active/<task>/HEARTBEAT.log -Wait -Tail 5`
