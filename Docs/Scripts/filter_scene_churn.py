#!/usr/bin/env python3
"""Drop the driven-value churn from a Unity scene diff, keeping every authored change.

Saving ShellScene after it has been opened or played re-serialises ~530 lines of values that a
LayoutGroup, TMP auto-sizing or a Scrollbar computed — anchors, sizes, font sizes, scroll
positions. None of it is authored, all of it is noise, and it buries the change under it.
This keeps a hunk only when it changes something OUTSIDE that derived set.
"""
import re, sys
CHURN = {"m_AnchorMin","m_AnchorMax","m_AnchoredPosition","m_SizeDelta","m_Pivot",
         "m_LocalPosition","m_LocalScale","m_LocalEulerAnglesHint","m_LocalRotation",
         "m_ConstrainProportionsScale","m_OffsetMin","m_OffsetMax","m_fontSize","m_Value"}

src, dst = sys.argv[1], sys.argv[2]
lines = open(src).read().split("\n")
header, hunks, cur = [], [], None
for line in lines:
    if line.startswith("@@"):
        cur = [line]; hunks.append(cur); continue
    (header if cur is None else cur).append(line)

def churn_only(h):
    body = h[1:]
    changed = [(i, l) for i, l in enumerate(body) if l.startswith("+") or l.startswith("-")]
    if not changed:
        return True
    for i, l in changed:
        m = re.match(r"[+-]\s*(-?\s*)?([A-Za-z_][A-Za-z0-9_]*):", l)
        key = m.group(2) if m else None
        if key in CHURN:
            continue
        if key == "value":                       # a PrefabInstance modification
            prop = None
            for j in range(i - 1, -1, -1):
                mm = re.match(r"[ +-]\s*propertyPath:\s*(.+)$", body[j])
                if mm:
                    prop = mm.group(1).strip(); break
            if prop and prop.split(".")[0] in CHURN:
                continue
        return False
    return True

kept = [h for h in hunks if not churn_only(h)]
print(f"hunks {len(hunks)} -> kept {len(kept)} (dropped {len(hunks)-len(kept)} churn-only)")
out = "\n".join(header) + "\n" + "\n".join("\n".join(h) for h in kept)
open(dst, "w").write(out if out.endswith("\n") else out + "\n")
