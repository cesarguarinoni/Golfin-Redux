DONE

Task: publish_blocked_catalogs
Approved by Cesar 2026-09-02 ("Done"), after he checked the one open question from the
report — whether the PRO mission tab being non-interactable was a defect. It is not:
`BuildTierPillListeners` sets `interactable = P.IsTierUnlocked(tier)`, `mission_tiers.csv`
gives Amateur/Pro/Legend `unlockClears = 8`, and this save has cleared 1 Beginner mission.
Mission 24 is therefore behind two correct gates (the Pro tier, 16 clears away, and its own
`unlock=clear:23`), which is also why the §7 in-round screenshot could not be taken.

Implementation commit: bbf9996e3
Close-out commit:      see git log for this folder's move to Completed/
Live admin build stamp (sidebar footer, admin.golfin.world): bbf9996e3 (clean, not -DIRTY)
Cloudflare Worker version: 33d07d75-705c-404d-9ad3-624cf10e8ed9

mission_loadouts  Published v1 -> v2   (0 added, 1 changed, 0 deactivated)
gacha_pools       Published v2         (already at v2; drafts match published, nothing to
                                        publish — see IMPLEMENTER_REPORT § Deviations D2)

npm test 286/286 (11 files) · EditMode 2209 passed, 0 failed, 3 pre-existing skips
export_content.py --check: clean
