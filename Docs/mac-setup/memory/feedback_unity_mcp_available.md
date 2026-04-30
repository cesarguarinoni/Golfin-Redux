---
name: Unity MCP tools are always available
description: Never assume Unity MCP tools are unavailable — they are always connected in this project
type: feedback
originSessionId: a2ccb569-2106-4458-9580-200e8172eb05
---
Unity MCP tools (skills like screenshot-game-view, console-get-logs, script-execute, editor-application-set-state, etc.) are ALWAYS available in this project. Do NOT assume they are missing or skip runtime verification because of that assumption.

**Why:** Cesar had to explicitly correct this multiple times. Assuming the tools are absent causes implementers to skip compile checks, play-mode verification, and screenshots — leaving tasks half-done and requiring Cesar to do the work manually.

**How to apply:** Before skipping any Unity MCP call, actually attempt the call. If it fails with a real error, report that error. Never pre-emptively declare the tools unavailable. This applies to all agents (implementer, architect, self-reviewer, main Claude Code).
