# Daily Report — Media drop folder

Drop **any file** in this folder that you want attached to the next GOLFIN daily
Telegram report. **Videos** (`.mp4 .mov .webm .m4v .avi .mkv`) and **images**
(`.png .jpg .jpeg .webp .gif`) are sent as media; **anything else** (`.docx`,
`.pdf`, `.csv`, `.zip`, …) is sent as a document. No extension filtering — if you
put it here, it goes out.

How it works (`Docs/Scripts/daily_report.py`):

1. When the report runs, every file in this folder is sent to the Telegram
   chat **after** the text summary, alongside any videos that appeared in today's
   git commits.
2. **Files here are deleted after a successful send.** This `README.md` and
   `.gitkeep` are never sent or deleted.
3. Telegram's Bot API caps uploads at **50 MB**. Oversize **videos** are
   **auto-compressed** to fit (two-pass, *same resolution* — only the bitrate
   drops) and then sent; the original drop file is deleted like any other
   successful send, so **keep your master copy elsewhere** if you need it.
   Oversize **non-video** files (images, zips, …) can't be transcoded and are
   skipped (and **not** deleted) so you know they didn't go out.
4. **Anything that still couldn't be attached** — an uncompressible oversize
   file, or an upload that failed — is listed in a short follow-up message
   posted to the Telegram chat, so recipients aren't left assuming everything
   went out.

The folder's contents are git-ignored (only `README.md` and `.gitkeep` are
tracked), so dropped media never gets committed.
