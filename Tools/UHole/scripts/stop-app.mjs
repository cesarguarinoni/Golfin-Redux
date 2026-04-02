import { readFile, rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const sessionPath = path.join(root, ".course-intake", "session.json");

async function main() {
  try {
    const session = JSON.parse(await readFile(sessionPath, "utf8"));
    if (session.pid) {
      process.kill(session.pid);
    }
  } catch {
    // Nothing to stop.
  } finally {
    await rm(sessionPath, { force: true });
  }
}

main().catch(() => {
  process.exitCode = 0;
});
