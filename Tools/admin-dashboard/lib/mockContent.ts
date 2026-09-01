import type { ContentCatalogSummary, ContentStoredRow, ContentVersionSummary } from "./types";

/**
 * Mock-mode fixtures for the content catalogs.
 *
 * ⚠️ DELIBERATELY, VISIBLY FAKE. Every price is 9999 and every id carries a
 * `mock_` prefix. ADMIN_DASHBOARD_OPS.md §3.5 records a real incident where mock
 * fixtures were read as production facts; a fixture that looks plausible is the
 * bug. If you find yourself wanting realistic numbers here, you want a real
 * service key instead (see lib/mode.ts).
 */

export const MOCK_CONTENT_CATALOGS: ContentCatalogSummary[] = [
  { name: "clubs", publishedVersion: 9999, isEnabled: true, publishedCount: 2, draftCount: 2, dirtyCount: 1 },
  { name: "characters", publishedVersion: 9999, isEnabled: true, publishedCount: 2, draftCount: 2, dirtyCount: 0 },
  { name: "items", publishedVersion: 9999, isEnabled: true, publishedCount: 2, draftCount: 2, dirtyCount: 0 },
  { name: "bags", publishedVersion: 9999, isEnabled: true, publishedCount: 2, draftCount: 2, dirtyCount: 0 },
  // DISABLED on purpose: the kill-switch badge and the "the game is not being
  // served this catalog" copy need a catalog in that state to render against.
  { name: "balls", publishedVersion: 9999, isEnabled: false, publishedCount: 2, draftCount: 2, dirtyCount: 0 },
  { name: "texts", publishedVersion: 9999, isEnabled: true, publishedCount: 1, draftCount: 1, dirtyCount: 0 },
  { name: "shop_catalog", publishedVersion: 9999, isEnabled: true, publishedCount: 1, draftCount: 1, dirtyCount: 0 },
  { name: "level_up_costs", publishedVersion: 9999, isEnabled: true, publishedCount: 3, draftCount: 3, dirtyCount: 0 },
  { name: "modes", publishedVersion: 9999, isEnabled: true, publishedCount: 2, draftCount: 2, dirtyCount: 0 },
  // gacha_admin_catalogs §5.1 — all four, so every gacha panel is exercisable
  // with MOCK_MODE=1 (tabs, badges, pickers, the odds table and the simulator).
  { name: "gacha_banners", publishedVersion: 9999, isEnabled: true, publishedCount: 3, draftCount: 3, dirtyCount: 0 },
  { name: "gacha_rates", publishedVersion: 9999, isEnabled: true, publishedCount: 6, draftCount: 6, dirtyCount: 0 },
  { name: "gacha_pools", publishedVersion: 9999, isEnabled: true, publishedCount: 6, draftCount: 6, dirtyCount: 0 },
  { name: "ticket_types", publishedVersion: 9999, isEnabled: true, publishedCount: 2, draftCount: 2, dirtyCount: 0 },
];

const row = (
  catalog: string,
  rowId: string,
  data: Record<string, string>
): ContentStoredRow => ({ catalog, rowId, data, minBuild: 0, isActive: true, version: 9999 });

export const MOCK_CONTENT_PUBLISHED: ContentStoredRow[] = [
  row("clubs", "mock_club_driver", {
    id: "mock_club_driver", name: "MOCK Driver", type: "Driver", rarity: "Common", brand: "MOCK",
    basePower: "9999", baseAccuracy: "9999", maxDurability: "9999", startLevel: "1", maxLevel: "9999",
  }),
  row("clubs", "mock_club_putter", {
    id: "mock_club_putter", name: "MOCK Putter", type: "Putter", rarity: "Common", brand: "MOCK",
    basePower: "9999", baseAccuracy: "9999", maxDurability: "9999", startLevel: "1", maxLevel: "9999",
  }),
  row("characters", "mock_char", {
    id: "mock_char", name: "MOCK", lastName: "FIXTURE", rarity: "Common",
    baseStrength: "1", baseClubControl: "1", baseRecovery: "1", baseStamina: "1",
    startLevel: "1", maxLevel: "9999",
    portraitSprite: "MockFixture", portraitFull: "BigRosterMockFixture",
  }),
  // URL SET, SPRITE NAME EMPTY on purpose — the `URL-only · not bundled` badge
  // (content_art_bundling §9.2) needs a row in that state to render against, for
  // the same reason `balls` is a disabled catalog above. This is what an
  // admin-created row looks like between "art uploaded" and "a build bundled it":
  // installed builds render it over the network, and GOLFIN/Content/Fetch URL Art
  // is the step that ends it.
  row("characters", "mock_char_urlonly", {
    id: "mock_char_urlonly", name: "MOCK", lastName: "URL-ONLY", rarity: "Common",
    baseStrength: "1", baseClubControl: "1", baseRecovery: "1", baseStamina: "1",
    startLevel: "1", maxLevel: "9999",
    portraitSprite: "", portraitFull: "",
    portraitUrl:
      "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/catalog-art/" +
      "characters-mock_char_urlonly-portraitUrl-999999999999.png",
  }),
  row("texts", "MOCK_KEY", { key: "MOCK_KEY", English: "MOCK FIXTURE", Japanese: "モックデータ" }),
  // A key with NO Japanese, so the Texts panel's "No Japanese" badge has
  // something to render in mock mode.
  row("texts", "MOCK_KEY_NO_JA", { key: "MOCK_KEY_NO_JA", English: "MOCK FIXTURE (no JA)", Japanese: "" }),
  row("items", "mock_item_repairkit", {
    id: "mock_item_repairkit", name: "MOCK Repair Kit", category: "repair", rarity: "Common",
    restorePercent: "9999", thumbnailSprite: "MOCK-Thumb", fullSprite: "MOCK-Full",
    proTip: "MOCK FIXTURE", info: "MOCK FIXTURE",
  }),
  row("items", "mock_item_potion", {
    id: "mock_item_potion", name: "MOCK Stamina Potion", category: "stamina", rarity: "Rare",
    restorePercent: "9999", thumbnailSprite: "MOCK-Thumb", fullSprite: "MOCK-Full",
    proTip: "MOCK FIXTURE", info: "MOCK FIXTURE",
  }),
  row("bags", "mock_bag_starter", {
    id: "mock_bag_starter", name: "MOCK Starter Bag", rarity: "Common",
    thumbnail: "MOCK-Thumb", fullImage: "MOCK-Full", description: "MOCK FIXTURE", unlocked: "true",
  }),
  row("bags", "mock_bag_locked", {
    id: "mock_bag_locked", name: "MOCK Locked Bag", rarity: "Legendary",
    thumbnail: "MOCK-Thumb", fullImage: "MOCK-Full", description: "MOCK FIXTURE", unlocked: "false",
  }),
  row("balls", "mock_ball_default", {
    id: "mock_ball_default", name: "MOCK Ball", brand: "MOCK", rarity: "Common",
    power: "9999", rebound: "9999",
    windResistance: "9999", roll: "9999", spin: "9999", thumbnailSprite: "MOCK-Thumb",
    fullSprite: "MOCK-Full", info: "MOCK FIXTURE",
  }),
  // Deactivated on purpose: the shop typeahead must be seen NOT offering it,
  // and the diff's `deactivated` category needs a row to be about.
  { ...row("balls", "mock_ball_retired", {
      id: "mock_ball_retired", name: "MOCK Retired Ball", brand: "MOCK", rarity: "Rare",
      power: "9999",
      rebound: "9999", windResistance: "9999", roll: "9999", spin: "9999",
      thumbnailSprite: "MOCK-Thumb", fullSprite: "MOCK-Full", info: "MOCK FIXTURE",
    }), isActive: false },
  row("shop_catalog", "mock_shop_entry", {
    entryId: "mock_shop_entry", category: "club", refId: "mock_club_driver",
    rpCost: "9999", saleRpCost: "9998", sortOrder: "9999", popular: "false", offer: "false", rarity: "",
  }),
  // Levels 1-3, contiguous. Enough to render the panel, the row editor and the
  // publish drawer against.
  //
  // ⚠️ PUBLISHING level_up_costs IN MOCK MODE FAILS, CORRECTLY. The coverage rule
  // takes its ceiling from the highest `maxLevel` any character or club can
  // reach, and the mock club above claims 9999 — so the validator reports ~9996
  // uncovered levels, which is exactly what it would report against a real
  // catalog that stopped at 3. That is the rule working, not a broken fixture;
  // do not "fix" it by loosening the rule.
  row("level_up_costs", "1", { level: "1", cost_r: "1", sp_reward: "1" }),
  row("level_up_costs", "2", { level: "2", cost_r: "1", sp_reward: "1" }),
  row("level_up_costs", "3", { level: "3", cost_r: "2", sp_reward: "1" }),
  // Two modes, and the pair is chosen to exercise the two rules that matter.
  //
  // `mock_mode_open` has an ABSURD fee (9999) like every other fixture, plus a
  // target the real client dispatches — so the withhold rule is NOT what it
  // demonstrates. `mock_mode_locked` is Coming Soon with target "none".
  //
  // ⚠️ THERE IS DELIBERATELY NO `versus_1v1` ROW HERE, so mock mode never fires
  // the one drift warning. The warning compares the card against the live
  // `game_point_actions.versus_win.pts`, which mock mode does not have; a
  // fixture that made it fire would be warning about a number nobody set.
  row("modes", "mock_mode_open", {
    id: "mock_mode_open", title: "MOCK MODE", tagline: "MOCK FIXTURE",
    description: "MOCK FIXTURE", entryFee: "9999", rewards: "9999", locked: "false",
    target: "hole_select", order: "9998", versusStrokeCapOverPar: "0",
    reward1Type: "", reward1Amount: "", reward2Type: "", reward2Amount: "",
    reward3Type: "", reward3Amount: "", rewardsTextKey: "",
  }),
  row("modes", "mock_mode_locked", {
    id: "mock_mode_locked", title: "MOCK LOCKED MODE", tagline: "MOCK FIXTURE",
    description: "MOCK FIXTURE", entryFee: "0", rewards: "0", locked: "true",
    target: "none", order: "9999", versusStrokeCapOverPar: "0",
    reward1Type: "", reward1Amount: "", reward2Type: "", reward2Amount: "",
    reward3Type: "", reward3Amount: "", rewardsTextKey: "",
  }),

  // ---- gacha (gacha_admin_catalogs §5.1) ----------------------------------
  //
  // Visibly fake like everything else here — MOCK titles, and 9999 nowhere it
  // would break arithmetic the panel does. The rates DO sum to 10 000 and every
  // rated rarity DOES have an entry, because a fixture that fails its own
  // validator teaches nothing about the panel and the effective-odds table
  // would read 0 % everywhere.
  //
  // Three banners, chosen so the STATE BADGE has one of each to render:
  // LIVE (open window), SCHEDULED (starts in 2099) and OFF (active=false).
  // ENDED is reachable by editing endUtc in the drawer, which is exactly what
  // the acceptance asks an operator to do.
  row("ticket_types", "0", { id: "0", key: "standard", nameEn: "MOCK Ticket", nameJa: "モックチケット", iconSprite: "", iconUrl: "" }),
  row("ticket_types", "1", { id: "1", key: "gold", nameEn: "MOCK Gold Ticket", nameJa: "モックゴールドチケット", iconSprite: "", iconUrl: "" }),

  row("gacha_rates", "mock_pool_common", { id: "mock_pool_common", poolId: "mock_pool", rarity: "Common", rateBp: "5500" }),
  row("gacha_rates", "mock_pool_uncommon", { id: "mock_pool_uncommon", poolId: "mock_pool", rarity: "Uncommon", rateBp: "2500" }),
  row("gacha_rates", "mock_pool_rare", { id: "mock_pool_rare", poolId: "mock_pool", rarity: "Rare", rateBp: "1200" }),
  row("gacha_rates", "mock_pool_mythic", { id: "mock_pool_mythic", poolId: "mock_pool", rarity: "Mythic", rateBp: "550" }),
  row("gacha_rates", "mock_pool_legendary", { id: "mock_pool_legendary", poolId: "mock_pool", rarity: "Legendary", rateBp: "200" }),
  row("gacha_rates", "mock_pool_supreme", { id: "mock_pool_supreme", poolId: "mock_pool", rarity: "Supreme", rateBp: "50" }),

  // One entry per rated rarity — the reachability rule (§5.5 rule 9) is
  // satisfied, so mock mode never shows an error that is only a fixture gap.
  // Both mock clubs are Common, so the four higher rarities reference the ball,
  // which HAS no rarity of its own and therefore takes the operator's choice —
  // which is also what demonstrates the editable-rarity half of §5.3.
  row("gacha_pools", "mock_entry_driver", { id: "mock_entry_driver", poolId: "mock_pool", kind: "club", refId: "mock_club_driver", rarity: "Common", weight: "100", quantity: "1", dupeRp: "20", featured: "false" }),
  row("gacha_pools", "mock_entry_ball_u", { id: "mock_entry_ball_u", poolId: "mock_pool", kind: "ball", refId: "mock_ball_default", rarity: "Uncommon", weight: "100", quantity: "3", dupeRp: "0", featured: "false" }),
  row("gacha_pools", "mock_entry_ball_r", { id: "mock_entry_ball_r", poolId: "mock_pool", kind: "ball", refId: "mock_ball_default", rarity: "Rare", weight: "100", quantity: "3", dupeRp: "0", featured: "false" }),
  row("gacha_pools", "mock_entry_ball_m", { id: "mock_entry_ball_m", poolId: "mock_pool", kind: "ball", refId: "mock_ball_default", rarity: "Mythic", weight: "100", quantity: "3", dupeRp: "0", featured: "false" }),
  row("gacha_pools", "mock_entry_ball_l", { id: "mock_entry_ball_l", poolId: "mock_pool", kind: "ball", refId: "mock_ball_default", rarity: "Legendary", weight: "100", quantity: "3", dupeRp: "0", featured: "true" }),
  row("gacha_pools", "mock_entry_ball_s", { id: "mock_entry_ball_s", poolId: "mock_pool", kind: "ball", refId: "mock_ball_default", rarity: "Supreme", weight: "100", quantity: "3", dupeRp: "0", featured: "true" }),

  row("gacha_banners", "mock_banner_live", {
    bannerId: "mock_banner_live", nameKey: "MOCK LIVE BANNER", artSprite: "MOCK-Banner",
    costX1: "9999", costX10: "9999", endUtc: "2099-01-01T00:00:00Z", rulesUrl: "", sortOrder: "1",
    active: "true", startUtc: "2020-01-01T00:00:00Z", poolId: "mock_pool", ticketType: "0",
    pityThreshold: "50", pityMinRarity: "Legendary", guaranteeMinRarityX10: "Rare",
    maxPullsPerPlayer: "", artUrl: "", nameEn: "MOCK LIVE BANNER", nameJa: "モック開催中バナー",
    taglineEn: "MOCK FIXTURE", taglineJa: "モックデータ", featuredRefIds: "mock_ball_default",
  }),
  // SCHEDULED — the window has not opened. No pity at all (pityThreshold 0),
  // which is decision 2's acceptance case: blank and 0 mean the same thing.
  row("gacha_banners", "mock_banner_scheduled", {
    bannerId: "mock_banner_scheduled", nameKey: "MOCK SCHEDULED BANNER", artSprite: "MOCK-Banner",
    costX1: "9999", costX10: "9999", endUtc: "2099-06-01T00:00:00Z", rulesUrl: "", sortOrder: "2",
    active: "true", startUtc: "2099-01-01T00:00:00Z", poolId: "mock_pool", ticketType: "1",
    pityThreshold: "0", pityMinRarity: "", guaranteeMinRarityX10: "",
    maxPullsPerPlayer: "", artUrl: "", nameEn: "MOCK SCHEDULED BANNER", nameJa: "モック開催予定バナー",
    taglineEn: "", taglineJa: "", featuredRefIds: "",
  }),
  // OFF — `active=false` in the DATA. Deliberately NOT `isActive: false`: the
  // two switches are different things (see gachaBannerState) and the fixture
  // that proves the badge reads the column is one where only the column is off.
  row("gacha_banners", "mock_banner_off", {
    bannerId: "mock_banner_off", nameKey: "MOCK OFF BANNER", artSprite: "MOCK-Banner",
    costX1: "9999", costX10: "9999", endUtc: "2099-01-01T00:00:00Z", rulesUrl: "", sortOrder: "3",
    active: "false", startUtc: "2020-01-01T00:00:00Z", poolId: "mock_pool", ticketType: "0",
    pityThreshold: "", pityMinRarity: "", guaranteeMinRarityX10: "",
    maxPullsPerPlayer: "", artUrl: "", nameEn: "MOCK OFF BANNER", nameJa: "モック停止中バナー",
    taglineEn: "", taglineJa: "", featuredRefIds: "",
  }),
];

/** Drafts start identical to published except one obviously-dirty club row. */
export const MOCK_CONTENT_DRAFTS: ContentStoredRow[] = MOCK_CONTENT_PUBLISHED.map((r) =>
  r.rowId === "mock_club_driver"
    ? { ...r, data: { ...r.data, name: "MOCK Driver (EDITED DRAFT)" } }
    : { ...r }
);

/**
 * Version snapshots (content_panels_gaps §2).
 *
 * Deliberately spans a RANGE with a gap at the bottom — v1 plus a couple of
 * recent ones — because the bug this replaces was "the list loses its tail":
 * the audit-log reconstruction could not see v1 at all. A fixture where v1 is
 * present and selectable is the one that would have caught it.
 */
export const MOCK_CONTENT_VERSIONS: ContentVersionSummary[] = [
  { catalog: "clubs", version: 9999, publishedBy: "mock@example.invalid", publishedAt: "2026-08-25T00:00:00Z", note: "MOCK FIXTURE — latest", rowCount: 2 },
  { catalog: "clubs", version: 9998, publishedBy: "mock@example.invalid", publishedAt: "2026-08-24T00:00:00Z", note: "MOCK FIXTURE", rowCount: 2 },
  { catalog: "clubs", version: 1, publishedBy: null, publishedAt: "2026-08-01T00:00:00Z", note: "MOCK FIXTURE — the seeded baseline, and the point of §2", rowCount: 2 },
  { catalog: "characters", version: 9999, publishedBy: "mock@example.invalid", publishedAt: "2026-08-25T00:00:00Z", note: "MOCK FIXTURE", rowCount: 1 },
  { catalog: "characters", version: 1, publishedBy: null, publishedAt: "2026-08-01T00:00:00Z", note: "MOCK FIXTURE — seeded baseline", rowCount: 1 },
  { catalog: "shop_catalog", version: 9999, publishedBy: "mock@example.invalid", publishedAt: "2026-08-25T00:00:00Z", note: "MOCK FIXTURE", rowCount: 1 },
  { catalog: "shop_catalog", version: 1, publishedBy: null, publishedAt: "2026-08-01T00:00:00Z", note: "MOCK FIXTURE — seeded baseline", rowCount: 1 },
  { catalog: "items", version: 9999, publishedBy: "mock@example.invalid", publishedAt: "2026-08-25T00:00:00Z", note: "MOCK FIXTURE", rowCount: 2 },
  { catalog: "items", version: 1, publishedBy: null, publishedAt: "2026-08-01T00:00:00Z", note: "MOCK FIXTURE — seeded baseline", rowCount: 2 },
  { catalog: "bags", version: 9999, publishedBy: "mock@example.invalid", publishedAt: "2026-08-25T00:00:00Z", note: "MOCK FIXTURE", rowCount: 2 },
  { catalog: "bags", version: 1, publishedBy: null, publishedAt: "2026-08-01T00:00:00Z", note: "MOCK FIXTURE — seeded baseline", rowCount: 2 },
  { catalog: "balls", version: 9999, publishedBy: "mock@example.invalid", publishedAt: "2026-08-25T00:00:00Z", note: "MOCK FIXTURE", rowCount: 2 },
  { catalog: "balls", version: 1, publishedBy: null, publishedAt: "2026-08-01T00:00:00Z", note: "MOCK FIXTURE — seeded baseline", rowCount: 2 },
  { catalog: "texts", version: 9999, publishedBy: "mock@example.invalid", publishedAt: "2026-08-25T00:00:00Z", note: "MOCK FIXTURE", rowCount: 2 },
  { catalog: "texts", version: 1, publishedBy: null, publishedAt: "2026-08-01T00:00:00Z", note: "MOCK FIXTURE — seeded baseline", rowCount: 2 },
  { catalog: "modes", version: 9999, publishedBy: "mock@example.invalid", publishedAt: "2026-08-28T00:00:00Z", note: "MOCK FIXTURE", rowCount: 2 },
  { catalog: "modes", version: 1, publishedBy: null, publishedAt: "2026-08-01T00:00:00Z", note: "MOCK FIXTURE — seeded baseline", rowCount: 2 },
];
