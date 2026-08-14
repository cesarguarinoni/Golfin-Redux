/**
 * Courses the game can actually draw a tournament card for.
 *
 * ⚠️ There is NO course registry in the Unity project — `courseId` is a free-form
 * string that nothing validates at load (TournamentCsvLoader.CheckReferentialIntegrity
 * checks prizeTableId and botFieldId only). The convention is held together by three
 * unlinked files that happen to agree:
 *   1. Assets/Resources/Data/tournaments.csv          — the courseId column
 *   2. Assets/Localization/LocalizationText.csv:190   — tourn.venue.<courseId>
 *   3. Assets/Art/Tournaments/CourseImages/<courseId> — the card photo
 * This list is that agreement, written down. The dashboard offers it as a DROPDOWN
 * (SPEC §5b) so the panel cannot author a tournament the game cannot draw.
 *
 * Client-safe: no server imports.
 */

export interface ShippingCourse {
  /** The `course_id` value written to the DB. */
  id: string;
  /** EN string from LocalizationText.csv (tourn.venue.<id>), trimmed of "· 18 Holes". */
  name: string;
  /** File in Assets/Art/Tournaments/CourseImages/ — the Phase-3 bundled fallback. */
  art: string;
  /** True when the course has playable hole content today (Assets/Golf/Courses). */
  playable: boolean;
}

export const SHIPPING_COURSES: readonly ShippingCourse[] = [
  { id: "kasumigaseki", name: "Kasumigaseki Country Club", art: "kasumigaseki.png", playable: false },
  { id: "hirono", name: "Hirono Golf Club", art: "hirono.png", playable: false },
  { id: "kisarazu", name: "Kisarazu Higashi CC", art: "kisarazu.png", playable: false },
  { id: "lomond", name: "Lomond Country Club", art: "lomond.png", playable: true },
  { id: "gotemba", name: "Taiheyo Club Gotemba", art: "gotemba.png", playable: false },
  { id: "kawana", name: "Kawana Hotel Golf Course", art: "kawana.jpg", playable: false },
] as const;

export function findCourse(id: string | null): ShippingCourse | undefined {
  if (!id) return undefined;
  return SHIPPING_COURSES.find((c) => c.id === id);
}

export function courseName(id: string | null): string {
  return findCourse(id)?.name ?? id ?? "—";
}

/** Bot fields from Assets/Resources/Data/tournament_bot_fields.csv (client-side sim tuning). */
export const BOT_FIELDS = [
  { id: "field_small", label: "field_small — 12 bots" },
  { id: "field_medium", label: "field_medium — 20 bots" },
  { id: "field_major", label: "field_major — 30 bots" },
] as const;

/** league_key values the T7 badge background understands. */
export const LEAGUE_KEYS = ["DIAMOND", "GOLD", "SILVER"] as const;

/** Card art spec, derived from the shipped 260×360 course photos. */
export const ART_SPEC = {
  width: 260,
  height: 360,
  aspect: 260 / 360,
  /** Warn (not block) outside ±12% of the card aspect. */
  aspectTolerance: 0.12,
  maxBytes: 512_000,
  mimeTypes: ["image/jpeg", "image/png", "image/webp"] as const,
};
