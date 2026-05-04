# Pre-Cesar-polish snapshot — 2026-05-04T03:38:47Z

Cesar is taking over visual polish manually after iter5. This file pins the
"as-shipped-by-pipeline" state so a later diff can isolate exactly what he
changed vs what landed through the implementer/architect chain.

## Git ref
- branch: `main`
- commit: `0a852a99db4699640b6b7ae7ad07f7e26976f2e0`
- tag (created by this snapshot): `hole-selection-iter5-pre-polish`

## To diff later
```bash
# What Cesar changed in the prefab:
git diff hole-selection-iter5-pre-polish..HEAD -- Assets/Prefabs/UI/HoleSelection/HoleCard.prefab

# What Cesar changed in the scene's HoleSelectionScreen subtree:
git diff hole-selection-iter5-pre-polish..HEAD -- Assets/Scenes/ShellScene.unity

# What changed everywhere relevant:
git diff hole-selection-iter5-pre-polish..HEAD -- \
  Assets/Prefabs/UI/HoleSelection/ \
  Assets/Scenes/ShellScene.unity \
  Assets/Scripts/UI/HoleSelection/ \
  Assets/Art/HoleSelectScreen/ \
  Assets/Resources/UI/HoleSelection/ \
  Assets/Resources/HoleImages/
```

## Snapshot files in this folder
- `collapsed_screen.png` — pre-polish full-screen collapsed view
- `expanded_hole1_play.png` — pre-polish Hole 1 expanded, gold PLAY button
- `matchmaking_from_play.png` — pre-polish matchmaking modal opening

## Quick metric refs (helpful for later regression checks)
- HoleCard.prefab:     5441 lines of YAML
- ShellScene.unity HoleSelectionScreen GameObject starts at line 11318
- 5 art assets in `Assets/Art/HoleSelectScreen/`: Arrow.png, Background.png, Button - Play.png, Button - Replay.png, Lock.png

## What was working at this snapshot (per architect-review)
- Functional pipeline: nav → screen → 18 cards → expand → PLAY → modal ✓
- Cesar's 8 iter-4 corrections all PASS visually
- Visual fidelity vs Figma reference: GAP. Cesar to address.
