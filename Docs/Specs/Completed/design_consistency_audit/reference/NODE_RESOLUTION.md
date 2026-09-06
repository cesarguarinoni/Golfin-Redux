# Node table resolution — Phase 0 (A0), Rule 9 re-pull

File `5gEAHjl6xAtW8iYY7NMvWd`. Every row of the SPEC's node table re-pulled before use.

## The shape — this is ONE defect, not a list of one-offs

**Checkable question:** *does the id in the SPEC node table's screen column resolve to a
1170×2532 frame?* **Seven rows failed.** The column holds a canvas, a card, a component, a
side-arrow asset, a row, or a page instead of the screen frame. Every id below was therefore
confirmed by reading `original_width`/`original_height` off the render call itself — not by
trusting the table, and not by eye.

| SPEC row | SPEC id | What that id actually is | USE THIS | render |
|---|---|---|---|---|
| HomeScreen | `13994:1935` | frame ✅ | `13994:1935` | ✅ |
| HomeScreen (notice) | `2098:8490` | frame ✅ | `2098:8490` | ✅ |
| **ModeSelectionScreen** | `13027:10222` | ❌ **carousel side-arrow ASSET, 30×60** (`mode_select_system`: "assets `13027:10222`/`10223`") | `13026:1924` | ✅ |
| **HoleSelectionScreen** | `12885:87551` | ❌ **canvas 3846×8343**, 2 variants + loose assets | `12961:1694` | ✅ |
| **HoleSelection/HoleCard** | `12961:1694` | ❌ **that is the SCREEN frame** | cards `12961:1728`…`1733` | — |
| **MissionSelectionScreen** | `4002:6036` | ❌ not the screen state | `4065:7960` NEXT (`4065:7961` REPLAY) | ✅ |
| TournamentSelectionScreen | `13386:1758` | frame ✅ | `13386:1758` | ✅ |
| TournamentHoleSelectionScreen | `13414:2936` | frame ✅ | `13414:2936` | ✅ |
| **TournamentLeaderboardScreen** | `13414:4041` | ❌ **LOCKED card, 978×164** | `13414:5598` | ✅ |
| RankingsScreen | `4079:1726` | frame ✅ | `4079:1726` | ✅ |
| RosterScreen | `4065:14998` | frame ✅ | `4065:14998` | ✅ |
| Roster Compare | `4300:63876` | frame ✅ | `4300:63876` | ✅ |
| Inventory · Clubs | `4065:9071` | frame ✅ | `4065:9071` | ✅ |
| **Inventory · Bags** | `2563:18880` | ❌ **page** — the parenthetical was the frame | `12754:40669` | ✅ |
| Inventory · Balls | `2636:1972` | frame ✅ | `2636:1972` | ✅ |
| **Inventory · Items** | `4063:393` | ❌ **canvas 5697×3031** | `4065:13487` "Items Screen -Menu" | ✅ |
| GeneralShopScreen | `4079:28230` | frame ✅ | `4079:28230` | ✅ |
| StaminaShopSelection | `13156:1178` | frame ✅ | `13156:1178` | ✅ |
| StaminaShopDetail | `13330:1139` | frame ✅ | `13330:1139` | ✅ |
| **GachaHistoryScreen** | `13622:21105` | ❌ **a `Rankings Card` component** (per the completed `gacha_history` spec) | `4079:18306` | ✅ |
| GachaPrizesScreen | `13622:2222` | frame ✅ | `13622:2222` | ✅ |
| SettingsScreen | `4065:16939` | frame ✅ | `4065:16939` | ✅ |
| PersistentUI · Top UI | `2098:8493` | strip 1170×321 ✅ | `2098:8493` | ✅ |
| PersistentUI · Nav Bar | `2098:7988` | strip 1170×273 ✅ | `2098:7988` | ✅ |
| TournamentResultModal | `13498:2067` | 1018×1451 ✅ | `13498:2067` | ✅ |
| VersusResultModal | `13274:877` | frame ✅ | `13274:877` | ✅ |
| MatchMakingModal | `12813:77056` | frame ✅ | `12813:77056` | ✅ |
| InGameSettingsModal | `13873:33610` | frame ✅ | `13873:33610` | ✅ |
| StartingCharacterConfirmModal | `13924:41976` | frame ✅ | `13924:41976` | ✅ |
| TournamentSignupModal | `13480:2479` | 1020×574 ✅ | `13480:2479` | ⚠️ see below |

## A0 honest limit — one render cannot reach the ≥1024 floor

`TournamentSignupModal` `13480:2479` is **1020×574 natural**. Figma renders at most 1:1 —
requesting `maxDimension: 2040` returns 1020×574 again (verified). The floor is therefore
unreachable for this node without upscaling, which would fabricate resolution rather than add
detail. The render ships at native 1:1, 4 px under the floor, and is flagged here rather than
silently upscaled.

## Still to resolve (not blocking Phase 1)

`GeneralShopCard` `13509:2978`; Settings submenus `4065:16941` Sounds / `16942` Language /
`16940` User Profile / `16946` About; Character Level Up page `4059:5509` and Club Level Up page
`4056:1542` (both PAGE ids — need the same metadata resolution); `GachaRatesModal`,
`HoleCompleteModal`, `Toast` (no id given in the SPEC at all). Tier-2 rows need no render
(SPEC: "inventory + lint only, no crop sheet").
