import { spawn } from "node:child_process";
import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
import { createConnection } from "node:net";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const runtimeDir = path.join(root, ".course-intake");
const sessionPath = path.join(runtimeDir, "session.json");
const host = "127.0.0.1";
const candidatePorts = [4173, 4174, 4175, 4180, 4181, 4182];

async function main() {
  await mkdir(runtimeDir, { recursive: true });

  const existing = await readSession();
  if (existing && await isHttpAlive(existing.port)) {
    openBrowser(existing.port);
    return;
  }

  const port = await findAvailablePort();
  const child = spawn(process.execPath, ["scripts/dev-server.mjs"], {
    cwd: root,
    detached: true,
    stdio: "ignore",
    env: {
      ...process.env,
      PORT: String(port)
    }
  });

  child.unref();

  await waitForServer(port, 10000);
  await writeFile(sessionPath, JSON.stringify({ pid: child.pid, port }, null, 2) + "\n", "utf8");
  openBrowser(port);
}

async function readSession() {
  try {
    const raw = await readFile(sessionPath, "utf8");
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

async function findAvailablePort() {
  for (const port of candidatePorts) {
    if (!await isTcpInUse(port)) {
      return port;
    }
  }
  throw new Error("No available launch port found.");
}

function isTcpInUse(port) {
  return new Promise((resolve) => {
    const socket = createConnection({ host, port });
    socket.on("connect", () => {
      socket.destroy();
      resolve(true);
    });
    socket.on("error", () => {
      resolve(false);
    });
  });
}

async function isHttpAlive(port) {
  try {
    const response = await fetch(`http://${host}:${port}/`);
    return response.ok;
  } catch {
    return false;
  }
}

async function waitForServer(port, timeoutMs) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await isHttpAlive(port)) {
      return;
    }
    await delay(250);
  }
  throw new Error("Course Intake server did not start in time.");
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function openBrowser(port) {
  const url = `http://${host}:${port}`;
  spawn("cmd.exe", ["/c", "start", "", url], {
    cwd: root,
    detached: true,
    stdio: "ignore"
  }).unref();
}

main().catch(async (error) => {
  await rm(sessionPath, { force: true });
  throw error;
});
