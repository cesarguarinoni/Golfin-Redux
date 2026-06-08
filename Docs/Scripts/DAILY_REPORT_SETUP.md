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
  rolling 24h (not "since midnight") so commits made after the 13:30 run aren't
  lost in a gap until the next day.
  ⚠️ **Task videos do NOT auto-attach.** Everything under `Docs/Specs/**/videos/`
  is git-ignored (large, regenerable — `.gitignore`, 2026-06-02), so it is never
  committed and the git auto-path can't see it. Orbit clips, trail captures, and
  any other task video must go through the manual drop folder below.
- **Manual drop folder** — `Docs/Reports/Media/`. Drop videos *or* images there
  and they'll be attached to the next report, then **deleted after a successful
  send** (`README.md`/`.gitkeep` are preserved). This is the reliable path for
  task videos. See that folder's README.
- Telegram caps uploads at **50 MB**. Oversize **videos** are **auto-compressed**
  (two-pass, *same resolution* — only the bitrate drops, ~42 MB target) and then
  sent; oversize non-video files are skipped and reported, never deleted. Auto-
  compress needs `ffmpeg`/`ffprobe`: found via `PATH` or common install dirs
  (incl. `~/.local/bin`), or set `GOLFIN_FFMPEG_PATH` / `GOLFIN_FFPROBE_PATH`.
  Because the launchd plist's `PATH` is minimal, the script probes `~/.local/bin`
  and Homebrew dirs directly so the scheduled run still finds ffmpeg.
- Bulk drops are paced (~20/min) and retry on HTTP 429, so a big batch sends
  fully instead of half-failing.

## 4. Schedule — launchd

The agent `com.golfin.dailyreport` runs the report **daily at 13:30** local time.

```bash
cp Docs/Scripts/com.golfin.dailyreport.plist ~/Library/LaunchAgents/
launchctl unload ~/Library/LaunchAgents/com.golfin.dailyreport.plist 2>/dev/null
launchctl load   ~/Library/LaunchAgents/com.golfin.dailyreport.plist
launchctl list | grep golfin          # confirm it's registered
```

Change the time by editing `StartCalendarInterval` (`Hour`/`Minute`) in the
plist, then re-copy + reload. Logs go to `Docs/Scripts/daily_report.log`.

## 5. Running it manually

```bash
cd ~/Documents/GolfinRedux
VENV=Docs/Scripts/.venv/bin/python

# Safe diagnostics: prints inputs + the media that WOULD be sent.
# No Claude call, no Telegram post, no deletion.
$VENV Docs/Scripts/daily_report.py --dry-run

# Real PREVIEW send to the TEST channel (TELEGRAM_TEST_CHAT_ID)
$VENV Docs/Scripts/daily_report.py --test

# Real send for today (PRODUCTION channel)
$VENV Docs/Scripts/daily_report.py

# Backfill a specific day / add a note
$VENV Docs/Scripts/daily_report.py --since "2026-05-27 00:00:00" --note "fixed green topology"

# Trigger the scheduled agent right now
launchctl start com.golfin.dailyreport
```

### Flags
| Flag | Effect |
|---|---|
| `--dry-run` | Print inputs + planned media; no API call, no post, no deletion |
| `--test` | Real send, but to `TELEGRAM_TEST_CHAT_ID` instead of production |
| `--no-media` | Send the text report only; skip all attachments |
| `--since <git-since>` | Override the commit window (default `midnight`) |
| `--note "<text>"` | Inject a developer note into the summary |

## 6. Troubleshooting

- **Nothing posted at 13:30** — check `Docs/Scripts/daily_report.log`. A
  `KeyError: 'ANTHROPIC_API_KEY'` means `.env` is missing or incomplete.
- **`git: command not found` in the log** — the plist sets `PATH` to
  `/usr/bin:/bin:/usr/sbin:/sbin`; `git` is at `/usr/bin/git`. If you use a
  Homebrew git, add `/opt/homebrew/bin` to the plist `PATH`.
- **Video skipped** — it's over Telegram's 50 MB upload cap. Compress it or host
  it elsewhere.
