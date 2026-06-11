#!/usr/bin/env python3
"""
GOLFIN Daily Development Report
Pulls git commits + Notion tasks, summarizes via Claude Sonnet, posts to Telegram.
Includes Japan holiday/weekend awareness.

Media attachments (added 2026-05-28, Mac setup):
  After the text report is posted, the tool also sends media to the same chat:
    1. Any video file (.mp4/.mov/.webm/...) added or modified in the last 24h of
       git commits.
       CAVEAT: task videos under Docs/Specs/**/videos/ are git-ignored (large,
       regenerable — see .gitignore, added 2026-06-02). Git-ignored videos are
       never committed, so this auto-path CANNOT see them. To attach a task
       video (orbit clip, trail capture, etc.), copy it into the media drop
       folder below — that is the only reliable path for task videos.
    2. Any video OR image you drop into the media folder (default: Docs/Reports/Media/).
       Drop-folder files are DELETED after a successful send (README.md/.gitkeep are kept).
  Telegram's Bot API caps uploads at 50 MB. Oversize VIDEOS are auto-compressed
  (two-pass, same resolution) to fit and then sent; oversize non-video files are
  skipped (and reported), never deleted. Auto-compress needs ffmpeg/ffprobe —
  found via PATH or the common install dirs (incl. ~/.local/bin), or override
  with GOLFIN_FFMPEG_PATH / GOLFIN_FFPROBE_PATH.

See DAILY_REPORT_SETUP.md for installation and configuration (venv, .env, launchd).
"""

import argparse
import os
import shutil
import subprocess
import sys
import tempfile
import time
from datetime import datetime, date

import anthropic
import requests

from dotenv import load_dotenv

# Load .env that sits NEXT TO this script (robust under launchd, where the CWD
# is not the script directory). override=True so the .env wins even when the
# launching environment already exports an (often empty) ANTHROPIC_API_KEY.
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
load_dotenv(os.path.join(SCRIPT_DIR, ".env"), override=True)
load_dotenv()  # fallback upward search; does not override the script-dir .env

# --- Config from environment variables ---
# Read lazily (no KeyError at import) so --dry-run / --help work without secrets.
# A real send validates these in main() and fails fast with a clear message.
ANTHROPIC_API_KEY = os.environ.get("ANTHROPIC_API_KEY", "")
TELEGRAM_BOT_TOKEN = os.environ.get("TELEGRAM_BOT_TOKEN", "")
TELEGRAM_CHAT_ID = os.environ.get("TELEGRAM_CHAT_ID", "")
# Separate channel for --test / preview sends, so the PRODUCTION channel only
# ever receives the scheduled daily report.
TELEGRAM_TEST_CHAT_ID = os.environ.get("TELEGRAM_TEST_CHAT_ID", "")

# Every send this run targets ACTIVE_CHAT_ID; main() flips it to the test chat
# when --test is passed.
ACTIVE_CHAT_ID = TELEGRAM_CHAT_ID

# Repo defaults to two levels up from this script (Docs/Scripts/ -> repo root),
# so it works on any machine without setting GOLFIN_REPO_PATH.
DEFAULT_REPO_PATH = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
REPO_PATH = os.environ.get("GOLFIN_REPO_PATH", DEFAULT_REPO_PATH)

# Manual drop folder for videos/images you want included in today's report.
REPORT_MEDIA_DIR = os.environ.get(
    "GOLFIN_REPORT_MEDIA_DIR", os.path.join(REPO_PATH, "Docs", "Reports", "Media")
)

# --- Notion config (optional — leave empty to skip) ---
NOTION_TOKEN = os.environ.get("NOTION_TOKEN", "")
NOTION_DATABASE_ID = os.environ.get("NOTION_DATABASE_ID", "")

# --- Notion column names (customize to match your database) ---
NOTION_TASK_NAME_COLUMN = os.environ.get("NOTION_TASK_NAME_COLUMN", "Name")
NOTION_STATUS_COLUMN = os.environ.get("NOTION_STATUS_COLUMN", "Status")

# --- Map your Notion status values to report categories ---
DONE_STATUSES = ["Done", "Complete", "Completed"]
IN_PROGRESS_STATUSES = ["In Progress", "In progress", "Doing"]
TODO_STATUSES = ["To Do", "To do", "Not Started", "Not started", "Planned"]

# --- Media / Telegram upload constraints ---
# Telegram Bot API hard cap on uploaded files is 50 MB.
TELEGRAM_MAX_UPLOAD_BYTES = 50 * 1024 * 1024
# Oversize videos are auto-compressed (two-pass, same resolution) to this target
# before sending. Kept ~8 MB under the 50 MB cap so x264's typical few-percent
# two-pass overshoot never crosses the limit (a 74 MB clip lands ~43 MB).
# Added 2026-06-08 after a 74 MB "Mode Selection.mp4" was skipped.
COMPRESS_TARGET_BYTES = 42 * 1024 * 1024
# Audio bitrate budget reserved when computing the video bitrate for compression.
COMPRESS_AUDIO_KBPS = 96
VIDEO_EXTS = {".mp4", ".mov", ".webm", ".m4v", ".avi", ".mkv"}
IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".webp"}
ANIM_EXTS = {".gif"}
MEDIA_EXTS = VIDEO_EXTS | IMAGE_EXTS | ANIM_EXTS
# Files in the drop folder that are part of the repo scaffold, never sent/deleted.
DROP_FOLDER_KEEP = {"README.md", ".gitkeep", ".DS_Store"}

# --- Media send pacing / rate-limit handling ---
# Telegram throttles bulk media to a group/channel (~20 messages per minute).
# The original tool fired every file in a tight loop with no pacing or retry, so
# a big drop (28 videos on 2026-06-02) blew past the limit: file 7 onward all
# came back HTTP 429 "Too Many Requests" and were silently dropped — sent=False,
# so never deleted. Pace consecutive uploads AND honour the server's retry_after
# so the whole batch goes out instead of half-failing.
MEDIA_SEND_INTERVAL_SEC = 3.0      # gap between consecutive media uploads (~20/min)
MEDIA_MAX_RETRIES = 5              # attempts per file before giving up
# Statuses worth retrying: 429 (rate limit) + transient gateway/server errors.
RETRYABLE_STATUSES = {429, 500, 502, 503, 504}

# --- Japan Public Holidays 2026 ---
JAPAN_HOLIDAYS_2026 = {
    date(2026, 1, 1): ("New Year's Day", "元日"),
    date(2026, 1, 12): ("Coming of Age Day", "成人の日"),
    date(2026, 2, 11): ("National Foundation Day", "建国記念の日"),
    date(2026, 2, 23): ("Emperor's Birthday", "天皇誕生日"),
    date(2026, 3, 20): ("Vernal Equinox Day", "春分の日"),
    date(2026, 4, 29): ("Showa Day", "昭和の日"),
    date(2026, 5, 3): ("Constitution Memorial Day", "憲法記念日"),
    date(2026, 5, 4): ("Greenery Day", "みどりの日"),
    date(2026, 5, 5): ("Children's Day", "こどもの日"),
    date(2026, 5, 6): ("Constitution Memorial Day (observed)", "憲法記念日（振替休日）"),
    date(2026, 7, 20): ("Marine Day", "海の日"),
    date(2026, 8, 11): ("Mountain Day", "山の日"),
    date(2026, 9, 21): ("Respect for the Aged Day", "敬老の日"),
    date(2026, 9, 22): ("Silver Week Holiday", "国民の休日"),
    date(2026, 9, 23): ("Autumnal Equinox Day", "秋分の日"),
    date(2026, 10, 12): ("Sports Day", "スポーツの日"),
    date(2026, 11, 3): ("Culture Day", "文化の日"),
    date(2026, 11, 23): ("Labor Thanksgiving Day", "勤労感謝の日"),
}


def get_day_note() -> str:
    """Return a note about weekends or Japan holidays for today."""
    today = date.today()
    notes = []

    # Check weekend
    if today.weekday() == 5:
        notes.append("🗓 Saturday (weekend)")
    elif today.weekday() == 6:
        notes.append("🗓 Sunday (weekend)")

    # Check Japan holiday
    if today in JAPAN_HOLIDAYS_2026:
        en_name, ja_name = JAPAN_HOLIDAYS_2026[today]
        notes.append(f"🇯🇵 Japan Holiday: {en_name} / {ja_name}")

    return "\n".join(notes) if notes else ""


def get_todays_commits(since: str = "24 hours ago") -> str:
    """Pull git log from the repo — chronological order (oldest first)."""
    result = subprocess.run(
        ["git", "log", f"--since={since}", "--reverse", "--format=%h %s (%an, %ar)", "--no-merges"],
        cwd=REPO_PATH,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def get_commit_count(since: str = "24 hours ago") -> int:
    """Count commits since the given time."""
    result = subprocess.run(
        ["git", "rev-list", "--count", f"--since={since}", "HEAD", "--no-merges"],
        cwd=REPO_PATH,
        capture_output=True,
        text=True,
    )
    try:
        return int(result.stdout.strip())
    except ValueError:
        return 0


def get_changed_files(since: str = "24 hours ago") -> str:
    """Get a summary of files changed since the given time."""
    result = subprocess.run(
        ["git", "log", f"--since={since}", "--reverse", "--stat", "--format=", "--no-merges"],
        cwd=REPO_PATH,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def get_todays_videos(since: str = "24 hours ago") -> list:
    """
    Return absolute paths of video files added/modified in commits since `since`.
    --diff-filter=d excludes deletions (lowercase d = "not deleted").
    Only files that still exist on disk are returned, de-duplicated, sorted.
    """
    result = subprocess.run(
        ["git", "log", f"--since={since}", "--name-only", "--diff-filter=d",
         "--format=", "--no-merges"],
        cwd=REPO_PATH,
        capture_output=True,
        text=True,
    )
    found = []
    seen = set()
    for line in result.stdout.splitlines():
        rel = line.strip()
        if not rel:
            continue
        if os.path.splitext(rel)[1].lower() not in VIDEO_EXTS:
            continue
        abs_path = os.path.normpath(os.path.join(REPO_PATH, rel))
        real = os.path.realpath(abs_path)
        if real in seen:
            continue
        if os.path.isfile(abs_path):
            seen.add(real)
            found.append(abs_path)
    return sorted(found)


def collect_drop_media(media_dir: str) -> list:
    """
    Return absolute paths of media files dropped into `media_dir` (non-recursive).
    Skips the repo scaffold files (README.md, .gitkeep, .DS_Store) and any
    extension we don't recognise as media.
    """
    if not os.path.isdir(media_dir):
        return []
    found = []
    for name in sorted(os.listdir(media_dir)):
        if name in DROP_FOLDER_KEEP or name.startswith("."):
            continue
        path = os.path.join(media_dir, name)
        if not os.path.isfile(path):
            continue
        if os.path.splitext(name)[1].lower() not in MEDIA_EXTS:
            continue
        found.append(path)
    return found


def read_ai_context() -> str:
    """Try to read AI_CONTEXT.md for project phase awareness."""
    context_path = os.path.join(REPO_PATH, "Docs", "AI_CONTEXT.md")
    if not os.path.exists(context_path):
        context_path = os.path.join(REPO_PATH, "AI_CONTEXT.md")
    if os.path.exists(context_path):
        with open(context_path, "r", encoding="utf-8") as f:
            return f.read(2000)
    return ""


# =============================================================================
# Notion Integration
# =============================================================================

def get_notion_tasks() -> dict:
    """
    Query Notion database and return tasks grouped by status.
    Returns: {"done": [...], "in_progress": [...], "todo": [...]}
    """
    if not NOTION_TOKEN or not NOTION_DATABASE_ID:
        return {}

    headers = {
        "Authorization": f"Bearer {NOTION_TOKEN}",
        "Content-Type": "application/json",
        "Notion-Version": "2022-06-28",
    }

    url = f"https://api.notion.com/v1/databases/{NOTION_DATABASE_ID}/query"

    try:
        response = requests.post(url, headers=headers, json={"page_size": 100}, timeout=10)
        response.raise_for_status()
        data = response.json()
    except Exception as e:
        print(f"[WARN] Notion API error: {e}")
        return {}

    tasks = {"done": [], "in_progress": [], "todo": []}

    for page in data.get("results", []):
        props = page.get("properties", {})

        # Extract task name
        name = ""
        name_prop = props.get(NOTION_TASK_NAME_COLUMN, {})
        if name_prop.get("type") == "title":
            title_parts = name_prop.get("title", [])
            if title_parts:
                name = title_parts[0].get("text", {}).get("content", "")

        if not name:
            continue

        # Extract status
        status = ""
        status_prop = props.get(NOTION_STATUS_COLUMN, {})
        status_type = status_prop.get("type", "")

        if status_type == "status":
            status_data = status_prop.get("status")
            if status_data:
                status = status_data.get("name", "")
        elif status_type == "select":
            select_data = status_prop.get("select")
            if select_data:
                status = select_data.get("name", "")

        # Categorize
        if status in DONE_STATUSES:
            tasks["done"].append(name)
        elif status in IN_PROGRESS_STATUSES:
            tasks["in_progress"].append(name)
        elif status in TODO_STATUSES:
            tasks["todo"].append(name)
        else:
            tasks["todo"].append(f"{name} [{status}]")

    return tasks


def format_notion_tasks(tasks: dict) -> str:
    """Format Notion tasks into a string for the Claude prompt."""
    if not tasks:
        return ""

    sections = []
    if tasks.get("done"):
        sections.append("Completed tasks:\n" + "\n".join(f"  - {t}" for t in tasks["done"]))
    if tasks.get("in_progress"):
        sections.append("In progress:\n" + "\n".join(f"  - {t}" for t in tasks["in_progress"]))
    if tasks.get("todo"):
        sections.append("Upcoming / planned:\n" + "\n".join(f"  - {t}" for t in tasks["todo"]))

    return "\n\n".join(sections)


# =============================================================================
# Claude Summarization
# =============================================================================

def summarize_with_claude(
    commits: str,
    file_changes: str,
    commit_count: int,
    ai_context: str,
    notion_tasks: str,
    day_note: str,
    extra_note: str = "",
) -> str:
    """Send commits + Notion tasks to Claude Sonnet for bilingual structured summary."""
    client = anthropic.Anthropic(api_key=ANTHROPIC_API_KEY)

    today = datetime.now().strftime("%Y-%m-%d (%A)")

    context_section = ""
    if ai_context:
        context_section = f"\nProject context (from AI_CONTEXT.md):\n{ai_context}\n"

    notion_section = ""
    if notion_tasks:
        notion_section = f"\nNotion task tracker:\n{notion_tasks}\n"

    day_note_section = ""
    if day_note:
        day_note_section = f"\nDay note (include at the top of the report, before the English section):\n{day_note}\n"

    extra_note_section = ""
    if extra_note:
        extra_note_section = f"\nExtra note from developer (include as a bullet in the blockers or 'what was done' section as appropriate — translate it to Japanese in the JP section):\n{extra_note}\n"

    message = client.messages.create(
        model="claude-sonnet-4-6",
        max_tokens=1500,
        messages=[
            {
                "role": "user",
                "content": f"""You are a development progress reporter for GOLFIN, a 3D mobile golf game built in Unity (C#).
{context_section}{notion_section}{extra_note_section}
Here are today's ({today}) git commits ({commit_count} total), listed in CHRONOLOGICAL ORDER (oldest first):

{commits}

Files changed:
{file_changes}

Write a daily development report using EXACTLY this structure. English section FIRST, then the full Japanese translation SECOND. Do not interleave languages.

List progress items in CHRONOLOGICAL ORDER — the first task done in the day should appear first, the last task done should appear last.

📋 GOLFIN Daily Report — {today}
Commits: {commit_count}
{day_note_section}
--- ENGLISH ---

🔨 What was done today
(3-5 bullet points using BOTH git commits AND Notion completed tasks, in chronological order)

🚧 Blockers
(Any issues or blockers inferred from commits, Notion in-progress items that seem stuck, or project context. If none obvious, write "None")

📅 Plan for tomorrow
(Use Notion upcoming/in-progress tasks AND project context to infer next steps. 2-3 bullet points.)

📌 Task Tracker
(Brief summary: X completed, Y in progress, Z planned — from Notion data. If no Notion data, skip this section.)

--- 日本語 ---

🔨 本日の作業内容
(Same bullets as English above, translated to Japanese, in same chronological order)

🚧 ブロッカー
(Same as English blockers section, in Japanese)

📅 明日の予定
(Same as English plan section, in Japanese)

📌 タスク状況
(Same as English task tracker, in Japanese. If no Notion data, skip.)

Keep it concise and professional. Each bullet should be one line.
Do NOT use markdown bold (**) — Telegram doesn't render it well. Use plain text.
If there are no commits and no Notion tasks, say "No development activity today" in English and "本日の開発活動はありませんでした" in Japanese.""",
            }
        ],
    )

    return message.content[0].text


# =============================================================================
# Telegram
# =============================================================================

def post_to_telegram(text: str):
    """Send a text message to the configured chat via Telegram Bot API."""
    url = f"https://api.telegram.org/bot{TELEGRAM_BOT_TOKEN}/sendMessage"

    # Telegram has a 4096 char limit — split if needed
    chunks = [text[i:i + 4000] for i in range(0, len(text), 4000)]

    for chunk in chunks:
        payload = {
            "chat_id": ACTIVE_CHAT_ID,
            "text": chunk,
        }
        resp = requests.post(url, json=payload, timeout=10)
        resp.raise_for_status()

    print(f"[OK] Telegram message sent to {ACTIVE_CHAT_ID}")


def _retry_after_seconds(resp_obj, default: float) -> float:
    """
    Extract Telegram's retry_after (seconds) from a 429 response so we wait the
    exact throttle window. Looks in the JSON body's `parameters.retry_after`
    first, then the `Retry-After` header; falls back to `default`. Adds a 1s
    buffer so we resume just after the window, not exactly on its edge.
    """
    if resp_obj is None:
        return default
    try:
        ra = resp_obj.json().get("parameters", {}).get("retry_after")
        if ra is not None:
            return float(ra) + 1.0
    except Exception:
        pass
    ra_hdr = getattr(resp_obj, "headers", {}).get("Retry-After")
    if ra_hdr:
        try:
            return float(ra_hdr) + 1.0
        except ValueError:
            pass
    return default


def _probe_video_dims(path: str):
    """
    Return (width, height, duration_seconds) for a video, or None on any failure.
    Telegram renders a SQUARE preview bubble for sendVideo uploads that omit
    width/height — so we probe and pass them explicitly (see _send_telegram_file).
    Portrait captures (1170x2532) showed up square in Telegram until this was added.
    """
    ffprobe = _find_media_tool("ffprobe", "GOLFIN_FFPROBE_PATH")
    if not ffprobe:
        return None
    try:
        out = subprocess.run(
            [ffprobe, "-v", "error", "-select_streams", "v:0",
             "-show_entries", "stream=width,height:format=duration",
             "-of", "default=noprint_wrappers=1:nokey=0", path],
            capture_output=True, text=True, timeout=60).stdout
        vals = {}
        for line in out.splitlines():
            if "=" in line:
                k, v = line.split("=", 1)
                vals[k.strip()] = v.strip()
        w = int(vals.get("width", "0"))
        h = int(vals.get("height", "0"))
        dur = int(round(float(vals.get("duration", "0") or 0)))
        if w > 0 and h > 0:
            return (w, h, dur)
    except Exception as e:
        print(f"[WARN] could not probe dims for {os.path.basename(path)}: {e}")
    return None


def _send_telegram_file(method: str, field: str, path: str, caption: str) -> bool:
    """
    Upload a single file to Telegram via the given method (sendVideo/sendPhoto/etc.).
    Retries on 429 (honouring retry_after) and transient 5xx errors up to
    MEDIA_MAX_RETRIES times. Returns True on success, False on terminal failure
    (caller decides whether to delete).
    """
    url = f"https://api.telegram.org/bot{TELEGRAM_BOT_TOKEN}/{method}"
    data = {"chat_id": ACTIVE_CHAT_ID, "caption": caption[:1024]}
    if method == "sendVideo":
        data["supports_streaming"] = "true"
        # Pass width/height/duration so Telegram renders the true aspect ratio.
        # Without them it falls back to a SQUARE preview bubble — portrait
        # (1170x2532) clips looked square until this was added (2026-06-11).
        dims = _probe_video_dims(path)
        if dims:
            data["width"], data["height"], data["duration"] = dims
    name = os.path.basename(path)

    for attempt in range(1, MEDIA_MAX_RETRIES + 1):
        try:
            with open(path, "rb") as fh:
                resp = requests.post(url, data=data, files={field: fh}, timeout=300)
            resp.raise_for_status()
            print(f"[OK] {method}: {name}")
            return True
        except Exception as e:
            resp_obj = getattr(e, "response", None)
            status = getattr(resp_obj, "status_code", None)
            # Retry rate-limit / transient errors; honour Telegram's retry_after.
            if status in RETRYABLE_STATUSES and attempt < MEDIA_MAX_RETRIES:
                wait = _retry_after_seconds(resp_obj, default=float(attempt * 5))
                print(f"[RETRY] {method} {name}: HTTP {status}, waiting {wait:.0f}s "
                      f"(attempt {attempt}/{MEDIA_MAX_RETRIES})")
                time.sleep(wait)
                continue
            detail = ""
            # Surface Telegram's JSON error description when present.
            if resp_obj is not None:
                detail = f" — {resp_obj.text[:300]}"
            print(f"[WARN] {method} failed for {name}: {e}{detail}")
            return False
    return False


def send_media_file(path: str, caption: str) -> bool:
    """Pick the right Telegram method based on extension and send the file."""
    ext = os.path.splitext(path)[1].lower()
    if ext in VIDEO_EXTS:
        return _send_telegram_file("sendVideo", "video", path, caption)
    if ext in ANIM_EXTS:
        return _send_telegram_file("sendAnimation", "animation", path, caption)
    if ext in IMAGE_EXTS:
        return _send_telegram_file("sendPhoto", "photo", path, caption)
    # Unknown — send as a generic document so nothing silently disappears.
    return _send_telegram_file("sendDocument", "document", path, caption)


def _find_media_tool(name: str, env_var: str):
    """
    Locate ffmpeg / ffprobe robustly. Under launchd the plist PATH is just
    /usr/bin:/bin:/usr/sbin:/sbin, which does NOT include the Homebrew or
    ~/.local/bin locations where ffmpeg usually lives — so PATH lookup alone
    fails in the scheduled run. Check an explicit env override first, then PATH,
    then the common install dirs. Returns an absolute path or None.
    """
    cand = os.environ.get(env_var, "")
    if cand and os.path.isfile(cand) and os.access(cand, os.X_OK):
        return cand
    found = shutil.which(name)
    if found:
        return found
    for d in ("~/.local/bin", "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin"):
        p = os.path.join(os.path.expanduser(d), name)
        if os.path.isfile(p) and os.access(p, os.X_OK):
            return p
    return None


def _compress_video(src: str):
    """
    Two-pass re-encode `src` to ~COMPRESS_TARGET_BYTES at the SAME resolution
    (only the bitrate drops — never downscale, per the full-res capture rule).
    Returns the path to a temp .mp4 under a fresh temp dir on success, or None
    on any failure (missing ffmpeg, probe error, encode error, still too big).
    Caller owns cleanup of the returned file's temp dir.
    """
    ffmpeg = _find_media_tool("ffmpeg", "GOLFIN_FFMPEG_PATH")
    ffprobe = _find_media_tool("ffprobe", "GOLFIN_FFPROBE_PATH")
    name = os.path.basename(src)
    if not ffmpeg or not ffprobe:
        print(f"[WARN] ffmpeg/ffprobe not found — cannot compress {name}. "
              f"Install ffmpeg or set GOLFIN_FFMPEG_PATH / GOLFIN_FFPROBE_PATH.")
        return None

    # Duration drives the target bitrate.
    try:
        dur = float(subprocess.run(
            [ffprobe, "-v", "error", "-show_entries", "format=duration",
             "-of", "default=nokey=1:noprint_wrappers=1", src],
            capture_output=True, text=True, timeout=60).stdout.strip())
    except Exception as e:
        print(f"[WARN] ffprobe failed for {name}: {e}")
        return None
    if dur <= 0:
        print(f"[WARN] bad/zero duration for {name} — cannot compress.")
        return None

    total_kbps = (COMPRESS_TARGET_BYTES * 8 / dur) / 1000.0
    video_kbps = int(max(500, total_kbps - COMPRESS_AUDIO_KBPS))

    tmpdir = tempfile.mkdtemp(prefix="golfin_report_")
    out = os.path.join(tmpdir, os.path.splitext(name)[0] + "_tg.mp4")
    plog = os.path.join(tmpdir, "ff2pass")
    base = [ffmpeg, "-y", "-hide_banner", "-loglevel", "error", "-i", src,
            "-c:v", "libx264", "-b:v", f"{video_kbps}k", "-preset", "medium",
            "-pix_fmt", "yuv420p", "-passlogfile", plog]
    try:
        print(f"[INFO] Compressing {name}: {os.path.getsize(src)/1024/1024:.1f}MB, "
              f"{dur:.0f}s -> video {video_kbps}k (target ~{COMPRESS_TARGET_BYTES//(1024*1024)}MB)…")
        r1 = subprocess.run(base + ["-pass", "1", "-an", "-f", "mp4", os.devnull],
                            capture_output=True, text=True, timeout=1800)
        if r1.returncode != 0:
            print(f"[WARN] compress pass 1 failed for {name}: {r1.stderr[:300]}")
            shutil.rmtree(tmpdir, ignore_errors=True)
            return None
        r2 = subprocess.run(base + ["-pass", "2", "-c:a", "aac",
                            "-b:a", f"{COMPRESS_AUDIO_KBPS}k", "-movflags", "+faststart", out],
                            capture_output=True, text=True, timeout=1800)
        if r2.returncode != 0:
            print(f"[WARN] compress pass 2 failed for {name}: {r2.stderr[:300]}")
            shutil.rmtree(tmpdir, ignore_errors=True)
            return None
    except Exception as e:
        print(f"[WARN] compression error for {name}: {e}")
        shutil.rmtree(tmpdir, ignore_errors=True)
        return None

    if not os.path.isfile(out):
        shutil.rmtree(tmpdir, ignore_errors=True)
        return None
    size = os.path.getsize(out)
    if size > TELEGRAM_MAX_UPLOAD_BYTES:
        print(f"[WARN] {name} still {size/1024/1024:.1f}MB after compression "
              f"(> 50 MB) — not sent.")
        shutil.rmtree(tmpdir, ignore_errors=True)
        return None
    print(f"[OK] Compressed {name}: {os.path.getsize(src)/1024/1024:.1f}MB -> "
          f"{size/1024/1024:.1f}MB")
    return out


def _post_unattached_notice(failed: list) -> None:
    """
    Post a follow-up message to the chat listing media that could NOT be attached
    (oversize + uncompressible, or upload failures), so recipients aren't left
    thinking everything went out. The text report is already posted by the time
    media is attempted, so this is a separate message in the same chat rather
    than an edit of the original. Best-effort: never crashes the run.
    """
    if not failed:
        return
    lines = [
        "⚠️ Some media could not be attached to today's report:",
        "⚠️ 一部のメディアを本日のレポートに添付できませんでした:",
        "",
    ]
    lines += [f"• {name} — {reason}" for name, reason in failed]
    try:
        post_to_telegram("\n".join(lines))
    except Exception as e:
        print(f"[WARN] Could not post unattached-media notice: {e}")


def send_all_media(git_videos: list, drop_media: list) -> None:
    """
    Send git videos (kept on disk) + drop-folder media (deleted after success).
    Oversize videos (>50 MB) are auto-compressed to fit before sending; oversize
    NON-video files (still > 50 MB) are skipped and reported, never deleted.
    Anything that still can't be attached (uncompressible or upload failure) is
    listed in a follow-up notice posted to the chat.
    """
    if not git_videos and not drop_media:
        print("[INFO] No media to attach today.")
        return

    sent_real_paths = set()
    uploads_attempted = 0  # only count real upload attempts, not dedupe/oversize skips
    failed = []            # (name, reason) for media that could NOT be attached

    def _process(path: str, caption_prefix: str, is_drop: bool):
        nonlocal uploads_attempted
        real = os.path.realpath(path)
        if real in sent_real_paths:
            return  # de-dupe across git + drop folder
        size = os.path.getsize(path)
        name = os.path.basename(path)
        send_path = path           # what we actually upload (may be a compressed temp)
        tmp_dir_to_clean = None
        caption_suffix = ""
        if size > TELEGRAM_MAX_UPLOAD_BYTES:
            mb = size / (1024 * 1024)
            ext = os.path.splitext(name)[1].lower()
            if ext not in VIDEO_EXTS:
                # Only videos can be transcoded down; images/anims just skip.
                print(f"[SKIP] {name} is {mb:.1f} MB > 50 MB Telegram limit — not sent.")
                failed.append((name, f"{mb:.0f} MB, over the 50 MB limit and not a "
                                     f"video, so it can't be compressed"))
                return
            print(f"[INFO] {name} is {mb:.1f} MB > 50 MB — auto-compressing to fit.")
            compressed = _compress_video(path)
            if not compressed:
                print(f"[SKIP] {name} is {mb:.1f} MB > 50 MB and could not be "
                      f"compressed — not sent (original kept).")
                failed.append((name, f"{mb:.0f} MB, couldn't be compressed under "
                                     f"the 50 MB limit"))
                return
            send_path = compressed
            tmp_dir_to_clean = os.path.dirname(compressed)
            caption_suffix = (f" (compressed {mb:.0f}MB→"
                              f"{os.path.getsize(compressed)/1024/1024:.0f}MB)")
        # Pace consecutive uploads under Telegram's group rate limit. Sleep BEFORE
        # the 2nd+ upload (not after the last) so we don't trail an idle gap.
        if uploads_attempted > 0:
            time.sleep(MEDIA_SEND_INTERVAL_SEC)
        uploads_attempted += 1
        caption = f"{caption_prefix}{name}{caption_suffix}"
        ok = send_media_file(send_path, caption)
        if ok:
            sent_real_paths.add(real)
            if is_drop:
                # The drop-folder contract is "deleted after a successful send" —
                # the ORIGINAL is removed even when we sent a compressed copy,
                # because its content was delivered. Masters live elsewhere.
                try:
                    os.remove(path)
                    print(f"[OK] Removed drop-folder file after send: {name}")
                except OSError as e:
                    print(f"[WARN] Could not delete {name} after send: {e}")
        else:
            failed.append((name, "upload to Telegram failed after retries"))
        # Always clean up the temp compressed file + its temp dir.
        if tmp_dir_to_clean:
            shutil.rmtree(tmp_dir_to_clean, ignore_errors=True)

    for v in git_videos:
        _process(v, "🎬 ", is_drop=False)

    for m in drop_media:
        _process(m, "📎 ", is_drop=True)

    # Tell the chat about anything that didn't make it.
    _post_unattached_notice(failed)


# =============================================================================
# Main
# =============================================================================

def main():
    parser = argparse.ArgumentParser(description="GOLFIN daily report")
    parser.add_argument("--note", default="", help="Extra note to include in today's report")
    parser.add_argument("--since", default="24 hours ago",
                        help="Git --since window (default: '24 hours ago' — a rolling 24h "
                             "window that tiles cleanly with the 13:30 daily run so commits "
                             "made after 13:30 are not lost in a gap. Override e.g. "
                             "'2026-05-14 00:00:00' to backfill a missed day)")
    parser.add_argument("--no-media", action="store_true", help="Skip all video/image attachments")
    parser.add_argument("--dry-run", action="store_true", help="Print the report + planned media to stdout; do NOT post to Telegram or delete anything")
    parser.add_argument("--test", action="store_true", help="Real send, but to TELEGRAM_TEST_CHAT_ID instead of the production channel")
    args = parser.parse_args()

    print(f"[{datetime.now().isoformat()}] Starting daily report...")
    print(f"[INFO] Repo: {REPO_PATH}")

    commits = get_todays_commits(args.since)
    commit_count = get_commit_count(args.since)
    file_changes = get_changed_files(args.since)
    ai_context = read_ai_context()
    day_note = get_day_note()

    # Notion tasks (optional — works without it)
    notion_tasks_data = get_notion_tasks()
    notion_tasks_text = format_notion_tasks(notion_tasks_data)

    has_commits = bool(commits)
    has_notion = bool(notion_tasks_data)

    # Gather media regardless of the report-text branch.
    git_videos = [] if args.no_media else get_todays_videos(args.since)
    drop_media = [] if args.no_media else collect_drop_media(REPORT_MEDIA_DIR)

    # --dry-run is a cheap, safe diagnostics check: no Claude call, no Telegram
    # post, no file deletion. Show the inputs and the media that WOULD be sent.
    if args.dry_run:
        print("\n========== DRY RUN — inputs ==========")
        print(f"Commits since '{args.since}': {commit_count}")
        print(f"Notion tasks found: {bool(notion_tasks_data)}")
        print("\n========== DRY RUN — media that WOULD be sent ==========")
        for v in git_videos:
            print(f"  🎬 git video : {os.path.relpath(v, REPO_PATH)}")
        for m in drop_media:
            print(f"  📎 drop file : {os.path.relpath(m, REPO_PATH)} (would be deleted after send)")
        if not git_videos and not drop_media:
            print("  (none)")
        print("\n[DRY RUN] Nothing posted to Telegram; nothing deleted; no Claude call.")
        return

    # Real send needs the secrets — fail fast with a clear message if any missing.
    # --test routes everything to the test channel so production only ever gets
    # the scheduled report.
    global ACTIVE_CHAT_ID
    needed = {
        "ANTHROPIC_API_KEY": ANTHROPIC_API_KEY,
        "TELEGRAM_BOT_TOKEN": TELEGRAM_BOT_TOKEN,
    }
    if args.test:
        needed["TELEGRAM_TEST_CHAT_ID"] = TELEGRAM_TEST_CHAT_ID
        ACTIVE_CHAT_ID = TELEGRAM_TEST_CHAT_ID
    else:
        needed["TELEGRAM_CHAT_ID"] = TELEGRAM_CHAT_ID
        ACTIVE_CHAT_ID = TELEGRAM_CHAT_ID
    missing = [k for k, v in needed.items() if not v]
    if missing:
        print(f"[FATAL] Missing required env var(s): {', '.join(missing)}. "
              f"Copy Docs/Scripts/.env.example to .env and fill it in "
              f"(see DAILY_REPORT_SETUP.md).")
        sys.exit(1)
    print(f"[INFO] Target channel: {'TEST' if args.test else 'PRODUCTION'} ({ACTIVE_CHAT_ID})")

    if not has_commits and not has_notion:
        today = datetime.now().strftime("%Y-%m-%d (%A)")
        report = (
            f"📋 GOLFIN Daily Report — {today}\n"
            f"Commits: 0\n"
        )
        if day_note:
            report += f"\n{day_note}\n"
        report += (
            f"\n--- ENGLISH ---\n\n"
            f"🔨 What was done today\n"
            f"No development activity today.\n\n"
            f"🚧 Blockers\nNone\n\n"
            f"📅 Plan for tomorrow\n"
            f"Check AI_CONTEXT.md for current phase.\n\n"
            f"--- 日本語 ---\n\n"
            f"🔨 本日の作業内容\n"
            f"本日の開発活動はありませんでした。\n\n"
            f"🚧 ブロッカー\nなし\n\n"
            f"📅 明日の予定\n"
            f"AI_CONTEXT.mdで現在のフェーズを確認。"
        )
    else:
        report = summarize_with_claude(
            commits, file_changes, commit_count, ai_context, notion_tasks_text, day_note, args.note
        )

    post_to_telegram(report)

    if not args.no_media:
        send_all_media(git_videos, drop_media)

    print("Done.")


if __name__ == "__main__":
    main()
