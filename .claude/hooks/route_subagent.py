#!/usr/bin/env python3
"""Subagent routing hook for GOLFIN Redux pipeline.

Fires on SubagentStop and Stop events. Walks every active task folder under
Docs/Specs/Active/, reads STATUS.md, and prints the next action to STDOUT (which
Claude Code surfaces in the terminal). For state changes that need Cesar's
attention, also fires a desktop notification (Windows / macOS / Linux) AND
optionally an email.

This hook does NOT spawn subagents directly (Claude Code handles delegation
based on subagent description fields). It just makes the next-step crystal
clear so the orchestrator can pick it up automatically and Cesar gets pinged
only when it matters.

State transitions handled:

  SPEC_READY                    -> "Use the golfin-implementer subagent on <task>"
  IMPLEMENTER_WORKING           -> (heartbeat check; warn if stale)
  READY_FOR_SELF_REVIEW         -> "Use the golfin-self-reviewer subagent on <task>"
  SELF_REVIEW_PASS              -> "Use the golfin-reviewer subagent on <task>"
  SELF_REVIEW_FAIL              -> "Use the golfin-implementer subagent on <task>" (redo)
  READY_FOR_ARCHITECT_REVIEW    -> "Use the golfin-reviewer subagent on <task>"
  ARCHITECT_REVIEW_PASS         -> Notify Cesar: "Ready for your final approval"
  ARCHITECT_REVIEW_FAIL         -> "Use the golfin-implementer subagent on <task>" (redo)
  ARCHITECT_REVIEW_ESCALATE     -> Notify Cesar: "Architect needs your input"
  CESAR_REJECTED                -> "Use the golfin-implementer subagent on <task>" (redo)
                                   This state is set when Cesar manually rejects after
                                   architect-pass. Implementer must read CESAR_REJECTION.md
                                   in the task folder before redoing.
  IMPLEMENTER_BLOCKED           -> Notify Cesar: "Implementer is blocked - manual help needed"
                                   Used when the implementer hits something it can't resolve
                                   (e.g., screenshot capture failing repeatedly, MCP unavailable)
  DONE                          -> (move to Completed/, no notification)

Notification channels:

  - Desktop notification (always tried; opt-out via NOTIFY_TOAST=0 env)
      * Windows: PowerShell System.Windows.Forms.NotifyIcon
      * macOS:   osascript display notification
      * Linux:   notify-send (if available)
  - Email via SMTP (opt-in via .claude/notify_config.json - see template)

Config file (optional): .claude/notify_config.json
  {
    "email": {
      "enabled": true,
      "smtp_host": "smtp.gmail.com",
      "smtp_port": 587,
      "smtp_user": "you@gmail.com",
      "smtp_password_env": "GOLFIN_NOTIFY_PASSWORD",
      "from_addr": "you@gmail.com",
      "to_addr": "you@gmail.com"
    }
  }
"""
import os
import re
import sys
import json
import subprocess
import time
from pathlib import Path
from datetime import datetime, timezone

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
ACTIVE_DIR = REPO_ROOT / "Docs" / "Specs" / "Active"
COMPLETED_DIR = REPO_ROOT / "Docs" / "Specs" / "Completed"
TEMPLATE_NAME = "_TEMPLATE"
CONFIG_PATH = REPO_ROOT / ".claude" / "notify_config.json"

# Cesar's standing rule (2026-06-09): every iteration's review image must be
# SURFACED in the main chat so he can eyeball it live and interrupt the pipeline
# before the reviewers spend tokens ("I'm faster than the reviewer most of the
# time but not always available"). The orchestrator renders the image inline via
# the Read tool; this hook just makes the path impossible to miss. Mirrors the
# `Canonical screenshot:` declaration the implementer writes (Rule 14).
CANONICAL_SHOT_RE = re.compile(
    r"canonical\s+(?:screenshot|frame|capture|image)\b[^\n]*?`?"
    r"((?:Docs/Specs/(?:Active|Completed)/[\w.\-]+/)?screenshots/[\w./\-]+\.(?:png|jpg|jpeg))",
    re.IGNORECASE,
)
# States where the implementer has just produced a fresh canonical frame — the
# moment to surface it (earliest window for Cesar to catch an issue).
SURFACE_STATES = {"READY_FOR_SELF_REVIEW", "READY_FOR_ARCHITECT_REVIEW"}


def canonical_shot_rel(task_dir):
    """Return the repo-relative path of the iteration's canonical screenshot,
    pulled from IMPLEMENTER_REPORT.md's `Canonical screenshot:` line. None if
    absent. Used to remind the orchestrator to Read+display it in the chat."""
    report = task_dir / "IMPLEMENTER_REPORT.md"
    if not report.exists():
        return None
    try:
        content = report.read_text(encoding="utf-8", errors="ignore")
    except Exception:
        return None
    m = CANONICAL_SHOT_RE.search(content)
    if not m:
        return None
    rel = m.group(1).strip()
    # Normalize to a repo-rooted path the orchestrator can Read directly.
    if rel.startswith("Docs/Specs/"):
        return rel
    try:
        task_rel = task_dir.resolve().relative_to(REPO_ROOT.resolve()).as_posix()
        return f"{task_rel}/{rel}"
    except ValueError:
        return rel

# When IMPLEMENTER_WORKING and no heartbeat update for this many minutes,
# flag the task as potentially stuck and notify Cesar.
HEARTBEAT_STALE_MINUTES = 15

# State -> next-action category. None = Cesar's turn. "" = no action.
NEXT_AGENT = {
    "SPEC_READY": "golfin-implementer",
    "IMPLEMENTER_WORKING": "",                         # in progress
    "READY_FOR_SELF_REVIEW": "golfin-self-reviewer",
    "SELF_REVIEW_PASS": "golfin-reviewer",
    "SELF_REVIEW_FAIL": "golfin-implementer",
    "READY_FOR_ARCHITECT_REVIEW": "golfin-reviewer",
    "READY_FOR_REDTEAM": "golfin-redteam-reviewer",    # reviewer PASSed -> adversarial check
    "ARCHITECT_REVIEW_PASS": None,                     # Cesar's turn (post red-team)
    "ARCHITECT_REVIEW_FAIL": "golfin-implementer",
    "ARCHITECT_REVIEW_ESCALATE": None,                 # Cesar's turn
    "CESAR_REJECTED": "golfin-implementer",            # redo with Cesar's notes
    "IMPLEMENTER_BLOCKED": None,                       # Cesar must unblock
    "DONE": "",
}

# Tier 4 — review scoreboard. Every time a task that the reviewer+red-teamer
# passed (ARCHITECT_REVIEW_PASS) is then rejected by Cesar (CESAR_REJECTED), the
# automated gate MISSED something Cesar caught. Logging these makes the miss
# rate visible so the pipeline self-corrects instead of silently rubber-stamping.
MISS_LOG_PATH = REPO_ROOT / ".claude" / "review_misses.log"


def read_status(task_dir):
    """Read STATUS.md, return first non-empty line or empty string."""
    status_file = task_dir / "STATUS.md"
    if not status_file.exists():
        return ""
    try:
        text = status_file.read_text(encoding="utf-8").strip()
        if not text:
            return ""
        return text.splitlines()[0].strip()
    except Exception:
        return ""


def heartbeat_age_minutes(task_dir):
    """Return age of HEARTBEAT.log's most recent line in minutes, or None if no heartbeat file."""
    hb = task_dir / "HEARTBEAT.log"
    if not hb.exists():
        return None
    try:
        age_seconds = time.time() - hb.stat().st_mtime
        return age_seconds / 60.0
    except Exception:
        return None


def load_notify_config():
    """Load notification config; return defaults if missing or invalid."""
    defaults = {"email": {"enabled": False}}
    if not CONFIG_PATH.exists():
        return defaults
    try:
        with open(CONFIG_PATH, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return defaults


def _notify_windows(title, message):
    """Windows toast via PowerShell. Best-effort; never raises."""
    try:
        title_esc = title.replace("'", "''")
        message_esc = message.replace("'", "''")
        ps_script = (
            "Add-Type -AssemblyName System.Windows.Forms; "
            "$balloon = New-Object System.Windows.Forms.NotifyIcon; "
            "$balloon.Icon = [System.Drawing.SystemIcons]::Information; "
            f"$balloon.BalloonTipTitle = '{title_esc}'; "
            f"$balloon.BalloonTipText = '{message_esc}'; "
            "$balloon.Visible = $true; "
            "$balloon.ShowBalloonTip(10000); "
            "Start-Sleep -Seconds 11; "
            "$balloon.Dispose()"
        )
        subprocess.Popen(
            ["powershell", "-NoProfile", "-WindowStyle", "Hidden", "-Command", ps_script],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
    except Exception:
        pass


def _notify_macos(title, message):
    """macOS notification via osascript. Best-effort; never raises.

    Strips double-quotes and backslashes from inputs to avoid breaking the
    AppleScript string literal. Newlines collapse to spaces (display
    notification doesn't render multi-line bodies anyway)."""
    try:
        def _sanitize(s):
            return s.replace("\\", "").replace('"', "").replace("\n", " ")
        title_s = _sanitize(title)
        message_s = _sanitize(message)
        applescript = f'display notification "{message_s}" with title "{title_s}"'
        subprocess.Popen(
            ["osascript", "-e", applescript],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
    except Exception:
        pass


def _notify_linux(title, message):
    """Linux notification via notify-send. Best-effort; never raises."""
    try:
        subprocess.Popen(
            ["notify-send", title, message],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
    except Exception:
        pass


def notify_desktop(title, message):
    """Fire a desktop notification on the current OS. Best-effort; never raises."""
    if os.environ.get("NOTIFY_TOAST", "1") == "0":
        return
    platform = sys.platform
    if platform == "win32":
        _notify_windows(title, message)
    elif platform == "darwin":
        _notify_macos(title, message)
    else:
        _notify_linux(title, message)


def notify_email(title, message, config):
    """Send an email if configured. Best-effort; never raises."""
    email_cfg = config.get("email", {})
    if not email_cfg.get("enabled"):
        return
    try:
        import smtplib
        from email.mime.text import MIMEText

        password_env = email_cfg.get("smtp_password_env", "GOLFIN_NOTIFY_PASSWORD")
        password = os.environ.get(password_env)
        if not password:
            # Silently skip; we don't want to log secrets-related issues.
            return

        msg = MIMEText(message)
        msg["Subject"] = f"[GOLFIN] {title}"
        msg["From"] = email_cfg["from_addr"]
        msg["To"] = email_cfg["to_addr"]

        with smtplib.SMTP(email_cfg["smtp_host"], int(email_cfg.get("smtp_port", 587)), timeout=10) as smtp:
            smtp.starttls()
            smtp.login(email_cfg["smtp_user"], password)
            smtp.send_message(msg)
    except Exception:
        pass  # Email is best-effort; never break the hook.


def alert_cesar(task, status, summary, where_lines, config):
    """Fire desktop notification + email + log to file + return formatted lines for terminal output."""
    body = f"Task: {task}\n" + "\n".join(where_lines)
    notify_desktop(f"GOLFIN: {summary}", body)
    notify_email(f"{summary} ({task})", body, config)
    log_alert(task, status, summary, where_lines)
    out = [f"  [{task}] {status}", f"    -> {summary}"]
    out.extend([f"    -> {w}" for w in where_lines])
    return out


def log_alert(task, status, summary, where_lines):
    """Append every Cesar-alert to .claude/alerts.log so nothing is lost
    if toast/email both fail. Best-effort; never raises."""
    try:
        log_path = REPO_ROOT / ".claude" / "alerts.log"
        ts = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        line = f"[{ts}] [{task}] {status} - {summary}"
        with open(log_path, "a", encoding="utf-8") as f:
            f.write(line + "\n")
            for w in where_lines:
                f.write(f"    {w}\n")
    except Exception:
        pass


def record_review_miss(task, task_dir):
    """Tier 4: log a reviewer/red-team miss (PASS later rejected by Cesar).

    Deduped by '<task>:<CESAR_REJECTION.md mtime>' so the repeatedly-firing Stop
    hook logs each distinct rejection exactly once. Returns (is_new, total_lines)
    where total_lines is the running count of logged misses. Best-effort.
    """
    try:
        rejection = task_dir / "CESAR_REJECTION.md"
        if not rejection.exists():
            return (False, _miss_count())
        key = f"{task}:{int(rejection.stat().st_mtime)}"
        existing = ""
        if MISS_LOG_PATH.exists():
            existing = MISS_LOG_PATH.read_text(encoding="utf-8", errors="ignore")
        if key in existing:
            return (False, _miss_count())
        ts = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        with open(MISS_LOG_PATH, "a", encoding="utf-8") as f:
            f.write(f"[{ts}] MISS key={key} - architect-PASS later rejected by Cesar\n")
        return (True, _miss_count())
    except Exception:
        return (False, 0)


def _miss_count():
    """Count logged misses (lines containing ' MISS key='). Best-effort -> 0."""
    try:
        if not MISS_LOG_PATH.exists():
            return 0
        return sum(
            1 for line in MISS_LOG_PATH.read_text(encoding="utf-8", errors="ignore").splitlines()
            if " MISS key=" in line
        )
    except Exception:
        return 0


def main():
    if not ACTIVE_DIR.exists():
        return 0

    config = load_notify_config()
    cesar_alert_lines = []
    next_steps = []
    heartbeat_warnings = []

    for task_dir in sorted(ACTIVE_DIR.iterdir()):
        if not task_dir.is_dir() or task_dir.name == TEMPLATE_NAME:
            continue

        status = read_status(task_dir)
        if not status:
            continue

        next_agent = NEXT_AGENT.get(status, "UNKNOWN")
        task = task_dir.name

        if next_agent == "UNKNOWN":
            next_steps.append(
                f"  [{task}] STATUS={status} (UNKNOWN STATE; check STATUS.md)"
            )
        elif status == "DONE":
            # Auto-move to Completed/. Idempotent: if target already exists, skip.
            try:
                COMPLETED_DIR.mkdir(parents=True, exist_ok=True)
                target = COMPLETED_DIR / task
                if not target.exists():
                    task_dir.rename(target)
                    next_steps.append(f"  [{task}] DONE -> moved to Docs/Specs/Completed/")
            except Exception as e:
                next_steps.append(f"  [{task}] DONE but move failed: {e}")
            continue
        elif next_agent == "":
            # In progress (IMPLEMENTER_WORKING) or done. Check heartbeat staleness.
            if status == "IMPLEMENTER_WORKING":
                age_min = heartbeat_age_minutes(task_dir)
                if age_min is not None and age_min > HEARTBEAT_STALE_MINUTES:
                    msg = f"Heartbeat stale: last update {age_min:.0f}min ago (threshold {HEARTBEAT_STALE_MINUTES}min)"
                    heartbeat_warnings.append((task, msg))
                    cesar_alert_lines.extend(
                        alert_cesar(
                            task, status,
                            "Implementer may be stuck",
                            [msg, f"Check Docs/Specs/Active/{task}/HEARTBEAT.log"],
                            config,
                        )
                    )
            continue
        elif next_agent is None:
            # Cesar's turn.
            if status == "ARCHITECT_REVIEW_PASS":
                # Check if there's a CESAR_REJECTION.md newer than ARCHITECT_REVIEW.md.
                # If so, it means Cesar rejected after architect-pass and STATUS is stale.
                rejection_file = task_dir / "CESAR_REJECTION.md"
                review_file = task_dir / "ARCHITECT_REVIEW.md"
                if (rejection_file.exists() and review_file.exists()
                        and rejection_file.stat().st_mtime > review_file.stat().st_mtime):
                    record_review_miss(task, task_dir)
                    cesar_alert_lines.extend(
                        alert_cesar(
                            task, status,
                            "STATUS stale (CESAR_REJECTION.md newer than ARCHITECT_REVIEW.md)",
                            [f"Manually set STATUS to CESAR_REJECTED in Docs/Specs/Active/{task}/STATUS.md"],
                            config,
                        )
                    )
                else:
                    cesar_alert_lines.extend(
                        alert_cesar(
                            task, status,
                            "Ready for your final approval (passed reviewer + red-team)",
                            [f"Review Docs/Specs/Active/{task}/ARCHITECT_REVIEW.md",
                             f"Red-team verdict: Docs/Specs/Active/{task}/REDTEAM_REVIEW.md",
                             f"Latest screenshot: Docs/Specs/Active/{task}/screenshots/"],
                            config,
                        )
                    )
            elif status == "ARCHITECT_REVIEW_ESCALATE":
                cesar_alert_lines.extend(
                    alert_cesar(
                        task, status,
                        "Architect needs your input",
                        [f"Read Docs/Specs/Active/{task}/ARCHITECT_REVIEW.md (Open questions section)"],
                        config,
                    )
                )
            elif status == "IMPLEMENTER_BLOCKED":
                cesar_alert_lines.extend(
                    alert_cesar(
                        task, status,
                        "Implementer is blocked - manual help needed",
                        [f"Read Docs/Specs/Active/{task}/IMPLEMENTER_REPORT.md (Open questions section)"],
                        config,
                    )
                )
        else:
            # Subagent's turn.
            cmd = f"Use the {next_agent} subagent on \"{task}\""
            extra = ""
            if status == "CESAR_REJECTED":
                extra = " (read CESAR_REJECTION.md first)"
                record_review_miss(task, task_dir)
            next_steps.append(f"  [{task}] STATUS={status} -> {cmd}{extra}")
            # Cesar's standing rule: surface this iteration's review image in the
            # main chat BEFORE chaining the reviewer, so Cesar can catch issues
            # live. The orchestrator must Read+display it (non-blocking).
            if status in SURFACE_STATES:
                shot = canonical_shot_rel(task_dir)
                if shot:
                    next_steps.append(
                        f"    -> 📸 SURFACE IN CHAT FIRST: Read `{shot}` and display it "
                        f"to Cesar (+ a still per new video in {task}/videos/) BEFORE "
                        f"dispatching {next_agent}. Non-blocking — proceed after."
                    )
                else:
                    next_steps.append(
                        f"    -> 📸 SURFACE IN CHAT FIRST: display the iteration's "
                        f"canonical screenshot from {task}/screenshots/ to Cesar "
                        f"before dispatching {next_agent} (report had no "
                        f"`Canonical screenshot:` line)."
                    )

    if not next_steps and not cesar_alert_lines:
        return 0

    print("")
    print("=" * 72)
    print("GOLFIN PIPELINE STATE")
    print("=" * 72)

    if cesar_alert_lines:
        print("")
        print(">>> CESAR: action required <<<")
        for line in cesar_alert_lines:
            print(line)

    if next_steps:
        print("")
        print("Pipeline next-steps:")
        for line in next_steps:
            print(line)

    misses = _miss_count()
    if misses:
        print("")
        print(f"Review scoreboard: {misses} architect-PASS->Cesar-reject miss(es) "
              f"logged in .claude/review_misses.log. Reviewer/red-team: tighten up.")

    print("=" * 72)
    print("")

    return 0


if __name__ == "__main__":
    sys.exit(main())
