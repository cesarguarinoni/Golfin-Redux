import "server-only";
import type { PrizeBand, TournamentEntryRow, TournamentRow } from "./types";

/**
 * Mock-mode tournament fixtures — a faithful copy of what
 * migrations/2026_08_13_tournaments_golfin.sql seeded into prod, so the panel
 * looks and behaves the same on fixtures as it does live.
 */

const MAJOR: Omit<PrizeBand, "id">[] = [
  { rankFrom: 1, rankTo: 1, rpReward: 2000, itemRewardId: "trophy_major" },
  { rankFrom: 2, rankTo: 3, rpReward: 1200, itemRewardId: "ticket_gold" },
  { rankFrom: 4, rankTo: 10, rpReward: 500, itemRewardId: null },
  { rankFrom: 11, rankTo: 50, rpReward: 100, itemRewardId: null },
];
const MEDIUM: Omit<PrizeBand, "id">[] = [
  { rankFrom: 1, rankTo: 1, rpReward: 500, itemRewardId: "ticket_gold" },
  { rankFrom: 2, rankTo: 3, rpReward: 300, itemRewardId: null },
  { rankFrom: 4, rankTo: 10, rpReward: 100, itemRewardId: null },
];
const SMALL: Omit<PrizeBand, "id">[] = [
  { rankFrom: 1, rankTo: 1, rpReward: 300, itemRewardId: null },
  { rankFrom: 2, rankTo: 3, rpReward: 150, itemRewardId: null },
  { rankFrom: 4, rankTo: 10, rpReward: 50, itemRewardId: null },
];

function bands(slug: string, template: Omit<PrizeBand, "id">[]): PrizeBand[] {
  return template.map((b, i) => ({ ...b, id: `${slug}_band_${i}` }));
}

interface Seed {
  slug: string;
  title: string;
  nameKey: string;
  courseId: string;
  startAt: string;
  endAt: string;
  fee: number;
  botField: string;
  sponsor: string;
  league: string;
  seed: number;
  template: Omit<PrizeBand, "id">[];
}

const SEEDS: Seed[] = [
  { slug: "kasumigaseki_open", title: "Kasumigaseki Open", nameKey: "tourn.kasumigaseki", courseId: "kasumigaseki", startAt: "2026-08-09T00:00:00Z", endAt: "2026-08-25T00:00:00Z", fee: 10, botField: "field_major", sponsor: "PUMA", league: "DIAMOND", seed: 1001, template: MAJOR },
  { slug: "hirono_invitational", title: "Hirono Invitational", nameKey: "tourn.hirono", courseId: "hirono", startAt: "2026-08-08T00:00:00Z", endAt: "2026-08-14T12:00:00Z", fee: 0, botField: "field_major", sponsor: "GOLFIN", league: "DIAMOND", seed: 1002, template: MAJOR },
  { slug: "kisarazu_cup", title: "Kisarazu Cup", nameKey: "tourn.kisarazu", courseId: "kisarazu", startAt: "2026-08-10T00:00:00Z", endAt: "2026-08-20T00:00:00Z", fee: 0, botField: "field_small", sponsor: "MIZUNO", league: "SILVER", seed: 1003, template: SMALL },
  { slug: "lomond_championship", title: "Lomond Championship", nameKey: "tourn.lomond", courseId: "lomond", startAt: "2026-08-18T00:00:00Z", endAt: "2026-08-24T00:00:00Z", fee: 0, botField: "field_medium", sponsor: "TITLEIST", league: "GOLD", seed: 1004, template: MEDIUM },
  { slug: "gotemba_masters", title: "Gotemba Masters", nameKey: "tourn.gotemba", courseId: "gotemba", startAt: "2026-07-28T00:00:00Z", endAt: "2026-08-05T00:00:00Z", fee: 50, botField: "field_major", sponsor: "TAIHEIYO", league: "GOLD", seed: 1005, template: MAJOR },
  { slug: "kawana_fuji_open", title: "Kawana Fuji Open", nameKey: "tourn.kawana", courseId: "kawana", startAt: "2026-07-20T00:00:00Z", endAt: "2026-07-27T00:00:00Z", fee: 0, botField: "field_major", sponsor: "GOLFIN", league: "DIAMOND", seed: 1006, template: MAJOR },
];

export const MOCK_TOURNAMENTS: TournamentRow[] = SEEDS.map((s) => ({
  id: `00000000-0000-4000-8000-${String(s.seed).padStart(12, "0")}`,
  kind: "golfin" as const,
  slug: s.slug,
  title: s.title,
  titleJa: null,
  modalBannerId: null,
  descriptionEn: null,
  descriptionJa: null,
  descriptionKey: null,
  nameKey: s.nameKey,
  courseId: s.courseId,
  holeSet: "1-18",
  startAt: s.startAt,
  endAt: s.endAt,
  resolveDelayMinutes: 30,
  entryFeePts: s.fee,
  botFieldId: s.botField,
  sponsorName: s.sponsor,
  leagueKey: s.league,
  bannerUrl: null,
  isActive: true,
  // Restrictions mirror the prod backfill: sponsor / level / own, bands unset.
  category: "sponsor",
  maxPlayers: null,
  playersPerDivision: null,
  divisionType: "level",
  charRarityMin: null,
  charRarityMax: null,
  charLevelMin: null,
  charLevelMax: null,
  gearRule: "own",
  clubRarityMax: null,
  botSeed: s.seed,
  status: "upcoming",
  tier: "open",
  createdAt: "2026-08-13T00:00:00Z",
  bands: bands(s.slug, s.template),
  entryCount: 0,
  humanEntryCount: 0,
}));

/** A couple of entries on the currently-Open tournament so the tab is not empty. */
/** The Kasumigaseki row — the one that is Open at the time of writing. */
const OPEN_ID = MOCK_TOURNAMENTS[0]?.id ?? "";

export const MOCK_TOURNAMENT_ENTRIES: Record<string, TournamentEntryRow[]> = {
  [OPEN_ID]: [
    {
      id: "e0000000-0000-4000-8000-000000000001",
      userId: "11111111-1111-4111-8111-111111111111",
      userEmail: "cesar.guarinoni@gmail.com",
      displayName: "Cratilo",
      characterId: "char_ken",
      isBot: false,
      bestScore: 71,
      holesCompleted: 18,
      status: "finished",
      finalRank: 2,
      prizePtsAwarded: null,
      prizeClaimed: false,
      enteredAt: "2026-08-12T09:12:00Z",
      submittedAt: "2026-08-12T10:41:00Z",
    },
    {
      id: "e0000000-0000-4000-8000-000000000002",
      userId: null,
      userEmail: null,
      displayName: "Bot_Takeda",
      characterId: null,
      isBot: true,
      bestScore: 69,
      holesCompleted: 18,
      status: "finished",
      finalRank: 1,
      prizePtsAwarded: null,
      prizeClaimed: false,
      enteredAt: "2026-08-12T08:30:00Z",
      submittedAt: "2026-08-12T09:58:00Z",
    },
    {
      id: "e0000000-0000-4000-8000-000000000003",
      userId: "22222222-2222-4222-8222-222222222222",
      userEmail: "cesar.guarinoni@wonderwall-g.com",
      displayName: "Cesar",
      characterId: "char_aoi",
      isBot: false,
      bestScore: null,
      holesCompleted: 7,
      status: "in_progress",
      finalRank: null,
      prizePtsAwarded: null,
      prizeClaimed: false,
      enteredAt: "2026-08-13T22:05:00Z",
      submittedAt: null,
    },
  ],
};

/** Keep entryCount honest on the fixtures. */
for (const t of MOCK_TOURNAMENTS) {
  const entries = MOCK_TOURNAMENT_ENTRIES[t.id] ?? [];
  t.entryCount = entries.length;
  t.humanEntryCount = entries.filter((e) => !e.isBot).length;
}
