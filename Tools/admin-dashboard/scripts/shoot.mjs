/**
 * Screenshot the running dashboard to PNG files, for a task's `screenshots/`.
 *
 * The browser tools an agent drives return an image to the AGENT and write no
 * file, so evidence captured that way cannot be surfaced to Cesar or read by a
 * reviewer (user memory: "never cite an unsent screenshot"). This drives a
 * headless Chrome over CDP instead and writes real PNGs.
 *
 * Auth: the dashboard's own httpOnly mock session cookie is set directly, so
 * this only works against a MOCK_MODE server (`admin-dashboard-mock`, :3100).
 * That is the point — it must never be pointed at production, and mock mode's
 * absurd fixtures are what makes a fixture-vs-prod mix-up obvious.
 *
 *   node scripts/shoot.mjs <outDir> <baseUrl> <name>=<path> [<name>=<path> …]
 *
 * A path may carry `#lang=ja` to render that shot in Japanese, `#h=<px>` to
 * override the viewport height, `#scrollto=<id>` to bring a section into view,
 * and `#do=<step>|<step>` to drive the page through its real controls before
 * capturing — which is how the expanded-row, dialog and drawer-tab shots are
 * taken by CLICKING rather than by a URL nobody can reach by hand. A step is
 * either a button's visible label (`Force return`, optionally `@<n>` to pick
 * the nth match when a label repeats), `row:<text>` to expand the log row that
 * CONTAINS that text, `select:<value>` to pick an option in a <select>, or
 * `fill:<text>`, which types into the first visible
 * textarea (or, failing that, the text input of an open dialog — the Rotations
 * typed confirmations) through React's own value setter — assigning `.value`
 * alone does not fire onChange, so the button would stay disabled.
 *
 * Prefer `row:` over `@<n>` for a log row: an expanded row's button reads
 * "Hide" rather than "Timeline & actions", so it drops out of the match list
 * and every index after it shifts by one. That silently pointed a
 * post-force-return shot at the wrong loan on first use.
 */

import { mkdir, writeFile } from "node:fs/promises";
import { spawn } from "node:child_process";
import { setTimeout as sleep } from "node:timers/promises";
import { stepExpression } from "./steps.mjs";

const CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
const PORT = 9333;
const EMAIL = "cesar.guarinoni@wonderwall-g.com";

const [outDir, baseUrl, ...shots] = process.argv.slice(2);
if (!outDir || !baseUrl || shots.length === 0) {
  console.error("usage: node scripts/shoot.mjs <outDir> <baseUrl> <name>=<path> …");
  process.exit(2);
}

await mkdir(outDir, { recursive: true });

const chrome = spawn(
  CHROME,
  [
    "--headless=new",
    `--remote-debugging-port=${PORT}`,
    "--no-first-run",
    "--no-default-browser-check",
    "--user-data-dir=/tmp/golfin-shoot-profile",
    "--hide-scrollbars",
    "--force-device-scale-factor=2",
    "about:blank",
  ],
  { stdio: "ignore" }
);

let ws;
try {
  // Wait for the debugging endpoint.
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

  const { hostname } = new URL(baseUrl);
  for (const shot of shots) {
    const eq = shot.indexOf("=");
    const name = shot.slice(0, eq);
    let path = shot.slice(eq + 1);

    let lang = "en";
    let height = 1400;
    let clicks = [];
    let scrollTo = null;
    path = path.replace(/#.*$/, (frag) => {
      const langMatch = /lang=([a-z]+)/.exec(frag);
      if (langMatch) lang = langMatch[1];
      const hMatch = /h=(\d+)/.exec(frag);
      if (hMatch) height = Number(hMatch[1]);
      const cMatch = /do=([^#]+)/.exec(frag);
      if (cMatch) clicks = cMatch[1].split("|");
      const sMatch = /scrollto=([\w-]+)/.exec(frag);
      if (sMatch) scrollTo = sMatch[1];
      return "";
    });

    await send("Emulation.setDeviceMetricsOverride", {
      width: 1440,
      height,
      deviceScaleFactor: 2,
      mobile: false,
    });
    for (const [cname, value] of [
      ["golfin_admin_mock_session", encodeURIComponent(EMAIL)],
      ["golfin_admin_lang", lang],
    ]) {
      await send("Network.setCookie", { name: cname, value, domain: hostname, path: "/" });
    }

    await send("Page.navigate", { url: `${baseUrl}${path}` });
    // Next renders these panels client-side after their fetches land; a load
    // event is not the frame worth capturing.
    await sleep(6000);

    for (const step of clicks) {
      const expression = stepExpression(step);
      const { result } = await send("Runtime.evaluate", { expression, returnByValue: true });
      console.log(`  ${result.value}`);
      if (String(result.value).startsWith("NOT FOUND")) {
        throw new Error(`${name}: step "${step}" found nothing — the shot would be wrong`);
      }
      await sleep(2500);
    }

    if (scrollTo) {
      await send("Runtime.evaluate", {
        expression: `document.getElementById(${JSON.stringify(scrollTo)})?.scrollIntoView({ block: "start" })`,
      });
      await sleep(1200);
    }

    const { data } = await send("Page.captureScreenshot", { format: "png" });
    const file = `${outDir}/${name}.png`;
    await writeFile(file, Buffer.from(data, "base64"));
    console.log(`${file}  (${1440 * 2}x${height * 2}, lang=${lang})`);
  }
} finally {
  ws?.close();
  chrome.kill();
}
