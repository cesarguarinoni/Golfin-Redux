# Daily Report — Media drop folder

Drop **any file** in this folder that you want attached to the next GOLFIN daily
Telegram report. **Subfolders work** — grouping a batch as `GPS/*.png` is fine;
the scan recurses and an emptied subfolder is removed after the send. **Videos** (`.mp4 .mov .webm .m4v .avi .mkv`) and **images**
(`.png .jpg .jpeg .webp .gif`) are sent as media; **anything else** (`.docx`,
`.pdf`, `.csv`, `.zip`, …) is sent as a document. No extension filtering — if you
put it here, it goes out.

How it works (`Docs/Scripts/daily_report.py`):

1. When the report runs, every file in this folder **and its subfolders** is
   sent to the Telegram chat **after** the text summary, alongside any videos
   that appeared in today's git commits. Files in a subfolder are labelled by
   their relative path (`GPS/Score Upload flow.png`).
2. **Files here are deleted after a successful send.** This `README.md` and
   `.gitkeep` are never sent or deleted.
3. Telegram's Bot API caps uploads at **50 MB**. Oversize **videos** are
   **auto-compressed** to fit (two-pass, *same resolution* — only the bitrate
   drops) and then sent; the original drop file is deleted like any other
   successful send, so **keep your master copy elsewhere** if you need it.
   Oversize **non-video** files (images, zips, …) can't be transcoded and are
   skipped (and **not** deleted) so you know they didn't go out.
   **Images have a second, tighter limit** that applies only to Telegram's photo
   endpoint: **10 MB** and **width+height ≤ 10000 px**. An image over either is
   **re-encoded down to fit and still sent as a photo** — it is scaled only as
   far as the dimension rule forces (often not at all: a 12.7 MB image within the
   px budget keeps its full resolution and just loses bytes), then JPEG quality
   is walked down until it fits. Images are never sent as documents. The caption
   records the change, e.g. `(resized 19MB→3.0MB, 7454x2344)`.
4. **Anything that still couldn't be attached** — an uncompressible oversize
   file, or an upload that failed — is listed in a short follow-up message
   posted to the Telegram chat, so recipients aren't left assuming everything
   went out.

5. **If the media didn't go out but the report did**, don't re-run with
   `--force` (that posts a duplicate report). Use:

   ```bash
   Docs/Scripts/.venv/bin/python Docs/Scripts/daily_report.py --media-only
   ```

   which sends the attachments alone and leaves the idempotency marker and any
   staged note untouched.

The folder's contents are git-ignored (only `README.md` and `.gitkeep` are
tracked), so dropped media never gets committed.
