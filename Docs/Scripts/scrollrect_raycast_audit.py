"""Which ScrollRects can be dragged from anywhere in their list?

Static YAML scan (no Editor needed): for every ScrollRect in Assets/Prefabs, Assets/Resources and
ShellScene, does the ScrollRect object OR its Viewport carry an ENABLED raycastTarget graphic?
A list without one only scrolls from a press that lands on a child graphic (text, portrait,
pill) — the rankings_list_drag_anywhere defect. NO-RAYCAST is a candidate, not a verdict:
confirm in play with ScrollDragAnywhereVerify (Assets/Scripts/UI/Polish/Editor).

    python3 Docs/Scripts/scrollrect_raycast_audit.py
"""
import re, sys, glob, os
os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
GRAPHIC_CLASSES = ('UnityEngine.UI.Image','UnityEngine.UI.RawImage','TMPro.TextMeshProUGUI')
def parse(path):
    txt = open(path, encoding='utf-8', errors='replace').read()
    blocks = re.split(r'^--- !u!(\d+) &(\d+)', txt, flags=re.M)
    objs = {}
    for i in range(1, len(blocks), 3):
        objs[blocks[i+1]] = (blocks[i], blocks[i+2])
    return objs
def audit(path):
    objs = parse(path)
    go_name = {}; go_comps = {}
    for fid,(t,body) in objs.items():
        if t == '1':
            m = re.search(r'^\s*m_Name: (.*)$', body, re.M)
            go_name[fid] = m.group(1).strip() if m else '?'
            go_comps[fid] = re.findall(r'- component: \{fileID: (\d+)\}', body)
    rt_go = {}; rt_father = {}
    for fid,(t,body) in objs.items():
        if t in ('224','4'):
            g = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)
            f = re.search(r'm_Father: \{fileID: (\d+)\}', body)
            if g: rt_go[fid] = g.group(1)
            if f: rt_father[fid] = f.group(1)
    go_rt = {v:k for k,v in rt_go.items()}
    def path_of(go):
        parts=[]; rt = go_rt.get(go)
        while rt and rt != '0':
            parts.append(go_name.get(rt_go.get(rt,''),'?')); rt = rt_father.get(rt)
        return '/'.join(reversed(parts))
    def raycastable(go):
        """enabled graphic with raycastTarget=1 on this GameObject"""
        found = []
        for c in go_comps.get(go, []):
            if c not in objs: continue
            t, body = objs[c]
            if t != '114': continue
            cls = re.search(r'm_EditorClassIdentifier: (.*)$', body, re.M)
            cls = cls.group(1).strip() if cls else ''
            if not any(cls.endswith(g) for g in GRAPHIC_CLASSES): continue
            en = re.search(r'^\s*m_Enabled: (\d)', body, re.M).group(1)
            rc = re.search(r'm_RaycastTarget: (\d)', body); rc = rc.group(1) if rc else '?'
            found.append((cls.split('.')[-1], en, rc))
        ok = any(en=='1' and rc=='1' for _,en,rc in found)
        return ok, found
    out = []
    for fid,(t,body) in objs.items():
        if t != '114' or 'UnityEngine.UI.ScrollRect' not in body: continue
        g = re.search(r'm_GameObject: \{fileID: (\d+)\}', body).group(1)
        vp = re.search(r'm_Viewport: \{fileID: (\d+)\}', body).group(1)
        vp_go = rt_go.get(vp)
        self_ok, self_found = raycastable(g)
        vp_ok, vp_found = (raycastable(vp_go) if vp_go else (None, [('<viewport not in this file / nested prefab>','?','?')]))
        verdict = 'OK' if (self_ok or vp_ok) else ('UNKNOWN' if vp_ok is None else 'NO-RAYCAST')
        out.append((verdict, path_of(g), f"self={self_found} viewport={vp_found}"))
    return out
files = sorted(glob.glob('Assets/Prefabs/**/*.prefab', recursive=True) + glob.glob('Assets/Resources/**/*.prefab', recursive=True) + ['Assets/Scenes/ShellScene.unity'])
for f in files:
    try:
        rows = audit(f)
    except Exception as e:
        print(f"ERR {f}: {e}"); continue
    for v,p,d in rows:
        print(f"{v:11s} {f:60s} {p}\n{'':12s}{d}")
