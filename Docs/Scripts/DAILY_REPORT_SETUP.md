# GOLFIN Daily Report — Setup (macOS)

`Docs/Scripts/daily_report.py` pulls the day's git commits (+ optional Notion
tasks), has Claude Sonnet write a bilingual EN/JP summary, posts it to Telegram,
then attaches the day's videos and any media you dropped in the media folder.

This doc covers the **macOS** install. (The original lived on a Windows PC and
used Task Scheduler; on Mac we use a `launchd` agent.)

---

## 1. Python virtual environment

A venv lives at `Docs/Scripts/.venv` (git-ignored). Create / recreate it with:

```bash
cd ~/Documents/GolfinRedux
python3 -m venv Docs/Scripts/.venv
Docs/Scripts/.venv/bin/python -m pip install --upgrade pip
Docs/Scripts/.venv/bin/python -m pip install anthropic requests python-dotenv
```

## 2. Secrets — `.env`

```bash
cp Docs/Scripts/.env.example Docs/Scripts/.env
```

Then edit `Docs/Scripts/.env` and fill in:

| Var | Required | What |
|---|---|---|
| `ANTHROPIC_API_KEY` | ✅ | Claude API key (summarization) |
| `TELEGRAM_BOT_TOKEN` | ✅ | BotFather token for the report bot |
| `TELEGRAM_CHAT_ID` | ✅ | Production chat/channel id (scheduled report) |
| `TELEGRAM_TEST_CHAT_ID` | — | Separate channel for `--test` preview sends |
| `GOLFIN_REPO_PATH` | — | Defaults to two levels up from the script |
| `GOLFIN_REPORT_MEDIA_DIR` | — | Defaults to `<repo>/Docs/Reports/Media` |
| `NOTION_TOKEN`, `NOTION_DATABASE_ID` | — | Leave blank to skip the task tracker |

`.env` is git-ignored and is loaded explicitly from the script's own folder, so
it works under `launchd` (whose working directory differs).

## 3. Media attachments

- **Git videos (last 24h)** — any `.mp4/.mov/.webm/...` added or modified in
  commits in the **last 24 hours** are attached automatically. The window is a
  rolling 24h (not "since midnight") so commits made after the 20:30 run aren't
  lost in a gap until the next day.
  ⚠️ **Task videos do NOT auto-attach.** Everything under `Docs/Specs/**/videos/`
  is git-ignored (large, regenerable — `.gitignore`, 2026-06-02), so it is never
  committed and the git auto-path can't see it. Orbit clips, trail captures, and
  any other task video must go through the manual drop folder below.
- **Manual drop folder** — `Docs/Reports/Media/`. Drop **any file** there
  (videos and images go as media; `.docx`/`.pdf`/`.csv`/`.zip`/… go as
  documents — no extension filter) and it'll be attached to the next report,
  then **deleted after a successful send** (`README.md`/`.gitkeep` are
  preserved). This is also the reliable path for task videos. See that folder's
  README.
- Telegram caps uploads at **50 MB**. Oversize **videos** are **auto-compressed**
  (two-pass, *same resolution* — only the bitrate drops, ~42 MB target) and then
  sent; oversize non-video files are skipped and reported, never deleted. Auto-
  compress needs `ffmpeg`/`ffprobe`: found via `PATH` or common install dirs
  (incl. `~/.local/bin`), or set `GOLFIN_FFMPEG_PATH` / `GOLFIN_FFPROBE_PATH`.
  Because the launchd plist's `PATH` is minimal, the script probes `~/.local/bin`
  and Homebrew dirs directly so the scheduled run still finds ffmpeg.
- Bulk drops are paced (~20/min) and retry on HTTP 429, so a big batch sends
  fully instead of half-failing.
- **Unattached-media notice** — if anything still can't be attached (an
  uncompressible oversize file, or an upload that failed after retries), the
  tool posts a short follow-up message to the chat listing it, so recipients
  know it didn't go out instead of only seeing a `[SKIP]` in the local log.

## 4. Schedule — launchd (POLL MODEL, 2026-07-15)

The agent `com.golfin.dailyreport` **polls every 30 min** (`StartInterval 1800`).
The **script** decides when to actually send: only at/after **20:30** (`SEND_AFTER`
in `daily_report.py`), and only the **first** poll of the evening posts — the rest
skip via the dedupe marker. Effectively: the report goes out on the first poll
at/after 20:30 each weekday.

```bash
cp Docs/Scripts/com.golfin.dailyreport.plist ~/Library/LaunchAgents/
launchctl unload ~/Library/LaunchAgents/com.golfin.dailyreport.plist 2>/dev/null
launchctl load   ~/Library/LaunchAgents/com.golfin.dailyreport.plist
launchctl print gui/$(id -u)/com.golfin.dailyreport | grep 'run interval'  # -> 1800 seconds
```

Change the send time by editing `SEND_AFTER` in `daily_report.py` (not the plist).
Change the poll frequency via `StartInterval` in the plist. Logs go to
`Docs/Scripts/daily_report.log`.

> **Why a poll, not `StartCalendarInterval`?** The calendar trigger no-showed at
> 20:30 on **three** weekday evenings running (2026-07-13/14/15) — the first night
> the Mac slept through it, but the next two the Mac was **awake**, the job was
> **armed** (`Hour 20 / Minute 30`, `watching = 1`), and launchd *still* never
> fired; it only ever ran as a useless ~03:30 catch-up that never re-armed. A poll
> has no single instant to miss and needs no re-arm, so it can't fail that way.

Three script-side guards make the poll correct (all in `daily_report.py`, all
bypassed by `--force` / `--test` / an explicit `--since`):

1. **Weekend skip (Cesar rule, 2026-06-13).** No send Sat/Sun; those commits fold
   into **Monday's** report (72h git window back to Friday). Script-side, not a
   launchd `Weekday` key, so it's testable and survives re-installs.
2. **Send window (2026-07-15).** An automatic run before 20:30 is a no-op, so the
   all-day polling never posts early and an off-hours catch-up (e.g. 03:30) can't
   post at all.
3. **Dedupe (2026-07-14).** Each production send stamps `Docs/Scripts/.last_sent`
   (git-ignored); an automatic run **skips if a send happened within the last 12h**
   (`DEDUPE_WINDOW`). This is what makes the 30-min polling safe — first evening
   poll sends, the rest skip. `--test` never writes the marker (test channel).

   > ⚠️ The dedupe window is an **interval**, deliberately *not* a same-calendar-day
   > check. A same-day rule is **wrong**: a 03:30 catch-up and that evening's real
   > 20:30 run share a date but are 17h apart — a same-day rule silently swallows
   > the real send (it did exactly that on 2026-07-14 before being corrected). 12h
   > cleanly separates "a replay of the run I just did" from "the next daily run".

**Optional belt-and-braces:** so a poll is guaranteed to run inside the window even
if the Mac would otherwise sleep through the evening, wake it just before 20:30:
`sudo pmset repeat wakeorpoweron MTWRF 20:29:00` (verify with `pmset -g sched`).

## 5. Running it manually

```bash
cd ~/Documents/GolfinRedux
VENV=Docs/Scripts/.venv/bin/python

# Safe diagnostics: prints inputs + the media that WOULD be sent.
# No Claude call, no Telegram post, no deletion.
$VENV Docs/Scripts/daily_report.py --dry-run

# Real PREVIEW send to the TEST channel (TELEGRAM_TEST_CHAT_ID)
$VENV Docs/Scripts/daily_report.py --test

# Real send for today (PRODUCTION channel). On a weekend this no-ops (folds into
# Monday); add --force to send anyway.
$VENV Docs/Scripts/daily_report.py
$VENV Docs/Scripts/daily_report.py --force      # send even on Sat/Sun

# Backfill a specific day / add a note
$VENV Docs/Scripts/daily_report.py --since "2026-05-27 00:00:00" --note "fixed green topology"

# Trigger the scheduled agent right now
launchctl start com.golfin.dailyreport
```

### Flags
| Flag | Effect |
|---|---|
| `--dry-run` | Print inputs + planned media; no API call, no post, no deletion (on a weekend, shows it would skip) |
| `--test` | Real send, but to `TELEGRAM_TEST_CHAT_ID` instead of production (also bypasses the weekend skip) |
| `--no-media` | Send the text report only; skip all attachments |
| `--since <git-since>` | Override the commit window (default is weekday-aware: 24h, or 72h on Monday). An explicit value also bypasses the weekend skip |
| `--note "<text>"` | Inject a developer note into the summary |
| `--force` | Send even on a Saturday/Sunday (bypass the weekend skip), bypass the send-window, **and bypass the duplicate guard** (force a send now) |

## 6. Troubleshooting

- **Nothing posted this evening** — work through these in order (poll model):
  1. **Day?** Saturday/Sunday don't send (folded into Monday).
  2. **Is the poll running at all?** `launchctl print gui/$(id -u)/com.golfin.dailyreport | grep -E 'run interval|runs'`.
     `run interval` should be `1800 seconds`; `runs` should climb by ~1 every 30
     min the Mac is awake. If it's not incrementing, the agent isn't loaded —
     re-copy the plist and `launchctl load` it (§4). If the Mac was **asleep** all
     evening no poll could run; add the `pmset` wake in §4.
  3. **What did the last poll decide?** `tail Docs/Scripts/daily_report.log`:
     - `Before the 20:30 send time … no-op` → normal for daytime polls; it'll send
       after 20:30.
     - `Already sent … — skipping` → it already went out within 12h (check the
       channel). Force another with `--force`, or delete `Docs/Scripts/.last_sent`.
  4. Otherwise read the log for errors; a `KeyError: 'ANTHROPIC_API_KEY'` means
     `.env` is missing or incomplete.
- **Report fired at a weird hour (e.g. 03:30)** — this was the old
  `StartCalendarInterval` failure mode (launchd replaying a missed job on the next
  wake). The poll model (§4) can't do this: an automatic run before 20:30 is a
  no-op, so an early-morning poll never posts. If you still see an off-hours post,
  it was a `--force`/manual run.
- **`git: command not found` in the log** — the plist sets `PATH` to
  `/usr/bin:/bin:/usr/sbin:/sbin`; `git` is at `/usr/bin/git`. If you use a
  Homebrew git, add `/opt/homebrew/bin` to the plist `PATH`.
- **Oversize video** — videos over Telegram's 50 MB cap are auto-compressed
  (same resolution) and sent; if compression can't get it under 50 MB (or
  ffmpeg is missing), it's skipped and a notice is posted to the chat.
