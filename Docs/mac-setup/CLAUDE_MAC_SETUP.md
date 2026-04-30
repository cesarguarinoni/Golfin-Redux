# Claude Code Mac Setup — GolfinRedux

> **Instructions for Claude Code on the new Mac:**
> Read this file in full, then execute every step below without asking for confirmation.
> This is a one-time migration setup from a Windows machine.

---

## What this file does

Migrates two things that are NOT in the git repo:

1. **Memory files** — project-specific memory Claude has accumulated about this codebase
2. **settings.local.json** — machine-local Claude Code config (MCP server enable flags)

---

## Step-by-step — execute all of these

### Step 1 — Create the memory directory

```bash
mkdir -p ~/.claude/projects/Users-cesar-Documents-GolfinRedux/memory
```

> This is Claude Code's path-encoded key for `~/Documents/GolfinRedux` on Mac.

### Step 2 — Copy memory files

Copy every `.md` file from `memory/` (in the same folder as this file) into:
```
~/.claude/projects/Users-cesar-Documents-GolfinRedux/memory/
```

The files to copy:
- `MEMORY.md` (the index — must be present)
- `bug_water_color_physicslab.md`
- `feedback_check_play_mode.md`
- `feedback_compile_check.md`
- `feedback_mcp_script_execute.md`
- `feedback_push_to_github.md`
- `feedback_screenshot_timing.md`
- `feedback_screenshot_workflow.md`
- `feedback_session_signoff.md`
- `feedback_uhole_geo_regen.md`
- `feedback_unity_mcp_available.md`
- `project_folder.md`
- `project_scene_ground_provider.md`
- `project_tee_skirt_resolved.md`
- `user_role.md`

### Step 3 — Write settings.local.json

Create `~/Documents/GolfinRedux/.claude/settings.local.json` with this exact content:

```json
{
  "enabledMcpjsonServers": [
    "ai-game-developer"
  ],
  "enableAllProjectMcpServers": true
}
```

### Step 4 — Update the project_folder memory

The `project_folder.md` memory was written on Windows and references a Windows path.
Open `~/.claude/projects/Users-cesar-Documents-GolfinRedux/memory/project_folder.md`
and update the working directory path to `~/Documents/GolfinRedux`.

### Step 5 — Verify

Confirm:
- [ ] `~/.claude/projects/Users-cesar-Documents-GolfinRedux/memory/MEMORY.md` exists
- [ ] `~/Documents/GolfinRedux/.claude/settings.local.json` exists with the content above
- [ ] `~/Documents/GolfinRedux/.claude/settings.json` exists (it's in git — should already be there after `git clone`)

### Step 6 — Done

Report back to Cesar with a one-line summary: "Setup complete — memory (N files) and settings.local.json written."

---

## Notes

- `settings.json` (hooks config) is committed to git — no action needed, it's already correct.
- The `UserPromptSubmit` hook in `settings.json` uses `python -c "print(...)"` which works on Mac without changes.
- All other hooks (`route_subagent.py`, `enforce_implementer_done.py`) call `python` — make sure Python 3 is on PATH as `python` or adjust to `python3` if needed.
- MCP servers (Unity AI Game Developer, Figma, etc.) need to be re-authorized on the Mac separately — that's done through Claude Code settings, not this file.
