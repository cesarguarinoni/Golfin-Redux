# "Punch it" — what Claude Code does on that one word

For the Architect. The mechanics of the lane live in `Docs/TESTFLIGHT_RUNBOOK.md` § One command;
this is the *operating agreement* around it — what runs unattended, what stops and asks, and why.

## Three variants, three phrases

| Phrase | Command | Profile | What ships | Tell on device |
|---|---|---|---|---|
| **"punch it"** | `./Tools/testflight.sh` | `iOS-Full` (no define) | The game. GPS surface **off** — the GPS screens refuse to open (`GpsGate`) and the Home banner that routes to `golfin://gps` is hidden with its slot collapsed | Icon **Golfin**, no Home GPS banner |
| **"punch it GPS"** | `./Tools/testflight.sh testflight_build_gps` | `iOS-Full-GPS` (`GOLFIN_GPS`) | The game + the GPS surface — banner shows, tap routes to the hub, all GPS screens reachable | Icon **Golfin**, Home GPS banner present |
| **"punch it standalone"** | `./Tools/testflight.sh testflight_build_standalone` | `iOS-Standalone` (`GOLFIN_GPS;GOLFIN_STANDALONE`, ShellScene-only scene list) | PLAYLIFE only — boots past Splash/Login straight to the GPS hub. No Home, no golf, no bottom nav, no ticket cluster (`StandaloneGate`) | Icon **GPS/PLAYLIFE**, app name **GOLFIN GPS** |

All three lanes share one body (`testflight_build_shared`), which now takes a `variant:` symbol
(`:standard | :gps | :standalone`); the difference between them is one row of the `variant_table`
in the Fastfile. The server banner row stays LIVE for every variant — the non-GPS build hides it
client-side, it is not deactivated for everyone.

### Two App Store records, not three

| Variant | Bundle id | ASC app | Apple ID | Upload guard file |
|---|---|---|---|---|
| punch it / punch it GPS | `com.nextinnovation.golfingame` | GOLFIN | — | `Docs/Versioning/last_uploaded_build.txt` |
| punch it standalone | `com.nextinnovation.golfingps` | GOLFIN GPS | 6737145432 | `Docs/Versioning/last_uploaded_build.golfingps.txt` |

Same team (`TCUV4A9VTJ`), so no new signing identity. The standalone's identity (bundle id,
product name `GOLFIN GPS`, version `1.0.0`, icon, the `golfingps://` URL scheme) is applied at
build time by `StandaloneBuildPreprocessor` and **restored afterwards** — `ProjectSettings.asset`
is byte-identical before and after, exactly like the build-number stamp.

### Shipping several variants of the same commit

App Store Connect requires the build number unique **per app**, and the build number is the commit
count. So:

- **punch it + punch it GPS** are the same record → **sequential with a commit between them**:

      punch it  →  mark-uploaded.sh dirties the guard file  →  commit it  →  punch it GPS

- **punch it standalone** is a different record with its own guard file → it never collides with
  either of the other two, and can run at the same commit as a game build.

The upload guard enforces this anyway (a second build at the same commit **on the same record** is
refused); the sequence above just makes it deliberate.

## The routine

Cesar says **"punch it"**. Claude Code then, in order:

1. **Preflight** — `git status`, `git rev-list --count HEAD` (= the build number), the upload guard
   **for the record being shipped** (`last_uploaded_build.txt`, or
   `last_uploaded_build.golfingps.txt` for the standalone), whether Unity holds
   `Temp/UnityLockfile`, and the content gate's **verdict line** (`export_content.py --check`).
2. **Clear the two preconditions** the lane refuses to run without — a clean tree and a closed
   Editor — under the standing rules below.
3. **Run `./Tools/testflight.sh [lane]`** (never bare `fastlane`: the wrapper exports the locale
   before Ruby starts, without which gym dies ~3 s into the archive on `➜`).
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
  commits. It takes the record as its second argument (`game` | `standalone`) — the lane passes it,
  and so does the Xcode archive post-action, resolved at build time.
- **Read the `--check` *verdict* line, not the summary table.** The table prints "unchanged" per
  catalog while the verdict above it says FAILED. Claude reported a clean check once from the tail
  and was wrong.
- **Scheduling needs `Docs/Scripts/run_testflight.py` under the daily-report venv python.** launchd
  cannot exec `/bin/bash` against `~/Documents` (TCC, exit 126) — that cost an overnight build.
  Nothing is called "armed" until it has fired once for real. See `tasks/lessons.md` Lesson AY.

## Not automated on purpose

Assigning testers, release notes, the App Store submission, and any decision about *what* ships.
The lane produces a TestFlight build from the current commit; it does not decide the commit.
