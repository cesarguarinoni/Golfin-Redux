/**
 * The loadout MASK VOCABULARY, in TypeScript.
 *
 * This is the publish-side twin of `Assets/Scripts/UI/MissionSelection/LoadoutTokens.cs`. The
 * runtime resolver reads a `mission_loadouts` mask to BUILD a bag; this reads the same mask to
 * refuse a publish that names a bag no `clubs` row can fill. Two implementations of one grammar
 * are only allowed to exist because `Tools/content/tests/loadout_tokens_fixture.csv` is run
 * through BOTH of them — `lib/__tests__/loadoutTokens.test.ts` reads it, and so does the EditMode
 * `LoadoutTokensTests`. The day they disagree a test goes red, rather than a mission shipping a
 * bag with a hole in it.
 *
 * THE GRAMMAR (case-insensitive):
 *   Driver | Wood | Putter   the matching club type
 *   AW | PW | SW             A.Wedge / P.Wedge / S.Wedge (the CSV spellings)
 *   Iron                     ANY iron, whatever its loft — a FAMILY token
 *   IronN (one digit)        an iron whose loft parses to N
 *   anything else            never matches; `isKnown` is false and the caller reports it
 *
 * THE LOFT PARSE IS ANCHORED. Its runtime predecessor asked whether id+name "contains 9" and
 * then whether it "contains 7", so `Iron 5 X7` was a 7-iron. Here the loft is the digit in
 * `Iron <N>` at the start of the NAME, or in `club_iron<N>` at the start of the ID, or nothing.
 */

/** The three `clubs` columns this grammar reads. `type` is the CSV spelling ("A.Wedge"). */
export interface LoadoutClub {
  id: string;
  name: string;
  type: string;
}

/** The families a token can name, in the mask's own spelling. */
const FAMILIES = ["Driver", "Wood", "Iron", "AW", "PW", "SW", "Putter"] as const;

/**
 * What an operator is shown when a mask names something this grammar does not know.
 * Iron0–Iron3 parse, but no shipped iron carries those lofts, so the hint names the real range.
 */
export const KNOWN_TOKENS_HINT = "Driver, Wood, Iron, Iron4–Iron9, AW, PW, SW, Putter";

const NAME_LOFT = /^\s*Iron\s+(\d)\b/i;
const ID_LOFT = /^club_iron(\d)/i;
const IRON_TOKEN = /^iron(\d)$/i;

/** The family a `clubs.type` value belongs to, or "" when it is not a club type at all. */
export function familyOf(type: string): string {
  switch ((type ?? "").trim().toLowerCase()) {
    case "driver":
      return "Driver";
    case "wood":
      return "Wood";
    case "iron":
      return "Iron";
    case "a.wedge":
    case "a_wedge":
    case "aw":
      return "AW";
    case "p.wedge":
    case "p_wedge":
    case "pw":
      return "PW";
    case "s.wedge":
    case "s_wedge":
    case "sw":
      return "SW";
    case "putter":
      return "Putter";
    default:
      return "";
  }
}

/** True when the grammar can say anything at all about this token. */
export function isKnown(token: string): boolean {
  const t = (token ?? "").trim();
  if (!t) return false;
  if (IRON_TOKEN.test(t)) return true;
  return FAMILIES.some((f) => f.toLowerCase() === t.toLowerCase());
}

/**
 * The iron number a club carries, or null for a loft-less iron (and for anything that is not an
 * iron — callers gate on the family first). Name wins over id; both are anchored.
 */
export function ironLoft(clubId: string, name: string): number | null {
  const byName = NAME_LOFT.exec(name ?? "");
  if (byName) return Number(byName[1]);
  const byId = ID_LOFT.exec(clubId ?? "");
  if (byId) return Number(byId[1]);
  return null;
}

/** Does `token` name this club? */
export function matches(club: LoadoutClub, token: string): boolean {
  const family = familyOf(club?.type ?? "");
  if (!family) return false;

  const t = (token ?? "").trim();
  if (!t) return false;

  if (t.toLowerCase() === family.toLowerCase()) return true;

  const iron = IRON_TOKEN.exec(t);
  if (iron && family === "Iron") return ironLoft(club.id ?? "", club.name ?? "") === Number(iron[1]);

  return false;
}
