---
name: Use MCP Skill directly for script-execute, not JSON files
description: Prefer the Skill tool or stdin pipe over tmp JSON files for script-execute calls
type: feedback
originSessionId: 4dbf2d84-8620-4202-96b5-c01ec83d510a
---
Use the `script-execute` MCP skill directly (via Skill tool or `echo '...' | npx unity-mcp-cli run-tool script-execute --input-file -`) instead of writing intermediate tmp JSON files. JSON files are not faster and add noise to the repo.

**Why:** User pointed out the JSON file approach is unnecessary overhead — it's the same underlying MCP tool call either way.

**How to apply:** For short/simple C# snippets, pipe via stdin. For complex multi-line code, use a heredoc or write to a temp file only if absolutely necessary to avoid shell escaping issues. Never leave tmp_*.json files in the project root.
