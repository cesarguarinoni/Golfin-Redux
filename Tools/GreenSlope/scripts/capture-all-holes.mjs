// One-off: screenshot the GreenSlope tool for all 18 holes (whole tool, hole loaded).
// Drives the tool's own importExport() so each shot shows the authored arrows +
// aligned PDF backdrop. Screenshot is the page viewport only -> no OS chrome.
import puppeteer from "puppeteer-core";
import { readFile, mkdir } from "node:fs/promises";
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const OUT_DIR = path.join(ROOT, "screenshots", "holes");
const URL = "http://127.0.0.1:4178";
const CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";

await mkdir(OUT_DIR, { recursive: true });

const browser = await puppeteer.launch({
  executablePath: CHROME,
  headless: "new",
  defaultViewport: { width: 1600, height: 1000, deviceScaleFactor: 2 },
  args: ["--no-sandbox", "--hide-scrollbars"],
});

const page = await browser.newPage();
page.on("dialog", (d) => d.accept()); // auto-accept confirm()/alert() from importExport
page.on("pageerror", (e) => console.log("  [pageerror]", e.message));

await page.goto(URL, { waitUntil: "load" });
// auto-load fires on window 'load'; give it a moment
await new Promise((r) => setTimeout(r, 1500));

async function settle(hole) {
  // wait until loading overlay is gone and the backdrop image has decoded
  await page.waitForFunction(() => {
    const ld = document.getElementById("loading");
    const bd = document.getElementById("backdrop");
    const loadingHidden = !ld || ld.style.display === "none";
    const bdReady = bd && bd.complete && bd.naturalWidth > 0;
    return loadingHidden && bdReady;
  }, { timeout: 15000 });
  await new Promise((r) => setTimeout(r, 600)); // draw + transform settle
}

for (let h = 1; h <= 18; h++) {
  const nn = String(h).padStart(2, "0");
  const jsonPath = path.join(ROOT, "output", `hole_${nn}_slope_authoring.json`);
  if (!existsSync(jsonPath)) {
    console.log(`hole ${nn}: NO output JSON, skipping`);
    continue;
  }
  const payload = JSON.parse(await readFile(jsonPath, "utf8"));
  await page.evaluate((p) => importExport(p), payload);
  await settle(h);
  const dest = path.join(OUT_DIR, `hole_${nn}.png`);
  await page.screenshot({ path: dest });
  const arrows = (payload.arrows || []).length;
  console.log(`hole ${nn}: saved ${path.relative(ROOT, dest)}  (${arrows} arrows)`);
}

await browser.close();
console.log("DONE ->", OUT_DIR);
