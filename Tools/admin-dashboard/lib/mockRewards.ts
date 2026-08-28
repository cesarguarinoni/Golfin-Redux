import type { RewardActionRow } from "./types";

/**
 * Mock-mode fixtures for the Rewards panel (game_modes_admin §3).
 *
 * ⚠️ DELIBERATELY, VISIBLY FAKE — every number is 9999, like every other fixture
 * in this dashboard (ADMIN_DASHBOARD_OPS.md §3.5 records mock fixtures being
 * read as production facts). The ACTION NAMES are real, because they are not
 * data: they are the four strings shipped clients send, and a fixture with made
 * up names would not exercise the one thing this panel refuses to do — create
 * or delete rows.
 *
 * `hole_complete` carries a NULL `pts` on purpose: the "client amount" badge and
 * the explanatory hint are the SPEC's named requirement ("pts is blank looks
 * like a bug otherwise"), so mock mode has to be able to render them.
 */
export const MOCK_REWARD_ACTIONS: RewardActionRow[] = [
  { action: "hole_complete", pts: null, maxPerEvent: 9999, dailyCap: 9999, oncePerUser: false },
  { action: "hole_replay", pts: null, maxPerEvent: 9999, dailyCap: 9999, oncePerUser: false },
  { action: "versus_win", pts: 9999, maxPerEvent: 9999, dailyCap: 9999, oncePerUser: false },
  { action: "tournament_prize", pts: null, maxPerEvent: 9999, dailyCap: null, oncePerUser: false },
];
