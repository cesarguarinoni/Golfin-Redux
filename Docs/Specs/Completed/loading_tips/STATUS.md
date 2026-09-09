DONE

Task: loading_tips
Approved by Cesar 2026-09-09.
Updated: 2026-09-09 by Claude Code (direct implementation, not the subagent chain — Cesar asked for it in one pass)
Iteration: iter-1. Two in-flight Cesar fixes, both reproduced/root-caused/fixed: the image outline (§ Rejection follow-up) and the Tip_LEVELUP cost, 805 RP -> 41 RP (§ Fix 2). No admin-side work outstanding.
Gates: GolfinRedux.Tests.EditMode 296 passed / 0 failed; Golfin.UI.Polish.Tests 162 passed / 0 failed.
Video: videos/loading_tips.mp4 (63s, 1170x2532, real hole-load path) — also in Docs/Reports/Media/loading_tips/.
Recording it exposed a third defect, now fixed: NextTip advanced the sequencer TWICE per tap
(tips 1,3,5,7 only) because UiMotion.Then's tail re-entered Run on the same handle. See
IMPLEMENTER_REPORT.md § Daily-report video.
Texts published: texts v50; export_content.py --check clean.
Post-ship: Cesar found the card collapsing on the SECOND hole load in build 2833 (a fresh-enable
height measured as 0 and eased to). Fixed in 3496f452a — see IMPLEMENTER_REPORT.md § Post-ship fix.
Build 2833 carries the defect; the fix is on main and in no binary yet.
Open: acceptance item 11 is PARTIAL — the LegacyBootHome loading screen never appears on a signed-in dev boot, so only the HoleLoad target was exercised.
