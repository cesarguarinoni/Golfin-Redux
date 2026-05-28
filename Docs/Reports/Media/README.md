# Daily Report — Media drop folder

Drop any **videos** (`.mp4 .mov .webm .m4v .avi .mkv`) or **images**
(`.png .jpg .jpeg .webp .gif`) in this folder that you want attached to the next
GOLFIN daily Telegram report.

How it works (`Docs/Scripts/daily_report.py`):

1. When the report runs, every media file in this folder is sent to the Telegram
   chat **after** the text summary, alongside any videos that appeared in today's
   git commits.
2. **Files here are deleted after a successful send.** This `README.md` and
   `.gitkeep` are never sent or deleted.
3. Telegram's Bot API caps uploads at **50 MB**. Files larger than that are
   skipped (and reported in the run log), and are **not** deleted — so you'll
   know they didn't go out.

The folder's contents are git-ignored (only `README.md` and `.gitkeep` are
tracked), so dropped media never gets committed.
