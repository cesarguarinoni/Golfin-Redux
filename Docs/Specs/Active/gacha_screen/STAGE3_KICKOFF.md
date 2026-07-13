# Stage 3 kickoff (2026-07-13) — bot-video polish gate

**Stages 0/1/2 APPROVED + committed** (Stage 2 = `70c8581bf`). This stage is the VIDEO deliverable
for Cesar's final accept, then move to Completed.

## Deliverable (SPEC §4 Stage 3)
ONE captioned bot-recorded demo clip at **full 1170×2532** showing, in order:
1. Open the Rewards Center on the **GACHA** tab (ticket counter shows 10).
2. **Swipe across banners** — show snap-to-center + neighbor falloff (scale/dim), dots updating.
3. **Countdown visibly ticking** — linger ~2-3s on the ENDS IN pill so the seconds decrement on screen.
4. **Tap RULES & RATES** — the `Application.OpenURL(rulesUrl)` attempt logged in the editor (show/log it).
5. **Tap PULL x10** — the "Coming soon" stub toast appears; ticket balance stays 10 (no spend).

## Rules (from memory)
- **Reuse the sanctioned recorder** — `BotVideoRecorder` + the Tournament/Rankings `DemoRecorder` family.
  Do NOT hand-stitch stills or PNG-sequence. (reference_ui_demo_recorder_family)
- **Full 1170×2532**, never 250x540/540p (a downscaled MP4 won't inline-preview). (feedback_record_bot_video_full_size, reference_bot_video_inline_preview)
- Drive the REAL widgets (real TabBar/carousel swipe/RulesButton/PullX10 onClick) — no test-only hooks. (real-entry rule)
- **Caption** the clip with `Docs/Scripts/build_bot_video.py` (textfile drawtext idiom) — no hand-rolled inline drawtext. (reference_video_caption_tool)
- Output the MP4 to `Docs/Specs/Active/gacha_screen/videos/` (clips → videos/, not screenshots/). (convention_videos_vs_screenshots)
- Report the FULL absolute path + parent folder of the clip. (feedback_always_include_full_video_path)
- Extract 1 representative still per new video into screenshots/ for the chat surface.

No production code/layout changes this stage — capture only. Leave editor clean.
