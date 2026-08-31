import "server-only";
import { auditOdds, type AuditPrize, type AuditPull } from "./gachaAudit";
import { fetchUserDirectory } from "./data";
import { isMockMode } from "./mode";
import {
  MOCK_GACHA_ENABLED,
  MOCK_GACHA_PULLS,
  MOCK_PLAYER_PITY,
  MOCK_TICKET_BALANCES,
  MOCK_TICKET_TRANSACTIONS,
} from "./mockGacha";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type {
  GachaOddsResponse,
  GachaPrizeRow,
  GachaPullRow,
  GachaPullsResponse,
  GachaStats,
  PlayerGachaResponse,
  PlayerPityRow,
  TicketBalanceRow,
  TicketTransactionRow,
} from "./types";

/**
 * Reading the LIVE gacha tables (gacha_server_pull §6).
 *
 * These are not content catalogs. `golfin_gacha_pulls`, `golfin_gacha_prizes`,
 * `golfin_tickets`, `golfin_ticket_transactions` and `golfin_gacha_pity` are
 * written by `golfin_gacha_pull()` and `golfin_ticket_credit()` per request;
 * there is no draft, no publish and no version. The panel over them is a log and
 * an audit, which is why it follows Telemetry (read-only, aggregate in
 * TypeScript behind a row cap) rather than `CatalogPanel`.
 *
 * EVERYTHING HERE IS SERVER TRUTH, and that is a real difference from the
 * Inventory tab. The inventory blob is client-asserted and carries a red
 * warning; a pull row was written by a security-definer function inside the
 * same transaction as the ticket debit. The absence of a warning on this panel
 * is deliberate.
 *
 * TOLERATES THE TABLES NOT EXISTING, on purpose. Between deploying this panel
 * and applying `2026_09_01_golfin_gacha.sql` every read here 404s, and a red 500
 * tells the operator nothing they can act on while looking identical to a panel
 * that is genuinely broken. `notMigrated` names the migration instead — the same
 * choice `dailyMissionData.ts` made for `missions_v1`.
 */

type Row = Record<string, unknown>;

const NOT_MIGRATED = "2026_09_01_golfin_gacha.sql";

/** PostgREST's undefined-table shapes, as lib/dailyMissionData.ts reads them. */
function isMissingRelation(message: string): boolean {
  const text = message.toLowerCase();
  return (
    text.includes("42p01") ||
    text.includes("does not exist") ||
    text.includes("could not find the table")
  );
}

function num(v: unknown, fallback = 0): number {
  const n = typeof v === "string" ? Number(v) : v;
  return typeof n === "number" && Number.isFinite(n) ? n : fallback;
}

function str(v: unknown): string | null {
  return typeof v === "string" && v.length > 0 ? v : null;
}

/** `content_rows.data` for one catalog, keyed by row_id. Never throws. */
async function catalogRows(catalog: string): Promise<Map<string, Row>> {
  const res = await getSupabaseAdmin()
    .from("content_rows")
    .select("row_id, data, is_active")
    .eq("catalog", catalog);
  const out = new Map<string, Row>();
  if (res.error) {
    console.warn(`content_rows(${catalog}) read failed:`, res.error.message);
    return out;
  }
  for (const r of (res.data ?? []) as Row[]) {
    out.set(String(r.row_id), (r.data ?? {}) as Row);
  }
  return out;
}

/**
 * ref id → human name, across the five catalogs a prize can point at.
 *
 * ONE map for all five rather than a per-kind lookup: a `club_iron9_klyro` and a
 * `repairkit_rare` cannot collide (the ids carry their own namespace by
 * convention), and five separate maps would mean five call sites remembering
 * which one to ask. A prize whose ref no longer resolves shows its raw id —
 * that IS the useful answer, because it means the catalog row was deleted out
 * from under a prize that was already paid.
 */
async function prizeNameIndex(): Promise<Map<string, string>> {
  const catalogs = ["clubs", "characters", "items", "balls", "ticket_types"];
  const names = new Map<string, string>();
  await Promise.all(
    catalogs.map(async (catalog) => {
      const rows = await catalogRows(catalog);
      for (const [id, data] of rows) {
        const name =
          str(data.name) ?? str(data.nameEn) ?? str(data.title) ?? null;
        if (name) names.set(`${catalog}:${id}`, name);
      }
    })
  );

  // A flat view keyed by `<kind>:<refId>` would force every caller to know the
  // kind→catalog map. Flatten it here instead, once.
  const flat = new Map<string, string>();
  const kindToCatalog: Record<string, string> = {
    club: "clubs",
    character: "characters",
    item: "items",
    ball: "balls",
    ticket: "ticket_types",
  };
  for (const [kind, catalog] of Object.entries(kindToCatalog)) {
    for (const [key, name] of names) {
      if (key.startsWith(`${catalog}:`)) {
        flat.set(`${kind}:${key.slice(catalog.length + 1)}`, name);
      }
    }
  }
  return flat;
}

export const GACHA_ENABLED_KEY = "gacha_enabled";

/**
 * The gacha pause switch, read the way `golfin_gacha_pull` reads it.
 *
 * ⚠️ FAILS OPEN, matching the function — and for the same reason
 * `fetchGlobalContentEnabled` does: if this returned false on an unreadable
 * flag, the panel would show a pause that is not in effect and the operator's
 * fix ("Resume") would write `true` over a row that already says `true`, while
 * the real problem stayed invisible.
 */
export async function fetchGachaEnabled(): Promise<boolean> {
  if (isMockMode()) return MOCK_GACHA_ENABLED.value;

  const res = await getSupabaseAdmin()
    .from("content_settings")
    .select("value")
    .eq("key", GACHA_ENABLED_KEY)
    .maybeSingle();

  if (res.error) {
    console.warn("content_settings(gacha_enabled) read failed:", res.error.message);
    return true;
  }
  if (!res.data) return true;
  return (res.data as { value: boolean }).value !== false;
}

function toPrize(r: Row, names: Map<string, string>): GachaPrizeRow {
  const kind = String(r.kind ?? "");
  const refId = String(r.ref_id ?? "");
  return {
    slot: num(r.slot),
    kind,
    refId,
    quantity: num(r.quantity, 1),
    rarity: String(r.rarity ?? ""),
    isDupe: r.is_dupe === true,
    dupeRp: num(r.dupe_rp),
    grantId: str(r.grant_id),
    refName: names.get(`${kind}:${refId}`) ?? null,
  };
}

/**
 * Attach every prize to its pull in ONE extra query.
 *
 * A per-pull read would be 51 round trips for a 50-row page against the slowest
 * hop in the request — the same property `routers/gacha.py::history` is pinned
 * on, for the same reason.
 */
async function attachPrizes(
  pulls: Row[],
  names: Map<string, string>
): Promise<Map<string, GachaPrizeRow[]>> {
  const byPull = new Map<string, GachaPrizeRow[]>();
  if (pulls.length === 0) return byPull;

  const res = await getSupabaseAdmin()
    .from("golfin_gacha_prizes")
    .select("*")
    .in("pull_id", pulls.map((p) => String(p.id)));

  if (res.error) {
    console.warn("golfin_gacha_prizes read failed:", res.error.message);
    return byPull;
  }
  for (const r of (res.data ?? []) as Row[]) {
    const key = String(r.pull_id);
    const list = byPull.get(key) ?? [];
    list.push(toPrize(r, names));
    byPull.set(key, list);
  }
  for (const list of byPull.values()) list.sort((a, b) => a.slot - b.slot);
  return byPull;
}

export interface PullFilters {
  email?: string;
  bannerId?: string;
  from?: string;
  to?: string;
  limit?: number;
  before?: string;
}

const PAGE = 50;

/**
 * Stats cards — pulls today / 7 d, tickets sunk, dupe RP paid.
 *
 * Counted over the same two tables the log reads, not sampled: at this scale a
 * count IS the number, and an estimate on an ops card is a number nobody can
 * act on. `ticketsSunk` is the SUM OF COSTS on the pull rows rather than the
 * negative side of the ticket ledger, deliberately: the ledger also carries
 * admin adjustments, and "how many tickets did players spend on the gacha" is
 * not the same question as "how many left balances".
 */
async function fetchStats(): Promise<GachaStats> {
  const admin = getSupabaseAdmin();
  const now = Date.now();
  const dayAgo = new Date(now - 24 * 60 * 60 * 1000).toISOString();
  const weekAgo = new Date(now - 7 * 24 * 60 * 60 * 1000).toISOString();

  const [recent, dupes] = await Promise.all([
    admin
      .from("golfin_gacha_pulls")
      .select("cost, created_at")
      .gte("created_at", weekAgo),
    admin
      .from("golfin_gacha_prizes")
      .select("dupe_rp, pull_id")
      .gt("dupe_rp", 0),
  ]);

  const stats: GachaStats = {
    pullsToday: 0,
    pulls7d: 0,
    ticketsSunkToday: 0,
    ticketsSunk7d: 0,
    dupeRp7d: 0,
  };

  const recentIds = new Set<string>();
  if (!recent.error) {
    for (const r of (recent.data ?? []) as Row[]) {
      const at = String(r.created_at ?? "");
      const cost = num(r.cost);
      stats.pulls7d += 1;
      stats.ticketsSunk7d += cost;
      if (at >= dayAgo) {
        stats.pullsToday += 1;
        stats.ticketsSunkToday += cost;
      }
    }
  }

  // The dupe total is scoped to the same 7-day window by joining against the
  // pull ids just read, rather than by a second time filter on a table that has
  // no timestamp of its own.
  if (!recent.error) {
    const idRes = await admin
      .from("golfin_gacha_pulls")
      .select("id")
      .gte("created_at", weekAgo);
    if (!idRes.error) {
      for (const r of (idRes.data ?? []) as Row[]) recentIds.add(String(r.id));
    }
  }
  if (!dupes.error) {
    for (const r of (dupes.data ?? []) as Row[]) {
      if (recentIds.has(String(r.pull_id))) stats.dupeRp7d += num(r.dupe_rp);
    }
  }

  return stats;
}

export async function fetchGachaPulls(
  filters: PullFilters = {}
): Promise<GachaPullsResponse> {
  const limit = Math.min(Math.max(filters.limit ?? PAGE, 1), 200);

  if (isMockMode()) {
    return {
      pulls: MOCK_GACHA_PULLS,
      banners: ["banner_standard_club1", "banner_test_a", "banner_test_b"],
      nextBefore: null,
      gachaEnabled: MOCK_GACHA_ENABLED.value,
      stats: {
        pullsToday: 11,
        pulls7d: 42,
        ticketsSunkToday: 525,
        ticketsSunk7d: 2150,
        dupeRp7d: 460,
      },
      mock: true,
    };
  }

  const admin = getSupabaseAdmin();

  // The email filter is resolved to user ids FIRST, because `golfin_gacha_pulls`
  // has no email — it has a `user_id`, and auth.users is not joinable over
  // PostgREST. Resolving it here also makes a partial match ("cesar") work,
  // which is what an operator actually types.
  let userIds: string[] | null = null;
  if (filters.email && filters.email.trim()) {
    const needle = filters.email.trim().toLowerCase();
    const directory = await fetchUserDirectory();
    userIds = [...directory.entries()]
      .filter(([, id]) => (id.email ?? "").toLowerCase().includes(needle))
      .map(([uid]) => uid);
    // No match is a real answer — an empty log, not the unfiltered one.
    if (userIds.length === 0) {
      return {
        pulls: [],
        banners: [...(await catalogRows("gacha_banners")).keys()].sort(),
        nextBefore: null,
        gachaEnabled: await fetchGachaEnabled(),
        stats: await fetchStats(),
        mock: false,
      };
    }
  }

  let query = admin
    .from("golfin_gacha_pulls")
    .select("*")
    .order("created_at", { ascending: false })
    .limit(limit);

  if (userIds) query = query.in("user_id", userIds);
  if (filters.bannerId) query = query.eq("banner_id", filters.bannerId);
  if (filters.from) query = query.gte("created_at", filters.from);
  if (filters.to) query = query.lte("created_at", filters.to);
  if (filters.before) query = query.lt("created_at", filters.before);

  const res = await query;
  if (res.error) {
    if (isMissingRelation(res.error.message)) {
      return {
        pulls: [],
        banners: [],
        nextBefore: null,
        gachaEnabled: true,
        stats: { pullsToday: 0, pulls7d: 0, ticketsSunkToday: 0, ticketsSunk7d: 0, dupeRp7d: 0 },
        mock: false,
        notMigrated: NOT_MIGRATED,
      };
    }
    throw new Error(`golfin_gacha_pulls read failed: ${res.error.message}`);
  }

  const rows = (res.data ?? []) as Row[];
  const [names, directory, banners, enabled, stats] = await Promise.all([
    prizeNameIndex(),
    fetchUserDirectory(),
    catalogRows("gacha_banners"),
    fetchGachaEnabled(),
    fetchStats(),
  ]);
  const byPull = await attachPrizes(rows, names);

  const pulls: GachaPullRow[] = rows.map((r) => ({
    id: String(r.id),
    userId: String(r.user_id ?? ""),
    userEmail: directory.get(String(r.user_id ?? ""))?.email ?? null,
    bannerId: String(r.banner_id ?? ""),
    poolId: String(r.pool_id ?? ""),
    pullCount: num(r.pull_count, 1),
    ticketType: num(r.ticket_type),
    cost: num(r.cost),
    pityBefore: num(r.pity_before),
    pityAfter: num(r.pity_after),
    pityForced: r.pity_forced === true,
    guaranteeForced: r.guarantee_forced === true,
    build: num(r.build),
    createdAt: String(r.created_at ?? ""),
    prizes: byPull.get(String(r.id)) ?? [],
  }));

  return {
    pulls,
    banners: [...banners.keys()].sort(),
    nextBefore: pulls.length === limit ? (pulls[pulls.length - 1]?.createdAt ?? null) : null,
    gachaEnabled: enabled,
    stats,
    mock: false,
  };
}

/** How many pulls the odds audit samples. `null` = every pull on the banner. */
export type OddsSample = 100 | 1000 | null;

/**
 * The odds audit for one banner (§6).
 *
 * The BANNER decides which pool's rate table is the published side, and the
 * sample is over that banner's pulls — not the pool's. Two banners can share a
 * pool while promising different pity, so auditing "the pool" would mix two
 * populations whose forced-slot rates differ.
 */
export async function fetchGachaOdds(
  bannerId: string,
  sample: OddsSample = 1000
): Promise<GachaOddsResponse> {
  const empty = (poolId: string, notMigrated?: string): GachaOddsResponse => ({
    bannerId,
    poolId,
    sampledPulls: 0,
    comparableSlots: 0,
    forcedSlots: 0,
    pityPulls: 0,
    guaranteePulls: 0,
    significant: false,
    tiers: [],
    mock: isMockMode(),
    ...(notMigrated ? { notMigrated } : {}),
  });

  if (isMockMode()) {
    const pulls: AuditPull[] = MOCK_GACHA_PULLS.filter((p) => p.bannerId === bannerId).map((p) => ({
      id: p.id,
      bannerId: p.bannerId,
      poolId: p.poolId,
      pullCount: p.pullCount,
      pityForced: p.pityForced,
      guaranteeForced: p.guaranteeForced,
      createdAt: p.createdAt,
    }));
    const prizes = new Map<string, AuditPrize[]>(
      MOCK_GACHA_PULLS.filter((p) => p.bannerId === bannerId).map((p) => [
        p.id,
        p.prizes.map((z) => ({
          pullId: p.id, slot: z.slot, rarity: z.rarity, kind: z.kind,
          refId: z.refId, isDupe: z.isDupe, dupeRp: z.dupeRp,
        })),
      ])
    );
    const published = { Common: 5500, Uncommon: 2500, Rare: 1200, Mythic: 550, Legendary: 200, Supreme: 50 };
    const audit = auditOdds(pulls, prizes, published);
    return {
      bannerId,
      poolId: pulls[0]?.poolId ?? "pool_standard_club1",
      sampledPulls: pulls.length,
      comparableSlots: audit.comparableSlots,
      forcedSlots: audit.forcedSlots,
      pityPulls: audit.pityPulls,
      guaranteePulls: audit.guaranteePulls,
      significant: audit.significant,
      tiers: audit.tiers,
      mock: true,
    };
  }

  const banners = await catalogRows("gacha_banners");
  const banner = banners.get(bannerId);
  const poolId = banner ? String(banner.poolId ?? "") : "";
  if (!poolId) return empty("");

  const admin = getSupabaseAdmin();
  let query = admin
    .from("golfin_gacha_pulls")
    .select("id, banner_id, pool_id, pull_count, pity_forced, guarantee_forced, created_at")
    .eq("banner_id", bannerId)
    .order("created_at", { ascending: false });
  // `null` is "all", and it is still capped: an unbounded read of a table that
  // grows per pull is how an ops panel becomes the slowest page in the app.
  query = query.limit(sample ?? 5000);

  const pullRes = await query;
  if (pullRes.error) {
    if (isMissingRelation(pullRes.error.message)) return empty(poolId, NOT_MIGRATED);
    throw new Error(`golfin_gacha_pulls read failed: ${pullRes.error.message}`);
  }

  const pullRows = (pullRes.data ?? []) as Row[];
  const pulls: AuditPull[] = pullRows.map((r) => ({
    id: String(r.id),
    bannerId: String(r.banner_id ?? ""),
    poolId: String(r.pool_id ?? ""),
    pullCount: num(r.pull_count, 1),
    pityForced: r.pity_forced === true,
    guaranteeForced: r.guarantee_forced === true,
    createdAt: String(r.created_at ?? ""),
  }));

  const prizesByPull = new Map<string, AuditPrize[]>();
  if (pulls.length > 0) {
    const prizeRes = await admin
      .from("golfin_gacha_prizes")
      .select("pull_id, slot, kind, ref_id, rarity, is_dupe, dupe_rp")
      .in("pull_id", pulls.map((p) => p.id));
    if (!prizeRes.error) {
      for (const r of (prizeRes.data ?? []) as Row[]) {
        const key = String(r.pull_id);
        const list = prizesByPull.get(key) ?? [];
        list.push({
          pullId: key,
          slot: num(r.slot),
          rarity: String(r.rarity ?? ""),
          kind: String(r.kind ?? ""),
          refId: String(r.ref_id ?? ""),
          isDupe: r.is_dupe === true,
          dupeRp: num(r.dupe_rp),
        });
        prizesByPull.set(key, list);
      }
    }
  }

  const rates = await catalogRows("gacha_rates");
  const publishedBp: Record<string, number> = {};
  for (const data of rates.values()) {
    if (String(data.poolId ?? "") !== poolId) continue;
    const rarity = String(data.rarity ?? "");
    publishedBp[rarity] = (publishedBp[rarity] ?? 0) + num(data.rateBp);
  }

  const audit = auditOdds(pulls, prizesByPull, publishedBp);
  return {
    bannerId,
    poolId,
    sampledPulls: pulls.length,
    comparableSlots: audit.comparableSlots,
    forcedSlots: audit.forcedSlots,
    pityPulls: audit.pityPulls,
    guaranteePulls: audit.guaranteePulls,
    significant: audit.significant,
    tiers: audit.tiers,
    mock: false,
  };
}

/**
 * One player's gacha state — balances, ledger, pity, recent pulls (§6 "Per user").
 *
 * The TICKET BALANCE HERE IS THE LEDGER, not the inventory blob. That is the
 * whole point of §5: an admin grant writes `golfin_ticket_transactions` and the
 * player's device counter is a legacy display until spec C retires it. The
 * Inventory tab labels its blob copy accordingly.
 */
export async function fetchPlayerGacha(userId: string): Promise<PlayerGachaResponse> {
  if (isMockMode()) {
    return {
      balances: MOCK_TICKET_BALANCES,
      transactions: MOCK_TICKET_TRANSACTIONS,
      pity: MOCK_PLAYER_PITY,
      pulls: MOCK_GACHA_PULLS,
      ticketTypes: [
        { id: 0, label: "Ticket" },
        { id: 1, label: "Gold Ticket" },
      ],
      mock: true,
    };
  }

  const admin = getSupabaseAdmin();
  const types = await catalogRows("ticket_types");
  const typeLabel = new Map<number, string>();
  const ticketTypes: Array<{ id: number; label: string }> = [];
  for (const [id, data] of types) {
    if (!/^\d+$/.test(id)) continue;
    const label = str(data.nameEn) ?? `Type ${id}`;
    typeLabel.set(Number(id), label);
    ticketTypes.push({ id: Number(id), label });
  }
  ticketTypes.sort((a, b) => a.id - b.id);

  const [balRes, txRes, pityRes, pullRes] = await Promise.all([
    admin.from("golfin_tickets").select("*").eq("user_id", userId).order("ticket_type"),
    admin
      .from("golfin_ticket_transactions")
      .select("*")
      .eq("user_id", userId)
      .order("created_at", { ascending: false })
      .limit(20),
    admin.from("golfin_gacha_pity").select("*").eq("user_id", userId).order("banner_id"),
    admin
      .from("golfin_gacha_pulls")
      .select("*")
      .eq("user_id", userId)
      .order("created_at", { ascending: false })
      .limit(20),
  ]);

  if (balRes.error && isMissingRelation(balRes.error.message)) {
    return {
      balances: [], transactions: [], pity: [], pulls: [],
      ticketTypes, mock: false, notMigrated: NOT_MIGRATED,
    };
  }

  const balances: TicketBalanceRow[] = ((balRes.data ?? []) as Row[]).map((r) => ({
    ticketType: num(r.ticket_type),
    label: typeLabel.get(num(r.ticket_type)) ?? null,
    balance: num(r.balance),
    updatedAt: str(r.updated_at),
  }));

  const transactions: TicketTransactionRow[] = ((txRes.data ?? []) as Row[]).map((r) => ({
    id: String(r.id),
    ticketType: num(r.ticket_type),
    delta: num(r.delta),
    balanceAfter: num(r.balance_after),
    reason: String(r.reason ?? ""),
    createdBy: str(r.created_by),
    createdAt: String(r.created_at ?? ""),
  }));

  const banners = await catalogRows("gacha_banners");
  const pity: PlayerPityRow[] = ((pityRes.data ?? []) as Row[]).map((r) => {
    const bannerId = String(r.banner_id ?? "");
    const banner = banners.get(bannerId);
    const rawThreshold = banner ? String(banner.pityThreshold ?? "").trim() : "";
    const rawLimit = banner ? String(banner.maxPullsPerPlayer ?? "").trim() : "";
    return {
      bannerId,
      counter: num(r.counter),
      totalPulls: num(r.total_pulls),
      // Blank OR zero is "no pity" — the same rule the function and the
      // simulator both apply, so the drawer cannot show a threshold the server
      // would never act on.
      threshold: /^\d+$/.test(rawThreshold) && Number(rawThreshold) > 0 ? Number(rawThreshold) : null,
      minRarity: banner ? (str(banner.pityMinRarity) ?? null) : null,
      pullLimit: /^\d+$/.test(rawLimit) ? Number(rawLimit) : null,
      updatedAt: str(r.updated_at),
    };
  });

  const names = await prizeNameIndex();
  const pullRows = (pullRes.data ?? []) as Row[];
  const byPull = await attachPrizes(pullRows, names);
  const pulls: GachaPullRow[] = pullRows.map((r) => ({
    id: String(r.id),
    userId,
    userEmail: null,
    bannerId: String(r.banner_id ?? ""),
    poolId: String(r.pool_id ?? ""),
    pullCount: num(r.pull_count, 1),
    ticketType: num(r.ticket_type),
    cost: num(r.cost),
    pityBefore: num(r.pity_before),
    pityAfter: num(r.pity_after),
    pityForced: r.pity_forced === true,
    guaranteeForced: r.guarantee_forced === true,
    build: num(r.build),
    createdAt: String(r.created_at ?? ""),
    prizes: byPull.get(String(r.id)) ?? [],
  }));

  return { balances, transactions, pity, pulls, ticketTypes, mock: false };
}
