# IMPLEMENTER_REPORT — safe_area_top_bar (smoke #2)

**Iteration shape:** shell_top_bar:notch_inset
**Design v4 (2026-08-05):** CENTER-ONLY, baseline-relative nudge. Only the center ticket cluster (TicketIcon, TicketCountText, ShopPlusButton + runtime pill) sits under the centered Dynamic Island, so ONLY it moves. RP counter (top-left), Settings (top-right) and UsernameText flank the Island and stay put on topBarPanel. The fitter subtracts the iPhone 14 inset (141px) and applies only the EXCESS: iPhone 14 → 0, 14 Pro Max → 36px (12pt delta), no-notch → 0. NO full inset; NO bg extend; bottom nav UNTOUCHED.
**v3→v4:** moved RP cluster + Settings back to topBarPanel; TopBarContent now holds only the 3 ticket elements. Code: `EnsureTicketPill` clones the RP-pill template from topBarPanel; `ApplyDemoTopBarTrim` finds RewardPointsBackground on topBarPanel; `SetTopBarChromeVisible` iterates BOTH topBarPanel (skip UsernameText) + topBarContent.

**⚠️ Scene-save discipline (learned the hard way, twice):** ANY layout rebuild in the in-memory scene before a save bakes ~4600 lines of anchor/size churn across the whole scene. Triggers: entering play mode, `Canvas.ForceUpdateCanvases()`, activating an inactive UI subtree in edit mode. Rule: do the structural surgery → SAVE immediately → do all play-mode/force-update VERIFICATION afterwards, and `git checkout` + reload before any subsequent save. The committed diff must be verified small (here: 105/17).

## v1 → v2 (what changed after rejection)
v1 reparented BOTH bars' full-height content strips into a full safe-area inset. Cesar rejected it:
- Content moved "way too far down" — the full 141/177px inset shifted the whole 321px strip off its own banner (leak).
- Extending the background would make the banner touch on-screen content — not viable.
- The bottom nav should never have been touched (smoke #2 is the top bar / Dynamic Island only).

v2:
- **Top bar:** only the 7 chrome elements (RP pill/icon/text, settings, ticket icon/count, shop+) move into `TopBarContent`, nudged down a small capped amount by a **capped** SafeAreaFitter (`_maxInsetPixels = 72`). The banner (321px, full-bleed) is unchanged and still covers the nudged chrome — no leak, no bg extend. `UsernameText`/nameplate stays on `TopBar` (excluded — it sits low and would separate from its tab).
- **Bottom nav:** fully reverted to HEAD — zero changes.

## Files modified
| File | Change |
|---|---|
| `Assets/Scripts/UI/Core/SafeAreaFitter.cs` | +`_baselineInsetPixels` (0 = full inset/original; >0 applies only the excess beyond a baseline). Top bar sets 141 (iPhone 14 inset). |
| `Assets/Scripts/UI/PersistentUIManager.cs` | +`topBarContent` ref; `ShowTopBar` toggles it; `SetTopBarChromeVisible`/`ApplyDemoTopBarTrim` retarget to it. (No `bottomNavContent` — reverted.) |
| `Assets/Scenes/ShellScene.unity` | +SafeArea(fitter,cap=72)+TopBarContent(7 chrome, inactive); 7 chrome reparented; UsernameText stays on TopBar; bottom nav untouched. Diff 105/17. |

## Scene diff = intended only (105 ins / 17 del)
SafeArea node (fitter, `_maxInsetPixels:72`) + TopBarContent (7 chrome) + 7 `m_Father` repoints + TopBar `m_Children` reduced to `[UsernameText]` + canvas gains SafeArea + PersistentUIManager `topBarContent` ref. Bottom nav: ZERO hunks. One benign TMP self-heal on the moved TicketCountText (`m_sharedMaterial 0→font default` [already the runtime fallback], `m_isOrthographic 0→1` [correct], font-features cosmetic) — serialization-only, zero visual impact.
NOTE: v1's earlier save baked a 4600-line layout churn (play-mode/activation → save). Recovered by `git checkout HEAD -- ShellScene.unity` + clean re-surgery in pure edit mode; scene never play-mode-saved again.

## Verification (simulated 72px nudge, play mode, numeric — no scene save after)
| Element | Measured | Verdict |
|---|---|---|
| TopBar bg | top=2532 bottom=2211 (unchanged, full-bleed) | PASS |
| Chrome (RP/settings/ticket/shop+) | tops ~205px from screen top | clears 14 Pro Max Dynamic Island (177px) by ~28px |
| Chrome bottoms | ~259px from top (< 321 bar) | on the banner, NO leak |
| UsernameText (on TopBar) | 2297→2237 unchanged | nameplate stays put |
| BottomNav bg + NavHome | 196→0 / 176→20 unchanged | bottom nav untouched |
| Editor (full safe area) | topInset 0 → nudge 0 | layout unchanged |

`_maxInsetPixels=72` clears both the iPhone-14 notch (141px) and the 14 Pro Max Island (177px) because the chrome already had ~120px headroom in the tall banner. Cap is trivially tunable if Cesar wants more/less.

## Real-sim status
v1's sim build failed: `Builds/iOS-Sim` was `SDKROOT=iphoneos` (append export doesn't flip SDK) → no simulator destination. Running a FULL sim-SDK re-export now to regenerate the project for `iphonesimulator`, then headless build + launch + real capture on iPhone 14 sim.

STATUS: v2 implemented + numerically verified; real-sim render in progress; awaiting Cesar device confirm on 14 Pro Max.
