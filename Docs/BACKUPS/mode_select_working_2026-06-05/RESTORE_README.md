# Restore point — mode_select_system (working / Cesar-approved)

Captured **2026-06-05**, immediately before the card-editability QoL work
(`mode_card_inspector_colors` + `mode_card_design_preview`). At this point both
the home carousel and the full-screen Mode Select are working and Cesar-approved.

## Primary restore point (authoritative)

Git annotated tag, pushed to origin:

```
restore/mode_select_working_2026-06-05   →  commit 35874b1f
```

The mode-select implementation itself was committed at `c5635008`
(`mode_select_system: DONE`), an ancestor of the tag.

### Restore the card files from the tag (surgical, recommended)
```bash
git checkout restore/mode_select_working_2026-06-05 -- \
  Assets/Scripts/UI/ModeSelect/ \
  Assets/Prefabs/UI/ModeSelect/
```
(Do NOT `git reset --hard` to the tag — the working tree carries unrelated
pre-existing drift you'd lose. Restore only the paths you need.)

## Secondary restore point (manual, no git)

Plain copies of the files the QoL specs will modify live next to this README:

```
Scripts/  ModeCardController.cs(.meta), ModeCarouselController.cs(.meta), ModeSelectScreenController.cs(.meta)
Prefabs/  ModeCard.prefab(.meta), ModeHomeCard.prefab(.meta)
```
To restore one by hand, copy it back over the live file (Unity closed or expect a
reload prompt), e.g.:
```bash
cp Docs/Backups/mode_select_working_2026-06-05/Prefabs/ModeHomeCard.prefab \
   Assets/Prefabs/UI/ModeSelect/ModeHomeCard.prefab
```
