#!/usr/bin/env python3
"""
Tools/unity-mcp-call.py — call a Unity MCP tool over HTTP from the shell.

WHY THIS EXISTS: the MCP tool surface can vanish from an agent session (a machine reset, a
transport drop, a plugin reload) while the Unity Editor and its MCP endpoint are perfectly
healthy. "Tools missing" is not "Unity down". This does the JSON-RPC handshake the endpoint
requires — initialize -> Mcp-Session-Id -> notifications/initialized -> tools/call — so work can
continue without the tools being re-registered.

  ./Tools/unity-mcp-call.py tools/list
  ./Tools/unity-mcp-call.py call <toolName> '<json-args>'
  ./Tools/unity-mcp-call.py exec <file.cs> <ClassName> <MethodName>

URL is read from .mcp.json so a re-pinned port needs no edit here.
"""
import json, sys, os, urllib.request, re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
URL = json.load(open(os.path.join(ROOT, ".mcp.json")))["mcpServers"]["ai-game-developer"]["url"]
SESSION = {"id": None}

def post(payload, notify=False):
    body = json.dumps(payload).encode()
    req = urllib.request.Request(URL, data=body, method="POST")
    req.add_header("Content-Type", "application/json")
    req.add_header("Accept", "application/json, text/event-stream")
    if SESSION["id"]:
        req.add_header("Mcp-Session-Id", SESSION["id"])
    with urllib.request.urlopen(req, timeout=300) as r:
        sid = r.headers.get("Mcp-Session-Id")
        if sid:
            SESSION["id"] = sid
        raw = r.read().decode("utf-8", "replace")
    if notify or not raw.strip():
        return None
    # The endpoint may answer as SSE ("data: {...}") or as a plain JSON body.
    for line in raw.splitlines():
        line = line.strip()
        if line.startswith("data:"):
            line = line[5:].strip()
        if line.startswith("{"):
            try:
                return json.loads(line)
            except json.JSONDecodeError:
                continue
    return {"raw": raw}

def handshake():
    post({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
        "protocolVersion": "2024-11-05",
        "capabilities": {},
        "clientInfo": {"name": "unity-mcp-call", "version": "1"}}})
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, notify=True)

def main():
    if len(sys.argv) < 2:
        print(__doc__); return 2
    handshake()
    cmd = sys.argv[1]

    if cmd == "tools/list":
        res = post({"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}})
        names = [t["name"] for t in res.get("result", {}).get("tools", [])]
        print(f"{len(names)} tools")
        for n in names: print("  " + n)
        return 0

    if cmd == "call":
        name, args = sys.argv[2], json.loads(sys.argv[3]) if len(sys.argv) > 3 else {}
    elif cmd == "exec":
        name = "script-execute"
        args = {"csharpCode": open(sys.argv[2]).read(),
                "className": sys.argv[3], "methodName": sys.argv[4]}
    else:
        print("unknown command: " + cmd); return 2

    res = post({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                "params": {"name": name, "arguments": args}})
    print(json.dumps(res, indent=2)[:4000])
    return 0

sys.exit(main())
