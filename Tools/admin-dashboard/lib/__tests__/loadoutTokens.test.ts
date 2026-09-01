import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { validateCatalog, type DraftRow, type ValidationContext } from "../contentValidate";
import { ironLoft, isKnown, matches } from "../loadoutTokens";

/**
 * The loadout mask vocabulary, and the PARITY that lets it exist twice.
 *
 * `lib/loadoutTokens.ts` and `Assets/Scripts/UI/MissionSelection/LoadoutTokens.cs` are the same
 * grammar in two languages, because the dashboard cannot import C# and the game cannot import
 * TypeScript. `Tools/content/tests/loadout_tokens_fixture.csv` is the contract between them: this
 * suite runs every row through the TypeScript one, and `LoadoutTokensTests` (EditMode) runs the
 * same rows through the C# one. A divergence fails one of the two.
 *
 * The second half is the regression this task exists for. `mission_loadouts` could not be
 * published at all: the supplied rule compared a mask token to the raw `clubs.type` column, so
 * `Iron7`, `Iron9`, `AW` and `PW` were all "a club nobody makes" — 17 errors on a catalog whose
 * every row is correct. That is checked against the REPO's Clubs.csv rather than a fixture, for
 * the reason missionScore.test.ts gives: a fixture drifts from the file that ships.
 */

const REPO = join(__dirname, "../../../..");
const DATA = join(REPO, "Assets/Resources/Data");

/** RFC4180-ish single line — Clubs.csv `info` is quoted and full of commas. */
function parseLine(line: string): string[] {
  const out: string[] = [];
  let field = "";
  let quoted = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (quoted) {
      if (ch === '"' && line[i + 1] === '"') { field += '"'; i++; }
      else if (ch === '"') quoted = false;
      else field += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === ",") { out.push(field); field = ""; }
    else field += ch;
  }
  out.push(field);
  return out;
}

function loadCsv(path: string): Array<Record<string, string>> {
  const lines = readFileSync(path, "utf-8")
    .split("\n")
    .map((l) => l.replace(/\r$/, ""))
    .filter((line) => line.length > 0 && !line.startsWith("#"));
  const header = parseLine(lines[0]!);
  return lines.slice(1).map((line) => {
    const values = parseLine(line);
    return Object.fromEntries(header.map((h, i) => [h, values[i] ?? ""]));
  });
}

// ── the shared fixture ──────────────────────────────────────────────────────

const FIXTURE_PATH = join(REPO, "Tools/content/tests/loadout_tokens_fixture.csv");
const fixture = loadCsv(FIXTURE_PATH);

describe("the shared parity fixture", () => {
  it("is the file the EditMode suite reads, and is not empty", () => {
    // A fixture that silently stopped loading would turn every it.each below into
    // zero assertions, and this suite would go green while proving nothing.
    expect(FIXTURE_PATH).toContain("Tools/content/tests/loadout_tokens_fixture.csv");
    expect(fixture.length).toBeGreaterThanOrEqual(13);
  });

  it.each(fixture.map((r) => [r.clubId!, r.name!, r.type!, r.token!, r.expected!] as const))(
    "%s (%s / %s) vs token %s → %s",
    (clubId, name, type, token, expected) => {
      expect(matches({ id: clubId, name, type }, token)).toBe(expected === "true");
    }
  );
});

describe("ironLoft is ANCHORED, not a digit hunt", () => {
  it("reads the number out of the name", () => {
    expect(ironLoft("club_iron_mireo_common", "Iron 7 MireO")).toBe(7);
    expect(ironLoft("club_iron_x", "Iron 5 X7")).toBe(5);
  });

  it("falls back to the id when the name carries no number", () => {
    expect(ironLoft("club_iron7_y", "FAIRLOFT Iron")).toBe(7);
  });

  it("returns null for a loft-less iron", () => {
    expect(ironLoft("club_iron_z", "GOLFIN Iron")).toBeNull();
  });
});

describe("isKnown", () => {
  it("knows the families and the single-digit irons", () => {
    for (const t of ["Driver", "wood", "Iron", "AW", "pw", "SW", "Putter", "Iron4", "Iron9"]) {
      expect(isKnown(t)).toBe(true);
    }
  });

  it("does not know a type spelled the CSV's way, or anything else", () => {
    for (const t of ["A.Wedge", "Iron10", "Irons", "", "Hybrid"]) {
      expect(isKnown(t)).toBe(false);
    }
  });
});

// ── the full shipped catalog ────────────────────────────────────────────────

const clubDraft = (r: Record<string, string>): DraftRow => ({
  rowId: r.id!,
  data: r,
  minBuild: 0,
  isActive: true,
});

const clubRows = loadCsv(join(DATA, "Clubs.csv"));
const clubs = new Map<string, DraftRow>(clubRows.map((r) => [r.id!, clubDraft(r)]));

const loadoutRows: DraftRow[] = loadCsv(join(DATA, "mission_loadouts.csv")).map((r) => ({
  rowId: r.id!,
  data: r,
  minBuild: 0,
  isActive: true,
}));

const ctx = (): ValidationContext => ({
  publishedMinBuild: new Map(),
  otherCatalogs: new Map([["clubs", clubs]]),
});

describe("mission_loadouts as it actually ships", () => {
  it("loaded the real catalogs", () => {
    expect(clubRows).toHaveLength(799);
    expect(loadoutRows).toHaveLength(13);
  });

  it("publishes with ZERO errors against the repo's Clubs.csv", () => {
    // The whole point of the task: this was 17 errors, every one of them false.
    const problems = validateCatalog("mission_loadouts", loadoutRows, ctx());
    expect(problems.filter((p) => p.severity === "error").map((p) => p.message)).toEqual([]);
  });

  it("`ban:Iron` bans every one of the 114 irons", () => {
    // `ban:Iron7,Iron9` — what OWN_NO_IRONS used to say — reached 18 of them, so
    // mission 24 "No Irons Allowed" let 96 irons play.
    const banned = clubRows.filter((r) => matches({ id: r.id!, name: r.name!, type: r.type! }, "Iron"));
    expect(banned).toHaveLength(114);
    expect(clubRows.filter((r) => matches({ id: r.id!, name: r.name!, type: r.type! }, "Iron7"))).toHaveLength(12);
    expect(clubRows.filter((r) => matches({ id: r.id!, name: r.name!, type: r.type! }, "Iron9"))).toHaveLength(6);
  });

  it("OWN_NO_IRONS says ban:Iron", () => {
    // The data half of the fix. If someone reverts the CSV, the count above still
    // passes (it asks the grammar, not the row) — this is what notices.
    expect(loadoutRows.find((r) => r.rowId === "OWN_NO_IRONS")!.data.clubs).toBe("ban:Iron");
  });
});
