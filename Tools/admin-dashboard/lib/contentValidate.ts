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

import { SHOP_CATEGORY_STRICT_BUILD } from "./buildGates";
import { holeBase, scoreGoal, type WeightRow } from "./missionScore";

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
  /**
   * `game_point_actions.versus_win.pts` — the amount the server ACTUALLY pays for
   * a 1v1 win. Loaded only when publishing `modes`, and used by exactly one
   * warning (see rule 10). `undefined` = not loaded, `null` = the row exists with
   * a NULL `pts`, which for a fixed-payout action would itself be a problem the
   * Rewards panel refuses to create.
   */
  versusWinPts?: number | null;
  /**
   * `game_point_actions.mission_clear.max_per_event` — the ceiling the claim
   * path enforces. Loaded only when publishing `missions`, and used by exactly
   * one BLOCKING rule (missions rule 11): a `firstClearRP` above it is a mission
   * that pays less than its card promises, every time, forever.
   */
  missionClearMax?: number | null;
  /**
   * `game_point_actions.daily_mission.pts` — what the daily actually pays.
   * Loaded only when publishing `daily_mission_weights`, for one WARNING.
   */
  dailyMissionPts?: number | null;
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
  level_up_costs: ["level", "cost_r", "sp_reward"],
  modes: ["id", "title", "entryFee", "order"],
  // missions_v1 §A1. `id` is the campaign number as text; `key` is the stable
  // slug the localization key `MISSION_NAME_<KEY>` is built from.
  missions: ["id", "order", "tier", "key", "holeId", "par", "startAreaId",
             "windPresetId", "loadoutId", "goal1Type", "firstClearRP", "replayRP",
             "courseId", "pinIndex", "staminaDrain"],
  mission_start_areas: ["id", "courseId", "holeId", "areaId", "kind", "weight"],
  mission_wind_presets: ["id", "label", "relDirDeg", "speed", "weight"],
  mission_loadouts: ["id", "kind", "clubs", "weight", "allowedStartKinds"],
  mission_goal_weights: ["id", "goal", "match", "weight"],
  mission_tiers: ["tier", "order", "scoreMin", "scoreMaxExcl", "firstClearRP",
                  "replayRP", "tierClearBonusRP", "unlockClears", "missionsInTier"],
  daily_mission_weights: ["id", "component", "optionId", "pickWeight"],
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
  level_up_costs: ["level", "cost_r", "sp_reward"],
  modes: ["entryFee", "rewards", "order", "reward1Amount", "reward2Amount", "reward3Amount",
          "versusStrokeCapOverPar"],
  missions: ["order", "holeId", "par", "difficultyScore", "firstClearRP", "replayRP",
             "pinIndex", "staminaDrain"],
  // x/y/z/pin_count are BLANK until the Phase B bake fills them, and `num("")`
  // is null, so a blank never reaches the parse check. A non-numeric one does.
  mission_start_areas: ["holeId", "weight", "x", "y", "z", "pin_count"],
  mission_wind_presets: ["relDirDeg", "speed", "weight"],
  mission_loadouts: ["weight"],
  mission_goal_weights: ["weight"],
  mission_tiers: ["order", "scoreMin", "scoreMaxExcl", "firstClearRP", "replayRP",
                  "tierClearBonusRP", "unlockClears", "missionsInTier"],
  daily_mission_weights: ["pickWeight"],
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
  level_up_costs: "level",
  modes: "id",
  missions: "id",
  mission_start_areas: "id",
  mission_wind_presets: "id",
  mission_loadouts: "id",
  mission_goal_weights: "id",
  // The tier NAME is the id, the way level_up_costs' `level` is: a missions row
  // references "Beginner", so a synthetic id would be a second name for it.
  mission_tiers: "tier",
  daily_mission_weights: "id",
};

/**
 * Row id shape for a NEWLY CREATED row (shop_stocking §2).
 *
 * Lower-case snake, because the id is what every other catalog resolves against
 * and what the exporter writes into the bundled CSV: `Shop_Foo` and `shop_foo`
 * are the same row to a human and two different rows to everything else.
 *
 * `texts` is the one exception, and it is less a special case than a different
 * convention: localisation keys are UPPER_SNAKE everywhere in the game
 * (`ROSTER_LEVEL_UP`), so forcing lower-case there would mint keys that match
 * nothing.
 *
 * EXISTING rows are never re-checked — this governs what may be brought into
 * being, not the 800-odd ids already in the catalogs, one of which would
 * otherwise become unsavable. Lives HERE, in the pure module, so the row editor
 * and `upsertDraftRow` check the same rule rather than two copies of it.
 */
export const ROW_ID_MAX = 80;
const ROW_ID_PATTERNS: Record<string, RegExp> = {
  texts: /^[A-Za-z0-9_]+$/,
  // A mission id is the campaign number ("41"), the way a level_up_costs id is
  // the level. Lower-case snake would forbid the only shape this catalog uses.
  missions: /^[0-9]+$/,
  // Component ids are the workbook's own UPPER_SNAKE (`TEE_BACK`, `SUP_FULL`,
  // `CROSS_S`) — they are what a missions row references BY NAME, so forcing
  // lower case here would mint ids that resolve against nothing.
  mission_wind_presets: /^[A-Z0-9_]+$/,
  mission_loadouts: /^[A-Z0-9_]+$/,
  mission_tiers: /^[A-Za-z][A-Za-z0-9_]*$/,
};
const DEFAULT_ROW_ID_PATTERN = /^[a-z0-9_]+$/;

export const rowIdPattern = (catalog: string): RegExp =>
  ROW_ID_PATTERNS[catalog] ?? DEFAULT_ROW_ID_PATTERN;

/** Shape only — collisions need the catalogs and live server-side. */
export const isValidNewRowId = (catalog: string, rowId: string): boolean =>
  rowId.length > 0 && rowId.length <= ROW_ID_MAX && rowIdPattern(catalog).test(rowId);

/**
 * Goal types whose param is a NUMBER (rule 11c). A "hole out in ≤ fairway"
 * mission is not hard, it is meaningless.
 */
const NUMERIC_GOALS = new Set(["SCORE", "SHOTS", "PUTTS", "DIST", "CARRY", "NEAR_PIN"]);

/** Goal types whose param names a SURFACE or a CLUB TYPE. */
const SURFACE_GOALS = new Set(["AVOID", "LAND_TEE", "LAND_ANY", "USE_CLUB", "AVOID_CLUB"]);

/**
 * Every goal type the game can evaluate — the Goals sheet of
 * GOLFIN_Missions_Redesign.xlsx, and the set `MissionGoalEvaluator` implements.
 * `mission_goal_weights` must carry a row for each (rule 15).
 */
export const ALL_GOAL_TYPES = [
  "SCORE", "SHOTS", "PUTTS", "NO_HAZARD", "AVOID", "LAND_TEE", "LAND_ANY",
  "GIR", "DIST", "CARRY", "NEAR_PIN", "USE_CLUB", "AVOID_CLUB", "UP_DOWN",
] as const;

/** The component groups the daily generator draws from (rule 17). */
export const DAILY_COMPONENTS = [
  "band", "startKind", "loadout", "wind", "primaryGoal", "secondaryGoal", "modifier",
] as const;

/**
 * DailyRewards.baseRP from the design workbook. WARNING-only reference point —
 * the Rewards panel is where the number that is actually paid lives.
 */
export const DAILY_BASE_RP = 30;

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

      // Hoisted so the build gates below can see it too — same lookup, once.
      const referenced = target && refId ? ctx.otherCatalogs.get(target)?.get(refId) : undefined;

      if (!target) {
        err(row.rowId, "category", `Unknown category "${category}". Known: ${Object.keys(SHOP_CATEGORY_TO_CATALOG).join(", ")}.`);
      } else if (!refId) {
        err(row.rowId, "refId", "refId is empty.");
      } else if (!referenced) {
        err(row.rowId, "refId", `refId "${refId}" does not exist in the ${target} catalog.`);
      } else if (!referenced.isActive) {
        err(row.rowId, "refId", `refId "${refId}" is deactivated in ${target} — the shop would offer an item the game hides.`);
      }

      // ---- The two build gates (shop_stocking §3) --------------------------
      //
      // ACTIVE rows only, deliberately. A deactivated row is never rendered by
      // any client — `GeneralShopCatalog.Admit` drops it on `is_active` before
      // anything else — so it cannot reach the failure these rules prevent.
      // Gating it anyway would be a trap rather than a rail: `min_build` is
      // immutable once published (rule 7 above), so a row published before
      // these rules existed could never satisfy them again, and the whole
      // catalog would become unpublishable with no way out. Deactivating IS
      // the way out (§I6: deactivate is the delete).

      if (row.isActive) {
        // G1 — a category older builds cannot parse must be withheld from them.
        //
        // A build before the strict fix maps ANY non-`ball` category onto
        // `club` and renders a card it cannot fill. The server-side min_build
        // filter is the only thing keeping the row away from those builds, and
        // min_build cannot be corrected after the first publish — so it has to
        // be right the first time, and this is what makes that automatic
        // rather than something to remember.
        if (target && category !== "club" && category !== "ball") {
          if (SHOP_CATEGORY_STRICT_BUILD === 0) {
            err(
              row.rowId,
              "min_build",
              `The client build that renders "${category}" rows has not been uploaded yet; ` +
                "set SHOP_CATEGORY_STRICT_BUILD (lib/buildGates.ts) after the archive, from " +
                "Docs/Versioning/last_uploaded_build.txt."
            );
          } else if (row.minBuild < SHOP_CATEGORY_STRICT_BUILD) {
            err(
              row.rowId,
              "min_build",
              `min_build ${row.minBuild} is below ${SHOP_CATEGORY_STRICT_BUILD}, the first build that ` +
                `renders "${category}" rows. Older builds would read this row as a club and show a ` +
                "card they cannot fill. Raise min_build (it is immutable once published)."
            );
          }
        }

        // G2 — never visible on a build that cannot see what it sells.
        //
        // Plan §11.4.6. The referenced row carries its own min_build; a shop
        // row that reaches a build first is a card whose club/character is not
        // in that build's catalog at all.
        if (referenced && row.minBuild < referenced.minBuild) {
          err(
            row.rowId,
            "min_build",
            `min_build ${row.minBuild} is below the min_build of "${refId}" in ${target} ` +
              `(${referenced.minBuild}). The shop row would be visible on a build that cannot ` +
              "see the row it sells."
          );
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

      // 7b. SCHEDULING WINDOWS (content_panels_gaps §3; plan §11.2/§11.4.3).
      //
      // Blocking, and fail-closed on the parse: a bound that is present but
      // unreadable is an ERROR here rather than something the runtime silently
      // ignores. The runtime does fail closed too (`shopState` → BROKEN), but a
      // row that never renders is a worse way to learn about a typo than a
      // publish that refuses.
      const bound = (column: string): number | null | "invalid" => {
        const raw = text(row.data[column]).trim();
        if (raw === "") return null;
        const ms = Date.parse(raw.replace(" ", "T"));
        return Number.isNaN(ms) ? "invalid" : ms;
      };

      const windows: Array<[string, string, string]> = [
        ["startAt", "endAt", "listing"],
        ["saleStartAt", "saleEndAt", "sale"],
      ];
      const parsed: Record<string, number | null> = {};
      let windowsUsable = true;

      for (const [startCol, endCol] of windows) {
        for (const column of [startCol, endCol]) {
          const value = bound(column);
          if (value === "invalid") {
            windowsUsable = false;
            err(
              row.rowId,
              column,
              `"${text(row.data[column])}" is not a readable timestamp. Use an ISO-8601 UTC ` +
                `instant like 2026-09-01T00:00:00Z, or leave it empty for "no bound".`
            );
          } else {
            parsed[column] = value;
          }
        }
      }

      if (windowsUsable) {
        // Each window well-ordered. endAt is EXCLUSIVE, so start === end is an
        // empty window — almost certainly a mistake, and it would list nothing.
        for (const [startCol, endCol, label] of windows) {
          const from = parsed[startCol];
          const to = parsed[endCol];
          if (from !== null && from !== undefined && to !== null && to !== undefined && from >= to) {
            err(
              row.rowId,
              endCol,
              `The ${label} window ends at or before it starts (${text(row.data[startCol])} → ` +
                `${text(row.data[endCol])}). endAt is EXCLUSIVE, so this window is empty.`
            );
          }
        }

        // The sale window must sit INSIDE the listing window: a sale on a row
        // that is not listed is a discount nobody can reach.
        const listFrom = parsed.startAt;
        const listTo = parsed.endAt;
        const saleFrom = parsed.saleStartAt;
        const saleTo = parsed.saleEndAt;
        if (saleFrom !== null && saleFrom !== undefined && listFrom !== null && listFrom !== undefined && saleFrom < listFrom) {
          err(row.rowId, "saleStartAt", `The sale starts before the row is listed (${text(row.data.startAt)}).`);
        }
        if (saleTo !== null && saleTo !== undefined && listTo !== null && listTo !== undefined && saleTo > listTo) {
          err(row.rowId, "saleEndAt", `The sale ends after the row stops being listed (${text(row.data.endAt)}).`);
        }
        // A sale window with no sale price is inert; the reverse is the one that
        // surprises people, so both are called out as warnings only.
        if ((saleFrom ?? saleTo) != null && saleRpCost === null) {
          warn(row.rowId, "saleRpCost", "A sale window is set but saleRpCost is empty, so nothing is discounted.");
        }
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

  // 9. level_up_costs — the cost table the SERVER prices from (progress_server_side §2).
  //
  // Three rules, all BLOCKING, and the third is the one that earns the section.
  //
  //   * cost_r / sp_reward must be non-negative. A negative cost is a level that
  //     PAYS the player to take it; a negative reward is an SP debit nothing
  //     implements.
  //   * `level` must be a positive integer. It is the row id, so it is also what
  //     `golfin_level_up()` looks a level up BY (`row_id = lv::text`) — "07" and
  //     "7" would be two rows and neither would be found for level 7.
  //   * COVERAGE MUST BE CONTIGUOUS from 1 to the highest `maxLevel` any
  //     character or club can reach. A gap is not a cosmetic hole: the server
  //     answers `costs_missing` and the client renders a dead LEVEL UP button
  //     with no explanation, for every player whose ref crosses it. Deactivating
  //     a row makes the same hole — the function joins on `is_active` — so
  //     deactivated rows do NOT count as coverage here either.
  //
  // The ceiling comes from the characters / clubs drafts when the caller loaded
  // them (publishCatalog does). When it did not, the highest level present in
  // this catalog stands in — which still catches an internal gap and is never
  // vacuous, but cannot know about a ref that reaches higher than the table does.
  if (catalog === "level_up_costs") {
    const levels: number[] = [];

    for (const row of rows) {
      const costR = num(row.data.cost_r);
      if (costR !== null && costR < 0) err(row.rowId, "cost_r", `cost_r ${costR} is negative.`);

      const spReward = num(row.data.sp_reward);
      if (spReward !== null && spReward < 0) {
        err(row.rowId, "sp_reward", `sp_reward ${spReward} is negative.`);
      }

      const level = num(row.data.level ?? row.rowId);
      if (level === null || !Number.isInteger(level) || level < 1) {
        err(row.rowId, "level", `"${row.rowId}" is not a positive whole level number.`);
        continue;
      }
      if (String(level) !== row.rowId.trim()) {
        err(
          row.rowId,
          "level",
          `The row id "${row.rowId}" is not the plain number ${level}. The server looks a ` +
            "level up by its row id, so a padded or spaced id is a level it cannot find."
        );
        continue;
      }
      // Only ACTIVE rows are coverage — see the note above.
      if (row.isActive) levels.push(level);
    }

    let ceiling = 0;
    let ceilingSource = "";
    for (const refCatalog of ["characters", "clubs"]) {
      const refRows = ctx.otherCatalogs.get(refCatalog);
      if (!refRows) continue;
      for (const refRow of refRows.values()) {
        const maxLevel = num(refRow.data.maxLevel);
        if (maxLevel !== null && maxLevel > ceiling) {
          ceiling = maxLevel;
          ceilingSource = `${refRow.rowId} (${refCatalog})`;
        }
      }
    }
    if (ceiling === 0 && levels.length > 0) {
      ceiling = Math.max(...levels);
      ceilingSource = "the highest level in this catalog";
    }

    const present = new Set(levels);
    const gaps: number[] = [];
    for (let level = 1; level <= ceiling; level++) {
      if (!present.has(level)) gaps.push(level);
    }
    if (gaps.length > 0) {
      const shown = gaps.slice(0, 12).join(", ");
      err(
        null,
        "level",
        `Coverage is not contiguous: ${gaps.length} level(s) between 1 and ${ceiling} have no ` +
          `active row — ${shown}${gaps.length > 12 ? `, +${gaps.length - 12} more` : ""}. ` +
          `The ceiling comes from ${ceilingSource}. A missing level is one the server refuses ` +
          "with costs_missing and the player sees as a dead LEVEL UP button."
      );
    }
  }

  // 10. modes — the catalog that prices MODE ENTRY (game_modes_admin §2).
  //
  // Four blocking rules and ONE warning, and the warning is the interesting part.
  //
  //   * `entryFee >= 0`. A negative fee is a mode that PAYS you to enter, which
  //     `golfin_mode_fees`'s own check constraint would refuse anyway — better
  //     here, where the operator can see which row.
  //   * `order` UNIQUE. It is the carousel's sort key and the client sorts by it
  //     with a plain comparison, so a tie is a pair of cards in arbitrary,
  //     build-dependent order. Not fatal, but it is a layout that changes under
  //     you for no visible reason, which is worse to debug than a refused publish.
  //   * `target` NON-EMPTY. An empty target is a card whose PLAY button routes
  //     nowhere. (An unrecognised-but-non-empty target is NOT an error here: the
  //     dashboard cannot know what the builds in the wild dispatch. The CLIENT
  //     withholds such a mode with a warning — ModesDatabaseCSV, §2 — which is
  //     the right place for a build-specific rule.)
  //   * `locked` must parse as a bool. `GetBool` treats anything it does not
  //     recognise as false, so "yes" would silently publish a Coming Soon mode
  //     as LIVE.
  //
  // THE DRIFT WARNING COVERS EXACTLY ONE PAIR, BY DECISION (Cesar, 2026-08-28).
  // Card reward numbers are DECOUPLED from `game_point_actions`: for every mode
  // except multiplayer the card shows an AVERAGE over a later selection (which
  // hole, how it is played), so it is copy the operator words freely and there is
  // nothing to compare it to. `versus_1v1` is the one card that claims an exact
  // paid amount, because `versus_win` is a fixed payout. So the check is
  // `versus_1v1.rewards` / `reward1Amount` vs `versus_win.pts`, and nothing else.
  // DO NOT GENERALISE THIS INTO A MAPPING TABLE — a table would invent a
  // relationship for four modes that deliberately do not have one, and every one
  // of them would warn forever.
  if (catalog === "modes") {
    const orderSeen = new Map<number, string>();

    for (const row of rows) {
      const entryFee = num(row.data.entryFee);
      if (entryFee !== null && entryFee < 0) {
        err(row.rowId, "entryFee", `entryFee ${entryFee} is negative.`);
      }

      const target = text(row.data.target).trim();
      if (!target) {
        err(
          row.rowId,
          "target",
          "target is empty — the card's PLAY button would route nowhere. Use \"none\" " +
            "for a mode that is deliberately not enterable yet (it renders as Coming Soon)."
        );
      }

      if ("locked" in row.data) {
        const locked = text(row.data.locked).trim().toLowerCase();
        if (locked !== "" && locked !== "true" && locked !== "false" && locked !== "1" && locked !== "0") {
          err(
            row.rowId,
            "locked",
            `"${text(row.data.locked)}" is not a boolean. The client reads anything it does not ` +
              "recognise as FALSE, so this would publish the mode as LIVE."
          );
        }
      }

      const order = num(row.data.order);
      if (order !== null) {
        const clash = orderSeen.get(order);
        if (clash !== undefined) {
          err(
            row.rowId,
            "order",
            `order ${order} is already used by "${clash}". The carousel sorts by this column, ` +
              "so a tie leaves the two cards in arbitrary order."
          );
        } else {
          orderSeen.set(order, row.rowId);
        }
      }
    }

    // The one drift warning. WARNING, not an error: the operator may be mid-way
    // through a two-step change (raise the payout here, publish the card copy
    // next), and blocking would make the intermediate state unpublishable.
    const versus = rows.find((r) => r.rowId === "versus_1v1");
    if (versus && ctx.versusWinPts !== undefined && ctx.versusWinPts !== null) {
      const paid = ctx.versusWinPts;
      const shown = num(versus.data.reward1Amount) ?? num(versus.data.rewards);
      if (shown !== null && shown !== paid) {
        warn(
          "versus_1v1",
          "rewards",
          `The 1v1 card advertises ${shown} RP but versus_win pays ${paid} (Rewards panel). ` +
            "Unlike every other mode, this card claims an EXACT payout, so the two should agree."
        );
      }
    }
  }


  // 11. missions — the catalog the SERVER PAYS FROM (missions_v1 §A6).
  //
  // Publishing this catalog mirrors every row's tier and RP into
  // `golfin_mission_rewards`, and `golfin_mission_claim()` pays from THAT. So a
  // number here is not card copy: it is what a player is credited. The rules are
  // the compatibility ones a composed mission can get wrong, and they are
  // blocking because every one of them produces a card that is dead, unpayable,
  // or paying the wrong amount — the two halves of the standing invariant.
  if (catalog === "missions") {
    const areas = ctx.otherCatalogs.get("mission_start_areas");
    const winds = ctx.otherCatalogs.get("mission_wind_presets");
    const loadouts = ctx.otherCatalogs.get("mission_loadouts");
    const weights = ctx.otherCatalogs.get("mission_goal_weights");
    const tiers = ctx.otherCatalogs.get("mission_tiers");

    const weightRows: WeightRow[] = weights
      ? [...weights.values()].map((r) => ({
          goal: text(r.data.goal),
          match: text(r.data.match),
          scope: text(r.data.scope),
          param: text(r.data.param),
          weight: text(r.data.weight),
        }))
      : [];

    // Start areas are keyed per (hole, areaId) — the same area id means a
    // different row on a different hole, because the coordinates are baked
    // per hole. Index by the pair the mission actually names.
    const areaByHoleAndId = new Map<string, DraftRow>();
    if (areas) {
      for (const row of areas.values()) {
        areaByHoleAndId.set(`${text(row.data.holeId)}:${text(row.data.areaId)}`, row);
      }
    }

    const orderSeen = new Map<number, string>();
    let previousOrder: number | null = null;

    for (const row of rows) {
      const holeId = num(row.data.holeId);
      const par = num(row.data.par);
      const startAreaId = text(row.data.startAreaId);
      const windId = text(row.data.windPresetId);
      const loadoutId = text(row.data.loadoutId);

      // --- 11a. the three components resolve ------------------------------
      const area = areaByHoleAndId.get(`${text(row.data.holeId)}:${startAreaId}`);
      if (areas && !area) {
        err(
          row.rowId,
          "startAreaId",
          `No mission_start_areas row for hole ${text(row.data.holeId)} area "${startAreaId}". ` +
            "The mission would have nowhere to put the ball."
        );
      } else if (area && !area.isActive) {
        err(
          row.rowId,
          "startAreaId",
          `Start area "${startAreaId}" on hole ${text(row.data.holeId)} is deactivated ` +
            "(no bunker on that hole, usually). Pick another area or reactivate the row."
        );
      }

      const wind = winds?.get(windId);
      if (winds && !wind) {
        err(row.rowId, "windPresetId", `Wind preset "${windId}" does not exist.`);
      }

      const loadout = loadouts?.get(loadoutId);
      if (loadouts && !loadout) {
        err(row.rowId, "loadoutId", `Loadout "${loadoutId}" does not exist.`);
      }

      // --- 11b. start <-> loadout compatibility ----------------------------
      //
      // `allowedStartKinds` is `any | tee | short | green`, and `green` is the
      // NARROWER short kind: SUP_PUTTER (putter only) is playable from ON the
      // green and nowhere else. A putter-only mission from the fairway is not a
      // hard mission, it is an unfinishable one.
      if (area && loadout) {
        const allowed = text(loadout.data.allowedStartKinds).toLowerCase() || "any";
        const kind = text(area.data.kind).toLowerCase();
        const areaId = text(area.data.areaId).toUpperCase();
        const ok =
          allowed === "any" ||
          allowed === kind ||
          (allowed === "green" && areaId === "GREEN");
        if (!ok) {
          err(
            row.rowId,
            "loadoutId",
            `Loadout "${loadoutId}" allows ${allowed} starts, but "${startAreaId}" is a ` +
              `${kind} start${allowed === "green" ? " (this loadout is green-only)" : ""}.`
          );
        }
      }

      // --- 11c. goals: typed, present, and never duplicated ----------------
      //
      // A DUPLICATE GOAL TYPE IS INVALID, explicitly (SPEC "Reference": the
      // mockup's three bullets with a repeated line are FILLER). Two SCORE goals
      // are either contradictory or redundant, and the card would render the
      // same sentence twice.
      const goalTypes: string[] = [];
      for (const slot of [1, 2, 3]) {
        const goalType = text(row.data[`goal${slot}Type`]);
        const goalParam = text(row.data[`goal${slot}Param`]);
        if (!goalType) {
          if (goalParam) {
            err(row.rowId, `goal${slot}Param`, `goal${slot}Param is set but goal${slot}Type is empty.`);
          }
          continue;
        }
        if (goalTypes.includes(goalType)) {
          err(
            row.rowId,
            `goal${slot}Type`,
            `Goal type "${goalType}" appears twice. A mission may not repeat a goal type — ` +
              "the card would show the same line twice and the evaluator would double-count it."
          );
        }
        goalTypes.push(goalType);

        // The goal must be one the weights table knows, or it scores 0 and the
        // mission silently lands in the wrong tier.
        if (weightRows.length > 0 && !weightRows.some((w) => w.goal === goalType)) {
          err(
            row.rowId,
            `goal${slot}Type`,
            `Goal type "${goalType}" has no row in mission_goal_weights, so it would score 0 ` +
              "and the mission would be tiered as if the goal were not there."
          );
        }

        if (NUMERIC_GOALS.has(goalType) && num(goalParam) === null) {
          err(row.rowId, `goal${slot}Param`, `"${goalType}" needs a numeric param (got "${goalParam}").`);
        }
        if (SURFACE_GOALS.has(goalType) && !goalParam) {
          err(row.rowId, `goal${slot}Param`, `"${goalType}" needs a surface param.`);
        }
      }
      if (goalTypes.length === 0) {
        err(row.rowId, "goal1Type", "A mission needs at least one goal.");
      }

      // --- 11d. difficultyScore is RECOMPUTED; the stored value is display --
      if (area && wind && loadout && weightRows.length > 0 && par !== null) {
        let score = holeBase(weightRows, par);
        score += num(area.data.weight) ?? 0;
        score += num(wind.data.weight) ?? 0;
        score += num(loadout.data.weight) ?? 0;
        for (const slot of [1, 2, 3]) {
          const goalType = text(row.data[`goal${slot}Type`]);
          if (!goalType) continue;
          score += scoreGoal(
            weightRows, goalType, text(row.data[`goal${slot}Param`]),
            text(area.data.kind), par
          );
        }

        const stored = num(row.data.difficultyScore);
        if (stored !== null && stored !== score) {
          warn(
            row.rowId,
            "difficultyScore",
            `difficultyScore is stored as ${stored} but the components score ${score}. ` +
              "The recomputed value is the one that counts; publish updates the display."
          );
        }

        // --- 11e. the tier BAND has to contain the score -------------------
        const tierName = text(row.data.tier);
        const tier = tiers?.get(tierName);
        if (tiers && !tier) {
          err(row.rowId, "tier", `Tier "${tierName}" does not exist in mission_tiers.`);
        } else if (tier) {
          const low = num(tier.data.scoreMin);
          const high = num(tier.data.scoreMaxExcl);
          if (low !== null && high !== null && (score < low || score >= high)) {
            warn(
              row.rowId,
              "tier",
              `Scores ${score}, which is outside the ${tierName} band ${low}-${high - 1}. ` +
                "Publishing anyway — a mission may be placed against its band deliberately."
            );
          }
        }
      }

      // --- 11f. RP within the ceiling the claim path enforces --------------
      const firstClear = num(row.data.firstClearRP);
      if (firstClear !== null && firstClear < 0) {
        err(row.rowId, "firstClearRP", `firstClearRP ${firstClear} is negative.`);
      }
      const replayRp = num(row.data.replayRP);
      if (replayRp !== null && replayRp < 0) {
        err(row.rowId, "replayRP", `replayRP ${replayRp} is negative.`);
      }
      if (
        firstClear !== null &&
        ctx.missionClearMax !== undefined &&
        ctx.missionClearMax !== null &&
        firstClear > ctx.missionClearMax
      ) {
        err(
          row.rowId,
          "firstClearRP",
          `firstClearRP ${firstClear} is above mission_clear.max_per_event ` +
            `(${ctx.missionClearMax}, Rewards panel). The claim would be refused and the ` +
            "player would clear the mission and be paid nothing. Raise the cap first."
        );
      }

      // --- 11g. campaign order ---------------------------------------------
      const order = num(row.data.order);
      if (order !== null) {
        const clash = orderSeen.get(order);
        if (clash !== undefined) {
          err(row.rowId, "order", `order ${order} is already used by "${clash}".`);
        } else {
          orderSeen.set(order, row.rowId);
        }
      }

      if (holeId !== null && (holeId < 1 || holeId > 18)) {
        err(row.rowId, "holeId", `holeId ${holeId} is not a Lomond hole (1-18).`);
      }
      const pinIndex = num(row.data.pinIndex);
      if (pinIndex !== null && pinIndex < 0) {
        err(row.rowId, "pinIndex", `pinIndex ${pinIndex} is negative.`);
      }
      const drain = num(row.data.staminaDrain);
      if (drain !== null && drain < 0) {
        err(row.rowId, "staminaDrain", `staminaDrain ${drain} is negative.`);
      }

      // --- 11h. the unlock chain resolves -----------------------------------
      const unlock = text(row.data.unlock);
      if (unlock && unlock !== "start" && unlock.startsWith("clear:")) {
        const target = unlock.slice("clear:".length).trim();
        if (!rows.some((r) => r.rowId === target)) {
          err(
            row.rowId,
            "unlock",
            `unlock "${unlock}" names mission "${target}", which is not in this catalog. ` +
              "The mission would never unlock."
          );
        }
      } else if (unlock && unlock !== "start") {
        err(row.rowId, "unlock", `unlock "${unlock}" is not "start" or "clear:<mission id>".`);
      }
    }

    // Campaign order NON-DECREASING by difficulty — a WARNING, because a
    // deliberately-placed showcase mission is a real editorial choice.
    const ordered = [...rows].sort((a, b) => (num(a.data.order) ?? 0) - (num(b.data.order) ?? 0));
    for (const row of ordered) {
      const stored = num(row.data.difficultyScore);
      if (stored === null) continue;
      if (previousOrder !== null && stored < previousOrder) {
        warn(
          row.rowId,
          "order",
          `Difficulty drops from ${previousOrder} to ${stored} at campaign order ` +
            `${text(row.data.order)}. The ladder is meant to climb.`
        );
      }
      previousOrder = stored;
    }
  }

  // 12. mission_loadouts — a supplied bag must be a bag that EXISTS.
  //
  // BLOCKING, and it is the rule that stops the invariant's worst case: a
  // supplied loadout naming a club type+rarity with no `clubs` row hands the
  // player an empty bag, on a mission they cannot then play. Better to refuse
  // the publish than to ship a card that dead-ends.
  if (catalog === "mission_loadouts") {
    const clubs = ctx.otherCatalogs.get("clubs");
    for (const row of rows) {
      const kind = text(row.data.kind).toLowerCase();
      if (kind !== "supplied" && kind !== "own") {
        err(row.rowId, "kind", `kind "${text(row.data.kind)}" is not "supplied" or "own".`);
        continue;
      }

      const allowed = text(row.data.allowedStartKinds).toLowerCase() || "any";
      if (!["any", "tee", "short", "green"].includes(allowed)) {
        err(
          row.rowId,
          "allowedStartKinds",
          `"${allowed}" is not one of any, tee, short, green.`
        );
      }

      const mask = text(row.data.clubs);
      if (kind === "own") {
        if (mask !== "*" && !mask.startsWith("ban:")) {
          err(row.rowId, "clubs", `An "own" loadout's clubs must be "*" or "ban:Type,Type" (got "${mask}").`);
        }
        continue;
      }

      if (!mask) {
        err(row.rowId, "clubs", "A supplied loadout must list its club types.");
        continue;
      }
      const rarity = text(row.data.rarity);
      if (!rarity) {
        err(row.rowId, "rarity", "A supplied loadout must name the rarity its clubs resolve at.");
        continue;
      }
      if (!clubs) continue;

      for (const type of mask.split(",").map((t) => t.trim()).filter(Boolean)) {
        const match = [...clubs.values()].some(
          (club) =>
            club.isActive &&
            text(club.data.type).toLowerCase() === type.toLowerCase() &&
            text(club.data.rarity).toLowerCase() === rarity.toLowerCase()
        );
        if (!match) {
          err(
            row.rowId,
            "clubs",
            `No active clubs row is type "${type}" at rarity "${rarity}", so this supplied bag ` +
              "would be missing that club and the mission would be unplayable."
          );
        }
      }
    }
  }

  // 13. mission_start_areas — the baked table.
  if (catalog === "mission_start_areas") {
    const byArea = new Map<string, { kind: string; weight: number; rowId: string }>();
    for (const row of rows) {
      const kind = text(row.data.kind).toLowerCase();
      if (kind !== "tee" && kind !== "short") {
        err(row.rowId, "kind", `kind "${text(row.data.kind)}" is not "tee" or "short".`);
      }

      const holeId = num(row.data.holeId);
      if (holeId === null || holeId < 1 || holeId > 18) {
        err(row.rowId, "holeId", `holeId "${text(row.data.holeId)}" is not a Lomond hole (1-18).`);
      }

      const hasCoords = ["x", "y", "z"].every((axis) => text(row.data[axis]) !== "");
      if (kind === "tee" && hasCoords) {
        err(
          row.rowId,
          "x",
          "A tee area must NOT carry coordinates — it resolves to the scene's " +
            "TeeMarker_<label>_L/R midpoint at runtime, and a baked point would override it."
        );
      }
      // NOT an error: a short area with no coordinates is the state every one of
      // them ships in until `Golfin/Missions/Bake Start Areas` runs. It is a
      // WARNING because the campaign is not playable yet and blocking here would
      // make the catalog unpublishable before Phase B — but a mission naming it
      // is refused by rule 11a's active check, so nothing dead can reach a player.
      if (kind === "short" && row.isActive && !hasCoords) {
        warn(
          row.rowId,
          "x",
          "Not baked yet — run Golfin/Missions/Bake Start Areas in Unity. Missions cannot " +
            "start here until x/y/z are filled."
        );
      }

      // The per-kind facts are denormalised onto every hole's row so a bunker on
      // one hole can be tuned apart from a bunker on another. That is a licence
      // to differ DELIBERATELY, not to drift — so disagreement is surfaced.
      const areaId = text(row.data.areaId);
      const weight = num(row.data.weight) ?? 0;
      const seenArea = byArea.get(areaId);
      if (!seenArea) {
        byArea.set(areaId, { kind, weight, rowId: row.rowId });
      } else if (seenArea.kind !== kind) {
        err(
          row.rowId,
          "kind",
          `Area "${areaId}" is "${kind}" here but "${seenArea.kind}" on "${seenArea.rowId}". ` +
            "The kind is a property of the AREA, not of the hole."
        );
      } else if (seenArea.weight !== weight) {
        warn(
          row.rowId,
          "weight",
          `Area "${areaId}" weighs ${weight} here but ${seenArea.weight} on "${seenArea.rowId}". ` +
            "Per-hole tuning is allowed; check this is deliberate."
        );
      }
    }
  }

  // 14. mission_wind_presets.
  if (catalog === "mission_wind_presets") {
    for (const row of rows) {
      const dir = num(row.data.relDirDeg);
      if (dir !== null && (dir < 0 || dir > 359)) {
        err(row.rowId, "relDirDeg", `relDirDeg ${dir} is not in 0-359.`);
      }
      const speed = num(row.data.speed);
      if (speed !== null && speed < 0) err(row.rowId, "speed", `speed ${speed} is negative.`);
    }
  }

  // 15. mission_goal_weights — the difficulty curve.
  if (catalog === "mission_goal_weights") {
    const MATCHES = ["exact", "lte", "default", "any", "as_score"];
    const goalsSeen = new Set<string>();
    for (const row of rows) {
      const match = text(row.data.match);
      if (!MATCHES.includes(match)) {
        err(row.rowId, "match", `match "${match}" is not one of ${MATCHES.join(", ")}.`);
      }
      const scope = text(row.data.scope);
      if (scope && scope !== "tee" && scope !== "short") {
        err(row.rowId, "scope", `scope "${scope}" is not blank, "tee" or "short".`);
      }
      goalsSeen.add(text(row.data.goal));
    }
    // EVERY goal type needs a row (AdminCatalogs sheet). A goal with no weight
    // scores 0, which silently mis-tiers every mission that uses it.
    for (const goal of ALL_GOAL_TYPES) {
      if (!goalsSeen.has(goal)) {
        err(null, "goal", `Goal type "${goal}" has no weight row — missions using it would score 0.`);
      }
    }
    for (const par of ["3", "4", "5"]) {
      if (!rows.some((r) => text(r.data.goal) === "HOLE_BASE" && text(r.data.param) === par)) {
        err(null, "goal", `No HOLE_BASE row for par ${par}.`);
      }
    }
  }

  // 16. mission_tiers — bands contiguous and non-overlapping.
  if (catalog === "mission_tiers") {
    const ladder = [...rows].sort((a, b) => (num(a.data.order) ?? 0) - (num(b.data.order) ?? 0));
    let previousMax: number | null = null;
    for (const row of ladder) {
      const low = num(row.data.scoreMin);
      const high = num(row.data.scoreMaxExcl);
      if (low !== null && high !== null && low >= high) {
        err(row.rowId, "scoreMaxExcl", `Band ${low}-${high} is empty (scoreMaxExcl is exclusive).`);
      }
      if (previousMax !== null && low !== null && low !== previousMax) {
        err(
          row.rowId,
          "scoreMin",
          `Band starts at ${low} but the previous tier ends at ${previousMax}. Bands must be ` +
            "contiguous — a gap is a difficulty score that belongs to no tier."
        );
      }
      if (high !== null) previousMax = high;

      const unlockClears = num(row.data.unlockClears);
      const size = num(row.data.missionsInTier);
      if (unlockClears !== null && size !== null && unlockClears > size) {
        err(
          row.rowId,
          "unlockClears",
          `unlockClears ${unlockClears} is more than the ${size} missions in the previous tier — ` +
            "the next tier could never open."
        );
      }
      const bonus = num(row.data.tierClearBonusRP);
      if (bonus !== null && bonus < 0) {
        err(row.rowId, "tierClearBonusRP", `tierClearBonusRP ${bonus} is negative.`);
      }
    }
  }

  // 17. daily_mission_weights — every draw group must be able to draw.
  if (catalog === "daily_mission_weights") {
    const groupTotal = new Map<string, number>();
    for (const row of rows) {
      const weight = num(row.data.pickWeight);
      if (weight !== null && weight < 0) {
        err(row.rowId, "pickWeight", `pickWeight ${weight} is negative.`);
      }
      const component = text(row.data.component);
      if (component && component !== "rule") {
        groupTotal.set(component, (groupTotal.get(component) ?? 0) + (weight ?? 0));
      }
    }
    for (const component of DAILY_COMPONENTS) {
      const total = groupTotal.get(component);
      if (total === undefined) {
        err(null, "component", `No "${component}" rows — the daily generator has nothing to draw.`);
      } else if (total <= 0) {
        err(
          null,
          "pickWeight",
          `Every "${component}" option has weight 0, so the generator cannot draw one and the ` +
            "daily mission would fail to generate."
        );
      }
    }
    if (ctx.dailyMissionPts !== undefined && ctx.dailyMissionPts !== null && ctx.dailyMissionPts !== DAILY_BASE_RP) {
      warn(
        null,
        "pickWeight",
        `daily_mission pays ${ctx.dailyMissionPts} RP (Rewards panel) but the design's base is ` +
          `${DAILY_BASE_RP} (DailyRewards.baseRP). Check which one is meant to have moved.`
      );
    }
  }

  return problems;
}

export const hasErrors = (problems: ContentProblem[]): boolean =>
  problems.some((p) => p.severity === "error");
