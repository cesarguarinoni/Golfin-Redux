/**
 * Blocking validation for a content publish (SPEC content_catalog §D1).
 *
 * PURE. No Supabase, no `server-only`, no I/O — everything it needs arrives in
 * `ValidationContext`. That is deliberate: this is the one place where a bad
 * publish is stopped, so it has to be testable without a database.
 *
 * A failure returns the FULL list of problems and publishes NOTHING. Never a
 * partial publish: half a catalog is a state no client can reason about, and
 * §2 I1 (the bundled CSV is the floor) only holds if what lands is coherent.
 *
 * Errors block. Warnings do not — see `rpCost` at the bottom.
 */

export type Severity = "error" | "warning";

export interface ContentProblem {
  severity: Severity;
  rowId: string | null;
  column: string | null;
  message: string;
}

export interface DraftRow {
  rowId: string;
  data: Record<string, unknown>;
  minBuild: number;
  isActive: boolean;
}

export interface ValidationContext {
  /**
   * Published `min_build` per row for THIS catalog. Present ⇒ the row is already
   * published, and §D1.7 makes its min_build immutable.
   */
  publishedMinBuild: Map<string, number>;
  /**
   * Draft rows of the OTHER catalogs, by catalog name — what `shop_catalog.refId`
   * resolves against (§D1.6). Drafts, not published rows: a shop entry and the
   * club it points at are normally published together, and validating against
   * published state would reject that correct edit.
   */
  otherCatalogs: Map<string, Map<string, DraftRow>>;
}

/**
 * Mirrored from Assets/Scripts/UI/Roster/Data/RarityStatCaps.cs
 * (`RarityStatCaps.GetStatCaps`) — read from that file on 2026-08-25, NOT
 * re-derived from the economy workbook. If the C# changes, change this and say
 * so in the same commit; a cap that is looser here silently lets a publish
 * through that the game will clamp.
 */
export const RARITY_STAT_CAPS: Record<string, { strength: number; clubControl: number; recovery: number; stamina: number }> = {
  Common: { strength: 25, clubControl: 25, recovery: 18, stamina: 22 },
  Uncommon: { strength: 28, clubControl: 28, recovery: 19, stamina: 25 },
  Rare: { strength: 30, clubControl: 30, recovery: 20, stamina: 27 },
  Mythic: { strength: 35, clubControl: 35, recovery: 25, stamina: 32 },
  Legendary: { strength: 40, clubControl: 40, recovery: 40, stamina: 40 },
  Supreme: { strength: 50, clubControl: 50, recovery: 50, stamina: 50 },
};

/** CharacterRarity, in ladder order. Same six as RarityHelper. */
export const RARITIES = ["Common", "Uncommon", "Rare", "Mythic", "Legendary", "Supreme"] as const;

/**
 * Advisory RP price band per rarity — ECONOMY_MASTER.md §3 ("Club roster (C2)").
 * WARNING ONLY (§D1.8). Prices are the single thing most likely to be
 * deliberately tuned, and blocking here would invent a rule Cesar did not set.
 */
const RP_BAND: Record<string, [number, number]> = {
  Common: [50, 200],
  Uncommon: [100, 400],
  Rare: [200, 800],
  Mythic: [400, 1600],
  Legendary: [750, 3000],
  Supreme: [1500, 6000],
};

/** Columns a row of each catalog must carry (§D1.1). The id column is added below. */
const REQUIRED: Record<string, string[]> = {
  clubs: ["id", "name", "type", "rarity", "brand", "basePower", "baseAccuracy", "maxDurability", "startLevel", "maxLevel"],
  characters: ["id", "name", "lastName", "rarity", "baseStrength", "baseClubControl", "baseRecovery", "baseStamina", "startLevel", "maxLevel"],
  items: ["id", "name", "category", "rarity"],
  bags: ["id", "name", "rarity"],
  balls: ["id", "name", "brand"],
  texts: ["key", "English", "Japanese"],
  shop_catalog: ["entryId", "category", "refId", "rpCost", "sortOrder"],
};

/** Columns that must parse as a number wherever they are present (§D1.3). */
const NUMERIC: Record<string, string[]> = {
  clubs: ["basePower", "baseAccuracy", "baseLieResistance", "baseLoft", "maxDurability", "baseDistance",
          "ballSpeedMps", "launchAngleDeg", "spinRateRpm", "expectedCarryYd", "startLevel", "maxLevel"],
  characters: ["baseStrength", "baseClubControl", "baseRecovery", "baseStamina", "startLevel", "maxLevel"],
  items: ["restorePercent"],
  bags: [],
  balls: ["power", "rebound", "windResistance", "roll", "spin"],
  texts: [],
  shop_catalog: ["rpCost", "saleRpCost", "sortOrder"],
};

/** `shop_catalog.category` → the catalog `refId` resolves in (§D1.6). */
export const SHOP_CATEGORY_TO_CATALOG: Record<string, string> = {
  club: "clubs",
  ball: "balls",
  item: "items",
  bag: "bags",
  character: "characters",
};

export const ID_COLUMN: Record<string, string> = {
  clubs: "id",
  characters: "id",
  items: "id",
  bags: "id",
  balls: "id",
  texts: "key",
  shop_catalog: "entryId",
};

/** The four character stat columns, paired with their cap key. */
const CHARACTER_STATS: Array<[string, keyof (typeof RARITY_STAT_CAPS)["Common"]]> = [
  ["baseStrength", "strength"],
  ["baseClubControl", "clubControl"],
  ["baseRecovery", "recovery"],
  ["baseStamina", "stamina"],
];

function text(v: unknown): string {
  return v === null || v === undefined ? "" : String(v);
}

/** A numeric CSV field. Empty means "absent", which is not a parse failure. */
function num(v: unknown): number | null {
  const s = text(v).trim();
  if (s === "") return null;
  const n = Number(s);
  return Number.isFinite(n) ? n : null;
}

export function validateCatalog(
  catalog: string,
  rows: DraftRow[],
  ctx: ValidationContext
): ContentProblem[] {
  const problems: ContentProblem[] = [];
  const err = (rowId: string | null, column: string | null, message: string) =>
    problems.push({ severity: "error", rowId, column, message });
  const warn = (rowId: string | null, column: string | null, message: string) =>
    problems.push({ severity: "warning", rowId, column, message });

  const idColumn = ID_COLUMN[catalog];
  if (!idColumn) {
    err(null, null, `Unknown catalog "${catalog}".`);
    return problems;
  }
  if (rows.length === 0) {
    err(null, null, "No draft rows — publishing an empty catalog would bump the version for nothing.");
    return problems;
  }

  // 1. row_id non-empty and unique; required columns present.
  const seen = new Set<string>();
  for (const row of rows) {
    if (!row.rowId.trim()) {
      err(null, idColumn, "Empty row id.");
      continue;
    }
    if (seen.has(row.rowId)) {
      err(row.rowId, idColumn, `Duplicate ${idColumn} "${row.rowId}".`);
    }
    seen.add(row.rowId);

    // The id column inside `data` must agree with row_id — the exporter writes
    // `data`, so a mismatch would put a different id in the CSV than the one
    // every other catalog resolves against.
    const inner = text(row.data[idColumn]).trim();
    if (inner && inner !== row.rowId) {
      err(row.rowId, idColumn, `data.${idColumn} is "${inner}" but the row id is "${row.rowId}".`);
    }

    for (const column of REQUIRED[catalog] ?? []) {
      if (!(column in row.data)) {
        err(row.rowId, column, `Missing required column "${column}".`);
      }
    }
  }

  for (const row of rows) {
    // 2. rarity is one of the six, wherever the column exists.
    if ("rarity" in row.data) {
      const rarity = text(row.data.rarity).trim();
      // shop_catalog.rarity is an optional display override and may be blank.
      if (rarity !== "" && !(RARITIES as readonly string[]).includes(rarity)) {
        err(row.rowId, "rarity", `Rarity "${rarity}" is not one of ${RARITIES.join(", ")}.`);
      }
    }

    // 3. numeric columns parse; startLevel <= maxLevel.
    for (const column of NUMERIC[catalog] ?? []) {
      if (!(column in row.data)) continue;
      const raw = text(row.data[column]).trim();
      if (raw === "") continue;
      if (num(raw) === null) {
        err(row.rowId, column, `"${raw}" is not a number.`);
      }
    }
    const start = num(row.data.startLevel);
    const max = num(row.data.maxLevel);
    if (start !== null && max !== null && start > max) {
      err(row.rowId, "startLevel", `startLevel ${start} is above maxLevel ${max}.`);
    }

    // 4. character stats within the rarity caps.
    if (catalog === "characters") {
      const rarity = text(row.data.rarity).trim();
      const caps = RARITY_STAT_CAPS[rarity];
      if (caps) {
        for (const [column, capKey] of CHARACTER_STATS) {
          const value = num(row.data[column]);
          if (value === null) continue;
          if (value < 0) err(row.rowId, column, `${column} ${value} is negative.`);
          if (value > caps[capKey]) {
            err(
              row.rowId,
              column,
              `${column} ${value} exceeds the ${rarity} cap of ${caps[capKey]} (RarityStatCaps.cs).`
            );
          }
        }
      }
    }

    // 5. texts need BOTH locales. A key with no Japanese renders as English in a
    //    Japanese build, which reads as a missing translation nobody logged.
    if (catalog === "texts") {
      for (const column of ["English", "Japanese"]) {
        if (!text(row.data[column]).trim()) {
          err(row.rowId, column, `"${column}" is empty — every key needs both locales.`);
        }
      }
    }

    // 7. min_build is immutable once published (§5). Changing it retroactively
    //    hides a row from builds that already received it.
    const publishedMinBuild = ctx.publishedMinBuild.get(row.rowId);
    if (publishedMinBuild !== undefined && publishedMinBuild !== row.minBuild) {
      err(
        row.rowId,
        "min_build",
        `min_build is immutable once published (${publishedMinBuild} → ${row.minBuild}). ` +
          "Deactivate the row and add a new one instead."
      );
    }
  }

  // 6. shop_catalog referential integrity.
  if (catalog === "shop_catalog") {
    for (const row of rows) {
      const category = text(row.data.category).trim();
      const refId = text(row.data.refId).trim();
      const target = SHOP_CATEGORY_TO_CATALOG[category];

      if (!target) {
        err(row.rowId, "category", `Unknown category "${category}". Known: ${Object.keys(SHOP_CATEGORY_TO_CATALOG).join(", ")}.`);
      } else if (!refId) {
        err(row.rowId, "refId", "refId is empty.");
      } else {
        const referenced = ctx.otherCatalogs.get(target)?.get(refId);
        if (!referenced) {
          err(row.rowId, "refId", `refId "${refId}" does not exist in the ${target} catalog.`);
        } else if (!referenced.isActive) {
          err(row.rowId, "refId", `refId "${refId}" is deactivated in ${target} — the shop would offer an item the game hides.`);
        }
      }

      const rpCost = num(row.data.rpCost);
      const saleRpCost = num(row.data.saleRpCost);
      if (rpCost !== null && rpCost < 0) err(row.rowId, "rpCost", `rpCost ${rpCost} is negative.`);
      if (saleRpCost !== null && saleRpCost < 0) err(row.rowId, "saleRpCost", `saleRpCost ${saleRpCost} is negative.`);
      // §D1.6: "saleRpCost < rpCost when present", BLOCKING — restored by
      // content_cursor_per_catalog §6 after Phase 0 relaxed it to a warning.
      //
      // Relaxing was right in the moment: shop_catalog.csv shipped
      // shop_club_pwedge_royal at 600/600, and a validator that cannot publish
      // the catalog the game ships is a validator that gets switched off. But
      // that row also carried offer=false and popular=false — it was not on sale
      // at all, and 600/600 was "no sale" written as an equal price. The DATA was
      // the bug, so the data was fixed (saleRpCost blanked) and the rule is a
      // rule again.
      //
      // BLANK is the way to say "no sale": `num("")` is null, so an unset
      // saleRpCost never reaches this branch. An always-warn rule on a field
      // whose whole job is to mean "on sale" is a rule nobody reads.
      if (rpCost !== null && saleRpCost !== null && saleRpCost >= rpCost) {
        err(
          row.rowId,
          "saleRpCost",
          saleRpCost === rpCost
            ? `saleRpCost equals rpCost (${rpCost}) — that is not a sale. Leave saleRpCost BLANK when the row is not discounted.`
            : `saleRpCost ${saleRpCost} is above rpCost ${rpCost} — the "sale" costs more.`,
        );
      }

      // 8. WARN ONLY — the economy band, not a rule.
      const rarity =
        text(row.data.rarity).trim() ||
        text(target ? ctx.otherCatalogs.get(target)?.get(refId)?.data.rarity : "").trim();
      const band = RP_BAND[rarity];
      if (band && rpCost !== null && (rpCost < band[0] || rpCost > band[1])) {
        warn(
          row.rowId,
          "rpCost",
          `rpCost ${rpCost} is outside the ${rarity} band ${band[0]}–${band[1]} RP ` +
            "(ECONOMY_MASTER.md §3). Publishing anyway — prices are tuned deliberately."
        );
      }
    }
  }

  return problems;
}

export const hasErrors = (problems: ContentProblem[]): boolean =>
  problems.some((p) => p.severity === "error");
