#!/usr/bin/env python3
"""PreToolUse hook: block hand-rolled play-mode captures.

Reads the Claude Code hook payload as JSON on stdin. If the tool call is the
Unity MCP `script-execute` tool AND its C# code hand-rolls a capture (reflection
into CaptureCore/CaptureHelper, SnapPlayModeSafe/SnapGameView, or the banned
ScreenCapture.* API), block it (exit 2) and tell the caller to use the sanctioned
`mcp__ai-game-developer__screenshot-game-view` MCP tool instead.

Why this exists (gacha_history, 2026-07-16): captures were repeatedly hand-rolled
via `CaptureCore.SnapPlayModeSafe` reflection instead of the dedicated MCP
screenshot tool, and kept returning the wrong (splash/title) frame. A user memory
already said "prefer typed MCP tools over raw script-execute" and was ignored, so
this converts the rule from advisory to enforced — the same reason
enforce_implementer_done.py exists.

Scope: ONLY blocks script-execute payloads that hand-roll capture. It does NOT
touch:
  - the MCP screenshot tools (different tool_name, never reaches here),
  - `EditorApplication.ExecuteMenuItem("GOLFIN/Screenshot/Capture Game View")`
    or ".../Capture/Snap Game View" (sanctioned menu path — those strings do not
    contain the banned identifiers),
  - compiled game code (coroutines calling CaptureCore live in MonoBehaviours,
    not in a script-execute payload),
  - measurement scripts (GetWorldCorners etc. — no capture identifiers).

Allow: exit 0 silently. Block: exit 2 with reason on stderr.
"""
import json
import re
import sys

# Identifiers that only appear when a script-execute payload hand-rolls a capture.
# Deliberately excludes the menu-item display strings ("Capture Game View",
# "Snap Game View" contain a space, not these identifiers) so the sanctioned
# ExecuteMenuItem path is never blocked.
BANNED = [
    "SnapPlayModeSafe",
    "SnapAtEndOfFrameAndPause",
    "SnapGameView",              # reflection/direct identifier (menu string has a space)
    "CaptureCore",
    "CaptureHelper",
    "ScreenCapture.CaptureScreenshot",
    "CaptureScreenshotAsTexture",
]


def read_payload() -> dict:
    try:
        return json.loads(sys.stdin.read() or "{}")
    except Exception:
        return {}


def main() -> int:
    payload = read_payload()
    tool_name = (payload.get("tool_name") or "")
    if not tool_name.endswith("script-execute"):
        return 0  # not our concern

    tool_input = payload.get("tool_input", {}) or {}
    # Concatenate every string value so we catch the code regardless of which
    # param field it lives in (csharpCode, className, methodName, ...).
    blob = " ".join(str(v) for v in tool_input.values() if isinstance(v, str))

    hits = [b for b in BANNED if b in blob]
    if not hits:
        return 0

    # Whitelist: an ExecuteMenuItem call to the sanctioned GOLFIN capture menu is
    # fine even if a banned identifier appears elsewhere — but only if NO direct
    # reflection/CaptureCore call is present. If the payload both ExecuteMenuItems
    # AND hand-rolls, we still block (the hand-roll is the problem).
    reflection_hits = [h for h in hits if h not in ()]  # all hits are reflection/direct
    if reflection_hits:
        print(
            "BLOCKED: this script-execute hand-rolls a play-mode capture "
            f"({', '.join(sorted(set(reflection_hits)))}).",
            file=sys.stderr,
        )
        print("", file=sys.stderr)
        print(
            "Use the dedicated MCP tool instead: "
            "mcp__ai-game-developer__screenshot-game-view "
            "(returns the Game View at native res, no editor chrome).",
            file=sys.stderr,
        )
        print(
            "If you need a saved file, run "
            'EditorApplication.ExecuteMenuItem(\"GOLFIN/Screenshot/Capture Game View\"). '
            "And capture AS A REAL USER: drive past the PLAY/login gate and navigate "
            "via real widget onClick before snapping — do not ShowScreen() a screen "
            "behind the gate. See memory reference_playmode_capture_runinbackground.",
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
