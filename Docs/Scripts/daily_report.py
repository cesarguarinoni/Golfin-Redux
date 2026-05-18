#!/usr/bin/env python3
"""
GOLFIN Daily Development Report
Pulls git commits + Notion tasks, summarizes via Claude Sonnet, posts to Telegram.
Includes Japan holiday/weekend awareness.

See DAILY_REPORT_SETUP.md for installation and configuration.
"""

import argparse
import os
import subprocess
import sys
from datetime import datetime, date

import anthropic
import requests

from dotenv import load_dotenv
load_dotenv()

# --- Config from environment variables ---
ANTHROPIC_API_KEY = os.environ["ANTHROPIC_API_KEY"]
TELEGRAM_BOT_TOKEN = os.environ["TELEGRAM_BOT_TOKEN"]
TELEGRAM_CHAT_ID = os.environ["TELEGRAM_CHAT_ID"]
REPO_PATH = os.environ.get("GOLFIN_REPO_PATH", os.path.expanduser("~/GolfinRedux"))

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


def get_todays_commits(since: str = "midnight") -> str:
    """Pull git log from the repo — chronological order (oldest first)."""
    result = subprocess.run(
        ["git", "log", f"--since={since}", "--reverse", "--format=%h %s (%an, %ar)", "--no-merges"],
        cwd=REPO_PATH,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def get_commit_count(since: str = "midnight") -> int:
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


def get_changed_files(since: str = "midnight") -> str:
    """Get a summary of files changed since the given time."""
    result = subprocess.run(
        ["git", "log", f"--since={since}", "--reverse", "--stat", "--format=", "--no-merges"],
        cwd=REPO_PATH,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


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
        model="claude-sonnet-4-20250514",
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
    """Send message to Ken via Telegram Bot API."""
    url = f"https://api.telegram.org/bot{TELEGRAM_BOT_TOKEN}/sendMessage"

    # Telegram has a 4096 char limit — split if needed
    chunks = [text[i:i + 4000] for i in range(0, len(text), 4000)]

    for chunk in chunks:
        payload = {
            "chat_id": TELEGRAM_CHAT_ID,
            "text": chunk,
        }
        resp = requests.post(url, json=payload, timeout=10)
        resp.raise_for_status()

    print(f"[OK] Telegram message sent to {TELEGRAM_CHAT_ID}")


# =============================================================================
# Main
# =============================================================================

def main():
    parser = argparse.ArgumentParser(description="GOLFIN daily report")
    parser.add_argument("--note", default="", help="Extra note to include in today's report")
    parser.add_argument("--since", default="midnight", help="Git --since value (e.g. '2026-05-14 00:00:00') to backfill missed days")
    args = parser.parse_args()

    print(f"[{datetime.now().isoformat()}] Starting daily report...")

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
    print("Done.")


if __name__ == "__main__":
    main()
