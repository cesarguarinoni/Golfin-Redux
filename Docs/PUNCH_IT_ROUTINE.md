# "Punch it" — what Claude Code does on that one word

For the Architect. The mechanics of the lane live in `Docs/TESTFLIGHT_RUNBOOK.md` § One command;
this is the *operating agreement* around it — what runs unattended, what stops and asks, and why.

## Two variants, two phrases

| Phrase | Command | Profile | GPS surface |
|---|---|---|---|
| **"punch it"** | `./Tools/testflight.sh` | `iOS-Full` (no define) | **Off** — the five GPS screens refuse to open (`GpsGate`), and the Home banner that routes to `golfin://gps` is hidden with its slot collapsed |
| **"punch it GPS"** | `./Tools/testflight.sh testflight_build_gps` | `iOS-Full-GPS` (`GOLFIN_GPS`) | **On** — banner shows, tap routes to the hub, all GPS screens reachable |

Both lanes share one body (`testflight_build_shared`); the only difference is the argument to the
Unity step. The server banner row stays LIVE either way — the non-GPS build hides it client-side,
it is not deactivated for everyone.

### Shipping BOTH variants of the same commit

The build number is the commit count and App Store Connect requires it unique, so the two runs are
**sequential with a commit between them**:

    punch it  →  mark-uploaded.sh dirties the guard file  →  commit it  →  punch it GPS

The upload guard enforces that order anyway (a second build at the same commit is refused); the
sequence above just makes it deliberate. Record which build number is which variant — **on device
the tell is the Home banner**: present = GPS build, absent = standard.

## The routine

Cesar says **"punch it"**. Claude Code then, in order:

1. **Preflight** — `git status`, `git rev-list --count HEAD` (= the build number), the upload guard
   (`Docs/Versioning/last_uploaded_build.txt`), whether Unity holds `Temp/UnityLockfile`, and the
   content gate's **verdict line** (`export_content.py --check`).
2. **Clear the two preconditions** the lane refuses to run without — a clean tree and a closed
   Editor — under the standing rules below.
3. **Run `./Tools/testflight.sh`** (never bare `fastlane`: the wrapper exports the locale before
   Ruby starts, without which gym dies ~3 s into the archive on `➜`).
4. **Confirm at Apple**, not in fastlane's log: poll the App Store Connect API until the build
   reports `state=VALID`.
5. **Report** — per-step timings, the build number, what was swept into the commit.

Typical: **~10 min** for the lane (Unity ~100 s, archive ~7 min, upload ~80 s) plus **~5 min** for
Apple to surface the build.

## Standing permissions (granted by Cesar, 2026-08)

- **Quitting Unity: yes** — but only after reading scene dirty state over MCP. Nothing unsaved →
  quit and build. **Never force-quit blind.**
- **The upload itself: yes** — "punch it" is the authorization. Claude does not re-ask per build.
- **Sweeping the tree into a commit: yes for ordinary work** (docs, specs, art, data). The commit
  message says plainly what was swept and that nobody reviewed it.

## What stops and asks

| Situation | Why it stops | Who decides |
|---|---|---|
| Unsaved scene, or MCP down so dirt can't be read | Quitting would discard work Claude cannot see | Cesar quits, or says force-quit |
| Content **drift** (CSV has ids the catalog lacks) | Fixing it writes to the live content DB | Cesar |
| Content **stale** (catalog ahead of the repo) | Export → commit → rerun; changes shipped content | Cesar, unless manifest-only |
| A sweep would commit large binaries | 116 MB nearly entered history once; `~` folders fool git, not Unity | Cesar |
| Unfinished feature work in the sweep | Ships unreviewed code to testers | Cesar |

## Facts that keep biting

- **Build number = commit count.** A dirty tree means the number doesn't describe the binary — that
  is why the lane refuses one, not fussiness.
- **The guard file is dirty after every run, by design.** `mark-uploaded.sh` writes it and never
  commits.
- **Read the `--check` *verdict* line, not the summary table.** The table prints "unchanged" per
  catalog while the verdict above it says FAILED. Claude reported a clean check once from the tail
  and was wrong.
- **Scheduling needs `Docs/Scripts/run_testflight.py` under the daily-report venv python.** launchd
  cannot exec `/bin/bash` against `~/Documents` (TCC, exit 126) — that cost an overnight build.
  Nothing is called "armed" until it has fired once for real. See `tasks/lessons.md` Lesson AY.

## Not automated on purpose

Assigning testers, release notes, the App Store submission, and any decision about *what* ships.
The lane produces a TestFlight build from the current commit; it does not decide the commit.
