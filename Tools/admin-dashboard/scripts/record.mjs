/**
 * Record the running dashboard as a clip, for a task's `videos/`.
 *
 * shoot.mjs writes one PNG per state; a flow — PREVIEW, MATERIALIZE, the typed
 * PUBLISH confirmation, the calendar turning LIVE — reads better as a clip than
 * as a gallery of stills. This drives the same headless Chrome over CDP, runs
 * the same `#do=` steps (scripts/steps.mjs) and, in parallel, captures a JPEG
 * of the viewport several times a second. The frames go to a concat list with
 * their measured durations, so ffmpeg assembles a real-time clip without the
 * driver having to hold a frame rate. Alongside the raw mp4 it writes a
 * `captions.json` sidecar (start/end seconds from the first frame) in the shape
 * `Docs/Scripts/build_bot_video.py --mode captionsjson` reads, so the caption
 * pass is the repo's existing tool, not a hand-rolled drawtext.
 *
 * Auth: the dashboard's own httpOnly mock session cookie is set directly, so
 * this only works against a MOCK_MODE server (`admin-dashboard-mock`, :3100).
 * Never point it at production; the MOCK DATA banner in every frame is the
 * proof of which one it was.
 *
 *   node scripts/record.mjs <outDir> <baseUrl> <path> <step> [<step> …]
 *
 * Steps are the shoot.mjs vocabulary (a button label, `row:`, `select:`,
 * `fill:`) plus, for a clip:
 *   caption:<text>   start a caption (ends when the next one starts)
 *   wait:<ms>        hold — let a notice be read, a fetch land
 *   scrollto:<id>    bring an element into view
 *   scroll:<px>      scroll the window by a delta
 *   lang:<code>      set the language cookie before navigation (default en)
 *
 * Output: <outDir>/raw.mp4 (padded with a black band underneath for captions),
 * <outDir>/captions.json, <outDir>/frames/ (kept for inspection).
 */

import { mkdir, writeFile } from "node:fs/promises";
import { spawn, spawnSync } from "node:child_process";
import { setTimeout as sleep } from "node:timers/promises";
import { homedir } from "node:os";
import { stepExpression } from "./steps.mjs";

const CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
const FFMPEG = `${homedir()}/.local/bin/ffmpeg`;
const PORT = 9334;
const EMAIL = "cesar.guarinoni@wonderwall-g.com";
const WIDTH = 1440;
const HEIGHT = 1100;
const SCALE = 2;
const CAPTION_BAND = 200; // px of black under the frame, at SCALE, for the caption pass
const FRAME_INTERVAL_MS = 150;

const [outDir, baseUrl, path, ...steps] = process.argv.slice(2);
if (!outDir || !baseUrl || !path) {
  console.error("usage: node scripts/record.mjs <outDir> <baseUrl> <path> <step> …");
  process.exit(2);
}
const lang = steps.find((s) => s.startsWith("lang:"))?.slice(5) ?? "en";

await mkdir(`${outDir}/frames`, { recursive: true });

const chrome = spawn(
  CHROME,
  [
    "--headless=new",
    `--remote-debugging-port=${PORT}`,
    "--no-first-run",
    "--no-default-browser-check",
    "--user-data-dir=/tmp/golfin-record-profile",
    "--hide-scrollbars",
    `--force-device-scale-factor=${SCALE}`,
    "about:blank",
  ],
  { stdio: "ignore" }
);

let ws;
try {
  let target = null;
  for (let i = 0; i < 60 && !target; i += 1) {
    await sleep(500);
    try {
      const list = await fetch(`http://127.0.0.1:${PORT}/json/list`).then((r) => r.json());
      target = list.find((t) => t.type === "page");
    } catch {
      /* not up yet */
    }
  }
  if (!target) throw new Error("Chrome's CDP endpoint never came up");

  ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((res, rej) => {
    ws.onopen = res;
    ws.onerror = rej;
  });

  let id = 0;
  const pending = new Map();
  ws.onmessage = (m) => {
    const msg = JSON.parse(m.data);
    if (msg.id && pending.has(msg.id)) {
      const { res, rej } = pending.get(msg.id);
      pending.delete(msg.id);
      msg.error ? rej(new Error(JSON.stringify(msg.error))) : res(msg.result);
    }
  };
  const send = (method, params = {}) =>
    new Promise((res, rej) => {
      id += 1;
      pending.set(id, { res, rej });
      ws.send(JSON.stringify({ id, method, params }));
    });

  await send("Page.enable");
  await send("Network.enable");
  await send("Emulation.setDeviceMetricsOverride", {
    width: WIDTH,
    height: HEIGHT,
    deviceScaleFactor: SCALE,
    mobile: false,
  });
  const { hostname } = new URL(baseUrl);
  for (const [cname, value] of [
    ["golfin_admin_mock_session", encodeURIComponent(EMAIL)],
    ["golfin_admin_lang", lang],
  ]) {
    await send("Network.setCookie", { name: cname, value, domain: hostname, path: "/" });
  }

  // ---- frame grabber: runs beside the driver until told to stop ----------
  const frames = []; // [{ t, file }]
  let recording = true;
  const t0 = Date.now();
  const grabber = (async () => {
    let n = 0;
    while (recording) {
      const t = (Date.now() - t0) / 1000;
      const { data } = await send("Page.captureScreenshot", { format: "jpeg", quality: 85 });
      const file = `${outDir}/frames/f${String(n).padStart(5, "0")}.jpg`;
      await writeFile(file, Buffer.from(data, "base64"));
      frames.push({ t, file });
      n += 1;
      await sleep(FRAME_INTERVAL_MS);
    }
  })();

  // ---- driver ------------------------------------------------------------
  const captions = []; // [{ start, end, text }]
  const now = () => (Date.now() - t0) / 1000;
  const caption = (text) => {
    const t = now();
    if (captions.length) captions[captions.length - 1].end = t;
    captions.push({ start: t, end: t, text });
    console.log(`  [${t.toFixed(1)}s] caption: ${text}`);
  };

  await send("Page.navigate", { url: `${baseUrl}${path}` });
  await sleep(6000);

  for (const step of steps) {
    if (step.startsWith("lang:")) continue;
    if (step.startsWith("caption:")) {
      caption(step.slice(8));
      continue;
    }
    if (step.startsWith("wait:")) {
      await sleep(Number(step.slice(5)));
      continue;
    }
    if (step.startsWith("scrollto:")) {
      await send("Runtime.evaluate", {
        expression: `document.getElementById(${JSON.stringify(step.slice(9))})?.scrollIntoView({ block: "start", behavior: "smooth" })`,
      });
      await sleep(1500);
      continue;
    }
    if (step.startsWith("scroll:")) {
      await send("Runtime.evaluate", {
        expression: `window.scrollBy({ top: ${Number(step.slice(7))}, behavior: "smooth" })`,
      });
      await sleep(1500);
      continue;
    }
    const { result } = await send("Runtime.evaluate", { expression: stepExpression(step), returnByValue: true });
    console.log(`  [${now().toFixed(1)}s] ${result.value}`);
    if (String(result.value).startsWith("NOT FOUND")) {
      throw new Error(`step "${step}" found nothing — the clip would be wrong`);
    }
    await sleep(2500);
  }
  if (captions.length) captions[captions.length - 1].end = now();

  recording = false;
  await grabber;

  // ---- assemble ----------------------------------------------------------
  // concat demuxer: each frame lasts until the next one was grabbed.
  const list = [];
  for (let i = 0; i < frames.length; i += 1) {
    const dur = i + 1 < frames.length ? frames[i + 1].t - frames[i].t : FRAME_INTERVAL_MS / 1000;
    list.push(`file '${frames[i].file}'`, `duration ${dur.toFixed(3)}`);
  }
  list.push(`file '${frames[frames.length - 1].file}'`);
  await writeFile(`${outDir}/frames.txt`, list.join("\n") + "\n");
  await writeFile(`${outDir}/captions.json`, JSON.stringify({ captions }, null, 2) + "\n");

  const raw = `${outDir}/raw.mp4`;
  const ff = spawnSync(
    FFMPEG,
    [
      "-y", "-f", "concat", "-safe", "0", "-i", `${outDir}/frames.txt`,
      "-vf", `pad=iw:ih+${CAPTION_BAND}:0:0:black,fps=10`,
      "-c:v", "libx264", "-pix_fmt", "yuv420p", "-preset", "medium", "-crf", "20",
      "-movflags", "+faststart", raw,
    ],
    { stdio: ["ignore", "ignore", "pipe"] }
  );
  if (ff.status !== 0) throw new Error(`ffmpeg failed:\n${ff.stderr}`);
  console.log(`${raw}  (${frames.length} frames, ${now().toFixed(1)}s, ${captions.length} captions)`);
} finally {
  ws?.close();
  chrome.kill();
}
