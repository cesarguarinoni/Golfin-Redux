import "server-only";
import { fetchPlayerGacha } from "./gachaData";
import { isMockMode } from "./mode";
import {
  MOCK_INVENTORY_GRANTS,
  MOCK_INVENTORY_REV,
  MOCK_PLAYER_INVENTORY,
} from "./mockInventory";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type {
  InventoryEntityRow,
  InventoryGrantRow,
  PlayerInventory,
  PlayerInventoryResponse,
} from "./types";

/**
 * Reading one player's game inventory (SPEC content_player_inventory §5).
 *
 * ⚠️ `profiles.golfin_inventory`, NOT `user_inventory` — the latter is the
 * PARTNER APP's gift inventory and nothing here touches it.
 *
 * THE BLOB IS DELTAS FROM THE CATALOG DEFAULT, and this dashboard does not have
 * the catalog. That is not a gap to paper over: a club stored as a bare id means
 * "whatever the catalog says today", and the honest rendering of that is the
 * word "default", not a level this panel would have to invent. Decoding here is
 * therefore deliberately shallow — split the rows, count the bytes, keep the raw
 * blob for the disclosure, and let the operator see the shape that is actually
 * stored.
 */

type Row = Record<string, unknown>;

function num(v: unknown, fallback = 0): number {
  return typeof v === "number" && Number.isFinite(v) ? v : fallback;
}

function str(v: unknown): string | null {
  return typeof v === "string" && v.length > 0 ? v : null;
}

/** A blob entry is either a bare id (at catalog default) or `{id, …deltas}`. */
function toEntityRows(value: unknown): InventoryEntityRow[] {
  if (!Array.isArray(value)) return [];
  const out: InventoryEntityRow[] = [];
  for (const entry of value) {
    if (typeof entry === "string") {
      if (entry.length > 0) out.push({ id: entry, atDefault: true, deltas: {} });
      continue;
    }
    if (entry && typeof entry === "object") {
      const obj = entry as Row;
      const id = str(obj.id);
      if (!id) continue;
      const deltas: Record<string, string | number | boolean> = {};
      for (const [k, v] of Object.entries(obj)) {
        if (k === "id") continue;
        if (typeof v === "string" || typeof v === "number" || typeof v === "boolean") {
          deltas[k] = v;
        }
      }
      out.push({ id, atDefault: Object.keys(deltas).length === 0, deltas });
    }
  }
  return out;
}

function toCounts(value: unknown): Record<string, number> {
  if (!value || typeof value !== "object" || Array.isArray(value)) return {};
  const out: Record<string, number> = {};
  for (const [k, v] of Object.entries(value as Row)) {
    if (typeof v === "number" && Number.isFinite(v)) out[k] = v;
  }
  return out;
}

export function decodeInventory(blob: unknown): PlayerInventory | null {
  if (blob === null || blob === undefined) return null;

  // Supabase hands JSONB back as a parsed object; a hand-written row could still
  // be a string. Accept both rather than rendering "no inventory" for a blob
  // that is plainly there.
  let parsed: Row;
  if (typeof blob === "string") {
    try {
      parsed = JSON.parse(blob) as Row;
    } catch {
      return null;
    }
  } else if (typeof blob === "object" && !Array.isArray(blob)) {
    parsed = blob as Row;
  } else {
    return null;
  }

  const raw = JSON.stringify(parsed);

  return {
    formatVersion: typeof parsed.v === "number" ? parsed.v : null,
    clubs: toEntityRows(parsed.clubs),
    characters: toEntityRows(parsed.characters),
    items: toCounts(parsed.items),
    balls: toCounts(parsed.balls),
    tickets: toCounts(parsed.tickets),
    unlockedHoles: Array.isArray(parsed.holes)
      ? (parsed.holes as unknown[]).filter((h): h is number => typeof h === "number")
      : [],
    starterCharacterId: str(parsed.starter),
    selectedCharacterId: str(parsed.selected),
    bytes: new TextEncoder().encode(raw).length,
    raw,
  };
}

function toGrantRow(r: Row): InventoryGrantRow {
  return {
    id: String(r.id ?? ""),
    kind: String(r.kind ?? ""),
    refId: String(r.ref_id ?? ""),
    amount: num(r.amount, 1),
    note: str(r.note),
    createdBy: str(r.created_by),
    createdAt: String(r.created_at ?? ""),
    appliedAt: str(r.applied_at),
  };
}

export async function fetchPlayerInventory(
  userId: string
): Promise<PlayerInventoryResponse> {
  if (isMockMode()) {
    const gacha = await fetchPlayerGacha(userId);
    return {
      inventory: MOCK_PLAYER_INVENTORY,
      rev: MOCK_INVENTORY_REV,
      updatedAt: "2026-08-26T00:00:00.000Z",
      grants: MOCK_INVENTORY_GRANTS,
      tickets: { balances: gacha.balances, transactions: gacha.transactions },
      mock: true,
    };
  }

  const admin = getSupabaseAdmin();

  const profileRes = await admin
    .from("profiles")
    .select("golfin_inventory, golfin_inventory_rev, golfin_inventory_at")
    .eq("id", userId)
    .limit(1);

  if (profileRes.error) {
    // A missing column means the migration has not been applied on this
    // project. Degrade to "never synced" — which is what the API does too — so
    // the tab renders and says so, rather than taking the whole drawer down.
    console.warn("golfin_inventory read failed:", profileRes.error.message);
    return { inventory: null, rev: 0, updatedAt: null, grants: [], mock: false };
  }

  const profile = (profileRes.data as Row[])[0] ?? {};

  // Grants are a SEPARATE degradation: the blob can be readable while the
  // grants table is not (they landed in the same migration, but a partial apply
  // is exactly the case worth surviving).
  let grants: InventoryGrantRow[] = [];
  const grantRes = await admin
    .from("golfin_pending_grants")
    .select("*")
    .eq("user_id", userId)
    .order("created_at", { ascending: false })
    .limit(100);
  if (grantRes.error) {
    console.warn("golfin_pending_grants read failed:", grantRes.error.message);
  } else {
    grants = (grantRes.data as Row[]).map(toGrantRow);
  }

  // THE TICKET LEDGER IS A THIRD, SEPARATE DEGRADATION (gacha_server_pull §5.1).
  // The blob can be readable while `golfin_tickets` does not exist yet — that is
  // exactly the window between deploying this dashboard and applying
  // 2026_09_01_golfin_gacha.sql — and the drawer must still open. `fetchPlayerGacha`
  // already answers `notMigrated` rather than throwing for that case; anything
  // else it throws is caught here for the same reason the grants read is.
  let tickets: PlayerInventoryResponse["tickets"];
  try {
    const gacha = await fetchPlayerGacha(userId);
    tickets = {
      balances: gacha.balances,
      transactions: gacha.transactions,
      ...(gacha.notMigrated ? { notMigrated: gacha.notMigrated } : {}),
    };
  } catch (err) {
    console.warn(
      "golfin_tickets read failed:",
      err instanceof Error ? err.message : String(err)
    );
  }

  return {
    inventory: decodeInventory(profile.golfin_inventory),
    rev: num(profile.golfin_inventory_rev),
    updatedAt: str(profile.golfin_inventory_at),
    grants,
    tickets,
    mock: false,
  };
}
