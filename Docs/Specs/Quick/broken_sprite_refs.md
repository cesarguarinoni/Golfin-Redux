# Quick — `broken_sprite_refs` (2026-09-01) · DONE

**Ask (Cesar):** "Fix the 86" — the 86 broken `Image.sprite` references surfaced while closing
`ball_data_wiring` §7, plus "make sure u did not break any link to the web admin either".

## What the 86 actually were

Not 15 scattered problems — **three groups, three different root causes.**

| # | group | count | cause | fix |
|---|---|---|---|---|
| 1 | `Bar` | **75** | `LevelUpWhite.png` was duplicated from `LevelUpBlueFill.png` and set to Sprite Mode **Single**, which destroyed the inherited sub-sprite `LevelUpBlueFill_0` @ `1614545952` that the prefabs still asked for | repointed to the same texture's Single sprite, fileID `21300000` |
| 2 | `ChevronIcon` | **6** | `Assets/Resources/UI/HoleSelection/S_HoleSel_ChevronRight.png` was deleted (~2026-05-03); the whole folder is gone | cleared to `{fileID: 0}` |
| 3 | `Map` `Border` `BG` `BGBall` `Handle` | **5** | 5 GUIDs never present in any reachable commit; both host prefabs are dead | cleared to `{fileID: 0}` |

### Group 1 — the only one that was visible breakage

`BagClubCard`'s 5 `Bar` images are on **active** GameObjects, so they were rendering a null sprite:
a flat hard-edged white block filling the whole track, instead of the rounded shaded pill at the
correct fill length. 20 serialized refs across 4 prefabs, which surface as 75 `Image` components
once nested prefab instances are walked.

The repoint is **lossless, not a guess**: the dead sub-sprite's rect was `(0,0,162,21)` — the entire
162×21 texture — identical to the Single-mode sprite now at `21300000`. Same file, same pixels.

Evidence: `Docs/Diagnostics/_capture/broken_sprite_bars_before_after.png` (whole card) and
`broken_sprite_bar_zoom.png` (one bar at 6×). Exactly **5 changed pixel bands of 10px** — the five
stat bars — and nothing else on the card.

**`ShellScene.unity` needed no edit.** Its 33 refs to fileID `1614545952` carry a *different* guid
(`7a471787…` = `LevelUpBlueFill.png`), which is still Sprite Mode **Multiple** and still defines that
sub-sprite. They resolve. No scene was opened or saved.

### Groups 2 + 3 — cleared, not invented

Group 2's GameObjects are **inactive in all 6 prefabs** and the art is deleted: restoring art for a
deliberately-disabled element would be inventing design. Group 3's two hosts
(`InGameMap.prefab`, `Spin.prefab`, both under legacy `Prefabs/Original/`) have **zero references**
from any scene or prefab. Clearing the dangling GUID removes the defect without deleting anyone's
UI structure or fabricating art.

Groups 2/3 could not be written through `PrefabUtility` — the two legacy prefabs raise
*"You are trying to save a Prefab with a missing script. This is not allowed."*, and Unity treats
"set an already-null-reading ref to null" as a no-op. Cleared by a targeted 5-line YAML edit; the
diff is 5 `m_Sprite` lines and nothing else.

## Result

```
Scanned 1404 prefabs / 1708 Image components
BROKEN Image.sprite refs: 0   (was 86)
Open scenes: 0 broken
Bars: BagClubCard 5/5, BagSwapClubCard 5/5, ItemUseClubCardGlowup 5/5,
      GachaPrizesScreen 55/55, GachaHistoryRow 5/5  -> all resolve to LevelUpWhite
```

Reserialization also persisted two previously-unwritten field defaults — `japaneseFontScale: 0`
(18×) and `textRewardsGap: 12` (1×). Both verified against their declarations
(`LocalizedText.cs:12` `= 0f`, and its own comment *"No effect when japaneseFontScale <= 0"*;
`ModeCardController.cs:71` `= 12f`). **Zero** layout, activation or transform churn in any prefab.

## Web-admin link — checked, nothing broken

| check | result |
|---|---|
| `S_Controls_Ball_*` / chevron / `LevelUpBlueFill_0` named in any `content_rows` **or** `content_drafts`, across all 20 catalogs | **0** |
| every sprite name the admin has PUBLISHED, resolved through the client's own `Resources` paths | 2469 checked, **balls 40/40, items 6/6, bags 2/2, characters 24/24** |
| admin source referencing any asset name touched here | none — the admin names catalog *columns*, never sprite assets |
| `npm test` / `tsc --noEmit` | 245 pass / exit 0 |
| `export_content.py --check` (balls) | clean, v8, no drift |

**One pre-existing gap found, NOT mine and NOT fixed:** 331 `clubs` rows name a `portraitFull`
(`Clubs/Full/Wedge-*`, `Putter-*`) that is not bundled — `Clubs/Full` has 96 sprites, all
drivers/woods/irons. I touched zero Clubs files in any of this session's four commits. It does not
make rows unrenderable: all 799 club rows carry a resolving `portraitSprite`, which is the PRIMARY
that gates `renderable`. Worth its own task.

## Also worth a separate task

- `InGameMap.prefab` and `Spin.prefab`: **zero references** and they carry **missing scripts**.
  Strong deletion candidates.
- The 331 unbundled club full-art sprites above.
