#!/usr/bin/env python3
"""launchd entry point for a scheduled TestFlight build.

WHY THIS FILE EXISTS — launchd cannot run /bin/bash against this repo.
    The repo lives under ~/Documents, which macOS protects with TCC. A launchd agent has no UI,
    so the consent prompt can never be shown and access is refused outright. On 2026-08-18 an
    agent whose program was /bin/bash died at 23:33 in under a second:

        /bin/bash: .../Tools/testflight-unattended.sh: Operation not permitted   (exit 126)

    and the overnight build was lost. Measured the next morning: /bin/bash cannot even read the
    repo's working directory from launchd, while Docs/Scripts/.venv/bin/python — the binary
    com.golfin.dailyreport has used 6,169 times with exit 0 — drives repo shell scripts fine.

    So the agent's program is that python, and this file hands the real work to the shell wrapper
    as a child process, which inherits the working TCC context.

    Full post-mortem: tasks/lessons.md Lesson AY.

Run it directly to rehearse:  Docs/Scripts/.venv/bin/python Docs/Scripts/run_testflight.py
"""
import datetime
import pathlib
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parents[2]
WRAPPER = REPO / "Tools" / "testflight-unattended.sh"
LOG = REPO / "Builds" / "testflight-unattended.log"
LABEL = sys.argv[1] if len(sys.argv) > 1 else ""


def log(msg):
    LOG.parent.mkdir(parents=True, exist_ok=True)
    with LOG.open("a") as f:
        f.write("%s  [runner] %s\n" % (datetime.datetime.now().strftime("%F %H:%M:%S"), msg))


def main():
    log("launchd fired the runner (python %s)" % sys.version.split()[0])
    if not WRAPPER.exists():
        log("FATAL: %s missing" % WRAPPER)
        return 1
    cmd = ["/bin/bash", str(WRAPPER)]
    if LABEL:
        cmd += ["--oneshot-label", LABEL]
    log("exec: %s" % " ".join(cmd))
    code = subprocess.run(cmd, cwd=str(REPO)).returncode
    log("wrapper exited %d" % code)
    return code


if __name__ == "__main__":
    sys.exit(main())
