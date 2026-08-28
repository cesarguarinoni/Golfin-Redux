READY_FOR_SELF_REVIEW

Reviewer iter-2 FAILED on SPEC item 9: `content_version.txt` read `modes=4` while
prod was at v6 — my own live rollback verification bumped the version and I did
not re-export. Re-derived before accepting it (disk 4, prod 6, --check exit 1),
fixed, and `--check --catalogs modes` is now exit 0 with modes.csv byte-identical.

SECOND instance of one shape (iter-1 caught it, iter-2 missed it), so the shape
was audited rather than the instance: all nine catalog cursors enumerated against
prod — only `modes` was stale, now 0 stale. Root cause written into
Tools/content/README.md: a rollback publishes FORWARD, so "restoring" leaves the
version higher than it started and the cursor is the only trace.

Also recorded: the iter-2 self-review claimed that command exited 0. It did not.
The reviewer catching a false PASS from the gate before it is the design working.
