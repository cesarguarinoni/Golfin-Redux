---
name: Always verify compile via MCP before declaring done
description: After every C# change, run the compile check via unity-mcp-cli script-execute before saying the task is complete
type: feedback
originSessionId: 4dbf2d84-8620-4202-96b5-c01ec83d510a
---
After every C# edit, verify compilation via MCP before declaring done:

```bash
echo '{"csharpCode": "using UnityEditor.Compilation; public class Script { public static object Main() { foreach (var a in CompilationPipeline.GetAssemblies()) if (a.name == \"<AssemblyName>\") return \"OK\"; return \"not found\"; } }", "className": "Script", "methodName": "Main"}' | npx unity-mcp-cli run-tool script-execute --input-file -
```

Also check Unity Editor log for `error CS`:
```powershell
Get-Content "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 50 | Select-String "error CS"
```

**Why:** Repeatedly shipped code with compile errors (struct null checks, missing assembly refs) and declared done without verifying. User had to report errors each time.

**How to apply:** Every single task that touches a .cs file. No exceptions. Do this BEFORE writing the done summary.
