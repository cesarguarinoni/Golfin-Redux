import "server-only";
import type { User } from "@supabase/supabase-js";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type {
  PrizeBand,
  TournamentEntriesResponse,
  TournamentEntryRow,
  TournamentKind,
  TournamentRow,
  TournamentsResponse,
} from "./types";

/** Read side of the Tournaments panel. Branches mock ↔ live like lib/data.ts. */

type Row = Record<string, unknown>;

function num(v: unknown, fallback = 0): number {
  return typeof v === "number" && Number.isFinite(v) ? v : fallback;
}
function numOrNull(v: unknown): number | null {
  return typeof v === "number" && Number.isFinite(v) ? v : null;
}
function str(v: unknown): string | null {
  return typeof v === "string" && v.length > 0 ? v : null;
}

export function mapBand(r: Row): PrizeBand {
  return {
    id: String(r.id ?? ""),
    rankFrom: num(r.rank_from, 1),
    rankTo: num(r.rank_to, 1),
    rpReward: num(r.rp_reward),
    itemRewardId: str(r.item_reward_id),
  };
}

function mapTournament(
  r: Row,
  bands: PrizeBand[],
  entryCount: number,
  humanEntryCount: number
): TournamentRow {
  const kind: TournamentKind = r.kind === "golfin" ? "golfin" : "gps";
  return {
    id: String(r.id ?? ""),
    kind,
    slug: str(r.slug),
    title: String(r.title ?? "(untitled)"),
    titleJa: str(r.title_ja),
    nameKey: str(r.name_key),
    courseId: str(r.course_id),
    holeSet: str(r.hole_set),
    startAt: str(r.start_at),
    endAt: str(r.end_at),
    resolveDelayMinutes: numOrNull(r.resolve_delay_minutes),
    entryFeePts: num(r.entry_fee_pts),
    botFieldId: str(r.bot_field_id),
    sponsorName: str(r.sponsor_name),
    leagueKey: str(r.league_key),
    bannerUrl: str(r.banner_url),
    botSeed: numOrNull(r.bot_seed),
    status: str(r.status),
    tier: str(r.tier),
    createdAt: str(r.created_at),
    bands: bands.sort((a, b) => a.rankFrom - b.rankFrom),
    entryCount,
    humanEntryCount,
  };
}

export async function fetchTournaments(): Promise<TournamentsResponse> {
  if (isMockMode()) {
    const db = mockDb();
    const tournaments = db.tournaments.map((t) => {
      const entries = db.tournamentEntries[t.id] ?? [];
      return {
        ...t,
        entryCount: entries.length,
        humanEntryCount: entries.filter((e) => !e.isBot).length,
      };
    });
    return { tournaments: sortBySchedule(tournaments), mock: true };
  }

  const admin = getSupabaseAdmin();
  const [tRes, bRes, eRes] = await Promise.all([
    admin.from("tournaments").select("*"),
    admin.from("tournament_prize_bands").select("*"),
    admin.from("tournament_entries").select("id, tournament_id, is_bot"),
  ]);
  if (tRes.error) throw new Error(`tournaments query failed: ${tRes.error.message}`);
  if (bRes.error) throw new Error(`tournament_prize_bands query failed: ${bRes.error.message}`);

  const bandsByT = new Map<string, PrizeBand[]>();
  for (const r of (bRes.data ?? []) as Row[]) {
    const key = String(r.tournament_id ?? "");
    const list = bandsByT.get(key) ?? [];
    list.push(mapBand(r));
    bandsByT.set(key, list);
  }

  const counts = new Map<string, { all: number; human: number }>();
  // tournament_entries may fail on a schema that predates the migration —
  // a missing count must not take the whole panel down.
  if (!eRes.error) {
    for (const r of (eRes.data ?? []) as Row[]) {
      const key = String(r.tournament_id ?? "");
      const c = counts.get(key) ?? { all: 0, human: 0 };
      c.all += 1;
      if (r.is_bot !== true) c.human += 1;
      counts.set(key, c);
    }
  } else {
    console.warn("tournament_entries count failed:", eRes.error.message);
  }

  const tournaments = (tRes.data as Row[]).map((r) => {
    const id = String(r.id ?? "");
    const c = counts.get(id) ?? { all: 0, human: 0 };
    return mapTournament(r, bandsByT.get(id) ?? [], c.all, c.human);
  });

  return { tournaments: sortBySchedule(tournaments), mock: false };
}

/** Newest-starting first — the schedule reads the way you edit it. */
function sortBySchedule(rows: TournamentRow[]): TournamentRow[] {
  return [...rows].sort((a, b) => (b.startAt ?? "").localeCompare(a.startAt ?? ""));
}

export async function fetchTournamentEntries(
  tournamentId: string
): Promise<TournamentEntriesResponse> {
  if (isMockMode()) {
    const entries = mockDb().tournamentEntries[tournamentId] ?? [];
    return { entries: sortEntries(entries), mock: true };
  }

  const admin = getSupabaseAdmin();
  const { data, error } = await admin
    .from("tournament_entries")
    .select("*")
    .eq("tournament_id", tournamentId)
    .limit(500);
  if (error) throw new Error(`tournament_entries query failed: ${error.message}`);

  const rows = (data ?? []) as Row[];
  const userIds = [...new Set(rows.map((r) => str(r.user_id)).filter((x): x is string => !!x))];

  // Emails come from auth.users; only fetch when there are humans to name.
  const emailById = new Map<string, string>();
  if (userIds.length > 0) {
    const perPage = 1000;
    for (let page = 1; ; page++) {
      const { data: pageData, error: pageErr } = await admin.auth.admin.listUsers({
        page,
        perPage,
      });
      if (pageErr) break;
      for (const u of pageData.users as User[]) {
        if (u.email) emailById.set(u.id, u.email);
      }
      if (pageData.users.length < perPage) break;
    }
  }

  const entries: TournamentEntryRow[] = rows.map((r) => {
    const userId = str(r.user_id);
    return {
      id: String(r.id ?? ""),
      userId,
      userEmail: userId ? emailById.get(userId) ?? null : null,
      displayName: str(r.display_name),
      characterId: str(r.character_id),
      isBot: r.is_bot === true,
      bestScore: numOrNull(r.best_score),
      holesCompleted: num(r.holes_completed),
      status: String(r.status ?? "in_progress"),
      finalRank: numOrNull(r.final_rank),
      prizePtsAwarded: numOrNull(r.prize_pts_awarded),
      prizeClaimed: r.prize_claimed === true,
      enteredAt: str(r.entered_at),
      submittedAt: str(r.submitted_at),
    };
  });

  return { entries: sortEntries(entries), mock: false };
}

/** Leaderboard order: finished by score, then in-progress by holes played. */
function sortEntries(entries: TournamentEntryRow[]): TournamentEntryRow[] {
  return [...entries].sort((a, b) => {
    const aDone = a.bestScore !== null;
    const bDone = b.bestScore !== null;
    if (aDone !== bDone) return aDone ? -1 : 1;
    if (aDone && bDone) return (a.bestScore ?? 0) - (b.bestScore ?? 0);
    return b.holesCompleted - a.holesCompleted;
  });
}
